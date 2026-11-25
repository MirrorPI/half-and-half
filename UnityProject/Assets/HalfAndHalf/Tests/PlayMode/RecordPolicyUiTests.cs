using System;
using System.Collections;
using System.Linq;
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
    public sealed class RecordPolicyUiTests
    {
        [UnityTest] public IEnumerator Unmapped24ShowsDeviceRecordsAndLocalOverall()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay29");yield return null;
            var panel=Object.FindFirstObjectByType<RankingPanel>();
            Assert.That(panel.Controller.LocalRankingsOnly,Is.True);
            Assert.That(panel.OpenButton.GetComponentInChildren<TMPro.TMP_Text>().text,Is.EqualTo("기록"));
            panel.Open();
            Assert.That(panel.Panel.activeSelf,Is.True);
            Assert.That(panel.LoginButton.gameObject.activeSelf,Is.False);
            Assert.That(panel.GuestButton.gameObject.activeSelf,Is.False);
            Assert.That(panel.RefreshButton.gameObject.activeSelf,Is.False);
            Assert.That(panel.Selected.text,Does.Contain("개인 기록"));
            panel.ObjectButtons[2].onClick.Invoke();
            Assert.That(panel.Selected.text,Does.Contain("24종 종합").And.Contain("0 / 24"));
            Assert.That(panel.LocalRecord.text,Does.Contain("24종 필요").And.Not.Contain("Game Center"));
            panel.Close();
        }

        GameCenterConfiguration testConfig;
        [TearDown] public void RemoveTestBoards()
        {
            if(testConfig!=null)Object.Destroy(testConfig);
        }
        [UnityTest] public IEnumerator FreshDeviceQueriesExistingOverallRankWithoutRestoringProgress()
        {
            void ConfigureTestBoards(Scene _,LoadSceneMode mode)
            {
                var service=Object.FindFirstObjectByType<GameCenterController>();
                var definitions=service.Collection.Samples.Select(s=>s.Definition()).ToArray();
                testConfig=ScriptableObject.CreateInstance<GameCenterConfiguration>();
                testConfig.EnableOnline=true;
                testConfig.EnableCurrentOverallAfterServiceVerification=true;
                var aggregate=new LeaderboardMapping{objectId="ALL24",contentVersion=OverallRecord.VersionFor(definitions),leaderboardId="test.overall"};
                testConfig.Mappings=definitions.Select(d=>new LeaderboardMapping{objectId=d.Id,contentVersion=d.ContentVersion,leaderboardId="test."+d.Id}).Append(aggregate).ToArray();
                service.Configuration=testConfig;
            }
            SceneManager.sceneLoaded+=ConfigureTestBoards;
            try {yield return SceneManager.LoadSceneAsync("Gameplay29");}
            finally {SceneManager.sceneLoaded-=ConfigureTestBoards;}
            yield return null;yield return null;
            var panel=Object.FindFirstObjectByType<RankingPanel>();var controller=panel.Controller;controller.enabled=false;
            var progress=controller.Collection.Progress;Assert.That(progress.RecordedCount,Is.Zero);
            var collection=controller.Collection;
            foreach(var label in new[]{collection.BestLabel,collection.RecoveryLabel,collection.Hint})
                foreach(char ch in "이 기기 기록 저장 복구 대기")Assert.That(label.font.HasCharacter(ch,true,true),Is.True,"Policy notices need a Korean font");
            var service=new ExistingRank();var flow=new GameCenterFlow(service,controller.Queue,controller.Configuration.Catalog());
            yield return Await(flow.Login());typeof(GameCenterController).GetProperty("Flow").SetValue(controller,flow);
            panel.Open();panel.ObjectButtons[2].onClick.Invoke();yield return null;
            Assert.That(panel.OverallSelected,Is.True);
            Assert.That(service.LastBoard,Is.EqualTo("test.overall"));
            Assert.That(panel.Rows[0].text,Does.Contain("99.50%"));
            Assert.That(panel.LocalRecord.text,Does.Contain("이 기기 기록 · 종합 미완성").And.Contain("Game Center 기록 · 내 순위"));
            Assert.That(progress.RecordedCount,Is.Zero);Assert.That(progress.UnlockedCount,Is.EqualTo(1));Assert.That(progress.OverallBest,Is.Null);
            RankingUiCapture.Save(panel.Game,"policy-fresh-device-overall.png",428,926);
            service.Fail=true;yield return Await(panel.Reload());
            Assert.That(panel.Rows[0].text,Does.Contain("조회 실패"));Assert.That(progress.RecordedCount,Is.Zero);
            panel.Close();var settings=panel.Game.GetComponent<GameSettings>();settings.OpenButton.onClick.Invoke();yield return null;
            var notice=settings.Panel.transform.Find("StorageNotice").GetComponent<TMPro.TMP_Text>();
            Assert.That(notice.text,Does.Contain("기기 간 동기화").And.Contain("삭제 후 자동 복원"));
            RankingUiCapture.Save(panel.Game,"policy-storage-help-small.png",375,667);
            settings.CloseButton.onClick.Invoke();Assert.That(panel.Game.UiOpen,Is.False);
            // Basic READY/CUT after both screens close, using the real gameplay input path.
            var game=panel.Game;game.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
            var point=game.GameCamera.WorldToScreenPoint(Vector3.zero);
            Assert.That(game.BeginPointer(7,point),Is.True);Assert.That(game.ReleasePointer(7,point),Is.True);
            yield return new WaitForSecondsRealtime(.65f);Assert.That(progress.RecordedCount,Is.EqualTo(1));
        }
        static IEnumerator Await(Task task)
        {while(!task.IsCompleted)yield return null;if(task.IsFaulted)throw task.Exception;}
        sealed class ExistingRank:IGameCenter
        {
            public bool Supported=>true;public string PlayerId=>"existing-account";public string LastBoard;public bool Fail;
            public Task<LoginResult> Authenticate()=>Task.FromResult(new LoginResult{Outcome=LoginOutcome.SignedIn,PlayerId=PlayerId});
            public Task Submit(string board,long score,string player)=>throw new InvalidOperationException("A fresh device must not submit server-derived progress.");
            public Task<RankingPage> Load(string board)
            {
                LastBoard=board;if(Fail)throw new System.IO.IOException("offline");
                var entry=new RankingEntry{Rank=3,Score=RankingScore.EncodeOverall(9950,400000),PlayerName="기존 Game Center 기록"};
                return Task.FromResult(new RankingPage{Entries=new[]{entry},LocalPlayer=entry,TotalPlayers=3});
            }
        }
    }
}
