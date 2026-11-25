#if UNITY_EDITOR
using System.Collections;using System.Linq;using HalfAndHalf.Content;using HalfAndHalf.Presentation;using NUnit.Framework;using UnityEditor;using UnityEngine;using UnityEngine.TestTools;
namespace HalfAndHalf.Tests {
 public sealed class Task5PreparedPartTests {
  [UnityTest]public IEnumerator CutAndRetryMustReusePreparedSceneNodes(){
   var host=new GameObject("isolated prepared part regression");var visual=host.AddComponent<SampleVisual>();
   try{
    visual.Show(AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/L01/L01Sample.asset"));yield return null;
    var nodes=host.GetComponentsInChildren<Transform>(true).Select(t=>t.GetInstanceID()).OrderBy(n=>n).ToArray();
    Assert.That(visual.Left,Is.Null);Assert.That(visual.Right,Is.Null);
    for(int i=0;i<3;i++){
     visual.Prepare(.217f+i*.02f);visual.Animate(1,.2f);
     Assert.That(host.GetComponentsInChildren<Transform>(true).Select(t=>t.GetInstanceID()).OrderBy(n=>n).ToArray(),Is.EqualTo(nodes),"CUT must not create scene nodes for immutable authored parts");
     Assert.That(visual.Left.gameObject.activeSelf,Is.True);Assert.That(visual.Right.gameObject.activeSelf,Is.True);
     visual.ResetAttempt();Assert.That(visual.Left,Is.Null);Assert.That(visual.Right,Is.Null);
     Assert.That(host.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled),Is.GreaterThan(0),"Whole model returns on Retry");yield return null;
    }
   }finally{Object.Destroy(host);}yield return null;
  }
 }
}
#endif
