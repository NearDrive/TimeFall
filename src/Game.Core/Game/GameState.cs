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
    ImmutableList<CardId> EnabledRewardPoolCardIds,
    IReadOnlyDictionary<string, RunDeckDefinition> DeckDefinitions,
    ImmutableList<string> AvailableDeckIds,
    string? SelectedDeckId,
    bool HasActiveRunSave,
    ImmutableList<CardInstance> RunDeck,
    DeckEditState? DeckEdit,
    RewardPoolEditState? RewardPoolEdit,
    int RunHp,
    int RunMaxHp,
    NodeInteractionState? NodeInteraction,
    IReadOnlyDictionary<string, EnemyDefinition> EnemyDefinitions,
    ZoneSpawnTable? Zone1SpawnTable,
    SandboxState? Sandbox = null,
    GameMode Mode = GameMode.Run)
{
    public const int DefaultRunMaxHp = 80;
    public const int RestHealAmount = 20;

    private static readonly MapState InitialMap = SampleMapFactory.CreateDefaultState();

    public static GameState Initial => new(
        GamePhase.MainMenu,
        GameRng.FromSeed(0),
        null,
        null,
        ImmutableDictionary<CardId, CardDefinition>.Empty,
        InitialMap,
        TimeState.Create(InitialMap),
        null,
        ImmutableList<CardId>.Empty,
        ImmutableDictionary<string, RunDeckDefinition>.Empty,
        ImmutableList<string>.Empty,
        null,
        false,
        ImmutableList<CardInstance>.Empty,
        null,
        null,
        DefaultRunMaxHp,
        DefaultRunMaxHp,
        null,
        ImmutableDictionary<string, EnemyDefinition>.Empty,
        null,
        null);

    public static GameState CreateInitial(
        IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions,
        IReadOnlyDictionary<string, RunDeckDefinition> deckDefinitions,
        IReadOnlyList<CardId> rewardCardPool,
        IReadOnlyDictionary<string, EnemyDefinition>? enemyDefinitions = null,
        ZoneSpawnTable? zone1SpawnTable = null)
    {
        var availableDeckIds = deckDefinitions.Keys.OrderBy(id => id, StringComparer.Ordinal).ToImmutableList();
        return Initial with
        {
            CardDefinitions = cardDefinitions,
            DeckDefinitions = deckDefinitions,
            AvailableDeckIds = availableDeckIds,
            EnabledRewardPoolCardIds = rewardCardPool.ToImmutableList(),
            EnemyDefinitions = enemyDefinitions ?? ImmutableDictionary<string, EnemyDefinition>.Empty,
            Zone1SpawnTable = zone1SpawnTable,
            Sandbox = null,
        };
    }

    public GameState ResetToDeckSelect()
    {
        var mapState = SampleMapFactory.CreateDefaultState();
        var maxHp = SelectedDeckId is { } deckId && DeckDefinitions.TryGetValue(deckId, out var deck)
            ? deck.BaseMaxHp
            : DefaultRunMaxHp;

        return this with
        {
            Phase = GamePhase.MainMenu,
            Rng = GameRng.FromSeed(0),
            Combat = null,
            ActiveCombatNodeId = null,
            Map = mapState,
            Time = TimeState.Create(mapState),
            Reward = null,
            RunDeck = ImmutableList<CardInstance>.Empty,
            DeckEdit = null,
            RewardPoolEdit = null,
            RunHp = maxHp,
            RunMaxHp = maxHp,
            NodeInteraction = null,
            Sandbox = null,
        };
    }
}
