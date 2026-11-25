using HalfAndHalf.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace HalfAndHalf.Services
{
    [DisallowMultipleComponent]
    public sealed class RoundAds : MonoBehaviour
    {
        const string ProgressKey = "halfandhalf.ads.rounds.v1";
        const string CooldownKey = "halfandhalf.ads.cooldownSeconds.v1";
        const string KnifeKey = "halfandhalf.knife.rewarded.v1";
        const double RetrySeconds = 30;
        const double AdLifetimeSeconds = 3600;

        [Serializable]
        sealed class PlatformSettings
        {
            public bool childDirected = false;
            // Enable only after the network's short-skippable inventory is confirmed.
            public bool shortSkipsConfirmed = false;
            public string appId = "";
            public string interstitialAdUnitId = "";
            public string rewardedAdUnitId = "";
            public bool RewardReady => !string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(rewardedAdUnitId);
            public bool Ready => shortSkipsConfirmed && !string.IsNullOrWhiteSpace(appId)
                && !string.IsNullOrWhiteSpace(interstitialAdUnitId);
        }

        [Serializable]
        sealed class Settings
        {
            public PlatformSettings ios = null;
            public PlatformSettings android = null;
        }

        // Native SDK initialization and UMP updates outlive gameplay scene reloads.
        static bool initializing, initialized, consentUpdating, consentUpdated, privacyFormOpen;
        static int consentGeneration, initGeneration;
        static double retryInitAt, initDeadline, retryConsentAt, consentDeadline;
        static KnifeAdProgress sharedKnifeProgress;
        static readonly HashSet<RewardedAd> pendingDisposal = new HashSet<RewardedAd>();
        BladeStore store;
        PlatformSettings platform;
        RoundAdBreak rounds;
        InterstitialAd ad;
        RewardedAd rewardAd;
        bool workshopOpen, rewardReady, rewardShowing, rewardDisplayed, loading, rewardLoading;
        int loadGeneration, rewardLoadGeneration;
        double rewardRetryAt, rewardDeadline, rewardLoadedAt, loadedAt;
        public KnifeAdProgress KnifeProgress { get; private set; }
        public bool RewardConfigured => platform?.RewardReady == true;
        public bool RewardReady => AdsPermitted && initialized && rewardReady && !rewardShowing
            && rewardAd != null && rewardAd.CanShowAd();
        public bool RewardBusy => rewardShowing;
        public bool RewardUnavailable => Time.realtimeSinceStartupAsDouble < rewardRetryAt;
        Action afterShow;
        readonly System.Diagnostics.Stopwatch foregroundPlay = new System.Diagnostics.Stopwatch();
        bool paused, focused, displaying;
        double retryLoadAt, callbackDeadline;

        public string LastDisplayedAd { get; private set; } = "";
        public bool CanConfigureAds => platform != null && (platform.Ready || platform.RewardReady);
        public bool PrivacyBusy => consentUpdating || privacyFormOpen;
        public bool PrivacyChecked => consentUpdated;
        public bool AdsRequestAllowed => consentUpdated && !PrivacyBusy && ConsentInformation.CanRequestAds();
        public bool PrivacyOptionsRequired => CanConfigureAds
            && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
        bool CanReceiveAds => store != null && store.EntitlementsKnown && !store.NoAds;
        bool AdsPermitted => AdsRequestAllowed;
        bool Eligible => AdsPermitted && platform?.Ready == true && CanReceiveAds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            initializing = initialized = consentUpdating = consentUpdated = privacyFormOpen = false;
            consentGeneration++;
            initGeneration++;
            retryInitAt = initDeadline = retryConsentAt = consentDeadline = 0;
            sharedKnifeProgress = null;
            pendingDisposal.Clear();
        }

        void Awake()
        {
            int savedKnifeProgress = PlayerPrefs.GetInt(KnifeKey);
            if (sharedKnifeProgress == null || sharedKnifeProgress.Count != savedKnifeProgress)
                sharedKnifeProgress = new KnifeAdProgress(savedKnifeProgress, value => SaveInt(KnifeKey, value));
            KnifeProgress = sharedKnifeProgress;
            // Retired app-wide ad choices are left untouched. Only UMP determines
            // privacy permission; choosing a workshop reward never changes it.
            rounds = new RoundAdBreak(PlayerPrefs.GetInt(ProgressKey), value => SaveInt(ProgressKey, value), () => foregroundPlay.Elapsed.TotalSeconds, PlayerPrefs.GetInt(CooldownKey));
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
#if UNITY_EDITOR
            ios = true;
#else
            if (!ios && Application.platform != RuntimePlatform.Android) return;
#endif
            var asset = Resources.Load<TextAsset>("RoundAdsConfig");
            if (asset != null)
            {
                try
                {
                    var settings = JsonUtility.FromJson<Settings>(asset.text);
                    platform = ios ? settings?.ios : settings?.android;
                }
                catch (ArgumentException e) { Debug.LogWarning("Round ad configuration: " + e.Message); }
            }
            if (Application.isEditor || Debug.isDebugBuild)
            {
                platform = new PlatformSettings {
                    childDirected = platform?.childDirected ?? false,
                    appId = ios ? "ca-app-pub-3940256099942544~1458002511" : "ca-app-pub-3940256099942544~3347511713",
                    rewardedAdUnitId = ios ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917",
                    interstitialAdUnitId = ios ? "ca-app-pub-3940256099942544/4411468910" : "ca-app-pub-3940256099942544/1033173712",
                    shortSkipsConfirmed = true
                };
            }
        }

        void OnEnable()
        {
            focused = Application.isFocused;
            // UMP may complete before MobileAds.Initialize creates its main-thread executor.
            if (CanConfigureAds) MobileAdsEventExecutor.Initialize();
            UpdateClock();
            if (privacyFormOpen) SetPrivacyFormOpen(true);
        }
        void OnApplicationFocus(bool hasFocus) { focused = hasFocus; UpdateClock(); }
        void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            UpdateClock();
            if (paused) SaveCooldown();
        }

        void SaveCooldown()
        {
            if (rounds != null && PlayerPrefs.GetInt(CooldownKey) != rounds.RemainingCooldownSeconds)
                SaveInt(CooldownKey, rounds.RemainingCooldownSeconds);
        }

        void RecordImpression()
        {
            rounds.RecordImpression();
            SaveCooldown();
        }

        void UpdateClock()
        {
            if (isActiveAndEnabled && focused && !paused && !displaying && !rewardDisplayed
                && !rewardShowing && !privacyFormOpen && afterShow == null) foregroundPlay.Start();
            else foregroundPlay.Stop();
        }

        static void SetPrivacyFormOpen(bool open)
        {
            privacyFormOpen = open;
            foreach (var controller in FindObjectsByType<RoundAds>(FindObjectsSortMode.None))
                controller.UpdateClock();
            foreach (var game in FindObjectsByType<HalfAndHalf.Gameplay.AppleGame>(FindObjectsSortMode.None))
                game.SetPrivacyOverlayOpen(open);
        }

        public void Configure(BladeStore bladeStore) => store = bladeStore;

        public void ShowPrivacyOptions(Action completed = null)
        {
            if (!PrivacyOptionsRequired || privacyFormOpen || consentUpdating || displaying || rewardShowing)
            {
                completed?.Invoke();
                return;
            }
            SetPrivacyFormOpen(true);
            EndAd();
            ReleaseReward();
            try
            {
                ConsentForm.ShowPrivacyOptionsForm(error => OnMainThread(() => {
                    SetPrivacyFormOpen(false);
                    if (error != null) Debug.LogWarning("Ad privacy options: " + error.Message);
                    completed?.Invoke();
                }));
            }
            catch (Exception error)
            {
                SetPrivacyFormOpen(false);
                Debug.LogWarning("Ad privacy options: " + error.Message);
                completed?.Invoke();
            }
        }

        static void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }

        static void OnMainThread(Action action) => MobileAdsEventExecutor.ExecuteInUpdate(action);
        public void CountCompletedRound() => rounds.CountCompletedRound();
        public void ContinueAfterAd(Action continuation) => rounds.ContinueAfterAd(Eligible, TryShow, continuation);

        void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (CanConfigureAds) PrepareSdk(now);
            PrepareReward();
            if (!Eligible)
            {
                if (ad != null || loading) EndAd();
                return;
            }
            if (ad != null || loading)
            {
                if (AdExpired(now))
                {
                    int expiredGeneration = loadGeneration;
                    // On iOS, opened/closed events can be queued while Unity is paused.
                    // Process those events before deciding that a callback was lost.
                    OnMainThread(() => {
                        if (this == null || !isActiveAndEnabled || expiredGeneration != loadGeneration
                            || !AdExpired(Time.realtimeSinceStartupAsDouble)) return;
                        retryLoadAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
                        EndAd();
                    });
                }
                return;
            }
            if (!initialized || now < retryLoadAt || Application.internetReachability == NetworkReachability.NotReachable) return;
            int generation = ++loadGeneration;
            loading = true;
            callbackDeadline = now + RetrySeconds;
            try
            {
                InterstitialAd.Load(platform.interstitialAdUnitId, new AdRequest(), (unit, error) => OnMainThread(() => {
                    if (this == null || !isActiveAndEnabled || generation != loadGeneration || !Eligible)
                    {
                        unit?.Destroy();
                        return;
                    }
                    loading = false;
                    if (error != null || unit == null)
                    {
                        unit?.Destroy();
                        retryLoadAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
                        EndAd();
                        return;
                    }
                    ad = unit;
                    loadedAt = Time.realtimeSinceStartupAsDouble;
                    callbackDeadline = double.PositiveInfinity;
                    unit.OnAdFullScreenContentOpened += () => OnMainThread(() => {
                        if (this == null || unit != ad || displaying) return;
                        displaying = true;
                        RecordAdInfo(unit.GetResponseInfo());
                        UpdateClock();
                        RecordImpression();
                        callbackDeadline = Time.realtimeSinceStartupAsDouble + 120;
                    });
                    unit.OnAdFullScreenContentClosed += () => OnMainThread(() => { if (this != null && unit == ad) EndAd(); });
                    unit.OnAdFullScreenContentFailed += failure => OnMainThread(() => {
                        if (this == null || unit != ad) return;
                        retryLoadAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
                        EndAd();
                    });
                }));
            }
            catch (Exception error)
            {
                Debug.LogWarning("Round ad preparation: " + error.Message);
                retryLoadAt = now + RetrySeconds;
                EndAd();
            }
        }

        bool AdExpired(double now) => (now >= callbackDeadline && Application.isFocused)
            || (ad != null && afterShow == null && now - loadedAt >= AdLifetimeSeconds);

        void PrepareSdk(double now)
        {
            if (consentUpdating && now >= consentDeadline && !privacyFormOpen)
            {
                consentGeneration++;
                consentUpdating = false;
                retryConsentAt = now + RetrySeconds;
            }
            if (initializing && now >= initDeadline)
            {
                initGeneration++;
                initializing = false;
                retryInitAt = now + RetrySeconds;
            }
            // Unknown can also occur after the Editor consent state is reset between sessions.
            if (consentUpdated && ConsentInformation.ConsentStatus == ConsentStatus.Unknown)
                consentUpdated = false;
            if (!consentUpdated)
            {
                if (!consentUpdating && !privacyFormOpen && now >= retryConsentAt
                    && Application.internetReachability != NetworkReachability.NotReachable)
                    GatherConsent(platform, now);
                return;
            }
            if (AdsPermitted && !initialized && !initializing && now >= retryInitAt)
                Initialize(platform, now);
        }

        static void GatherConsent(PlatformSettings settings, double now)
        {
            int generation = ++consentGeneration;
            consentUpdating = true;
            consentDeadline = now + RetrySeconds;
            try
            {
                ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = settings.childDirected },
                    error => OnMainThread(() => {
                        if (generation != consentGeneration) return;
                        if (error != null)
                        {
                            FinishConsent(generation, error);
                            return;
                        }
                        // Native consent UI owns its dismissal; do not time it out while it is open.
                        if (ConsentInformation.ConsentStatus == ConsentStatus.Required) SetPrivacyFormOpen(true);
                        try
                        {
                            ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                                OnMainThread(() => FinishConsent(generation, formError)));
                        }
                        catch (Exception formError)
                        {
                            Debug.LogWarning("Ad consent form: " + formError.Message);
                            FinishConsent(generation, null, false);
                        }
                    }));
            }
            catch (Exception error)
            {
                Debug.LogWarning("Ad consent update: " + error.Message);
                FinishConsent(generation, null, false);
            }
        }

        static void FinishConsent(int generation, FormError error, bool completed = true)
        {
            if (generation != consentGeneration) return;
            consentUpdating = false;
            SetPrivacyFormOpen(false);
            consentUpdated = ConsentInformation.CanRequestAds() || (completed && error == null);
            retryConsentAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
            if (error != null) Debug.LogWarning("Ad consent: " + error.Message);
        }

        static void Initialize(PlatformSettings settings, double now)
        {
            int generation = ++initGeneration;
            initializing = true;
            initDeadline = now + RetrySeconds;
            try
            {
                MobileAds.SetRequestConfiguration(new RequestConfiguration {
                    PublisherPrivacyPersonalizationState = PublisherPrivacyPersonalizationState.Disabled,
                    PublisherFirstPartyIdEnabled = false,
                    AgeRestrictedTreatment = settings.childDirected ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified
                });
                MobileAds.SetiOSAppPauseOnBackground(true);
                MobileAds.Initialize(status => OnMainThread(() => {
                    if (generation != initGeneration) return;
                    initializing = false;
                    initialized = status != null;
                    if (!initialized) retryInitAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
                }));
            }
            catch (Exception error)
            {
                initializing = false;
                retryInitAt = now + RetrySeconds;
                Debug.LogWarning("Ad initialization: " + error.Message);
            }
        }

        bool TryShow(Action done)
        {
            if (!Eligible || rewardShowing || ad == null || Application.internetReachability == NetworkReachability.NotReachable) return false;
            try
            {
                if (!ad.CanShowAd()) return false;
                afterShow = done;
                UpdateClock();
                callbackDeadline = Time.realtimeSinceStartupAsDouble + RetrySeconds;
                ad.Show();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogWarning("Round ad display: " + error.Message);
                retryLoadAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
                EndAd();
                return false;
            }
        }

        void RecordAdInfo(ResponseInfo info)
        {
            LastDisplayedAd = L10n.T("표시 시각: ") + DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'") +
                L10n.T("\n광고 네트워크: ") + (info?.GetMediationAdapterClassName() ?? L10n.T("정보 없음")) +
                L10n.T("\n광고 응답 ID: ") + (info?.GetResponseId() ?? L10n.T("정보 없음"));
        }

        void EndAd()
        {
            loadGeneration++;
            loading = false;
            var finished = afterShow;
            afterShow = null;
            displaying = false;
            UpdateClock();
            var old = ad;
            ad = null;
            if (old != null)
            {
                try { old.Destroy(); }
                catch (Exception error) { Debug.LogWarning("Round ad cleanup: " + error.Message); }
            }
            finished?.Invoke();
        }

        public void SetWorkshopOpen(bool open)
        {
            workshopOpen = open;
            if (!open && !rewardShowing) ReleaseReward();
        }

        void PrepareReward()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (!AdsPermitted && !rewardShowing)
            {
                if (rewardAd != null || rewardLoading) ReleaseReward();
                return;
            }
            if (rewardAd != null || rewardLoading)
            {
                if (RewardExpired(now))
                {
                    int expiredGeneration = rewardLoadGeneration;
                    OnMainThread(() => {
                        if (this != null && isActiveAndEnabled && expiredGeneration == rewardLoadGeneration
                            && RewardExpired(Time.realtimeSinceStartupAsDouble)) RewardFailed();
                    });
                }
                return;
            }
            if (!workshopOpen || !AdsPermitted || !RewardConfigured || KnifeProgress.Unlocked || store?.PhotoKnife == true)
                return;
            if (!initialized || now < rewardRetryAt || Application.internetReachability == NetworkReachability.NotReachable) return;
            int generation = ++rewardLoadGeneration;
            rewardLoading = true;
            rewardDeadline = now + RetrySeconds;
            try
            {
                RewardedAd.Load(platform.rewardedAdUnitId, new AdRequest(), (unit, error) => OnMainThread(() => {
                    if (this == null || !isActiveAndEnabled || generation != rewardLoadGeneration || !AdsPermitted || !workshopOpen)
                    {
                        unit?.Destroy();
                        return;
                    }
                    rewardLoading = false;
                    if (error != null || unit == null)
                    {
                        unit?.Destroy();
                        RewardFailed();
                        return;
                    }
                    rewardAd = unit;
                    rewardReady = true;
                    rewardLoadedAt = Time.realtimeSinceStartupAsDouble;
                    unit.OnAdFullScreenContentFailed += failure => OnMainThread(() => { if (this != null && unit == rewardAd) RewardFailed(); });
                    unit.OnAdFullScreenContentOpened += () => OnMainThread(() => {
                        if (this == null || unit != rewardAd || !rewardShowing || rewardDisplayed) return;
                        rewardDisplayed = true;
                        UpdateClock();
                        RecordAdInfo(unit.GetResponseInfo());
                        RecordImpression();
                    });
                    unit.OnAdFullScreenContentClosed += () => OnMainThread(() => {
                        if (this != null && unit == rewardAd) ForgetCurrentReward();
                        RetireReward(unit, RetrySeconds);
                    });
                }));
            }
            catch (Exception error)
            {
                Debug.LogWarning("Workshop ad preparation: " + error.Message);
                RewardFailed();
            }
        }

        bool RewardExpired(double now) => (!rewardReady && now >= rewardDeadline && Application.isFocused)
            || (rewardReady && now - rewardLoadedAt >= AdLifetimeSeconds);

        public void ShowReward()
        {
            if (!workshopOpen || !RewardReady || displaying || afterShow != null || KnifeProgress.Unlocked) return;
            try
            {
                rewardReady = false;
                rewardShowing = true;
                UpdateClock();
                rewardDeadline = Time.realtimeSinceStartupAsDouble + 180;
                var grant = KnifeProgress.ForOneShowing();
                // The SDK's configured amount is not our progress: one completed showing is one credit.
                rewardAd.Show(reward => OnMainThread(() => grant()));
            }
            catch (Exception error)
            {
                Debug.LogWarning("Workshop ad display: " + error.Message);
                RewardFailed();
            }
        }

        static void RetireReward(RewardedAd unit, double delay)
        {
            if (unit == null || !pendingDisposal.Add(unit)) return;
            // The executor survives scene changes, so late rewards can arrive before destruction.
            MobileAdsEventExecutor.Initialize();
            MobileAdsEventExecutor.instance.StartCoroutine(DisposeAfterCallbacks(unit, delay));
        }

        static IEnumerator DisposeAfterCallbacks(RewardedAd unit, double delay)
        {
            yield return new WaitForSecondsRealtime((float)delay);
            if (!pendingDisposal.Remove(unit)) yield break;
            try { unit.Destroy(); }
            catch (Exception error) { Debug.LogWarning("Workshop ad cleanup: " + error.Message); }
        }

        void ForgetCurrentReward()
        {
            rewardLoadGeneration++;
            rewardAd = null;
            rewardLoading = rewardReady = rewardShowing = rewardDisplayed = false;
            UpdateClock();
        }

        void ReleaseReward(bool preserveShowing = false)
        {
            var unit = rewardAd;
            bool wasShowing = rewardShowing;
            ForgetCurrentReward();
            if (unit == null) return;
            if (preserveShowing && wasShowing) RetireReward(unit, 180);
            else
            {
                try { unit.Destroy(); }
                catch (Exception error) { Debug.LogWarning("Workshop ad cleanup: " + error.Message); }
            }
        }

        void RewardFailed()
        {
            rewardRetryAt = Time.realtimeSinceStartupAsDouble + RetrySeconds;
            ReleaseReward(true);
        }

        void OnDisable()
        {
            EndAd();
            ReleaseReward(true);
            foregroundPlay.Stop();
            SaveCooldown();
        }
    }
}
