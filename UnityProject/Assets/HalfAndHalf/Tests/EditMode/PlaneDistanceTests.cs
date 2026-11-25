using System;
using System.Reflection;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class PlaneDistanceTests
    {
        static Func<Vector3,Vector3,float> Distance()
        {
            var method=typeof(MeshSection).GetMethod("PlaneDistanceSquared",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null,"Preserve squared-distance arithmetic while removing intermediate Vector3 values");
            return (Func<Vector3,Vector3,float>)method.CreateDelegate(typeof(Func<Vector3,Vector3,float>));
        }
        static void Equal(Func<Vector3,Vector3,float> distance,Vector3 a,Vector3 b)
        {
            float expected=(a-b).sqrMagnitude,actual=distance(a,b);
            if(float.IsNaN(expected))Assert.That(float.IsNaN(actual),Is.True);
            else Assert.That(BitConverter.SingleToInt32Bits(actual),Is.EqualTo(BitConverter.SingleToInt32Bits(expected)));
            Assert.That(actual<1e-10f,Is.EqualTo(expected<1e-10f));
        }
        [Test] public void ScalarDistanceKeepsSignedCellAndStrictEpsilonBoundaries()
        {
            var distance=Distance();int cases=0;
            for(int cell=-800000;cell<=800000;cell+=1973)
                foreach(double fraction in new[]{0,.25,.49949,.4995,.5,.5005,.50051,.75,1})
                    foreach(double delta in new[]{-.000010001,-.00001,-.000009999,0,.000009999,.00001,.000010001})
                    {
                        float p=(float)((cell+fraction)*.00002),q=(float)((double)p+delta);
                        Equal(distance,new Vector3(0,p,p),new Vector3(0,q,q));cases++;
                    }
            Assert.That(cases,Is.GreaterThan(50000));
        }
        [Test] public void ScalarDistanceKeepsFiniteThreeAxisFloatBits()
        {
            var distance=Distance();var random=new System.Random(47017);
            for(int i=0;i<10000;i++)
            {
                var a=new Vector3((float)(random.NextDouble()*32-16),(float)(random.NextDouble()*32-16),(float)(random.NextDouble()*32-16));
                var b=i%2==0?new Vector3(a.x+(float)(random.NextDouble()*.00003-.000015),a.y+(float)(random.NextDouble()*.00003-.000015),a.z+(float)(random.NextDouble()*.00003-.000015)):new Vector3((float)(random.NextDouble()*32-16),(float)(random.NextDouble()*32-16),(float)(random.NextDouble()*32-16));
                Equal(distance,a,b);
            }
        }
        [Test] public void ScalarDistanceKeepsZeroInfinityNanAndOverflowRelation()
        {
            var distance=Distance();float[] values={0f,BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),float.Epsilon,-float.Epsilon,1e-5f,-1e-5f,float.MaxValue,float.MinValue,float.PositiveInfinity,float.NegativeInfinity,float.NaN};
            foreach(float a in values)foreach(float b in values)
            {
                Equal(distance,new Vector3(a,0,b),new Vector3(b,a,0));
                Equal(distance,new Vector3(0,a,0),new Vector3(0,b,0));
            }
        }
    }
}
