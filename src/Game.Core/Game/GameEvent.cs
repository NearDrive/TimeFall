namespace Game.Core.Game;

public abstract record GameEvent;

public sealed record RunStarted(int Seed) : GameEvent;
