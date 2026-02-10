using System;
using NLog;
using iCUERedGreen.Tray;

namespace iCUERedGreen.Tests;

/// <summary>
/// Tests for the Credential Manager wrapper.
/// </summary>
public sealed class CredentialStoreTests
{
    /// <summary>
    /// Ensures credentials can be written and read back.
    /// </summary>
    [Fact]
    public void WriteAndReadRoundTrip()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ICUEREDGREEN_RUN_CRED_TESTS"), "1", StringComparison.Ordinal))
        {
            return;
        }

        string targetName = $"iCUERedGreen/Tests/{Guid.NewGuid():N}";
        Logger logger = LogManager.GetLogger("CredentialStoreTests");
        CredentialStore store = new CredentialStore(logger, targetName);

        try
        {
            store.Write(new CredentialEntry("test-user", "test-password"));

            bool found = store.TryRead(out CredentialEntry? entry);

            Assert.True(found);
            Assert.NotNull(entry);
            Assert.Equal("test-user", entry!.UserName);
            Assert.Equal("test-password", entry.Password);
        }
        finally
        {
            store.Delete();
        }
    }
}
