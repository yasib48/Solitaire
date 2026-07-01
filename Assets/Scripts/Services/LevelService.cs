using System;
using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     Persistent player level, backed by <see cref="IStorageService" />.
    ///     New players start at <see cref="StartLevel" />; every finished game
    ///     bumps the level by one.
    /// </summary>
    public class LevelService : ILevelService
    {
        private const string StorageKey = "Level";
        private const int StartLevel = 1;

        private readonly IStorageService _storageService;
        private readonly IntReactiveProperty _level = new();

        public LevelService(IStorageService storageService)
        {
            _storageService = storageService;

            var data = _storageService.Load<Data>(StorageKey);
            if (data == null)
            {
                _level.Value = StartLevel;
                Persist();
            }
            else
            {
                _level.Value = Math.Max(StartLevel, data.Level);
            }
        }

        public IReadOnlyReactiveProperty<int> Level => _level;

        public void Increment()
        {
            _level.Value += 1;
            Persist();
        }

        private void Persist()
        {
            _storageService.Save(StorageKey, new Data { Level = _level.Value });
        }

        [Serializable]
        private class Data
        {
            public int Level;
        }
    }
}
