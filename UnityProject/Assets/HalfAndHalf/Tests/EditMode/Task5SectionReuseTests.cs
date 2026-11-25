using System;using System.Reflection;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5SectionReuseTests {
  [Test]public void CutThroughMiddleVertexKeepsOriginalSideSpecificFloatInterpolation(){
   var source=new Mesh();Mesh first=null,actual=null,expected=null;
   try{
    source.vertices=new[]{new Vector3(-1,1,0),new Vector3(0,.1f,1),new Vector3(1,-1,0),new Vector3(-1,-1,2),new Vector3(0,-.1f,3),new Vector3(1,1,2)};source.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward,Vector3.forward};source.triangles=new[]{0,1,2,3,4,5};
    var type=typeof(MeshSection).GetNestedType("Prepared",BindingFlags.NonPublic);var prepared=Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{source},null);var slice=typeof(MeshSection).GetMethod("Slice",BindingFlags.Static|BindingFlags.NonPublic);
    first=(Mesh)slice.Invoke(null,new object[]{prepared,0f,true,null,0f});actual=(Mesh)slice.Invoke(null,new object[]{prepared,0f,false,null,0f});expected=MeshSection.Slice(source,0f,false);
    var a=actual.vertices;var b=expected.vertices;Assert.That(actual.GetTriangles(1).Length,Is.EqualTo(expected.GetTriangles(1).Length));var ai=actual.GetTriangles(1);var bi=expected.GetTriangles(1);for(int i=0;i<ai.Length;i++){Assert.That(BitConverter.SingleToInt32Bits(a[ai[i]].x),Is.EqualTo(BitConverter.SingleToInt32Bits(b[bi[i]].x)));Assert.That(BitConverter.SingleToInt32Bits(a[ai[i]].y),Is.EqualTo(BitConverter.SingleToInt32Bits(b[bi[i]].y)),"Side-specific interpolation operand order must remain exact");Assert.That(BitConverter.SingleToInt32Bits(a[ai[i]].z),Is.EqualTo(BitConverter.SingleToInt32Bits(b[bi[i]].z)));}
   }finally{UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(actual);UnityEngine.Object.DestroyImmediate(expected);UnityEngine.Object.DestroyImmediate(source);}
  }

  [TestCase(true),TestCase(false)]public void ReusedCutsPreserveTangentFacesOppositeWindingAndBiasedCoordinates(bool startLeft){
   var source=new Mesh();var type=typeof(MeshSection).GetNestedType("Prepared",BindingFlags.NonPublic);var slice=typeof(MeshSection).GetMethod("Slice",BindingFlags.Static|BindingFlags.NonPublic);
   try{
    source.vertices=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
    source.normals=Array.ConvertAll(source.vertices,p=>p.normalized);source.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
    var prepared=Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{source},null);
    foreach(float x in new[]{-.217f,-.217f,0f,0f,-1f,-1f,1f,1f,.701f,2f,.701f})foreach(bool left in new[]{startLeft,!startLeft}){
     float bias=left?.003f:-.003f;var actual=(Mesh)slice.Invoke(null,new object[]{prepared,x,left,null,bias});var expected=MeshSection.Slice(source,x,left);
     try{var points=expected.vertices;for(int i=0;i<points.Length;i++)points[i].x+=bias;Assert.That(actual.vertices,Is.EqualTo(points),"x="+x+" left="+left);Assert.That(actual.normals,Is.EqualTo(expected.normals));Assert.That(actual.subMeshCount,Is.EqualTo(expected.subMeshCount));for(int sub=0;sub<expected.subMeshCount;sub++)Assert.That(actual.GetTriangles(sub),Is.EqualTo(expected.GetTriangles(sub)));}
     finally{UnityEngine.Object.DestroyImmediate(actual);UnityEngine.Object.DestroyImmediate(expected);}
    }
   }finally{UnityEngine.Object.DestroyImmediate(source);}
  }
 }
}
