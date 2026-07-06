using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     Tracks the player's current level and persists it between sessions.
    ///     The level advances once per finished game.
    /// </summary>
    public interface ILevelService
    {
        /// <summary>The player's current level.</summary>
        IReadOnlyReactiveProperty<int> Level { get; }

        /// <summary>Current XP inside the active level.</summary>
        IReadOnlyReactiveProperty<int> Experience { get; }

        /// <summary>XP required to reach the next level.</summary>
        IReadOnlyReactiveProperty<int> RequiredExperience { get; }

        /// <summary>Adds end-of-game XP and returns the full breakdown/records.</summary>
        LevelProgressResult AddCompletedGame(int points, int moves, int timeSeconds);

        /// <summary>Breaks the consecutive-win combo (e.g. a game was abandoned).</summary>
        void ResetCombo();

        /// <summary>XP needed to clear the given level. Lets the UI animate multi-level rollovers.</summary>
        int GetRequiredExperienceForLevel(int level);

        /// <summary>Coins granted for claiming the next level-up reward at this level.</summary>
        int GetRewardCoinsForLevel(int level);
    }

    public class LevelProgressResult
    {
        public bool LeveledUp;
        public bool IsFirstLevelUp;
        public int Level;
        public int Experience;
        public int RequiredExperience;
        public int ExperienceGained;
        public int RewardCoins;

        // XP breakdown for the sequenced level-bar reveal.
        public int BaseXp;
        public int ComboXp;
        public int ComboCount;
        public int BestScoreXp;
        public bool NewBestScore;

        // Records for the results table (updated to include this game).
        public int BestScore;
        public int BestTimeSeconds;
        public int BestMoves;
    }
}
