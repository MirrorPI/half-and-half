using System;
using System.Collections.Generic;
using HalfAndHalf.Core;
using UnityEngine;
namespace HalfAndHalf.Presentation
{
 public readonly struct SpaceVoxel
 {
  public readonly Vector3 Center;
  public readonly float Size,OrbitSpeed;
  public readonly Color32 Color;
  public readonly decimal Mass;
  public readonly int Faces,Part;
  public SpaceVoxel(Vector3 p,float size,Color32 c,decimal mass,int faces,int part,float orbitSpeed=1){OrbitSpeed=orbitSpeed;Center=p;Size=size;Color=c;Mass=mass;Faces=faces;Part=part;}
 }
 // Versioned trial motion: time comes exclusively from READY/CUT elapsed milliseconds.
 public static class SpaceTrialModel
 {
  public const string Version="space-pixel-trial-v5-projection";
  static readonly List<SpaceVoxel> planet=Sphere(.69f,.04f,80,0);
  static readonly List<SpaceVoxel> moon=Sphere(.69f,.04f,100,0);
  static readonly List<SpaceVoxel>[] moons={Sphere(.115f,.035f,5,1),Sphere(.10f,.03f,5,2),Sphere(.13f,.035f,5,3),Sphere(.09f,.03f,5,4)};
  static readonly List<SpaceVoxel>[] galaxies={Galaxy(113,38,0),Galaxy(781,62,1)};
  public static ObjectDefinition Definition(string id)=>new ObjectDefinition(id,id=="P01"?"task6-moon-trial-v1":Version,MassAt(id,0),"SpacePixelTrial/"+id,t=>MassAt(id,t));
  public static MassMap MassAt(string id,long ms)
  {
   var cells=new List<SpaceVoxel>();Fill(cells,id,ms);var map=new List<MassCell>(cells.Count);
   // Keep the displayed float geometry's precision. Five-place rounding moved fine grain edges.
   foreach(var v in cells)if(v.Mass>0){decimal x=(decimal)(double)v.Center.x,y=(decimal)(double)v.Center.y,h=(decimal)(double)v.Size*.5m;map.Add(new MassCell(x-h,x+h,y-h,y+h,v.Mass));}
   return new MassMap(map);
  }
  static decimal D(float f)=>(decimal)Math.Round(f,5,MidpointRounding.AwayFromZero);
  static Vector3 Q(Vector3 p)=>new Vector3((float)D(p.x),(float)D(p.y),(float)D(p.z));
  public static void Fill(List<SpaceVoxel> output,string id,long milliseconds)
  {
   output.Clear();float t=Mathf.Clamp(milliseconds,0,120000)/1000f;
   if(id=="P01")
   {
    // Conservative resampling of a rotating dense rock region on the existing sphere grid.
    // Surface darkness and mass use the same region; this is a functional prototype, not final art.
    float angle=t*.22f,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
    var denseCenter=new Vector3(.36f*c-.35f*s,.08f,-.36f*s-.35f*c);
    decimal total=0,used=0;
    foreach(var v in moon)total+=MoonDensity(v.Center,denseCenter);
    for(int i=0;i<moon.Count;i++)
    {
     var v=moon[i];decimal density=MoonDensity(v.Center,denseCenter);
     decimal mass=i==moon.Count-1?100m-used:decimal.Round(100m*density/total,12);used+=mass;
     float shade=.76f-.32f*(float)((density-1)/4);
     var local=new Vector3(v.Center.x*c-v.Center.z*s,v.Center.y,v.Center.x*s+v.Center.z*c);
     float crater=Mathf.Sin(local.x*19+local.z*11)*Mathf.Cos(local.y*17)>.75f?.8f:1;
     float light=Mathf.Clamp01(Vector3.Dot(v.Center.normalized,new Vector3(-.4f,.6f,-.8f).normalized)*.45f+.6f);
     float grey=shade*crater*(.58f+.42f*Mathf.Round(light*7)/7);
     output.Add(new SpaceVoxel(v.Center,v.Size,new Color(grey,grey,grey),mass,v.Faces,0));
    }
   }
   else if(id=="P04")
   {
    foreach(var v in planet)output.Add(new SpaceVoxel(v.Center,v.Size,v.Faces==0?(Color32)Color.white:Jupiter(v.Center,t),v.Mass,v.Faces,0));
    for(int i=0;i<4;i++)
    {
     float angle=t*(.58f-i*.065f)+i*1.58f+.4f;float radius=.92f+i*.08f;
     var p=Q(new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*(.26f+i*.10f),Mathf.Sin(angle)*.78f));
     foreach(var v in moons[i])output.Add(new SpaceVoxel(Q(v.Center+p),v.Size,Moon(v.Center,i),v.Mass,v.Faces,i+1));
    }
   }
   else if(id=="G03")
   {
    for(int g=0;g<2;g++)
    {
     GalaxyPose(g,milliseconds,out var center,out var motion);
     GalaxyPose(g,milliseconds,out _,out var slow,.55f);GalaxyPose(g,milliseconds,out _,out var fast,1.65f);
     foreach(var v in galaxies[g])
     {
      var p=GalaxyPosition(v.Center,center,v.OrbitSpeed<1?slow:v.OrbitSpeed>1?fast:motion);
      output.Add(new SpaceVoxel(p,v.Size,v.Color,v.Mass,v.Faces,g,v.OrbitSpeed));
     }
    }
   }
   else throw new ArgumentException("Trial supports P01, P04 and G03 only",nameof(id));
  }
  static decimal MoonDensity(Vector3 point,Vector3 center)=>(decimal)Math.Round(1+4*Math.Exp(-(double)(point-center).sqrMagnitude/.075),8,MidpointRounding.AwayFromZero);
  static List<SpaceVoxel> Sphere(float radius,float step,decimal weight,int part)
  {
   int n=Mathf.CeilToInt(radius/step);var points=new List<Vector3Int>();var set=new HashSet<Vector3Int>();
   for(int x=-n;x<n;x++)for(int y=-n;y<n;y++)for(int z=-n;z<n;z++)
   {var p=new Vector3((x+.5f)*step,(y+.5f)*step,(z+.5f)*step);if(p.sqrMagnitude<radius*radius){var c=new Vector3Int(x,y,z);points.Add(c);set.Add(c);}}
   var cells=new List<SpaceVoxel>();decimal unit=decimal.Round(weight/points.Count,12),used=0;int index=0;
   foreach(var c in points)
   {
    int faces=0;for(int axis=0;axis<3;axis++)for(int side=0;side<2;side++){var d=c;d[axis]+=side==0?-1:1;if(!set.Contains(d))faces|=1<<(axis*2+side);}
    decimal mass=++index==points.Count?weight-used:unit;used+=mass;
    cells.Add(new SpaceVoxel(new Vector3((c.x+.5f)*step,(c.y+.5f)*step,(c.z+.5f)*step),step,Color.white,mass,faces,part));
   }
   return cells;
  }
  static readonly Color[] JupiterPalette={new Color(.84f,.52f,.32f),new Color(.94f,.73f,.48f),new Color(.97f,.86f,.67f),new Color(.90f,.67f,.43f),new Color(.70f,.37f,.26f),new Color(.96f,.81f,.58f)};
  static readonly Color[] MoonPalette={new Color(.83f,.71f,.44f),new Color(.76f,.87f,.90f),new Color(.65f,.72f,.84f),new Color(.63f,.57f,.52f)};
  static Color32 Jupiter(Vector3 p,float t)
  {
   float wave=p.y+Mathf.Sin(Mathf.Atan2(p.z,p.x)*4+t*.42f)*.022f;
   int band=Mathf.Abs(Mathf.FloorToInt((wave+.8f)*13))%6;

   var color=JupiterPalette[band];float longitude=Mathf.Atan2(p.x,-p.z)-t*.25f;
   float spot=Mathf.DeltaAngle(longitude*Mathf.Rad2Deg,22)*Mathf.Deg2Rad;
   if(spot*spot/.16f+(p.y+.18f)*(p.y+.18f)/.008f<1)color=new Color(.68f,.29f,.22f);
   float light=Mathf.Clamp01(Vector3.Dot(p.normalized,new Vector3(-.4f,.6f,-.8f).normalized)*.45f+.6f);
   color*=.58f+.42f*(Mathf.Round(light*7)/7);return color;
  }
  static Color32 Moon(Vector3 p,int i)
  {

   float shade=Mathf.Clamp01(Vector3.Dot(p.normalized,new Vector3(-.4f,.6f,-.8f).normalized)*.5f+.5f);
   float crater=Mathf.Sin(p.x*73+p.z*59+i)*Mathf.Cos(p.y*61)> .63f?.76f:1;
   return MoonPalette[i]*(.52f+.48f*Mathf.Round(shade*5)/5)*crater;
  }
  public static IReadOnlyList<SpaceVoxel> GalaxyGrains(int part)=>galaxies[part];
  // The CPU mass/section and GPU renderer receive identical precomputed pose coefficients.
  public static void GalaxyPose(int part,long milliseconds,out Vector4 center,out Vector4 motion,float speed=1)
  {
   float t=Mathf.Clamp(milliseconds,0,120000)/1000f;
   float approach=Mathf.SmoothStep(0,1,Mathf.Min(t/6,1));float angle=t*(part==0?.24f:-.19f)*speed;
   center=new Vector4((part==0?-1:1)*Mathf.Lerp(.78f,.25f,approach),(part==0?1:-1)*Mathf.Lerp(.24f,.08f,approach),part==0?-.04f:.04f,0);
   motion=new Vector4(Mathf.Cos(angle),Mathf.Sin(angle),1+.12f*approach,1-.12f*approach);
  }
  public static Vector3 GalaxyPosition(Vector3 p,Vector4 center,Vector4 motion)=>new Vector3(center.x+(p.x*motion.x-p.y*motion.y)*motion.z,center.y+(p.x*motion.y+p.y*motion.x)*motion.w,center.z+p.z);
  static List<SpaceVoxel> Galaxy(int seed,decimal weight,int part)
  {
   var random=new System.Random(seed);var cells=new List<SpaceVoxel>();int count=3500;decimal unit=decimal.Round(weight/count,12),used=0;
   for(int i=0;i<count;i++)
   {
    // Broad dusty arms, dense granular core, and a sparse surrounding halo.
    bool coreGrain=i<650,halo=i>=2800;
    float u=(float)random.NextDouble(),r=coreGrain?Mathf.Sqrt(u)*.16f:.11f+Mathf.Pow(u,.7f)*(halo?.66f:.54f);
    int arm=i%4;float angle=arm*Mathf.PI*.5f+r*8.5f+((float)random.NextDouble()-.5f)*(coreGrain||halo?6.28f:1.05f);
    float x=Mathf.Cos(angle)*r,y=Mathf.Sin(angle)*r*.67f,z=((float)random.NextDouble()-.5f)*.13f;
    // Reflect the left spiral locally; retain its existing rotation and approach.
    if(part==0)x=-x;
    float size=(halo?.006f:.008f)+(float)random.NextDouble()*(halo?.005f:.010f);
    Color core=new Color(1,.92f,.74f),edge=part==0?new Color(.34f,.73f,.98f):new Color(.89f,.43f,.79f);
    var color=Color.Lerp(core,edge,Mathf.Clamp01(r/.42f));color*=halo?.32f+(float)random.NextDouble()*.4f:.56f+(float)random.NextDouble()*.44f;
    decimal mass=i==count-1?weight-used:unit;used+=mass;cells.Add(new SpaceVoxel(new Vector3(x,y,z),size,color,mass,63,part,i%16==0?.55f:i%16==1?1.65f:1));
   }
   return cells;
  }
  public static Color32 Section(SpaceVoxel cell)
  {
   if(cell.Part>0)return Color.Lerp(cell.Color,new Color(.91f,.82f,.67f),.45f);
   float r=Mathf.Sqrt(cell.Center.y*cell.Center.y+cell.Center.z*cell.Center.z);
   if(r<.18f)return new Color(1,.82f,.43f);
   if(r<.36f)return new Color(.89f,.44f,.22f);
   if(r<.53f)return new Color(.99f,.67f,.35f);
   return new Color(.98f,.85f,.63f);
  }
 }
}
