using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Core.Cards;

public static class CardEffectResolver
{
    public static bool HasResolvableEffects(CardDefinition definition)
        => definition.Effects.Count > 0;

    public static bool HasResolvableEffects(CardInstance card, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
        => cardDefinitions.TryGetValue(card.DefinitionId, out var definition) && HasResolvableEffects(definition);

    public static bool RequiresEnemyTarget(CardDefinition definition)
        => definition.Effects.Any(effect => effect switch
        {
            DealDamageToAllEnemiesCardEffect => false,
            DamageCardEffect e => e.Target == CardTarget.Opponent,
            DamageIgnoringArmorCardEffect e => e.Target == CardTarget.Opponent,
            DamageNTimesCardEffect e => e.Target == CardTarget.Opponent,
            DealDamagePerMomentumSpentCardEffect e => e.Target == CardTarget.Opponent,
            DealDamagePerAllMomentumSpentCardEffect e => e.Target == CardTarget.Opponent,
            DealDamagePerCurrentMomentumCardEffect e => e.Target == CardTarget.Opponent,
            DealDamageAndDrawPerCurrentMomentumCardEffect e => e.Target == CardTarget.Opponent,
            ApplyBleedCardEffect e => e.Target == CardTarget.Opponent,
            RemoveEnemyArmorCardEffect => true,
            _ => false,
        });

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

        foreach (var effect in definition.Effects)
        {
            switch (effect)
            {
                case DamageCardEffect e: mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, false, events, selectedEnemyId); break;
                case DamageIgnoringArmorCardEffect e: mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, true, events, selectedEnemyId); break;
                case DamageNTimesCardEffect e:
                    for (var i = 0; i < e.Times; i++) mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, false, events, selectedEnemyId);
                    break;
                case DealDamagePerMomentumSpentCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * mutable.LastCardMomentumSpent, e.Target, false, events, selectedEnemyId);
                    break;
                case DealDamagePerAllMomentumSpentCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * mutable.LastCardMomentumSpent, e.Target, false, events, selectedEnemyId);
                    break;
                case DealDamagePerCurrentMomentumCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * GetMomentum(mutable.Player), e.Target, false, events, selectedEnemyId);
                    break;
                case DealDamageToAllEnemiesCardEffect e:
                    mutable = ApplyDamageToAllEnemies(mutable, card, actor, e.Amount, false, events);
                    break;
                case DealDamageAndDrawPerCurrentMomentumCardEffect e:
                    var m = GetMomentum(mutable.Player);
                    if (m > 0)
                    {
                        mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * m, e.Target, false, events, selectedEnemyId);
                        var dr = ApplyDrawEffect(mutable, new DrawCardsCardEffect(e.DrawPerMomentum * m, CardTarget.Self), actor, currentRng);
                        mutable = dr.CombatState; currentRng = dr.Rng; events.AddRange(dr.Events);
                    }
                    break;
                case GainArmorCardEffect e: mutable = ApplyArmorEffect(mutable, e, actor, selectedEnemyId); break;
                case ConditionalGainArmorIfMomentumAtLeastCardEffect e:
                    if (GetMomentum(mutable.Player) >= e.MinimumMomentum) mutable = ApplyArmorEffect(mutable, new GainArmorCardEffect(e.Amount, e.Target), actor, selectedEnemyId);
                    break;
                case DrawCardsCardEffect e:
                    var d = ApplyDrawEffect(mutable, e, actor, currentRng); mutable = d.CombatState; currentRng = d.Rng; events.AddRange(d.Events); break;
                case HealCardEffect e: mutable = ApplyHeal(mutable, e, actor, selectedEnemyId); break;
                case ApplyBleedCardEffect e:
                    mutable = ApplyBleed(mutable, e, actor, selectedEnemyId);
                    events.Add(new StatusApplied(actor, ResolveTarget(e.Target, actor), "Bleed", e.Amount));
                    break;
                case GainGeneratedMomentumCardEffect e: mutable = ApplyGmGain(mutable, e.Amount, actor, e.Target, events, "Card effect"); break;
                case ReflectNextEnemyAttackDamageCardEffect e:
                    if (ResolveTarget(e.Target, actor) == TurnOwner.Player) mutable = mutable with { Player = mutable.Player with { ReflectNextEnemyAttackDamage = e.Amount } };
                    break;
                case AttackCountThisTurnToGmCardEffect:
                    mutable = ApplyGmGain(mutable, mutable.AttacksPlayedThisTurn, actor, CardTarget.Self, events, "Attack count this turn");
                    break;
                case RemoveEnemyArmorCardEffect:
                    mutable = UpdateEnemyById(mutable, selectedEnemyId, enemy => enemy with { Armor = 0 });
                    break;
                case NextAttackBonusDamageThisTurnCardEffect e: mutable = mutable with { NextAttackBonusDamageThisTurn = e.Amount }; break;
                case NextAttackDoubleThisTurnCardEffect: mutable = mutable with { NextAttackDoubleThisTurn = true }; break;
                case TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect e: mutable = mutable with { AllAttacksBonusDamageThisTurn = e.Amount }; break;
                case TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect: mutable = mutable with { AllAttacksDoubleThisTurn = true }; break;
                case LifestealPercentOfDamageDealtCardEffect e:
                    var heal = (int)Math.Floor(mutable.LastCardDamageDealt * (e.Percent / 100.0));
                    mutable = ApplyHeal(mutable, new HealCardEffect(heal, e.Target), actor, selectedEnemyId);
                    break;
            }
        }

        return new CardEffectResolution(mutable, currentRng, events, true);
    }

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
        var after = Math.Max(0, before + amount);
        events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, before, after, reason));
        return cs with { Player = cs.Player with { Resources = cs.Player.Resources.SetItem(ResourceType.Momentum, after) } };
    }

    private static CombatState ApplyDamageToAllEnemies(CombatState combatState, CardInstance card, TurnOwner actor, int amount, bool ignoreArmor, ICollection<GameEvent> events)
    {
        var enemyIds = combatState.Enemies.Select(e => e.EntityId).ToArray();
        var mutable = combatState;
        foreach (var enemyId in enemyIds)
        {
            mutable = ApplyDamageEffect(mutable, card, actor, amount, CardTarget.Opponent, ignoreArmor, events, enemyId);
        }

        return mutable;
    }

    private static CombatState ApplyDamageEffect(CombatState combatState, CardInstance card, TurnOwner actor, int amount, CardTarget targetType, bool ignoreArmor, ICollection<GameEvent> events, string? selectedEnemyId)
    {
        var target = ResolveTarget(targetType, actor);
        var modified = amount;
        if (actor == TurnOwner.Player && target == TurnOwner.Enemy)
        {
            modified += combatState.NextAttackBonusDamageThisTurn + combatState.AllAttacksBonusDamageThisTurn;
            if (combatState.NextAttackDoubleThisTurn || combatState.AllAttacksDoubleThisTurn) modified *= 2;
            combatState = combatState with { NextAttackBonusDamageThisTurn = 0, NextAttackDoubleThisTurn = false };
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
                state = ApplyDamageEffect(state with { Player = state.Player with { ReflectNextEnemyAttackDamage = 0 } }, card, TurnOwner.Player, state.Player.ReflectNextEnemyAttackDamage, CardTarget.Opponent, true, events, selectedEnemyId);
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
        events.Add(new PlayerStrikePlayed(card, modified, enemyBeforeHp, enemyHitResult.UpdatedEntity.HP, enemyBeforeArmor, enemyHitResult.UpdatedEntity.Armor, enemyBlocked));
        return UpdateEnemyById(combatState, enemy.EntityId, _ => enemyHitResult.UpdatedEntity) with
        {
            LastCardDamageDealt = combatState.LastCardDamageDealt + Math.Max(0, enemyBeforeHp - enemyHitResult.UpdatedEntity.HP),
        };
    }

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

    private static CombatState ApplyBleed(CombatState state, ApplyBleedCardEffect effect, TurnOwner actor, string? selectedEnemyId)
    {
        var target = ResolveTarget(effect.Target, actor);
        if (target == TurnOwner.Player)
        {
            return state with { Player = state.Player with { Bleed = state.Player.Bleed + effect.Amount } };
        }

        return UpdateEnemyById(state, selectedEnemyId, enemy => enemy with { Bleed = enemy.Bleed + effect.Amount });
    }

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

        return state.Enemies.FirstOrDefault();
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
