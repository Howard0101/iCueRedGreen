using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NLog;

namespace iCUERedGreen;

/// <summary>
/// Defines the Corsair error codes.
/// </summary>
internal enum CorsairError : int
{
    /// <summary>
    /// Indicates success.
    /// </summary>
    CE_Success = 0,
    /// <summary>
    /// Indicates iCUE is not connected or third-party control is disabled.
    /// </summary>
    CE_NotConnected = 1,
    /// <summary>
    /// Indicates no control permissions.
    /// </summary>
    CE_NoControl = 2,
    /// <summary>
    /// Indicates incompatible protocol versions.
    /// </summary>
    CE_IncompatibleProtocol = 3,
    /// <summary>
    /// Indicates invalid arguments.
    /// </summary>
    CE_InvalidArguments = 4,
    /// <summary>
    /// Indicates an invalid operation for the current state.
    /// </summary>
    CE_InvalidOperation = 5,
    /// <summary>
    /// Indicates the specified device was not found.
    /// </summary>
    CE_DeviceNotFound = 6,
    /// <summary>
    /// Indicates the operation is not allowed by iCUE settings.
    /// </summary>
    CE_NotAllowed = 7
}

/// <summary>
/// Defines the Corsair session states.
/// </summary>
internal enum CorsairSessionState : int
{
    /// <summary>
    /// Invalid session state.
    /// </summary>
    CSS_Invalid = 0,
    /// <summary>
    /// Session is closed.
    /// </summary>
    CSS_Closed = 1,
    /// <summary>
    /// Session is connecting.
    /// </summary>
    CSS_Connecting = 2,
    /// <summary>
    /// Session timed out.
    /// </summary>
    CSS_Timeout = 3,
    /// <summary>
    /// Connection was refused.
    /// </summary>
    CSS_ConnectionRefused = 4,
    /// <summary>
    /// Connection was lost.
    /// </summary>
    CSS_ConnectionLost = 5,
    /// <summary>
    /// Session is connected.
    /// </summary>
    CSS_Connected = 6
}

/// <summary>
/// Defines Corsair device types.
/// </summary>
[Flags]
internal enum CorsairDeviceType : int
{
    /// <summary>
    /// Unknown device.
    /// </summary>
    CDT_Unknown = 0x0000,
    /// <summary>
    /// Keyboard device.
    /// </summary>
    CDT_Keyboard = 0x0001,
    /// <summary>
    /// Mouse device.
    /// </summary>
    CDT_Mouse = 0x0002,
    /// <summary>
    /// Mousemat device.
    /// </summary>
    CDT_Mousemat = 0x0004,
    /// <summary>
    /// Headset device.
    /// </summary>
    CDT_Headset = 0x0008,
    /// <summary>
    /// Headset stand device.
    /// </summary>
    CDT_HeadsetStand = 0x0010,
    /// <summary>
    /// Fan LED controller device.
    /// </summary>
    CDT_FanLedController = 0x0020,
    /// <summary>
    /// LED controller device.
    /// </summary>
    CDT_LedController = 0x0040,
    /// <summary>
    /// Memory module device.
    /// </summary>
    CDT_MemoryModule = 0x0080,
    /// <summary>
    /// Cooler device.
    /// </summary>
    CDT_Cooler = 0x0100,
    /// <summary>
    /// Motherboard device.
    /// </summary>
    CDT_Motherboard = 0x0200,
    /// <summary>
    /// Graphics card device.
    /// </summary>
    CDT_GraphicsCard = 0x0400,
    /// <summary>
    /// Touchbar device.
    /// </summary>
    CDT_Touchbar = 0x0800,
    /// <summary>
    /// Game controller device.
    /// </summary>
    CDT_GameController = 0x1000,
    /// <summary>
    /// All devices.
    /// </summary>
    CDT_All = unchecked((int)0xFFFFFFFF)
}

/// <summary>
/// Defines LED groups for building LED LUIDs.
/// </summary>
internal enum CorsairLedGroup : int
{
    /// <summary>
    /// Keyboard LED group.
    /// </summary>
    CLG_Keyboard = 0
}

