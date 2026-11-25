using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HalfAndHalf.Presentation
{
    // Selection-owned integer topology; interpolation and cap construction remain managed.
    internal sealed class NativeCutTopology : IDisposable
    {
        NativeArray<MeshSection.CutTriangle> triangles;
        NativeArray<int> skin,crossingSlots,counts;
        bool disposed,registered;
        static int activeOwners;
        static long totalRuns;
        internal static bool BurstEnabled => BurstCompiler.IsEnabled;
        internal static int ActiveOwners => Volatile.Read(ref activeOwners);
        internal static long TotalRuns => Interlocked.Read(ref totalRuns);

        internal NativeCutTopology(MeshSection.CutTriangle[] source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            try
            {
                triangles=new NativeArray<MeshSection.CutTriangle>(source,Allocator.Persistent);
                skin=new NativeArray<int>(checked(source.Length*6),Allocator.Persistent);
                crossingSlots=new NativeArray<int>(checked(source.Length*2),Allocator.Persistent);
                counts=new NativeArray<int>(2,Allocator.Persistent);
                var warm=Job(float.NegativeInfinity,true,0,0,0);
                warm.Run();
                Interlocked.Increment(ref activeOwners);registered=true;
            }
            catch{Dispose();throw;}
        }
        TopologyJob Job(float x,bool left,int begin,int end,int originalCount)=>new TopologyJob
        {
            Triangles=triangles,X=x,Left=left,Begin=begin,End=end,OriginalCount=originalCount,
            Skin=skin,CrossingSlots=crossingSlots,Counts=counts
        };
        internal int Run(float x,bool left,int begin,int end,int originalCount,int[] targetSkin)
        {
            ThrowIfDisposed();
            if(targetSkin==null)throw new ArgumentNullException(nameof(targetSkin));
            if(targetSkin.Length<skin.Length)throw new ArgumentException("Target must hold the bounded skin output",nameof(targetSkin));
            var job=Job(x,left,begin,end,originalCount);job.Run();
            Interlocked.Increment(ref totalRuns);
            int count=counts[0];NativeArray<int>.Copy(skin,0,targetSkin,0,count);return count;
        }
        internal int CrossingSlotCount {get{ThrowIfDisposed();return counts[1];}}
        internal int GetCrossingSlot(int index)
        {
            ThrowIfDisposed();
            if(index<0||index>=counts[1])throw new ArgumentOutOfRangeException(nameof(index));
            return crossingSlots[index];
        }
        internal int CopyCrossingSlots(int[] target)
        {
            ThrowIfDisposed();
            if(target==null)throw new ArgumentNullException(nameof(target));
            int count=counts[1];
            if(count<0||count>crossingSlots.Length)throw new InvalidOperationException("Invalid completed crossing prefix");
            if(target.Length<count)throw new ArgumentException("Target must hold the completed crossing prefix",nameof(target));
            if(count>0)NativeArray<int>.Copy(crossingSlots,0,target,0,count);
            return count;
        }
        void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(NativeCutTopology));}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(triangles.IsCreated)triangles.Dispose();
            if(skin.IsCreated)skin.Dispose();
            if(crossingSlots.IsCreated)crossingSlots.Dispose();
            if(counts.IsCreated)counts.Dispose();
            if(registered){Interlocked.Decrement(ref activeOwners);registered=false;}
        }

        [BurstCompile(FloatMode=FloatMode.Strict,FloatPrecision=FloatPrecision.Standard,CompileSynchronously=true)]
        struct TopologyJob : IJob
        {
            [ReadOnly] public NativeArray<MeshSection.CutTriangle> Triangles;
            public float X;
            public bool Left;
            public int Begin,End,OriginalCount;
            public NativeArray<int> Skin,CrossingSlots,Counts;
            static void Append(ref int p0,ref int p1,ref int p2,ref int p3,int index,int value)
            {
                if(index==0)p0=value;else if(index==1)p1=value;else if(index==2)p2=value;else p3=value;
            }
            public void Execute()
            {
                Counts[0]=Counts[1]=0;int skinCount=0,crossingCount=0;
                for(int t=0;t<Triangles.Length;t++)
                {
                    var triangle=Triangles[t];
                    if(Left?X<triangle.Min:X>triangle.Max)continue;
                    if(Left?X>=triangle.Max:X<=triangle.Min)
                    {
                        Skin[skinCount++]=triangle.A-Begin;Skin[skinCount++]=triangle.B-Begin;Skin[skinCount++]=triangle.C-Begin;continue;
                    }
                    int polygonCount=0,p0=0,p1=0,p2=0,p3=0;
                    for(int j=0;j<3;j++)
                    {
                        int a=j==0?triangle.A:j==1?triangle.B:triangle.C,b=j==0?triangle.B:j==1?triangle.C:triangle.A;
                        bool inA=Left?a<End:a>=Begin,inB=Left?b<End:b>=Begin;
                        if(inA)Append(ref p0,ref p1,ref p2,ref p3,polygonCount++,a-Begin);
                        if(inA!=inB)
                        {
                            CrossingSlots[crossingCount]=t*3+j;
                            Append(ref p0,ref p1,ref p2,ref p3,polygonCount++,OriginalCount+crossingCount++);
                        }
                    }
                    if(polygonCount>=3){Skin[skinCount++]=p0;Skin[skinCount++]=p1;Skin[skinCount++]=p2;}
                    if(polygonCount==4){Skin[skinCount++]=p0;Skin[skinCount++]=p2;Skin[skinCount++]=p3;}
                }
                Counts[0]=skinCount;Counts[1]=crossingCount;
            }
        }
    }
}
