using System;
using System.Linq;
using UnityEngine;

namespace HalfAndHalf.Services
{
    [Serializable]
    public sealed class GoogleRankConfiguration
    {
        public string projectId;
        public LeaderboardMapping[] mappings;
        public bool HasProject => !string.IsNullOrEmpty(projectId) && projectId.All(c => c >= '0' && c <= '9') && projectId.Any(c => c != '0');
        public static GoogleRankConfiguration Load()
        {
            var asset = Resources.Load<TextAsset>("GooglePlayRanks");
            return asset == null ? null : JsonUtility.FromJson<GoogleRankConfiguration>(asset.text);
        }
        public LeaderboardCatalog CreateCatalog()
        {
            var active = (mappings ?? Array.Empty<LeaderboardMapping>()).Where(m => m != null && !string.IsNullOrWhiteSpace(m.leaderboardId)).ToArray();
            if (!HasProject || active.Length == 0) return null;
            // Play Console assigns Cgk…-style opaque board IDs. Never accept Apple bundle-style identifiers.
            if (active.Any(m => m.leaderboardId.Contains(".") || m.leaderboardId.Any(char.IsWhiteSpace))) return null;
            return new LeaderboardCatalog(active);
        }
    }
}
