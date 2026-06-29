using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace iCUERedGreen;

/// <summary>
/// Abstraction for reading and updating the Windows global audio mute state.
/// </summary>
internal interface IWindowsAudioMuteService
{
    /// <summary>
    /// Reads the current mute state.
    /// </summary>
    /// <returns>The current mute state, or <see cref="SoundMuteState.Unknown"/> on failure.</returns>
    SoundMuteState TryGetMuteState();

    /// <summary>
    /// Sets the current mute state.
    /// </summary>
    /// <param name="muted">True to mute; false to unmute.</param>
    /// <returns>True when the call succeeded; otherwise false.</returns>
    bool TrySetMuted(bool muted);
}

/// <summary>
/// Provides Windows Core Audio access to the default render endpoint mute state.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsAudioMuteService : IWindowsAudioMuteService
{
    private const uint ClsCtxAll = 23;

    /// <summary>
    /// Reads the current mute state.
    /// </summary>
    /// <returns>The current mute state, or <see cref="SoundMuteState.Unknown"/> on failure.</returns>
    public SoundMuteState TryGetMuteState()
    {
        try
        {
            IAudioEndpointVolume? endpointVolume = null;
            try
            {
                endpointVolume = CreateEndpointVolume();
                endpointVolume.GetMute(out bool isMuted);
                return isMuted ? SoundMuteState.Muted : SoundMuteState.Unmuted;
            }
            finally
            {
                ReleaseComObject(endpointVolume);
            }
        }
        catch
        {
            return SoundMuteState.Unknown;
        }
    }

    /// <summary>
    /// Sets the current mute state.
    /// </summary>
    /// <param name="muted">True to mute; false to unmute.</param>
    /// <returns>True when the call succeeded; otherwise false.</returns>
    public bool TrySetMuted(bool muted)
    {
        try
        {
            IAudioEndpointVolume? endpointVolume = null;
            try
            {
                endpointVolume = CreateEndpointVolume();
                Guid eventContext = Guid.NewGuid();
                endpointVolume.SetMute(muted, ref eventContext);
                return true;
            }
            finally
            {
                ReleaseComObject(endpointVolume);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the default render endpoint volume interface.
    /// </summary>
    /// <returns>The endpoint volume interface.</returns>
    private static IAudioEndpointVolume CreateEndpointVolume()
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        object? endpointObject = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.ERender, ERole.EMultimedia, out device);

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, ClsCtxAll, IntPtr.Zero, out endpointObject);
            return (IAudioEndpointVolume)endpointObject;
        }
        finally
        {
            ReleaseComObject(device);
            ReleaseComObject(enumerator);
        }
    }

    /// <summary>
    /// Releases a COM object when present.
    /// </summary>
    /// <param name="instance">The COM object to release.</param>
    private static void ReleaseComObject(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
        {
            return;
        }

        Marshal.FinalReleaseComObject(instance);
    }

    /// <summary>
    /// Defines the audio endpoint volume COM interface.
    /// </summary>
    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        /// <summary>
        /// Registers for change notifications.
        /// </summary>
        /// <param name="notify">The callback instance.</param>
        void RegisterControlChangeNotify(IntPtr notify);

        /// <summary>
        /// Unregisters a change notification callback.
        /// </summary>
        /// <param name="notify">The callback instance.</param>
        void UnregisterControlChangeNotify(IntPtr notify);

        /// <summary>
        /// Gets the channel count.
        /// </summary>
        /// <param name="channelCount">The channel count.</param>
        void GetChannelCount(out uint channelCount);

        /// <summary>
        /// Sets the master volume in dB.
        /// </summary>
        void SetMasterVolumeLevel(float levelDb, ref Guid eventContext);

        /// <summary>
        /// Sets the master volume scalar.
        /// </summary>
        void SetMasterVolumeLevelScalar(float level, ref Guid eventContext);

        /// <summary>
        /// Gets the master volume in dB.
        /// </summary>
        void GetMasterVolumeLevel(out float levelDb);

        /// <summary>
        /// Gets the master volume scalar.
        /// </summary>
        void GetMasterVolumeLevelScalar(out float level);

        /// <summary>
        /// Sets a channel volume in dB.
        /// </summary>
        void SetChannelVolumeLevel(uint channelNumber, float levelDb, ref Guid eventContext);

        /// <summary>
        /// Sets a channel volume scalar.
        /// </summary>
        void SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);

        /// <summary>
        /// Gets a channel volume in dB.
        /// </summary>
        void GetChannelVolumeLevel(uint channelNumber, out float levelDb);

        /// <summary>
        /// Gets a channel volume scalar.
        /// </summary>
        void GetChannelVolumeLevelScalar(uint channelNumber, out float level);

        /// <summary>
        /// Sets the mute state.
        /// </summary>
        /// <param name="isMuted">True to mute.</param>
        /// <param name="eventContext">The event context.</param>
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, ref Guid eventContext);

        /// <summary>
        /// Gets the mute state.
        /// </summary>
        /// <param name="isMuted">True when muted.</param>
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);

        /// <summary>
        /// Gets the volume step information.
        /// </summary>
        void GetVolumeStepInfo(out uint step, out uint stepCount);

        /// <summary>
        /// Moves to the next volume step.
        /// </summary>
        void VolumeStepUp(ref Guid eventContext);

        /// <summary>
        /// Moves to the previous volume step.
        /// </summary>
        void VolumeStepDown(ref Guid eventContext);

        /// <summary>
        /// Queries hardware support.
        /// </summary>
        void QueryHardwareSupport(out uint hardwareSupportMask);

        /// <summary>
        /// Gets the volume range.
        /// </summary>
        void GetVolumeRange(out float levelMinDb, out float levelMaxDb, out float incrementDb);
    }

    /// <summary>
    /// Defines the multimedia device enumerator COM interface.
    /// </summary>
    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        /// <summary>
        /// Enumerates audio endpoints.
        /// </summary>
        /// <remarks>
        /// This method is unused, but it must remain declared first to preserve the COM vtable
        /// layout. <c>EnumAudioEndpoints</c> is the first method of <c>IMMDeviceEnumerator</c>;
        /// omitting it shifts <see cref="GetDefaultAudioEndpoint"/> onto the wrong vtable slot and
        /// the returned object fails to cast to <see cref="IMMDevice"/> (E_NOINTERFACE).
        /// </remarks>
        /// <param name="dataFlow">The data flow.</param>
        /// <param name="stateMask">The device state mask.</param>
        /// <param name="devices">The device collection pointer.</param>
        void EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);

        /// <summary>
        /// Gets the default audio endpoint.
        /// </summary>
        /// <param name="dataFlow">The data flow.</param>
        /// <param name="role">The endpoint role.</param>
        /// <param name="device">The device.</param>
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
    }

    /// <summary>
    /// Defines the multimedia device COM interface.
    /// </summary>
    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        /// <summary>
        /// Activates a device interface.
        /// </summary>
        /// <param name="iid">The requested interface identifier.</param>
        /// <param name="clsCtx">The COM class context.</param>
        /// <param name="activationParams">Optional activation parameters.</param>
        /// <param name="interfaceInstance">The activated interface.</param>
        void Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfaceInstance);
    }

    /// <summary>
    /// Defines the default audio endpoint data flow.
    /// </summary>
    private enum EDataFlow
    {
        /// <summary>
        /// Output/render endpoint.
        /// </summary>
        ERender = 0
    }

    /// <summary>
    /// Defines the default audio endpoint role.
    /// </summary>
    private enum ERole
    {
        /// <summary>
        /// Multimedia role.
        /// </summary>
        EMultimedia = 1
    }

    /// <summary>
    /// Instantiates the MMDevice enumerator COM class.
    /// </summary>
    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject
    {
    }
}
