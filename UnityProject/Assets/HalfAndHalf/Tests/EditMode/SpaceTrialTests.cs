using System;
using System.Linq;
using HalfAndHalf.Core;
using NUnit.Framework;
namespace HalfAndHalf.Tests
{
 public sealed class SpaceTrialTests
 {
  [Test] public void SomeGrainsMoveAtDifferentSpeedsAndApproachCompletesAtSixSeconds()
  {
   var cells=new System.Collections.Generic.List<HalfAndHalf.Presentation.SpaceVoxel>();
   HalfAndHalf.Presentation.SpaceTrialModel.Fill(cells,"G03",6000);
   int movedDifferently=0,index=0;
   for(int g=0;g<2;g++)
   {
    HalfAndHalf.Presentation.SpaceTrialModel.GalaxyPose(g,6000,out var center,out var motion);
    Assert.That(UnityEngine.Mathf.Abs(center.x),Is.EqualTo(.25f).Within(.00001f));
    foreach(var grain in HalfAndHalf.Presentation.SpaceTrialModel.GalaxyGrains(g))
    {
     var rigid=HalfAndHalf.Presentation.SpaceTrialModel.GalaxyPosition(grain.Center,center,motion);
     if(UnityEngine.Vector3.Distance(rigid,cells[index++].Center)>.0001f)movedDifferently++;
    }
   }
   Assert.That(movedDifferently,Is.GreaterThan(0));Assert.That(movedDifferently,Is.LessThan(cells.Count/4));
  }
  [Test] public void GalaxyDustUsesFineGrainsAndKeepsMassForEveryGrain()
  {
   var cells=new System.Collections.Generic.List<HalfAndHalf.Presentation.SpaceVoxel>();
   HalfAndHalf.Presentation.SpaceTrialModel.Fill(cells,"G03",4000);
   Assert.That(cells.Count,Is.GreaterThanOrEqualTo(6000));
   Assert.That(cells.Max(c=>c.Size),Is.LessThanOrEqualTo(.0181f));
   Assert.That(cells.Sum(c=>c.Mass),Is.EqualTo(100m));
   Assert.That(HalfAndHalf.Presentation.SpaceTrialModel.MassAt("G03",4000).Cells.Count,Is.EqualTo(cells.Count));
  }
  // Rejects scoring from a static map while the visible bodies move.
  [Test] public void CutElapsedTimeSelectsMassAndReplaysAfterInterruption()
  {
   var ctor=typeof(ObjectDefinition).GetConstructors().FirstOrDefault(c=>c.GetParameters().Length==5);
   Assert.That(ctor,Is.Not.Null,"Missing time-indexed mass definition");
   Func<long,MassMap> sample=t=>new MassMap(new[]{new MassCell(-1,0,0,1,t==700?2:6),new MassCell(0,1,0,1,t==700?6:2)});
   var definition=(ObjectDefinition)ctor.Invoke(new object[]{"P04","trial",sample(0),"trial",sample});
   var session=new AttemptSession(definition,-2,2);session.Land(1000,TransformState.Identity);session.BeginDrag(1,0,1200);session.ReleaseDrag(1,0,1700);
   Assert.That(session.Result.LeftMass,Is.EqualTo(2));Assert.That(session.Result.AccuracyUnits,Is.EqualTo(5000));
   var saved=session.Snapshot;session.Interrupt();
   Assert.That(Scoring.Evaluate(definition,saved).LeftMass,Is.EqualTo(2));Assert.That(session.Result.LeftMass,Is.EqualTo(2));
   session.Retry();session.Land(3000,TransformState.Identity);session.BeginDrag(1,0,3100);session.ReleaseDrag(1,0,3200);
   Assert.That(session.Result.LeftMass,Is.EqualTo(6));
  }
  [TestCase("P04"),TestCase("G03")]
  public void MovingFramesConserveMassRemainBoundedAndReplay(string id)
  {
   var type=typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.SpaceTrialModel");Assert.That(type,Is.Not.Null,"Missing moving space model");
   var method=type.GetMethod("MassAt");Func<long,MassMap> map=t=>(MassMap)method.Invoke(null,new object[]{id,t});
   var first=map(0);Assert.That(first.TotalMass,Is.EqualTo(100m));
   bool changed=false;
   foreach(long time in new long[]{1000,4000,8000,24000,119999})
   {
    var frame=map(time);Assert.That(frame.TotalMass,Is.EqualTo(100m));var split=frame.Split(.173m);var again=map(time).Split(.173m);
    Assert.That(split.Left,Is.EqualTo(again.Left));Assert.That(split.Left+split.Right,Is.EqualTo(100m));
    changed|=Math.Abs(frame.Split(0).Left-first.Split(0).Left)>.1m;
    Assert.That(frame.Cells.All(c=>c.MinX>=-1.75m&&c.MaxX<=1.75m&&c.MinY>=-1.5m&&c.MaxY<=1.5m),Is.True);
   }
   Assert.That(changed,Is.True,"Movement must influence mass split");
   if(id=="G03")Assert.That(first.Split(0).Right,Is.GreaterThan(first.Split(0).Left),"Right galaxy initially heavier");
  }
 }
}
