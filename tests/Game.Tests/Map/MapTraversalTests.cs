using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Map;

public class MapTraversalTests
{
    [Fact]
    public void MoveToAdjacentNode_Succeeds()
    {
        var state = CreateMapExplorationState();
        var target = new NodeId("combat-1");

        var (newState, events) = GameReducer.Reduce(state, new MoveToNodeAction(target));

        Assert.Equal(target, newState.Map.CurrentNodeId);
        Assert.Contains(target, newState.Map.VisitedNodeIds);
        Assert.Contains(events, e => e is MovedToNode { NodeId: var id } && id == target);
    }

    [Fact]
    public void MoveToNonAdjacentNode_IsRejected()
    {
        var state = CreateMapExplorationState();
        var nonAdjacentExisting = new NodeId("elite-1");

        var (newState, events) = GameReducer.Reduce(state, new MoveToNodeAction(nonAdjacentExisting));

        Assert.Equal(state, newState);
        Assert.Empty(events);
    }

    [Fact]
    public void MoveToUnknownNode_IsRejected()
    {
        var state = CreateMapExplorationState();

        var (newState, events) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("unknown")));

        Assert.Equal(state, newState);
        Assert.Empty(events);
    }

    [Fact]
    public void RevisitingNode_DoesNotResolveEncounterTwice()
    {
        var state = CreateMapExplorationState();

        var (afterFirstVisit, firstEvents) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("shop-1")));
        var (afterReturnToStart, _) = GameReducer.Reduce(afterFirstVisit, new MoveToNodeAction(new NodeId("start")));
        var (afterRevisit, secondEvents) = GameReducer.Reduce(afterReturnToStart, new MoveToNodeAction(new NodeId("shop-1")));

        Assert.Contains(firstEvents, e => e is EncounterResolved { NodeId: var id } && id == new NodeId("shop-1"));
        Assert.Contains(secondEvents, e => e is EncounterAlreadyResolved { NodeId: var id } && id == new NodeId("shop-1"));
        Assert.Equal(2, afterRevisit.Map.ResolvedEncounterNodeIds.Count);
    }

    [Fact]
    public void MapState_TracksVisitedNodes()
    {
        var state = CreateMapExplorationState();

        var (afterCombat, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var (afterElite, _) = GameReducer.Reduce(afterCombat, new MoveToNodeAction(new NodeId("elite-1")));

        Assert.Contains(new NodeId("start"), afterElite.Map.VisitedNodeIds);
        Assert.Contains(new NodeId("combat-1"), afterElite.Map.VisitedNodeIds);
        Assert.Contains(new NodeId("elite-1"), afterElite.Map.VisitedNodeIds);
        Assert.Equal(3, afterElite.Map.VisitedNodeIds.Count);
    }

    [Fact]
    public void MoveAction_IsRejected_OutsideMapExplorationPhase()
    {
        var state = CreateMapExplorationState() with { Phase = GamePhase.Combat };

        var (newState, events) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Equal(state, newState);
        Assert.Empty(events);
    }

    [Fact]
    public void SampleMap_IsDeterministic()
    {
        var mapA = SampleMapFactory.CreateDefaultState();
        var mapB = SampleMapFactory.CreateDefaultState();

        Assert.Equal(mapA.CurrentNodeId, mapB.CurrentNodeId);
        Assert.Equal(mapA.BossNodeId, mapB.BossNodeId);
        Assert.Equal(mapA.Graph.Nodes.OrderBy(n => n.Id.Value).ToArray(), mapB.Graph.Nodes.OrderBy(n => n.Id.Value).ToArray());
        Assert.Equal(
            mapA.Graph.GetNeighbors(new NodeId("start")).OrderBy(id => id.Value).ToArray(),
            mapB.Graph.GetNeighbors(new NodeId("start")).OrderBy(id => id.Value).ToArray());
        Assert.Equal(
            new[] { new NodeId("combat-1"), new NodeId("shop-1") },
            mapA.Graph.GetNeighbors(new NodeId("start")).OrderBy(id => id.Value).ToArray());
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
