using System;using System.Reflection;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5PreparedEmptyTests {
  [Test]public void EmptySideAfterNonemptyCutClearsManagedMetadata(){
   var source=new Mesh();Mesh first=null,empty=null;
   try{
    source.vertices=new[]{new Vector3(-1,1,0),new Vector3(1,1,0),new Vector3(0,-1,0)};source.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward};source.triangles=new[]{0,1,2};
    var type=typeof(MeshSection).GetNestedType("Prepared",BindingFlags.NonPublic);
    var prepared=Activator.CreateInstance(type,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{source},null);
    var slice=typeof(MeshSection).GetMethod("Slice",BindingFlags.Static|BindingFlags.NonPublic);
    first=(Mesh)slice.Invoke(null,new object[]{prepared,0f,true,null,0f});
    Assert.That(first.vertexCount,Is.GreaterThan(0));
    empty=(Mesh)slice.Invoke(null,new object[]{prepared,2f,false,null,0f});
    Assert.That(empty.vertexCount,Is.Zero);
    foreach(string name in new[]{"VertexCount","SkinCount","CapCount","PlaneCount"})
     Assert.That((int)type.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(prepared),Is.Zero,name+" must describe the empty result, not the previous side");
   }finally{UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(empty);UnityEngine.Object.DestroyImmediate(source);}
  }
 }
}
