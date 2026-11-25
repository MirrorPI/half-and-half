using System;
using NUnit.Framework;
using UnityEngine;
namespace HalfAndHalf.Tests
{
    public sealed class VoxelAppleTests
    {
        // Catches snapping the blade to a voxel boundary, losing cells, or reversed/open caps.
        [TestCase(-.713f),TestCase(0f),TestCase(.217f),TestCase(.993f)]
        public void ContinuousCutConservesVolumeAndHasOutwardCap(float x)
        {
            var type=typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.VoxelAppleGeometry");
            Assert.That(type,Is.Not.Null,"Missing volumetric B01 builder");
            var method=type.GetMethod("Build");
            var whole=(Mesh)method.Invoke(null,new object[]{float.PositiveInfinity,true});
            var left=(Mesh)method.Invoke(null,new object[]{x,true});
            var right=(Mesh)method.Invoke(null,new object[]{x,false});
            try
            {
                Assert.That(Volume(left),Is.GreaterThan(0));Assert.That(Volume(right),Is.GreaterThan(0));
                Assert.That(Volume(left)+Volume(right),Is.EqualTo(Volume(whole)).Within(.00002));
                if(x==0)Assert.That(Volume(left),Is.EqualTo(Volume(right)).Within(.00002));
                foreach(var pair in new[]{(left,1f),(right,-1f)})
                {
                    var v=pair.Item1.vertices;var cap=pair.Item1.GetTriangles(1);
                    Assert.That(cap.Length,Is.GreaterThan(0));
                    for(int i=0;i<cap.Length;i+=3)
                    {
                        Assert.That(v[cap[i]].x,Is.EqualTo(x).Within(.000001));
                        Assert.That(Vector3.Cross(v[cap[i+1]]-v[cap[i]],v[cap[i+2]]-v[cap[i]]).x*pair.Item2,Is.GreaterThan(0));
                    }
                    Assert.That(pair.Item1.colors32.Length,Is.EqualTo(pair.Item1.vertexCount),"Palette lost at cut");
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(whole);UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
        }
        [TestCase(.36f,true),TestCase(.36f,false),TestCase(.08f,true),TestCase(.08f,false)]
        public void LeavesAndStemCannotCrossKnifePlaneAfterCut(float x,bool left)
        {
            var type=typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.VoxelAppleGeometry");
            var method=type.GetMethod("DecorationSide");Assert.That(method,Is.Not.Null,"Decoration must be cut rather than assigned whole");
            var mesh=(Mesh)method.Invoke(null,new object[]{x,left});
            try{Assert.That(mesh.vertexCount,Is.GreaterThan(0));foreach(var v in mesh.vertices)Assert.That(left?v.x-x:x-v.x,Is.LessThanOrEqualTo(.000001f));}
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static double Volume(Mesh m){var v=m.vertices;var t=m.triangles;double sum=0;for(int i=0;i<t.Length;i+=3)sum+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;return sum;}
    }
}
