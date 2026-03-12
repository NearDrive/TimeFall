using Game.Core.Game;

namespace Game.Data.Save;

public sealed record SaveGameDto(
    int Version,
    GamePhase Phase,
    RngDto Rng,
    string? ActiveCombatNodeId,
    MapDto Map,
    TimeDto Time,
    CombatDto? Combat,
    RewardDto? Reward,
    DeckEditDto? DeckEdit,
    NodeInteractionDto? NodeInteraction,
    string? SelectedDeckId,
    IReadOnlyList<string> RunDeck,
    int RunHp,
    int RunMaxHp)
{
    public const int CurrentVersion = 1;
}

public sealed record RngDto(int Seed, uint State);

public sealed record MapDto(
    IReadOnlyList<MapNodeDto> Nodes,
    IReadOnlyList<MapEdgeDto> Connections,
    IReadOnlyDictionary<string, int> DistanceFromStart,
    string StartNodeId,
    string CurrentNodeId,
    IReadOnlyList<string> VisitedNodeIds,
    IReadOnlyList<string> TriggeredEncounterNodeIds,
    IReadOnlyList<string> ResolvedEncounterNodeIds,
    string? BossNodeId);

public sealed record MapNodeDto(string NodeId, int NodeType);
public sealed record MapEdgeDto(string A, string B);

public sealed record TimeDto(
    int CurrentStep,
    int CurrentAct,
    int MapTurnsSinceTimeAdvance,
    int TimeAdvanceInterval,
    IReadOnlyList<string> CollapsedNodeIds,
    IReadOnlyList<string> CollapseOrder,
    int CollapseCursor,
    bool PlayerCaughtByTime,
    bool TimeBossTriggerPending);

public sealed record RewardDto(int RewardType, IReadOnlyList<string> CardOptions, bool IsClaimed, string? SourceNodeId);
public sealed record DeckEditDto(int RemainingRemovals);
public sealed record NodeInteractionDto(string NodeId, int NodeType, IReadOnlyList<int> Options);

public sealed record CombatDto(
    int TurnOwner,
    CombatEntityDto Player,
    IReadOnlyList<CombatEntityDto> Enemies,
    bool NeedsOverflowDiscard,
    int RequiredOverflowDiscardCount,
    bool PendingDiscardIsFatigue,
    int AttacksPlayedThisTurn,
    bool PlayedAttackThisTurn,
    int NextAttackBonusDamageThisTurn,
    bool NextAttackDoubleThisTurn,
    int AllAttacksBonusDamageThisTurn,
    bool AllAttacksDoubleThisTurn,
    int LastCardMomentumSpent,
    int LastCardDamageDealt);

public sealed record CombatEntityDto(
    string EntityId,
    int HP,
    int MaxHP,
    int Armor,
    IReadOnlyDictionary<int, int> Resources,
    DeckStateDto Deck,
    int Bleed,
    int ReflectNextEnemyAttackDamage);

public sealed record DeckStateDto(
    IReadOnlyList<string> DrawPile,
    IReadOnlyList<string> Hand,
    IReadOnlyList<string> DiscardPile,
    IReadOnlyList<string> BurnPile,
    int ReshuffleCount);
