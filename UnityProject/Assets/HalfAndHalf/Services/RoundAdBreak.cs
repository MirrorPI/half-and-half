using System;

namespace HalfAndHalf.Services
{
    public sealed class RoundAdBreak
    {
        const int Interval = 8;
        const double FirstAdDelaySeconds = 60;
        const double RepeatAdDelaySeconds = 90;
        readonly Func<double> playSeconds;
        double nextAdAt = FirstAdDelaySeconds;
        readonly Action<int> saveProgress;
        int progress;
        bool due, transitionTaken, hasCooldown;

        public bool AtAdBreak => due && !transitionTaken && playSeconds() >= nextAdAt;

        public int RemainingCooldownSeconds => hasCooldown
            ? (int)Math.Ceiling(Math.Max(0, nextAdAt - playSeconds())) : 0;

        public RoundAdBreak(int savedProgress, Action<int> saveProgress, Func<double> playSeconds, int savedCooldownSeconds = 0)
        {
            progress = savedProgress >= 0 && savedProgress < Interval ? savedProgress : 0;
            this.saveProgress = saveProgress;
            this.playSeconds = playSeconds;
            hasCooldown = savedCooldownSeconds > 0;
            nextAdAt = Math.Max(FirstAdDelaySeconds, Math.Min(RepeatAdDelaySeconds, savedCooldownSeconds));
        }

        public void RecordImpression()
        {
            hasCooldown = true;
            nextAdAt = playSeconds() + RepeatAdDelaySeconds;
        }

        public void CountCompletedRound()
        {
            progress = (progress + 1) % Interval;
            due = progress == 0;
            transitionTaken = false;
            saveProgress(progress);
        }

        public void ContinueAfterAd(bool eligible, Func<Action, bool> tryShow, Action continuation)
        {
            if (transitionTaken) return;
            bool canShow = eligible && AtAdBreak;
            transitionTaken = true;
            if (canShow)
            {
                // The SDK can deliver more than one terminal event for a show attempt.
                // The same guard handles a synchronous failure followed by a late callback.
                bool finished = false;
                Action finish = () =>
                {
                    if (finished) return;
                    finished = true;
                    continuation();
                };
                if (!tryShow(finish)) finish();
                return;
            }
            continuation();
        }
    }
}
