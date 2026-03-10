using GameRng = Game.Core.Common.GameRng;
using Game.Core.Game;
using Game.Core.Map;
using MapNodeId = Game.Core.Map.NodeId;
using Game.Data.Content;

namespace Game.Tests.Game;

public class EncounterGenerationTests
{
    [Fact]
    public void Distance1_NormalEncounter_HasTierSumAtMost1()
    {
        var encounter = SelectNormalEncounter(distanceFromStart: 1, seed: 7);

        Assert.InRange(GetTierSum(encounter), 1, 1);
    }

    [Fact]
    public void Distance2_NormalEncounter_HasTierSumAtMost2()
    {
        var encounter = SelectNormalEncounter(distanceFromStart: 2, seed: 19);

        Assert.InRange(GetTierSum(encounter), 1, 2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void DistanceN_NormalEncounter_NeverExceedsBudget(int distance)
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var encounter = SelectNormalEncounter(distance, seed);
            Assert.True(GetTierSum(encounter) <= distance, $"Distance {distance}, Seed {seed}, TierSum {GetTierSum(encounter)}");
        }
    }


    [Fact]
    public void NormalEncounterGenerator_AvoidsDuplicateArmorWalls()
    {
        var mapState = CreateMapStateAtDistance(distanceFromStart: 2);

        for (var seed = 1; seed <= 150; seed++)
        {
            var selected = MapNodeEncounterSelector.TrySelect(
                NodeType.Combat,
                mapState,
                GameRng.FromSeed(seed),
                Content.EnemyDefinitions,
                Content.Zone1SpawnTable,
                Content.CardDefinitions,
                Content.RewardCardPool,
                out var encounter,
                out _);

            Assert.True(selected);
            var armorCount = encounter.Blueprint.Enemies
                .Select(enemy => Content.EnemyDefinitions[enemy.EntityId])
                .Count(enemy => string.Equals(enemy.Role, "Armor", StringComparison.OrdinalIgnoreCase));

            Assert.True(armorCount <= 1, $"Seed {seed} generated more than one Armor-role enemy.");
        }
    }

