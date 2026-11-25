using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace HalfAndHalf.Presentation
{
 public sealed class SpaceVoxelMesh
 {
  readonly List<Vector3> vertices=new List<Vector3>(70000);
  readonly List<Color32> colors=new List<Color32>(70000);
  readonly List<int> outside=new List<int>(110000),cap=new List<int>(18000);
  public float SectionTop {get;private set;}
  public void Fill(Mesh mesh,IReadOnlyList<SpaceVoxel> cells,float knife,bool left,bool galaxy,bool moon=false)
  {
   vertices.Clear();colors.Clear();outside.Clear();cap.Clear();SectionTop=float.NegativeInfinity;
   foreach(var cell in cells)
   {
    if(cell.Faces==0&&float.IsInfinity(knife))continue;
    var lo=cell.Center-Vector3.one*(cell.Size*.5f);var hi=cell.Center+Vector3.one*(cell.Size*.5f);
    float oldLo=lo.x,oldHi=hi.x;
    if(left)hi.x=Mathf.Min(hi.x,knife);else lo.x=Mathf.Max(lo.x,knife);
    if(hi.x-lo.x<.000001f)continue;
    bool capped=knife>oldLo+.000001f&&knife<oldHi-.000001f;
    // Grid-aligned knife planes also expose previously internal faces.
    bool negativeCap=!left&&Mathf.Abs(lo.x-knife)<.000001f&&(capped||(cell.Faces&1)==0);
    bool positiveCap=left&&Mathf.Abs(hi.x-knife)<.000001f&&(capped||(cell.Faces&2)==0);
    if(galaxy)
    {
     lo.z=hi.z=cell.Center.z;Face(lo,hi,2,-1,cell.Color,false);
     if(knife>=oldLo&&knife<=oldHi)SectionTop=Mathf.Max(SectionTop,hi.y);
     continue;
    }
    for(int axis=0;axis<3;axis++)for(int side=0;side<2;side++)
    {
     bool isCap=axis==0&&(side==0?negativeCap:positiveCap);
     if(!isCap&&(cell.Faces&(1<<(axis*2+side)))==0)continue;
     var color=isCap?(moon?(Color32)Color.Lerp(cell.Color,Color.white,.35f):SpaceTrialModel.Section(cell)):cell.Color;
     Face(lo,hi,axis,side==0?-1:1,color,isCap);
     if(isCap)SectionTop=Mathf.Max(SectionTop,hi.y);
    }
   }
   mesh.Clear();mesh.indexFormat=IndexFormat.UInt32;mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.subMeshCount=2;mesh.SetTriangles(outside,0);mesh.SetTriangles(cap,1);mesh.RecalculateBounds();
  }
  void Face(Vector3 lo,Vector3 hi,int axis,int sign,Color32 color,bool section)
  {
   int u=(axis+1)%3,v=(axis+2)%3,n=vertices.Count;
   var a=lo;a[axis]=sign>0?hi[axis]:lo[axis];var b=a;b[u]=hi[u];var c=b;c[v]=hi[v];var d=a;d[v]=hi[v];
   vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);for(int i=0;i<4;i++)colors.Add(color);
   var t=section?cap:outside;t.Add(n);t.Add(n+(sign>0?1:2));t.Add(n+(sign>0?2:1));t.Add(n);t.Add(n+(sign>0?2:3));t.Add(n+(sign>0?3:2));
  }
 }
}
