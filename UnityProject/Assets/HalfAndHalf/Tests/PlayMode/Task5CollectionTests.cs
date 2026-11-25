using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HalfAndHalf.Content;
using HalfAndHalf.Core;
using HalfAndHalf.Services;
using HalfAndHalf.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HalfAndHalf.Tests
{
    // Extra definitions are explicitly synthetic QA fixtures, never authored content or server records.
    public sealed class Task5CollectionTests
    {
        readonly List<Object> fixtures = new List<Object>();
        string folder;
        [SetUp] public void Setup() { folder = Path.Combine(Path.GetTempPath(), "HalfTask5-" + Guid.NewGuid()); Directory.CreateDirectory(folder); }
        [TearDown] public void Cleanup() { foreach (var f in fixtures) Object.DestroyImmediate(f); fixtures.Clear(); Directory.Delete(folder, true); }

        VisualSampleAsset[] Expand(VisualSampleAsset[] originals)
        {
            var samples = originals.ToList();
            foreach (string id in new[] { "L01", "L02", "L03", "L04", "L05", "N01", "N02", "N03", "N04", "N05" })
            {
                var sample = Object.Instantiate(originals[0]); fixtures.Add(sample);
                sample.ObjectId = id; sample.ContentVersion = "task5-qa-fixture-v1"; sample.DisplayName = "QA " + id;
                var data = JsonUtility.FromJson<AppleDefinitionAsset.MapData>(originals[0].MassData.text);
                data.objectId = id; data.contentVersion = sample.ContentVersion;
                sample.MassData = new TextAsset(JsonUtility.ToJson(data)); fixtures.Add(sample.MassData);
                samples.Add(sample);
            }
            return samples.ToArray();
        }
        static AttemptResult Perfect(ObjectDefinition d, long time)
        {
            decimal lo = -1.35m, hi = 1.35m;
            for (int i = 0; i < 60; i++) { decimal mid = (lo + hi) / 2; if (d.Mass.Split(mid).Left < d.Mass.TotalMass / 2) lo = mid; else hi = mid; }
            return Scoring.Evaluate(d, new CutSnapshot(d.Id, d.ContentVersion, (lo + hi) / 2, time, TransformState.Identity, true));
        }
        static void Cut(MvpCollection m)
        {
            var d = m.Samples[m.Progress.Selected].Definition(); var r = Perfect(d, 100);
            m.Game.Session.Land((long)(Time.realtimeSinceStartupAsDouble * 1000), TransformState.Identity);
            var point = m.Game.GameCamera.WorldToScreenPoint(new Vector3((float)r.Snapshot.KnifeX, 0, 0));
            Assert.That(m.Game.BeginPointer(515, point), Is.True); Assert.That(m.Game.ReleasePointer(515, point), Is.True);
        }

        [UnityTest] public IEnumerator ExpandedListShowsTwentyAndLastRowCanSelectWithoutDuplicates()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay"); yield return null;
            var m = Object.FindFirstObjectByType<MvpCollection>();
            m.Samples = Expand(m.Samples); m.SavePath = Path.Combine(folder, "expanded.json");
            var defs = m.Samples.Select(s => s.Definition()).ToArray();
            var progress = CollectionProgress.Restore(defs, defs.Select((d, i) => Perfect(d, 500 + i)), 19);
            new LocalProgressStore(m.SavePath).Save(progress); m.Reload(); m.Open();
            Assert.That(m.ProgressLabel.text, Does.Contain("20 / 20"), "A twenty-object save must not appear as twenty of ten");
            Assert.That(m.Items.Length, Is.EqualTo(20), "Every definition needs a reachable button");
            var scroll = m.Panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null); Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.verticalNormalizedPosition = 0; Canvas.ForceUpdateCanvases();
            m.Items[19].onClick.Invoke(); Assert.That(m.Progress.Selected, Is.EqualTo(19)); Assert.That(m.Panel.activeSelf, Is.False);
            m.Reload(); m.Open(); Assert.That(m.Items.Length, Is.EqualTo(20));
            Assert.That(scroll.content.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length, Is.EqualTo(20));
            Assert.That(m.ItemLabels[19].text, Does.Contain("QA N05")); Assert.That(m.Game.Presentation.Knife.gameObject.activeSelf, Is.False);
        }
        [UnityTest] public IEnumerator TenthCutUnlocksEleventhAndOnlyTwentiethCutCompletesCollection()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay"); yield return null;
            var m = Object.FindFirstObjectByType<MvpCollection>(); m.Samples = Expand(m.Samples);
            m.SavePath = Path.Combine(folder, "boundary.json"); var defs = m.Samples.Select(s => s.Definition()).ToArray();
            var p = CollectionProgress.Restore(defs, defs.Take(10).Select((d, i) => Perfect(d, 5000 + i)), 9);
            new LocalProgressStore(m.SavePath).Save(p); m.Reload(); Cut(m); yield return new WaitForSecondsRealtime(.55f);
            Assert.That(m.Progress.UnlockedCount, Is.EqualTo(11));
            Assert.That(m.Outcome.text, Does.Not.Contain("COLLECTION COMPLETE"), "A05 is no longer the final object");
            p = CollectionProgress.Restore(defs, defs.Take(19).Select((d, i) => Perfect(d, 5000 + i)), 19);
            var store = new LocalProgressStore(m.SavePath); store.Load(defs); store.Save(p);
            m.Reload(); Cut(m); yield return new WaitForSecondsRealtime(.55f);
            Assert.That(m.Progress.Best(19).AccuracyUnits, Is.EqualTo(10000)); Assert.That(m.Outcome.text, Does.Contain("COLLECTION COMPLETE"));
        }
        [UnityTest] public IEnumerator ExplicitOfflineExpansionMappingOffersOnlyRegisteredVersionToQueue()
        {
            yield return SceneManager.LoadSceneAsync("Gameplay"); yield return null;
            var m = Object.FindFirstObjectByType<MvpCollection>(); m.Samples = Expand(m.Samples);
            var controller = Object.FindFirstObjectByType<GameCenterController>();
            var config = Object.Instantiate(controller.Configuration); fixtures.Add(config); config.EnableOnline = false;
            config.Mappings = new[] { new LeaderboardMapping { objectId="L01", contentVersion="task5-qa-fixture-v1", leaderboardId="fixture.L01" } };
            controller.Configuration=config; m.SavePath=Path.Combine(folder,"queue.json");
            var defs=m.Samples.Select(s=>s.Definition()).ToArray();
            new LocalProgressStore(m.SavePath).Save(CollectionProgress.Restore(defs,defs.Take(12).Select((d,i)=>Perfect(d,1000+i)),11));
            m.Reload(); yield return new WaitForSecondsRealtime(.65f);
            Assert.That(controller.PendingCount,Is.EqualTo(1),"Only explicitly mapped L01 may enter this offline queue");
            Assert.That(controller.Queue.Entries.Single(e=>e.pending).objectId,Is.EqualTo("L01"));
            Assert.That(controller.Flow.IsSignedIn,Is.False,"QA never authenticates or submits");
        }
    }
}
