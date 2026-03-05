using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Core.Combat;

public static class HandManager
{
    public static DrawResult Draw(CombatState combatState, GameRng rng, int count)
    {
        var mutable = Clone(combatState);
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
            mutable.Player.Deck.DrawPile.RemoveAt(0);
            mutable.Player.Deck.Hand.Add(topCard);
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
        var mutable = Clone(combatState);
        foreach (var index in indexes.OrderDescending())
        {
            var card = mutable.Player.Deck.Hand[index];
            mutable.Player.Deck.Hand.RemoveAt(index);
            mutable.Player.Deck.DiscardPile.Add(card);
        }

        var requiredDiscardCount = RequireOverflowDiscard(mutable);
        return mutable with
        {
            NeedsOverflowDiscard = requiredDiscardCount > 0,
            RequiredOverflowDiscardCount = requiredDiscardCount,
        };
    }

    private static CombatState Clone(CombatState combatState)
    {
        var playerDeck = combatState.Player.Deck;
        var enemyDeck = combatState.Enemy.Deck;

        return combatState with
        {
            Player = combatState.Player with
            {
                Resources = new Dictionary<ResourceType, int>(combatState.Player.Resources),
                Deck = playerDeck with
                {
                    DrawPile = new List<CardInstance>(playerDeck.DrawPile),
                    Hand = new List<CardInstance>(playerDeck.Hand),
                    DiscardPile = new List<CardInstance>(playerDeck.DiscardPile),
                    BurnPile = new List<CardInstance>(playerDeck.BurnPile),
                },
            },
            Enemy = combatState.Enemy with
            {
                Resources = new Dictionary<ResourceType, int>(combatState.Enemy.Resources),
                Deck = enemyDeck with
                {
                    DrawPile = new List<CardInstance>(enemyDeck.DrawPile),
                    Hand = new List<CardInstance>(enemyDeck.Hand),
                    DiscardPile = new List<CardInstance>(enemyDeck.DiscardPile),
                    BurnPile = new List<CardInstance>(enemyDeck.BurnPile),
                },
            },
        };
    }
}

public sealed record DrawResult(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<CardInstance> DrawnCards,
    IReadOnlyList<GameEvent> Events);
