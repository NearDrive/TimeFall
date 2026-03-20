using System.Collections.Immutable;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Core.Cards;

public static class CardEffectResolver
{
    public static bool HasResolvableEffects(CardDefinition definition)
        => definition.Effects.Count > 0;

    public static bool HasResolvableEffects(CardInstance card, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
        => cardDefinitions.TryGetValue(card.DefinitionId, out var definition) && HasResolvableEffects(definition);

    public static bool RequiresEnemyTarget(CardDefinition definition)
        => definition.Effects.Any(RequiresEnemyTarget);

    public static CardEffectResolution Resolve(
        CombatState combatState,
        CardInstance card,
        TurnOwner actor,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions,
        GameRng? rng = null,
        string? selectedEnemyId = null)
    {
        if (!cardDefinitions.TryGetValue(card.DefinitionId, out var definition) || definition.Effects.Count == 0)
        {
            return new CardEffectResolution(combatState, rng ?? GameRng.FromSeed(0), Array.Empty<GameEvent>(), false);
        }

        var mutable = combatState with { LastCardDamageDealt = 0 };
        var events = new List<GameEvent>();
        var currentRng = rng ?? GameRng.FromSeed(0);
        var context = new EffectExecutionContext(GetMomentum(mutable.Player));
        var isAttackCard = definition.HasLabel("Attack");

        foreach (var effect in definition.Effects)
        {
            var result = ResolveEffect(mutable, card, actor, effect, isAttackCard, events, currentRng, selectedEnemyId, context, 0);
            mutable = result.CombatState;
            currentRng = result.Rng;
        }

        return new CardEffectResolution(mutable, currentRng, events, true);
    }

    private static EffectResolutionResult ResolveEffect(
        CombatState state,
        CardInstance card,
        TurnOwner actor,
        CardEffect effect,
        bool isAttackCard,
        List<GameEvent> events,
        GameRng rng,
        string? selectedEnemyId,
        EffectExecutionContext context,
        int recursionDepth)
    {
        if (recursionDepth > 16)
        {
            return new EffectResolutionResult(state, rng);
        }

        var mutable = state;
        var currentRng = rng;

        switch (effect)
        {
            case DamageCardEffect e:
                mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, false, isAttackCard, events, selectedEnemyId);
                break;
            case DamageIgnoringArmorCardEffect e:
                mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, true, isAttackCard, events, selectedEnemyId);
                break;
            case DamageNTimesCardEffect e:
                for (var i = 0; i < e.Times; i++)
                {
                    mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, false, isAttackCard, events, selectedEnemyId);
                }
                break;
            case DealDamagePerMomentumSpentCardEffect e:
                mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * mutable.LastCardMomentumSpent, e.Target, false, isAttackCard, events, selectedEnemyId);
                break;
            case DealDamagePerAllMomentumSpentCardEffect e:
                mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * mutable.LastCardMomentumSpent, e.Target, false, isAttackCard, events, selectedEnemyId);
                break;
            case DealDamagePerCurrentMomentumCardEffect e:
                mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * context.InitialCurrentMomentum, e.Target, false, isAttackCard, events, selectedEnemyId);
                break;
            case DealDamageToAllEnemiesCardEffect e:
                mutable = ApplyDamageToAllEnemies(mutable, card, actor, e.Amount, false, isAttackCard, events);
                break;
            case DealDamageAndDrawPerCurrentMomentumCardEffect e:
                if (context.InitialCurrentMomentum > 0)
                {
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * context.InitialCurrentMomentum, e.Target, false, isAttackCard, events, selectedEnemyId);
                    var dr = ApplyDrawEffect(mutable, new DrawCardsCardEffect(e.DrawPerMomentum * context.InitialCurrentMomentum, CardTarget.Self), actor, currentRng);
                    mutable = dr.CombatState;
                    currentRng = dr.Rng;
                    events.AddRange(dr.Events);
                }
                break;
            case DamageWithAttackCountScalingCardEffect e:
                mutable = ApplyDamageEffect(mutable, card, actor, e.BaseAmount + (e.DamagePerAttackPlayedThisTurn * mutable.AttacksPlayedThisTurn), e.Target, false, isAttackCard, events, selectedEnemyId);
                break;
            case GainArmorCardEffect e:
                mutable = ApplyArmorEffect(mutable, e, actor, selectedEnemyId);
                break;
            case ConditionalGainArmorIfMomentumAtLeastCardEffect e:
                if (GetMomentum(mutable.Player) >= e.MinimumMomentum)
                {
                    mutable = ApplyArmorEffect(mutable, new GainArmorCardEffect(e.Amount, e.Target), actor, selectedEnemyId);
                }
                break;
            case DrawCardsCardEffect e:
                var d = ApplyDrawEffect(mutable, e, actor, currentRng);
                mutable = d.CombatState;
                currentRng = d.Rng;
                events.AddRange(d.Events);
                break;
            case HealCardEffect e:
                mutable = ApplyHeal(mutable, e, actor, selectedEnemyId);
                break;
            case ApplyStatusCardEffect e:
                mutable = ApplyStatus(mutable, e.Status, e.Amount, e.Target, actor, selectedEnemyId);
                events.Add(new StatusApplied(actor, ResolveTarget(e.Target, actor), e.Status.ToString(), e.Amount));
                break;
            case ApplyBleedCardEffect e:
                mutable = ApplyStatus(mutable, StatusKind.Bleed, e.Amount, e.Target, actor, selectedEnemyId);
                events.Add(new StatusApplied(actor, ResolveTarget(e.Target, actor), StatusKind.Bleed.ToString(), e.Amount));
                break;
            case ApplyStatusPerCurrentMomentumCardEffect e:
                var statusAmount = e.BaseAmount + (e.AmountPerCurrentMomentum * context.InitialCurrentMomentum);
                mutable = ApplyStatus(mutable, e.Status, statusAmount, e.Target, actor, selectedEnemyId);
                events.Add(new StatusApplied(actor, ResolveTarget(e.Target, actor), e.Status.ToString(), statusAmount));
                break;
            case GainGeneratedMomentumCardEffect e:
                mutable = ApplyGmGain(mutable, e.Amount, actor, e.Target, events, "Card effect");
                break;
            case ReflectNextEnemyAttackDamageCardEffect e:
                if (ResolveTarget(e.Target, actor) == TurnOwner.Player)
                {
                    mutable = mutable with { Player = mutable.Player with { ReflectNextEnemyAttackDamage = e.Amount } };
                }
                break;
            case AttackCountThisTurnToGmCardEffect:
                mutable = ApplyGmGain(mutable, mutable.AttacksPlayedThisTurn, actor, CardTarget.Self, events, "Attack count this turn");
                break;
            case RemoveEnemyArmorCardEffect:
                mutable = UpdateEnemyById(mutable, selectedEnemyId, enemy => enemy with { Armor = 0 });
                break;
            case RemoveAllArmorCardEffect:
                mutable = mutable with { Enemies = mutable.Enemies.Select(enemy => enemy with { Armor = 0 }).ToImmutableList() };
                break;
            case NextAttackBonusDamageThisTurnCardEffect e:
                mutable = mutable with { NextAttackBonusDamageThisTurn = mutable.NextAttackBonusDamageThisTurn + e.Amount };
                break;
            case NextAttackDoubleThisTurnCardEffect:
            case NextAttackDoubleDamageThisTurnCardEffect:
                mutable = mutable with { NextAttackDamageMultiplierThisTurn = mutable.NextAttackDamageMultiplierThisTurn + 1m };
                break;
            case TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect e:
                mutable = mutable with { AllAttacksBonusDamageThisTurn = mutable.AllAttacksBonusDamageThisTurn + e.Amount };
                break;
            case AllAttacksBonusDamageThisTurnCardEffect e:
                mutable = mutable with { AllAttacksBonusDamageThisTurn = mutable.AllAttacksBonusDamageThisTurn + e.Amount };
                break;
            case TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect:
            case AllAttacksDoubleDamageThisTurnCardEffect:
                mutable = mutable with { AllAttacksDamageMultiplierThisTurn = mutable.AllAttacksDamageMultiplierThisTurn + 1m };
                break;
            case LifestealPercentOfDamageDealtCardEffect e:
                var heal = (int)Math.Floor(mutable.LastCardDamageDealt * (e.Percent / 100.0));
                mutable = ApplyHeal(mutable, new HealCardEffect(heal, e.Target), actor, selectedEnemyId);
                break;
            case RepeatEffectsPerCurrentMomentumCardEffect e:
                for (var i = 0; i < context.InitialCurrentMomentum; i++)
                {
                    foreach (var nested in e.Effects)
                    {
                        var nestedResult = ResolveEffect(mutable, card, actor, nested, isAttackCard, events, currentRng, selectedEnemyId, context, recursionDepth + 1);
                        mutable = nestedResult.CombatState;
                        currentRng = nestedResult.Rng;
                    }
                }
                break;
        }

        return new EffectResolutionResult(mutable, currentRng);
    }

    private static bool RequiresEnemyTarget(CardEffect effect)
        => effect switch
        {
            DealDamageToAllEnemiesCardEffect => false,
            RepeatEffectsPerCurrentMomentumCardEffect e => e.Effects.Any(RequiresEnemyTarget),
            DamageCardEffect e => e.Target == CardTarget.Opponent,
            DamageIgnoringArmorCardEffect e => e.Target == CardTarget.Opponent,
            DamageNTimesCardEffect e => e.Target == CardTarget.Opponent,
            DealDamagePerMomentumSpentCardEffect e => e.Target == CardTarget.Opponent,
            DealDamagePerAllMomentumSpentCardEffect e => e.Target == CardTarget.Opponent,
            DealDamagePerCurrentMomentumCardEffect e => e.Target == CardTarget.Opponent,
            DealDamageAndDrawPerCurrentMomentumCardEffect e => e.Target == CardTarget.Opponent,
            DamageWithAttackCountScalingCardEffect e => e.Target == CardTarget.Opponent,
            ApplyStatusCardEffect e => e.Target == CardTarget.Opponent,
            ApplyBleedCardEffect e => e.Target == CardTarget.Opponent,
            ApplyStatusPerCurrentMomentumCardEffect e => e.Target == CardTarget.Opponent,
            RemoveEnemyArmorCardEffect => true,
            _ => false,
        };

    private static int GetMomentum(CombatEntity entity)
    {
        var gm = entity.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
        return MomentumMath.DerivedMomentumFromGm(gm);
    }

    private static CombatState ApplyGmGain(CombatState cs, int amount, TurnOwner actor, CardTarget target, List<GameEvent> events, string reason)
    {
        var resolved = ResolveTarget(target, actor);
        if (resolved != TurnOwner.Player || amount == 0)
        {
            return cs;
        }

        var before = cs.Player.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
        var gained = MomentumMath.Threshold(amount);
        var after = Math.Max(0, before + gained);
        events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, before, after, $"{reason} (GM +{gained} from M {amount})"));
        return cs with { Player = cs.Player with { Resources = cs.Player.Resources.SetItem(ResourceType.Momentum, after) } };
    }

    private static CombatState ApplyDamageToAllEnemies(CombatState combatState, CardInstance card, TurnOwner actor, int amount, bool ignoreArmor, bool isAttackCard, List<GameEvent> events)
    {
        var enemyIds = combatState.Enemies.Select(e => e.EntityId).ToArray();
        var mutable = combatState;
        foreach (var enemyId in enemyIds)
        {
            mutable = ApplyDamageEffect(mutable, card, actor, amount, CardTarget.Opponent, ignoreArmor, isAttackCard, events, enemyId);
        }

        return mutable;
    }

    private static CombatState ApplyDamageEffect(CombatState combatState, CardInstance card, TurnOwner actor, int amount, CardTarget targetType, bool ignoreArmor, bool isAttackCard, List<GameEvent> events, string? selectedEnemyId)
    {
        var target = ResolveTarget(targetType, actor);
        var baseDamage = amount;
        var source = ResolveActorEntity(combatState, actor, selectedEnemyId);
        var weakPenalty = source?.Weak ?? 0;
        var modified = amount;
        var momentumBonus = 0;
        if (actor == TurnOwner.Player && target == TurnOwner.Enemy && isAttackCard)
        {
            if (TryGetMomentumAttackBonus(combatState, out var attackMomentumBonus))
            {
                momentumBonus = attackMomentumBonus;
            }

            modified += momentumBonus;
            modified += combatState.NextAttackBonusDamageThisTurn + combatState.AllAttacksBonusDamageThisTurn;
            modified = Math.Max(0, modified - weakPenalty);
            modified = ApplyAttackDamageMultiplier(modified, combatState);
            combatState = combatState with { NextAttackBonusDamageThisTurn = 0, NextAttackDamageMultiplierThisTurn = 0m };
        }
        else
        {
            modified = Math.Max(0, modified - weakPenalty);
        }

        if (target == TurnOwner.Player)
        {
            var beforeHp = combatState.Player.HP;
            var beforeArmor = combatState.Player.Armor;
            var hitResult = ignoreArmor
                ? DamageSystem.ApplyArmorIgnoringHit(combatState.Player, modified)
                : DamageSystem.ApplyHit(combatState.Player, modified);
            var blocked = hitResult.Events.OfType<DamageDealt>().Select(e => Math.Max(0, e.Incoming - e.Taken)).FirstOrDefault();
            events.Add(new EnemyAttackPlayed(card, modified, beforeHp, hitResult.UpdatedEntity.HP, beforeArmor, hitResult.UpdatedEntity.Armor, blocked));
            var state = combatState with { Player = hitResult.UpdatedEntity };
            if (state.Player.ReflectNextEnemyAttackDamage > 0)
            {
                state = ApplyDamageEffect(state with { Player = state.Player with { ReflectNextEnemyAttackDamage = 0 } }, card, TurnOwner.Player, state.Player.ReflectNextEnemyAttackDamage, CardTarget.Opponent, true, false, events, selectedEnemyId);
            }

            return state;
        }

        var enemy = ResolveEnemy(combatState, selectedEnemyId);
        if (enemy is null)
        {
            return combatState;
        }

        var enemyBeforeHp = enemy.HP;
        var enemyBeforeArmor = enemy.Armor;
        var enemyHitResult = ignoreArmor
            ? DamageSystem.ApplyArmorIgnoringHit(enemy, modified)
            : DamageSystem.ApplyHit(enemy, modified);
        var enemyBlocked = enemyHitResult.Events.OfType<DamageDealt>().Select(e => Math.Max(0, e.Incoming - e.Taken)).FirstOrDefault();
        events.Add(new PlayerStrikePlayed(card, modified, baseDamage, momentumBonus, enemyBeforeHp, enemyHitResult.UpdatedEntity.HP, enemyBeforeArmor, enemyHitResult.UpdatedEntity.Armor, enemyBlocked));

        var updatedState = UpdateEnemyById(combatState, enemy.EntityId, _ => enemyHitResult.UpdatedEntity) with
        {
            LastCardDamageDealt = combatState.LastCardDamageDealt + Math.Max(0, enemyBeforeHp - enemyHitResult.UpdatedEntity.HP),
        };

        if (actor == TurnOwner.Player && target == TurnOwner.Enemy && isAttackCard && modified > 0)
        {
            updatedState = ApplyGmGain(updatedState, 1, actor, CardTarget.Self, events, "Attack hit bonus");
        }

        return updatedState;
    }

    private static bool TryGetMomentumAttackBonus(CombatState combatState, out int bonus)
    {
        bonus = GetMomentum(combatState.Player);
        return bonus > 0;
    }

    private static int ApplyAttackDamageMultiplier(int damage, CombatState combatState)
    {
        var multiplier = combatState.NextAttackDamageMultiplierThisTurn + combatState.AllAttacksDamageMultiplierThisTurn;
        if (multiplier <= 0m)
        {
            return damage;
        }

        return (int)Math.Floor(damage * (1m + multiplier));
    }

    private static CombatEntity? ResolveActorEntity(CombatState state, TurnOwner actor, string? selectedEnemyId)
        => actor == TurnOwner.Player ? state.Player : ResolveEnemy(state, selectedEnemyId);

    private static CombatState ApplyArmorEffect(CombatState combatState, GainArmorCardEffect effect, TurnOwner actor, string? selectedEnemyId)
    {
        var target = ResolveTarget(effect.Target, actor);
        return target == TurnOwner.Player
            ? combatState with { Player = combatState.Player with { Armor = combatState.Player.Armor + effect.Amount } }
            : UpdateEnemyById(combatState, selectedEnemyId, enemy => enemy with { Armor = enemy.Armor + effect.Amount });
    }

    private static CombatState ApplyHeal(CombatState state, HealCardEffect effect, TurnOwner actor, string? selectedEnemyId)
    {
        var target = ResolveTarget(effect.Target, actor);
        if (target == TurnOwner.Player)
        {
            var hp = Math.Min(state.Player.MaxHP, state.Player.HP + effect.Amount);
            return state with { Player = state.Player with { HP = hp } };
        }

        return UpdateEnemyById(state, selectedEnemyId, enemy => enemy with { HP = Math.Min(enemy.MaxHP, enemy.HP + effect.Amount) });
    }

    private static CombatState ApplyStatus(CombatState state, StatusKind status, int amount, CardTarget target, TurnOwner actor, string? selectedEnemyId)
    {
        if (amount <= 0)
        {
            return state;
        }

        var resolvedTarget = ResolveTarget(target, actor);
        if (resolvedTarget == TurnOwner.Player)
        {
            return state with { Player = ApplyStatus(state.Player, status, amount) };
        }

        return UpdateEnemyById(state, selectedEnemyId, enemy => ApplyStatus(enemy, status, amount));
    }

    private static CombatEntity ApplyStatus(CombatEntity entity, StatusKind status, int amount)
        => status switch
        {
            StatusKind.Bleed => entity with { Bleed = entity.Bleed + amount },
            StatusKind.Weak => entity with { Weak = entity.Weak + amount },
            StatusKind.Vulnerable => entity with { Vulnerable = entity.Vulnerable + amount },
            _ => entity,
        };

    private static DrawEffectResult ApplyDrawEffect(CombatState combatState, DrawCardsCardEffect effect, TurnOwner actor, GameRng rng)
    {
        var target = ResolveTarget(effect.Target, actor);
        if (target == TurnOwner.Player)
        {
            var drawResult = HandManager.Draw(combatState, rng, effect.Amount);
            var events = drawResult.Events.Concat(drawResult.DrawnCards.Select(card => (GameEvent)new CardDrawn(card))).ToArray();
            return new DrawEffectResult(drawResult.CombatState, drawResult.Rng, events);
        }

        return new DrawEffectResult(combatState, rng, Array.Empty<GameEvent>());
    }

    private static TurnOwner ResolveTarget(CardTarget target, TurnOwner actor)
        => target == CardTarget.Self ? actor : actor == TurnOwner.Player ? TurnOwner.Enemy : TurnOwner.Player;

    private static CombatEntity? ResolveEnemy(CombatState state, string? enemyId)
    {
        if (!string.IsNullOrWhiteSpace(enemyId))
        {
            return state.Enemies.FirstOrDefault(e => e.EntityId == enemyId);
        }

        return state.Enemies.FirstOrDefault(e => e.HP > 0);
    }

    private static CombatState UpdateEnemyById(CombatState state, string? enemyId, Func<CombatEntity, CombatEntity> update)
    {
        var enemy = ResolveEnemy(state, enemyId);
        if (enemy is null)
        {
            return state;
        }

        var index = state.Enemies.FindIndex(e => e.EntityId == enemy.EntityId);
        if (index < 0)
        {
            return state;
        }

        return state with { Enemies = state.Enemies.SetItem(index, update(enemy)) };
    }
}

public sealed record CardEffectResolution(CombatState CombatState, GameRng Rng, IReadOnlyList<GameEvent> Events, bool WasResolved);
public sealed record DrawEffectResult(CombatState CombatState, GameRng Rng, IReadOnlyList<GameEvent> Events);
internal sealed record EffectExecutionContext(int InitialCurrentMomentum);
internal sealed record EffectResolutionResult(CombatState CombatState, GameRng Rng);
