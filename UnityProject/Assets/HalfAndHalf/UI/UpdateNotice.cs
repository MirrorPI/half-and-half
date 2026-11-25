using System.Collections;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Localization;
using HalfAndHalf.Services;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    [DisallowMultipleComponent]
    public sealed class UpdateNotice : MonoBehaviour
    {
        static bool checkedThisLaunch;
        AppleGame game;
        GameObject backdrop, previousSelection;
        Button later;
        static readonly Color Ink = new Color32(39, 40, 58, 255);
        static readonly Color Paper = new Color32(255, 245, 223, 255);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { checkedThisLaunch = false; }
        void Awake() { game = GetComponent<AppleGame>(); }
        IEnumerator Start()
        {
            if (Application.platform != RuntimePlatform.IPhonePlayer || checkedThisLaunch) yield break;
            checkedThisLaunch = true;
            string country = L10n.Code switch { "ko" => "kr", "ja" => "jp", "de" => "de", _ => "us" };
            using var request = UnityWebRequest.Get("https://itunes.apple.com/lookup?id=" + AppStoreUpdate.AppId + "&country=" + country);
            request.timeout = 8;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success ||
                AppStoreUpdate.AvailableVersion(request.downloadHandler.text, Application.version) == null) yield break;
            while (game != null && (game.UiOpen || game.OverlayOpen || !Application.isFocused ||
                (game.Workshop != null && game.Workshop.Ads != null &&
                 (game.Workshop.Ads.PrivacyBusy || game.Workshop.Ads.RewardBusy)))) yield return null;
            if (game != null) Show();
        }

        public void Show()
        {
            if (game == null || game.UiOpen) return;
            if (backdrop == null && !Build()) return;
            if (backdrop.activeSelf) return;
            previousSelection = EventSystem.current?.currentSelectedGameObject;
            game.SetUpdateNoticeOpen(true);
            backdrop.SetActive(true);
            backdrop.transform.SetAsLastSibling();
            later.Select();
        }
        public void Dismiss()
        {
            if (backdrop == null || !backdrop.activeSelf) return;
            backdrop.SetActive(false);
            if (game != null) game.SetUpdateNoticeOpen(false);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        }
        void OpenStore()
        {
            Dismiss();
            Application.OpenURL(AppStoreUpdate.StoreUrl);
        }
        void OnDestroy()
        {
            Dismiss();
            if (backdrop != null) Destroy(backdrop);
        }

        bool Build()
        {
            var canvas = game.Hud != null ? game.Hud.GetComponentInParent<Canvas>() : null;
            var template = game.GetComponent<GameSettings>()?.CloseButton;
            if (canvas == null || template == null) return false;
            backdrop = new GameObject("UpdateNotice", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvas.transform, false);
            Place((RectTransform)backdrop.transform, 0, 0, 1, 1);
            backdrop.GetComponent<Image>().color = new Color(0, 0, 0, .72f);
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(backdrop.transform, false);
            Place((RectTransform)card.transform, .06f, .31f, .94f, .69f);
            card.GetComponent<Image>().color = Paper;
            var font = template.GetComponentInChildren<TMP_Text>(true).font;
            void Label(string name, string source, float bottom, float top, float size)
            {
                var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                label.transform.SetParent(card.transform, false);
                Place(label.rectTransform, .075f, bottom, .925f, top);
                label.font = font; label.fontSize = size; label.fontSizeMax = size; label.fontSizeMin = size * .75f;
                label.enableAutoSizing = true; label.color = Ink; label.alignment = TextAlignmentOptions.Center;
                label.richText = false; label.raycastTarget = false;
                L10n.Bind(label, source);
            }
            Label("Title", "새 업데이트가 있어요", .72f, .92f, 47);
            Label("Body", "더 나은 플레이를 위해 최신 버전으로 업데이트해 주세요.", .37f, .69f, 34);
            Button Action(string name, string source, float left, float right, UnityEngine.Events.UnityAction action)
            {
                var button = Instantiate(template, card.transform, false);
                button.name = name;
                Place((RectTransform)button.transform, left, .09f, right, .29f);
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(action);
                var text = button.GetComponentInChildren<TMP_Text>(true);
                text.fontSize = text.fontSizeMax = 36; text.fontSizeMin = 25; text.enableAutoSizing = true;
                L10n.Bind(text, source);
                button.gameObject.SetActive(true);
                return button;
            }
            later = Action("Later", "나중에", .075f, .475f, Dismiss);
            var update = Action("Update", "업데이트", .525f, .925f, OpenStore);
            later.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = update };
            update.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = later };
            backdrop.SetActive(false);
            return true;
        }
        static void Place(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
