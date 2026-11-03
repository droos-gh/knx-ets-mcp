using System;

namespace Knx.EtsBridge.Addin
{
    /// <summary>
    /// Holds the operator preferences that gate bus-writing operations, as set in
    /// the AddIn Configuration dialog (ETS6) or panel (ETS5). The decision itself
    /// lives in <see cref="BusWritePolicy"/>.
    ///
    /// Thread-safe: the backing fields use volatile.
    /// </summary>
    internal sealed class ApprovalManager
    {
        /// <summary>
        /// When true, firmware.update proceeds without further approval.
        /// When false, firmware.update returns approval_required.
        /// Set via the "Unattended firmware update" checkbox in the Configuration dialog.
        /// Default: false (approval required).
        /// </summary>
        private volatile bool _unattended;
        public bool Unattended
        {
            get => _unattended;
            set => _unattended = value;
        }

        /// <summary>
        /// When true, every bus write (program, group.write, reset, unload, individual
        /// address, firmware) returns approval_required; bus reads stay allowed.
        /// Set via the "Bus read-only" checkbox. Default: false (bus writes allowed).
        /// </summary>
        private volatile bool _busReadOnly;
        public bool BusReadOnly
        {
            get => _busReadOnly;
            set => _busReadOnly = value;
        }
    }
}
