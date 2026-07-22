using System;
using System.Collections.Generic;
using Solitaire.Models;

namespace Solitaire.Services
{
    /// <summary>
    /// Plays a shuffled Klondike deal in memory. A deal is accepted only when
    /// search reaches all four completed foundations. A successful result is
    /// therefore a real solution proof, not an opening-layout estimate.
    /// </summary>
    public static class SolvableDealSearch
    {
        public sealed class Result
        {
            public bool IsSolved;
            public int Moves;
            public int StockPasses;
            public int TableauTransfers;
            public int FoundationBacktracks;
            public int LongestStall;
            public int Decisions;
            public int SearchedStates;

            // Only orders deals that have already been proven solvable.
            public bool IsEasierThan(Result other)
            {
                if (other == null) return true;
                if (FoundationBacktracks != other.FoundationBacktracks)
                    return FoundationBacktracks < other.FoundationBacktracks;
                if (StockPasses != other.StockPasses)
                    return StockPasses < other.StockPasses;
                if (LongestStall != other.LongestStall)
                    return LongestStall < other.LongestStall;
                if (TableauTransfers != other.TableauTransfers)
                    return TableauTransfers < other.TableauTransfers;
                if (Decisions != other.Decisions)
                    return Decisions < other.Decisions;
                return Moves < other.Moves;
            }
        }

        private const int CardCount = 52;
        private const int StockCount = 24;
        private static readonly int[] FaceUpIndices = { 51, 49, 46, 42, 37, 31, 24 };

        public static Result Solve(IList<Card> cards, bool drawThree, int stateLimit)
        {
            if (cards == null || cards.Count != CardCount || stateLimit <= 0)
                return new Result();

            var open = new StateHeap();
            var visited = new HashSet<ulong>();
            var initial = CreateInitialState(cards);
            open.Push(initial, Priority(initial));

            var searched = 0;
            while (open.Count > 0 && searched < stateLimit)
            {
                var state = open.Pop();
                if (!visited.Add(Hash(state))) continue;
                searched++;

                if (FoundationTotal(state) == CardCount)
                    return ToResult(state, searched);

                var next = GenerateMoves(state, drawThree);
                var isDecision = next.Count > 1;
                for (var i = 0; i < next.Count; i++)
                {
                    if (isDecision) next[i].Decisions++;
                    open.Push(next[i], Priority(next[i]));
                }
            }
            return new Result { SearchedStates = searched };
        }

        private static State CreateInitialState(IList<Card> cards)
        {
            var state = new State();
            for (var i = 0; i < StockCount; i++) state.Stock.Add(CardId(cards[i]));

            for (var column = 0; column < 7; column++)
            {
                var faceUp = FaceUpIndices[column];
                for (var offset = column; offset >= 0; offset--)
                    state.Tableau[column].Add(CardId(cards[faceUp + offset]));
                state.Hidden[column] = column;
            }
            return state;
        }

