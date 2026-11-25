using System;
using HalfAndHalf.Services;
using NUnit.Framework;

namespace HalfAndHalf.Tests
{
    public class RoundAdBreakTests
    {
        [Test]
        public void FirstEighthRoundDuringWarmupContinuesWithoutAd()
        {
            var rounds = new RoundAdBreak(7, _ => { }, () => 59.9);
            rounds.CountCompletedRound();
            bool continued = false;
            rounds.ContinueAfterAd(true, _ => { Assert.Fail("Ad during first minute"); return false; }, () => continued = true);
            Assert.IsTrue(continued);
        }

        [Test]
        public void EighthCompletedRoundOffersOneAdAndOneTransition()
        {
            int progress = 0, shown = 0, transitions = 0;
            var rounds = new RoundAdBreak(0, value => progress = value, () => 60);
            Action closed = null;
            Func<Action, bool> show = done => { shown++; closed = done; return true; };
            for (int i = 1; i <= 7; i++)
            {
                rounds.CountCompletedRound();
                rounds.ContinueAfterAd(true, show, () => transitions++);
                Assert.AreEqual(i, progress);
            }
            Assert.AreEqual(0, shown);

            // Reopening the game after seven rounds preserves its place in the cadence.
            rounds = new RoundAdBreak(progress, value => progress = value, () => 60);
            rounds.CountCompletedRound();
            rounds.ContinueAfterAd(true, show, () => transitions++);
            rounds.ContinueAfterAd(true, show, () => transitions++);
            Assert.AreEqual(0, progress);
            Assert.AreEqual(1, shown);
            Assert.AreEqual(7, transitions);
            closed();
            closed();
            Assert.AreEqual(8, transitions);
        }

        [Test]
        public void DisplayedAdRestartsClockAndSkippedEighthsDoNotCarryOver()
        {
            double clock = 60;
            int shown = 0, transitions = 0;
            var rounds = new RoundAdBreak(7, _ => { }, () => clock);
            void Play(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    rounds.CountCompletedRound();
                    rounds.ContinueAfterAd(true, close => {
                        rounds.RecordImpression();
                        shown++;
                        close();
                        return true;
                    }, () => transitions++);
                }
            }
            Play(1);
            Assert.AreEqual(1, shown);
            clock = 149.9;
            Play(8);
            clock = 150;
            Play(7);
            Assert.AreEqual(1, shown);
            Play(1);
            Assert.AreEqual(2, shown);
            Assert.AreEqual(17, transitions);
        }

        [Test]
        public void RelaunchPreservesRemainingPlayTimeAfterAnAd()
        {
            double clock = 60;
            var previous = new RoundAdBreak(0, _ => { }, () => clock);
            previous.RecordImpression();
            clock += 8;
            int savedDelay = previous.RemainingCooldownSeconds;
            clock = 0;
            var reopened = new RoundAdBreak(7, _ => { }, () => clock, savedDelay);
            reopened.CountCompletedRound();
            clock = 60;
            Assert.IsFalse(reopened.AtAdBreak);
            clock = 82;
            Assert.IsTrue(reopened.AtAdBreak);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void UnavailableOpportunityContinuesImmediatelyAndExpires(bool eligible, bool ready)
        {
            int showAttempts = 0, transitions = 0;
            var rounds = new RoundAdBreak(7, _ => { }, () => 60);
            rounds.CountCompletedRound();
            rounds.ContinueAfterAd(eligible, done => { showAttempts++; return ready; }, () => transitions++);
            rounds.CountCompletedRound();
            rounds.ContinueAfterAd(true, done => { Assert.Fail("Late ad in a new round"); return true; }, () => transitions++);
            Assert.AreEqual(eligible ? 1 : 0, showAttempts);
            Assert.AreEqual(2, transitions);
        }
    }
}