    [Fact]
    public void ValidMixedNormalGroups_StillGenerate()
    {
        var mapState = CreateMapStateAtDistance(distanceFromStart: 2);

        var foundMixed = false;
        for (var seed = 1; seed <= 300; seed++)
        {
            var selected = MapNodeEncounterSelector.TrySelect(
                NodeType.Combat,
                mapState,
                GameRng.FromSeed(seed),
                Content.EnemyDefinitions,
                Content.Zone1SpawnTable,
                Content.CardDefinitions,
                Content.RewardCardPool,
                out var encounter,
                out _);

            Assert.True(selected);
            var roles = encounter.Blueprint.Enemies
                .Select(enemy => Content.EnemyDefinitions[enemy.EntityId].Role)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (encounter.Blueprint.Enemies.Count > 1 && roles.Length > 1)
            {
                foundMixed = true;
                break;
            }
        }

        Assert.True(foundMixed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void NormalEncounter_GroupSize_IsBetween1And3(int distance)
    {
        var encounter = SelectNormalEncounter(distance, seed: 77);

        Assert.InRange(encounter.Blueprint.Enemies.Count, 1, 3);
    }

    [Fact]
    public void EliteEncounter_SpawnsExactlyOneEnemy()
    {
        var selected = MapNodeEncounterSelector.TrySelect(
            NodeType.Elite,
            CreateMapStateAtDistance(distanceFromStart: 2),
            GameRng.FromSeed(33),
            Content.EnemyDefinitions,
            Content.Zone1SpawnTable,
            Content.CardDefinitions,
            Content.RewardCardPool,
            out var encounter,
            out _);

        Assert.True(selected);
        Assert.Single(encounter.Blueprint.Enemies);
        Assert.Equal("EliteTierI", Content.EnemyDefinitions[encounter.Blueprint.Enemy.EntityId].Tier);
    }

    [Fact]
    public void BossEncounter_RemainsSeparateFromNormalGroupGeneration()
    {
        var selected = MapNodeEncounterSelector.TrySelect(
            NodeType.Boss,
            CreateMapStateAtDistance(distanceFromStart: 4),
            GameRng.FromSeed(9),
            Content.EnemyDefinitions,
            Content.Zone1SpawnTable,
            Content.CardDefinitions,
            Content.RewardCardPool,
            out var encounter,
            out _);

        Assert.True(selected);
        Assert.Single(encounter.Blueprint.Enemies);
        Assert.Equal(Content.Zone1SpawnTable!.BossEnemyId, encounter.Blueprint.Enemy.EntityId);
    }

    [Fact]
    public void GroupGeneration_IsDeterministicForSeedAndDistance()
    {
        var mapState = CreateMapStateAtDistance(distanceFromStart: 3);

        var selectedA = MapNodeEncounterSelector.TrySelect(
            NodeType.Combat,
            mapState,
            GameRng.FromSeed(1234),
            Content.EnemyDefinitions,
            Content.Zone1SpawnTable,
            Content.CardDefinitions,
            Content.RewardCardPool,
            out var encounterA,
            out _);

        var selectedB = MapNodeEncounterSelector.TrySelect(
            NodeType.Combat,
            mapState,
            GameRng.FromSeed(1234),
            Content.EnemyDefinitions,
            Content.Zone1SpawnTable,
            Content.CardDefinitions,
            Content.RewardCardPool,
            out var encounterB,
            out _);

        Assert.True(selectedA);
        Assert.True(selectedB);
        Assert.Equal(encounterA.Blueprint.Enemies.Select(x => x.EntityId), encounterB.Blueprint.Enemies.Select(x => x.EntityId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void GeneratedEnemies_ComeFromAllowedNormalPoolsOnly(int distance)
    {
        var encounter = SelectNormalEncounter(distance, seed: 41);
        var allowedNormalIds = Content.Zone1SpawnTable!.NormalEncounterPools
            .Values
            .SelectMany(x => x.Values)
            .SelectMany(x => x)
            .Select(x => x.EnemyId)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(encounter.Blueprint.Enemies, enemy => Assert.Contains(enemy.EntityId, allowedNormalIds));
    }

    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    private static SelectedEncounter SelectNormalEncounter(int distanceFromStart, int seed)
    {
        var selected = MapNodeEncounterSelector.TrySelect(
            NodeType.Combat,
            CreateMapStateAtDistance(distanceFromStart),
            GameRng.FromSeed(seed),
            Content.EnemyDefinitions,
            Content.Zone1SpawnTable,
            Content.CardDefinitions,
            Content.RewardCardPool,
            out var encounter,
            out _);

        Assert.True(selected);
        return encounter;
    }

    private static int GetTierSum(SelectedEncounter encounter)
    {
        return encounter.Blueprint.Enemies
            .Select(enemy => Content.EnemyDefinitions[enemy.EntityId])
            .Sum(enemy => EnemyTierBudget.GetNormalTierValue(enemy.Tier));
    }

    private static MapState CreateMapStateAtDistance(int distanceFromStart)
    {
        var start = new Node(new MapNodeId("start"), NodeType.Start);
        var nodes = new List<Node> { start };
        var connections = new List<(MapNodeId A, MapNodeId B)>();

        var previous = start;
        for (var i = 1; i <= distanceFromStart; i++)
        {
            var node = new Node(new MapNodeId($"step-{i}"), NodeType.Combat);
            nodes.Add(node);
            connections.Add((previous.Id, node.Id));
            previous = node;
        }

        var graph = new MapGraph(nodes, connections);
        return MapState.Create(graph, start.Id, bossNodeId: null) with { CurrentNodeId = previous.Id };
    }
}
