using Game.Core.Map;

namespace Game.Core.TimeSystem;

public readonly record struct TimeAdvanceResult(TimeState TimeState, IReadOnlyList<NodeId> NewlyCollapsedNodes, bool PlayerCaughtThisStep);

public static class TimeAdvancer
{
    public static TimeAdvanceResult Advance(TimeState timeState, NodeId playerNodeId)
    {
        var stepped = timeState with { CurrentStep = timeState.CurrentStep + 1 };
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

        return new TimeAdvanceResult(collapsed, newlyCollapsed, caught);
    }
}
