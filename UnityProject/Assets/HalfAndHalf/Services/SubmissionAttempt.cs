using System;
using System.Globalization;
using HalfAndHalf.Core;

namespace HalfAndHalf.Services
{
    // Decimal strings preserve the exact CUT input and result; this is consistency evidence, not a signature.
    [Serializable] public sealed class SubmissionAttempt
    {
        public string id,version,owner,knifeX,left,right,x,y,scaleX,scaleY,rotation;
        public int accuracy;
        public long time;
        public bool onlineValid;
        static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
        static string S(decimal value)=>value.ToString(Culture);
        static decimal D(string value)=>decimal.Parse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,Culture);
        public static SubmissionAttempt From(AttemptResult r,string player)
        {
            var s=r.Snapshot;var t=s.Transform;
            return new SubmissionAttempt{id=s.ObjectId,version=s.ContentVersion,owner=player??"",knifeX=S(s.KnifeX),left=S(r.LeftMass),right=S(r.RightMass),accuracy=r.AccuracyUnits,time=s.ElapsedMilliseconds,onlineValid=s.OnlineValid,x=S(t.X),y=S(t.Y),scaleX=S(t.ScaleX),scaleY=S(t.ScaleY),rotation=S(t.RotationDegrees)};
        }
        public AttemptResult Result()=>new AttemptResult(new CutSnapshot(id,version,D(knifeX),time,new TransformState(D(x),D(y),D(scaleX),D(scaleY),D(rotation)),onlineValid),D(left),D(right),accuracy);
        public SubmissionAttempt Copy()=>(SubmissionAttempt)MemberwiseClone();
    }
}
