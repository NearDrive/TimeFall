using System.IO;
using Game.Cli;
using Game.Core.Game;
using Game.Data.Content;

namespace Game.Tests.Cli;

public sealed class SandboxCliIntegrationTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void SandboxCommand_ParsesEntryAction()
    {
        var ok = CliCommandParser.TryParse("sandbox", out var command, out var error);

        Assert.True(ok, error);
        var action = Assert.IsType<EnterSandboxModeAction>(command.Action);
        Assert.Equal(CliCommandParser.DefaultSeed, action.Seed);
    }

    [Fact]
    public void SandboxDeckSelection_CommandResolvesIndex()
    {
        var state = EnteredSandboxState();
        var ok = CliCommandParser.TryParse("select-sandbox-deck 0", out var command, out var error);

        Assert.True(ok, error);
        var action = Assert.IsType<SelectSandboxDeckAction>(CliLoop.ResolveContextualAction(command, state));
        Assert.Equal(state.AvailableDeckIds[0], action.DeckId);
    }

    [Fact]
    public void SandboxEquipAndUnequip_ResolveByIndexWithIntent()
    {
        var selectedDeck = SelectSandboxDeck(EnteredSandboxState());
        var sortedCards = selectedDeck.DeckDefinitions[selectedDeck.Sandbox!.SelectedDeckId!].StarterCardIds
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        var equippedCard = selectedDeck.Sandbox.EquippedCardIds[0];
        var unequipIndex = Array.IndexOf(sortedCards, equippedCard);
        Assert.True(unequipIndex >= 0);

        var unequipParsed = CliCommandParser.TryParse($"unequip {unequipIndex}", out var unequipCommand, out var unequipError);
        Assert.True(unequipParsed, unequipError);
        var unequip = Assert.IsType<ToggleSandboxLoadoutCardAction>(CliLoop.ResolveContextualAction(unequipCommand, selectedDeck));
        Assert.Equal(equippedCard, unequip.CardId);

        var afterUnequip = GameReducer.Reduce(selectedDeck, unequip).NewState;
        var equipParsed = CliCommandParser.TryParse($"equip {unequipIndex}", out var equipCommand, out var equipError);
        Assert.True(equipParsed, equipError);
        var equip = Assert.IsType<ToggleSandboxLoadoutCardAction>(CliLoop.ResolveContextualAction(equipCommand, afterUnequip));
        Assert.Equal(equippedCard, equip.CardId);
    }

    [Fact]
    public void SandboxEnemySelect_AndStart_UseSandboxActions()
    {
        var deckState = SelectSandboxDeck(EnteredSandboxState());
        var enemySelectState = GameReducer.Reduce(deckState, new OpenSandboxEnemySelectAction()).NewState;

        var selectParsed = CliCommandParser.TryParse("select-enemy 0", out var selectCommand, out var selectError);
        Assert.True(selectParsed, selectError);
        var selectAction = Assert.IsType<SelectSandboxEnemyAction>(CliLoop.ResolveContextualAction(selectCommand, enemySelectState));

        var withEnemy = GameReducer.Reduce(enemySelectState, selectAction).NewState;

        var startParsed = CliCommandParser.TryParse("start", out var startCommand, out var startError);
        Assert.True(startParsed, startError);
        Assert.IsType<StartSandboxCombatAction>(CliLoop.ResolveContextualAction(startCommand, withEnemy));
    }

    [Fact]
    public void SandboxRepeatCommand_ParsesAndIsUsablePostCombat()
    {
        var combatState = StartSandboxCombatState();
        var postCombat = GameReducer.Reduce(combatState with { RunHp = 1 }, new EndTurnAction()).NewState;
        Assert.Equal(GamePhase.SandboxPostCombat, postCombat.Phase);

        var parsed = CliCommandParser.TryParse("repeat", out var command, out var error);
        Assert.True(parsed, error);

        var repeat = Assert.IsType<RepeatSandboxCombatAction>(CliLoop.ResolveContextualAction(command, postCombat) ?? command.Action);
        var repeated = GameReducer.Reduce(postCombat, repeat).NewState;
        Assert.Equal(GamePhase.SandboxCombat, repeated.Phase);
    }

    [Fact]
    public void SandboxHelp_IsPhaseSpecific()
    {
        var deckEditHelp = CaptureConsole(() => CliRenderer.RenderHelp(GamePhase.SandboxDeckEdit));
        Assert.Contains("equip <id|index>", deckEditHelp);
        Assert.Contains("clear-loadout", deckEditHelp);

        var postCombatHelp = CaptureConsole(() => CliRenderer.RenderHelp(GamePhase.SandboxPostCombat));
        Assert.Contains("repeat", postCombatHelp);
        Assert.Contains("setup", postCombatHelp);
    }

    [Fact]
    public void SandboxRenderer_ShowsSetupAndEnemyViews()
    {
        var deckState = SelectSandboxDeck(EnteredSandboxState());
        var deckOutput = CaptureConsole(() => CliRenderer.RenderState(deckState, [], Content.CardDefinitions));
        Assert.Contains("Loadout valid", deckOutput);
        Assert.Contains("Commands: cards, equip, unequip", deckOutput);

        var enemyState = GameReducer.Reduce(deckState, new OpenSandboxEnemySelectAction()).NewState;
        var enemyOutput = CaptureConsole(() => CliRenderer.RenderState(enemyState, [], Content.CardDefinitions));
        Assert.Contains("Sandbox enemy selection", enemyOutput);
        Assert.Contains("Sandbox enemies:", enemyOutput);
    }

    private static GameState EnteredSandboxState()
    {
        var initial = GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool, Content.EnemyDefinitions, Content.Zone1SpawnTable);
        return GameReducer.Reduce(initial, new EnterSandboxModeAction(9001)).NewState;
    }

    private static GameState SelectSandboxDeck(GameState state)
    {
        return GameReducer.Reduce(state, new SelectSandboxDeckAction(state.AvailableDeckIds[0])).NewState;
    }

    private static GameState StartSandboxCombatState()
    {
        var deckState = SelectSandboxDeck(EnteredSandboxState());
        var enemySelect = GameReducer.Reduce(deckState, new OpenSandboxEnemySelectAction()).NewState;
        var enemyId = enemySelect.EnemyDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).First();
        var withEnemy = GameReducer.Reduce(enemySelect, new SelectSandboxEnemyAction(enemyId)).NewState;
        return GameReducer.Reduce(withEnemy, new StartSandboxCombatAction()).NewState;
    }

    private static string CaptureConsole(Action render)
    {
        var writer = new StringWriter();
        var previousOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            render();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previousOut);
        }
    }
}
