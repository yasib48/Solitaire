using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     Tracks how many magic wand charges the player owns and persists the
    ///     count between sessions. New players receive a small free allowance on
    ///     their very first launch.
    /// </summary>
    public interface IMagicWandService
    {
        /// <summary>The number of charges currently available.</summary>
        IReadOnlyReactiveProperty<int> Count { get; }

        /// <summary>
        ///     Consumes a single charge. Returns false when none are left.
        /// </summary>
        bool TryUse();

        /// <summary>
        ///     Grants additional charges (e.g. after watching a rewarded ad).
        /// </summary>
        void Add(int amount);
    }
}
