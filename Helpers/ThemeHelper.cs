using System.Windows;

namespace TrayPenguinDPI.Helpers
{
    public static class ThemeHelper
    {
        public static bool IsSystemThemeDark()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }

        public static void SwitchTheme(bool useDarkTheme)
        {
            var dicts = Application.Current.Resources.MergedDictionaries;
            var themeDict = dicts.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Program/Theme/") == true);
            if (themeDict != null) dicts.Remove(themeDict);
            dicts.Add(new ResourceDictionary { Source = new Uri(useDarkTheme ? "/Program/Theme/DarkTheme.xaml" : "/Program/Theme/LightTheme.xaml", UriKind.Relative) });

            foreach (Window window in Application.Current.Windows)
            {
                var windowDicts = window.Resources.MergedDictionaries;
                var windowThemeDict = windowDicts.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Program/Theme/") == true);
                if (windowThemeDict != null) windowDicts.Remove(windowThemeDict);
                windowDicts.Add(new ResourceDictionary { Source = new Uri(useDarkTheme ? "/Program/Theme/DarkTheme.xaml" : "/Program/Theme/LightTheme.xaml", UriKind.Relative) });
            }
        }
    }
}