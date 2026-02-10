using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace iCUERedGreen.Tray;

/// <summary>
/// Reads and writes credentials in Windows Credential Manager.
/// </summary>
internal sealed class CredentialStore
{
    private const int MaxCredentialBlobSize = 512;
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;
    private readonly Logger _logger;
    private readonly string _targetName;
    private const string DefaultTargetName = "iCUERedGreen/FritzPassword";

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialStore"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    public CredentialStore(Logger logger, string? targetName = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _targetName = string.IsNullOrWhiteSpace(targetName) ? DefaultTargetName : targetName;
    }

    /// <summary>
    /// Attempts to read credentials.
    /// </summary>
    /// <param name="entry">The returned credential entry.</param>
    /// <returns>True when credentials were found; otherwise false.</returns>
    public bool TryRead(out CredentialEntry? entry)
    {
        entry = null;
        if (!CredRead(_targetName, CredTypeGeneric, 0, out IntPtr credentialPtr))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 1168)
            {
                return false;
            }

            _logger.Error("CredRead failed with error: {0}.", error);
            return false;
        }

        try
        {
            Credential credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            string userName = credential.UserName ?? string.Empty;
            string password = ReadCredentialSecret(credential);
            entry = new CredentialEntry(userName, password);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read credential data.");
            return false;
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    /// <summary>
    /// Writes credentials.
    /// </summary>
    /// <param name="entry">The credential entry to store.</param>
    public void Write(CredentialEntry entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        string userName = entry.UserName ?? string.Empty;
        string password = entry.Password ?? string.Empty;
        byte[] passwordBytes = Encoding.Unicode.GetBytes(password);

        if (passwordBytes.Length > MaxCredentialBlobSize)
        {
            throw new InvalidOperationException("Password is too long for Credential Manager storage.");
        }

        IntPtr passwordPtr = IntPtr.Zero;
        try
        {
            passwordPtr = Marshal.AllocHGlobal(passwordBytes.Length);
            Marshal.Copy(passwordBytes, 0, passwordPtr, passwordBytes.Length);

            Credential credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = _targetName,
                CredentialBlobSize = (uint)passwordBytes.Length,
                CredentialBlob = passwordPtr,
                Persist = CredPersistLocalMachine,
                UserName = userName
            };

            if (!CredWrite(ref credential, 0))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"CredWrite failed with error: {error}.");
            }
        }
        finally
        {
            if (passwordPtr != IntPtr.Zero)
            {
                // Clear unmanaged memory to reduce residual secrets.
                ZeroMemory(passwordPtr, (UIntPtr)passwordBytes.Length);
                Marshal.FreeHGlobal(passwordPtr);
            }
        }
    }

    /// <summary>
    /// Deletes stored credentials.
    /// </summary>
    public void Delete()
    {
        if (!CredDelete(_targetName, CredTypeGeneric, 0))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == 1168)
            {
                return;
            }

            throw new InvalidOperationException($"CredDelete failed with error: {error}.");
        }
    }

    /// <summary>
    /// Reads the secret from a credential blob.
    /// </summary>
    /// <param name="credential">The credential data.</param>
    /// <returns>The secret string.</returns>
    private static string ReadCredentialSecret(Credential credential)
    {
        if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
        {
            return string.Empty;
        }

        int charCount = (int)credential.CredentialBlobSize / 2;
        return Marshal.PtrToStringUni(credential.CredentialBlob, charCount) ?? string.Empty;
    }

    /// <summary>
    /// Represents the native credential structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    /// <summary>
    /// Reads a credential from the Windows credential store.
    /// </summary>
    /// <param name="target">The credential target.</param>
    /// <param name="type">The credential type.</param>
    /// <param name="reservedFlag">Reserved flags.</param>
    /// <param name="credentialPtr">The returned credential pointer.</param>
    /// <returns>True when successful; otherwise false.</returns>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    /// <summary>
    /// Writes a credential to the Windows credential store.
    /// </summary>
    /// <param name="userCredential">The credential to write.</param>
    /// <param name="flags">Reserved flags.</param>
    /// <returns>True when successful; otherwise false.</returns>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref Credential userCredential, uint flags);

    /// <summary>
    /// Deletes a credential from the Windows credential store.
    /// </summary>
    /// <param name="target">The credential target.</param>
    /// <param name="type">The credential type.</param>
    /// <param name="flags">Reserved flags.</param>
    /// <returns>True when successful; otherwise false.</returns>
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    /// <summary>
    /// Frees a credential pointer allocated by the system.
    /// </summary>
    /// <param name="credentialPtr">The credential pointer.</param>
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);

    /// <summary>
    /// Zeros memory in unmanaged space.
    /// </summary>
    /// <param name="dest">The target pointer.</param>
    /// <param name="size">The size to zero.</param>
    [DllImport("kernel32.dll", SetLastError = false)]
    private static extern void ZeroMemory(IntPtr dest, UIntPtr size);
}

/// <summary>
/// Holds a username and password pair.
/// </summary>
internal sealed class CredentialEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialEntry"/> class.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    public CredentialEntry(string userName, string password)
    {
        UserName = userName;
        Password = password;
    }

    /// <summary>
    /// Gets the user name.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// Gets the password.
    /// </summary>
    public string Password { get; }
}
