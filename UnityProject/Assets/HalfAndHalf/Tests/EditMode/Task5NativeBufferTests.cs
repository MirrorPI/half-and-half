using System;using System.Linq;using System.Reflection;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;using UnityEngine.Rendering;
namespace HalfAndHalf.Tests {
 public sealed class Task5NativeBufferTests {
  [TestCase("L02"),TestCase("N01"),TestCase("N04")]
  public void CachedBuffersKeepExactLayoutNormalsAndIndependentMeshes(string id){
   var volume=id=="L02"?null:VoxelPrototypeGeometry.For(id);Mesh left=null,right=null,empty=null;
   Func<float,bool,Mesh> build=(x,l)=>id=="L02"?VoxelCarGeometry.Build(x,l):volume.Build(x,l);
   Func<float> top=()=>id=="L02"?(float)typeof(VoxelCarGeometry).GetProperty("CutTop",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null):(float)typeof(VoxelVolume).GetProperty("CutTop",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(volume);
   Action<Mesh,bool> rest=(m,l)=>{var rotation=Quaternion.Euler(0,l?30:-30,l?-6:6);var method=(id=="L02"?typeof(VoxelCarGeometry):typeof(VoxelVolume)).GetMethod("CutRestY",BindingFlags.NonPublic|(id=="L02"?BindingFlags.Static:BindingFlags.Instance));float actual=(float)method.Invoke(volume,new object[]{rotation,-1.305f});Assert.That(actual,Is.EqualTo(MeshSection.RestY(m,rotation,-1.305f)),"Rectangle extreme corner must equal the original vertex minimum");};
   try{
    left=build(.217f,true);Verify(left);rest(left,true);Assert.That(top(),Is.EqualTo(MeshSection.Top(left)));
    var positions=left.vertices;var normals=left.normals;var colors=left.colors32;var triangles=left.triangles;
    right=build(.217f,false);Verify(right);rest(right,false);Assert.That(top(),Is.EqualTo(MeshSection.Top(right)));
    CollectionAssert.AreEqual(positions,left.vertices,"Uploading the other side must not mutate a returned mesh");CollectionAssert.AreEqual(normals,left.normals);CollectionAssert.AreEqual(colors,left.colors32);CollectionAssert.AreEqual(triangles,left.triangles);
    empty=build(-5,true);rest(empty,true);Assert.That(empty.vertexCount,Is.Zero);Assert.That(top(),Is.EqualTo(float.NegativeInfinity));
   }finally{UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);UnityEngine.Object.DestroyImmediate(empty);}
  }
  static void Verify(Mesh mesh){
   Assert.That(mesh.vertexCount,Is.GreaterThan(0));Assert.That(mesh.GetVertexBufferStride(0),Is.EqualTo(28));
   Assert.That(mesh.GetVertexAttributeOffset(VertexAttribute.Position),Is.Zero);Assert.That(mesh.GetVertexAttributeOffset(VertexAttribute.Normal),Is.EqualTo(12));Assert.That(mesh.GetVertexAttributeOffset(VertexAttribute.Color),Is.EqualTo(24));Assert.That(mesh.GetVertexAttributeFormat(VertexAttribute.Color),Is.EqualTo(VertexAttributeFormat.UNorm8));
   var p=mesh.vertices;var n=mesh.normals;var c=mesh.colors32;Assert.That(n.Length,Is.EqualTo(p.Length));Assert.That(c.Length,Is.EqualTo(p.Length));
   for(int sub=0;sub<2;sub++){var t=mesh.GetTriangles(sub);for(int i=0;i<t.Length;i+=3){var expected=Vector3.Cross(p[t[i+1]]-p[t[i]],p[t[i+2]]-p[t[i]]).normalized;foreach(int at in new[]{t[i],t[i+1],t[i+2]})Assert.That(n[at],Is.EqualTo(expected),"Raw normal channel must agree with the frozen outward winding");}}
  }
 }
}
