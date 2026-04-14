using Game.Core.Game;

namespace Game.Application;

public interface IGameSession
{
    GameState State { get; }

    IReadOnlyList<GameEvent> ApplyPlayerAction(GameAction action);

    IReadOnlyList<GameEvent> SetSavedRunState(GameState? savedRunState);
}
