#if UNITY_EDITOR
using System.Collections;
using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace HalfAndHalf.Tests {
 public sealed class Task5CutVisualTests {
  [UnityTest] public IEnumerator TerrainAndWaterDoNotCoverSeparatedCutAndReturnOnRetry(){
   foreach(string id in new[]{"N02","N05"}){
    var host=new GameObject("isolated decor regression");var visual=host.AddComponent<VoxelPrototypeVisual>();
    try{
     visual.Show(AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/"+id+"/VoxelPrototype/"+id+"Voxel.asset"));
     var decor=host.transform.Find(id+" decoration mass0");Assert.That(decor,Is.Not.Null);Assert.That(decor.gameObject.activeSelf,Is.True);
     visual.Prepare(.217f);Assert.That(decor.gameObject.activeSelf,Is.True,"Keep the READY/cutting reference until fragments appear");
     visual.Animate(.5f,.5f);Assert.That(decor.gameObject.activeSelf,Is.False,"Mass0 context must not obscure separated fragments");
     visual.ResetAttempt();Assert.That(decor.gameObject.activeSelf,Is.True);Assert.That(visual.FragmentCount,Is.Zero);
    }finally{Object.Destroy(host);}yield return null;
   }
  }
 }
}
#endif
