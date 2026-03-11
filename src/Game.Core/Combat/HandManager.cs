using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Core.Combat;

public static class HandManager
{
    public const int InitialHandSize = 5;

    public static DrawResult Draw(CombatState combatState, GameRng rng, int count)
    {
        var mutable = combatState;
        var currentRng = rng;
        var drawn = new List<CardInstance>();
        var events = new List<GameEvent>();

        for (var i = 0; i < count; i++)
        {
            if (mutable.Player.Deck.DrawPile.Count == 0)
            {
                var cycleResult = DeckCycleSystem.EnsureDrawAvailable(mutable.Player.Deck, currentRng, out var cycleEvents);
                mutable = mutable with
                {
                    Player = mutable.Player with
                    {
                        Deck = cycleResult.Deck,
                    },
                };
                currentRng = cycleResult.Rng;
                events.AddRange(cycleEvents);

                if (cycleEvents.Any(e => e is DeckReshuffled))
                {
                    var (fatigueState, fatigueEvents) = ApplyPlayerReshuffleFatigue(mutable);
                    mutable = fatigueState;
                    events.AddRange(fatigueEvents);
                }

                if (mutable.Player.Deck.DrawPile.Count == 0)
                {
                    break;
                }
            }

            var topCard = mutable.Player.Deck.DrawPile[0];
            mutable = mutable with
            {
                Player = mutable.Player with
                {
                    Deck = mutable.Player.Deck with
                    {
                        DrawPile = mutable.Player.Deck.DrawPile.RemoveAt(0),
                        Hand = mutable.Player.Deck.Hand.Add(topCard),
                    },
                },
            };
            drawn.Add(topCard);
        }

        var requiredDiscardCount = mutable.RequiredOverflowDiscardCount > 0
            ? mutable.RequiredOverflowDiscardCount
            : RequireOverflowDiscard(mutable);
        mutable = mutable with
        {
            NeedsOverflowDiscard = requiredDiscardCount > 0,
            RequiredOverflowDiscardCount = requiredDiscardCount,
            PendingDiscardIsFatigue = mutable.PendingDiscardIsFatigue,
        };

        return new DrawResult(mutable, currentRng, drawn, events);
    }

    public static int RequireOverflowDiscard(CombatState combatState, int maxHand = 7)
    {
        var handCount = combatState.Player.Deck.Hand.Count;
        return handCount > maxHand ? handCount - maxHand : 0;
    }

    public static CombatState ApplyDiscard(CombatState combatState, IReadOnlyCollection<int> indexes)
    {
        var mutable = combatState;
        foreach (var index in indexes.OrderDescending())
        {
            var card = mutable.Player.Deck.Hand[index];
            mutable = mutable with
            {
                Player = mutable.Player with
                {
                    Deck = mutable.Player.Deck with
                    {
                        Hand = mutable.Player.Deck.Hand.RemoveAt(index),
                        DiscardPile = mutable.Player.Deck.DiscardPile.Add(card),
                    },
                },
            };
        }

        var requiredDiscardCount = RequireOverflowDiscard(mutable);
        return mutable with
        {
            NeedsOverflowDiscard = requiredDiscardCount > 0,
            RequiredOverflowDiscardCount = requiredDiscardCount,
            PendingDiscardIsFatigue = false,
        };
    }

    private static (CombatState CombatState, IReadOnlyList<GameEvent> Events) ApplyPlayerReshuffleFatigue(CombatState combatState)
    {
        var mutable = combatState;
        var events = new List<GameEvent>();
        var missingCards = Math.Max(0, InitialHandSize - mutable.Player.Deck.Hand.Count);

        for (var i = 0; i < missingCards && mutable.Player.Deck.DrawPile.Count > 0; i++)
        {
            var topCard = mutable.Player.Deck.DrawPile[0];
            mutable = mutable with
            {
                Player = mutable.Player with
                {
                    Deck = mutable.Player.Deck with
                    {
                        DrawPile = mutable.Player.Deck.DrawPile.RemoveAt(0),
                        Hand = mutable.Player.Deck.Hand.Add(topCard),
                    },
                },
            };
            events.Add(new CardDrawn(topCard));
        }

        var fatigueDiscardCount = Math.Min(mutable.Player.Deck.ReshuffleCount, mutable.Player.Deck.Hand.Count);
        events.Add(new ReshuffleFatigueApplied(TurnOwner.Player, fatigueDiscardCount));

        return (mutable with
        {
            NeedsOverflowDiscard = fatigueDiscardCount > 0,
            RequiredOverflowDiscardCount = fatigueDiscardCount,
            PendingDiscardIsFatigue = fatigueDiscardCount > 0,
        }, events);
    }

}

public sealed record DrawResult(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<CardInstance> DrawnCards,
    IReadOnlyList<GameEvent> Events);