/// <summary>
/// Defines keyboard LED identifiers.
/// </summary>
internal enum CorsairLedIdKeyboard : int
{
    /// <summary>
    /// Scroll Lock LED.
    /// </summary>
    CLK_ScrollLock = 85,
    /// <summary>
    /// Volume Mute LED.
    /// Verified from iCUESDKLedIdEnum.h: CLK_Mute = 100.
    /// </summary>
    CLK_Mute = 100
}

/// <summary>
/// Defines Corsair access levels.
/// </summary>
internal enum CorsairAccessLevel : int
{
    /// <summary>
    /// Shared access.
    /// </summary>
    CAL_Shared = 0,
    /// <summary>
    /// Exclusive lighting control.
    /// </summary>
    CAL_ExclusiveLightingControl = 1,
    /// <summary>
    /// Exclusive key events listening.
    /// </summary>
    CAL_ExclusiveKeyEventsListening = 2,
    /// <summary>
    /// Exclusive lighting and key events.
    /// </summary>
    CAL_ExclusiveLightingControlAndKeyEventsListening = 3
}

/// <summary>
/// Defines a Corsair version.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CorsairVersion
{
    /// <summary>
    /// Gets or sets the major version.
    /// </summary>
    public int major;
    /// <summary>
    /// Gets or sets the minor version.
    /// </summary>
    public int minor;
    /// <summary>
    /// Gets or sets the patch version.
    /// </summary>
    public int patch;
}

/// <summary>
/// Defines Corsair session details.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CorsairSessionDetails
{
    /// <summary>
    /// Gets or sets the client version.
    /// </summary>
    public CorsairVersion clientVersion;
    /// <summary>
    /// Gets or sets the server version.
    /// </summary>
    public CorsairVersion serverVersion;
    /// <summary>
    /// Gets or sets the host (iCUE) version.
    /// </summary>
    public CorsairVersion serverHostVersion;
}

/// <summary>
/// Defines a session state change event payload.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CorsairSessionStateChanged
{
    /// <summary>
    /// Gets or sets the session state.
    /// </summary>
    public CorsairSessionState state;
    /// <summary>
    /// Gets or sets the session details.
    /// </summary>
    public CorsairSessionDetails details;
}

/// <summary>
/// Defines device search filters.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CorsairDeviceFilter
{
    /// <summary>
    /// Gets or sets the device type mask.
    /// </summary>
    public int deviceTypeMask;
}

/// <summary>
/// Defines a Corsair device info structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
internal struct CorsairDeviceInfo
{
    /// <summary>
    /// Gets or sets the device type.
    /// </summary>
    public CorsairDeviceType type;
    /// <summary>
    /// Gets or sets the device identifier.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CorsairConstants.StringSizeM)]
    public string id;
    /// <summary>
    /// Gets or sets the device serial number.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CorsairConstants.StringSizeM)]
    public string serial;
    /// <summary>
    /// Gets or sets the device model.
    /// </summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CorsairConstants.StringSizeM)]
    public string model;
    /// <summary>
    /// Gets or sets the LED count.
    /// </summary>
    public int ledCount;
    /// <summary>
    /// Gets or sets the channel count.
    /// </summary>
    public int channelCount;
}

/// <summary>
/// Defines a Corsair LED position.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CorsairLedPosition
{
    /// <summary>
    /// Gets or sets the LED LUID.
    /// </summary>
    public uint id;
    /// <summary>
    /// Gets or sets the X coordinate.
    /// </summary>
    public double cx;
    /// <summary>
    /// Gets or sets the Y coordinate.
    /// </summary>
    public double cy;
}

/// <summary>
/// Defines a Corsair LED color.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct CorsairLedColor
{
    /// <summary>
    /// Gets or sets the LED LUID.
    /// </summary>
    public uint id;
    /// <summary>
    /// Gets or sets the red channel.
    /// </summary>
    public byte r;
    /// <summary>
    /// Gets or sets the green channel.
    /// </summary>
    public byte g;
    /// <summary>
    /// Gets or sets the blue channel.
    /// </summary>
    public byte b;
    /// <summary>
    /// Gets or sets the alpha channel.
    /// </summary>
    public byte a;
}

