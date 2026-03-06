using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.TimeSystem;

public sealed record TimeState(
    int CurrentStep,
    ImmutableSortedSet<NodeId> CollapsedNodeIds,
    ImmutableList<NodeId> CollapseOrder,
    int CollapseCursor,
    bool PlayerCaughtByTime,
    bool TimeBossTriggerPending)
{
    public static TimeState Create(MapState mapState)
    {
        var collapseOrder = NodeCollapseSystem.BuildDeterministicCollapseOrder(mapState);

        return new TimeState(
            CurrentStep: 0,
            CollapsedNodeIds: ImmutableSortedSet.Create(MapState.NodeIdComparer),
            CollapseOrder: collapseOrder,
            CollapseCursor: 0,
            PlayerCaughtByTime: false,
            TimeBossTriggerPending: false);
    }
}
