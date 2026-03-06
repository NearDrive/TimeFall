using Game.Core.Combat;
using Game.Core.Map;

namespace Game.Core.Game;

public abstract record GameEvent;

public sealed record RunStarted(int Seed) : GameEvent;

public sealed record EnteredCombat : GameEvent;

public sealed record CardDrawn(CardInstance Card) : GameEvent;

public sealed record PlayerStrikePlayed(CardInstance Card, int Damage, int EnemyHpAfterHit) : GameEvent;

public sealed record TurnEnded(TurnOwner NextTurnOwner) : GameEvent;

public sealed record CardDiscarded(CardInstance Card) : GameEvent;

public sealed record DeckReshuffled : GameEvent;

public sealed record CardBurned(CardInstance Card) : GameEvent;

public sealed record EnemyAttackPlayed(CardInstance Card, int Damage, int PlayerHpAfterHit) : GameEvent;

public sealed record MovedToNode(NodeId NodeId) : GameEvent;

public sealed record EncounterResolved(NodeId NodeId, NodeType NodeType) : GameEvent;

public sealed record EncounterAlreadyResolved(NodeId NodeId, NodeType NodeType) : GameEvent;


public sealed record TimeAdvanced(int Step) : GameEvent;

public sealed record NodeCollapsed(NodeId NodeId) : GameEvent;

public sealed record TimeCaughtPlayer(NodeId NodeId, int Step) : GameEvent;
