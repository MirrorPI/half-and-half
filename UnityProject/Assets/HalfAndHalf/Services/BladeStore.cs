using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace HalfAndHalf.Services
{
    // The workshop is ad-supported. Read only previously purchased rights.
    public sealed class BladeStore : MonoBehaviour
    {
        const string BundleId = "starter_pack";
        const string NoAdsId = "no_ads";
        const string PhotoKnifeId = "photo_knife";
        readonly HashSet<string> ownedProducts = new HashSet<string>();

        public bool NoAds => ownedProducts.Contains(BundleId) || ownedProducts.Contains(NoAdsId);
        public bool PhotoKnife => ownedProducts.Contains(BundleId) || ownedProducts.Contains(PhotoKnifeId);
        public bool EntitlementsKnown { get; private set; }

        void OnEnable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            RefreshOwnership();
#else
#if UNITY_ANDROID && !UNITY_EDITOR
            foreach (string id in new[] { BundleId, NoAdsId, PhotoKnifeId })
                SetOwnership(id, AndroidStoreOwnership.Read(id));
#endif
            EntitlementsKnown = true;
#endif
        }

        void SetOwnership(string id, bool owned)
        {
            if (owned) ownedProducts.Add(id);
            else ownedProducts.Remove(id);
        }

#if UNITY_IOS && !UNITY_EDITOR
        static BladeStore active;
        static int latestRequest;
        bool querying;
        delegate void OwnershipResult(int request, int rights, int verified);
        static readonly OwnershipResult callback = ReceiveOwnership;

        [DllImport("__Internal")]
        static extern void HHReadLegacyEntitlements(int request, OwnershipResult result);

        void RefreshOwnership()
        {
            active = this;
            querying = true;
            EntitlementsKnown = false;
            HHReadLegacyEntitlements(++latestRequest, callback);
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && isActiveAndEnabled && !querying) RefreshOwnership();
        }

        void OnDisable()
        {
            if (active == this) active = null;
        }

        [AOT.MonoPInvokeCallback(typeof(OwnershipResult))]
        static void ReceiveOwnership(int request, int rights, int verified)
        {
            if (request != latestRequest || active == null) return;
            active.querying = false;
            active.SetOwnership(BundleId, (rights & 1) != 0);
            active.SetOwnership(NoAdsId, (rights & 2) != 0);
            active.SetOwnership(PhotoKnifeId, (rights & 4) != 0);
            active.EntitlementsKnown = verified != 0;
        }
#endif
    }
}
