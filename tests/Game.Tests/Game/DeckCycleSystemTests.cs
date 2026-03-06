using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Tests.Game;

[Trait("Lane", "integration")]
public class DeckCycleSystemTests
{
    [Fact]
    public void Reshuffle_ShufflesDiscardDeterministically()
    {
        var firstRun = ReshuffleDrawPile(seed: 77);
        var secondRun = ReshuffleDrawPile(seed: 77);

        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void Reshuffle_DoesNotUseOrderedAppendSemantics()
    {
        var discard = new List<CardInstance>
        {
            Card("d1"), Card("d2"), Card("d3"), Card("d4"), Card("d5"),
        };
        var combatState = CreateCombatState(draw: [], discard: discard);

        var result = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, GameRng.FromSeed(17), out _);
        var actualDrawPile = result.Deck.DrawPile.Select(c => c.DefinitionId.Value).ToList();
        var orderedAppend = discard.Skip(1).Select(c => c.DefinitionId.Value).ToList();

        Assert.NotEqual(orderedAppend, actualDrawPile);
    }

    [Fact]
    public void Burn_EscalatesAfterEachReshuffle()
    {
        var combatState = CreateCombatState(
            draw: [],
            discard: Enumerable.Range(1, 12).Select(i => Card($"c{i}")).ToList());
        var rng = GameRng.FromSeed(99);

        var first = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, rng, out var firstEvents);
        Assert.Equal(1, first.Deck.ReshuffleCount);
        Assert.Single(firstEvents.OfType<CardBurned>());

        var firstDeck = PrepareNextReshuffle(first.Deck, 13, 24);
        var second = DeckCycleSystem.EnsureDrawAvailable(firstDeck, first.Rng, out var secondEvents);
        Assert.Equal(2, second.Deck.ReshuffleCount);
        Assert.Equal(2, secondEvents.OfType<CardBurned>().Count());

