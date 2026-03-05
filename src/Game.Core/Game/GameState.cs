using Game.Core.Common;

namespace Game.Core.Game;

public sealed record GameState(GamePhase Phase, GameRng Rng)
{
    public static GameState Initial => new(GamePhase.DeckSelect, GameRng.FromSeed(0));
}
