using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Core.Map;

public sealed record EnemyDefinition(
    string Id,
    string Name,
    int Zone,
    string Tier,
    string Category,
    string Role,
    int Hp,
    int StartingArmor,
    IReadOnlyList<CardId> Deck,
    IReadOnlyList<string>? Tags,
    string? Notes);

public sealed record WeightedEnemy(string EnemyId, int Weight);

public sealed record ZoneSpawnTable(
    int Zone,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<WeightedEnemy>>> NormalEncounterPools,
    IReadOnlyDictionary<string, IReadOnlyList<WeightedEnemy>> EliteEncounterPools,
    string BossEnemyId);

public static class EnemyEncounterFactory
{
    public static CombatBlueprint CreateBlueprint(EnemyDefinition enemyDefinition)
    {
        return new CombatBlueprint(
            Player: CreatePlayerBlueprint(),
            Enemies: [new CombatantBlueprint(
                EntityId: enemyDefinition.Id,
                HP: enemyDefinition.Hp,
                MaxHP: enemyDefinition.Hp,
                Armor: enemyDefinition.StartingArmor,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: enemyDefinition.Deck)]);
    }

    private static CombatantBlueprint CreatePlayerBlueprint()
    {
        return new CombatantBlueprint(
            EntityId: "player",
            HP: 80,
            MaxHP: 80,
            Armor: 0,
            Resources: new Dictionary<ResourceType, int> { [ResourceType.Energy] = 3 },
            DrawPile:
            [
                new CardId("strike"),
                new CardId("strike"),
                new CardId("strike"),
                new CardId("strike"),
                new CardId("strike"),
                new CardId("guard"),
                new CardId("guard"),
                new CardId("guard"),
                new CardId("quick-draw"),
                new CardId("quick-draw"),
            ]);
    }
}