        var secondDeck = PrepareNextReshuffle(second.Deck, 25, 36);
        var third = DeckCycleSystem.EnsureDrawAvailable(secondDeck, second.Rng, out var thirdEvents);
        Assert.Equal(3, third.Deck.ReshuffleCount);
        Assert.Equal(3, thirdEvents.OfType<CardBurned>().Count());
    }

    [Fact]
    public void PlayerAndEnemy_ReshuffleCounts_AreIndependent()
    {
        var combatState = CreateCombatState(draw: [], discard: Enumerable.Range(1, 8).Select(i => Card($"p{i}")).ToList()) with
        {
            Enemy = CreateEntity("enemy", [], Enumerable.Range(1, 8).Select(i => Card($"e{i}")).ToList()),
        };
        var rng = GameRng.FromSeed(123);

        var enemyCycle = DeckCycleSystem.EnsureDrawAvailable(combatState.Enemy.Deck, rng, out var enemyEvents);
        combatState = combatState with { Enemy = combatState.Enemy with { Deck = enemyCycle.Deck } };

        var playerCycle = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, enemyCycle.Rng, out var playerEvents);

        Assert.Equal(1, combatState.Enemy.Deck.ReshuffleCount);
        Assert.Equal(0, combatState.Player.Deck.ReshuffleCount);
        Assert.Equal(1, playerCycle.Deck.ReshuffleCount);
        Assert.Single(enemyEvents.OfType<CardBurned>());
        Assert.Single(playerEvents.OfType<CardBurned>());
    }

    [Fact]
    public void PlayerDeck_BurnEscalates_OnlyFromPlayerReshuffles()
    {
        var state = CreateCombatState(draw: [], discard: Enumerable.Range(1, 12).Select(i => Card($"p{i}")).ToList()) with
        {
            Enemy = CreateEntity("enemy", [], Enumerable.Range(1, 12).Select(i => Card($"e{i}")).ToList()),
        };
        var rng = GameRng.FromSeed(456);

        var playerFirst = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, rng, out var playerFirstEvents);
        var enemyFirst = DeckCycleSystem.EnsureDrawAvailable(state.Enemy.Deck, playerFirst.Rng, out _);
        var playerSecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerFirst.Deck, 100, 111), enemyFirst.Rng, out var playerSecondEvents);
        var enemySecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(enemyFirst.Deck, 200, 211), playerSecond.Rng, out _);
        var playerThird = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerSecond.Deck, 300, 311), enemySecond.Rng, out var playerThirdEvents);

        Assert.Single(playerFirstEvents.OfType<CardBurned>());
        Assert.Equal(2, playerSecondEvents.OfType<CardBurned>().Count());
        Assert.Equal(3, playerThirdEvents.OfType<CardBurned>().Count());
    }

    [Fact]
    public void EnemyDeck_BurnEscalates_OnlyFromEnemyReshuffles()
    {
        var state = CreateCombatState(draw: [], discard: Enumerable.Range(1, 12).Select(i => Card($"p{i}")).ToList()) with
        {
            Enemy = CreateEntity("enemy", [], Enumerable.Range(1, 12).Select(i => Card($"e{i}")).ToList()),
        };
        var rng = GameRng.FromSeed(789);

        var enemyFirst = DeckCycleSystem.EnsureDrawAvailable(state.Enemy.Deck, rng, out var enemyFirstEvents);
        var playerFirst = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, enemyFirst.Rng, out _);
        var enemySecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(enemyFirst.Deck, 100, 111), playerFirst.Rng, out var enemySecondEvents);
        var playerSecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerFirst.Deck, 200, 211), enemySecond.Rng, out _);
        var enemyThird = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(enemySecond.Deck, 300, 311), playerSecond.Rng, out var enemyThirdEvents);

        Assert.Single(enemyFirstEvents.OfType<CardBurned>());
        Assert.Equal(2, enemySecondEvents.OfType<CardBurned>().Count());
        Assert.Equal(3, enemyThirdEvents.OfType<CardBurned>().Count());
    }

    [Fact]
    public void Burn_IsDeterministicForSeed()
    {
        var firstRun = SimulateBurnPile(seed: 1234);
        var secondRun = SimulateBurnPile(seed: 1234);

        Assert.Equal(firstRun, secondRun);
    }

    [Fact]
    public void Reshuffle_Burn_HappenBeforeSubsequentDraw()
    {
        var discard = new List<CardInstance>
        {
            Card("d1"), Card("d2"), Card("d3"), Card("d4"), Card("d5"),
        };
        var combatState = CreateCombatState(draw: [], discard: discard);

        var cycle = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, GameRng.FromSeed(17), out var events);
        var drawn = HandManager.Draw(
            combatState with { Player = combatState.Player with { Deck = cycle.Deck } },
            cycle.Rng,
            1);

        Assert.Contains(events, e => e is DeckReshuffled);
        var burned = events.OfType<CardBurned>().Select(e => e.Card.DefinitionId.Value).ToList();
        Assert.Single(burned);

        var expectedTopAfterBurn = cycle.Deck.DrawPile[0].DefinitionId.Value;
        Assert.Single(drawn.DrawnCards);
        Assert.Equal(expectedTopAfterBurn, drawn.DrawnCards[0].DefinitionId.Value);
    }

    private static List<string> ReshuffleDrawPile(int seed)
    {
        var combatState = CreateCombatState(
            draw: [],
            discard: Enumerable.Range(1, 8).Select(i => Card($"c{i}")).ToList());

        var result = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, GameRng.FromSeed(seed), out _);
        return result.Deck.DrawPile.Select(c => c.DefinitionId.Value).ToList();
    }

    private static List<string> SimulateBurnPile(int seed)
    {
        var combatState = CreateCombatState(
            draw: [],
            discard: Enumerable.Range(1, 12).Select(i => Card($"c{i}")).ToList());
        var rng = GameRng.FromSeed(seed);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var result = DeckCycleSystem.EnsureDrawAvailable(combatState.Player.Deck, rng, out _);
            combatState = combatState with { Player = combatState.Player with { Deck = result.Deck } };
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
        var player = CreateEntity("player", draw, discard);
        var enemy = CreateEntity("enemy", [], []);

        return new CombatState(TurnOwner.Player, player, enemy, false, 0);
    }

    private static CombatEntity CreateEntity(string id, List<CardInstance> draw, List<CardInstance> discard)
    {
        return new CombatEntity(
            EntityId: id,
            HP: 10,
            MaxHP: 10,
            Armor: 0,
            Resources: ImmutableDictionary<ResourceType, int>.Empty,
            Deck: new DeckState(draw.ToImmutableList(), ImmutableList<CardInstance>.Empty, discard.ToImmutableList(), ImmutableList<CardInstance>.Empty, 0));
    }

    private static CardInstance Card(string id) => new(new CardId(id));
}
