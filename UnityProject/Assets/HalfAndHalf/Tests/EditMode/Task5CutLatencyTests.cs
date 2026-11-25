using System;
using System.Linq;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5CutLatencyTests {
  [TestCase(true),TestCase(false)] public void PlaneExactlyOneToleranceFromGridStillHasAnOutwardCap(bool left){
   var mesh=VoxelPrototypeGeometry.For("N01").Build(left?.0000001f:-.0000001f,left);
   try{Assert.That(mesh.GetTriangles(1).Length,Is.GreaterThan(0),"Retained width equal to the existing tolerance still needs a cap");}
   finally{UnityEngine.Object.DestroyImmediate(mesh);}
  }
  [TestCase(true),TestCase(false)] public void ThinOuterFragmentKeepsItsOutwardExterior(bool left){
   var volume=new VoxelVolume("thin boundary fixture",p=>Mathf.Abs(p.x-(left?-1.171875f:1.171875f))<VoxelVolume.Step*.5f&&Mathf.Abs(p.y)<.125f&&Mathf.Abs(p.z)<.125f?(byte)1:(byte)0,(id,m,p,a,sign,cap)=>new Color32(50,100,150,255));var whole=volume.Build(float.PositiveInfinity,true);
   float edge=left?whole.bounds.min.x:whole.bounds.max.x;UnityEngine.Object.DestroyImmediate(whole);
   int bits=BitConverter.ToInt32(BitConverter.GetBytes(edge),0);float x=BitConverter.ToSingle(BitConverter.GetBytes(bits-1),0);
   var mesh=volume.Build(x,left);
   try{Assert.That(mesh.normals.Any(n=>n.x*(left?-1:1)>.99f),Is.True,"A retained thin fragment must keep the outward boundary face");}
   finally{UnityEngine.Object.DestroyImmediate(mesh);}
  }
  [Test] public void CutReusesUnchangedExteriorPaletteInsteadOfRevisitingEveryCell(){
   int exteriorCalls=0,capCalls=0;
   var volume=new VoxelVolume("latency fixture",p=>Mathf.Abs(p.x)<.8f&&Mathf.Abs(p.y)<.8f&&Mathf.Abs(p.z)<.4f?(byte)1:(byte)0,
    (id,m,p,a,s,cap)=>{if(cap)capCalls++;else exteriorCalls++;return new Color32(50,100,150,255);});
   var whole=volume.Build(float.PositiveInfinity,true);UnityEngine.Object.DestroyImmediate(whole);exteriorCalls=capCalls=0;
   var left=volume.Build(.217f,true);var right=volume.Build(.217f,false);
   try{Assert.That(left.GetTriangles(1).Length,Is.GreaterThan(0));Assert.That(right.GetTriangles(1).Length,Is.GreaterThan(0));Assert.That(capCalls,Is.Zero,"Immutable cap colours are also prepared before CUT");Assert.That(exteriorCalls,Is.Zero,"CUT must reuse immutable exterior faces baked at selection");}
   finally{UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
  }
 }
}
