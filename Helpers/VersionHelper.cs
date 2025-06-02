using System;
using System.Text.RegularExpressions;

namespace TrayPenguinDPI.Helpers
{
    public static class VersionHelper
    {
        public static string GetVersionFromIni(string iniContent, string key)
        {
            string pattern = $@"{Regex.Escape(key)}\s*=\s*([^\r\n]+)";
            Match match = Regex.Match(iniContent, pattern);
            return match.Success ? match.Groups[1].Value.Trim() : "0.0";
        }

        public static int CompareVersions(string current, string latest)
        {
            Version v1 = ParseVersion(current) ?? new Version("0.0");
            Version v2 = ParseVersion(latest) ?? new Version("0.0");

            return v1.CompareTo(v2);
        }

        private static Version? ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version) || version == "Неизвестно")
                return new Version("0.0");

            try
            {
                string[] parts = version.Split('.');
                if (parts.Length < 2)
                    return new Version($"{version}.0");
                return new Version(version);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is InvalidOperationException)
            {
                return null;
            }
        }
    }
}