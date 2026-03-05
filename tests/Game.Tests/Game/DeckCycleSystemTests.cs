using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;

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

        PrepareNextReshuffle(first.CombatState.Player.Deck, 13, 24);
        var second = DeckCycleSystem.EnsureDrawAvailable(first.CombatState.Player.Deck, first.Rng, first.CombatState, out var secondEvents);
        Assert.Equal(2, second.CombatState.ReshuffleCount);
        Assert.Equal(2, secondEvents.OfType<CardBurned>().Count());

        PrepareNextReshuffle(second.CombatState.Player.Deck, 25, 36);
        var third = DeckCycleSystem.EnsureDrawAvailable(second.CombatState.Player.Deck, second.Rng, second.CombatState, out var thirdEvents);
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
                PrepareNextReshuffle(combatState.Player.Deck, (cycle + 1) * 100, (cycle + 1) * 100 + 11);
            }
        }

        return combatState.Player.Deck.BurnPile.Select(c => c.DefinitionId.Value).ToList();
    }

    private static void PrepareNextReshuffle(DeckState deck, int min, int max)
    {
        deck.DrawPile.Clear();
        deck.DiscardPile.Clear();
        deck.DiscardPile.AddRange(Enumerable.Range(min, max - min + 1).Select(i => Card($"c{i}")));
    }

    private static CombatState CreateCombatState(List<CardInstance> draw, List<CardInstance> discard)
    {
        var player = new CombatEntity(
            EntityId: "player",
            HP: 10,
            MaxHP: 10,
            Armor: 0,
            Resources: new Dictionary<ResourceType, int>(),
            Deck: new DeckState(draw, new List<CardInstance>(), discard, new List<CardInstance>()));

        var enemy = new CombatEntity(
            EntityId: "enemy",
            HP: 10,
            MaxHP: 10,
            Armor: 0,
            Resources: new Dictionary<ResourceType, int>(),
            Deck: new DeckState(new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>()));

        return new CombatState(TurnOwner.Player, 0, player, enemy, false, 0);
    }

    private static CardInstance Card(string id) => new(new CardId(id));
}
