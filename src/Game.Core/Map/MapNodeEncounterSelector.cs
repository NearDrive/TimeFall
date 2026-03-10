using Game.Core.Cards;
using GameRng = Game.Core.Common.GameRng;
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

        IReadOnlyList<string>? selectedEnemyIds = nodeType switch
        {
            NodeType.Combat => SelectNormalEnemyGroup(zoneSpawnTable, mapState, rng, enemyDefinitions, out nextRng),
            NodeType.Elite => SelectEliteEnemy(zoneSpawnTable, rng, out nextRng) is { } elite ? [elite] : null,
            NodeType.Boss => [zoneSpawnTable.BossEnemyId],
            _ => null,
        };

        if (selectedEnemyIds is null || selectedEnemyIds.Count == 0)
        {
            return false;
        }

        var selectedEnemies = new List<EnemyDefinition>(selectedEnemyIds.Count);
        foreach (var enemyId in selectedEnemyIds)
        {
            if (!enemyDefinitions.TryGetValue(enemyId, out var enemyDefinition))
            {
                return false;
            }

            selectedEnemies.Add(enemyDefinition);
        }

        selectedEncounter = new SelectedEncounter(
            Blueprint: EnemyEncounterFactory.CreateBlueprint(selectedEnemies),
            CardDefinitions: cardDefinitions,
            RewardCardPool: rewardCardPool);

        return true;
    }

    public static bool IsCombatNode(NodeType nodeType)
    {
        return nodeType is NodeType.Combat or NodeType.Elite or NodeType.Boss;
    }

    private static IReadOnlyList<string>? SelectNormalEnemyGroup(ZoneSpawnTable table, MapState mapState, GameRng rng, IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions, out GameRng nextRng)
    {
        var distanceFromStart = GetDistanceFromStart(mapState);
        var budget = Math.Max(1, distanceFromStart);
        var band = GetNormalBandByDistance(distanceFromStart);

        if (!table.NormalEncounterPools.TryGetValue(band, out var bandPools))
        {
            nextRng = rng;
            return null;
        }

        var maxGroupSize = Math.Min(3, budget);
        var desiredGroupSize = SelectDesiredGroupSize(budget, maxGroupSize, rng, out var groupSizeRng);

        var result = new List<string>(desiredGroupSize);
        var remainingBudget = budget;
        var currentRng = groupSizeRng;

        for (var index = 0; index < desiredGroupSize; index++)
        {
            var remainingSlots = desiredGroupSize - index;
            var maxTierValueForSlot = remainingBudget - (remainingSlots - 1);
            if (maxTierValueForSlot <= 0)
            {
                break;
            }

            var tierCandidates = bandPools
                .Select(kvp => new
                {
                    Tier = kvp.Key,
                    Value = EnemyTierBudget.GetNormalTierValue(kvp.Key),
                    Pool = kvp.Value,
                })
                .Where(candidate => candidate.Value > 0 && candidate.Value <= maxTierValueForSlot && candidate.Pool.Count > 0)
                .ToArray();

            if (tierCandidates.Length == 0)
            {
                break;
            }

            var tierWeightedPool = tierCandidates
                .Select(candidate => new WeightedEnemy(candidate.Tier, Math.Max(1, candidate.Pool.Sum(entry => Math.Max(0, entry.Weight)))))
                .ToArray();

            var selectedTier = SelectByWeight(tierWeightedPool, currentRng, out currentRng);
            if (selectedTier is null)
            {
                break;
            }

            var tierCandidate = tierCandidates.First(candidate => StringComparer.Ordinal.Equals(candidate.Tier, selectedTier));
            var selectedEnemyId = SelectByWeight(
                tierCandidate.Pool.Where(candidate => IsEnemyAllowedForNormalGroup(candidate.EnemyId, result, enemyDefinitions)).ToArray(),
                currentRng,
                out currentRng);
            if (selectedEnemyId is null)
            {
                break;
            }

            result.Add(selectedEnemyId);
            remainingBudget -= tierCandidate.Value;
        }

        nextRng = currentRng;
        return result.Count > 0 ? result : null;
    }


    private static bool IsEnemyAllowedForNormalGroup(
        string enemyId,
        IReadOnlyCollection<string> currentGroup,
        IReadOnlyDictionary<string, EnemyDefinition> enemyDefinitions)
    {
        if (!enemyDefinitions.TryGetValue(enemyId, out var candidate))
        {
            return false;
        }

        if (!string.Equals(candidate.Role, "Armor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return currentGroup
            .Select(existingId => enemyDefinitions.TryGetValue(existingId, out var existing) ? existing : null)
            .All(existing => existing is null || !string.Equals(existing.Role, "Armor", StringComparison.OrdinalIgnoreCase));
    }

    private static int SelectDesiredGroupSize(int budget, int maxGroupSize, GameRng rng, out GameRng nextRng)
    {
        if (maxGroupSize <= 1)
        {
            nextRng = rng;
            return 1;
        }

        WeightedEnemy[] sizeWeights = budget switch
        {
            2 => [new WeightedEnemy("1", 65), new WeightedEnemy("2", 35)],
            _ => [new WeightedEnemy("1", 20), new WeightedEnemy("2", 35), new WeightedEnemy("3", 45)],
        };

        var filtered = sizeWeights
            .Where(entry => int.Parse(entry.EnemyId) <= maxGroupSize)
            .ToArray();

        var chosen = SelectByWeight(filtered, rng, out nextRng);
        return chosen is null ? 1 : int.Parse(chosen);
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

    private static string GetNormalBandByDistance(int distanceFromStart)
    {
        return distanceFromStart switch
        {
            <= 1 => "early",
            2 => "mid",
            _ => "late",
        };
    }

    private static int GetDistanceFromStart(MapState mapState)
    {
        return mapState.TryGetDistanceFromStart(mapState.CurrentNodeId, out var distance)
            ? distance
            : 1;
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
