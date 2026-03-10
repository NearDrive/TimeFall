using Game.Core.Cards;
using Game.Core.Combat;

namespace Game.Core.Game;

public sealed record RunDeckDefinition(
    string Id,
    string Name,
    string? Description,
    ResourceType ResourceType,
    int BaseMaxHp,
    IReadOnlyList<CardId> StartingDeck,
    IReadOnlyDictionary<ResourceType, int> StartingResources);
