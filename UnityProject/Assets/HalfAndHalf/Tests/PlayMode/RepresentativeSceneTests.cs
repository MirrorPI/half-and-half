using System.Collections;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace HalfAndHalf.Tests
{
    public sealed class RepresentativeSceneTests
    {
        [UnityTest] public IEnumerator MissingIceDoesNotContactCosmeticWaterline()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var gallery=Object.FindFirstObjectByType<SampleGallery>();gallery.Select(4);var game=gallery.Game;
            game.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
            game.Presentation.Aim(1.10m);var screen=game.GameCamera.WorldToScreenPoint(new Vector3(1.10f,0,0));
            Assert.That(game.BeginPointer(42,screen),Is.True);Assert.That(game.ReleasePointer(42,screen),Is.True);
            game.enabled=false;game.Presentation.AnimateCut(1);
            Assert.That(game.Presentation.ContactProgress,Is.EqualTo(float.PositiveInfinity));
            Assert.That(game.Presentation.Audio.CutSource.isPlaying,Is.False);game.enabled=true;
        }
        [UnityTest] public IEnumerator MaterialSoundStartsAtBladeContact()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var game=Object.FindFirstObjectByType<AppleGame>();
            game.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
            game.Presentation.Aim(0m);
            var screen=game.GameCamera.WorldToScreenPoint(Vector3.zero);
            Assert.That(game.BeginPointer(42,screen),Is.True);Assert.That(game.ReleasePointer(42,screen),Is.True);
            game.enabled=false;
            game.Presentation.AnimateCut(.01f);Assert.That(game.Presentation.Audio.CutSource.isPlaying,Is.False);
            game.Presentation.AnimateCut(.20f);Assert.That(game.Presentation.Audio.CutSource.isPlaying,Is.True);
            game.enabled=true;
        }
        [UnityTest] public IEnumerator EveryRepresentativeCutsFrozenMassAndRetryClearsFragments()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay");yield return null;
            var gallery=Object.FindFirstObjectByType<SampleGallery>();var game=gallery.Game;
            Assert.That(gallery.Samples.Length, Is.EqualTo(7));
            var audio=game.Presentation.Audio;
            Assert.That(audio.CutSource.outputAudioMixerGroup.name,Is.EqualTo("Cut"));
            Assert.That(audio.ResultSource.outputAudioMixerGroup.name,Is.EqualTo("UI"));
            Assert.That(audio.MusicSource.outputAudioMixerGroup.name,Is.EqualTo("Music"));
            foreach(var sample in gallery.Samples)
            {
                gallery.Select(System.Array.IndexOf(gallery.Samples,sample));
                game.Session.Land((long)(Time.realtimeSinceStartupAsDouble*1000),TransformState.Identity);
                game.Presentation.Aim(.18m);
                var screen=game.GameCamera.WorldToScreenPoint(new Vector3(.18f,0,0));
                Assert.That(game.BeginPointer(42,screen),Is.True);Assert.That(game.ReleasePointer(42,screen),Is.True);
                var frozen=game.Session.Result;Assert.That(frozen.Snapshot.ObjectId,Is.EqualTo(sample.ObjectId));
                yield return new WaitForSecondsRealtime(.55f);
                Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Result));
                Assert.That(game.Session.Result,Is.SameAs(frozen));
                Assert.That(game.Presentation.FragmentCount,Is.EqualTo(2));
                game.Retry();
                foreach(var fragment in game.Presentation.BodyRoot.GetComponentsInChildren<Transform>())
                    Assert.That(fragment.name=="Left"||fragment.name=="Right",Is.False,"Retry must hide fragments immediately before deferred destruction");
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(game.Presentation.FragmentCount,Is.Zero);Assert.That(audio.CutSource.isPlaying,Is.False);
                Assert.That(game.GetComponents<AudioSource>().Length,Is.EqualTo(4));
            }
        }
    }
}
