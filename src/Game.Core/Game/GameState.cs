using Game.Core.Common;
using Game.Core.Combat;
using Game.Core.Cards;
using Game.Core.Map;
using Game.Core.TimeSystem;
using Game.Core.Rewards;
using Game.Core.Decks;
using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public sealed record GameState(
    GamePhase Phase,
    GameRng Rng,
    CombatState? Combat,
    global::Game.Core.Map.NodeId? ActiveCombatNodeId,
    IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions,
    MapState Map,
    TimeState Time,
    RewardState? Reward,
    ImmutableList<CardId> RewardCardPool,
    ImmutableList<CardInstance> RunDeck,
    DeckEditState? DeckEdit,
    int RunHp,
    int RunMaxHp,
    NodeInteractionState? NodeInteraction)
{
    public const int DefaultRunMaxHp = 80;
    public const int RestHealAmount = 20;

    private static readonly MapState InitialMap = SampleMapFactory.CreateDefaultState();

    public static GameState Initial => new(
        GamePhase.DeckSelect,
        GameRng.FromSeed(0),
        null,
        null,
        ImmutableDictionary<CardId, CardDefinition>.Empty,
        InitialMap,
        TimeState.Create(InitialMap),
        null,
        ImmutableList<CardId>.Empty,
        ImmutableList<CardInstance>.Empty,
        null,
        DefaultRunMaxHp,
        DefaultRunMaxHp,
        null);
}
