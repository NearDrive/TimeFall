using Game.Core.Common;

namespace Game.Core.Map;

public static class SampleMapFactory
{
    public static MapState CreateDefaultState()
    {
        var start = new Node(new NodeId("start"), NodeType.Start);
        var combat = new Node(new NodeId("combat-1"), NodeType.Combat);
        var shop = new Node(new NodeId("shop-1"), NodeType.Shop);
        var elite = new Node(new NodeId("elite-1"), NodeType.Elite);
        var rest = new Node(new NodeId("rest-1"), NodeType.Rest);
        var boss = new Node(new NodeId("boss-1"), NodeType.Boss);

        var nodes = new[] { start, combat, shop, elite, rest, boss };
        var connections = new (NodeId A, NodeId B)[]
        {
            (start.Id, combat.Id),
            (start.Id, shop.Id),
            (combat.Id, elite.Id),
            (shop.Id, rest.Id),
            (elite.Id, boss.Id),
            (rest.Id, boss.Id),
        };

        var graph = new MapGraph(nodes, connections);
        return MapState.Create(graph, start.Id, boss.Id);
    }

    public static MapState CreateZone1State(int seed)
    {
        var rng = GameRng.FromSeed(seed);

        var (bossDistance, afterDistance) = rng.NextInt(5, 11);
        rng = afterDistance;

        var minimumTotalNodes = Math.Max(20, bossDistance + 1);
        var (targetNodeCount, afterCount) = rng.NextInt(minimumTotalNodes, 31);
        rng = afterCount;

        var (restCount, afterRest) = rng.NextInt(1, 4);
        rng = afterRest;
        var (shopCount, afterShop) = rng.NextInt(0, 3);
        rng = afterShop;
        var (eliteCount, afterElite) = rng.NextInt(2, 6);
        rng = afterElite;
        var (eventCount, afterEvent) = rng.NextInt(1, 4);
        rng = afterEvent;

        var specialWithoutBossAndStart = restCount + shopCount + eliteCount + eventCount;
        var maxSpecialsAllowed = targetNodeCount - 2;
        if (specialWithoutBossAndStart > maxSpecialsAllowed)
        {
            throw new InvalidOperationException("Zone 1 constraints produced too many special nodes for the selected node count.");
        }

        var nodes = new List<Node>(targetNodeCount)
        {
            new(new NodeId("start"), NodeType.Start),
        };
        var connections = new List<(NodeId A, NodeId B)>();

        var previous = nodes[0].Id;
        for (var step = 1; step <= bossDistance; step++)
        {
            var id = step == bossDistance ? new NodeId("boss") : new NodeId($"path-{step}");
            var type = step == bossDistance ? NodeType.Boss : NodeType.Combat;
            nodes.Add(new Node(id, type));
            connections.Add((previous, id));
            previous = id;
        }

        var attachableBackbone = nodes
            .Select(node => node.Id)
            .Where(id => id != new NodeId("boss"))
            .ToArray();

        var extraNodesNeeded = targetNodeCount - nodes.Count;
        var branchIndex = 0;
        for (var i = 0; i < extraNodesNeeded; i++)
        {
            branchIndex++;
            var branchId = new NodeId($"branch-{branchIndex}");
            nodes.Add(new Node(branchId, NodeType.Combat));

            var (attachIndex, afterAttach) = rng.NextInt(0, attachableBackbone.Length);
            rng = afterAttach;
            var attachTo = attachableBackbone[attachIndex];
            connections.Add((attachTo, branchId));

            if (i > 0)
            {
                var shouldReconnectRoll = rng.NextInt(0, 100);
                rng = shouldReconnectRoll.NextRng;
                if (shouldReconnectRoll.Value < 35)
                {
                    var priorBranchNodes = nodes
                        .Where(node => node.Id.Value.StartsWith("branch-", StringComparison.Ordinal) && node.Id != branchId)
                        .Select(node => node.Id)
                        .ToArray();

                    if (priorBranchNodes.Length > 0)
                    {
                        var (reconnectIndex, afterReconnectIndex) = rng.NextInt(0, priorBranchNodes.Length);
                        rng = afterReconnectIndex;
                        var reconnectTo = priorBranchNodes[reconnectIndex];

                        if (reconnectTo != branchId)
                        {
                            connections.Add((branchId, reconnectTo));
                        }
                    }
                }
            }
        }

        var assignableNodeIds = nodes
            .Where(node => node.Type == NodeType.Combat)
            .Select(node => node.Id)
            .ToList();

        AssignNodeType(assignableNodeIds, nodes, NodeType.Elite, eliteCount, ref rng);
        AssignNodeType(assignableNodeIds, nodes, NodeType.Rest, restCount, ref rng);
        AssignNodeType(assignableNodeIds, nodes, NodeType.Event, eventCount, ref rng);
        AssignNodeType(assignableNodeIds, nodes, NodeType.Shop, shopCount, ref rng);

        var graph = new MapGraph(nodes, connections);
        return MapState.Create(graph, new NodeId("start"), new NodeId("boss"));
    }

    private static void AssignNodeType(List<NodeId> availableNodeIds, List<Node> nodes, NodeType targetType, int count, ref GameRng rng)
    {
        for (var i = 0; i < count; i++)
        {
            if (availableNodeIds.Count == 0)
            {
                throw new InvalidOperationException("No nodes left for assignment.");
            }

            var preferred = availableNodeIds
                .Select((id, index) => new { id, index, distance = ExtractNumericSuffix(id.Value) })
                .OrderBy(entry => entry.distance)
                .ThenBy(entry => entry.id.Value, StringComparer.Ordinal)
                .ToArray();

            var minWindow = Math.Min(4, preferred.Length);
            var (selectedInWindow, afterPick) = rng.NextInt(0, minWindow);
            rng = afterPick;
            var selected = preferred[selectedInWindow];

            var nodeIndex = nodes.FindIndex(node => node.Id == selected.id);
            nodes[nodeIndex] = nodes[nodeIndex] with { Type = targetType };
            availableNodeIds.RemoveAt(selected.index);
        }
    }

    private static int ExtractNumericSuffix(string value)
    {
        var suffix = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(suffix, out var parsed) ? parsed : int.MaxValue;
    }
}
