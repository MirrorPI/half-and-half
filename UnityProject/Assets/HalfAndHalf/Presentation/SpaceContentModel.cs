using System;
using System.Collections.Generic;
using System.Linq;
using HalfAndHalf.Core;
using UnityEngine;

namespace HalfAndHalf.Presentation
{
    // Functional content only. Reuses the trial sphere/grains; no final art or object-specific audio.
    public static class SpaceContentModel
    {
        public const string Version="space-functional-prototype-v1";
        public static readonly string[] Ids={"P01","P02","P03","P04","P05","S01","G01","G02","G03"};
        static readonly SpaceVoxel[] sphere=SphereTemplate();
        static readonly Dictionary<string,ObjectDefinition> definitions=new Dictionary<string,ObjectDefinition>();
        public static bool Supports(string id)=>Array.IndexOf(Ids,id)>=0;
        public static ObjectDefinition Definition(string id)
        {
            if(!Supports(id))throw new ArgumentException("Unknown space content: "+id);
            if(!definitions.TryGetValue(id,out var result))definitions[id]=result=new ObjectDefinition(id,Version,MassAt(id,0),"SpacePrototype/"+id,t=>MassAt(id,t));
            return result;
        }
        static SpaceVoxel[] SphereTemplate()
        {
            var old=new List<SpaceVoxel>();SpaceTrialModel.Fill(old,"P01",0);
            // Reuse the existing trial volume at a coarser functional resolution.
            var groups=old.GroupBy(v=>new Vector3Int(Mathf.FloorToInt(v.Center.x/.12f),Mathf.FloorToInt(v.Center.y/.12f),Mathf.FloorToInt(v.Center.z/.12f))).ToArray();
            var occupied=new HashSet<Vector3Int>(groups.Select(g=>g.Key));
            return groups.Select(g=>{
                int faces=0;for(int axis=0;axis<3;axis++)for(int side=0;side<2;side++){var next=g.Key;next[axis]+=side==0?-1:1;if(!occupied.Contains(next))faces|=1<<(axis*2+side);}
                return new SpaceVoxel(((Vector3)g.Key+Vector3.one*.5f)*.12f,.12f,g.First().Color,1,faces,0);
            }).ToArray();
        }
        public static MassMap MassAt(string id,long time)
        {
            var points=new List<SpaceVoxel>();Fill(points,id,time);var cells=new List<MassCell>(points.Count);
            foreach(var v in points){decimal x=(decimal)(double)v.Center.x,y=(decimal)(double)v.Center.y,h=(decimal)(double)v.Size/2;cells.Add(new MassCell(x-h,x+h,y-h,y+h,v.Mass));}
            return new MassMap(cells);
        }
        public static void Fill(List<SpaceVoxel> output,string id,long milliseconds)
        {
            output.Clear();float t=Mathf.Clamp(milliseconds,0,120000)/1000f;
            if(id=="G03"){SpaceTrialModel.Fill(output,id,milliseconds);return;}
            if(id=="G01"||id=="G02"){Galaxy(output,id,t);return;}
            if(id=="S01"){Solar(output,t);return;}
            int kind=Array.IndexOf(Ids,id);if(kind<0||kind>4)throw new ArgumentException("Unknown space content: "+id);
            decimal body=kind==3?80:kind==4?72:100;
            AddSphere(output,Vector3.zero,.69f,body,0,t*(.22f+kind*.055f),kind);
            int satellites=kind==3?4:kind==4?2:0;
            for(int i=0;i<satellites;i++){
                float angle=t*(.58f-i*.065f)+i*1.58f+.4f,r=.94f+i*.07f;
                AddSphere(output,new Vector3(Mathf.Cos(angle)*r,Mathf.Sin(angle)*(.26f+i*.10f),Mathf.Sin(angle)*.4f),.09f,5,i+1,t*.2f,0);
            }
            if(kind==4)Ring(output,t,18,3);
        }
        static void AddSphere(List<SpaceVoxel> output,Vector3 position,float radius,decimal weight,int part,float rotation,int kind)
        {
            float c=Mathf.Cos(rotation),s=Mathf.Sin(rotation);var densities=new decimal[sphere.Length];decimal total=0,used=0;
            for(int i=0;i<sphere.Length;i++){
                var p=sphere[i].Center;var local=new Vector3(p.x*c+p.z*s,p.y,-p.x*s+p.z*c);
                double a=Math.Exp(-(local-new Vector3(.36f,.10f,-.32f)).sqrMagnitude/.065f);
                double b=Math.Exp(-(local-new Vector3(-.22f,-.25f,.34f)).sqrMagnitude/.045f);
                double mountain=kind==1?Math.Exp(-(local-new Vector3(.12f,.35f,.24f)).sqrMagnitude/.05f):0;
                densities[i]=(decimal)Math.Round(1+(kind==2?4:3)*a+1.5*b+2*mountain,8,MidpointRounding.AwayFromZero);total+=densities[i];
            }
            for(int i=0;i<sphere.Length;i++){
                var v=sphere[i];decimal mass=i==sphere.Length-1?weight-used:decimal.Round(weight*densities[i]/total,12);used+=mass;
                Color tint=kind==1?new Color(.3f,.66f,.84f):kind==2?new Color(.78f,.42f,.3f):kind==3?new Color(.85f,.69f,.5f):kind==4?new Color(.83f,.76f,.6f):Color.gray;
                float shade=.95f-.14f*(float)(densities[i]-1);Color color=tint*Mathf.Clamp(shade,.4f,1);
                output.Add(new SpaceVoxel(position+v.Center*(radius/.69f),v.Size*(radius/.69f),color,mass,v.Faces,part));
            }
        }
        static void Ring(List<SpaceVoxel> output,float t,decimal weight,int part)
        {
            const int count=120;decimal total=0,used=0;var weights=new decimal[count];
            for(int i=0;i<count;i++){weights[i]=1+(decimal)(.55f*Mathf.Cos(i*Mathf.PI*2/count));total+=weights[i];}
            for(int i=0;i<count;i++){
                float a=i*Mathf.PI*2/count+t*.28f,r=.90f+.06f*(i%3);
                var p=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.28f,Mathf.Sin(a)*r*.6f);
                decimal mass=i==count-1?weight-used:decimal.Round(weight*weights[i]/total,12);used+=mass;
                output.Add(new SpaceVoxel(p,.065f,new Color(.73f,.66f,.52f),mass,63,part));
            }
        }
        static void Solar(List<SpaceVoxel> output,float t)
        {
            AddSphere(output,new Vector3(-.06f,.04f,0),.16f,25,0,t*.2f,4);
            decimal[] weights={2,4,8,5,17,14,8,7};var positions=new Vector3[8];
            for(int i=0;i<8;i++){
                float a=i*.84f+.3f+t*(.7f-i*.065f)*(i%2==0?1:-1),r=.28f+i*.105f;
                positions[i]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.73f,0);
                AddSphere(output,positions[i],i==4||i==5?.10f:.06f,weights[i],i+1,t*(.3f+i*.04f),i==2?1:i==3?2:3);
            }
            int[] parents={2,4,4,4,4,5,5};decimal[] moonWeights={2,2,1.5m,1,1.5m,1,1};
            for(int i=0;i<7;i++){
                float a=i*1.7f+t*(1.2f-i*.05f);var p=positions[parents[i]]+new Vector3(Mathf.Cos(a)*.12f,Mathf.Sin(a)*.10f,-.03f);
                AddSphere(output,p,.028f,moonWeights[i],9+i,t*.3f,0);
            }
        }
        static void Galaxy(List<SpaceVoxel> output,string id,float t)
        {
            var grains=SpaceTrialModel.GalaxyGrains(1);decimal total=0,used=0;var weights=new decimal[grains.Count];
            for(int i=0;i<grains.Count;i++){
                var p=grains[i].Center;weights[i]=p.sqrMagnitude<.03f?4:p.x>.22f?2.8m:1;total+=weights[i];
            }
            for(int i=0;i<grains.Count;i++){
                var v=grains[i];float expansion=id=="G02"?Mathf.Clamp01(t/8):0;
                float rate=v.Center.x>.22f?.65f:.18f;var p=v.Center*(1+expansion*rate);
                float a=t*.24f*v.OrbitSpeed;float c=Mathf.Cos(a),s=Mathf.Sin(a);
                p=new Vector3((p.x*c-p.y*s)*1.2f,(p.x*s+p.y*c)*1.2f,p.z);
                decimal mass=i==grains.Count-1?100-used:decimal.Round(100*weights[i]/total,12);used+=mass;
                output.Add(new SpaceVoxel(p,v.Size,v.Color,mass,63,0,v.OrbitSpeed));
            }
        }
    }
}
