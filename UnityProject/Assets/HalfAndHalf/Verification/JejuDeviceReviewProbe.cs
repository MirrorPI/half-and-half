#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_JEJU_REVIEW
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Explicit opt-in QA only. Normal startup is untouched when the environment key is absent.
    public sealed class JejuDeviceReviewProbe:MonoBehaviour
    {
        MvpCollection collection;string folder;bool failed;int errors;
        Dictionary<string,string> before;
        readonly List<object> cuts=new List<object>();readonly List<object> frames=new List<object>();
        static string Json(object v)=>Newtonsoft.Json.JsonConvert.SerializeObject(v,Newtonsoft.Json.Formatting.Indented);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            if(Application.isEditor||Environment.GetEnvironmentVariable("HALF_JEJU_REVIEW")!="jeju-2026-10-18")return;
            var go=new GameObject("Isolated Jeju device review");DontDestroyOnLoad(go);
            var p=go.AddComponent<JejuDeviceReviewProbe>();p.folder=Path.Combine(Application.persistentDataPath,"jeju-review-2026-10-18");
            SceneManager.sceneLoaded+=p.Loaded;Application.logMessageReceived+=p.Log;
            try{p.before=p.Hashes();if(Directory.Exists(p.folder))throw new IOException("Fresh QA folder required");Directory.CreateDirectory(p.folder);}
            catch(Exception e){p.failed=true;Debug.LogError("JEJU_REVIEW_FAILED "+e.Message);}
        }
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            collection=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();
            if(collection==null)return;
            collection.SavePath=Path.Combine(folder,"progress.json");
            collection.Game.GetComponent<GameCenterController>().enabled=false;
            var ranking=collection.Game.GetComponent<RankingPanel>();ranking.enabled=false;ranking.OpenButton.gameObject.SetActive(false);
            if(failed){collection.enabled=false;collection.Game.enabled=false;return;}
            StartCoroutine(Run());
        }
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void Fail(string message){failed=true;File.WriteAllText(Path.Combine(folder,"failure.json"),Json(new{message,errors,cuts,frames}));Debug.LogError("JEJU_REVIEW_FAILED "+message);}
        Dictionary<string,string> Hashes()
        {
            var h=new Dictionary<string,string>();foreach(var n in new[]{"mvp-progress.json","game-center-queue.json"})foreach(var suffix in new[]{"",".bak",".tmp"})
            {string path=Path.Combine(Application.persistentDataPath,n+suffix);if(File.Exists(path))using(var sha=SHA256.Create())h[n+suffix]=Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}return h;
        }
        static AttemptResult Perfect(ObjectDefinition d)
        {
            decimal lo=-1.35m,hi=1.35m;for(int i=0;i<100;i++){decimal x=(lo+hi)/2;if(d.Mass.Split(x).Left<50m)lo=x;else hi=x;}
            return Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,(lo+hi)/2,5000,TransformState.Identity,true));
        }
        IEnumerator Ready()
        {
            double end=Time.realtimeSinceStartupAsDouble+10;
            while(collection.Game.Session==null||collection.Game.Session.Phase!=AttemptPhase.Ready){if(Time.realtimeSinceStartupAsDouble>end){Fail("Ready timeout");yield break;}yield return null;}
        }
        IEnumerator Capture(string name)
        {
            yield return null;yield return new WaitForEndOfFrame();
            var t=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(folder,name),t.EncodeToPNG());}finally{Destroy(t);}
        }
        IEnumerator Frames(string phase,int count)
        {
            var samples=new List<double>();double last=Time.realtimeSinceStartupAsDouble;
            for(int i=0;i<count;i++){yield return null;double now=Time.realtimeSinceStartupAsDouble;samples.Add((now-last)*1000);last=now;if(!Application.isFocused){Fail("App lost foreground during sampling");yield break;}}
            samples.Sort();frames.Add(new{phase,count,meanMs=samples.Average(),p95Ms=samples[(int)(count*.95)],maxMs=samples.Last(),over33Ms=samples.Count(x=>x>33.34),unityAllocatedBytes=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()});
        }
        IEnumerator Run()
        {
            yield return null;var g=collection.Game;
            if(collection.Progress==null||g.GetComponent<GameCenterController>().Flow!=null){Fail("Isolation/startup failed");yield break;}
            int index=Array.FindIndex(collection.Samples,s=>s.ObjectId=="N05");var defs=collection.Samples.Select(s=>s.Definition()).ToArray();
            if(index<0||defs[index].ContentVersion!="N05-jeju-blender-v1"){Fail("Wrong island build");yield break;}
            var store=new LocalProgressStore(collection.SavePath);store.Load(defs);store.Save(CollectionProgress.Restore(defs,defs.Select(Perfect),index));collection.Reload();
            yield return Ready();if(failed)yield break;yield return Capture("jeju-ready.png");yield return new WaitForSeconds(.5f);
            yield return Frames("ready",300);if(failed)yield break;
            foreach(float x in new[]{-.8f,-.4f,0f,.217f,.7f,1.05f})
            {
                g.Retry();yield return Ready();if(failed)yield break;
                var screen=g.GameCamera.WorldToScreenPoint(new Vector3(x,0,0));
                if(!g.BeginPointer(1718,screen)){Fail("Pointer rejected");yield break;}yield return null;
                var watch=System.Diagnostics.Stopwatch.StartNew();bool accepted=g.ReleasePointer(1718,screen);watch.Stop();
                if(!accepted){Fail("Release rejected");yield break;}
                double end=Time.realtimeSinceStartupAsDouble+10;while(g.Session.Phase==AttemptPhase.Cutting){yield return null;if(Time.realtimeSinceStartupAsDouble>end){Fail("Cut timeout");yield break;}}
                var r=g.Session.Result;if(r==null||r.LeftMass+r.RightMass!=100m||g.Presentation.FragmentCount!=2){Fail("Cut mass/fragments mismatch");yield break;}
                cuts.Add(new{x,inputMs=watch.Elapsed.TotalMilliseconds,r.AccuracyUnits,r.LeftMass,r.RightMass,fragments=g.Presentation.FragmentCount});
                if(x==0f){yield return Capture("jeju-cut.png");yield return new WaitForSeconds(.25f);yield return Frames("result",180);if(failed)yield break;}
            }
            var after=Hashes();bool unchanged=before.Count==after.Count&&before.All(p=>after.TryGetValue(p.Key,out var h)&&h==p.Value);
            if(!unchanged||errors>0||g.GetComponent<GameCenterController>().Flow!=null){Fail("Normal state, online isolation or console failed");yield break;}
            File.WriteAllText(Path.Combine(folder,"completed.json"),Json(new{status="completed",model=SystemInfo.deviceModel,system=SystemInfo.operatingSystem,width=Screen.width,height=Screen.height,targetFrameRate=Application.targetFrameRate,version=defs[index].ContentVersion,normalSavesUnchanged=unchanged,errors,cuts,frames,before,after}));
            g.Retry();Debug.Log("JEJU_REVIEW_COMPLETED");
        }
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
    }
}
#endif
