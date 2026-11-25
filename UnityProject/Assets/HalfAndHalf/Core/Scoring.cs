using System;
using System.Globalization;

namespace HalfAndHalf.Core
{
    public static class Scoring
    {
        public static int Accuracy(decimal leftMass, decimal rightMass)
        {
            if (leftMass < 0 || rightMass < 0 || leftMass + rightMass <= 0)
                throw new ArgumentException("Accuracy requires non-negative masses and a positive total.");
            decimal raw = 100m - 100m * Math.Abs(leftMass - rightMass) / (leftMass + rightMass);
            return (int)Math.Round(raw * 100m, 0, MidpointRounding.AwayFromZero);
        }
        public static Grade GradeFor(int accuracyUnits)
        {
            if (accuracyUnits < 0 || accuracyUnits > 10000) throw new ArgumentOutOfRangeException(nameof(accuracyUnits));
            if (accuracyUnits == 10000) return Grade.Perfect;
            if (accuracyUnits >= 9990) return Grade.SPlus;
            if (accuracyUnits >= 9900) return Grade.S;
            if (accuracyUnits >= 9800) return Grade.A;
            if (accuracyUnits >= 9500) return Grade.B;
            return accuracyUnits >= 9000 ? Grade.C : Grade.None;
        }
        public static AttemptResult Evaluate(ObjectDefinition definition, CutSnapshot snapshot)
        {
            if (definition == null || snapshot == null) throw new ArgumentNullException();
            if (definition.Id != snapshot.ObjectId || definition.ContentVersion != snapshot.ContentVersion)
                throw new ArgumentException("Snapshot content ID/version does not match.");
            var split = MassProjection.Split(definition.MassAt(snapshot.ElapsedMilliseconds), snapshot.KnifeX, snapshot.Transform);
            return new AttemptResult(snapshot, split.Left, split.Right, Accuracy(split.Left, split.Right));
        }
        // Negative means first ranks ahead. Never compare hidden raw accuracy digits.
        public static int Compare(AttemptResult first, AttemptResult second)
        {
            int accuracy = second.AccuracyUnits.CompareTo(first.AccuracyUnits);
            return accuracy != 0 ? accuracy : first.ElapsedMilliseconds.CompareTo(second.ElapsedMilliseconds);
        }
        public static string Percent(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture);
    }
}
