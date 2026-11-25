using System;
using System.Linq;
using HalfAndHalf.Core;
using NUnit.Framework;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class VoxelExpansionGeometryTests
    {
        static object Model(string id)
        {
            var type=typeof(HalfAndHalf.Gameplay.AppleGame).Assembly.GetType("HalfAndHalf.Presentation.VoxelPrototypeGeometry");
            Assert.That(type,Is.Not.Null,"Missing approved seven-object voxel authoring");
            return type.GetMethod("For").Invoke(null,new object[]{id});
        }
        static Mesh Build(object model,float x,bool left)=>(Mesh)model.GetType().GetMethod("Build").Invoke(model,new object[]{x,left});
        static MassMap Mass(object model)=>(MassMap)model.GetType().GetMethod("Mass").Invoke(model,null);
        static decimal Density(object model,Vector3 p)=>(decimal)model.GetType().GetMethod("DensityAt").Invoke(model,new object[]{p});
        [TestCase("L03"),TestCase("L04"),TestCase("L05"),TestCase("N01"),TestCase("N02"),TestCase("N04"),TestCase("N05")]
        public void AuthoredMassAllowsPerfectAndNearbyPositionsStayContinuous(string id)
        {
            var mass=Mass(Model(id));Assert.That(mass.TotalMass,Is.EqualTo(100m));
            Assert.That(mass.Split(-1.35m).Left,Is.Zero);Assert.That(mass.Split(1.35m).Right,Is.Zero);
            decimal lo=-1.35m,hi=1.35m;
            for(int i=0;i<100;i++){decimal x=(lo+hi)/2;if(mass.Split(x).Left<50m)lo=x;else hi=x;}
            decimal median=(lo+hi)/2;var definition=new ObjectDefinition(id,"test",mass,"test");
            Assert.That(Scoring.Evaluate(definition,new CutSnapshot(id,"test",median,100,TransformState.Identity,true)).AccuracyUnits,Is.EqualTo(10000));
            var nearby=new[]{-.0003m,-.0002m,-.0001m,0m,.0001m,.0002m,.0003m}.Select(d=>mass.Split(median+d)).ToArray();
            Assert.That(nearby.Select(s=>s.Left).Distinct().Count(),Is.EqualTo(7));
            foreach(var split in nearby)Assert.That(split.Left+split.Right,Is.EqualTo(100m));
        }
        [TestCase("L03"),TestCase("L04"),TestCase("L05"),TestCase("N01"),TestCase("N02"),TestCase("N04"),TestCase("N05")]
        public void ArbitraryCutsConserveClosedVolumePaletteAndOutwardCaps(string id)
        {
            var model=Model(id);var whole=Build(model,float.PositiveInfinity,true);
            try
            {
                Assert.That(Volume(whole),Is.GreaterThan(0));Assert.That(whole.colors32.Distinct().Count(),Is.GreaterThanOrEqualTo(3));
                Assert.That(whole.bounds.min.x,Is.GreaterThan(-1.35f));Assert.That(whole.bounds.max.x,Is.LessThan(1.35f));
                foreach(float x in new[]{-.773f,-.217f,0f,.701f})
                {
                    var left=Build(model,x,true);var right=Build(model,x,false);
                    try
                    {
                        Assert.That(Volume(left)+Volume(right),Is.EqualTo(Volume(whole)).Within(.00002));
                        foreach(var pair in new[]{(left,1f),(right,-1f)})
                        {
                            var vertices=pair.Item1.vertices;Assert.That(pair.Item1.colors32.Length,Is.EqualTo(vertices.Length));
                            foreach(var p in vertices)Assert.That(pair.Item2>0?p.x-x:x-p.x,Is.LessThanOrEqualTo(.000001f));
                            var cap=pair.Item1.GetTriangles(1);
                            for(int i=0;i<cap.Length;i+=3)
                            {
                                Assert.That(vertices[cap[i]].x,Is.EqualTo(x).Within(.000001f));
                                Assert.That(Vector3.Cross(vertices[cap[i+1]]-vertices[cap[i]],vertices[cap[i+2]]-vertices[cap[i]]).x*pair.Item2,Is.GreaterThan(0));
                            }
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);}
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(whole);}
        }
        [TestCase("L03"),TestCase("L04"),TestCase("L05"),TestCase("N01"),TestCase("N02"),TestCase("N04"),TestCase("N05")]
        public void RepeatedAndWithinCellCutsUseFrozenGeometryWithoutSnap(string id)
        {
            var model=Model(id);var a=Build(model,.217f,true);var b=Build(model,.21705f,true);var repeated=Build(model,.217f,true);
            try{Assert.That(Volume(b),Is.GreaterThan(Volume(a)));Assert.That(repeated.vertices,Is.EqualTo(a.vertices));Assert.That(repeated.triangles,Is.EqualTo(a.triangles));Assert.That(repeated.colors32,Is.EqualTo(a.colors32));}
            finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);UnityEngine.Object.DestroyImmediate(repeated);}
        }
        [TestCase("L03",0f,.38f,0f,.61f),TestCase("L04",-.22f,.31f,0f,.61f),TestCase("L05",.10f,.35f,0f,.14f),TestCase("N04",0f,.50f,0f,-.55f)]
        public void PassengerRoomsAndCraterStayEmptyAtTheCut(string id,float x,float airY,float airZ,float solidY)
        {
            var model=Model(id);var mesh=Build(model,x,true);
            try{Assert.That(Density(model,new Vector3(x,airY,airZ)),Is.Zero);Assert.That(CapCovers(mesh,new Vector2(airY,airZ)),Is.False);Assert.That(CapCovers(mesh,new Vector2(solidY,airZ)),Is.True);}
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        [Test] public void BusRearEngineAndExcavatorCounterweightAreDenseDistinctStructures()
        {
            var bus=Model("L03");Assert.That(Density(bus,new Vector3(-.84f,-.40f,0)),Is.GreaterThan(4m));
            var digger=Model("L04");Assert.That(Density(digger,new Vector3(-.76f,-.23f,0)),Is.GreaterThan(4m));Assert.That(Density(digger,new Vector3(1.07f,-.38f,0)),Is.GreaterThan(4m));
        }
        [TestCase("N02",1.14f,-.10f),TestCase("N05",1.20f,-.046875f)]
        public void TerrainAndWaterAreVisibleDecorationWithZeroScoringMass(string id,float x,float y)
        {
            var model=Model(id);Assert.That(Density(model,new Vector3(x,y,0)),Is.Zero);
            var mesh=(Mesh)model.GetType().GetMethod("Decoration").Invoke(model,null);
            try{Assert.That(mesh.vertexCount,Is.GreaterThan(0));Assert.That(mesh.GetTriangles(1).Length,Is.Zero);}
            finally{UnityEngine.Object.DestroyImmediate(mesh);}
        }
        static double Volume(Mesh m){var v=m.vertices;var t=m.triangles;double sum=0;for(int i=0;i<t.Length;i+=3)sum+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6d;return sum;}
        static bool CapCovers(Mesh m,Vector2 p)
        {
            var v=m.vertices;var t=m.GetTriangles(1);
            for(int i=0;i<t.Length;i+=3){var a=new Vector2(v[t[i]].y,v[t[i]].z);var b=new Vector2(v[t[i+1]].y,v[t[i+1]].z);var c=new Vector2(v[t[i+2]].y,v[t[i+2]].z);float s1=Cross(b-a,p-a),s2=Cross(c-b,p-b),s3=Cross(a-c,p-c);if((s1>=0&&s2>=0&&s3>=0)||(s1<=0&&s2<=0&&s3<=0))return true;}return false;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    }
}
