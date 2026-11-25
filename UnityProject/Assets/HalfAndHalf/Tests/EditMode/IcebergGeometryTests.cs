using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace HalfAndHalf.Tests
{
    public sealed class IcebergGeometryTests
    {
        static double SignedVolume(Mesh mesh)
        {
            var v=mesh.vertices;var t=mesh.triangles;double sum=0;
            for(int i=0;i<t.Length;i+=3)sum+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;
            return sum;
        }
        [TestCase(0,.4467919)] [TestCase(1,1.2983465)]
        public void IceCapsFaceOutwardAndSignedVolumeIsConserved(int index,double expected)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HalfAndHalf/Content/N03/Ice"+index+".asset");
            Assert.That(SignedVolume(mesh),Is.EqualTo(expected).Within(.00003));
            var v=mesh.vertices;var t=mesh.triangles;Vector3 center=Vector3.zero;foreach(var p in v)center+=p;center/=v.Length;
            for(int i=0;i<t.Length;i+=3)
            {
                var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];
                Assert.That(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3-center),Is.GreaterThanOrEqualTo(-.00001f));
            }
            foreach(float cut in new[]{-.8f,0f,.5f})
            {
                var left=MeshSection.Slice(mesh,cut,true);var right=MeshSection.Slice(mesh,cut,false);
                try{Assert.That(SignedVolume(left)+SignedVolume(right),Is.EqualTo(SignedVolume(mesh)).Within(.00003));}
                finally{Object.DestroyImmediate(left);Object.DestroyImmediate(right);}
            }
        }
    }
}
