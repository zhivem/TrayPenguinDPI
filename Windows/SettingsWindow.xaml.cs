using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;
using AdonisUI.Controls;
using TrayPenguinDPI.Helpers;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;

namespace TrayPenguinDPI
{
    public partial class SettingsWindow : AdonisWindow
    {
        private static readonly string BlacklistPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", "Blacklist"));
        private static readonly string IpsetUrl = "https://raw.githubusercontent.com/zhivem/traypenguindpi/refs/heads/main/blacklist/ipset-unlock.txt";
        private static readonly string GeneralListUrl = "https://raw.githubusercontent.com/zhivem/traypenguindpi/refs/heads/main/blacklist/list-general.txt";
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
            string resourcesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", "Resources");
            var languageFiles = await Task.Run(() => Directory.GetFiles(resourcesPath, "Resources.*.xaml"));

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

        private async Task UpdateBlacklistsAsync()
        {
            Directory.CreateDirectory(BlacklistPath);
            using var client = new HttpClient();
            await File.WriteAllTextAsync(Path.Combine(BlacklistPath, "ipset-unlock.txt"), await client.GetStringAsync(IpsetUrl));
            await File.WriteAllTextAsync(Path.Combine(BlacklistPath, "list-general.txt"), await client.GetStringAsync(GeneralListUrl));
            MessageBox.Show(GetResourceString("BlacklistsUpdatedSuccess"), GetResourceString("SuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateDetailsSection()
        {
            var iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", "Config", "setting.ini");
            string iniContent = File.ReadAllText(iniPath);
            string zapretVersion = VersionHelper.GetVersionFromIni(iniContent, "ZapretVersion");
            string programVersion = VersionHelper.GetVersionFromIni(iniContent, "ProgramVersion");
            string configVersion = VersionHelper.GetVersionFromIni(iniContent, "ConfigVersion");

            if (Application.Current.Windows.OfType<UpdateManagerWindow>().FirstOrDefault() is UpdateManagerWindow updateWindow)
                zapretVersion = updateWindow.GetLatestZapretVersion() ?? zapretVersion;

            string appVersionTemplate = GetResourceString("ApplicationVersion") ?? "Application version: {0}";
            string configVersionTemplate = GetResourceString("Version") ?? "Version: {0}";
            string zapretInfoTemplate = GetResourceString("ZapretInfo") ?? "Zapret | Version: {0} | Author: bol-van |";

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

        private void UpdateThemeButtonState() => ToggleThemeButton.IsEnabled = !SyncThemeCheckBox.IsChecked.GetValueOrDefault();

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
            await UpdateBlacklistsAsync();
            UpdateBlacklistButton.IsEnabled = true;
        }

        private void OpenStrategFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string strategFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", "Strateg");
            Directory.CreateDirectory(strategFolderPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = strategFolderPath, UseShellExecute = true });
        }

        private void DnsButton_Click(object sender, RoutedEventArgs e)
        {
            var dnsWindow = new DnsSettingsWindow { Owner = this };
            dnsWindow.ShowDialog();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
            e.Handled = true;
        }

        private void OpenBlacklistFolderButton_Click(object sender, RoutedEventArgs e)
        {
            string blacklistFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", "Blacklist");
            Directory.CreateDirectory(blacklistFolderPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = blacklistFolderPath, UseShellExecute = true });
        }

        private async void CreateServiceButton_Click(object sender, RoutedEventArgs e)
        {
            string serviceName = "Zapret";
            string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program", "Zapret", "winws.exe");
            string args = App.GetCurrentStrategyArgs();
            string fullBinPath = $"\"{binPath}\" {args}";

            await ProcessHelper.CleanupProcessesAndServices();
            int createExitCode = await ProcessHelper.RunProcessAsync("sc.exe", $"create {serviceName} binPath= \"{fullBinPath}\" DisplayName= \"Zapret\" start= auto");

            if (createExitCode == 0)
            {
                await ProcessHelper.RunProcessAsync("sc.exe", $"description {serviceName} \"Zapret DPI bypass software\"");
                int startExitCode = await ProcessHelper.RunProcessAsync("sc.exe", $"start {serviceName}");
                MessageBox.Show(startExitCode != 0 ? GetResourceString("ServiceCreatedButNotStarted") : GetResourceString("ServiceCreatedSuccess"),
                    startExitCode != 0 ? GetResourceString("WarningTitle") : GetResourceString("SuccessTitle"),
                    MessageBoxButton.OK,
                    startExitCode != 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(GetResourceString("ServiceCreationFailed"), GetResourceString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteServiceButton_Click(object sender, RoutedEventArgs e)
        {
            await ProcessHelper.CleanupProcessesAndServices();
            MessageBox.Show(GetResourceString("ServiceDeletedSuccess"),
                GetResourceString("SuccessTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void UninstallButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                (string)FindResource("UninstallConfirmationMessage"),
                (string)FindResource("UninstallConfirmationTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Очистка процессов и служб с помощью ProcessHelper
                    await ProcessHelper.CleanupProcessesAndServices();
                    RegistrySettings.ClearAllSettings();

                    string appDir = AppDomain.CurrentDomain.BaseDirectory;
                    string appPath = Environment.ProcessPath;
                    string appFile = Path.GetFileName(appPath);
                    string programFolder = Path.Combine(appDir, "Program");
                    string batchPath = Path.Combine(Path.GetTempPath(), "uninstall_traypenguindpi.bat");

                    // Обновленный .bat-файл: удаляет только .exe и папку Program
                    string batchContent = $@"
                    @echo off
                    echo Waiting for process {appFile} to exit...
                    timeout /t 5 >nul

                    :waitloop
                    tasklist | find /i ""{appFile}"" >nul
                    if not errorlevel 1 (
                        echo Process is still running, forcing termination...
                        taskkill /f /im ""{appFile}"" >nul 2>&1
                        timeout /t 2 >nul
                        goto waitloop
                    )

                    echo Ensuring the process is terminated...
                    taskkill /f /im ""{appFile}"" >nul 2>&1

                    echo Deleting Program folder...
                    rd /s /q ""{programFolder}"" 2>nul
                    if exist ""{programFolder}"" (
                        echo Program folder could not be deleted. Retrying...
                        timeout /t 2 >nul
                        rd /s /q ""{programFolder}"" 2>nul
                    )

                    echo Deleting executable...
                    del /f /q ""{appPath}"" 2>nul
                    if exist ""{appPath}"" (
                        echo Executable could not be deleted. Retrying...
                        timeout /t 2 >nul
                        del /f /q ""{appPath}"" 2>nul
                    )

                    echo Deleting batch file...
                    del /q ""%~f0"" 2>nul
                    exit
                    ";

                    File.WriteAllText(batchPath, batchContent);

                    // Запуск .bat-файла с правами администратора
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = batchPath,
                        Arguments = "",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true
                    });

                    // Задержка перед завершением приложения
                    await Task.Delay(2000);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        string.Format((string)FindResource("UninstallErrorMessage"), ex.Message),
                        (string)FindResource("UninstallErrorTitle"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
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
            MessageBox.Show(GetResourceString("SettingsSaved"), GetResourceString("SuccessTitle"), AdonisUI.Controls.MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string GetResourceString(string key) => Application.Current.TryFindResource(key) as string ?? key;
    }
}