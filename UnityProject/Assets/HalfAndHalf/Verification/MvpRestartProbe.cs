#if HALF_TASK3_PROBE && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using HalfAndHalf.Core;
using HalfAndHalf.UI;
using UnityEngine;

namespace HalfAndHalf.Verification
{
    // Only compiled into the local QA build with an explicit extra define. Never present in normal players.
    public sealed class MvpRestartProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartProbe(){new GameObject("Task3RestartProbe").AddComponent<MvpRestartProbe>();}
        IEnumerator Start()
        {
            yield return null;var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-half-task3-save");
            if(flag<0||flag+1>=args.Length){Application.Quit(2);yield break;}
            string path=args[flag+1];var m=FindFirstObjectByType<MvpCollection>();m.SavePath=path;m.Reload();
            if(Array.IndexOf(args,"-half-task3-verify")>=0)
            {
                bool okay=m.Progress.UnlockedCount==10;
                for(int i=0;i<10;i++)okay&=m.Progress.Best(i)!=null&&m.Progress.Best(i).AccuracyUnits==10000;
                File.WriteAllText(path+".verified",okay?"10/10 bests and unlocks recovered":"FAILED");Application.Quit(okay?0:3);yield break;
            }
            for(int i=0;i<10;i++)
            {
                if(!m.Select(i))throw new Exception("Locked content "+i);
                while(m.Game.Session.Phase!=AttemptPhase.Ready)yield return null;
                yield return new WaitForSecondsRealtime(.2f);var mass=m.Samples[i].Definition().Mass;decimal lo=-1.35m,hi=1.35m;
                for(int j=0;j<60;j++){decimal mid=(lo+hi)/2;if(mass.Split(mid).Left<mass.TotalMass/2)lo=mid;else hi=mid;}
                var p=m.Game.GameCamera.WorldToScreenPoint(new Vector3((float)((lo+hi)/2),0,0));m.Game.BeginPointer(42,p);m.Game.ReleasePointer(42,p);
                while(m.Game.Session.Phase==AttemptPhase.Cutting)yield return null;
                if(m.Progress.Best(i)==null||m.SavePending)throw new Exception("Best was not durably saved "+i);
            }
            File.WriteAllText(path+".ready-to-kill","10 completed actual CUTs saved; waiting for external SIGKILL");
            while(true)yield return null;
        }
    }
}
#endif
