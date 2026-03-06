using Game.Core.Common;
using Game.Core.Combat;
using Game.Core.Cards;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public sealed record GameState(
    GamePhase Phase,
    GameRng Rng,
    CombatState? Combat,
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions)
{
    public static GameState Initial => new(GamePhase.DeckSelect, GameRng.FromSeed(0), null, new Dictionary<CardId, CardDefinition>());
}
