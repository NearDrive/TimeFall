using System.Collections.Immutable;

namespace Game.Core.Map;

public sealed record MapState(
    MapGraph Graph,
    NodeId CurrentNodeId,
    ImmutableSortedSet<NodeId> VisitedNodeIds,
    ImmutableSortedSet<NodeId> ResolvedEncounterNodeIds,
    NodeId? BossNodeId)
{
    public static IComparer<NodeId> NodeIdComparer { get; } = Comparer<NodeId>.Create((a, b) => StringComparer.Ordinal.Compare(a.Value, b.Value));

    public static MapState Create(MapGraph graph, NodeId startNodeId, NodeId? bossNodeId)
    {
        if (!graph.ContainsNode(startNodeId))
        {
            throw new ArgumentException("Start node must exist in graph.", nameof(startNodeId));
        }

        if (bossNodeId is { } boss && !graph.ContainsNode(boss))
        {
            throw new ArgumentException("Boss node must exist in graph.", nameof(bossNodeId));
        }

        return new MapState(
            Graph: graph,
            CurrentNodeId: startNodeId,
            VisitedNodeIds: ImmutableSortedSet.Create(NodeIdComparer, startNodeId),
            ResolvedEncounterNodeIds: ImmutableSortedSet.Create<NodeId>(NodeIdComparer),
            BossNodeId: bossNodeId);
    }
}
