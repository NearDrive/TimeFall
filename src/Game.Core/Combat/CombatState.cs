using System.Collections.Immutable;

namespace Game.Core.Combat;

public sealed record CombatState(
    TurnOwner TurnOwner,
    CombatEntity Player,
    ImmutableList<CombatEntity> Enemies,
    bool NeedsOverflowDiscard,
    int RequiredOverflowDiscardCount,
    bool PendingDiscardIsFatigue = false,
    int AttacksPlayedThisTurn = 0,
    bool PlayedAttackThisTurn = false,
    int NextAttackBonusDamageThisTurn = 0,
    bool NextAttackDoubleThisTurn = false,
    int AllAttacksBonusDamageThisTurn = 0,
    bool AllAttacksDoubleThisTurn = false,
    int LastCardMomentumSpent = 0,
    int LastCardDamageDealt = 0)
{
    public CombatState(
        TurnOwner TurnOwner,
        CombatEntity Player,
        CombatEntity Enemy,
        bool NeedsOverflowDiscard,
        int RequiredOverflowDiscardCount,
        bool PendingDiscardIsFatigue = false,
        int AttacksPlayedThisTurn = 0,
        bool PlayedAttackThisTurn = false,
        int NextAttackBonusDamageThisTurn = 0,
        bool NextAttackDoubleThisTurn = false,
        int AllAttacksBonusDamageThisTurn = 0,
        bool AllAttacksDoubleThisTurn = false,
        int LastCardMomentumSpent = 0,
        int LastCardDamageDealt = 0)
        : this(TurnOwner, Player, ImmutableList.Create(Enemy), NeedsOverflowDiscard, RequiredOverflowDiscardCount, PendingDiscardIsFatigue, AttacksPlayedThisTurn, PlayedAttackThisTurn, NextAttackBonusDamageThisTurn, NextAttackDoubleThisTurn, AllAttacksBonusDamageThisTurn, AllAttacksDoubleThisTurn, LastCardMomentumSpent, LastCardDamageDealt)
    {
    }

    public CombatEntity Enemy
    {
        get => Enemies[0];
        init => Enemies = Enemies.Count == 0 ? ImmutableList.Create(value) : Enemies.SetItem(0, value);
    }
}
