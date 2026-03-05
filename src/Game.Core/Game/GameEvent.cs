using Game.Core.Combat;

namespace Game.Core.Game;

public abstract record GameEvent;

public sealed record RunStarted(int Seed) : GameEvent;

public sealed record EnteredCombat : GameEvent;

public sealed record CardDrawn(CardInstance Card) : GameEvent;

public sealed record TurnEnded(TurnOwner NextTurnOwner) : GameEvent;

public sealed record CardDiscarded(CardInstance Card) : GameEvent;
