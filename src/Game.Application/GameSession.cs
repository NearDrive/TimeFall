using Game.Core.Game;

namespace Game.Application;

public sealed class GameSession : IGameSession
{
    private GameState? savedRunState;

    public GameSession(GameState initialState, GameState? savedRunState = null)
    {
        State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        this.savedRunState = savedRunState;
        DispatchCore(new SetContinueAvailabilityAction(savedRunState is not null));
    }

    public GameState State { get; private set; }

    public IReadOnlyList<GameEvent> ApplyPlayerAction(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var allEvents = new List<GameEvent>();

        allEvents.AddRange(DispatchCore(new SetContinueAvailabilityAction(savedRunState is not null)));

        var resolvedAction = ResolvePlayerAction(action);
        if (resolvedAction is null)
        {
            return allEvents;
        }

        allEvents.AddRange(DispatchCore(resolvedAction));
        allEvents.AddRange(DispatchCore(new SetContinueAvailabilityAction(savedRunState is not null)));

        return allEvents;
    }

    public IReadOnlyList<GameEvent> SetSavedRunState(GameState? savedRunState)
    {
        this.savedRunState = savedRunState;
        return DispatchCore(new SetContinueAvailabilityAction(savedRunState is not null));
    }

    private GameAction? ResolvePlayerAction(GameAction action)
    {
        if (action is not ContinueRunAction)
        {
            return action;
        }

        return savedRunState is null
            ? null
            : new ContinueRunAction(savedRunState);
    }

    private IReadOnlyList<GameEvent> DispatchCore(GameAction action)
    {
        var (newState, events) = GameReducer.Reduce(State, action);
        State = newState;
        return events;
    }
}
