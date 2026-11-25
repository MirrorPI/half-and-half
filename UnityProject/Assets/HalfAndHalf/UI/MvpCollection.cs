using HalfAndHalf.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Content;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using TMPro;
using UnityEngine;

#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using HalfAndHalf.Verification;
#endif

namespace HalfAndHalf.UI
{
    public sealed class MvpCollection : MonoBehaviour
    {
        public AppleGame Game;
        public HalfAndHalf.Presentation.StageAtmosphere Atmosphere;
        public VisualSampleAsset[] Samples;
        [HideInInspector] public string[] LegacySelectionOrder;
        public TMP_Text Label,Hint,BestLabel,Outcome,ProgressLabel,RecoveryLabel,Difference;
        public GameObject Panel;
        public UnityEngine.UI.Button OpenButton,CloseButton,NextButton;
        public UnityEngine.UI.Button[] Items;
        public TMP_Text[] ItemLabels;
        public CollectionProgress Progress { get; private set; }
        public string SavePath { get; set; }
        public bool SavePending { get; private set; }
        public Func<string> RecordOwner;
        CutSnapshot ownedSnapshot;
        string cutOwner="";
        LocalProgressStore store;
        bool loaded;
        public static bool ModelReview
        {
            get
            {
#if DEVELOPMENT_BUILD && HALF_MODEL_REVIEW
                return true;
#else
                return false;
#endif
            }
        }
        int reviewSelected;
        public int SelectedIndex=>ModelReview?reviewSelected:Progress.Selected;
        void Awake()
        {
            if(!ModelReview)return;
            var online=GetComponent<GameCenterController>();if(online!=null)online.enabled=false;
            var ranking=GetComponent<RankingPanel>();
            if(ranking!=null){ranking.enabled=false;ranking.OpenButton.gameObject.SetActive(false);ranking.Panel.SetActive(false);}
        }
        void Start()
        {
            var korean=GetComponent<RankingPanel>()?.Status?.font;
            if(korean!=null){BestLabel.font=korean;RecoveryLabel.font=korean;Hint.font=korean;Game.Hud.Hint.font=korean;}
            OpenButton.onClick.AddListener(()=>Game.AfterResult(Open));CloseButton.onClick.AddListener(Close);
            NextButton.onClick.AddListener(()=>Game.AfterResult(()=>Select(SelectedIndex+1)));
            for(int i=0;i<Items.Length;i++){int index=i;Items[i].onClick.AddListener(()=>Select(index));}
            Game.AttemptCompleted+=Completed;Game.CutConfirmed+=CaptureOwner;Panel.SetActive(false);Reload();
        }
        public void Reload()
        {
            ExpandItems();
            if(ModelReview)
            {
                Progress=new CollectionProgress(Samples.Select(s=>s.Definition()));
                reviewSelected=Math.Max(0,Array.FindIndex(Samples,s=>s.ObjectId=="L01"));
                SavePending=false;loaded=true;RecoveryLabel.text="";ShowContent();return;
            }
            store=new LocalProgressStore(SavePath??Path.Combine(Application.persistentDataPath,"mvp-progress.json"),LegacySelectionOrder);
            Progress=store.Load(Samples.Select(s=>s.Definition()));SavePending=false;loaded=true;
            RecoveryLabel.text=store.Recovery=="backup"?L10n.T("이 기기 기록을 정상 백업에서 복구했습니다."):store.Recovery=="staged"?L10n.T("중단된 이 기기 저장을 복구했습니다."):store.Recovery=="reset"?L10n.T("저장을 복구하지 못해 새 기기 컬렉션으로 시작합니다."):"";
            if(store.PreservedFailure)RecoveryLabel.text+=L10n.T("\n문제 원본 파일을 별도로 보존했습니다.");
            if(store.PreservationPending)RecoveryLabel.text+=L10n.T("\n문제 원본 보존 대기 · 디스크 저장 보류");
            ShowContent();
        }
        public bool Select(int index)
        {
            if(!loaded||(Game.UiOpen&&!Panel.activeSelf)||index<0||index>=Samples.Length)return false;
            if(ModelReview)reviewSelected=index;
            else if(!Progress.Select(index))return false;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Begin();
            try{
#endif
            Panel.SetActive(false);Game.UiOpen=false;ShowContent();
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double saveStarted=SelectionPreparationReview.Start();
#endif
            Save();
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.SaveMs,saveStarted);
#endif
            return true;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            }finally{SelectionPreparationReview.End();}
#endif

        }
        readonly Dictionary<TMP_Text,Color> daylightText=new Dictionary<TMP_Text,Color>();
        Color daylightBackground;
        bool nightSky;