/// <summary>
/// Defines a session state changed callback.
/// </summary>
/// <param name="context">User-defined context pointer.</param>
/// <param name="eventData">Session state data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void CorsairSessionStateChangedHandler(IntPtr context, ref CorsairSessionStateChanged eventData);

/// <summary>
/// Provides native P/Invoke signatures for the iCUE SDK.
/// </summary>
internal static class CorsairNative
{
    /// <summary>
    /// Connects to the iCUE SDK.
    /// </summary>
    /// <param name="onStateChanged">State change callback.</param>
    /// <param name="context">User context pointer.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl)]
    internal static extern CorsairError CorsairConnect(CorsairSessionStateChangedHandler onStateChanged, IntPtr context);

    /// <summary>
    /// Retrieves session details.
    /// </summary>
    /// <param name="details">Output details.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl)]
    internal static extern CorsairError CorsairGetSessionDetails(out CorsairSessionDetails details);


    /// <summary>
    /// Disconnects from the iCUE SDK.
    /// </summary>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl)]
    internal static extern CorsairError CorsairDisconnect();

    /// <summary>
    /// Retrieves the list of devices.
    /// </summary>
    /// <param name="filter">Device filter.</param>
    /// <param name="sizeMax">Maximum size.</param>
    /// <param name="devices">Device buffer.</param>
    /// <param name="size">Number of devices.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl)]
    internal static extern CorsairError CorsairGetDevices(
        ref CorsairDeviceFilter filter,
        int sizeMax,
        [Out] CorsairDeviceInfo[] devices,
        ref int size);

    /// <summary>
    /// Retrieves LED positions for a device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="sizeMax">Maximum size.</param>
    /// <param name="ledPositions">LED positions buffer.</param>
    /// <param name="size">Number of LEDs.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern CorsairError CorsairGetLedPositions(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        int sizeMax,
        [Out] CorsairLedPosition[] ledPositions,
        ref int size);

    /// <summary>
    /// Sets LED colors for a device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="size">Number of LEDs.</param>
    /// <param name="ledColors">LED colors.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern CorsairError CorsairSetLedColors(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        int size,
        [In] CorsairLedColor[] ledColors);

    /// <summary>
    /// Sets the layer priority for this client.
    /// </summary>
    /// <param name="priority">Priority value (0-255).</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl)]
    internal static extern CorsairError CorsairSetLayerPriority(uint priority);

    /// <summary>
    /// Requests access control for a device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="accessLevel">The desired access level.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern CorsairError CorsairRequestControl(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId,
        CorsairAccessLevel accessLevel);

    /// <summary>
    /// Releases access control for a device.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <returns>The error code.</returns>
    [DllImport("CUESDK", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern CorsairError CorsairReleaseControl(
        [MarshalAs(UnmanagedType.LPStr)] string deviceId);
}

/// <summary>
/// Defines constants from the iCUE SDK.
/// </summary>
internal static class CorsairConstants
{
    /// <summary>
    /// Maximum length of medium strings.
    /// </summary>
    public const int StringSizeM = 128;
    /// <summary>
    /// Maximum number of devices.
    /// </summary>
    public const int DeviceCountMax = 64;
    /// <summary>
    /// Maximum LED count per device.
    /// </summary>
    public const int DeviceLedCountMax = 512;
}

/// <summary>
/// Builds LED LUID values.
/// </summary>
internal static class CorsairLedLuidHelper
{
    /// <summary>
    /// Builds an LED LUID for a keyboard LED.
    /// </summary>
    /// <param name="ledId">The keyboard LED identifier.</param>
    /// <returns>The LED LUID.</returns>
    public static uint FromKeyboard(CorsairLedIdKeyboard ledId)
    {
        return ((uint)CorsairLedGroup.CLG_Keyboard << 16) | (uint)ledId;
    }
}

/// <summary>
/// Resolves and loads the CUE SDK DLL.
/// </summary>
internal static class CueSdkLoader
{
    private static bool _installed;
    private static string? _explicitPath;

    /// <summary>
    /// Installs the DLL resolver for the CUE SDK.
    /// </summary>
    /// <param name="explicitPath">Optional explicit DLL path.</param>
    public static void Install(string? explicitPath)
    {
        _explicitPath = explicitPath;

        if (_installed)
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(typeof(CorsairNative).Assembly, (name, assembly, path) =>
        {
            if (!string.Equals(name, "CUESDK", StringComparison.OrdinalIgnoreCase))
            {
                return IntPtr.Zero;
            }

            if (!string.IsNullOrWhiteSpace(_explicitPath) && File.Exists(_explicitPath))
            {
                return NativeLibrary.Load(_explicitPath);
            }

            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "iCUESDK.x64_2019.dll"),
                Path.Combine(baseDir, "CUESDK.x64_2017.dll"),
                Path.Combine(baseDir, "CUESDK.x64_2015.dll"),
                Path.Combine(baseDir, "CUESDK.x64_2013.dll")
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return NativeLibrary.Load(candidate);
                }
            }

            throw new DllNotFoundException("CUE SDK DLL not found. Place iCUESDK.x64_2019.dll next to the exe or use --cuesdk-path.");
        });

        _installed = true;
    }
}

