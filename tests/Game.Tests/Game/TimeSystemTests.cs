using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Game;

public class TimeSystemTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void Act1_TimeAdvancesEvery4MapTurns()
    {
        var state = CreateMapExplorationState(act: 1);

        state = Move(state, "combat-1");
        Assert.Equal(0, state.Time.CurrentStep);
        Assert.Equal(1, state.Time.MapTurnsSinceTimeAdvance);

        state = Move(state, "start");
        Assert.Equal(0, state.Time.CurrentStep);
        Assert.Equal(2, state.Time.MapTurnsSinceTimeAdvance);

        state = Move(state, "shop-1");
        Assert.Equal(0, state.Time.CurrentStep);
        Assert.Equal(3, state.Time.MapTurnsSinceTimeAdvance);

        state = Move(state, "start");
        Assert.Equal(1, state.Time.CurrentStep);
        Assert.Equal(0, state.Time.MapTurnsSinceTimeAdvance);
    }

    [Fact]
    public void Act2_TimeAdvancesEvery3MapTurns()
    {
        var state = CreateMapExplorationState(act: 2);

        state = Move(state, "combat-1");
        state = Move(state, "start");
        Assert.Equal(0, state.Time.CurrentStep);
        Assert.Equal(2, state.Time.MapTurnsSinceTimeAdvance);

        state = Move(state, "shop-1");
        Assert.Equal(1, state.Time.CurrentStep);
        Assert.Equal(0, state.Time.MapTurnsSinceTimeAdvance);
    }

    [Fact]
    public void Act3_TimeAdvancesEvery2MapTurns()
    {
        var state = CreateMapExplorationState(act: 3);

        state = Move(state, "combat-1");
        Assert.Equal(0, state.Time.CurrentStep);
        Assert.Equal(1, state.Time.MapTurnsSinceTimeAdvance);

        state = Move(state, "elite-1");
        Assert.Equal(1, state.Time.CurrentStep);
        Assert.Equal(0, state.Time.MapTurnsSinceTimeAdvance);
    }

    [Fact]
    public void CombatTurns_DoNotIncreaseMapTurnProgress()
    {
        var state = CreateMapExplorationState(act: 1);
        var initialProgress = state.Time.MapTurnsSinceTimeAdvance;

        var (combatState, _) = GameReducer.Reduce(state, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));
        var (afterTurn, _) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(initialProgress, combatState.Time.MapTurnsSinceTimeAdvance);
        Assert.Equal(initialProgress, afterTurn.Time.MapTurnsSinceTimeAdvance);
    }

    [Fact]
    public void RevisitingNode_StillCountsTowardTimePacing()
    {
        var state = CreateMapExplorationState(act: 1);

        var (afterFirstMove, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var (afterRevisit, _) = GameReducer.Reduce(afterFirstMove, new MoveToNodeAction(new NodeId("start")));

        Assert.Equal(2, afterRevisit.Time.MapTurnsSinceTimeAdvance);
        Assert.Equal(0, afterRevisit.Time.CurrentStep);
    }

    [Fact]
    public void NoCollapseOccursBeforeThreshold()
    {
        var state = CreateMapExplorationState(act: 1);

        var (afterFirstMove, firstEvents) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var (afterSecondMove, secondEvents) = GameReducer.Reduce(afterFirstMove, new MoveToNodeAction(new NodeId("start")));
        var (_, fourthEvents) = GameReducer.Reduce(
            GameReducer.Reduce(afterSecondMove, new MoveToNodeAction(new NodeId("shop-1"))).NewState,
            new MoveToNodeAction(new NodeId("start")));

        Assert.Empty(afterSecondMove.Time.CollapsedNodeIds);
        Assert.DoesNotContain(firstEvents, e => e is NodeCollapsed or TimeAdvanced);
        Assert.DoesNotContain(secondEvents, e => e is NodeCollapsed or TimeAdvanced);
        Assert.Contains(fourthEvents, e => e is TimeAdvanced { Step: 1 });
        Assert.Contains(fourthEvents, e => e is NodeCollapsed);
    }

    [Fact]
    public void TimeCaughtPlayer_StillWorks_WhenAdvanceOccursAtThreshold()
    {
        var state = CreateMapExplorationState(act: 3);

        state = Move(state, "combat-1");
        state = Move(state, "elite-1");
        state = Move(state, "combat-1");
        var (caughtState, events) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("elite-1")));

        Assert.True(caughtState.Time.PlayerCaughtByTime);
        Assert.True(caughtState.Time.TimeBossTriggerPending);
        Assert.Contains(events, e => e is TimeAdvanced { Step: 2 });
        Assert.Contains(events, e => e is TimeCaughtPlayer { NodeId: var id, Step: 2 } && id == new NodeId("elite-1"));
    }

    [Fact]
    public void CollapseRule_IsDeterministic()
    {
        var stateA = CreateMapExplorationState(act: 1);
        var stateB = CreateMapExplorationState(act: 1);

        var actions = new[]
        {
            new MoveToNodeAction(new NodeId("combat-1")),
            new MoveToNodeAction(new NodeId("start")),
            new MoveToNodeAction(new NodeId("shop-1")),
            new MoveToNodeAction(new NodeId("start")),
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
    public void BossNode_IsNeverCollapsedByTime()
    {
        var map = SampleMapFactory.CreateDefaultState();
        var time = TimeState.Create(map, act: 3);

        for (var i = 0; i < 20; i++)
        {
            time = TimeAdvancer.AdvanceForMapMove(time, playerNodeId: new NodeId("combat-1")).TimeState;
        }

        Assert.DoesNotContain(new NodeId("boss-1"), time.CollapsedNodeIds);
        Assert.DoesNotContain(new NodeId("boss-1"), time.CollapseOrder);
    }

    [Fact]
    public void CollapseOrder_FollowsDistanceFromStart()
    {
        var map = SampleMapFactory.CreateDefaultState();
        var time = TimeState.Create(map);

        var collapseDistances = time.CollapseOrder
            .Select(nodeId => map.DistanceFromStart[nodeId])
            .ToArray();

        Assert.Equal(collapseDistances.OrderBy(d => d).ToArray(), collapseDistances);
    }

    [Fact]
    public void CollapseOrder_UsesDeterministicTieBreak()
    {
        var start = new Node(new NodeId("start"), NodeType.Start);
        var c = new Node(new NodeId("c-node"), NodeType.Combat);
        var a = new Node(new NodeId("a-node"), NodeType.Combat);
        var boss = new Node(new NodeId("boss"), NodeType.Boss);
        var graph = new MapGraph(
            [start, c, a, boss],
            [(start.Id, c.Id), (start.Id, a.Id), (c.Id, boss.Id), (a.Id, boss.Id)]);
        var map = MapState.Create(graph, start.Id, boss.Id);

        var order = NodeCollapseSystem.BuildDeterministicCollapseOrder(map);

        Assert.Equal(new[] { new NodeId("start"), new NodeId("a-node"), new NodeId("c-node") }, order);
    }

    [Fact]
    public void TimeAdvance_DoesNotSelectArbitraryFarNodeBeforeNearerNode()
    {
        var map = SampleMapFactory.CreateDefaultState();
        var time = TimeState.Create(map, act: 1);

        var firstCollapsed = time.CollapseOrder[0];
        var laterNode = new NodeId("elite-1");

        Assert.True(map.DistanceFromStart[firstCollapsed] < map.DistanceFromStart[laterNode]);
        Assert.Equal(new NodeId("start"), firstCollapsed);
    }

    [Fact]
    public void BossRemainsReachableUnderNormalTimeAdvance()
    {
        var state = CreateMapExplorationState(act: 3);

        for (var i = 0; i < 6; i++)
        {
            state = Move(state, i % 2 == 0 ? "combat-1" : "elite-1");
        }

        Assert.False(state.Time.CollapsedNodeIds.Contains(new NodeId("boss-1")));
        Assert.True(HasPathWithoutCollapsedNodes(state.Map, state.Time.CollapsedNodeIds, state.Map.CurrentNodeId, new NodeId("boss-1")));
    }

    [Fact]
    public void TimeCollapse_IsDeterministicForSeedAndMap()
    {
        var mapA = SampleMapFactory.CreateZone1State(seed: 42);
        var mapB = SampleMapFactory.CreateZone1State(seed: 42);

        var timeA = TimeState.Create(mapA, act: 1);
        var timeB = TimeState.Create(mapB, act: 1);

        Assert.Equal(timeA.CollapseOrder, timeB.CollapseOrder);
    }

    private static GameState CreateMapExplorationState(int act)
    {
        var map = SampleMapFactory.CreateDefaultState();
        return GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map, act),
        };
    }

    private static GameState Move(GameState state, string nodeId)
    {
        return GameReducer.Reduce(state, new MoveToNodeAction(new NodeId(nodeId))).NewState;
    }

    private static bool HasPathWithoutCollapsedNodes(MapState map, IReadOnlySet<NodeId> collapsed, NodeId from, NodeId to)
    {
        if (collapsed.Contains(from) || collapsed.Contains(to))
        {
            return false;
        }

        var queue = new Queue<NodeId>();
        var seen = new HashSet<NodeId> { from };
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to)
            {
                return true;
            }

            foreach (var neighbor in map.Graph.GetNeighbors(current))
            {
                if (collapsed.Contains(neighbor))
                {
                    continue;
                }

                if (seen.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false;
    }
}
