using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Game;

public class TimeSystemTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void MoveAction_AdvancesTimeByOne()
    {
        var state = CreateMapExplorationState();

        var (afterMove, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Equal(state.Time.CurrentStep + 1, afterMove.Time.CurrentStep);
    }

    [Fact]
    public void CombatTurns_DoNotAdvanceTime()
    {
        var state = CreateMapExplorationState();
        var initialTime = state.Time.CurrentStep;

        var (combatState, _) = GameReducer.Reduce(state, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));
        var (afterTurn, _) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(initialTime, combatState.Time.CurrentStep);
        Assert.Equal(initialTime, afterTurn.Time.CurrentStep);
    }

    [Fact]
    public void CollapsedNode_CannotBeEntered()
    {
        var collapsed = System.Collections.Immutable.ImmutableSortedSet.Create(MapState.NodeIdComparer, new NodeId("combat-1"));
        var state = CreateMapExplorationState() with { Time = CreateMapExplorationState().Time with { CollapsedNodeIds = collapsed } };

        var result = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Equal(state, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Movement_TriggersNodeCollapse()
    {
        var state = CreateMapExplorationState();

        var (afterMove, events) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Contains(new NodeId("boss-1"), afterMove.Time.CollapsedNodeIds);
        Assert.Contains(events, e => e is TimeAdvanced { Step: 1 });
        Assert.Contains(events, e => e is NodeCollapsed { NodeId: var id } && id == new NodeId("boss-1"));
    }

    [Fact]
    public void PlayerCaughtByTime_WhenCurrentNodeCollapses()
    {
        var state = CreateMapExplorationState();

        var (afterCombat, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var (afterElite, events) = GameReducer.Reduce(afterCombat, new MoveToNodeAction(new NodeId("elite-1")));

        Assert.True(afterElite.Time.PlayerCaughtByTime);
        Assert.True(afterElite.Time.TimeBossTriggerPending);
        Assert.Contains(events, e => e is TimeCaughtPlayer { NodeId: var nodeId, Step: 2 } && nodeId == new NodeId("elite-1"));
    }

    [Fact]
    public void RevisitingNode_StillAdvancesTime()
    {
        var state = CreateMapExplorationState();

        var (afterFirstMove, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var (afterRevisit, _) = GameReducer.Reduce(afterFirstMove, new MoveToNodeAction(new NodeId("start")));

        Assert.Equal(2, afterRevisit.Time.CurrentStep);
    }

    [Fact]
    public void CollapseRule_IsDeterministic()
    {
        var stateA = CreateMapExplorationState();
        var stateB = CreateMapExplorationState();

        var actions = new[]
        {
            new MoveToNodeAction(new NodeId("combat-1")),
            new MoveToNodeAction(new NodeId("start")),
            new MoveToNodeAction(new NodeId("shop-1")),
        };

        foreach (var action in actions)
        {
            (stateA, _) = GameReducer.Reduce(stateA, action);
            (stateB, _) = GameReducer.Reduce(stateB, action);
        }

        Assert.Equal(stateA.Time.CurrentStep, stateB.Time.CurrentStep);
        Assert.Equal(stateA.Time.CollapseCursor, stateB.Time.CollapseCursor);
        Assert.Equal(stateA.Time.CollapseOrder, stateB.Time.CollapseOrder);
        Assert.Equal(stateA.Time.CollapsedNodeIds, stateB.Time.CollapsedNodeIds);
    }

    [Fact]
    public void TimeState_HasSingleSourceOfTruthForCollapsedNodes()
    {
        Assert.Contains(nameof(TimeState.CollapsedNodeIds), typeof(TimeState).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("CollapsedNodeIds", typeof(MapState).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void MovingIntoCombatNode_ThenCombat_DoesNotAdvanceTimeFurther()
    {
        var state = CreateMapExplorationState();

        var (afterMove, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var timeAfterMove = afterMove.Time.CurrentStep;

        var (combatState, _) = GameReducer.Reduce(afterMove, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));
        var (afterTurn, _) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(1, timeAfterMove);
        Assert.Equal(timeAfterMove, combatState.Time.CurrentStep);
        Assert.Equal(timeAfterMove, afterTurn.Time.CurrentStep);
    }

    private static GameState CreateMapExplorationState()
    {
        var map = SampleMapFactory.CreateDefaultState();
        return GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map),
        };
    }
}
