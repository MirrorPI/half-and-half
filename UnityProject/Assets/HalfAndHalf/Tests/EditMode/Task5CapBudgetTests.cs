using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5CapBudgetTests {
  [TestCase(true),TestCase(false)]public void WarmCutsMustReuseImmutableCapColours(bool left){
   int calls=0;var v=new VoxelVolume("cap-cache-literal",p=>(byte)(Mathf.Abs(p.x)<.08f&&Mathf.Abs(p.y)<.08f&&Mathf.Abs(p.z)<.08f?1:0),(id,m,p,axis,sign,cap)=>{if(cap)calls++;return new Color32(37,83,105,245);});
   var whole=v.Build(float.PositiveInfinity,true);Object.DestroyImmediate(whole);calls=0;
   foreach(float x in new[]{-.03f,0f,.03f}){var mesh=v.Build(x,left);try{Assert.That(mesh.vertexCount,Is.GreaterThan(0));Assert.That(mesh.GetTriangles(1).Length,Is.GreaterThan(0));}finally{Object.DestroyImmediate(mesh);}}
   Assert.That(calls,Is.Zero,"All cap material colours are immutable; prepare them before the CUT hot path");
  }
 }
}
