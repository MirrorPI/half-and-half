#if UNITY_IOS && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Opt-in development QA only. Programmatic input is never labelled as a human touch/quality check.
    public sealed class MvpDeviceProbe : MonoBehaviour
    {
        string mode,path,folder;
        MvpCollection collection;
        [Serializable] sealed class Row {public string id;public int accuracy;public long timeMs;}
        [Serializable] sealed class Evidence
        {
            public string platform,system,mode,kind="Actual iPhone development player; automated pointer input; not human touch/audio inspection";
            public int width,height,unlocked,bests,queuePending,audioSources,errors,warnings;
            public string[] missingGlyphs;
            public bool knifeHidden,onlineConfigured;
            public float safeX,safeY,safeWidth,safeHeight,meanFrameMs,p95FrameMs;
            public long allocatedMemory;
            public Row[] records;
        }
        int errors,warnings;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Begin()
        {
            string mode=Environment.GetEnvironmentVariable("HALF_TASK4_DEVICE_PROBE");if(mode!="record"&&mode!="restore"&&mode!="header")return;
            var go=new GameObject("Task4DeviceProbe");DontDestroyOnLoad(go);var probe=go.AddComponent<MvpDeviceProbe>();probe.mode=mode;
            probe.folder=Path.Combine(Application.persistentDataPath,"task4-device-qa");Directory.CreateDirectory(probe.folder);probe.path=Path.Combine(probe.folder,"progress.json");
            if(mode=="record")foreach(string suffix in new[]{"",".bak",".tmp",".queue",".queue.bak",".queue.tmp"})if(File.Exists(probe.path+suffix))File.Delete(probe.path+suffix);
            SceneManager.sceneLoaded+=probe.Loaded;Application.logMessageReceived+=probe.Log;
        }
        void Loaded(Scene scene,LoadSceneMode _)
        {
            collection=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();
            if(collection!=null){collection.SavePath=path;StartCoroutine(Run());}
        }
        void Log(string condition,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;if(type==LogType.Warning)warnings++;}
        IEnumerator Run()
        {
            yield return null;var game=collection.Game;var controller=game.GetComponent<GameCenterController>();var ranking=game.GetComponent<RankingPanel>();
            if(mode=="header")
            {
                double deadline=Time.realtimeSinceStartupAsDouble+10;
                while(game.Session.Phase!=AttemptPhase.Ready){if(Time.realtimeSinceStartupAsDouble>deadline){Fail("Header READY timeout");yield break;}yield return null;}
                yield return new WaitForEndOfFrame();Capture("header-actual.png");
            }
            if(mode=="record")
            {
                for(int i=0;i<collection.Samples.Length;i++)
                {
                    if(!collection.Select(i)){Fail("Locked content "+i);yield break;}
                    double deadline=Time.realtimeSinceStartupAsDouble+5;while(game.Session.Phase!=AttemptPhase.Ready){if(Time.realtimeSinceStartupAsDouble>deadline){Fail("READY timeout");yield break;}yield return null;}
                    var mass=collection.Samples[i].Definition().Mass;decimal lo=-1.35m,hi=1.35m;for(int j=0;j<60;j++){decimal mid=(lo+hi)/2;if(mass.Split(mid).Left<mass.TotalMass/2)lo=mid;else hi=mid;}
                    var point=game.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));
                    if(!game.BeginPointer(42,point)||!game.ReleasePointer(42,point)){Fail("CUT failed");yield break;}
                    yield return new WaitForSecondsRealtime(.65f);if(collection.Progress.Best(i)==null){Fail("Missing local best "+i);yield break;}
                }
                yield return new WaitForSecondsRealtime(.7f);collection.Open();yield return new WaitForEndOfFrame();Capture("collection.png");yield return new WaitForSecondsRealtime(.2f);collection.Close();
                ranking.Open();yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture("ranking-guest.png");yield return new WaitForSecondsRealtime(.2f);ranking.Close();
            }
            yield return new WaitForSecondsRealtime(1.0f);
            var frameTimes=new List<float>();for(int i=0;i<180;i++){frameTimes.Add(Time.unscaledDeltaTime*1000);yield return null;}frameTimes.Sort();
            var safe=Screen.safeArea;var missing=new HashSet<string>();
            foreach(var t in ranking.Panel.GetComponentsInChildren<TMPro.TMP_Text>(true))foreach(char ch in t.text)if(!char.IsControl(ch)&&!t.font.HasCharacter(ch,true,true))missing.Add("U+"+((int)ch).ToString("X4"));
            var evidence=new Evidence{mode=mode,platform=Application.platform.ToString(),system=SystemInfo.operatingSystem,width=Screen.width,height=Screen.height,safeX=safe.x,safeY=safe.y,safeWidth=safe.width,safeHeight=safe.height,unlocked=collection.Progress.UnlockedCount,bests=Enumerable.Range(0,10).Count(i=>collection.Progress.Best(i)!=null),queuePending=controller.PendingCount,audioSources=game.GetComponents<AudioSource>().Length,knifeHidden=!game.Presentation.Knife.gameObject.activeSelf,onlineConfigured=controller.Configured,errors=errors,warnings=warnings,missingGlyphs=missing.ToArray(),meanFrameMs=frameTimes.Average(),p95FrameMs=frameTimes[(int)(frameTimes.Count*.95)],allocatedMemory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),records=Enumerable.Range(0,10).Select(i=>{var r=collection.Progress.Best(i);return new Row{id=collection.Samples[i].ObjectId,accuracy=r?.AccuracyUnits??-1,timeMs=r?.ElapsedMilliseconds??-1};}).ToArray()};
            if(mode=="header")evidence.kind="Actual iPhone development player; READY screenshot only; no pointer input, CUT, authentication or server submission";
            File.WriteAllText(Path.Combine(folder,mode+".json"),JsonUtility.ToJson(evidence,true));Debug.Log("TASK4_DEVICE_PROBE_COMPLETE "+mode);
        }
        void Capture(string name)
        {
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(folder,name),texture.EncodeToPNG());}
            finally{Destroy(texture);}
        }
        void Fail(string message){File.WriteAllText(Path.Combine(folder,"failure.txt"),message);Debug.LogError("TASK4_DEVICE_PROBE_FAILED "+message);}
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Application.logMessageReceived-=Log;}
    }
}
#endif
