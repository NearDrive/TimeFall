using Game.Application;
using Game.Client.Screens;
using Game.Core.Game;
using Game.Data.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client;

public sealed class ClientGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly IGameSession _session;
    private readonly ScreenManager _screenManager;
    private readonly InputHandler _input;

    private SpriteBatch _spriteBatch = null!;

    public ClientGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.PreferredBackBufferHeight = 720;
        var content = StaticGameContentProvider.LoadDefault();
        var initialState = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);
        _session = new GameSession(initialState);
        BootstrapRunForInputTesting();

        _input = new InputHandler();
        _screenManager = new ScreenManager(ScreenType.MainMenu, _session, _input);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        RenderPrimitives.Initialize(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        _ = _session.State;
        _input.Update();

        if (_input.IsKeyPressed(Keys.F1))
        {
            _screenManager.SwitchTo(ScreenType.MainMenu);
        }

        _screenManager.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _screenManager.Draw(_spriteBatch);
        base.Draw(gameTime);
    }

    private void BootstrapRunForInputTesting()
    {
        if (_session.State.AvailableDeckIds.Count == 0)
        {
            return;
        }

        _session.ApplyPlayerAction(new EnterNewRunMenuAction());
        _session.ApplyPlayerAction(new OpenDeckSelectAction());
        _session.ApplyPlayerAction(new SelectDeckAction(_session.State.AvailableDeckIds[0]));
        _session.ApplyPlayerAction(new ReturnToNewRunMenuAction());
        _session.ApplyPlayerAction(new StartRunAction(12345));
    }
}
