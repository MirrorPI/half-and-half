using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class SubmissionQueueTests
    {
        string folder,path;
        ObjectDefinition definition;
        object queue;
        Type QueueType
        {
            get {var t=typeof(LocalProgressStore).Assembly.GetType("HalfAndHalf.Services.SubmissionQueue");Assert.That(t,Is.Not.Null,"Task4 needs a persistent submission queue.");return t;}
        }
        object Create()=>Activator.CreateInstance(QueueType,new object[]{path,new[]{definition},null});
        object Call(string method,params object[] args)=>QueueType.GetMethod(method).Invoke(queue,args);
        QueuedScore[] Entries=>(QueuedScore[])QueueType.GetProperty("Entries").GetValue(queue);
        AttemptResult Result(int accuracy=9800,long ms=2000,bool valid=true,string version="v1")
        {
            decimal x=accuracy/20000m;var s=new CutSnapshot("B01",version,x,ms,TransformState.Identity,valid);
            return new AttemptResult(s,x*100,(1-x)*100,accuracy);
        }
        [SetUp] public void Setup()
        {
            folder=Path.Combine(Path.GetTempPath(),"half-task4-queue-"+Guid.NewGuid());Directory.CreateDirectory(folder);path=Path.Combine(folder,"queue.json");
            definition=new ObjectDefinition("B01","v1",new MassMap(new[]{new MassCell(0,1,0,1,100)}),"");
        }
        [TearDown] public void Cleanup(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
        [Test] public void OfflineCandidateSurvivesRestartAndRetainsOfficialScore()
        {queue=Create();Assert.That(Call("Offer",Result(),""),Is.True);Assert.That(Call("Persist"),Is.True);queue=Create();Assert.That(Entries.Single().score,Is.EqualTo(2000002000));Assert.That(Entries.Single().pending,Is.True);}
        [Test] public void WorseAndDuplicateCandidatesNeverRegressTheBest()
        {queue=Create();Call("Offer",Result(),"p1");Assert.That(Call("Offer",Result(9700),"p1"),Is.False);Assert.That(Call("Offer",Result(),"p1"),Is.False);Call("Offer",Result(9800,1000),"p1");Assert.That(Entries.Single().score,Is.EqualTo(2000001000));}
        [Test] public void OldInFlightAcknowledgementDoesNotRemoveNewBetterAttempt()
        {queue=Create();Call("Offer",Result(),"p1");var old=Entries.Single();Call("Offer",Result(9900),"p1");Call("Acknowledge",old);Assert.That(Entries.Single().pending,Is.True);Call("Acknowledge",Entries.Single());Assert.That(Entries.Single().pending,Is.False);}
        [Test] public void GuestBindsOnceAndAcknowledgedRecordIsNotTransferredToNewAccount()
        {queue=Create();Call("Offer",Result(),"");Call("BindGuest","p1");Call("Acknowledge",Entries.Single());Call("Persist");queue=Create();Call("BindGuest","p2");Assert.That(Entries.Single().playerId,Is.EqualTo("p1"));Assert.That(Call("Reconcile",Result()),Is.False);}
        [Test] public void InterruptedTimedOutForgedAndWrongVersionAreExcluded()
        {queue=Create();foreach(var r in new[]{Result(valid:false),Result(ms:120000),Result(ms:-1),Result(version:"v2"),new AttemptResult(Result().Snapshot,50,50,9800)})Assert.That(Call("Offer",r,""),Is.False);Assert.That(Entries,Is.Empty);}
        [Test] public void CompleteStagingIsRecoveredBeforeNextSaveAndCorruptPrimaryUsesBackup()
        {queue=Create();Call("Offer",Result(),"p1");Call("Persist");Call("Offer",Result(9900),"p1");Call("Persist");File.Copy(path,path+".tmp",true);File.Copy(path+".bak",path,true);queue=Create();Assert.That(Entries.Single().score,Is.EqualTo(1000002000));Call("Persist");File.WriteAllText(path,"broken");queue=Create();Assert.That(Entries.Single().score,Is.EqualTo(1000002000));}
        [Test] public void DeniedPersistenceLeavesCandidatePendingForRetry()
        {Directory.CreateDirectory(path);queue=Create();Call("Offer",Result(),"");Assert.That(Call("Persist"),Is.False);Assert.That(Entries.Single().pending,Is.True);Directory.Delete(path);Assert.That(Call("Persist"),Is.True);queue=Create();Assert.That(Entries.Single().pending,Is.True);}
        [Test] public void CandidateVersionDoesNotMigrateOnContentChange()
        {queue=Create();Call("Offer",Result(),"p1");Call("Persist");definition=new ObjectDefinition("B01","v2",definition.Mass,"");queue=Create();Assert.That(Entries,Is.Empty);}
    }
}
