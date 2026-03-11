using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.TimeSystem;

public static class NodeCollapseSystem
{
    // Deterministic collapse rule:
    // - Collapse advances from Start outward using shortest-path distance from Start.
    // - Boss node is excluded from normal collapse candidates.
    // - Ties are resolved by NodeId ordinal ordering.
    public static ImmutableList<NodeId> BuildDeterministicCollapseOrder(MapState mapState)
    {
        var bossNodeId = mapState.BossNodeId;

        return mapState.Graph.Nodes
            .Select(node => node.Id)
            .Where(nodeId => bossNodeId is null || nodeId != bossNodeId.Value)
            .Where(mapState.DistanceFromStart.ContainsKey)
            .OrderBy(nodeId => mapState.DistanceFromStart[nodeId])
            .ThenBy(nodeId => nodeId.Value, StringComparer.Ordinal)
            .ToImmutableList();
    }

    public static TimeState CollapseNextNode(TimeState timeState)
    {
        if (timeState.CollapseCursor >= timeState.CollapseOrder.Count)
        {
            return timeState;
        }

        var nextNode = timeState.CollapseOrder[timeState.CollapseCursor];
        return timeState with
        {
            CollapsedNodeIds = timeState.CollapsedNodeIds.Add(nextNode),
            CollapseCursor = timeState.CollapseCursor + 1,
        };
    }
}
