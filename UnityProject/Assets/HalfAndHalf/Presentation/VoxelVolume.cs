using System;
using System.Collections.Generic;
using System.Linq;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using HalfAndHalf.Verification;
#endif

namespace HalfAndHalf.Presentation
{
    // A static authoring grid; cut locations and official scoring remain continuous.
    public sealed class VoxelVolume
    {
        public const float Step=.03125f;
        readonly int NX,NY,NZ;
        readonly float GridStep;
        public int Resolution {get;}
        readonly byte[,,] materials;
        readonly List<Vector3Int> physical=new List<Vector3Int>(),decoration=new List<Vector3Int>();
        readonly Func<string,byte,Vector3,int,int,bool,Color32> color;
        readonly Func<Color32,bool> matchEdges;
        readonly List<Vector3Int>[] columns;
        List<Quad> exterior;
        CachedQuad[] exteriorTemplates;
        readonly Surface cutSurface=new Surface();
        readonly List<Quad> clippedBuffer=new List<Quad>(),capBuffer=new List<Quad>();
        internal float CutTop=>cutSurface.Top;
        internal float CutRestY(Quaternion rotation,float trayY)
        {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double started=Time.realtimeSinceStartupAsDouble;
#endif
            float result=cutSurface.RestY(rotation,trayY);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.RestMs+=(Time.realtimeSinceStartupAsDouble-started)*1000;
#endif
            return result;
        }
        readonly CachedQuad[,,][] capTemplates;
        const float Epsilon=.0000001f;
        readonly struct Quad
        {
            public readonly Vector3 lo,hi;public readonly int axis,sign;public readonly Color32 color;
            public Quad(Vector3 a,Vector3 b,int axis,int sign,Color32 color){lo=a;hi=b;this.axis=axis;this.sign=sign;this.color=color;}
        }
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct PackedVertex{public Vector3 Position,Normal;public Color32 Color;}
        readonly struct CachedQuad
        {
            public readonly Quad Face;
            public readonly PackedVertex A,B,C,D;
            public CachedQuad(Quad q)
            {
                Face=q;var a=q.lo;Vector3 b,c,d,normal;
                if(q.axis==0){b=new Vector3(a.x,q.hi.y,a.z);c=new Vector3(a.x,q.hi.y,q.hi.z);d=new Vector3(a.x,a.y,q.hi.z);normal=new Vector3(q.sign,0,0);}
                else if(q.axis==1){b=new Vector3(a.x,a.y,q.hi.z);c=new Vector3(q.hi.x,a.y,q.hi.z);d=new Vector3(q.hi.x,a.y,a.z);normal=new Vector3(0,q.sign,0);}
                else{b=new Vector3(q.hi.x,a.y,a.z);c=new Vector3(q.hi.x,q.hi.y,a.z);d=new Vector3(a.x,q.hi.y,a.z);normal=new Vector3(0,0,q.sign);}
                A=new PackedVertex{Position=a,Normal=normal,Color=q.color};B=new PackedVertex{Position=b,Normal=normal,Color=q.color};
                C=new PackedVertex{Position=c,Normal=normal,Color=q.color};D=new PackedVertex{Position=d,Normal=normal,Color=q.color};
            }
        }
        static CachedQuad[] CacheQuads(List<Quad> faces)
        {
            var result=new CachedQuad[faces.Count];for(int i=0;i<result.Length;i++)result[i]=new CachedQuad(faces[i]);return result;
        }
        public string Id {get;}
        public int CellCount=>physical.Count;
        public int DecorationCells=>decoration.Count;
        public VoxelVolume(string id,Func<Vector3,byte> author,Func<string,byte,Vector3,int,int,bool,Color32> palette,Func<Color32,bool> matchEdges=null,int resolution=1) : this(id,palette,matchEdges,resolution)
        {
            for(int i=0;i<NX;i++)for(int j=0;j<NY;j++)for(int k=0;k<NZ;k++)
            {
                byte m=author(Center(i,j,k));if(m==0)continue;materials[i,j,k]=m;
                var cell=new Vector3Int(i,j,k);
                if(VoxelPrototypeGeometry.Densities[m]>0){physical.Add(cell);if(columns[i]==null)columns[i]=new List<Vector3Int>();columns[i].Add(cell);}else decoration.Add(cell);
            }
        }
        VoxelVolume(string id,Func<string,byte,Vector3,int,int,bool,Color32> palette,Func<Color32,bool> matchEdges,int resolution)
        {
            if(resolution!=1&&resolution!=2)throw new ArgumentOutOfRangeException(nameof(resolution));
            Resolution=resolution;GridStep=Step/resolution;NX=80*resolution;NY=80*resolution;NZ=40*resolution;
            materials=new byte[NX,NY,NZ];columns=new List<Vector3Int>[NX];capTemplates=new CachedQuad[NX,2,2][];
            Id=id;color=palette;this.matchEdges=matchEdges;
        }
        internal byte PreparedMaterial(int x,int y,int z)=>materials[x,y,z];
        internal static VoxelVolume ReadPrepared(string id,Func<string,byte,Vector3,int,int,bool,Color32> palette,Func<Color32,bool> matchEdges,System.IO.BinaryReader reader,int resolution=1)
        {
            var volume=new VoxelVolume(id,palette,matchEdges,resolution);int NX=volume.NX,NY=volume.NY,NZ=volume.NZ,size=NX*NY*NZ;
            var grid=reader.ReadBytes(size);if(grid.Length!=size)throw new System.IO.InvalidDataException("Truncated prepared grid");
            Buffer.BlockCopy(grid,0,volume.materials,0,size);
            var seen=new bool[size];var positive=Array.ConvertAll(VoxelPrototypeGeometry.Densities,m=>m>0);
            ReadCells(true);ReadCells(false);
            for(int i=0;i<size;i++)if(grid[i]>=positive.Length||(grid[i]!=0)!=seen[i])throw new System.IO.InvalidDataException("Prepared material/cell coverage mismatch");
            volume.exterior=ReadQuads(false);volume.exteriorTemplates=CacheQuads(volume.exterior);
            for(int x=0;x<NX;x++)for(int side=0;side<2;side++)
            {
                var all=ReadQuads(true);volume.capTemplates[x,side,0]=all==null?null:CacheQuads(all);
                byte same=reader.ReadByte();if(same>1)throw new System.IO.InvalidDataException("Invalid prepared cap alias");
                if(same==1)volume.capTemplates[x,side,1]=volume.capTemplates[x,side,0];
                else{var joined=ReadQuads(true);volume.capTemplates[x,side,1]=joined==null?null:CacheQuads(joined);}
                bool physicalColumn=volume.columns[x]!=null;var allFaces=volume.capTemplates[x,side,0];var joinedFaces=volume.capTemplates[x,side,1];
                if(physicalColumn?(allFaces==null||allFaces.Length==0||joinedFaces==null):(allFaces!=null||joinedFaces!=null))throw new System.IO.InvalidDataException("Prepared cap/column mismatch");
            }
            int maxCap=0;foreach(var cap in volume.capTemplates)if(cap!=null)maxCap=Math.Max(maxCap,cap.Length);
            volume.clippedBuffer.Capacity=volume.exterior.Count;volume.capBuffer.Capacity=maxCap*2;volume.cutSurface.Reserve(volume.exterior.Count,maxCap*2);
            return volume;
            void ReadCells(bool solid)
            {
                int count=reader.ReadInt32();if(count<0||count>size)throw new System.IO.InvalidDataException("Invalid prepared cell count");
                var coordinates=reader.ReadBytes(count*3);if(coordinates.Length!=count*3)throw new System.IO.InvalidDataException("Truncated prepared cells");
                var list=solid?volume.physical:volume.decoration;list.Capacity=count;int prior=-1;var counts=new int[NX];
                if(solid){for(int i=0;i<count;i++){int x=coordinates[i*3];if(x>=NX)throw new System.IO.InvalidDataException("Prepared column out of range");counts[x]++;}for(int x=0;x<NX;x++)if(counts[x]>0)volume.columns[x]=new List<Vector3Int>(counts[x]);}
                for(int i=0;i<count;i++)
                {
                    int x=coordinates[i*3],y=coordinates[i*3+1],z=coordinates[i*3+2];if(x>=NX||y>=NY||z>=NZ)throw new System.IO.InvalidDataException("Prepared cell out of range");
                    int index=(x*NY+y)*NZ+z;byte m=grid[index];if(index<=prior||seen[index]||m==0||m>=positive.Length||positive[m]!=solid)throw new System.IO.InvalidDataException("Invalid prepared cell order/material");
                    prior=index;seen[index]=true;var cell=new Vector3Int(x,y,z);list.Add(cell);if(solid)volume.columns[x].Add(cell);
                }
            }
            List<Quad> ReadQuads(bool cap)
            {
                int count=reader.ReadInt32();if(cap&&count==-1)return null;
                if(count<0||count>(cap?NY*NZ:size*6))throw new System.IO.InvalidDataException("Invalid prepared face count");
                if((long)count*30>reader.BaseStream.Length-reader.BaseStream.Position)throw new System.IO.InvalidDataException("Truncated prepared faces");
                var result=new List<Quad>(count);
                for(int i=0;i<count;i++)
                {
                    var lo=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());var hi=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                    int axis=reader.ReadByte(),sign=reader.ReadSByte();var shade=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());
                    if(axis>2||(sign!=1&&sign!=-1)||cap&&(axis!=0||lo.x!=0||hi.x!=0))throw new System.IO.InvalidDataException("Invalid prepared face orientation");
                    for(int a=0;a<3;a++)if(float.IsNaN(lo[a])||float.IsInfinity(lo[a])||float.IsNaN(hi[a])||float.IsInfinity(hi[a])||lo[a]>hi[a]||lo[a]<-2||hi[a]>2)throw new System.IO.InvalidDataException("Invalid prepared face bounds");
                    if(lo[axis]!=hi[axis])throw new System.IO.InvalidDataException("Prepared face is not planar");
                    result.Add(new Quad(lo,hi,axis,sign,shade));
                }
                return result;
            }
        }
