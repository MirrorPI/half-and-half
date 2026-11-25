#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
 public sealed class Task6FreezeTests
 {
  static readonly string[] PoseNames={"_Center0","_Center1","_Motion0","_Motion1","_Slow0","_Slow1","_Fast0","_Fast1"};
  static Vector4[] Pose(MeshRenderer r){var p=new MaterialPropertyBlock();r.GetPropertyBlock(p);return PoseNames.Select(n=>p.GetVector(n)).ToArray();}
  [UnityTest] public IEnumerator FrameSchedulesFreezeEveryPoseAndBothSectionsAtSameCut()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SpacePixelTrial.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
   var c=Object.FindFirstObjectByType<SpaceTrialController>();var g=c.Game;var v=c.Visual;g.enabled=false;
   try
   {
    foreach(string id in new[]{"P01","P04","G03"})
    {
     Vector3[] reference=null;Color32[] referenceColors=null;Vector4[] referencePose=null;
     foreach(int fps in new[]{30,60,120})
     {
      c.Select(id);g.Presentation.Drop(1);
      for(int frame=0;frame*1000L/fps<2037;frame++)v.ShowTime(frame*1000L/fps);
      var cut=new CutSnapshot(id,v.Definition().ContentVersion,.137123m,2037,TransformState.Identity,true);
      v.Prepare(Scoring.Evaluate(v.Definition(),cut));
      var whole=v.transform.Find("Whole");var wholeMesh=whole.GetComponent<MeshFilter>().sharedMesh;
      var left=v.transform.Find("Left").GetComponent<MeshFilter>().sharedMesh;var right=v.transform.Find("Right").GetComponent<MeshFilter>().sharedMesh;
      var vertices=left.vertices;var colors=wholeMesh.colors32;var pose=Pose(whole.GetComponent<MeshRenderer>());
      if(reference!=null){Assert.That(vertices,Is.EqualTo(reference));Assert.That(colors,Is.EqualTo(referenceColors));Assert.That(pose,Is.EqualTo(referencePose));}
      reference=vertices;referenceColors=colors;referencePose=pose;
      var frozenWhole=wholeMesh.vertices;var frozenRight=right.vertices;var frozenLeftColors=left.colors32;var frozenRightColors=right.colors32;
      foreach(long later in new[]{2038L,6000L,119999L})v.ShowTime(later);
      v.Animate(1,.1f);yield return null;
      Assert.That(v.DisplayedMilliseconds,Is.EqualTo(2037));Assert.That(v.FrozenMilliseconds,Is.EqualTo(2037));
      Assert.That(wholeMesh.vertices,Is.EqualTo(frozenWhole));Assert.That(wholeMesh.colors32,Is.EqualTo(colors));
      Assert.That(left.vertices,Is.EqualTo(vertices));Assert.That(right.vertices,Is.EqualTo(frozenRight));
      Assert.That(left.colors32,Is.EqualTo(frozenLeftColors));Assert.That(right.colors32,Is.EqualTo(frozenRightColors));Assert.That(Pose(whole.GetComponent<MeshRenderer>()),Is.EqualTo(pose));
      Assert.That(left.vertices.All(p=>p.x<=(float)cut.KnifeX+.0000002f),Is.True);Assert.That(right.vertices.All(p=>p.x>=(float)cut.KnifeX-.0000002f),Is.True);
      g.Retry();yield return null;Assert.That(v.FragmentCount,Is.Zero);Assert.That(v.DisplayedMilliseconds,Is.Zero);Assert.That(v.FrozenMilliseconds,Is.EqualTo(-1));
     }
    }
   }
   finally{g.enabled=true;}
  }
  [UnityTest] public IEnumerator ImmediateCutAspectChangeAndFastRetryKeepTrialsIsolated()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SpacePixelTrial.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
   var c=Object.FindFirstObjectByType<SpaceTrialController>();var g=c.Game;var v=c.Visual;
   Assert.That(g.GetComponent<MvpCollection>().enabled,Is.False);Assert.That(g.GetComponent<GameCenterController>().enabled,Is.False);
   float aspect=g.GameCamera.aspect,size=g.GameCamera.orthographicSize;g.enabled=false;
   try
   {
    foreach(string id in new[]{"P01","P04","G03"})foreach(float ratio in new[]{428f/926,375f/667,926f/428})
    {
     c.Select(id);Assert.That(g.Presentation.Audio.CutVariants,Is.EqualTo(id=="G03"?c.GalaxyClips:c.PlanetClips),"P01 uses explicitly temporary planet clips");g.Presentation.Drop(1);g.GameCamera.aspect=ratio;g.GameCamera.orthographicSize=Mathf.Max(3.8f,1.85f/ratio);
     long now=(long)(Time.realtimeSinceStartupAsDouble*1000);g.Session.Land(now,TransformState.Identity);
     Assert.That(g.Presentation.Knife.gameObject.activeSelf,Is.False);
     // Convert a fixed world position through each viewport before the real input entry points.
     var screen=g.GameCamera.WorldToScreenPoint(new Vector3(.137123f,0,0));
     Assert.That(g.BeginPointer(19,screen),Is.True);Assert.That(g.ReleasePointer(19,screen),Is.True);
     Assert.That((double)g.Session.Snapshot.KnifeX,Is.EqualTo(.137123).Within(.000001));
     Assert.That(v.FrozenMilliseconds,Is.EqualTo(g.Session.Snapshot.ElapsedMilliseconds));Assert.That(g.Session.Result.LeftMass+g.Session.Result.RightMass,Is.EqualTo(100m));
     var frozen=g.Session.Result;g.GameCamera.aspect=ratio==428f/926?926f/428:428f/926;v.ShowTime(6000);
     Assert.That(g.Session.Result,Is.SameAs(frozen));Assert.That(v.DisplayedMilliseconds,Is.EqualTo(frozen.ElapsedMilliseconds));
     // Retry during the knife lead-in, before either half has separated.
     g.Hud.RetryButton.onClick.Invoke();yield return null;
     Assert.That(v.FragmentCount,Is.Zero);Assert.That(g.Session.Snapshot,Is.Null);Assert.That(g.Session.ActivePointer,Is.Null);
     Assert.That(g.Presentation.Knife.gameObject.activeSelf,Is.False);Assert.That(g.Presentation.Guide.gameObject.activeSelf,Is.False);
     Assert.That(v.FrozenMilliseconds,Is.EqualTo(-1));Assert.That(v.DisplayedMilliseconds,Is.Zero);
     Assert.That(Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length,Is.EqualTo(4));
    }
    c.Select("P04");c.SwitchButton.onClick.Invoke();Assert.That(v.Kind,Is.EqualTo("G03"));c.SwitchButton.onClick.Invoke();Assert.That(v.Kind,Is.EqualTo("P01"));c.SwitchButton.onClick.Invoke();Assert.That(v.Kind,Is.EqualTo("P04"));
   }
   finally{g.GameCamera.aspect=aspect;g.GameCamera.orthographicSize=size;g.enabled=true;}
  }
  [UnityTest] public IEnumerator GalaxyGpuGeometryMatchesCpuProjectionAtCutTime()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/SpacePixelTrial.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
   var c=Object.FindFirstObjectByType<SpaceTrialController>();c.Select("G03");var g=c.Game;g.enabled=false;g.Presentation.Drop(1);
   var v=c.Visual;var filter=v.transform.Find("Whole").GetComponent<MeshFilter>();var renderer=filter.GetComponent<MeshRenderer>();
   Mesh original=filter.sharedMesh;var materials=renderer.sharedMaterials;var target=new RenderTexture(428,926,24);var cpuMesh=new Mesh();var previous=g.GameCamera.targetTexture;var active=RenderTexture.active;
   try
   {
    g.GameCamera.targetTexture=target;
    foreach(long t in new[]{0L,5999L,6000L,6187L,24000L})
    {
     v.ShowTime(t);g.GameCamera.Render();RenderTexture.active=target;
     var gpu=new Texture2D(428,926,TextureFormat.RGB24,false);gpu.ReadPixels(new Rect(0,0,428,926),0,0);gpu.Apply();var expected=gpu.GetPixels32();Object.Destroy(gpu);
     var cells=new List<SpaceVoxel>();SpaceTrialModel.Fill(cells,"G03",t);new SpaceVoxelMesh().Fill(cpuMesh,cells,float.PositiveInfinity,true,true);
     filter.sharedMesh=cpuMesh;renderer.sharedMaterials=new[]{v.Palette,v.Palette};renderer.SetPropertyBlock(null);g.GameCamera.Render();
     var cpu=new Texture2D(428,926,TextureFormat.RGB24,false);cpu.ReadPixels(new Rect(0,0,428,926),0,0);cpu.Apply();var actual=cpu.GetPixels32();Object.Destroy(cpu);
     int differing=0;for(int i=0;i<actual.Length;i++)if(Mathf.Abs(actual[i].r-expected[i].r)>2||Mathf.Abs(actual[i].g-expected[i].g)>2||Mathf.Abs(actual[i].b-expected[i].b)>2)differing++;
     Assert.That(differing,Is.LessThan(80),"GPU and CPU particle coverage differs at "+t+"ms");
     filter.sharedMesh=original;renderer.sharedMaterials=materials;v.ShowTime(t);
    }
   }
   finally{filter.sharedMesh=original;renderer.sharedMaterials=materials;g.GameCamera.targetTexture=previous;RenderTexture.active=active;Object.Destroy(target);Object.Destroy(cpuMesh);g.enabled=true;}
   yield return null;
  }
 }
}
#endif
