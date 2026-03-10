using Game.Core.Map;

namespace Game.Tests.Map;

public class Zone1MapGenerationTests
{
    private const int Seed = 1337;

    [Fact]
    public void Zone1Map_HasExactlyOneStart()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);

        Assert.Equal(1, map.Graph.Nodes.Count(node => node.Type == NodeType.Start));
        Assert.Equal(new NodeId("start"), map.StartNodeId);
    }

    [Fact]
    public void Zone1Map_HasExactlyOneBoss()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);

        Assert.Equal(1, map.Graph.Nodes.Count(node => node.Type == NodeType.Boss));
        Assert.NotNull(map.BossNodeId);
    }

    [Fact]
    public void Zone1Map_TotalNodes_IsBetween20And30()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);

        Assert.InRange(map.Graph.Nodes.Count, 20, 30);
    }

    [Fact]
    public void Zone1Map_BossDistance_IsBetween5And10()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);

        Assert.True(map.BossNodeId is not null);
        Assert.True(map.TryGetDistanceFromStart(map.BossNodeId.Value, out var bossDistance));
        Assert.InRange(bossDistance, 5, 10);
    }

    [Fact]
    public void Zone1Map_RestCount_IsWithinBounds()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);
        var count = map.Graph.Nodes.Count(node => node.Type == NodeType.Rest);

        Assert.InRange(count, 1, 3);
    }

    [Fact]
    public void Zone1Map_ShopCount_IsWithinBounds()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);
        var count = map.Graph.Nodes.Count(node => node.Type == NodeType.Shop);

        Assert.InRange(count, 0, 2);
    }

    [Fact]
    public void Zone1Map_EliteCount_IsWithinBounds()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);
        var count = map.Graph.Nodes.Count(node => node.Type == NodeType.Elite);

        Assert.InRange(count, 2, 5);
    }

    [Fact]
    public void Zone1Map_EventCount_IsWithinBounds()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);
        var count = map.Graph.Nodes.Count(node => node.Type == NodeType.Event);

        Assert.InRange(count, 1, 3);
    }


    [Fact]
    public void Zone1Map_NoEliteAtDistance1FromStart()
    {
        for (var seed = 1; seed <= 100; seed++)
        {
            var map = SampleMapFactory.CreateZone1State(seed);
            var invalidElite = map.Graph.Nodes
                .Where(node => node.Type == NodeType.Elite)
                .Any(node => map.TryGetDistanceFromStart(node.Id, out var distance) && distance == 1);

            Assert.False(invalidElite, $"Seed {seed} produced an elite adjacent to Start.");
        }
    }

    [Fact]
    public void Zone1Map_RemainingNodes_AreNormalCombat()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);

        var nonCombatTypes = new[] { NodeType.Start, NodeType.Boss, NodeType.Rest, NodeType.Shop, NodeType.Elite, NodeType.Event };
        var remaining = map.Graph.Nodes.Where(node => !nonCombatTypes.Contains(node.Type)).ToArray();

        Assert.All(remaining, node => Assert.Equal(NodeType.Combat, node.Type));
    }

    [Fact]
    public void Zone1Map_BossReachableFromStart()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);

        Assert.True(map.BossNodeId is not null);
        Assert.True(map.Graph.TryGetShortestPathDistance(map.StartNodeId, map.BossNodeId.Value, out _));
        Assert.Equal(map.Graph.Nodes.Count, map.DistanceFromStart.Count);
    }

    [Fact]
    public void Zone1Map_DistancesFromStart_AreStableAndQueryable()
    {
        var map = SampleMapFactory.CreateZone1State(Seed);
        var firstSnapshot = map.DistanceFromStart;
        var recomputed = map.Graph.GetDistancesFrom(map.StartNodeId);

        foreach (var node in map.Graph.Nodes)
        {
            Assert.True(map.TryGetDistanceFromStart(node.Id, out var distance));
            Assert.Equal(firstSnapshot[node.Id], distance);
            Assert.Equal(recomputed[node.Id], distance);
        }
    }

    [Fact]
    public void Zone1Map_Generation_IsDeterministicForSeed()
    {
        var mapA = SampleMapFactory.CreateZone1State(Seed);
        var mapB = SampleMapFactory.CreateZone1State(Seed);

        Assert.Equal(mapA.StartNodeId, mapB.StartNodeId);
        Assert.Equal(mapA.BossNodeId, mapB.BossNodeId);
        Assert.Equal(mapA.Graph.Nodes.OrderBy(n => n.Id.Value).ToArray(), mapB.Graph.Nodes.OrderBy(n => n.Id.Value).ToArray());
        Assert.Equal(mapA.DistanceFromStart.OrderBy(kvp => kvp.Key.Value).ToArray(), mapB.DistanceFromStart.OrderBy(kvp => kvp.Key.Value).ToArray());

        foreach (var node in mapA.Graph.Nodes)
        {
            var neighborsA = mapA.Graph.GetNeighbors(node.Id).OrderBy(id => id.Value).ToArray();
            var neighborsB = mapB.Graph.GetNeighbors(node.Id).OrderBy(id => id.Value).ToArray();
            Assert.Equal(neighborsA, neighborsB);
        }
    }
}
