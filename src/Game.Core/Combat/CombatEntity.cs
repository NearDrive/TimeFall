using System.Collections.Immutable;

namespace Game.Core.Combat;

public sealed record CombatEntity(
    string EntityId,
    int HP,
    int MaxHP,
    int Armor,
    ImmutableDictionary<ResourceType, int> Resources,
    DeckState Deck);
