namespace Game.Core.Combat;

public sealed record DeckState(
    List<CardInstance> DrawPile,
    List<CardInstance> Hand,
    List<CardInstance> DiscardPile,
    List<CardInstance> BurnPile);
