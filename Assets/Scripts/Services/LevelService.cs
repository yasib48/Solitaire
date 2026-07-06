using System;
using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     Persistent player level and XP, backed by <see cref="IStorageService" />.
    ///     Finished games grant XP made of three parts: a flat base, a
    ///     consecutive-win combo bonus, and a best-score bonus.
    /// </summary>
    public class LevelService : ILevelService
    {
        private const string StorageKey = "Level";
        private const int StartLevel = 1;

        // XP required to advance a level: level 1 -> 2 needs 10, then every
        // higher level needs 20 * (level - 1) => 20, 40, 60, 80, 100, 120 ...
        private const int FirstLevelRequirement = 10;
        private const int RequiredXpPerLevel = 20;

        private const int BaseRewardCoins = 10;
        private const int RewardCoinsStep = 2;
        private const int RewardCoinsMilestoneBonus = 5;

        // XP sources.
        private const int BaseWinXp = 10;          // every finished game
        private const int ComboXpStep = 5;         // extra per consecutive win (2nd:+5, 3rd:+10 ...)
        private const int BestScoreXp = 10;        // beating the best score
        private const double ComboWindowHours = 1.0; // consecutive wins must be within this window

        private readonly IStorageService _storageService;
        private readonly IntReactiveProperty _level = new();
        private readonly IntReactiveProperty _experience = new();
        private readonly IntReactiveProperty _requiredExperience = new();

        private int _combo;
        private long _lastWinTicks;
        private int _bestScore;
        private int _bestTimeSeconds;
        private int _bestMoves;

        public LevelService(IStorageService storageService)
        {
            _storageService = storageService;

            var data = _storageService.Load<Data>(StorageKey);
            if (data == null)
            {
                _level.Value = StartLevel;
                _experience.Value = 0;
                _requiredExperience.Value = GetRequiredExperience(_level.Value);
                Persist();
            }
            else
            {
                _level.Value = Math.Max(StartLevel, data.Level);
                _experience.Value = Math.Max(0, data.Experience);
                _requiredExperience.Value = GetRequiredExperience(_level.Value);
                _combo = Math.Max(0, data.Combo);
                _lastWinTicks = data.LastWinTicks;
                _bestScore = Math.Max(0, data.BestScore);
                _bestTimeSeconds = Math.Max(0, data.BestTimeSeconds);
                _bestMoves = Math.Max(0, data.BestMoves);
            }
        }

        public IReadOnlyReactiveProperty<int> Level => _level;
        public IReadOnlyReactiveProperty<int> Experience => _experience;
        public IReadOnlyReactiveProperty<int> RequiredExperience => _requiredExperience;

        public LevelProgressResult AddCompletedGame(int points, int moves, int timeSeconds)
        {
            var previousLevel = _level.Value;

            // --- Combo: consecutive wins within the time window ---
            var now = DateTime.UtcNow;
            var withinWindow = _combo > 0
                && (now - new DateTime(_lastWinTicks, DateTimeKind.Utc)).TotalHours <= ComboWindowHours;
            _combo = withinWindow ? _combo + 1 : 1;
            _lastWinTicks = now.Ticks;
            var comboXp = ComboXpStep * (_combo - 1);

            // --- Best score bonus ---
            var newBestScore = points > _bestScore;
            var bestScoreXp = newBestScore ? BestScoreXp : 0;
            if (newBestScore)
                _bestScore = points;

            // Track best (lowest) time and moves for the results table.
            if (timeSeconds > 0 && (_bestTimeSeconds == 0 || timeSeconds < _bestTimeSeconds))
                _bestTimeSeconds = timeSeconds;
            if (moves > 0 && (_bestMoves == 0 || moves < _bestMoves))
                _bestMoves = moves;

            var gainedXp = BaseWinXp + comboXp + bestScoreXp;
            _experience.Value += gainedXp;

            var leveledUp = false;
            while (_experience.Value >= _requiredExperience.Value)
            {
                _experience.Value -= _requiredExperience.Value;
                _level.Value += 1;
                _requiredExperience.Value = GetRequiredExperience(_level.Value);
                leveledUp = true;
            }

            Persist();
            return new LevelProgressResult
            {
                LeveledUp = leveledUp,
                IsFirstLevelUp = leveledUp && previousLevel == StartLevel,
                Level = _level.Value,
                Experience = _experience.Value,
                RequiredExperience = _requiredExperience.Value,
                ExperienceGained = gainedXp,
                RewardCoins = leveledUp ? GetRewardCoinsForLevel(_level.Value) : 0,
                BaseXp = BaseWinXp,
                ComboXp = comboXp,
                ComboCount = _combo,
                BestScoreXp = bestScoreXp,
                NewBestScore = newBestScore,
                BestScore = _bestScore,
                BestTimeSeconds = _bestTimeSeconds,
                BestMoves = _bestMoves
            };
        }

        public void ResetCombo()
        {
            if (_combo == 0)
                return;

            _combo = 0;
            Persist();
        }

        public int GetRequiredExperienceForLevel(int level)
        {
            return GetRequiredExperience(level);
        }

        private static int GetRequiredExperience(int level)
        {
            if (level <= StartLevel)
                return FirstLevelRequirement;

            return RequiredXpPerLevel * (level - StartLevel);
        }

        public int GetRewardCoinsForLevel(int level)
        {
            var steps = Math.Max(0, level - StartLevel);
            return BaseRewardCoins + steps * RewardCoinsStep + steps / 5 * RewardCoinsMilestoneBonus;
        }

        private void Persist()
        {
            _storageService.Save(StorageKey, new Data
            {
                Level = _level.Value,
                Experience = _experience.Value,
                Combo = _combo,
                LastWinTicks = _lastWinTicks,
                BestScore = _bestScore,
                BestTimeSeconds = _bestTimeSeconds,
                BestMoves = _bestMoves
            });
        }

        [Serializable]
        private class Data
        {
            public int Level;
            public int Experience;
            public int Combo;
            public long LastWinTicks;
            public int BestScore;
            public int BestTimeSeconds;
            public int BestMoves;
        }
    }
}
