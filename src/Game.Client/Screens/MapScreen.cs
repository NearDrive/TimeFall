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
    private readonly List<(NodeId NodeId, Rectangle Region)> _nodeRegions = new();
    private Texture2D? _pixel;

    public MapScreen(IGameSession session, InputHandler input)
    {
        _session = session;
        _input = input;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildNodeRegions();

        foreach (var nodeRegion in _nodeRegions)
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
        spriteBatch.GraphicsDevice.Clear(Color.ForestGreen);
        EnsurePixel(spriteBatch.GraphicsDevice);

        spriteBatch.Begin();
        foreach (var nodeRegion in _nodeRegions)
        {
            var isCurrent = nodeRegion.NodeId == _session.State.Map.CurrentNodeId;
            var color = isCurrent ? Color.Gold : Color.DarkSlateBlue;
            spriteBatch.Draw(_pixel!, nodeRegion.Region, color);
        }

        spriteBatch.End();
    }

    private void BuildNodeRegions()
    {
        _nodeRegions.Clear();

        if (_session.State.Phase != GamePhase.MapExploration)
        {
            return;
        }

        var neighbors = _session.State.Map.Graph.GetNeighbors(_session.State.Map.CurrentNodeId).ToArray();
        for (var index = 0; index < neighbors.Length; index++)
        {
            var x = 40 + (index * 120);
            _nodeRegions.Add((neighbors[index], new Rectangle(x, 120, 90, 90)));
        }
    }

    private void EnsurePixel(GraphicsDevice graphicsDevice)
    {
        if (_pixel is not null)
        {
            return;
        }

        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }
}
