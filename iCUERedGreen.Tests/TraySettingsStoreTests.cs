using System;
using System.IO;
using NLog;
using iCUERedGreen.Tray;

namespace iCUERedGreen.Tests;

/// <summary>
/// Tests for tray settings persistence.
/// </summary>
public sealed class TraySettingsStoreTests
{
    /// <summary>
    /// Ensures missing settings files return defaults.
    /// </summary>
    [Fact]
    public void LoadOrDefaultReturnsDefaultsWhenMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string path = Path.Combine(tempDir, "appsettings.json");
        Logger logger = LogManager.GetLogger("TraySettingsStoreTests");
        TraySettingsStore store = new TraySettingsStore(path);
        try
        {
            TraySettings settings = store.LoadOrDefault(logger);

            Assert.False(settings.DevMode);
            Assert.False(settings.ToggleOnKeypress);
            Assert.Equal(5, settings.Polling.IntervalSeconds);
            Assert.NotNull(settings.Fritz);
            Assert.NotNull(settings.CueSdk);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    /// <summary>
    /// Ensures settings can roundtrip through disk.
    /// </summary>
    [Fact]
    public void SaveAndLoadRoundTrip()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string path = Path.Combine(tempDir, "appsettings.json");
        Logger logger = LogManager.GetLogger("TraySettingsStoreTests");
        TraySettingsStore store = new TraySettingsStore(path);
        try
        {
            TraySettings original = new TraySettings
            {
                DevMode = true,
                ToggleOnKeypress = true,
                Fritz = new FritzSettings { Host = "fritz.box", Ain = "12345 6789012" },
                Polling = new PollingSettings { IntervalSeconds = 7 },
                CueSdk = new CueSdkSettings { Path = "C:\\Temp\\iCUESDK.x64_2019.dll" }
            };

            store.Save(original);
            TraySettings loaded = store.LoadOrDefault(logger);

            Assert.True(loaded.DevMode);
            Assert.True(loaded.ToggleOnKeypress);
            Assert.Equal("fritz.box", loaded.Fritz.Host);
            Assert.Equal("12345 6789012", loaded.Fritz.Ain);
            Assert.Equal(7, loaded.Polling.IntervalSeconds);
            Assert.Equal("C:\\Temp\\iCUESDK.x64_2019.dll", loaded.CueSdk.Path);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
