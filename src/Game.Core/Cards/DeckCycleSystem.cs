using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Core.Cards;

public static class DeckCycleSystem
{
    public static (DeckState Deck, CombatState CombatState, GameRng Rng) EnsureDrawAvailable(
        DeckState deck,
        GameRng rng,
        CombatState combatState,
        out IReadOnlyList<GameEvent> events)
    {
        if (deck.DrawPile.Count > 0 || deck.DiscardPile.Count == 0)
        {
            events = Array.Empty<GameEvent>();
            return (deck, combatState, rng);
        }

        var generatedEvents = new List<GameEvent>();
        var reshuffledDeck = Reshuffle(deck);
        generatedEvents.Add(new DeckReshuffled());

        var burnCount = combatState.ReshuffleCount + 1;
        var (burnedDeck, nextRng, burnedCards) = Burn(reshuffledDeck, burnCount, rng);
        generatedEvents.AddRange(burnedCards.Select(c => new CardBurned(c)));

        events = generatedEvents;
        return (burnedDeck, combatState with { ReshuffleCount = combatState.ReshuffleCount + 1 }, nextRng);
    }

    private static DeckState Reshuffle(DeckState deck)
    {
        deck.DrawPile.AddRange(deck.DiscardPile);
        deck.DiscardPile.Clear();
        return deck;
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
            deck.DrawPile.RemoveAt(burnIndex);
            deck.BurnPile.Add(burnedCard);
            burnedCards.Add(burnedCard);
            currentRng = nextRng;
        }

        return (deck, currentRng, burnedCards);
    }
}
