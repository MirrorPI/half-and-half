#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
namespace HalfAndHalf.Tests
{
 public sealed class SpaceTrialPlayTests
 {
  [UnityTest] public IEnumerator MovingBodiesFreezeAtCutAndRetryResetsBothTrials()
  {
   bool background=Application.runInBackground;Application.runInBackground=true;
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SpacePixelTrial.unity",new LoadSceneParameters(LoadSceneMode.Single));
   yield return null;
   var controller=Object.FindFirstObjectByType<SpaceTrialController>();var game=controller.Game;var visual=controller.Visual;
   try
   {
    Assert.That(game.GetComponent<MvpCollection>().enabled,Is.False);Assert.That(game.GetComponent<GameCenterController>().enabled,Is.False);
    foreach(string id in new[]{"P04","G03"})
    {
     controller.Select(id);Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Dropping));
     double until=Time.realtimeSinceStartupAsDouble+5;
     while(game.Session.Phase==AttemptPhase.Dropping&&Time.realtimeSinceStartupAsDouble<until)yield return null;
     Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Ready));
     var mesh=visual.transform.Find("Whole").GetComponent<MeshFilter>().sharedMesh;
     var before=mesh.vertices;long timeBefore=visual.DisplayedMilliseconds;var block=new MaterialPropertyBlock();
     var renderer=visual.transform.Find("Whole").GetComponent<MeshRenderer>();renderer.GetPropertyBlock(block);var poseBefore=block.GetVector("_Motion0");
     yield return new WaitForSecondsRealtime(.5f);
     Assert.That(visual.DisplayedMilliseconds,Is.GreaterThan(timeBefore));
     if(id=="G03")
     {
      Assert.That(mesh.vertices,Is.EqualTo(before),"READY must reuse static grain geometry");renderer.GetPropertyBlock(block);Assert.That(block.GetVector("_Motion0"),Is.Not.EqualTo(poseBefore));
      Assert.That(mesh.vertexCount,Is.EqualTo(28000));
     }
     else Assert.That(mesh.vertices.SequenceEqual(before),Is.False,"Visible positions must move before CUT");
     var point=game.GameCamera.WorldToScreenPoint(new Vector3(.13f,0,0));
     Assert.That(game.BeginPointer(93,point),Is.True);Assert.That(game.ReleasePointer(93,point),Is.True);
     var result=game.Session.Result;Assert.That(visual.FrozenMilliseconds,Is.EqualTo(result.Snapshot.ElapsedMilliseconds));
     Assert.That(result.Snapshot.ObjectId,Is.EqualTo(id));Assert.That(result.LeftMass+result.RightMass,Is.EqualTo(100m));
     renderer.GetPropertyBlock(block);var frozenPose=block.GetVector("_Motion0");var frozenFast=block.GetVector("_Fast0");
     var left=visual.transform.Find("Left").GetComponent<MeshFilter>().sharedMesh;var frozen=left.vertices;
     if(id=="P04")Assert.That(left.GetTriangles(1).Length,Is.GreaterThan(0),"Solid planet cut must expose its interior");
     Assert.That(left.vertexCount,Is.GreaterThan(0));Assert.That(left.vertices.All(p=>p.x<=(float)result.Snapshot.KnifeX+.00001f),Is.True);
     var right=visual.transform.Find("Right").GetComponent<MeshFilter>().sharedMesh;Assert.That(right.vertexCount,Is.GreaterThan(0));Assert.That(right.vertices.All(p=>p.x>=(float)result.Snapshot.KnifeX-.00001f),Is.True);
     yield return new WaitForSecondsRealtime(.7f);
     Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Result));Assert.That(visual.FragmentCount,Is.EqualTo(2));
     Assert.That(visual.DisplayedMilliseconds,Is.EqualTo(result.ElapsedMilliseconds));Assert.That(left.vertices,Is.EqualTo(frozen));
     if(id=="G03"){renderer.GetPropertyBlock(block);Assert.That(block.GetVector("_Motion0"),Is.EqualTo(frozenPose));Assert.That(block.GetVector("_Fast0"),Is.EqualTo(frozenFast));}
     Assert.That(Scoring.Evaluate(visual.Definition(),result.Snapshot).AccuracyUnits,Is.EqualTo(result.AccuracyUnits));
     game.Hud.RetryButton.onClick.Invoke();yield return null;
     Assert.That(visual.FrozenMilliseconds,Is.EqualTo(-1));Assert.That(visual.DisplayedMilliseconds,Is.Zero);Assert.That(visual.FragmentCount,Is.Zero);
    }
   }
   finally{Application.runInBackground=background;}
  }
 }
}
#endif
