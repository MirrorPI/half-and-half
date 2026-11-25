using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HalfAndHalf.Core
{
    // The source attempts travel with the aggregate, including their account ownership.
    public sealed class OverallRecord
    {
        // Historical public identifiers also appear in archived device probes.
        public const string Id = "ALL29";
        public const string CurrentId = "ALL24";
        public const int RequiredCount = 29;
        public const int CurrentCount = 24;
        public static string IdFor(int count) => count == CurrentCount ? CurrentId : count == RequiredCount ? Id : null;
        public static bool IsAggregate(string id) => id == CurrentId || id == Id;
        public const string UnresolvedOwner = "!unresolved-account-claim";
        public string Version { get; }
        public IReadOnlyList<AttemptResult> Records { get; }
        public IReadOnlyList<string> Owners { get; }
        public int AccuracyUnits { get; }
        public long TimeMilliseconds { get; }
        public long Score => RankingScore.EncodeOverall(AccuracyUnits, TimeMilliseconds, Records.Count);
        OverallRecord(string version, AttemptResult[] records, string[] owners)
        {
            Version=version;Records=Array.AsReadOnly(records);Owners=Array.AsReadOnly(owners);
            AccuracyUnits=(int)decimal.Round(records.Sum(r=>(decimal)r.AccuracyUnits)/records.Length,0,MidpointRounding.AwayFromZero);
            TimeMilliseconds=records.Sum(r=>r.ElapsedMilliseconds);
        }
        public static string VersionFor(IReadOnlyList<ObjectDefinition> content)
        {
            if(IdFor(content.Count)==null)return null;
            string keys=string.Join("\n",content.Select(d=>d.Id+":"+d.ContentVersion));
            using(var hash=SHA256.Create())return "collection"+content.Count+"-v1-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(keys))).Replace("-","").Substring(0,16).ToLowerInvariant();
        }
        public static OverallRecord Calculate(IReadOnlyList<ObjectDefinition> content, IEnumerable<AttemptResult> records, IEnumerable<string> owners=null)
        {
            string version=VersionFor(content);if(version==null||records==null)return null;
            var source=records.ToArray();if(source.Length!=content.Count)return null;
            for(int i=0;i<content.Count;i++)if(!CollectionProgress.Valid(content[i],source[i]))return null;
            var accountOwners=owners?.Select(o=>o??"").ToArray()??new string[content.Count];
            if(accountOwners.Length!=content.Count)return null;
            return new OverallRecord(version,source,accountOwners);
        }
        public bool TrySubmissionOwner(out string owner)
        {
            var signed=Owners.Where(o=>!string.IsNullOrEmpty(o)).Distinct().ToArray();
            owner=signed.Length==1?signed[0]:"";return signed.Length<=1&&!signed.Contains(UnresolvedOwner);
        }
    }
}
