using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using CardId = Game.Core.Cards.CardId;
using Game.Core.Game;
using System.Collections.Immutable;

namespace Game.Tests.Game;

public class DeckCycleSystemTests
{
    [Fact]
    public void Draw_WhenDrawPileIsConsumed_ReshufflesAndBurns()
    {
        var combatState = CreateCombatState(
            draw: [Card("draw-1")],
            discard: [Card("discard-1"), Card("discard-2")]);

        var result = HandManager.Draw(combatState, GameRng.FromSeed(17), 2);

        Assert.Equal(1, result.CombatState.ReshuffleCount);
        Assert.Empty(result.CombatState.Player.Deck.DiscardPile);
        Assert.Single(result.CombatState.Player.Deck.BurnPile);
        Assert.Contains(result.Events, e => e is DeckReshuffled);
        Assert.Single(result.Events.OfType<CardBurned>());
    }

    [Fact]
    public void EnsureDrawAvailable_BurnEscalatesAcrossReshuffles()
    {
        var combatState = CreateCombatState(
            draw: [],
            discard: Enumerable.Range(1, 12).Select(i => Card($"c{i}")).ToList());
        var rng = GameRng.FromSeed(99);

        var first = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, rng, combatState, out var firstEvents);
        Assert.Equal(1, first.CombatState.ReshuffleCount);
        Assert.Single(firstEvents.OfType<CardBurned>());

        var firstState = first.CombatState with { Player = first.CombatState.Player with { Deck = PrepareNextReshuffle(first.CombatState.Player.Deck, 13, 24) } };
        var second = DeckCycleSystem.EnsureDrawAvailable(firstState.Player.Deck, first.Rng, firstState, out var secondEvents);
        Assert.Equal(2, second.CombatState.ReshuffleCount);
        Assert.Equal(2, secondEvents.OfType<CardBurned>().Count());

        var secondState = second.CombatState with { Player = second.CombatState.Player with { Deck = PrepareNextReshuffle(second.CombatState.Player.Deck, 25, 36) } };
        var third = DeckCycleSystem.EnsureDrawAvailable(secondState.Player.Deck, second.Rng, secondState, out var thirdEvents);
        Assert.Equal(3, third.CombatState.ReshuffleCount);
        Assert.Equal(3, thirdEvents.OfType<CardBurned>().Count());
    }

    [Fact]
    public void EnsureDrawAvailable_IsDeterministicForSameSeedAndDrawSequence()
    {
        var firstRun = SimulateBurnPile(seed: 1234);
        var secondRun = SimulateBurnPile(seed: 1234);

        Assert.Equal(firstRun, secondRun);
    }

    private static List<string> SimulateBurnPile(int seed)
    {
        var combatState = CreateCombatState(
            draw: [],
            discard: Enumerable.Range(1, 12).Select(i => Card($"c{i}")).ToList());
        var rng = GameRng.FromSeed(seed);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var result = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, rng, combatState, out _);
            combatState = result.CombatState;
            rng = result.Rng;

            if (cycle < 2)
            {
                combatState = combatState with
                {
                    Player = combatState.Player with
                    {
                        Deck = PrepareNextReshuffle(combatState.Player.Deck, (cycle + 1) * 100, (cycle + 1) * 100 + 11),
                    },
                };
            }
        }

        return combatState.Player.Deck.BurnPile.Select(c => c.DefinitionId.Value).ToList();
    }

    private static DeckState PrepareNextReshuffle(DeckState deck, int min, int max)
    {
        return deck with
        {
            DrawPile = ImmutableList<CardInstance>.Empty,
            DiscardPile = Enumerable.Range(min, max - min + 1).Select(i => Card($"c{i}")).ToImmutableList(),
        };
    }

    private static CombatState CreateCombatState(List<CardInstance> draw, List<CardInstance> discard)
    {
        var player = new CombatEntity(
            EntityId: "player",
            HP: 10,
            MaxHP: 10,
            Armor: 0,
            Resources: ImmutableDictionary<ResourceType, int>.Empty,
            Deck: new DeckState(draw.ToImmutableList(), ImmutableList<CardInstance>.Empty, discard.ToImmutableList(), ImmutableList<CardInstance>.Empty));

        var enemy = new CombatEntity(
            EntityId: "enemy",
            HP: 10,
            MaxHP: 10,
            Armor: 0,
            Resources: ImmutableDictionary<ResourceType, int>.Empty,
            Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty));

        return new CombatState(TurnOwner.Player, 0, player, enemy, false, 0);
    }

    private static CardInstance Card(string id) => new(new CardId(id));
}
