using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Solitaire.Services
{
    /// <summary>
    ///     Visual effects for the magic wand's dramatic reveal: a full-screen dim,
    ///     a golden glow behind the lifted card and sparkle bursts. Implemented by a
    ///     scene presenter so the <see cref="Solitaire.Models.Game" /> model can drive
    ///     the sequence without knowing about the camera or renderers.
    /// </summary>
    public interface IMagicWandVfx
    {
        /// <summary>Scale the revealed card grows to while held in the centre.</summary>
        float RevealScale { get; }

        /// <summary>How long (ms) a card lingers, big and glowing, in the centre.</summary>
        int HoldMs { get; }

        /// <summary>How dim (0-1) bystander cards go while the wand is active.</summary>
        float CardDimAmount { get; }

        /// <summary>Delay (ms) before the next card starts lifting, when revealing more than one.</summary>
        int LiftStaggerMs { get; }

        /// <summary>Fade the board-dimming overlay in (dim = true) or out.</summary>
        UniTask FadeAsync(bool dim);

        /// <summary>
        ///     Cinematic intro: the wand icon flies to the centre and grows, then
        ///     shrinks and hops over each target position with a little rotation,
        ///     as if picking those cards, before vanishing. onReachTarget fires
        ///     with the index each time the wand lands on a target.
        /// </summary>
        UniTask WaveWandAsync(IReadOnlyList<Vector3> targets, System.Action<int> onReachTarget);

        /// <summary>World position where the i-th revealed card should be held.</summary>
        Vector3 GetRevealSlot(int index, int count);

        /// <summary>Spawn a pulsing glow at a world position; returns a handle to hide it.</summary>
        Transform ShowGlow(Vector3 worldPos);

        /// <summary>Fade out and destroy a glow returned by <see cref="ShowGlow" />.</summary>
        void HideGlow(Transform glow);

        /// <summary>One-shot golden sparkle burst at a world position.</summary>
        void Sparkle(Vector3 worldPos);
    }
}
