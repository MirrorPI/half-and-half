using System;
using System.Collections.Generic;
using System.IO;
using HalfAndHalf.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HalfAndHalf.Presentation
{
    public sealed class GalaxyVolume
    {
        const float Inclination = .72f;
        readonly bool expands;
        readonly Knot[] knots;
        struct Knot
        {
            public Vector3 Center, Radii;
            public float Growth;
            public Color32 Color;
            public decimal Weight;
        }

        public GalaxyVolume(byte[] bytes, bool expands)
        {
            this.expands = expands;
            using var r = new BinaryReader(new MemoryStream(bytes));
            if (r.ReadUInt32() != 0x31474848) throw new InvalidDataException("Unknown galaxy point format");
            knots = new Knot[r.ReadInt32()];
            decimal total = 0;
            for (int i = 0; i < knots.Length; i++)
            {
                var p = new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                var radii = new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                float density = r.ReadSingle();
                var color = new Color32(r.ReadByte(),r.ReadByte(),r.ReadByte(),r.ReadByte());
                // The eastern chain of outer star-forming regions travels furthest.
                float radius = new Vector2(p.x,p.y).magnitude;
                float growth = Mathf.Clamp01(radius/5.6f) * (.28f + .20f*p.x/Mathf.Max(radius,.001f));
                decimal weight = (decimal)(radii.x*radii.y*radii.z*density);
                knots[i] = new Knot {Center=p, Radii=radii, Growth=growth, Color=color, Weight=weight};
                total += weight;
            }
            decimal assigned = 0;
            for (int i=0;i<knots.Length;i++)
            {
                knots[i].Weight = i+1==knots.Length ? 100-assigned : decimal.Round(100*knots[i].Weight/total,14);
                assigned += knots[i].Weight;
            }
        }

        // One absolute clock for the vertex shader, knife contact and score replay.
        public Vector4 Motion(long milliseconds)
        {
            float t = Mathf.Clamp(milliseconds,0,120000)*.001f;
            float angle = t*(expands ? .42f : .34f);
            return new Vector4(Mathf.Cos(angle),Mathf.Sin(angle),expands?Mathf.Clamp01(t/8):0,Inclination);
        }
        static Vector3 Pose(Knot knot,Vector4 motion)
        {
            var p = knot.Center;
            float scale = 1+knot.Growth*motion.z;
            return new Vector3((p.x*motion.x-p.y*motion.y)*scale,
                (p.x*motion.y+p.y*motion.x)*scale*Inclination,p.z);
        }
        public MassMap MassAt(long milliseconds)
        {
            Vector4 motion=Motion(milliseconds);
            var cells=new List<MassCell>(knots.Length*3);
            foreach(var knot in knots)
            {
                var p=Pose(knot,motion);
                decimal x=(decimal)p.x, rx=(decimal)knot.Radii.x, y=(decimal)p.y;
                decimal h=(decimal)(knot.Radii.y*Inclination);
                decimal outer=knot.Weight*7/27;
                // Three equal-width strips integrate the sphere's 1-x² marginal.
                cells.Add(new MassCell(x-rx,x-rx/3,y-h*.75m,y+h*.75m,outer));
                cells.Add(new MassCell(x-rx/3,x+rx/3,y-h,y+h,knot.Weight-2*outer));
                cells.Add(new MassCell(x+rx/3,x+rx,y-h*.75m,y+h*.75m,outer));
            }
            return new MassMap(cells);
        }

        public Mesh Build(long milliseconds,float knife,bool left,out float sectionTop)
        {
            bool whole=float.IsInfinity(knife);
            var mesh=new Mesh {name=whole?"Galaxy field":"Frozen star clouds",indexFormat=IndexFormat.UInt32};
            var positions=new List<Vector3>(); var colors=new List<Color32>();
            var uvs=new List<Vector2>(); var centers=new List<Vector3>(); var triangles=new List<int>();
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float u0,float u1,Color32 color,Vector3 orbit)
            {
                int n=positions.Count;
                positions.Add(a);positions.Add(b);positions.Add(c);positions.Add(d);
                uvs.Add(new Vector2(u0,-1));uvs.Add(new Vector2(u0,1));uvs.Add(new Vector2(u1,1));uvs.Add(new Vector2(u1,-1));
                for(int j=0;j<4;j++){colors.Add(color);centers.Add(orbit);}
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            sectionTop=float.NegativeInfinity;
            var motion=Motion(milliseconds);
            foreach(var k in knots)
            {
                var p=Pose(k,motion);
                float rx=k.Radii.x, ry=k.Radii.y*Inclination;
                float lo=p.x-rx,hi=p.x+rx;
                if(!whole){if(left)hi=Mathf.Min(knife,hi);else lo=Mathf.Max(knife,lo);}
                if(lo>=hi)continue;
                float bottom=p.y-ry,top=p.y+ry;
                var orbit=whole?new Vector3(k.Center.x,k.Center.y,k.Growth):Vector3.zero;
                Quad(new Vector3(lo,bottom,p.z),new Vector3(lo,top,p.z),new Vector3(hi,top,p.z),
                    new Vector3(hi,bottom,p.z),(lo-p.x)/rx,(hi-p.x)/rx,k.Color,orbit);
                float dx=(knife-p.x)/rx;
                if(!whole && Mathf.Abs(dx)<1)
                {
                    float chord=Mathf.Sqrt(1-dx*dx);
                    float depth=k.Radii.z*chord;
                    bottom=p.y-ry*chord;top=p.y+ry*chord;
                    sectionTop=Mathf.Max(sectionTop,top);
                    Color32 cap=Color32.Lerp(k.Color,new Color32(255,223,172,255),.32f);
                    Quad(new Vector3(knife,bottom,p.z-depth),new Vector3(knife,top,p.z-depth),
                        new Vector3(knife,top,p.z+depth),new Vector3(knife,bottom,p.z+depth),-1,1,cap,Vector3.zero);
                }
            }
            mesh.SetVertices(positions);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetUVs(1,centers);
            mesh.SetTriangles(triangles,0);
            if(whole)mesh.bounds=new Bounds(Vector3.zero,Vector3.one*22);
            return mesh;
        }
    }
}
