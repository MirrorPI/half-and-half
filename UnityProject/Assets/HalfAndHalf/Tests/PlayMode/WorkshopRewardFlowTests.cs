#if UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
using System.Collections;
using System.Reflection;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HalfAndHalf.Tests
{
    public sealed class WorkshopRewardFlowTests
    {
        const string CountKey = "halfandhalf.knife.rewarded.v1";
        const string CooldownKey = "halfandhalf.ads.cooldownSeconds.v1";
        const string PrivacyKey = "halfandhalf.ads.contextual.admob.v1";
        const string UmpConsentKey = "GoogleMobileAds.Ump.ConsentStatus";

        [UnityTest]
        public IEnumerator WorkshopUnlocksPhotosOnlyAfterFiveOptInAds()
        {
            int originalCount = PlayerPrefs.GetInt(CountKey, -1);
            int originalCooldown = PlayerPrefs.GetInt(CooldownKey, -1);
            BladeWorkshop shop = null;
            int originalChoice = PlayerPrefs.GetInt(PrivacyKey, -1);
            int originalUmpConsent = PlayerPrefs.GetInt(UmpConsentKey, -1);
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 1;
                PlayerPrefs.DeleteKey(CountKey);
                PlayerPrefs.DeleteKey(PrivacyKey);
                PlayerPrefs.DeleteKey(UmpConsentKey);
                foreach (var oldAds in Object.FindObjectsByType<RoundAds>(FindObjectsSortMode.None)) oldAds.enabled = false;
                yield return null;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Gameplay29.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                shop = Object.FindFirstObjectByType<BladeWorkshop>();
                shop.OpenButton.onClick.Invoke();
                yield return null;
                Assert.IsTrue(shop.Panel.activeInHierarchy);
                Assert.IsTrue(shop.DefaultPreview.activeInHierarchy);
                Assert.IsFalse(shop.PickButton.gameObject.activeInHierarchy);
                Assert.IsTrue(shop.RemoveAdsButton.transform.Find("VideoIcon").gameObject.activeInHierarchy);
                var counter = shop.RemoveAdsButton.transform.Find("RewardProgress").GetComponent<TMP_Text>();
                Assert.AreEqual("0/5", counter.text);
                // Merely opening/leaving the workshop never starts a rewarded ad.
                Assert.IsFalse(shop.PrivacyPanel.activeInHierarchy);
                shop.CloseButton.onClick.Invoke();
                shop.OpenButton.onClick.Invoke();
                yield return null;
                Assert.IsFalse(shop.PrivacyPanel.activeInHierarchy);
                Assert.AreEqual(0, shop.Ads.KnifeProgress.Count);
                Assert.IsFalse(PlayerPrefs.HasKey(PrivacyKey));
                for (int i = 1; i <= 5; i++)
                {
                    float deadline = Time.realtimeSinceStartup + 12;
                    while (!shop.Ads.RewardReady && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(shop.Ads.RewardReady, shop.StoreStatus.text + " step=" + i
                        + " privacy=" + shop.Ads.AdsRequestAllowed + " count=" + shop.Ads.KnifeProgress.Count
                        + " panel=" + shop.Panel.activeSelf + " photo=" + shop.Store.PhotoKnife
                        + " open=" + typeof(RoundAds).GetField("workshopOpen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shop.Ads)
                        + " loading=" + typeof(RoundAds).GetField("rewardLoading", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shop.Ads));
                    shop.RemoveAdsButton.onClick.Invoke();
                    deadline = Time.realtimeSinceStartup + 15;
                    while (shop.Ads.KnifeProgress.Count < i && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.AreEqual(i, PlayerPrefs.GetInt(CountKey));
                    // Google's Editor ad grants after its five-second countdown, then
                    // waits for Close Ad. Closing exercises the real SDK close callback.
                    yield return CloseEditorAd();
                    deadline = Time.realtimeSinceStartup + 5;
                    while (shop.Ads.RewardBusy && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsFalse(shop.Ads.RewardBusy);
                    yield return null;
                    Assert.AreEqual(i == 5, shop.PickButton.gameObject.activeInHierarchy);
                    if (i < 5) Assert.AreEqual(i + "/5", counter.text);
                    if (i == 2)
                    {
                        foreach (var oldAds in Object.FindObjectsByType<RoundAds>(FindObjectsSortMode.None)) oldAds.enabled = false;
                        yield return null;
                        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Gameplay29.unity",
                            new LoadSceneParameters(LoadSceneMode.Single));
                        yield return null;
                        shop = Object.FindFirstObjectByType<BladeWorkshop>();
                        shop.OpenButton.onClick.Invoke();
                        yield return null;
                        counter = shop.RemoveAdsButton.transform.Find("RewardProgress").GetComponent<TMP_Text>();
                        Assert.AreEqual("2/5", counter.text);
                        Assert.IsFalse(shop.PrivacyPanel.activeInHierarchy);
                        Assert.IsFalse(PlayerPrefs.HasKey(PrivacyKey));
                    }
                }
                Assert.IsFalse(shop.RemoveAdsButton.gameObject.activeInHierarchy);
                shop.CloseButton.onClick.Invoke();
                shop.OpenButton.onClick.Invoke();
                Assert.IsTrue(shop.PickButton.gameObject.activeInHierarchy);
            }
            finally
            {
                if (shop != null) shop.Ads.enabled = false;
                Restore(CooldownKey, originalCooldown);
                Restore(CountKey, originalCount);
                Restore(PrivacyKey, originalChoice);
                Restore(UmpConsentKey, originalUmpConsent);
                PlayerPrefs.Save();
                CleanupEditorAd();
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator EighthRoundAdWorksWithoutWorkshopOptInAndReturnsWithoutKnifeCredit()
        {
            int originalCooldown = PlayerPrefs.GetInt(CooldownKey, -1);
            int originalChoice = PlayerPrefs.GetInt(PrivacyKey, -1);
            int originalUmpConsent = PlayerPrefs.GetInt(UmpConsentKey, -1);
            float originalTimeScale = Time.timeScale;
            RoundAds ads = null;
            try
            {
                Time.timeScale = 1;
                // The retired app-wide switch must not prevent ordinary ads.
                // Google's separate UMP choice still controls whether ads can load.
                PlayerPrefs.SetInt(PrivacyKey, 0);
                PlayerPrefs.DeleteKey(UmpConsentKey);
                foreach (var oldAds in Object.FindObjectsByType<RoundAds>(FindObjectsSortMode.None)) oldAds.enabled = false;
                yield return null;
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Gameplay29.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                ads = Object.FindFirstObjectByType<RoundAds>();
                float deadline = Time.realtimeSinceStartup + 8;
                // Google creates its visible Editor ad only when Show is called.
                while (!InterstitialReady(ads) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(InterstitialReady(ads), "AdMob interstitial did not become ready.");
                Assert.AreEqual(0, PlayerPrefs.GetInt(PrivacyKey), "Do not rewrite legacy choices as consent.");
                // Inject elapsed play time to exercise the actual SDK show and close without waiting a minute.
                var rounds = new RoundAdBreak(7, _ => {}, () => 60);
                typeof(RoundAds).GetField("rounds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ads, rounds);
                int before = ads.KnifeProgress.Count;
                int resumed = 0;
                ads.CountCompletedRound();
                ads.ContinueAfterAd(() => resumed++);
                Assert.AreEqual(0, resumed);
                deadline = Time.realtimeSinceStartup + 5;
                while (string.IsNullOrEmpty(ads.LastDisplayedAd) && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsNotEmpty(ads.LastDisplayedAd);
                yield return CloseEditorAd();
                deadline = Time.realtimeSinceStartup + 5;
                while (resumed == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                yield return null;
                Assert.AreEqual(1, resumed);
                Assert.AreEqual(before, ads.KnifeProgress.Count);
                Assert.AreEqual(90, rounds.RemainingCooldownSeconds);
                // Removing our extra switch must not bypass Google's privacy gate.
                ads.enabled = false;
                System.Type.GetType("GoogleMobileAds.Ump.Api.ConsentInformation, GoogleMobileAds.Ump", true)
                    .GetMethod("Reset").Invoke(null, null);
                Assert.IsFalse(ads.AdsRequestAllowed);
                Assert.IsFalse(ads.RewardReady);
                rounds = new RoundAdBreak(7, _ => {}, () => 60);
                typeof(RoundAds).GetField("rounds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ads, rounds);
                ads.CountCompletedRound();
                int withoutConsent = 0;
                ads.ContinueAfterAd(() => withoutConsent++);
                Assert.AreEqual(1, withoutConsent, "Play continues when Google cannot request ads.");
            }
            finally
            {
                if (ads != null) ads.enabled = false;
                Restore(CooldownKey, originalCooldown);
                Restore(PrivacyKey, originalChoice);
                Restore(UmpConsentKey, originalUmpConsent);
                PlayerPrefs.Save();
                CleanupEditorAd();
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator PrivacyFormPausesPlayableAttemptAndAdGraceClock()
        {
            foreach (var oldAds in Object.FindObjectsByType<RoundAds>(FindObjectsSortMode.None)) oldAds.enabled = false;
            yield return null;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Gameplay29.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var game = Object.FindFirstObjectByType<HalfAndHalf.Gameplay.AppleGame>();
            var ads = Object.FindFirstObjectByType<RoundAds>();
            var setForm = typeof(RoundAds).GetMethod("SetPrivacyFormOpen", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(setForm, "Native privacy presentation must suspend gameplay and ad clocks.");
            var clock = (System.Diagnostics.Stopwatch)typeof(RoundAds)
                .GetField("foregroundPlay", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ads);
            var gameClock = typeof(HalfAndHalf.Gameplay.AppleGame).GetProperty("Now", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                setForm.Invoke(null, new object[] { true });
                long before = (long)gameClock.GetValue(game);
                double adBefore = clock.Elapsed.TotalSeconds;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.AreEqual(before, (long)gameClock.GetValue(game));
                Assert.AreEqual(adBefore, clock.Elapsed.TotalSeconds);
                Assert.IsFalse(game.BeginPointer(-1, Vector2.zero));
                setForm.Invoke(null, new object[] { false });
                yield return new WaitForSecondsRealtime(.1f);
                Assert.Greater((long)gameClock.GetValue(game), before);
            }
            finally { setForm.Invoke(null, new object[] { false }); }
        }

        static bool InterstitialReady(RoundAds ads)
        {
            var ad = typeof(RoundAds).GetField("ad", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ads);
            return ad != null && (bool)ad.GetType().GetMethod("CanShowAd").Invoke(ad, null);
        }

        static GameObject FindEditorAd() => GameObject.Find("768x1024(Clone)")
            ?? GameObject.Find("1024x768(Clone)");

        static IEnumerator CloseEditorAd()
        {
            float deadline = Time.realtimeSinceStartup + 5;
            Button close = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var ad = FindEditorAd();
                close = ad == null ? null : ad.transform.Find("Ad/Button").GetComponent<Button>();
                if (close != null && close.gameObject.activeInHierarchy && close.interactable) break;
                yield return null;
            }
            Assert.IsNotNull(close, "Google Editor ad has no Close Ad button.");
            Assert.IsTrue(close.gameObject.activeInHierarchy && close.interactable);
            close.onClick.Invoke();
            yield return null;
        }

        static void CleanupEditorAd()
        {
            var ad = FindEditorAd();
            if (ad != null) Object.Destroy(ad);
        }

        static void Restore(string key, int previous)
        {
            if (previous == -1) PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetInt(key, previous);
        }
    }
}
#endif
