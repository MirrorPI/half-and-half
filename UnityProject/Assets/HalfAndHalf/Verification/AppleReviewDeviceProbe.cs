#if DEVELOPMENT_BUILD && HALF_MODEL_REVIEW
using System;
using System.Collections;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using UnityEngine;

namespace HalfAndHalf.Verification
{
    // Opt-in capture of one ordinary cut and Retry; never touches progress or the submission queue.
    public sealed class AppleReviewDeviceProbe:MonoBehaviour
    {
        string folder;int errors,warnings;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(Environment.GetEnvironmentVariable("HALF_APPLE_REVIEW_CAPTURE")!="1")return;
            var go=new GameObject("Apple review capture");go.AddComponent<AppleReviewDeviceProbe>();
        }
        void Awake(){folder=Path.Combine(Application.persistentDataPath,"apple-review-2026-10-22");Directory.CreateDirectory(folder);Application.logMessageReceived+=Log;}
        void Log(string m,string s,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;else if(type==LogType.Warning)warnings++;}
        IEnumerator Capture(string name){yield return null;yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(folder,name),t.EncodeToPNG());}finally{Destroy(t);}}
        IEnumerator Ready(HalfAndHalf.Gameplay.AppleGame g)
        {
            double end=Time.realtimeSinceStartupAsDouble+12;
            while(g.Session==null||g.Session.Phase!=AttemptPhase.Ready)
            {if(Time.realtimeSinceStartupAsDouble>end)throw new InvalidOperationException("Model review Ready timeout");yield return null;}
        }
        IEnumerator Start()
        {
            yield return null;
            var c=FindFirstObjectByType<MvpCollection>();var g=c.Game;
            yield return Ready(g);
            int appleIndex=Array.FindIndex(c.Samples,s=>s.ObjectId=="B01");
            if(!c.Select(appleIndex))throw new InvalidOperationException("Unable to select B01");
            yield return Ready(g);
            yield return Capture("apple-ready.png");
            var screen=g.GameCamera.WorldToScreenPoint(Vector3.zero);
            bool began=g.BeginPointer(2121,screen);yield return null;bool released=g.ReleasePointer(2121,screen);
            double end=Time.realtimeSinceStartupAsDouble+12;
            while(g.Session.Phase==AttemptPhase.Cutting&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            var result=g.Session.Result;int fragments=g.Presentation.FragmentCount;
            yield return Capture("apple-cut.png");
            g.Hud.RetryButton.onClick.Invoke();yield return Ready(g);
            bool retried=g.Session.Phase==AttemptPhase.Ready&&g.Presentation.FragmentCount==0;
            yield return Capture("apple-retry.png");
            c.Open();yield return Capture("collection.png");bool allSelectable=c.Items.Take(c.Samples.Length).All(b=>b.interactable);c.Close();
            File.WriteAllText(Path.Combine(folder,"completed.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{selected=c.Samples[c.SelectedIndex].ObjectId,contentVersion=c.Samples[c.SelectedIndex].ContentVersion,model=SystemInfo.deviceModel,width=Screen.width,height=Screen.height,modelReview=MvpCollection.ModelReview,allSelectable,count=c.Samples.Length,began,released,fragments,accuracy=result?.AccuracyUnits,retried,records=c.Progress.RecordedCount,onlineDisabled=!c.GetComponent<GameCenterController>().enabled,errors,warnings,samples=c.Samples.Select(s=>new{s.ObjectId,s.ContentVersion}).ToArray()},Newtonsoft.Json.Formatting.Indented));
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
#endif
