using System.Linq;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEditor;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5SharedVertexTests {
  [TestCase(true),TestCase(false)]public void B01SkinReusesOriginalVertexReferences(bool left){
   var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HalfAndHalf/Content/B01/B01Body.asset");var mesh=MeshSection.Slice(source,.217f,left);
   try{Assert.That(mesh.GetVertexBufferStride(0),Is.EqualTo(24));Assert.That(mesh.GetVertexAttributeOffset(UnityEngine.Rendering.VertexAttribute.Normal),Is.EqualTo(12));var v=source.vertices;var n=source.normals;var allowed=Enumerable.Range(0,v.Length).GroupBy(i=>new{p=v[i],n=n[i]}).ToDictionary(g=>g.Key,g=>g.Count());var output=mesh.vertices;var normals=mesh.normals;var skin=mesh.GetTriangles(0).Distinct().Where(i=>left?output[i].x<.216999f:output[i].x>.217001f).GroupBy(i=>new{p=output[i],n=normals[i]});
    foreach(var group in skin)Assert.That(group.Count(),Is.LessThanOrEqualTo(allowed[group.Key]),"Untouched original vertices must be shared across retained triangles");
   }finally{Object.DestroyImmediate(mesh);}
  }
 }
}
