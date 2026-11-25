using HalfAndHalf.Localization;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Presentation;
using UnityEngine;
using TMPro;

namespace HalfAndHalf.UI
{
    public sealed class GameSettings : MonoBehaviour
    {
        public AppleGame Game;
        public CutAudio Audio;
        public GameObject Panel;
        public UnityEngine.UI.Button OpenButton, CloseButton;
        public UnityEngine.UI.Slider Effects, Music;
        public UnityEngine.UI.Toggle Vibration;
        public TMP_Text EffectsValue, MusicValue;
        const string Prefix = "HalfAndHalf.Settings.";
        void Start()
        {
            AddStorageNotice();
            AddLanguageControls();
            L10n.Changed+=RefreshLanguage;
            Effects.SetValueWithoutNotify(PlayerPrefs.GetFloat(Prefix + "Effects", .8f));
            Music.SetValueWithoutNotify(PlayerPrefs.GetFloat(Prefix + "Music", .5f));
            Vibration.SetIsOnWithoutNotify(PlayerPrefs.GetInt(Prefix + "Vibration", 1) == 1);
            Apply(); Panel.SetActive(false);
            Effects.onValueChanged.AddListener(_ => Save()); Music.onValueChanged.AddListener(_ => Save());
            Vibration.onValueChanged.AddListener(_ => Save());
            OpenButton.onClick.AddListener(() => Game.AfterResult(() => { if(Game.UiOpen)return;Game.UiOpen = true; Game.SetInterrupted(); Panel.SetActive(true);Panel.transform.SetAsLastSibling(); }));
            CloseButton.onClick.AddListener(() => { Panel.SetActive(false); Game.UiOpen = false; Game.Retry(); });
        }
        void AddStorageNotice()
        {
            if(Panel.transform.Find("StorageNotice")!=null)return;
            var panel=(RectTransform)Panel.transform;
            if(Game.Hud.Arcade==null){
            panel.anchorMin=new Vector2(panel.anchorMin.x,.15f);panel.anchorMax=new Vector2(panel.anchorMax.x,.85f);
            foreach(RectTransform child in panel)
            {child.anchorMin=new Vector2(child.anchorMin.x,.26f+.74f*child.anchorMin.y);child.anchorMax=new Vector2(child.anchorMax.x,.26f+.74f*child.anchorMax.y);}
            }
            var label=new GameObject("StorageNotice",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(panel,false);label.rectTransform.anchorMin=new Vector2(.06f,.025f);label.rectTransform.anchorMax=new Vector2(.94f,.23f);
            label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
            var template=Game.GetComponent<RankingPanel>()?.Status??Panel.GetComponentInChildren<TMP_Text>(true);
            label.font=template.font;label.fontSize=30;label.color=template.color;label.alignment=TextAlignmentOptions.Center;label.richText=false;label.raycastTarget=false;
            if(Game.Hud.Arcade!=null){
                label.rectTransform.anchorMin=new Vector2(.09f,.135f);label.rectTransform.anchorMax=new Vector2(.91f,.23f);
                label.color=Game.Hud.Arcade.Paper;label.fontSize=26;label.enableAutoSizing=true;label.fontSizeMin=21;label.fontSizeMax=26;
            }
            L10n.Bind(label,"진행은 이 기기에 저장되며 기기 간 동기화와 삭제 후 자동 복원을 지원하지 않습니다.\n온라인 순위 기록으로는 진행·해금을 복원할 수 없습니다.");

        }
        readonly string[] languageCodes={"ko","en","de","ja"};
        readonly string[] languageNames={"한국어","English","Deutsch","日本語"};
        UnityEngine.UI.Button[] languageButtons;
        void AddLanguageControls()
        {
            void Place(Transform target,float bottom,float top)
            {
                var rect=(RectTransform)target;
                rect.anchorMin=new Vector2(rect.anchorMin.x,bottom);
                rect.anchorMax=new Vector2(rect.anchorMax.x,top);
            }
            Place(Panel.transform.Find("EffectsLabel"),.71f,.79f);Place(EffectsValue.transform,.71f,.79f);Place(Effects.transform,.655f,.71f);
            Place(Panel.transform.Find("MusicLabel"),.55f,.63f);Place(MusicValue.transform,.55f,.63f);Place(Music.transform,.495f,.55f);
            Place(Vibration.transform,.40f,.47f);
            var title=Instantiate(Panel.transform.Find("MusicLabel").GetComponent<TMP_Text>(),Panel.transform,false);
            title.name="LanguageLabel";Place(title.transform,.335f,.385f);title.fontSize=30;title.fontSizeMax=30;
            L10n.Bind(title,"언어");
            languageButtons=new UnityEngine.UI.Button[4];
            for(int i=0;i<languageButtons.Length;i++)
            {
                int index=i;
                var button=Instantiate(CloseButton,Panel.transform,false);
                button.name="Language_"+languageCodes[i];
                var rect=(RectTransform)button.transform;
                rect.anchorMin=new Vector2(.085f+i*.21f,.25f);rect.anchorMax=new Vector2(.285f+i*.21f,.328f);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
                var caption=button.GetComponentInChildren<TMP_Text>(true);
                L10n.Bind(caption,languageNames[i]);
                caption.fontSize=30;caption.enableAutoSizing=true;caption.fontSizeMin=22;caption.fontSizeMax=30;
                button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(()=>L10n.Select(languageCodes[index]));
                languageButtons[i]=button;
            }
            RefreshLanguage();
        }
        void RefreshLanguage()
        {
            if(languageButtons!=null)for(int i=0;i<languageButtons.Length;i++)languageButtons[i].interactable=L10n.Code!=languageCodes[i];
            if(Game.Hud.Arcade!=null&&Game.Hud.Arcade.Collection.Progress!=null)Game.Hud.Arcade.RefreshLanguage();
        }
        void OnDestroy(){L10n.Changed-=RefreshLanguage;}
        void Apply()
        {
            Audio.ApplySettings(Effects.value, Music.value, Vibration.isOn);
            if(EffectsValue!=null)EffectsValue.text=Mathf.RoundToInt(Effects.value*100)+"%";
            if(MusicValue!=null)MusicValue.text=Mathf.RoundToInt(Music.value*100)+"%";
        }
        void Save()
        {
            Apply(); PlayerPrefs.SetFloat(Prefix + "Effects", Effects.value);
            PlayerPrefs.SetFloat(Prefix + "Music", Music.value); PlayerPrefs.SetInt(Prefix + "Vibration", Vibration.isOn ? 1 : 0); PlayerPrefs.Save();
        }
    }
}
