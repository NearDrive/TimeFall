using Game.Core.Combat;
using Game.Core.Map;
using Game.Core.Rewards;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public abstract record GameEvent;

public sealed record RunStarted(int Seed) : GameEvent;

public sealed record EnteredCombat(NodeId? NodeId, NodeType? NodeType) : GameEvent;

public sealed record CardDrawn(CardInstance Card) : GameEvent;

public sealed record PlayerStrikePlayed(CardInstance Card, int Damage, int EnemyHpAfterHit) : GameEvent;

public sealed record TurnEnded(TurnOwner NextTurnOwner) : GameEvent;

public sealed record CardDiscarded(CardInstance Card) : GameEvent;

public sealed record DeckReshuffled : GameEvent;

public sealed record CardBurned(CardInstance Card) : GameEvent;

public sealed record EnemyAttackPlayed(CardInstance Card, int Damage, int PlayerHpAfterHit) : GameEvent;

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
