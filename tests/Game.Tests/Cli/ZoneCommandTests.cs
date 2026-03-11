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
    public void ZoneCommand_ShowsAllNodes()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        foreach (var node in state.Map.Graph.Nodes)
        {
            Assert.Contains(node.Id.Value, output);
        }
    }

    [Fact]
    public void ZoneCommand_GroupsNodesByDepth()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("Depth 0:", output);
        Assert.Contains("Depth 1:", output);
        Assert.Contains("Depth 2:", output);
        Assert.Contains("Depth 3:", output);
    }

    [Fact]
    public void ZoneCommand_ShowsCurrentNode()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[E:elite-1@]", output);
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

        Assert.Contains("[$:shop-1 X]", output);
    }

    [Fact]
    public void ZoneCommand_ShowsStartAndBoss()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[S:start]", output);
        Assert.Contains("[B:boss-1]", output);
    }

    [Fact]
    public void ZoneCommand_RenderingIsDeterministic()
    {
        var state = CreateZoneState();

        var first = CaptureConsole(() => CliRenderer.RenderZone(state));
        var second = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Equal(first, second);
        Assert.True(first.IndexOf("[C:combat-1]", StringComparison.Ordinal) < first.IndexOf("[$:shop-1]", StringComparison.Ordinal));
    }

    [Fact]
    public void HelpCommand_ListsZoneCommand()
    {
        var output = CaptureConsole(CliRenderer.RenderHelp);

        Assert.Contains("map                 Show current node and neighbors", output);
        Assert.Contains("zone                Show full zone map", output);
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
