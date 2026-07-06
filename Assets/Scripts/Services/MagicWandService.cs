using System;
using UniRx;
using UnityEngine;

namespace Solitaire.Services
{
    /// <summary>
    ///     Persistent store for the player's magic wand charges. Backed by
    ///     <see cref="IStorageService" /> so the count survives between sessions.
    ///     Each game entry starts with at least one free charge available.
    /// </summary>
    public class MagicWandService : IMagicWandService
    {
        private const string StorageKey = "MagicWand";

        private readonly IStorageService _storageService;
        private readonly Config _config;
        private readonly IntReactiveProperty _count = new();

        public MagicWandService(IStorageService storageService, Config config)
        {
            _storageService = storageService;
            _config = config;

            var data = _storageService.Load<Data>(StorageKey);
            var savedCount = data?.Count ?? 0;
            _count.Value = Math.Max(_config.StartingCharges, savedCount);

            if (data == null || savedCount < _config.StartingCharges)
                Persist();
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

        /// <summary>Bound from the GameConfig asset - edit there, no code/recompile needed.</summary>
        [Serializable]
        public class Config
        {
            [Tooltip("Charges a player has on first launch, and the floor their count is topped up to if it's ever lower (e.g. after a save-data change).")]
            public int StartingCharges = 1;
        }
    }
}
