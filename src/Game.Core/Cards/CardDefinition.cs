namespace Game.Core.Cards;

public readonly record struct CardId(string Value);

public sealed record CardDefinition(CardId Id, string Name, int Cost);
