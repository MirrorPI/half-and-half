using System.Reflection;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public class BladeBundleTests
    {
        [Test]
        public void FiveDistinctRewardCompletionsUnlockAcrossSessions()
        {
            int saved = 0;
            var progress = new KnifeAdProgress(saved, value => saved = value);
            progress.ForOneShowing();
            Assert.AreEqual(0, saved);
            var first = progress.ForOneShowing();
            first();
            first();
            Assert.AreEqual(1, saved);
            progress = new KnifeAdProgress(saved, value => saved = value);
            for (int i = 0; i < 3; i++) progress.ForOneShowing()();
            Assert.IsFalse(progress.Unlocked);
            progress.ForOneShowing()();
            progress.ForOneShowing()();
            Assert.AreEqual(5, saved);
            Assert.IsTrue(new KnifeAdProgress(saved, null).Unlocked);
        }

        [Test]
        public void VerifiedBundleGrantsBothAndRefundPreservesIndependentPurchase()
        {
            var gameObject = new GameObject();
            gameObject.SetActive(false);
            try
            {
                var store = gameObject.AddComponent<BladeStore>();
                var set = typeof(BladeStore).GetMethod("SetOwnership", BindingFlags.Instance | BindingFlags.NonPublic);
                void Own(string id, bool value) => set.Invoke(store, new object[]{id, value});
                Own("starter_pack", true);
                Assert.IsTrue(store.NoAds && store.PhotoKnife);
                Own("no_ads", true);
                Own("starter_pack", false);
                Assert.IsTrue(store.NoAds);
                Assert.IsFalse(store.PhotoKnife);
                Own("no_ads", false);
                Assert.IsFalse(store.NoAds);
            }
            finally { Object.DestroyImmediate(gameObject); }
        }
    }
}
