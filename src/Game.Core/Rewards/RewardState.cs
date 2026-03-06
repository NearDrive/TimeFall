using Game.Core.Cards;
using Game.Core.Map;
using System.Collections.Immutable;

namespace Game.Core.Rewards;

public sealed record RewardState(
    RewardType RewardType,
    ImmutableList<CardId> CardOptions,
    bool IsClaimed,
    NodeId? SourceNodeId);
