using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
    // B01 fine-voxel prototype. Occupancy is volumetric; the knife never snaps to this grid.
    public static class VoxelAppleGeometry
    {
        public const float Step=.035f;
        const int N=64;
        static readonly bool[,,] solid=new bool[N,N,N];
        static readonly List<Vector3Int> cells=new List<Vector3Int>();
        static VoxelAppleGeometry()
        {
            for(int i=0;i<N;i++)for(int j=0;j<N;j++)for(int k=0;k<N;k++)
            {var p=Center(i,j,k);if(Inside(p)){solid[i,j,k]=true;cells.Add(new Vector3Int(i,j,k));}}
        }
        static Vector3 Center(int i,int j,int k)=>new Vector3((i-31.5f)*Step,(j-31.5f)*Step,(k-31.5f)*Step);
        static bool Inside(Vector3 p)
        {
            float y=p.y/.94f, z=p.z/.86f;
            float r=Mathf.Sqrt(p.x*p.x+z*z);
            float radius=Mathf.Sqrt(Mathf.Max(0,1-y*y))*(1.04f+.09f*y);
            return Mathf.Abs(y)<1 && r<radius && p.y<.94f-.16f*Mathf.Exp(-r*r/.08f);
        }
        static bool Has(int i,int j,int k)=>i>=0&&i<N&&j>=0&&j<N&&k>=0&&k<N&&solid[i,j,k];
        public static int CellCount=>cells.Count;
        // Depth sums export a separate 2D mass map for this trial version only.
        public static IEnumerable<(float x,float y,float mass)> ProjectedMass()
        {
            for(int i=0;i<N;i++)for(int j=0;j<N;j++)
            {int count=0;for(int k=0;k<N;k++)if(solid[i,j,k])count++;if(count>0)yield return ((i-32)*Step,(j-32)*Step,count*Step*Step*Step);}
        }
        public static Mesh Build(float knifeX,bool left)
        {
            var b=new Builder();
            foreach(var c in cells)
            {
                var p=Center(c.x,c.y,c.z);var lo=p-Vector3.one*Step*.5f;var hi=p+Vector3.one*Step*.5f;
                float a=left?lo.x:Mathf.Max(lo.x,knifeX),d=left?Mathf.Min(hi.x,knifeX):hi.x;
                if(d-a<.000001f)continue;
                lo.x=a;hi.x=d;
                bool capLeft=!left && Mathf.Abs(a-knifeX)<.000001f && (knifeX>p.x-Step*.5f+.000001f || Has(c.x-1,c.y,c.z));
                bool capRight=left && Mathf.Abs(d-knifeX)<.000001f && (knifeX<p.x+Step*.5f-.000001f || Has(c.x+1,c.y,c.z));
                if(capLeft||!Has(c.x-1,c.y,c.z))b.Face(lo,hi,0,-1,capLeft?Section(new Vector3(a,p.y,p.z)):Skin(p,0,-1),capLeft);
                if(capRight||!Has(c.x+1,c.y,c.z))b.Face(lo,hi,0,1,capRight?Section(new Vector3(d,p.y,p.z)):Skin(p,0,1),capRight);
                if(!Has(c.x,c.y-1,c.z))b.Face(lo,hi,1,-1,Skin(p,1,-1),false);
                if(!Has(c.x,c.y+1,c.z))b.Face(lo,hi,1,1,Skin(p,1,1),false);
                if(!Has(c.x,c.y,c.z-1))b.Face(lo,hi,2,-1,Skin(p,2,-1),false);
                if(!Has(c.x,c.y,c.z+1))b.Face(lo,hi,2,1,Skin(p,2,1),false);
            }
            return b.Mesh("B01 fine voxel "+(float.IsInfinity(knifeX)?"whole":left?"left":"right"));
        }
        static Color32 Skin(Vector3 p,int axis,int sign)
        {
            // Broad quantized matte planes; no random noise and no white specular spots.
            var n=new Vector3(p.x,p.y*.85f,p.z/ .74f).normalized;
            float light=Mathf.Clamp01(Vector3.Dot(n,new Vector3(-.5f,.65f,-.65f).normalized)*.5f+.48f);
            light=Mathf.Round(light*7)/7;
            var col=Color.Lerp(new Color(.43f,.075f,.13f),new Color(.91f,.29f,.27f),light);
            if(axis==1&&sign>0)col*=1.025f; else if(axis==0&&sign>0)col*=.97f;
            return col;
        }
        static Color32 Section(Vector3 p)
        {
            float edge=.06f;
            if(!Inside(p+Vector3.up*edge)||!Inside(p-Vector3.up*edge)||!Inside(p+Vector3.forward*edge)||!Inside(p-Vector3.forward*edge))return new Color(.72f,.15f,.19f);
            var flesh=new Color(.96f,.84f,.60f);
            if(Mathf.Abs(p.x)<.17f && Mathf.Abs(p.z)<(.05f+.05f*Mathf.Cos(p.y*5)) && Mathf.Abs(p.y-.02f)<.53f)flesh=new Color(.86f,.70f,.43f);
            if(Mathf.Abs(p.x)<.11f)
            {
                foreach(float z in new[]{-.19f,.19f})foreach(float y in new[]{-.18f,.13f})
                {float a=(p.z-z)/.078f,b=(p.y-y)/.11f;if(a*a+b*b<1)flesh=new Color(.30f,.16f,.10f);}
            }
            return flesh;
        }
        // Decoration is real small block geometry, attached to the corresponding cut half.
        public static Mesh Decoration(bool leaf)
        {
            return DecorationPart(leaf,float.PositiveInfinity,true);
        }
        public static Mesh DecorationSide(float x,bool left)
        {
            var a=DecorationPart(false,x,left);var b=DecorationPart(true,x,left);var result=new Mesh{name="Cut stem and leaf",indexFormat=IndexFormat.UInt32};
            result.CombineMeshes(new[]{new CombineInstance{mesh=a,transform=Matrix4x4.identity},new CombineInstance{mesh=b,transform=Matrix4x4.identity}},true,true);
            if(Application.isPlaying){UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);}else{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}return result;
        }
        static Mesh DecorationPart(bool leaf,float knifeX,bool left)
        {
            var b=new Builder{Knife=knifeX,Left=left};
            if(!leaf)
            {
                for(int i=0;i<10;i++){var p=new Vector3(.01f+i*.009f,.80f+i*.027f,0);b.Box(p,new Vector3(.067f,.035f,.065f),new Color(.30f+i*.012f,.17f+i*.008f,.095f));}
            }
            else
            {
                for(int i=0;i<18;i++)for(int j=-5;j<=5;j++)
                {
                    float t=(i+.5f)/18, width=Mathf.Sin(t*Mathf.PI)*4.6f;
                    if(Mathf.Abs(j)>width)continue;
                    var p=new Vector3(.08f+t*.57f,1.035f+t*.20f+j*.014f,-.035f+j*.027f);
                    b.Box(p,new Vector3(.035f,.026f,.032f),j==0?new Color(.56f,.63f,.27f):j<0?new Color(.26f,.43f,.19f):new Color(.38f,.52f,.22f));
                }
            }
            return b.Mesh(leaf?"B01 voxel leaf":"B01 voxel stem");
        }
        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color32> colors=new List<Color32>();
            readonly List<int>[] triangles={new List<int>(),new List<int>()};
            public float Knife=float.PositiveInfinity;public bool Left=true;
            public void Box(Vector3 p,Vector3 size,Color c){var lo=p-size*.5f;var hi=p+size*.5f;if(Left)hi.x=Mathf.Min(hi.x,Knife);else lo.x=Mathf.Max(lo.x,Knife);if(hi.x-lo.x<.000001f)return;for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)Face(lo,hi,axis,sign,c,false);}
            public void Face(Vector3 lo,Vector3 hi,int axis,int sign,Color32 color,bool cap)
            {
                int u=(axis+1)%3,v=(axis+2)%3,start=vertices.Count;
                var a=lo;a[axis]=sign>0?hi[axis]:lo[axis];var d=a;d[u]=hi[u];var e=d;e[v]=hi[v];var f=a;f[v]=hi[v];
                vertices.Add(a);vertices.Add(d);vertices.Add(e);vertices.Add(f);for(int n=0;n<4;n++)colors.Add(color);
                var t=triangles[cap?1:0];if(sign>0)t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});else t.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(vertices);m.SetColors(colors);m.subMeshCount=2;m.SetTriangles(triangles[0],0);m.SetTriangles(triangles[1],1);m.RecalculateNormals();m.RecalculateBounds();return m;}
        }
    }
}
