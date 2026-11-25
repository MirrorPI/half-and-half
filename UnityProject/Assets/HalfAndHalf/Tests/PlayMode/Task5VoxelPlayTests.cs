#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
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
    public sealed class Task5VoxelPlayTests
    {
        [UnityTest] public IEnumerator RealTwentyPlaysUnlocksPersistsAndSwitchesRenderersWithoutTouchingPlayerSave()
        {
            const string scene="Assets/Scenes/Gameplay20.unity";Assert.That(File.Exists(scene),Is.True,"Real20 scene missing");
            GameCenterConfiguration config=null;
            UnityEngine.Events.UnityAction<Scene,LoadSceneMode> offline=(s,mode)=>{var gc=Object.FindFirstObjectByType<GameCenterController>();config=Object.Instantiate(gc.Configuration);config.EnableOnline=false;gc.Configuration=config;};
            SceneManager.sceneLoaded+=offline;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scene,new LoadSceneParameters(LoadSceneMode.Single));
            SceneManager.sceneLoaded-=offline;yield return null;
            try
            {
                var m=Object.FindFirstObjectByType<MvpCollection>();var game=m.Game;var p=game.Presentation;var gc=game.GetComponent<GameCenterController>();
                Assert.That(m.SavePath,Is.Not.Null,"MvpTestIsolation must redirect the save before Start");Assert.That(m.Samples.Length,Is.EqualTo(20));Assert.That(m.Progress.UnlockedCount,Is.EqualTo(1));
                var voxField=p.GetType().GetField("Voxel");Assert.That(voxField,Is.Not.Null);var voxel=(Component)voxField.GetValue(p);Assert.That(voxel,Is.Not.Null);Assert.That(p.Car,Is.Not.Null);
                var newIds=new[]{"L03","L04","L05","N01","N02","N04","N05"};
                for(int i=0;i<20;i++)
                {
                    Assert.That(m.Select(i),Is.True);double deadline=Time.realtimeSinceStartupAsDouble+5;
                    while(game.Session.Phase==AttemptPhase.Dropping&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Ready));var definition=m.Samples[i].Definition();Assert.That(definition.ContentVersion,Is.Not.EqualTo("task5-qa-fixture-v1"));
                    Assert.That(p.Car.gameObject.activeSelf,Is.EqualTo(definition.Id=="L02"));Assert.That(voxel.gameObject.activeSelf,Is.EqualTo(newIds.Contains(definition.Id)));
                    Assert.That(p.Knife.gameObject.activeSelf,Is.False);
                    decimal lo=-1.35m,hi=1.35m;for(int j=0;j<100;j++){decimal x=(lo+hi)/2;if(definition.Mass.Split(x).Left<definition.Mass.TotalMass/2)lo=x;else hi=x;}
                    var point=game.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));Assert.That(game.BeginPointer(741,point),Is.True);Assert.That(game.ReleasePointer(741,point),Is.True);var frozen=game.Session.Result;
                    yield return new WaitForSecondsRealtime(.58f);
                    Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Result));Assert.That(game.Session.Result,Is.SameAs(frozen));Assert.That(frozen.AccuracyUnits,Is.EqualTo(10000));Assert.That(frozen.LeftMass+frozen.RightMass,Is.EqualTo(definition.Mass.TotalMass));Assert.That(frozen.LeftPercent+frozen.RightPercent,Is.EqualTo(100m));
                    Assert.That(p.FragmentCount,Is.EqualTo(2));Assert.That(m.Progress.Best(i).Snapshot.ContentVersion,Is.EqualTo(definition.ContentVersion));Assert.That(m.SavePending,Is.False);
                    if(i==9){Assert.That(m.Progress.UnlockedCount,Is.EqualTo(11));Assert.That(m.Outcome.text,Does.Not.Contain("COLLECTION COMPLETE"));}
                    if(i==19)Assert.That(m.Outcome.text,Does.Contain("COLLECTION COMPLETE"));
                    game.Hud.RetryButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.08f);
                    Assert.That(p.FragmentCount,Is.Zero);Assert.That(p.Audio.CutSource.isPlaying,Is.False);Assert.That(p.Knife.gameObject.activeSelf,Is.False);Assert.That(game.GetComponents<AudioSource>().Length,Is.EqualTo(4));
                }
                Assert.That(m.Progress.UnlockedCount,Is.EqualTo(20));m.Reload();Assert.That(m.Progress.UnlockedCount,Is.EqualTo(20));
                for(int i=0;i<20;i++)Assert.That(m.Progress.Best(i).AccuracyUnits,Is.EqualTo(10000));
                m.Open();Assert.That(m.Items.Length,Is.EqualTo(20));Assert.That(m.ProgressLabel.text,Does.Contain("20 / 20"));m.Items[19].onClick.Invoke();Assert.That(m.Progress.Selected,Is.EqualTo(19));Assert.That(m.Panel.activeSelf,Is.False);
                yield return new WaitForSecondsRealtime(.65f);
                Assert.That(gc.Queue.Entries.Select(e=>e.objectId).Distinct().OrderBy(id=>id).ToArray(),Is.EqualTo(new[]{"A04","B01","B03"}));Assert.That(gc.Flow.IsSignedIn,Is.False);
            }
            finally{if(config!=null)Object.Destroy(config);}
        }
    }
}
#endif
