using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5SelectionPreparationTests {
  [Test]public void RepeatedWholeBuildReusesPreparedFacesAndKeepsExactMesh(){
   int colors=0;var volume=new VoxelVolume("prepared-whole",p=>(byte)(Mathf.Abs(p.x)<.08f&&Mathf.Abs(p.y)<.08f&&Mathf.Abs(p.z)<.08f?1:0),(id,m,p,axis,sign,cap)=>{colors++;return new Color32(37,83,105,245);});
   var first=volume.Build(float.PositiveInfinity,true);Mesh second=null;
   try{colors=0;second=volume.Build(float.PositiveInfinity,true);Assert.That(colors,Is.Zero,"Reselection must reuse prepared immutable faces");Assert.That(second.vertices,Is.EqualTo(first.vertices));Assert.That(second.normals,Is.EqualTo(first.normals));Assert.That(second.colors32,Is.EqualTo(first.colors32));for(int i=0;i<2;i++)Assert.That(second.GetTriangles(i),Is.EqualTo(first.GetTriangles(i)));Assert.That(second.bounds,Is.EqualTo(first.bounds));}
   finally{Object.DestroyImmediate(first);if(second!=null)Object.DestroyImmediate(second);}
  }
 }
}
