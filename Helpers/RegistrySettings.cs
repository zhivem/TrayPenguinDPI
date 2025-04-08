using Microsoft.Win32;

namespace TreyPenguinDPI.Helpers
{
    public static class RegistrySettings
    {
        private const string RegistryAppPath = @"Software\TreyPenguinDPI";
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TreyPenguinDPI";

        public static T GetValue<T>(string keyName, T defaultValue)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryAppPath);
            return key != null && key.GetValue(keyName) is object value ? (T)Convert.ChangeType(value, typeof(T)) : defaultValue;
        }

        public static void SetValue(string keyName, object value)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryAppPath);
            key.SetValue(keyName, value);
        }

        public static bool IsAutoStartEnabled()
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, false);
            return key?.GetValue(AppName)?.ToString() == Environment.ProcessPath;
        }

        public static void SetAutoStart(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true) ?? Registry.CurrentUser.CreateSubKey(RegistryRunPath);
            if (enable && Environment.ProcessPath is string exePath)
                key.SetValue(AppName, exePath);
            else if (key.GetValue(AppName) != null)
                key.DeleteValue(AppName);
        }

        public static void ClearAllSettings()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryAppPath, true))
            {
                if (key != null)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(RegistryAppPath);
                }
            }

            using var runKey = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true);
            if (runKey != null && runKey.GetValue(AppName) != null)
            {
                runKey.DeleteValue(AppName);
            }
        }
    }
}