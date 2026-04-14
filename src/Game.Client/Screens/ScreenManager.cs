using Game.Application;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class ScreenManager
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private IScreen _currentScreen;

    public ScreenManager(ScreenType initialScreen, IGameSession session, InputHandler input)
    {
        _session = session;
        _input = input;
        CurrentScreenType = initialScreen;
        _currentScreen = CreateScreen(initialScreen);
    }

    public ScreenType CurrentScreenType { get; private set; }

    public void SwitchTo(ScreenType screenType)
    {
        if (CurrentScreenType == screenType)
        {
            return;
        }

        CurrentScreenType = screenType;
        _currentScreen = CreateScreen(screenType);
    }

    public void Update(GameTime time)
    {
        _currentScreen.Update(time);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _currentScreen.Draw(spriteBatch);
    }

    private IScreen CreateScreen(ScreenType screenType) => screenType switch
    {
        ScreenType.MainMenu => new MainMenuScreen(),
        ScreenType.Map => new MapScreen(_session, _input),
        ScreenType.Combat => new CombatScreen(_session, _input),
        ScreenType.Reward => throw new NotSupportedException("Reward screen is not part of Phase 2."),
        _ => throw new ArgumentOutOfRangeException(nameof(screenType), screenType, "Unsupported screen type."),
    };
}
