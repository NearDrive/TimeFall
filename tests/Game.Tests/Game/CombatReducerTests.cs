using Game.Core.Cards;
using CardsCardId = Game.Core.Cards.CardId;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Tests.Game;

[IntegrationLane]
public class CombatReducerTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void BeginCombat_DrawsInitialFiveCards()
    {
        var initial = GameState.Initial;

        var (newState, events) = GameReducer.Reduce(initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        Assert.Equal(GamePhase.Combat, newState.Phase);
        Assert.NotNull(newState.Combat);
        Assert.Equal(5, newState.Combat!.Player.Deck.Hand.Count);
        Assert.Contains(events, e => e is EnteredCombat);
        Assert.Equal(5, events.Count(e => e is CardDrawn));
    }


    [Fact]
    public void PlayCard_StrikeDamagesEnemyAndDiscardsCard()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));
        var strikeIndex = FindCardIndex(stateAfterBegin.Combat!.Player.Deck.Hand, "strike");

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
    public void PlayCard_NonStrikeWithDefinedEffect_IsPlayable()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));
        var nonStrikeIndex = FindCardIndex(stateAfterBegin.Combat!.Player.Deck.Hand, "defend");

        Assert.True(nonStrikeIndex >= 0);

        var armorBefore = stateAfterBegin.Combat.Player.Armor;
        var discardBefore = stateAfterBegin.Combat.Player.Deck.DiscardPile.Count;

        var (newState, events) = GameReducer.Reduce(stateAfterBegin, new PlayCardAction(nonStrikeIndex));

        Assert.Equal(armorBefore + 3, newState.Combat!.Player.Armor);
        Assert.Equal(discardBefore + 1, newState.Combat.Player.Deck.DiscardPile.Count);
        Assert.Contains(events, e => e is CardDiscarded);
    }

    [Fact]
    public void EndTurn_DoesNotAutoDiscardHand()
    {
        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        var (enemyTurnState, _) = GameReducer.Reduce(combatState, new EndTurnAction());
        var handCountOnEnemyTurn = enemyTurnState.Combat!.Player.Deck.Hand.Count;

        var (playerTurnState, _) = GameReducer.Reduce(enemyTurnState, new EndTurnAction());

        Assert.Equal(handCountOnEnemyTurn + 1, playerTurnState.Combat!.Player.Deck.Hand.Count);
    }

    [Fact]
    public void EndTurn_PlayerTurnDrawsExactlyOneCard_UnderControlledDeckState()
    {
        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 30,
                MaxHP: 30,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile:
                [
                    new CardsCardId("strike"),
                    new CardsCardId("defend"),
                    new CardsCardId("focus"),
                    new CardsCardId("defend"),
                    new CardsCardId("strike"),
                    new CardsCardId("focus"),
                    new CardsCardId("defend"),
                    new CardsCardId("strike"),
                ]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 40,
                MaxHP: 40,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile:
                [
                    new CardsCardId("defend"),
                ]));

        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, Content.CardDefinitions));
        var handCountBeforeEndTurn = combatState.Combat!.Player.Deck.Hand.Count;

        var (afterEndTurnState, events) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(handCountBeforeEndTurn + 1, afterEndTurnState.Combat!.Player.Deck.Hand.Count);

        var drawnCards = events.OfType<CardDrawn>().Select(e => e.Card.DefinitionId.Value).ToArray();
        Assert.Equal(2, drawnCards.Length);
        Assert.Equal(["defend", "focus"], drawnCards);
    }

    [Fact]
    public void OverflowRequiresDiscardBeforeContinuingTurns()
    {
        var (stateAfterBegin, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

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
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(
                    DrawPile: ImmutableList<CardInstance>.Empty,
                    Hand: Enumerable.Repeat(new CardInstance(new CardsCardId("strike")), 10).ToImmutableList(),
                    DiscardPile: ImmutableList<CardInstance>.Empty,
                    BurnPile: ImmutableList<CardInstance>.Empty)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty)),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: 3);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState, Content.CardDefinitions);

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
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(
                    DrawPile: ImmutableList<CardInstance>.Empty,
                    Hand: Enumerable.Repeat(new CardInstance(new CardsCardId("strike")), 10).ToImmutableList(),
                    DiscardPile: ImmutableList<CardInstance>.Empty,
                    BurnPile: ImmutableList<CardInstance>.Empty)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty)),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: 3);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState, Content.CardDefinitions);

        var result = GameReducer.Reduce(state, new DiscardOverflowAction(indexes));

        Assert.Equal(state, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void EndTurn_PlayerToPlayer_EnemyDrawsAndAttacks()
    {
        var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(123));
        var (combatState, _) = GameReducer.Reduce(seededState, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        var playerHpBefore = combatState.Combat!.Player.HP;
        var (afterEnemyTurn, events) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(TurnOwner.Player, afterEnemyTurn.Combat!.TurnOwner);
        Assert.Contains(events, e => e is CardDrawn { Card.DefinitionId.Value: "attack" });
        Assert.Contains(events, e => e is EnemyAttackPlayed);
        Assert.True(afterEnemyTurn.Combat.Player.HP < playerHpBefore);
    }

    [Fact]
    public void EndTurn_EnemyUsesEffectDrivenCardEvenWhenIdIsUnknown()
    {
        var clawCardId = new CardsCardId("claw");
        var cardDefinitions = new Dictionary<CardsCardId, CardDefinition>(Content.CardDefinitions)
        {
            [clawCardId] = new(clawCardId, "Claw", 1, [new DamageCardEffect(2, CardTarget.Opponent)]),
        };

        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: [new CardsCardId("defend")]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: [clawCardId]));

        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, cardDefinitions));
        var (newState, events) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(8, newState.Combat!.Player.HP);
        Assert.Contains(events, e => e is EnemyAttackPlayed { Damage: 2 });
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
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList.Create(new CardInstance(new CardsCardId("strike"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 4,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty)),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(10), combatState, Content.CardDefinitions);

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
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList.Create(new CardInstance(new CardsCardId("defend"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList.Create(new CardInstance(new CardsCardId("attack"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty)),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(10), combatState, Content.CardDefinitions);

        var (newState, events) = GameReducer.Reduce(state, new EndTurnAction());

        Assert.Equal(GamePhase.RunEnded, newState.Phase);
        Assert.Null(newState.Combat);
        Assert.Contains(events, e => e is EnemyAttackPlayed { PlayerHpAfterHit: <= 0 });
    }

    [Fact]
    [CanaryLane]
    public void Canary_LongCombatSequence_RemainsPlayableAndDeterministic()
    {
        static (int PlayerHp, int EnemyHp, int HandCount, int DiscardCount) RunSequence(int seed)
        {
            var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(seed));
            var (state, _) = GameReducer.Reduce(seededState, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

            for (var turn = 0; turn < 24 && state.Phase == GamePhase.Combat; turn++)
            {
                var combat = state.Combat!;
                if (combat.TurnOwner == TurnOwner.Player)
                {
                    if (combat.NeedsOverflowDiscard)
                    {
                        var indexes = Enumerable.Range(0, combat.RequiredOverflowDiscardCount).ToArray();
                        state = GameReducer.Reduce(state, new DiscardOverflowAction(indexes)).NewState;
                        continue;
                    }

                    if (combat.Player.Deck.Hand.Count > 0)
                    {
                        state = GameReducer.Reduce(state, new PlayCardAction(0)).NewState;
                        continue;
                    }
                }

                state = GameReducer.Reduce(state, new EndTurnAction()).NewState;
            }

            var finalCombat = state.Combat;
            return (
                PlayerHp: finalCombat?.Player.HP ?? 0,
                EnemyHp: finalCombat?.Enemy.HP ?? 0,
                HandCount: finalCombat?.Player.Deck.Hand.Count ?? 0,
                DiscardCount: finalCombat?.Player.Deck.DiscardPile.Count ?? 0);
        }

        var first = RunSequence(seed: 4242);
        var second = RunSequence(seed: 4242);

        Assert.Equal(first, second);
    }

    private static int FindCardIndex(IReadOnlyList<CardInstance> cards, string id)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i].DefinitionId.Value == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static (int PlayerHp, int EnemyCardsDrawn, int EnemyAttacksPlayed) SimulatePlayerEnemyTurns(int seed, int turns)
    {
        var (seededState, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(seed));
        var (state, _) = GameReducer.Reduce(seededState, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

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
