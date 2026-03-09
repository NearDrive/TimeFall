using Game.Core.Cards;
using CardsCardId = Game.Core.Cards.CardId;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;
using System.Collections.Immutable;

namespace Game.Tests.Game;

[Trait("Lane", "integration")]
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
    public void BeginCombat_IsRejected_WhenAlreadyInCombat()
    {
        var (stateAfterBegin, beginEvents) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        Assert.Equal(GamePhase.Combat, stateAfterBegin.Phase);
        Assert.NotNull(stateAfterBegin.Combat);
        Assert.Contains(beginEvents, e => e is EnteredCombat);

        var (rejectedState, rejectedEvents) = GameReducer.Reduce(stateAfterBegin, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        Assert.Equal(stateAfterBegin, rejectedState);
        Assert.Empty(rejectedEvents);
    }

    [Fact]
    public void BeginCombat_IsRejected_InInvalidPhase()
    {
        var state = GameState.Initial with { Phase = GamePhase.RewardSelection };

        var (newState, events) = GameReducer.Reduce(state, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        Assert.Equal(state, newState);
        Assert.Empty(events);
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
        var nonStrikeIndex = FindCardIndex(stateAfterBegin.Combat!.Player.Deck.Hand, "guard");

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
    public void PlayerTurnStart_DrawsExactlyOneCard()
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
                    new CardsCardId("guard"),
                    new CardsCardId("quick-draw"),
                    new CardsCardId("guard"),
                    new CardsCardId("strike"),
                    new CardsCardId("quick-draw"),
                ]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 40,
                MaxHP: 40,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile:
                [
                    new CardsCardId("enemy-attack"),
                ]));

        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, Content.CardDefinitions));
        var handCountBeforeEndTurn = combatState.Combat!.Player.Deck.Hand.Count;
        var drawPileCountBeforeEndTurn = combatState.Combat.Player.Deck.DrawPile.Count;

        var (afterEndTurnState, events) = GameReducer.Reduce(combatState, new EndTurnAction());

        Assert.Equal(handCountBeforeEndTurn + 1, afterEndTurnState.Combat!.Player.Deck.Hand.Count);
        Assert.Equal(drawPileCountBeforeEndTurn - 1, afterEndTurnState.Combat.Player.Deck.DrawPile.Count);

        var drawnCards = events.OfType<CardDrawn>().Select(e => e.Card.DefinitionId.Value).ToArray();
        Assert.Equal(1, drawnCards.Count(id => id == "quick-draw"));
    }

    [Fact]
    public void EndTurn_IsBlocked_WhenOverflowDiscardPending()
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
    public void PlayCard_IsBlocked_WhenOverflowDiscardPending()
    {
        var overflowState = CreateOverflowState(requiredDiscardCount: 1, handSize: 8);

        var result = GameReducer.Reduce(overflowState, new PlayCardAction(0));

        Assert.Equal(overflowState, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void DiscardOverflow_IsRejected_WhenNoOverflowPending()
    {
        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        var result = GameReducer.Reduce(combatState, new DiscardOverflowAction([0]));

        Assert.Equal(combatState, result.NewState);
        Assert.Empty(result.Events);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void InvalidHandIndex_IsRejected(int handIndex)
    {
        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        var playResult = GameReducer.Reduce(combatState, new PlayCardAction(handIndex));

        Assert.Equal(combatState, playResult.NewState);
        Assert.Empty(playResult.Events);

        var overflowState = CreateOverflowState(requiredDiscardCount: 1, handSize: 8);
        var discardResult = GameReducer.Reduce(overflowState, new DiscardOverflowAction([handIndex]));

        Assert.Equal(overflowState, discardResult.NewState);
        Assert.Empty(discardResult.Events);
    }

    [Fact]
    public void InvalidPhaseActions_AreRejected()
    {
        var state = GameState.Initial;

        var playResult = GameReducer.Reduce(state, new PlayCardAction(0));
        var endTurnResult = GameReducer.Reduce(state, new EndTurnAction());

        Assert.Equal(state, playResult.NewState);
        Assert.Empty(playResult.Events);
        Assert.Equal(state, endTurnResult.NewState);
        Assert.Empty(endTurnResult.Events);
    }

    [Fact]
    public void PendingState_AllowsOnlyResolutionAction()
    {
        var overflowState = CreateOverflowState(requiredDiscardCount: 1, handSize: 8);

        var playResult = GameReducer.Reduce(overflowState, new PlayCardAction(0));
        var endTurnResult = GameReducer.Reduce(overflowState, new EndTurnAction());
        var discardResult = GameReducer.Reduce(overflowState, new DiscardOverflowAction([0]));

        Assert.Equal(overflowState, playResult.NewState);
        Assert.Empty(playResult.Events);
        Assert.Equal(overflowState, endTurnResult.NewState);
        Assert.Empty(endTurnResult.Events);
        Assert.NotEqual(overflowState, discardResult.NewState);
        Assert.NotEmpty(discardResult.Events);
    }

    [Fact]
    public void DiscardOverflow_ResolvesToExactMaxHandSize()
    {
        var overflowCombatState = new CombatState(
            TurnOwner: TurnOwner.Player,
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
                    BurnPile: ImmutableList<CardInstance>.Empty,
            ReshuffleCount: 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: 3);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState, null, Content.CardDefinitions, SampleMapFactory.CreateDefaultState(), TimeState.Create(SampleMapFactory.CreateDefaultState()), null, ImmutableList<CardsCardId>.Empty, ImmutableList<CardInstance>.Empty, null, 10, 10, null);

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
                    BurnPile: ImmutableList<CardInstance>.Empty,
            ReshuffleCount: 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: 3);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState, null, Content.CardDefinitions, SampleMapFactory.CreateDefaultState(), TimeState.Create(SampleMapFactory.CreateDefaultState()), null, ImmutableList<CardsCardId>.Empty, ImmutableList<CardInstance>.Empty, null, 10, 10, null);

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
        Assert.Contains(events, e => e is CardDrawn { Card.DefinitionId.Value: "enemy-attack" });
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
                DrawPile: [new CardsCardId("guard")]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: [clawCardId]));

        var initialState = GameState.Initial with { RunHp = 10, RunMaxHp = 10 };
        var (combatState, _) = GameReducer.Reduce(initialState, new BeginCombatAction(blueprint, cardDefinitions));
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
            Player: new CombatEntity(
                EntityId: "player",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList.Create(new CardInstance(new CardsCardId("strike"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 4,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(10), combatState, null, Content.CardDefinitions, SampleMapFactory.CreateDefaultState(), TimeState.Create(SampleMapFactory.CreateDefaultState()), null, ImmutableList<CardsCardId>.Empty, ImmutableList<CardInstance>.Empty, null, combatState.Player.HP, combatState.Player.MaxHP, null);

        var (newState, events) = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Equal(GamePhase.RewardSelection, newState.Phase);
        Assert.Null(newState.Combat);
        Assert.Contains(events, e => e is PlayerStrikePlayed { EnemyHpAfterHit: <= 0 });
    }

    [Fact]
    public void EndTurn_EnemyKillTransitionsToRunEndedPhase()
    {
        var combatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 4,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList.Create(new CardInstance(new CardsCardId("guard"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList.Create(new CardInstance(new CardsCardId("enemy-attack"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(GamePhase.Combat, GameRng.FromSeed(10), combatState, null, Content.CardDefinitions, SampleMapFactory.CreateDefaultState(), TimeState.Create(SampleMapFactory.CreateDefaultState()), null, ImmutableList<CardsCardId>.Empty, ImmutableList<CardInstance>.Empty, null, combatState.Player.HP, combatState.Player.MaxHP, null);

        var (newState, events) = GameReducer.Reduce(state, new EndTurnAction());

        Assert.Equal(GamePhase.RunEnded, newState.Phase);
        Assert.Null(newState.Combat);
        Assert.Contains(events, e => e is EnemyAttackPlayed { PlayerHpAfterHit: <= 0 });
    }

    [Fact]
    [Trait("Lane", "canary")]
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


    [Fact]
    public void BeginCombat_WithoutExplicitRewardPool_FallsBackToProvidedDefinitions()
    {
        var alpha = new CardsCardId("alpha");
        var beta = new CardsCardId("beta");
        var cardDefinitions = new Dictionary<CardsCardId, CardDefinition>
        {
            [alpha] = new(alpha, "Alpha", 1, [new DamageCardEffect(4, CardTarget.Opponent)]),
            [beta] = new(beta, "Beta", 1, [new GainArmorCardEffect(2, CardTarget.Self)]),
        };

        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint(
                EntityId: "player",
                HP: 20,
                MaxHP: 20,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: [alpha, alpha, alpha, alpha, beta]),
            Enemy: new CombatantBlueprint(
                EntityId: "enemy",
                HP: 4,
                MaxHP: 4,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                DrawPile: [alpha]));

        var (combatState, _) = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, cardDefinitions));
        var alphaIndex = FindCardIndex(combatState.Combat!.Player.Deck.Hand, "alpha");

        var (victoryState, _) = GameReducer.Reduce(combatState, new PlayCardAction(alphaIndex));

        Assert.Equal(GamePhase.RewardSelection, victoryState.Phase);
        Assert.NotNull(victoryState.Reward);
        Assert.All(victoryState.Reward!.CardOptions, id => Assert.Contains(id, cardDefinitions.Keys));
    }

    [Fact]
    public void PlayCard_QuickDraw_EmitsDeckCycleEvents_WhenDeckCycles()
    {
        var combatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 20,
                MaxHP: 20,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(
                    DrawPile: ImmutableList<CardInstance>.Empty,
                    Hand: ImmutableList.Create(new CardInstance(new CardsCardId("quick-draw"))),
                    DiscardPile: ImmutableList.Create(new CardInstance(new CardsCardId("strike"))),
                    BurnPile: ImmutableList<CardInstance>.Empty,
                    ReshuffleCount: 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 20,
                MaxHP: 20,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            NeedsOverflowDiscard: false,
            RequiredOverflowDiscardCount: 0);

        var state = new GameState(
            GamePhase.Combat,
            GameRng.FromSeed(11),
            combatState,
            null,
            Content.CardDefinitions,
            SampleMapFactory.CreateDefaultState(),
            TimeState.Create(SampleMapFactory.CreateDefaultState()),
            null,
            ImmutableList<CardsCardId>.Empty,
            ImmutableList<CardInstance>.Empty,
            null,
            20,
            20,
            null);

        var (newState, events) = GameReducer.Reduce(state, new PlayCardAction(0));

        Assert.Contains(events, e => e is DeckReshuffled);
        Assert.Contains(events, e => e is CardDrawn { Card.DefinitionId.Value: "strike" });
        Assert.NotNull(newState.Combat);
        Assert.Single(newState.Combat!.Player.Deck.BurnPile);
    }

    private static GameState CreateOverflowState(int requiredDiscardCount, int handSize)
    {
        var overflowCombatState = new CombatState(
            TurnOwner: TurnOwner.Player,
            Player: new CombatEntity(
                EntityId: "player",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(
                    DrawPile: ImmutableList<CardInstance>.Empty,
                    Hand: Enumerable.Repeat(new CardInstance(new CardsCardId("strike")), handSize).ToImmutableList(),
                    DiscardPile: ImmutableList<CardInstance>.Empty,
                    BurnPile: ImmutableList<CardInstance>.Empty,
            ReshuffleCount: 0)),
            Enemy: new CombatEntity(
                EntityId: "enemy",
                HP: 10,
                MaxHP: 10,
                Armor: 0,
                Resources: ImmutableDictionary<ResourceType, int>.Empty,
                Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
            NeedsOverflowDiscard: true,
            RequiredOverflowDiscardCount: requiredDiscardCount);

        return new GameState(GamePhase.Combat, GameRng.FromSeed(1), overflowCombatState, null, Content.CardDefinitions, SampleMapFactory.CreateDefaultState(), TimeState.Create(SampleMapFactory.CreateDefaultState()), null, ImmutableList<CardsCardId>.Empty, ImmutableList<CardInstance>.Empty, null, 10, 10, null);
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
            totalEnemyDraws += result.Events.Count(e => e is CardDrawn { Card.DefinitionId.Value: "enemy-attack" or "guard" or "quick-draw" or "strike" });
            totalEnemyAttacks += result.Events.Count(e => e is EnemyAttackPlayed);
        }

        return (state.Combat!.Player.HP, totalEnemyDraws, totalEnemyAttacks);
    }

}
