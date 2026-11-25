using System;
using System.Collections.Generic;
using System.IO;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
    // One Blender volume drives the rotating exterior, the frozen cut and the projected mass.
    public sealed class PixelPlanetVolume
    {
        readonly int n;
        readonly float step, speed;
        readonly byte[] grid;
        readonly ushort[] density;
        readonly Color32[] skin, section;
        public readonly float RestOffset;
        readonly List<SurfaceCell> surface = new List<SurfaceCell>();
        struct SurfaceCell { public Vector3 center; public int faces; public byte material; }
        readonly bool photosphere;
        public PixelPlanetVolume(byte[] bytes,bool photosphere=false)
        {
            this.photosphere=photosphere;
            using var reader = new BinaryReader(new MemoryStream(bytes));
            if (reader.ReadUInt32()!=0x31504848) throw new InvalidDataException("Invalid planet volume");
            n=reader.ReadByte(); step=reader.ReadSingle(); speed=reader.ReadSingle();
            int count=reader.ReadByte()+1;
            density=new ushort[count]; skin=new Color32[count]; section=new Color32[count];
            Color32 Color() => new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),255);
            for(int i=1;i<count;i++){density[i]=reader.ReadUInt16();skin[i]=Color();section[i]=Color();
                if(photosphere)skin[i].a=0;}
            grid=reader.ReadBytes(n*n*n);
            if(grid.Length!=n*n*n)throw new EndOfStreamException();
            int lowest=n;
            int[] stride={n*n,n,1};
            for(int x=0;x<n;x++)for(int y=0;y<n;y++)for(int z=0;z<n;z++)
            {
                int j=(x*n+y)*n+z;
                if(grid[j]==0)continue;
                if(x==0||y==0||z==0||x==n-1||y==n-1||z==n-1)throw new InvalidDataException("Planet needs an air border");
                lowest=Math.Min(lowest,y);
                int mask=0;
                for(int axis=0;axis<3;axis++){
                    if(grid[j-stride[axis]]==0)mask|=1<<(2*axis);
                    if(grid[j+stride[axis]]==0)mask|=2<<(2*axis);
                }
                if(mask!=0)surface.Add(new SurfaceCell{center=new Vector3(At(x),At(y),At(z)),material=grid[j],faces=mask});
            }
            RestOffset=-1.305f-(lowest-n*.5f)*step;
        }
        float At(int i)=>(i+.5f-n*.5f)*step;
        public Quaternion Pose(long ms)=>Quaternion.Euler(0,speed*(Math.Min(120000L,Math.Max(0L,ms))*.001f),0);

        // A rotated square projects to a trapezoid. Integrate that profile into
        // half-voxel-wide screen columns, retaining continuous cuts within each column.
        static double Fraction(double x,double a,double b)
        {
            if(x<=0)return 0;
            if(x>=a+b)return 1;
            if(b<1e-7)return Math.Min(1,x/a);
            if(x<b)return x*x/(2*a*b);
            if(x<a)return (x-.5*b)/a;
            double tail=a+b-x;return 1-tail*tail/(2*a*b);
        }
        public MassMap MassAt(long ms)
        {
            var forward=Pose(ms)*Vector3.forward;
            double c=forward.z,s=forward.x;
            double a=Math.Max(Math.Abs(c),Math.Abs(s))*step,b=Math.Min(Math.Abs(c),Math.Abs(s))*step;
            double bin=step*.5;
            int halfBins=(int)Math.Ceiling(n*Math.Sqrt(2)),bins=2*halfBins;
            var mass=new double[bins*n];
            for(int x=0;x<n;x++)for(int z=0;z<n;z++)
            {
                double start=At(x)*c+At(z)*s-(a+b)/2;
                int first=(int)Math.Floor(start/bin)+halfBins,last=(int)Math.Ceiling((start+a+b)/bin)+halfBins;
                double prior=0;
                for(int col=first;col<last;col++){
                    double next=Fraction((col+1-halfBins)*bin-start,a,b);
                    double weight=next-prior;prior=next;
                    if(weight<=0)continue;
                    for(int y=0;y<n;y++)mass[col*n+y]+=weight*density[grid[(x*n+y)*n+z]];
                }
            }
            var cells=new List<MassCell>();decimal dx=(decimal)step/2,dy=(decimal)step;
            for(int i=0;i<bins;i++)for(int y=0;y<n;y++){
                if(mass[i*n+y]<1e-8)continue;
                decimal lo=(i-halfBins)*dx, bottom=(y-n/2)*dy;
                cells.Add(new MassCell(lo,lo+dx,bottom,bottom+dy,(decimal)Math.Round(mass[i*n+y],8)));
            }
            return new MassMap(cells);
        }

        sealed class Geometry
        {
            public readonly List<Vector3> points=new List<Vector3>(), normals=new List<Vector3>();
            public readonly List<Color32> colors=new List<Color32>();
            public readonly List<int> skin=new List<int>(),cap=new List<int>();
            public void Polygon(Vector3[] p,int count,Vector3 normal,Color32 color,bool section)
            {
                var indices=section?cap:skin;int first=points.Count;
                for(int i=0;i<count;i++){points.Add(p[i]);normals.Add(normal);colors.Add(color);}
                for(int i=2;i<count;i++){indices.Add(first);indices.Add(first+i-1);indices.Add(first+i);}
            }
            public Mesh Mesh(){
                var m=new Mesh{indexFormat=IndexFormat.UInt32};
                m.SetVertices(points);m.SetNormals(normals);m.SetColors(colors);m.subMeshCount=2;
                m.SetTriangles(skin,0);m.SetTriangles(cap,1);m.RecalculateBounds();return m;
            }
        }
        static int Clip(Vector3[] face,Vector3[] output,float x,bool left)
        {
            int count=0;var a=face[3];
            foreach(var b in face){
                bool inA=left?a.x<=x:a.x>=x,inB=left?b.x<=x:b.x>=x;
                if(inA!=inB)output[count++]=a+(b-a)*((x-a.x)/(b.x-a.x));
                if(inB)output[count++]=b;
                a=b;
            }
            return count;
        }
        public Mesh Build(long ms,float cut,bool left,out float top)
        {
            top=float.NegativeInfinity;
            var pose=Pose(ms);float h=step*.5f;bool whole=float.IsPositiveInfinity(cut);
            var g=new Geometry();var quad=new Vector3[4];var clipped=new Vector3[8];
            foreach(var cell in surface)
            {
                var center=pose*cell.center;
                if(!whole&&(left?center.x-h*1.415f>cut:center.x+h*1.415f<cut))continue;
                for(int face=0;face<6;face++){
                    if((cell.faces&(1<<face))==0)continue;
                    int axis=face/2,u=(axis+1)%3,v=(axis+2)%3;int sign=face%2==0?-1:1;
                    Vector3 normal=Vector3.zero;normal[axis]=sign;
                    for(int i=0;i<4;i++){
                        int corner=sign>0?i:3-i;
                        Vector3 p=cell.center+normal*h;
                        p[u]+=corner==1||corner==2?h:-h;p[v]+=corner>=2?h:-h;
                        quad[i]=pose*p;
                    }
                    var shadeNormal=pose*(photosphere?cell.center.normalized:(cell.center.normalized+.11f*normal).normalized);
                    int count=whole?4:Clip(quad,clipped,cut,left);
                    if(count>=3)g.Polygon(whole?quad:clipped,count,shadeNormal,skin[cell.material],false);
                }
            }
            if(!whole){
                for(int x=0;x<n;x++)for(int z=0;z<n;z++){
                    float lo=float.PositiveInfinity,hi=float.NegativeInfinity,minX=lo,maxX=hi;
                    for(int i=0;i<4;i++){
                        quad[i]=pose*new Vector3(At(x)+(i==1||i==2?h:-h),0,At(z)+(i>=2?h:-h));
                        minX=Mathf.Min(minX,quad[i].x);maxX=Mathf.Max(maxX,quad[i].x);
                    }
                    if(left?(cut<=minX||cut>maxX):(cut<minX||cut>=maxX))continue;
                    for(int i=0;i<4;i++){
                        var a=quad[i];var b=quad[(i+1)%4];
                        if(cut<Mathf.Min(a.x,b.x)||cut>Mathf.Max(a.x,b.x))continue;
                        if(a.x==b.x){lo=Mathf.Min(lo,Mathf.Min(a.z,b.z));hi=Mathf.Max(hi,Mathf.Max(a.z,b.z));}
                        else {float t=a.z+(b.z-a.z)*((cut-a.x)/(b.x-a.x));lo=Mathf.Min(lo,t);hi=Mathf.Max(hi,t);}
                    }
                    if(hi<=lo)continue;
                    for(int y=0;y<n;y++){
                        byte m=grid[(x*n+y)*n+z];if(m==0)continue;
                        float y0=At(y)-h,y1=At(y)+h;top=Mathf.Max(top,y1);
                        clipped[0]=new Vector3(cut,y0,left?lo:hi);clipped[1]=new Vector3(cut,y1,left?lo:hi);
                        clipped[2]=new Vector3(cut,y1,left?hi:lo);clipped[3]=new Vector3(cut,y0,left?hi:lo);
                        g.Polygon(clipped,4,left?Vector3.right:Vector3.left,section[m],true);
                    }
                }
            }
            return g.Mesh();
        }
    }
}
