using System;
using System.Linq;
using HalfAndHalf.Core;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class VoxelCarTests
    {
        static Type Geometry()
        {
            var type = typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.VoxelCarGeometry");
            Assert.That(type, Is.Not.Null, "Missing approved L02 volumetric trial");
            return type;
        }
        static Mesh Build(float x, bool left) => (Mesh)Geometry().GetMethod("Build").Invoke(null, new object[]{x,left});
        static MassMap Mass() => (MassMap)Geometry().GetMethod("Mass").Invoke(null,null);
        static double Volume(Mesh mesh)
        {
            var v=mesh.vertices;var t=mesh.triangles;double sum=0;
            for(int i=0;i<t.Length;i+=3)sum+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;
            return sum;
        }
        [TestCase(-.773f),TestCase(-.217f),TestCase(0f),TestCase(.701f)]
        public void ArbitraryCarCutConservesClosedVolumeAndPalette(float x)
        {
            var whole=Build(float.PositiveInfinity,true);var left=Build(x,true);var right=Build(x,false);
            try
            {
                Assert.That(Volume(left),Is.GreaterThan(0));Assert.That(Volume(right),Is.GreaterThan(0));
                Assert.That(Volume(left)+Volume(right),Is.EqualTo(Volume(whole)).Within(.00001));
                foreach(var pair in new[]{(left,1f),(right,-1f)})
                {
                    var v=pair.Item1.vertices;var cap=pair.Item1.GetTriangles(1);Assert.That(cap.Length,Is.GreaterThan(0));
                    Assert.That(pair.Item1.colors32.Length,Is.EqualTo(v.Length));
                    foreach(var p in v)Assert.That(pair.Item2>0?p.x-x:x-p.x,Is.LessThanOrEqualTo(.000001f));
                    for(int i=0;i<cap.Length;i+=3)
                    {
                        Assert.That(v[cap[i]].x,Is.EqualTo(x).Within(.000001f));
                        Assert.That(Vector3.Cross(v[cap[i+1]]-v[cap[i]],v[cap[i+2]]-v[cap[i]]).x*pair.Item2,Is.GreaterThan(0));
                    }
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(whole);UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
        }
        [Test] public void FrontEngineChangesMassFromUniformGeometricVolume()
        {
            var mass=Mass();var whole=Build(float.PositiveInfinity,true);var left=Build(0,true);
            try
            {
                Assert.That(mass.TotalMass,Is.EqualTo(100m));
                Assert.That(mass.Split(-1.35m).Left,Is.Zero);Assert.That(mass.Split(1.35m).Left,Is.EqualTo(100m));
                double uniform=100*Volume(left)/Volume(whole);
                Assert.That((double)mass.Split(0).Left,Is.LessThan(uniform-5),"Dense front engine must contribute beyond its visible volume");
                var split=mass.Split(.217m);Assert.That(split.Left+split.Right,Is.EqualTo(100m));
                Assert.That(mass.Split(.21705m).Left,Is.GreaterThan(split.Left),"Knife must not snap to a cell boundary");
            }
            finally {UnityEngine.Object.DestroyImmediate(whole);UnityEngine.Object.DestroyImmediate(left);}
        }
        [Test] public void CutPreservesEmptyCabinInsteadOfFillingItsContour()
        {
            var mesh=Build(0,true);
            try
            {
                // Hand-chosen air point above the seats, and a solid point on the roof.
                Assert.That(CapCovers(mesh,new Vector2(.22f,0)),Is.False,"Passenger air must not become a giant solid cap");
                Assert.That(CapCovers(mesh,new Vector2(.40f,0)),Is.True,"Roof shell must still close at the cut");
            }
            finally {UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void RepeatedCutsAndWithinCellGeometryRemainDeterministicAndContinuous()
        {
            var a=Build(.217f,true);var b=Build(.21705f,true);var repeated=Build(.217f,true);
            try
            {
                Assert.That(Volume(b),Is.GreaterThan(Volume(a)));
                Assert.That(repeated.vertices,Is.EqualTo(a.vertices));Assert.That(repeated.triangles,Is.EqualTo(a.triangles));Assert.That(repeated.colors32,Is.EqualTo(a.colors32));
            }
            finally {UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(repeated);}
        }
        static bool CapCovers(Mesh mesh, Vector2 point)
        {
            var vertices=mesh.vertices;var triangles=mesh.GetTriangles(1);
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]];
                float s1=Cross(new Vector2(b.y-a.y,b.z-a.z),point-new Vector2(a.y,a.z));
                float s2=Cross(new Vector2(c.y-b.y,c.z-b.z),point-new Vector2(b.y,b.z));
                float s3=Cross(new Vector2(a.y-c.y,a.z-c.z),point-new Vector2(c.y,c.z));
                if((s1>=0&&s2>=0&&s3>=0)||(s1<=0&&s2<=0&&s3<=0))return true;
            }
            return false;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }
}
