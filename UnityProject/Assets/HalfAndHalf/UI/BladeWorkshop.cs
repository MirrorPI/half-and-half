using HalfAndHalf.Localization;
using System;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    public sealed class BladeWorkshop : MonoBehaviour
    {
        public AppleGame Game;
        public MvpCollection Collection;
        public PhotoBlade Photos;
        public BladeStore Store;
        public RoundAds Ads;
        // Legacy consent references remain serialized for existing scenes; the panel is never shown.
        public GameObject Panel, PrivacyPanel, DefaultPreview;
        public Button EditButton;
        public RawImage Preview;
        public AspectRatioFitter PreviewFit;
        public Button OpenButton, CloseButton, PickButton, EquipButton, DefaultButton, RemoveAdsButton,
            RestoreButton, PrivacyButton, AllowButton, DenyButton;
        public TMP_Text EquipCaption, AdsCaption, PhotoStatus, StoreStatus, PrivacyCaption, BundleDetail;
        Action nextRound;
        bool transitioning;
        ColorBlock primaryButton, secondaryButton;
        Color primaryText, secondaryText;
        TMP_Text pickCaption;

        TMP_Text progressCaption, sectionTitle, detail;
        GameObject photoPrivacy;

        bool CanUsePhotoKnife() => Store.PhotoKnife || Ads.KnifeProgress?.Unlocked == true;

        static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void ConfigureUnlockPanel()
        {
            sectionTitle = Panel.transform.Find("PhotoTitle").GetComponent<TMP_Text>();
            detail = Panel.transform.Find("PhotoDetail").GetComponent<TMP_Text>();
            photoPrivacy = Panel.transform.Find("PhotoPrivacy").gameObject;
            Panel.transform.Find("Rule2").gameObject.SetActive(false);
            Panel.transform.Find("AdTitle").gameObject.SetActive(false);
            RestoreButton.gameObject.SetActive(false);
            Place((RectTransform)PrivacyButton.transform, .056f, .024f, .944f, .086f);
            Place((RectTransform)RemoveAdsButton.transform, .056f, .398f, .944f, .485f);
            Place(AdsCaption.rectTransform, .13f, .05f, .81f, .95f);
            AdsCaption.text = L10n.T("광고 보기");
            progressCaption = Instantiate(AdsCaption, AdsCaption.transform.parent, false);
            progressCaption.name = "RewardProgress";
            Place(progressCaption.rectTransform, .80f, .08f, .98f, .92f);
            var badge = new GameObject("VideoIcon", typeof(RectTransform));
            badge.transform.SetParent(RemoveAdsButton.transform, false);
            Place((RectTransform)badge.transform, .035f, .19f, .135f, .81f);
            var iconObject = new GameObject("Glyph", typeof(RectTransform), typeof(RawImage));
            iconObject.transform.SetParent(badge.transform, false);
            var icon = iconObject.GetComponent<RawImage>();
            icon.texture = Resources.Load<Texture2D>("WorkshopAdIcon");
            var fit = iconObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = 4f / 3f;
            icon.color = primaryText;
            icon.raycastTarget = false;
            Place(BundleDetail.rectTransform, .058f, .315f, .944f, .393f);
            BundleDetail.textWrappingMode=TextWrappingModes.Normal;BundleDetail.fontSize=26;BundleDetail.enableAutoSizing=true;BundleDetail.fontSizeMin=23;BundleDetail.fontSizeMax=26;
            Place(StoreStatus.rectTransform, .058f, .233f, .944f, .310f);
            StoreStatus.textWrappingMode=TextWrappingModes.Normal;StoreStatus.enableAutoSizing=true;StoreStatus.fontSizeMin=23;StoreStatus.fontSizeMax=26;
            L10n.Bind(BundleDetail,"광고를 총 5편 끝까지 보면 내 사진으로 칼을 무제한 만들 수 있어요.\n해금은 이 기기에 저장돼요.");
            if (PrivacyPanel != null) PrivacyPanel.SetActive(false);
            L10n.Bind(PrivacyCaption,"Google 개인정보 선택");
        }

        void Awake()
        {
            pickCaption = PickButton.GetComponentInChildren<TMP_Text>();
            primaryButton = EquipButton.colors;
            secondaryButton = PickButton.colors;
            primaryText = EquipCaption.color;
            secondaryText = pickCaption.color;
            Photos.Appearance = Game.Presentation.KnifeAppearance;
            Photos.CanEquip = CanUsePhotoKnife;
            Ads.Configure(Store);
            Game.AttemptCompleted += CountRound;
            OpenButton.onClick.AddListener(()=>Game.AfterResult(OpenPanel));
            CloseButton.onClick.AddListener(Close);
            PickButton.onClick.AddListener(Photos.Pick);
            EquipButton.onClick.AddListener(Photos.Apply);
            EditButton.onClick.AddListener(GetComponent<PhotoCutoutEditor>().EditPending);
            DefaultButton.onClick.AddListener(Photos.ResetBlade);
            ConfigureUnlockPanel();
            RemoveAdsButton.onClick.AddListener(Ads.ShowReward);
            PrivacyButton.onClick.AddListener(() => Ads.ShowPrivacyOptions());
            PrivacySupportPanel.Build(this);
        }

        void OnDestroy() => Game.AttemptCompleted -= CountRound;

        void CountRound(AttemptResult result)
        {
            if (!MvpCollection.ModelReview && Collection.Progress != null &&
                Collection.Samples[Collection.SelectedIndex].ObjectId == result.Snapshot.ObjectId)
                Ads.CountCompletedRound();
        }

        void OpenPanel()
        {
            Game.UiOpen = true;
            Game.SetInterrupted();
            Panel.transform.SetAsLastSibling();
            Panel.SetActive(true);
            Ads.SetWorkshopOpen(true);
            Refresh();
        }

        void Close()
        {
            Ads.SetWorkshopOpen(false);
            Panel.SetActive(false);
            Game.UiOpen = false;
            Game.Retry();
        }

        public void ContinueFromResult(Action continuation)
        {
            if (transitioning) return;
            transitioning = true;
            Game.UiOpen = true;
            nextRound = continuation;
            Continue();
        }

        void Continue()
        {
            Ads.ContinueAfterAd(() => {
                var action = nextRound;
                nextRound = null;
                transitioning = false;
                Game.UiOpen = false;
                action?.Invoke();
            });
        }

        void Update()
        {
            if (Panel.activeSelf) Refresh();
        }

        void Refresh()
        {
            AdsCaption.text=L10n.T("광고 보기");
            bool canEquip = CanUsePhotoKnife();
            var photo = canEquip ? Photos.Preview : null;
            if (Preview.texture != photo) {
                Preview.texture = photo;
                if (photo != null) PreviewFit.aspectRatio = (float)photo.width / photo.height;
            }
            Preview.enabled = photo != null;
            DefaultPreview.SetActive(photo == null);
            sectionTitle.text = canEquip ? L10n.T("무제한 커스텀 칼") : L10n.T("커스텀 칼");
            detail.text = canEquip ? L10n.T("내 사진을 선택하고 원하는 모양으로 잘라 보세요.") : L10n.T("내 사진 속 사람이 칼이 되어 사과를 잘라요.");
            PickButton.gameObject.SetActive(canEquip);
            EquipButton.gameObject.SetActive(canEquip);
            EditButton.gameObject.SetActive(canEquip && Photos.HasPendingPhoto);
            DefaultButton.gameObject.SetActive(canEquip && Photos.HasEquippedPhoto);
            PhotoStatus.gameObject.SetActive(canEquip);
            PhotoStatus.text = Photos.Status == "기본 칼 장착 중" ? "" : L10n.T(Photos.Status);
            photoPrivacy.SetActive(canEquip);
            RemoveAdsButton.gameObject.SetActive(!canEquip);
            BundleDetail.gameObject.SetActive(!canEquip);
            StoreStatus.gameObject.SetActive(!canEquip);
            progressCaption.SetText("{0}/5", Ads.KnifeProgress.Count);
            RemoveAdsButton.interactable = Ads.RewardReady;
            if (!Ads.RewardConfigured) StoreStatus.text = L10n.T("현재 이용할 수 있는 보상형 광고가 없어요.");
            else if (Ads.PrivacyBusy) StoreStatus.text = L10n.T("광고 개인정보 설정을 확인하고 있어요.");
            else if (Ads.PrivacyChecked && !Ads.AdsRequestAllowed) StoreStatus.text = L10n.T("현재 개인정보 설정으로 광고를 표시할 수 없어요.");
            else if (Ads.RewardBusy) StoreStatus.text = "";
            else if (Ads.RewardUnavailable || Application.internetReachability == NetworkReachability.NotReachable)
                StoreStatus.text = L10n.T("지금 광고를 불러올 수 없어요. 잠시 후 다시 시도해 주세요.");
            else StoreStatus.text = Ads.RewardReady ? "" : L10n.T("광고를 준비하고 있어요.");
            bool readyToEquip = canEquip && Photos.HasPendingPhoto;
            EquipButton.interactable = readyToEquip;
            EquipCaption.color = readyToEquip ? primaryText : secondaryText;
            PickButton.colors = readyToEquip ? secondaryButton : primaryButton;
            pickCaption.color = readyToEquip ? secondaryText : primaryText;
            pickCaption.text = Photos.HasPendingPhoto ? L10n.T("다른 사진 선택") : L10n.T("사진 선택");
            PrivacyButton.gameObject.SetActive(Ads.PrivacyOptionsRequired);
            PrivacyButton.interactable = !Ads.PrivacyBusy && !Ads.RewardBusy;

        }
    }
}
