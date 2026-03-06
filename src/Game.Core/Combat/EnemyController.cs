using Game.Core.Common;
using CardId = Game.Core.Cards.CardId;
using Game.Core.Game;

namespace Game.Core.Combat;

public static class EnemyController
{
    private static readonly CardId AttackCardId = new("attack");
    private const int AttackDamage = 4;

    public static EnemyTurnResult ExecuteTurn(CombatState combatState, GameRng rng)
    {
        var mutable = Clone(combatState);
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

            var attackIndex = mutable.Enemy.Deck.Hand.FindIndex(CanPlayAttackCard);
            if (attackIndex < 0)
            {
                break;
            }

            var attackCard = mutable.Enemy.Deck.Hand[attackIndex];
            mutable.Enemy.Deck.Hand.RemoveAt(attackIndex);
            mutable.Enemy.Deck.DiscardPile.Add(attackCard);

            var hitResult = DamageSystem.ApplyHit(mutable.Player, AttackDamage);
            mutable = mutable with { Player = hitResult.UpdatedEntity };
            events.Add(new EnemyAttackPlayed(attackCard, AttackDamage, hitResult.UpdatedEntity.HP));
            actionCount++;
        }

        return new EnemyTurnResult(mutable, currentRng, events, actionCount);
    }


    private static bool CanPlayAttackCard(CardInstance card)
    {
        return card.DefinitionId == AttackCardId;
    }

    private static DrawResult DrawEnemyCards(CombatState combatState, GameRng rng, int count)
    {
        var mutable = Clone(combatState);
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
            mutable.Enemy.Deck.DrawPile.RemoveAt(0);
            mutable.Enemy.Deck.Hand.Add(topCard);
            drawn.Add(topCard);
        }

        return new DrawResult(mutable, currentRng, drawn, events);
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

public sealed record EnemyTurnResult(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<GameEvent> Events,
    int ActionCount);
