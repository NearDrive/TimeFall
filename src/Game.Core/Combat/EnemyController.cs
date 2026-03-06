using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Game;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Combat;

public static class EnemyController
{
    public static EnemyTurnResult ExecuteTurn(
        CombatState combatState,
        GameRng rng,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        var mutable = combatState;
        var currentRng = rng;
        var events = new List<GameEvent>();
        var actionCount = 0;

        while (true)
        {
            if (mutable.Enemy.Deck.Hand.Count == 0)
            {
                var drawResult = DrawEnemyCards(mutable, currentRng, 1);
                mutable = drawResult.CombatState;
                currentRng = drawResult.Rng;
                events.AddRange(drawResult.Events);
                events.AddRange(drawResult.DrawnCards.Select(c => new CardDrawn(c)));

                if (drawResult.DrawnCards.Count == 0)
                {
                    break;
                }
            }

            var attackIndex = FindPlayableCardIndex(mutable.Enemy.Deck.Hand, cardDefinitions);
            if (attackIndex < 0)
            {
                break;
            }

            var attackCard = mutable.Enemy.Deck.Hand[attackIndex];
            mutable = mutable with
            {
                Enemy = mutable.Enemy with
                {
                    Deck = mutable.Enemy.Deck with
                    {
                        Hand = mutable.Enemy.Deck.Hand.RemoveAt(attackIndex),
                        DiscardPile = mutable.Enemy.Deck.DiscardPile.Add(attackCard),
                    },
                },
            };

            var resolution = CardEffectResolver.Resolve(mutable, attackCard, TurnOwner.Enemy, cardDefinitions);
            mutable = resolution.CombatState;
            events.AddRange(resolution.Events);
            actionCount++;
        }

        return new EnemyTurnResult(mutable, currentRng, events, actionCount);
    }

    private static DrawResult DrawEnemyCards(CombatState combatState, GameRng rng, int count)
    {
        var mutable = combatState;
        var currentRng = rng;
        var drawn = new List<CardInstance>();
        var events = new List<GameEvent>();

        for (var i = 0; i < count; i++)
        {
            if (mutable.Enemy.Deck.DrawPile.Count == 0)
            {
                var cycleResult = DeckCycleSystem.EnsureDrawAvailable(mutable.Enemy.Deck, currentRng, mutable, out var cycleEvents);
                mutable = cycleResult.CombatState;
                currentRng = cycleResult.Rng;
                events.AddRange(cycleEvents);

                if (mutable.Enemy.Deck.DrawPile.Count == 0)
                {
                    break;
                }
            }

            var topCard = mutable.Enemy.Deck.DrawPile[0];
            mutable = mutable with
            {
                Enemy = mutable.Enemy with
                {
                    Deck = mutable.Enemy.Deck with
                    {
                        DrawPile = mutable.Enemy.Deck.DrawPile.RemoveAt(0),
                        Hand = mutable.Enemy.Deck.Hand.Add(topCard),
                    },
                },
            };
            drawn.Add(topCard);
        }

        return new DrawResult(mutable, currentRng, drawn, events);
    }

    private static int FindPlayableCardIndex(
        IReadOnlyList<CardInstance> hand,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        for (var i = 0; i < hand.Count; i++)
        {
            if (CardEffectResolver.HasResolvableEffects(hand[i], cardDefinitions))
            {
                return i;
            }
        }

        return -1;
    }
}

public sealed record EnemyTurnResult(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<GameEvent> Events,
    int ActionCount);
