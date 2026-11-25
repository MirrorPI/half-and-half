using System;
using System.IO;
using System.Linq;
using HalfAndHalf.Content;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using NUnit.Framework;
using UnityEditor;

namespace HalfAndHalf.Tests
{
    public sealed class Task5ExpansionTests
    {
        static readonly string[] Ids = { "B01", "B02", "B03", "B04", "B05", "A01", "A02", "A03", "A04", "A05" };
        static ObjectDefinition[] Twenty()
        {
            var old = Ids.Select(id => AssetDatabase.LoadAssetAtPath<VisualSampleAsset>("Assets/HalfAndHalf/Content/" + id + "/" + id + "Sample.asset").Definition());
            // Logical fixtures verify append-only progression without pretending seven missing models exist.
            var extra = new[] { "L01", "L02", "L03", "L04", "L05", "N01", "N02", "N03", "N04", "N05" }
                .Select(id => new ObjectDefinition(id, "task5-qa-fixture-v1", new MassMap(new[] { new MassCell(-1, 1, -1, 1, 100) }), "test"));
            return old.Concat(extra).ToArray();
        }
        [Test] public void ActualLegacyTenSaveKeepsExactRecordsAndUnlocksOnlyEleventh()
        {
            string folder = Path.Combine(Path.GetTempPath(), "HalfTask5Legacy-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
            try {
                string path = Path.Combine(folder, "progress.json"); File.Copy("../reports/task-3/editor-progress.json", path);
                var defs = Twenty(); var before = new LocalProgressStore(path).Load(defs.Take(10));
                Assert.That(before.UnlockedCount, Is.EqualTo(10));
                var after = new LocalProgressStore(path).Load(defs); Assert.That(after.Selected, Is.EqualTo(9)); Assert.That(after.UnlockedCount, Is.EqualTo(11));
                for (int i = 0; i < 10; i++) {
                    var a = after.Best(i); var b = before.Best(i); Assert.That(a, Is.Not.Null);
                    Assert.That(a.Snapshot.KnifeX, Is.EqualTo(b.Snapshot.KnifeX)); Assert.That(a.LeftMass, Is.EqualTo(b.LeftMass));
                    Assert.That(a.RightMass, Is.EqualTo(b.RightMass)); Assert.That(a.AccuracyUnits, Is.EqualTo(b.AccuracyUnits));
                    Assert.That(a.ElapsedMilliseconds, Is.EqualTo(b.ElapsedMilliseconds)); Assert.That(a.Snapshot.ContentVersion, Is.EqualTo(b.Snapshot.ContentVersion));
                    Assert.That(after.BestOwner(i), Is.Empty, "Legacy owner omission remains guest");
                }
                Assert.That(after.Select(11), Is.False); Assert.That(after.Best(10), Is.Null);
                new LocalProgressStore(path).Save(after); var roundTrip = new LocalProgressStore(path).Load(defs);
                Assert.That(roundTrip.UnlockedCount, Is.EqualTo(11)); Assert.That(roundTrip.Best(9).Snapshot.KnifeX, Is.EqualTo(before.Best(9).Snapshot.KnifeX));
            } finally { Directory.Delete(folder, true); }
        }
        [Test] public void TwentyProgressUsesCThresholdAndRejectsSkippingTimeoutAndWrongVersion()
        {
            var defs = Twenty(); var legacy = new LocalProgressStore("../reports/task-3/editor-progress.json").Load(defs.Take(10));
            var p = CollectionProgress.Restore(defs, Enumerable.Range(0, 10).Select(legacy.Best), 10);
            Func<int, decimal, long, AttemptResult> attempt = (i, x, time) => Scoring.Evaluate(defs[i], new CutSnapshot(defs[i].Id, defs[i].ContentVersion, x, time, TransformState.Identity, true));
            Assert.That(p.Record(attempt(11, 0, 1)), Is.False); Assert.That(p.Record(attempt(10, -.10006m, 100)), Is.True); Assert.That(p.UnlockedCount, Is.EqualTo(11));
            Assert.That(p.Record(attempt(10, -.1m, 900)), Is.True); Assert.That(p.UnlockedCount, Is.EqualTo(12));
            Assert.That(p.Record(attempt(11, 0, 120000)), Is.False);
            for (int i = 11; i < 20; i++) Assert.That(p.Record(attempt(i, 0, 1000 + i), "qa-owner"), Is.True);
            Assert.That(p.UnlockedCount, Is.EqualTo(20)); Assert.That(p.Select(19), Is.True); Assert.That(p.Select(20), Is.False);
            Assert.That(p.Record(attempt(19, .00001m, 1019)), Is.False, "Same official accuracy and time do not improve a record");
            Assert.That(p.Record(attempt(19, .00001m, 1)), Is.True); Assert.That(p.Best(19).ElapsedMilliseconds, Is.EqualTo(1));
            var wrong = new CutSnapshot("N05", "other-version", 0, 1, TransformState.Identity, true);
            Assert.That(p.Record(new AttemptResult(wrong, 50, 50, 10000)), Is.False);
        }
    }
}
