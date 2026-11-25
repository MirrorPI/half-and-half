#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
    public sealed class VoxelCarTrialTests
    {
        [UnityTest] public IEnumerator CarTrialRunsItsOwnMassAndCutWithoutRecordsOrOnlineInitialization()
        {
            const string scene="Assets/Scenes/L02VoxelTrial.unity";
            Assert.That(File.Exists(scene),Is.True,"Approved single-car trial scene missing");
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(scene,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game=UnityEngine.Object.FindFirstObjectByType<AppleGame>();
            Assert.That(game.GetComponent<MvpCollection>().enabled,Is.False);
            Assert.That(game.GetComponent<MvpCollection>().Progress,Is.Null);
            Assert.That(game.GetComponent<GameCenterController>().enabled,Is.False);
            Assert.That(game.GetComponent<GameCenterController>().Flow,Is.Null);
            var field=game.Presentation.GetType().GetField("Car");Assert.That(field,Is.Not.Null);
            var car=(Component)field.GetValue(game.Presentation);Assert.That(car,Is.Not.Null);
            foreach(float x in new[]{0f,.217f,-.773f})
            {
                double until=Time.realtimeSinceStartupAsDouble+4;
                while(game.Session.Phase==AttemptPhase.Dropping&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Ready));
                Assert.That(game.Presentation.Knife.gameObject.activeSelf,Is.False);
                var point=game.GameCamera.WorldToScreenPoint(new Vector3(x,0,0));
                Assert.That(game.BeginPointer(936,point),Is.True);Assert.That(game.ReleasePointer(936,point),Is.True);
                var frozen=game.Session.Result;
                Assert.That(frozen.Snapshot.ObjectId,Is.EqualTo("L02"));
                Assert.That(frozen.Snapshot.ContentVersion,Is.EqualTo("L02-fine-voxel-trial-v1"));
                Assert.That(frozen.LeftMass+frozen.RightMass,Is.EqualTo(100m));
                var clips=game.Presentation.Audio.CutVariants;Assert.That(clips.Length,Is.EqualTo(3));Assert.That(clips[0].name,Does.StartWith("L02_cut_"));
                yield return new WaitForSecondsRealtime(.55f);
                Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Result));Assert.That(game.Session.Result,Is.SameAs(frozen));
                Assert.That(game.Presentation.FragmentCount,Is.EqualTo(2));Assert.That(game.Presentation.Body.enabled,Is.False);
                var halves=car.GetComponentsInChildren<MeshFilter>();
                Assert.That(halves.Length,Is.EqualTo(2));
                foreach(var half in halves)
                {
                    bool left=half.name.Contains("left");
                    foreach(var v in half.sharedMesh.vertices)Assert.That(left?v.x-x:x-v.x,Is.LessThanOrEqualTo(.000001f));
                }
                game.Hud.RetryButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.08f);
                Assert.That(game.Presentation.FragmentCount,Is.Zero);Assert.That(game.Presentation.Audio.CutSource.isPlaying,Is.False);
                Assert.That(game.Presentation.Knife.gameObject.activeSelf,Is.False);Assert.That(game.GetComponents<AudioSource>().Length,Is.EqualTo(4));
            }
        }
    }
}
#endif
