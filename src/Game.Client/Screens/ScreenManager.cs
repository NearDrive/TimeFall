using Game.Application;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class ScreenManager : IClientActionDispatcher
{
    private readonly IGameSession _session;
    private readonly InputHandler _input;
    private readonly EventPlaybackController _playbackController = new();
    private IScreen _currentScreen;

    public ScreenManager(ScreenType initialScreen, IGameSession session, InputHandler input)
    {
        _session = session;
        _input = input;
        CurrentScreenType = initialScreen;
        _currentScreen = CreateScreen(initialScreen);
        SyncScreenWithSessionState();
    }

    public ScreenType CurrentScreenType { get; private set; }
    public ActiveEventPlayback? ActivePlayback => _playbackController.ActivePlayback;

    public void SwitchTo(ScreenType screenType)
    {
        if (CurrentScreenType == screenType)
        {
            return;
        }

        CurrentScreenType = screenType;
        _currentScreen = CreateScreen(screenType);
    }

    public IReadOnlyList<GameEvent> Dispatch(GameAction action)
    {
        var events = _session.ApplyPlayerAction(action);
        _playbackController.EnqueueRange(events);
        SyncScreenWithSessionState();
        return events;
    }

    public void Update(GameTime time)
    {
        SyncScreenWithSessionState();
        _playbackController.Update(time);
        _currentScreen.Update(time);
        SyncScreenWithSessionState();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _currentScreen.Draw(spriteBatch);
    }

    private IScreen CreateScreen(ScreenType screenType) => screenType switch
    {
        ScreenType.MainMenu => new MainMenuScreen(),
        ScreenType.Map => new MapScreen(_session, _input, this),
        ScreenType.Combat => new CombatScreen(_session, _input, this),
        ScreenType.Reward => new RewardScreen(_session, _input, this),
        _ => throw new ArgumentOutOfRangeException(nameof(screenType), screenType, "Unsupported screen type."),
    };

    private void SyncScreenWithSessionState()
    {
        var targetScreen = ResolveScreenType(_session.State.Phase);
        if (targetScreen is { } screenType && screenType != CurrentScreenType)
        {
            SwitchTo(screenType);
        }
    }

    private static ScreenType? ResolveScreenType(GamePhase phase) => phase switch
    {
        GamePhase.MapExploration => ScreenType.Map,
        GamePhase.Combat => ScreenType.Combat,
        GamePhase.RewardSelection => ScreenType.Reward,
        _ => null,
    };
}
