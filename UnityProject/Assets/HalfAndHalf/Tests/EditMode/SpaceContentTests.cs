using System.Collections.Generic;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Presentation;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class SpaceContentTests
    {
        [Test] public void NineStagesHaveDeterministicMassAndSolarGroupsMatchDesign()
        {
            var cells=new List<SpaceVoxel>();
            foreach(var id in SpaceContentModel.Ids){
                SpaceContentModel.Fill(cells,id,1800);
                Assert.That(cells.Sum(v=>v.Mass),Is.EqualTo(100m),id);
                Assert.That(cells.All(v=>System.Math.Abs(v.Center.x)+v.Size*.5f<1.85f&&System.Math.Abs(v.Center.y)+v.Size*.5f<1.4f),Is.True,id+" stays in game view");
                var map=SpaceContentModel.MassAt(id,1800);var split=map.Split(.13m);
                Assert.That(split.Left+split.Right,Is.EqualTo(100m),id);
                Assert.That(SpaceContentModel.MassAt(id,1800).Split(.13m).Left,Is.EqualTo(split.Left),id);
            }
            SpaceContentModel.Fill(cells,"S01",1800);
            Assert.That(cells.Where(v=>v.Part==0).Sum(v=>v.Mass),Is.EqualTo(25m));
            Assert.That(cells.Where(v=>v.Part>=1&&v.Part<=8).Sum(v=>v.Mass),Is.EqualTo(65m));
            Assert.That(cells.Where(v=>v.Part>=9).Sum(v=>v.Mass),Is.EqualTo(10m));
            Assert.That(cells.Select(v=>v.Part).Distinct().Count(),Is.EqualTo(16));
            SpaceContentModel.Fill(cells,"P05",1800);
            Assert.That(cells.Where(v=>v.Part==3).Sum(v=>v.Mass),Is.EqualTo(18m));
            Assert.That(cells.Select(v=>v.Part).Distinct().Count(),Is.EqualTo(4));
            foreach(string id in new[]{"P01","P02","P03","G01","G02"})
                Assert.That(SpaceContentModel.MassAt(id,0).Split(.1m).Left,Is.Not.EqualTo(SpaceContentModel.MassAt(id,2400).Split(.1m).Left),id+" moves scoring mass");
        }
    }
}
