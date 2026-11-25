using System;
using HalfAndHalf.Services;
using NUnit.Framework;
namespace HalfAndHalf.Tests
{
    public sealed class Task5LeaderboardTests
    {
        [TestCase("L01"), TestCase("L02"), TestCase("L03"), TestCase("L04"), TestCase("L05"),
         TestCase("N01"), TestCase("N02"), TestCase("N03"), TestCase("N04"), TestCase("N05")]
        public void ExplicitExpansionMappingResolvesOnlyItsVersion(string id)
        {
            var catalog=new LeaderboardCatalog(new[]{new LeaderboardMapping {objectId=id,contentVersion="qa-v1",leaderboardId="fixture."+id}});
            Assert.That(catalog.Resolve(id,"qa-v1"),Is.EqualTo("fixture."+id));
            Assert.That(catalog.Resolve(id,"qa-v2"),Is.Null);
        }
        [TestCase("L06"),TestCase("N06"),TestCase("P01"),TestCase("G03"),TestCase("UNKNOWN")]
        public void MappingOutsideCurrentTwentyIsRejected(string id)
        {
            Assert.Throws<ArgumentException>(()=>new LeaderboardCatalog(new[]{new LeaderboardMapping {objectId=id,contentVersion="qa-v1",leaderboardId="fixture."+id}}));
        }
        [Test] public void UnregisteredExpansionHasNoImplicitBoard()
        {
            var catalog=new LeaderboardCatalog(new[]{new LeaderboardMapping {objectId="B01",contentVersion="qa-v1",leaderboardId="fixture.apple"}});
            Assert.That(catalog.Resolve("L01","L01-sample-v1"),Is.Null);Assert.That(catalog.Resolve("N05","qa-v1"),Is.Null);
        }
    }
}
