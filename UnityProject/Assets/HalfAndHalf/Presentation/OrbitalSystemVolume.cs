using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
    // Immutable authored bodies. Orbital positions are evaluated from the same READY clock
    // used by score replay; no transform or elapsed frame history enters the mass map.
    public sealed class OrbitalSystemVolume
    {
        public sealed class Body
        {
            public readonly string Name;
            public readonly int Parent;
            public readonly decimal Weight;
            public readonly PixelPlanetVolume Volume;
            public readonly float Radius;
            readonly float height,depth,slope,phase,rate;
            readonly Vector3 offset;
            public Body(BinaryReader r,bool solarLimbDarkening)
            {
                Name=Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));Parent=r.ReadInt32();Weight=(decimal)r.ReadSingle();
                Radius=r.ReadSingle();height=r.ReadSingle();depth=r.ReadSingle();slope=r.ReadSingle();
                phase=r.ReadSingle();rate=r.ReadSingle();offset=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                Volume=new PixelPlanetVolume(r.ReadBytes(r.ReadInt32()),solarLimbDarkening && Name=="Sun");
            }
            public Vector3 OnPath(float radians)
            {
                float x=Radius*Mathf.Cos(radians),s=Mathf.Sin(radians);
                return offset+new Vector3(x,height*s+slope*x,depth*s);
            }
            public Vector3 LocalPosition(long ms)=>OnPath((phase+rate*(ms*.001f))*Mathf.Deg2Rad);
        }
        public readonly Body[] Bodies;
        readonly Vector3[] centers;
        public OrbitalSystemVolume(byte[] data,bool solarLimbDarkening=false)
        {
            using var r=new BinaryReader(new MemoryStream(data));
            if(r.ReadUInt32()!=0x534F4848)throw new InvalidDataException("Invalid orbital volume");
            Bodies=new Body[r.ReadByte()];centers=new Vector3[Bodies.Length];
            decimal sum=0;
            for(int i=0;i<Bodies.Length;i++){
                var b=Bodies[i]=new Body(r,solarLimbDarkening);sum+=b.Weight;
                if(b.Parent>=i||b.Parent< -1||b.Weight<=0)throw new InvalidDataException("Invalid orbital body");
            }
            if(sum!=100 || r.BaseStream.Position!=data.Length)throw new InvalidDataException("Invalid orbital mass budget");
        }
        void Locate(long ms)
        {
            for(int i=0;i<Bodies.Length;i++){
                var b=Bodies[i];centers[i]=b.LocalPosition(ms)+(b.Parent<0?Vector3.zero:centers[b.Parent]);
            }
        }
        public void Pose(Transform root,long ms)
        {
            Locate(ms);
            for(int i=0;i<Bodies.Length;i++){
                var child=root.GetChild(i);child.localPosition=centers[i];child.localRotation=Bodies[i].Volume.Pose(ms);
            }
        }
        public MassMap MassAt(long ms)
        {
            Locate(ms);var projected=new List<MassCell>();
            for(int i=0;i<Bodies.Length;i++){
                var body=Bodies[i];var map=body.Volume.MassAt(ms);decimal cumulative=0,used=0;
                decimal x=(decimal)centers[i].x,y=(decimal)centers[i].y;
                for(int j=0;j<map.Cells.Count;j++){
                    var c=map.Cells[j];cumulative+=c.Mass;
                    decimal through=j==map.Cells.Count-1?body.Weight:decimal.Round(body.Weight*cumulative/map.TotalMass,14);
                    projected.Add(new MassCell(x+c.MinX,x+c.MaxX,y+c.MinY,y+c.MaxY,through-used));used=through;
                }
            }
            return new MassMap(projected);
        }
        public Mesh Slice(Transform root,long ms,float knife,bool left,out float top)
        {
            Locate(ms);top=float.NegativeInfinity;
            var parts=new List<CombineInstance>();var temporary=new List<Mesh>();
            try{
                for(int i=0;i<Bodies.Length;i++){
                    var b=Bodies[i];var child=root.GetChild(i);var pos=centers[i];var rotation=b.Volume.Pose(ms);
                    var mesh=child.GetComponent<MeshFilter>().sharedMesh;
                    float radius=mesh.bounds.extents.magnitude;
                    float x=knife-pos.x;
                    if(left?x< -radius:x>radius)continue;
                    if(Mathf.Abs(x)<=radius){
                        mesh=b.Volume.Build(ms,x,left,out float cap);temporary.Add(mesh);
                        top=Mathf.Max(top,pos.y+cap);rotation=Quaternion.identity;
                    }
                    for(int j=0;j<mesh.subMeshCount;j++){
                        if(mesh.GetIndexCount(j)==0)continue;
                        parts.Add(new CombineInstance{mesh=mesh,subMeshIndex=j,transform=Matrix4x4.TRS(pos,rotation,Vector3.one)});
                    }
                }
                var piece=new Mesh{indexFormat=IndexFormat.UInt32};
                if(parts.Count!=0)piece.CombineMeshes(parts.ToArray(),true,true);
                return piece;
            }finally{foreach(var mesh in temporary)UnityEngine.Object.Destroy(mesh);}
        }

        // Orbit paths are thin ribbons behind the system along the game camera's view
        // direction. They never enter the volume, score projection or cut sections.
        public Mesh OrbitPaths()
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color32>();var normals=new List<Vector3>();
            foreach(var body in Bodies){
                if(body.Parent!=0)continue;
                const int steps=192;
                for(int j=0;j<steps;j++){
                    float t=j*2*Mathf.PI/steps,t1=(j+1)*2*Mathf.PI/steps;
                    var a=body.OnPath(t);var b=body.OnPath(t1);
                    var w=new Vector3(-(b.y-a.y),b.x-a.x,0).normalized*.009f;
                    var back=new Vector3(0,-.47f,2.67f);int k=vertices.Count;
                    vertices.Add(a-w+back);vertices.Add(a+w+back);vertices.Add(b+w+back);vertices.Add(b-w+back);
                    var tint=Color32.Lerp(new Color32(155,183,172,255),new Color32(200,212,190,255),.5f+.5f*Mathf.Sin(t));
                    for(int q=0;q<4;q++){colors.Add(tint);normals.Add(Vector3.back);}
                    triangles.Add(k);triangles.Add(k+1);triangles.Add(k+2);triangles.Add(k);triangles.Add(k+2);triangles.Add(k+3);
                }
            }
            var m=new Mesh();m.SetVertices(vertices);m.SetNormals(normals);m.SetColors(colors);m.SetTriangles(triangles,0);return m;
        }
    }
}
