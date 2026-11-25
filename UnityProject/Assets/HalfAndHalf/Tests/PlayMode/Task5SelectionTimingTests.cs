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
 public sealed class Task5SelectionTimingTests
 {
  [UnityTest] public IEnumerator SelectTimingsCoverExclusiveStagesAndClearPreviousObjectWithoutChangingRecords()
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
    var values=(Dictionary<string,double>)type.GetMethod("Snapshot",BindingFlags.Public|BindingFlags.Static).Invoke(null,null);
    string[] exclusive={"visualMs","headerUiMs","definitionMs","gameInitMs","hudMs","saveMs"};
    Assert.That(values.Values.All(x=>x>=0&&!double.IsNaN(x)&&!double.IsInfinity(x)),Is.True);
    Assert.That(exclusive.Sum(k=>values[k]),Is.LessThanOrEqualTo(watch.Elapsed.TotalMilliseconds+1),"Nested geometry/save timings must not be added twice to Select");
    Assert.That(values["definitionMs"],Is.GreaterThan(0));Assert.That(values["saveSerializeMs"],Is.GreaterThan(0));Assert.That(values["saveFlushMs"],Is.GreaterThan(0));
    Assert.That((bool)type.GetField("Active").GetValue(null),Is.False,"Timing must stop before CUT/other saves");
    type.GetField("CarGridMs").SetValue(null,1234d);
    Assert.That(m.Select(17),Is.True);values=(Dictionary<string,double>)type.GetMethod("Snapshot").Invoke(null,null);
    Assert.That(values["carGridMs"],Is.EqualTo(0),"A new object must not inherit a previous object's first-use cost");
    Assert.That(best(),Is.EqualTo(before));Assert.That(m.SavePending,Is.False);
    Assert.That(m.Select(20),Is.False);Assert.That((bool)type.GetField("Active").GetValue(null),Is.False);
   }
   finally{SceneManager.sceneLoaded-=isolate;Directory.Delete(folder,true);}
  }
 }
}
#endif
