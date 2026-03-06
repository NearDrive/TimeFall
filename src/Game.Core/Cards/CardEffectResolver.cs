using Game.Core.Combat;
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
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        if (!cardDefinitions.TryGetValue(card.DefinitionId, out var definition) || definition.Effects.Count == 0)
        {
            return new CardEffectResolution(combatState, Array.Empty<GameEvent>(), false);
        }

        var mutable = combatState;
        var events = new List<GameEvent>();

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
            }
        }

        return new CardEffectResolution(mutable, events, true);
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
    IReadOnlyList<GameEvent> Events,
    bool WasResolved);
