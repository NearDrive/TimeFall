using Game.Core.Game;

namespace Game.Client.Screens;

public interface IClientActionDispatcher
{
    IReadOnlyList<GameEvent> Dispatch(GameAction action);
}
