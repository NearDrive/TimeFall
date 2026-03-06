using Game.Core.Common;
using Game.Core.Combat;
using Game.Core.Cards;
using Game.Core.Map;
using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public sealed record GameState(
    GamePhase Phase,
    GameRng Rng,
    CombatState? Combat,
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    MapState Map)
{
    public static GameState Initial => new(
        GamePhase.DeckSelect,
        GameRng.FromSeed(0),
        null,
        ImmutableDictionary<CardId, CardDefinition>.Empty,
        SampleMapFactory.CreateDefaultState());
}
