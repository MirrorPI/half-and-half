#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HalfAndHalf.UI;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
 public sealed class Task5SelectionDefinitionTests
 {
  [UnityTest] public IEnumerator SelectUsesTheAlreadyValidatedCollectionDefinition()
  {
   var type=typeof(MvpCollection).Assembly.GetType("HalfAndHalf.Verification.SelectionPreparationReview");
   Assert.That(type,Is.Not.Null,"Selection needs separate first-use and persistence timings");
   string folder=Path.Combine(Path.GetTempPath(),"HalfSelectionTiming-"+Guid.NewGuid());Directory.CreateDirectory(folder);
   string save=Path.Combine(folder,"progress.json");
   File.Copy(Path.GetFullPath("../reports/task-5/startup-scale-2026-10-15/device-v13-complete/progress.json"),save);
   UnityEngine.Events.UnityAction<Scene,LoadSceneMode> isolate=(scene,mode)=>{
    var m=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();
    if(m==null)return;m.SavePath=save;m.Game.GetComponent<GameCenterController>().enabled=false;m.Game.GetComponent<RankingPanel>().enabled=false;
   };
   SceneManager.sceneLoaded+=isolate;
   try
   {
    yield return SceneManager.LoadSceneAsync("Gameplay20");yield return null;
    var m=UnityEngine.Object.FindFirstObjectByType<MvpCollection>();
    Func<string> best=()=>string.Join(";",Enumerable.Range(0,20).Select(i=>{var b=m.Progress.Best(i);return b.Snapshot.ObjectId+"/"+b.AccuracyUnits+"/"+b.ElapsedMilliseconds;}));
    string before=best();var watch=Stopwatch.StartNew();Assert.That(m.Select(11),Is.True);watch.Stop();
    var field=m.Game.Session.GetType().GetField("definition",BindingFlags.NonPublic|BindingFlags.Instance);
    Assert.That(field.GetValue(m.Game.Session),Is.SameAs(m.Progress.Content[11]),"Select must reuse the collection's validated immutable mass definition");
    Assert.That(best(),Is.EqualTo(before));Assert.That(m.SavePending,Is.False);
    Assert.That(m.Select(20),Is.False);
   }
   finally{SceneManager.sceneLoaded-=isolate;Directory.Delete(folder,true);}
  }
 }
}
#endif
