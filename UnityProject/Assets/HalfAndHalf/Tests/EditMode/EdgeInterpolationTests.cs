using System;
using System.Reflection;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class EdgeInterpolationTests
    {
        const BindingFlags All=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static void Bits(Vector3 actual,Vector3 expected)
        {
            for(int axis=0;axis<3;axis++)
                if(float.IsNaN(expected[axis]))Assert.That(float.IsNaN(actual[axis]),Is.True);
                else Assert.That(BitConverter.SingleToInt32Bits(actual[axis]),Is.EqualTo(BitConverter.SingleToInt32Bits(expected[axis])));
        }
        static MethodInfo Helper()
        {
            var method=typeof(MeshSection).GetMethod("InterpolateSectionVertex",All);
            Assert.That(method,Is.Not.Null,"Keep Unity interpolation and normalization while reducing intermediate Vector3 work");return method;
        }
        static void Check(MethodInfo method,Vector3 a,Vector3 b,Vector3 na,Vector3 nb,float t)
        {
            var value=method.Invoke(null,new object[]{a,b,na,nb,t});var type=value.GetType();
            Bits((Vector3)type.GetField("Position",All).GetValue(value),Vector3.LerpUnclamped(a,b,t));
            Bits((Vector3)type.GetField("Normal",All).GetValue(value),Vector3.LerpUnclamped(na,nb,t).normalized);
        }
        [Test] public void ScalarEdgeKeepsFiniteInterpolationAndNormalizationBits()
        {
            var method=Helper();var random=new System.Random(68273);
            Func<Vector3> point=()=>new Vector3((float)(random.NextDouble()*32-16),(float)(random.NextDouble()*32-16),(float)(random.NextDouble()*32-16));
            for(int i=0;i<3000;i++)Check(method,point(),point(),point(),point(),(float)(random.NextDouble()*3-1));
        }
        [Test] public void ScalarEdgeKeepsEndpointZeroThresholdAndNonfiniteRelations()
        {
            var method=Helper();float zero=BitConverter.Int32BitsToSingle(unchecked((int)0x80000000));
            float[] values={0,zero,float.Epsilon,-float.Epsilon,BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1e-5f)-1),1e-5f,BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(1e-5f)+1),1,-1,float.MaxValue,float.PositiveInfinity,float.NegativeInfinity,float.NaN};
            foreach(float n in values)foreach(float t in new[]{0f,zero,1f,-1f,.5f,2f,float.PositiveInfinity,float.NaN})
            {
                Check(method,new Vector3(zero,1,-2),new Vector3(0,-3,4),new Vector3(n,0,zero),new Vector3(n,zero,0),t);
                Check(method,Vector3.zero,Vector3.one,new Vector3(n,n,n),new Vector3(-n,n,zero),t);
            }
        }
        [Test] public void DirectedCacheKeepsOperandsAndInvalidatesAtTheNextCut()
        {
            var mesh=new Mesh();
            try
            {
                Vector3[] p={new Vector3(-.7f,.4f,-.3f),new Vector3(.9f,-.6f,.2f),Vector3.up};
                Vector3[] n={new Vector3(.1234f,.8997f,.0567f),new Vector3(-.6771f,.2551f,.8713f),Vector3.forward};
                mesh.vertices=p;mesh.normals=n;mesh.triangles=new[]{0,1,2,1,0,2};
                var type=typeof(MeshSection).GetNestedType("Prepared",All);var prepared=Activator.CreateInstance(type,All,null,new object[]{mesh},null);
                foreach(float x in new[]{.217f,0f,-.217f})
                {
                    type.GetMethod("BeginCut",All).Invoke(prepared,new object[]{x});
                    foreach(int slot in new[]{0,3,0,3})
                    {
                        int a=slot==0?0:1,b=slot==0?1:0;float t=(x-p[a].x)/(p[b].x-p[a].x);
                        var value=type.GetMethod("Intersection",All).Invoke(prepared,new object[]{slot,x});var vertex=value.GetType();
                        Bits((Vector3)vertex.GetField("Position",All).GetValue(value),Vector3.LerpUnclamped(p[a],p[b],t));
                        Bits((Vector3)vertex.GetField("Normal",All).GetValue(value),Vector3.LerpUnclamped(n[a],n[b],t).normalized);
                    }
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
