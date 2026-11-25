using System;
using System.Reflection;
using HalfAndHalf.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class Task5NativeTopologyTests
    {
        const BindingFlags Instance=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public;
        Type preparedType,nativeType;
        MethodInfo enable,release,slice;
        void Backend()
        {
            preparedType=typeof(MeshSection).GetNestedType("Prepared",BindingFlags.NonPublic);
            enable=preparedType.GetMethod("EnableNativeTopology",Instance);
            release=preparedType.GetMethod("ReleaseNativeTopology",Instance);
            nativeType=typeof(MeshSection).Assembly.GetType("HalfAndHalf.Presentation.NativeCutTopology");
            Assert.That(enable,Is.Not.Null,"Prepared native backend is missing");
            Assert.That(release,Is.Not.Null,"Prepared native release is missing");
            Assert.That(nativeType,Is.Not.Null,"Native topology runner is missing");
            foreach(string name in new[]{"BurstEnabled","ActiveOwners","TotalRuns"})Assert.That(nativeType.GetProperty(name,Static),Is.Not.Null,name);
            Assert.That(nativeType.GetMethod("Run",Instance),Is.Not.Null);
            Assert.That(nativeType.GetMethod("GetCrossingSlot",Instance),Is.Not.Null);
            Assert.That(nativeType.GetProperty("CrossingSlotCount",Instance),Is.Not.Null);
            slice=typeof(MeshSection).GetMethod("Slice",BindingFlags.Static|BindingFlags.NonPublic);
        }
        void RequireBurst()
        {
            Backend();
            if(!(bool)nativeType.GetProperty("BurstEnabled",Static).GetValue(null))Assert.Ignore("Burst disabled: native execution requires verification in a Burst-enabled environment");
        }
        int Owners=>(int)nativeType.GetProperty("ActiveOwners",Static).GetValue(null);
        long Runs=>(long)nativeType.GetProperty("TotalRuns",Static).GetValue(null);
        object Prepare(Mesh mesh)=>Activator.CreateInstance(preparedType,Instance,null,new object[]{mesh},null);
        Mesh Cut(object source,float x,bool left)=>(Mesh)slice.Invoke(null,new object[]{source,x,left,null,0f});
        int Count(object source,string name)=>(int)preparedType.GetField(name,Instance).GetValue(source);
        object Runner(object source)
        {
            foreach(var field in preparedType.GetFields(Instance))if(field.FieldType==nativeType)return field.GetValue(source);
            Assert.Fail("Prepared has no native runner owner");return null;
        }
        void Release(object source){if(source!=null)release.Invoke(source,null);}
        static void Bits(Vector3[] actual,Vector3[] expected,string channel)
        {
            Assert.That(actual.Length,Is.EqualTo(expected.Length),channel);
            for(int i=0;i<actual.Length;i++)for(int axis=0;axis<3;axis++)Assert.That(BitConverter.SingleToInt32Bits(actual[i][axis]),Is.EqualTo(BitConverter.SingleToInt32Bits(expected[i][axis])),channel+" vertex="+i+" axis="+axis);
        }
        static void EqualMesh(Mesh actual,Mesh expected)
        {
            Bits(actual.vertices,expected.vertices,"position");Bits(actual.normals,expected.normals,"normal");
            Assert.That(actual.subMeshCount,Is.EqualTo(expected.subMeshCount));
            for(int sub=0;sub<expected.subMeshCount;sub++)CollectionAssert.AreEqual(expected.GetTriangles(sub),actual.GetTriangles(sub),"submesh/winding "+sub);
        }
        static Mesh Triangle(int mask)
        {
            var mesh=new Mesh{name="Literal triangle"};
            mesh.vertices=new[]{new Vector3((mask&1)!=0?-1:1,.7f,-.2f),new Vector3((mask&2)!=0?-1:1,-.4f,.8f),new Vector3((mask&4)!=0?-1:1,.1f,-.9f)};
            mesh.normals=new[]{new Vector3(.2f,.3f,.9f).normalized,new Vector3(-.4f,.8f,.1f).normalized,new Vector3(.7f,-.2f,.6f).normalized};
            mesh.triangles=new[]{0,1,2};return mesh;
        }
        // Reversing a crossing edge, fan index, normal operand, or rank test must fail.
        [TestCase(1),TestCase(2),TestCase(3),TestCase(4),TestCase(5),TestCase(6)]
        public void SixCrossingMasksKeepExactManagedGeometryAndSlotOrder(int mask)
        {
            RequireBurst();int baseline=Owners;long runs=Runs;var mesh=Triangle(mask);object source=null;
            try
            {
                source=Prepare(mesh);Assert.That((bool)enable.Invoke(source,null),Is.True);Assert.That(Owners,Is.EqualTo(baseline+1));Assert.That(Runs,Is.EqualTo(runs),"Warm-up is not a CUT run");
                foreach(bool left in new[]{true,false})
                {
                    Mesh actual=null,expected=null;
                    try
                    {
                        actual=Cut(source,0,left);expected=MeshSection.Slice(mesh,0,left);EqualMesh(actual,expected);
                        Assert.That(Count(source,"SkinCount"),Is.EqualTo((left?mask==1||mask==2||mask==4:mask==3||mask==5||mask==6)?3:6));
                        var runner=Runner(source);Assert.That((int)nativeType.GetProperty("CrossingSlotCount",Instance).GetValue(runner),Is.EqualTo(2));
                        int at=0;for(int j=0;j<3;j++)if(((mask>>j)&1)!=((mask>>((j+1)%3))&1))Assert.That((int)nativeType.GetMethod("GetCrossingSlot",Instance).Invoke(runner,new object[]{at++}),Is.EqualTo(j));
                    }
                    finally{UnityEngine.Object.DestroyImmediate(actual);UnityEngine.Object.DestroyImmediate(expected);}
                }
                Assert.That(Runs,Is.EqualTo(runs+2));
            }
            finally{Release(source);Release(source);UnityEngine.Object.DestroyImmediate(mesh);Assert.That(Owners,Is.EqualTo(baseline));}
        }
        // Stale slots, retained full/empty metadata, changed cap sort, and leaked owners must fail.
        [Test]public void RepeatedClosedCutsResetSlotsKeepCapsAndReleaseOwners()
        {
            RequireBurst();int baseline=Owners;var mesh=new Mesh{name="Literal tetrahedron"};object source=null,managed=null;
            try
            {
                mesh.vertices=new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(-.3f,1,-1),new Vector3(.4f,0,1)};
                mesh.normals=new[]{Vector3.left,Vector3.right,Vector3.up,Vector3.forward};mesh.triangles=new[]{0,2,1,0,1,3,1,2,3,2,0,3};
                source=Prepare(mesh);managed=Prepare(mesh);Assert.That(Runner(managed),Is.Null);Assert.That((bool)enable.Invoke(source,null),Is.True);Assert.That((bool)enable.Invoke(source,null),Is.True);Assert.That(Owners,Is.EqualTo(baseline+1));
                foreach(float x in new[]{-.217f,-.217f,0f,.701f,-1f,1f,-2f,2f,.217f})foreach(bool left in new[]{true,false})
                {
                    Mesh actual=null,expected=null,publicMesh=null;
                    try
                    {
                        actual=Cut(source,x,left);expected=Cut(managed,x,left);publicMesh=MeshSection.Slice(mesh,x,left);EqualMesh(actual,expected);EqualMesh(actual,publicMesh);
                        foreach(string name in new[]{"VertexCount","SkinCount","CapCount","PlaneCount"})Assert.That(Count(source,name),Is.EqualTo(Count(managed,name)),name+" x="+x+" left="+left);
                        if(x> -1&&x<1)
                        {
                            int slots=(int)nativeType.GetProperty("CrossingSlotCount",Instance).GetValue(Runner(source));
                            Assert.That(slots,Is.EqualTo(x<-.3f||x>.4f?6:8),"Counts reset rather than accumulate");
                        }
                        Assert.That(Owners,Is.EqualTo(baseline+1),"Public Slice must own no native arrays");Assert.That(Runner(managed),Is.Null);
                    }
                    finally{UnityEngine.Object.DestroyImmediate(actual);UnityEngine.Object.DestroyImmediate(expected);UnityEngine.Object.DestroyImmediate(publicMesh);}
                }
                Release(source);Release(source);Assert.That(Owners,Is.EqualTo(baseline));
                Assert.That((bool)enable.Invoke(source,null),Is.True);Assert.That(Owners,Is.EqualTo(baseline+1));
            }
            finally{Release(source);Release(managed);UnityEngine.Object.DestroyImmediate(mesh);Assert.That(Owners,Is.EqualTo(baseline));}
        }
        // Guard failures must not execute a native run; released native arrays cannot be reused.
        [Test]public void RunnerRejectsInvalidTargetsAndDisposedAccessBeforeExecuting()
        {
            RequireBurst();int baseline=Owners;var mesh=Triangle(1);object source=null;
            try
            {
                source=Prepare(mesh);enable.Invoke(source,null);var runner=Runner(source);var run=nativeType.GetMethod("Run",Instance);long before=Runs;
                var error=Assert.Throws<TargetInvocationException>(()=>run.Invoke(runner,new object[]{0f,true,0,1,1,null}));Assert.That(error.InnerException,Is.TypeOf<ArgumentNullException>());
                error=Assert.Throws<TargetInvocationException>(()=>run.Invoke(runner,new object[]{0f,true,0,1,1,new int[5]}));Assert.That(error.InnerException,Is.TypeOf<ArgumentException>());Assert.That(Runs,Is.EqualTo(before));
                var target=new int[6];
                Assert.That((int)run.Invoke(runner,new object[]{0f,true,0,1,1,target}),Is.EqualTo(3));Assert.That((int)nativeType.GetProperty("CrossingSlotCount",Instance).GetValue(runner),Is.EqualTo(2));
                CollectionAssert.AreEqual(new[]{0,1,2},new[]{target[0],target[1],target[2]},"Single-inside triangle fan retains source winding");
                Assert.That((int)run.Invoke(runner,new object[]{2f,true,0,3,3,target}),Is.EqualTo(3));Assert.That((int)nativeType.GetProperty("CrossingSlotCount",Instance).GetValue(runner),Is.Zero);
                Assert.That((int)run.Invoke(runner,new object[]{-2f,true,0,0,0,target}),Is.Zero);Assert.That((int)nativeType.GetProperty("CrossingSlotCount",Instance).GetValue(runner),Is.Zero);
                error=Assert.Throws<TargetInvocationException>(()=>nativeType.GetMethod("GetCrossingSlot",Instance).Invoke(runner,new object[]{0}));Assert.That(error.InnerException,Is.TypeOf<ArgumentOutOfRangeException>());
                Release(source);error=Assert.Throws<TargetInvocationException>(()=>run.Invoke(runner,new object[]{0f,true,0,1,1,new int[6]}));Assert.That(error.InnerException,Is.TypeOf<ObjectDisposedException>());Assert.That(Runs,Is.EqualTo(before+3));
            }
            finally{Release(source);UnityEngine.Object.DestroyImmediate(mesh);Assert.That(Owners,Is.EqualTo(baseline));}
        }
        [Test]public void EmptyPreparedAndPublicSourcesNeverAcquireNativeOwnership()
        {
            Backend();int baseline=Owners;var mesh=new Mesh{name="Empty source"};object source=null;Mesh actual=null,expected=null;
            try
            {
                source=Prepare(mesh);Assert.That((bool)enable.Invoke(source,null),Is.False);Assert.That(Runner(source),Is.Null);
                actual=Cut(source,0,true);expected=MeshSection.Slice(mesh,0,true);EqualMesh(actual,expected);
                foreach(string name in new[]{"VertexCount","SkinCount","CapCount","PlaneCount"})Assert.That(Count(source,name),Is.Zero,name);
                Assert.That(Owners,Is.EqualTo(baseline));
            }
            finally{Release(source);UnityEngine.Object.DestroyImmediate(actual);UnityEngine.Object.DestroyImmediate(expected);UnityEngine.Object.DestroyImmediate(mesh);Assert.That(Owners,Is.EqualTo(baseline));}
        }
    }
}
