namespace Game.Core.Combat;

public sealed record CombatState(
    TurnOwner TurnOwner,
    CombatEntity Player,
    CombatEntity Enemy,
    bool NeedsOverflowDiscard,
    int RequiredOverflowDiscardCount,
    int AttacksPlayedThisTurn = 0,
    bool PlayedAttackThisTurn = false,
    int NextAttackBonusDamageThisTurn = 0,
    bool NextAttackDoubleThisTurn = false,
    int AllAttacksBonusDamageThisTurn = 0,
    bool AllAttacksDoubleThisTurn = false,
    int LastCardMomentumSpent = 0,
    int LastCardDamageDealt = 0);
