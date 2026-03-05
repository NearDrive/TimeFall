namespace Game.Core.Combat;

public sealed record CombatEntity(
    string EntityId,
    int HP,
    int MaxHP,
    int Armor,
    Dictionary<ResourceType, int> Resources,
    DeckState Deck);
