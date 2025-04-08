using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AdonisUI.Controls;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

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
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "interface show interface",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

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
                    Log(App.GetResourceString("WarningNoInterfaces"));
                }
            }
            catch (Exception ex)
            {
                Log(string.Format(App.GetResourceString("ErrorFetchingInterfaces"), ex.Message));
                InterfaceComboBox.Items.Add("Ethernet");
                MessageBox.Show(
                    string.Format(App.GetResourceString("ErrorFetchingInterfaces"), ex.Message).Replace(". ", ".\n"),
                    App.GetResourceString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"interface ip show dns name=\"{interfaceName}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

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
                    process.StartInfo.FileName = "ipconfig";
                    process.StartInfo.Arguments = "/all";
                    process.Start();
                    output = await process.StandardOutput.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

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
            catch (Exception ex)
            {
                Log(string.Format(App.GetResourceString("ErrorFetchingDns"), ex.Message));
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
                Log(string.Format(App.GetResourceString("DnsProviderSelected"), item.Content.ToString()));
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
                MessageBox.Show(
                    App.GetResourceString("SelectInterfaceAndProvider"),
                    App.GetResourceString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string interfaceName = InterfaceComboBox.SelectedItem?.ToString() ?? string.Empty;
            string? dnsChoice = (DnsComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string primaryDns = SelectedPrimaryDnsTextBox.Text;
            string secondaryDns = SelectedSecondaryDnsTextBox.Text;

            Log(string.Format(App.GetResourceString("ApplyingDns"), interfaceName, dnsChoice));

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
                Log(string.Format(App.GetResourceString("ErrorApplyingDns"), ex.Message));
                MessageBox.Show(
                    string.Format(App.GetResourceString("FailedToApplyDns"), ex.Message).Replace(". ", ".\n"),
                    App.GetResourceString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async void ClearDnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem == null)
            {
                MessageBox.Show(
                    App.GetResourceString("SelectInterface"),
                    App.GetResourceString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string interfaceName = InterfaceComboBox.SelectedItem.ToString() ?? string.Empty;
            Log(string.Format(App.GetResourceString("ClearingDns"), interfaceName));

            try
            {
                await ClearDnsAsync(interfaceName);
                await UpdateCurrentDnsServersAsync(interfaceName);
            }
            catch (Exception ex)
            {
                Log(string.Format(App.GetResourceString("ErrorClearingDns"), ex.Message));
                MessageBox.Show(
                    string.Format(App.GetResourceString("FailedToClearDns"), ex.Message).Replace(". ", ".\n"),
                    App.GetResourceString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private async Task SetDnsAsync(string interfaceName, string primaryDns, string secondaryDns)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(primaryDns) || primaryDns == "Не выбрано")
                    throw new Exception("Не указан предпочитаемый DNS-сервер.");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"interface ip set dns name=\"{interfaceName}\" source=static address={primaryDns}",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                Log(string.Format(App.GetResourceString("PrimaryDnsSetSuccess"), primaryDns));

                if (!string.IsNullOrEmpty(secondaryDns) && secondaryDns != "Не выбрано")
                {
                    process.StartInfo.Arguments = $"interface ip add dns name=\"{interfaceName}\" address={secondaryDns} index=2";
                    process.Start();
                    await process.StandardOutput.ReadToEndAsync();
                    await Task.Run(() => process.WaitForExit());

                    Log(string.Format(App.GetResourceString("SecondaryDnsSetSuccess"), secondaryDns));
                }
            }
            catch (Exception ex)
            {
                Log(string.Format(App.GetResourceString("ErrorSettingDns"), ex.Message));
                throw;
            }
        }

        private async Task ClearDnsAsync(string interfaceName)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"interface ip set dns name=\"{interfaceName}\" source=dhcp",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                Log(string.Format(App.GetResourceString("DnsClearedSuccess"), interfaceName));
            }
            catch (Exception ex)
            {
                Log(string.Format(App.GetResourceString("ErrorClearingDns"), ex.Message));
                throw;
            }
        }

        private async void FlushDnsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ipconfig",
                        Arguments = "/flushdns",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                await process.StandardOutput.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());

                Log(App.GetResourceString("DnsCacheClearedSuccess"));
            }
            catch (Exception ex)
            {
                Log(string.Format(App.GetResourceString("ErrorFlushingDnsCache"), ex.Message));
                MessageBox.Show(
                    string.Format(App.GetResourceString("FailedToFlushDnsCache"), ex.Message).Replace(". ", ".\n"),
                    App.GetResourceString("ErrorTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Log(string message)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss}: {message}\n");
                    LogTextBox.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка логирования: {ex.Message}");
            }
        }
    }
}