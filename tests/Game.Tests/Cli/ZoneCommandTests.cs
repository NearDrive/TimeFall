using System.Collections.Immutable;
using System.IO;
using Game.Cli;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Cli;

public sealed class ZoneCommandTests
{
    [Fact]
    public void ZoneCommand_RendersLayeredLayoutAndConnections()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("Depth 0: [S:Start]", output);
        Assert.Contains("Depth 1: [C:A1] [R:A2]", output);
        Assert.Contains("Connections:", output);
        Assert.Contains("Start -> A1, A2", output);
    }

    [Fact]
    public void ZoneCommand_ShowsStartBossAndCurrentNode()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[S:Start]", output);
        Assert.Contains("[B:Boss]", output);
        Assert.Contains("[E:C2@]", output);
    }

    [Fact]
    public void ZoneCommand_ShowsCollapsedNodes()
    {
        var baseState = CreateZoneState();
        var state = baseState with
        {
            Time = baseState.Time with
            {
                CollapsedNodeIds = ImmutableSortedSet.Create(MapState.NodeIdComparer, new NodeId("shop-1")),
            },
        };

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[$:C3 X]", output);
        Assert.Contains("Collapsed nodes: 1", output);
    }

    [Fact]
    public void ZoneCommand_UsesDeterministicOrdering()
    {
        var state = CreateZoneState();

        var first = CaptureConsole(() => CliRenderer.RenderZone(state));
        var second = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Equal(first, second);
        Assert.True(first.IndexOf("A1", StringComparison.Ordinal) < first.IndexOf("A2", StringComparison.Ordinal));
    }

    [Fact]
    public void HelpCommand_ListsZoneCommand()
    {
        var output = CaptureConsole(() => CliRenderer.RenderHelp(GamePhase.MapExploration));

        Assert.Contains("map                 Show current node and neighbors", output);
        Assert.Contains("zone                Show full zone map (layered + connections)", output);
        Assert.Contains("move <displayId|i>", output);
    }

    [Fact]
    public void MapCommand_RemainsLocalAdjacencyOnly()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderMap(state));

        Assert.Contains("Current node: C2", output);
        Assert.Contains("- [0] Boss", output);
        Assert.Contains("- [1] A1", output);
        Assert.DoesNotContain("C3", output);
        Assert.DoesNotContain("Zone Map", output);
    }

    [Fact]
    public void MoveCommand_ResolvesDisplayIdToInternalNodeId()
    {
        var state = CreateZoneState();
        var parsed = CliCommandParser.TryParse("move A1", out var command, out var error);

        Assert.True(parsed);
        Assert.Equal(string.Empty, error);

        var resolved = CliLoop.ResolveContextualAction(command, state);
        var move = Assert.IsType<MoveToNodeAction>(resolved);
        Assert.Equal(new NodeId("combat-1"), move.NodeId);
    }

    private static GameState CreateZoneState()
    {
        var map = SampleMapFactory.CreateDefaultState();
        var movedMap = map with
        {
            CurrentNodeId = new NodeId("elite-1"),
        };

        return GameStateTestFactory.CreateStartedRun() with
        {
            Map = movedMap,
            Time = TimeState.Create(movedMap),
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
