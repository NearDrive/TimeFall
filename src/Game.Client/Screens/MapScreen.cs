using Game.Application;
using Game.Core.Game;
using Game.Core.Map;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class MapScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly List<(NodeId NodeId, Rectangle Region)> _clickableNodeRegions = new();
    private readonly List<MapNodeRenderInfo> _nodeRenderInfos = new();

    public MapScreen(IGameSession session, InputHandler input)
    {
        _session = session;
        _input = input;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildNodeRegions();

        foreach (var nodeRegion in _clickableNodeRegions)
        {
            if (_input.IsLeftClick(nodeRegion.Region))
            {
                _session.ApplyPlayerAction(new MoveToNodeAction(nodeRegion.NodeId));
                BuildNodeRegions();
                break;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(21, 34, 28));

        spriteBatch.Begin();
        DrawMap(spriteBatch);
        spriteBatch.End();
    }

    private void DrawMap(SpriteBatch spriteBatch)
    {
        var pixel = RenderPrimitives.Pixel;
        var font = RenderPrimitives.Font;

        if (_session.State.Phase != GamePhase.MapExploration)
        {
            spriteBatch.DrawString(font, "Map available during MapExploration phase.", new Vector2(20, 20), Color.White);
            return;
        }

        spriteBatch.DrawString(font, "Map Nodes (click adjacent nodes to move)", new Vector2(20, 20), Color.White);

        foreach (var renderInfo in _nodeRenderInfos)
        {
            var color = renderInfo.IsCurrent
                ? Color.Gold
                : renderInfo.IsAdjacent
                    ? Color.CornflowerBlue
                    : renderInfo.IsVisited
                        ? Color.DimGray
                        : Color.DarkSlateBlue;

            spriteBatch.Draw(pixel, renderInfo.Region, color);
            spriteBatch.DrawString(font, renderInfo.Label, new Vector2(renderInfo.Region.X + 8, renderInfo.Region.Y + 12), Color.White);
        }
    }

    private void BuildNodeRegions()
    {
        _clickableNodeRegions.Clear();
        _nodeRenderInfos.Clear();

        if (_session.State.Phase != GamePhase.MapExploration)
        {
            return;
        }

        var graph = _session.State.Map.Graph;
        var currentNodeId = _session.State.Map.CurrentNodeId;
        var adjacentNodeIds = graph.GetNeighbors(currentNodeId).ToHashSet();
        var allNodes = graph.Nodes
            .OrderBy(node => _session.State.Map.DistanceFromStart.TryGetValue(node.Id, out var distance) ? distance : int.MaxValue)
            .ThenBy(node => node.Id.Value, StringComparer.Ordinal)
            .ToArray();

        const int left = 20;
        const int top = 70;
        const int nodeWidth = 140;
        const int nodeHeight = 70;
        const int xGap = 25;
        const int yGap = 30;

        var rows = allNodes
            .GroupBy(node => _session.State.Map.DistanceFromStart.TryGetValue(node.Id, out var distance) ? distance : 99)
            .OrderBy(group => group.Key)
            .ToArray();

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var rowNodes = rows[rowIndex].OrderBy(node => node.Id.Value, StringComparer.Ordinal).ToArray();
            for (var columnIndex = 0; columnIndex < rowNodes.Length; columnIndex++)
            {
                var node = rowNodes[columnIndex];
                var x = left + (columnIndex * (nodeWidth + xGap));
                var y = top + (rowIndex * (nodeHeight + yGap));
                var region = new Rectangle(x, y, nodeWidth, nodeHeight);
                var isCurrent = node.Id == currentNodeId;
                var isAdjacent = adjacentNodeIds.Contains(node.Id);
                var isVisited = _session.State.Map.VisitedNodeIds.Contains(node.Id);

                _nodeRenderInfos.Add(new MapNodeRenderInfo(
                    node.Id,
                    region,
                    $"{node.Id.Value} ({node.Type})",
                    isCurrent,
                    isAdjacent,
                    isVisited));

                if (isAdjacent)
                {
                    _clickableNodeRegions.Add((node.Id, region));
                }
            }
        }
    }

    private sealed record MapNodeRenderInfo(
        NodeId NodeId,
        Rectangle Region,
        string Label,
        bool IsCurrent,
        bool IsAdjacent,
        bool IsVisited);
}
