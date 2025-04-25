using AdonisUI.Controls;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Windows;
using System.Windows.Navigation;
using TrayPenguinDPI.Helpers;

namespace TrayPenguinDPI
{
    public partial class UpdateManagerWindow : AdonisWindow
    {
        private string _latestZapretVersion = "Unknown";
        private string _latestConfigVersion = "Unknown";
        private string _latestProgramVersion = "Unknown";
        private bool _hasProgramUpdate;
        private bool _hasZapretUpdate;
        private bool _hasConfigUpdate;
        private readonly string _iniPath = Path.Combine(App.ConfigPath, "setting.ini");

        public UpdateManagerWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await CheckUpdatesAsync();
        }

        public string GetLatestZapretVersion() => _latestZapretVersion;

        private async Task CheckUpdatesAsync()
        {
            InstallButton.Visibility = Visibility.Collapsed;
            UpdateTextBox.Text = App.GetResourceString("CheckingUpdates");

            try
            {
                string iniContent = File.ReadAllText(_iniPath);
                string currentProgramVersion = VersionHelper.GetVersionFromIni(iniContent, "ProgramVersion");
                string currentZapretVersion = VersionHelper.GetVersionFromIni(iniContent, "ZapretVersion");
                string currentConfigVersion = VersionHelper.GetVersionFromIni(iniContent, "ConfigVersion");

                await FetchLatestVersionsAsync();
                UpdateTextBox.Text = BuildUpdateMessage(currentProgramVersion, currentZapretVersion, currentConfigVersion);
                InstallButton.Visibility = _hasProgramUpdate || _hasZapretUpdate || _hasConfigUpdate ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                UpdateTextBox.Text = $"Ошибка при проверке обновлений: {ex.Message}";
                InstallButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task FetchLatestVersionsAsync()
        {
            string iniContent = File.ReadAllText(_iniPath);
            string currentProgramVersion = VersionHelper.GetVersionFromIni(iniContent, "ProgramVersion") ?? "0.0";
            string currentZapretVersion = VersionHelper.GetVersionFromIni(iniContent, "ZapretVersion") ?? "0.0";
            string currentConfigVersion = VersionHelper.GetVersionFromIni(iniContent, "ConfigVersion") ?? "0.0";

            using var client = new HttpClient();
            string remoteIniContent = await client.GetStringAsync("https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/version/setting.ini");

            _latestZapretVersion = VersionHelper.GetVersionFromIni(remoteIniContent, "ZapretVersion") ?? "0.0";
            _latestConfigVersion = VersionHelper.GetVersionFromIni(remoteIniContent, "ConfigVersion") ?? "0.0";
            _latestProgramVersion = VersionHelper.GetVersionFromIni(remoteIniContent, "ProgramVersion") ?? "0.0";

            _hasProgramUpdate = VersionHelper.CompareVersions(currentProgramVersion, _latestProgramVersion) < 0;
            _hasZapretUpdate = VersionHelper.CompareVersions(currentZapretVersion, _latestZapretVersion) < 0;
            _hasConfigUpdate = VersionHelper.CompareVersions(currentConfigVersion, _latestConfigVersion) < 0;
        }

        private string BuildUpdateMessage(string currentProgram, string currentZapret, string currentConfig)
        {
            if (!_hasProgramUpdate && !_hasZapretUpdate && !_hasConfigUpdate)
                return App.GetResourceString("AllComponentsUpToDate");

            string message = "";
            if (_hasProgramUpdate) message += string.Format(App.GetResourceString("NewProgramVersionAvailable"), _latestProgramVersion, currentProgram) + "\n";
            if (_hasZapretUpdate) message += string.Format(App.GetResourceString("NewZapretVersionAvailable"), _latestZapretVersion, currentZapret) + "\n";
            if (_hasConfigUpdate) message += string.Format(App.GetResourceString("NewConfigVersionAvailable"), _latestConfigVersion, currentConfig) + "\n";
            return message.TrimEnd('\n');
        }

        private void UpdateConfiguration()
        {
            string iniContent = File.ReadAllText(_iniPath);
            var lines = iniContent.Split('\n').Select(line => line.Trim()).ToList();
            var updatedLines = new List<string>();
            bool inSettingsSection = false;

            foreach (var line in lines)
            {
                if (line.StartsWith("[Settings]"))
                {
                    inSettingsSection = true;
                    updatedLines.Add(line);
                    continue;
                }

                if (inSettingsSection)
                {
                    if (line.StartsWith("ProgramVersion="))
                        updatedLines.Add($"ProgramVersion={_latestProgramVersion}");
                    else if (line.StartsWith("ZapretVersion="))
                        updatedLines.Add($"ZapretVersion={_latestZapretVersion}");
                    else if (line.StartsWith("ConfigVersion="))
                        updatedLines.Add($"ConfigVersion={_latestConfigVersion}");
                    else
                        updatedLines.Add(line);
                }
                else
                {
                    updatedLines.Add(line);
                }
            }

            File.WriteAllLines(_iniPath, updatedLines);
        }

        private async Task DownloadAndExtractArchiveAsync(string url, string tempZipName, string targetDirName)
        {
            string tempZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tempZipName);
            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", targetDirName);

            try
            {
                await ProcessHelper.CleanupProcessesAndServicesAsync();

                using var client = new HttpClient();
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write))
                    await stream.CopyToAsync(fileStream);

                if (Directory.Exists(targetDir))
                {
                    try
                    {
                        Directory.Delete(targetDir, true);
                    }
                    catch (IOException ex)
                    {
                        UpdateTextBox.Text += $"\nНе удалось удалить папку {targetDirName}: {ex.Message}. Возможно, файлы используются.";
                        return;
                    }
                }

                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(tempZipPath, targetDir);
                File.Delete(tempZipPath);

                App.ResetRunningState();
            }
            catch (HttpRequestException ex)
            {
                UpdateTextBox.Text += $"\nОшибка загрузки {tempZipName}: {ex.Message}";
            }
            catch (IOException ex)
            {
                UpdateTextBox.Text += $"\nОшибка работы с файлами {tempZipName}: {ex.Message}";
            }
            catch (Exception ex)
            {
                UpdateTextBox.Text += $"\nНеизвестная ошибка при обновлении {targetDirName}: {ex.Message}";
            }
        }

        private async Task DownloadAndRunUpdateBat()
        {
            string batUrl = "https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/version/new_version.bat";
            string batPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "new_version.bat");

            using var client = new HttpClient();
            var response = await client.GetAsync(batUrl);
            response.EnsureSuccessStatusCode();
            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = new FileStream(batPath, FileMode.Create, FileAccess.Write))
                await stream.CopyToAsync(fileStream);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                CreateNoWindow = false,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            });
            Application.Current.Shutdown();
        }

        private async Task InstallComponentUpdates()
        {
            if (_hasZapretUpdate)
                await DownloadAndExtractArchiveAsync("https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/update/zapret.zip", "zapret_temp.zip", "Zapret");
            if (_hasConfigUpdate)
            {
                await DownloadAndExtractArchiveAsync("https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/update/config.zip", "config_temp.zip", "Strateg");
                App.LoadStrategies();
            }
            if (_hasZapretUpdate || _hasConfigUpdate)
                UpdateConfiguration();
            if (_hasProgramUpdate)
                await DownloadAndRunUpdateBat();
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasProgramUpdate && !_hasZapretUpdate && !_hasConfigUpdate) return;

            await InstallComponentUpdates();
            UpdateTextBox.Text = App.GetResourceString("UpdatesInstalled");
            InstallButton.Visibility = Visibility.Collapsed;
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync();

        private async void Reinstall_Click(object sender, RoutedEventArgs e)
        {
            if (AdonisUI.Controls.MessageBox.Show(App.GetResourceString("ConfirmReinstall"), App.GetResourceString("ConfirmReinstallTitle"), AdonisUI.Controls.MessageBoxButton.YesNo, AdonisUI.Controls.MessageBoxImage.Warning) != AdonisUI.Controls.MessageBoxResult.Yes)
                return;

            UpdateTextBox.Text = App.GetResourceString("Reinstalling");
            await Task.WhenAll(
                DownloadAndExtractArchiveAsync("https://github.com/zhivem/traypenguindpi/raw/refs/heads/main/update/zapret.zip", "zapret_temp.zip", "Zapret"),
                DownloadAndExtractArchiveAsync("https://github.com/zhivem/traypenguindpi/raw/refs/heads/main/update/config.zip", "config_temp.zip", "Strateg")
            );
            UpdateConfiguration();
            App.LoadStrategies();
            UpdateTextBox.Text = App.GetResourceString("ReinstallComplete");
            InstallButton.Visibility = Visibility.Collapsed;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }
    }
}