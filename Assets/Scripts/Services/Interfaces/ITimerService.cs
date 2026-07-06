namespace Solitaire.Services
{
    /// <summary>
    ///     Elapsed play time for the current deal. Driven by the HUD each frame
    ///     while playing and read at game end for the results table.
    /// </summary>
    public interface ITimerService
    {
        /// <summary>Seconds elapsed in the current game.</summary>
        float Elapsed { get; }

        /// <summary>Whole seconds elapsed, for display and records.</summary>
        int ElapsedSeconds { get; }

        void Reset();
        void Tick(float deltaTime);
    }
}
