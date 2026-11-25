using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
    internal static class ReviewCutTimings
    {
        internal static double MeshMs,RestMs,ClipMs,UploadMs,SortMs;
        internal static void Reset(){MeshMs=RestMs=ClipMs=UploadMs=SortMs=0;}
    }
#endif
    // Immutable source data and bounded, selection-owned buffers; no vertex work is deferred after CUT.
    public static class MeshSection
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct SectionVertex{public Vector3 Position,Normal;}
        internal struct CutTriangle{internal int A,B,C;internal float Min,Max;}
        static readonly VertexAttributeDescriptor[] layout={new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3)};
        public static float Top(Mesh mesh)
        {
            float top=float.NegativeInfinity;var vertices=mesh.vertices;
            if(mesh.subMeshCount>1)foreach(int i in mesh.GetTriangles(1))top=Mathf.Max(top,vertices[i].y);
            return top;
        }
        public static float RestY(Mesh mesh,Quaternion rotation,float trayY)
        {
            var vertices=mesh.vertices;float bottom=float.PositiveInfinity;
            float yx=(rotation*Vector3.right).y,yy=(rotation*Vector3.up).y,yz=(rotation*Vector3.forward).y;
            for(int i=0;i<vertices.Length;i++){var p=vertices[i];bottom=Mathf.Min(bottom,yx*p.x+yy*p.y+yz*p.z);}
            return float.IsInfinity(bottom)?0:trayY-bottom;
        }
        internal static float RestY(Prepared source,Quaternion rotation,float trayY)
        {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double started=Time.realtimeSinceStartupAsDouble;
#endif
            float bottom=float.PositiveInfinity;
            float yx=(rotation*Vector3.right).y,yy=(rotation*Vector3.up).y,yz=(rotation*Vector3.forward).y;
            ReadOnlySpan<SectionVertex> vertices=source.Vertices;int count=source.VertexCount;
            for(int i=0;i<count;i++){var p=vertices[i].Position;bottom=Mathf.Min(bottom,yx*p.x+yy*p.y+yz*p.z);}
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.RestMs+=(Time.realtimeSinceStartupAsDouble-started)*1000;
#endif
            return float.IsInfinity(bottom)?0:trayY-bottom;
        }
        internal static float Top(Prepared source)
        {
            float top=float.NegativeInfinity;for(int i=0;i<source.CapCount;i++)top=Mathf.Max(top,source.Vertices[source.Cap[i]].Position.y);return top;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float PlaneDistanceSquared(Vector3 a,Vector3 b)
        {
            // Match the float32 rounding of Vector3 subtraction field stores.
            float dx=(float)(a.x-b.x),dy=(float)(a.y-b.y),dz=(float)(a.z-b.z);
            return dx*dx+dy*dy+dz*dz;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static SectionVertex InterpolateSectionVertex(Vector3 a,Vector3 b,Vector3 na,Vector3 nb,float t)
        {
            // Keep the installed Vector3 operations' float order and component divisions.
            float px=a.x+(b.x-a.x)*t,py=a.y+(b.y-a.y)*t,pz=a.z+(b.z-a.z)*t;
            float nx=na.x+(nb.x-na.x)*t,ny=na.y+(nb.y-na.y)*t,nz=na.z+(nb.z-na.z)*t;
            float magnitude=(float)Math.Sqrt(nx*nx+ny*ny+nz*nz);
            return new SectionVertex{Position=new Vector3(px,py,pz),Normal=magnitude>1e-5f?new Vector3(nx/magnitude,ny/magnitude,nz/magnitude):Vector3.zero};
        }
        internal sealed class Prepared
        {
            internal readonly string Name;
            internal readonly Vector3[] Points,Normals,Plane;
            internal readonly float[] TriangleMin,TriangleMax;
            readonly int[] edgeIds,planeEdgeIds,edgePlane,edgeReady;
            readonly SectionVertex[] edgeVertices;
            int edgeStamp;float edgeX;bool edgeHasX;
            readonly HashSet<float> vertexX=new HashSet<float>();
            internal bool CapReady,CapShareAllowed;internal int CachedPlaneCount;internal Vector3 CachedCenter;
            internal readonly int[] Indices,Remap,Skin,Cap,Polygon=new int[4];
            internal readonly SectionVertex[] Vertices,OriginalPacked;
            internal readonly int[] OriginalOrder;
            internal readonly CutTriangle[] PackedTriangles;
            internal readonly bool PackOriginals;
            internal NativeCutTopology NativeTopology;
            internal bool EnableNativeTopology()
            {
                if(NativeTopology!=null)return true;
                if(!PackOriginals||PackedTriangles.Length==0||!NativeCutTopology.BurstEnabled)return false;
                NativeTopology=new NativeCutTopology(PackedTriangles);return true;
            }
            internal void ReleaseNativeTopology(){NativeTopology?.Dispose();NativeTopology=null;}
            internal readonly float Min,Max;
            internal int VertexCount,SkinCount,CapCount,PlaneCount;
            readonly PlaneBins bins;
            readonly int[] next;
            readonly struct PlaneEdgeKey:IEquatable<PlaneEdgeKey>
            {
                readonly Vector3 a,b;
                internal PlaneEdgeKey(Vector3 a,Vector3 b){this.a=a;this.b=b;}
                public bool Equals(PlaneEdgeKey other)=>(a.Equals(other.a)&&b.Equals(other.b))||(a.Equals(other.b)&&b.Equals(other.a));
                public override bool Equals(object value)=>value is PlaneEdgeKey other&&Equals(other);
                public override int GetHashCode()=>a.GetHashCode()^b.GetHashCode();
            }
            internal readonly PlaneOrder Order=new PlaneOrder();
            internal readonly AnglePoint[] OrderedPlane;
            internal struct AnglePoint { internal Vector3 Point; internal float Angle; }
            internal sealed class PlaneOrder:IComparer<AnglePoint>
            {
                public int Compare(AnglePoint a,AnglePoint b)=>a.Angle.CompareTo(b.Angle);
            }
            internal void Clear(){VertexCount=SkinCount=CapCount=PlaneCount=0;bins.Clear();Array.Clear(Remap,0,Remap.Length);Array.Clear(edgePlane,0,edgePlane.Length);}
            // A vertex hit can use different endpoint interpolation operands on opposite sides.
            internal void BeginCut(float x)
            {
                if(edgeHasX&&BitConverter.SingleToInt32Bits(x)==BitConverter.SingleToInt32Bits(edgeX))return;
                edgeX=x;edgeHasX=true;CapReady=false;CapShareAllowed=!vertexX.Contains(x);if(edgeStamp==int.MaxValue){Array.Clear(edgeReady,0,edgeReady.Length);edgeStamp=1;}else edgeStamp++;
            }
            // One mutable context per crossing pass. Never copy it after writes: PlaneCount
            // belongs to this local until publication before cached-cap restoration.
            internal ref struct CrossingContext
            {
                ReadOnlySpan<int> edgeIds,planeEdgeIds,Indices;
                ReadOnlySpan<Vector3> Points,Normals;
                Span<int> edgePlane,edgeReady,next;
                Span<SectionVertex> edgeVertices;
                Span<Vector3> Plane;
                readonly PlaneBins bins;
                readonly int edgeStamp;
                internal int PlaneCount;
                internal CrossingContext(Prepared source)
                {
                    edgeIds=source.edgeIds;planeEdgeIds=source.planeEdgeIds;Indices=source.Indices;
                    Points=source.Points;Normals=source.Normals;edgePlane=source.edgePlane;
                    edgeReady=source.edgeReady;next=source.next;edgeVertices=source.edgeVertices;
                    Plane=source.Plane;bins=source.bins;edgeStamp=source.edgeStamp;PlaneCount=source.PlaneCount;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                internal SectionVertex Intersection(int slot,float x)
                {
                    int edge=edgeIds[slot];if(edgeReady[edge]==edgeStamp)return edgeVertices[edge];
                    int ai=Indices[slot],bi=Indices[slot-slot%3+(slot+1)%3];var a=Points[ai];var b=Points[bi];float t=(x-a.x)/(b.x-a.x);
                    var value=InterpolateSectionVertex(a,b,Normals[ai],Normals[bi],t);
                    edgeVertices[edge]=value;edgeReady[edge]=edgeStamp;return value;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                static long Bin(int y,int z){unchecked{return ((long)y<<32)|(uint)(z*397);}}
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                int FindPlanePoint(long key,Vector3 p)
                {
                    var plane=Plane;var links=next;
                    for(int i=bins.Get(key);i>=0;i=links[i])if(PlaneDistanceSquared(plane[i],p)<1e-10f)return i;
                    return -1;
                }
                internal void IncludePlanePoint(Vector3 p,int slot)
                {
                    int edge=planeEdgeIds[slot],stored=edgePlane[edge];
                    if(stored>0&&PlaneDistanceSquared(Plane[stored-1],p)<1e-10f)return;
                    if(PlaneCount>0&&PlaneDistanceSquared(Plane[PlaneCount-1],p)<1e-10f){edgePlane[edge]=PlaneCount;return;}
                    double scaledY=(double)p.y/.00002,scaledZ=(double)p.z/.00002;
                    int y=(int)Math.Floor(scaledY),z=(int)Math.Floor(scaledZ);var key=Bin(y,z);
                    int found=FindPlanePoint(key,p);if(found>=0){edgePlane[edge]=found+1;return;}
                    PlaneNeighbourRange(p.y,scaledY,y,out int firstY,out int lastY);PlaneNeighbourRange(p.z,scaledZ,z,out int firstZ,out int lastZ);
                    for(int b=firstY;b<=lastY;b++)for(int c=firstZ;c<=lastZ;c++)if(b!=0||c!=0){found=FindPlanePoint(Bin(y+b,z+c),p);if(found>=0){edgePlane[edge]=found+1;return;}}
                    int prior=bins.Replace(key,PlaneCount);next[PlaneCount]=prior;Plane[PlaneCount++]=p;edgePlane[edge]=PlaneCount;
                }
            }
            internal CrossingContext CreateCrossingContext()=>new CrossingContext(this);
            internal SectionVertex Intersection(int slot,float x)
            {var context=CreateCrossingContext();return context.Intersection(slot,x);}
            internal void IncludePlanePoint(Vector3 p,int slot)
            {
                var context=CreateCrossingContext();
                try{context.IncludePlanePoint(p,slot);}finally{PlaneCount=context.PlaneCount;}
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int Append(Vector3 p,Vector3 n){int index=VertexCount++;Vertices[index]=new SectionVertex{Position=p,Normal=n};return index;}
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int IncludeOriginal(int original)
            {
                int stored=Remap[original];if(stored!=0)return stored-1;
                int index=Append(Points[original],Normals[original]);Remap[original]=index+1;return index;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Triangle(int a,int b,int c){Skin[SkinCount++]=a;Skin[SkinCount++]=b;Skin[SkinCount++]=c;}
            // Preserve Unity 6000.3.25f1 base ArraySortHelper's exact traversal and tie order.
            internal void SortPlane()
            {
                int count=PlaneCount;
                if(count<0)throw new ArgumentOutOfRangeException(nameof(count));
                if(count>OrderedPlane.Length)throw new ArgumentException("Plane count exceeds bounded sort storage",nameof(count));
                if(count<2)return;int log=0;
                for(int n=count;n>=1;n/=2)log++;
                PlaneIntroSort(OrderedPlane,0,count-1,2*log);
            }
            static void PlaneSwap(AnglePoint[] keys,int a,int b)
            {if(a==b)return;var t=keys[a];keys[a]=keys[b];keys[b]=t;}
            static void PlaneSwapIfGreater(AnglePoint[] keys,int a,int b)
            {if(a!=b&&keys[a].Angle.CompareTo(keys[b].Angle)>0)PlaneSwap(keys,a,b);}
            static void PlaneIntroSort(AnglePoint[] keys,int lo,int hi,int depth)
            {
                while(hi>lo)
                {
                    int count=hi-lo+1;
                    if(count<=16)
                    {
                        if(count==2){PlaneSwapIfGreater(keys,lo,hi);return;}
                        if(count==3){PlaneSwapIfGreater(keys,lo,hi-1);PlaneSwapIfGreater(keys,lo,hi);PlaneSwapIfGreater(keys,hi-1,hi);return;}
                        for(int i=lo;i<hi;i++){int j=i;var t=keys[i+1];while(j>=lo&&t.Angle.CompareTo(keys[j].Angle)<0){keys[j+1]=keys[j];j--;}keys[j+1]=t;}
                        return;
                    }
                    if(depth==0){PlaneHeapSort(keys,lo,hi);return;}
                    depth--;int p=PlanePartition(keys,lo,hi);
                    PlaneIntroSort(keys,p+1,hi,depth);hi=p-1;
                }
            }
            static int PlanePartition(AnglePoint[] keys,int lo,int hi)
            {
                int middle=lo+(hi-lo)/2;PlaneSwapIfGreater(keys,lo,middle);PlaneSwapIfGreater(keys,lo,hi);PlaneSwapIfGreater(keys,middle,hi);
                var pivot=keys[middle];PlaneSwap(keys,middle,hi-1);int left=lo,right=hi-1;
                while(left<right){while(keys[++left].Angle.CompareTo(pivot.Angle)<0){}while(pivot.Angle.CompareTo(keys[--right].Angle)<0){}if(left>=right)break;PlaneSwap(keys,left,right);}
                PlaneSwap(keys,left,hi-1);return left;
            }
            static void PlaneHeapSort(AnglePoint[] keys,int lo,int hi)
            {
                int count=hi-lo+1;for(int i=count/2;i>=1;i--)PlaneDownHeap(keys,i,count,lo);
                for(int i=count;i>1;i--){PlaneSwap(keys,lo,lo+i-1);PlaneDownHeap(keys,1,i-1,lo);}
            }
            static void PlaneDownHeap(AnglePoint[] keys,int i,int count,int lo)
            {
                var d=keys[lo+i-1];
                while(i<=count/2){int child=2*i;if(child<count&&keys[lo+child-1].Angle.CompareTo(keys[lo+child].Angle)<0)child++;if(d.Angle.CompareTo(keys[lo+child-1].Angle)>=0)break;keys[lo+i-1]=keys[lo+child-1];i=child;}
                keys[lo+i-1]=d;
            }
            internal Prepared(Mesh source)
            {
                Name=source.name;Points=source.vertices;Normals=source.normals;Indices=source.triangles;Remap=new int[Points.Length];
                // Directed source edges preserve each interpolation's operand order, including split normals.
                edgeIds=new int[Indices.Length];planeEdgeIds=new int[Indices.Length];var edges=CreateSourceEdges(Indices.Length);var planeEdges=new Dictionary<PlaneEdgeKey,int>();
                for(int slot=0;slot<Indices.Length;slot++){int a=Indices[slot],b=Indices[slot-slot%3+(slot+1)%3];long key=((long)a<<32)|(uint)b;if(!edges.TryGetValue(key,out int id)){id=edges.Count;edges.Add(key,id);}edgeIds[slot]=id;var planeKey=new PlaneEdgeKey(Points[a],Points[b]);if(!planeEdges.TryGetValue(planeKey,out int planeId)){planeId=planeEdges.Count;planeEdges.Add(planeKey,planeId);}planeEdgeIds[slot]=planeId;}
                edgePlane=new int[planeEdges.Count];edgeReady=new int[edges.Count];edgeVertices=new SectionVertex[edges.Count];
                Min=float.PositiveInfinity;Max=float.NegativeInfinity;bool finite=true;
                foreach(var p in Points){vertexX.Add(p.x);Min=Mathf.Min(Min,p.x);Max=Mathf.Max(Max,p.x);if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||float.IsInfinity(p.z)||Mathf.Abs(p.x)>16||Mathf.Abs(p.y)>16||Mathf.Abs(p.z)>16)finite=false;}
                PackOriginals=finite&&Normals.Length==Points.Length;
                if(PackOriginals)
                {
                    var order=new List<int>(Math.Min(Points.Length,Indices.Length));foreach(int original in Indices)if(Remap[original]==0){Remap[original]=1;order.Add(original);}OriginalOrder=order.ToArray();
                    Array.Sort(OriginalOrder,(a,b)=>{int c=Points[a].x.CompareTo(Points[b].x);return c!=0?c:a.CompareTo(b);});
                    OriginalPacked=new SectionVertex[OriginalOrder.Length];
                    for(int i=0;i<OriginalOrder.Length;i++){int original=OriginalOrder[i];OriginalPacked[i]=new SectionVertex{Position=Points[original],Normal=Normals[original]};Remap[original]=i+1;}
                }
                else{OriginalPacked=Array.Empty<SectionVertex>();OriginalOrder=Array.Empty<int>();}
                TriangleMin=new float[Indices.Length/3];TriangleMax=new float[Indices.Length/3];
                PackedTriangles=PackOriginals?new CutTriangle[Indices.Length/3]:Array.Empty<CutTriangle>();
                for(int i=0;i<Indices.Length;i+=3){float a=Points[Indices[i]].x,b=Points[Indices[i+1]].x,c=Points[Indices[i+2]].x;float min=Mathf.Min(a,Mathf.Min(b,c)),max=Mathf.Max(a,Mathf.Max(b,c));TriangleMin[i/3]=min;TriangleMax[i/3]=max;if(PackOriginals)PackedTriangles[i/3]=new CutTriangle{A=Remap[Indices[i]]-1,B=Remap[Indices[i+1]]-1,C=Remap[Indices[i+2]]-1,Min=min,Max=max};}
                Array.Clear(Remap,0,Remap.Length);
                // Each triangle creates at most two intersections and two clipped triangles.
                int intersections=Indices.Length/3*2;
                Vertices=new SectionVertex[Points.Length+intersections*2+1];Skin=new int[Indices.Length*2];Cap=new int[intersections*3];
                Plane=new Vector3[intersections];OrderedPlane=new AnglePoint[intersections];next=new int[intersections];bins=new PlaneBins(intersections);
            }
        }
        // The strict squared-distance test has radius 1e-5 inside 2e-5 bins.
        // A 1e-8 margin covers float subtraction/square rounding and double bin arithmetic.
        // Keep the original 3x3 order and fallback outside the bounded source domain.
        internal static void PlaneNeighbours(float value,int cell,out int first,out int last)
            =>PlaneNeighbourRange(value,(double)value/.00002,cell,out first,out last);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void PlaneNeighbourRange(float value,double scaled,int cell,out int first,out int last)
        {
            if(!(value>=-16f&&value<=16f)){first=-1;last=1;return;}
            double fraction=scaled-cell;
            first=fraction<=.5005?-1:0;last=fraction>=.4995?1:0;
        }
        // Selection-owned exact key -> head index. No resizing or allocation occurs during CUT.
        internal sealed class PlaneBins
        {
            readonly long[] keys;
            readonly int[] heads;
            readonly int capacity;
            int count;
            internal PlaneBins(int capacity)
            {
                if(capacity<0)throw new ArgumentOutOfRangeException(nameof(capacity));
                int size=1;while(size<capacity)size=checked(size*2);
                if(capacity>0)size=checked(size*2);
                this.capacity=capacity;keys=new long[size];heads=new int[size];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            int Bucket(long key)
            {
                unchecked {uint hash=(uint)key^(uint)(key>>32);hash^=hash>>16;hash*=0x7feb352dU;hash^=hash>>15;return (int)(hash&(uint)(heads.Length-1));}
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int Get(long key)
            {
                var values=heads;var stored=keys;int mask=values.Length-1;
                for(int i=Bucket(key);values[i]!=0;i=(i+1)&mask)if(stored[i]==key)return values[i]-1;
                return -1;
            }
            internal void Set(long key,int head)=>Replace(key,head);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal int Replace(long key,int head)
            {
                var values=heads;var stored=keys;int mask=values.Length-1,bucket=Bucket(key);
                while(values[bucket]!=0){if(stored[bucket]==key){int prior=values[bucket]-1;values[bucket]=head+1;return prior;}bucket=(bucket+1)&mask;}
                if(count==capacity)throw new InvalidOperationException("Plane bin capacity exceeded");
                count++;stored[bucket]=key;values[bucket]=head+1;return -1;
            }
            internal void Clear(){count=0;Array.Clear(heads,0,heads.Length);}
        }
        sealed class DirectedSourceEdgeComparer : IEqualityComparer<long>
        {
            public bool Equals(long a,long b) => a==b;
            public int GetHashCode(long value)
            {
                // The default long hash XORs the two vertex indices: adjacent edges form huge chains.
                unchecked
                {
                    ulong mixed=(ulong)value;
                    mixed=(mixed^(mixed>>33))*0xff51afd7ed558ccdUL;
                    mixed=(mixed^(mixed>>33))*0xc4ceb9fe1a85ec53UL;
                    mixed^=mixed>>33;
                    return (int)(mixed^(mixed>>32));
                }
            }
        }
        static readonly IEqualityComparer<long> sourceEdgeComparer=new DirectedSourceEdgeComparer();
        internal static Dictionary<long,int> CreateSourceEdges(int capacity) => new Dictionary<long,int>(capacity,sourceEdgeComparer);
        static Mesh Output(Prepared source,bool left,Mesh target)
        {
            if(target==null)target=new Mesh{indexFormat=IndexFormat.UInt32};else target.Clear();
            target.name=source.Name+(left?" left":" right");target.subMeshCount=2;return target;
        }
        public static Mesh Slice(Mesh source,float x,bool left)=>Slice(new Prepared(source),x,left);
        internal static Mesh Slice(Prepared source,float x,bool left,Mesh target=null,float bias=0)
        {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double started=Time.realtimeSinceStartupAsDouble;
#endif
            var result=SlicePrepared(source,x,left,target,bias);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.MeshMs+=(Time.realtimeSinceStartupAsDouble-started)*1000;
#endif
            return result;
        }
        static Mesh SlicePrepared(Prepared source,float x,bool left,Mesh target,float bias)
        {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double clipStarted=Time.realtimeSinceStartupAsDouble;
#endif
            var mesh=Output(source,left,target);source.BeginCut(x);source.Clear();bool reuseCap=source.CapReady,cacheCap=source.CapShareAllowed,packed=source.PackOriginals;
            ReadOnlySpan<Vector3> points=source.Points;ReadOnlySpan<int> indices=source.Indices;
            ReadOnlySpan<float> triangleMin=source.TriangleMin,triangleMax=source.TriangleMax;
            Span<int> skin=source.Skin,cap=source.Cap,polygon=source.Polygon;int skinCount=0,capCount=0;
            if(points.Length==0||(left?x<source.Min:x>source.Max))return mesh;
            bool full=left?x>=source.Max:x<=source.Min;
            if(full)
            {
                // Preserve the public whole-mesh first-reference vertex order.
                foreach(int original in indices)skin[skinCount++]=source.IncludeOriginal(original);
            }
            else
            {
                var crossing=source.CreateCrossingContext();
                try
                {
                if(packed)
                {
                    if(float.IsNaN(x))return mesh;
                    ReadOnlySpan<SectionVertex> originals=source.OriginalPacked;int lo=0,hi=originals.Length;
                    // Inclusive halves use upper/lower bounds respectively, including all equal-X vertices.
                    while(lo<hi){int mid=lo+(hi-lo)/2;float px=originals[mid].Position.x;if(left?px<=x:px<x)lo=mid+1;else hi=mid;}
                    int begin=left?0:lo,end=left?lo:originals.Length,count=end-begin;
                    originals.Slice(begin,count).CopyTo(source.Vertices.AsSpan());source.VertexCount=count;
                    if(source.NativeTopology!=null)
                    {
                        skinCount=source.NativeTopology.Run(x,left,begin,end,count,source.Skin);
                        // Cap has 6*T entries; the completed slot prefix has at most 2*T.
                        // All scratch reads finish before cap indices overwrite this buffer.
                        int crossings=source.NativeTopology.CopyCrossingSlots(source.Cap);
                        Span<SectionVertex> outputVertices=source.Vertices;int vertexCount=source.VertexCount;
                        for(int i=0;i<crossings;i++)
                        {
                            int slot=cap[i];var value=crossing.Intersection(slot,x);
                            outputVertices[vertexCount++]=value;if(!reuseCap)crossing.IncludePlanePoint(value.Position,slot);
                        }
                        source.VertexCount=vertexCount;
                    }
                    else
                    {
                    ReadOnlySpan<CutTriangle> triangles=source.PackedTriangles;
                    for(int t=0;t<triangles.Length;t++)
                    {
                        var triangle=triangles[t];
                        if(left?x<triangle.Min:x>triangle.Max)continue;
                        if(left?x>=triangle.Max:x<=triangle.Min){skin[skinCount++]=triangle.A-begin;skin[skinCount++]=triangle.B-begin;skin[skinCount++]=triangle.C-begin;continue;}
                        int polygonCount=0;
                        for(int j=0;j<3;j++)
                        {
                            int a=j==0?triangle.A:j==1?triangle.B:triangle.C,b=j==0?triangle.B:j==1?triangle.C:triangle.A;
                            bool inA=left?a<end:a>=begin,inB=left?b<end:b>=begin;
                            if(inA)polygon[polygonCount++]=a-begin;
                            if(inA!=inB){int slot=t*3+j;var value=crossing.Intersection(slot,x);polygon[polygonCount++]=source.Append(value.Position,value.Normal);if(!reuseCap)crossing.IncludePlanePoint(value.Position,slot);}
                        }
                        for(int j=1;j<polygonCount-1;j++){skin[skinCount++]=polygon[0];skin[skinCount++]=polygon[j];skin[skinCount++]=polygon[j+1];}
                    }
                    }
                }
                else
                {
                for(int i=0,triangle=0;i<indices.Length;i+=3,triangle++)
                {
                    float min=triangleMin[triangle],max=triangleMax[triangle];
                    if(left?x<min:x>max)continue;
                    if(left?x>=max:x<=min)
                    {
                        skin[skinCount++]=source.IncludeOriginal(indices[i]);skin[skinCount++]=source.IncludeOriginal(indices[i+1]);skin[skinCount++]=source.IncludeOriginal(indices[i+2]);
                        continue;
                    }
                    int polygonCount=0;
                    for(int j=0;j<3;j++)
                    {
                        int ai=indices[i+j],bi=indices[i+(j+1)%3];float ax=points[ai].x,bx=points[bi].x;
                        bool inA=left?ax<=x:ax>=x,inB=left?bx<=x:bx>=x;
                        if(inA)polygon[polygonCount++]=source.IncludeOriginal(ai);
                        if(inA!=inB)
                        {
                            var value=crossing.Intersection(i+j,x);
                            polygon[polygonCount++]=source.Append(value.Position,value.Normal);if(!reuseCap)crossing.IncludePlanePoint(value.Position,i+j);
                        }
                    }
                    for(int j=1;j<polygonCount-1;j++){skin[skinCount++]=polygon[0];skin[skinCount++]=polygon[j];skin[skinCount++]=polygon[j+1];}
                }
                }
                }
                finally{source.PlaneCount=crossing.PlaneCount;}
                if(reuseCap)source.PlaneCount=source.CachedPlaneCount;
                if(source.PlaneCount>=3)
                {
                    Vector3 center;
                    if(reuseCap)center=source.CachedCenter;
                    else
                    {
                        center=Vector3.zero;for(int i=0;i<source.PlaneCount;i++)center+=source.Plane[i];center/=source.PlaneCount;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
                        double sortStarted=Time.realtimeSinceStartupAsDouble;
#endif
                        // The float comparison and Array.Sort tie order match the original traversal.
                        for(int i=0;i<source.PlaneCount;i++){var p=source.Plane[i];source.OrderedPlane[i]=new Prepared.AnglePoint{Point=p,Angle=Mathf.Atan2(p.z-center.z,p.y-center.y)};}
                        source.SortPlane();
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
                        ReviewCutTimings.SortMs+=(Time.realtimeSinceStartupAsDouble-sortStarted)*1000;
#endif
                        source.CachedCenter=center;source.CachedPlaneCount=source.PlaneCount;source.CapReady=cacheCap;
                    }
                    var normal=left?Vector3.right:Vector3.left;int c=source.Append(center,normal);
                    for(int i=0;i<source.PlaneCount;i++)source.Append(source.OrderedPlane[i].Point,normal);
                    for(int j=0;j<source.PlaneCount;j++){int a=c+1+j,b=c+1+(j+1)%source.PlaneCount;cap[capCount++]=c;cap[capCount++]=left?a:b;cap[capCount++]=left?b:a;}
                }
            }
            source.SkinCount=skinCount;source.CapCount=capCount;
            if(bias!=0)for(int i=0;i<source.VertexCount;i++)source.Vertices[i].Position.x+=bias;
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.ClipMs+=(Time.realtimeSinceStartupAsDouble-clipStarted)*1000;double uploadStarted=Time.realtimeSinceStartupAsDouble;
#endif
            if(source.VertexCount>0){mesh.SetVertexBufferParams(source.VertexCount,layout);mesh.SetVertexBufferData(source.Vertices,0,0,source.VertexCount,0,MeshUpdateFlags.DontRecalculateBounds);}
            mesh.subMeshCount=2;mesh.SetIndices(source.Skin,0,source.SkinCount,MeshTopology.Triangles,0,false);mesh.SetIndices(source.Cap,0,source.CapCount,MeshTopology.Triangles,1,false);mesh.RecalculateBounds();
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.UploadMs+=(Time.realtimeSinceStartupAsDouble-uploadStarted)*1000;
#endif
            return mesh;
        }
    }
}
