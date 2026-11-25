using System;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Content;
using HalfAndHalf.UI;
using UnityEngine;

namespace HalfAndHalf.Services
{
    public sealed class GameCenterController : MonoBehaviour
    {
        public MvpCollection Collection;
        public GameCenterConfiguration Configuration;
        public SubmissionQueue Queue { get; private set; }
        public GameCenterFlow Flow { get; private set; }
        double nextCheck,nextFlush;
        string boundPath;
        GoogleRankConfiguration googleConfig;
        LeaderboardCatalog googleBoards;
        LeaderboardCatalog activeBoards;
#if UNITY_ANDROID && !UNITY_EDITOR
        GooglePlayRankings googleService;
#endif
        public int PendingCount=>Queue?.Entries.Count(e=>e.pending)??0;
        public int HeldCount=>Queue?.Entries.Count(e=>e.pending&&!string.IsNullOrEmpty(e.heldReason))??0;
        public string HeldNotice=>Queue?.Entries.Any(e=>e.pending&&e.heldReason=="missing-evidence")==true?"원본 근거 없음 · 기존 후보 제출 보류":HeldCount>0?"원본·점수·계정 불일치 · 제출 보류":"";
        public static bool OnAndroid => Application.platform == RuntimePlatform.Android;
        public string ServiceName => OnAndroid ? "Google Play Games" : "Game Center";
        VisualSampleAsset[] catalogSamples;
        GameCenterConfiguration catalogConfig;
        bool hasMappedStages;
        void PrepareCatalog()
        {
            if(catalogSamples==Collection.Samples && catalogConfig==Configuration)return;
            catalogSamples=Collection.Samples;
            catalogConfig=Configuration;
            activeBoards=OnAndroid?googleBoards:Configuration?.PlayableCatalog(true);
            hasMappedStages=activeBoards!=null && catalogSamples.Any(s=>activeBoards.Resolve(s.ObjectId,s.ContentVersion)!=null);
            boundPath=null;
        }
        public bool LocalRankingsOnly
        {
            get { PrepareCatalog(); return !hasMappedStages || !Configured; }
        }
        public bool Configured=>OnAndroid ? googleBoards != null : Configuration!=null&&Configuration.EnableOnline;
        public bool OverallConfigured {get; private set;}
        void Awake()
        {
            if(!OnAndroid)return;
            try { googleConfig=GoogleRankConfiguration.Load();googleBoards=googleConfig?.CreateCatalog(); }
            catch(ArgumentException e){Debug.LogWarning("Google Play Games configuration: "+e.Message);}
        }
        public bool ObjectConfigured(int index)
        {
            if(!Configured||Collection?.Progress==null||index<0||index>=Collection.Progress.Content.Count)return false;
            var d=Collection.Progress.Content[index];return activeBoards?.Resolve(d.Id,d.ContentVersion)!=null;
        }
        void Update()
        {
            if(Collection?.Progress==null)return;
            PrepareCatalog();
            if(!hasMappedStages)return;
            var path=Collection.SavePath==null?Path.Combine(Application.persistentDataPath,OnAndroid?"google-play-queue.json":"game-center-queue.json")
                :Collection.SavePath+(OnAndroid?".google-queue":".queue");
            if(Flow==null || path!=boundPath)
            {
                // A non-default collection path is also a non-default queue path (tests and local QA never touch player saves).
                Flow?.UseGuest();if(Queue?.SavePending==true)Queue.Persist();boundPath=path;nextCheck=nextFlush=0;
                var definitions=Collection.Progress.Content;
                var catalog=activeBoards;
                OverallConfigured=!OnAndroid && Configured && Configuration.OverallReady(definitions,catalog);
                if(!OnAndroid && !OverallConfigured && Configuration!=null)catalog=Configuration.PlayableCatalog(false);
                Queue=new SubmissionQueue(path,Collection.Progress.Content.Where(d=>catalog.Resolve(d.Id,d.ContentVersion)!=null),OverallConfigured?OverallRecord.VersionFor(Collection.Progress.Content):null);
                bool CanSubmit(string board,long score,string player) => Flow!=null && Flow.IsSignedIn && Flow.ActivePlayerId==player &&
                    Queue.Entries.Any(e=>e.score==score&&catalog.Resolve(e.objectId,e.contentVersion)==board&&Queue.CanSubmit(e,player));
#if UNITY_ANDROID && !UNITY_EDITOR
                googleService=new GooglePlayRankings(googleConfig.projectId,CanSubmit);
                Flow=new GameCenterFlow(googleService,Queue,catalog);
#else
                string overallBoard=OverallConfigured?catalog.Resolve(OverallRecord.IdFor(definitions.Count),OverallRecord.VersionFor(definitions)):null;
                Flow=new GameCenterFlow(new AppleGameCenter(Configured,overallBoard,CanSubmit,definitions.Count),Queue,catalog);
#endif
                Collection.RecordOwner=()=>Flow.IsSignedIn?Flow.ActivePlayerId:"";
                // Attach an exact legacy guest source before login can claim the collection for an account.
                if(!Collection.SavePending)
                {
                    for(int i=0;i<Collection.Progress.Content.Count;i++)Queue.ReconcileOwned(Collection.Progress.Best(i),Collection.Progress.BestOwner(i));
                    if(OverallConfigured)Queue.ReconcileOverall(Collection.Progress.OverallBest);
                }
            }
            Flow.RefreshAccount();
            double now=Time.realtimeSinceStartupAsDouble;
            if(now>=nextCheck && !Collection.SavePending)
            {
                nextCheck=now+.5;
                var retainedOverall=Collection.Progress.OverallBest;
                Collection.ClaimGuestRecords(r=>Queue.ClaimOwner(r,Flow.IsSignedIn?Flow.ActivePlayerId:"",retainedOverall));
                if(Collection.SavePending)return;
                for(int i=0;i<Collection.Progress.Content.Count;i++)
                    if(Queue.ReconcileOwned(Collection.Progress.Best(i),Collection.Progress.BestOwner(i)))nextFlush=0;
                if(OverallConfigured&&Queue.ReconcileOverall(Collection.Progress.OverallBest))nextFlush=0;
                if(Flow.IsSignedIn)Queue.BindGuest(Flow.ActivePlayerId);
                if(Queue.SavePending)Queue.Persist();
            }
            if(now>=nextFlush && PendingCount>0 && Flow.IsSignedIn && !Flow.IsFlushing)
            {nextFlush=now+30;FlushWithoutBlocking();}
        }
        async void FlushWithoutBlocking(){await Flow.Flush();}
        void OnApplicationPause(bool paused)
        {
            if(paused){if(Queue?.SavePending==true)Queue.Persist();}
            else
            {
                nextFlush=0;
#if UNITY_ANDROID && !UNITY_EDITOR
                googleService?.RefreshIdentity();
#endif
            }
        }
        void OnDestroy(){Flow?.UseGuest();if(Queue?.SavePending==true)Queue.Persist();}
    }
}
