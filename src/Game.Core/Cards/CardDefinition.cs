namespace Game.Core.Cards;

public readonly record struct CardId(string Value);

public enum CardTarget
{
    Self,
    Opponent,
}

public abstract record CardEffect;

public sealed record DamageCardEffect(int Amount, CardTarget Target) : CardEffect;

public sealed record GainArmorCardEffect(int Amount, CardTarget Target) : CardEffect;

public sealed record CardDefinition(CardId Id, string Name, int Cost, IReadOnlyList<CardEffect> Effects);
