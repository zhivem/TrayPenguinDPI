using System.Text.RegularExpressions;

namespace TreyPenguinDPI.Helpers
{
    public static class VersionHelper
    {
        public static string GetVersionFromIni(string iniContent, string key)
        {
            string pattern = $@"{key}\s*=\s*([^\r\n]+)";
            return Regex.Match(iniContent, pattern) is { Success: true } match ? match.Groups[1].Value.Trim() : "0.0";
        }

        public static int CompareVersions(string current, string latest)
        {
            
            string NormalizeVersion(string version)
            {
                if (string.IsNullOrWhiteSpace(version) || version == "Неизвестно")
                    return "0.0";

                if (!version.Contains("."))
                    return version + ".0";

                return version;
            }

            try
            {
                Version v1 = new Version(NormalizeVersion(current));
                Version v2 = new Version(NormalizeVersion(latest));
                return v1.CompareTo(v2);
            }
            catch (ArgumentException)
            {
                return -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}