using Game.Application;
using Game.Core.Game;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game.Client.Screens;

public sealed class ScreenManager : IClientActionDispatcher
{
    private IGameSession _session;
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
    public IGameSession Session => _session;
    public ActiveEventPlayback? ActivePlayback => _playbackController.ActivePlayback;
    public IReadOnlyList<GameEvent> RecentEvents => _playbackController.RecentEvents;

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

        spriteBatch.Begin();
        DebugOverlayRenderer.Draw(spriteBatch, _session.State, CurrentScreenType, _playbackController.RecentEvents);
        spriteBatch.End();
    }

    public void ResetSession(IGameSession session)
    {
        _session = session;
        _playbackController.Reset();
        _currentScreen = CreateScreen(CurrentScreenType);
        SyncScreenWithSessionState();
    }

    private IScreen CreateScreen(ScreenType screenType) => screenType switch
    {
        ScreenType.MainMenu => new MainMenuScreen(_session, _input, this),
        ScreenType.Map => new MapScreen(_session, _input, this),
        ScreenType.Combat => new CombatScreen(_session, _input, this),
        ScreenType.Reward => new RewardScreen(_session, _input, this),
        ScreenType.SandboxDeckSelect => new SandboxDeckSelectScreen(_session, _input, this),
        ScreenType.SandboxDeckEdit => new SandboxDeckEditScreen(_session, _input, this),
        ScreenType.SandboxEnemySelect => new SandboxEnemySelectScreen(_session, _input, this),
        ScreenType.SandboxPostCombat => new SandboxPostCombatScreen(_session, _input, this),
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
        GamePhase.MainMenu => ScreenType.MainMenu,
        GamePhase.MapExploration => ScreenType.Map,
        GamePhase.Combat => ScreenType.Combat,
        GamePhase.SandboxCombat => ScreenType.Combat,
        GamePhase.RewardSelection => ScreenType.Reward,
        GamePhase.SandboxDeckSelect => ScreenType.SandboxDeckSelect,
        GamePhase.SandboxDeckEdit => ScreenType.SandboxDeckEdit,
        GamePhase.SandboxEnemySelect => ScreenType.SandboxEnemySelect,
        GamePhase.SandboxPostCombat => ScreenType.SandboxPostCombat,
        _ => null,
    };
}
