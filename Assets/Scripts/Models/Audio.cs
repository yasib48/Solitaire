using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solitaire.Models
{
    public static class Audio
    {
        public const string Music = "Music";
        public const string SfxShuffle = "Shuffle";
        public const string SfxDeal = "Deal";
        public const string SfxDraw = "Draw";
        public const string SfxHint = "hint_button";
        public const string SfxClick = "Click";
        public const string SfxError = "Error";
        public const string SfxUndo = "undo";
        public const string SfxMagicWand = "MagicWand";

        [Serializable]
        public class Config
        {
            public List<AudioClip> AudioClips;
        }
    }
}