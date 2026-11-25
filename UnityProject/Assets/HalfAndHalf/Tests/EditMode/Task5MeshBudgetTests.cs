using System.Linq;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5MeshBudgetTests {
  [TestCase(true),TestCase(false)] public void MeshWithNoReferencedTrianglesStaysEmpty(bool left){
   var source=new Mesh();source.vertices=new[]{Vector3.zero};source.normals=new[]{Vector3.up};source.RecalculateBounds();var mesh=MeshSection.Slice(source,left?2f:-2f,left);
   try{Assert.That(mesh.vertexCount,Is.Zero,"Stored points without triangles must not create a ghost fragment");}
   finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(source);}
  }
  [TestCase(true),TestCase(false)] public void UnreferencedSourceVertexCannotChangeFragmentRestHeight(bool left){
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);var original=go.GetComponent<MeshFilter>().sharedMesh;var source=Object.Instantiate(original);
   var points=source.vertices;var normals=source.normals;source.vertices=points.Concat(new[]{new Vector3(0,-100,0)}).ToArray();source.normals=normals.Concat(new[]{Vector3.up}).ToArray();source.RecalculateBounds();
   var mesh=MeshSection.Slice(source,left?2f:-2f,left);
   try{Assert.That(MeshSection.RestY(mesh,Quaternion.identity,-1.305f),Is.EqualTo(MeshSection.RestY(original,Quaternion.identity,-1.305f)).Within(.000001f),"Unreferenced vertices must not move a whole fragment off its tray");}
   finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(source);Object.DestroyImmediate(go);}
  }
  [TestCase(true),TestCase(false)] public void WholeSideRetainsIndexedVerticesInsteadOfExpandingEveryTriangle(bool left){
   var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);var source=go.GetComponent<MeshFilter>().sharedMesh;var cut=MeshSection.Slice(source,left?2f:-2f,left);
   try{Assert.That(cut.vertexCount,Is.LessThanOrEqualTo(source.vertexCount),"An uncut convex part must retain indexed vertices");Assert.That(cut.GetTriangles(1).Length,Is.Zero);Assert.That(cut.GetTriangles(0).Length,Is.EqualTo(source.triangles.Length));}
   finally{Object.DestroyImmediate(cut);Object.DestroyImmediate(go);}
  }
  [TestCase(true),TestCase(false)] public void CarCutStaysWithinMergedSurfaceVertexBudget(bool left){
   var whole=VoxelCarGeometry.Build(float.PositiveInfinity,true);Object.DestroyImmediate(whole);var mesh=VoxelCarGeometry.Build(.217f,left);
   try{Assert.That(mesh.vertexCount,Is.LessThan(10000),"CUT must reuse merged immutable exterior instead of emitting every voxel face");}
   finally{Object.DestroyImmediate(mesh);}
  }
 }
}
