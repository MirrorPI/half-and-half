using System;
using System.Linq;
using System.Reflection;
using HalfAndHalf.Presentation;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class NativeCrossingCopyTests
    {
        const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        sealed class Fixture:IDisposable
        {
            internal readonly Type Type;internal readonly object Owner;internal readonly MethodInfo Copy;readonly int before;
            internal Fixture(int count=2)
            {
                Type=typeof(MeshSection).Assembly.GetType("HalfAndHalf.Presentation.NativeCutTopology");Copy=Type.GetMethod("CopyCrossingSlots",All);
                Assert.That(Copy,Is.Not.Null,"Copy the completed native slot prefix once while retaining bounds and lifetime checks");
                before=(int)Type.GetProperty("ActiveOwners",All).GetValue(null);
                var triangle=typeof(MeshSection).GetNestedType("CutTriangle",All);var values=Array.CreateInstance(triangle,count);
                for(int i=0;i<count;i++)
                {
                    var t=Activator.CreateInstance(triangle);triangle.GetField("A",All).SetValue(t,i==0?0:2);triangle.GetField("B",All).SetValue(t,1);triangle.GetField("C",All).SetValue(t,i==0?2:3);triangle.GetField("Min",All).SetValue(t,-1f);triangle.GetField("Max",All).SetValue(t,1f);values.SetValue(t,i);
                }
                Owner=Activator.CreateInstance(Type,All,null,new object[]{values},null);
                Type.GetMethod("Run",All).Invoke(Owner,new object[]{0f,true,0,2,2,new int[count*6]});
            }
            internal int Count=>(int)Type.GetProperty("CrossingSlotCount",All).GetValue(Owner);
            internal int[] Slots()=>Enumerable.Range(0,Count).Select(i=>(int)Type.GetMethod("GetCrossingSlot",All).Invoke(Owner,new object[]{i})).ToArray();
            internal int CopyTo(int[] target)=>(int)Copy.Invoke(Owner,new object[]{target});
            internal void Release()=>((IDisposable)Owner).Dispose();
            public void Dispose(){Release();Assert.That((int)Type.GetProperty("ActiveOwners",All).GetValue(null),Is.EqualTo(before));}
        }
        [TestCase(0),TestCase(2)] public void CopyKeepsCompletedSlotOrderTailAndNativeOwnership(int triangles)
        {
            using(var f=new Fixture(triangles))
            {
                var expected=f.Slots();var target=Enumerable.Repeat(-727,expected.Length+3).ToArray();
                Assert.That(f.CopyTo(target),Is.EqualTo(expected.Length));CollectionAssert.AreEqual(expected,target.Take(expected.Length));Assert.That(target.Skip(expected.Length).All(x=>x==-727),Is.True);
                if(expected.Length>0)target[0]=-193;CollectionAssert.AreEqual(expected,f.Slots(),"Managed scratch must not alias or mutate the native result");
                Assert.That(f.CopyTo(target),Is.EqualTo(expected.Length));CollectionAssert.AreEqual(expected,target.Take(expected.Length));
                // A subsequent completed job must replace the count, including nonempty -> empty.
                f.Type.GetMethod("Run",All).Invoke(f.Owner,new object[]{float.NegativeInfinity,true,0,0,0,new int[triangles*6]});
                Array.Fill(target,-193);Assert.That(f.CopyTo(target),Is.Zero);Assert.That(target.All(x=>x==-193),Is.True);
            }
        }
        [Test] public void InvalidTargetIsRejectedBeforeAnyWrite()
        {
            using(var f=new Fixture())
            {
                var e=Assert.Throws<TargetInvocationException>(()=>f.CopyTo(null));Assert.That(e.InnerException,Is.TypeOf<ArgumentNullException>());
                var target=Enumerable.Repeat(-727,f.Count-1).ToArray();e=Assert.Throws<TargetInvocationException>(()=>f.CopyTo(target));Assert.That(e.InnerException,Is.TypeOf<ArgumentException>());Assert.That(target.All(x=>x==-727),Is.True);
            }
        }
        [Test] public void CopyRejectsDisposedNativeStorage()
        {
            using(var f=new Fixture())
            {
                f.Release();var e=Assert.Throws<TargetInvocationException>(()=>f.CopyTo(new int[8]));Assert.That(e.InnerException,Is.TypeOf<ObjectDisposedException>());
            }
        }
    }
}
