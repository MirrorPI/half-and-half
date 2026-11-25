using System;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class FullCollectionTests
    {
        static ObjectDefinition[] Content()=>Enumerable.Range(0,29).Select(i=>new ObjectDefinition("test"+i,"v1",new MassMap(new[]{new MassCell(-1,1,-1,1,100)}),"test")).ToArray();
        static AttemptResult Attempt(ObjectDefinition d, decimal x=0,long ms=1000)=>Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,x,ms,TransformState.Identity,true));
        [Test] public void All29RequiredAndAverageUsesOfficialUnitsWithSameAttemptTimes()
        {
            var d=Content();var records=d.Select((x,i)=>Attempt(x,i==0?.0001m:0,1000+i)).ToArray();
            Assert.That(OverallRecord.Calculate(d,records.Take(28)),Is.Null);
            var overall=OverallRecord.Calculate(d,records);
            Assert.That(overall,Is.Not.Null);Assert.That(overall.AccuracyUnits,Is.EqualTo(10000));
            Assert.That(overall.TimeMilliseconds,Is.EqualTo(records.Sum(r=>r.ElapsedMilliseconds)));
            Assert.That(overall.Records[0],Is.SameAs(records[0]));
            Assert.That(RankingScore.TryDecodeOverall(overall.Score,out var accuracy,out var time),Is.True);
            Assert.That(accuracy,Is.EqualTo(10000));Assert.That(time,Is.EqualTo(overall.TimeMilliseconds));
            Assert.That(OverallRecord.VersionFor(d),Is.Not.EqualTo(OverallRecord.VersionFor(d.Select((x,i)=>i==28?new ObjectDefinition(x.Id,"v2",x.Mass,"test"):x).ToArray())));
        }
        [Test] public void Current24AndLegacy29KeepSeparateBestSnapshotsAcrossSaves()
        {
            var legacy=Content();
            var current=legacy.Take(24).ToArray();
            var bests=legacy.Select((d,i)=>Attempt(d,i==0?.0001m:0)).ToArray();
            var old=CollectionProgress.Restore(legacy,bests,28);
            Assert.That(old.Record(Attempt(legacy[0],0,8000)),Is.True);
            Assert.That(old.OverallBest.TimeMilliseconds,Is.EqualTo(29000));
            Assert.That(CollectionProgress.Restore(current,bests.Take(23),0).OverallBest,Is.Null);
            string folder=Path.Combine(Path.GetTempPath(),"Half24-"+Guid.NewGuid());
            string path=Path.Combine(folder,"progress.json");
            try
            {
                new LocalProgressStore(path).Save(old);
                var store=new LocalProgressStore(path);
                var active=store.Load(current);
                Assert.That(active.OverallBest,Is.Not.Null);
                Assert.That(active.OverallBest.Version,Does.StartWith("collection24-v1-"));
                Assert.That(active.OverallBest.AccuracyUnits,Is.EqualTo(10000));
                Assert.That(active.OverallBest.TimeMilliseconds,Is.EqualTo(31000));
                store.Save(active);
                var legacyStore=new LocalProgressStore(path);
                var reopened=legacyStore.Load(legacy);
                Assert.That(reopened.OverallBest.TimeMilliseconds,Is.EqualTo(29000));
                legacyStore.Save(reopened);
                Assert.That(new LocalProgressStore(path).Load(current).OverallBest.TimeMilliseconds,Is.EqualTo(31000));
            }
            finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [Test] public void PreviousOverallSnapshotSurvivesSlowerRoundedTieAndSaveReload()
        {
            var d=Content();var records=d.Select((x,i)=>Attempt(x,i==0?.0001m:0,1000)).ToArray();
            var p=CollectionProgress.Restore(d,records,28);var previous=p.OverallBest;
            Assert.That(p.Record(Attempt(d[0],0,8000),"account-A"),Is.True);
            Assert.That(p.OverallBest,Is.SameAs(previous));Assert.That(p.Best(0).ElapsedMilliseconds,Is.EqualTo(8000));
            string folder=Path.Combine(Path.GetTempPath(),"HalfFull-"+Guid.NewGuid());Directory.CreateDirectory(folder);
            try {
                string path=Path.Combine(folder,"progress.json");new LocalProgressStore(path).Save(p);
                var restored=new LocalProgressStore(path).Load(d);
                Assert.That(restored.RecordedCount,Is.EqualTo(29));Assert.That(restored.OverallBest.TimeMilliseconds,Is.EqualTo(29000));
                Assert.That(restored.OverallBest.Records[0].AccuracyUnits,Is.EqualTo(9999));
                // Older collection scenes can still open/save without erasing the extra nine records.
                var oldStore=new LocalProgressStore(path);var old=oldStore.Load(d.Take(20));oldStore.Save(old);
                Assert.That(new LocalProgressStore(path).Load(d).RecordedCount,Is.EqualTo(29));
            } finally {Directory.Delete(folder,true);}
        }
        [Test] public void LegacySchemaOneExpandsWithoutChangingRecordsOrOwners()
        {
            var d=Content();var p=CollectionProgress.Restore(d.Take(20),d.Take(20).Select(x=>Attempt(x)),19);
            string folder=Path.Combine(Path.GetTempPath(),"HalfLegacy20-"+Guid.NewGuid());Directory.CreateDirectory(folder);
            try {
                string path=Path.Combine(folder,"progress.json");new LocalProgressStore(path).Save(p);
                var full=new LocalProgressStore(path).Load(d);
                Assert.That(full.RecordedCount,Is.EqualTo(20));Assert.That(full.UnlockedCount,Is.EqualTo(21));
                Assert.That(full.Selected,Is.EqualTo(19));Assert.That(full.OverallBest,Is.Null);
                Assert.That(full.Best(19).Snapshot.KnifeX,Is.EqualTo(p.Best(19).Snapshot.KnifeX));
                Assert.That(full.CompletionPercent,Is.EqualTo(68.97m));
                var updated=d.Select((x,i)=>i==19?new ObjectDefinition(x.Id,"v2",x.Mass,"test"):x).ToArray();
                var changedStore=new LocalProgressStore(path);var changed=changedStore.Load(updated);
                Assert.That(changed.Best(19),Is.Null,"Versions retain separate eligibility");new LocalProgressStore(path).Save(changed);
                Assert.That(new LocalProgressStore(path).Load(d).RecordedCount,Is.EqualTo(20),"Earlier-version record remains stored for its own content version");
            } finally {Directory.Delete(folder,true);}
        }
    }
}
