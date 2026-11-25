using System;
using NUnit.Framework;
using UnityEngine;
namespace HalfAndHalf.Tests
{
    public sealed class MeshSectionTests
    {
        [TestCase(true, .25f, .75f), TestCase(false, .25f, .25f)]
        public void CubeSectionHasClosedOutwardCapAndConservesVolume(bool left, float x, float expectedVolume)
        {
            var type=typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.MeshSection");
            Assert.That(type, Is.Not.Null, "Missing generic closed-section implementation");
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh=(Mesh)type.GetMethod("Slice").Invoke(null,new object[]{cube.GetComponent<MeshFilter>().sharedMesh,x,left});
            try
            {
                Assert.That(mesh.subMeshCount, Is.EqualTo(2));
                var v=mesh.vertices; var triangles=mesh.triangles; double volume=0;
                for(int i=0;i<triangles.Length;i+=3) volume+=Vector3.Dot(v[triangles[i]],Vector3.Cross(v[triangles[i+1]],v[triangles[i+2]]))/6d;
                Assert.That(Math.Abs(volume), Is.EqualTo(expectedVolume).Within(.0001));
                var cap=mesh.GetTriangles(1); Assert.That(cap.Length, Is.GreaterThan(0));
                for(int i=0;i<cap.Length;i+=3)
                {
                    Assert.That(v[cap[i]].x, Is.EqualTo(x).Within(.00001));
                    var n=Vector3.Cross(v[cap[i+1]]-v[cap[i]],v[cap[i+2]]-v[cap[i]]);
                    Assert.That(n.x*(left?1:-1), Is.GreaterThanOrEqualTo(0));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cube); UnityEngine.Object.DestroyImmediate(mesh); }
        }
    }
}
