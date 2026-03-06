namespace Game.Core.Map;

public enum EncounterResolutionStatus
{
    Resolved,
    AlreadyResolved,
}

public readonly record struct EncounterResolutionResult(MapState MapState, EncounterResolutionStatus Status);

public static class EncounterResolver
{
    public static EncounterResolutionResult Resolve(MapState mapState, NodeId nodeId)
    {
        if (mapState.ResolvedEncounterNodeIds.Contains(nodeId))
        {
            return new EncounterResolutionResult(mapState, EncounterResolutionStatus.AlreadyResolved);
        }

        var newState = mapState with
        {
            ResolvedEncounterNodeIds = mapState.ResolvedEncounterNodeIds.Add(nodeId),
        };

        return new EncounterResolutionResult(newState, EncounterResolutionStatus.Resolved);
    }
}
