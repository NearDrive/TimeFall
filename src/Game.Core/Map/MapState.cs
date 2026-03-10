using System.Collections.Immutable;

namespace Game.Core.Map;

public sealed record MapState(
    MapGraph Graph,
    NodeId StartNodeId,
    NodeId CurrentNodeId,
    ImmutableDictionary<NodeId, int> DistanceFromStart,
    ImmutableSortedSet<NodeId> VisitedNodeIds,
    ImmutableSortedSet<NodeId> TriggeredEncounterNodeIds,
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

        var distances = graph.GetDistancesFrom(startNodeId);

        return new MapState(
            Graph: graph,
            StartNodeId: startNodeId,
            CurrentNodeId: startNodeId,
            DistanceFromStart: distances,
            VisitedNodeIds: ImmutableSortedSet.Create(NodeIdComparer, startNodeId),
            TriggeredEncounterNodeIds: ImmutableSortedSet.Create<NodeId>(NodeIdComparer),
            ResolvedEncounterNodeIds: ImmutableSortedSet.Create<NodeId>(NodeIdComparer),
            BossNodeId: bossNodeId);
    }

    public bool TryGetDistanceFromStart(NodeId nodeId, out int distance)
    {
        return DistanceFromStart.TryGetValue(nodeId, out distance);
    }
}