#if UNITY_EDITOR
        internal void WritePrepared(System.IO.BinaryWriter writer)
        {
            if(exterior==null)throw new InvalidOperationException("Prepare the volume before baking");
            for(int x=0;x<NX;x++)for(int y=0;y<NY;y++)for(int z=0;z<NZ;z++)writer.Write(materials[x,y,z]);
            foreach(var list in new[]{physical,decoration}){writer.Write(list.Count);foreach(var cell in list){writer.Write((byte)cell.x);writer.Write((byte)cell.y);writer.Write((byte)cell.z);}}
            WriteQuads(exterior);
            for(int x=0;x<NX;x++)for(int side=0;side<2;side++)
            {
                var all=capTemplates[x,side,0];WriteQuads(all==null?null:Array.ConvertAll(all,q=>q.Face).ToList());
                bool same=ReferenceEquals(all,capTemplates[x,side,1]);writer.Write((byte)(same?1:0));
                if(!same){var joined=capTemplates[x,side,1];WriteQuads(joined==null?null:Array.ConvertAll(joined,q=>q.Face).ToList());}
            }
            void WriteQuads(List<Quad> faces)
            {
                writer.Write(faces?.Count??-1);if(faces==null)return;
                foreach(var q in faces){writer.Write(q.lo.x);writer.Write(q.lo.y);writer.Write(q.lo.z);writer.Write(q.hi.x);writer.Write(q.hi.y);writer.Write(q.hi.z);writer.Write((byte)q.axis);writer.Write((sbyte)q.sign);writer.Write(q.color.r);writer.Write(q.color.g);writer.Write(q.color.b);writer.Write(q.color.a);}
            }
        }
