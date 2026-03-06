using System.Collections.Immutable;

namespace Game.Core.Combat;

public sealed record DeckState(
    ImmutableList<CardInstance> DrawPile,
    ImmutableList<CardInstance> Hand,
    ImmutableList<CardInstance> DiscardPile,
    ImmutableList<CardInstance> BurnPile,
    int ReshuffleCount);
