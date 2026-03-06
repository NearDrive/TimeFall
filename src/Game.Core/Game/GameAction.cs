namespace Game.Core.Game;

public abstract record GameAction;

public sealed record StartRunAction(int Seed) : GameAction;

public sealed record BeginCombatAction(CombatBlueprint Blueprint) : GameAction;

public sealed record PlayCardAction(int HandIndex) : GameAction;

public sealed record EndTurnAction : GameAction;

public sealed record DiscardOverflowAction(int[] Indexes) : GameAction;
