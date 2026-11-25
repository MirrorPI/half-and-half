using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using HalfAndHalf.Core;
using UnityEngine;

#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using HalfAndHalf.Verification;
#endif

namespace HalfAndHalf.Services
{
    // A durable, checksummed staged file precedes the atomic replace. A backup retains the previous committed revision.
    public sealed class LocalProgressStore
    {
        public string Path { get; }
        public string Recovery { get; private set; }="new";
        readonly RejectedData rejected=new RejectedData();
        public bool PreservedFailure=>rejected.Preserved;
        public bool PreservationPending=>rejected.Pending;
        long revision;
        readonly IReadOnlyList<string> legacySelectionOrder;
        bool stagedPromotionPending;
        RecordData[] retained=Array.Empty<RecordData>();
        OverallData[] retainedOveralls=Array.Empty<OverallData>();
        static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
        [Serializable] sealed class Envelope { public string payload,sha256; }
        [Serializable] sealed class Data { public int schema=1,selected; public string selectedId; public long revision; public RecordData[] records,overall; public string overallVersion; public OverallData[] previousOveralls; }
        [Serializable] sealed class OverallData { public string version; public RecordData[] records; }
        [Serializable] sealed class RecordData
        {
            public string id,version,knifeX,left,right,x,y,scaleX,scaleY,rotation,ownerPlayerId;
            public int accuracy; public long time; public bool onlineValid;
        }
        public LocalProgressStore(string path, IReadOnlyList<string> legacySelectionOrder = null)
        {
            Path=System.IO.Path.GetFullPath(path);
            this.legacySelectionOrder=legacySelectionOrder;
        }
        public CollectionProgress Load(IEnumerable<ObjectDefinition> content)
        {
            var definitions=new List<ObjectDefinition>(content);CollectionProgress latest=null;long newest=-1;
            stagedPromotionPending=false;
            bool any=false;string latestFile=null;
            foreach(var suffix in new[]{"",".bak",".tmp"})
            {
                any|=File.Exists(Path+suffix);
                if(TryRead(Path+suffix,definitions,out var state,out var generation))
                {if(generation>newest){latest=state;newest=generation;latestFile=Path+suffix;Recovery=suffix==""?"primary":suffix==".bak"?"backup":"staged";}}
                else rejected.Add(Path+suffix);
            }
            if(latest!=null)
            {
                var raw=JsonUtility.FromJson<Data>(JsonUtility.FromJson<Envelope>(File.ReadAllText(latestFile)).payload);
                retained=raw.records.Where(r=>!definitions.Any(d=>d.Id==r.id&&d.ContentVersion==r.version)).ToArray();
                retainedOveralls=Snapshots(raw).ToArray();
                revision=newest;
                if(Recovery=="staged")
                {
                    stagedPromotionPending=true;
                    try{PromoteStaged(definitions);}
                    catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){ }
                }
                return latest;
            }
            retained=Array.Empty<RecordData>();retainedOveralls=Array.Empty<OverallData>();
            revision=0;Recovery=any?"reset":"new";return new CollectionProgress(definitions);
        }
        public void Save(CollectionProgress progress)
        {
            // A caller may save a restored collection through a fresh store instance.
            // Recover its revision and version history before replacing that file.
            if(revision==0&&File.Exists(Path))Load(progress.Content);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double started=SelectionPreparationReview.Start();
#endif

            // A recovered staged generation must become durable before its path can be truncated again.
            // If promotion is denied, leave that file intact and surface a retryable save failure.
            if(stagedPromotionPending)PromoteStaged(progress.Content);
            if(!rejected.Preserve())throw new IOException("Failed save could not be preserved; replacement deferred.");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            var records=new List<RecordData>();
            for(int i=0;i<progress.Content.Count;i++)
            {
                var r=progress.Best(i);if(r!=null)records.Add(Serialize(r,progress.BestOwner(i)));
            }
            records.AddRange(retained);
            var overall=progress.OverallBest;
            var snapshot=overall==null?retainedOveralls.FirstOrDefault():new OverallData{
                version=overall.Version,records=overall.Records.Select((r,i)=>Serialize(r,overall.Owners[i])).ToArray()};
            var data=new Data{selected=progress.Selected,selectedId=progress.Content[progress.Selected].Id,revision=checked(revision+1),records=records.ToArray(),
                overallVersion=snapshot?.version,overall=snapshot?.records,
                previousOveralls=retainedOveralls.Where(s=>s.version!=snapshot?.version).ToArray()};
            string payload=JsonUtility.ToJson(data);byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope{payload=payload,sha256=Hash(payload)}));
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.SaveSerializeMs,started);started=SelectionPreparationReview.Start();
#endif
            using(var stream=new FileStream(Path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.SaveFlushMs,started);started=SelectionPreparationReview.Start();
#endif
            if(File.Exists(Path))
            {
                // Never replace a sound backup with a primary already known to be corrupt.
                bool primaryValid=TryRead(Path,progress.Content,out _,out _);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
                SelectionPreparationReview.Finish(ref SelectionPreparationReview.SaveValidateMs,started);started=SelectionPreparationReview.Start();
#endif
                File.Replace(Path+".tmp",Path,primaryValid?Path+".bak":null);
            }
            else { File.Move(Path+".tmp",Path); if(!File.Exists(Path+".bak"))File.Copy(Path,Path+".bak"); }
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.SaveCommitMs,started);
#endif
            revision=data.revision;retainedOveralls=Snapshots(data).ToArray();
        }
        static IEnumerable<OverallData> Snapshots(Data data)
        {
            if(!string.IsNullOrEmpty(data.overallVersion))yield return new OverallData{version=data.overallVersion,records=data.overall};
            if(data.previousOveralls!=null)foreach(var snapshot in data.previousOveralls)yield return snapshot;
        }
        void PromoteStaged(IReadOnlyList<ObjectDefinition> content)
        {
            if(!rejected.Preserve())throw new IOException("Failed save could not be preserved; recovery deferred.");
            if(File.Exists(Path))File.Replace(Path+".tmp",Path,TryRead(Path,content,out _,out _)?Path+".bak":null);
            else File.Move(Path+".tmp",Path);
            stagedPromotionPending=false;
        }
        static string S(decimal value)=>value.ToString(Culture);
        static decimal D(string value)=>decimal.Parse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,Culture);
        static RecordData Serialize(AttemptResult r,string owner)
        {
            var s=r.Snapshot;var t=s.Transform;
            return new RecordData{id=s.ObjectId,version=s.ContentVersion,knifeX=S(s.KnifeX),left=S(r.LeftMass),right=S(r.RightMass),accuracy=r.AccuracyUnits,time=s.ElapsedMilliseconds,onlineValid=s.OnlineValid,x=S(t.X),y=S(t.Y),scaleX=S(t.ScaleX),scaleY=S(t.ScaleY),rotation=S(t.RotationDegrees),ownerPlayerId=owner};
        }
        static AttemptResult Deserialize(RecordData r)=>new AttemptResult(new CutSnapshot(r.id,r.version,D(r.knifeX),r.time,new TransformState(D(r.x),D(r.y),D(r.scaleX),D(r.scaleY),D(r.rotation)),r.onlineValid),D(r.left),D(r.right),r.accuracy);
        static string Hash(string value)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
        void RestoreSelection(CollectionProgress progress, Data data)
        {
            string id = data.selectedId;
            if (string.IsNullOrEmpty(id))
            {
                if (legacySelectionOrder == null || data.selected < 0 || data.selected >= legacySelectionOrder.Count)
                {
                    progress.Select(data.selected);
                    return;
                }
                id = legacySelectionOrder[data.selected];
            }
            for (int i = 0; i < progress.Content.Count; i++)
                if (progress.Content[i].Id == id) { progress.Select(i); return; }

            // A retired stage resumes at its next surviving, unlocked neighbour.
            if (legacySelectionOrder == null) return;
            int position = legacySelectionOrder.ToList().IndexOf(id);
            if (position < 0) return;
            var next = legacySelectionOrder.Skip(position + 1).FirstOrDefault(candidate => progress.Content.Any(d => d.Id == candidate));
            int index = next == null ? progress.Content.Count - 1 : progress.Content.ToList().FindIndex(d => d.Id == next);
            progress.Select(Math.Min(index, progress.UnlockedCount - 1));
        }
        bool TryRead(string file,IReadOnlyList<ObjectDefinition> content,out CollectionProgress progress,out long generation)
        {
            progress=null;generation=0;
            try
            {
                if(!File.Exists(file)||new FileInfo(file).Length>1024*1024)return false;
                var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
                if(envelope?.payload==null||Hash(envelope.payload)!=envelope.sha256)return false;
                var data=JsonUtility.FromJson<Data>(envelope.payload);
                if(data==null||data.schema!=1||data.revision<1||data.records==null||data.records.Length>128)return false;
                var results=new List<AttemptResult>();var ids=new HashSet<string>();var owners=new Dictionary<string,string>();
                foreach(var r in data.records)
                {
                    if(r==null||!ids.Add(r.id+"\n"+r.version))return false;
                    ObjectDefinition definition=null;foreach(var c in content)if(c.Id==r.id){definition=c;break;}
                    if(definition==null||definition.ContentVersion!=r.version)continue; // Content versions own their records.
                    var result=Deserialize(r);
                    if(!CollectionProgress.Valid(definition,result))return false;
                    results.Add(result);owners[r.id]=r.ownerPlayerId??"";
                }
                progress=CollectionProgress.Restore(content,results,0,owners);
                RestoreSelection(progress,data);
                var versions=new HashSet<string>();
                string activeVersion=OverallRecord.VersionFor(content);
                foreach(var snapshot in Snapshots(data))
                {
                    if(snapshot==null||string.IsNullOrEmpty(snapshot.version)||!versions.Add(snapshot.version))return false;
                    if(snapshot.version!=activeVersion)continue;
                    if(snapshot.records==null||snapshot.records.Length!=content.Count||snapshot.records.Any(r=>r==null))return false;
                    var overall=OverallRecord.Calculate(content,snapshot.records.Select(Deserialize),snapshot.records.Select(r=>r.ownerPlayerId));
                    if(overall==null)return false;
                    progress.KeepOverall(overall);
                }
                generation=data.revision;return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException||e is FormatException||e is OverflowException)
            { return false; }
        }
    }
}
