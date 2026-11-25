using System;

namespace HalfAndHalf.Core
{
    // Smaller scores rank first. One official accuracy unit outweighs every valid attempt time.
    public static class RankingScore
    {
        public const long Base = 10000000;
        public const long AttemptLimit = 120000;
        public const long MaximumAttemptScore = 10000 * Base + AttemptLimit - 1;
        public static long Encode(int accuracyUnits,long timeMs)
        {
            if(accuracyUnits<0 || accuracyUnits>10000)throw new ArgumentOutOfRangeException(nameof(accuracyUnits));
            if(timeMs<0 || timeMs>=AttemptLimit)throw new ArgumentOutOfRangeException(nameof(timeMs));
            return checked((10000L-accuracyUnits)*Base+timeMs);
        }
        public static bool TryDecodeAttempt(long score,out int accuracyUnits,out long timeMs)
        {
            accuracyUnits=0;timeMs=0;
            if(score<0 || score>MaximumAttemptScore || score%Base>=AttemptLimit)return false;
            accuracyUnits=10000-(int)(score/Base);timeMs=score%Base;return true;
        }
        public static long EncodeOverall(int accuracyUnits,long timeMs,int count=OverallRecord.RequiredCount)
        {
            if(accuracyUnits<0||accuracyUnits>10000)throw new ArgumentOutOfRangeException(nameof(accuracyUnits));
            if(OverallRecord.IdFor(count)==null||timeMs<0||timeMs>=AttemptLimit*count)throw new ArgumentOutOfRangeException(nameof(timeMs));
            return checked((10000L-accuracyUnits)*Base+timeMs);
        }
        public static bool TryDecodeOverall(long score,out int accuracyUnits,out long timeMs,int count=OverallRecord.RequiredCount)
        {
            accuracyUnits=0;timeMs=0;
            if(OverallRecord.IdFor(count)==null||score<0||score/Base>10000||score%Base>=AttemptLimit*count)return false;
            accuracyUnits=10000-(int)(score/Base);timeMs=score%Base;return true;
        }
    }
}
