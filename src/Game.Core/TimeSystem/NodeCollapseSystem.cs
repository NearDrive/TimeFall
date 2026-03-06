using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.TimeSystem;

public static class NodeCollapseSystem
{
    // Placeholder deterministic rule:
    // - Build a stable collapse order with a BFS wave that starts from the boss node.
    // - Ties inside each wave are resolved by NodeId ordinal ordering.
    // - Exactly one node collapses per time step according to this order.
    // This is deterministic and map-driven while staying simple to test.
    public static ImmutableList<NodeId> BuildDeterministicCollapseOrder(MapState mapState)
    {
        var sourceNodeId = mapState.BossNodeId
            ?? mapState.Graph.Nodes.OrderBy(node => node.Id.Value, StringComparer.Ordinal).First().Id;

        var queue = new Queue<NodeId>();
        var seen = new HashSet<NodeId>();
        var order = new List<NodeId>();

        queue.Enqueue(sourceNodeId);
        seen.Add(sourceNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            var neighbors = mapState.Graph
                .GetNeighbors(current)
                .OrderBy(id => id.Value, StringComparer.Ordinal)
                .ToArray();

            foreach (var neighbor in neighbors)
            {
                if (seen.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return order.ToImmutableList();
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
