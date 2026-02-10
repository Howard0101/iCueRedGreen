using iCUERedGreen;

namespace iCUERedGreen.Tests;

/// <summary>
/// Tests for CLI option parsing.
/// </summary>
public sealed class OptionsTests
{
    /// <summary>
    /// Verifies the toggle flag is enabled when requested.
    /// </summary>
    [Fact]
    public void ToggleOnKeypressIsEnabledWhenRequested()
    {
        Options options = Options.Load(new[] { "--toggle-on-keypress" }, out bool showHelp);

        Assert.False(showHelp);
        Assert.True(options.ToggleOnKeypress);
    }

    /// <summary>
    /// Verifies the toggle flag defaults to false.
    /// </summary>
    [Fact]
    public void ToggleOnKeypressDefaultsToFalse()
    {
        Options options = Options.Load(Array.Empty<string>(), out _);

        Assert.False(options.ToggleOnKeypress);
    }
}
