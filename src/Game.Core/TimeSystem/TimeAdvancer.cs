using Game.Core.Map;

namespace Game.Core.TimeSystem;

public readonly record struct TimeAdvanceResult(TimeState TimeState, IReadOnlyList<NodeId> NewlyCollapsedNodes, bool PlayerCaughtThisStep, bool TimeAdvancedThisMove);

public static class TimeAdvancer
{
    public static TimeAdvanceResult AdvanceForMapMove(TimeState timeState, NodeId playerNodeId)
    {
        // A map turn is only counted when the player actually moves between map nodes.
        // Combat turns and non-movement interactions do not call this path.
        var progressed = timeState with
        {
            MapTurnsSinceTimeAdvance = timeState.MapTurnsSinceTimeAdvance + 1,
        };

        if (progressed.MapTurnsSinceTimeAdvance < progressed.TimeAdvanceInterval)
        {
            return new TimeAdvanceResult(progressed, Array.Empty<NodeId>(), PlayerCaughtThisStep: false, TimeAdvancedThisMove: false);
        }

        var stepped = progressed with
        {
            CurrentStep = progressed.CurrentStep + 1,
            MapTurnsSinceTimeAdvance = progressed.MapTurnsSinceTimeAdvance - progressed.TimeAdvanceInterval,
        };

        var collapsed = NodeCollapseSystem.CollapseNextNode(stepped);

        NodeId[] newlyCollapsed = [];
        if (collapsed.CollapseCursor > stepped.CollapseCursor)
        {
            newlyCollapsed = [collapsed.CollapseOrder[stepped.CollapseCursor]];
        }

        var caught = collapsed.CollapsedNodeIds.Contains(playerNodeId) && !collapsed.PlayerCaughtByTime;
        if (caught)
        {
            collapsed = TimeBossTrigger.MarkPlayerCaught(collapsed);
        }

        return new TimeAdvanceResult(collapsed, newlyCollapsed, caught, TimeAdvancedThisMove: true);
    }
}
