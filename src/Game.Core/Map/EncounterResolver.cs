namespace Game.Core.Map;

public enum EncounterLifecycleStatus
{
    Triggered,
    AlreadyTriggered,
    Resolved,
    AlreadyResolved,
}

public readonly record struct EncounterLifecycleResult(MapState MapState, EncounterLifecycleStatus Status);

public static class EncounterResolver
{
    public static EncounterLifecycleResult Trigger(MapState mapState, NodeId nodeId)
    {
        if (mapState.TriggeredEncounterNodeIds.Contains(nodeId))
        {
            return new EncounterLifecycleResult(mapState, EncounterLifecycleStatus.AlreadyTriggered);
        }

        var newState = mapState with
        {
            TriggeredEncounterNodeIds = mapState.TriggeredEncounterNodeIds.Add(nodeId),
        };

        return new EncounterLifecycleResult(newState, EncounterLifecycleStatus.Triggered);
    }

    public static EncounterLifecycleResult Resolve(MapState mapState, NodeId nodeId)
    {
        if (mapState.ResolvedEncounterNodeIds.Contains(nodeId))
        {
            return new EncounterLifecycleResult(mapState, EncounterLifecycleStatus.AlreadyResolved);
        }

        var triggeredState = mapState.TriggeredEncounterNodeIds.Contains(nodeId)
            ? mapState
            : mapState with { TriggeredEncounterNodeIds = mapState.TriggeredEncounterNodeIds.Add(nodeId) };

        var newState = triggeredState with
        {
            ResolvedEncounterNodeIds = triggeredState.ResolvedEncounterNodeIds.Add(nodeId),
        };

        return new EncounterLifecycleResult(newState, EncounterLifecycleStatus.Resolved);
    }
}
