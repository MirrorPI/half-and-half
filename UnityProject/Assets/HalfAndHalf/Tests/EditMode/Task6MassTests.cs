using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HalfAndHalf.Core;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using NUnit.Framework;
namespace HalfAndHalf.Tests
{
 public sealed class Task6MassTests
 {
  // A rotated cell must be clipped by its area, not rejected or replaced by its AABB.
  [TestCase(90, -.25, 75), TestCase(180, -.5, 75), TestCase(270, .25, 25)]
  public void RotatedCellsSplitByIncludedArea(int degrees,double knife,double left)
  {
   var definition=new ObjectDefinition("fixture","v1",new MassMap(new[]{new MassCell(0,2,0,1,100)}),"");
   var cut=new CutSnapshot("fixture","v1",(decimal)knife,0,new TransformState(0,0,1,1,degrees),true);
   var result=Scoring.Evaluate(definition,cut);
   Assert.That(result.LeftMass,Is.EqualTo((decimal)left).Within(.000000001m));
   Assert.That(result.LeftMass+result.RightMass,Is.EqualTo(100m));
  }
  [Test] public void ObliqueScaledTranslatedCellUsesPolygonArea()
  {
   var definition=new ObjectDefinition("fixture","v1",new MassMap(new[]{new MassCell(-1,1,-1,1,100)}),"");
   // Rotated square diamond: x=sqrt(2)/2 excludes one triangle, 1/8 of total area.
   var cut=new CutSnapshot("fixture","v1",(decimal)(Math.Sqrt(2)/2),0,new TransformState(0,0,1,1,45),true);
   var r=Scoring.Evaluate(definition,cut);Assert.That(r.LeftMass,Is.EqualTo(87.5m).Within(.00000001m));
   var rectangle=new ObjectDefinition("fixture","v1",new MassMap(new[]{new MassCell(0,2,0,1,100)}),"");
   r=Scoring.Evaluate(rectangle,new CutSnapshot("fixture","v1",.25m,0,new TransformState(1,5,2,3,90),true));
   Assert.That(r.LeftMass,Is.EqualTo(75m));
  }
  [TestCase("P01"),TestCase("P04"),TestCase("G03")]
  public void SameCutIgnoresFrameCadenceAndReadyAbsoluteTime(string id)
  {
   var definition=SpaceTrialModel.Definition(id);AttemptResult reference=null;
   foreach(int fps in new[]{30,60,120})foreach(long ready in new[]{0L,10000L,987654321L})
   {
    var session=new AttemptSession(definition,-2,2);session.Land(ready,TransformState.Identity);
    // Full frames plus the same input timestamp, independent of the last rendered frame.
    for(int frame=0;frame*1000L/fps<6187;frame++)session.Tick(ready+frame*1000L/fps);
    session.BeginDrag(9,.173456m,ready+6187);session.ReleaseDrag(9,.173456m,ready+6187);
    var r=session.Result;Assert.That(r.ElapsedMilliseconds,Is.EqualTo(6187));
    if(reference!=null){Assert.That(r.LeftMass,Is.EqualTo(reference.LeftMass));Assert.That(r.AccuracyUnits,Is.EqualTo(reference.AccuracyUnits));}
    reference=r;
   }
  }
  [TestCase("P01"),TestCase("P04"),TestCase("G03")]
  public void MassFollowsDisplayedCellBoundsAndGroupsConserveAtOverlap(string id)
  {
   var cells=new List<SpaceVoxel>();
   foreach(long t in new[]{0L,1L,5999L,6000L,6187L,24000L,119999L})
   {
    SpaceTrialModel.Fill(cells,id,t);var map=SpaceTrialModel.MassAt(id,t);
    Assert.That(map.TotalMass,Is.EqualTo(100m));Assert.That(map.Cells.Count,Is.EqualTo(cells.Count(c=>c.Mass>0)));
    decimal[] groups=id=="P04"?new[]{80m,5m,5m,5m,5m}:id=="G03"?new[]{38m,62m}:new[]{100m};
    for(int part=0;part<groups.Length;part++)Assert.That(cells.Where(c=>c.Part==part).Sum(c=>c.Mass),Is.EqualTo(groups[part]));
    int i=0;foreach(var c in cells.Where(c=>c.Mass>0))
    {
     var m=map.Cells[i++];double half=c.Size*.5;
     Assert.That((double)m.MinX,Is.EqualTo(c.Center.x-half).Within(.0000002));
     Assert.That((double)m.MaxX,Is.EqualTo(c.Center.x+half).Within(.0000002));
     Assert.That((double)m.MinY,Is.EqualTo(c.Center.y-half).Within(.0000002));
     Assert.That((double)m.MaxY,Is.EqualTo(c.Center.y+half).Within(.0000002));
     Assert.That(m.Mass,Is.EqualTo(c.Mass));
    }
   }
  }
  [Test] public void OverlappingBodiesAndDecorationUseMassSumWithoutDistanceWeights()
  {
   var map=new MassMap(new[]{new MassCell(-10,-8,0,1,20),new MassCell(0,2,0,1,50),new MassCell(0,2,0,1,30),new MassCell(-100,100,-50,50,0)});
   Assert.That(map.Split(1).Left,Is.EqualTo(60m));Assert.That(map.Split(1).Right,Is.EqualTo(40m));
   // Move the whole left body ten times farther: same left mass, no torque/distance scoring.
   var farther=new MassMap(new[]{new MassCell(-100,-98,0,1,20),new MassCell(0,2,0,1,80)});
   Assert.That(farther.Split(1).Left,Is.EqualTo(60m));
   Assert.That(map.Split(.99999m).Left,Is.EqualTo(59.9996m));Assert.That(map.Split(1.00001m).Left,Is.EqualTo(60.0004m));
   Assert.That(map.Split(0).Left,Is.EqualTo(20m));Assert.That(map.Split(2).Left,Is.EqualTo(100m));
  }
  [TestCase("P01"),TestCase("P04"),TestCase("G03")]
  public void CellEdgesAreContinuousAndImmediateCutRetryReplays(string id)
  {
   var definition=SpaceTrialModel.Definition(id);var map=definition.MassAt(6187);var cell=map.Cells.First(c=>c.MinX<1.3m&&c.MaxX>-.9m);
   decimal middle=(cell.MinX+cell.MaxX)/2,epsilon=.00000001m;
   var a=map.Split(middle-epsilon);var b=map.Split(middle);var c=map.Split(middle+epsilon);
   Assert.That(a.Left,Is.LessThan(b.Left));Assert.That(b.Left,Is.LessThan(c.Left));
   foreach(decimal x in new[]{cell.MinX-epsilon,cell.MinX,cell.MinX+epsilon,cell.MaxX-epsilon,cell.MaxX,cell.MaxX+epsilon})
   {var s=map.Split(x);Assert.That(s.Left+s.Right,Is.EqualTo(100m));Assert.That(Math.Abs(map.Split(x+epsilon).Left-s.Left),Is.LessThan(.001m));}
   var session=new AttemptSession(definition,-2,2);AttemptResult first=null;
   foreach(long ready in new[]{1000L,5000L})
   {
    session.Retry();session.Land(ready,TransformState.Identity);Assert.That(session.BeginDrag(1,.15m,ready),Is.True);Assert.That(session.ReleaseDrag(1,.15m,ready),Is.True);
    Assert.That(session.Elapsed(ready+9999),Is.Zero);Assert.That(session.Result.LeftMass+session.Result.RightMass,Is.EqualTo(100m));
    if(first!=null)Assert.That(session.Result.LeftMass,Is.EqualTo(first.LeftMass));first=session.Result;
   }
  }
  [Test] public void MoonDensityMovesWhileTotalMassIsConserved()
  {
   var a=SpaceTrialModel.MassAt("P01",0);var b=SpaceTrialModel.MassAt("P01",7000);
   Assert.That(a.TotalMass,Is.EqualTo(100m));Assert.That(b.TotalMass,Is.EqualTo(100m));
   Assert.That(Math.Abs(a.Split(0).Left-b.Split(0).Left),Is.GreaterThan(1m));
  }
  [TestCase("P01"),TestCase("P04"),TestCase("G03")]
  public void DynamicSnapshotRoundTripsThroughExistingStoreWithoutOfficialMapping(string id)
  {
   string path=Path.Combine(Path.GetTempPath(),"half-task6-"+Guid.NewGuid(),"progress.json");
   try
   {
    var d=SpaceTrialModel.Definition(id);var s=new CutSnapshot(id,d.ContentVersion,.173456m,6187,TransformState.Identity,true);
    var original=Scoring.Evaluate(d,s);var progress=new CollectionProgress(new[]{d});progress.Record(original);
    var store=new LocalProgressStore(path);store.Save(progress);var restored=new LocalProgressStore(path).Load(new[]{SpaceTrialModel.Definition(id)}).Best(0);
    Assert.That(restored,Is.Not.Null);Assert.That(restored.LeftMass,Is.EqualTo(original.LeftMass));Assert.That(restored.AccuracyUnits,Is.EqualTo(original.AccuracyUnits));Assert.That(restored.Snapshot.KnifeX,Is.EqualTo(s.KnifeX));Assert.That(restored.ElapsedMilliseconds,Is.EqualTo(6187));
   }
   finally{if(Directory.Exists(Path.GetDirectoryName(path)))Directory.Delete(Path.GetDirectoryName(path),true);}
  }
 }
}
