using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
    public sealed class CutAudioTests
    {
        GameObject root;
        Component audio;
        Type type;
        AudioClip[] clips;
        [UnitySetUp] public IEnumerator Setup()
        {
            type = typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.CutAudio");
            Assert.That(type, Is.Not.Null, "Missing contact-synchronized audio controller");
            root = new GameObject("Audio test"); audio = root.AddComponent(type);
            clips = new AudioClip[3];
            for (int i = 0; i < 3; i++) clips[i] = AudioClip.Create("Material variant " + i, 48000, 1, 48000, false);
            type.GetField("CutVariants").SetValue(audio, clips);
            yield return null;
        }
        void Call(string method, params object[] args) => type.GetMethod(method).Invoke(audio, args);
        int Variant => (int)type.GetProperty("LastVariant").GetValue(audio);
        AudioSource Source => (AudioSource)type.GetField("CutSource").GetValue(audio);
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            if (clips != null) foreach (var clip in clips) UnityEngine.Object.Destroy(clip);
            yield return null;
        }
        [UnityTest] public IEnumerator MaterialSoundStartsOnlyAtContactAndOncePerCut()
        {
            Call("BeginCut"); Call("Progress", .2f, .28f);
            Assert.That(Source.isPlaying, Is.False);
            Call("Progress", .3f, .28f); var variant = Variant;
            Assert.That(Source.isPlaying, Is.True);
            Call("Progress", .8f, .28f); Assert.That(Variant, Is.EqualTo(variant));
            yield return null;
        }
        [UnityTest] public IEnumerator TenRapidRetriesNeverRepeatVariantOrLeaveAudioPlaying()
        {
            int previous = -1;
            for (int i = 0; i < 10; i++)
            {
                Call("BeginCut"); Call("Progress", .3f, .28f);
                Assert.That(Variant, Is.Not.EqualTo(previous)); previous = Variant;
                Call("ResetAttempt"); yield return new WaitForSecondsRealtime(.08f);
                Assert.That(Source.isPlaying, Is.False);
                Assert.That(root.GetComponents<AudioSource>().Length, Is.EqualTo(4));
            }
        }
    }
}
