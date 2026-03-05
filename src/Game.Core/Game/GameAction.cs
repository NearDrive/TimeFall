namespace Game.Core.Game;

public abstract record GameAction;

public sealed record StartRunAction(int Seed) : GameAction;
