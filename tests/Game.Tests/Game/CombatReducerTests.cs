using Game.Core.Combat;
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
    public void PlayCard_StrikeDamagesEnemyAndDiscardsCard()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction());
        var strikeIndex = stateAfterBegin.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value == "strike");

        Assert.True(strikeIndex >= 0);

        var enemyHpBefore = stateAfterBegin.Combat.Enemy.HP;
        var discardBefore = stateAfterBegin.Combat.Player.Deck.DiscardPile.Count;

        var (newState, events) = GameReducer.Reduce(stateAfterBegin, new PlayCardAction(strikeIndex));

        Assert.Equal(enemyHpBefore - 4, newState.Combat!.Enemy.HP);
        Assert.Equal(discardBefore + 1, newState.Combat.Player.Deck.DiscardPile.Count);
        Assert.Contains(events, e => e is PlayerStrikePlayed { Damage: 4 });
        Assert.Contains(events, e => e is CardDiscarded);
    }

    [Fact]
    public void PlayCard_NonStrikeDoesNothing()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction());
        var nonStrikeIndex = stateAfterBegin.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value != "strike");

        Assert.True(nonStrikeIndex >= 0);

        var result = GameReducer.Reduce(stateAfterBegin, new PlayCardAction(nonStrikeIndex));

        Assert.Equal(stateAfterBegin, result.NewState);
        Assert.Empty(result.Events);
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


    [Fact]
    public void EndTurn_PlayerToPlayer_EnemyDrawsAndAttacks()
    {
        var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(123));
        var (combatState, _) = GameReducer.Reduce(seededState, new BeginCombatAction());

        var playerHpBefore = combatState.Combat!.Player.HP;
        var (afterEnemyTurn, events) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(TurnOwner.Player, afterEnemyTurn.Combat!.TurnOwner);
        Assert.Contains(events, e => e is CardDrawn { Card.DefinitionId.Value: "attack" });
        Assert.Contains(events, e => e is EnemyAttackPlayed);
        Assert.True(afterEnemyTurn.Combat.Player.HP < playerHpBefore);
    }

    [Fact]
    public void EnemyTurn_IsDeterministicAcrossMultipleTurns_WithSameSeed()
    {
        var firstRun = SimulatePlayerEnemyTurns(seed: 2024, turns: 3);
        var secondRun = SimulatePlayerEnemyTurns(seed: 2024, turns: 3);

        Assert.Equal(firstRun.PlayerHp, secondRun.PlayerHp);
        Assert.Equal(firstRun.EnemyCardsDrawn, secondRun.EnemyCardsDrawn);
        Assert.Equal(firstRun.EnemyAttacksPlayed, secondRun.EnemyAttacksPlayed);
    }

    private static (int PlayerHp, int EnemyCardsDrawn, int EnemyAttacksPlayed) SimulatePlayerEnemyTurns(int seed, int turns)
    {
        var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(seed));
        var (state, _) = GameReducer.Reduce(seededState, new BeginCombatAction());

        var totalEnemyDraws = 0;
        var totalEnemyAttacks = 0;
        for (var i = 0; i < turns; i++)
        {
            var result = GameReducer.Reduce(state, new EndTurnAction());
            state = result.NewState;
            totalEnemyDraws += result.Events.Count(e => e is CardDrawn { Card.DefinitionId.Value: "attack" or "defend" or "focus" or "strike" });
            totalEnemyAttacks += result.Events.Count(e => e is EnemyAttackPlayed);
        }

        return (state.Combat!.Player.HP, totalEnemyDraws, totalEnemyAttacks);
    }

}