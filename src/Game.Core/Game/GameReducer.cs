using Game.Core.Common;

namespace Game.Core.Game;

public static class GameReducer
{
    public static (GameState NewState, IReadOnlyList<GameEvent> Events) Reduce(GameState state, GameAction action)
    {
        return action switch
        {
            StartRunAction startRunAction => StartRun(state, startRunAction),
            _ => (state, Array.Empty<GameEvent>()),
        };
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) StartRun(GameState state, StartRunAction action)
    {
        _ = state;

        var newState = new GameState(GamePhase.DeckSelect, GameRng.FromSeed(action.Seed));
        var events = new GameEvent[] { new RunStarted(action.Seed) };

        return (newState, events);
    }
}
