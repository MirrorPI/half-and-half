#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HalfAndHalf.Verification
{
    // Opt-in, build-defined instrumentation. All input is automated and all saves are isolated.
    public sealed class Task5DeviceReviewProbe : MonoBehaviour
    {
        string mode, folder, path;
        MvpCollection collection;
        int errors, warnings;
        int startupDeviceConditions;
        bool failed;
        bool screensInputValid;
        readonly List<object> attempts = new List<object>();
        readonly List<object> performance = new List<object>();
        readonly List<object> preparation = new List<object>();
        readonly List<object> focusEvents = new List<object>();
        object carTopology;
        readonly List<object> captures = new List<object>();
        Dictionary<string,string> normalBefore;
        static string D(decimal value) => value.ToString(CultureInfo.InvariantCulture);
        static string Json(object value) => JsonConvert.SerializeObject(value, Formatting.Indented);

        [System.Runtime.InteropServices.DllImport("__Internal", EntryPoint="HalfTask5ReadDeviceConditions")]
        static extern int ReadDeviceConditions();
        static int DeviceConditionFlags()
        {
            if (Application.isEditor) return -1; // Dynamic Editor guard fixtures never call iOS native code.
            return ReadDeviceConditions();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            if (Application.isEditor) return;
            string mode = Environment.GetEnvironmentVariable("HALF_TASK5_DEVICE_REVIEW");
            string reviewRun = Environment.GetEnvironmentVariable("HALF_TASK5_DEVICE_REVIEW_RUN");
            if (string.IsNullOrEmpty(mode)&&string.IsNullOrEmpty(reviewRun)) return;
            var go = new GameObject("Task5 isolated device review");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<Task5DeviceReviewProbe>();
            probe.mode = mode;
            string original = Path.Combine(Application.persistentDataPath, "task5-review-2026-10-14");
            probe.folder = mode == "screens" || mode == "fan" ? Path.Combine(Application.persistentDataPath, mode == "fan" ? "task5-fan-review-2026-10-14" : "task5-capture-review-2026-10-14") : original;
            bool latencyRun = reviewRun == "cut-latency-2026-10-15" || reviewRun == "cut-latency-2026-10-15-v2" || reviewRun == "cut-budget-2026-10-15" || reviewRun == "cut-budget-2026-10-15-v2" || reviewRun == "cut-budget-2026-10-15-v3" || reviewRun == "cut-budget-2026-10-15-v4" || reviewRun == "cut-budget-2026-10-15-v5" || reviewRun == "cut-budget-2026-10-15-v6" || reviewRun == "cut-budget-2026-10-15-v7" || reviewRun == "cut-budget-2026-10-15-v8" || reviewRun == "startup-scale-2026-10-15-v1" || reviewRun == "startup-scale-2026-10-15-v2" || reviewRun == "startup-scale-2026-10-15-v3" || reviewRun == "startup-scale-2026-10-15-v4" || reviewRun == "startup-scale-2026-10-15-v5" || reviewRun == "startup-scale-2026-10-15-v6" || reviewRun == "startup-scale-2026-10-15-v7" || reviewRun == "startup-scale-2026-10-15-v8" || reviewRun == "startup-scale-2026-10-15-v9" || reviewRun == "startup-scale-2026-10-15-v10" || reviewRun == "startup-scale-2026-10-15-v11" || reviewRun == "startup-scale-2026-10-15-v12" || reviewRun == "startup-scale-2026-10-15-v13" || reviewRun == "startup-scale-2026-10-15-v14";
            bool profileRun=reviewRun=="cut-budget-2026-10-15-profile-v1"||reviewRun=="startup-scale-2026-10-15-profile-v1"||reviewRun=="startup-scale-2026-10-15-profile-v2"||reviewRun=="startup-scale-2026-10-15-profile-v3";
            bool preparationRun=reviewRun=="selection-preparation-2026-10-15-profile-v1"||reviewRun=="selection-preparation-2026-10-15-profile-v2"||reviewRun=="selection-preparation-2026-10-15-profile-v3"||reviewRun=="selection-preparation-2026-10-15-profile-v4";
            if (latencyRun||profileRun||preparationRun) probe.folder = Path.Combine(Application.persistentDataPath, "task5-"+reviewRun);
            probe.path = Path.Combine(probe.folder, "progress.json");
            // Install isolation before any I/O that can fail. A failed clone must never start normal/online flow.
            SceneManager.sceneLoaded += probe.Loaded;
            Application.logMessageReceived += probe.Log;
            try { probe.normalBefore = probe.NormalHashes(); probe.startupDeviceConditions=DeviceConditionFlags(); }
            catch (Exception e)
            {
                probe.failed = true;
                Debug.LogError("TASK5_DEVICE_REVIEW_FAILED baseline hash: " + e.Message);
                return;
            }
            if(mode!="record"&&mode!="restore"&&mode!="screens"&&mode!="fan"&&mode!="profile"&&mode!="prepare")
            {
                probe.failed=true;Debug.LogError("TASK5_DEVICE_REVIEW_FAILED invalid mode with explicit review intent; normal flow disabled");return;
            }
            bool folderExisted = Directory.Exists(probe.folder);
            if ((!string.IsNullOrEmpty(reviewRun) && !latencyRun&&!profileRun&&!preparationRun) ||
                (profileRun&&mode!="profile") || (mode=="profile"&&!profileRun) ||
                (preparationRun&&mode!="prepare") || (mode=="prepare"&&!preparationRun) ||
                (latencyRun && (mode != "record" && mode != "restore")) ||
                (latencyRun && mode == "record" && folderExisted) ||
                (latencyRun && mode == "restore" && !File.Exists(Path.Combine(probe.folder,"expected-records.json"))))
            {
                probe.failed = true;
                Debug.LogError("TASK5_DEVICE_REVIEW_FAILED fresh latency run required; prior evidence untouched");
                return;
            }
            if (mode == "screens" || mode == "fan" || mode == "profile" || mode == "prepare")
            {
                string source = Path.Combine(original, "progress.json");
                string expected = Path.Combine(original, "expected-records.json");
                if (folderExisted || !File.Exists(source) || !File.Exists(expected))
                {
                    probe.failed = true;
                    Debug.LogError("TASK5_DEVICE_REVIEW_FAILED screens requires fresh output and completed source; existing output left untouched");
                    return;
                }
                try
                {
                    Directory.CreateDirectory(probe.folder);
                    File.Copy(source, probe.path, false);
                    File.Copy(expected, Path.Combine(probe.folder, "expected-records.json"), false);
                    if(mode=="prepare")
                    {
                        // Only the fresh QA clone is normalized. Keep every other payload/envelope field.
                        var envelope=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(probe.path));
                        string originalPayload=(string)envelope["payload"];
                        using(var sha=SHA256.Create())
                        {
                            string actual=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(originalPayload))).Replace("-","");
                            if(!string.Equals(actual,(string)envelope["sha256"],StringComparison.Ordinal))throw new InvalidDataException("Preparation source checksum mismatch");
                        }
                        var payload=Newtonsoft.Json.Linq.JObject.Parse(originalPayload);payload["selected"]=0;
                        string text=payload.ToString(Formatting.None);envelope["payload"]=text;
                        using(var sha=SHA256.Create())envelope["sha256"]=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","");
                        File.WriteAllText(probe.path,envelope.ToString(Formatting.None));
                    }
                    probe.screensInputValid = true;
                }
                catch (Exception e)
                {
                    probe.failed = true;
                    Debug.LogError("TASK5_DEVICE_REVIEW_FAILED screens clone: " + e.Message);
                }
            }
            else
            {
                try { Directory.CreateDirectory(probe.folder); }
                catch (Exception e)
                {
                    probe.failed = true;
                    Debug.LogError("TASK5_DEVICE_REVIEW_FAILED output directory: " + e.Message);
                }
            }
        }

        void Loaded(Scene scene, LoadSceneMode _)
        {
            collection = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MvpCollection>(true)).FirstOrDefault();
            if (collection == null) return;
            // sceneLoaded precedes Start/Update: neither the normal store nor online flow is initialized.
            collection.SavePath = path;
            collection.Game.GetComponent<GameCenterController>().enabled = false;
            var ranking = collection.Game.GetComponent<RankingPanel>();
            ranking.enabled = false;
            ranking.OpenButton.gameObject.SetActive(false);
            if (failed || ((mode == "screens" || mode == "fan" || mode == "profile" || mode == "prepare") && !screensInputValid))
            {
                // Refuse before Start: do not load/promote/write anything in a previous output folder.
                collection.enabled = false;
                collection.Game.enabled = false;
                return;
            }
            StartCoroutine(Run());
        }

        void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            if (type == LogType.Warning) warnings++;
        }

        static int ReviewCutCount(string name)
        {return (name=="task5-cut-budget-2026-10-15-v2"||name=="task5-cut-budget-2026-10-15-v3"||name=="task5-cut-budget-2026-10-15-v4"||name=="task5-cut-budget-2026-10-15-v5"||name=="task5-cut-budget-2026-10-15-v6"||name=="task5-cut-budget-2026-10-15-v7"||name=="task5-cut-budget-2026-10-15-v8"||name=="task5-startup-scale-2026-10-15-v1"||name=="task5-startup-scale-2026-10-15-v2"||name=="task5-startup-scale-2026-10-15-v3"||name=="task5-startup-scale-2026-10-15-v4"||name=="task5-startup-scale-2026-10-15-v5"||name=="task5-startup-scale-2026-10-15-v6"||name=="task5-startup-scale-2026-10-15-v7"||name=="task5-startup-scale-2026-10-15-v8"||name=="task5-startup-scale-2026-10-15-v9"||name=="task5-startup-scale-2026-10-15-v10"||name=="task5-startup-scale-2026-10-15-v11"||name=="task5-startup-scale-2026-10-15-v12"||name=="task5-startup-scale-2026-10-15-v13"||name=="task5-startup-scale-2026-10-15-v14")?7:3;}

        IEnumerator Run()
        {
            yield return null;
            var g = collection.Game;
            if (g.gameObject.scene.path != "Assets/Scenes/Gameplay20.unity" || collection.Samples.Length != 20)
            { Fail("Actual Gameplay20 required"); yield break; }
            if(mode=="prepare"){yield return PreparationProfile();if(failed)yield break;}
            else if(mode=="profile"){yield return NativeProfile();if(failed)yield break;}
            else if (mode == "screens" || mode == "fan")
            {
                if (!screensInputValid || collection.Progress.UnlockedCount != 20 ||
                    Json(Records()) != File.ReadAllText(Path.Combine(folder, "expected-records.json")))
                { Fail("Screen review requires a fresh isolated clone of the completed twenty records"); yield break; }
                string bestBefore = Json(Records());
                for (int i = 10; i < (mode == "fan" ? 11 : 20); i++)
                {
                    if (!collection.Select(i)) { Fail("Screen selection failed"); yield break; }
                    yield return Ready();
                    if (failed) yield break;
                    var definition = collection.Samples[i].Definition();
                    yield return Capture(definition.Id + "-ready.png");
                    if (failed) yield break;
                    var screen = g.GameCamera.WorldToScreenPoint(new Vector3(.217f, 0, 0));
                    if (!g.BeginPointer(5205, screen)) { Fail("Screen input rejected"); yield break; }
                    yield return null;
                    if (!g.ReleasePointer(5205, screen)) { Fail("Screen release rejected"); yield break; }
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (g.Session.Phase == AttemptPhase.Cutting && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    if (g.Session.Phase != AttemptPhase.Result) { Fail("Screen RESULT timeout"); yield break; }
                    var result = g.Session.Result;
                    attempts.Add(new { id = definition.Id, version = definition.ContentVersion, x = D(result.Snapshot.KnifeX),
                        left = D(result.LeftMass), right = D(result.RightMass), accuracy = result.AccuracyUnits, timeMs = result.ElapsedMilliseconds });
                    yield return Capture(definition.Id + "-result.png");
                    if (failed) yield break;
                }
                if (Json(Records()) != bestBefore) { Fail("Screen review changed restored best records"); yield break; }
            }
            else if (mode == "record")
            {
                if (collection.Progress.UnlockedCount != 1 || collection.Progress.Best(0) != null)
                { Fail("Refusing to overwrite an existing QA progress run"); yield break; }
                bool startupScaleRun=Path.GetFileName(folder)=="task5-startup-scale-2026-10-15-v1"||Path.GetFileName(folder)=="task5-startup-scale-2026-10-15-v2";
                for (int i = 0; i < 20; i++)
                {
                    double selectionStart=Time.realtimeSinceStartupAsDouble;
                    var selectWatch=System.Diagnostics.Stopwatch.StartNew();
                    if (!collection.Select(i)) { Fail("Cannot select " + i); yield break; }
                    selectWatch.Stop();
                    yield return Ready();
                    if (g.Session.Phase != AttemptPhase.Ready) yield break;
                    var definition = collection.Samples[i].Definition();
                    int selectionConditions=DeviceConditionFlags();
                    preparation.Add(new{id=definition.Id,selectionMs=selectWatch.Elapsed.TotalMilliseconds,
                        selectionToReadyMs=(Time.realtimeSinceStartupAsDouble-selectionStart)*1000,
                        thermalState=selectionConditions&255,lowPowerMode=(selectionConditions&256)!=0,
                        initialBodyWarmMs=definition.Id=="B01"?g.Presentation.ReviewBodyWarmMs:0,
                        bodySourcePrepareMs=definition.Id=="B01"?g.Presentation.ReviewBodySourcePrepareMs:0,
                        bodyNativeWarmMs=definition.Id=="B01"?g.Presentation.ReviewBodyNativeWarmMs:0,
                        bodyWarmCount=definition.Id=="B01"?g.Presentation.ReviewBodyWarmCount:0,
                        bodyCacheCheckMs=definition.Id=="B01"?g.Presentation.ReviewBodyCacheCheckMs:0,
                        sampleLandedWarmMs=g.Presentation.Sample.gameObject.activeSelf?g.Presentation.Sample.ReviewWarmMs:0});
                    if (i >= 10 || startupScaleRun) yield return Capture(definition.Id + "-ready.png");
                    yield return Frames(definition.Id, "READY", 180);
                    AttemptResult repeat = null;
                    int cutCount=ReviewCutCount(Path.GetFileName(folder));
                    for (int cut = 0; cut < cutCount; cut++)
                    {
                        double retryMs=0,retryToReadyMs=0;
                        if (cut > 0)
                        {
                            double retryStart=Time.realtimeSinceStartupAsDouble;
                            g.Hud.RetryButton.onClick.Invoke();
                            retryMs=(Time.realtimeSinceStartupAsDouble-retryStart)*1000;
                            yield return Ready();
                            retryToReadyMs=(Time.realtimeSinceStartupAsDouble-retryStart)*1000;
                        }
                        if (g.Session.Phase != AttemptPhase.Ready) yield break;
                        decimal x = cut==3?-.773m:cut==4?-.217m:cut==5?.701m:cut==6?0m:.217m;
                        if (cut == 2)
                        {
                            decimal lo = -1.35m, hi = 1.35m;
                            for (int j = 0; j < 80; j++)
                            { decimal mid = (lo + hi) / 2; if (definition.Mass.Split(mid).Left < definition.Mass.TotalMass / 2) lo = mid; else hi = mid; }
                            x = (lo + hi) / 2;
                        }
                        int conditions=DeviceConditionFlags();
                        var screen = g.GameCamera.WorldToScreenPoint(new Vector3((float)x, 0, 0));
                        if (!g.BeginPointer(5205, screen)) { Fail("Begin rejected"); yield break; }
                        yield return null;
                        var watch = new System.Diagnostics.Stopwatch();
                        int gcBefore=GC.CollectionCount(0);
                        ReviewCutTimings.Reset();
                        long nativeBefore=NativeCutTopology.TotalRuns;
                        watch.Start();
                        bool released = g.ReleasePointer(5205, screen); watch.Stop();
                        long nativeRuns=NativeCutTopology.TotalRuns-nativeBefore;
                        int releaseGcCollections=GC.CollectionCount(0)-gcBefore;
                        if (!released) { Fail("Release rejected"); yield break; }
                        var frozen = g.Session.Result;
                        var frames = new List<float>();
                        double deadline = Time.realtimeSinceStartupAsDouble + 5;
                        while (g.Session.Phase == AttemptPhase.Cutting && Time.realtimeSinceStartupAsDouble < deadline)
                        { yield return null; frames.Add(Time.unscaledDeltaTime * 1000); }
                        var result = g.Session.Result;
                        if (g.Session.Phase != AttemptPhase.Result || !ReferenceEquals(frozen, result) ||
                            result.LeftMass + result.RightMass != definition.Mass.TotalMass ||
                            Scoring.Evaluate(definition, result.Snapshot).AccuracyUnits != result.AccuracyUnits ||
                            g.Presentation.FragmentCount != 2 || (cut == 2 && result.AccuracyUnits != 10000))
                        { Fail("CUT conservation/snapshot/Perfect failure " + definition.Id); yield break; }
                        if (cut == 1 && (repeat.AccuracyUnits != result.AccuracyUnits || repeat.LeftMass != result.LeftMass ||
                            repeat.RightMass != result.RightMass || repeat.Snapshot.KnifeX != result.Snapshot.KnifeX))
                        { Fail("Repeated input changed result " + definition.Id); yield break; }
                        if (cut == 0) { repeat = result; if (i >= 10 || startupScaleRun) yield return Capture(definition.Id + "-result.png");
                            if (i == 11 && Path.GetFileName(folder) == "task5-cut-budget-2026-10-15") yield return CarTopologyAB();
                        }
                        attempts.Add(new { id = definition.Id, version = definition.ContentVersion, cut,
                            x = D(result.Snapshot.KnifeX), left = D(result.LeftMass), right = D(result.RightMass),
                            total = D(definition.Mass.TotalMass), accuracy = result.AccuracyUnits,
                            retryMs,retryToReadyMs,
                            thermalState=conditions&255,lowPowerMode=(conditions&256)!=0,
                            retainedBodyWarmMs=definition.Id=="B01"?g.Presentation.ReviewBodyWarmMs:0,
                            bodyWarmCount=definition.Id=="B01"?g.Presentation.ReviewBodyWarmCount:0,
                            bodyCacheCheckMs=definition.Id=="B01"?g.Presentation.ReviewBodyCacheCheckMs:0,
                            timeMs = result.ElapsedMilliseconds, cutPreparationMs = watch.Elapsed.TotalMilliseconds,
                            sessionMs=g.ReviewSessionMs, subscribersMs=g.ReviewSubscribersMs, presentationMs=g.ReviewPresentationMs,
                            meshMs=ReviewCutTimings.MeshMs,restMs=ReviewCutTimings.RestMs,clipMs=ReviewCutTimings.ClipMs,uploadMs=ReviewCutTimings.UploadMs,sortMs=ReviewCutTimings.SortMs,
                            gcCollectionsDuringRelease=releaseGcCollections,
                            nativeTopologyRuns=nativeRuns,nativeBurstEnabled=NativeCutTopology.BurstEnabled,nativeResidentOwners=NativeCutTopology.ActiveOwners,
                            variant = g.Presentation.Audio.LastVariant, fragments = g.Presentation.FragmentCount });
                        Stats(definition.Id, "CUT", frames);
                    }
                    g.Hud.RetryButton.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.08f);
                    if (g.Presentation.FragmentCount != 0 || g.Presentation.Knife.gameObject.activeSelf ||
                        g.Presentation.Audio.CutSource.isPlaying || g.GetComponents<AudioSource>().Length != 4)
                    { Fail("Retry cleanup failure " + definition.Id); yield break; }
                    yield return Frames(definition.Id, "Retry/drop", 60);
                }
                if (collection.Progress.UnlockedCount != 20 || collection.SavePending)
                { Fail("Collection/save incomplete"); yield break; }
                collection.Open(); yield return null;
                var scroll = collection.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
                scroll.verticalNormalizedPosition = 0;
                yield return Capture("collection-last.png");
                yield return Frames("20-row collection", "UI", 180);
                bool blocked = !g.BeginPointer(5299, new Vector2(Screen.width / 2, Screen.height / 2));
                if (!blocked || collection.Items.Length != 20) { Fail("Modal input/rows failure"); yield break; }
                collection.Items[19].onClick.Invoke();
                if (collection.Progress.Selected != 19 || collection.Panel.activeSelf) { Fail("Last button failed"); yield break; }
                yield return Ready();
                if (failed) yield break;
                File.WriteAllText(Path.Combine(folder, "expected-records.json"), Json(Records()));
            }
            else
            {
                string expected = Path.Combine(folder, "expected-records.json");
                if (!File.Exists(expected) || Json(Records()) != File.ReadAllText(expected) || collection.Progress.UnlockedCount != 20)
                { Fail("New process did not restore exact twenty records"); yield break; }
                yield return Ready();
                if (failed) yield break;
                yield return Capture("restore-ready.png");
            }
            var normalAfter = NormalHashes();
            if (normalBefore.Count != normalAfter.Count || normalBefore.Any(p => !normalAfter.TryGetValue(p.Key, out var hash) || hash != p.Value))
            { Fail("Normal progress/queue changed"); yield break; }
            var safe = Screen.safeArea;
            var evidence = new { mode, status = "completed", kind = mode=="prepare"?"Actual iPhone synchronous Select phase diagnostic; not CUT or whole startup acceptance":mode=="profile"?"Actual iPhone direct Presentation.Cut diagnostic under native profiler; not synchronous ReleasePointer budget acceptance":"Actual iPhone; API pointer/buttons; no human touch, listening, haptic or art acceptance",
                scene = g.gameObject.scene.path, model = SystemInfo.deviceModel, os = SystemInfo.operatingSystem,
                width = Screen.width, height = Screen.height, safeX = safe.x, safeY = safe.y, safeWidth = safe.width, safeHeight = safe.height,
                objects = collection.Samples.Length, unlocked = collection.Progress.UnlockedCount,
                launchThermalState=startupDeviceConditions&255,launchLowPowerMode=(startupDeviceConditions&256)!=0,
                recordObjectCaptureProtocol=mode=="prepare"?"No captures; two20-object selection sweeps; no CUT":(Path.GetFileName(folder)=="task5-startup-scale-2026-10-15-v1"||Path.GetFileName(folder)=="task5-startup-scale-2026-10-15-v2")?"20 objects / 40 READY-RESULT PNG":"L/N10 objects / 20 READY-RESULT PNG (priorv8 protocol)",
                records = Records(), attempts, performance, captures, preparation, focusEvents, carTopology, errors, warnings,
                onlineFlowInitialized = g.GetComponent<GameCenterController>().Flow != null,
                normalSaveBefore = normalBefore, normalSaveAfter = normalAfter,
                normalSavePreserved = true, memoryIsUnityAllocatedNotProcessResident = true,
                heatAndLongSessionAcceptance = "not measured", humanAcceptance = false };
            WriteCompleted(evidence);
        }


        IEnumerator PreparationProfile()
        {
            if(!screensInputValid||collection.Progress.UnlockedCount!=20||collection.Progress.Selected!=0||Json(Records())!=File.ReadAllText(Path.Combine(folder,"expected-records.json")))
            {Fail("Preparation profile requires isolated twenty-record clone starting at B01");yield break;}
            string recordsBefore=Json(Records());var g=collection.Game;
            // B01 has already been initialized by Start; firstTimedSelect is not twenty cold objects.
            for(int sweep=0;sweep<2;sweep++)for(int index=0;index<20;index++)
            {
                int conditions=DeviceConditionFlags();double started=Time.realtimeSinceStartupAsDouble;
                int gcBefore=GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2);
                long managedBefore=GC.GetTotalMemory(false),unityBefore=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                if(!collection.Select(index)){Fail("Preparation selection failed");yield break;}
                watch.Stop();
                int gcAfter=GC.CollectionCount(0)+GC.CollectionCount(1)+GC.CollectionCount(2);
                long managedAfter=GC.GetTotalMemory(false),unityAfter=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                var stages=SelectionPreparationReview.Snapshot();
                if(SelectionPreparationReview.Active||collection.SavePending){Fail("Selection timing/persistence did not finish");yield break;}
                yield return Ready();if(failed)yield break;
                preparation.Add(new{id=collection.Samples[index].ObjectId,sweep,firstTimedSelect=sweep==0,startupSelected=0,
                    b01InitializedBeforeTimedSelection=index==0,selectionMs=watch.Elapsed.TotalMilliseconds,stages,preparedCacheLoadedThisSelect=SelectionPreparationReview.PreparedCacheLoadedThisSelect,
                    gcCollectionsDuringSelect=gcAfter-gcBefore,managedBytesBefore=managedBefore,managedBytesAfter=managedAfter,unityAllocatedBytesBefore=unityBefore,unityAllocatedBytesAfter=unityAfter,
                    selectionToReadyMs=(Time.realtimeSinceStartupAsDouble-started)*1000,thermalState=conditions&255,lowPowerMode=(conditions&256)!=0,
                    retainedBodyWarmMs=g.Presentation.ReviewBodyWarmMs,bodyWarmCount=g.Presentation.ReviewBodyWarmCount,
                    sampleLandedWarmMs=g.Presentation.Sample.gameObject.activeSelf?g.Presentation.Sample.ReviewWarmMs:0});
                yield return null;
            }
            if(Json(Records())!=recordsBefore){Fail("Preparation diagnostic changed cloned best records");yield break;}
        }

        IEnumerator NativeProfile()
        {
            if(!screensInputValid||collection.Progress.UnlockedCount!=20||Json(Records())!=File.ReadAllText(Path.Combine(folder,"expected-records.json")))
            {Fail("Profile requires a fresh isolated clone of completed twenty records");yield break;}
            string recordsBefore=Json(Records());var g=collection.Game;
            // Leave time for the host Time Profiler to attach; no normal/user store or service is used.
            yield return new WaitForSecondsRealtime(20);
            bool focused=folder.EndsWith("-profile-v2",StringComparison.Ordinal)||folder.EndsWith("-profile-v3",StringComparison.Ordinal);
            foreach(int index in focused?new[]{10}:new[]{10,0,5})
            {
                if(!collection.Select(index)){Fail("Profile selection failed");yield break;}
                yield return Ready();if(failed)yield break;var d=collection.Samples[index].Definition();
                for(int i=0;i<(focused?1200:90);i++)
                {
                    decimal x=focused?((i/2)%2==0?0m:.001025939m):(i%3==0?0m:i%3==1?.217m:.701m);
                    var result=Scoring.Evaluate(d,new CutSnapshot(d.Id,d.ContentVersion,x,5000,TransformState.Identity,true));
                    g.Presentation.ResetAttempt();g.Presentation.Drop(1);ReviewCutTimings.Reset();
                    var watch=System.Diagnostics.Stopwatch.StartNew();g.Presentation.Cut(result);watch.Stop();
                    if(g.Presentation.FragmentCount!=2){Fail("Profile fragments failure");yield break;}
                    attempts.Add(new{id=d.Id,iteration=i,x=D(x),firstAfterSwitch=focused&&i%2==0,directPresentationMs=watch.Elapsed.TotalMilliseconds,meshMs=ReviewCutTimings.MeshMs,restMs=ReviewCutTimings.RestMs,clipMs=ReviewCutTimings.ClipMs,sortMs=ReviewCutTimings.SortMs,uploadMs=ReviewCutTimings.UploadMs});
                    yield return null;
                }
            }
            g.Presentation.ResetAttempt();g.Presentation.Drop(1);
            if(Json(Records())!=recordsBefore)Fail("Profile diagnostic changed cloned best records");
        }

        IEnumerator CarTopologyAB()
        {
            var filter=collection.Game.Presentation.Car.GetComponentsInChildren<MeshFilter>(true).First(f=>f.name=="Car voxel left");
            var source=filter.sharedMesh;var points=source.vertices;var shades=source.colors32;var sourceNormals=source.normals;
            var capVertices=new HashSet<int>(source.GetTriangles(1));var vertices=new List<Vector3>();var colors=new List<Color32>();var normals=new List<Vector3>();var tris=new[]{new List<int>(),new List<int>()};int changed=0;
            Action<Vector3,Vector3,int,int,Color32,int> append=(lo,hi,axis,sign,shade,sub)=>{
                int u=(axis+1)%3,v=(axis+2)%3;var a=lo;var b=a;b[u]=hi[u];var c=b;c[v]=hi[v];var d=a;d[v]=hi[v];int first=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);var normal=Vector3.zero;normal[axis]=sign;for(int j=0;j<4;j++){colors.Add(shade);normals.Add(normal);}
                tris[sub].Add(first);tris[sub].Add(first+(sign>0?1:2));tris[sub].Add(first+(sign>0?2:1));tris[sub].Add(first);tris[sub].Add(first+(sign>0?2:3));tris[sub].Add(first+(sign>0?3:2));
            };
            for(int i=0;i<points.Length;i+=4)
            {
                var lo=points[i];var hi=points[i+2];int axis=0;while(axis<2&&lo[axis]!=hi[axis])axis++;int sign=sourceNormals[i][axis]>0?1:-1,sub=capVertices.Contains(i)?1:0;var shade=shades[i];
                if(axis==2&&sign==-1&&sub==0&&lo==new Vector3(-.5625f,0,-.3125f)&&hi==new Vector3(-.09375f,.125f,-.3125f)&&shade.r==37&&shade.g==83&&shade.b==105)
                {
                    var h=hi;h.x=-.53125f;append(lo,h,axis,sign,shade,sub);var l=lo;l.x=-.53125f;append(l,hi,axis,sign,shade,sub);changed++;
                }
                else append(lo,hi,axis,sign,shade,sub);
            }
            if(changed!=1){Fail("Car topology diagnostic expected one original glass quad");yield break;}
            var diagnostic=new Mesh{name="Car isolated glass topology B",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};diagnostic.SetVertices(vertices);diagnostic.SetColors(colors);diagnostic.SetNormals(normals);diagnostic.subMeshCount=2;diagnostic.SetTriangles(tris[0],0,false);diagnostic.SetTriangles(tris[1],1,false);diagnostic.RecalculateBounds();
            carTopology=new{kind="Runtime diagnostic only; same pose/colour/plane, one glass quad split at x=-.53125; not source-asset or final-art approval",changedQuads=changed,beforeVertices=source.vertexCount,afterVertices=diagnostic.vertexCount,a="L02-result.png",b="L02-topology-b.png"};
            filter.sharedMesh=diagnostic;
            try{yield return Capture("L02-topology-b.png");}
            finally{filter.sharedMesh=source;Destroy(diagnostic);}
        }

        object[] Records() => Enumerable.Range(0, 20).Select(i => {
            var r = collection.Progress.Best(i);
            return (object)new { id = collection.Samples[i].ObjectId, version = r?.Snapshot.ContentVersion,
                x = r == null ? null : D(r.Snapshot.KnifeX), left = r == null ? null : D(r.LeftMass),
                right = r == null ? null : D(r.RightMass), accuracy = r?.AccuracyUnits,
                timeMs = r?.ElapsedMilliseconds, owner = collection.Progress.BestOwner(i) };
        }).ToArray();

        IEnumerator Ready()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (collection.Game.Session.Phase == AttemptPhase.Dropping && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            if (collection.Game.Session.Phase != AttemptPhase.Ready) Fail("READY timeout or interrupted app");
        }

        IEnumerator Frames(string id, string phase, int count)
        {
            var frames = new List<float>();
            for (int i = 0; i < count; i++) { yield return null; frames.Add(Time.unscaledDeltaTime * 1000); }
            Stats(id, phase, frames);
        }

        void Stats(string id, string phase, List<float> frames)
        {
            if (frames.Count == 0) return;
            frames.Sort();
            int conditions=DeviceConditionFlags();
            performance.Add(new { id, phase, frames = frames.Count, meanMs = frames.Average(),
                thermalState=conditions&255,lowPowerMode=(conditions&256)!=0,
                p95Ms = frames[Math.Min(frames.Count - 1, (int)(frames.Count * .95))], maxMs = frames.Last(),
                over33Ms = frames.Count(t => t > 33.34f), unityAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),
                unityReserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(), managed = GC.GetTotalMemory(false) });
        }

        Dictionary<string,string> NormalHashes()
        {
            var result = new Dictionary<string,string>();
            foreach (var name in new[] { "mvp-progress.json", "game-center-queue.json" })
            foreach (var suffix in new[] { "", ".bak", ".tmp" })
            {
                string file = Path.Combine(Application.persistentDataPath, name + suffix);
                if (!File.Exists(file)) continue;
                using (var sha = SHA256.Create()) result[name + suffix] = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-", "").ToLowerInvariant();
            }
            return result;
        }

        IEnumerator Capture(string name)
        {
            // The session can transition after this frame's Update/render. Read the next completed frame.
            yield return null;
            yield return new WaitForEndOfFrame();
            var g = collection.Game;
            string expected = name.EndsWith("-ready.png") ? "READY" : name.EndsWith("-result.png") ? "RESULT" : null;
            if (expected != null && (g.Session.Phase.ToString().ToUpperInvariant() != expected || g.Hud.State.text != expected ||
                g.Hud.ResultPanel.activeSelf != (expected == "RESULT")))
            { Fail("Capture phase/HUD mismatch " + name); yield break; }
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(Path.Combine(folder, name), image.EncodeToPNG()); }
            finally { Destroy(image); }
            captures.Add(new { name, phase = g.Session.Phase.ToString(), hudState = g.Hud.State.text,
                resultPanel = g.Hud.ResultPanel.activeSelf, width = Screen.width, height = Screen.height });
        }

        void Fail(string message)
        {
            failed = true;
            try { File.WriteAllText(Path.Combine(folder, mode + "-failure.txt"), message); }
            catch (Exception e) { Debug.LogError("TASK5_FAILURE_NOTE_WRITE_FAILED " + e.Message); }
            try
            {
                var game=collection==null?null:collection.Game;
                File.WriteAllText(Path.Combine(folder, mode + "-partial.json"),Json(new {
                    mode,status="failed",message,kind="Partial diagnostic; never completed performance acceptance",
                    selected=collection==null||collection.Progress==null?-1:collection.Progress.Selected,
                    phase=game==null||game.Session==null?"uninitialized":game.Session.Phase.ToString(),
                    focused=Application.isFocused,attempts,preparation,captures,focusEvents,
                    normalSaveBefore=normalBefore,humanAcceptance=false,budgetAcceptance=false }));
            }
            catch(Exception e){Debug.LogError("TASK5_PARTIAL_WRITE_FAILED " + e.Message);}
            Debug.LogError("TASK5_DEVICE_REVIEW_FAILED " + message);
        }

        void WriteCompleted(object evidence)
        {
            if (failed) throw new InvalidOperationException("A failed QA run cannot publish completed evidence");
            File.WriteAllText(Path.Combine(folder, mode + ".json"), Json(evidence));
            Debug.Log("TASK5_DEVICE_REVIEW_COMPLETE " + mode);
        }

        void RecordFocus(string kind,bool value)
        {
            var game=collection==null?null:collection.Game;
            focusEvents.Add(new {kind,value,seconds=Time.realtimeSinceStartupAsDouble,
                selected=collection==null||collection.Progress==null?-1:collection.Progress.Selected,
                phase=game==null||game.Session==null?"uninitialized":game.Session.Phase.ToString()});
        }
        void OnApplicationPause(bool value){RecordFocus("pause",value);}
        void OnApplicationFocus(bool value){RecordFocus("focus",value);}
        void OnDestroy()
        { SceneManager.sceneLoaded -= Loaded; Application.logMessageReceived -= Log; }
    }
}
#endif
