using System;
using System.IO;
using System.Linq;
using HalfAndHalf.Content;
using HalfAndHalf.Core;
using HalfAndHalf.Presentation;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HalfAndHalf.Tests
{
    public sealed class Task5VoxelAssetTests
    {
        public static string PathFor(string id)=>"Assets/HalfAndHalf/Content/"+id+"/VoxelPrototype/"+id+"Voxel.asset";
        public static VisualSampleAsset[] Twenty()=>new[]{"B01","B02","B03","B04","B05","A01","A02","A03","A04","A05","L01","L02","L03","L04","L05","N01","N02","N03","N04","N05"}.Select(id=>AssetDatabase.LoadAssetAtPath<VisualSampleAsset>(id=="L02"?"Assets/HalfAndHalf/Content/L02/VoxelTrial/L02VoxelTrial.asset":Array.IndexOf(VoxelPrototypeGeometry.Ids,id)>=0?PathFor(id):"Assets/HalfAndHalf/Content/"+id+"/"+id+"Sample.asset")).ToArray();
        [TestCase("L03"),TestCase("L04"),TestCase("L05"),TestCase("N01"),TestCase("N02"),TestCase("N04"),TestCase("N05")]
        public void SavedPrototypeUsesExactAuthoredMeshMassVersionAndOwnThreeSounds(string id)
        {
            var a=AssetDatabase.LoadAssetAtPath<VisualSampleAsset>(PathFor(id));Assert.That(a,Is.Not.Null,"Missing saved voxel prototype "+id);
            Assert.That(a.Status,Is.EqualTo("prototype"));Assert.That(a.ContentVersion,Is.EqualTo(VoxelPrototypeGeometry.Version(id)));Assert.That(a.ObjectId,Is.EqualTo(id));
            Assert.That(a.CutVariants.Length,Is.EqualTo(3));foreach(var clip in a.CutVariants)Assert.That(clip.name,Does.StartWith(id+"_cut_"));
            var saved=a.Definition().Mass;var generated=VoxelPrototypeGeometry.For(id).Mass();Assert.That(saved.Cells.Count,Is.EqualTo(generated.Cells.Count));
            for(int i=0;i<saved.Cells.Count;i++){var s=saved.Cells[i];var g=generated.Cells[i];Assert.That((s.MinX,s.MaxX,s.MinY,s.MaxY,s.Mass),Is.EqualTo((g.MinX,g.MaxX,g.MinY,g.MaxY,g.Mass)));}
            var mesh=a.Model.GetComponent<MeshFilter>().sharedMesh;var source=VoxelPrototypeGeometry.For(id).Build(float.PositiveInfinity,true);
            try{Assert.That(mesh.vertices,Is.EqualTo(source.vertices));Assert.That(mesh.triangles,Is.EqualTo(source.triangles));Assert.That(mesh.colors32,Is.EqualTo(source.colors32));}
            finally{UnityEngine.Object.DestroyImmediate(source);}
        }
        [Test] public void BuildStartsWithRealTwentyAndLegacySaveKeepsExactFirstTenRecords()
        {
            Assert.That(EditorBuildSettings.scenes.First(s=>s.enabled).path,Is.EqualTo("Assets/Scenes/Gameplay20.unity"),"Real twenty-object scene must be default player entry");
            var assets=Twenty();Assert.That(assets.All(a=>a!=null),Is.True);var definitions=assets.Select(a=>a.Definition()).ToArray();
            Assert.That(definitions.Length,Is.EqualTo(20));Assert.That(definitions.Select(d=>d.Id).Distinct().Count(),Is.EqualTo(20));
            Assert.That(definitions.Any(d=>d.ContentVersion=="task5-qa-fixture-v1"),Is.False);
            string folder=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"HalfActual20-"+Guid.NewGuid());Directory.CreateDirectory(folder);
            try
            {
                string save=System.IO.Path.Combine(folder,"progress.json");File.Copy("../reports/task-3/editor-progress.json",save);
                var before=new LocalProgressStore(save).Load(definitions.Take(10));var after=new LocalProgressStore(save).Load(definitions);
                Assert.That(after.UnlockedCount,Is.EqualTo(11));Assert.That(after.Selected,Is.EqualTo(9));Assert.That(after.Best(10),Is.Null);
                for(int i=0;i<10;i++){var a=after.Best(i);var b=before.Best(i);Assert.That((a.Snapshot.ObjectId,a.Snapshot.ContentVersion,a.Snapshot.KnifeX,a.LeftMass,a.RightMass,a.AccuracyUnits,a.ElapsedMilliseconds,after.BestOwner(i)),Is.EqualTo((b.Snapshot.ObjectId,b.Snapshot.ContentVersion,b.Snapshot.KnifeX,b.LeftMass,b.RightMass,b.AccuracyUnits,b.ElapsedMilliseconds,before.BestOwner(i))));}
                var store=new LocalProgressStore(save);store.Load(definitions);store.Save(after);var recovered=new LocalProgressStore(save).Load(definitions);Assert.That(recovered.UnlockedCount,Is.EqualTo(11));
                for(int i=0;i<10;i++)Assert.That(recovered.Best(i).AccuracyUnits,Is.EqualTo(before.Best(i).AccuracyUnits));
            }
            finally{Directory.Delete(folder,true);}
        }
    }
}
