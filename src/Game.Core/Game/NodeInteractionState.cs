using System.Collections.Immutable;
using Game.Core.Map;

namespace Game.Core.Game;

public enum NodeInteractionOption
{
    RestHeal = 0,
    ShopRemoveCard = 1,
}

public sealed record NodeInteractionState(
    NodeId NodeId,
    NodeType NodeType,
    ImmutableArray<NodeInteractionOption> Options);

public enum RestOption
{
    Heal = 0,
}
