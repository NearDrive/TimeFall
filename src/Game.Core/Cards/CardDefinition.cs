using System.Collections.Immutable;

namespace Game.Core.Cards;

public readonly record struct CardId(string Value);

public enum CardTarget
{
    Self,
    Opponent,
}

public enum StatusKind
{
    Bleed,
    Weak,
    Vulnerable,
}

public abstract record CardCost;
public sealed record NoCost() : CardCost;
public sealed record RequireMomentumCost(int Minimum) : CardCost;
public sealed record SpendMomentumCost(int Amount) : CardCost;
public sealed record SpendAllMomentumCost() : CardCost;
public sealed record SpendUpToMomentumCost(int Max) : CardCost;

public abstract record CardEffect;

public sealed record DamageCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record DamageIgnoringArmorCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record DamageNTimesCardEffect(int Amount, int Times, CardTarget Target) : CardEffect;
public sealed record DealDamagePerMomentumSpentCardEffect(int DamagePerMomentum, CardTarget Target) : CardEffect;
public sealed record DealDamagePerAllMomentumSpentCardEffect(int DamagePerMomentum, CardTarget Target) : CardEffect;
public sealed record DealDamagePerCurrentMomentumCardEffect(int DamagePerMomentum, CardTarget Target) : CardEffect;
public sealed record DealDamageToAllEnemiesCardEffect(int Amount) : CardEffect;
public sealed record DealDamageAndDrawPerCurrentMomentumCardEffect(int DamagePerMomentum, int DrawPerMomentum, CardTarget Target) : CardEffect;
public sealed record DamageWithAttackCountScalingCardEffect(int BaseAmount, int DamagePerAttackPlayedThisTurn, CardTarget Target) : CardEffect;
public sealed record GainArmorCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record ConditionalGainArmorIfMomentumAtLeastCardEffect(int MinimumMomentum, int Amount, CardTarget Target) : CardEffect;
public sealed record DrawCardsCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record HealCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record ApplyStatusCardEffect(StatusKind Status, int Amount, CardTarget Target) : CardEffect;
public sealed record ApplyBleedCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record ApplyStatusPerCurrentMomentumCardEffect(StatusKind Status, int BaseAmount, int AmountPerCurrentMomentum, CardTarget Target) : CardEffect;
public sealed record GainGeneratedMomentumCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record ReflectNextEnemyAttackDamageCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record AttackCountThisTurnToGmCardEffect(CardTarget Target) : CardEffect;
public sealed record RemoveEnemyArmorCardEffect(CardTarget Target) : CardEffect;
public sealed record RemoveAllArmorCardEffect(CardTarget Target) : CardEffect;
public sealed record NextAttackBonusDamageThisTurnCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record NextAttackDoubleThisTurnCardEffect(CardTarget Target) : CardEffect;
public sealed record NextAttackDoubleDamageThisTurnCardEffect(CardTarget Target) : CardEffect;
public sealed record TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record AllAttacksBonusDamageThisTurnCardEffect(int Amount, CardTarget Target) : CardEffect;
public sealed record TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect(CardTarget Target) : CardEffect;
public sealed record AllAttacksDoubleDamageThisTurnCardEffect(CardTarget Target) : CardEffect;
public sealed record LifestealPercentOfDamageDealtCardEffect(int Percent, CardTarget Target) : CardEffect;
public sealed record RepeatEffectsPerCurrentMomentumCardEffect(IReadOnlyList<CardEffect> Effects, CardTarget Target) : CardEffect;

public sealed record CardDefinition(
    CardId Id,
    string Name,
    int Cost,
    IReadOnlyList<CardEffect> Effects,
    IReadOnlyList<CardCost>? PlayCosts = null,
    IReadOnlySet<string>? Labels = null,
    string DeckAffinity = "",
    string Rarity = "",
    string RulesText = "")
{
    public CardDefinition(
        CardId Id,
        string Name,
        int Cost,
        IReadOnlyList<CardEffect> Effects,
        CardCost? PlayCost,
        IReadOnlySet<string>? Labels = null,
        string DeckAffinity = "",
        string Rarity = "",
        string RulesText = "")
        : this(Id, Name, Cost, Effects, PlayCost is null ? null : [PlayCost], Labels, DeckAffinity, Rarity, RulesText)
    {
    }

    public IReadOnlyList<CardCost> PlayCostsOrDefault => PlayCosts is null || PlayCosts.Count == 0 ? [new NoCost()] : PlayCosts;
    public IImmutableSet<string> LabelsOrEmpty => Labels is null ? ImmutableHashSet<string>.Empty : Labels.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);
    public bool HasLabel(string label) => LabelsOrEmpty.Contains(label);
}
