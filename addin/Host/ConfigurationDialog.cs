using System;
using System.Windows;
using System.Windows.Controls;
using Knx.Ets.Sdk;

namespace Knx.EtsBridge.Addin
{
    /// <summary>
    /// Code-only WPF dialog for the AddIn configuration, shown by ETS when the
    /// user opens the app's configuration from the ETS settings dialog.
    /// Implements <see cref="IModalDialog"/> so ETS can host it via
    /// <see cref="IDialogService.ShowModal"/>.
    /// </summary>
    internal sealed class ConfigurationDialog : UserControl, IModalDialog
    {
        private readonly CheckBox _unattendedCheckBox;
        private readonly CheckBox _busReadOnlyCheckBox;
        private readonly CheckBox _tcpEnabledCheckBox;
        private readonly TextBox _tcpPortTextBox;
        private readonly CheckBox _tcpAllowLanCheckBox;
        private readonly TextBox _tokenTextBox;

        public ConfigurationDialog(bool unattendedFirmwareUpdate, bool busReadOnly,
                                   bool tcpEnabled, int tcpPort, bool tcpAllowLan,
                                   string token)
        {
            Width = 420;
            MinHeight = 260;
            Background = SystemColors.WindowBrush;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: title
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: bus safety
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: TCP section
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 3: buttons

            // Title
            var title = new TextBlock
            {
                Text = "KNX-ETS MCP Bridge Configuration",
                FontSize = 15,
                Margin = new Thickness(10, 10, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(title, 0);
            grid.Children.Add(title);

            // --- Bus safety section ---
            var busPanel = new StackPanel { Margin = new Thickness(10, 10, 10, 10) };

            _busReadOnlyCheckBox = new CheckBox
            {
                Content = "Bus read-only (block programming, group writes, resets)",
                IsChecked = busReadOnly,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "When checked, the assistant can still read the bus (scan, ping, group read,\n"
                        + "monitor) but every bus write -- device.program, group.write, device.reset,\n"
                        + "device.unload, bus.setIndividualAddress, firmware.update -- is refused."
            };
            busPanel.Children.Add(_busReadOnlyCheckBox);

            _unattendedCheckBox = new CheckBox
            {
                Content = "Unattended firmware update",
                IsChecked = unattendedFirmwareUpdate,
                ToolTip = "When checked, firmware.update runs without a further prompt\n"
                        + "(unless 'Bus read-only' is checked)."
            };
            busPanel.Children.Add(_unattendedCheckBox);

            Grid.SetRow(busPanel, 1);
            grid.Children.Add(busPanel);

            // --- TCP endpoint section ---
            var tcpPanel = new StackPanel { Margin = new Thickness(10, 0, 10, 10) };

            _tcpEnabledCheckBox = new CheckBox
            {
                Content = "Enable TCP endpoint (network)",
                IsChecked = tcpEnabled,
                Margin = new Thickness(0, 0, 0, 6),
                ToolTip = "Exposes the JSON-line protocol over TCP in addition to the named pipe."
            };
            tcpPanel.Children.Add(_tcpEnabledCheckBox);

            // Port row
            var portPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 6) };
            portPanel.Children.Add(new TextBlock
            {
                Text = "Port:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _tcpPortTextBox = new TextBox
            {
                Text = tcpPort.ToString(),
                Width = 70,
                MaxLength = 5,
                VerticalAlignment = VerticalAlignment.Center
            };
            portPanel.Children.Add(_tcpPortTextBox);
            tcpPanel.Children.Add(portPanel);

            _tcpAllowLanCheckBox = new CheckBox
            {
                Content = "Allow LAN (all interfaces)",
                IsChecked = tcpAllowLan,
                Margin = new Thickness(20, 0, 0, 6),
                ToolTip = "When unchecked, TCP binds to 127.0.0.1 only (loopback).\n"
                        + "When checked, TCP binds to 0.0.0.0 (all interfaces)."
            };
            tcpPanel.Children.Add(_tcpAllowLanCheckBox);

            // Token row (empty = auto-generated random token; auth is always on).
            var tokenRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 0, 0, 6) };
            tokenRow.Children.Add(new TextBlock
            {
                Text = "Token:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _tokenTextBox = new TextBox
            {
                Text = token ?? "",
                Width = 230,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Auth token for the endpoint (always required).\n"
                        + "Leave empty to generate a new random token."
            };
            tokenRow.Children.Add(_tokenTextBox);
            tcpPanel.Children.Add(tokenRow);

            // Warning label
            var warning = new TextBlock
            {
                Text = "Warning: TCP has no TLS. Token and data travel in cleartext.\n"
                     + "Use only on a trusted LAN. Changes take effect immediately.",
                Foreground = System.Windows.Media.Brushes.OrangeRed,
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 2, 0, 0)
            };
            tcpPanel.Children.Add(warning);

            // Enable/disable sub-controls based on TCP checkbox
            Action updateTcpSubControls = () =>
            {
                bool enabled = _tcpEnabledCheckBox.IsChecked == true;
                _tcpPortTextBox.IsEnabled = enabled;
                _tcpAllowLanCheckBox.IsEnabled = enabled;
            };
            _tcpEnabledCheckBox.Checked += (_, __) => updateTcpSubControls();
            _tcpEnabledCheckBox.Unchecked += (_, __) => updateTcpSubControls();
            updateTcpSubControls();

            Grid.SetRow(tcpPanel, 2);
            grid.Children.Add(tcpPanel);

            // OK / Cancel buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(5, 5, 5, 10)
            };

            var okButton = new Button
            {
                Content = "OK",
                MinWidth = 75,
                Margin = new Thickness(5),
                IsDefault = true
            };
            okButton.Click += (_, __) =>
            {
                Result = true;
                EndDialog?.Invoke(this, EventArgs.Empty);
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 75,
                Margin = new Thickness(5),
                IsCancel = true
            };
            cancelButton.Click += (_, __) =>
            {
                Result = false;
                EndDialog?.Invoke(this, EventArgs.Empty);
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 3);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        /// <summary>The checkbox value after the dialog closes.</summary>
        public bool UnattendedFirmwareUpdate => _unattendedCheckBox.IsChecked == true;

        public bool BusReadOnly => _busReadOnlyCheckBox.IsChecked == true;

        public bool TcpEnabled => _tcpEnabledCheckBox.IsChecked == true;

        /// <summary>
        /// Parsed TCP port. Returns the text box value if it is a valid port number
        /// in the range 1-65535; otherwise returns the default 8730.
        /// </summary>
        public int TcpPort
        {
            get
            {
                if (int.TryParse(_tcpPortTextBox.Text, out int port) && port >= 1 && port <= 65535)
                    return port;
                return 8730;
            }
        }

        public bool TcpAllowLan => _tcpAllowLanCheckBox.IsChecked == true;

        /// <summary>Token; empty means "generate a new random token".</summary>
        public string Token => _tokenTextBox.Text?.Trim() ?? "";

        // --- IModalDialog implementation ---
        public event EventHandler? EndDialog;
        public bool? Result { get; private set; }
    }
}
