using System;
using System.Collections.Generic;

namespace Knx.EtsBridge.Addin
{
    /// <summary>
    /// Decides whether a request that WRITES TO THE KNX BUS (programming, group
    /// writes, resets, ...) may run, based on the operator's AddIn preferences.
    ///
    /// - "Bus read-only" blocks every bus write (reads such as scan/ping/group.read
    ///   and group.monitor stay allowed). Off by default, so automatic programming
    ///   keeps working unless the operator opts in.
    /// - firmware.update additionally needs "Unattended firmware update".
    ///
    /// Pure (no ETS SDK / WPF dependency) so it is unit-testable on any platform.
    /// </summary>
    internal static class BusWritePolicy
    {
        /// <summary>Protocol methods that change the state of a physical device or the bus.</summary>
        private static readonly HashSet<string> BusWriteMethods = new HashSet<string>(StringComparer.Ordinal)
        {
            "device.program",
            "firmware.update",
            "group.write",
            "device.unload",
            "device.reset",
            "bus.setIndividualAddress"
        };

        public static bool IsBusWrite(string method) => BusWriteMethods.Contains(method);

        public static BusWriteDecision Evaluate(string method, bool busReadOnly, bool unattendedFirmwareUpdate)
        {
            if (!IsBusWrite(method))
                return BusWriteDecision.Allowed;

            if (busReadOnly)
            {
                return BusWriteDecision.Denied(
                    $"{method} writes to the KNX bus, but the AddIn is in 'Bus read-only' mode. "
                  + "Ask the user to disable 'Bus read-only' in the AddIn configuration if this "
                  + "operation is intended.");
            }

            if (method == "firmware.update" && !unattendedFirmwareUpdate)
            {
                return BusWriteDecision.Denied(
                    "firmware.update requires the 'Unattended firmware update' "
                  + "preference to be enabled in the AddIn Configuration dialog.");
            }

            return BusWriteDecision.Allowed;
        }
    }

    /// <summary>Outcome of <see cref="BusWritePolicy.Evaluate"/>.</summary>
    internal sealed class BusWriteDecision
    {
        public static readonly BusWriteDecision Allowed = new BusWriteDecision(true, "");

        private BusWriteDecision(bool isAllowed, string reason)
        {
            IsAllowed = isAllowed;
            Reason = reason;
        }

        public bool IsAllowed { get; }

        /// <summary>Why the request was denied; empty when allowed.</summary>
        public string Reason { get; }

        public static BusWriteDecision Denied(string reason) => new BusWriteDecision(false, reason);
    }
}
