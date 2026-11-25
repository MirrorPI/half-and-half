using System;
using System.Collections.Generic;
using System.IO;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
    // The Blender file contains one closed box per star or parcel of gas.
    // Each box keeps its volume while its own orbit changes its position.
    public sealed class VoxelGalaxyVolume
    {
        const int Strips = 4;
        const float BoxCos = .948f, BoxSin = .318f;
        readonly bool ring,encounter;
        readonly Star[] stars;
        readonly Vector3[] corners;
        readonly Vector3[] liveVertices;
        readonly Strip[] strips;
        readonly Vector2[] footprint;
        static readonly int[] boxFaces = {0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0};

        struct Star
        {
            public float Radius, Angle, Height, HalfHeight, Phase, Speed, Growth, Delay;
            public byte Group;
            public Color32 Color;
            public decimal Weight;
        }
        struct Strip { public float Left,Right,Low,High; public decimal Mass; }
        struct Frame
        {
            public float Time, SpinCos,SpinSin, InclCos,InclSin,RollCos,RollSin;
            public float PassagePhase,Approach,LagFactor;
        }
        public int Count=>stars.Length;
        public VoxelGalaxyVolume(byte[] bytes,string objectId)
        {
            ring=objectId=="G02";
            encounter=objectId=="G03";
            using var reader=new BinaryReader(new MemoryStream(bytes));
            if(reader.ReadUInt32()!=0x32564748)throw new InvalidDataException("Invalid 3D galaxy volume");
            int count=reader.ReadInt32();
            stars=new Star[count];corners=new Vector3[count*8];liveVertices=new Vector3[count*8];
            footprint=new Vector2[count*4];strips=new Strip[count*Strips];
            decimal total=0;
            var random=new System.Random(encounter?403839:ring?22484:5169);
            for(int i=0;i<count;i++){
                float x=reader.ReadSingle(),y=reader.ReadSingle(),height=reader.ReadSingle();
                float rx=reader.ReadSingle(),ry=reader.ReadSingle(),rz=reader.ReadSingle(),density=reader.ReadSingle();
                var color=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),255);
                byte group=reader.ReadByte();
                // Galactic disk Blender Z is height. The source's Y and Z radii
                // become box depth and height in Unity respectively.
                float ox=group==2?7.48f:(!ring||group==1)?(ring?-1.12f:-1f):0;
                float oy=group==2?2.4f:ring&&group==1?.38f:0;
                if(encounter){ox=group<4?-6.4f:6.4f;oy=0;}
                float u=x-ox,v=y-oy,r=Mathf.Sqrt(u*u+v*v);
                var star=new Star{Radius=r,Angle=Mathf.Atan2(v,u),Height=height,HalfHeight=rz,
                    Group=group,Color=color,Phase=(float)random.NextDouble()*2*Mathf.PI,
                    Speed=.72f+(float)random.NextDouble()*.87f,Weight=(decimal)(8*rx*ry*rz*density),
                    Growth=Mathf.Clamp01(r/7.15f)*(.37f+.24f*u/Mathf.Max(.01f,r)),
                    Delay=.4f+1.3f*Mathf.Clamp01(r/7.15f)+.65f*(1+v/Mathf.Max(.01f,r))};
                stars[i]=star;total+=star.Weight;
                float yaw=.36f+.38f*(float)random.NextDouble(),c=Mathf.Cos(yaw),s=Mathf.Sin(yaw);
                for(int j=0;j<4;j++){
                    float a=(j==0||j==3)?-rx:rx,b=j<2?-ry:ry;
                    var q=new Vector2(a*c+b*s,-a*s+b*c);
                    footprint[i*4+j]=q;
                    corners[i*8+j]=Offset(q,-rz);corners[i*8+4+j]=Offset(q,rz);
                }
            }
            decimal used=0;
            var quad=new Vector2[8];var first=new Vector2[8];var band=new Vector2[8];
            for(int i=0;i<count;i++){
                decimal w=i+1==count?100-used:decimal.Round(100*stars[i].Weight/total,13);
                stars[i].Weight=w;used+=w;
                Array.Copy(footprint,i*4,quad,0,4);
                float extent=Mathf.Max(Mathf.Abs(quad[0].x),Mathf.Abs(quad[1].x)),area=Area(quad,4);
                decimal assigned=0;
                for(int j=0;j<Strips;j++){
                    float lo=extent*(2f*j/Strips-1),hi=extent*(2f*(j+1)/Strips-1);
                    int n=Clip(quad,4,first,lo,false);n=Clip(first,n,band,hi,true);
                    float minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity;
                    for(int k=0;k<n;k++){minZ=Mathf.Min(minZ,band[k].y);maxZ=Mathf.Max(maxZ,band[k].y);}
                    decimal mass=j+1==Strips?w-assigned:w*(decimal)(Area(band,n)/area);
                    assigned+=mass;
                    strips[i*Strips+j]=new Strip{Left=lo,Right=hi,Low=minZ*BoxSin-stars[i].HalfHeight*BoxCos,
                        High=maxZ*BoxSin+stars[i].HalfHeight*BoxCos,Mass=mass};
                }
            }
        }
        static Vector3 Offset(Vector2 xz,float y)=>new Vector3(xz.x,y*BoxCos+xz.y*BoxSin,xz.y*BoxCos-y*BoxSin);
        static float Area(Vector2[] points,int n){
            float sum=0;
            for(int i=0;i<n;i++){var a=points[i];var b=points[(i+1)%n];sum+=a.x*b.y-b.x*a.y;}
            return Mathf.Abs(sum)*.5f;
        }
        static int Clip(Vector2[] input,int n,Vector2[] output,float x,bool left){
            if(n==0)return 0;
            int count=0;var a=input[n-1];bool insideA=left?a.x<=x:a.x>=x;
            for(int i=0;i<n;i++){
                var b=input[i];bool insideB=left?b.x<=x:b.x>=x;
                if(insideA!=insideB && a.x!=x && b.x!=x)
                    output[count++]=new Vector2(x,Mathf.LerpUnclamped(a.y,b.y,(x-a.x)/(b.x-a.x)));
                if(insideB)output[count++]=b;
                a=b;insideA=insideB;
            }
            return count;
        }
        Frame At(long ms){
            float t=Mathf.Clamp(ms,0,120000)*.001f;
            if(encounter){
                float phase=t*.43f;
                return new Frame{Time=t,PassagePhase=phase,Approach=Mathf.Sin(phase),LagFactor=1-Mathf.Exp(-t*.75f)};
            }
            float a=t*(ring?.54f:.42f);
            float tilt=(ring?.94f:1.06f)+.13f*Mathf.Sin(t*.46f);
            float roll=(ring?.20f:-.18f)+.12f*Mathf.Sin(t*.57f);
            return new Frame{Time=t,SpinCos=Mathf.Cos(a),SpinSin=Mathf.Sin(a),InclCos=Mathf.Cos(tilt),
                InclSin=Mathf.Sin(tilt),RollCos=Mathf.Cos(roll),RollSin=Mathf.Sin(roll)};
        }
        // The outer streamers trail their parent nucleus through a close passage.
        // Heights and distances are in model space; the same pose feeds the mesh and mass map.
        static Vector3 EncounterCenter(Star s,Frame frame)
        {
            bool right=s.Group>=4;
            int region=s.Group%4; // disk, nucleus, tidal tail, or inner gas lane
            float side=right?1f:-1f;
            float time=frame.Time;
            float radialFraction=Mathf.Clamp01(s.Radius/9.5f);
            float lag=(region==2?1.08f:.25f)*radialFraction;
            float trailingPhase=frame.PassagePhase-lag*frame.LagFactor;
            float approach=frame.Approach;
            float individual=Mathf.Sin(s.Phase+time*s.Speed)-Mathf.Sin(s.Phase);
            float rate=region==1?.54f+.17f*s.Speed:region==2?.115f:.29f;
            float angle=s.Angle+side*time*rate+individual*(region==1?.17f:.09f)
                +side*.26f*radialFraction*approach;
            float tidal=(region==2?.19f:.065f)*radialFraction*approach*approach;
            float r=s.Radius*(1+tidal)+.11f*individual;
            float x=r*Mathf.Cos(angle),y=r*Mathf.Sin(angle);
            float height=s.Height+.23f*(Mathf.Sin(s.Phase*.8f+time*s.Speed)-Mathf.Sin(s.Phase*.8f));
            float tilt=right?.77f:1.04f;
            float v=y*Mathf.Cos(tilt)+height*Mathf.Sin(tilt);
            float z=y*Mathf.Sin(tilt)-height*Mathf.Cos(tilt);
            float roll=right?-.29f:.53f;
            return new Vector3(side*6.4f*Mathf.Cos(trailingPhase)+x*Mathf.Cos(roll)-v*Mathf.Sin(roll),
                side*(1.45f+.95f*approach)+x*Mathf.Sin(roll)+v*Mathf.Cos(roll),
                side*1.65f*approach+z*.71f);
        }
        Vector3 Center(Star s,Frame f){
            if(encounter)return EncounterCenter(s,f);
            float t=f.Time,r=s.Radius,a=s.Angle,h=s.Height;
            float stream=Mathf.Sin(s.Phase+t*s.Speed)-Mathf.Sin(s.Phase);
            float floatUp=Mathf.Sin(s.Phase*.7f+t*(.8f+s.Speed)) - Mathf.Sin(s.Phase*.7f);
            float ox=ring?0:-1,oy=0;
            if(s.Group==1||s.Group==2){
                if(s.Group==2){ox=7.48f;oy=2.4f;h+=.55f*Mathf.Sin(t*.62f);}
                else if(ring){ox=-1.12f;oy=.38f;}
                a+=t*(.40f+.42f*s.Speed)/(1+.22f*r);
                h+=.17f*floatUp;
            }else if(ring){
                float wave=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-s.Delay)/(8-s.Delay)));
                r*=1+s.Growth*wave;
                a+=.085f*stream+.08f*Mathf.Sin(t*.61f+s.Angle)*Mathf.Sin(t*.60f);
                h+=.26f*floatUp+.85f*Mathf.Sin(Mathf.PI*wave)*Mathf.Sin(a*2);
            }else{
                float radial=(s.Group==3?.10f:.50f)*stream;
                r=Mathf.Max(.02f,r+radial);
                a+=radial*.62f+.075f*stream+.13f*Mathf.Sin(.54f*t+s.Angle)*Mathf.Sin(.46f*t);
                h+=.23f*floatUp;
            }
            float x=ox+r*Mathf.Cos(a),y=oy+r*Mathf.Sin(a);
            float u=x*f.SpinCos-y*f.SpinSin,v=x*f.SpinSin+y*f.SpinCos;
            float skyY=v*f.InclCos+h*f.InclSin;
            return new Vector3(u*f.RollCos-skyY*f.RollSin,u*f.RollSin+skyY*f.RollCos,
                (v*f.InclSin-h*f.InclCos)*.82f);
        }
        public Vector3 CenterAt(int i,long time)=>Center(stars[i],At(time));
        public MassMap MassAt(long ms){
            var frame=At(ms);var cells=new List<MassCell>(strips.Length);
            for(int i=0;i<stars.Length;i++){
                var p=Center(stars[i],frame);
                for(int j=0;j<Strips;j++){
                    var s=strips[i*Strips+j];
                    cells.Add(new MassCell((decimal)(p.x+s.Left),(decimal)(p.x+s.Right),
                        (decimal)(p.y+s.Low),(decimal)(p.y+s.High),s.Mass));
                }
            }
            return new MassMap(cells);
        }
        public void Pose(Mesh mesh,long ms){
            var frame=At(ms);
            for(int i=0;i<stars.Length;i++){
                var p=Center(stars[i],frame);
                for(int j=i*8;j<i*8+8;j++)liveVertices[j]=p+corners[j];
            }
            mesh.SetVertices(liveVertices,0,liveVertices.Length,MeshUpdateFlags.DontRecalculateBounds);
        }
        // Sample the posed solids for a soft layer of unresolved starlight.
        // The shared mesh freezes with the solids; each fragment clips it in local X.
        const int HazeStride=4;
        Vector3[] hazeVertices;
        public Mesh BuildHaze(){
            int count=(stars.Length+HazeStride-1)/HazeStride;
            hazeVertices=new Vector3[count*4];
            var coordinates=new List<Vector3>(count*4);
            var colours=new Color32[count*4];
            var indices=new int[count*6];
            for(int i=0;i<count;i++){
                var star=stars[i*HazeStride];
                float radius=.38f+star.HalfHeight*8.5f;
                int start=i*4;
                for(int k=0;k<4;k++){
                    coordinates.Add(new Vector3(k>=2?1:-1,k==1||k==2?1:-1,radius));
                    colours[start+k]=star.Color;
                }
                int t=i*6;
                indices[t]=start;indices[t+1]=start+1;indices[t+2]=start+2;
                indices[t+3]=start;indices[t+4]=start+2;indices[t+5]=start+3;
            }
            var mesh=new Mesh{name="Diffuse galactic light"};
            mesh.MarkDynamic();mesh.vertices=hazeVertices;mesh.colors32=colours;mesh.SetUVs(0,coordinates);
            mesh.SetTriangles(indices,0);mesh.bounds=new Bounds(Vector3.zero,Vector3.one*(encounter?52:40));
            return mesh;
        }
        public void PoseHaze(Mesh mesh){
            for(int i=0;i<hazeVertices.Length/4;i++){
                int vertex=i*HazeStride*8;
                var center=liveVertices[vertex]-corners[vertex];
                for(int k=0;k<4;k++)hazeVertices[i*4+k]=center;
            }
            mesh.SetVertices(hazeVertices,0,hazeVertices.Length,MeshUpdateFlags.DontRecalculateBounds);
        }
        public Mesh Whole(){
            var mesh=new Mesh{name="Orbiting solid voxels",indexFormat=IndexFormat.UInt32};
            int[] indices=new int[stars.Length*boxFaces.Length];var colors=new Color32[corners.Length];
            for(int i=0;i<stars.Length;i++){
                for(int j=0;j<8;j++)colors[i*8+j]=stars[i].Color;
                for(int j=0;j<boxFaces.Length;j++)indices[i*boxFaces.Length+j]=i*8+boxFaces[j];
            }
            Pose(mesh,0);mesh.colors32=colors;mesh.SetIndices(indices,MeshTopology.Triangles,0);
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*(encounter?46:34));return mesh;
        }
        public Mesh Slice(long ms,float knife,bool left,out float top){
            var frame=At(ms);top=float.NegativeInfinity;
            var vertices=new List<Vector3>();var colors=new List<Color32>();var triangles=new List<int>();
            var src=new Vector2[4];var section=new Vector2[8];
            for(int i=0;i<stars.Length;i++){
                var star=stars[i];var p=Center(star,frame);float x=knife-p.x;
                float lo=strips[i*Strips].Left,hi=strips[i*Strips+Strips-1].Right;
                if(left?x<=lo:x>=hi)continue;
                int start=vertices.Count;
                if(left?x>=hi:x<=lo){
                    for(int k=0;k<8;k++){vertices.Add(p+corners[8*i+k]);colors.Add(star.Color);}
                    foreach(int k in boxFaces)triangles.Add(start+k);
                    continue;
                }
                Array.Copy(footprint,i*4,src,0,4);int n=Clip(src,4,section,x,left);
                for(int side=0;side<2;side++)for(int k=0;k<n;k++){
                    vertices.Add(p+Offset(section[k],(side==0?-1:1)*star.HalfHeight));colors.Add(star.Color);
                }
                for(int k=1;k+1<n;k++){
                    triangles.Add(start);triangles.Add(start+k);triangles.Add(start+k+1);
                    triangles.Add(start+n);triangles.Add(start+n+k+1);triangles.Add(start+n+k);
                }
                for(int k=0;k<n;k++){
                    int k1=(k+1)%n,l=start+k,b=start+k1,u=l+n,v=b+n;
                    if(section[k].x==x && section[k1].x==x){
                        top=Mathf.Max(top,Mathf.Max(vertices[u].y,vertices[v].y));
                        Color32 cap=Color32.Lerp(star.Color,new Color32(255,213,158,255),.32f);
                        int c=vertices.Count;
                        vertices.Add(vertices[l]);vertices.Add(vertices[u]);vertices.Add(vertices[v]);vertices.Add(vertices[b]);
                        for(int j=0;j<4;j++)colors.Add(cap);
                        l=c;u=c+1;v=c+2;b=c+3;
                    }
                    triangles.Add(l);triangles.Add(u);triangles.Add(v);triangles.Add(l);triangles.Add(v);triangles.Add(b);
                }
            }
            var mesh=new Mesh{name="Cut solid star field",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);return mesh;
        }
    }
}
