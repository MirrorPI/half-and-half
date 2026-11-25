using System.Collections;
using HalfAndHalf.Core;
using HalfAndHalf.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HalfAndHalf.Tests
{
    public sealed class GameplayTests
    {
        GameObject root;
        AppleGame game;
        [UnitySetUp] public IEnumerator Setup()
        {
            root = new GameObject("GameTest"); game = root.AddComponent<AppleGame>();
            game.Initialize(new ObjectDefinition("B01", "test-v1", new MassMap(new[] { new MassCell(-1, 1, -1, 1, 100) }), "B01"));
            yield return null;
        }
        [UnityTearDown] public IEnumerator Teardown() { Object.Destroy(root); yield return null; }
        IEnumerator Ready()
        {
            double until = Time.realtimeSinceStartupAsDouble + 2;
            while (game.Session.Phase == AttemptPhase.Dropping && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Ready));
        }
        Vector2 Point(float fraction) => new Vector2(Screen.width * fraction, Screen.height * .35f);

        [UnityTest] public IEnumerator DropLandsBeforeClockAndInputStart()
        {
            Assert.That(game.BeginPointer(1, Point(.5f)), Is.False);
            yield return Ready();
            Assert.That(game.Session.Elapsed((long)(Time.realtimeSinceStartupAsDouble * 1000)), Is.LessThan(100));
            Assert.That(game.BeginPointer(1, Point(.5f)), Is.True);
        }
        [UnityTest] public IEnumerator ReleaseFreezesOneResultAndWaitsForCutAnimation()
        {
            yield return Ready(); game.BeginPointer(1, Point(.5f));
            Assert.That(game.ReleasePointer(1, Point(.5f)), Is.True);
            var snapshot = game.Session.Snapshot;
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Cutting));
            Assert.That(game.ReleasePointer(1, Point(.1f)), Is.False);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Result));
            Assert.That(game.Session.Snapshot, Is.SameAs(snapshot));
            Assert.That(game.Session.Result.AccuracyUnits, Is.EqualTo(10000));
        }
        [UnityTest] public IEnumerator SecondPointerAndCanceledDragNeverCut()
        {
            yield return Ready(); game.BeginPointer(4, Point(.5f));
            Assert.That(game.BeginPointer(5, Point(.9f)), Is.False);
            Assert.That(game.ReleasePointer(5, Point(.9f)), Is.False);
            game.CancelPointer(4);
            Assert.That(game.ReleasePointer(4, Point(.5f)), Is.False);
            Assert.That(game.Session.Result, Is.Null);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Ready));
        }
        [UnityTest] public IEnumerator InterruptDuringCutExcludesOnlineResult()
        {
            yield return Ready(); game.BeginPointer(1, Point(.5f)); game.ReleasePointer(1, Point(.5f));
            game.SetInterrupted(); yield return new WaitForSecondsRealtime(.65f);
            Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Invalid));
            Assert.That(game.Session.Result.OnlineValid, Is.False);
            game.Retry(); yield return Ready();
            Assert.That(game.Session.Result, Is.Null);
        }
        [UnityTest] public IEnumerator TwentyRetriesClearResultPointerAndOldAnimation()
        {
            for (int i = 0; i < 20; i++)
            {
                yield return Ready(); Assert.That(game.BeginPointer(1, Point(.5f)), Is.True);
                Assert.That(game.ReleasePointer(1, Point(.5f)), Is.True);
                if (i % 2 == 0) yield return new WaitForSecondsRealtime(.65f);
                game.Retry();
                Assert.That(game.Session.Phase, Is.EqualTo(AttemptPhase.Dropping));
                Assert.That(game.Session.Result, Is.Null); Assert.That(game.Session.Snapshot, Is.Null);
                Assert.That(game.Session.ActivePointer, Is.Null);
            }
            yield return Ready(); Assert.That(game.Session.KnifeX, Is.Zero);
        }
    }
}
