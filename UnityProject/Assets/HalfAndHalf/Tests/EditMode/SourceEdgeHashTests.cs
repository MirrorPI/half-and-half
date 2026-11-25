using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HalfAndHalf.Content;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEditor;
namespace HalfAndHalf.Tests {
 public sealed class SourceEdgeHashTests {
  static Dictionary<long,int> Edges()=> (Dictionary<long,int>)typeof(MeshSection).GetMethod("CreateSourceEdges",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{18432});
  [Test] public void AdjacentSourceEdgesDoNotCollapseIntoLongHashChains(){
   var mesh=AssetDatabase.LoadAssetAtPath<AppleDefinitionAsset>("Assets/HalfAndHalf/Content/B01/B01Prototype.asset").Body;var triangles=mesh.triangles;var edges=Edges();
   for(int slot=0;slot<triangles.Length;slot++){long key=((long)triangles[slot]<<32)|(uint)triangles[slot-slot%3+(slot+1)%3];if(!edges.ContainsKey(key))edges.Add(key,edges.Count);}
   Assert.That(edges.Count,Is.GreaterThan(1000));
   int worst=edges.Keys.GroupBy(key=>edges.Comparer.GetHashCode(key)&int.MaxValue).Max(group=>group.Count());
   Assert.That(worst,Is.LessThanOrEqualTo(16),"Adjacent source vertex pairs collapse under the default long high-XOR-low hash, making cold preparation quadratic");
  }
  [TestCase(0,1),TestCase(12,4096),TestCase(int.MaxValue,int.MinValue)]
  public void DirectedSourceEdgesKeepExactIdentity(int a,int b){
   var edges=Edges();long forward=((long)a<<32)|(uint)b,backward=((long)b<<32)|(uint)a;
   edges.Add(forward,7);edges.Add(backward,11);Assert.That(edges[forward],Is.EqualTo(7));Assert.That(edges[backward],Is.EqualTo(11));
  }
 }
}
