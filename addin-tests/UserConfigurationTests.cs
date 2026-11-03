using System;
using System.IO;
using Xunit;

namespace Knx.EtsBridge.Addin.Tests
{
    /// <summary>
    /// File persistence used by the ETS5 build (ETS5 does not persist the AddIn
    /// configuration), so the safety-critical "Bus read-only" survives an ETS restart.
    /// </summary>
    public sealed class UserConfigurationTests : IDisposable
    {
        private readonly string _directory =
            Path.Combine(Path.GetTempPath(), "knx-ets-bridge-tests-" + Guid.NewGuid().ToString("N"));

        private string ConfigPath => Path.Combine(_directory, "nested", "ets5-config.xml");

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public void SaveThenLoad_RoundTripsEverySetting()
        {
            var saved = new UserConfiguration
            {
                BusReadOnly = true,
                UnattendedFirmwareUpdate = true,
                TcpEnabled = true,
                TcpAllowLan = true,
                TcpPort = 9001,
                Token = "secret-token",
            };

            Assert.True(saved.SaveToFile(ConfigPath));
            var loaded = UserConfiguration.LoadFromFile(ConfigPath);

            Assert.NotNull(loaded);
            Assert.True(loaded!.BusReadOnly);
            Assert.True(loaded.UnattendedFirmwareUpdate);
            Assert.True(loaded.TcpEnabled);
            Assert.True(loaded.TcpAllowLan);
            Assert.Equal(9001, loaded.TcpPort);
            Assert.Equal("secret-token", loaded.Token);
        }

        [Fact]
        public void Save_OverwritesAnExistingFile()
        {
            Assert.True(new UserConfiguration { BusReadOnly = true }.SaveToFile(ConfigPath));
            Assert.True(new UserConfiguration { BusReadOnly = false }.SaveToFile(ConfigPath));

            Assert.False(UserConfiguration.LoadFromFile(ConfigPath)!.BusReadOnly);
        }

        [Fact]
        public void Load_MissingFile_ReturnsNull()
        {
            Assert.Null(UserConfiguration.LoadFromFile(ConfigPath));
        }

        [Fact]
        public void Load_CorruptFile_ReturnsNull()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, "not xml at all");

            Assert.Null(UserConfiguration.LoadFromFile(ConfigPath));
        }
    }
}
