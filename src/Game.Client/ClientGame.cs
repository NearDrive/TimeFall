using Game.Application;
using Game.Client.Screens;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Game.Client;

public sealed class ClientGame : Microsoft.Xna.Framework.Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly IGameSession _session;
    private readonly ScreenManager _screenManager;

    private SpriteBatch _spriteBatch = null!;
    private KeyboardState _previousKeyboardState;

    public ClientGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        _session = new GameSession(GameState.Initial);
        _screenManager = new ScreenManager(ScreenType.MainMenu);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        _ = _session.State;
        var keyboardState = Keyboard.GetState();

        if (IsKeyPressed(keyboardState, Keys.F1))
        {
            _screenManager.SwitchTo(ScreenType.MainMenu);
        }

        if (IsKeyPressed(keyboardState, Keys.F2))
        {
            _screenManager.SwitchTo(ScreenType.Map);
        }

        if (IsKeyPressed(keyboardState, Keys.F3))
        {
            _screenManager.SwitchTo(ScreenType.Combat);
        }

        _screenManager.Update(gameTime);
        _previousKeyboardState = keyboardState;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        _screenManager.Draw(_spriteBatch);
        base.Draw(gameTime);
    }

    private bool IsKeyPressed(KeyboardState currentState, Keys key)
    {
        return currentState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key);
    }
}
