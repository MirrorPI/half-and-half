using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace HalfAndHalf.Tests
{
    public sealed class GameCenterReviewTests
    {
        static IEnumerator Cut(MvpCollection m,float x)
        {var g=m.Game;g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);var p=g.GameCamera.WorldToScreenPoint(new Vector3(x,0,0));Assert.That(g.BeginPointer(42,p),Is.True);Assert.That(g.ReleasePointer(42,p),Is.True);yield return new WaitForSecondsRealtime(.65f);}
        [UnityTest] public IEnumerator ChangingCollectionSavePathIsolatesQaQueueAndDestroy()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;var m=Object.FindFirstObjectByType<MvpCollection>();var c=m.GetComponent<GameCenterController>();yield return null;
            string original=m.SavePath+".queue",qa=Path.Combine(Path.GetTempPath(),"half-task4-review-"+Guid.NewGuid()+".json");
            var d=m.Samples[0].Definition();var r=Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,.2m,2000,TransformState.Identity,true));c.Queue.Offer(r,"sentinel");c.Queue.Persist();byte[] bytes=File.ReadAllBytes(original);
            try
            {
                m.SavePath=qa;m.Reload();yield return null;yield return Cut(m,0);yield return new WaitForSecondsRealtime(.6f);
                Assert.That(File.Exists(qa+".queue"),Is.True,"Fresh QA save must own its own submission queue");
                Assert.That(File.ReadAllBytes(original),Is.EqualTo(bytes));Object.Destroy(c);yield return null;Assert.That(File.ReadAllBytes(original),Is.EqualTo(bytes));
                Assert.That(new SubmissionQueue(qa+".queue",m.Progress.Content).Entries.Single().playerId,Is.Empty);
            }
            finally{foreach(var suffix in new[]{"",".bak",".tmp",".queue",".queue.bak",".queue.tmp"})if(File.Exists(qa+suffix))File.Delete(qa+suffix);}
        }
        [UnityTest] public IEnumerator CutOwnerSurvivesAccountChangeFailedSaveAndRestart()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;var m=Object.FindFirstObjectByType<MvpCollection>();yield return null;
            var provider=typeof(MvpCollection).GetField("RecordOwner");Assert.That(provider,Is.Not.Null,"CUT ownership must be captured before shared progress polling");string account="A";provider.SetValue(m,(Func<string>)(()=>account));Directory.CreateDirectory(m.SavePath);
            var g=m.Game;g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);var point=g.GameCamera.WorldToScreenPoint(Vector3.zero);g.BeginPointer(42,point);g.ReleasePointer(42,point);account="B";yield return new WaitForSecondsRealtime(.65f);Assert.That(m.SavePending,Is.True);
            Directory.Delete(m.SavePath);g.Retry();yield return Cut(m,.3f);yield return new WaitForSecondsRealtime(.6f);Assert.That(m.SavePending,Is.False);
            var bestOwner=typeof(CollectionProgress).GetMethod("BestOwner");Assert.That(bestOwner,Is.Not.Null);Assert.That(bestOwner.Invoke(m.Progress,new object[]{0}),Is.EqualTo("A"));
            var restored=new LocalProgressStore(m.SavePath).Load(m.Progress.Content);Assert.That(bestOwner.Invoke(restored,new object[]{0}),Is.EqualTo("A"));Assert.That(m.GetComponent<GameCenterController>().Queue.Entries.Single().playerId,Is.EqualTo("A"));
        }
        [UnityTest] public IEnumerator GuestDuringHeldLoadClearsRowsAndRestoresObjectButtons()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;var p=Object.FindFirstObjectByType<RankingPanel>();yield return null;var service=new HeldService();
            var flow=new GameCenterFlow(service,p.Controller.Queue,p.Controller.Configuration.Catalog());var login=flow.Login();while(!login.IsCompleted)yield return null;
            typeof(GameCenterController).GetProperty("Flow").SetValue(p.Controller,flow);p.Open();yield return null;p.GuestButton.onClick.Invoke();yield return null;
            Assert.That(p.ObjectButtons.All(b=>b.interactable),Is.True,"Guest must invalidate the load and release controls");
            service.LoadGate.SetResult(new RankingPage{Entries=new[]{new RankingEntry{Rank=1,Score=1,PlayerName="stale account"}}});yield return null;yield return null;Assert.That(p.Rows.All(t=>t.text==""),Is.True);p.Close();
        }
        sealed class HeldService:IGameCenter
        {
            public bool Supported=>true;public string PlayerId=>"A";
            public TaskCompletionSource<RankingPage> LoadGate=new TaskCompletionSource<RankingPage>();
            public Task<LoginResult> Authenticate()=>Task.FromResult(new LoginResult{Outcome=LoginOutcome.SignedIn,PlayerId="A"});
            public Task Submit(string board,long score,string player)=>Task.CompletedTask;
            public Task<RankingPage> Load(string board)=>LoadGate.Task;
        }
    }
}