/// <summary>
/// Controls tracked keyboard LEDs via the iCUE SDK.
/// </summary>
internal sealed class CueKeyController
{
    private const uint LayerPriority = 200;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static CorsairSessionStateChangedHandler? _sessionHandler;
    private readonly string _deviceId;
    private readonly Dictionary<uint, CorsairLedColor> _ledColors = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CueKeyController"/> class.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="hasMuteKey">True when the keyboard exposes the mute LED.</param>
    private CueKeyController(string deviceId, bool hasMuteKey)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        HasMuteKey = hasMuteKey;
    }

    /// <summary>
    /// Gets a value indicating whether the keyboard exposes the Volume Mute LED.
    /// </summary>
    public bool HasMuteKey { get; }

    /// <summary>
    /// Initializes the iCUE SDK and returns a controller for the keyboard LEDs used by the app.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The key controller instance.</returns>
    public static CueKeyController Initialize(Logger logger)
    {
        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        CorsairSessionState state = ConnectToIcue(logger);
        if (state != CorsairSessionState.CSS_Connected)
        {
            throw new InvalidOperationException($"iCUE session state is {state}.");
        }

        KeyboardDeviceSelection selection = FindKeyboardDevice(logger);
        CorsairError controlResult = CorsairNative.CorsairRequestControl(
            selection.DeviceId,
            CorsairAccessLevel.CAL_Shared);
        if (controlResult != CorsairError.CE_Success)
        {
            throw new InvalidOperationException($"CorsairRequestControl failed: {controlResult}.");
        }

        // Ensure our single LED override sits above default iCUE lighting.
        CorsairError priorityResult = CorsairNative.CorsairSetLayerPriority(LayerPriority);
        if (priorityResult != CorsairError.CE_Success)
        {
            throw new InvalidOperationException($"CorsairSetLayerPriority failed: {priorityResult}.");
        }

        return new CueKeyController(selection.DeviceId, selection.HasMuteKey);
    }

    /// <summary>
    /// Applies the Scroll Lock LED state.
    /// </summary>
    /// <param name="state">The state to represent.</param>
    public void SetScrollLockState(SwitchState state)
    {
        switch (state)
        {
            case SwitchState.On:
                SetKeyboardLedColor(CorsairLedIdKeyboard.CLK_ScrollLock, 255, 0, 0, 255);
                break;
            case SwitchState.Off:
                SetKeyboardLedColor(CorsairLedIdKeyboard.CLK_ScrollLock, 0, 255, 0, 255);
                break;
            default:
                ClearKeyboardLed(CorsairLedIdKeyboard.CLK_ScrollLock);
                break;
        }
    }

    /// <summary>
    /// Applies the Volume Mute LED state.
    /// </summary>
    /// <param name="state">The state to represent.</param>
    public void SetMuteState(SoundMuteState state)
    {
        if (!HasMuteKey)
        {
            return;
        }

        switch (state)
        {
            case SoundMuteState.Muted:
                SetKeyboardLedColor(CorsairLedIdKeyboard.CLK_Mute, 255, 0, 0, 255);
                break;
            case SoundMuteState.Unmuted:
                SetKeyboardLedColor(CorsairLedIdKeyboard.CLK_Mute, 0, 255, 0, 255);
                break;
            default:
                ClearKeyboardLed(CorsairLedIdKeyboard.CLK_Mute);
                break;
        }
    }

    /// <summary>
    /// Sets a keyboard LED to the specified RGBA color.
    /// </summary>
    /// <param name="ledId">The keyboard LED identifier.</param>
    /// <param name="r">Red channel.</param>
    /// <param name="g">Green channel.</param>
    /// <param name="b">Blue channel.</param>
    /// <param name="a">Alpha channel.</param>
    public void SetKeyboardLedColor(CorsairLedIdKeyboard ledId, byte r, byte g, byte b, byte a)
    {
        uint ledLuid = CorsairLedLuidHelper.FromKeyboard(ledId);
        _ledColors[ledLuid] = new CorsairLedColor
        {
            id = ledLuid,
            r = r,
            g = g,
            b = b,
            a = a
        };

        FlushLedColors();
    }

    /// <summary>
    /// Clears a tracked keyboard LED override by setting it fully transparent on this layer.
    /// </summary>
    /// <param name="ledId">The keyboard LED identifier.</param>
    public void ClearKeyboardLed(CorsairLedIdKeyboard ledId)
    {
        SetKeyboardLedColor(ledId, 0, 0, 0, 0);
    }

    /// <summary>
    /// Flushes all tracked keyboard LED colors to the iCUE SDK.
    /// </summary>
    private void FlushLedColors()
    {
        CorsairLedColor[] colors = _ledColors.Values.ToArray();
        CorsairError result = CorsairNative.CorsairSetLedColors(_deviceId, colors.Length, colors);
        if (result != CorsairError.CE_Success)
        {
            throw new InvalidOperationException($"CorsairSetLedColors failed: {result}.");
        }
    }

    /// <summary>
    /// Selects a keyboard device and reports supported LED capabilities.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The device selection.</returns>
    private static KeyboardDeviceSelection FindKeyboardDevice(Logger logger)
    {
        CorsairDeviceFilter filter = new CorsairDeviceFilter
        {
            deviceTypeMask = (int)CorsairDeviceType.CDT_Keyboard
        };

        CorsairDeviceInfo[] devices = new CorsairDeviceInfo[CorsairConstants.DeviceCountMax];
        int size = devices.Length;
        CorsairError result = CorsairNative.CorsairGetDevices(ref filter, devices.Length, devices, ref size);
        if (result != CorsairError.CE_Success)
        {
            throw new InvalidOperationException($"CorsairGetDevices failed: {result}.");
        }

        if (size <= 0)
        {
            throw new InvalidOperationException("No keyboard devices detected by iCUE.");
        }

        uint scrollLockLuid = CorsairLedLuidHelper.FromKeyboard(CorsairLedIdKeyboard.CLK_ScrollLock);
        uint muteLuid = CorsairLedLuidHelper.FromKeyboard(CorsairLedIdKeyboard.CLK_Mute);
        for (int i = 0; i < size; i++)
        {
            string deviceId = devices[i].id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            KeyboardLedCapabilities capabilities = GetKeyboardLedCapabilities(deviceId, scrollLockLuid, muteLuid, logger);
            if (capabilities.HasScrollLock)
            {
                logger.Info("Using keyboard device: {0}", deviceId);
                return new KeyboardDeviceSelection(deviceId, capabilities.HasMuteKey);
            }
        }

        throw new InvalidOperationException("Scroll Lock LED not found on detected keyboards.");
    }

    /// <summary>
    /// Checks whether a device exposes a specific LED LUID.
    /// </summary>
    /// <param name="deviceId">The device identifier.</param>
    /// <param name="ledLuid">The LED LUID.</param>
    /// <param name="logger">The logger to use.</param>
    /// <returns>True if the LED exists; otherwise false.</returns>
    private static KeyboardLedCapabilities GetKeyboardLedCapabilities(string deviceId, uint scrollLockLuid, uint muteLuid, Logger logger)
    {
        CorsairLedPosition[] positions = new CorsairLedPosition[CorsairConstants.DeviceLedCountMax];
        int size = positions.Length;
        CorsairError result = CorsairNative.CorsairGetLedPositions(deviceId, positions.Length, positions, ref size);
        if (result != CorsairError.CE_Success)
        {
            logger.Warn("CorsairGetLedPositions failed for device {0}: {1}", deviceId, result);
            return default;
        }

        bool hasScrollLock = false;
        bool hasMuteKey = false;
        for (int i = 0; i < size; i++)
        {
            if (positions[i].id == scrollLockLuid)
            {
                hasScrollLock = true;
            }

            if (positions[i].id == muteLuid)
            {
                hasMuteKey = true;
            }
        }

        return new KeyboardLedCapabilities(hasScrollLock, hasMuteKey);
    }

    /// <summary>
    /// Connects to iCUE and waits for a connected session.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The resulting session state.</returns>
    private static CorsairSessionState ConnectToIcue(Logger logger)
    {
        CorsairSessionState currentState = CorsairSessionState.CSS_Invalid;
        TaskCompletionSource<CorsairSessionState> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        CorsairSessionStateChangedHandler handler = (IntPtr context, ref CorsairSessionStateChanged eventData) =>
        {
            currentState = eventData.state;
            if (currentState != CorsairSessionState.CSS_Connecting)
            {
                tcs.TrySetResult(currentState);
            }
        };

        _sessionHandler = handler;

        CorsairError connectResult = CorsairNative.CorsairConnect(_sessionHandler, IntPtr.Zero);
        if (connectResult == CorsairError.CE_InvalidOperation)
        {
            CorsairNative.CorsairDisconnect();
            connectResult = CorsairNative.CorsairConnect(_sessionHandler, IntPtr.Zero);
        }

        if (connectResult != CorsairError.CE_Success)
        {
            throw new InvalidOperationException($"CorsairConnect failed: {connectResult}.");
        }

        if (!tcs.Task.Wait(ConnectTimeout))
        {
            throw new TimeoutException("Timed out waiting for iCUE connection.");
        }

        if (currentState == CorsairSessionState.CSS_Connected)
        {
            CorsairError detailsResult = CorsairNative.CorsairGetSessionDetails(out CorsairSessionDetails details);
            if (detailsResult == CorsairError.CE_Success)
            {
                logger.Info(
                    "iCUE SDK client {0}.{1}.{2}, server {3}.{4}.{5}, host {6}.{7}.{8}",
                    details.clientVersion.major,
                    details.clientVersion.minor,
                    details.clientVersion.patch,
                    details.serverVersion.major,
                    details.serverVersion.minor,
                    details.serverVersion.patch,
                    details.serverHostVersion.major,
                    details.serverHostVersion.minor,
                    details.serverHostVersion.patch);
            }
        }

        return currentState;
    }

    /// <summary>
    /// Represents a keyboard device selection result.
    /// </summary>
    /// <param name="DeviceId">The device identifier.</param>
    /// <param name="HasMuteKey">True when the mute key LED is available.</param>
    private readonly record struct KeyboardDeviceSelection(string DeviceId, bool HasMuteKey);

    /// <summary>
    /// Represents LED capability flags for a keyboard device.
    /// </summary>
    /// <param name="HasScrollLock">True when Scroll Lock is available.</param>
    /// <param name="HasMuteKey">True when Volume Mute is available.</param>
    private readonly record struct KeyboardLedCapabilities(bool HasScrollLock, bool HasMuteKey);
}
