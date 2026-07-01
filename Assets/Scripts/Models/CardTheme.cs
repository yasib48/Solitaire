using UnityEngine;

namespace Solitaire.Models
{
    [CreateAssetMenu(menuName = "Solitaire/Card Theme")]
    public class CardTheme : ScriptableObject
    {
        public string ThemeName;
        public Sprite Back;

        [Header("13 each: Ace, 2, 3, 4, 5, 6, 7, 8, 9, 10, J, Q, K")]
        public Sprite[] Spades;
        public Sprite[] Clubs;
        public Sprite[] Hearts;
        public Sprite[] Diamonds;

        public Sprite GetCard(Card.Suits suit, Card.Types type)
        {
            var cards = suit switch
            {
                Card.Suits.Spade => Spades,
                Card.Suits.Club => Clubs,
                Card.Suits.Heart => Hearts,
                Card.Suits.Diamond => Diamonds,
                _ => null
            };
            var i = (int)type;
            return cards != null && i < cards.Length ? cards[i] : null;
        }
    }
}
