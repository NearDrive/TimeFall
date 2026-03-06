using Game.Core.Common;
using Game.Core.Combat;
using Game.Core.Cards;
using Game.Core.Map;
using Game.Core.TimeSystem;
using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public sealed record GameState(
    GamePhase Phase,
    GameRng Rng,
    CombatState? Combat,
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    MapState Map,
    TimeState Time)
{
    private static readonly MapState InitialMap = SampleMapFactory.CreateDefaultState();

    public static GameState Initial => new(
        GamePhase.DeckSelect,
        GameRng.FromSeed(0),
        null,
        ImmutableDictionary<CardId, CardDefinition>.Empty,
        InitialMap,
        TimeState.Create(InitialMap));
}
