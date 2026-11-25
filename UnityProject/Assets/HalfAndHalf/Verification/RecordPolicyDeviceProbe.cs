#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_RECORD_POLICY_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Opt-in device verification. Source QA and normal player files are read-only.
    public sealed class RecordPolicyDeviceProbe : MonoBehaviour
    {
        const string ApprovedPlayer = "D6BA5846987C173C";
        const string SourceRun = "fullonline-20261019-01";
        [Serializable] public sealed class Guard { public string board; public long score; public bool valid; }
        [Serializable] public sealed class Remote { public string id; public long submitted,server,rank; public bool acceptedBest; }
        [Serializable] public sealed class Proof
        {
            public string mode,stage,error,device,os,playerToken,sourceRun,sourceHashBefore,sourceHashAfter,queueHashBefore,queueHashAfter;
            public bool success,normalPreserved,sourcePreserved,authenticated,restored,queueDurable,freshUnchanged,storageNoticeVisible,productionOverallEnabled;
            public int records,unlocked,pendingBefore,pendingAfter,errors,warnings;
            public string[] normalBefore,normalAfter;
            public Guard[] nativeChecks; public Remote[] remote;
            public string localLabel,selectedLabel,storageNotice;
            public SubmissionAttempt newCut;
        }
        MvpCollection collection;
        GameCenterController controller;
        SubmissionQueue queue;
        AppleGameCenter native;
        GameCenterFlow flow;
        Proof proof;
        string folder,path;
        readonly List<Guard> checks = new List<Guard>();
        readonly List<Remote> remote = new List<Remote>();
        string SourcePath => Path.Combine(Application.persistentDataPath,SourceRun,"progress.json");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            string mode=Environment.GetEnvironmentVariable("HALF_RECORD_POLICY_QA");
            if(mode!="prepare"&&mode!="submit"&&mode!="fresh")return;
            string run=Environment.GetEnvironmentVariable("HALF_RECORD_POLICY_RUN");
            if(string.IsNullOrEmpty(run)||!run.StartsWith("recordpolicy-")||run.Length>48||run.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))
                throw new ArgumentException("Explicit isolated QA run required");
            var go=new GameObject("Record policy isolated device QA");DontDestroyOnLoad(go);
            var probe=go.AddComponent<RecordPolicyDeviceProbe>();
            probe.folder=Path.Combine(Application.persistentDataPath,run);
            probe.path=Path.Combine(probe.folder,"progress.json");
            Directory.CreateDirectory(probe.folder);
            probe.proof=new Proof{mode=mode,stage="starting",device=SystemInfo.deviceModel,os=SystemInfo.operatingSystem,
                normalBefore=NormalHashes(),sourceRun=SourceRun,sourceHashBefore=FileHash(probe.SourcePath),queueHashBefore=FileHash(probe.SourcePath+".queue")};
            SceneManager.sceneLoaded+=probe.Loaded;Application.logMessageReceived+=probe.Log;
        }
        static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","");}
        static string FileHash(string file)=>File.Exists(file)?Hash(File.ReadAllBytes(file)):"absent";
        static string Token(string player)=>Hash(Encoding.UTF8.GetBytes(player??"")).Substring(0,16);
        static string[] NormalHashes()=>new[]{"mvp-progress.json","mvp-progress.json.bak","game-center-queue.json","game-center-queue.json.bak"}
            .Select(f=>f+":"+FileHash(Path.Combine(Application.persistentDataPath,f))).ToArray();
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            collection=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();
            if(collection==null)return;
            controller=collection.GetComponent<GameCenterController>();
            proof.productionOverallEnabled=controller.Configuration.EnableOverallAfterServiceVerification;
            // Disable automatic queue reconciliation/submission for the two-candidate probe.
            // Fresh mode uses the normal controller with an empty isolated collection.
            controller.enabled=proof.mode=="fresh";
            collection.SavePath=path;
            if(proof.mode=="prepare")
            {
                if(File.Exists(path)||File.Exists(Path.Combine(folder,"candidates.json")))
                {Fail("Fresh prepare folder required; existing QA preserved");return;}
                if(!File.Exists(SourcePath)){Fail("Previous actual CUT source is unavailable");return;}
                File.Copy(SourcePath,path);
            }
            StartCoroutine(SafeRun());
        }
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)proof.errors++;if(type==LogType.Warning)proof.warnings++;}
        void Save()
        {
            proof.records=collection?.Progress?.RecordedCount??0;proof.unlocked=collection?.Progress?.UnlockedCount??0;
            proof.pendingAfter=queue?.Entries.Count(e=>e.pending)??controller?.PendingCount??0;
            proof.nativeChecks=checks.ToArray();proof.remote=remote.ToArray();
            File.WriteAllText(Path.Combine(folder,proof.mode+".json"),JsonUtility.ToJson(proof,true));
            Debug.Log("RECORD_POLICY_QA "+proof.mode+" "+proof.stage+(proof.error==null?"":" "+proof.error));
        }
        void Stage(string stage){proof.stage=stage;Save();}
        void Fail(string error){proof.error=error;proof.success=false;proof.stage="failed";Save();}
        void Require(bool value,string error){if(!value)throw new InvalidOperationException(error);}
        IEnumerator SafeRun()
        {
            var run=Run();
            // Flatten nested coroutines so verification exceptions are recorded without touching saves.
            var stack=new Stack<IEnumerator>();stack.Push(run);
            while(stack.Count>0)
            {
                object value=null;bool next=false;Exception failure=null;
                try{next=stack.Peek().MoveNext();if(next)value=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){Fail(failure.ToString());yield break;}
                if(!next){stack.Pop();continue;}
                if(value is IEnumerator nested)stack.Push(nested);else yield return value;
            }
        }
        IEnumerator Await(Task task)
        {while(!task.IsCompleted)yield return null;if(task.IsFaulted)throw task.Exception.GetBaseException();if(task.IsCanceled)throw new Exception("Operation cancelled");}
        IEnumerator Run()
        {
            yield return null;
            Require(collection.Samples.Length==29&&collection.SavePath==path&&proof.productionOverallEnabled,"Production content/configuration or QA isolation missing");
            var definitions=collection.Progress.Content;
            var catalog=controller.Configuration.Catalog();
            var overallVersion=OverallRecord.VersionFor(definitions);
            if(proof.mode=="fresh")
            {
                Require(collection.Progress.RecordedCount==0&&collection.Progress.UnlockedCount==1&&collection.Progress.OverallBest==null,"Fresh QA already has progress");
                while(controller.Flow==null)yield return null;
                Stage("native-authentication-read-only");yield return Await(controller.Flow.Login());
                Require(controller.Flow.IsSignedIn,"Native authentication unavailable; no account changes attempted");
                proof.playerToken=Token(controller.Flow.ActivePlayerId);proof.authenticated=true;
                Require(proof.playerToken==ApprovedPlayer,"Active player differs from approved comparison QA account; no submission");
                Stage("existing-server-overall-with-no-local-progress");
                var load=controller.Flow.Load(OverallRecord.Id,overallVersion);yield return Await(load);
                Require(load.Result?.LocalPlayer!=null,"Existing server overall record unavailable");
                remote.Add(new Remote{id=OverallRecord.Id,server=load.Result.LocalPlayer.Score,rank=load.Result.LocalPlayer.Rank,acceptedBest=load.Result.LocalPlayer.Rank>0});
                var ranking=collection.GetComponent<RankingPanel>();ranking.Open();yield return null;
                ranking.ObjectButtons[2].onClick.Invoke();yield return null;
                yield return Await(ranking.Reload());yield return null;
                proof.localLabel=ranking.LocalRecord.text;proof.selectedLabel=ranking.Selected.text;
                Require(proof.localLabel.Contains("이 기기 기록")&&proof.localLabel.Contains("종합 미완성")&&proof.localLabel.Contains("Game Center 기록")&&!proof.localLabel.EndsWith("--"),"Device/server record labels missing");
                yield return Capture("fresh-overall.png");ranking.Close();yield return null;
                var settings=collection.GetComponent<GameSettings>();settings.OpenButton.onClick.Invoke();yield return null;
                var notice=settings.Panel.transform.Find("StorageNotice")?.GetComponent<TMPro.TMP_Text>();
                proof.storageNotice=notice?.text;proof.storageNoticeVisible=notice!=null&&notice.gameObject.activeInHierarchy&&settings.Panel.activeSelf;
                Require(proof.storageNoticeVisible&&proof.storageNotice.Contains("기기 간 동기화")&&proof.storageNotice.Contains("자동 복원"),"Storage notice missing");
                yield return Capture("storage-notice.png");settings.CloseButton.onClick.Invoke();yield return null;
                new LocalProgressStore(path).Save(collection.Progress);
                var restored=new LocalProgressStore(path).Load(definitions);
                proof.freshUnchanged=restored.RecordedCount==0&&restored.UnlockedCount==1&&restored.OverallBest==null&&controller.PendingCount==0;
                Require(proof.freshUnchanged,"Server query changed local progress");
                Finish();yield break;
            }
            Require(collection.Progress.RecordedCount==29&&collection.Progress.OverallBest!=null,"Previous valid 29 actual CUT records did not restore");
            var oldOverall=collection.Progress.OverallBest;
            Require(OverallRecord.Calculate(definitions,oldOverall.Records,oldOverall.Owners)?.Score==oldOverall.Score,"Reused 29 sources failed recalculation");
            Require(oldOverall.TrySubmissionOwner(out var sourceOwner)&&Token(sourceOwner)==ApprovedPlayer,"Reused overall owner differs from approved QA account");
            string candidatePath=Path.Combine(folder,"candidates.json");
            queue=new SubmissionQueue(candidatePath,definitions,overallVersion);
            native=new AppleGameCenter(true,catalog.Resolve(OverallRecord.Id,overallVersion),(board,score,player)=>
            {
                bool valid=Token(player)==ApprovedPlayer&&queue.Entries.Any(e=>e.score==score&&catalog.Resolve(e.objectId,e.contentVersion)==board&&queue.CanSubmit(e,player));
                checks.Add(new Guard{board=board,score=score,valid=valid});return valid;
            });
            flow=new GameCenterFlow(native,queue,catalog);
            Stage("native-authentication");yield return Await(flow.Login());
            Require(flow.IsSignedIn,"Native authentication unavailable; submission deferred");
            proof.playerToken=Token(flow.ActivePlayerId);proof.authenticated=true;
            Require(proof.playerToken==ApprovedPlayer,"Active player differs from approved QA account; submission deferred");
            if(proof.mode=="prepare")
            {
                Require(queue.Entries.Length==0,"Candidate queue must be new");
                collection.RecordOwner=()=>native.PlayerId;
                Require(collection.Select(0),"Cannot select apple");
                var game=collection.Game;double deadline=Time.realtimeSinceStartupAsDouble+15;
                while(game.Session.Phase!=AttemptPhase.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Require(game.Session.Phase==AttemptPhase.Ready,"READY timeout");
                yield return new WaitForSecondsRealtime(.25f);
                AttemptResult actual=null;Action<AttemptResult> capture=r=>actual=r;game.AttemptCompleted+=capture;
                var d=definitions[0];long time=game.Session.Elapsed((long)(Time.realtimeSinceStartupAsDouble*1000));var mass=d.MassAt(time);
                decimal lo=-1.35m,hi=1.35m;
                // A normal non-perfect cut keeps the earlier QA best/aggregate intact.
                for(int i=0;i<24;i++){decimal mid=(lo+hi)/2;if(mass.Split(mid).Left<mass.TotalMass*.49m)lo=mid;else hi=mid;}
                var point=game.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));
                Stage("one-actual-ready-cut");
                Require(game.BeginPointer(951,point)&&game.ReleasePointer(951,point),"Actual input CUT rejected");
                deadline=Time.realtimeSinceStartupAsDouble+10;
                while(actual==null&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                game.AttemptCompleted-=capture;
                Require(actual!=null&&CollectionProgress.Valid(d,actual),"Actual CUT did not produce a valid attempt");
                proof.newCut=SubmissionAttempt.From(actual,native.PlayerId);
                File.WriteAllText(Path.Combine(folder,"new-cut.json"),JsonUtility.ToJson(proof.newCut,true));
                Require(queue.Offer(actual,native.PlayerId)&&queue.ReconcileOverall(oldOverall),"Two candidates were not created");
                Require(queue.Persist(),"Candidate save failed");
                new LocalProgressStore(path).Save(collection.Progress);
                Require(queue.Entries.Length==2&&queue.Entries.All(e=>queue.CanSubmit(e,native.PlayerId)),"Saved candidate evidence invalid");
                proof.pendingBefore=2;proof.queueDurable=!queue.SavePending;
                yield return Capture("actual-cut.png");
            }
            else
            {
                proof.restored=true;proof.pendingBefore=queue.Entries.Count(e=>e.pending);
                Require(proof.pendingBefore==2&&queue.Entries.Any(e=>e.objectId=="B01"&&e.evidence.Length==1)&&queue.Entries.Any(e=>e.objectId==OverallRecord.Id&&e.evidence.Length==29),"Both persisted candidates and independent evidence must restore");
                proof.newCut=JsonUtility.FromJson<SubmissionAttempt>(File.ReadAllText(Path.Combine(folder,"new-cut.json")));
                Require(queue.Entries.Where(e=>e.objectId=="B01").Single().evidence[0].knifeX==proof.newCut.knifeX,"Restored individual candidate is not the new actual CUT");
                var pending=queue.Entries;
                Stage("flush-two-candidates-through-native-final-guard");yield return Await(flow.Flush());
                Require(!flow.SubmissionFailed&&queue.Entries.All(e=>!e.pending),"Actual submission incomplete; queue preserved");
                Require(checks.Count==4&&checks.All(c=>c.valid),"Expected native pre-load and final-submit checks for both candidates");
                foreach(var candidate in pending)
                {
                    Stage("query-"+candidate.objectId);
                    var load=flow.Load(candidate.objectId,candidate.contentVersion);yield return Await(load);
                    var entry=load.Result?.LocalPlayer;
                    var row=new Remote{id=candidate.objectId,submitted=candidate.score,server=entry?.Score??-1,rank=entry?.Rank??0,
                        acceptedBest=entry!=null&&entry.Rank>0&&entry.Score<=candidate.score};
                    remote.Add(row);Require(row.acceptedBest,"Server best missing or worse than accepted candidate");
                }
                var persisted=new SubmissionQueue(candidatePath,definitions,overallVersion);
                Require(persisted.Entries.Length==2&&persisted.Entries.All(e=>!e.pending),"Submitted watermarks did not persist");
                proof.queueDurable=!queue.SavePending;
            }
            Finish();
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(folder,name),texture.EncodeToPNG());}finally{Destroy(texture);}
        }
        void Finish()
        {
            proof.normalAfter=NormalHashes();proof.normalPreserved=proof.normalBefore.SequenceEqual(proof.normalAfter);
            proof.sourceHashAfter=FileHash(SourcePath);proof.queueHashAfter=FileHash(SourcePath+".queue");
            proof.sourcePreserved=proof.sourceHashBefore==proof.sourceHashAfter&&proof.queueHashBefore==proof.queueHashAfter;
            Require(proof.normalPreserved&&proof.sourcePreserved&&proof.errors==0,"Preservation or device console check failed");
            proof.success=true;proof.stage="complete";Save();
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
    }
}
#endif
