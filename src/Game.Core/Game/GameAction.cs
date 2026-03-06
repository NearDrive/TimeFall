using Game.Core.Cards;
using Game.Core.Map;
using CardId = Game.Core.Cards.CardId;

namespace Game.Core.Game;

public abstract record GameAction;

public sealed record StartRunAction(int Seed) : GameAction;

public sealed record BeginCombatAction(CombatBlueprint Blueprint, IReadOnlyDictionary<CardId, CardDefinition> CardDefinitions) : GameAction;

public sealed record PlayCardAction(int HandIndex) : GameAction;

public sealed record EndTurnAction : GameAction;

public sealed record DiscardOverflowAction(int[] Indexes) : GameAction;

public sealed record MoveToNodeAction(NodeId NodeId) : GameAction;
