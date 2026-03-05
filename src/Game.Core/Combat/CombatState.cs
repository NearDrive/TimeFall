namespace Game.Core.Combat;

public sealed record CombatState(
    TurnOwner TurnOwner,
    int ReshuffleCount,
    CombatEntity Player,
    CombatEntity Enemy,
    bool NeedsOverflowDiscard,
    int RequiredOverflowDiscardCount);
