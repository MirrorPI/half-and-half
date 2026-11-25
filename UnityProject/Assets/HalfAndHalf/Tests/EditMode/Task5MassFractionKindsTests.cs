// Correctness compatibility assertions; the performance RED is the preserved actual v5 device record.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HalfAndHalf.Core;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public sealed class Task5MassFractionKindsTests
    {
        [Test]
        public void InteriorFractionRoundedToZeroMustKeepV5SkipAndBits()
        {
            var cells = new[] { new MassCell(0m, 3m, 0m, 1m, .1m), new MassCell(-1m, 0m, 0m, 1m, 1m) };
            var split = new MassMap(cells).Split(.0000000000000000000000000001m);
            Assert.That(decimal.GetBits(split.Left), Is.EqualTo(decimal.GetBits(1m)));
            Assert.That(decimal.GetBits(split.Right), Is.EqualTo(decimal.GetBits(.1m)));
        }

        [Test]
        public void ExcludedMixedScaleAndNegativeZeroMustKeepV5SkipBits()
        {
            foreach (decimal zeroMass in new[] { .1000m, new decimal(0, 0, 0, true, 28) })
            {
                var cells = new[] { new MassCell(0m, 3m, 0m, 1m, zeroMass), new MassCell(-1m, 0m, 0m, 1m, 1m) };
                Assert.That(decimal.GetBits(new MassMap(cells).Split(0m).Left), Is.EqualTo(decimal.GetBits(1m)));
            }
        }

        [Test]
        public void HeapFractionKindsAndConcurrentCallsMustKeepFrozenV5Arithmetic()
        {
            var cells = new List<MassCell>();
            // Reversed first cells force the unordered path; all 129 intervals remain distinct.
            for (int i = 128; i >= 0; i--)
                cells.Add(new MassCell(i, i + 3, 0, 1, i % 3 == 0 ? .1000m : .1m));
            var map = new MassMap(cells);
            decimal[] knives = { -1m, 0m, .0000000000000000000000000001m, 1m, 1.23456789m, 64m, 64.217m, 130m, 131m, 132m };
            Parallel.For(0, 512, i =>
            {
                decimal knife = knives[i % knives.Length];
                var expected = V5(cells, knife);
                var actual = map.Split(knife);
                Assert.That(decimal.GetBits(actual.Left), Is.EqualTo(decimal.GetBits(expected.Left)));
                Assert.That(decimal.GetBits(actual.Right), Is.EqualTo(decimal.GetBits(expected.Right)));
            });
        }

        static MassSplit V5(IReadOnlyList<MassCell> cells, decimal knife)
        {
            decimal left = 0m, total = 0m;
            foreach (var cell in cells)
            {
                total += cell.Mass;
                decimal fraction = knife <= cell.MinX ? 0m : knife >= cell.MaxX ? 1m : Math.Max(0m, Math.Min(1m, (knife - cell.MinX) / (cell.MaxX - cell.MinX)));
                if (fraction == 0m) continue;
                if (fraction == 1m) { left += cell.Mass; continue; }
                left += cell.Mass * fraction;
            }
            return new MassSplit(left, total - left);
        }
    }
}
