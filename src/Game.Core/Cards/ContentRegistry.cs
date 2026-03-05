namespace Game.Core.Cards;

public static class ContentRegistry
{
    public static readonly IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions =
        new Dictionary<CardId, CardDefinition>
        {
            [new CardId("strike")] = new(new CardId("strike"), "Strike", 1),
            [new CardId("defend")] = new(new CardId("defend"), "Defend", 1),
            [new CardId("focus") ] = new(new CardId("focus"), "Focus", 1),
        };
}
