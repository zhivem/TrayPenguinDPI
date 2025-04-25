using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using TrayPenguinDPI.Helpers;
using NotifyIcon = NotifyIconEx.NotifyIcon;

namespace TrayPenguinDPI
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = "TrayPenguinDPIMutex";
        private const string IpsetUrl = "https://raw.githubusercontent.com/zhivem/TrayPenguinDPI/refs/heads/master/blacklist/ipset-unlock.txt";
        private const string GeneralListUrl = "https://raw.githubusercontent.com/zhivem/TrayPenguinDPI/refs/heads/master/blacklist/list-general.txt";
        private const string VersionUrl = "https://github.com/zhivem/TrayPenguinDPI/raw/refs/heads/master/version/setting.ini";

        public static readonly string ProgramPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Program");
        public static readonly string ZapretPath = Path.Combine(ProgramPath, "Zapret");
        public static readonly string BlacklistPath = Path.Combine(ProgramPath, "Blacklist");
        public static readonly string StrategiesPath = Path.Combine(ProgramPath, "Strateg");
        public static readonly string ConfigPath = Path.Combine(ProgramPath, "Config");
        public static readonly string ResourcesPath = Path.Combine(ProgramPath, "Resources");
        public static readonly string AssetsImagesPath = Path.Combine(ProgramPath, "Assets", "Images");

        private static readonly Dictionary<string, string> _pathReplacements = new() { { "{ZAPRET}", ZapretPath }, { "{BLACKLIST}", BlacklistPath } };
        private static readonly Regex _replacementRegex = new(string.Join("|", _pathReplacements.Keys.Select(Regex.Escape)));
        private static readonly object _processLock = new();

        private static NotifyIcon? trayIcon;
        private static readonly List<Process> _processList = [];
        private static readonly List<string> _strategyExecutables = [];
        public static readonly List<string> _strategyArgs = [];
        private static List<string> _strategyNames = [];
        private static readonly List<ToolStripMenuItem> _strategyMenuItems = [];
        private static ToolStripMenuItem? _startItem;
        private static ToolStripMenuItem? _stopItem;
        private static ToolStripMenuItem? _strategiesMenu;
        private static SettingsWindow? _activeSettingsWindow;
        private static Mutex? _mutex;
        private static bool _isRunning;
        private static int _currentStrategyIndex;
        private static readonly Queue<(string title, string message)> _notificationQueue = new();
        private static bool _isShowingNotification = false;

        public static bool NotificationsEnabled { get; set; } = true;

        public static int GetCurrentStrategyIndex() => _currentStrategyIndex;

        public static void SetCurrentStrategyIndex(int index)
        {
            if (index >= 0 && index < _strategyExecutables.Count)
                _currentStrategyIndex = index;
        }

        public static string GetCurrentStrategyArgs() => _strategyArgs.Count > _currentStrategyIndex ? _strategyArgs[_currentStrategyIndex] : string.Empty;

        public static string GetResourceString(string key) => Current.TryFindResource(key) as string ?? key;

        public static void ShowErrorMessage(string message, string title = null)
        {
            AdonisUI.Controls.MessageBox.Show(
                message.Replace(". ", ".\n"),
                title ?? GetResourceString("ErrorTitle"),
                AdonisUI.Controls.MessageBoxButton.OK,
                AdonisUI.Controls.MessageBoxImage.Error);
        }

        public static void ShowWarningMessage(string message, string title = null)
        {
            AdonisUI.Controls.MessageBox.Show(
                message.Replace(". ", ".\n"),
                title ?? GetResourceString("WarningTitle"),
                AdonisUI.Controls.MessageBoxButton.OK,
                AdonisUI.Controls.MessageBoxImage.Warning);
        }

        public static void ShowMessage(string message, string title, AdonisUI.Controls.MessageBoxImage image)
        {
            AdonisUI.Controls.MessageBox.Show(
                message.Replace(". ", ".\n"),
                title,
                AdonisUI.Controls.MessageBoxButton.OK,
                image);
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            if (!EnsureSingleInstance() || !EnsureAdminRights(e.Args))
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            string language = RegistrySettings.GetValue("Language", "ru");
            LoadSettings();
            SwitchLanguage(language);
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            LoadStrategies();
            InitializeTrayIcon();
            await HandleStartupTasks();
            UpdateMenuState();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }

        public static void SwitchLanguage(string cultureCode)
        {
            string resourcePath = Path.Combine(ResourcesPath, $"Resources.{cultureCode}.xaml");

            if (!File.Exists(resourcePath))
            {
                cultureCode = "ru";
                resourcePath = Path.Combine(ResourcesPath, $"Resources.{cultureCode}.xaml");
            }

            var mergedDictionaries = Current.Resources.MergedDictionaries;
            var adonisDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("AdonisUI") == true);
            var themeDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Program/Theme/") == true);
            var langDict = mergedDictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("Resources.") == true);
            bool isDarkTheme = themeDict?.Source?.OriginalString.Contains("DarkTheme") == true;

            if (langDict != null)
                mergedDictionaries.Remove(langDict);

            var newDict = new ResourceDictionary { Source = new Uri(resourcePath, UriKind.Absolute) };
            mergedDictionaries.Add(newDict);

            if (adonisDict != null && !mergedDictionaries.Contains(adonisDict))
                mergedDictionaries.Insert(0, adonisDict);
            if (themeDict != null && !mergedDictionaries.Contains(themeDict))
                mergedDictionaries.Add(themeDict);
            else
                ThemeHelper.SwitchTheme(isDarkTheme);

            if (trayIcon != null)
            {
                trayIcon.Dispose();
                ((App)Current).InitializeTrayIcon();
            }

            foreach (Window window in Current.Windows)
            {
                var windowDicts = window.Resources.MergedDictionaries;
                windowDicts.Clear();
                if (adonisDict != null) windowDicts.Add(adonisDict);
                windowDicts.Add(new ResourceDictionary { Source = new Uri(isDarkTheme ? "/Program/Theme/DarkTheme.xaml" : "/Program/Theme/LightTheme.xaml", UriKind.Relative) });
                windowDicts.Add(newDict);
            }
        }

        private bool EnsureSingleInstance()
        {
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                ShowMessage(GetResourceString("ApplicationAlreadyRunning") ?? "TrayPenguinDPI is already running.",
                    GetResourceString("InformationTitle") ?? "Information",
                    AdonisUI.Controls.MessageBoxImage.Information);
            }
            return isNewInstance;
        }

        private bool EnsureAdminRights(string[] args)
        {
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                return true;

            Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = Environment.ProcessPath,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory,
                Arguments = string.Join(" ", args)
            });
            return false;
        }

        private void LoadSettings()
        {
            NotificationsEnabled = RegistrySettings.GetValue("NotificationsEnabled", true);
            bool syncTheme = RegistrySettings.GetValue("SyncThemeWithSystem", false);
            bool isDarkTheme = RegistrySettings.GetValue("CurrentTheme", "Light") == "Dark";
            bool useLastConfig = RegistrySettings.GetValue("UseLastConfig", false);
            _currentStrategyIndex = RegistrySettings.GetValue("LastStrategyIndex", 0);

            ThemeHelper.SwitchTheme(syncTheme ? ThemeHelper.IsSystemThemeDark() : isDarkTheme);
            if (useLastConfig)
                _currentStrategyIndex = Math.Min(_currentStrategyIndex, _strategyExecutables.Count - 1);
        }

        private void InitializeTrayIcon()
        {
            var mainModule = Process.GetCurrentProcess().MainModule;
            trayIcon = new NotifyIcon
            {
                Text = "TrayPenguinDPI Control",
                Icon = mainModule != null ? Icon.ExtractAssociatedIcon(mainModule.FileName) ?? SystemIcons.Application : SystemIcons.Application,
                Visible = true
            };

            _startItem = (ToolStripMenuItem)trayIcon.AddMenu(GetResourceString("Start"), LoadImageFromResource("start.png"), StartZapret_Click);
            _stopItem = (ToolStripMenuItem)trayIcon.AddMenu(GetResourceString("Stop"), LoadImageFromResource("stop.png"), StopZapret_Click);
            trayIcon.AddMenu("-");

            _strategiesMenu = (ToolStripMenuItem)trayIcon.AddMenu(GetResourceString("Configurations"), null, []);
            for (int i = 0; i < _strategyNames.Count; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem(_strategyNames[i], null, (_, _) => SelectStrategy_Click(index)) { Tag = index, Checked = i == _currentStrategyIndex };
                _strategyMenuItems.Add(item);
                _strategiesMenu.DropDownItems.Add(item);
            }

            trayIcon.AddMenu(GetResourceString("Settings"), null, Settings_Click);
            trayIcon.AddMenu("-");
            trayIcon.AddMenu(GetResourceString("Exit"), null, Exit_Click);
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            UpdateMenuState();
        }

        private static async Task HandleStartupTasks()
        {
            if (RegistrySettings.GetValue("UpdateBlacklistOnStartup", false))
                await UpdateBlacklistsAsync();

            try
            {
                if (await CheckForUpdatesAsync())
                {
                    var updateWindow = new UpdateManagerWindow();
                    updateWindow.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Update check failed: {ex.Message}");
            }

            if (RegistrySettings.GetValue("UseLastConfig", false) && _strategyExecutables.Count > 0)
            {
                UpdateStrategyMenuState();
                StartZapret_Click(null, null);
            }
        }

        public static async Task UpdateBlacklistsAsync()
        {
            Directory.CreateDirectory(BlacklistPath);
            using var client = new HttpClient();
            await File.WriteAllTextAsync(Path.Combine(BlacklistPath, "ipset-unlock.txt"), await client.GetStringAsync(IpsetUrl));
            await File.WriteAllTextAsync(Path.Combine(BlacklistPath, "list-general.txt"), await client.GetStringAsync(GeneralListUrl));
            if (NotificationsEnabled)
                ShowNotification("TrayPenguinDPI", "BlacklistsUpdated");
        }

        private static async Task<bool> CheckForUpdatesAsync()
        {
            var iniPath = Path.Combine(ConfigPath, "setting.ini");
            string iniContent = File.ReadAllText(iniPath);
            string currentProgramVersion = VersionHelper.GetVersionFromIni(iniContent, "ProgramVersion") ?? "0.0";
            string currentZapretVersion = VersionHelper.GetVersionFromIni(iniContent, "ZapretVersion") ?? "0.0";
            string currentConfigVersion = VersionHelper.GetVersionFromIni(iniContent, "ConfigVersion") ?? "0.0";

            using var client = new HttpClient();
            string remoteIniContent = await client.GetStringAsync(VersionUrl);

            string latestZapretVersion = VersionHelper.GetVersionFromIni(remoteIniContent, "ZapretVersion") ?? "0.0";
            string latestConfigVersion = VersionHelper.GetVersionFromIni(remoteIniContent, "ConfigVersion") ?? "0.0";
            string latestProgramVersion = VersionHelper.GetVersionFromIni(remoteIniContent, "ProgramVersion") ?? "0.0";

            return VersionHelper.CompareVersions(currentProgramVersion, latestProgramVersion) < 0 ||
                   VersionHelper.CompareVersions(currentZapretVersion, latestZapretVersion) < 0 ||
                   VersionHelper.CompareVersions(currentConfigVersion, latestConfigVersion) < 0;
        }

        public static void LoadStrategies()
        {
            _strategyExecutables.Clear();
            _strategyArgs.Clear();
            _strategyNames.Clear();

            foreach (string file in Directory.GetFiles(StrategiesPath, "*.ini"))
                ParseStrategyFile(file);
        }

        private static Bitmap LoadImageFromResource(string fileName)
        {
            string fullPath = Path.Combine(AssetsImagesPath, fileName);
            return File.Exists(fullPath) ? new Bitmap(fullPath) : new Bitmap(16, 16);
        }

        public static void ShowNotification(string title, string textKey, params object[] args)
        {
            if (!NotificationsEnabled) return;

            string text = Current.TryFindResource(textKey) as string ?? textKey;
            if (args.Length > 0)
                text = string.Format(text, args);

            if (_isShowingNotification)
            {
                _notificationQueue.Enqueue((title, text));
                return;
            }

            try
            {
                _isShowingNotification = true;
                var notification = new NotificationWindow(title, text, 2000);
                notification.Closed += (s, e) =>
                {
                    _isShowingNotification = false;
                    if (_notificationQueue.Count > 0)
                    {
                        var next = _notificationQueue.Dequeue();
                        ShowNotification(next.title, next.message);
                    }
                };
                notification.Show();
            }
            catch
            {
            }
        }

        public static string ReplacePaths(string input)
        {
            return _replacementRegex.Replace(input, match => _pathReplacements[match.Value]);
        }

        private static void ParseStrategyFile(string file)
        {
            string[] lines = File.ReadAllLines(file);
            string strategyName = "Unknown";
            string executable = "";
            var argsBuilder = new StringBuilder();
            bool inArgsSection = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    strategyName = trimmed[1..^1];
                else if (trimmed.StartsWith("executable") && trimmed.IndexOf('=') is int eIdx && eIdx > -1)
                    executable = trimmed[(eIdx + 1)..].Trim();
                else if (trimmed.StartsWith("args"))
                {
                    inArgsSection = true;
                    if (trimmed.IndexOf('=') is int aIdx && aIdx > -1)
                        argsBuilder.Append(trimmed[(aIdx + 1)..].Trim());
                }
                else if (inArgsSection && !string.IsNullOrEmpty(trimmed))
                    argsBuilder.Append(trimmed);
            }

            string args = argsBuilder.ToString().Replace(";", " ").Trim();
            if (!string.IsNullOrEmpty(executable) && !string.IsNullOrEmpty(args))
            {
                _strategyExecutables.Add(executable);
                _strategyArgs.Add(args);
                _strategyNames.Add(strategyName);
            }
        }

        private static async Task CleanupProcessesAsync()
        {
            lock (_processLock)
            {
                if (_isRunning && _processList.Count > 0)
                {
                    _processList.ForEach(p =>
                    {
                        if (p?.HasExited == false)
                            p.Kill();
                    });
                    _processList.Clear();
                    _isRunning = false;
                }
            }

            await ProcessHelper.CleanupProcessesAndServicesAsync();
        }

        public static void ResetRunningState()
        {
            lock (_processLock)
            {
                _processList.Clear();
                _isRunning = false;
            }
            UpdateMenuState();
            UpdateStrategyMenuState();
            ShowNotification("TrayPenguinDPI", "ZapretStopped");
        }

        private static async void StartZapret_Click(object? sender, EventArgs? e)
        {
            if (_isRunning) return;

            await ProcessHelper.CleanupProcessesAndServicesAsync();

            string executable = ReplacePaths(_strategyExecutables[_currentStrategyIndex]);
            if (!File.Exists(executable))
            {
                ShowErrorMessage($"File {executable} not found.");
                return;
            }

            string args = ReplacePaths(_strategyArgs[_currentStrategyIndex]);

            try
            {
                Process process = ProcessHelper.StartProcess(executable, args);
                lock (_processLock)
                {
                    _processList.Add(process);
                    _isRunning = true;
                }
                ShowNotification("TrayPenguinDPI", "ZapretStarted", _strategyNames[_currentStrategyIndex]);
                UpdateStrategyMenuState();
                _ = StartProcessMonitoring();
                UpdateMenuState();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Error: {ex.Message}");
                lock (_processLock)
                {
                    _isRunning = false;
                }
                UpdateMenuState();
            }
        }

        private static async void StopZapret_Click(object? sender, EventArgs? e)
        {
            if (!_isRunning || !_processList.Any()) return;

            lock (_processLock)
            {
                _processList.ForEach(p => p?.Kill());
                _processList.Clear();
                _isRunning = false;
            }

            await ProcessHelper.CleanupProcessesAndServicesAsync();

            ShowNotification("TrayPenguinDPI", "ZapretStopped");
            UpdateStrategyMenuState();
            UpdateMenuState();
        }

        private static void TrayIcon_DoubleClick(object? sender, EventArgs e)
        {
            if (_isRunning)
                StopZapret_Click(sender, e);
            else
                StartZapret_Click(sender, e);
        }

        private static void SelectStrategy_Click(int strategyIndex)
        {
            _currentStrategyIndex = strategyIndex;
            UpdateStrategyMenuState();
            ShowNotification("TrayPenguinDPI", "StrategySelected", _strategyNames[_currentStrategyIndex]);
        }

        public static void Settings_Click(object? sender, EventArgs? e)
        {
            if (_activeSettingsWindow?.IsLoaded == true)
            {
                _activeSettingsWindow.Activate();
                return;
            }

            _activeSettingsWindow = new SettingsWindow();
            _activeSettingsWindow.Closed += (_, _) => _activeSettingsWindow = null;
            _activeSettingsWindow.Show();
        }

        private static async void Exit_Click(object? sender, EventArgs? e)
        {
            await CleanupProcessesAsync();
            trayIcon?.Dispose();
            Current.Shutdown();
        }

        private static async Task StartProcessMonitoring(CancellationToken token = default)
        {
            while (_processList.Count > 0 && !token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(2), token);
                lock (_processLock)
                {
                    if (_processList.Any(p => p?.HasExited ?? true))
                    {
                        StopZapret_Click(null, null);
                        ShowNotification("TrayPenguinDPI", "ProcessTerminated");
                        StartZapret_Click(null, null);
                        break;
                    }
                }
            }
        }

        private static void UpdateMenuState()
        {
            if (_startItem != null) _startItem.Enabled = !_isRunning;
            if (_stopItem != null) _stopItem.Enabled = _isRunning;
        }

        private static void UpdateStrategyMenuState()
        {
            foreach (var item in _strategyMenuItems)
                if (item.Tag is int tag)
                    item.Checked = tag == _currentStrategyIndex;
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General) return;
            if (RegistrySettings.GetValue("SyncThemeWithSystem", false))
            {
                bool isDark = ThemeHelper.IsSystemThemeDark();
                ThemeHelper.SwitchTheme(isDark);
                RegistrySettings.SetValue("CurrentTheme", isDark ? "Dark" : "Light");
            }
        }
    }
}