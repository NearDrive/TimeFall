using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Core.Cards;

public static class DeckCycleSystem
{
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

        // Deck cycle order must remain explicit and deterministic:
        // 1) reshuffle discard into draw, 2) burn escalated count, 3) resume draw.
        // This keeps replay traces stable for the same seed + action sequence.
        var (reshuffledDeck, reshuffleRng) = Reshuffle(deck, rng);
        generatedEvents.Add(new DeckReshuffled());

        var burnCount = reshuffledDeck.ReshuffleCount + 1;
        var (burnedDeck, nextRng, burnedCards) = Burn(
            reshuffledDeck with { ReshuffleCount = reshuffledDeck.ReshuffleCount + 1 },
            burnCount,
            reshuffleRng);
        generatedEvents.AddRange(burnedCards.Select(c => new CardBurned(c)));

        events = generatedEvents;
        return (burnedDeck, nextRng);
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

    private static (DeckState Deck, GameRng Rng, IReadOnlyList<CardInstance> BurnedCards) Burn(DeckState deck, int count, GameRng rng)
    {
        var burnedCards = new List<CardInstance>();
        var currentRng = rng;
        var burnsToApply = Math.Min(count, deck.DrawPile.Count);

        for (var i = 0; i < burnsToApply; i++)
        {
            var (burnIndex, nextRng) = currentRng.NextInt(0, deck.DrawPile.Count);
            var burnedCard = deck.DrawPile[burnIndex];
            deck = deck with
            {
                DrawPile = deck.DrawPile.RemoveAt(burnIndex),
                BurnPile = deck.BurnPile.Add(burnedCard),
            };
            burnedCards.Add(burnedCard);
            currentRng = nextRng;
        }

        return (deck, currentRng, burnedCards);
    }
}
