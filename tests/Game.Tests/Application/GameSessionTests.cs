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

    [Fact]
    public void SandboxActions_AreOrchestratedThroughSessionPipeline()
    {
        var session = new GameSession(GameStateTestFactory.CreateInitialWithContent(), savedRunState: null);

        session.ApplyPlayerAction(new EnterSandboxModeAction(1234));
        session.ApplyPlayerAction(new SelectSandboxDeckAction(session.State.AvailableDeckIds[0]));
        session.ApplyPlayerAction(new OpenSandboxEnemySelectAction());
        var enemyId = session.State.EnemyDefinitions.Keys.OrderBy(x => x, StringComparer.Ordinal).First();
        session.ApplyPlayerAction(new SelectSandboxEnemyAction(enemyId));
        var events = session.ApplyPlayerAction(new StartSandboxCombatAction());

        Assert.Equal(GameMode.Sandbox, session.State.Mode);
        Assert.Equal(GamePhase.SandboxCombat, session.State.Phase);
        Assert.Contains(events, e => e is SandboxCombatStarted);
        Assert.False(session.State.HasActiveRunSave);
    }
}
