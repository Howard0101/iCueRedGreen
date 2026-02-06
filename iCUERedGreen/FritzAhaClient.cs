using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using NLog;

namespace iCUERedGreen;

/// <summary>
/// Provides access to the FRITZ!Box AHA HTTP interface.
/// </summary>
internal sealed class FritzAhaClient : IDisposable
{
    private const string InvalidSid = "0000000000000000";
    private readonly HttpClient _httpClient;
    private readonly string _host;
    private readonly string _username;
    private readonly string _password;
    private readonly string _ain;
    private readonly Logger _logger;
    private string? _sid;

    /// <summary>
    /// Initializes a new instance of the <see cref="FritzAhaClient"/> class.
    /// </summary>
    /// <param name="options">Resolved options containing FRITZ!Box settings.</param>
    /// <param name="logger">The logger to use.</param>
    public FritzAhaClient(Options options, Logger logger)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _host = options.FritzHost ?? throw new ArgumentNullException(nameof(options.FritzHost));
        _username = options.FritzUsername ?? throw new ArgumentNullException(nameof(options.FritzUsername));
        _password = options.FritzPassword ?? throw new ArgumentNullException(nameof(options.FritzPassword));
        _ain = options.FritzAin ?? throw new ArgumentNullException(nameof(options.FritzAin));

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3),
            BaseAddress = BuildBaseUri(_host)
        };
    }

    /// <summary>
    /// Retrieves the current switch state.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True when the switch is on; otherwise false.</returns>
    public async Task<bool> GetSwitchStateAsync(CancellationToken cancellationToken)
    {
        return await GetSwitchStateCoreAsync(allowRetry: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Toggles the switch state.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the toggle request finishes.</returns>
    public async Task ToggleSwitchAsync(CancellationToken cancellationToken)
    {
        string sid = await EnsureSidAsync(cancellationToken).ConfigureAwait(false);
        await SendAhaCommandAsync("setswitchtoggle", sid, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases managed resources.
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// Retrieves the switch state and optionally retries after re-authentication.
    /// </summary>
    /// <param name="allowRetry">Whether a single retry is allowed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>True when the switch is on; otherwise false.</returns>
    private async Task<bool> GetSwitchStateCoreAsync(bool allowRetry, CancellationToken cancellationToken)
    {
        string sid = await EnsureSidAsync(cancellationToken).ConfigureAwait(false);
        string response = await SendAhaCommandAsync("getswitchstate", sid, cancellationToken).ConfigureAwait(false);

        if (response == "0")
        {
            return false;
        }

        if (response == "1")
        {
            return true;
        }

        string safeResponse = string.IsNullOrWhiteSpace(response) ? "<empty>" : response;
        string reason = DescribeInvalidSwitchResponse(response);

        if (allowRetry)
        {
            _logger.Debug("DEBUG: Switch response invalid: {0} (raw: {1}). Retrying after re-authentication.", reason, safeResponse);
            _sid = null;
            return await GetSwitchStateCoreAsync(allowRetry: false, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"{reason} (response: {safeResponse}).");
    }

    private static string DescribeInvalidSwitchResponse(string response)
    {
        if (string.Equals(response, "inval", StringComparison.OrdinalIgnoreCase))
        {
            return "DECT200 unreachable or AIN invalid";
        }

        if (string.IsNullOrWhiteSpace(response))
        {
            return "Empty switch response";
        }

        return "Unexpected switch response";
    }

    /// <summary>
    /// Ensures a valid session ID.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The session ID.</returns>
    private async Task<string> EnsureSidAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_sid))
        {
            return _sid!;
        }

        LoginState initial = await GetLoginStateAsync(cancellationToken).ConfigureAwait(false);
        string response = ComputeResponse(initial.Challenge, _password);

        var query = new Dictionary<string, string>
        {
            ["username"] = _username,
            ["response"] = response
        };

        Uri uri = BuildUri("login_sid.lua", query);
        string xml = await GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        LoginState login = ParseLoginState(xml);

        if (login.Sid == InvalidSid)
        {
            throw new InvalidOperationException("Login failed; check FRITZ credentials.");
        }

        _sid = login.Sid;
        return _sid;
    }

    /// <summary>
    /// Requests the current login state (SID and challenge).
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The login state.</returns>
    private async Task<LoginState> GetLoginStateAsync(CancellationToken cancellationToken)
    {
        Uri uri = BuildUri("login_sid.lua", null);
        string xml = await GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        return ParseLoginState(xml);
    }

    /// <summary>
    /// Sends an AHA command and returns the raw response body.
    /// </summary>
    /// <param name="command">The AHA command.</param>
    /// <param name="sid">The session ID.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The response body.</returns>
    private async Task<string> SendAhaCommandAsync(string command, string sid, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string>
        {
            ["switchcmd"] = command,
            ["ain"] = _ain,
            ["sid"] = sid
        };

        Uri uri = BuildUri("webservices/homeautoswitch.lua", query);
        string response = await GetStringAsync(uri, cancellationToken).ConfigureAwait(false);
        return response.Trim();
    }

    /// <summary>
    /// Performs an HTTP GET and returns the response body.
    /// </summary>
    /// <param name="uri">The request URI.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The response body.</returns>
    private async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"FRITZ request failed with status {(int)response.StatusCode}.");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a URI for the FRITZ!Box host.
    /// </summary>
    /// <param name="path">The relative path.</param>
    /// <param name="query">Optional query parameters.</param>
    /// <returns>The resulting URI.</returns>
    private Uri BuildUri(string path, IDictionary<string, string>? query)
    {
        var builder = new UriBuilder(_httpClient.BaseAddress!)
        {
            Path = path,
            Query = BuildQueryString(query)
        };

        return builder.Uri;
    }

    /// <summary>
    /// Builds an URL-encoded query string.
    /// </summary>
    /// <param name="query">Optional query parameters.</param>
    /// <returns>The query string without a leading question mark.</returns>
    private static string BuildQueryString(IDictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        bool first = true;

        foreach (KeyValuePair<string, string> pair in query)
        {
            if (!first)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(pair.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(pair.Value));
            first = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses the login state from the XML response.
    /// </summary>
    /// <param name="xml">The raw XML response.</param>
    /// <returns>The parsed login state.</returns>
    private static LoginState ParseLoginState(string xml)
    {
        XDocument doc = XDocument.Parse(xml);
        string sid = GetRequiredXmlValue(doc, "SID");
        string challenge = GetRequiredXmlValue(doc, "Challenge");
        return new LoginState(sid, challenge);
    }

    /// <summary>
    /// Retrieves a required XML element value.
    /// </summary>
    /// <param name="doc">The parsed XML document.</param>
    /// <param name="elementName">The element name.</param>
    /// <returns>The element value.</returns>
    private static string GetRequiredXmlValue(XDocument doc, string elementName)
    {
        string? value = doc.Root?.Element(elementName)?.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing {elementName} in FRITZ response.");
        }

        return value;
    }

    /// <summary>
    /// Computes the challenge response string.
    /// </summary>
    /// <param name="challenge">The challenge value.</param>
    /// <param name="password">The FRITZ!Box password.</param>
    /// <returns>The response string.</returns>
    private static string ComputeResponse(string challenge, string password)
    {
        string data = $"{challenge}-{password}";
        byte[] bytes = Encoding.Unicode.GetBytes(data);
        byte[] hash = MD5.HashData(bytes);
        string md5 = BytesToHex(hash);
        return $"{challenge}-{md5}";
    }

    /// <summary>
    /// Converts a byte array into a lower-case hex string.
    /// </summary>
    /// <param name="bytes">The byte array.</param>
    /// <returns>The hex string.</returns>
    private static string BytesToHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Builds the base URI for the FRITZ!Box host.
    /// </summary>
    /// <param name="host">The host value.</param>
    /// <returns>The base URI.</returns>
    private static Uri BuildBaseUri(string host)
    {
        string trimmed = host.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsed))
        {
            return EnsureTrailingSlash(parsed);
        }

        var builder = new UriBuilder
        {
            Scheme = Uri.UriSchemeHttp,
            Host = trimmed,
            Path = "/"
        };

        return builder.Uri;
    }

    /// <summary>
    /// Ensures the base URI ends with a trailing slash.
    /// </summary>
    /// <param name="uri">The base URI.</param>
    /// <returns>The normalized URI.</returns>
    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Path = uri.AbsolutePath + "/"
        };

        return builder.Uri;
    }

    /// <summary>
    /// Holds FRITZ!Box login state information.
    /// </summary>
    /// <param name="Sid">The session ID.</param>
    /// <param name="Challenge">The challenge value.</param>
    private sealed record LoginState(string Sid, string Challenge);
}
