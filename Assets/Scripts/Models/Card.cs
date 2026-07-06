using System;
using UniRx;
using UnityEngine;

namespace Solitaire.Models
{
    public class Card
    {
        public enum Suits : byte
        {
            Spade,
            Club,
            Heart,
            Diamond
        }

        public enum Types : byte
        {
            Ace,
            Two,
            Three,
            Four,
            Five,
            Six,
            Seven,
            Eight,
            Nine,
            Ten,
            Jack,
            Queen,
            King
        }

public Card()
        {
            IsFaceUp = new BoolReactiveProperty();
            Position = new Vector3ReactiveProperty();
            Order = new IntReactiveProperty();
            FrontOrderOverride = new ReactiveProperty<int?>();
            Alpha = new FloatReactiveProperty(1f);
            Dim = new FloatReactiveProperty(0f);
            Highlight = new FloatReactiveProperty(0f);
            IsVisible = new BoolReactiveProperty(true);
            IsInteractable = new BoolReactiveProperty(true);
            Scale = new FloatReactiveProperty(1f);
        }

        public Suits Suit { get; private set; }
        public Types Type { get; private set; }
        public BoolReactiveProperty IsFaceUp { get; }
        public Vector3ReactiveProperty Position { get; }
        public IntReactiveProperty Order { get; }

        // When set, forces the card's front-face sorting order to this exact
        // value regardless of Order (e.g. the magic wand's centre reveal,
        // which needs to stay on top through its whole flight home). Null
        // means "no override, use the normal Order-based sorting".
        public ReactiveProperty<int?> FrontOrderOverride { get; }

        public FloatReactiveProperty Alpha { get; }
        public FloatReactiveProperty Dim { get; }

        // 0 = normal, 1 = fully highlighted (yellow) - the magic wand lights up
        // the cards it selects.
        public FloatReactiveProperty Highlight { get; }

        public BoolReactiveProperty IsVisible { get; }
        public BoolReactiveProperty IsInteractable { get; }

        // Visual scale multiplier applied on top of the card's authored scale.
        // Normally 1; the magic wand pushes it up for its dramatic centre reveal.
        public FloatReactiveProperty Scale { get; }

        public Pile Pile { get; set; }
        public Vector3 DragOrigin { get; set; }
        public Vector3 DragOffset { get; set; }
        public int OrderToRestore { get; set; }
        public bool IsDragged { get; set; }

        public bool IsInPile => Pile != null;
        public bool IsOnBottom => Pile.BottomCard() == this;
        public bool IsOnTop => Pile.TopCard() == this;

        public bool IsMoveable =>
            IsInPile
            && ((Pile.IsWaste && IsOnTop && IsFaceUp.Value) || (!Pile.IsWaste && IsFaceUp.Value));

        public bool IsDrawable => IsInPile && Pile.IsStock && IsOnTop && !IsFaceUp.Value;

        public void Init(Suits suit, Types type)
        {
            Suit = suit;
            Type = type;
        }

public void Reset(Vector3 position)
        {
            Pile = null;
            IsFaceUp.Value = false;
            Position.Value = position;
            Order.Value = 0;
            FrontOrderOverride.Value = null;
            Alpha.Value = 1f;
            Dim.Value = 0f;
            Highlight.Value = 0f;
            IsVisible.Value = true;
            IsInteractable.Value = true;
            Scale.Value = 1f;
            DragOrigin = Vector3.zero;
            DragOffset = Vector3.zero;
            OrderToRestore = 0;
            IsDragged = false;
        }

        public int GetValue()
        {
            if (Type == Types.Jack || Type == Types.Queen || Type == Types.King)
                return 10;

            if (Type == Types.Ace)
                return 11;

            return (int)Type + 1;
        }

        public void Flip()
        {
            IsFaceUp.Value = !IsFaceUp.Value;
        }

        public override string ToString()
        {
            return $"{Suit} {Type}";
        }

        [Serializable]
        public class Config
        {
            public float AnimationDuration = 0.5f;
            public CardTheme Theme;

            // Legacy composite rendering — used when Theme is null
            public Color[] Colors;
            public Sprite[] SuitSprites;
            public Sprite[] TypeSprites;
        }
    }
}
