using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HalfAndHalf.Core;
using UnityEngine;

namespace HalfAndHalf.Services
{
    // Keep acknowledged watermarks as well as pending scores; restarting or changing account must not replay old local bests.
    public sealed class SubmissionQueue
    {
        readonly string path;
        readonly Dictionary<string,ObjectDefinition> content;
        readonly string overallVersion;
        readonly string overallId;
        readonly ObjectDefinition[] orderedContent;
        readonly RejectedData rejected=new RejectedData();
        readonly List<QueuedScore> entries=new List<QueuedScore>();
        readonly List<QueuedScore> retained=new List<QueuedScore>();
        long revision;
        bool promoteStaged;
        public bool SavePending { get; private set; }
        public string Recovery { get; private set; }="new";
        public bool PreservedFailure=>rejected.Preserved;
        public bool PreservationPending=>rejected.Pending;
        public QueuedScore[] Entries=>entries.Select(Copy).ToArray();
        [Serializable] sealed class Envelope { public string payload,sha256; }
        [Serializable] sealed class Data { public int schema=2; public long revision; public QueuedScore[] entries; }
        public SubmissionQueue(string file,IEnumerable<ObjectDefinition> definitions,string aggregateVersion=null)
        {
            path=Path.GetFullPath(file);orderedContent=definitions.ToArray();content=orderedContent.ToDictionary(d=>d.Id);overallVersion=aggregateVersion;overallId=OverallRecord.IdFor(orderedContent.Length);
            Data newest=null;string source=null;
            var generations=new List<Data>();
            foreach(var suffix in new[]{"",".bak",".tmp"})
            {
                if(Read(path+suffix,out var data))
                {
                    generations.Add(data);
                    if(data.entries.Any(e=>CurrentContent(e)&&e.pending&&Reason(e)!=null&&Reason(e)!="missing-evidence"))rejected.Add(path+suffix);
                    if(newest==null||data.revision>newest.revision){newest=data;source=suffix;}
                }
                else rejected.Add(path+suffix);
            }
            if(newest!=null)
            {
                revision=newest.revision;
                entries.AddRange(newest.entries.Where(CurrentContent).Select(Copy));
                retained.AddRange(newest.entries.Where(e=>!CurrentContent(e)).Select(Copy));
                promoteStaged=source==".tmp";
                Recovery=source==""?"primary":source==".bak"?"backup":"staged";
                foreach(var item in entries.Where(e=>e.pending))
                {
                    string reason=Reason(item);
                    if(reason!=null)
                    {
                        // Recover only identical candidate evidence. Never replace its score/owner or lose watermarks.
                        var backup=generations.SelectMany(g=>g.entries).FirstOrDefault(e=>SameKey(e,item)&&e.score==item.score&&Reason(e)==null);
                        if(backup!=null){item.evidence=Copy(backup).evidence;item.needsEvidence=false;reason=null;Recovery="backup-evidence";}
                    }
                    item.heldReason=reason;
                }
                SavePending=newest.schema==1||Recovery!="primary"||entries.Any(e=>e.pending&&e.heldReason!=null);
            }
            else if(rejected.Preserved||rejected.Pending)Recovery="reset";
        }
        bool CurrentContent(QueuedScore e)=>e.objectId==overallId?overallVersion!=null&&e.contentVersion==overallVersion:content.TryGetValue(e.objectId,out var d)&&d.ContentVersion==e.contentVersion;
        public bool Offer(AttemptResult result,string playerId)
        {
            if(result?.Snapshot==null || !content.TryGetValue(result.Snapshot.ObjectId,out var d) || !CollectionProgress.Valid(d,result))return false;
            var next=new QueuedScore{objectId=d.Id,contentVersion=d.ContentVersion,playerId=playerId??"",score=RankingScore.Encode(result.AccuracyUnits,result.ElapsedMilliseconds),pending=true,evidence=new[]{SubmissionAttempt.From(result,playerId)}};
            return Merge(next);
        }
        public bool Reconcile(AttemptResult best)
        {return ReconcileOwned(best,"");}
        public string ClaimOwner(AttemptResult source,string unclaimedOwner,OverallRecord retainedOverall=null)
        {
            long score=RankingScore.Encode(source.AccuracyUnits,source.ElapsedMilliseconds);
            var claimed=entries.Where(e=>e.objectId==source.Snapshot.ObjectId&&e.contentVersion==source.Snapshot.ContentVersion&&e.playerId!=""&&e.score<=score).Select(e=>e.playerId).Distinct().ToArray();
            if(claimed.Length==0&&retainedOverall!=null&&retainedOverall.Records.Any(r=>r.Snapshot.ObjectId==source.Snapshot.ObjectId&&r.Snapshot.ContentVersion==source.Snapshot.ContentVersion&&RankingScore.Encode(r.AccuracyUnits,r.ElapsedMilliseconds)==score))
                claimed=entries.Where(e=>e.objectId==overallId&&e.contentVersion==retainedOverall.Version&&e.score==retainedOverall.Score&&e.playerId!="").Select(e=>e.playerId).Distinct().ToArray();
            return claimed.Length==0?unclaimedOwner:claimed.Length==1?claimed[0]:OverallRecord.UnresolvedOwner;
        }
        public bool ReconcileOverall(OverallRecord best)
        {
            if(best==null||overallVersion==null||best.Version!=overallVersion||!best.TrySubmissionOwner(out _))return false;
            var claimed=best.Records.Select((r,i)=>string.IsNullOrEmpty(best.Owners[i])?ClaimOwner(r,"",best):best.Owners[i]).Where(o=>!string.IsNullOrEmpty(o)).Distinct().ToArray();
            if(claimed.Length>1||claimed.Contains(OverallRecord.UnresolvedOwner))return false;
            string owner=claimed.Length==1?claimed[0]:"";
            var next=new QueuedScore{objectId=overallId,contentVersion=overallVersion,playerId=owner,score=best.Score,pending=true,
                evidence=best.Records.Select(r=>SubmissionAttempt.From(r,owner)).ToArray()};
            if(Reason(next)!=null)return false;
            if(AttachLegacy(next))return true;
            if(entries.Any(e=>e.objectId==overallId&&e.contentVersion==overallVersion&&e.score<=best.Score))return false;
            return Merge(next);
        }
        public bool ReconcileOwned(AttemptResult best,string owner)
        {
            if(best?.Snapshot==null || !content.TryGetValue(best.Snapshot.ObjectId,out var d) || !CollectionProgress.Valid(d,best))return false;
            var source=new QueuedScore{objectId=d.Id,contentVersion=d.ContentVersion,playerId=owner??"",score=RankingScore.Encode(best.AccuracyUnits,best.ElapsedMilliseconds),evidence=new[]{SubmissionAttempt.From(best,owner)}};
            if(AttachLegacy(source))return true;
            // Local progress is shared across accounts. Only a genuinely new score can become an unclaimed guest score.
            if(entries.Any(e=>e.objectId==best.Snapshot.ObjectId && e.contentVersion==best.Snapshot.ContentVersion && e.score<=RankingScore.Encode(best.AccuracyUnits,best.ElapsedMilliseconds)))return false;
            return Offer(best,owner);
        }
        bool AttachLegacy(QueuedScore source)
        {
            var old=entries.Find(e=>e.pending&&(e.needsEvidence||e.evidence==null)&&SameKey(e,source)&&e.score==source.score);
            if(old==null)return false;
            old.evidence=Copy(source).evidence;old.needsEvidence=false;old.heldReason=Reason(old);SavePending=true;return old.heldReason==null;
        }
        string Reason(QueuedScore item)
        {
            if(item.needsEvidence||item.evidence==null)return "missing-evidence";
            try
            {
                if(item.playerId==OverallRecord.UnresolvedOwner||item.evidence.Any(e=>e==null||e.owner==null||e.owner!=item.playerId))return "owner-mismatch";
                if(item.objectId==overallId)
                {
                    if(overallVersion==null||item.contentVersion!=overallVersion)return "content-mismatch";
                    var overall=OverallRecord.Calculate(orderedContent,item.evidence.Select(e=>e.Result()),item.evidence.Select(e=>e.owner));
                    return overall!=null&&overall.Version==item.contentVersion&&overall.Score==item.score&&overall.TrySubmissionOwner(out var owner)&&owner==item.playerId?null:"result-mismatch";
                }
                if(!content.TryGetValue(item.objectId,out var definition)||definition.ContentVersion!=item.contentVersion)return "content-mismatch";
                if(item.evidence.Length!=1)return "result-mismatch";
                var result=item.evidence[0].Result();
                return CollectionProgress.Valid(definition,result)&&RankingScore.Encode(result.AccuracyUnits,result.ElapsedMilliseconds)==item.score?null:"result-mismatch";
            }
            catch(Exception e) when(e is ArgumentException||e is FormatException||e is OverflowException){return "result-mismatch";}
        }
        public bool CanSubmit(QueuedScore item,string player)
        {
            if(item==null||!item.pending||string.IsNullOrEmpty(player)||item.playerId!=player)return false;
            string reason=Reason(item);
            if(reason==null)return true;
            var current=entries.Find(e=>SameKey(e,item)&&e.score==item.score);
            if(current!=null&&current.heldReason!=reason){current.heldReason=reason;SavePending=true;}
            return false;
        }
        bool Merge(QueuedScore next)
        {
            var old=entries.Find(e=>SameKey(e,next));
            if(old!=null && old.score<=next.score)return false;
            if(old!=null)entries.Remove(old);entries.Add(Copy(next));SavePending=true;return true;
        }
        public void BindGuest(string playerId)
        {
            if(string.IsNullOrEmpty(playerId))return;
            foreach(var guest in entries.Where(e=>e.pending&&e.playerId==""&&Reason(e)==null).ToArray())
            {entries.Remove(guest);var next=Copy(guest);next.playerId=playerId;foreach(var evidence in next.evidence)evidence.owner=playerId;Merge(next);SavePending=true;}
        }
        public void Acknowledge(QueuedScore submitted)
        {
            var current=entries.Find(e=>SameKey(e,submitted));
            if(current!=null && current.score==submitted.score){current.pending=false;SavePending=true;}
        }
        public bool Persist()
        {
            try
            {
                if(!rejected.Preserve()){SavePending=true;return false;}
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Preserve a recovered complete staged generation before truncating its path for the next transaction.
                if(promoteStaged){Commit();promoteStaged=false;}
                var data=new Data{revision=checked(revision+1),entries=Entries.Concat(retained.Select(Copy)).ToArray()};string payload=JsonUtility.ToJson(data);
                byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope{payload=payload,sha256=Hash(payload)}));
                using(var stream=new FileStream(path+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))
                {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                Commit();revision=data.revision;SavePending=false;return true;
            }
            catch(Exception e) when(e is IOException || e is UnauthorizedAccessException){SavePending=true;return false;}
        }
        void Commit()
        {
            if(File.Exists(path))File.Replace(path+".tmp",path,Read(path,out _)?path+".bak":null);
            else{File.Move(path+".tmp",path);if(!File.Exists(path+".bak"))File.Copy(path,path+".bak");}
        }
        static bool SameKey(QueuedScore a,QueuedScore b)=>a.objectId==b.objectId && a.contentVersion==b.contentVersion && a.playerId==b.playerId;
        static QueuedScore Copy(QueuedScore e)=>new QueuedScore{objectId=e.objectId,contentVersion=e.contentVersion,playerId=e.playerId,score=e.score,pending=e.pending,evidence=e.evidence?.Select(a=>a?.Copy()).ToArray(),heldReason=e.heldReason,needsEvidence=e.needsEvidence};
        static string Hash(string payload)
        {using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");}
        static bool Read(string file,out Data data)
        {
            data=null;
            try
            {
                if(!File.Exists(file)||new FileInfo(file).Length>8*1024*1024)return false;
                var env=JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
                if(env?.payload==null||Hash(env.payload)!=env.sha256)return false;
                var candidate=JsonUtility.FromJson<Data>(env.payload);
                if(candidate?.entries==null||(candidate.schema!=1&&candidate.schema!=2)||candidate.revision<1||candidate.entries.Length>1000)return false;
                var keys=new HashSet<string>();
                foreach(var e in candidate.entries)
                {
                    if(e==null || string.IsNullOrEmpty(e.objectId)||string.IsNullOrEmpty(e.contentVersion)||e.playerId==null || !(OverallRecord.IsAggregate(e.objectId)?RankingScore.TryDecodeOverall(e.score,out _,out _,e.objectId==OverallRecord.CurrentId?OverallRecord.CurrentCount:OverallRecord.RequiredCount):RankingScore.TryDecodeAttempt(e.score,out _,out _)) || !keys.Add(e.playerId+"\n"+e.objectId+"\n"+e.contentVersion))return false;
                    if(e.evidence==null||(candidate.schema==1&&e.evidence.Length==0))e.needsEvidence=true;
                }
                data=candidate;return true;
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){return false;}
        }
    }
}
