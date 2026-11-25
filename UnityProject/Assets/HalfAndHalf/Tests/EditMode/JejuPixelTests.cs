using System;
using System.Linq;
using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class JejuPixelTests
    {
        static VisualSampleAsset Asset => AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/N05/JejuPixel/N05JejuPixel.asset");
        [Test] public void ReauthoringReplacesDecodedVolumeWithoutDomainReload()
        {
            var source=Asset.Model.GetComponent<JejuPixelSource>();var old=source.Volume();
            JejuPixelSource.InvalidatePreparedCache();
            Assert.That(source.Volume(),Is.Not.SameAs(old));
        }
        static double Volume(Mesh m)
        {
            var v=m.vertices;var t=m.triangles;double result=0;
            for(int i=0;i<t.Length;i+=3)result+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;
            return Math.Abs(result);
        }
        [Test] public void PixelIslandUsesPixelRendererAndFitsWholeIslandBudget()
        {
            Assert.That(Asset,Is.Not.Null,"A smooth V2 model is not the requested pixel island");
            Assert.That(Asset.ContentVersion,Is.EqualTo("N05-jeju-pixel-v1"));
            Assert.That(VoxelPrototypeVisual.CanRender(Asset),Is.True);
            var filters=Asset.Model.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length,Is.LessThanOrEqualTo(2));
            Assert.That(filters.Sum(f=>f.sharedMesh.triangles.Length/3),Is.LessThan(30000));
            foreach(var f in filters)
            {
                Assert.That(f.sharedMesh.bounds.min.x,Is.GreaterThanOrEqualTo(-1.25f));
                Assert.That(f.sharedMesh.bounds.max.x,Is.LessThanOrEqualTo(1.25f));
                foreach(var p in f.sharedMesh.vertices) Assert.That(p.x*32,Is.EqualTo(Mathf.Round(p.x*32)).Within(.00001));
            }
        }
        [TestCase(-.8f),TestCase(-.4f),TestCase(0f),TestCase(.217f),TestCase(.7f),TestCase(1.05f)]
        public void PixelCutsConserveGeometryAndMatchMass(float x)
        {
            Assert.That(Asset,Is.Not.Null);
            var source=Asset.Model.GetComponent<JejuPixelSource>();
            var volume=source.Volume();var whole=volume.Build(float.PositiveInfinity,true);
            var left=volume.Build(x,true);var right=volume.Build(x,false);
            try
            {
                Assert.That(Volume(left)+Volume(right),Is.EqualTo(Volume(whole)).Within(.00001));
                var live=volume.Mass().Split((decimal)x);var authored=Asset.Definition().Mass.Split((decimal)x);
                Assert.That(live.Left,Is.EqualTo(authored.Left));Assert.That(authored.Left+authored.Right,Is.EqualTo(100m));
                foreach(var mesh in new[]{left,right})
                {
                    var v=mesh.vertices;var caps=mesh.GetTriangles(1);
                    Assert.That(caps.Length,Is.GreaterThan(0));
                    foreach(int i in caps)Assert.That(v[i].x,Is.EqualTo(x).Within(.00001));
                    for(int i=0;i<caps.Length;i+=3)
                    {
                        var n=Vector3.Cross(v[caps[i+1]]-v[caps[i]],v[caps[i+2]]-v[caps[i]]);
                        Assert.That(n.x*(mesh==left?1:-1),Is.GreaterThanOrEqualTo(0));
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(whole);UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
        }
    }
}
