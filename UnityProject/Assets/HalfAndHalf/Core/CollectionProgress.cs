using System;
using System.Collections.Generic;

namespace HalfAndHalf.Core
{
    // One completed attempt owns both its accuracy and its time. Display ratios never enter this comparison.
    public sealed class CollectionProgress
    {
        public IReadOnlyList<ObjectDefinition> Content { get; }
        public int Selected { get; private set; }
        public OverallRecord OverallBest { get; private set; }
        public int RecordedCount => records.Count;
        public decimal CompletionPercent => decimal.Round(RecordedCount*100m/Content.Count,2,MidpointRounding.AwayFromZero);
        readonly Dictionary<string, AttemptResult> records = new Dictionary<string, AttemptResult>();
        readonly Dictionary<string, string> owners = new Dictionary<string, string>();
        public int UnlockedCount
        {
            get
            {
                int count=1;
                while(count<Content.Count && Best(count-1)!=null && Best(count-1).AccuracyUnits>=9000) count++;
                return count;
            }
        }
        public CollectionProgress(IEnumerable<ObjectDefinition> content)
        {
            var copy=new List<ObjectDefinition>(content ?? throw new ArgumentNullException(nameof(content)));
            var ids=new HashSet<string>();
            foreach(var item in copy) if(item==null || !ids.Add(item.Id)) throw new ArgumentException("Content IDs must be unique.");
            if(copy.Count==0) throw new ArgumentException("Collection must contain content.");
            Content=copy.AsReadOnly();
        }
        public bool Select(int index)
        {if(index<0 || index>=UnlockedCount)return false;Selected=index;return true;}
        public AttemptResult Best(int index)
        { if(index<0 || index>=Content.Count)return null;return records.TryGetValue(Content[index].Id,out var value)?value:null; }
        public string BestOwner(int index)
        {return Best(index)!=null&&owners.TryGetValue(Content[index].Id,out var owner)?owner:"";}
        public bool Record(AttemptResult result,string owner="")
        {
            int index=Index(result?.Snapshot?.ObjectId);
            if(index<0 || index>=UnlockedCount || !Valid(Content[index],result))return false;
            var best=Best(index);
            if(best!=null && Scoring.Compare(result,best)>=0)return false;
            records[result.Snapshot.ObjectId]=result;owners[result.Snapshot.ObjectId]=owner??"";
            UpdateOverall();return true;
        }
        void UpdateOverall()
        {
            if(OverallRecord.IdFor(Content.Count)==null||RecordedCount!=Content.Count)return;
            var source=new List<AttemptResult>();var accountOwners=new List<string>();
            for(int i=0;i<Content.Count;i++){source.Add(Best(i));accountOwners.Add(BestOwner(i));}
            KeepOverall(OverallRecord.Calculate(Content,source,accountOwners));
        }
        public void KeepOverall(OverallRecord candidate)
        {
            if(candidate==null||candidate.Version!=OverallRecord.VersionFor(Content))return;
            if(OverallBest==null||candidate.Score<OverallBest.Score)OverallBest=candidate;
        }
        public bool ClaimGuestRecords(Func<AttemptResult,string> resolveOwner)
        {
            bool changed=false;
            for(int i=0;i<Content.Count;i++)
            {
                var best=Best(i);if(best==null||!string.IsNullOrEmpty(BestOwner(i)))continue;
                string owner=resolveOwner(best);if(string.IsNullOrEmpty(owner))continue;
                owners[Content[i].Id]=owner;changed=true;
            }
            if(OverallBest!=null)
            {
                var claimed=new List<string>();bool snapshotChanged=false;
                for(int i=0;i<OverallBest.Records.Count;i++)
                {
                    string owner=OverallBest.Owners[i];
                    if(string.IsNullOrEmpty(owner)){owner=resolveOwner(OverallBest.Records[i]);snapshotChanged|=!string.IsNullOrEmpty(owner);}
                    claimed.Add(owner??"");
                }
                if(snapshotChanged){OverallBest=OverallRecord.Calculate(Content,OverallBest.Records,claimed);changed=true;}
            }
            return changed;
        }
        int Index(string id)
        {for(int i=0;i<Content.Count;i++)if(Content[i].Id==id)return i;return -1;}
        public static bool Valid(ObjectDefinition definition,AttemptResult result)
        {
            if(result?.Snapshot==null || !result.OnlineValid || result.ElapsedMilliseconds<0 || result.ElapsedMilliseconds>=120000)return false;
            try
            {
                var evaluated=Scoring.Evaluate(definition,result.Snapshot);
                return evaluated.AccuracyUnits==result.AccuracyUnits && evaluated.LeftMass==result.LeftMass && evaluated.RightMass==result.RightMass;
            }
            catch(Exception e) when(e is ArgumentException||e is OverflowException){return false;}
        }
        public static CollectionProgress Restore(IEnumerable<ObjectDefinition> content,IEnumerable<AttemptResult> bests,int selected,IReadOnlyDictionary<string,string> recordOwners=null)
        {
            var progress=new CollectionProgress(content);
            foreach(var result in bests)
            {
                int index=progress.Index(result?.Snapshot?.ObjectId);
                if(index<0 || !Valid(progress.Content[index],result))continue;
                var previous=progress.Best(index);
                if(previous==null || Scoring.Compare(result,previous)<0)
                {
                    progress.records[result.Snapshot.ObjectId]=result;
                    progress.owners[result.Snapshot.ObjectId]=recordOwners!=null&&recordOwners.TryGetValue(result.Snapshot.ObjectId,out var owner)?owner??"":"";
                }
            }
            progress.Select(selected);progress.UpdateOverall();return progress;
        }
    }
}
