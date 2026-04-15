using Game.Application;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client.Screens;

public sealed class MainMenuScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;

    private readonly Rectangle _startRunRegion = new(40, 120, 420, 80);
    private readonly Rectangle _startSandboxRegion = new(40, 220, 420, 80);

    public MainMenuScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;

        if (_input.IsLeftClick(_startRunRegion) || _input.IsKeyPressed(Keys.R))
        {
            StartRunFromMenu();
            return;
        }

        if (_input.IsLeftClick(_startSandboxRegion) || _input.IsKeyPressed(Keys.B))
        {
            _dispatcher.Dispatch(new EnterSandboxModeAction(424242));
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(16, 20, 36));

        spriteBatch.Begin();
        var pixel = RenderPrimitives.Pixel;

        DebugTextRenderer.DrawText(spriteBatch, pixel, "TIMEFALL DECK", new Vector2(40, 40), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, "MAIN MENU", new Vector2(40, 75), Color.White);

        DrawButton(spriteBatch, pixel, _startRunRegion, "START RUN (R)");
        DrawButton(spriteBatch, pixel, _startSandboxRegion, "ENTER SANDBOX (B)");

        DebugTextRenderer.DrawText(spriteBatch, pixel, $"PHASE: {_session.State.Phase}", new Vector2(40, 330), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"MODE: {_session.State.Mode}", new Vector2(40, 360), Color.White);

        spriteBatch.End();
    }

    private void StartRunFromMenu()
    {
        if (_session.State.AvailableDeckIds.Count == 0)
        {
            return;
        }

        var deckId = _session.State.AvailableDeckIds[0];
        _dispatcher.Dispatch(new EnterNewRunMenuAction());
        _dispatcher.Dispatch(new OpenDeckSelectAction());
        _dispatcher.Dispatch(new SelectDeckAction(deckId));
        _dispatcher.Dispatch(new ReturnToNewRunMenuAction());
        _dispatcher.Dispatch(new StartRunAction(12345));
    }

    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle region, string label)
    {
        spriteBatch.Draw(pixel, region, new Color(112, 132, 176));
        DebugTextRenderer.DrawText(spriteBatch, pixel, label, new Vector2(region.X + 20, region.Y + 28), Color.Black);
    }
}
