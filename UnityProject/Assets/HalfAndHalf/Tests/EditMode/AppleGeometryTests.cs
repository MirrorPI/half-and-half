using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using HalfAndHalf.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class AppleGeometryTests
    {
        [TestCase(true, 1)] [TestCase(false, -1)]
        public void ExposedCapFacesOutward(bool left, int direction)
        {
            var body = AppleGeometry.Body(); var slice = AppleGeometry.Slice(body, .25f, left);
            try
            {
                var triangles = slice.GetTriangles(1); var vertices = slice.vertices;
                Assert.That(triangles.Length, Is.GreaterThan(0));
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                    Assert.That(a.x, Is.EqualTo(.25f).Within(.0001f));
                    Assert.That(Vector3.Cross(b - a, c - a).x * direction, Is.GreaterThanOrEqualTo(-.0000001f));
                }
            }
            finally { Object.DestroyImmediate(slice); Object.DestroyImmediate(body); }
        }
        [Test] public void AuthoredB01ConservesMassAndHasExactCenterSplit()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AppleDefinitionAsset>("Assets/HalfAndHalf/Content/B01/B01Prototype.asset");
            var definition = asset.Definition(); var split = definition.Mass.Split(0);
            Assert.That(split.Left, Is.EqualTo(split.Right));
            Assert.That(Scoring.Accuracy(split.Left, split.Right), Is.EqualTo(10000));
            for (int i = -30; i <= 30; i++)
            {
                var a = definition.Mass.Split(i / 20m); var b = definition.Mass.Split(-i / 20m);
                Assert.That(a.Left + a.Right, Is.EqualTo(definition.Mass.TotalMass));
                Assert.That(a.Left, Is.EqualTo(b.Right).Within(.000000000000001m));
            }
        }
    }
}
