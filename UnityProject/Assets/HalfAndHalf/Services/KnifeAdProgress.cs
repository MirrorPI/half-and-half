using System;

namespace HalfAndHalf.Services
{
    public sealed class KnifeAdProgress
    {
        public const int Required = 5;
        readonly Action<int> save;
        public int Count { get; private set; }
        public bool Unlocked => Count == Required;

        public KnifeAdProgress(int saved, Action<int> save)
        {
            Count = Math.Max(0, Math.Min(Required, saved));
            this.save = save;
        }

        public Action ForOneShowing()
        {
            bool credited = false;
            return () => {
                if (credited || Unlocked) return;
                credited = true;
                Count++;
                save?.Invoke(Count);
            };
        }
    }
}
