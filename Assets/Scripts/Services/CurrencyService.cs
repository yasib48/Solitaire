using System;
using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     Persistent coin wallet, backed by <see cref="IStorageService" />.
    ///     Starts empty; balance never goes below zero.
    /// </summary>
    public class CurrencyService : ICurrencyService
    {
        private const string StorageKey = "Currency";
        private const int StartBalance = 0;

        private readonly IStorageService _storageService;
        private readonly IntReactiveProperty _balance = new();

        public CurrencyService(IStorageService storageService)
        {
            _storageService = storageService;

            var data = _storageService.Load<Data>(StorageKey);
            _balance.Value = data == null ? StartBalance : Math.Max(0, data.Balance);
        }

        public IReadOnlyReactiveProperty<int> Balance => _balance;

        public void Add(int amount)
        {
            if (amount <= 0)
                return;

            _balance.Value += amount;
            Persist();
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || _balance.Value < amount)
                return false;

            _balance.Value -= amount;
            Persist();
            return true;
        }

        private void Persist()
        {
            _storageService.Save(StorageKey, new Data { Balance = _balance.Value });
        }

        [Serializable]
        private class Data
        {
            public int Balance;
        }
    }
}