        void SetNightPalette(bool active)
        {
            if(active==nightSky)return;
            var camera=Game.GameCamera;
            if(active){
                daylightBackground=camera.backgroundColor;
                // Only the labels printed directly on the sky need new ink.
                // Menus and buttons retain their own opaque surfaces and text.
                void LightenLabels(Transform parent){
                    foreach(Transform child in parent){
                        if(!child.TryGetComponent<TMP_Text>(out var label))continue;
                        var ink=label.color;
                        daylightText.Add(label,ink);
                        Color lit=ink.g<.36f?new Color32(229,231,245,255):new Color32(165,176,204,255);
                        lit.a=ink.a;label.color=lit;
                    }
                }
                if (Game.Hud.Arcade == null) { LightenLabels(Label.transform.parent); LightenLabels(Game.Hud.Accuracy.transform.parent); }
                camera.backgroundColor=new Color32(14,17,34,255);
            }else{
                camera.backgroundColor=daylightBackground;
                foreach(var item in daylightText)if(item.Key!=null)item.Key.color=item.Value;
                daylightText.Clear();
            }
            if(Game.Hud.Arcade!=null)Game.Hud.Arcade.SetSky(active);
            nightSky=active;
        }
        void ShowContent()
        {
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double stageStarted=SelectionPreparationReview.Start();
#endif
            var sample=Samples[SelectedIndex];var presentation=Game.Presentation;
            SetNightPalette(Atmosphere!=null ? Atmosphere.Select(sample.ObjectId,presentation.Audio) : sample.GalaxyHaze!=null);
            bool authoredSpace=sample.IsPixelPlanet||sample.IsOrbitalSystem||sample.IsGalaxy;
            bool space=authoredSpace||sample.ContentVersion==HalfAndHalf.Presentation.SpaceContentModel.Version;
            if(presentation.Space!=null){presentation.Space.gameObject.SetActive(space);if(space){presentation.Space.PixelAsset=authoredSpace?sample:null;presentation.Space.Kind=sample.ObjectId;presentation.Space.ContentMode=true;}}
            bool voxel=HalfAndHalf.Presentation.VoxelPrototypeVisual.CanRender(sample);
            bool car=sample.ObjectId=="L02"&&(sample.ContentVersion==HalfAndHalf.Presentation.VoxelCarGeometry.Version||sample.ContentVersion==HalfAndHalf.Presentation.CarPixelSource.Version||sample.ContentVersion==HalfAndHalf.Presentation.CarPixelDetailSource.Version);
            if(presentation.Car!=null)presentation.Car.gameObject.SetActive(car);
            if(presentation.Voxel!=null)presentation.Voxel.gameObject.SetActive(voxel);
            if(car&&presentation.Car==null||voxel&&presentation.Voxel==null)throw new InvalidOperationException("Selected voxel content needs its authored renderer");
            if(car)presentation.Car.Show(sample);
            if(voxel)presentation.Voxel.Show(sample);
            if(sample.ObjectId=="B01"||voxel||car||space)presentation.Sample.gameObject.SetActive(false);
            else{presentation.Sample.gameObject.SetActive(true);presentation.Sample.Show(sample);}
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.VisualMs,stageStarted);stageStarted=SelectionPreparationReview.Start();
#endif
            presentation.Audio.ResetAttempt();presentation.Audio.CutVariants=sample.CutVariants;
            Label.text=L10n.T(sample.DisplayName);Hint.text=L10n.T(sample.MassHint)+(space&&!authoredSpace?L10n.T("\n임시 표현 · 공용 임시음 / 최종 제작 대기"):L10n.T("\n시제품 · 최종 아트 검수 대기"));Outcome.text="";Difference.text="";
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.HeaderUiMs,stageStarted);stageStarted=SelectionPreparationReview.Start();
#endif
            var definition=Progress.Content[SelectedIndex];
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.DefinitionMs,stageStarted);stageStarted=SelectionPreparationReview.Start();
#endif
            Game.Initialize(definition);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.GameInitMs,stageStarted);stageStarted=SelectionPreparationReview.Start();
