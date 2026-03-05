using Game.Core.Common;
using Game.Core.Combat;

namespace Game.Core.Game;

public sealed record GameState(GamePhase Phase, GameRng Rng, CombatState? Combat)
{
    public static GameState Initial => new(GamePhase.DeckSelect, GameRng.FromSeed(0), null);
}