#endif
        Vector3 Center(int i,int j,int k)=>new Vector3((i-(NX/2-.5f))*GridStep,-1.125f+(j+.5f)*GridStep,(k-(NZ/2-.5f))*GridStep);
        byte At(int i,int j,int k)=>i<0||i>=NX||j<0||j>=NY||k<0||k>=NZ?(byte)0:materials[i,j,k];
        public byte MaterialAt(Vector3 p)=>At(Mathf.FloorToInt(p.x/GridStep)+NX/2,Mathf.FloorToInt((p.y+1.125f)/GridStep),Mathf.FloorToInt(p.z/GridStep)+NZ/2);
        public decimal DensityAt(Vector3 p)=>VoxelPrototypeGeometry.Densities[MaterialAt(p)];
        bool Has(int i,int j,int k,bool solid){byte m=At(i,j,k);return m!=0&&(VoxelPrototypeGeometry.Densities[m]>0)==solid;}
        public MassMap Mass()
        {
            var projected=new decimal[NX,NY];decimal total=0,step=.03125m/Resolution;
            foreach(var c in physical){decimal weight=VoxelPrototypeGeometry.Densities[materials[c.x,c.y,c.z]]*step*step*step;projected[c.x,c.y]+=weight;total+=weight;}
            var cells=new List<MassCell>();decimal sum=0;
            for(int i=0;i<NX;i++)for(int j=0;j<NY;j++)if(projected[i,j]>0)
            {
                decimal x=(i-NX/2)*step,y=-1.125m+j*step,m=Math.Round(100m*projected[i,j]/total,12);cells.Add(new MassCell(x,x+step,y,y+step,m));sum+=m;
            }
            var last=cells[cells.Count-1];cells[cells.Count-1]=new MassCell(last.MinX,last.MaxX,last.MinY,last.MaxY,last.Mass+100m-sum);return new MassMap(cells);
        }
        public Mesh Build(float x,bool left)
        {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double started=Time.realtimeSinceStartupAsDouble;
#endif
            var result=BuildPrepared(x,left);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.MeshMs+=(Time.realtimeSinceStartupAsDouble-started)*1000;
#endif
            return result;
        }
        Mesh BuildPrepared(float x,bool left)
        {
            if(float.IsPositiveInfinity(x)&&left)
            {
                // The caller owns each returned mesh; prepared face data remains immutable.
                if(exterior!=null&&matchEdges==null){var cachedWhole=cutSurface.Mesh(Id+" whole",exterior);cachedWhole.RecalculateNormals();return cachedWhole;}
                var whole=BuildSurface(x,left,true);if(exterior==null)RememberExterior(whole);return whole;
            }
            if(exterior==null)
            {
                var whole=BuildSurface(float.PositiveInfinity,true,true);RememberExterior(whole);
                if(Application.isPlaying)UnityEngine.Object.Destroy(whole);else UnityEngine.Object.DestroyImmediate(whole);
            }
            float renderedX=x;
            int intersectedColumn=float.IsInfinity(x)?-1:Mathf.FloorToInt(x/GridStep)+NX/2;
            if(intersectedColumn>=0&&intersectedColumn<NX)
            {
                float lo=(intersectedColumn-NX/2)*GridStep,hi=lo+GridStep;
                // Match the original minimum retained cell width, including equality.
                if(left&&x-lo<Epsilon)renderedX=lo;
                if(!left&&hi-x<Epsilon)renderedX=hi;
            }
            var clipped=clippedBuffer;clipped.Clear();var caps=capBuffer;caps.Clear();cutSurface.BeginCached(clipped,caps);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            double clipStarted=Time.realtimeSinceStartupAsDouble;
#endif
            for(int i=0;i<exteriorTemplates.Length;i++)
            {
                ref readonly var template=ref exteriorTemplates[i];var face=template.Face;
                var lo=face.lo;var hi=face.hi;
                if(face.axis==0)
                {
                    float plane=lo.x;
                    bool keep=left?(face.sign>0?!(x<plane-Epsilon):x-plane>=Epsilon):(face.sign<0?!(x>plane+Epsilon):plane-x>=Epsilon);
                    if(!keep)continue;
                    lo.x=hi.x=left?Mathf.Min(plane,renderedX):Mathf.Max(plane,renderedX);
                }
                else
                {
                    if(left)hi.x=Mathf.Min(hi.x,renderedX);else lo.x=Mathf.Max(lo.x,renderedX);
                    if(hi.x-lo.x<Epsilon)continue;
                }
                clipped.Add(new Quad(lo,hi,face.axis,face.sign,face.color));
                cutSurface.AppendCached(in template,lo.x,hi.x,false);
            }
            if(!float.IsInfinity(x))
            {
                int first=left?intersectedColumn-1:intersectedColumn,last=left?intersectedColumn:intersectedColumn+1;
                for(int column=first;column<=last;column++)
                {
                    if(column<0||column>=NX||columns[column]==null)continue;
                    float lo=(column-NX/2)*GridStep,hi=lo+GridStep,originalLo=lo,originalHi=hi;
                    if(left)hi=Mathf.Min(hi,x);else lo=Mathf.Max(lo,x);
                    if(hi-lo<Epsilon)continue;
                    float plane=left?hi:lo;if(Mathf.Abs(plane-x)>=Epsilon)continue;
                    bool interior=left?x<originalHi-Epsilon:x>originalLo+Epsilon;
                    var templates=capTemplates[column,left?0:1,interior?0:1];
                    for(int i=0;i<templates.Length;i++)
                    {
                        ref readonly var template=ref templates[i];var q=template.Face;
                        var qlo=q.lo;var qhi=q.hi;qlo.x=qhi.x=plane;caps.Add(new Quad(qlo,qhi,0,q.sign,q.color));
                        cutSurface.AppendCached(in template,plane,plane,true);
                    }
                }
            }
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
            ReviewCutTimings.ClipMs+=(Time.realtimeSinceStartupAsDouble-clipStarted)*1000;
#endif
            return cutSurface.EndCached(Id+(left?" left":" right"));
        }
        void WarmCaps()
        {
            // All cap faces have axis X and plane zero. A dense Y/Z mask avoids
            // allocating and sorting a dictionary entry for every physical cell.
            var allMask=new int[NY*NZ];var joinedMask=new int[NY*NZ];
            var shades=new List<Color32>();var shadeIndices=new Dictionary<Color32,int>();
            var joinedShades=new List<Color32>();var joinedIndices=new Dictionary<Color32,int>();
            for(int column=0;column<NX;column++)
            {
                if(columns[column]==null)continue;
                for(int side=0;side<2;side++)
                {
                    Array.Clear(allMask,0,allMask.Length);Array.Clear(joinedMask,0,joinedMask.Length);shades.Clear();shadeIndices.Clear();joinedShades.Clear();joinedIndices.Clear();
                    int sign=side==0?1:-1;bool allJoined=true;
                    foreach(var c in columns[column])
                    {
                        var p=Center(c.x,c.y,c.z);var shade=color(Id,materials[c.x,c.y,c.z],p,0,sign,true);
                        if(!shadeIndices.TryGetValue(shade,out int shadeIndex)){shades.Add(shade);shadeIndex=shades.Count;shadeIndices.Add(shade,shadeIndex);}
                        int index=c.y+NY*c.z;allMask[index]=shadeIndex;
                        if(Has(c.x+sign,c.y,c.z,true))
                        {
                            if(!joinedIndices.TryGetValue(shade,out int joinedIndex)){joinedShades.Add(shade);joinedIndex=joinedShades.Count;joinedIndices.Add(shade,joinedIndex);}
                            joinedMask[index]=joinedIndex;
                        }
                        else allJoined=false;
                    }
                    capTemplates[column,side,0]=CapQuads(allMask,shades,sign);
                    capTemplates[column,side,1]=allJoined?capTemplates[column,side,0]:CapQuads(joinedMask,joinedShades,sign);
                }
            }
        }
        CachedQuad[] CapQuads(int[] mask,List<Color32> shades,int sign)
        {
            var faces=new List<Quad>();
            // Keep the original first-seen colour groups, then ascending Z/Y
            // greedy rectangles. Identical face order preserves mesh snapshots.
            for(int shade=1;shade<=shades.Count;shade++)for(int z=0;z<NZ;z++)for(int y=0;y<NY;y++)
            {
                int index=y+NY*z;if(mask[index]!=shade)continue;int width=1,height=1;
                while(y+width<NY&&mask[index+width]==shade)width++;
                bool grow=true;
                while(z+height<NZ&&grow){for(int j=0;j<width;j++)if(mask[index+NY*height+j]!=shade){grow=false;break;}if(grow)height++;}
                var lo=new Vector3(0,-1.125f+y*GridStep,(z-NZ/2)*GridStep);var hi=new Vector3(0,-1.125f+(y+width)*GridStep,(z+height-NZ/2)*GridStep);
                for(int row=0;row<height;row++)for(int col=0;col<width;col++)mask[index+NY*row+col]=0;
                faces.Add(new Quad(lo,hi,0,sign,shades[shade-1]));
            }
            return CacheQuads(faces);
        }
        void RememberExterior(Mesh whole)
        {
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double started=SelectionPreparationReview.Start();
#endif

            var vertices=whole.vertices;var colors=whole.colors32;var normals=whole.normals;
            exterior=new List<Quad>(vertices.Length/4);
            for(int i=0;i<vertices.Length;i+=4)
            {
                var lo=vertices[i];var hi=vertices[i+2];int axis=0;while(axis<2&&lo[axis]!=hi[axis])axis++;
                exterior.Add(new Quad(lo,hi,axis,normals[i][axis]>0?1:-1,colors[i]));
            }
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double edgeStarted=SelectionPreparationReview.Start();
#endif
            if(matchEdges!=null)MatchExteriorEdges();
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.ExteriorEdgesMs,edgeStarted);
#endif

            exteriorTemplates=CacheQuads(exterior);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double capStarted=SelectionPreparationReview.Start();
