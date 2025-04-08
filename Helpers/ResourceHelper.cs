using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace TreyPenguinDPI.Helpers
{
    public static class ResourceHelper
    {
        static ResourceHelper()
        {
            if (!UriParser.IsKnownScheme("pack"))
                _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        }

        public static Stream GetStream(string uriString)
        {
            Uri uri = new(uriString);
            StreamResourceInfo info = Application.GetResourceStream(uri);
            return info?.Stream ?? throw new FileNotFoundException($"Resource not found: {uriString}");
        }
    }
}