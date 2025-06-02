using System;
using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace TrayPenguinDPI.Helpers
{
    public static class ResourceHelper
    {
        static ResourceHelper()
        {
            if (!UriParser.IsKnownScheme("pack"))
            {
                // Запуск регистрации схемы "pack://" через typeof для совместимости
                _ = typeof(System.IO.Packaging.PackUriHelper);
            }
        }

        public static Stream GetStream(string uriString)
        {
            Uri uri = new(uriString);
            StreamResourceInfo? info = Application.GetResourceStream(uri);

            if (info?.Stream is { } stream)
            {
                return stream;
            }

            throw new FileNotFoundException($"Resource not found: {uriString}");
        }
    }
}