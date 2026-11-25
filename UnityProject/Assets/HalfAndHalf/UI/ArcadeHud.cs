using HalfAndHalf.Localization;
using System;
using System.Globalization;
using HalfAndHalf.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalfAndHalf.UI
{
    public sealed class ArcadeHud : MonoBehaviour
    {
        [Serializable] public struct Entry { public string Id, Name, Chapter; }
        public Entry[] Catalog;
        public Texture2D Thumbnails;
        public GameHud Hud;
        public MvpCollection Collection;
        public TMP_Text Chapter, Goal, NextCaption;
        public GameObject AimPanel;
        public RectTransform BalanceFill, Cursor;
        public Image Accent;
        public TMP_Text[] SkyText;
        public Color Ink, Paper, Amber;
        AttemptSession attempt;
        AttemptPhase phase = (AttemptPhase)(-1);
        bool dragging;
        long displayedHundredth = -1;
        float revealStart;
        string Percent(decimal value) => Scoring.Percent(value) + "%";

        public void SetSky(bool night)
        {
            foreach (var label in SkyText) label.color = night ? Paper : Ink;
            Accent.color = night ? Amber : Ink;
        }

        public void RefreshLanguage() { phase=(AttemptPhase)(-1); ShowProgress(); }

        public void ShowProgress()
        {
            var c = Collection;
            int selected = c.SelectedIndex;
            var current = c.Samples[selected];
            int Find(string id) => Array.FindIndex(Catalog, item => item.Id == id);
            int entry = Find(current.ObjectId);
            string Display(int index, int key) => key >= 0 ? Catalog[key].Name : c.Samples[index].ObjectId switch {
                "B05"=>L10n.T("레몬"),"A03"=>L10n.T("배"),"N03"=>L10n.T("빙산"),"P03"=>L10n.T("화성"),"P04"=>L10n.T("목성"),_=>c.Samples[index].DisplayName
            };
            Chapter.text = $"STAGE  {selected+1:00} / {c.Samples.Length:00}     ·     " + L10n.T(entry >= 0 ? Catalog[entry].Chapter : "보관 스테이지");
            c.Label.text = L10n.T(Display(selected, entry));
            var best = c.Progress.Best(selected);
            c.BestLabel.text = MvpCollection.ModelReview ? L10n.T("모델 확인") : best == null ? L10n.T("첫 기록에 도전") :
                L10n.F("최고  {0}  ·  {1}초",Percent(best.AccuracyUnits/100m),(best.ElapsedMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture));
            c.Hint.text = L10n.T(!MvpCollection.ModelReview && current.ObjectId == "B01" && best == null
                ? "보이는 면적이 아니라 무게를 반으로 나눠 보세요." : current.MassHint);
            int unlocked = MvpCollection.ModelReview ? c.Samples.Length : c.Progress.UnlockedCount;
            c.ProgressLabel.text = MvpCollection.ModelReview ? L10n.T("모델 확인 · 전체 열림") :
                c.Progress.RecordedCount == c.Samples.Length ?
                L10n.F("{0}종 완성 · 종합 정확도는 ‘기록’에서\n더 정확히 잘라 종합 기록을 갱신하세요.",c.Samples.Length) :
                L10n.F("해금 {0} / {1}  ·  기록 {2}\n정확도 90%로 다음 대상 열기",unlocked,c.Samples.Length,c.Progress.RecordedCount);
            for (int i=0; i<c.Items.Length; i++)
            {
                var b=c.Items[i]; b.gameObject.SetActive(i<c.Samples.Length);
                if (i>=c.Samples.Length) continue;
                int key=Find(c.Samples[i].ObjectId);
                b.interactable=i<unlocked;
                var record=c.Progress.Best(i);
                c.ItemLabels[i].text=L10n.T(Display(i,key));
                var status=b.transform.Find("CardStatus").GetComponent<TMP_Text>();
                status.text=!b.interactable ? L10n.T("잠김") : i==selected ? L10n.T("선택 중") : record==null ? L10n.T("도전") : Percent(record.AccuracyUnits/100m);
                var index=b.transform.Find("CardIndex").GetComponent<TMP_Text>();
                index.text=$"{i+1:00}   /   "+L10n.T(key>=0?Catalog[key].Chapter:"보관");
                var art=b.transform.Find("Portrait").GetComponent<RawImage>();
                art.enabled=key>=0;
                b.transform.Find("ArchiveMark").gameObject.SetActive(key<0);
                if(key>=0)art.uvRect=new Rect((key%6)/6f,1-(key/6+1)/4f,1/6f,1/4f);
                art.color=b.interactable ? Color.white : new Color(.78f,.78f,.78f,1);
                b.transform.Find("SelectedEdge").gameObject.SetActive(i==selected);
            }
            c.NextButton.interactable=selected+1<unlocked;
            NextCaption.text=c.NextButton.interactable ? L10n.T("다음 대상") : selected+1>=c.Samples.Length ? L10n.T("마지막 대상") : L10n.T("90%로 해금");
        }

        public void FocusCurrentCard()
        {
            var scroll=Collection.Panel.GetComponentInChildren<ScrollRect>();
            if(scroll==null)return;
            Canvas.ForceUpdateCanvases();
            var card=(RectTransform)Collection.Items[Collection.SelectedIndex].transform;
            float centerY=-scroll.content.InverseTransformPoint(card.TransformPoint(card.rect.center)).y;
            float height=scroll.viewport.rect.height;
            float travel=scroll.content.rect.height-height;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition=travel>0 ? 1-Mathf.Clamp01((centerY-height*.5f)/travel) : 1;
        }
        public void Render(AttemptSession session, long now)
        {
            bool held=session.ActivePointer.HasValue;
            bool changed=attempt!=session || phase!=session.Phase;
            if (changed || held!=dragging)
            {
                attempt=session; phase=session.Phase; dragging=held;
                bool result=phase==AttemptPhase.Result, invalid=phase==AttemptPhase.Invalid;
                Hud.ResultPanel.SetActive(result);
                AimPanel.SetActive(!result);
                Hud.RetryButton.gameObject.SetActive(result || invalid);
                Hud.State.text=phase switch {
                    AttemptPhase.Ready => held ? L10n.T("조준 중") : L10n.T("절단 준비"),
                    AttemptPhase.Dropping => L10n.T("준비 중"), AttemptPhase.Cutting => L10n.T("절단 중"),
                    AttemptPhase.Invalid => L10n.T("다시 도전"), _ => "" };
                Goal.text=invalid ? (session.InvalidReason==InvalidReason.TimedOut ? L10n.T("시간이 다 됐어요") : L10n.T("플레이가 중단됐어요")) : "50 <color=#F6AB56>/</color> 50";
                Hud.Hint.text=invalid ? "" : held ? L10n.T("손을 놓으면 잘려요") : L10n.T("누른 채 좌우로 움직이고, 놓아서 자르세요");
                Hud.Hint.gameObject.SetActive(!invalid);
                Cursor.parent.gameObject.SetActive(!invalid);
                if (result)
                {
                    var r=session.Result;
                    Hud.Accuracy.text=Percent(r.AccuracyUnits/100m);
                    Hud.GradeLabel.text=r.Grade switch {Grade.None=>L10n.T("재도전"),Grade.Perfect=>"PERFECT",Grade.SPlus=>"S+",_=>L10n.F("등급 {0}",r.Grade.ToString())};
                    Hud.Left.text=L10n.T("왼쪽  ")+Percent(r.LeftPercent); Hud.Right.text=L10n.T("오른쪽  ")+Percent(r.RightPercent);
                    Hud.TimeLabel.text=(r.ElapsedMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture)+L10n.T("초");
                    revealStart=Time.unscaledTime;
                }
                displayedHundredth=-1;
            }
            if (phase==AttemptPhase.Result)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.unscaledTime-revealStart)/.22f));
                float x=Mathf.Lerp(.5f,(float)session.Result.LeftPercent/100,t);
                if(BalanceFill.anchorMax.x!=x)BalanceFill.anchorMax=new Vector2(x,1);
            }
            else
            {
                long ticks=phase==AttemptPhase.Ready ? session.Elapsed(now)/10 : 0;
                if(displayedHundredth!=ticks){
                    displayedHundredth=ticks;
                    Hud.TimeLabel.text=(ticks/100m).ToString("F2",CultureInfo.InvariantCulture)+L10n.T("초");
                }
                float x=.5f;
                if(dragging && Collection.Game.GameCamera!=null)
                    x=Mathf.Clamp01(Collection.Game.GameCamera.WorldToViewportPoint(new Vector3((float)session.KnifeX,0,0)).x);
                if(Cursor.anchorMin.x!=x){Cursor.anchorMin=new Vector2(x,0);Cursor.anchorMax=new Vector2(x,1);}
            }
        }
    }
}
