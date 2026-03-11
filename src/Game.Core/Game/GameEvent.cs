using Game.Core.Combat;
using Game.Core.Map;
using Game.Core.Rewards;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public abstract record GameEvent;

public enum PlayCardRejectionReason
{
    NotInCombat,
    NotPlayerTurn,
    ActionBlockedByPendingDiscard,
    InvalidHandIndex,
    CardDefinitionMissing,
    CardHasNoResolvableEffects,
    CostNotPayable,
    MissingTarget,
    InvalidTarget,
    TargetIsDead,
}

public sealed record DeckSelected(string DeckId) : GameEvent;

public sealed record RunStarted(int Seed) : GameEvent;

public sealed record EnteredCombat(NodeId? NodeId, NodeType? NodeType) : GameEvent;

public sealed record CardDrawn(CardInstance Card) : GameEvent;

public sealed record PlayCardRejected(PlayCardRejectionReason Reason, string Message) : GameEvent;

public sealed record PlayerStrikePlayed(
    CardInstance Card,
    int Damage,
    int BaseDamage,
    int MomentumBonus,
    int EnemyHpBeforeHit,
    int EnemyHpAfterHit,
    int EnemyArmorBeforeHit,
    int EnemyArmorAfterHit,
    int DamageBlockedByArmor) : GameEvent;

public sealed record TurnEnded(TurnOwner NextTurnOwner) : GameEvent;

public sealed record ResourceChanged(TurnOwner Owner, ResourceType ResourceType, int Before, int After, string Reason) : GameEvent;

public sealed record MomentumDecayApplied(int BeforeGm, int AfterGm) : GameEvent;

public sealed record CardDiscarded(CardInstance Card) : GameEvent;

public sealed record DeckReshuffled : GameEvent;

public sealed record CardBurned(CardInstance Card) : GameEvent;

public sealed record ReshuffleFatigueApplied(TurnOwner Owner, int DiscardCount) : GameEvent;

public sealed record FatigueDiscardResolved(TurnOwner Owner, int DiscardCount) : GameEvent;

public sealed record EnemyAttackPlayed(
    CardInstance Card,
    int Damage,
    int PlayerHpBeforeHit,
    int PlayerHpAfterHit,
    int PlayerArmorBeforeHit,
    int PlayerArmorAfterHit,
    int DamageBlockedByArmor) : GameEvent;

public sealed record StatusApplied(TurnOwner Source, TurnOwner Target, string StatusName, int Amount) : GameEvent;

public sealed record StatusTriggered(TurnOwner Target, string StatusName, int Amount, int HpBefore, int HpAfter) : GameEvent;

public sealed record StatusExpired(TurnOwner Target, string StatusName) : GameEvent;

public sealed record MovedToNode(NodeId NodeId) : GameEvent;

public sealed record EncounterTriggered(NodeId NodeId, NodeType NodeType) : GameEvent;

public sealed record EncounterResolved(NodeId NodeId, NodeType NodeType) : GameEvent;

public sealed record EncounterAlreadyResolved(NodeId NodeId, NodeType NodeType) : GameEvent;

public sealed record CombatEnded(NodeId? NodeId, NodeType? NodeType, bool PlayerWon) : GameEvent;

public sealed record CombatVictory(NodeId? NodeId, NodeType? NodeType) : GameEvent;

public sealed record RewardOffered(RewardType RewardType, IReadOnlyList<CardId> CardOptions, NodeId? SourceNodeId) : GameEvent;

public sealed record RewardChosen(RewardType RewardType, CardId CardId, NodeId? SourceNodeId) : GameEvent;

public sealed record RewardSkipped(RewardType RewardType, NodeId? SourceNodeId) : GameEvent;

public sealed record CardAddedToDeck(CardId CardId) : GameEvent;

public sealed record DeckRemovalBegan(int RemainingRemovals) : GameEvent;

public sealed record CardRemovedFromDeck(CardId CardId) : GameEvent;

public sealed record RestUsed(NodeId NodeId, RestOption Option) : GameEvent;

public sealed record Healed(int Amount, int CurrentHp, int MaxHp) : GameEvent;

public sealed record ShopRemovalUsed(NodeId NodeId, CardId CardId) : GameEvent;

public sealed record TimeAdvanced(int Step) : GameEvent;

public sealed record NodeCollapsed(NodeId NodeId) : GameEvent;

public sealed record TimeCaughtPlayer(NodeId NodeId, int Step) : GameEvent;
