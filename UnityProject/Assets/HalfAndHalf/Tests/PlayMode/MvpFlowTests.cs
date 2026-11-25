using System;
using System.Collections;
using System.IO;
using HalfAndHalf.Content;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace HalfAndHalf.Tests
{
    public sealed class MvpFlowTests
    {
        string path;
        [SetUp] public void Setup(){path=Path.Combine(Path.GetTempPath(),"HalfMvp-"+Guid.NewGuid()+".json");}
        [TearDown] public void Cleanup(){foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);}
        [UnityTest] public IEnumerator TenContentCompletesThroughActualCutAndRecoversOnReload()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();Assert.That(m,Is.Not.Null,"10-content MVP must be wired into Gameplay");
            m.SavePath=path;m.Reload();Assert.That(m.Progress.UnlockedCount,Is.EqualTo(1));Assert.That(m.Select(1),Is.False);
            var g=m.Game;
            for(int i=0;i<10;i++)
            {
                Assert.That(m.Select(i),Is.True);Assert.That(g.Presentation.Knife.gameObject.activeSelf,Is.False);
                var d=m.Samples[i].Definition();decimal lo=-1.35m,hi=1.35m;
                for(int j=0;j<60;j++){decimal mid=(lo+hi)/2;if(d.Mass.Split(mid).Left<d.Mass.TotalMass/2)lo=mid;else hi=mid;}
                g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
                var point=g.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));Assert.That(g.BeginPointer(42,point),Is.True);Assert.That(g.ReleasePointer(42,point),Is.True);
                yield return new WaitForSecondsRealtime(.53f);
                Assert.That(m.Progress.Best(i),Is.Not.Null);Assert.That(m.Progress.Best(i).AccuracyUnits,Is.EqualTo(10000));
                Assert.That(m.Outcome.text,Does.Contain("NEW PERSONAL BEST"));Assert.That(g.Presentation.FragmentCount,Is.EqualTo(2));
                Assert.That(m.Progress.UnlockedCount,Is.EqualTo(Math.Min(10,i+2)));
            }
            m.Reload();Assert.That(m.Progress.UnlockedCount,Is.EqualTo(10));Assert.That(m.Progress.Selected,Is.EqualTo(9));
            for(int i=0;i<10;i++)Assert.That(m.Progress.Best(i).AccuracyUnits,Is.EqualTo(10000));
        }
        [UnityTest] public IEnumerator CollectionBlocksCutsAndLockedButtonsAndCloseRestarts()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();Assert.That(m,Is.Not.Null);m.SavePath=path;m.Reload();
            m.Game.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);m.OpenButton.onClick.Invoke();
            Assert.That(m.Panel.activeSelf,Is.True);Assert.That(m.Items[0].interactable,Is.True);Assert.That(m.Items[1].interactable,Is.False);
            Assert.That(m.Game.BeginPointer(42,Vector2.zero),Is.False);m.CloseButton.onClick.Invoke();Assert.That(m.Game.UiOpen,Is.False);Assert.That(m.Game.Session.Phase,Is.EqualTo(AttemptPhase.Dropping));
            Assert.That(m.Game.Presentation.Knife.gameObject.activeSelf,Is.False);
        }
        [UnityTest] public IEnumerator InterruptedCutDoesNotUnlockOrSaveBest()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();Assert.That(m,Is.Not.Null);m.SavePath=path;m.Reload();var g=m.Game;
            g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);var p=g.GameCamera.WorldToScreenPoint(Vector3.zero);g.BeginPointer(42,p);g.ReleasePointer(42,p);g.SetInterrupted();
            yield return new WaitForSecondsRealtime(.55f);Assert.That(m.Progress.Best(0),Is.Null);Assert.That(m.Progress.UnlockedCount,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SettingsPersistAcrossScenes()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var settings=Object.FindFirstObjectByType<GameSettings>();float effects=settings.Effects.value,music=settings.Music.value;bool haptic=settings.Vibration.isOn;
            try
            {
                settings.OpenButton.onClick.Invoke();settings.Effects.value=.31f;settings.Music.value=.27f;settings.Vibration.isOn=false;settings.CloseButton.onClick.Invoke();
                yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;settings=Object.FindFirstObjectByType<GameSettings>();
                Assert.That(settings.Effects.value,Is.EqualTo(.31f).Within(.0001f));Assert.That(settings.Music.value,Is.EqualTo(.27f).Within(.0001f));Assert.That(settings.Vibration.isOn,Is.False);
                Assert.That(Object.FindFirstObjectByType<MvpCollection>(),Is.Not.Null);
            }
            finally{settings.Effects.value=effects;settings.Music.value=music;settings.Vibration.isOn=haptic;}
        }
        [UnityTest] public IEnumerator SettingsModalCannotBeBypassedByCollectionNavigation()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();m.SavePath=path;m.Reload();
            var settings=Object.FindFirstObjectByType<GameSettings>();settings.OpenButton.onClick.Invoke();
            m.OpenButton.onClick.Invoke();Assert.That(m.Panel.activeSelf,Is.False,"settings owns the modal input block");
            Assert.That(m.Select(0),Is.False);Assert.That(m.Game.UiOpen,Is.True);
            settings.CloseButton.onClick.Invoke();Assert.That(m.Game.UiOpen,Is.False);
        }
        [UnityTest] public IEnumerator FailedSaveKeepsBestAndRetriesOnNextAttempt()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var m=Object.FindFirstObjectByType<MvpCollection>();m.SavePath=path;m.Reload();Directory.CreateDirectory(path);
            var g=m.Game;g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);var p=g.GameCamera.WorldToScreenPoint(Vector3.zero);g.BeginPointer(42,p);g.ReleasePointer(42,p);
            yield return new WaitForSecondsRealtime(.55f);Assert.That(m.SavePending,Is.True);Assert.That(m.Progress.Best(0).AccuracyUnits,Is.EqualTo(10000));
            Directory.Delete(path);g.Retry();g.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
            yield return new WaitForSecondsRealtime(.1f);g.BeginPointer(42,p);g.ReleasePointer(42,p);
            yield return new WaitForSecondsRealtime(.55f);Assert.That(m.SavePending,Is.False);Assert.That(m.RecoveryLabel.text,Is.Empty);
            Assert.That(new LocalProgressStore(path).Load(m.Progress.Content).Best(0).AccuracyUnits,Is.EqualTo(10000));
        }
    }
}
