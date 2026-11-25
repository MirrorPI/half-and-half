using System;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HalfAndHalf.Tests
{
    public sealed class FullRankingTests
    {
        [TestCase(29)] [TestCase(24)] public void AllMappingsAndExplicitVerificationGateRequiredAndOverallQueueRoundTrips(int count)
        {
            string[] ids={"B01","B02","B03","B04","B05","A01","A02","A03","A04","A05","L01","L02","L03","L04","L05","N01","N02","N03","N04","N05","P01","P02","P03","P04","P05","S01","G01","G02","G03"};
            if(count==24)ids=ids.Except(new[]{"B05","A03","N03","P03","P04"}).ToArray();
            var defs=ids.Select(id=>new ObjectDefinition(id,"test-v1",new MassMap(new[]{new MassCell(-1,1,-1,1,100)}),"test")).ToArray();
            var records=defs.Select(d=>Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,0,9000,TransformState.Identity,true))).ToArray();
            var overall=OverallRecord.Calculate(defs,records);var config=ScriptableObject.CreateInstance<GameCenterConfiguration>();
            string folder=Path.Combine(Path.GetTempPath(),"HalfFullRanking-"+Guid.NewGuid());Directory.CreateDirectory(folder);
            try {
                config.EnableOnline=true;config.EnableOverallAfterServiceVerification=true;
                config.EnableCurrentOverallAfterServiceVerification=true;
                config.Mappings=defs.Select(d=>new LeaderboardMapping{objectId=d.Id,contentVersion=d.ContentVersion,leaderboardId="qa."+d.Id}).ToArray();
                Assert.That(config.OverallReady(defs),Is.False);
                config.Mappings=config.Mappings.Concat(new[]{new LeaderboardMapping{objectId="ALL"+count,contentVersion=overall.Version,leaderboardId="qa.all"+count}}).ToArray();
                Assert.That(config.OverallReady(defs),Is.True);if(count==24)config.EnableCurrentOverallAfterServiceVerification=false;else config.EnableOverallAfterServiceVerification=false;Assert.That(config.OverallReady(defs),Is.False);
                string path=Path.Combine(folder,"queue.json");var queue=new SubmissionQueue(path,defs,overall.Version);
                Assert.That(queue.ReconcileOverall(overall),Is.True);queue.BindGuest("account-A");Assert.That(queue.Persist(),Is.True);
                var restored=new SubmissionQueue(path,defs,overall.Version);var score=restored.Entries.Single();
                Assert.That(score.objectId,Is.EqualTo("ALL"+count));Assert.That(score.playerId,Is.EqualTo("account-A"));
                Assert.That(RankingScore.TryDecodeOverall(score.score,out var accuracy,out var time),Is.True);Assert.That(time,Is.EqualTo(count*9000));
                Assert.That(accuracy,Is.EqualTo(10000));Assert.That(restored.ReconcileOverall(overall),Is.False);
                var progress=CollectionProgress.Restore(defs,records,count-1);
                Assert.That(progress.ClaimGuestRecords(r=>restored.ClaimOwner(r,"account-B",progress.OverallBest)),Is.True);
                Assert.That(progress.BestOwner(0),Is.EqualTo("account-A"),"Existing aggregate claims survive an account switch");
                Assert.That(progress.OverallBest.Owners.All(o=>o=="account-A"),Is.True,"Retained source snapshot owners are persisted too");
                new LocalProgressStore(Path.Combine(folder,"progress.json")).Save(progress);
                var loaded=new LocalProgressStore(Path.Combine(folder,"progress.json")).Load(defs);
                Assert.That(loaded.OverallBest.Owners.All(o=>o=="account-A"),Is.True);
                var mixed=OverallRecord.Calculate(defs,records,ids.Select((id,i)=>i==0?"account-A":"account-B"));
                Assert.That(new SubmissionQueue(Path.Combine(folder,"mixed.json"),defs,overall.Version).ReconcileOverall(mixed),Is.False);
            } finally {Object.DestroyImmediate(config);Directory.Delete(folder,true);}
        }
    }
}