        private static List<State> GenerateMoves(State state, bool drawThree)
        {
            var result = new List<State>(24);

            if (state.Waste.Count > 0)
            {
                var card = Top(state.Waste);
                if (CanMoveToFoundation(state, card))
                {
                    var next = state.Copy();
                    next.Waste.RemoveAt(next.Waste.Count - 1);
                    next.Foundation[Suit(card)]++;
                    Advance(next, true);
                    result.Add(next);
                }
            }

            for (var source = 0; source < 7; source++)
            {
                var pile = state.Tableau[source];
                if (pile.Count == 0) continue;
                var card = Top(pile);
                if (!CanMoveToFoundation(state, card)) continue;

                var next = state.Copy();
                next.Tableau[source].RemoveAt(next.Tableau[source].Count - 1);
                next.Foundation[Suit(card)]++;
                RevealIfNeeded(next, source);
                Advance(next, true);
                result.Add(next);
            }

            if (state.Waste.Count > 0)
            {
                var card = Top(state.Waste);
                for (var target = 0; target < 7; target++)
                {
                    if (!CanMoveToTableau(card, state.Tableau[target])) continue;
                    var next = state.Copy();
                    next.Waste.RemoveAt(next.Waste.Count - 1);
                    next.Tableau[target].Add(card);
                    Advance(next, false);
                    result.Add(next);
                }
            }

            for (var source = 0; source < 7; source++)
            {
                var pile = state.Tableau[source];
                for (var start = state.Hidden[source]; start < pile.Count; start++)
                {
                    var card = pile[start];
                    for (var target = 0; target < 7; target++)
                    {
                        if (source == target || !CanMoveToTableau(card, state.Tableau[target]))
                            continue;
                        if (start == 0 && Rank(card) == 12 && state.Tableau[target].Count == 0)
                            continue;

                        var next = state.Copy();
                        var moving = next.Tableau[source].GetRange(start, pile.Count - start);
                        next.Tableau[source].RemoveRange(start, pile.Count - start);
                        next.Tableau[target].AddRange(moving);
                        var revealed = RevealIfNeeded(next, source);
                        next.TableauTransfers++;
                        Advance(next, revealed);
                        result.Add(next);
                    }
                }
            }

            // The real game permits foundation-to-tableau moves. Keeping them
            // here prevents valid deals that require temporary backtracking from
            // being incorrectly rejected.
            for (var suit = 0; suit < 4; suit++)
            {
                if (state.Foundation[suit] == 0) continue;
                var card = (byte)(suit * 13 + state.Foundation[suit] - 1);
                for (var target = 0; target < 7; target++)
                {
                    if (!CanMoveToTableau(card, state.Tableau[target])) continue;
                    var next = state.Copy();
                    next.Foundation[suit]--;
                    next.Tableau[target].Add(card);
                    next.FoundationBacktracks++;
                    Advance(next, false);
                    result.Add(next);
                }
            }

            if (state.Stock.Count > 0)
            {
                var next = state.Copy();
                var count = drawThree ? Math.Min(3, next.Stock.Count) : 1;
                for (var i = 0; i < count; i++)
                {
                    var top = Top(next.Stock);
                    next.Stock.RemoveAt(next.Stock.Count - 1);
                    next.Waste.Add(top);
                }
                Advance(next, false);
                result.Add(next);
            }
            else if (state.Waste.Count > 0)
            {
                var next = state.Copy();
                while (next.Waste.Count > 0)
                {
                    var top = Top(next.Waste);
                    next.Waste.RemoveAt(next.Waste.Count - 1);
                    next.Stock.Add(top);
                }
                next.StockPasses++;
                Advance(next, false);
                result.Add(next);
            }
            return result;
        }

        private static bool RevealIfNeeded(State state, int column)
        {
            if (state.Hidden[column] == 0 || state.Tableau[column].Count != state.Hidden[column])
                return false;
            state.Hidden[column]--;
            return true;
        }

        private static void Advance(State state, bool progress)
        {
            state.Moves++;
            if (progress)
            {
                state.Stall = 0;
                return;
            }
            state.Stall++;
            if (state.Stall > state.LongestStall) state.LongestStall = state.Stall;
        }

        private static bool CanMoveToFoundation(State state, byte card)
        {
            return Rank(card) == state.Foundation[Suit(card)];
        }

        private static bool CanMoveToTableau(byte card, List<byte> target)
        {
            if (target.Count == 0) return Rank(card) == 12;
            var top = Top(target);
            return Rank(top) == Rank(card) + 1 && IsBlack(top) != IsBlack(card);
        }

        // Queue ordering makes the proof search fast; it never decides whether
        // a deal is accepted. Only completing all 52 foundation cards does that.
        private static int Priority(State state)
        {
            var hidden = 0;
            var empty = 0;
            for (var i = 0; i < 7; i++)
            {
                hidden += state.Hidden[i];
                if (state.Tableau[i].Count == 0) empty++;
            }
            return FoundationTotal(state) * 1000 - hidden * 140 + empty * 20
                - state.FoundationBacktracks * 15 - state.StockPasses * 4 - state.Moves;
        }

        private static Result ToResult(State state, int searched)
        {
            return new Result
            {
                IsSolved = true,
                Moves = state.Moves,
                StockPasses = state.StockPasses,
                TableauTransfers = state.TableauTransfers,
                FoundationBacktracks = state.FoundationBacktracks,
                LongestStall = state.LongestStall,
                Decisions = state.Decisions,
                SearchedStates = searched
            };
        }

