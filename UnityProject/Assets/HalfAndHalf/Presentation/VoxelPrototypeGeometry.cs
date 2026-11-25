using System;
using System.Collections.Generic;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // Task5 authored material volumes. These densities are puzzle values, not measured physics.
    public static class VoxelPrototypeGeometry
    {
        public static readonly string[] Ids={"L03","L04","L05","N01","N02","N04","N05"};
        static readonly Dictionary<string,VoxelVolume> models=new Dictionary<string,VoxelVolume>();
        public static string Version(string id)=>id+"-voxel-prototype-v1";
        public static VoxelVolume For(string id)
        {
            if(Array.IndexOf(Ids,id)<0)throw new ArgumentException("Unknown Task5 voxel object: "+id);
            if(!models.TryGetValue(id,out var model)){if(!VoxelPreparationCache.TryLoad(id,Color,null,out model))model=new VoxelVolume(id,p=>Material(id,p),Color);models.Add(id,model);}return model;
        }
        const byte Paint=1,Glass=2,Tire=3,Alloy=4,Steel=5,Dense=6,Seat=7,Ice=8,Rock=9,Soil=10,Landscape=11,Lava=12,Sand=13,Copper=14,Rebar=15,Snow=16,Water=17;
        internal static readonly decimal[] Densities={0,1.3m,.35m,1.1m,2.4m,2.2m,6m,.3m,.92m,2.2m,1.1m,0m,2.8m,1.35m,1.6m,5.5m,.55m,0m};
        static bool Box(Vector3 p,Vector3 c,Vector3 h)=>Mathf.Abs(p.x-c.x)<h.x&&Mathf.Abs(p.y-c.y)<h.y&&Mathf.Abs(p.z-c.z)<h.z;
        static bool Rounded(Vector3 p,Vector3 c,Vector3 h,float r)
        {
            var q=new Vector3(Mathf.Abs(p.x-c.x),Mathf.Abs(p.y-c.y),Mathf.Abs(p.z-c.z))-h+Vector3.one*r;
            return new Vector3(Mathf.Max(q.x,0),Mathf.Max(q.y,0),Mathf.Max(q.z,0)).magnitude+Mathf.Min(Mathf.Max(q.x,Mathf.Max(q.y,q.z)),0)<r;
        }
        static bool Segment(Vector3 p,Vector3 a,Vector3 b,float r){var d=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude);return (p-a-d*t).sqrMagnitude<r*r;}
        static byte Material(string id,Vector3 p)
        {
            switch(id){case "L03":return Bus(p);case "L04":return Excavator(p);case "L05":return Building(p);case "N01":return Mountain(p);case "N02":return Glacier(p);case "N04":return Volcano(p);default:return Island(p);}
        }
        static byte Bus(Vector3 p)
        {
            for(int i=0;i<2;i++)
            {
                float x=i==0?-.82f:.82f,r=Mathf.Sqrt((p.x-x)*(p.x-x)+(p.y+.75f)*(p.y+.75f));
                if(Mathf.Abs(p.z)>.29f&&Mathf.Abs(p.z)<.50f&&r<.235f)return r<.12f?Alloy:Tire;
            }
            if(Box(p,new Vector3(-.88f,-.40f,0),new Vector3(.19f,.12f,.23f)))return Dense;
            if(Box(p,new Vector3(0,-.56f,0),new Vector3(1.16f,.035f,.29f)))return Steel;
            for(int i=0;i<4;i++)
            {
                float x=-.58f+i*.36f;
                if(Box(p,new Vector3(x,-.28f,0),new Vector3(.10f,.065f,.23f))||Box(p,new Vector3(x-.075f,-.10f,0),new Vector3(.035f,.20f,.23f)))return Seat;
            }
            if(!Rounded(p,new Vector3(0,0,0),new Vector3(1.22f,.64f,.35f),.055f))return 0;
            for(int i=0;i<2;i++){float x=i==0?-.82f:.82f;if(p.y<-.47f&&(p.x-x)*(p.x-x)+(p.y+.75f)*(p.y+.75f)<.265f*.265f)return 0;}
            if(Rounded(p,Vector3.zero,new Vector3(1.174f,.586f,.304f),.035f))return 0;
            bool window=p.y>.12f&&p.y<.49f&&((Mathf.Abs(p.z)>.30f&&Mathf.Repeat(p.x+1.10f,.35f)>.065f)||(Mathf.Abs(p.x)>1.17f&&Mathf.Abs(p.z)<.28f));
            return window?Glass:Paint;
        }
        static byte Excavator(Vector3 p)
        {
            if(Rounded(p,new Vector3(-.76f,-.23f,0),new Vector3(.26f,.22f,.30f),.075f))return Dense;
            if(Rounded(p,new Vector3(1.08f,-.38f,0),new Vector3(.16f,.19f,.28f),.05f))return p.y>-.33f&&p.x<1.13f&&Mathf.Abs(p.z)<.21f?(byte)0:Dense;
            for(int s=-1;s<=1;s+=2)if(Rounded(p,new Vector3(-.20f,-.83f,s*.34f),new Vector3(.72f,.16f,.105f),.09f))return Mathf.Repeat(p.x+.92f,.125f)<.031f&&p.y<-.86f?Steel:Tire;
            if(Box(p,new Vector3(-.20f,-.45f,0),new Vector3(.65f,.09f,.30f)))return Steel;
            if(Rounded(p,new Vector3(-.22f,.19f,0),new Vector3(.27f,.45f,.28f),.04f))
            {
                if(Box(p,new Vector3(-.22f,-.12f,0),new Vector3(.13f,.08f,.18f)))return Seat;
                if(Rounded(p,new Vector3(-.22f,.19f,0),new Vector3(.22f,.398f,.23f),.025f))return 0;
                return p.y>.02f&&p.y<.51f&&Mathf.Abs(p.z)>.235f?Glass:Paint;
            }
            var a=new Vector3(-.03f,.49f,0);var b=new Vector3(.48f,.85f,0);var c=new Vector3(1.07f,-.15f,0);
            if(Segment(p,a,b,.072f)||Segment(p,b,c,.060f))return Paint;
            for(int s=-1;s<=1;s+=2){var offset=new Vector3(0,-.04f,s*.12f);if(Segment(p,a+offset,b+offset,.026f)||Segment(p,b+offset,c+offset,.025f))return Alloy;}
            if((p-b).sqrMagnitude<.105f*.105f||(p-c).sqrMagnitude<.09f*.09f)return Alloy;
            return 0;
        }
        static byte Building(Vector3 p)
        {
            bool main=Rounded(p,new Vector3(-.28f,-.03f,0),new Vector3(.51f,1.045f,.35f),.035f);
            bool annex=Rounded(p,new Vector3(.75f,-.63f,0),new Vector3(.39f,.43f,.39f),.035f);
            if(main)
            {
                if(Mathf.Abs(p.x+.42f)<.055f)return Rebar;
                for(int i=0;i<5;i++)if(Mathf.Abs(p.y-(-.75f+i*.445f))<.029f)return Steel;
                bool shell=!Box(p,new Vector3(-.28f,-.03f,0),new Vector3(.465f,1.0f,.305f));
                if(!shell)return 0;
                bool window=Mathf.Abs(p.z)>.31f&&Mathf.Repeat(p.y+.83f,.445f)>.12f&&Mathf.Repeat(p.y+.83f,.445f)<.34f&&Mathf.Repeat(p.x+.75f,.235f)>.065f;
                return window?Glass:Paint;
            }
            if(annex)
            {
                if(Mathf.Abs(p.x-.95f)<.04f)return Rebar;
                if(Mathf.Abs(p.y+.80f)<.03f||Mathf.Abs(p.y+.24f)<.03f)return Steel;
                if(Box(p,new Vector3(.75f,-.63f,0),new Vector3(.345f,.385f,.345f)))return 0;
                return p.y>-.67f&&p.y<-.39f&&Mathf.Abs(p.z)>.345f?Glass:Paint;
            }
            if(Box(p,new Vector3(.29f,-.82f,0),new Vector3(.09f,.12f,.20f)))return Rock;
            return 0;
        }
        static byte Mountain(Vector3 p)
        {
            if(p.x< -1.18f||p.x>1.16f||Mathf.Abs(p.z)>.40f||p.y<-.94f)return 0;
            float ridge=Mathf.Max(1-Mathf.Abs(p.x+.32f)/.85f,.64f*(1-Mathf.Abs(p.x-.65f)/.48f));
            float height=-.30f+1.35f*ridge-.65f*Mathf.Abs(p.z);
            if(p.y>height)return 0;
            if(height>.73f&&p.y>height-.07f)return Snow;
            if(p.x<-.30f&&p.y<height-.035f)return Dense;
            return p.y>height-.05f?Soil:Rock;
        }
        static byte Glacier(Vector3 p)
        {
            if(Mathf.Abs(p.x)>1.045f&&Mathf.Abs(p.x)<1.24f&&Mathf.Abs(p.z)<.49f&&p.y>-.70f&&p.y<(p.x<0?.63f:.43f)-.3f*Mathf.Abs(p.z))return Landscape;
            float surface=.27f+.19f*(-p.x+.1f)+.08f*Mathf.Sin(p.x*5f),width=.36f+.055f*Mathf.Cos(p.x*3f);
            if(p.x< -1.02f||p.x>.99f||Mathf.Abs(p.z)>width||p.y<-.66f+.10f*p.x||p.y>surface)return 0;
            if(p.y>surface-.09f&&Mathf.Abs(Mathf.Repeat(p.x+.12f,.32f)-.12f)<.019f)return 0;
            if(Box(p,new Vector3(-.48f,-.42f,0),new Vector3(.30f,.11f,.28f)))return Dense;
            if(p.x<-.36f&&p.x>-.67f&&p.y<-.31f)return Rock;
            return Ice;
        }
        static byte Volcano(Vector3 p)
        {
            float radial=Mathf.Sqrt(p.x*p.x/(1.06f*1.06f)+p.z*p.z/(.55f*.55f));float height=1.02f-1.62f*radial;
            if(radial>1||p.y<-.94f||p.y>height)return 0;
            float crater=Mathf.Sqrt(p.x*p.x+p.z*p.z);
            if(crater<.19f&&p.y>0)return 0;
            if((crater>.19f&&crater<.24f&&p.y>.60f)||(Mathf.Abs(p.x-.25f)<.06f&&p.z<-.02f&&p.y>-.55f&&p.y>height-.075f))return Lava;
            if(Box(p,new Vector3(.45f,-.56f,0),new Vector3(.25f,.17f,.29f))||(p.x>.25f&&p.y<height-.04f))return Dense;
            return Rock;
        }
        static byte Island(Vector3 p)
        {
            float width=.44f+.045f*Mathf.Cos(p.x*3f),r=(p.x+.15f)*(p.x+.15f)/(1.10f*1.10f)+p.z*p.z/(width*width);
            float top=.21f+.10f*Mathf.Cos(p.x*3f)+.06f*Mathf.Sin(p.z*4f);
            if(r<(p.y<-.25f?1.12f:1f)&&p.y>-.94f&&p.y<top)
            {
                if(p.y<-.03f)return p.x<-.40f||p.y<-.65f?Dense:Rock;
                if(r>.78f)return Sand;
                return Soil;
            }
            if(Mathf.Abs(p.x)<1.24f&&Mathf.Abs(p.z)<.57f&&p.y>-.063f&&p.y<-.031f)return Water;
            return 0;
        }
        static Color32 Color(string id,byte material,Vector3 p,int axis,int sign,bool cap)
        {
            Color c;
            switch(material)
            {
                case Paint:c=cap?new Color(.52f,.57f,.55f):id=="L03"?new Color(.27f,.65f,.65f):id=="L04"?new Color(.94f,.66f,.22f):new Color(.76f,.74f,.61f);break;
                case Glass:c=cap?new Color(.43f,.68f,.70f):new Color(.16f,.34f,.42f);break;
                case Tire:c=new Color(.14f,.17f,.20f);break;
                case Alloy:c=new Color(.70f,.75f,.73f);break;
                case Steel:c=new Color(.30f,.37f,.41f);break;
                case Dense:c=id.StartsWith("L")?new Color(.36f,.40f,.40f):id=="N05"?new Color(.27f,.42f,.48f):new Color(.39f,.42f,.38f);break;
                case Seat:c=new Color(.26f,.36f,.32f);break;
                case Ice:c=cap?new Color(.62f,.83f,.87f):new Color(.78f,.91f,.91f);break;
                case Soil:c=cap?new Color(.48f,.36f,.25f):new Color(.43f,.58f,.32f);break;
                case Landscape:c=new Color(.49f,.43f,.34f);break;
                case Lava:c=new Color(.93f,.41f,.17f);break;
                case Sand:c=new Color(.85f,.77f,.54f);break;
                case Copper:c=new Color(.69f,.39f,.20f);break;
                case Rebar:c=cap?new Color(.29f,.36f,.39f):new Color(.41f,.48f,.46f);break;
                case Snow:c=new Color(.91f,.93f,.87f);break;
                case Water:c=new Color(.43f,.71f,.75f);break;
                default:c=id=="N04"?new Color(.43f,.40f,.36f):new Color(.61f,.53f,.41f);if(cap&&((int)((p.y+1.125f)/.1875f)&1)==0)c*=.84f;break;
            }
            float light=cap?1:axis==1?(sign>0?1.08f:.68f):axis==2?(sign<0?.96f:.75f):(sign<0?.84f:.91f);return c*light;
        }
    }
}
