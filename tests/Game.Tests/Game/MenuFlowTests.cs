using System.IO;
using Game.Cli;
using Game.Core.Game;

namespace Game.Tests.Game;

public sealed class MenuFlowTests
{
    [Fact]
    public void Startup_EntersMainMenu()
    {
        var state = GameStateTestFactory.CreateInitialWithContent();

        Assert.Equal(GamePhase.MainMenu, state.Phase);
    }

    [Fact]
    public void Continue_IsAvailableOnlyWhenSaveExists()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();

        var unavailable = GameReducer.Reduce(initial, new ContinueRunAction(GameStateTestFactory.CreateStartedRun())).NewState;
        var available = GameReducer.Reduce(initial, new SetContinueAvailabilityAction(true)).NewState;

        Assert.Equal(GamePhase.MainMenu, unavailable.Phase);
        Assert.True(available.HasActiveRunSave);
    }

    [Fact]
    public void New_EntersNewRunMenu()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();

        var next = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;

        Assert.Equal(GamePhase.NewRunMenu, next.Phase);
    }

    [Fact]
    public void NewRunMenu_StartRequiresSelectedDeck()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;

        var started = GameReducer.Reduce(newRun, new StartRunAction(1337)).NewState;

        Assert.Equal(GamePhase.NewRunMenu, started.Phase);
    }

    [Fact]
    public void NewRunMenu_EditDeckRequiresSelectedDeck()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;

        var edited = GameReducer.Reduce(newRun, new OpenDeckEditAction()).NewState;

        Assert.Equal(GamePhase.NewRunMenu, edited.Phase);
    }

    [Fact]
    public void BackFromNewRunMenu_ReturnsToMainMenu()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;

        var returned = GameReducer.Reduce(newRun, new ReturnToMainMenuAction()).NewState;

        Assert.Equal(GamePhase.MainMenu, returned.Phase);
    }

    [Fact]
    public void Continue_LoadsSavedRun()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var savedRun = GameStateTestFactory.CreateStartedRun();
        var withAvailability = GameReducer.Reduce(initial, new SetContinueAvailabilityAction(true)).NewState;

        var resumed = GameReducer.Reduce(withAvailability, new ContinueRunAction(savedRun)).NewState;

        Assert.Equal(GamePhase.MapExploration, resumed.Phase);
        Assert.Equal(savedRun.Map.CurrentNodeId, resumed.Map.CurrentNodeId);
    }

    [Fact]
    public void DeckSelection_FromNewRunMenu_PersistsSelectedDeckForStart()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;
        var deckSelect = GameReducer.Reduce(newRun, new OpenDeckSelectAction()).NewState;
        var selected = GameReducer.Reduce(deckSelect, new SelectDeckAction(deckSelect.AvailableDeckIds[0])).NewState;
        var backToNew = GameReducer.Reduce(selected, new ReturnToNewRunMenuAction()).NewState;

        var started = GameReducer.Reduce(backToNew, new StartRunAction(1337)).NewState;

        Assert.Equal(GamePhase.MapExploration, started.Phase);
        Assert.Equal(selected.SelectedDeckId, started.SelectedDeckId);
    }

    [Fact]
    public void HelpOutput_ShowsCorrectCommandsPerMenu()
    {
        var mainMenuHelp = CaptureConsole(() => CliRenderer.RenderHelp(GamePhase.MainMenu));
        var newRunHelp = CaptureConsole(() => CliRenderer.RenderHelp(GamePhase.NewRunMenu));

        Assert.Contains("continue", mainMenuHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("new", mainMenuHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("select-deck", newRunHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edit-deck", newRunHelp, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MainMenu_Rendering_IsClear()
    {
        var state = GameStateTestFactory.CreateInitialWithContent();

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], state.CardDefinitions));

        Assert.Contains("Main Menu", output);
        Assert.Contains("continue", output);
        Assert.Contains("new", output);
    }

    [Fact]
    public void NewRunMenu_Rendering_ShowsSelectedDeck()
    {
        var state = GameStateTestFactory.CreateInitialWithContent() with
        {
            Phase = GamePhase.NewRunMenu,
            SelectedDeckId = "deck-blades",
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], state.CardDefinitions));

        Assert.Contains("New Run Menu", output);
        Assert.Contains("Selected deck: deck-blades", output);
    }

    private static string CaptureConsole(Action action)
    {
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
