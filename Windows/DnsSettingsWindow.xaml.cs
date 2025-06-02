using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        private async void PopulateInterfaceList(object? sender = null, EventArgs? e = null)
        {
            try
            {
                LogMessage("Получение списка сетевых интерфейсов...");
                var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync("netsh", "interface show interface");

                if (exitCode != 0)
                    throw new InvalidOperationException("Не удалось получить список интерфейсов.");

                InterfaceComboBox.Items.Clear();

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
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
                    LogMessage("Сетевые интерфейсы не найдены. Используется 'Ethernet' по умолчанию.");
                    App.ShowWarningMessage(App.GetResourceString("WarningNoInterfaces"));
                }

                InterfaceComboBox.SelectedIndex = 0;
                await UpdateCurrentDnsServersAsync(InterfaceComboBox.SelectedItem?.ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                InterfaceComboBox.Items.Clear();
                InterfaceComboBox.Items.Add("Ethernet");
                LogMessage($"Ошибка при получении интерфейсов: {ex.Message}");
                App.ShowErrorMessage(string.Format(App.GetResourceString("ErrorFetchingInterfaces"), ex.Message));
            }
        }

        private async Task UpdateCurrentDnsServersAsync(string interfaceName)
        {
            try
            {
                LogMessage($"Обновление текущих DNS для интерфейса '{interfaceName}'...");

                var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync("netsh", $"interface ip show dns name=\"{interfaceName}\"");

                if (exitCode != 0)
                    throw new InvalidOperationException("Не удалось получить DNS через netsh.");

                bool isDhcp = false;
                string primaryDns = "Не определен";
                string secondaryDns = "Не определен";

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.Contains("DHCP") || line.Contains("автоматически"))
                    {
                        isDhcp = true;
                        break;
                    }

                    if (line.Contains("статически") || line.Contains("DNS-серверы"))
                    {
                        var match = Regex.Match(line, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                        if (match.Success)
                            primaryDns = match.Value;
                    }
                    else if (line.Contains("index=2"))
                    {
                        var match = Regex.Match(line, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                        if (match.Success)
                            secondaryDns = match.Value;
                    }
                }

                if (primaryDns == "Не определен" && !isDhcp)
                {
                    // Резервный метод через ipconfig
                    var (exitCode2, output2) = await ProcessHelper.RunProcessWithOutputAsync("ipconfig", "/all");
                    if (exitCode2 == 0)
                    {
                        (primaryDns, secondaryDns) = ParseDnsFromIpConfig(output2, interfaceName);
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
                    LogMessage($"Текущие DNS: Основной = {primaryDns}, Вторичный = {secondaryDns}");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    PrimaryDnsTextBlock.Text = "Ошибка";
                    SecondaryDnsTextBlock.Text = "Ошибка";
                    LogMessage($"Ошибка при получении DNS: {ex.Message}");
                });
            }
        }

        private (string Primary, string Secondary) ParseDnsFromIpConfig(string output, string interfaceName)
        {
            string primaryDns = "Не определен";
            string secondaryDns = "Не определен";
            bool foundInterface = false;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (line.Contains(interfaceName))
                    foundInterface = true;

                if (!foundInterface) continue;

                if (line.Contains("DNS Servers") || line.Contains("DNS-серверы"))
                {
                    var match = Regex.Match(line, @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                    if (match.Success)
                    {
                        primaryDns = match.Value;

                        for (int j = i + 1; j < lines.Length; j++)
                        {
                            var nextMatch = Regex.Match(lines[j], @"(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})|([0-9a-fA-F:]+:[0-9a-fA-F:]+)");
                            if (nextMatch.Success)
                            {
                                secondaryDns = nextMatch.Value;
                                break;
                            }

                            if (!lines[j].Contains(" "))
                                break;
                        }

                        break;
                    }
                }
            }

            return (primaryDns, secondaryDns);
        }

        private void InterfaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem != null)
            {
                LogMessage($"Выбран интерфейс: {InterfaceComboBox.SelectedItem}");
                _ = UpdateCurrentDnsServersAsync(InterfaceComboBox.SelectedItem.ToString() ?? string.Empty);
            }
        }

        private void DnsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DnsComboBox.SelectedItem is ComboBoxItem item && item.Content != null && _dnsServers.TryGetValue(item.Content.ToString() ?? string.Empty, out var dns))
            {
                SelectedPrimaryDnsTextBox.Text = string.IsNullOrEmpty(dns.Primary) ? "Не выбрано" : dns.Primary;
                SelectedSecondaryDnsTextBox.Text = string.IsNullOrEmpty(dns.Secondary) ? "Не выбрано" : dns.Secondary;
                LogMessage($"Выбран провайдер DNS: {item.Content}, Основной = {dns.Primary}, Вторичный = {dns.Secondary}");
            }
            else
            {
                SelectedPrimaryDnsTextBox.Text = "Не выбрано";
                SelectedSecondaryDnsTextBox.Text = "Не выбрано";
                LogMessage("DNS провайдер не выбран.");
            }
        }

        private async void ApplyDnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem == null || DnsComboBox.SelectedItem == null)
            {
                LogMessage("Ошибка: Не выбран сетевой интерфейс или провайдер DNS.");
                App.ShowWarningMessage(App.GetResourceString("SelectInterfaceAndProvider"));
                return;
            }

            string interfaceName = InterfaceComboBox.SelectedItem.ToString() ?? string.Empty;
            string? dnsChoice = (DnsComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string primaryDns = SelectedPrimaryDnsTextBox.Text;
            string secondaryDns = SelectedSecondaryDnsTextBox.Text;

            try
            {
                LogMessage($"Применение DNS для интерфейса '{interfaceName}': Основной = {primaryDns}, Вторичный = {secondaryDns}");

                if (dnsChoice == "DNS по умолчанию")
                {
                    await ClearDnsAsync(interfaceName);
                }
                else
                {
                    if (!IsValidIPAddress(primaryDns) && primaryDns != "Не выбрано")
                        throw new Exception("Некорректный основной DNS-адрес.");
                    if (!string.IsNullOrEmpty(secondaryDns) && secondaryDns != "Не выбрано" && !IsValidIPAddress(secondaryDns))
                        throw new Exception("Некорректный вторичный DNS-адрес.");

                    await SetDnsAsync(interfaceName, primaryDns, secondaryDns);
                }

                await UpdateCurrentDnsServersAsync(interfaceName);
                LogMessage("DNS успешно применён.");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка при применении DNS: {ex.Message}");
                App.ShowErrorMessage(string.Format(App.GetResourceString("FailedToApplyDns"), ex.Message));
            }
        }

        private async void ClearDnsButton_Click(object sender, RoutedEventArgs e)
        {
            if (InterfaceComboBox.SelectedItem == null)
            {
                LogMessage("Ошибка: Не выбран сетевой интерфейс.");
                App.ShowWarningMessage(App.GetResourceString("SelectInterface"));
                return;
            }

            string interfaceName = InterfaceComboBox.SelectedItem.ToString() ?? string.Empty;

            try
            {
                LogMessage($"Очистка DNS для интерфейса '{interfaceName}'.");
                await ClearDnsAsync(interfaceName);
                await UpdateCurrentDnsServersAsync(interfaceName);
                LogMessage("DNS успешно очищен.");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка при очистке DNS: {ex.Message}");
                App.ShowErrorMessage(string.Format(App.GetResourceString("FailedToClearDns"), ex.Message));
            }
        }

        private async void FlushDnsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LogMessage("Очистка кэша DNS...");
                var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync("ipconfig", "/flushdns");

                if (exitCode != 0)
                    throw new InvalidOperationException("Не удалось очистить кэш DNS.");

                LogMessage("Кэш DNS успешно очищен.");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка при очистке кэша DNS: {ex.Message}");
                App.ShowErrorMessage(string.Format(App.GetResourceString("FailedToFlushDnsCache"), ex.Message));
            }
        }

        private static async Task SetDnsAsync(string interfaceName, string primaryDns, string secondaryDns)
        {
            if (string.IsNullOrWhiteSpace(primaryDns) || primaryDns == "Не выбрано")
                throw new ArgumentException("Основной DNS не указан.");

            var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync(
                "netsh",
                $"interface ip set dns name=\"{interfaceName}\" source=static address={primaryDns}");

            if (exitCode != 0)
                throw new InvalidOperationException($"Не удалось установить основной DNS: {output}");

            if (!string.IsNullOrEmpty(secondaryDns) && secondaryDns != "Не выбрано")
            {
                var (exitCode2, output2) = await ProcessHelper.RunProcessWithOutputAsync(
                    "netsh",
                    $"interface ip add dns name=\"{interfaceName}\" address={secondaryDns} index=2");

                if (exitCode2 != 0)
                    throw new InvalidOperationException($"Не удалось установить вторичный DNS: {output2}");
            }
        }

        private async Task ClearDnsAsync(string interfaceName)
        {
            var (exitCode, output) = await ProcessHelper.RunProcessWithOutputAsync(
                "netsh",
                $"interface ip set dns name=\"{interfaceName}\" source=dhcp");

            if (exitCode != 0)
                throw new InvalidOperationException($"Не удалось очистить DNS: {output}");
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                LogTextBox.ScrollToEnd();
            });
        }

        private bool IsValidIPAddress(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "Не выбрано")
                return false;

            // IPv4
            if (Regex.IsMatch(ipAddress, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
            {
                var parts = ipAddress.Split('.');
                foreach (var part in parts)
                {
                    if (!int.TryParse(part, out int num) || num < 0 || num > 255)
                        return false;
                }
                return true;
            }

            // IPv6
            if (Regex.IsMatch(ipAddress, @"^([0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}$"))
                return true;

            return false;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}