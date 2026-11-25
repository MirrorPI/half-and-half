#if HALF_TASK5_PROBE && !UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using HalfAndHalf.Core;
using HalfAndHalf.UI;
using HalfAndHalf.Services;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Only in the explicitly defined local QA player, never in normal players.
    public sealed class Task5RestartProbe:MonoBehaviour
    {
        static string path;
        [Serializable] sealed class Row {public string id,version,x,left,right,owner;public int accuracy;public long milliseconds;}
        [Serializable] sealed class Proof {public bool okay;public string stage,scene;public int count,unlocked;public Row[] rows;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-half-task5-save");if(flag>=0&&flag+1<args.Length)path=args[flag+1];
            SceneManager.sceneLoaded+=(scene,mode)=>
            {
                var m=FindFirstObjectByType<MvpCollection>();var gc=FindFirstObjectByType<GameCenterController>();
                if(string.IsNullOrEmpty(path)){m.enabled=false;gc.enabled=false;Application.Quit(2);return;}
                m.SavePath=path;var config=Instantiate(gc.Configuration);config.EnableOnline=false;gc.Configuration=config;
                new GameObject("Task5 isolated restart proof").AddComponent<Task5RestartProbe>();
            };
        }
        static string S(decimal value)=>value.ToString(CultureInfo.InvariantCulture);
        static Row[] Rows(MvpCollection m)=>Enumerable.Range(0,20).Where(i=>m.Progress.Best(i)!=null).Select(i=>{var r=m.Progress.Best(i);return new Row{id=r.Snapshot.ObjectId,version=r.Snapshot.ContentVersion,x=S(r.Snapshot.KnifeX),left=S(r.LeftMass),right=S(r.RightMass),accuracy=r.AccuracyUnits,milliseconds=r.ElapsedMilliseconds,owner=m.Progress.BestOwner(i)};}).ToArray();
        bool Check(bool condition,string message)
        {
            if(condition)return true;File.WriteAllText(path+".failed",message);Debug.LogError(message);Application.Quit(3);return false;
        }
        IEnumerator Start()
        {
            yield return null;var m=FindFirstObjectByType<MvpCollection>();var gc=m.Game.GetComponent<GameCenterController>();
            if(!Check(m.SavePath==path&&m.Samples.Length==20&&!gc.Configured,"Real20/QA isolation missing"))yield break;
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-half-task5-verify")>=0)
            {
                bool good=m.Progress.UnlockedCount==20&&Rows(m).Length==20&&Enumerable.Range(10,10).All(i=>m.Progress.Best(i).AccuracyUnits==10000);
                File.WriteAllText(path+".verified.json",JsonUtility.ToJson(new Proof{okay=good,stage="new_process_reload",scene=SceneManager.GetActiveScene().path,count=20,unlocked=m.Progress.UnlockedCount,rows=Rows(m)},true));Application.Quit(good?0:3);yield break;
            }
            File.WriteAllText(path+".before.json",JsonUtility.ToJson(new Proof{okay=true,stage="legacy_copy_loaded",scene=SceneManager.GetActiveScene().path,count=20,unlocked=m.Progress.UnlockedCount,rows=Rows(m)},true));
            if(!Check(m.Progress.UnlockedCount==11,"Legacy complete10 must unlock only11"))yield break;
            for(int i=10;i<20;i++)
            {
                if(!Check(m.Select(i),"Cannot select "+i))yield break;double deadline=Time.realtimeSinceStartupAsDouble+5;
                while(m.Game.Session.Phase!=AttemptPhase.Ready&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                if(!Check(m.Game.Session.Phase==AttemptPhase.Ready,"Ready timeout"))yield break;
                if(m.Game.Session.ActivePointer.HasValue)m.Game.CancelPointer(m.Game.Session.ActivePointer.Value);
                var d=m.Samples[i].Definition();decimal lo=-1.35m,hi=1.35m;
                for(int j=0;j<100;j++){decimal x=(lo+hi)/2;if(d.Mass.Split(x).Left<d.Mass.TotalMass/2)lo=x;else hi=x;}
                var point=m.Game.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));
                if(!Check(m.Game.BeginPointer(670+i,point)&&m.Game.ReleasePointer(670+i,point),"Cut rejected"))yield break;
                while(m.Game.Session.Phase==AttemptPhase.Cutting)yield return null;
                if(!Check(m.Progress.Best(i)!=null&&m.Progress.Best(i).AccuracyUnits==10000&&!m.SavePending,"Durable actual Perfect missing "+i))yield break;
                m.Game.Hud.RetryButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.08f);
                if(!Check(m.Game.Presentation.FragmentCount==0&&!m.Game.Presentation.Audio.CutSource.isPlaying,"Retry cleanup missing"))yield break;
            }
            File.WriteAllText(path+".ready-to-kill.json",JsonUtility.ToJson(new Proof{okay=true,stage="ten_added_actual_cuts_saved",scene=SceneManager.GetActiveScene().path,count=20,unlocked=m.Progress.UnlockedCount,rows=Rows(m)},true));
            while(true)yield return null;
        }
    }
}
#endif
