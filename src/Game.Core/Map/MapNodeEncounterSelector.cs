using Game.Core.Cards;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Game;

namespace Game.Core.Map;

public readonly record struct SelectedEncounter(
    CombatBlueprint Blueprint,
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    IReadOnlyList<CardId> RewardCardPool);

public static class MapNodeEncounterSelector
{
    public static bool TrySelect(
        NodeType nodeType,
        MapState mapState,
        GameRng rng,
        IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions,
        ZoneSpawnTable? zoneSpawnTable,
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions,
        IReadOnlyList<CardId> rewardCardPool,
        out SelectedEncounter selectedEncounter,
        out GameRng nextRng)
    {
        nextRng = rng;
        selectedEncounter = default;

        if (zoneSpawnTable is null || enemyDefinitions.Count == 0)
        {
            if (!PlaytestContent.TryCreateEncounter(nodeType, out var fallbackBlueprint))
            {
                return false;
            }

            selectedEncounter = new SelectedEncounter(
                Blueprint: fallbackBlueprint,
                CardDefinitions: cardDefinitions,
                RewardCardPool: rewardCardPool);
            return true;
        }

        var selectedEnemyId = nodeType switch
        {
            NodeType.Combat => SelectNormalEnemy(zoneSpawnTable, mapState, rng, out nextRng),
            NodeType.Elite => SelectEliteEnemy(zoneSpawnTable, rng, out nextRng),
            NodeType.Boss => zoneSpawnTable.BossEnemyId,
            _ => null,
        };

        if (selectedEnemyId is null || !enemyDefinitions.TryGetValue(selectedEnemyId, out var enemyDefinition))
        {
            return false;
        }

        selectedEncounter = new SelectedEncounter(
            Blueprint: EnemyEncounterFactory.CreateBlueprint(enemyDefinition),
            CardDefinitions: cardDefinitions,
            RewardCardPool: rewardCardPool);

        return true;
    }

    public static bool IsCombatNode(NodeType nodeType)
    {
        return nodeType is NodeType.Combat or NodeType.Elite or NodeType.Boss;
    }

    private static string? SelectNormalEnemy(ZoneSpawnTable table, MapState mapState, GameRng rng, out GameRng nextRng)
    {
        var band = GetNormalBand(mapState);
        if (!table.NormalEncounterPools.TryGetValue(band, out var bandPools))
        {
            nextRng = rng;
            return null;
        }

        var weightedPool = bandPools.Values.SelectMany(x => x).ToArray();
        return SelectByWeight(weightedPool, rng, out nextRng);
    }

    private static string? SelectEliteEnemy(ZoneSpawnTable table, GameRng rng, out GameRng nextRng)
    {
        if (!table.EliteEncounterPools.TryGetValue("EliteTierI", out var pool))
        {
            nextRng = rng;
            return null;
        }

        return SelectByWeight(pool, rng, out nextRng);
    }

    private static string GetNormalBand(MapState mapState)
    {
        var resolvedNormalCount = mapState.ResolvedEncounterNodeIds
            .Select(id => mapState.Graph.TryGetNode(id, out var node) ? node : null)
            .Where(node => node?.Type == NodeType.Combat)
            .Count();

        return resolvedNormalCount switch
        {
            0 => "early",
            1 => "mid",
            _ => "late",
        };
    }

    private static string? SelectByWeight(IReadOnlyList<WeightedEnemy> pool, GameRng rng, out GameRng nextRng)
    {
        var validPool = pool.Where(x => x.Weight > 0).ToArray();
        if (validPool.Length == 0)
        {
            nextRng = rng;
            return null;
        }

        var totalWeight = validPool.Sum(x => x.Weight);
        var (roll, updatedRng) = rng.NextInt(0, totalWeight);
        nextRng = updatedRng;

        var threshold = 0;
        foreach (var entry in validPool)
        {
            threshold += entry.Weight;
            if (roll < threshold)
            {
                return entry.EnemyId;
            }
        }

        return validPool[^1].EnemyId;
    }
}