        private static int FoundationTotal(State state)
        {
            return state.Foundation[0] + state.Foundation[1] + state.Foundation[2] + state.Foundation[3];
        }

        private static byte Top(List<byte> pile) => pile[pile.Count - 1];
        private static byte CardId(Card card) => (byte)((int)card.Suit * 13 + (int)card.Type);
        private static int Suit(byte card) => card / 13;
        private static int Rank(byte card) => card % 13;
        private static bool IsBlack(byte card) => Suit(card) < 2;

        private static ulong Hash(State state)
        {
            const ulong prime = 1099511628211UL;
            var hash = 1469598103934665603UL;
            for (var i = 0; i < 4; i++) Mix(ref hash, (byte)(state.Foundation[i] + 1), prime);
            for (var i = 0; i < 7; i++)
            {
                Mix(ref hash, (byte)(state.Hidden[i] + 64), prime);
                var pile = state.Tableau[i];
                for (var j = 0; j < pile.Count; j++) Mix(ref hash, (byte)(pile[j] + 1), prime);
                Mix(ref hash, 0, prime);
            }
            for (var i = 0; i < state.Stock.Count; i++) Mix(ref hash, (byte)(state.Stock[i] + 1), prime);
            Mix(ref hash, 63, prime);
            for (var i = 0; i < state.Waste.Count; i++) Mix(ref hash, (byte)(state.Waste[i] + 1), prime);
            return hash;
        }

        private static void Mix(ref ulong hash, byte value, ulong prime)
        {
            hash ^= value;
            hash *= prime;
        }

        private sealed class State
        {
            public readonly List<byte>[] Tableau =
            {
                new List<byte>(13), new List<byte>(13), new List<byte>(13),
                new List<byte>(13), new List<byte>(13), new List<byte>(13), new List<byte>(13)
            };
            public readonly int[] Hidden = new int[7];
            public readonly List<byte> Stock = new List<byte>(24);
            public readonly List<byte> Waste = new List<byte>(24);
            public readonly int[] Foundation = new int[4];
            public int Moves;
            public int StockPasses;
            public int TableauTransfers;
            public int FoundationBacktracks;
            public int Stall;
            public int LongestStall;
            public int Decisions;

            public State Copy()
            {
                var copy = new State
                {
                    Moves = Moves,
                    StockPasses = StockPasses,
                    TableauTransfers = TableauTransfers,
                    FoundationBacktracks = FoundationBacktracks,
                    Stall = Stall,
                    LongestStall = LongestStall,
                    Decisions = Decisions
                };
                for (var i = 0; i < 7; i++)
                {
                    copy.Tableau[i].AddRange(Tableau[i]);
                    copy.Hidden[i] = Hidden[i];
                }
                copy.Stock.AddRange(Stock);
                copy.Waste.AddRange(Waste);
                Array.Copy(Foundation, copy.Foundation, Foundation.Length);
                return copy;
            }
        }

        private sealed class StateHeap
        {
            private readonly List<Entry> _items = new List<Entry>();
            public int Count => _items.Count;

            public void Push(State state, int priority)
            {
                var entry = new Entry(state, priority);
                var index = _items.Count;
                _items.Add(entry);
                while (index > 0)
                {
                    var parent = (index - 1) / 2;
                    if (_items[parent].Priority >= priority) break;
                    _items[index] = _items[parent];
                    index = parent;
                }
                _items[index] = entry;
            }

            public State Pop()
            {
                var root = _items[0].State;
                var last = _items[_items.Count - 1];
                _items.RemoveAt(_items.Count - 1);
                if (_items.Count == 0) return root;

                var index = 0;
                while (true)
                {
                    var left = index * 2 + 1;
                    if (left >= _items.Count) break;
                    var right = left + 1;
                    var child = right < _items.Count && _items[right].Priority > _items[left].Priority
                        ? right : left;
                    if (_items[child].Priority <= last.Priority) break;
                    _items[index] = _items[child];
                    index = child;
                }
                _items[index] = last;
                return root;
            }

            private readonly struct Entry
            {
                public readonly State State;
                public readonly int Priority;
                public Entry(State state, int priority)
                {
                    State = state;
                    Priority = priority;
                }
            }
        }
    }
}
