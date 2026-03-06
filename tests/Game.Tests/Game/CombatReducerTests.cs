using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Tests.Game;

public class CombatReducerTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void BeginCombat_DrawsInitialFiveCards()
    {
        var initial = GameState.Initial;

        var (newState, events) = GameReducer.Reduce(initial, new BeginCombatAction(Content.OpeningCombat));

        Assert.Equal(GamePhase.Combat, newState.Phase);
        Assert.NotNull(newState.Combat);
        Assert.Equal(5, newState.Combat!.Player.Deck.Hand.Count);
        Assert.Contains(events, e => e is EnteredCombat);
        Assert.Equal(5, events.Count(e => e is CardDrawn));
    }


    [Fact]
    public void PlayCard_StrikeDamagesEnemyAndDiscardsCard()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat));
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
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat));
        var nonStrikeIndex = stateAfterBegin.Combat!.Player.Deck.Hand.FindIndex(c => c.DefinitionId.Value != "strike");

        Assert.True(nonStrikeIndex >= 0);

        var result = GameReducer.Reduce(stateAfterBegin, new PlayCardAction(nonStrikeIndex));

        Assert.Equal(stateAfterBegin, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void EndTurn_DoesNotAutoDiscardHand()
    {
        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat));

        var (enemyTurnState, _) = GameReducer.Reduce(combatState, new EndTurnAction());
        var handCountOnEnemyTurn = enemyTurnState.Combat!.Player.Deck.Hand.Count;

        var (playerTurnState, _) = GameReducer.Reduce(enemyTurnState, new EndTurnAction());

        Assert.Equal(handCountOnEnemyTurn + 1, playerTurnState.Combat!.Player.Deck.Hand.Count);
    }

    [Fact]
    public void EndTurn_PlayerTurnDrawsExactlyOneCard()
    {
        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 30,
                MaxHP: 30,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                DrawPile:
                [
                    new CardId("strike"),
                    new CardId("defend"),
                    new CardId("focus"),
                    new CardId("defend"),
                    new CardId("strike"),
                    new CardId("focus"),
                    new CardId("defend"),
                    new CardId("strike"),
                ]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 40,
                MaxHP: 40,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                DrawPile:
                [
                    new CardId("defend"),
                ]));

        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint));
        var handCountBeforeEndTurn = combatState.Combat!.Player.Deck.Hand.Count;

        var (afterEndTurnState, events) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(handCountBeforeEndTurn + 1, afterEndTurnState.Combat!.Player.Deck.Hand.Count);
        Assert.Single(events.OfType<CardDrawn>());
    }

    [Fact]
    public void OverflowRequiresDiscardBeforeContinuingTurns()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat));

        var (stateAfterEnemyTurn, _) = GameReducer.Reduce(stateAfterBegin, new EndTurnAction());
        var (stateBeforeOverflow, _) = GameReducer.Reduce(stateAfterEnemyTurn, new EndTurnAction());
        var (overflowState, _) = GameReducer.Reduce(stateBeforeOverflow, new EndTurnAction());
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
    public void DiscardOverflow_ResolvesToExactMaxHandSize()
    {
        var overflowCombatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            ReshuffleCount: 0,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState(
                    DrawPile: [],
                    Hand:
                    [
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                    ],
                    DiscardPile: [],
                    BurnPile: [])),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState([], [], [], [])),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: 3);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState);

        var (newState, events) = GameReducer.Reduce(state, new DiscardOverflowAction([0, 1, 2]));

        Assert.Equal(7, newState.Combat!.Player.Deck.Hand.Count);
        Assert.False(newState.Combat.NeedsOverflowDiscard);
        Assert.Equal(3, events.Count(e => e is CardDiscarded));
    }

    [Theory]
    [InlineData(new[] { 0, 1 })]
    [InlineData(new[] { 0, 0, 1 })]
    [InlineData(new[] { -1, 0, 1 })]
    [InlineData(new[] { 0, 1, 10 })]
    public void DiscardOverflow_InvalidIndexes_AreRejected(int[] indexes)
    {
        var overflowCombatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            ReshuffleCount: 0,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState(
                    DrawPile: [],
                    Hand:
                    [
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                        new CardInstance(new CardId("strike")),
                    ],
                    DiscardPile: [],
                    BurnPile: [])),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState([], [], [], [])),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: 3);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState);

        var result = GameReducer.Reduce(state, new DiscardOverflowAction(indexes));

        Assert.Equal(state, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void EndTurn_PlayerToPlayer_EnemyDrawsAndAttacks()
    {
        var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(123));
        var (combatState, _) = GameReducer.Reduce(seededState, new BeginCombatAction(Content.OpeningCombat));

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

    [Fact]
    public void PlayCard_KillingEnemyTransitionsToRewardPhase()
    {
        var combatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            ReshuffleCount: 0,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState([], [new CardInstance(new CardId("strike"))], [], [])),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 4,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState([], [], [], [])),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(10), combatState);

        var (newState, events) = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Equal(GamePhase.Reward, newState.Phase);
        Assert.Null(newState.Combat);
        Assert.Contains(events, e => e is PlayerStrikePlayed { EnemyHpAfterHit: <= 0 });
    }

    [Fact]
    public void EndTurn_EnemyKillTransitionsToRunEndedPhase()
    {
        var combatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            ReshuffleCount: 0,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 4,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState([], [new CardInstance(new CardId("defend"))], [], [])),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: new Dictionary<ResourceType, int>(),
                Deck: new DeckState([new CardInstance(new CardId("attack"))], [], [], [])),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(10), combatState);

        var (newState, events) = GameReducer.Reduce(state, new EndTurnAction());

        Assert.Equal(GamePhase.RunEnded, newState.Phase);
        Assert.Null(newState.Combat);
        Assert.Contains(events, e => e is EnemyAttackPlayed { PlayerHpAfterHit: <= 0 });
    }

    private static (int PlayerHp, int EnemyCardsDrawn, int EnemyAttacksPlayed) SimulatePlayerEnemyTurns(int seed, int turns)
    {
        var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(seed));
        var (state, _) = GameReducer.Reduce(seededState, new BeginCombatAction(Content.OpeningCombat));

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
