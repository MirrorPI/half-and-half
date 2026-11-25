using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

namespace HalfAndHalf.Localization
{
    public static class L10n
    {
        public const string Table = "GameText";
        public const string Preference = "HalfAndHalf.Settings.Language.v1";
        [Serializable] public sealed class Entry { public string key, source, ko, en, de, ja; }
        [Serializable] public sealed class Catalog { public Entry[] entries; }
        static Dictionary<string,string> keys;
        static readonly Dictionary<string,string> cache = new Dictionary<string,string>();
        public static event Action Changed;
        public static string Code => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { keys=null;cache.Clear();Changed=null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
            LocalizationSettings.InitializationOperation.WaitForCompletion();
        }

        static void LocaleChanged(Locale locale) { cache.Clear();Changed?.Invoke(); }

        static void LoadKeys()
        {
            if(keys!=null)return;
            keys=new Dictionary<string,string>(StringComparer.Ordinal);
            var asset=Resources.Load<TextAsset>("GameTextCatalog");
            if(asset==null)return;
            var catalog=JsonUtility.FromJson<Catalog>(asset.text);
            foreach(var entry in catalog.entries)
                if(!string.IsNullOrEmpty(entry.source))keys[entry.source]=entry.key;
        }

        public static string Key(string source)
        {
            LoadKeys();
            return source!=null&&keys.TryGetValue(source,out var key)?key:null;
        }

        public static string T(string source)
        {
            if(string.IsNullOrEmpty(source))return source??"";
            if(cache.TryGetValue(source,out var translated))return translated;
            var key=Key(source);
            if(key==null)return source;
            translated=LocalizationSettings.StringDatabase.GetLocalizedString(Table,key);
            if(string.IsNullOrEmpty(translated))translated=source;
            cache[source]=translated;
            return translated;
        }

        public static string F(string source,params object[] arguments) => string.Format(CultureInfo.InvariantCulture,T(source),arguments);

        public static void Select(string code)
        {
            var locale=LocalizationSettings.AvailableLocales.GetLocale(code);
            if(locale==null)return;
            PlayerPrefs.SetString(Preference,code);
            PlayerPrefs.Save();
            LocalizationSettings.SelectedLocale=locale;
        }

        // Runtime-created captions use the same table bindings as authored labels.
        public static void Bind(TMP_Text text,string source)
        {
            if(text.GetComponent<LocaleFont>()==null){var font=text.gameObject.AddComponent<LocaleFont>();font.Body=text.font!=null&&!text.font.name.Contains("Galmuri");font.Font.TableEntryReference=font.Body?"ui.body":"ui.main";}
            var binding=text.GetComponent<LocalizeStringEvent>();
            var key=Key(source);
            if(key==null)
            {
                if(binding!=null)binding.enabled=false;
                text.text=T(source);
                return;
            }
            if(binding==null)
            {
                binding=text.gameObject.AddComponent<LocalizeStringEvent>();
                binding.OnUpdateString.AddListener(value=>text.text=value);
            }
            binding.StringReference.SetReference(Table,key);
            binding.enabled=true;
            binding.RefreshString();
        }
    }

    [Serializable]
    public sealed class SavedLanguageSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales)
        {
            var saved=PlayerPrefs.GetString(L10n.Preference,"");
            return string.IsNullOrEmpty(saved)?null:availableLocales.GetLocale(saved);
        }
    }
}
