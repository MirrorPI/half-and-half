using System;
using HalfAndHalf.Content;
using HalfAndHalf.Core;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace HalfAndHalf.Tests
{
    public sealed class MvpContentTests
    {
        static readonly string[] Ids={"B01","B02","B03","B04","B05","A01","A02","A03","A04","A05"};
        [Test] public void TenStaticPuzzlesHaveUniqueAudioAndReachablePerfectCuts()
        {
            var clips=new System.Collections.Generic.HashSet<AudioClip>();
            foreach(var id in Ids)
            {
                var a=AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/"+id+"/"+id+"Sample.asset");Assert.That(a,Is.Not.Null,id);
                Assert.That(a.Model,Is.Not.Null,id);Assert.That(a.CutVariants.Length,Is.EqualTo(3));
                foreach(var clip in a.CutVariants){Assert.That(clip,Is.Not.Null,id);Assert.That(clips.Add(clip),Is.True);}
                var d=a.Definition();decimal lo=-1.35m,hi=1.35m;
                for(int i=0;i<60;i++){decimal mid=(lo+hi)/2;if(d.Mass.Split(mid).Left<d.Mass.TotalMass/2)lo=mid;else hi=mid;}
                var result=Scoring.Evaluate(d,new CutSnapshot(id,d.ContentVersion,(lo+hi)/2,1000,TransformState.Identity,true));
                Assert.That(result.AccuracyUnits,Is.EqualTo(10000),id);Assert.That(result.LeftMass+result.RightMass,Is.EqualTo(d.Mass.TotalMass));
                var x=(lo+hi)/2;var before=d.Mass.Split(x-.0001m);var after=d.Mass.Split(x+.0001m);
                Assert.That(after.Left,Is.GreaterThan(before.Left),id+" must continuously divide mass within cells");
                foreach(var part in a.Model.GetComponentsInChildren<SamplePart>())Assert.That(part.GetComponent<MeshFilter>().sharedMesh.bounds.max.x,Is.LessThanOrEqualTo(1.35f),id+" stays in knife range");
            }
        }
        [Test] public void AsymmetricPuzzlesDifferFromSymmetricApple()
        {
            foreach(var id in new[]{"B04","B05","A01","A02","A03","A04","A05"})
            {
                var a=AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/"+id+"/"+id+"Sample.asset");Assert.That(a,Is.Not.Null,id);
                var mass=a.Definition().Mass;decimal left=mass.Split(0).Left/mass.TotalMass;
                Assert.That(Math.Abs(left-.5m),Is.GreaterThan(.006m),id+" must require inference beyond the screen midpoint");
            }
        }
        [Test] public void NewFruitSurfacesKeepOutwardCapsAndSignedVolumeAtManyCuts()
        {
            foreach(var id in new[]{"B05","A01","A02","A03"})
            {
                var asset=AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/"+id+"/"+id+"Sample.asset");
                foreach(var part in asset.Model.GetComponentsInChildren<SamplePart>())
                {
                    if(part.Density<=0)continue;var source=part.GetComponent<MeshFilter>().sharedMesh;double total=SignedVolume(source);Assert.That(total,Is.GreaterThan(0),id+" "+part.name);
                    foreach(float x in new[]{-.85f,-.4f,0,.25f,.7f})
                    {
                        var left=MeshSection.Slice(source,x,true);var right=MeshSection.Slice(source,x,false);
                        try
                        {
                            Assert.That(SignedVolume(left)+SignedVolume(right),Is.EqualTo(total).Within(.00002),id+" "+part.name+" "+x);
                            foreach(var pair in new[]{Tuple.Create(left,Vector3.right),Tuple.Create(right,Vector3.left)})
                            {
                                var v=pair.Item1.vertices;var tri=pair.Item1.GetTriangles(1);
                                for(int j=0;j<tri.Length;j+=3)Assert.That(Vector3.Dot(Vector3.Cross(v[tri[j+1]]-v[tri[j]],v[tri[j+2]]-v[tri[j]]),pair.Item2),Is.GreaterThanOrEqualTo(-.000001f));
                            }
                        }
                        finally{UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
                    }
                }
            }
        }
        static double SignedVolume(Mesh mesh)
        {var v=mesh.vertices;var t=mesh.triangles;double sum=0;for(int i=0;i<t.Length;i+=3)sum+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;return sum;}
    }
}
