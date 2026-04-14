using Game.Application;
using Game.Core.Game;

namespace Game.Tests.Application;

public sealed class GameSessionTests
{
    [Fact]
    public void ApplyPlayerAction_UsesPipelineAndUpdatesState()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var session = new GameSession(initial, savedRunState: null);

        var events = session.ApplyPlayerAction(new EnterNewRunMenuAction());

        Assert.Empty(events);
        Assert.Equal(GamePhase.NewRunMenu, session.State.Phase);
    }

    [Fact]
    public void ContinueRun_UsesTrackedSavedRunStateFromSession()
    {
        var savedRun = GameStateTestFactory.CreateStartedRun();
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var session = new GameSession(initial, savedRun);

        var events = session.ApplyPlayerAction(new ContinueRunAction(GameState.Initial));

        Assert.Empty(events);
        Assert.Equal(savedRun.Phase, session.State.Phase);
        Assert.Equal(savedRun.RunDeck.Count, session.State.RunDeck.Count);
        Assert.True(session.State.HasActiveRunSave);
    }

    [Fact]
    public void SetSavedRunState_UpdatesContinueAvailabilityViaReducer()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var session = new GameSession(initial, savedRunState: null);

        session.SetSavedRunState(GameStateTestFactory.CreateStartedRun());

        Assert.True(session.State.HasActiveRunSave);

        session.SetSavedRunState(null);

        Assert.False(session.State.HasActiveRunSave);
    }
}
