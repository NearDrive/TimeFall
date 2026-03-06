using Game.Core.Cards;
using Game.Core.Combat;

namespace Game.Core.Game;

public sealed record CombatantBlueprint(
    string EntityId,
    int HP,
    int MaxHP,
    int Armor,
    IReadOnlyDictionary<ResourceType, int> Resources,
    IReadOnlyList<CardId> DrawPile);

public sealed record CombatBlueprint(
    CombatantBlueprint Player,
    CombatantBlueprint Enemy);
