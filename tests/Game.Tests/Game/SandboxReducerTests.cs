using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Game;

namespace Game.Tests.Game;

public sealed class SandboxReducerTests
{
    [Fact]
    public void EnterSandboxMode_SetsOfficialModeAndPhase()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();

        var result = GameReducer.Reduce(initial, new EnterSandboxModeAction(777));

        Assert.Equal(GameMode.Sandbox, result.NewState.Mode);
        Assert.Equal(GamePhase.SandboxDeckSelect, result.NewState.Phase);
        Assert.NotNull(result.NewState.Sandbox);
        Assert.Equal(777, result.NewState.Sandbox!.SessionSeed);
        Assert.Null(result.NewState.Reward);
        Assert.Null(result.NewState.Combat);
    }

    [Fact]
    public void SandboxDeckSelectAndLoadoutToggle_UsesSelectedDeckCards()
    {
        var entered = EnterSandbox(101);
        var deckId = entered.AvailableDeckIds[0];

        var selected = GameReducer.Reduce(entered, new SelectSandboxDeckAction(deckId)).NewState;

        Assert.Equal(GamePhase.SandboxDeckEdit, selected.Phase);
        Assert.Equal(deckId, selected.Sandbox!.SelectedDeckId);
        Assert.NotEmpty(selected.Sandbox.EquippedCardIds);

        var cardId = selected.Sandbox.EquippedCardIds[0];
        var toggledOff = GameReducer.Reduce(selected, new ToggleSandboxLoadoutCardAction(cardId)).NewState;
        Assert.DoesNotContain(cardId, toggledOff.Sandbox!.EquippedCardIds);

        var toggledOn = GameReducer.Reduce(toggledOff, new ToggleSandboxLoadoutCardAction(cardId)).NewState;
        Assert.Contains(cardId, toggledOn.Sandbox!.EquippedCardIds);
    }

    [Fact]
    public void SandboxRejectsCardFromAnotherDeck()
    {
        var initial = EnterSandbox(202);
        var baseDeck = initial.DeckDefinitions[initial.AvailableDeckIds[0]];
        var foreignCard = new CardId("enemy-attack");
        var expandedDecks = new Dictionary<string, RunDeckDefinition>(initial.DeckDefinitions, StringComparer.Ordinal)
        {
            ["deck-foreign"] = new(
                "deck-foreign",
                "Foreign",
                "foreign",
                baseDeck.ResourceType,
                baseDeck.BaseMaxHp,
                [foreignCard],
                [foreignCard],
                baseDeck.StartingResources),
        };

        var state = initial with { DeckDefinitions = expandedDecks };
        var selected = GameReducer.Reduce(state, new SelectSandboxDeckAction(baseDeck.Id)).NewState;
        var before = selected.Sandbox!.EquippedCardIds;

        var after = GameReducer.Reduce(selected, new ToggleSandboxLoadoutCardAction(foreignCard)).NewState;

        Assert.Equal(before, after.Sandbox!.EquippedCardIds);
    }

    [Fact]
    public void SandboxEnemySelectAndStartCombat_UsesRealEnemyDefinitions()
    {
        var editState = SelectSandboxDeck(EnterSandbox(303));
        var enemySelect = GameReducer.Reduce(editState, new OpenSandboxEnemySelectAction()).NewState;
        var enemyId = enemySelect.EnemyDefinitions.Keys.First();
        var withEnemy = GameReducer.Reduce(enemySelect, new SelectSandboxEnemyAction(enemyId)).NewState;

        var started = GameReducer.Reduce(withEnemy, new StartSandboxCombatAction()).NewState;

        Assert.Equal(GamePhase.SandboxCombat, started.Phase);
        Assert.NotNull(started.Combat);
        Assert.Equal(enemyId, started.Sandbox!.SelectedEnemyId);
        Assert.Equal(started.Sandbox.EquippedCardIds.Count, started.RunDeck.Count);
    }

    [Fact]
    public void SandboxCombatEnd_GoesToSandboxPostCombat_NotRewardOrMap()
    {
        var combat = StartSandboxCombat(404);
        var victoryReady = combat with { Combat = combat.Combat! with { Enemies = ImmutableList<global::Game.Core.Combat.CombatEntity>.Empty } };

        var resolved = GameReducer.Reduce(victoryReady, new EndTurnAction()).NewState;

        Assert.Equal(GamePhase.SandboxPostCombat, resolved.Phase);
        Assert.Null(resolved.Reward);
        Assert.Null(resolved.Combat);
        Assert.Equal(GameMode.Sandbox, resolved.Mode);
    }

    [Fact]
    public void SandboxRepeatCombat_IsDeterministicForSameSeedAndActions()
    {
        var first = BuildRepeatedSandboxCombatState(505);
        var second = BuildRepeatedSandboxCombatState(505);

        Assert.Equal(first.Sandbox!.LastCombatSeed, second.Sandbox!.LastCombatSeed);
        Assert.Equal(first.Combat!.Player.Deck.Hand.Select(c => c.DefinitionId.Value), second.Combat!.Player.Deck.Hand.Select(c => c.DefinitionId.Value));
        Assert.Equal(first.Combat.Enemies.Select(e => e.EntityId), second.Combat.Enemies.Select(e => e.EntityId));
    }

    [Fact]
    public void SandboxFlow_DoesNotChangeRunMetaState()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var startedRun = GameStateTestFactory.CreateStartedRun();
        var sandbox = StartSandboxCombat(606);

        Assert.Equal(initial.Map, sandbox.Map);
        Assert.Equal(initial.Time, sandbox.Time);
        Assert.Null(sandbox.Reward);
        Assert.NotEqual(GamePhase.MapExploration, sandbox.Phase);
        Assert.Equal(startedRun.HasActiveRunSave, sandbox.HasActiveRunSave);
    }

    private static GameState EnterSandbox(int seed)
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        return GameReducer.Reduce(initial, new EnterSandboxModeAction(seed)).NewState;
    }

    private static GameState SelectSandboxDeck(GameState state)
    {
        return GameReducer.Reduce(state, new SelectSandboxDeckAction(state.AvailableDeckIds[0])).NewState;
    }

    private static GameState StartSandboxCombat(int seed)
    {
        var entered = EnterSandbox(seed);
        var deck = SelectSandboxDeck(entered);
        var enemySelect = GameReducer.Reduce(deck, new OpenSandboxEnemySelectAction()).NewState;
        var enemyId = enemySelect.EnemyDefinitions.Keys.OrderBy(x => x, StringComparer.Ordinal).First();
        var withEnemy = GameReducer.Reduce(enemySelect, new SelectSandboxEnemyAction(enemyId)).NewState;
        return GameReducer.Reduce(withEnemy, new StartSandboxCombatAction()).NewState;
    }

    private static GameState BuildRepeatedSandboxCombatState(int seed)
    {
        var combat = StartSandboxCombat(seed);
        var won = combat with { Combat = combat.Combat! with { Enemies = ImmutableList<global::Game.Core.Combat.CombatEntity>.Empty } };
        var post = GameReducer.Reduce(won, new EndTurnAction()).NewState;
        return GameReducer.Reduce(post, new RepeatSandboxCombatAction()).NewState;
    }
}
