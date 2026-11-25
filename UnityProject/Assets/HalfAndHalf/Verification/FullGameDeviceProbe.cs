#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_FULL_GAME_QA
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Included only in the explicit development QA build; inert without opt-in launch environment.
    public sealed class FullGameDeviceProbe:MonoBehaviour
    {
        [Serializable] public sealed class HashRow {public string file,sha256;}
        [Serializable] public sealed class CutRow {public string id;public int accuracy;public long time;public bool valid,freeze,retry;}
        [Serializable] public sealed class Proof
        {
            public bool success,onlineDisabled,normalSavePreserved,aggregate28Excluded,aggregate29Created,navigation,restored;
            public string mode,stage,error,device,os,savePath,fingerprint;public int records,errors,warnings;public CutRow[] cuts;public HashRow[] normalBefore,normalAfter;
        }
        string mode,folder,path;
        MvpCollection collection;
        Proof proof;
        readonly List<CutRow> cuts=new List<CutRow>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Begin()
        {
            if(Application.isEditor)return;
            string mode=Environment.GetEnvironmentVariable("HALF_FULL_GAME_QA");if(mode!="smoke"&&mode!="restore")return;
            string run=Environment.GetEnvironmentVariable("HALF_FULL_GAME_QA_RUN");
            if(string.IsNullOrEmpty(run)||run.Length>48||run.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))throw new ArgumentException("QA run ID required");
            var go=new GameObject("Full29 isolated device QA");DontDestroyOnLoad(go);var p=go.AddComponent<FullGameDeviceProbe>();
            p.mode=mode;p.folder=Path.Combine(Application.persistentDataPath,run);p.path=Path.Combine(p.folder,"progress.json");
            p.proof=new Proof{mode=mode,stage="starting",device=SystemInfo.deviceModel,os=SystemInfo.operatingSystem,savePath=p.path,normalBefore=NormalHashes()};
            Directory.CreateDirectory(p.folder);SceneManager.sceneLoaded+=p.Loaded;Application.logMessageReceived+=p.Log;
        }
        static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","");}
        static HashRow[] NormalHashes()=>new[]{"mvp-progress.json","mvp-progress.json.bak","mvp-progress.json.tmp","game-center-queue.json","game-center-queue.json.bak","game-center-queue.json.tmp"}.Select(file=>{string path=Path.Combine(Application.persistentDataPath,file);return new HashRow{file=file,sha256=File.Exists(path)?Hash(File.ReadAllBytes(path)):"absent"};}).ToArray();
        static string Fingerprint(CollectionProgress p)
        {
            var rows=new List<string>();
            for(int i=0;i<p.Content.Count;i++){
                var r=p.Best(i);if(r==null){rows.Add(p.Content[i].Id+":missing");continue;}
                var s=r.Snapshot;rows.Add(string.Join("|",s.ObjectId,s.ContentVersion,s.KnifeX.ToString(CultureInfo.InvariantCulture),r.LeftMass.ToString(CultureInfo.InvariantCulture),r.RightMass.ToString(CultureInfo.InvariantCulture),r.AccuracyUnits.ToString(),r.ElapsedMilliseconds.ToString(),p.BestOwner(i)));
            }
            var o=p.OverallBest;if(o!=null)rows.Add(o.Version+"|"+o.Score+"|"+string.Join(",",o.Records.Select(r=>r.AccuracyUnits+":"+r.ElapsedMilliseconds)));
            return Hash(Encoding.UTF8.GetBytes(string.Join("\n",rows)));
        }
        static AttemptResult Good(ObjectDefinition d,long time)
        {
            var map=d.MassAt(time);decimal lo=-1.35m,hi=1.35m;
            for(int j=0;j<30;j++){decimal x=(lo+hi)/2;if(map.Split(x).Left<map.TotalMass/2)lo=x;else hi=x;}
            return Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,(lo+hi)/2,time,TransformState.Identity,true));
        }
        void Loaded(Scene scene,LoadSceneMode load)
        {
            collection=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();if(collection==null)return;
            collection.SavePath=path;var gc=collection.GetComponent<GameCenterController>();
            var config=Instantiate(gc.Configuration);config.EnableOnline=false;config.EnableOverallAfterServiceVerification=false;gc.Configuration=config;
            StartCoroutine(Run());
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)proof.errors++;if(type==LogType.Warning)proof.warnings++;}
        void Save(string file){proof.cuts=cuts.ToArray();File.WriteAllText(Path.Combine(folder,file),JsonUtility.ToJson(proof,true));}
        bool Check(bool condition,string message)
        {
            if(condition)return true;proof.success=false;proof.stage="failed";proof.error=message;Save("failure.json");Debug.LogError("FULL29_QA_FAILED "+message);return false;
        }
        void Stage(string stage){proof.stage=stage;Save("status.json");}
        IEnumerator Run()
        {
            yield return null;
            var gc=collection.GetComponent<GameCenterController>();
            proof.onlineDisabled=!gc.Configured;
            if(!Check(collection.SavePath==path&&collection.Samples.Length==29&&proof.onlineDisabled,"Isolation/29-content missing"))yield break;
            if(mode=="restore"){
                Stage("restore");
                string expected=File.ReadAllText(Path.Combine(folder,"fingerprint.txt"));
                proof.fingerprint=Fingerprint(collection.Progress);proof.records=collection.Progress.RecordedCount;
                proof.restored=proof.records==29&&collection.Progress.OverallBest!=null&&expected==proof.fingerprint;
                proof.normalAfter=NormalHashes();proof.normalSavePreserved=proof.normalBefore.Zip(proof.normalAfter,(a,b)=>a.file==b.file&&a.sha256==b.sha256).All(v=>v);
                proof.success=proof.restored&&proof.normalSavePreserved&&proof.errors==0;proof.stage=proof.success?"restore-complete":"failed";Save("restore.json");yield break;
            }
            if(!Check(!File.Exists(path),"Use a fresh QA run folder"))yield break;
            Stage("seed-28-fixtures");var defs=collection.Progress.Content.ToArray();
            var p=CollectionProgress.Restore(defs,defs.Take(28).Select(d=>Good(d,1000)),28);
            new LocalProgressStore(path).Save(p);collection.Reload();
            proof.aggregate28Excluded=collection.Progress.RecordedCount==28&&collection.Progress.OverallBest==null;
            if(!Check(proof.aggregate28Excluded,"Partial aggregate incorrectly eligible"))yield break;
            foreach(int index in new[]{28,0,19,21,24,25,27}){
                Stage("cut-"+defs[index].Id);yield return Cut(index);if(proof.error!=null)yield break;
            }
            proof.records=collection.Progress.RecordedCount;proof.aggregate29Created=proof.records==29&&collection.Progress.OverallBest?.Records.Count==29;
            if(!Check(proof.aggregate29Created&&!collection.SavePending,"Full aggregate/durable save missing"))yield break;
            Stage("navigation");collection.Open();yield return null;
            if(!Check(collection.Items.Length==29&&collection.Panel.activeSelf,"Collection not reachable"))yield break;collection.Close();
            var rank=collection.GetComponent<RankingPanel>();rank.Open();yield return null;rank.ObjectButtons[2].onClick.Invoke();yield return null;
            if(!Check(rank.Panel.activeSelf&&rank.OverallSelected&&!gc.OverallConfigured,"Aggregate status/navigation incorrect"))yield break;rank.Close();
            var settings=collection.GetComponent<GameSettings>();settings.OpenButton.onClick.Invoke();yield return null;
            var phase=collection.Game.Session.Phase;collection.Game.Hud.RetryButton.onClick.Invoke();
            if(!Check(settings.Panel.activeSelf&&collection.Game.Session.Phase==phase,"Modal input leak"))yield break;
            settings.CloseButton.onClick.Invoke();proof.navigation=!collection.Game.UiOpen;
            proof.normalAfter=NormalHashes();proof.normalSavePreserved=proof.normalBefore.Zip(proof.normalAfter,(a,b)=>a.file==b.file&&a.sha256==b.sha256).All(v=>v);
            proof.fingerprint=Fingerprint(collection.Progress);File.WriteAllText(Path.Combine(folder,"fingerprint.txt"),proof.fingerprint);
            proof.success=proof.aggregate28Excluded&&proof.aggregate29Created&&proof.navigation&&proof.normalSavePreserved&&proof.errors==0;
            proof.stage=proof.success?"smoke-complete":"failed";Save("smoke.json");
        }
        IEnumerator Cut(int index)
        {
            if(!Check(collection.Select(index),"Selection rejected "+index))yield break;
            var g=collection.Game;double deadline=Time.realtimeSinceStartupAsDouble+15;
            while(g.Session.Phase!=AttemptPhase.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(!Check(g.Session.Phase==AttemptPhase.Ready,"READY timeout "+index))yield break;
            yield return new WaitForSecondsRealtime(.15f);
            long elapsed=g.Session.Elapsed((long)(Time.realtimeSinceStartupAsDouble*1000));
            var target=Good(collection.Progress.Content[index],elapsed);
            var point=g.GameCamera.WorldToScreenPoint(new Vector3((float)target.Snapshot.KnifeX,0,0));
            if(!Check(g.BeginPointer(741,point)&&g.ReleasePointer(741,point),"CUT rejected "+index))yield break;
            var r=g.Session.Result;bool dynamic=index>=20;var visual=g.Presentation.Space;
            bool frozen=!dynamic||visual.FrozenMilliseconds==r.ElapsedMilliseconds;
            yield return new WaitForSecondsRealtime(.65f);
            bool valid=CollectionProgress.Valid(collection.Progress.Content[index],r);
            frozen&=!dynamic||visual.DisplayedMilliseconds==r.ElapsedMilliseconds;
            if(!Check(valid&&frozen&&g.Session.Phase==AttemptPhase.Result&&g.Presentation.FragmentCount==2,"Result/freeze invalid "+index))yield break;
            g.Retry();bool retry=g.Session.Phase==AttemptPhase.Dropping&&(!dynamic||visual.FrozenMilliseconds==-1&&visual.DisplayedMilliseconds==0);
            cuts.Add(new CutRow{id=r.Snapshot.ObjectId,accuracy=r.AccuracyUnits,time=r.ElapsedMilliseconds,valid=valid,freeze=frozen,retry=retry});
            Check(retry,"Retry failed "+index);
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
    }
}
#endif
