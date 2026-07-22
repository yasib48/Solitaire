using System;
using System.Collections.Generic;
using NUnit.Framework;
using Solitaire.Models;
using Solitaire.Services;

namespace Solitaire.Tests
{
    [TestFixture]
    public class SolvableDealSearchTest
    {
        [Test]
        public void FindsACompleteSolutionForKnownDeal()
        {
            var cards = CreateShuffledDeck(12345);

            var result = SolvableDealSearch.Solve(cards, false, 40000);

            Assert.That(result.IsSolved, Is.True);
            Assert.That(result.Moves, Is.GreaterThan(0));
            Assert.That(result.SearchedStates, Is.LessThanOrEqualTo(40000));
        }

        [Test]
        public void RejectsAnIncompleteDeck()
        {
            var cards = CreateShuffledDeck(12345);
            cards.RemoveAt(cards.Count - 1);

            var result = SolvableDealSearch.Solve(cards, false, 40000);

            Assert.That(result.IsSolved, Is.False);
        }

        private static List<Card> CreateShuffledDeck(int seed)
        {
            var cards = new List<Card>(52);
            foreach (Card.Suits suit in Enum.GetValues(typeof(Card.Suits)))
            foreach (Card.Types type in Enum.GetValues(typeof(Card.Types)))
            {
                var card = new Card();
                card.Init(suit, type);
                cards.Add(card);
            }

            var random = new Random(seed);
            for (var i = cards.Count - 1; i > 0; i--)
            {
                var other = random.Next(i + 1);
                (cards[i], cards[other]) = (cards[other], cards[i]);
            }
            return cards;
        }
    }
}
