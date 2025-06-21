using AdonisUI.Controls;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using TrayPenguinDPI.Helpers;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace TrayPenguinDPI
{
    public partial class SettingsWindow : AdonisWindow
    {
        private void UpdateThemeButtonState() => ToggleThemeButton.IsEnabled = !SyncThemeCheckBox.IsChecked.GetValueOrDefault();
        private bool _isDarkTheme = false;

        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadLanguagesAsync();
            LoadWindowSettings();
            UpdateDetailsSection();
        }

        private async Task LoadLanguagesAsync()
        {
            LanguageComboBox.Items.Clear();
            var languageFiles = await Task.Run(() => Directory.GetFiles(App.ResourcesPath, "Resources.*.xaml"));

            foreach (string file in languageFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string cultureCode = fileName.Replace("Resources.", "");
                if (!string.IsNullOrEmpty(cultureCode))
                {
                    LanguageComboBox.Items.Add(new ComboBoxItem
                    {
                        Content = cultureCode == "ru" ? "Русский" : "English",
                        Tag = cultureCode
                    });
                }
            }

            string savedLanguage = RegistrySettings.GetValue("Language", "ru");
            var selectedItem = LanguageComboBox.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == savedLanguage) ?? LanguageComboBox.Items[0] as ComboBoxItem;
            LanguageComboBox.SelectedItem = selectedItem;
        }

        private void LoadWindowSettings()
        {
            AutoStartCheckBox.IsChecked = RegistrySettings.IsAutoStartEnabled();
            LastConfigCheckBox.IsChecked = RegistrySettings.GetValue("UseLastConfig", false);
            DisableNotificationsCheckBox.IsChecked = !RegistrySettings.GetValue("NotificationsEnabled", true);
            UpdateBlacklistCheckBox.IsChecked = RegistrySettings.GetValue("UpdateBlacklistOnStartup", false);
            SyncThemeCheckBox.IsChecked = RegistrySettings.GetValue("SyncThemeWithSystem", false);
            _isDarkTheme = RegistrySettings.GetValue("CurrentTheme", "Light") == "Dark";
            App.SetCurrentStrategyIndex(RegistrySettings.GetValue("LastStrategyIndex", 0));
            UpdateThemeButtonState();
        }

        private void SaveSettings()
        {
            bool autoStart = AutoStartCheckBox.IsChecked ?? false;
            bool lastConfig = LastConfigCheckBox.IsChecked ?? false;
            bool notificationsDisabled = DisableNotificationsCheckBox.IsChecked ?? false;
            bool updateBlacklist = UpdateBlacklistCheckBox.IsChecked ?? false;
            bool syncTheme = SyncThemeCheckBox.IsChecked ?? false;
            string cultureCode = (LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ru";
            string savedLanguage = RegistrySettings.GetValue("Language", "ru");

            RegistrySettings.SetAutoStart(autoStart);
            RegistrySettings.SetValue("UseLastConfig", lastConfig);
            RegistrySettings.SetValue("NotificationsEnabled", !notificationsDisabled);
            RegistrySettings.SetValue("UpdateBlacklistOnStartup", updateBlacklist);
            RegistrySettings.SetValue("SyncThemeWithSystem", syncTheme);
            RegistrySettings.SetValue("CurrentTheme", _isDarkTheme ? "Dark" : "Light");
            RegistrySettings.SetValue("LastStrategyIndex", lastConfig ? App.GetCurrentStrategyIndex() : 0);
            RegistrySettings.SetValue("Language", cultureCode);

            App.NotificationsEnabled = !notificationsDisabled;
            if (syncTheme && ThemeHelper.IsSystemThemeDark() != _isDarkTheme)
            {
                _isDarkTheme = ThemeHelper.IsSystemThemeDark();
                ThemeHelper.SwitchTheme(_isDarkTheme);
            }
            else
            {
                ThemeHelper.SwitchTheme(_isDarkTheme);
            }

            if (cultureCode != savedLanguage)
                App.SwitchLanguage(cultureCode);
        }

        private void UpdateDetailsSection()
        {
            var iniPath = Path.Combine(App.ConfigPath, "setting.ini");
            string iniContent = File.ReadAllText(iniPath);
            string zapretVersion = VersionHelper.GetVersionFromIni(iniContent, "ZapretVersion");
            string programVersion = VersionHelper.GetVersionFromIni(iniContent, "ProgramVersion");
            string configVersion = VersionHelper.GetVersionFromIni(iniContent, "ConfigVersion");

            if (Application.Current.Windows.OfType<UpdateManagerWindow>().FirstOrDefault() is UpdateManagerWindow updateWindow)
                zapretVersion = updateWindow.GetLatestZapretVersion() ?? zapretVersion;

            string appVersionTemplate = App.GetResourceString("ApplicationVersion") ?? "Application version: {0}";
            string configVersionTemplate = App.GetResourceString("Version") ?? "Version: {0}";
            string zapretInfoTemplate = App.GetResourceString("ZapretInfo") ?? "Zapret | Version: {0} | Author: bol-van |";

            this.DataContext = new
            {
                ApplicationVersion = string.Format(appVersionTemplate, programVersion),
                ConfigVersion = string.Format(configVersionTemplate, configVersion),
                ZapretInfo = string.Format(zapretInfoTemplate, zapretVersion)
            };

            if (ZapretVersionTextBlock != null)
            {
                ZapretVersionTextBlock.Inlines.Clear();
                ZapretVersionTextBlock.Inlines.Add(new Run(string.Format(zapretInfoTemplate, zapretVersion)));
                var hyperlink = new Hyperlink(new Run(" GitHub")) { NavigateUri = new Uri("https://github.com/bol-van/") };
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                ZapretVersionTextBlock.Inlines.Add(hyperlink);
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                string cultureCode = selectedItem.Tag?.ToString() ?? "ru";
                App.SwitchLanguage(cultureCode);
                UpdateDetailsSection();
            }
        }

        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            if (SyncThemeCheckBox.IsChecked == true) return;
            _isDarkTheme = !_isDarkTheme;
            ThemeHelper.SwitchTheme(_isDarkTheme);
        }

        private void SyncThemeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            bool isSyncEnabled = SyncThemeCheckBox.IsChecked ?? false;
            UpdateThemeButtonState();
            if (isSyncEnabled && ThemeHelper.IsSystemThemeDark() != _isDarkTheme)
            {
                _isDarkTheme = ThemeHelper.IsSystemThemeDark();
                ThemeHelper.SwitchTheme(_isDarkTheme);
            }
        }

        private async void UpdateBlacklistButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateBlacklistButton.IsEnabled = false;
            await App.UpdateBlacklistsAsync();
            UpdateBlacklistButton.IsEnabled = true;
        }

        private void OpenStrategFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(App.StrategiesPath);
            Process.Start(new ProcessStartInfo { FileName = App.StrategiesPath, UseShellExecute = true });
        }

        private void DnsButton_Click(object sender, RoutedEventArgs e)
        {
            var dnsWindow = new DnsSettingsWindow { Owner = this };
            dnsWindow.ShowDialog();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }

        private void OpenBlacklistFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Directory.CreateDirectory(App.BlacklistPath);
            Process.Start(new ProcessStartInfo { FileName = App.BlacklistPath, UseShellExecute = true });
        }

        private async void CreateServiceButton_Click(object sender, RoutedEventArgs e)
        {
            string serviceName = "Zapret";
            string binPath = Path.Combine(App.ZapretPath, "winws.exe");
            string args = App.GetCurrentStrategyArgs();
            string fullBinPath = $"\"{binPath}\" {args}";

            await ProcessHelper.CleanupProcessesAndServicesAsync();
            int createExitCode = await ProcessHelper.RunProcessAsync("sc.exe", $"create {serviceName} binPath= \"{fullBinPath}\" DisplayName= \"Zapret\" start= auto");

            if (createExitCode == 0)
            {
                await ProcessHelper.RunProcessAsync("sc.exe", $"description {serviceName} \"Zapret DPI bypass software\"");
                int startExitCode = await ProcessHelper.RunProcessAsync("sc.exe", $"start {serviceName}");
                App.ShowMessage(startExitCode != 0 ? App.GetResourceString("ServiceCreatedButNotStarted") : App.GetResourceString("ServiceCreatedSuccess"),
                    startExitCode != 0 ? App.GetResourceString("WarningTitle") : App.GetResourceString("SuccessTitle"),
                    startExitCode != 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            else
            {
                App.ShowErrorMessage(App.GetResourceString("ServiceCreationFailed"));
            }
        }

        private async void DeleteServiceButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessHelper.CleanupProcessesAndServicesAsync();
            App.ShowMessage(App.GetResourceString("ServiceDeletedSuccess"),
                App.GetResourceString("SuccessTitle"),
                MessageBoxImage.Information);
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var result = AdonisUI.Controls.MessageBox.Show(
                (string)FindResource("UninstallConfirmationMessage"),
                (string)FindResource("UninstallConfirmationTitle"),
                AdonisUI.Controls.MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await ProcessHelper.CleanupProcessesAndServicesAsync();
                    RegistrySettings.ClearAllSettings();

                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string? appPath = Environment.ProcessPath;

                    if (string.IsNullOrEmpty(appPath))
                    {
                        App.ShowErrorMessage("Failed to determine executable path.");
                        return;
                    }

                    string batchUrl = "https://raw.githubusercontent.com/zhivem/TrayPenguinDPI/refs/heads/master/version/uninstall.bat";
                    string batchPath = Path.Combine(appDir, "uninstall.bat");

                    // Скачиваем bat-файл
                    using var client = new HttpClient();
                    var response = await client.GetAsync(batchUrl);
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(batchPath, FileMode.Create, FileAccess.Write))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    // Запуск bat-файла с правами администратора
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batchPath,
                        UseShellExecute = true,
                        Verb = "runas", // Запуск от имени администратора
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = appDir
                    });

                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    App.ShowErrorMessage(string.Format((string)FindResource("UninstallErrorMessage"), ex.Message));
                }
            }
        }

        private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            new UpdateManagerWindow().ShowDialog();
            UpdateDetailsSection();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            App.ShowMessage(App.GetResourceString("SettingsSaved"), App.GetResourceString("SuccessTitle"), MessageBoxImage.Information);
        }
    }
}