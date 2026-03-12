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
    public void ZoneCommand_RendersTreeStyleLayout()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[S:start]", output);
        Assert.Contains("├─", output);
        Assert.Contains("└─", output);
    }

    [Fact]
    public void ZoneCommand_ShowsStartBossAndCurrentNode()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[S:start]", output);
        Assert.Contains("[B:boss-1]", output);
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
        Assert.Contains("Collapsed nodes: 1", output);
    }

    [Fact]
    public void ZoneCommand_UsesDeterministicOrdering()
    {
        var state = CreateZoneState();

        var first = CaptureConsole(() => CliRenderer.RenderZone(state));
        var second = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Equal(first, second);
        Assert.True(first.IndexOf("[C:combat-1]", StringComparison.Ordinal) < first.IndexOf("[$:shop-1]", StringComparison.Ordinal));
    }

    [Fact]
    public void ZoneCommand_HandlesReconnectionsDeterministically()
    {
        var state = CreateReconnectionState();

        var output = CaptureConsole(() => CliRenderer.RenderZone(state));

        Assert.Contains("[C:mid]", output);
        Assert.Contains("[E:merge ↺]", output);
        Assert.Equal(output, CaptureConsole(() => CliRenderer.RenderZone(state)));
    }

    [Fact]
    public void HelpCommand_ListsZoneCommand()
    {
        var output = CaptureConsole(CliRenderer.RenderHelp);

        Assert.Contains("map                 Show current node and neighbors", output);
        Assert.Contains("zone                Show full zone map (tree fallback)", output);
    }

    [Fact]
    public void MapCommand_RemainsLocalAdjacencyOnly()
    {
        var state = CreateZoneState();

        var output = CaptureConsole(() => CliRenderer.RenderMap(state));

        Assert.Contains("Current node: elite-1", output);
        Assert.Contains("- [0] boss-1", output);
        Assert.Contains("- [1] combat-1", output);
        Assert.DoesNotContain("shop-1", output);
        Assert.DoesNotContain("Zone Map", output);
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

    private static GameState CreateReconnectionState()
    {
        var start = new Node(new NodeId("start"), NodeType.Start);
        var left = new Node(new NodeId("left"), NodeType.Combat);
        var right = new Node(new NodeId("right"), NodeType.Rest);
        var mid = new Node(new NodeId("mid"), NodeType.Combat);
        var merge = new Node(new NodeId("merge"), NodeType.Elite);
        var boss = new Node(new NodeId("boss"), NodeType.Boss);

        var graph = new MapGraph(
            new[] { start, left, right, mid, merge, boss },
            new (NodeId A, NodeId B)[]
            {
                (start.Id, left.Id),
                (start.Id, right.Id),
                (left.Id, mid.Id),
                (right.Id, merge.Id),
                (mid.Id, merge.Id),
                (merge.Id, boss.Id),
            });

        var map = MapState.Create(graph, start.Id, boss.Id) with
        {
            CurrentNodeId = mid.Id,
        };

        return GameStateTestFactory.CreateStartedRun() with
        {
            Map = map,
            Time = TimeState.Create(map),
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
