using System;using System.Reflection;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class PlaneNeighbourBoundsTests {
  static int[] Range(float value){var method=typeof(MeshSection).GetMethod("PlaneNeighbours",BindingFlags.Static|BindingFlags.NonPublic);Assert.That(method,Is.Not.Null,"Bound plane-bin probes without losing near-equal cap points");int cell=(int)Math.Floor((double)value/.00002);object[] args={value,cell,0,0};method.Invoke(null,args);return new[]{(int)args[2],(int)args[3]};}
  [TestCase(.000004f,-1,0),TestCase(.000016f,0,1),TestCase(.000010f,-1,1),TestCase(-.000004f,0,1),TestCase(-.000016f,-1,0)]
  public void DistantBinsAreExcludedWhileCenterRetainsBothNeighbours(float value,int first,int last){CollectionAssert.AreEqual(new[]{first,last},Range(value));}
  [TestCase(float.NaN),TestCase(float.PositiveInfinity),TestCase(float.NegativeInfinity),TestCase(17f),TestCase(-17f)]
  public void UnboundedInputsKeepTheOriginalThreeBinFallback(float value){CollectionAssert.AreEqual(new[]{-1,1},Range(value));}
  [TestCase(1),TestCase(-1)]
  public void MultipleNearPointsKeepTheOriginalSpatialRepresentative(int sign){
   const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;var type=typeof(MeshSection).GetNestedType("Prepared",BindingFlags.NonPublic);var mesh=new Mesh();
   try{
    var vertices=new Vector3[12];var normals=new Vector3[12];var triangles=new int[12];for(int i=0;i<12;i++){vertices[i]=new Vector3(i*.01f,i*.02f,i*.03f);normals[i]=Vector3.forward;triangles[i]=i;}mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=triangles;
    var prepared=Activator.CreateInstance(type,flags,null,new object[]{mesh},null);var include=type.GetMethod("IncludePlanePoint",flags);
    var a=new Vector3(0,-.000006f,.000006f)*sign;var b=new Vector3(0,.000006f,-.000006f)*sign;var q=new Vector3(0,.000001f,.000001f)*sign;
    var earlier=sign>0?b:a;var spatialFirst=sign>0?a:b;
    include.Invoke(prepared,new object[]{earlier,0});include.Invoke(prepared,new object[]{spatialFirst,3});include.Invoke(prepared,new object[]{new Vector3(0,.001f,.001f),6});include.Invoke(prepared,new object[]{q,9});
    Assert.That((int)type.GetField("PlaneCount",flags).GetValue(prepared),Is.EqualTo(3),"Query must deduplicate without adding another cap vertex");var ids=(int[])type.GetField("planeEdgeIds",flags).GetValue(prepared);var cache=(int[])type.GetField("edgePlane",flags).GetValue(prepared);Assert.That(cache[ids[9]],Is.EqualTo(2),"Keep the original 3x3 traversal's representative, even when another matching point was inserted earlier");
   }finally{UnityEngine.Object.DestroyImmediate(mesh);}
  }
  [Test] public void EveryEpsilonNearPointRemainsReachableAcrossSignedCellBoundaries(){
   var method=typeof(MeshSection).GetMethod("PlaneNeighbours",BindingFlags.Static|BindingFlags.NonPublic);Assert.That(method,Is.Not.Null);int near=0;
   for(int cell=-800000;cell<=800000;cell+=1973)foreach(double fraction in new[]{0,.00001,.25,.49949,.4995,.49951,.5,.50049,.5005,.50051,.75,.99999,1}){
    float p=(float)((cell+fraction)*.00002);int own=(int)Math.Floor((double)p/.00002);object[] args={p,own,0,0};method.Invoke(null,args);int first=(int)args[2],last=(int)args[3];
    foreach(double delta in new[]{-.000010011,-.000010001,-.000010000001,-.00001,-.000009999999,-.000009999,0,.000009999,.000009999999,.00001,.000010000001,.000010001,.000010011}){
     float q=(float)((double)p+delta);float squared=(new Vector3(p,0,0)-new Vector3(q,0,0)).sqrMagnitude;if(!(squared<1e-10f))continue;near++;int other=(int)Math.Floor((double)q/.00002)-own;
     if(other<first||other>last)Assert.Fail("Missing near point p="+p.ToString("R")+" q="+q.ToString("R")+" squared="+squared.ToString("R")+" bucket="+other+" range="+first+".."+last);
    }
   }
   Assert.That(near,Is.GreaterThan(50000),"Boundary corpus must actually cover close pairs");
  }
 }
}
