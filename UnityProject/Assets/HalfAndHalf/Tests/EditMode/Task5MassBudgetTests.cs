using HalfAndHalf.Core;using NUnit.Framework;
namespace HalfAndHalf.Tests {
 public sealed class Task5MassBudgetTests {
  [TestCase(true),TestCase(false)] public void OutsideMapMustNotDivideOrOverflow(bool right){
   var map=new MassMap(new[]{new MassCell(-1m,0m,0m,1m,30m),new MassCell(0m,1m,0m,1m,70m)});
   var s=map.Split(right?decimal.MaxValue:decimal.MinValue);Assert.That(s.Left,Is.EqualTo(right?100m:0m));Assert.That(s.Right,Is.EqualTo(right?0m:100m));
  }
  [Test]public void UnorderedAndOverlappingCellsMustKeepOriginalAdditionOrder(){
   var m=new MassMap(new[]{new MassCell(0,3,0,1,.1m),new MassCell(-1,0,0,1,.399975m),new MassCell(0,3,0,1,.1m),new MassCell(2,3,0,1,.300025m),new MassCell(0,3,0,1,.1m)});
   var s=m.Split(1);Assert.That(s.Left,Is.EqualTo(.4999749999999999999999999999m));Assert.That(Scoring.Accuracy(s.Left,s.Right),Is.EqualTo(9999));
  }
  [Test]public void OrderedColumnsMustKeepRoundedProductsAfterFullPrefix(){
   var m=new MassMap(new[]{new MassCell(-2,-1,0,1,.399975m),new MassCell(0,3,0,1,.1m),new MassCell(0,3,0,1,.1m),new MassCell(0,3,0,1,.1m),new MassCell(4,5,0,1,.300025m)});
   Assert.That(m.Split(-.5m).Left,Is.EqualTo(.399975m));Assert.That(m.Split(0).Left,Is.EqualTo(.399975m));Assert.That(m.Split(1).Left,Is.EqualTo(.4999749999999999999999999999m));Assert.That(m.Split(3).Left,Is.EqualTo(.699975m));
  }
  [Test]public void InteriorDivisionMustKeepOriginalDecimalRounding(){
   Assert.That(new MassMap(new[]{new MassCell(0,3,0,1,3)}).Split(1).Left,Is.EqualTo(.9999999999999999999999999999m));
   Assert.That(new MassMap(new[]{new MassCell(0,3,0,1,1)}).Split(2).Left,Is.EqualTo(.6666666666666666666666666667m));
  }
  [Test]public void PerCellRoundingMustNotChangePerfectAtHalfUpBoundary(){
   var m=new MassMap(new[]{new MassCell(0,3,0,1,.1m),new MassCell(0,3,0,1,.1m),new MassCell(0,3,0,1,.1m),new MassCell(-1,0,0,1,.399975m),new MassCell(2,3,0,1,.300025m)});
   var s=m.Split(1);Assert.That(s.Left,Is.EqualTo(.4999749999999999999999999999m));Assert.That(Scoring.Accuracy(s.Left,s.Right),Is.EqualTo(9999));
  }
  [Test]public void IdenticalColumnsMustNotRegroupRoundedProducts(){
   var m=new MassMap(new[]{new MassCell(0,3,0,1,.1m),new MassCell(0,3,1,2,.1m),new MassCell(0,3,2,3,.1m)});
   Assert.That(m.Split(1).Left,Is.EqualTo(.0999999999999999999999999999m));
  }
 }
}