#endif
            WarmCaps();
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.ExteriorCapsMs,capStarted);
#endif

            int maxCap=0;foreach(var cap in capTemplates)if(cap!=null)maxCap=Math.Max(maxCap,cap.Length);
            clippedBuffer.Capacity=exterior.Count;capBuffer.Capacity=maxCap*2;cutSurface.Reserve(exterior.Count,maxCap*2);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.ExteriorCacheMs,started);
#endif

        }
        readonly struct EdgeLine:IEquatable<EdgeLine>
        {
            readonly int axis;readonly float a,b;
            public EdgeLine(int axis,Vector3 p){this.axis=axis;a=p[(axis+1)%3];b=p[(axis+2)%3];}
            public bool Equals(EdgeLine line)=>axis==line.axis&&a==line.a&&b==line.b;
            public override bool Equals(object o)=>o is EdgeLine line&&Equals(line);
            public override int GetHashCode(){unchecked{return (axis*397^a.GetHashCode())*397^b.GetHashCode();}}
        }
        void MatchExteriorEdges()
        {
            bool changed;
            do
            {
                var lines=new Dictionary<EdgeLine,SortedSet<float>>();
                foreach(var q in exterior)
                {
                    int u=(q.axis+1)%3,v=(q.axis+2)%3;
                    var a=q.lo;var b=a;b[u]=q.hi[u];var c=q.hi;var d=a;d[v]=q.hi[v];
                    foreach(var p in new[]{a,b,c,d})for(int axis=0;axis<3;axis++)
                    {
                        var key=new EdgeLine(axis,p);if(!lines.TryGetValue(key,out var values)){values=new SortedSet<float>();lines.Add(key,values);}values.Add(p[axis]);
                    }
                }
                var result=new List<Quad>(exterior.Count);changed=false;
                foreach(var q in exterior)
                {
                    if(!matchEdges(q.color)){result.Add(q);continue;}
                    int u=(q.axis+1)%3,v=(q.axis+2)%3;
                    var us=new SortedSet<float>{q.lo[u],q.hi[u]};var vs=new SortedSet<float>{q.lo[v],q.hi[v]};
                    var opposite=q.lo;opposite[v]=q.hi[v];
                    Gather(u,q.lo,q.lo[u],q.hi[u],us);Gather(u,opposite,q.lo[u],q.hi[u],us);
                    opposite=q.lo;opposite[u]=q.hi[u];
                    Gather(v,q.lo,q.lo[v],q.hi[v],vs);Gather(v,opposite,q.lo[v],q.hi[v],vs);
                    if(us.Count==2&&vs.Count==2){result.Add(q);continue;}
                    changed=true;var xs=us.ToArray();var ys=vs.ToArray();
                    for(int j=0;j<ys.Length-1;j++)for(int i=0;i<xs.Length-1;i++)
                    {
                        var lo=q.lo;var hi=q.hi;lo[u]=xs[i];hi[u]=xs[i+1];lo[v]=ys[j];hi[v]=ys[j+1];result.Add(new Quad(lo,hi,q.axis,q.sign,q.color));
                    }
                }
                exterior=result;
                void Gather(int axis,Vector3 point,float lo,float hi,SortedSet<float> output)
                {
                    if(lines.TryGetValue(new EdgeLine(axis,point),out var values))foreach(float x in values.GetViewBetween(lo,hi))if(x>lo&&x<hi)output.Add(x);
                }
            }while(changed);
        }
        public Mesh Decoration()=>BuildSurface(float.PositiveInfinity,true,false);
        Mesh BuildSurface(float x,bool left,bool solid)
        {
            var builder=new Surface();var cells=solid?physical:decoration;
            foreach(var c in cells)
            {
                var p=Center(c.x,c.y,c.z);var lo=p-Vector3.one*GridStep*.5f;var hi=p+Vector3.one*GridStep*.5f;float originalLo=lo.x,originalHi=hi.x;
                if(left)hi.x=Mathf.Min(hi.x,x);else lo.x=Mathf.Max(lo.x,x);if(hi.x-lo.x<.0000001f)continue;
                byte m=materials[c.x,c.y,c.z];
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    var n=c;n[axis]+=sign;bool neighbour=Has(n.x,n.y,n.z,solid);
                    bool cap=solid&&axis==0&&(sign<0?!left&&Mathf.Abs(lo.x-x)<.0000001f&&(x>originalLo+.0000001f||neighbour):left&&Mathf.Abs(hi.x-x)<.0000001f&&(x<originalHi-.0000001f||neighbour));
                    if(!cap&&neighbour)continue;
                    builder.Face(lo,hi,c,axis,sign,color(Id,m,p,axis,sign,cap),cap);
                }
            }
            return builder.Mesh(Id+(solid?(float.IsInfinity(x)?" whole":left?" left":" right"):" decoration"));
        }
        sealed class Surface
        {
            internal readonly List<Vector3> Vertices=new List<Vector3>();
            readonly List<Vector3> outputNormals=new List<Vector3>();
            readonly List<Color32> outputColors=new List<Color32>();
            readonly List<int>[] outputTriangles={new List<int>(),new List<int>()};
            static readonly VertexAttributeDescriptor[] layout={
                new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
                new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.UNorm8,4)};
            PackedVertex[] packed=Array.Empty<PackedVertex>();
            int[] skinIndices=Array.Empty<int>(),capIndices=Array.Empty<int>();
            int vertexCount,skinCount,capCount;
            float cutTop=float.NegativeInfinity;
            List<Quad> restExterior,restCaps;
            internal float Top=>cutTop;
            internal float RestY(Quaternion rotation,float trayY)
            {
                float bottom=float.PositiveInfinity;
                float yx=(rotation*Vector3.right).y,yy=(rotation*Vector3.up).y,yz=(rotation*Vector3.forward).y;
                // A linear row is monotone in each coordinate. Pick its minimum corner once per rectangle.
                if(restExterior!=null)foreach(var q in restExterior)bottom=Mathf.Min(bottom,MinimumY(q,yx,yy,yz));
                if(restCaps!=null)foreach(var q in restCaps)bottom=Mathf.Min(bottom,MinimumY(q,yx,yy,yz));
                return float.IsInfinity(bottom)?0:trayY-bottom;
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            static float MinimumY(Quad q,float x,float y,float z)=>x*(x>=0?q.lo.x:q.hi.x)+y*(y>=0?q.lo.y:q.hi.y)+z*(z>=0?q.lo.z:q.hi.z);
            internal void Reserve(int exteriorCount,int capCount)
            {
                int count=(exteriorCount+capCount)*4;
                if(packed.Length<count)packed=new PackedVertex[count];
                if(skinIndices.Length<exteriorCount*6)skinIndices=new int[exteriorCount*6];
                if(capIndices.Length<capCount*6)capIndices=new int[capCount*6];
            }
            internal void BeginCached(List<Quad> exterior,List<Quad> caps)
            {
                vertexCount=skinCount=capCount=0;cutTop=float.NegativeInfinity;restExterior=exterior;restCaps=caps;
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal void AppendCached(in CachedQuad template,float loX,float hiX,bool cap)
            {
                // Only X changes at CUT; copy the immutable normal, color and Y/Z template verbatim.
                var a=template.A;var b=template.B;var c=template.C;var d=template.D;a.Position.x=loX;
                int axis=template.Face.axis;
                if(axis==0){b.Position.x=c.Position.x=d.Position.x=loX;}
                else if(axis==1){b.Position.x=loX;c.Position.x=d.Position.x=hiX;}
                else{b.Position.x=c.Position.x=hiX;d.Position.x=loX;}
                int first=vertexCount;
                packed[vertexCount++]=a;packed[vertexCount++]=b;packed[vertexCount++]=c;packed[vertexCount++]=d;
                var indices=cap?capIndices:skinIndices;int at=cap?capCount:skinCount;
                indices[at]=indices[at+3]=first;
                if(template.Face.sign>0){indices[at+1]=first+1;indices[at+2]=first+2;indices[at+4]=first+2;indices[at+5]=first+3;}
                else{indices[at+1]=first+2;indices[at+2]=first+1;indices[at+4]=first+3;indices[at+5]=first+2;}
                if(cap){capCount=at+6;cutTop=Mathf.Max(cutTop,template.Face.hi.y);}else skinCount=at+6;
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            void AppendCached(Quad q,bool cap)
            {
                var a=q.lo;Vector3 b,c,d,normal;
                if(q.axis==0){b=new Vector3(a.x,q.hi.y,a.z);c=new Vector3(a.x,q.hi.y,q.hi.z);d=new Vector3(a.x,a.y,q.hi.z);normal=new Vector3(q.sign,0,0);}
                else if(q.axis==1){b=new Vector3(a.x,a.y,q.hi.z);c=new Vector3(q.hi.x,a.y,q.hi.z);d=new Vector3(q.hi.x,a.y,a.z);normal=new Vector3(0,q.sign,0);}
                else{b=new Vector3(q.hi.x,a.y,a.z);c=new Vector3(q.hi.x,q.hi.y,a.z);d=new Vector3(a.x,q.hi.y,a.z);normal=new Vector3(0,0,q.sign);}
                int first=vertexCount;
                packed[vertexCount++]=new PackedVertex{Position=a,Normal=normal,Color=q.color};
                packed[vertexCount++]=new PackedVertex{Position=b,Normal=normal,Color=q.color};
                packed[vertexCount++]=new PackedVertex{Position=c,Normal=normal,Color=q.color};
                packed[vertexCount++]=new PackedVertex{Position=d,Normal=normal,Color=q.color};
                var indices=cap?capIndices:skinIndices;int at=cap?capCount:skinCount;
                indices[at++]=first;indices[at++]=first+(q.sign>0?1:2);indices[at++]=first+(q.sign>0?2:1);
                indices[at++]=first;indices[at++]=first+(q.sign>0?2:3);indices[at++]=first+(q.sign>0?3:2);
                if(cap){capCount=at;cutTop=Mathf.Max(cutTop,q.hi.y);}else skinCount=at;
            }
            Mesh CachedMesh(string name,List<Quad> exterior,List<Quad> caps)
            {
                Reserve(exterior.Count,caps?.Count??0);BeginCached(exterior,caps);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
                double appendStarted=Time.realtimeSinceStartupAsDouble;
#endif
                foreach(var q in exterior)AppendCached(q,false);
                if(caps!=null)foreach(var q in caps)AppendCached(q,true);
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
                ReviewCutTimings.ClipMs+=(Time.realtimeSinceStartupAsDouble-appendStarted)*1000;
#endif
                return EndCached(name);
            }
            internal Mesh EndCached(string name)
            {
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
                double uploadStarted=Time.realtimeSinceStartupAsDouble;
#endif
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                if(vertexCount>0){mesh.SetVertexBufferParams(vertexCount,layout);mesh.SetVertexBufferData(packed,0,0,vertexCount,0,MeshUpdateFlags.DontRecalculateBounds);}
                mesh.subMeshCount=2;mesh.SetIndices(skinIndices,0,skinCount,MeshTopology.Triangles,0,false);mesh.SetIndices(capIndices,0,capCount,MeshTopology.Triangles,1,false);mesh.RecalculateBounds();
#if UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW
                ReviewCutTimings.UploadMs+=(Time.realtimeSinceStartupAsDouble-uploadStarted)*1000;
#endif
                return mesh;
            }
            // Eight-bit coordinates cover both the original and the 2x authoring grids.
            readonly Dictionary<FaceKey,Dictionary<int,Rect>> groups=new Dictionary<FaceKey,Dictionary<int,Rect>>();
            struct Rect{public Vector3 lo,hi;}
            readonly struct FaceKey:IEquatable<FaceKey>
            {
                public readonly int axis,sign;public readonly float plane;public readonly Color32 color;public readonly bool cap;
                public FaceKey(int a,int s,float p,Color32 c,bool cut){axis=a;sign=s;plane=p;color=c;cap=cut;}
                public bool Equals(FaceKey k)=>axis==k.axis&&sign==k.sign&&plane==k.plane&&color.Equals(k.color)&&cap==k.cap;
                public override bool Equals(object o)=>o is FaceKey k&&Equals(k);
                public override int GetHashCode(){unchecked{return (((axis*3+sign)*397^plane.GetHashCode())*397^color.GetHashCode())*2+(cap?1:0);}}
            }
            public void Face(Vector3 lo,Vector3 hi,Vector3Int cell,int axis,int sign,Color32 color,bool cap)
            {
                int u=(axis+1)%3,v=(axis+2)%3;var key=new FaceKey(axis,sign,sign>0?hi[axis]:lo[axis],color,cap);
                if(!groups.TryGetValue(key,out var mask)){mask=new Dictionary<int,Rect>();groups.Add(key,mask);}mask.Add(cell[u]+256*cell[v],new Rect{lo=lo,hi=hi});
            }
            static void Append(Vector3 lo,Vector3 hi,int axis,int sign,Color32 color,List<Vector3> vertices,List<Color32> colors,List<int> triangles,List<Vector3> normals=null)
            {
                int u=(axis+1)%3,v=(axis+2)%3;var a=lo;var b=a;b[u]=hi[u];var c=b;c[v]=hi[v];var d=a;d[v]=hi[v];int start=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);for(int i=0;i<4;i++)colors.Add(color);
                if(normals!=null){var n=Vector3.zero;n[axis]=sign;for(int i=0;i<4;i++)normals.Add(n);}
                triangles.Add(start);triangles.Add(start+(sign>0?1:2));triangles.Add(start+(sign>0?2:1));
                triangles.Add(start);triangles.Add(start+(sign>0?2:3));triangles.Add(start+(sign>0?3:2));
            }
            public List<Quad> Quads()
            {
                var result=new List<Quad>();
                foreach(var group in groups)
                {
                    var key=group.Key;var mask=group.Value;int u=(key.axis+1)%3,v=(key.axis+2)%3;
                    foreach(int index in mask.Keys.OrderBy(i=>i).ToArray())
                    {
                        if(!mask.TryGetValue(index,out var first))continue;int col=index%256,row=index/256,width=1,height=1;
                        while(col+width<256&&mask.ContainsKey(index+width))width++;
                        bool grow=true;
                        while(grow){for(int j=0;j<width;j++)if(!mask.ContainsKey(index+256*height+j)){grow=false;break;}if(grow)height++;}
                        var last=mask[index+256*(height-1)+width-1];var lo=first.lo;var hi=last.hi;lo[key.axis]=hi[key.axis]=key.plane;
                        for(int r=0;r<height;r++)for(int c=0;c<width;c++)mask.Remove(index+256*r+c);
                        result.Add(new Quad(lo,hi,key.axis,key.sign,key.color));
                    }
                }
                return result;
            }
            public Mesh Mesh(string name,List<Quad> exterior=null,List<Quad> caps=null)
            {
                if(exterior!=null)return CachedMesh(name,exterior,caps);
                var vertices=Vertices;var colors=outputColors;var triangles=outputTriangles;vertices.Clear();colors.Clear();triangles[0].Clear();triangles[1].Clear();outputNormals.Clear();
                var normals=exterior==null?null:outputNormals;
                if(exterior!=null)foreach(var q in exterior)Append(q.lo,q.hi,q.axis,q.sign,q.color,vertices,colors,triangles[0],normals);
                if(caps!=null)foreach(var q in caps)Append(q.lo,q.hi,q.axis,q.sign,q.color,vertices,colors,triangles[1],normals);
                foreach(var group in groups)
                {
                    var key=group.Key;var mask=group.Value;int u=(key.axis+1)%3,v=(key.axis+2)%3;
                    foreach(int index in mask.Keys.OrderBy(i=>i).ToArray())
                    {
                        if(!mask.TryGetValue(index,out var first))continue;int col=index%256,row=index/256,width=1,height=1;
                        while(col+width<256&&mask.ContainsKey(index+width))width++;
                        bool grow=true;
                        while(grow){for(int j=0;j<width;j++)if(!mask.ContainsKey(index+256*height+j)){grow=false;break;}if(grow)height++;}
                        var last=mask[index+256*(height-1)+width-1];var lo=first.lo;var hi=last.hi;lo[key.axis]=hi[key.axis]=key.plane;
                        for(int r=0;r<height;r++)for(int c=0;c<width;c++)mask.Remove(index+256*r+c);
                        Append(lo,hi,key.axis,key.sign,key.color,vertices,colors,triangles[key.cap?1:0]);
                    }
                }
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.subMeshCount=2;mesh.SetTriangles(triangles[0],0,false);mesh.SetTriangles(triangles[1],1,false);if(normals==null)mesh.RecalculateNormals();else mesh.SetNormals(normals);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
