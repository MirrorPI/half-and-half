using System;
using System.Linq;
using UnityEngine;

namespace HalfAndHalf.Services
{
    public sealed class GameCenterConfiguration : ScriptableObject
    {
        // Enable only after these IDs exist in this app's App Store Connect Game Center configuration.
        public bool EnableOnline;
        public bool EnableOverallAfterServiceVerification;
        public bool EnableCurrentOverallAfterServiceVerification;
        public bool OverallReady(System.Collections.Generic.IReadOnlyList<Core.ObjectDefinition> content, LeaderboardCatalog catalog=null)
        {
            string id=Core.OverallRecord.IdFor(content.Count);
            bool verified=id==Core.OverallRecord.CurrentId ? EnableCurrentOverallAfterServiceVerification : EnableOverallAfterServiceVerification;
            if(!EnableOnline||!verified||id==null)return false;
            catalog??=PlayableCatalog(true);
            foreach(var d in content)if(catalog.Resolve(d.Id,d.ContentVersion)==null)return false;
            return catalog.Resolve(id,Core.OverallRecord.VersionFor(content))!=null;
        }
        public LeaderboardMapping[] Mappings=Array.Empty<LeaderboardMapping>();
        public LeaderboardCatalog Catalog()=>new LeaderboardCatalog(Mappings);

        [Serializable] sealed class CurrentBoards
        {
            public LeaderboardMapping[] mappings = null;
        }
        public LeaderboardCatalog PlayableCatalog(bool includeOverall)
        {
            var historical = Mappings.Where(m => includeOverall || !Core.OverallRecord.IsAggregate(m.objectId));
            var asset = Resources.Load<TextAsset>("GameCenterRanks");
            if(asset != null)
            {
                try
                {
                    var extra = JsonUtility.FromJson<CurrentBoards>(asset.text)?.mappings ?? Array.Empty<LeaderboardMapping>();
                    return new LeaderboardCatalog(historical.Concat(extra.Where(m =>
                        m != null && (includeOverall || !Core.OverallRecord.IsAggregate(m.objectId)) && !string.IsNullOrWhiteSpace(m.leaderboardId))));
                }
                catch(ArgumentException e){Debug.LogWarning("Game Center configuration: "+e.Message);}
            }
            return new LeaderboardCatalog(historical);
        }
    }
}
