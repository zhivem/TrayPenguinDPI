using AdonisUI.Controls;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using TrayPenguinDPI.Helpers;

namespace TrayPenguinDPI
{
    public partial class DnsSettingsWindow : AdonisWindow
    {
        private readonly Dictionary<string, (string Primary, string Secondary)> _dnsServers = new()
        {
            { "DNS по умолчанию", ("", "") },
            { "CloudFlare", ("1.1.1.1", "1.0.0.1") },
            { "Google", ("8.8.8.8", "8.8.4.4") },
            { "Quad9", ("9.9.9.9", "149.112.112.112") },
            { "Adguard", ("94.140.14.14", "94.140.15.15") },
            { "Comodo", ("8.26.56.26", "8.20.247.20") },
            { "Яндекс", ("77.88.8.8", "77.88.8.1") }
        };

        public DnsSettingsWindow()
        {
            InitializeComponent();
            PopulateInterfaceList();
        }

        private async void PopulateInterfaceList()
        {
            try
            {
                var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync("netsh", "interface show interface");
                if (exitCode != 0) throw new InvalidOperationException("Failed to fetch interfaces");

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("Подключен") || line.Contains("Connected"))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 3)
                            InterfaceComboBox.Items.Add(parts[^1].Trim());
                    }
                }

                if (InterfaceComboBox.Items.Count == 0)
                {
                    InterfaceComboBox.Items.Add("Ethernet");
                    App.ShowWarningMessage(App.GetResourceString("WarningNoInterfaces"));
                }
            }
            catch (Exception ex)
            {
                InterfaceComboBox.Items.Add("Ethernet");
                App.ShowErrorMessage(string.Format(App.GetResourceString("ErrorFetchingInterfaces"), ex.Message));
            }

            InterfaceComboBox.SelectedIndex = 0;
            await UpdateCurrentDnsServersAsync(InterfaceComboBox.SelectedItem?.ToString() ?? string.Empty);
        }

        private async Task UpdateCurrentDnsServersAsync(string interfaceName)
        {
            try
            {
                string primaryDns = "Не определен";
                string secondaryDns = "Не определен";
                bool isDhcp = false;

                var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync("netsh", $"interface ip show dns name=\"{interfaceName}\"");
                if (exitCode != 0) throw new InvalidOperationException("Failed to fetch DNS");

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains("DHCP") || line.Contains("автоматически"))
                    {
                        isDhcp = true;
                        break;
                    }
                    if (line.Contains("статически") || line.Contains("DNS-серверы"))
                    {
                        var match = Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                        if (match.Success)
                            primaryDns = match.Value;
                    }
                    else if (line.Contains("index=2"))
                    {
                        var match = Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                        if (match.Success)
                            secondaryDns = match.Value;
                    }
                }

                if (primaryDns == "Не определен" && !isDhcp)
                {
                    (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync("ipconfig", "/all");
                    if (exitCode != 0) throw new InvalidOperationException("Failed to fetch DNS via ipconfig");

                    lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    bool foundInterface = false;
                    foreach (var line in lines)
                    {
                        if (line.Contains(interfaceName))
                            foundInterface = true;
                        if (foundInterface && (line.Contains("DNS Servers") || line.Contains("DNS-серверы")))
                        {
                            var match = Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                            if (match.Success)
                            {
                                primaryDns = match.Value;
                                int index = Array.IndexOf(lines, line);
                                for (int i = index + 1; i < lines.Length; i++)
                                {
                                    var nextMatch = Regex.Match(lines[i], @"(\d+\.\d+\.\d+\.\d+)|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                                    if (nextMatch.Success)
                                    {
                                        secondaryDns = nextMatch.Value;
                                        break;
                                    }
                                    if (!string.IsNullOrWhiteSpace(lines[i]) && !lines[i].Contains(" ")) break;
                                }
                            }
                            break;
                        }
                    }
                }

                if (isDhcp)
                {
                    primaryDns = "DHCP (автоматически)";
                    secondaryDns = "DHCP (автоматически)";
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    PrimaryDnsTextBlock.Text = primaryDns;
                    SecondaryDnsTextBlock.Text = secondaryDns;
                });
            }
            catch
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    PrimaryDnsTextBlock.Text = "Ошибка";
                    SecondaryDnsTextBlock.Text = "Ошибка";
                });
            }
        }

        private void InterfaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem != null)
            {
                _ = UpdateCurrentDnsServersAsync(InterfaceComboBox.SelectedItem.ToString() ?? string.Empty);
            }
        }

        private void DnsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DnsComboBox.SelectedItem is ComboBoxItem item && item.Content != null && _dnsServers.TryGetValue(item.Content.ToString() ?? string.Empty, out var dns))
            {
                SelectedPrimaryDnsTextBox.Text = dns.Primary == "" ? "Не выбрано" : dns.Primary;
                SelectedSecondaryDnsTextBox.Text = dns.Secondary == "" ? "Не выбрано" : dns.Secondary;
            }
            else
            {
                SelectedPrimaryDnsTextBox.Text = "Не выбрано";
                SelectedSecondaryDnsTextBox.Text = "Не выбрано";
            }
        }

        private async void ApplyDnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem == null || DnsComboBox.SelectedItem == null)
            {
                App.ShowWarningMessage(App.GetResourceString("SelectInterfaceAndProvider"));
                return;
            }

            string interfaceName = InterfaceComboBox.SelectedItem?.ToString() ?? string.Empty;
            string? dnsChoice = (DnsComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string primaryDns = SelectedPrimaryDnsTextBox.Text;
            string secondaryDns = SelectedSecondaryDnsTextBox.Text;

            try
            {
                if (dnsChoice == "DNS по умолчанию")
                    await ClearDnsAsync(interfaceName);
                else
                    await SetDnsAsync(interfaceName, primaryDns, secondaryDns);

                await UpdateCurrentDnsServersAsync(interfaceName);
            }
            catch (Exception ex)
            {
                App.ShowErrorMessage(string.Format(App.GetResourceString("FailedToApplyDns"), ex.Message));
            }
        }

        private async void ClearDnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem == null)
            {
                App.ShowWarningMessage(App.GetResourceString("SelectInterface"));
                return;
            }

            string interfaceName = InterfaceComboBox.SelectedItem.ToString() ?? string.Empty;

            try
            {
                await ClearDnsAsync(interfaceName);
                await UpdateCurrentDnsServersAsync(interfaceName);
            }
            catch (Exception ex)
            {
                App.ShowErrorMessage(string.Format(App.GetResourceString("FailedToClearDns"), ex.Message));
            }
        }

        private async Task SetDnsAsync(string interfaceName, string primaryDns, string secondaryDns)
        {
            if (string.IsNullOrWhiteSpace(primaryDns) || primaryDns == "Не выбрано")
                throw new Exception("Preferred DNS server not specified.");

            var (exitCode, _) = await ProcessHelper.RunProcessWithOutputAsync("netsh", $"interface ip set dns name=\"{interfaceName}\" source=static address={primaryDns}");
            if (exitCode != 0) throw new InvalidOperationException("Failed to set primary DNS");

            if (!string.IsNullOrEmpty(secondaryDns) && secondaryDns != "Не выбрано")
            {
                (exitCode, _) = await ProcessHelper.RunProcessWithOutputAsync("netsh", $"interface ip add dns name=\"{interfaceName}\" address={secondaryDns} index=2");
                if (exitCode != 0) throw new InvalidOperationException("Failed to set secondary DNS");
            }
        }

        private async Task ClearDnsAsync(string interfaceName)
        {
            var (exitCode, _) = await ProcessHelper.RunProcessWithOutputAsync("netsh", $"interface ip set dns name=\"{interfaceName}\" source=dhcp");
            if (exitCode != 0) throw new InvalidOperationException("Failed to clear DNS");
        }

        private async void FlushDnsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var (exitCode, _) = await ProcessHelper.RunProcessWithOutputAsync("ipconfig", "/flushdns");
                if (exitCode != 0) throw new InvalidOperationException("Failed to flush DNS cache");
            }
            catch (Exception ex)
            {
                App.ShowErrorMessage(string.Format(App.GetResourceString("FailedToFlushDnsCache"), ex.Message));
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}