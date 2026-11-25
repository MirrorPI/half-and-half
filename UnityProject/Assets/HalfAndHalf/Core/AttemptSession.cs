using System;

namespace HalfAndHalf.Core
{
    public sealed class AttemptSession
    {
        public AttemptPhase Phase { get; private set; }
        public InvalidReason InvalidReason { get; private set; }
        public decimal KnifeX { get; private set; }
        public CutSnapshot Snapshot { get; private set; }
        public AttemptResult Result { get; private set; }
        public int? ActivePointer { get; private set; }
        readonly ObjectDefinition definition;
        readonly decimal minX, maxX;
        long readyMs;
        TransformState transform;
        public AttemptSession(ObjectDefinition definition, decimal minX, decimal maxX)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (minX >= maxX) throw new ArgumentException("Knife range must have positive width.");
            this.minX = minX; this.maxX = maxX; Retry();
        }
        public void Land(long nowMs, TransformState transform)
        {
            if (Phase != AttemptPhase.Dropping) return;
            readyMs = nowMs; this.transform = transform; Phase = AttemptPhase.Ready;
        }
        decimal Clamp(decimal x) => Math.Max(minX, Math.Min(maxX, x));
        public bool BeginDrag(int pointerId, decimal x, long nowMs)
        {
            Tick(nowMs);
            if (Phase != AttemptPhase.Ready || ActivePointer.HasValue) return false;
            ActivePointer = pointerId; KnifeX = Clamp(x); return true;
        }
        public bool MoveDrag(int pointerId, decimal x, long nowMs)
        {
            Tick(nowMs);
            if (Phase != AttemptPhase.Ready || ActivePointer != pointerId) return false;
            KnifeX = Clamp(x); return true;
        }
        public bool ReleaseDrag(int pointerId, decimal x, long nowMs)
        {
            Tick(nowMs);
            if (Phase != AttemptPhase.Ready || ActivePointer != pointerId) return false;
            KnifeX = Clamp(x); ActivePointer = null;
            Snapshot = new CutSnapshot(definition.Id, definition.ContentVersion, KnifeX, Elapsed(nowMs), transform, true);
            Result = Scoring.Evaluate(definition, Snapshot); Phase = AttemptPhase.Cutting; return true;
        }
        public void CancelDrag(int pointerId) { if (ActivePointer == pointerId) ActivePointer = null; }
        public void Tick(long nowMs)
        { if (Phase == AttemptPhase.Ready && Elapsed(nowMs) >= 120000) Invalidate(InvalidReason.TimedOut); }
        public void Interrupt()
        {
            // Interruption after CUT cannot turn a frozen attempt into an online candidate.
            if (Phase != AttemptPhase.Invalid) Invalidate(InvalidReason.Interrupted);
        }
        void Invalidate(InvalidReason reason)
        {
            Phase = AttemptPhase.Invalid; InvalidReason = reason; ActivePointer = null;
            if (Snapshot != null)
            {
                Snapshot = new CutSnapshot(Snapshot.ObjectId, Snapshot.ContentVersion, Snapshot.KnifeX,
                    Snapshot.ElapsedMilliseconds, Snapshot.Transform, false);
                Result = Scoring.Evaluate(definition, Snapshot);
            }
        }
        public void FinishCut() { if (Phase == AttemptPhase.Cutting) Phase = AttemptPhase.Result; }
        public void Retry()
        {
            Phase = AttemptPhase.Dropping; InvalidReason = InvalidReason.None; ActivePointer = null;
            Snapshot = null; Result = null; KnifeX = Clamp(0); readyMs = 0; transform = TransformState.Identity;
        }
        public long Elapsed(long nowMs) => Snapshot != null ? Snapshot.ElapsedMilliseconds : Math.Max(0, nowMs - readyMs);
    }
}
