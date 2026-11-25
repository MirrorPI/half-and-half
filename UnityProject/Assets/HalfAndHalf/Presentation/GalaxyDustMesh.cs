using System.Collections.Generic;
using UnityEngine;
namespace HalfAndHalf.Presentation
{
 public static class GalaxyDustMesh
 {
  public static void Build(Mesh mesh)
  {
   var vertices=new List<Vector3>(28000);var colors=new List<Color32>(28000);var offsets=new List<Vector2>(28000);var groups=new List<Vector2>(28000);var triangles=new List<int>(42000);
   for(int group=0;group<2;group++)foreach(var cell in SpaceTrialModel.GalaxyGrains(group))
   {
    int n=vertices.Count;float h=cell.Size*.5f;
    for(int j=0;j<4;j++){vertices.Add(cell.Center);colors.Add(cell.Color);groups.Add(new Vector2(group,cell.OrbitSpeed<1?1:cell.OrbitSpeed>1?2:0));}
    offsets.Add(new Vector2(-h,-h));offsets.Add(new Vector2(-h,h));offsets.Add(new Vector2(h,h));offsets.Add(new Vector2(h,-h));
    triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
   }
   mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,offsets);mesh.SetUVs(1,groups);mesh.SetTriangles(triangles,0);
   // Shader moves the grain centers; include the full bounded orbit in culling bounds.
   mesh.bounds=new Bounds(Vector3.zero,new Vector3(3.5f,3,1));
  }
 }
}
