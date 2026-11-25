using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class RecordPolicyTests
    {
        string folder,path;
        ObjectDefinition definition;
        [Serializable] sealed class Envelope { public string payload,sha256; }
        [SetUp] public void Setup()
        {
            folder=Path.Combine(Path.GetTempPath(),"HalfRecordPolicy-"+Guid.NewGuid());Directory.CreateDirectory(folder);path=Path.Combine(folder,"queue.json");
            definition=new ObjectDefinition("B01","v1",new MassMap(new[]{new MassCell(0,1,0,1,100)}),"");
        }
        [TearDown] public void Cleanup()=>Directory.Delete(folder,true);
        AttemptResult Result(long time=2000)=>Scoring.Evaluate(definition,new CutSnapshot("B01","v1",.49m,time,TransformState.Identity,true));
        static string Hash(string value)
        {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
        void WritePayload(string payload)=>File.WriteAllText(path,JsonUtility.ToJson(new Envelope{payload=payload,sha256=Hash(payload)}));
        async Task<Receiver> Flush(SubmissionQueue queue,string player="A")
        {
            var receiver=new Receiver{PlayerId=player};
            var mappings=new[]{new LeaderboardMapping{objectId="B01",contentVersion="v1",leaderboardId="qa.apple"}};
            var aggregate=queue.Entries.FirstOrDefault(e=>e.objectId==OverallRecord.Id);
            if(aggregate!=null)mappings=mappings.Concat(new[]{new LeaderboardMapping{objectId=OverallRecord.Id,contentVersion=aggregate.contentVersion,leaderboardId="qa.overall"}}).ToArray();
            var flow=new GameCenterFlow(receiver,queue,new LeaderboardCatalog(mappings));
            await flow.Login();await flow.Flush();return receiver;
        }
        [Test] public async Task RecomputedChecksumCannotMakeMismatchingQueueScoreSubmit()
        {
            var queue=new SubmissionQueue(path,new[]{definition});queue.Offer(Result(),"A");queue.Persist();
            var env=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
            WritePayload(env.payload.Replace("2000002000","1000002000"));File.Delete(path+".bak");
            var restored=new SubmissionQueue(path,new[]{definition});var receiver=await Flush(restored);
            Assert.That(receiver.Scores,Is.Empty,"A checksum is not proof of the encoded score");
            Assert.That(restored.Entries.Single().pending,Is.True);
        }
        [Test] public void InvalidProgressIsPreservedBeforeBackupRecoveryAndSave()
        {
            var progress=new CollectionProgress(new[]{definition});progress.Record(Result(),"A");var store=new LocalProgressStore(path);store.Save(progress);
            File.WriteAllText(path,"broken-original");var restored=store.Load(new[]{definition});
            Assert.That(store.Recovery,Is.EqualTo("backup"));store.Save(restored);
            Assert.That(Directory.GetFiles(folder,"*.rejected-*").Any(p=>File.ReadAllText(p)=="broken-original"),Is.True);
            Assert.That(restored.BestOwner(0),Is.EqualTo("A"));
        }
        [Test] public async Task LegacyPendingWithoutProofStaysOwnedAndAcknowledgedMarkersSurvive()
        {
            WritePayload("{\"schema\":1,\"revision\":1,\"entries\":[{\"objectId\":\"B01\",\"contentVersion\":\"v1\",\"playerId\":\"A\",\"score\":2000002000,\"pending\":true}]}");
            var restored=new SubmissionQueue(path,new[]{definition});var receiver=await Flush(restored);
            Assert.That(receiver.Scores,Is.Empty);Assert.That(restored.Entries.Single().playerId,Is.EqualTo("A"));
            restored.Acknowledge(restored.Entries.Single());restored.Persist();
            restored=new SubmissionQueue(path,new[]{definition});Assert.That(restored.Entries.Single().pending,Is.False);
            Assert.That(restored.ReconcileOwned(Result(),"B"),Is.False);
        }
        [Test] public async Task LegacyExactOwnedSourceCanBeAttachedAfterHeldRestart()
        {
            WritePayload("{\"schema\":1,\"revision\":1,\"entries\":[{\"objectId\":\"B01\",\"contentVersion\":\"v1\",\"playerId\":\"A\",\"score\":2000002000,\"pending\":true}]}");
            var queue=new SubmissionQueue(path,new[]{definition});queue.Persist();queue=new SubmissionQueue(path,new[]{definition});
            Assert.That(queue.ReconcileOwned(Result(),"B"),Is.False);
            Assert.That(queue.ReconcileOwned(Result(),"A"),Is.True);
            var receiver=await Flush(queue);Assert.That(receiver.Scores,Is.EqualTo(new[]{2000002000L}));
            Assert.That(queue.Entries.Single().pending,Is.False);
        }
        [Test] public async Task EarlierAccountCandidateKeepsItsOwnSourceWhenDeviceBestChanges()
        {
            var queue=new SubmissionQueue(path,new[]{definition});queue.Offer(Result(),"A");queue.Offer(Result(1000),"B");queue.Persist();
            queue=new SubmissionQueue(path,new[]{definition});var receiver=await Flush(queue);
            Assert.That(receiver.Scores,Is.EqualTo(new[]{2000002000L}));
            Assert.That(queue.Entries.Single(e=>e.playerId=="B").pending,Is.True);
        }
        [Test] public async Task OverallCandidateRetains29SourcesAndMismatchingTotalCannotSubmit()
        {
            var defs=Enumerable.Range(0,29).Select(i=>new ObjectDefinition("id"+i,"v1",definition.Mass,"")).ToArray();
            var records=defs.Select(d=>Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,.5m,2000,TransformState.Identity,true))).ToArray();
            var overall=OverallRecord.Calculate(defs,records,defs.Select(_=>"A"));
            var queue=new SubmissionQueue(path,defs,overall.Version);Assert.That(queue.ReconcileOverall(overall),Is.True);queue.Persist();
            queue=new SubmissionQueue(path,defs,overall.Version);
            Assert.That(queue.Entries.Single().evidence.Length,Is.EqualTo(29));
            Assert.That(queue.CanSubmit(queue.Entries.Single(),"A"),Is.True);
            var env=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));WritePayload(env.payload.Replace("\"score\":58000","\"score\":57000"));File.Delete(path+".bak");
            queue=new SubmissionQueue(path,defs,overall.Version);var receiver=await Flush(queue);
            Assert.That(receiver.Scores,Is.Empty);Assert.That(queue.Entries.Single().heldReason,Is.EqualTo("result-mismatch"));
        }
        [Test] public void FailedEvidenceUsesOnlyMatchingBackupAndKeepsAcknowledgedOtherVersion()
        {
            var queue=new SubmissionQueue(path,new[]{definition});queue.Offer(Result(),"A");queue.Persist();queue.Persist();
            var env=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));WritePayload(env.payload.Replace("\"accuracy\":9800","\"accuracy\":9700"));
            queue=new SubmissionQueue(path,new[]{definition});Assert.That(queue.CanSubmit(queue.Entries.Single(),"A"),Is.True);
            Assert.That(queue.Recovery,Is.EqualTo("backup-evidence"));Assert.That(queue.PreservedFailure,Is.True);
            queue.Acknowledge(queue.Entries.Single());queue.Persist();
            var v2=new ObjectDefinition("B01","v2",definition.Mass,"");var changed=new SubmissionQueue(path,new[]{v2});changed.Persist();
            queue=new SubmissionQueue(path,new[]{definition});Assert.That(queue.Entries.Single().pending,Is.False);
            Assert.That(queue.Entries.Single().playerId,Is.EqualTo("A"));
        }
        sealed class Receiver:IGameCenter
        {
            public bool Supported=>true;public string PlayerId {get;set;}
            public readonly System.Collections.Generic.List<long> Scores=new System.Collections.Generic.List<long>();
            public Task<LoginResult> Authenticate()=>Task.FromResult(new LoginResult{Outcome=LoginOutcome.SignedIn,PlayerId=PlayerId});
            public Task Submit(string board,long score,string owner){Scores.Add(score);return Task.CompletedTask;}
            public Task<RankingPage> Load(string board)=>Task.FromResult(new RankingPage{Entries=new RankingEntry[0]});
        }
    }
}
