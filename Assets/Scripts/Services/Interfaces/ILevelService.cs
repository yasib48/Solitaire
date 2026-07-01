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

        /// <summary>Advances the level by one and persists the new value.</summary>
        void Increment();
    }
}
