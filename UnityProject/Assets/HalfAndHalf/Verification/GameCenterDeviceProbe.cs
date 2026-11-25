#if UNITY_IOS && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Opt-in native service QA. Local saves are isolated; remote scores come only from real CUT results.
    public sealed class GameCenterDeviceProbe : MonoBehaviour
    {
        [Serializable] sealed class RemoteRow { public long rank,score; }
        [Serializable] sealed class BoardEvidence
        {
            public string objectId,contentVersion,leaderboardId,error;
            public int accuracyUnits;
            public long timeMs,expectedScore,localRank,localScore,totalPlayers;
            public bool localMatches,sharedRanksValid;
            public RemoteRow[] entries;
        }
        [Serializable] sealed class Evidence
        {
            public string mode,stage,login,playerToken,error;
            public string kind="Actual iPhone/Game Center service; automated CUT input; no synthetic score submission";
            public string rankingPolicy="Game competition ranks group equal official encoded scores; native ordinals are normalized";
            public bool configured,signedIn,queueDurable,success;
            public int bests,unlocked,pendingBefore,pendingAfter,errors,warnings;
            public BoardEvidence[] boards;
        }
        string mode,folder,path;
        MvpCollection collection;
        GameCenterController controller;
        Evidence evidence;
        readonly List<BoardEvidence> boards=new List<BoardEvidence>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Begin()
        {
            string mode=Environment.GetEnvironmentVariable("HALF_TASK4_GAME_CENTER_PROBE");
            if(mode!="offline" && mode!="comparison-offline" && mode!="network-offline" && mode!="verify" && mode!="resume")return;
            var go=new GameObject("GameCenterDeviceProbe");DontDestroyOnLoad(go);
            var probe=go.AddComponent<GameCenterDeviceProbe>();probe.mode=mode;
            probe.folder=Path.Combine(Application.persistentDataPath,"task4-game-center-qa");
            Directory.CreateDirectory(probe.folder);probe.path=Path.Combine(probe.folder,"progress.json");
            probe.evidence=new Evidence{mode=mode,stage="starting"};
            if(mode=="offline" || mode=="comparison-offline" || mode=="network-offline")foreach(string suffix in new[]{"",".bak",".tmp",".queue",".queue.bak",".queue.tmp"})
                if(File.Exists(probe.path+suffix))File.Delete(probe.path+suffix);
            SceneManager.sceneLoaded+=probe.Loaded;Application.logMessageReceived+=probe.Log;
        }
        void Loaded(Scene scene,LoadSceneMode _)
        {
            collection=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();
            if(collection==null)return;
            collection.SavePath=path;StartCoroutine(Run());
        }
        IEnumerator Run()
        {
            yield return null;controller=collection.Game.GetComponent<GameCenterController>();
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(controller.Flow==null){if(Time.realtimeSinceStartupAsDouble>deadline){Fail("Controller initialization timeout");yield break;}yield return null;}
            evidence.configured=controller.Configured;
            if(mode=="offline" || mode=="comparison-offline" || mode=="network-offline")
            {
                SetStage("creating-real-cut-records-as-guest");
                for(int i=0;i<collection.Samples.Length;i++)
                {
                    // Solve before READY so computation does not determine the comparison attempt time.
                    bool comparison=mode!="offline";
                    string objectId=collection.Samples[i].ObjectId;
                    var mass=collection.Samples[i].Definition().Mass;decimal lo=-1.35m,hi=1.35m;
                    decimal leftTarget=mass.TotalMass*(comparison&&objectId=="B01"?.50005m:.5m);
                    for(int j=0;j<60;j++){decimal mid=(lo+hi)/2;if(mass.Split(mid).Left<leftTarget)lo=mid;else hi=mid;}
                    if(!collection.Select(i)){Fail("Content locked: "+i);yield break;}
                    var game=collection.Game;
                    var point=game.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));
                    long target=objectId=="B01"?80:objectId=="B03"?26:objectId=="A04"?67:200;
                    // New real CUT times must improve remote bests, so reconnect proves more than replaying cached scores.
                    if(mode=="network-offline" && (objectId=="B01" || objectId=="A04"))target--;
                    bool captured=false;
                    for(int attempt=0;attempt<(comparison?8:1);attempt++)
                    {
                        deadline=Time.realtimeSinceStartupAsDouble+5;
                        while(game.Session.Phase!=AttemptPhase.Ready){if(Time.realtimeSinceStartupAsDouble>deadline){Fail("READY timeout");yield break;}yield return null;}
                        if(comparison&&Elapsed(game)>target){game.Retry();continue;}
                        if(!game.BeginPointer(42,point)){Fail("Begin CUT failed");yield break;}
                        if(comparison)
                        {
                            while(Elapsed(game)<target-18)yield return null;
                            if(Elapsed(game)>target){game.Retry();continue;}
                            // At most 18 ms of real wall-clock waiting; no injected clock or synthetic result.
                            while(Elapsed(game)<target){}
                        }
                        if(!game.ReleasePointer(42,point)){Fail("Release CUT failed");yield break;}
                        yield return new WaitForSecondsRealtime(.65f);
                        if(!comparison || game.Session.Result.Snapshot.ElapsedMilliseconds==target){captured=true;break;}
                        game.Retry();
                    }
                    if(!captured){Fail("Could not capture actual target millisecond: "+objectId);yield break;}
                    if(collection.Progress.Best(i)==null){Fail("Local best missing: "+i);yield break;}
                    if(comparison)
                    {
                        var best=collection.Progress.Best(i);int accuracy=objectId=="B01"?9999:10000;
                        if(best.AccuracyUnits!=accuracy || best.ElapsedMilliseconds!=target){Fail("Comparison CUT differs from target: "+objectId);yield break;}
                    }
                }
                yield return new WaitForSecondsRealtime(1);
                evidence.pendingBefore=evidence.pendingAfter=controller.PendingCount;
                evidence.success=controller.PendingCount==3 && !collection.SavePending && !controller.Queue.SavePending;
                Finish();yield break;
            }
            evidence.pendingBefore=controller.PendingCount;
            if(!controller.Configured){Fail("Online configuration is disabled");yield break;}
            if(collection.Progress.UnlockedCount!=10 || Enumerable.Range(0,10).Any(i=>collection.Progress.Best(i)==null))
            {Fail("Run isolated offline mode before service verification");yield break;}
            SetStage("awaiting-game-center-authentication");
            var login=controller.Flow.Login();yield return Wait(login,120);
            if(evidence.error!=null){Finish();yield break;}
            evidence.login=controller.Flow.LastLogin.ToString();evidence.signedIn=controller.Flow.IsSignedIn;
            if(!evidence.signedIn){Fail("Native authentication outcome: "+evidence.login);yield break;}
            using(var hash=SHA256.Create())evidence.playerToken=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(controller.Flow.ActivePlayerId))).Replace("-","").Substring(0,16);
            SetStage("flushing-persisted-real-cut-records");
            var flush=controller.Flow.Flush();yield return Wait(flush,120);
            deadline=Time.realtimeSinceStartupAsDouble+120;
            while(controller.Flow.IsFlushing){if(Time.realtimeSinceStartupAsDouble>deadline){Fail("Queue flush timeout");yield break;}yield return null;}
            evidence.pendingAfter=controller.PendingCount;
            if(evidence.error!=null){Finish();yield break;}
            foreach(var mapping in controller.Configuration.Mappings)
            {
                int index=collection.Progress.Content.ToList().FindIndex(d=>d.Id==mapping.objectId);
                var best=collection.Progress.Best(index);
                var board=new BoardEvidence{objectId=mapping.objectId,contentVersion=mapping.contentVersion,leaderboardId=mapping.leaderboardId,accuracyUnits=best.AccuracyUnits,timeMs=best.ElapsedMilliseconds,expectedScore=RankingScore.Encode(best.AccuracyUnits,best.ElapsedMilliseconds)};
                boards.Add(board);SetStage("querying-"+mapping.objectId);
                RankingPage page=null;
                for(int retry=0;retry<3;retry++)
                {
                    var load=controller.Flow.Load(mapping.objectId,mapping.contentVersion);yield return Wait(load,90);
                    if(evidence.error!=null){board.error=evidence.error;break;}
                    page=load.Result;
                    if(page?.LocalPlayer?.Score==board.expectedScore)break;
                    yield return new WaitForSecondsRealtime(2);
                }
                if(page==null){board.error=board.error??"No authenticated ranking page";continue;}
                board.totalPlayers=page.TotalPlayers;board.localRank=page.LocalPlayer?.Rank??0;board.localScore=page.LocalPlayer?.Score??-1;
                board.localMatches=board.localRank>0 && board.localScore==board.expectedScore;
                board.entries=(page.Entries??Array.Empty<RankingEntry>()).Select(e=>new RemoteRow{rank=e.Rank,score=e.Score}).ToArray();
                // These representative QA boards contain only valid scores and the full top-two population.
                // Validate game rank independently of the normalizer; native ordinals1/2 must fail a tied score.
                board.sharedRanksValid=board.entries.All(e=>e.rank==1+board.entries.LongCount(other=>other.score<e.score));
                if(board.entries.Any(e=>e.score==board.localScore))
                    board.sharedRanksValid &= board.localRank==1+board.entries.LongCount(e=>e.score<board.localScore);
                {
                    var ranking=collection.Game.GetComponent<RankingPanel>();ranking.Open();
                    ranking.ObjectButtons[Array.IndexOf(new[]{"B01","B03","A04"},mapping.objectId)].onClick.Invoke();
                    var reload=ranking.Reload();yield return Wait(reload,120);
                    yield return new WaitForEndOfFrame();Capture("ranking-actual-"+mode+"-"+mapping.objectId+".png");ranking.Close();
                }
            }
            evidence.success=evidence.error==null && evidence.signedIn && controller.PendingCount==0 && boards.Count==3 && boards.All(b=>b.localMatches&&b.sharedRanksValid);
            Finish();
        }
        static long Elapsed(AppleGame game)=>game.Session.Elapsed((long)(Time.realtimeSinceStartupAsDouble*1000));
        IEnumerator Wait(Task task,double seconds)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+seconds;
            while(!task.IsCompleted){if(Time.realtimeSinceStartupAsDouble>deadline){evidence.error="Probe wait timeout";yield break;}yield return null;}
            if(task.IsFaulted)evidence.error=task.Exception.GetBaseException().GetType().Name+": "+task.Exception.GetBaseException().Message;
            else if(task.IsCanceled)evidence.error="Task cancelled";
        }
        void SetStage(string stage){evidence.stage=stage;Write("status.json");Debug.Log("TASK4_GAME_CENTER_STAGE "+stage);}
        void Fail(string error){evidence.error=error;evidence.success=false;Finish();}
        void Finish()
        {
            evidence.stage=evidence.success?"completed":"incomplete";
            evidence.bests=Enumerable.Range(0,collection?.Samples.Length??0).Count(i=>collection.Progress.Best(i)!=null);
            evidence.unlocked=collection?.Progress.UnlockedCount??0;evidence.pendingAfter=controller?.PendingCount??0;
            evidence.queueDurable=controller?.Queue?.SavePending==false && collection?.SavePending==false;
            evidence.boards=boards.ToArray();Write(mode+".json");Write("status.json");
            Debug.Log("TASK4_GAME_CENTER_PROBE_FINISHED "+mode+" success="+evidence.success);
        }
        void Write(string name){File.WriteAllText(Path.Combine(folder,name),JsonUtility.ToJson(evidence,true));}
        void Capture(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(folder,name),texture.EncodeToPNG());}finally{Destroy(texture);}
        }
        void Log(string _,string __,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)evidence.errors++;if(type==LogType.Warning)evidence.warnings++;}
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
    }
}
#endif
