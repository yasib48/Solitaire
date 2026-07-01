using System;
using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     Persistent store for the player's magic wand charges. Backed by
    ///     <see cref="IStorageService" /> so the count survives between sessions.
    ///     The very first launch (no stored data yet) hands out a free charge.
    /// </summary>
    public class MagicWandService : IMagicWandService
    {
        private const string StorageKey = "MagicWand";
        private const int FirstLaunchGift = 1;

        private readonly IStorageService _storageService;
        private readonly IntReactiveProperty _count = new();

        public MagicWandService(IStorageService storageService)
        {
            _storageService = storageService;

            var data = _storageService.Load<Data>(StorageKey);
            if (data == null)
            {
                // Very first launch: grant the free charge and remember it so the
                // gift is only ever given once.
                _count.Value = FirstLaunchGift;
                Persist();
            }
            else
            {
                _count.Value = Math.Max(0, data.Count);
            }
        }

        public IReadOnlyReactiveProperty<int> Count => _count;

        public bool TryUse()
        {
            if (_count.Value <= 0)
                return false;

            _count.Value -= 1;
            Persist();
            return true;
        }

        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            _count.Value += amount;
            Persist();
        }

        private void Persist()
        {
            _storageService.Save(StorageKey, new Data { Count = _count.Value });
        }

        [Serializable]
        private class Data
        {
            public int Count;
        }
    }
}
