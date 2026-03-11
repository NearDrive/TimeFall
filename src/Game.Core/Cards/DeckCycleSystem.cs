using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Core.Cards;

public static class DeckCycleSystem
{
    public static (IReadOnlyList<CardInstance> Shuffled, GameRng Rng) ShuffleInitialDrawPile(
        IReadOnlyList<CardInstance> cards,
        GameRng rng)
    {
        var (shuffled, nextRng) = Shuffle(cards.ToList(), rng);
        return (shuffled, nextRng);
    }

    public static (DeckState Deck, GameRng Rng) EnsureDrawAvailable(
        DeckState deck,
        GameRng rng,
        out IReadOnlyList<GameEvent> events)
    {
        if (deck.DrawPile.Count > 0 || deck.DiscardPile.Count == 0)
        {
            events = Array.Empty<GameEvent>();
            return (deck, rng);
        }

        var generatedEvents = new List<GameEvent>();

        var (reshuffledDeck, reshuffleRng) = Reshuffle(deck, rng);
        generatedEvents.Add(new DeckReshuffled());

        events = generatedEvents;
        return (reshuffledDeck with { ReshuffleCount = reshuffledDeck.ReshuffleCount + 1 }, reshuffleRng);
    }

    private static (DeckState Deck, GameRng Rng) Reshuffle(DeckState deck, GameRng rng)
    {
        var candidatePool = deck.DiscardPile.ToList();
        var (shuffledCards, nextRng) = Shuffle(candidatePool, rng);

        return (deck with
        {
            DrawPile = shuffledCards.ToImmutableList(),
            DiscardPile = ImmutableList<CardInstance>.Empty,
        }, nextRng);
    }

    private static (List<CardInstance> Shuffled, GameRng Rng) Shuffle(List<CardInstance> cards, GameRng rng)
    {
        var shuffled = new List<CardInstance>(cards);
        var currentRng = rng;

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var (swapIndex, nextRng) = currentRng.NextInt(0, i + 1);
            (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
            currentRng = nextRng;
        }

        return (shuffled, currentRng);
    }
}
