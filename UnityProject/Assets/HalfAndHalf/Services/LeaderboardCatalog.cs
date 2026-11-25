using System;
using System.Collections.Generic;
using System.Linq;

namespace HalfAndHalf.Services
{
    public sealed class LeaderboardCatalog
    {
        readonly LeaderboardMapping[] mappings;
        static readonly HashSet<string> SupportedIds=new HashSet<string>{"B01","B02","B03","B04","B05","A01","A02","A03","A04","A05","L01","L02","L03","L04","L05","N01","N02","N03","N04","N05","P01","P02","P03","P04","P05","S01","G01","G02","G03","ALL29","ALL24"};
        public LeaderboardCatalog(IEnumerable<LeaderboardMapping> values)
        {
            mappings=values.Select(m=>new LeaderboardMapping{objectId=m.objectId,contentVersion=m.contentVersion,leaderboardId=m.leaderboardId}).ToArray();
            var keys=new HashSet<string>();var boards=new HashSet<string>();
            foreach(var m in mappings)
                if(!SupportedIds.Contains(m.objectId) || string.IsNullOrWhiteSpace(m.contentVersion) || string.IsNullOrWhiteSpace(m.leaderboardId) || !keys.Add(m.objectId+"\n"+m.contentVersion) || !boards.Add(m.leaderboardId))
                    throw new ArgumentException("Boards require supported object IDs, unique version mappings and leaderboard IDs.");
        }
        public string Resolve(string objectId,string version)=>mappings.FirstOrDefault(m=>m.objectId==objectId&&m.contentVersion==version)?.leaderboardId;
    }
}
