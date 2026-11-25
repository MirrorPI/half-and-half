using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEngine;
namespace HalfAndHalf.Tests {
 public sealed class Task5CarJoinTests {
  [Test] public void ObservedGlassJoinMustHaveMatchingEndpoints(){
   var mesh=VoxelCarGeometry.Build(.217f,true);
   try{var v=mesh.vertices;var c=mesh.colors32;bool junction=false;
    for(int i=0;i<v.Length;i+=4){var lo=v[i];var hi=v[i+2];if(c[i].r!=37||c[i].g!=83||c[i].b!=105||c[i].a!=245||lo.z!=-.3125f||hi.z!=-.3125f)continue;
     if((lo.y==.125f||hi.y==.125f)&&lo.x<-.53125f&&hi.x>-.53125f)junction=true;
    }
    Assert.That(junction,Is.False,"Observed glass endpoint (-.53125,.125,-.3125) must not sit inside the adjacent long edge");
   }finally{Object.DestroyImmediate(mesh);}
  }
 }
}
