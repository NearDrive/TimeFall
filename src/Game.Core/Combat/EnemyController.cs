using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Game;
using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Combat;

public static class EnemyController
{
    public const int DefaultEnemyTurnDrawCount = 1;

    public static DrawResult DrawAtTurnStart(
        CombatState combatState,
        GameRng rng,
        int count = DefaultEnemyTurnDrawCount)
    {
        var mutable = combatState;
        var currentRng = rng;
        var drawn = new List<CardInstance>();
        var events = new List<GameEvent>();

        for (var enemyIndex = 0; enemyIndex < mutable.Enemies.Count; enemyIndex++)
        {
            if (mutable.Enemies[enemyIndex].HP <= 0)
            {
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                if (mutable.Enemies[enemyIndex].Deck.DrawPile.Count == 0)
                {
                    var cycleResult = DeckCycleSystem.EnsureDrawAvailable(mutable.Enemies[enemyIndex].Deck, currentRng, out var cycleEvents);
                    mutable = mutable with { Enemies = mutable.Enemies.SetItem(enemyIndex, mutable.Enemies[enemyIndex] with { Deck = cycleResult.Deck }) };
                    currentRng = cycleResult.Rng;
                    events.AddRange(cycleEvents);

                    if (cycleEvents.Any(e => e is DeckReshuffled))
                    {
                        var fatigue = ApplyEnemyReshuffleFatigue(mutable, enemyIndex);
                        mutable = fatigue.CombatState;
                        events.AddRange(fatigue.Events);
                    }

                    if (mutable.Enemies[enemyIndex].Deck.DrawPile.Count == 0)
                    {
                        break;
                    }
                }

                var topCard = mutable.Enemies[enemyIndex].Deck.DrawPile[0];
                mutable = mutable with
                {
                    Enemies = mutable.Enemies.SetItem(enemyIndex, mutable.Enemies[enemyIndex] with
                    {
                        Deck = mutable.Enemies[enemyIndex].Deck with
                        {
                            DrawPile = mutable.Enemies[enemyIndex].Deck.DrawPile.RemoveAt(0),
                            Hand = mutable.Enemies[enemyIndex].Deck.Hand.Add(topCard),
                        },
                    }),
                };
                drawn.Add(topCard);
            }
        }

        return new DrawResult(mutable, currentRng, drawn, events);
    }

    public static EnemyTurnResult ExecuteTurn(
        CombatState combatState,
        GameRng rng,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        var mutable = combatState;
        var currentRng = rng;
        var events = new List<GameEvent>();
        var actionCount = 0;

        for (var enemyIndex = 0; enemyIndex < mutable.Enemies.Count; enemyIndex++)
        {
            var enemy = mutable.Enemies[enemyIndex];
            if (enemy.HP <= 0)
            {
                continue;
            }

            while (true)
            {
                var attackIndex = FindPlayableCardIndex(enemy.Deck.Hand, cardDefinitions);
                if (attackIndex < 0)
                {
                    break;
                }

                var attackCard = enemy.Deck.Hand[attackIndex];
                enemy = enemy with
                {
                    Deck = enemy.Deck with
                    {
                        Hand = enemy.Deck.Hand.RemoveAt(attackIndex),
                        DiscardPile = enemy.Deck.DiscardPile.Add(attackCard),
                    },
                };
                mutable = mutable with { Enemies = mutable.Enemies.SetItem(enemyIndex, enemy) };

                var resolution = CardEffectResolver.Resolve(mutable, attackCard, TurnOwner.Enemy, cardDefinitions, currentRng, enemy.EntityId);
                mutable = resolution.CombatState;
                currentRng = resolution.Rng;
                events.AddRange(resolution.Events);
                actionCount++;

                enemy = mutable.Enemies[enemyIndex];
                if (mutable.Player.HP <= 0)
                {
                    return new EnemyTurnResult(mutable, currentRng, events, actionCount);
                }
            }
        }

        return new EnemyTurnResult(mutable, currentRng, events, actionCount);
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

    private static (CombatState CombatState, IReadOnlyList<GameEvent> Events) ApplyEnemyReshuffleFatigue(CombatState combatState, int enemyIndex)
    {
        var mutable = combatState;
        var events = new List<GameEvent>();
        var enemy = mutable.Enemies[enemyIndex];
        var missingCards = Math.Max(0, HandManager.InitialHandSize - enemy.Deck.Hand.Count);

        for (var i = 0; i < missingCards && enemy.Deck.DrawPile.Count > 0; i++)
        {
            var topCard = enemy.Deck.DrawPile[0];
            enemy = enemy with
            {
                Deck = enemy.Deck with
                {
                    DrawPile = enemy.Deck.DrawPile.RemoveAt(0),
                    Hand = enemy.Deck.Hand.Add(topCard),
                },
            };
            events.Add(new CardDrawn(topCard));
        }

        var discardCount = Math.Min(enemy.Deck.ReshuffleCount, enemy.Deck.Hand.Count);
        events.Add(new ReshuffleFatigueApplied(TurnOwner.Enemy, discardCount));

        for (var i = 0; i < discardCount; i++)
        {
            var card = enemy.Deck.Hand[0];
            enemy = enemy with
            {
                Deck = enemy.Deck with
                {
                    Hand = enemy.Deck.Hand.RemoveAt(0),
                    DiscardPile = enemy.Deck.DiscardPile.Add(card),
                },
            };
            events.Add(new CardDiscarded(card));
        }

        mutable = mutable with { Enemies = mutable.Enemies.SetItem(enemyIndex, enemy) };
        return (mutable, events);
    }
}

public sealed record EnemyTurnResult(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<GameEvent> Events,
    int ActionCount);
