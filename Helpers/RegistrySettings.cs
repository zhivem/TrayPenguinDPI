using Microsoft.Win32;
using System;

namespace TrayPenguinDPI.Helpers
{
    public static class RegistrySettings
    {
        private const string RegistryAppPath = @"Software\TreyPenguinDPI";
        private const string RegistryRunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "TreyPenguinDPI";

        public static T GetValue<T>(string keyName, T defaultValue)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryAppPath);
            if (key?.GetValue(keyName) is object value && value is not null)
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }

            return defaultValue;
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
            const bool writable = true;
            using var key = Registry.CurrentUser.CreateSubKey(RegistryRunPath, writable);
            if (enable && Environment.ProcessPath is string exePath)
            {
                key.SetValue(AppName, exePath);
            }
            else
            {
                if (key.GetValue(AppName) != null)
                    key.DeleteValue(AppName);
            }
        }

        public static void ClearAllSettings()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(RegistryAppPath);
            }
            catch { /* Ignore if key doesn't exist */ }

            using var runKey = Registry.CurrentUser.OpenSubKey(RegistryRunPath, true);
            if (runKey?.GetValue(AppName) != null)
            {
                runKey.DeleteValue(AppName);
            }
        }
    }
}