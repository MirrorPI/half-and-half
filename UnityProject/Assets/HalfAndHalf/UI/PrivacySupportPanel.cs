using HalfAndHalf.Localization;
using System;
using System.Net.Mail;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    public sealed class PrivacySupportPanel : MonoBehaviour
    {
        [Serializable] sealed class Settings
        {
            public string operatorName = "", supportEmail = "", privacyUrl = "", supportUrl = "";
        }
        [Serializable] sealed class Policy
        {
            public string revised = "";
            public string adProviderPrivacyUrl = "", adProviderPrivacyName = "";
            public Section[] sections = Array.Empty<Section>();
        }
        [Serializable] sealed class Section
        {
            public string title = "", body = "";
        }
        enum Page { Privacy, Help, AdReport }

        static readonly Color Ink = new Color32(39, 40, 58, 255);
        static readonly Color Paper = new Color32(255, 245, 223, 255);
        static readonly Color Accent = new Color32(255, 189, 99, 255);
        BladeWorkshop workshop;
        Settings settings;
        Policy policy;
        Button entry, opener, close, primary, secondary;
        Button[] tabs;
        RectTransform panel;
        TMP_Text body, status;
        ScrollRect scroll;
        Page current;
        bool workshopWasOpen;

        public static void Build(BladeWorkshop workshop)
        {
            if (workshop.GetComponent<PrivacySupportPanel>() != null) return;
            var view = workshop.gameObject.AddComponent<PrivacySupportPanel>();
            view.Initialize(workshop);
        }

        void Initialize(BladeWorkshop owner)
        {
            workshop = owner;
            settings = Load<Settings>("PlayerSupport") ?? new Settings();
            policy = Load<Policy>("PlayerPrivacy");
            var workshopTitle = workshop.Panel.transform.Find("Title") as RectTransform;
            if (workshopTitle != null) workshopTitle.anchorMax = new Vector2(.49f, workshopTitle.anchorMax.y);

            entry = Key(workshop.Panel.transform, "PrivacySupportButton", "개인정보·지원", .49f, .894f, .775f, .955f,
                () => Open(entry));
            panel = Rect(workshop.Panel.transform.parent, "PrivacySupportPanel", 0, 0, 1, 1);
            panel.gameObject.SetActive(false);
            panel.gameObject.AddComponent<Image>().color = Ink;
            Label(panel, "Title", "개인정보·지원", 60, .055f, .886f, .77f, .969f);
            close = Key(panel, "Close", "닫기", .79f, .894f, .948f, .955f, Close);
            tabs = new[] {
                Key(panel, "Privacy", "개인정보", .055f, .783f, .335f, .857f, () => Show(Page.Privacy)),
                Key(panel, "Help", "고객지원", .36f, .783f, .64f, .857f, () => Show(Page.Help)),
                Key(panel, "Report", "광고 신고", .665f, .783f, .945f, .857f, () => Show(Page.AdReport))
            };
            var viewport = Rect(panel, "PolicyViewport", .06f, .213f, .94f, .753f);
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            body = Label(viewport, "Body", "", 36, 0, 1, 1, 1);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.rectTransform.pivot = new Vector2(0, 1);
            body.lineSpacing = 8;
            body.textWrappingMode=TextWrappingModes.Normal;
            body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = body.rectTransform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50;
            status = Label(panel, "Status", "", 30, .06f, .124f, .945f, .20f);
            primary = Key(panel, "Primary", "", .055f, .038f, .49f, .117f, Primary);
            secondary = Key(panel, "Secondary", "", .51f, .038f, .945f, .117f, Secondary);
        }

        static T Load<T>(string key) where T : class
        {
            var asset = Resources.Load<TextAsset>(key);
            if (asset == null) return null;
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (ArgumentException) { return null; }
        }

        static RectTransform Rect(Transform parent, string name, float x, float y, float right, float top)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetBounds(rect, x, y, right, top);
            return rect;
        }

        static void SetBounds(RectTransform rect, float x, float y, float right, float top)
        {
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        TMP_Text Label(Transform parent, string name, string text, int size, float x, float y, float right, float top)
        {
            var label = Rect(parent, name, x, y, right, top).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = workshop.PrivacyCaption.font;
            label.color = Paper;
            label.richText = false;
            label.raycastTarget = false;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            L10n.Bind(label,text);

            return label;
        }

        Button Key(Transform parent, string name, string caption, float x, float y, float right, float top, UnityAction action)
        {
            var button = Instantiate(workshop.CloseButton, parent, false);
            button.name = name;
            SetBounds((RectTransform)button.transform, x, y, right, top);
            L10n.Bind(button.GetComponentInChildren<TMP_Text>(),caption);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
            return button;
        }

        void Open(Button source)
        {
            opener = source;
            workshopWasOpen = workshop.Panel.activeSelf;
            workshop.Panel.SetActive(false);
            panel.SetAsLastSibling();
            panel.gameObject.SetActive(true);
            Show(Page.Privacy);
            close.Select();
        }

        void Close()
        {
            panel.gameObject.SetActive(false);
            workshop.Panel.SetActive(workshopWasOpen);
            opener.Select();
        }

        string ReportText()
        {
            var impression = workshop.Ads.LastDisplayedAd;
            return L10n.T("앱 버전: ") + Application.version + " (" + Application.platform + ")\n" +
                (string.IsNullOrWhiteSpace(impression) ? L10n.T("이 실행에서 확인된 광고 정보가 없습니다.") : impression) +
                L10n.T("\n\n신고 사유 및 광고 내용:\n");
        }

        string PrivacyText()
        {
            var text = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(settings.operatorName)) text.AppendLine(L10n.T("운영자  ") + settings.operatorName);
            if (policy?.sections == null || policy.sections.Length == 0)
                return L10n.T("기기 내 개인정보 처리방침을 열 수 없습니다. 공개 방침이나 고객지원을 이용해 주세요.");
            text.AppendLine(L10n.T("개인정보 처리방침  ·  ") + policy.revised);
            foreach (var item in policy.sections)
                text.Append("\n").AppendLine(L10n.T(item.title)).AppendLine(L10n.T(item.body));
            if (Email != "") text.Append(L10n.T("\n개인정보 문의  ")).Append(Email);
            return text.ToString().TrimEnd();
        }

        string Email
        {
            get
            {
                if (string.IsNullOrWhiteSpace(settings.supportEmail)) return "";
                var email = settings.supportEmail.Trim();
                try { return new MailAddress(email).Address == email && !email.Contains("?") && !email.Contains("#") ? email : ""; }
                catch (FormatException) { return ""; }
            }
        }

        static bool Https(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 && string.IsNullOrEmpty(uri.UserInfo);

        void Show(Page page)
        {
            current = page;
            for (int i = 0; i < tabs.Length; i++)
                tabs[i].GetComponentInChildren<TMP_Text>().color = i == (int)page ? Accent : Paper;
            bool supportPage = Https(settings.supportUrl);
            bool hasEmail = Email != "";
            status.text = "";
            switch (page)
            {
                case Page.Privacy:
                    body.text = PrivacyText();
                    status.text = L10n.T("위아래로 밀어 방침 전체를 읽을 수 있어요.");
                    ActionButton(primary, L10n.T("공개 처리방침"), Https(settings.privacyUrl));
                    ActionButton(secondary, L10n.T(policy?.adProviderPrivacyName ?? "광고 개인정보 안내"), Https(policy?.adProviderPrivacyUrl));
                    break;
                case Page.Help:
                    body.text = L10n.T("게임 이용 및 개인정보 문의\n\n공방에서 ‘광고 보기’를 선택해 총 5편을 끝까지 보면 커스텀 칼이 열려요. 시청 횟수와 해금 상태는 이 기기에 저장돼요. 사진은 ‘기본 칼’로 바꾸면 삭제돼요. 지역에 따라 공방에 ‘Google 개인정보 선택’이 표시되며, 여기서 개인정보 선택을 변경할 수 있어요.\n\n개인정보 열람·삭제 요청이나 게임 오류는 앱 버전과 문제 상황을 알려주세요. 비밀번호와 결제 수단 정보는 보내지 마세요.\n\n") + ContactText();
                    ActionButton(primary, L10n.T("이메일 문의"), hasEmail);
                    ActionButton(secondary, L10n.T("지원 페이지"), supportPage);
                    break;
                case Page.AdReport:
                    body.text = L10n.T("부적절한 광고 신고\n\n광고 내용과 신고 이유를 적어 주세요. 아래 정보는 메일 초안이나 복사본에 포함돼요. 스크린샷은 원할 때만 직접 첨부하세요.\n\n") +
                        ReportText() + "\n" + ContactText();
                    ActionButton(primary, hasEmail ? L10n.T("신고 메일 작성") : L10n.T("지원 페이지"), hasEmail || supportPage);
                    ActionButton(secondary, L10n.T("신고 양식 복사"), true);
                    break;
            }
            Canvas.ForceUpdateCanvases();
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
        }

        string ContactText()
        {
            if (Email != "") return L10n.T("문의 및 신고 접수  ") + Email;
            if (Https(settings.supportUrl)) return L10n.T("지원 페이지에서 문의하거나 복사한 신고 내용을 전달해 주세요.");
            return L10n.T("이 빌드에는 운영자 문의처가 등록되지 않았어요. 광고에 신고 메뉴가 표시되면 해당 메뉴를 이용할 수 있어요.");
        }

        static void ActionButton(Button button, string label, bool visible)
        {
            button.gameObject.SetActive(visible);
            button.GetComponentInChildren<TMP_Text>(true).text = label;
        }

        void Primary()
        {
            if (current == Page.Privacy) { if (Https(settings.privacyUrl)) Application.OpenURL(settings.privacyUrl); }
            else if (Email != "")
            {
                bool report = current == Page.AdReport;
                string subject = report ? L10n.T("Half&Half 부적절한 광고 신고") : L10n.T("Half&Half 고객지원 문의");
                string draft = report ? ReportText() : L10n.T("앱 버전: ") + Application.version + L10n.T("\n문의 내용:\n");
                Application.OpenURL("mailto:" + Uri.EscapeDataString(Email).Replace("%40", "@") +
                    "?subject=" + Uri.EscapeDataString(subject) + "&body=" + Uri.EscapeDataString(draft));
                status.text = L10n.T("메일 앱에서 내용을 확인한 뒤 보내주세요.");
            }
            else if (Https(settings.supportUrl)) Application.OpenURL(settings.supportUrl);
        }

        void Secondary()
        {
            if (current == Page.Privacy) { if (Https(policy?.adProviderPrivacyUrl)) Application.OpenURL(policy.adProviderPrivacyUrl); }
            else if (current == Page.AdReport)
            {
                GUIUtility.systemCopyBuffer = L10n.T("Half&Half 부적절한 광고 신고\n") + ReportText();
                status.text = L10n.T("신고 양식을 복사했어요. 문의에 붙여넣어 주세요.");
            }
            else if (Https(settings.supportUrl)) Application.OpenURL(settings.supportUrl);
        }

    }
}
