using System;
using System.Linq;
using System.Collections.Generic;
using HalfAndHalf.Content;
using HalfAndHalf.Core;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class JejuIslandV2Tests
    {
        static VisualSampleAsset Asset => AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/N05/JejuV2/N05Jeju.asset");
        static double Volume(Mesh mesh)
        {
            var v=mesh.vertices;var t=mesh.triangles;double sum=0;
            for(int i=0;i<t.Length;i+=3)sum+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;
            return Math.Abs(sum);
        }
        static string[] PlaneBoundary(Mesh mesh,int submesh,float x)
        {
            var edges=new Dictionary<string,int>();var vertices=mesh.vertices;var t=mesh.GetTriangles(submesh);
            Func<Vector3,string> key=p=>Math.Round(p.y*100000)+","+Math.Round(p.z*100000);
            for(int i=0;i<t.Length;i+=3)for(int j=0;j<3;j++)
            {
                var a=vertices[t[i+j]];var b=vertices[t[i+(j+1)%3]];
                if(Mathf.Abs(a.x-x)>.00001f||Mathf.Abs(b.x-x)>.00001f)continue;
                string ka=key(a),kb=key(b);if(ka==kb)continue;
                string edge=string.CompareOrdinal(ka,kb)<0?ka+"/"+kb:kb+"/"+ka;
                edges.TryGetValue(edge,out int count);edges[edge]=count+1;
            }
            return edges.Where(p=>p.Value%2==1).Select(p=>p.Key).OrderBy(k=>k).ToArray();
        }
        [Test] public void AuthoredIslandFitsMobileBudgetAndUsesGenericSectionRenderer()
        {
            Assert.That(Asset,Is.Not.Null);Assert.That(Asset.ContentVersion,Is.EqualTo("N05-jeju-blender-v2"));
            Assert.That(VoxelPrototypeVisual.CanRender(Asset),Is.False);
            var filters=Asset.Model.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Sum(f=>f.sharedMesh.triangles.Length/3),Is.LessThanOrEqualTo(18000));
            Assert.That(filters.Length,Is.LessThanOrEqualTo(80));
            foreach(var f in filters){float limit=f.GetComponent<SamplePart>().Density>0?1.35f:1.4f;Assert.That(f.sharedMesh.bounds.min.x,Is.GreaterThan(-limit));Assert.That(f.sharedMesh.bounds.max.x,Is.LessThan(limit));}
            Assert.That(Asset.Model.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial.mainTexture==null),Is.True);
        }
        [TestCase(-.8f),TestCase(-.4f),TestCase(0f),TestCase(.217f),TestCase(.7f),TestCase(1.05f)]
        public void RealIslandSectionsConserveVolumeAndFaceOutward(float x)
        {
            foreach(var part in Asset.Model.GetComponentsInChildren<SamplePart>())
            {
                if(part.Density<=0)continue;
                var mesh=part.GetComponent<MeshFilter>().sharedMesh;
                if(x<=mesh.bounds.min.x||x>=mesh.bounds.max.x)continue;
                var left=MeshSection.Slice(mesh,x,true);var right=MeshSection.Slice(mesh,x,false);
                try
                {
                    Assert.That(Volume(left)+Volume(right),Is.EqualTo(Volume(mesh)).Within(.00001),part.name);
                    foreach(var half in new[]{left,right})
                    {
                        Assert.That(PlaneBoundary(half,1,x),Is.EqualTo(PlaneBoundary(half,0,x)),part.name+" cap must follow the actual skin contour");
                        var v=half.vertices;var t=half.GetTriangles(1);Assert.That(t.Length,Is.GreaterThan(0),part.name);
                        for(int i=0;i<t.Length;i+=3){var n=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]);Assert.That(n.x*(half==left?1:-1),Is.GreaterThanOrEqualTo(-.0000001f),part.name);}
                    }
                }
                finally{UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
            }
        }
        [Test] public void IslandMassStaysMonotonicAndConservesItsTotal()
        {
            var d=Asset.Definition();Assert.That(d.Mass.TotalMass,Is.EqualTo(100m));
            decimal previous=0;
            for(int i=-135;i<=135;i++)
            {
                decimal x=i/100m;var split=d.Mass.Split(x);Assert.That(split.Left+split.Right,Is.EqualTo(100m));
                Assert.That(split.Left,Is.InRange(0m,100m));Assert.That(split.Right,Is.InRange(0m,100m));
                Assert.That(split.Left,Is.GreaterThanOrEqualTo(previous));previous=split.Left;
            }
        }
    }
}
