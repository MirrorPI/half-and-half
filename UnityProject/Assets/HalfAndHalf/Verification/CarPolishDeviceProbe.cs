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
    // Launch-only opt-in: select the new car and leave it Ready for the human.
    // No automatic cut, progress mutation, online request or normal-launch change.
    public sealed class CarPolishDeviceProbe:MonoBehaviour
    {
        int errors,warnings;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if(Environment.GetEnvironmentVariable("HALF_POLISH_REVIEW_READY")!="1")return;
            new GameObject("Car device ready capture").AddComponent<CarPolishDeviceProbe>();
        }
        void Log(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;else if(type==LogType.Warning)warnings++;}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;yield return null;
            var c=FindFirstObjectByType<MvpCollection>();var g=c.Game;
            double end=Time.realtimeSinceStartupAsDouble+15;
            while(g.Session==null||g.Session.Phase!=AttemptPhase.Ready){if(Time.realtimeSinceStartupAsDouble>end)throw new InvalidOperationException("Car Ready timeout");yield return null;}
            int index=Array.FindIndex(c.Samples,s=>s.ObjectId=="L02");
            if(!MvpCollection.ModelReview||index!=11||c.Samples[index].ContentVersion!=Presentation.CarPixelDetailSource.Version||!c.Select(index))throw new InvalidOperationException("New car/model review unavailable");
            end=Time.realtimeSinceStartupAsDouble+15;
            while(g.Session.Phase!=AttemptPhase.Ready){if(Time.realtimeSinceStartupAsDouble>end)throw new InvalidOperationException("Selected car timeout");yield return null;}
            yield return null;yield return new WaitForEndOfFrame();
            string folder=Path.Combine(Application.persistentDataPath,"polish-ready-2026-10-25");Directory.CreateDirectory(folder);
            var image=ScreenCapture.CaptureScreenshotAsTexture();try{File.WriteAllBytes(Path.Combine(folder,"car-ready.png"),image.EncodeToPNG());}finally{Destroy(image);}
            File.WriteAllText(Path.Combine(folder,"completed.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{islandDisplayName=c.Samples.Single(s=>s.ObjectId=="N05").DisplayName,scaleBeamMesh=g.Presentation.ScaleBeam.GetComponent<MeshFilter>().sharedMesh.name,selected=c.Samples[c.SelectedIndex].ObjectId,contentVersion=c.Samples[c.SelectedIndex].ContentVersion,rendererVersion=g.Presentation.Car.ContentVersion,model=SystemInfo.deviceModel,width=Screen.width,height=Screen.height,modelReview=MvpCollection.ModelReview,allSelectable=c.Items.Take(c.Samples.Length).All(b=>b.interactable),records=c.Progress.RecordedCount,onlineDisabled=!c.GetComponent<GameCenterController>().enabled,phase=g.Session.Phase.ToString(),fragments=g.Presentation.FragmentCount,knifeHidden=!g.Presentation.Knife.gameObject.activeSelf,guideHidden=!g.Presentation.Guide.gameObject.activeSelf,errors,warnings},Newtonsoft.Json.Formatting.Indented));
            Destroy(gameObject);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
#endif
