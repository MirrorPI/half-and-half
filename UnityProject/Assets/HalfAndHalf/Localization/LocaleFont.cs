using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace HalfAndHalf.Localization
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocaleFont : MonoBehaviour
    {
        public bool Body;
        public LocalizedTmpFont Font = new LocalizedTmpFont { TableReference="GameFonts", TableEntryReference="ui.main" };
        TMP_Text text;
        void Awake() { text=GetComponent<TMP_Text>(); }
        void OnEnable() { Font.TableEntryReference=Body?"ui.body":"ui.main";Font.AssetChanged+=Apply; }
        void OnDisable() { Font.AssetChanged-=Apply; }
        void Apply(TMP_FontAsset font)
        {
            if(text==null)text=GetComponent<TMP_Text>();
            if(font!=null&&text!=null)text.font=font;
        }
    }
}
