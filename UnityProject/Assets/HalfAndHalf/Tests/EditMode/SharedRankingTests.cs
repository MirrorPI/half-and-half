using System;
using System.Linq;
using System.Threading.Tasks;
using HalfAndHalf.Services;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class SharedRankingTests
    {
        static RankingEntry Row(long rank,long score)=>new RankingEntry{Rank=rank,Score=score,PlayerName="Player "+rank};
        static Task<RankingPage> Normalize(RankingPage page,Func<long,Task<RankingEntry>> read)
        {
            var type=typeof(RankingPage).Assembly.GetType("HalfAndHalf.Services.SharedRanking");
            Assert.That(type,Is.Not.Null,"Equal official scores must receive a shared game rank instead of native ordinal ranks.");
            return (Task<RankingPage>)type.GetMethod("Normalize").Invoke(null,new object[]{page,read});
        }
        static Task<RankingEntry> NoRead(long rank)=>throw new Exception("The first page already establishes this shared rank.");

        [Test] public async Task ActualNativeTwentySixMillisecondTieUsesSharedRankOne()
        {
            // Actual two-account Game Center response: both score26, native ranks1 and2.
            var source=new RankingPage{Entries=new[]{Row(1,26),Row(2,26)},LocalPlayer=Row(2,26),TotalPlayers=2};
            var page=await Normalize(source,NoRead);
            Assert.That(page.Entries.Select(e=>e.Rank),Is.EqualTo(new long[]{1,1}));
            Assert.That(page.LocalPlayer.Rank,Is.EqualTo(1));
            Assert.That(source.Entries[1].Rank,Is.EqualTo(2));
            Assert.That(page.TotalPlayers,Is.EqualTo(2));
        }
        [Test] public async Task DistinctScoresKeepCompetitionRankGapsAndTheSameOfficialScore()
        {
            var page=await Normalize(new RankingPage{Entries=new[]{Row(1,26),Row(2,26),Row(3,67),Row(4,10000080)},LocalPlayer=Row(4,10000080)},NoRead);
            Assert.That(page.Entries.Select(e=>e.Rank),Is.EqualTo(new long[]{1,1,3,4}));
            Assert.That(page.Entries.Select(e=>e.Score),Is.EqualTo(new long[]{26,26,67,10000080}));
            Assert.That(page.LocalPlayer.Rank,Is.EqualTo(4));
        }
        [Test] public async Task ALocalTieSpanningBeyondTopTenUsesItsFirstVisibleRank()
        {
            var rows=Enumerable.Range(1,10).Select(i=>Row(i,i<9?i:100)).ToArray();
            var page=await Normalize(new RankingPage{Entries=rows,LocalPlayer=Row(500,100),TotalPlayers=500},NoRead);
            Assert.That(page.LocalPlayer.Rank,Is.EqualTo(9));
            Assert.That(page.Entries[9].Rank,Is.EqualTo(9));
        }
        [Test] public async Task ALocalTieOutsideTopTenFindsItsGlobalStartWithoutEnumeratingPlayers()
        {
            int reads=0;
            var rows=Enumerable.Range(1,10).Select(i=>Row(i,26)).ToArray();
            var page=await Normalize(new RankingPage{Entries=rows,LocalPlayer=Row(1000000000000,67)},rank=>
            {reads++;return Task.FromResult(Row(rank,rank<750000000001?26:67));});
            Assert.That(page.LocalPlayer.Rank,Is.EqualTo(750000000001));
            Assert.That(reads,Is.LessThan(45),"Global ties must not require fetching every preceding player.");
        }
        [Test] public async Task FirstPlayerAfterTheVisiblePageDoesNotNeedAnExtraRequest()
        {
            var rows=Enumerable.Range(1,10).Select(i=>Row(i,26)).ToArray();
            var page=await Normalize(new RankingPage{Entries=rows,LocalPlayer=Row(11,67)},NoRead);
            Assert.That(page.LocalPlayer.Rank,Is.EqualTo(11));
        }
        [Test] public void FailedGlobalRankReadDoesNotPresentAnUnverifiedOrdinalAsSharedRank()
        {
            var source=new RankingPage{Entries=Array.Empty<RankingEntry>(),LocalPlayer=Row(100,67)};
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await Normalize(source,rank=>Task.FromResult<RankingEntry>(null)));
        }
        [Test] public void ChangedScoreAtTheComputedBoundaryMustRequestAFreshPage()
        {
            var source=new RankingPage{Entries=Array.Empty<RankingEntry>(),LocalPlayer=Row(100,67)};
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await Normalize(source,rank=>Task.FromResult(Row(rank,68))));
        }
        [Test] public void LeaderboardMutationDuringLookupMustNotReturnACachedFalseBoundary()
        {
            bool changed=false;
            var source=new RankingPage{Entries=Enumerable.Range(1,10).Select(i=>Row(i,26)).ToArray(),LocalPlayer=Row(100,67)};
            Assert.ThrowsAsync<InvalidOperationException>(async()=>await Normalize(source,rank=>
            {
                if(rank==51)changed=true;
                return Task.FromResult(Row(rank,rank<(changed?60:51)?26:67));
            }));
        }
        static void CheckSnapshot(RankingPage page,long count,RankingEntry local)
        {
            var type=typeof(RankingPage).Assembly.GetType("HalfAndHalf.Services.SharedRanking");
            var method=type?.GetMethod("EnsureSnapshot");
            Assert.That(method,Is.Not.Null,"Separate native queries must reject changed local score/rank or participant counts.");
            try{method.Invoke(null,new object[]{page,count,local});}
            catch(System.Reflection.TargetInvocationException e){throw e.InnerException;}
        }
        [TestCase(101,100,67)] [TestCase(100,101,67)] [TestCase(100,100,26)]
        public void ChangedNativePlayerOrParticipantMetadataCannotMixWithTheOriginalPage(long count,long rank,long score)
        {
            var source=new RankingPage{TotalPlayers=100,LocalPlayer=Row(100,67)};
            Assert.Throws<InvalidOperationException>(()=>CheckSnapshot(source,count,Row(rank,score)));
        }
        [Test] public void SameNativeScoreRankAndCountRemainsUsableAfterNicknameChanges()
        {
            var source=new RankingPage{TotalPlayers=100,LocalPlayer=Row(100,67)};
            var local=Row(100,67);local.PlayerName="Renamed";
            Assert.DoesNotThrow(()=>CheckSnapshot(source,100,local));
        }
    }
}
