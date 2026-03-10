using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.TimeSystem;

public sealed record TimeState(
    int CurrentStep,
    int CurrentAct,
    int MapTurnsSinceTimeAdvance,
    int TimeAdvanceInterval,
    ImmutableSortedSet<NodeId> CollapsedNodeIds,
    ImmutableList<NodeId> CollapseOrder,
    int CollapseCursor,
    bool PlayerCaughtByTime,
    bool TimeBossTriggerPending)
{
    public static TimeState Create(MapState mapState, int act = 1)
    {
        var collapseOrder = NodeCollapseSystem.BuildDeterministicCollapseOrder(mapState);
        var interval = ResolveTimeAdvanceInterval(act);

        return new TimeState(
            CurrentStep: 0,
            CurrentAct: act,
            MapTurnsSinceTimeAdvance: 0,
            TimeAdvanceInterval: interval,
            CollapsedNodeIds: ImmutableSortedSet.Create(MapState.NodeIdComparer),
            CollapseOrder: collapseOrder,
            CollapseCursor: 0,
            PlayerCaughtByTime: false,
            TimeBossTriggerPending: false);
    }

    public static int ResolveTimeAdvanceInterval(int act)
    {
        return act switch
        {
            <= 1 => 4,
            2 => 3,
            _ => 2,
        };
    }
}
