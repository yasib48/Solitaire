namespace Solitaire.Presenters
{
    /// <summary>
    ///     Reward-fly animation for the end-of-game stars. Behaves exactly like
    ///     <see cref="CoinFlyRewardPresenter" /> (scatter from the centre, then fly
    ///     to a target) but exists as its own component type so the level-bar star
    ///     fly and the coin-counter coin fly can each live on the same Canvas
    ///     GameObject without their GetComponent lookups colliding.
    /// </summary>
    public class StarFlyRewardPresenter : CoinFlyRewardPresenter
    {
    }
}
