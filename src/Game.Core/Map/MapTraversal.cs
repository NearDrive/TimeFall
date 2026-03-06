using Game.Core.Common;

namespace Game.Core.Map;

public readonly record struct MapTraversalResult(MapState MapState, EncounterResolutionStatus EncounterStatus);

public static class MapTraversal
{
    public static Result<MapTraversalResult> MoveToNode(MapState state, NodeId targetNodeId)
    {
        if (!state.Graph.ContainsNode(targetNodeId))
        {
            return Result<MapTraversalResult>.Failure("Target node does not exist.");
        }

        if (!state.Graph.IsAdjacent(state.CurrentNodeId, targetNodeId))
        {
            return Result<MapTraversalResult>.Failure("Target node is not adjacent to current node.");
        }

        var moved = state with
        {
            CurrentNodeId = targetNodeId,
            VisitedNodeIds = state.VisitedNodeIds.Add(targetNodeId),
        };

        var resolution = EncounterResolver.Resolve(moved, targetNodeId);
        return Result<MapTraversalResult>.Success(new MapTraversalResult(resolution.MapState, resolution.Status));
    }
}
