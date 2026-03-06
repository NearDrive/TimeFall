using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Game;

public class MapCombatIntegrationTests
{
    [Fact]
    public void FirstVisit_CombatNode_StartsCombat()
    {
        var state = CreateMapExplorationState();

        var (newState, events) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Equal(GamePhase.Combat, newState.Phase);
        Assert.NotNull(newState.Combat);
        Assert.Equal(new NodeId("combat-1"), newState.ActiveCombatNodeId);
        Assert.Contains(events, e => e is EncounterTriggered { NodeId: var id } && id == new NodeId("combat-1"));
        Assert.Contains(events, e => e is EnteredCombat { NodeId: var id } && id == new NodeId("combat-1"));
    }

    [Fact]
    public void FirstVisit_EliteNode_StartsCombat()
    {
        var state = CreateMapExplorationState();
        var (afterCombat, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var rewardState = ForceCompleteCombatAtCurrentNode(afterCombat);
        var mapState = ClaimFirstRewardCard(rewardState);

        var (newState, _) = GameReducer.Reduce(mapState, new MoveToNodeAction(new NodeId("elite-1")));

        Assert.Equal(GamePhase.Combat, newState.Phase);
        Assert.NotNull(newState.Combat);
        Assert.Equal(new NodeId("elite-1"), newState.ActiveCombatNodeId);
    }

    [Fact]
    public void FirstVisit_BossNode_StartsCombat()
    {
        var state = CreateMapExplorationState();
        var (toShop, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("shop-1")));
        var (toRest, _) = GameReducer.Reduce(toShop, new MoveToNodeAction(new NodeId("rest-1")));

        var (newState, _) = GameReducer.Reduce(toRest, new MoveToNodeAction(new NodeId("boss-1")));

        Assert.Equal(GamePhase.Combat, newState.Phase);
        Assert.NotNull(newState.Combat);
        Assert.Equal(new NodeId("boss-1"), newState.ActiveCombatNodeId);
    }

    [Fact]
    public void RevisitingResolvedCombatNode_DoesNotRestartCombat()
    {
        var state = CreateMapExplorationState();
        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var rewardState = ForceCompleteCombatAtCurrentNode(afterEntry);
        var afterVictory = ClaimFirstRewardCard(rewardState);
        var (afterMoveToStart, _) = GameReducer.Reduce(afterVictory, new MoveToNodeAction(new NodeId("start")));

        var (revisit, events) = GameReducer.Reduce(afterMoveToStart, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Equal(GamePhase.MapExploration, revisit.Phase);
        Assert.Null(revisit.Combat);
        Assert.Contains(events, e => e is EncounterAlreadyResolved { NodeId: var id } && id == new NodeId("combat-1"));
    }

    [Fact]
    public void CombatVictory_TransitionsToRewardSelection()
    {
        var state = CreateMapExplorationState();
        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        var afterVictory = ForceCompleteCombatAtCurrentNode(afterEntry);

        Assert.Equal(GamePhase.RewardSelection, afterVictory.Phase);
        Assert.Null(afterVictory.Combat);
        Assert.Null(afterVictory.ActiveCombatNodeId);
    }

    [Fact]
    public void CombatVictory_MarksEncounterResolved()
    {
        var state = CreateMapExplorationState();
        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.DoesNotContain(new NodeId("combat-1"), afterEntry.Map.ResolvedEncounterNodeIds);

        var rewardState = ForceCompleteCombatAtCurrentNode(afterEntry);

        Assert.Contains(new NodeId("combat-1"), rewardState.Map.ResolvedEncounterNodeIds);
    }

    [Fact]
    public void MovementIntoCombatNode_AdvancesTimeOnceOnly()
    {
        var state = CreateMapExplorationState();

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        var afterTurn = GameReducer.Reduce(afterEntry, new EndTurnAction()).NewState;

        Assert.Equal(1, afterEntry.Time.CurrentStep);
        Assert.Equal(afterEntry.Time.CurrentStep, afterTurn.Time.CurrentStep);
    }

    [Theory]
    [InlineData("shop-1")]
    [InlineData("rest-1")]
    public void NonCombatNode_DoesNotStartCombat(string nodeId)
    {
        var state = CreateMapExplorationState();

        var (newState, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId(nodeId)));

        Assert.Equal(GamePhase.MapExploration, newState.Phase);
        Assert.Null(newState.Combat);
        Assert.Null(newState.ActiveCombatNodeId);
    }

    [Fact]
    public void CombatNode_NotPrematurelyResolvedOnEntry()
    {
        var state = CreateMapExplorationState();

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Contains(new NodeId("combat-1"), afterEntry.Map.TriggeredEncounterNodeIds);
        Assert.DoesNotContain(new NodeId("combat-1"), afterEntry.Map.ResolvedEncounterNodeIds);
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

    private static GameState ClaimFirstRewardCard(GameState rewardState)
    {
        var selected = rewardState.Reward!.CardOptions[0];
        return GameReducer.Reduce(rewardState, new ChooseRewardCardAction(selected)).NewState;
    }

    private static GameState ForceCompleteCombatAtCurrentNode(GameState combatState)
    {
        var strikeIndex = combatState.Combat!.Player.Deck.Hand
            .Select((card, index) => (card, index))
            .First(tuple => tuple.card.DefinitionId.Value == "strike")
            .index;

        var lethal = combatState with
        {
            Combat = combatState.Combat with
            {
                Enemy = combatState.Combat.Enemy with { HP = 4 },
            },
        };

        return GameReducer.Reduce(lethal, new PlayCardAction(strikeIndex)).NewState;
    }
}
