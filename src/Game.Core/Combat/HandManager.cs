using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Core.Combat;

public static class HandManager
{
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
                var cycleResult = DeckCycleSystem.EnsureDrawAvailable(mutable.Player.Deck, currentRng, mutable, out var cycleEvents);
                mutable = cycleResult.CombatState;
                currentRng = cycleResult.Rng;
                events.AddRange(cycleEvents);

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

        var requiredDiscardCount = RequireOverflowDiscard(mutable);
        mutable = mutable with
        {
            NeedsOverflowDiscard = requiredDiscardCount > 0,
            RequiredOverflowDiscardCount = requiredDiscardCount,
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
        };
    }

}

public sealed record DrawResult(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<CardInstance> DrawnCards,
    IReadOnlyList<GameEvent> Events);
