using HalfAndHalf.Localization;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using TMPro;
using UnityEngine;

namespace HalfAndHalf.UI
{
    public sealed class RankingPanel : MonoBehaviour
    {
        public AppleGame Game;
        public GameCenterController Controller;
        public GameObject Panel;
        public GameObject Backdrop;
        public UnityEngine.UI.Button OpenButton,CloseButton,LoginButton,GuestButton,RefreshButton;
        public UnityEngine.UI.Button[] ObjectButtons;
        public TMP_Text Status,Selected,LocalRecord;
        public TMP_Text[] Rows;
        string[] ids={"B01","B03","A04"},names={"사과","수박","망치"};
        bool HasOverall => OverallRecord.IdFor(Controller.Collection.Samples.Length) != null;
        bool FullMode => HasOverall || Game.Hud.Arcade != null;
        public bool OverallSelected=>HasOverall&&selected==ids.Length;
        bool PageHasOnlineBoard => !Controller.LocalRankingsOnly && (!FullMode || (OverallSelected?Controller.OverallConfigured:Controller.ObjectConfigured(selected)));
        string OverallLabel => L10n.F("{0}종 종합",ids.Length);
        int selected,request;
        bool loading;
        GameCenterFlow remoteFlow;
        int remoteSession;
        bool CurrentRemote => remoteFlow != null && Controller.Flow == remoteFlow
            && remoteFlow.SessionVersion == remoteSession && remoteFlow.IsSignedIn;
        void Start()
        {
            L10n.Changed += RefreshLanguage;
            Panel.SetActive(false);
            foreach(var row in Rows)row.richText=false;
            LocalRecord.richText=false;
            var footer=Panel.transform.Find("AggregateDisabled")?.GetComponent<TMP_Text>();
            if(footer!=null)footer.text=Controller.LocalRankingsOnly
                ?L10n.T("개인 기록은 이 기기에 저장됩니다."):L10n.F("기기 진행과 {0} 기록은 별도로 보관됩니다.",Controller.ServiceName);
            if(GameCenterController.OnAndroid)L10n.Bind(LoginButton.GetComponentInChildren<TMP_Text>(true),"Google Play 로그인");
            if(Controller.LocalRankingsOnly)
            {
                L10n.Bind(OpenButton.GetComponentInChildren<TMP_Text>(true),"기록");
                var title=Panel.transform.Find("RankingsTitle")?.GetComponent<TMP_Text>();
                if(title!=null)L10n.Bind(title,"개인 기록");
                LoginButton.gameObject.SetActive(false);
                GuestButton.gameObject.SetActive(false);
                RefreshButton.gameObject.SetActive(false);
            }
            if(Backdrop!=null)Backdrop.SetActive(false);
            OpenButton.onClick.AddListener(()=>Game.AfterResult(Open));CloseButton.onClick.AddListener(Close);
            LoginButton.onClick.AddListener(async()=>await Login());
            GuestButton.onClick.AddListener(Guest);RefreshButton.onClick.AddListener(async()=>await Reload());
            if(FullMode)
            {
                ids=Controller.Collection.Samples.Select(s=>s.ObjectId).ToArray();
                var labels=Game.Hud.Arcade?.Catalog;
                names=Controller.Collection.Samples.Select(s=>labels==null?s.DisplayName:
                    Array.Find(labels,entry=>entry.Id==s.ObjectId).Name??s.DisplayName).ToArray();
                L10n.Bind(ObjectButtons[0].GetComponentInChildren<TMP_Text>(),"이전 대상");
                L10n.Bind(ObjectButtons[1].GetComponentInChildren<TMP_Text>(),"다음 대상");
                int pages = ids.Length + (HasOverall ? 1 : 0);
                ObjectButtons[2].gameObject.SetActive(HasOverall);
                if (HasOverall) ObjectButtons[2].GetComponentInChildren<TMP_Text>().text=OverallLabel;
                ObjectButtons[0].onClick.AddListener(async()=>{selected=(selected+pages-1)%pages;await Reload();});
                ObjectButtons[1].onClick.AddListener(async()=>{selected=(selected+1)%pages;await Reload();});
                ObjectButtons[2].onClick.AddListener(async()=>{selected=ids.Length;await Reload();});
            }
            else for(int i=0;i<ObjectButtons.Length;i++){int index=i;ObjectButtons[i].onClick.AddListener(async()=>{selected=index;await Reload();});}
        }
        public void Open()
        {
            if(Game.UiOpen || Controller.Collection.Progress==null
                || (!Controller.LocalRankingsOnly && Controller.Flow==null))return;
            if(FullMode)selected=Controller.Collection.Progress.Selected;
            Game.UiOpen=true;Game.SetInterrupted();if(Backdrop!=null)Backdrop.SetActive(true);Panel.SetActive(true);Panel.transform.SetAsLastSibling();Clear();ReloadWithoutBlocking();
        }
        public void Close()
        {if(!Panel.activeSelf)return;request++;Panel.SetActive(false);Game.UiOpen=false;if(Backdrop!=null)Backdrop.SetActive(false);Game.Retry();}
        async void ReloadWithoutBlocking(){await Reload();}
        async Task Login()
        {
            var flow=Controller.Flow;
            if(flow==null)return;
            int generation=++request;loading=false;
            Clear();await flow.Login();
            if(this==null || generation!=request || Controller.Flow!=flow || !Panel.activeSelf)return;
            await Reload();
        }
        void Guest(){request++;loading=false;Controller.Flow?.UseGuest();Clear();}
        void Clear()
        {
            remoteFlow=null;
            foreach(var row in Rows)row.text="";LocalRecord.text=L10n.T("이 기기 기록  --");
            Selected.text=(!PageHasOnlineBoard?L10n.T("개인 기록 · "):Controller.ServiceName+L10n.T(" 기록 · "))+(OverallSelected?OverallLabel:L10n.T(names[selected]))+L10n.T("\n정확도 우선 · 시간 차선");
            if(FullMode)
            {
                var p=Controller.Collection.Progress;
                if(OverallSelected)
                {
                    Selected.text+=L10n.T("\n기록 ")+p.RecordedCount+" / "+ids.Length+" · "+Scoring.Percent(p.CompletionPercent)+"%";
                    var overall=p.OverallBest;
                    LocalRecord.text=overall==null?L10n.F("이 기기 기록 · 종합 미완성 ({0}종 필요)",ids.Length):L10n.T("이 기기 기록 · 종합  ")+Scoring.Percent(overall.AccuracyUnits/100m)+"% / "+(overall.TimeMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture)+L10n.T("초");
                    if(PageHasOnlineBoard&&overall!=null&&!overall.TrySubmissionOwner(out _))LocalRecord.text+=L10n.T("\n여러 계정의 기록 포함 · 온라인 종합 제출 제외");
                }
                else
                {
                    var r=p.Best(selected);
                    LocalRecord.text=r==null?L10n.T("이 기기 기록  --"):L10n.T("이 기기 기록  ")+Scoring.Percent(r.AccuracyUnits/100m)+"% / "+(r.ElapsedMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture)+L10n.T("초");
                }
            }
            else
            {
                var p=Controller.Collection.Progress;var r=p.Best(p.Content.ToList().FindIndex(d=>d.Id==ids[selected]));
                if(r!=null)LocalRecord.text=L10n.T("이 기기 기록  ")+Scoring.Percent(r.AccuracyUnits/100m)+"% / "+(r.ElapsedMilliseconds/1000m).ToString("F3",CultureInfo.InvariantCulture)+L10n.T("초");
            }
            if(PageHasOnlineBoard)LocalRecord.text+="\n"+Controller.ServiceName+L10n.T(" 기록 · 내 순위  --");
        }
        public async Task Reload()
        {
            int generation=++request;Clear();loading=true;
            try
            {
                if(Controller.LocalRankingsOnly)
                {Status.text=L10n.T("이 기기의 개인 기록");return;}
                if(!PageHasOnlineBoard)
                {Rows[0].text=L10n.T("이 기록은 이 기기에 저장됩니다.");return;}
                var flow=Controller.Flow;
                if(!flow.IsSignedIn)return;
                remoteFlow=flow;remoteSession=flow.SessionVersion;
                await flow.Flush();
                if(this==null || generation!=request || !Panel.activeSelf || !CurrentRemote)return;
                var definition=OverallSelected?null:Controller.Collection.Progress.Content.First(d=>d.Id==ids[selected]);
                var page=await flow.Load(OverallSelected?OverallRecord.IdFor(ids.Length):definition.Id,OverallSelected?OverallRecord.VersionFor(Controller.Collection.Progress.Content):definition.ContentVersion);
                if(this==null || generation!=request || !Panel.activeSelf || !CurrentRemote)return;
                Display(page);
            }
            catch(Exception){if(this!=null&&generation==request&&Panel.activeSelf&&CurrentRemote){Clear();Rows[0].text=Controller.ServiceName+L10n.T(" 기록 조회 실패\n로컬 플레이는 계속할 수 있습니다.");}}
            finally{if(generation==request)loading=false;}
        }
        public void Display(RankingPage page)
        {
            Clear();if(page==null || !PageHasOnlineBoard)return;
            if(Controller.Flow?.IsSignedIn==true){remoteFlow=Controller.Flow;remoteSession=remoteFlow.SessionVersion;}
            int i=0;foreach(var entry in page.Entries??Array.Empty<RankingEntry>())
            {
                if(i>=Rows.Length)break;
                if(!Decode(entry.Score,out _,out _))continue;
                Rows[i++].text=Format(entry);
            }
            if(i==0)Rows[0].text=L10n.T("아직 등록된 기록이 없습니다.");
            if(page.LocalPlayer!=null && Decode(page.LocalPlayer.Score,out _,out _))LocalRecord.text=LocalRecord.text.Replace(Controller.ServiceName+L10n.T(" 기록 · 내 순위  --"),Controller.ServiceName+L10n.T(" 기록 · 내 순위  ")+Format(page.LocalPlayer));
        }
        bool Decode(long score,out int accuracy,out long time)=>OverallSelected?RankingScore.TryDecodeOverall(score,out accuracy,out time,ids.Length):RankingScore.TryDecodeAttempt(score,out accuracy,out time);
        string Format(RankingEntry entry)
        {
            Decode(entry.Score,out var accuracy,out var time);
            string name=(entry.PlayerName??"Player").Replace('\n',' ').Replace('\r',' ');
            if(name.Length>16)name=name.Substring(0,16)+"…";
            return "#"+entry.Rank+"  "+name+"\n"+Scoring.Percent(accuracy/100m)+"%  /  "+(time/1000m).ToString("F3",CultureInfo.InvariantCulture)+L10n.T("초");
        }
        void Update()
        {
            // Content stays inside SafeRoot; the shared modal backdrop also covers the notch/home-indicator margins.
            if(Backdrop!=null&&Backdrop.activeSelf!=Game.UiOpen)Backdrop.SetActive(Game.UiOpen);
            if(!Panel.activeSelf || Controller.LocalRankingsOnly)return;
            if(remoteFlow!=null && !CurrentRemote){request++;loading=false;Clear();}
            if(Controller.Flow==null)return;
            var flow=Controller.Flow;
            Status.text=flow.IsLoggingIn?Controller.ServiceName+L10n.T(" 로그인 중…"):flow.IsSignedIn?Controller.ServiceName+L10n.T(" 연결됨"):flow.LastLogin==LoginOutcome.Cancelled?L10n.T("로그인을 취소했습니다. 게스트 플레이 가능"):flow.LastLogin==LoginOutcome.Failed?L10n.T("로그인 실패. 게스트 플레이 가능"):!Controller.Configured?L10n.T("온라인 랭킹 준비 중 · 게스트 플레이 가능"):Application.isEditor?L10n.T("에디터 게스트 · 기기에서 Game Center 로그인"):L10n.T("게스트 · 개인 기록은 기기에 저장됩니다.");
            Status.text+=L10n.T("\n제출 대기 ")+Controller.PendingCount+L10n.T("개")+(Controller.Queue.SavePending?L10n.T(" · 저장 재시도 대기"):flow.SubmissionFailed?L10n.T(" · 연결 후 재시도"):"");
            if(Controller.HeldCount>0)Status.text+=L10n.T(" · 보류 ")+Controller.HeldCount+L10n.T("개\n")+L10n.T(Controller.HeldNotice);
            else if(Controller.Queue.PreservationPending)Status.text+=L10n.T("\n문제 원본 보존 대기 · 제출 저장 보류");
            else if(Controller.Queue.PreservedFailure)Status.text+=L10n.T("\n문제 원본 보존 · 정상 후보만 제출");
            LoginButton.interactable=!flow.IsLoggingIn && !flow.IsFlushing;
            RefreshButton.interactable=flow.IsSignedIn&&!loading&&!flow.IsFlushing;
            foreach(var b in ObjectButtons)b.interactable=!loading;
        }
        void RefreshLanguage()
        {
            var footer=Panel.transform.Find("AggregateDisabled")?.GetComponent<TMP_Text>();
            if(footer!=null)footer.text=Controller.LocalRankingsOnly?L10n.T("개인 기록은 이 기기에 저장됩니다."):L10n.F("기기 진행과 {0} 기록은 별도로 보관됩니다.",Controller.ServiceName);
            if(HasOverall)ObjectButtons[2].GetComponentInChildren<TMP_Text>().text=OverallLabel;
            if(Panel.activeSelf)ReloadWithoutBlocking();
        }
        void OnDestroy(){request++;L10n.Changed-=RefreshLanguage;}
    }
}
