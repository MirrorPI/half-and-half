using System;
using System.IO;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class ProgressStoreTests
    {
        string folder,path;
        ObjectDefinition[] content;
        [SetUp] public void Setup()
        {
            folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HalfProgressTests-"+Guid.NewGuid());Directory.CreateDirectory(folder);path=System.IO.Path.Combine(folder,"progress.json");
            content=new[]{Definition("B01"),Definition("B02"),Definition("B03")};
        }
        [TearDown] public void Cleanup(){Directory.Delete(folder,true);}
        static ObjectDefinition Definition(string id,string version="v1") => new ObjectDefinition(id,version,new MassMap(new[]{new MassCell(-1,1,-1,1,100)}),"test");
        static AttemptResult Attempt(ObjectDefinition d,decimal x,long time,bool valid=true) => Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,x,time,TransformState.Identity,valid));
        [Test] public void LockedContentCannotBePlayedOrUnlockBySkipping()
        {
            var p=new CollectionProgress(content);Assert.That(p.Select(0),Is.True);Assert.That(p.Select(1),Is.False);
            Assert.That(p.Record(Attempt(content[1],0,100)),Is.False);Assert.That(p.Best(1),Is.Null);
            p.Record(Attempt(content[0],-.101m,300));Assert.That(p.UnlockedCount,Is.EqualTo(1));
            p.Record(Attempt(content[0],-.1m,800));Assert.That(p.UnlockedCount,Is.EqualTo(2));Assert.That(p.Select(1),Is.True);
        }
        [Test] public void BestUsesOfficialAccuracyThenTimeFromSameAttempt()
        {
            var p=new CollectionProgress(content);var first=Attempt(content[0],.00001m,850);
            Assert.That(p.Record(first),Is.True);Assert.That(p.Record(Attempt(content[0],.00002m,850)),Is.False,"hidden raw digits must not break ties");
            var fast=Attempt(content[0],.00002m,700);Assert.That(p.Record(fast),Is.True);Assert.That(p.Best(0),Is.SameAs(fast));
            Assert.That(p.Record(Attempt(content[0],.04m,10)),Is.False);Assert.That(p.Best(0).ElapsedMilliseconds,Is.EqualTo(700));
        }
        [TestCase(false,1000)] [TestCase(true,120000)] [TestCase(true,-1)]
        public void InterruptedOrOutOfRangeAttemptDoesNotBecomeBest(bool valid,long time)
        {var p=new CollectionProgress(content);Assert.That(p.Record(Attempt(content[0],0,time,valid)),Is.False);Assert.That(p.Best(0),Is.Null);}
        [Test] public void WrongVersionAndForgedResultAreRejected()
        {
            var p=new CollectionProgress(content);Assert.That(p.Record(Attempt(Definition("B01","old"),0,50)),Is.False);
            var snap=new CutSnapshot("B01","v1",.2m,40,TransformState.Identity,true);
            Assert.That(p.Record(new AttemptResult(snap,50,50,10000)),Is.False);
        }
        [Test] public void RestartPreservesExactSnapshotBestAndSequentialUnlocks()
        {
            var p=new CollectionProgress(content);p.Record(Attempt(content[0],-.034567890123456789m,950));p.Select(1);p.Record(Attempt(content[1],.02m,760));p.Select(2);
            var s=new LocalProgressStore(path);s.Save(p);var restored=new LocalProgressStore(path).Load(content);
            Assert.That(restored.UnlockedCount,Is.EqualTo(3));Assert.That(restored.Selected,Is.EqualTo(2));
            Assert.That(restored.Best(0).Snapshot.KnifeX,Is.EqualTo(-.034567890123456789m));Assert.That(restored.Best(0).LeftMass,Is.EqualTo(48.27160549382716055m));
            Assert.That(restored.Best(0).ElapsedMilliseconds,Is.EqualTo(950));Assert.That(restored.Best(1).AccuracyUnits,Is.EqualTo(9800));
        }
        [Test] public void RemovingStageKeepsSelectedObjectAndArchivedBestAfterSave()
        {
            var original = CollectionProgress.Restore(content, Array.ConvertAll(content, d => Attempt(d, 0, 100)), 2);
            var store = new LocalProgressStore(path);
            store.Save(original);
            var reduced = store.Load(new[] { content[0], content[2] });
            Assert.That(reduced.Content[reduced.Selected].Id, Is.EqualTo(content[2].Id));
            store.Save(reduced);
            var restored = new LocalProgressStore(path).Load(content);
            Assert.That(restored.RecordedCount, Is.EqualTo(content.Length));
            Assert.That(restored.Selected, Is.EqualTo(2));
        }
        [Test] public void CorruptPrimaryRecoversBackupThenCanSaveWithoutLosingRecoveredBest()
        {
            var p=new CollectionProgress(content);p.Record(Attempt(content[0],.04m,800));var s=new LocalProgressStore(path);s.Save(p);p.Record(Attempt(content[0],.01m,900));s.Save(p);
            File.WriteAllText(path,"broken");var recovered=new LocalProgressStore(path);var r=recovered.Load(content);
            Assert.That(recovered.Recovery,Is.EqualTo("backup"));Assert.That(r.Best(0).AccuracyUnits,Is.EqualTo(9600));
            recovered.Save(r);Assert.That(new LocalProgressStore(path).Load(content).Best(0).AccuracyUnits,Is.EqualTo(9600));
        }
        [Test] public void InterruptedWriteRecoversCompleteStagedFileAndIgnoresPartialOne()
        {
            var p=new CollectionProgress(content);p.Record(Attempt(content[0],.04m,800));var s=new LocalProgressStore(path);s.Save(p);
            p.Record(Attempt(content[0],.01m,900));s.Save(p);File.Move(path,path+".tmp");File.Copy(path+".bak",path);
            var recovered=new LocalProgressStore(path);Assert.That(recovered.Load(content).Best(0).AccuracyUnits,Is.EqualTo(9900));Assert.That(recovered.Recovery,Is.EqualTo("staged"));
            File.WriteAllText(path+".tmp","partial");Assert.That(new LocalProgressStore(path).Load(content).Best(0).AccuracyUnits,Is.EqualTo(9900),"recovered generation must already be durable");
        }
        [Test] public void AllCorruptFilesResetToPlayableFirstContent()
        {
            File.WriteAllText(path,"bad");File.WriteAllText(path+".bak","also bad");File.WriteAllText(path+".tmp","{");
            var store=new LocalProgressStore(path);var p=store.Load(content);Assert.That(store.Recovery,Is.EqualTo("reset"));Assert.That(p.Select(0),Is.True);Assert.That(p.UnlockedCount,Is.EqualTo(1));store.Save(p);
            Assert.That(new LocalProgressStore(path).Load(content).Select(0),Is.True);
        }
        [Test] public void RecoveredStagedBestSurvivesASecondInterruptedWrite()
        {
            var p=new CollectionProgress(content);p.Record(Attempt(content[0],.04m,800));var s=new LocalProgressStore(path);s.Save(p);
            p.Record(Attempt(content[0],.01m,900));s.Save(p);File.Move(path,path+".tmp");File.Copy(path+".bak",path);
            var recovering=new LocalProgressStore(path);var recovered=recovering.Load(content);
            Assert.That(recovered.Best(0).AccuracyUnits,Is.EqualTo(9900));
            // The next save may die while truncating its staging destination. A previously recovered best is already owned.
            File.WriteAllText(path+".tmp","partial second write");
            Assert.That(new LocalProgressStore(path).Load(content).Best(0).AccuracyUnits,Is.EqualTo(9900));
        }
        [Test] public void ChecksumDamageDoesNotReplaceValidBackup()
        {
            var p=new CollectionProgress(content);p.Record(Attempt(content[0],.03m,500));var s=new LocalProgressStore(path);s.Save(p);s.Save(p);
            File.WriteAllText(path,File.ReadAllText(path).Replace("500","501"));
            Assert.That(new LocalProgressStore(path).Load(content).Best(0).ElapsedMilliseconds,Is.EqualTo(500));
        }
        [Test] public void ContentVersionChangeRetainsMatchingRecordsOnly()
        {
            var p=new CollectionProgress(content);p.Record(Attempt(content[0],0,100));p.Record(Attempt(content[1],0,200));new LocalProgressStore(path).Save(p);
            var newer=new[]{Definition("B01","v2"),content[1],content[2]};var r=new LocalProgressStore(path).Load(newer);
            Assert.That(r.Best(0),Is.Null);Assert.That(r.UnlockedCount,Is.EqualTo(1));
        }
    }
}
