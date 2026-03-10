using System.Collections.Immutable;

namespace Game.Core.Combat;

public sealed record CombatState(
    TurnOwner TurnOwner,
    CombatEntity Player,
    ImmutableList<CombatEntity> Enemies,
    bool NeedsOverflowDiscard,
    int RequiredOverflowDiscardCount,
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
        TurnOwner turnOwner,
        CombatEntity player,
        CombatEntity enemy,
        bool needsOverflowDiscard,
        int requiredOverflowDiscardCount,
        int attacksPlayedThisTurn = 0,
        bool playedAttackThisTurn = false,
        int nextAttackBonusDamageThisTurn = 0,
        bool nextAttackDoubleThisTurn = false,
        int allAttacksBonusDamageThisTurn = 0,
        bool allAttacksDoubleThisTurn = false,
        int lastCardMomentumSpent = 0,
        int lastCardDamageDealt = 0)
        : this(turnOwner, player, ImmutableList.Create(enemy), needsOverflowDiscard, requiredOverflowDiscardCount, attacksPlayedThisTurn, playedAttackThisTurn, nextAttackBonusDamageThisTurn, nextAttackDoubleThisTurn, allAttacksBonusDamageThisTurn, allAttacksDoubleThisTurn, lastCardMomentumSpent, lastCardDamageDealt)
    {
    }

    public CombatEntity Enemy => Enemies[0];
}
