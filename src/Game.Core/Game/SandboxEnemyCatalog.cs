using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.Game;

public static class SandboxEnemyCatalog
{
    public const string InfiniteHpEnemyId = "sandbox-infinite-hp";

    public static IReadOnlyList<string> GetEnemyIds(IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions)
    {
        return enemyDefinitions.Keys
            .Append(InfiniteHpEnemyId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool IsSandboxEnemyId(string enemyId, IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions)
    {
        return string.Equals(enemyId, InfiniteHpEnemyId, StringComparison.Ordinal) ||
               enemyDefinitions.ContainsKey(enemyId);
    }

    public static CombatantBlueprint CreateEnemyBlueprint(
        string enemyId,
        IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions)
    {
        if (string.Equals(enemyId, InfiniteHpEnemyId, StringComparison.Ordinal))
        {
            return new CombatantBlueprint(
                EntityId: InfiniteHpEnemyId,
                HP: int.MaxValue,
                MaxHP: int.MaxValue,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: ImmutableList<CardId>.Empty);
        }

        var enemy = enemyDefinitions[enemyId];
        return EnemyEncounterFactory.CreateBlueprint(enemy).Enemies[0];
    }
}
