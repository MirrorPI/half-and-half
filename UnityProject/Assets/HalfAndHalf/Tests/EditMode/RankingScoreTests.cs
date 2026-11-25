using System;
using System.Reflection;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class RankingScoreTests
    {
        static Type Codec
        {
            get
            {
                var type=typeof(Core.Scoring).Assembly.GetType("HalfAndHalf.Core.RankingScore");
                Assert.That(type,Is.Not.Null,"Task4 needs the official 64-bit ranking codec.");return type;
            }
        }
        static long Encode(int accuracy,long time)
        {return (long)Codec.GetMethod("Encode").Invoke(null,new object[]{accuracy,time});}
        static bool Decode(long score,out int accuracy,out long time)
        {object[] args={score,0,0L};bool ok=(bool)Codec.GetMethod("TryDecodeAttempt").Invoke(null,args);accuracy=(int)args[1];time=(long)args[2];return ok;}
        [TestCase(10000,0,0L)] [TestCase(10000,119999,119999L)]
        [TestCase(9999,0,10000000L)] [TestCase(0,119999,100000119999L)]
        [TestCase(9999,2410,10002410L)]
        public void OfficialUnitsAndSameAttemptTimeRoundTrip(int accuracy,long time,long score)
        {Assert.That(Encode(accuracy,time),Is.EqualTo(score));Assert.That(Decode(score,out var a,out var t),Is.True);Assert.That(a,Is.EqualTo(accuracy));Assert.That(t,Is.EqualTo(time));}
        [Test] public void OneAccuracyUnitOutranksEveryValidTimeDifference()
        {for(int a=1;a<=10000;a++)Assert.That(Encode(a,119999),Is.LessThan(Encode(a-1,0)));}
        [Test] public void EqualOfficialAccuracyUsesTimeAndExactTiesRemainEqual()
        {Assert.That(Encode(9999,1010),Is.LessThan(Encode(9999,1220)));Assert.That(Encode(9999,1010),Is.EqualTo(Encode(9999,1010)));}
        [TestCase(-1,0)] [TestCase(10001,0)] [TestCase(10000,-1)] [TestCase(10000,120000)] [TestCase(0,long.MaxValue)]
        public void RejectInvalidAttemptRange(int accuracy,long time)
        {var error=Assert.Throws<TargetInvocationException>(()=>Encode(accuracy,time));Assert.That(error.InnerException,Is.InstanceOf<ArgumentOutOfRangeException>());}
        [TestCase(-1L)] [TestCase(long.MaxValue)] [TestCase(120000L)] [TestCase(100001000000L)]
        public void InvalidRemoteScoreIsNotDisplayedAsARecord(long score)
        {Assert.That(Decode(score,out _,out _),Is.False);}
    }
}
