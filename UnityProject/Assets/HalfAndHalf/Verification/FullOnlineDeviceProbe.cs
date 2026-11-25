#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_FULL_ONLINE_QA
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
 // Only opt-in development QA; actual READY/CUT clock and isolated local files.
 public sealed class FullOnlineDeviceProbe:MonoBehaviour
 {
  [Serializable] public sealed class Row{public string id,version,board,error;public int accuracy;public long time,expected,remote,rank,players;public bool matches;}
  [Serializable] public sealed class Proof{public string mode,stage,error,playerToken,device,os,overallVersion;public bool success,signedIn,normalPreserved,restored,queueDurable,productionOverallEnabled;public int records,pendingBefore,pendingAfter,errors,warnings;public Row[] boards;public string[] normalBefore,normalAfter;}
  MvpCollection collection;GameCenterController gc;Proof p;string folder,path;readonly List<Row> rows=new List<Row>();
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Begin(){string mode=Environment.GetEnvironmentVariable("HALF_FULL_ONLINE_QA");if(mode!="record"&&mode!="verify"&&mode!="restore")return;string run=Environment.GetEnvironmentVariable("HALF_FULL_ONLINE_RUN");if(string.IsNullOrEmpty(run)||run.Length>48||run.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Fresh QA run ID required");var go=new GameObject("Full online isolated QA");DontDestroyOnLoad(go);var probe=go.AddComponent<FullOnlineDeviceProbe>();probe.folder=Path.Combine(Application.persistentDataPath,run);probe.path=Path.Combine(probe.folder,"progress.json");Directory.CreateDirectory(probe.folder);probe.p=new Proof{mode=mode,stage="starting",normalBefore=NormalHashes(),device=SystemInfo.deviceModel,os=SystemInfo.operatingSystem};SceneManager.sceneLoaded+=probe.Loaded;Application.logMessageReceived+=probe.Log;}
  static string Hash(byte[] bytes){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-","");}
  static string[] NormalHashes()=>new[]{"mvp-progress.json","mvp-progress.json.bak","game-center-queue.json","game-center-queue.json.bak"}.Select(f=>{string q=Path.Combine(Application.persistentDataPath,f);return f+":"+(File.Exists(q)?Hash(File.ReadAllBytes(q)):"absent");}).ToArray();
  void Loaded(Scene scene,LoadSceneMode mode){collection=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();if(collection==null)return;collection.SavePath=path;gc=collection.GetComponent<GameCenterController>();p.productionOverallEnabled=gc.Configuration.EnableOverallAfterServiceVerification;var cfg=Instantiate(gc.Configuration);cfg.EnableOnline=p.mode!="record";cfg.EnableOverallAfterServiceVerification=p.mode=="verify"||p.productionOverallEnabled;gc.Configuration=cfg;StartCoroutine(Run());}
  void Log(string m,string stack,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)p.errors++;if(t==LogType.Warning)p.warnings++;}
  void Save(string file){p.boards=rows.ToArray();p.records=collection?.Progress?.RecordedCount??0;p.pendingAfter=gc?.PendingCount??0;File.WriteAllText(Path.Combine(folder,file),JsonUtility.ToJson(p,true));}
  void Stage(string stage){p.stage=stage;Save("status.json");}
  bool Check(bool value,string error){if(value)return true;p.error=error;p.success=false;p.stage="failed";Save(p.mode+".json");return false;}
  IEnumerator Await(Task t){while(!t.IsCompleted)yield return null;if(t.IsFaulted)p.error=t.Exception.GetBaseException().ToString();if(t.IsCanceled)p.error="Cancelled";}
  void Finish(){p.records=collection.Progress.RecordedCount;p.normalAfter=NormalHashes();p.normalPreserved=p.normalBefore.SequenceEqual(p.normalAfter);p.queueDurable=!collection.SavePending&&!gc.Queue.SavePending;p.success=p.error==null&&p.normalPreserved&&p.queueDurable&&p.errors==0&&(p.mode=="record"?p.records==29:rows.Count==30&&rows.All(r=>r.matches)&&gc.PendingCount==0);p.stage=p.success?"complete":"failed";Save(p.mode+".json");}
  IEnumerator Run(){yield return null;if(!Check(collection.Samples.Length==29&&collection.SavePath==path,"29 content/isolation missing"))yield break;p.overallVersion=OverallRecord.VersionFor(collection.Progress.Content);
   if(p.mode=="record"){
    if(!Check(!File.Exists(path),"Use a fresh run folder; never overwrite existing QA records"))yield break;
    for(int i=0;i<29;i++){
     var d=collection.Progress.Content[i];Stage("actual-cut-"+d.Id);if(!Check(collection.Select(i),"Locked "+d.Id))yield break;var g=collection.Game;double end=Time.realtimeSinceStartupAsDouble+15;while(g.Session.Phase!=AttemptPhase.Ready&&Time.realtimeSinceStartupAsDouble<end)yield return null;if(!Check(g.Session.Phase==AttemptPhase.Ready,"READY timeout"))yield break;
     yield return new WaitForSecondsRealtime(.15f);long time=g.Session.Elapsed((long)(Time.realtimeSinceStartupAsDouble*1000));var map=d.MassAt(time);decimal lo=-1.35m,hi=1.35m;for(int j=0;j<25;j++){decimal mid=(lo+hi)/2;if(map.Split(mid).Left<map.TotalMass/2)lo=mid;else hi=mid;}
     var point=g.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));if(!Check(g.BeginPointer(942,point)&&g.ReleasePointer(942,point),"Actual CUT rejected "+d.Id))yield break;yield return new WaitForSecondsRealtime(.65f);var r=collection.Progress.Best(i);if(!Check(r!=null&&r.AccuracyUnits>=9000,"CUT must unlock next content "+d.Id))yield break;rows.Add(new Row{id=d.Id,version=d.ContentVersion,accuracy=r.AccuracyUnits,time=r.ElapsedMilliseconds,expected=RankingScore.Encode(r.AccuracyUnits,r.ElapsedMilliseconds),matches=CollectionProgress.Valid(d,r)});
    }
    yield return new WaitForSecondsRealtime(1);p.pendingBefore=gc.PendingCount;if(!Check(collection.Progress.OverallBest!=null,"Aggregate missing"))yield break;Finish();yield break;
   }
   if(!Check(collection.Progress.RecordedCount==29&&collection.Progress.OverallBest!=null,"Run real record mode first"))yield break;p.restored=true;if(p.mode=="restore"&&!Check(p.productionOverallEnabled&&gc.OverallConfigured,"Production overall setting not enabled"))yield break;p.pendingBefore=gc.PendingCount;
   Stage("native-authentication");var login=gc.Flow.Login();yield return Await(login);p.signedIn=gc.Flow.IsSignedIn;if(!Check(p.error==null&&p.signedIn,"Native authentication: "+gc.Flow.LastLogin))yield break;p.playerToken=Hash(Encoding.UTF8.GetBytes(gc.Flow.ActivePlayerId)).Substring(0,16);
   yield return new WaitForSecondsRealtime(1);Stage("flush-30-real-records");var flush=gc.Flow.Flush();yield return Await(flush);while(gc.Flow.IsFlushing)yield return null;
   if(!Check(p.error==null&&!gc.Flow.SubmissionFailed&&gc.PendingCount==0,"Submission incomplete; queue preserved"))yield break;
   foreach(var m in gc.Configuration.Mappings){bool overall=m.objectId==OverallRecord.Id;int idx=overall?-1:collection.Progress.Content.ToList().FindIndex(d=>d.Id==m.objectId);var r=overall?null:collection.Progress.Best(idx);var o=collection.Progress.OverallBest;var row=new Row{id=m.objectId,version=m.contentVersion,board=m.leaderboardId,accuracy=overall?o.AccuracyUnits:r.AccuracyUnits,time=overall?o.TimeMilliseconds:r.ElapsedMilliseconds,expected=overall?o.Score:RankingScore.Encode(r.AccuracyUnits,r.ElapsedMilliseconds)};rows.Add(row);Stage("query-"+m.objectId);RankingPage page=null;
    for(int attempt=0;attempt<3;attempt++){var load=gc.Flow.Load(m.objectId,m.contentVersion);yield return Await(load);if(p.error!=null){row.error=p.error;break;}page=load.Result;if(page?.LocalPlayer!=null&&page.LocalPlayer.Score<=row.expected)break;yield return new WaitForSecondsRealtime(2);}
    row.remote=page?.LocalPlayer?.Score??-1;row.rank=page?.LocalPlayer?.Rank??0;row.players=page?.TotalPlayers??0;bool prior=m.objectId=="B01"||m.objectId=="B03"||m.objectId=="A04";row.matches=row.rank>0&&(prior?row.remote<=row.expected:row.remote==row.expected);if(!row.matches)row.error=row.error??"Remote best differs from actual CUT/aggregate";
   }
   foreach(var id in new[]{"B02","N05","G03","ALL29"}){Stage("ui-"+id);if(id!="ALL29")collection.Select(collection.Progress.Content.ToList().FindIndex(d=>d.Id==id));var rank=collection.GetComponent<RankingPanel>();rank.Open();yield return null;if(id=="ALL29")rank.ObjectButtons[2].onClick.Invoke();var reload=rank.Reload();yield return Await(reload);yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(folder,"ranking-"+id+".png"),texture.EncodeToPNG());}finally{Destroy(texture);}yield return null;rank.Close();}
   Finish();
  }
  void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
 }
}
#endif
