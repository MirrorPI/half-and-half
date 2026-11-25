using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HalfAndHalf.Services
{
    // Game Center uses ordinal ranks for equal scores. Game ranks count strictly better players + 1.
    public static class SharedRanking
    {
        public static async Task<RankingPage> Normalize(RankingPage page,Func<long,Task<RankingEntry>> readRank)
        {
            var entries=page.Entries??Array.Empty<RankingEntry>();
            var first=entries.GroupBy(e=>e.Score).ToDictionary(g=>g.Key,g=>g.Min(e=>e.Rank));
            var samples=entries.ToDictionary(e=>e.Rank,e=>e);
            if(page.LocalPlayer!=null)samples[page.LocalPlayer.Rank]=page.LocalPlayer;
            bool queried=false;
            async Task<RankingEntry> Fresh(long rank)
            {
                var entry=readRank==null?null:await readRank(rank);
                if(entry==null || entry.Rank!=rank)throw new InvalidOperationException("Global ranking changed or is unavailable. Refresh the page.");
                samples[rank]=entry;return entry;
            }
            async Task<RankingEntry> Read(long rank)
            {
                if(samples.TryGetValue(rank,out var cached))return cached;
                queried=true;return await Fresh(rank);
            }
            long localRank=0;
            if(page.LocalPlayer!=null)
            {
                var local=page.LocalPlayer;
                if(!first.TryGetValue(local.Score,out localRank))
                {
                    // Ascending scores permit a logarithmic lookup even when the local tie is outside the top ten.
                    long lo=1,hi=local.Rank;
                    while(lo<hi)
                    {
                        long mid=lo+(hi-lo)/2;
                        if((await Read(mid)).Score<local.Score)lo=mid+1;else hi=mid;
                    }
                    var boundary=await Read(lo);
                    if(queried)
                    {
                        // Re-read the predecessor BEFORE the boundary. Ascending best scores can only improve.
                        if(lo>1 && (await Fresh(lo-1)).Score>=local.Score)throw new InvalidOperationException("Global ranking changed. Refresh the page.");
                        boundary=await Fresh(lo);
                    }
                    if(boundary.Score!=local.Score)throw new InvalidOperationException("Global ranking changed. Refresh the page.");
                    localRank=lo;
                }
            }
            return new RankingPage
            {
                Entries=entries.Select(e=>Copy(e,first[e.Score])).ToArray(),
                LocalPlayer=page.LocalPlayer==null?null:Copy(page.LocalPlayer,localRank),
                TotalPlayers=page.TotalPlayers
            };
        }
        public static void EnsureSnapshot(RankingPage original,long totalPlayers,RankingEntry localPlayer)
        {
            if(original.LocalPlayer==null || localPlayer==null || original.TotalPlayers!=totalPlayers ||
                original.LocalPlayer.Rank!=localPlayer.Rank || original.LocalPlayer.Score!=localPlayer.Score)
                throw new InvalidOperationException("Global ranking changed. Refresh the page.");
        }
        static RankingEntry Copy(RankingEntry entry,long rank)=>new RankingEntry{Rank=rank,Score=entry.Score,PlayerName=entry.PlayerName};
    }
}
