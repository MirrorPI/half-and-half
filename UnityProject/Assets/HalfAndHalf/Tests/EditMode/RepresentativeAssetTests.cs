using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class RepresentativeAssetTests
    {
        // Missing definitions must fail before building a gallery that appears complete.
        [TestCase("B01"), TestCase("A04"), TestCase("L01"), TestCase("L02"), TestCase("N03"), TestCase("P02"), TestCase("G03")]
        public void RepresentativeHasRealModelAndThreeDistinctLoadedClips(string id)
        {
            var sample = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/HalfAndHalf/Content/" + id + "/" + id + "Sample.asset");
            Assert.That(sample, Is.Not.Null, "Missing representative definition: " + id);
            var data = new SerializedObject(sample);
            var model = data.FindProperty("Model").objectReferenceValue as GameObject;
            Assert.That(model, Is.Not.Null);
            Assert.That(model.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0));
            var clips = data.FindProperty("CutVariants");
            Assert.That(clips.arraySize, Is.EqualTo(3));
            var seen = new System.Collections.Generic.HashSet<AudioClip>();
            for (int i = 0; i < 3; i++)
            {
                var clip = clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                Assert.That(clip, Is.Not.Null); Assert.That(clip.samples, Is.GreaterThan(100));
                Assert.That(clip.channels, Is.EqualTo(1)); Assert.That(seen.Add(clip), Is.True);
            }
        }
    }
}
