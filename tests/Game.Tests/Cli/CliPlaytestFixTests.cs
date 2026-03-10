using System.IO;
using Game.Cli;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;
using Game.Data.Content;

namespace Game.Tests.Cli;

public sealed class CliPlaytestFixTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void OverflowDiscard_CommandPath_IsResolvable()
    {
        var parsed = CliCommandParser.TryParse("discard 0", out var command, out var error);

        Assert.True(parsed, error);
        var action = Assert.IsType<DiscardOverflowAction>(command.Action);
        Assert.Equal([0], action.Indexes);

        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);
        var (nextState, events) = GameReducer.Reduce(state, action);

        Assert.False(nextState.Combat!.NeedsOverflowDiscard);
        Assert.Contains(events, e => e is CardDiscarded);
    }

    [Fact]
    public void OverflowDiscard_State_DoesNotSoftLockPlaytestFlow()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);

        var (afterDiscard, discardEvents) = GameReducer.Reduce(state, new DiscardOverflowAction([0]));
        Assert.False(afterDiscard.Combat!.NeedsOverflowDiscard);
        Assert.NotEmpty(discardEvents);

        var (afterPlay, playEvents) = GameReducer.Reduce(afterDiscard, new PlayCardAction(0));
        Assert.NotEmpty(playEvents);
        Assert.NotEqual(afterDiscard, afterPlay);
    }


    [Fact]
    public void CliRenderer_Combat_ShowsPileSizes()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1);

        var output = CaptureConsole(() => CliRenderer.RenderState(state, [], Content.CardDefinitions));

        Assert.Contains("Player piles: draw", output);
        Assert.Contains("| hand", output);
        Assert.Contains("| discard", output);
        Assert.Contains("| burn", output);
        Assert.Contains("Enemy piles: draw", output);
    }

    [Fact]
    public void Renderer_UsesCorrectHpLabels()
    {
        var state = CreateOverflowPendingCombatState(requiredDiscards: 1) with { RunHp = 65, RunMaxHp = 80 };
        var withArmor = state with
        {
            Combat = state.Combat! with
            {
                Player = state.Combat.Player with { Armor = 9 },
            },
        };

        var output = CaptureConsole(() => CliRenderer.RenderState(withArmor, [], Content.CardDefinitions));

        Assert.Contains("Run HP: 65/80", output);
        Assert.DoesNotContain("Run HP/Armor", output);
        Assert.Contains("Combat Player Armor: 9", output);
    }

    [Fact]
    public void CliHelp_ClarifiesIndexing()
    {
        var output = CaptureConsole(CliRenderer.RenderHelp);

        Assert.Contains("0-based", output);
        Assert.Contains("discardpile", output);
    }


    [Fact]
    public void CliMove_CanUseAdjacentIndex()
    {
        var state = CreateMapExplorationState();
        var parsed = CliCommandParser.TryParse("move 1", out var command, out var error);

        Assert.True(parsed, error);
        var resolved = CliLoop.ResolveContextualAction(command, state);

        var move = Assert.IsType<MoveToNodeAction>(resolved);
        Assert.Equal(new NodeId("shop-1"), move.NodeId);
    }

    [Fact]
    public void MapRender_ShowsDeterministicAdjacentIndexes()
    {
        var output = CaptureConsole(() => CliRenderer.RenderMap(CreateMapExplorationState()));

        Assert.Contains("[0] combat-1", output);
        Assert.Contains("[1] shop-1", output);
    }

    [Fact]
    public void CliMove_NodeIdPathStillWorks()
    {
        var state = CreateMapExplorationState();
        var parsed = CliCommandParser.TryParse("move combat-1", out var command, out var error);

        Assert.True(parsed, error);
        var resolved = CliLoop.ResolveContextualAction(command, state) ?? command.Action;

        var move = Assert.IsType<MoveToNodeAction>(resolved);
        Assert.Equal(new NodeId("combat-1"), move.NodeId);
    }

    [Fact]
    public void StartCommand_UsesDeterministicDefaultSeed_WhenOmitted()
    {
        var ok = CliCommandParser.TryParse("start", out var command, out var error);

        Assert.True(ok, error);
        var start = Assert.IsType<StartRunAction>(command.Action);
        Assert.Equal(CliCommandParser.DefaultSeed, start.Seed);
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

    private static GameState CreateOverflowPendingCombatState(int requiredDiscards)
    {
        var started = GameReducer.Reduce(GameState.Initial, new StartRunAction(123)).NewState;
        var combat = GameReducer.Reduce(started, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions)).NewState;

        return combat with
        {
            Combat = combat.Combat! with
            {
                NeedsOverflowDiscard = true,
                RequiredOverflowDiscardCount = requiredDiscards,
            },
        };
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
