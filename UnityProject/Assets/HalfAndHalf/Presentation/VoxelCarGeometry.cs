using System;
using System.Collections.Generic;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
using HalfAndHalf.Verification;
#endif

namespace HalfAndHalf.Presentation
{
    // Approved one-car prototype. The cell grid authors material volume, never knife positions.
    public static class VoxelCarGeometry
    {
        public const float Step = .03125f;
        public const string Version = "L02-fine-voxel-trial-v1";
        const int NX=80, NY=60, NZ=40;
        const byte Paint=1, Glass=2, Tire=3, Alloy=4, Steel=5, Engine=6, Seat=7, Lamp=8, Tail=9, Copper=10;
        static readonly byte[,,] material = new byte[NX,NY,NZ];
        static readonly List<Vector3Int> cells = new List<Vector3Int>();
        static VoxelVolume cutVolume;
        internal static float CutTop=>cutVolume.CutTop;
        internal static float CutRestY(Quaternion rotation,float trayY)=>cutVolume.CutRestY(rotation,trayY);
        static readonly HashSet<Color32> glassShades=GlassShades();
        static HashSet<Color32> GlassShades(){var shades=new HashSet<Color32>();for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)shades.Add(ColorFor(Glass,Vector3.zero,axis,sign,false));return shades;}
        static readonly decimal[] density = {0,1.3m,.35m,1.1m,2.4m,2.2m,6m,.3m,.1m,.1m,1.6m};
        static VoxelCarGeometry()
        {
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double started=SelectionPreparationReview.Start();
#endif

            bool prepared=VoxelPreparationCache.TryLoad("L02",(id,m,p,axis,sign,cap)=>ColorFor(m,p,axis,sign,cap),shade=>glassShades.Contains(shade),out cutVolume);
            for(int i=0;i<NX;i++)for(int j=0;j<NY;j++)for(int k=0;k<NZ;k++)
            {
                byte m=prepared?cutVolume.PreparedMaterial(i,j,k):MaterialAt(Center(i,j,k));
                if(m==0)continue;material[i,j,k]=m;cells.Add(new Vector3Int(i,j,k));
            }
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.CarGridMs,started);
#endif
        }
        public static int CellCount => cells.Count;
        static Vector3 Center(int i,int j,int k)=>new Vector3((i-39.5f)*Step,-1.125f+(j+.5f)*Step,(k-19.5f)*Step);
        static bool Has(int i,int j,int k)=>i>=0&&i<NX&&j>=0&&j<NY&&k>=0&&k<NZ&&material[i,j,k]!=0;
        static bool Box(Vector3 p,Vector3 center,Vector3 half)=>Mathf.Abs(p.x-center.x)<half.x&&Mathf.Abs(p.y-center.y)<half.y&&Mathf.Abs(p.z-center.z)<half.z;
        static bool RoundedBox(Vector3 p,Vector3 center,Vector3 half,float radius)
        {
            var q=new Vector3(Mathf.Abs(p.x-center.x),Mathf.Abs(p.y-center.y),Mathf.Abs(p.z-center.z))-half+Vector3.one*radius;
            return new Vector3(Mathf.Max(q.x,0),Mathf.Max(q.y,0),Mathf.Max(q.z,0)).magnitude+Mathf.Min(Mathf.Max(q.x,Mathf.Max(q.y,q.z)),0)<radius;
        }
        static byte MaterialAt(Vector3 p)
        {
            for(int wheel=0;wheel<2;wheel++)
            {
                float wheelX=wheel==0?-.76f:.76f;
                float r=Mathf.Sqrt((p.x-wheelX)*(p.x-wheelX)+(p.y+.77f)*(p.y+.77f));
                if(Mathf.Abs(p.z)>.30f&&Mathf.Abs(p.z)<.51f&&r<.275f&&r>.13f)return Tire;
                if(Mathf.Abs(p.z)>.31f&&Mathf.Abs(p.z)<.535f&&r<.14f)return Alloy;
                if(Mathf.Abs(p.x-wheelX)<.06f&&p.y>-.78f&&p.y<-.51f&&Mathf.Abs(p.z)<.43f)return Steel;
            }
            if(Box(p,new Vector3(.72f,-.29f,0),new Vector3(.25f,.14f,.225f)))
            {
                if(p.y>-.23f&&Mathf.Abs(p.z)<.07f)return Copper;
                return Engine;
            }
            if(Box(p,new Vector3(-.02f,-.545f,0),new Vector3(1.10f,.035f,.30f)))return Steel;
            for(int seat=0;seat<2;seat++)
            {
                float seatX=seat==0?-.39f:.04f;
                if(RoundedBox(p,new Vector3(seatX,-.365f,0),new Vector3(.12f,.08f,.23f),.035f))return Seat;
                if(RoundedBox(p,new Vector3(seatX-.095f,-.19f,0),new Vector3(.045f,.18f,.23f),.025f))return Seat;
            }
            if(Box(p,new Vector3(.365f,-.02f,0),new Vector3(.055f,.08f,.27f)))return Seat;
            if(Box(p,new Vector3(1.17f,-.19f,0),new Vector3(.035f,.055f,.28f)))return Lamp;
            if(Box(p,new Vector3(-1.185f,-.20f,0),new Vector3(.03f,.06f,.275f)))return Tail;
            if(Box(p,new Vector3(1.175f,-.42f,0),new Vector3(.04f,.035f,.30f))||Box(p,new Vector3(-1.18f,-.42f,0),new Vector3(.04f,.035f,.30f)))return Alloy;
            // A hollow passenger cabin with roof, pillars and solid tinted glass panels.
            float front=.50f-Mathf.Max(0,p.y-.10f)*.75f;
            float rear=-.66f+Mathf.Max(0,p.y+.10f)*.18f;
            bool cabin=RoundedBox(p,new Vector3(-.09f,.16f,0),new Vector3(.60f,.29f,.315f),.045f)&&p.x<front&&p.x>rear;
            if(cabin)
            {
                bool shell=Mathf.Abs(p.z)>.265f||p.y>.375f||p.x>front-.04f||p.x<rear+.04f;
                if(shell)
                {
                    bool window=p.y>-.015f&&p.y<.34f&&p.x>rear+.07f&&p.x<front-.075f&&Mathf.Abs(p.x+.08f)>.025f;
                    if(window&&Mathf.Abs(p.z)>.265f)return Glass;
                    if(p.x>front-.04f&&p.y>.015f&&p.y<.34f&&Mathf.Abs(p.z)<.255f)return Glass;
                    if(p.x<rear+.04f&&p.y>.04f&&p.y<.31f&&Mathf.Abs(p.z)<.255f)return Glass;
                    return Paint;
                }
            }
            bool body=RoundedBox(p,new Vector3(-.015f,-.31f,0),new Vector3(1.205f,.325f,.37f),.085f);
            if(body)
            {
                for(int wheel=0;wheel<2;wheel++)
                {float wheelX=wheel==0?-.76f:.76f;if(p.y<-.38f&&(p.x-wheelX)*(p.x-wheelX)+(p.y+.77f)*(p.y+.77f)<.31f*.31f)return 0;}
                bool inner=RoundedBox(p,new Vector3(-.015f,-.31f,0),new Vector3(1.168f,.288f,.333f),.07f);
                // Open the top under the cabin instead of sealing the passenger volume.
                if(!inner&&!(p.y>-.025f&&p.x>-.63f&&p.x<.49f&&Mathf.Abs(p.z)<.27f))return Paint;
            }
            // Small side handles and sill trim preserve cues at mobile size.
            if(Mathf.Abs(p.z)>.368f&&Mathf.Abs(p.z)<.401f&&p.y>-.105f&&p.y<-.072f&&p.x>-.43f&&p.x<-.28f)return Alloy;
            if(Mathf.Abs(p.z)>.365f&&Mathf.Abs(p.z)<.387f&&p.y>-.455f&&p.y<-.425f&&p.x>-.43f&&p.x<.42f)return Alloy;
            return 0;
        }
        public static MassMap Mass()
        {
            var projected=new decimal[NX,NY];decimal total=0;
            foreach(var c in cells){decimal weight=density[material[c.x,c.y,c.z]]*.03125m*.03125m*.03125m;projected[c.x,c.y]+=weight;total+=weight;}
            var list=new List<MassCell>();decimal sum=0;
            for(int i=0;i<NX;i++)for(int j=0;j<NY;j++)
            {
                if(projected[i,j]==0)continue;
                decimal x=(i-40)*.03125m,y=-1.125m+j*.03125m,m=Math.Round(100m*projected[i,j]/total,12);
                list.Add(new MassCell(x,x+.03125m,y,y+.03125m,m));sum+=m;
            }
            var last=list[list.Count-1];list[list.Count-1]=new MassCell(last.MinX,last.MaxX,last.MinY,last.MaxY,last.Mass+100m-sum);
            return new MassMap(list);
        }
        static void PrepareCutVolume()
        {
            if(cutVolume!=null)return;
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double volumeStarted=SelectionPreparationReview.Start();
#endif
            // Render only: scoring continues to use the original car density table and mass map.
            cutVolume=new VoxelVolume("L02",p=>{
                int i=Mathf.FloorToInt(p.x/Step)+40,j=Mathf.FloorToInt((p.y+1.125f)/Step),k=Mathf.FloorToInt(p.z/Step)+20;
                return Has(i,j,k)?material[i,j,k]:(byte)0;
            },(id,m,p,axis,sign,cap)=>ColorFor(m,p,axis,sign,cap),shade=>glassShades.Contains(shade));
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.CarVolumeMs,volumeStarted);double cacheStarted=SelectionPreparationReview.Start();
#endif
            var temporary=cutVolume.Build(float.PositiveInfinity,true);
            if(Application.isPlaying)UnityEngine.Object.Destroy(temporary);else UnityEngine.Object.DestroyImmediate(temporary);
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.CarCacheMeshMs,cacheStarted);
#endif

        }
        public static Mesh Build(float knifeX,bool left)
        {
            PrepareCutVolume();
            if(!float.IsPositiveInfinity(knifeX)||!left)return cutVolume.Build(knifeX,left);
            // Keep the frozen whole mesh byte-for-byte in authoring/asset checks.
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            double wholeStarted=SelectionPreparationReview.Start();
#endif
            var b=new Builder();
            foreach(var c in cells)
            {
                var p=Center(c.x,c.y,c.z);var lo=p-Vector3.one*Step*.5f;var hi=p+Vector3.one*Step*.5f;
                float originalLo=lo.x,originalHi=hi.x;
                if(left)hi.x=Mathf.Min(hi.x,knifeX);else lo.x=Mathf.Max(lo.x,knifeX);
                if(hi.x-lo.x<.0000001f)continue;
                bool capLeft=!left&&Mathf.Abs(lo.x-knifeX)<.0000001f&&(knifeX>originalLo+.0000001f||Has(c.x-1,c.y,c.z));
                bool capRight=left&&Mathf.Abs(hi.x-knifeX)<.0000001f&&(knifeX<originalHi-.0000001f||Has(c.x+1,c.y,c.z));
                byte m=material[c.x,c.y,c.z];
                if(capLeft||!Has(c.x-1,c.y,c.z))b.Face(lo,hi,0,-1,ColorFor(m,p,0,-1,capLeft),capLeft);
                if(capRight||!Has(c.x+1,c.y,c.z))b.Face(lo,hi,0,1,ColorFor(m,p,0,1,capRight),capRight);
                if(!Has(c.x,c.y-1,c.z))b.Face(lo,hi,1,-1,ColorFor(m,p,1,-1,false),false);
                if(!Has(c.x,c.y+1,c.z))b.Face(lo,hi,1,1,ColorFor(m,p,1,1,false),false);
                if(!Has(c.x,c.y,c.z-1))b.Face(lo,hi,2,-1,ColorFor(m,p,2,-1,false),false);
                if(!Has(c.x,c.y,c.z+1))b.Face(lo,hi,2,1,ColorFor(m,p,2,1,false),false);
            }
            var whole=b.Mesh(float.IsInfinity(knifeX)?"L02 voxel whole":left?"L02 voxel left":"L02 voxel right");
#if UNITY_EDITOR || (UNITY_IOS && DEVELOPMENT_BUILD && HALF_TASK5_DEVICE_REVIEW)
            SelectionPreparationReview.Finish(ref SelectionPreparationReview.CarWholeMs,wholeStarted);
#endif
            return whole;
        }
        static Color32 ColorFor(byte m,Vector3 p,int axis,int sign,bool cap)
        {
            Color color;
            switch(m)
            {
                case Paint:color=cap?new Color(.52f,.57f,.55f):new Color(.94f,.66f,.24f);break;
                case Glass:color=cap?new Color(.38f,.67f,.71f):new Color(.15f,.34f,.43f);break;
                case Tire:color=new Color(.14f,.17f,.21f);break;
                case Alloy:color=new Color(.69f,.75f,.73f);break;
                case Steel:color=new Color(.30f,.37f,.41f);break;
                case Engine:color=new Color(.35f,.39f,.40f);break;
                case Seat:color=new Color(.27f,.34f,.31f);break;
                case Lamp:color=new Color(.97f,.91f,.66f);break;
                case Tail:color=new Color(.72f,.15f,.13f);break;
                default:color=new Color(.69f,.39f,.20f);break;
            }
            float light=cap?1:axis==1?(sign>0?1.08f:.67f):axis==2?(sign<0?.96f:.74f):(sign<0?.84f:.91f);
            // Broad stepped shading follows form; no noisy photo texture or moving light.
            if(m==Paint&&!cap&&axis==2)light*=.89f+.11f*Mathf.Clamp01((p.y+.6f)/.9f);
            return color*light;
        }
        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<Color32> colors=new List<Color32>();
            readonly List<int>[] triangles={new List<int>(),new List<int>()};
            public void Face(Vector3 lo,Vector3 hi,int axis,int sign,Color32 color,bool cap)
            {
                int u=(axis+1)%3,v=(axis+2)%3,start=vertices.Count;
                var a=lo;a[axis]=sign>0?hi[axis]:lo[axis];var d=a;d[u]=hi[u];var e=d;e[v]=hi[v];var f=a;f[v]=hi[v];
                vertices.Add(a);vertices.Add(d);vertices.Add(e);vertices.Add(f);for(int i=0;i<4;i++)colors.Add(color);
                var t=triangles[cap?1:0];if(sign>0)t.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});else t.AddRange(new[]{start,start+2,start+1,start,start+3,start+2});
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.subMeshCount=2;mesh.SetTriangles(triangles[0],0);mesh.SetTriangles(triangles[1],1);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
