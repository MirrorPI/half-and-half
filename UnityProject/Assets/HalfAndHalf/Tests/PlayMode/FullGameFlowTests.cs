#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace HalfAndHalf.Tests
{
    public sealed class FullGameFlowTests
    {
        const string Evidence="../reports/task-7-8-functional/";
        static AttemptResult Good(ObjectDefinition d,long ms)
        {
            var map=d.MassAt(ms);decimal lo=-1.35m,hi=1.35m;
            for(int i=0;i<30;i++){decimal x=(lo+hi)/2;if(map.Split(x).Left<map.TotalMass/2)lo=x;else hi=x;}
            return Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,(lo+hi)/2,ms,TransformState.Identity,true));
        }
        static void Shot(string name)=>ScreenCapture.CaptureScreenshot(Path.GetFullPath(Evidence+name+".png"));
        [UnityTest] public IEnumerator FullCollectionPlaysSpaceStagesSavesAndNavigates()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Gameplay29.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();var game=m.Game;var space=game.Presentation.Space;
            var defs=m.Progress.Content.ToArray();Assert.That(defs.Length,Is.EqualTo(29));
            // MvpTestIsolation supplies a temporary path before Start; real player saves are untouched.
            Assert.That(m.SavePath,Does.Contain("HalfSceneTest-"));
            var seeded=CollectionProgress.Restore(defs,defs.Take(20).Select(d=>Good(d,1000)),19);
            new LocalProgressStore(m.SavePath).Save(seeded);m.Reload();m.Open();yield return null;
            Assert.That(m.Progress.RecordedCount,Is.EqualTo(20));Assert.That(m.Progress.UnlockedCount,Is.EqualTo(21));
            Assert.That(m.Progress.OverallBest,Is.Null);Assert.That(m.Items.Length,Is.EqualTo(29));Shot("collection-20-of-29");yield return null;
            m.Close();
            for(int index=20;index<29;index++){
                Assert.That(m.Select(index),Is.True,defs[index].Id);
                game.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
                var aim=Good(defs[index],0);
                var point=game.GameCamera.WorldToScreenPoint(new Vector3((float)aim.Snapshot.KnifeX,0,0));
                if(index==25){yield return null;Shot("solar-ready");yield return null;}
                Assert.That(game.BeginPointer(712,point),Is.True);Assert.That(game.ReleasePointer(712,point),Is.True);
                var cut=game.Session.Result;Assert.That(space.FrozenMilliseconds,Is.EqualTo(cut.ElapsedMilliseconds));
                long frozen=space.DisplayedMilliseconds;
                yield return new WaitForSecondsRealtime(.6f);
                Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Result));Assert.That(space.DisplayedMilliseconds,Is.EqualTo(frozen));
                Assert.That(cut.AccuracyUnits,Is.GreaterThanOrEqualTo(9000),defs[index].Id);
                Assert.That(m.Progress.Best(index),Is.Not.Null);Assert.That(space.FragmentCount,Is.EqualTo(2));
                if(index==25){Shot("solar-result");yield return null;}
                if(index==27){Shot("expanding-galaxy-result");yield return null;}
                game.Retry();Assert.That(space.FrozenMilliseconds,Is.EqualTo(-1));Assert.That(space.DisplayedMilliseconds,Is.Zero);
            }
            Assert.That(m.Progress.OverallBest,Is.Not.Null);Assert.That(m.Progress.RecordedCount,Is.EqualTo(29));
            Assert.That(m.SavePending,Is.False);m.Reload();Assert.That(m.Progress.RecordedCount,Is.EqualTo(29));
            Assert.That(m.Progress.OverallBest.Records.Count,Is.EqualTo(29));
            var ranking=Object.FindFirstObjectByType<RankingPanel>();var controller=Object.FindFirstObjectByType<GameCenterController>();
            yield return null;ranking.Open();yield return null;
            Assert.That(ranking.Panel.activeSelf,Is.True);Assert.That(ranking.LocalRecord.text,Does.Contain("온라인 미연결"));Shot("individual-ranking-pending");yield return null;
            ranking.ObjectButtons[2].onClick.Invoke();yield return null;
            Assert.That(ranking.OverallSelected,Is.True);Assert.That(ranking.LocalRecord.text,Does.Contain("종합 최고"));
            Assert.That(controller.OverallConfigured,Is.False);Shot("overall-complete-pending");yield return null;
            var settings=Object.FindFirstObjectByType<GameSettings>();settings.OpenButton.onClick.Invoke();
            Assert.That(settings.Panel.activeSelf,Is.False,"Only one modal can open at a time");
            ranking.Close();settings.OpenButton.onClick.Invoke();yield return null;
            Assert.That(settings.Panel.activeSelf,Is.True);Shot("settings");yield return null;
            settings.CloseButton.onClick.Invoke();yield return null;
            Assert.That(game.UiOpen,Is.False);Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Dropping));
            File.WriteAllText(Path.GetFullPath(Evidence+"play-flow.json"),JsonUtility.ToJson(new Summary{records=m.Progress.RecordedCount,overallAccuracy=m.Progress.OverallBest.AccuracyUnits,totalMs=m.Progress.OverallBest.TimeMilliseconds,spaceCuts=9,onlineOverallEnabled=controller.OverallConfigured,userSaveTouched=false},true));
        }
        [UnityTest] public IEnumerator FinalRankingScreensAndModalButtons()
        {
            bool background=Application.runInBackground;Application.runInBackground=true;
            try {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Gameplay29.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
                var m=Object.FindFirstObjectByType<MvpCollection>();var defs=m.Progress.Content.ToArray();
                var records=defs.Select(d=>Good(d,1000)).ToArray();
                new LocalProgressStore(m.SavePath).Save(CollectionProgress.Restore(defs,records,28));m.Reload();yield return null;
                m.Open();yield return null;
                var scroll=m.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);scroll.verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();
                Shot("collection-space-stages");yield return null;m.Close();
                var ranking=Object.FindFirstObjectByType<RankingPanel>();ranking.Open();yield return null;
                ranking.ObjectButtons[2].onClick.Invoke();yield return null;Shot("overall-complete-pending");yield return null;
                var phase=m.Game.Session.Phase;m.Game.Hud.RetryButton.onClick.Invoke();Assert.That(m.Game.Session.Phase,Is.EqualTo(phase));
                Assert.That(m.Game.Hud.RetryButton.interactable,Is.False);ranking.Close();
                foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(m.SavePath+suffix))File.Delete(m.SavePath+suffix);
                new LocalProgressStore(m.SavePath).Save(CollectionProgress.Restore(defs,records.Take(28),27));m.Reload();yield return null;
                ranking.Open();yield return null;ranking.ObjectButtons[2].onClick.Invoke();yield return null;
                Assert.That(ranking.LocalRecord.text,Does.Contain("미참가"));Assert.That(m.Progress.OverallBest,Is.Null);
                Shot("overall-incomplete");yield return null;ranking.Close();
                var settings=Object.FindFirstObjectByType<GameSettings>();settings.OpenButton.onClick.Invoke();yield return null;
                phase=m.Game.Session.Phase;m.Game.Hud.RetryButton.onClick.Invoke();Assert.That(m.Game.Session.Phase,Is.EqualTo(phase));
                Assert.That(settings.Music.interactable,Is.True);Shot("settings");yield return null;
                settings.CloseButton.onClick.Invoke();Assert.That(m.Game.UiOpen,Is.False);
            } finally {Application.runInBackground=background;}
        }
        [Serializable] sealed class Summary {public int records,overallAccuracy,spaceCuts;public long totalMs;public bool onlineOverallEnabled,userSaveTouched;}
    }
}
#endif
