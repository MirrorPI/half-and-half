using System;
using System.Reflection;
using HalfAndHalf.Services;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class AppStoreUpdateTests
    {
        string Offer(string installed, string response)
        {
            var type = typeof(GameCenterFlow).Assembly.GetType("HalfAndHalf.Services.AppStoreUpdate");
            Assert.That(type, Is.Not.Null, "Public App Store versions need an update notice policy.");
            return (string)type.GetMethod("AvailableVersion").Invoke(null, new object[] { response, installed });
        }
        const string Published = "{\"resultCount\":1,\"results\":[{\"trackId\":6821765037,\"bundleId\":\"com.sungsoo.halfandhalf.mvp\",\"version\":\"1.0.1\"}]}";

        [Test] public void PublishedNewVersionIsOfferedButInstalledOrReviewVersionIsNot()
        {
            Assert.That(Offer("1.0", Published), Is.EqualTo("1.0.1"));
            Assert.That(Offer("1.0.1", Published), Is.Null);
            Assert.That(Offer("1.0.2", Published), Is.Null);
            Assert.That(Offer("1.0.0", Published.Replace("1.0.1", "1.0")), Is.Null);
            Assert.That(Offer("1.9", Published.Replace("1.0.1", "1.10")), Is.EqualTo("1.10"));
        }
        [Test] public void MissingInvalidOrDifferentAppResponseDoesNotOfferAnUpdate()
        {
            Assert.That(Offer("1.0", ""), Is.Null);
            Assert.That(Offer("1.0", "{\"resultCount\":0,\"results\":[]}"), Is.Null);
            Assert.That(Offer("1.0", Published.Replace("6821765037", "1")), Is.Null);
            Assert.That(Offer("1.0", Published.Replace("1.0.1", "invalid")), Is.Null);
        }
    }
}
