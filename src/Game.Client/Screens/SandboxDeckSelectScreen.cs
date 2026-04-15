using Game.Application;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class SandboxDeckSelectScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;
    private readonly List<(string DeckId, Rectangle Region)> _deckRegions = new();

    public SandboxDeckSelectScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;
        BuildRegions();

        foreach (var (deckId, region) in _deckRegions)
        {
            if (_input.IsLeftClick(region))
            {
                _dispatcher.Dispatch(new SelectSandboxDeckAction(deckId));
                return;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(18, 30, 30));

        spriteBatch.Begin();
        var pixel = RenderPrimitives.Pixel;
        DrawHeader(spriteBatch, pixel);

        var selectedDeckId = _session.State.Sandbox?.SelectedDeckId;
        foreach (var (deckId, region) in _deckRegions)
        {
            var selected = string.Equals(deckId, selectedDeckId, StringComparison.Ordinal);
            spriteBatch.Draw(pixel, region, selected ? Color.Gold : new Color(109, 149, 165));

            var deck = _session.State.DeckDefinitions[deckId];
            DebugTextRenderer.DrawText(spriteBatch, pixel, deck.Name, new Vector2(region.X + 12, region.Y + 12), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"ID: {deckId}", new Vector2(region.X + 12, region.Y + 38), Color.Black);
            DebugTextRenderer.DrawText(spriteBatch, pixel, $"BASE HP: {deck.BaseMaxHp}", new Vector2(region.X + 12, region.Y + 64), Color.Black);
        }

        spriteBatch.End();
    }

    private void DrawHeader(SpriteBatch spriteBatch, Texture2D pixel)
    {
        DebugTextRenderer.DrawText(spriteBatch, pixel, "SANDBOX: SELECT A DECK", new Vector2(20, 20), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, "CLICK A DECK TO CONTINUE TO LOADOUT", new Vector2(20, 50), Color.White);
    }

    private void BuildRegions()
    {
        _deckRegions.Clear();

        var deckIds = _session.State.AvailableDeckIds;
        for (var index = 0; index < deckIds.Count; index++)
        {
            var x = 20 + (index % 3) * 410;
            var y = 100 + (index / 3) * 120;
            _deckRegions.Add((deckIds[index], new Rectangle(x, y, 390, 100)));
        }
    }
}
