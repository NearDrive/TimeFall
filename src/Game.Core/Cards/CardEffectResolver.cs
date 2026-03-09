using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Core.Cards;

public static class CardEffectResolver
{
    public static bool HasResolvableEffects(
        CardInstance card,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        return cardDefinitions.TryGetValue(card.DefinitionId, out var definition)
            && definition.Effects.Count > 0;
    }

    public static CardEffectResolution Resolve(
        CombatState combatState,
        CardInstance card,
        TurnOwner actor,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions,
        GameRng? rng = null)
    {
        if (!cardDefinitions.TryGetValue(card.DefinitionId, out var definition) || definition.Effects.Count == 0)
        {
            return new CardEffectResolution(combatState, rng ?? GameRng.FromSeed(0), Array.Empty<GameEvent>(), false);
        }

        var mutable = combatState;
        var events = new List<GameEvent>();
        var currentRng = rng ?? GameRng.FromSeed(0);

        foreach (var effect in definition.Effects)
        {
            switch (effect)
            {
                case DamageCardEffect damage:
                    mutable = ApplyDamageEffect(mutable, card, actor, damage, events);
                    break;
                case GainArmorCardEffect gainArmor:
                    mutable = ApplyArmorEffect(mutable, gainArmor, actor);
                    break;
                case DrawCardsCardEffect drawCards:
                {
                    var drawResult = ApplyDrawEffect(mutable, drawCards, actor, currentRng);
                    mutable = drawResult.CombatState;
                    currentRng = drawResult.Rng;
                    events.AddRange(drawResult.Events);
                    break;
                }
            }
        }

        return new CardEffectResolution(mutable, currentRng, events, true);
    }

    private static CombatState ApplyDamageEffect(
        CombatState combatState,
        CardInstance card,
        TurnOwner actor,
        DamageCardEffect effect,
        ICollection<GameEvent> events)
    {
        var target = ResolveTarget(effect.Target, actor);

        if (target == TurnOwner.Player)
        {
            var hitResult = DamageSystem.ApplyHit(combatState.Player, effect.Amount);
            events.Add(new EnemyAttackPlayed(card, effect.Amount, hitResult.UpdatedEntity.HP));
            return combatState with { Player = hitResult.UpdatedEntity };
        }

        var enemyHitResult = DamageSystem.ApplyHit(combatState.Enemy, effect.Amount);
        events.Add(new PlayerStrikePlayed(card, effect.Amount, enemyHitResult.UpdatedEntity.HP));
        return combatState with { Enemy = enemyHitResult.UpdatedEntity };
    }

    private static CombatState ApplyArmorEffect(
        CombatState combatState,
        GainArmorCardEffect effect,
        TurnOwner actor)
    {
        var target = ResolveTarget(effect.Target, actor);

        if (target == TurnOwner.Player)
        {
            return combatState with
            {
                Player = combatState.Player with { Armor = combatState.Player.Armor + effect.Amount },
            };
        }

        return combatState with
        {
            Enemy = combatState.Enemy with { Armor = combatState.Enemy.Armor + effect.Amount },
        };
    }


    private static DrawEffectResult ApplyDrawEffect(
        CombatState combatState,
        DrawCardsCardEffect effect,
        TurnOwner actor,
        GameRng rng)
    {
        var target = ResolveTarget(effect.Target, actor);
        if (target == TurnOwner.Player)
        {
            var drawResult = HandManager.Draw(combatState, rng, effect.Amount);
            var events = drawResult.Events
                .Concat(drawResult.DrawnCards.Select(card => (GameEvent)new CardDrawn(card)))
                .ToArray();
            return new DrawEffectResult(drawResult.CombatState, drawResult.Rng, events);
        }

        var mutable = combatState;
        var currentRng = rng;
        var eventsList = new List<GameEvent>();

        for (var i = 0; i < effect.Amount; i++)
        {
            if (mutable.Enemy.Deck.DrawPile.Count == 0)
            {
                var cycleResult = DeckCycleSystem.EnsureDrawAvailable(mutable.Enemy.Deck, currentRng, out var cycleEvents);
                mutable = mutable with
                {
                    Enemy = mutable.Enemy with
                    {
                        Deck = cycleResult.Deck,
                    },
                };
                currentRng = cycleResult.Rng;
                eventsList.AddRange(cycleEvents);

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
            eventsList.Add(new CardDrawn(topCard));
        }

        return new DrawEffectResult(mutable, currentRng, eventsList);
    }

    private static TurnOwner ResolveTarget(CardTarget target, TurnOwner actor)
    {
        if (target == CardTarget.Self)
        {
            return actor;
        }

        return actor == TurnOwner.Player ? TurnOwner.Enemy : TurnOwner.Player;
    }
}

public sealed record CardEffectResolution(
    CombatState CombatState,
    GameRng Rng,
    IReadOnlyList<GameEvent> Events,
    bool WasResolved);

public sealed record DrawEffectResult(CombatState CombatState, GameRng Rng, IReadOnlyList<GameEvent> Events);
