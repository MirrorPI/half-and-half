#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
 public sealed class Task5StartupTelemetryTests
 {
  // A cached ResetAttempt must retain the real source preparation sample.
  [UnityTest] public IEnumerator CachedResetKeepsActualBodyPreparationCost()
  {
   var property=typeof(ApplePresentation).GetProperty("ReviewBodyWarmMs",BindingFlags.Instance|BindingFlags.Public);
   Assert.That(property,Is.Not.Null,"Editor must expose the same preparation diagnostic used by device QA");
   var mesh=new Mesh{name="Cold preparation fixture"};
   mesh.vertices=new[]{new Vector3(-1,0,0),new Vector3(1,0,0),new Vector3(0,1,0)};
   mesh.normals=new[]{Vector3.forward,Vector3.forward,Vector3.forward};mesh.triangles=new[]{0,1,2};
   var content=ScriptableObject.CreateInstance<AppleDefinitionAsset>();content.Body=mesh;
   var host=new GameObject("Preparation diagnostic owner");
   try
   {
    var p=host.AddComponent<ApplePresentation>();p.Content=content;
    p.BodyRoot=Child(host,"Body",typeof(MeshFilter),typeof(MeshRenderer));p.Body=p.BodyRoot.GetComponent<MeshRenderer>();
    p.BodyRoot.GetComponent<MeshFilter>().sharedMesh=mesh;
    p.DecorationRoot=Child(host,"Decoration");p.Knife=Child(host,"Knife");p.Guide=Child(host,"Guide",typeof(LineRenderer)).GetComponent<LineRenderer>();
    p.ResetAttempt();double cold=Convert.ToDouble(property.GetValue(p));Assert.That(cold,Is.GreaterThan(0));
    Assert.That(p.ReviewBodyWarmCount,Is.EqualTo(1));
    Assert.That(p.ReviewBodySourcePrepareMs,Is.GreaterThan(0));
    Assert.That(p.ReviewBodyNativeWarmMs,Is.GreaterThanOrEqualTo(0));
    yield return null;
    p.ResetAttempt();Assert.That(Convert.ToDouble(property.GetValue(p)),Is.EqualTo(cold),"Cached reselection overwrote actual preparation cost");
    p.ResetAttempt();Assert.That(Convert.ToDouble(property.GetValue(p)),Is.EqualTo(cold),"Repeated Retry must preserve actual preparation cost");
    Assert.That(p.ReviewBodyWarmCount,Is.EqualTo(1));
    Assert.That(p.ReviewBodyCacheCheckMs,Is.GreaterThanOrEqualTo(0));
    var replacement=UnityEngine.Object.Instantiate(mesh);
    try
    {
     content.Body=replacement;p.ResetAttempt();
     Assert.That(p.ReviewBodyWarmCount,Is.EqualTo(2),"A different body must replace and measure its source");
     double next=Convert.ToDouble(property.GetValue(p));Assert.That(next,Is.GreaterThan(0));
     p.ResetAttempt();Assert.That(Convert.ToDouble(property.GetValue(p)),Is.EqualTo(next));
     Assert.That(p.ReviewBodyWarmCount,Is.EqualTo(2));
    }
    finally{UnityEngine.Object.DestroyImmediate(replacement);}
   }
   finally{UnityEngine.Object.DestroyImmediate(host);UnityEngine.Object.DestroyImmediate(content);UnityEngine.Object.DestroyImmediate(mesh);}
  }
  static Transform Child(GameObject parent,string name,params Type[] components)
  {var go=new GameObject(name,components);go.transform.SetParent(parent.transform,false);return go.transform;}
 }
}

#endif
