using Xunit;

namespace Knx.EtsBridge.Addin.Tests
{
    public class BusWritePolicyTests
    {
        public static TheoryData<string> BusWriteMethods => new TheoryData<string>
        {
            "device.program",
            "firmware.update",
            "group.write",
            "device.unload",
            "device.reset",
            "bus.setIndividualAddress"
        };

        public static TheoryData<string> NonBusWriteMethods => new TheoryData<string>
        {
            // Bus reads
            "bus.ping", "bus.scanLine", "bus.reconstructLine",
            "device.readInfo", "device.readGroupObjects",
            "group.read", "group.monitor",
            // Job control
            "job.status", "job.cancel",
            // Project mutations and reads
            "ga.create", "param.set", "batch.apply", "devices.list"
        };

        [Theory]
        [MemberData(nameof(BusWriteMethods))]
        public void IsBusWrite_BusWritingMethod_ReturnsTrue(string method)
        {
            Assert.True(BusWritePolicy.IsBusWrite(method));
        }

        [Theory]
        [MemberData(nameof(NonBusWriteMethods))]
        public void IsBusWrite_OtherMethod_ReturnsFalse(string method)
        {
            Assert.False(BusWritePolicy.IsBusWrite(method));
        }

        [Fact]
        public void IsBusWrite_IsCaseSensitive()
        {
            // Protocol method names are exact; a differently-cased name is not a known method.
            Assert.False(BusWritePolicy.IsBusWrite("Device.Program"));
        }

        [Theory]
        [MemberData(nameof(BusWriteMethods))]
        public void Evaluate_BusReadOnly_DeniesEveryBusWrite(string method)
        {
            // Even with "Unattended firmware update" on, read-only wins.
            var decision = BusWritePolicy.Evaluate(method, busReadOnly: true, unattendedFirmwareUpdate: true);

            Assert.False(decision.IsAllowed);
            Assert.Contains("Bus read-only", decision.Reason);
            Assert.Contains(method, decision.Reason);
        }

        [Theory]
        [MemberData(nameof(NonBusWriteMethods))]
        public void Evaluate_BusReadOnly_AllowsEverythingElse(string method)
        {
            var decision = BusWritePolicy.Evaluate(method, busReadOnly: true, unattendedFirmwareUpdate: false);

            Assert.True(decision.IsAllowed);
            Assert.Equal("", decision.Reason);
        }

        [Theory]
        [InlineData("device.program")]
        [InlineData("group.write")]
        [InlineData("device.unload")]
        [InlineData("device.reset")]
        [InlineData("bus.setIndividualAddress")]
        public void Evaluate_DefaultPreferences_AllowsBusWritesExceptFirmware(string method)
        {
            // Default: "Bus read-only" off -> programming runs automatically.
            Assert.True(BusWritePolicy.Evaluate(method, busReadOnly: false, unattendedFirmwareUpdate: false).IsAllowed);
        }

        [Fact]
        public void Evaluate_FirmwareWithoutUnattended_IsDenied()
        {
            var decision = BusWritePolicy.Evaluate("firmware.update", busReadOnly: false, unattendedFirmwareUpdate: false);

            Assert.False(decision.IsAllowed);
            Assert.Contains("Unattended firmware update", decision.Reason);
        }

        [Fact]
        public void Evaluate_FirmwareWithUnattended_IsAllowed()
        {
            var decision = BusWritePolicy.Evaluate("firmware.update", busReadOnly: false, unattendedFirmwareUpdate: true);

            Assert.True(decision.IsAllowed);
        }
    }
}
