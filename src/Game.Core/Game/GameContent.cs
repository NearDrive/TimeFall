using Game.Core.Cards;
using Game.Core.Combat;
using System.Collections.Immutable;

namespace Game.Core.Game;

public sealed record CombatantBlueprint(
    string EntityId,
    int HP,
    int MaxHP,
    int Armor,
    IReadOnlyDictionary<ResourceType, int> Resources,
    IReadOnlyList<CardId> DrawPile);

public sealed record CombatBlueprint
{
    public CombatBlueprint(CombatantBlueprint Player, CombatantBlueprint Enemy)
        : this(Player, ImmutableList.Create(Enemy))
    {
    }

    public CombatBlueprint(CombatantBlueprint Player, IReadOnlyList<CombatantBlueprint> Enemies)
    {
        this.Player = Player;
        this.Enemies = Enemies;
    }

    public CombatantBlueprint Player { get; init; }

    public IReadOnlyList<CombatantBlueprint> Enemies { get; init; }

    public CombatantBlueprint Enemy => Enemies[0];
}
