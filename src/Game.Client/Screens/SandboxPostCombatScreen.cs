using Game.Application;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class SandboxPostCombatScreen : IScreen
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly IClientActionDispatcher _dispatcher;

    private readonly Rectangle _repeatRegion = new(40, 200, 360, 80);
    private readonly Rectangle _enemySelectRegion = new(40, 300, 360, 80);
    private readonly Rectangle _deckEditRegion = new(40, 400, 360, 80);

    public SandboxPostCombatScreen(IGameSession session, InputHandler input, IClientActionDispatcher dispatcher)
    {
        _session = session;
        _input = input;
        _dispatcher = dispatcher;
    }

    public void Update(GameTime time)
    {
        _ = time;

        if (_input.IsLeftClick(_repeatRegion))
        {
            _dispatcher.Dispatch(new RepeatSandboxCombatAction());
            return;
        }

        if (_input.IsLeftClick(_enemySelectRegion))
        {
            _dispatcher.Dispatch(new OpenSandboxEnemySelectAction());
            return;
        }

        if (_input.IsLeftClick(_deckEditRegion))
        {
            _dispatcher.Dispatch(new OpenSandboxDeckEditAction());
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.GraphicsDevice.Clear(new Color(18, 18, 18));

        spriteBatch.Begin();
        var pixel = RenderPrimitives.Pixel;

        var sandbox = _session.State.Sandbox;
        var resultLabel = sandbox?.LastCombatWon switch
        {
            true => "RESULT: VICTORY",
            false => "RESULT: DEFEAT",
            null => "RESULT: UNKNOWN",
        };

        DebugTextRenderer.DrawText(spriteBatch, pixel, "SANDBOX POST COMBAT", new Vector2(40, 40), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, resultLabel, new Vector2(40, 80), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"DECK: {sandbox?.SelectedDeckId ?? "NONE"}", new Vector2(40, 110), Color.White);
        DebugTextRenderer.DrawText(spriteBatch, pixel, $"ENEMY: {sandbox?.SelectedEnemyId ?? "NONE"}", new Vector2(40, 140), Color.White);

        DrawButton(spriteBatch, pixel, _repeatRegion, "REPEAT SAME ENEMY");
        DrawButton(spriteBatch, pixel, _enemySelectRegion, "CHANGE ENEMY");
        DrawButton(spriteBatch, pixel, _deckEditRegion, "RETURN TO LOADOUT");

        spriteBatch.End();
    }

    private static void DrawButton(SpriteBatch spriteBatch, Texture2D pixel, Rectangle region, string label)
    {
        spriteBatch.Draw(pixel, region, new Color(152, 179, 152));
        DebugTextRenderer.DrawText(spriteBatch, pixel, label, new Vector2(region.X + 18, region.Y + 28), Color.Black);
    }
}
