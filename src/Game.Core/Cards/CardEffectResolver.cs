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

        var mutable = combatState with { LastCardDamageDealt = 0 };
        var events = new List<GameEvent>();
        var currentRng = rng ?? GameRng.FromSeed(0);

        foreach (var effect in definition.Effects)
        {
            switch (effect)
            {
                case DamageCardEffect e: mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, false, events); break;
                case DamageIgnoringArmorCardEffect e: mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, true, events); break;
                case DamageNTimesCardEffect e:
                    for (var i = 0; i < e.Times; i++) mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, e.Target, false, events);
                    break;
                case DealDamagePerMomentumSpentCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * mutable.LastCardMomentumSpent, e.Target, false, events);
                    break;
                case DealDamagePerAllMomentumSpentCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * mutable.LastCardMomentumSpent, e.Target, false, events);
                    break;
                case DealDamagePerCurrentMomentumCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * GetMomentum(mutable.Player), e.Target, false, events);
                    break;
                case DealDamageToAllEnemiesCardEffect e:
                    mutable = ApplyDamageEffect(mutable, card, actor, e.Amount, CardTarget.Opponent, false, events);
                    break;
                case DealDamageAndDrawPerCurrentMomentumCardEffect e:
                    var m = GetMomentum(mutable.Player);
                    if (m > 0)
                    {
                        mutable = ApplyDamageEffect(mutable, card, actor, e.DamagePerMomentum * m, e.Target, false, events);
                        var dr = ApplyDrawEffect(mutable, new DrawCardsCardEffect(e.DrawPerMomentum * m, CardTarget.Self), actor, currentRng);
                        mutable = dr.CombatState; currentRng = dr.Rng; events.AddRange(dr.Events);
                    }
                    break;
                case GainArmorCardEffect e: mutable = ApplyArmorEffect(mutable, e, actor); break;
                case ConditionalGainArmorIfMomentumAtLeastCardEffect e:
                    if (GetMomentum(mutable.Player) >= e.MinimumMomentum) mutable = ApplyArmorEffect(mutable, new GainArmorCardEffect(e.Amount, e.Target), actor);
                    break;
                case DrawCardsCardEffect e:
                    var d = ApplyDrawEffect(mutable, e, actor, currentRng); mutable = d.CombatState; currentRng = d.Rng; events.AddRange(d.Events); break;
                case HealCardEffect e: mutable = ApplyHeal(mutable, e, actor); break;
                case ApplyBleedCardEffect e: mutable = ApplyBleed(mutable, e, actor); break;
                case GainGeneratedMomentumCardEffect e: mutable = ApplyGmGain(mutable, e.Amount, actor, e.Target, events, "Card effect"); break;
                case ReflectNextEnemyAttackDamageCardEffect e:
                    if (ResolveTarget(e.Target, actor) == TurnOwner.Player) mutable = mutable with { Player = mutable.Player with { ReflectNextEnemyAttackDamage = e.Amount } };
                    break;
                case AttackCountThisTurnToGmCardEffect:
                    mutable = ApplyGmGain(mutable, mutable.AttacksPlayedThisTurn, actor, CardTarget.Self, events, "Attack count this turn");
                    break;
                case RemoveEnemyArmorCardEffect: mutable = mutable with { Enemy = mutable.Enemy with { Armor = 0 } }; break;
                case NextAttackBonusDamageThisTurnCardEffect e: mutable = mutable with { NextAttackBonusDamageThisTurn = e.Amount }; break;
                case NextAttackDoubleThisTurnCardEffect: mutable = mutable with { NextAttackDoubleThisTurn = true }; break;
                case TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect e: mutable = mutable with { AllAttacksBonusDamageThisTurn = e.Amount }; break;
                case TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect: mutable = mutable with { AllAttacksDoubleThisTurn = true }; break;
                case LifestealPercentOfDamageDealtCardEffect e:
                    var heal = (int)Math.Floor(mutable.LastCardDamageDealt * (e.Percent / 100.0));
                    mutable = ApplyHeal(mutable, new HealCardEffect(heal, e.Target), actor);
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

    private static CombatState ApplyDamageEffect(CombatState combatState, CardInstance card, TurnOwner actor, int amount, CardTarget targetType, bool ignoreArmor, ICollection<GameEvent> events)
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
            events.Add(new EnemyAttackPlayed(card, modified, beforeHp, hitResult.UpdatedEntity.HP, beforeArmor, hitResult.UpdatedEntity.Armor, 0));
            var state = combatState with { Player = hitResult.UpdatedEntity };
            if (state.Player.ReflectNextEnemyAttackDamage > 0)
            {
                state = ApplyDamageEffect(state with { Player = state.Player with { ReflectNextEnemyAttackDamage = 0 } }, card, TurnOwner.Player, state.Player.ReflectNextEnemyAttackDamage, CardTarget.Opponent, true, events);
            }
            return state;
        }

        var enemyBeforeHp = combatState.Enemy.HP;
        var enemyBeforeArmor = combatState.Enemy.Armor;
        var enemyHitResult = ignoreArmor
            ? DamageSystem.ApplyArmorIgnoringHit(combatState.Enemy, modified)
            : DamageSystem.ApplyHit(combatState.Enemy, modified);
        events.Add(new PlayerStrikePlayed(card, modified, enemyBeforeHp, enemyHitResult.UpdatedEntity.HP, enemyBeforeArmor, enemyHitResult.UpdatedEntity.Armor, 0));
        return combatState with
        {
            Enemy = enemyHitResult.UpdatedEntity,
            LastCardDamageDealt = combatState.LastCardDamageDealt + Math.Max(0, enemyBeforeHp - enemyHitResult.UpdatedEntity.HP),
        };
    }

    private static CombatState ApplyArmorEffect(CombatState combatState, GainArmorCardEffect effect, TurnOwner actor)
    {
        var target = ResolveTarget(effect.Target, actor);
        return target == TurnOwner.Player
            ? combatState with { Player = combatState.Player with { Armor = combatState.Player.Armor + effect.Amount } }
            : combatState with { Enemy = combatState.Enemy with { Armor = combatState.Enemy.Armor + effect.Amount } };
    }

    private static CombatState ApplyHeal(CombatState state, HealCardEffect effect, TurnOwner actor)
    {
        var target = ResolveTarget(effect.Target, actor);
        if (target == TurnOwner.Player)
        {
            var hp = Math.Min(state.Player.MaxHP, state.Player.HP + effect.Amount);
            return state with { Player = state.Player with { HP = hp } };
        }

        var enemyHp = Math.Min(state.Enemy.MaxHP, state.Enemy.HP + effect.Amount);
        return state with { Enemy = state.Enemy with { HP = enemyHp } };
    }

    private static CombatState ApplyBleed(CombatState state, ApplyBleedCardEffect effect, TurnOwner actor)
    {
        var target = ResolveTarget(effect.Target, actor);
        return target == TurnOwner.Player
            ? state with { Player = state.Player with { Bleed = state.Player.Bleed + effect.Amount } }
            : state with { Enemy = state.Enemy with { Bleed = state.Enemy.Bleed + effect.Amount } };
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
}

public sealed record CardEffectResolution(CombatState CombatState, GameRng Rng, IReadOnlyList<GameEvent> Events, bool WasResolved);
public sealed record DrawEffectResult(CombatState CombatState, GameRng Rng, IReadOnlyList<GameEvent> Events);