#endif
            Game.Hud.Refresh(Game.Session,(long)(Time.realtimeSinceStartupAsDouble*1000));Refresh();
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.HudMs,stageStarted);
#endif

        }
        void Completed(AttemptResult result)
        {
            if(ModelReview){Outcome.text="MODEL REVIEW · NOT SAVED";Difference.text="DIFFERENCE  "+Scoring.Percent(Math.Abs(result.LeftPercent-result.RightPercent))+"pp";Refresh();return;}
            // The review gallery remains available to editor probes; it must never write unrelated records.
            if(result.Snapshot.ObjectId!=Samples[Progress.Selected].ObjectId)return;
            int previous=Progress.UnlockedCount;bool best=Progress.Record(result,ownedSnapshot==result.Snapshot?cutOwner:"");
            Outcome.text=(best?"NEW PERSONAL BEST": "")+(Progress.UnlockedCount>previous?"\nNEXT OBJECT UNLOCKED":Progress.Selected==Samples.Length-1&&Progress.RecordedCount==Samples.Length?"\nCOLLECTION COMPLETE":"");
            Difference.text="DIFFERENCE  "+Scoring.Percent(Math.Abs(result.LeftPercent-result.RightPercent))+"pp";
            if(Game.Hud.Arcade!=null){
                if (Progress.UnlockedCount > previous) {
                    string nextId=Samples[previous].ObjectId;
                    string title=Array.Find(Game.Hud.Arcade.Catalog, entry=>entry.Id==nextId).Name;
                    Outcome.text=L10n.F("{0} 열림!",L10n.T(title ?? Samples[previous].DisplayName));
                } else if (SelectedIndex == Samples.Length-1 && Progress.RecordedCount == Samples.Length)
                    Outcome.text=L10n.F("{0}종 완성!\n‘기록’에서 종합 정확도 확인",Samples.Length);
                else if (SelectedIndex+1 == Progress.UnlockedCount && Progress.UnlockedCount < Samples.Length && result.AccuracyUnits < 9000)
                    Outcome.text=L10n.F("해금까지 {0}%p",Scoring.Percent((9000-result.AccuracyUnits)/100m));
                else Outcome.text=best ? L10n.T("개인 최고 기록!") : "";
                Difference.text=L10n.T("무게 차이  ")+Scoring.Percent(Math.Abs(result.LeftPercent-result.RightPercent))+"pp";
            }
            if(best||SavePending)Save();Refresh();
        }
        void Refresh()
        {
            if(Game.Hud.Arcade!=null){Game.Hud.Arcade.ShowProgress();return;}
            if(ModelReview)
            {
                BestLabel.text=L10n.T("모델 확인 · 기록 저장 안 함");
                ProgressLabel.text="MODEL REVIEW  |  ALL "+Samples.Length+" OBJECTS";
                for(int i=0;i<Items.Length;i++)
                {
                    Items[i].gameObject.SetActive(i<Samples.Length);if(i>=Samples.Length)continue;
                    Items[i].interactable=true;
                    ItemLabels[i].text=(i+1).ToString("D2")+"  "+Samples[i].DisplayName+L10n.T("  [시제품]\nVIEW / CUT / RETRY");
                }
                NextButton.interactable=SelectedIndex+1<Samples.Length;return;
            }
            var best=Progress.Best(Progress.Selected);
            BestLabel.text=best==null?L10n.T("이 기기 기록  --"):L10n.T("이 기기 기록  ")+Scoring.Percent(best.AccuracyUnits/100m)+"%  /  "+(best.ElapsedMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture)+" sec";
            ProgressLabel.text=Progress.UnlockedCount+" / "+Samples.Length+" UNLOCKED  |  C 90.00% TO ADVANCE";
            if(OverallRecord.IdFor(Samples.Length)!=null)ProgressLabel.text+=L10n.T("\n기록 ")+Progress.RecordedCount+" / "+Samples.Length+" · "+Scoring.Percent(Progress.CompletionPercent)+"% · "+(Progress.OverallBest==null?L10n.T("종합 미참가"):L10n.T("종합 기록 완성"));
            for(int i=0;i<Items.Length;i++)
            {
                Items[i].gameObject.SetActive(i<Samples.Length);
                if(i>=Samples.Length)continue;
                Items[i].interactable=i<Progress.UnlockedCount;var r=Progress.Best(i);
                ItemLabels[i].text=(i+1).ToString("D2")+"  "+Samples[i].DisplayName+L10n.T("  [시제품]\n")+(i>=Progress.UnlockedCount?"LOCKED  |  Previous object: C or better":r==null?"PLAY  |  No record":Scoring.Percent(r.AccuracyUnits/100m)+"%  /  "+(r.ElapsedMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture)+" sec  /  "+(r.Grade==Grade.Perfect?"PERFECT":r.Grade==Grade.SPlus?"S+":r.Grade.ToString()));
            }
            NextButton.interactable=Progress.Selected+1<Progress.UnlockedCount;
        }
        // Preserve the authored ten-row MVP. Larger collections reuse its row design in a scrolling list.
        void ExpandItems()
        {
            if(Samples.Length<=Items.Length)return;
            var scroll=Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            RectTransform content;
            if(scroll==null)
            {
                var root=new GameObject("CollectionScroll",typeof(RectTransform),typeof(UnityEngine.UI.ScrollRect));
                var rect=root.GetComponent<RectTransform>();rect.SetParent(Panel.transform,false);
                rect.anchorMin=Game.Hud.Arcade!=null?new Vector2(.06f,.045f):new Vector2(.04f,.11f);rect.anchorMax=Game.Hud.Arcade!=null?new Vector2(.94f,.794f):new Vector2(.96f,.85f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                var viewport=new GameObject("Viewport",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Mask));
                var view=viewport.GetComponent<RectTransform>();view.SetParent(rect,false);view.anchorMin=Vector2.zero;view.anchorMax=Vector2.one;view.offsetMin=view.offsetMax=Vector2.zero;
                viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
                var list=new GameObject("Content",typeof(RectTransform),typeof(UnityEngine.UI.ContentSizeFitter));
                content=list.GetComponent<RectTransform>();content.SetParent(view,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
                if(Game.Hud.Arcade!=null){
                    var grid=list.AddComponent<ArcadeCardGrid>();grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount=2;grid.spacing=new Vector2(20,22);
                }else{
                var layout=list.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                }
                list.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                scroll=root.GetComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
                foreach(var row in Items)MoveRow(row,content);
            }
            else content=scroll.content;
            var buttons=new List<UnityEngine.UI.Button>(Items);var labels=new List<TMP_Text>(ItemLabels);
            while(buttons.Count<Samples.Length)
            {
                int index=buttons.Count;var button=Instantiate(Items[0],content);button.name="CollectionItem"+index;
                button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>Select(index));
                MoveRow(button,content);buttons.Add(button);labels.Add(button.GetComponentInChildren<TMP_Text>());
            }
            Items=buttons.ToArray();ItemLabels=labels.ToArray();
        }
        static void MoveRow(UnityEngine.UI.Button button,RectTransform content)
        {
            var rect=button.GetComponent<RectTransform>();float height=rect.sizeDelta.y;
            rect.SetParent(content,false);rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.anchoredPosition=Vector2.zero;
            var layout=button.GetComponent<UnityEngine.UI.LayoutElement>()??button.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.preferredHeight=height>0?height:135;layout.flexibleHeight=0;
        }
        public void Open()
        {
            if(!loaded||(Game.UiOpen&&!Panel.activeSelf))return;Game.UiOpen=true;Game.SetInterrupted();Refresh();Panel.SetActive(true);Panel.transform.SetAsLastSibling();
            if(Game.Hud.Arcade!=null)Game.Hud.Arcade.FocusCurrentCard();
        }
        public void Close()
        {Panel.SetActive(false);Game.UiOpen=false;Game.Retry();Outcome.text="";Difference.text="";}
        void Save()
        {
            if(ModelReview)return;
            try {store.Save(Progress);if(SavePending)RecoveryLabel.text="";SavePending=false;}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException)
            {SavePending=true;RecoveryLabel.text=L10n.T("기기 저장 재시도 대기 · 현재 플레이는 계속할 수 있습니다.");}
        }
        public void ClaimGuestRecords(Func<AttemptResult,string> resolveOwner)
        {if(Progress.ClaimGuestRecords(resolveOwner))Save();}
        void Update()
        {
            if(!loaded)return;var phase=Game.Session.Phase;
            Hint.gameObject.SetActive(!Game.UiOpen&&phase==AttemptPhase.Ready);
            Outcome.gameObject.SetActive(!Game.UiOpen&&phase==AttemptPhase.Result);
            Difference.gameObject.SetActive(!Game.UiOpen&&phase==AttemptPhase.Result);
            NextButton.gameObject.SetActive(!Game.UiOpen&&phase==AttemptPhase.Result);
        }
        void OnApplicationPause(bool paused){if(paused&&loaded&&SavePending)Save();}
        void CaptureOwner(CutSnapshot snapshot){ownedSnapshot=snapshot;cutOwner=RecordOwner?.Invoke()??"";}
        void OnDestroy(){if(Game!=null){if(Game.GameCamera!=null)SetNightPalette(false);Game.AttemptCompleted-=Completed;Game.CutConfirmed-=CaptureOwner;}}
    }
}
