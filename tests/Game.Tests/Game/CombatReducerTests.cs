using Game.Core.Game;

namespace Game.Tests.Game;

public class CombatReducerTests
{
    [Fact]
    public void BeginCombat_DrawsInitialFiveCards()
    {
        var initial = GameState.Initial;

        var (newState, events) = GameReducer.Reduce(initial, new BeginCombatAction());

        Assert.Equal(GamePhase.Combat, newState.Phase);
        Assert.NotNull(newState.Combat);
        Assert.Equal(5, newState.Combat!.Player.Deck.Hand.Count);
        Assert.Contains(events, e => e is EnteredCombat);
        Assert.Equal(5, events.Count(e => e is CardDrawn));
    }

    [Fact]
    public void EndTurn_DoesNotAutoDiscardHand()
    {
        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction());

        var (enemyTurnState, _) = GameReducer.Reduce(combatState, new EndTurnAction());
        var handCountOnEnemyTurn = enemyTurnState.Combat!.Player.Deck.Hand.Count;

        var (playerTurnState, _) = GameReducer.Reduce(enemyTurnState, new EndTurnAction());

        Assert.Equal(handCountOnEnemyTurn + 1, playerTurnState.Combat!.Player.Deck.Hand.Count);
    }

    [Fact]
    public void OverflowRequiresDiscardBeforeContinuingTurns()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction());

        var (stateAfterEnemyTurn, _) = GameReducer.Reduce(stateAfterBegin, new EndTurnAction());
        var (overflowState, _) = GameReducer.Reduce(stateAfterEnemyTurn, new EndTurnAction());
        var (blockedState, blockedEvents) = GameReducer.Reduce(overflowState, new EndTurnAction());

        Assert.True(overflowState.Combat!.NeedsOverflowDiscard);
        Assert.Equal(1, overflowState.Combat.RequiredOverflowDiscardCount);
        Assert.Equal(overflowState, blockedState);
        Assert.Empty(blockedEvents);

        var handCountBeforeDiscard = overflowState.Combat.Player.Deck.Hand.Count;
        var (resolvedState, discardEvents) = GameReducer.Reduce(overflowState, new DiscardOverflowAction([0]));

        Assert.False(resolvedState.Combat!.NeedsOverflowDiscard);
        Assert.Equal(handCountBeforeDiscard - 1, resolvedState.Combat.Player.Deck.Hand.Count);
        Assert.Single(discardEvents);
        Assert.IsType<CardDiscarded>(discardEvents[0]);
    }
}
