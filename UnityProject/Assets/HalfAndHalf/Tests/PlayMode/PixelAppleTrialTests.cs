#if UNITY_EDITOR
using System.Collections;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
namespace HalfAndHalf.Tests
{
 public sealed class PixelAppleTrialTests
 {
  [UnityTest] public IEnumerator TrialUsesRealDropCutAndRetryWithoutRecordWrites()
  {
   bool background=Application.runInBackground;Application.runInBackground=true;
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/B01PixelTrial.unity",new LoadSceneParameters(LoadSceneMode.Single));
   var game=Object.FindFirstObjectByType<AppleGame>();
   try
   {
    Assert.That(game.GetComponent<MvpCollection>().enabled,Is.False);
    Assert.That(game.GetComponent<GameCenterController>().enabled,Is.False);
    yield return null;Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Dropping));
    foreach(float x in new[]{0f,.217f,-.217f})
    {
     double until=Time.realtimeSinceStartupAsDouble+4;
     while(game.Session.Phase==AttemptPhase.Dropping&&Time.realtimeSinceStartupAsDouble<until)yield return null;
     Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Ready));
     Assert.That(game.Presentation.Body.enabled,Is.False);
     Assert.That(game.Presentation.PixelApple.FragmentCount,Is.Zero);
     var point=game.GameCamera.WorldToScreenPoint(new Vector3(x,0,0));
     Assert.That(game.BeginPointer(93,point),Is.True);Assert.That(game.ReleasePointer(93,point),Is.True);
     yield return new WaitForSecondsRealtime(.55f);
     Assert.That(game.Session.Phase,Is.EqualTo(AttemptPhase.Result));
     Assert.That(game.Presentation.FragmentCount,Is.EqualTo(2));
     if(x==0)Assert.That(game.Session.Result.AccuracyUnits,Is.EqualTo(10000));
     Assert.That(game.Session.Result.Snapshot.ContentVersion,Is.EqualTo("B01-fine-voxel-trial-v1"));
     Assert.That(game.Hud.ResultPanel.activeSelf,Is.True);
     game.Hud.RetryButton.onClick.Invoke();yield return null;
     Assert.That(game.Presentation.FragmentCount,Is.Zero);Assert.That(game.Presentation.Knife.gameObject.activeSelf,Is.False);
    }
   }
   finally{Application.runInBackground=background;}
  }
 }
}
#endif
