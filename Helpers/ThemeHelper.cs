using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace TrayPenguinDPI.Helpers
{
    public static class ThemeHelper
    {
        public static bool IsSystemThemeDark()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }

        public static void SwitchTheme(bool useDarkTheme)
        {
            var themeUri = useDarkTheme
                ? "/Program/Theme/DarkTheme.xaml"
                : "/Program/Theme/LightTheme.xaml";

            UpdateTheme(Application.Current.Resources.MergedDictionaries, themeUri);

            foreach (Window window in Application.Current.Windows)
            {
                UpdateTheme(window.Resources.MergedDictionaries, themeUri);
            }
        }

        private static void UpdateTheme(
            Collection<ResourceDictionary> dictionaries,
            string themeUri)
        {
            // Находим и удаляем старую тему
            var oldTheme = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.Contains("/Program/Theme/") == true);
            if (oldTheme != null) dictionaries.Remove(oldTheme);

            // Добавляем новую
            dictionaries.Add(new ResourceDictionary { Source = new Uri(themeUri, UriKind.Relative) });
        }
    }
}