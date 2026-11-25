using System;
using System.Globalization;
using HalfAndHalf.Core;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class CoreTests
    {
        static decimal D(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
        static MassMap Uniform() => new MassMap(new[] { new MassCell(-1, 1, -1, 1, 100) });
        static ObjectDefinition Apple() => new ObjectDefinition("B01", "test-v1", Uniform(), "B01");
        static AttemptResult Result(decimal left, long ms) => Scoring.Evaluate(Apple(), new CutSnapshot("B01", "test-v1", left / 50m - 1m, ms, TransformState.Identity, true));

        [TestCase("-2", "0", "100")]
        [TestCase("-1", "0", "100")]
        [TestCase("0", "50", "50")]
        [TestCase("0.25", "62.5", "37.5")]
        [TestCase("1", "100", "0")]
        [TestCase("2", "100", "0")]
        public void CellCrossingDividesContinuouslyAndConservesMass(string x, string left, string right)
        {
            var map = Uniform(); var split = map.Split(D(x));
            Assert.That(split.Left, Is.EqualTo(D(left)));
            Assert.That(split.Right, Is.EqualTo(D(right)));
            Assert.That(map.TotalMass, Is.EqualTo(100m));
        }

        [Test] public void NonUniformCellsKeepTheirWeightsAtSharedBoundary()
        {
            var map = new MassMap(new[] { new MassCell(-1, 0, 0, 1, 30), new MassCell(0, 1, 0, 1, 70) });
            Assert.That(map.Split(0).Left, Is.EqualTo(30m));
            Assert.That(map.Split(.5m).Left, Is.EqualTo(65m));
            Assert.That(map.Split(.5m).Right, Is.EqualTo(35m));
        }

        [Test] public void MirroredCutsExchangeSidesWithoutLosingMass()
        {
            for (int i = -20; i <= 20; i++)
            {
                var a = Uniform().Split(i / 20m); var b = Uniform().Split(-i / 20m);
                Assert.That(a.Left, Is.EqualTo(b.Right));
                Assert.That(a.Left + a.Right, Is.EqualTo(100m));
            }
        }

        [TestCase("50", "50", 10000)]
        [TestCase("49", "51", 9800)]
        [TestCase("49.997", "50.003", 9999)]
        [TestCase("49.9975", "50.0025", 10000)]
        [TestCase("0", "100", 0)]
        public void OfficialAccuracyRoundsHalfUpOnlyAtTheEnd(string left, string right, int expected)
        { Assert.That(Scoring.Accuracy(D(left), D(right)), Is.EqualTo(expected)); }

        [Test] public void EqualDisplayRatiosCanHaveDifferentOfficialScores()
        {
            Assert.That(Scoring.Percent(49.997m), Is.EqualTo("50.00"));
            Assert.That(Scoring.Percent(50.003m), Is.EqualTo("50.00"));
            Assert.That(Scoring.Accuracy(49.997m, 50.003m), Is.EqualTo(9999));
        }

        [TestCase(8999, Grade.None)] [TestCase(9000, Grade.C)]
        [TestCase(9499, Grade.C)] [TestCase(9500, Grade.B)]
        [TestCase(9799, Grade.B)] [TestCase(9800, Grade.A)]
        [TestCase(9899, Grade.A)] [TestCase(9900, Grade.S)]
        [TestCase(9989, Grade.S)] [TestCase(9990, Grade.SPlus)]
        [TestCase(9999, Grade.SPlus)] [TestCase(10000, Grade.Perfect)]
        public void GradeUsesOfficialUnitsAtEachBoundary(int units, Grade grade)
        { Assert.That(Scoring.GradeFor(units), Is.EqualTo(grade)); }

        [Test] public void OfficialTiesUseTimeWithoutHiddenDigits()
        {
            Assert.That(Scoring.Compare(Result(49.997m, 2000), Result(49.996m, 1000)), Is.GreaterThan(0));
            Assert.That(Scoring.Compare(Result(49.997m, 1000), Result(49.996m, 1000)), Is.EqualTo(0));
            Assert.That(Scoring.Compare(Result(50m, 5000), Result(49m, 100)), Is.LessThan(0));
        }

        [Test] public void WorldKnifeUsesFrozenTranslationAndScale()
        {
            var snapshot = new CutSnapshot("B01", "test-v1", 4.5m, 2410, new TransformState(4, 2, 2, 1), true);
            var result = Scoring.Evaluate(Apple(), snapshot);
            Assert.That(result.LeftMass, Is.EqualTo(62.5m));
            Assert.That(result.AccuracyUnits, Is.EqualTo(7500));
            Assert.That(result.ElapsedMilliseconds, Is.EqualTo(2410));
        }

        [Test] public void InvalidMassAndContentCannotProduceOfficialResults()
        {
            Assert.Throws<ArgumentException>(() => new MassMap(new[] { new MassCell(0, 0, 0, 1, 1) }));
            Assert.Throws<ArgumentException>(() => new MassMap(new[] { new MassCell(0, 1, 0, 1, -1) }));
            Assert.Throws<ArgumentException>(() => new MassMap(Array.Empty<MassCell>()));
            Assert.Throws<ArgumentException>(() => Scoring.Accuracy(0, 0));
            Assert.Throws<ArgumentException>(() => Scoring.Evaluate(Apple(), new CutSnapshot("B01", "other", 0, 10, TransformState.Identity, true)));
        }

        [Test] public void SnapshotSurvivesRetryAndReplaysTheSameResult()
        {
            var s = new AttemptSession(Apple(), -1, 1); s.Land(1000, TransformState.Identity);
            s.BeginDrag(1, .25m, 1100); s.ReleaseDrag(1, .25m, 3410);
            var snap = s.Snapshot; var first = s.Result; s.Retry();
            var replay = Scoring.Evaluate(Apple(), snap);
            Assert.That(replay.LeftMass, Is.EqualTo(62.5m));
            Assert.That(replay.AccuracyUnits, Is.EqualTo(7500));
            Assert.That(replay.ElapsedMilliseconds, Is.EqualTo(2410));
            Assert.That(first.Snapshot, Is.SameAs(snap));
            Assert.That(s.Phase, Is.EqualTo(AttemptPhase.Dropping));
            Assert.That(s.Snapshot, Is.Null);
        }

        [Test] public void OnlyPrimaryPointerCanMoveReleaseAndCutOnce()
        {
            var s = new AttemptSession(Apple(), -1, 1);
            Assert.That(s.BeginDrag(1, 0, 1), Is.False);
            s.Land(1000, TransformState.Identity);
            Assert.That(s.BeginDrag(1, 0, 1000), Is.True);
            Assert.That(s.BeginDrag(2, 1, 1001), Is.False);
            Assert.That(s.MoveDrag(2, 1, 1002), Is.False);
            Assert.That(s.ReleaseDrag(2, 1, 1003), Is.False);
            Assert.That(s.ReleaseDrag(1, .25m, 1250), Is.True);
            Assert.That(s.ReleaseDrag(1, -1, 1300), Is.False);
            Assert.That(s.Snapshot.KnifeX, Is.EqualTo(.25m));
            Assert.That(s.Snapshot.ElapsedMilliseconds, Is.EqualTo(250));
        }

        [Test] public void CancelLeavesReadyAndDoesNotCut()
        {
            var s = new AttemptSession(Apple(), -1, 1); s.Land(0, TransformState.Identity);
            s.BeginDrag(1, .5m, 100); s.CancelDrag(2);
            Assert.That(s.ActivePointer, Is.EqualTo(1)); s.CancelDrag(1);
            Assert.That(s.ActivePointer, Is.Null); Assert.That(s.Result, Is.Null);
            Assert.That(s.ReleaseDrag(1, .5m, 200), Is.False);
            Assert.That(s.BeginDrag(3, 0, 300), Is.True);
        }

        [TestCase(119999, true)] [TestCase(120000, false)] [TestCase(120001, false)]
        public void TimeoutExcludesCutAtAndBeyond120Seconds(int elapsed, bool valid)
        {
            var s = new AttemptSession(Apple(), -1, 1); s.Land(1000, TransformState.Identity);
            s.BeginDrag(1, 0, 1000);
            Assert.That(s.ReleaseDrag(1, 0, 1000 + elapsed), Is.EqualTo(valid));
            if (!valid) { Assert.That(s.Phase, Is.EqualTo(AttemptPhase.Invalid)); Assert.That(s.InvalidReason, Is.EqualTo(InvalidReason.TimedOut)); Assert.That(s.Result, Is.Null); }
        }

        [Test] public void AppInterruptionClearsPointerAndRequiresRetry()
        {
            var s = new AttemptSession(Apple(), -1, 1); s.Land(0, TransformState.Identity);
            s.BeginDrag(1, 0, 100); s.Interrupt();
            Assert.That(s.Phase, Is.EqualTo(AttemptPhase.Invalid));
            Assert.That(s.InvalidReason, Is.EqualTo(InvalidReason.Interrupted));
            Assert.That(s.ActivePointer, Is.Null);
            Assert.That(s.ReleaseDrag(1, 0, 200), Is.False);
            s.Retry(); s.Land(1000, TransformState.Identity); s.BeginDrag(1, 0, 1000);
            Assert.That(s.ReleaseDrag(1, 0, 1000), Is.True);
            Assert.That(s.Result.ElapsedMilliseconds, Is.Zero);
        }
    }
}
