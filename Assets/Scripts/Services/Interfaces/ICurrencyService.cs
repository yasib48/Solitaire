using UniRx;

namespace Solitaire.Services
{
    /// <summary>
    ///     The player's soft-currency wallet (coins). Persists between sessions.
    ///     Earning hooks are added later; this is the storage/spend backbone.
    /// </summary>
    public interface ICurrencyService
    {
        /// <summary>The current coin balance.</summary>
        IReadOnlyReactiveProperty<int> Balance { get; }

        /// <summary>Credits the wallet with the given (positive) amount.</summary>
        void Add(int amount);

        /// <summary>
        ///     Deducts the amount if the player can afford it.
        ///     Returns false (and changes nothing) when the balance is too low.
        /// </summary>
        bool TrySpend(int amount);
    }
}
