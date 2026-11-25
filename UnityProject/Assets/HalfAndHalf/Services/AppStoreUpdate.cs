using System;
using System.Globalization;
using UnityEngine;

namespace HalfAndHalf.Services
{
    public static class AppStoreUpdate
    {
        public const long AppId = 6821765037;
        public const string BundleId = "com.sungsoo.halfandhalf.mvp";
        public const string StoreUrl = "https://apps.apple.com/app/id6821765037";
        [Serializable] sealed class Lookup { public int resultCount; public Listing[] results; }
        [Serializable] sealed class Listing { public long trackId; public string bundleId, version; }

        public static string AvailableVersion(string response, string installed)
        {
            if (string.IsNullOrEmpty(response) || !TryVersion(installed, out var current)) return null;
            try
            {
                var lookup = JsonUtility.FromJson<Lookup>(response);
                if (lookup == null || lookup.resultCount < 1 || lookup.results == null) return null;
                foreach (var item in lookup.results)
                    if (item != null && item.trackId == AppId && item.bundleId == BundleId &&
                        TryVersion(item.version, out var published) && published > current)
                        return item.version;
            }
            catch (ArgumentException) { }
            return null;
        }

        static bool TryVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split('.');
            if (parts.Length < 1 || parts.Length > 3) return false;
            var values = new int[3];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out values[i])) return false;
            version = new Version(values[0], values[1], values[2]);
            return true;
        }
    }
}
