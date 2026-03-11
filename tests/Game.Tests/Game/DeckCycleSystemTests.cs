using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Content;
using Game.Core.Game;
using System.Collections.Immutable;
using CardId = Game.Core.Cards.CardId;

namespace Game.Tests.Game;

[Trait("Lane", "integration")]
public class DeckCycleSystemTests
{
    [Fact]
    public void FirstReshuffle_RefillThenDiscard1()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: Cards("a", "b", "c", "d", "e"));

        var result = HandManager.Draw(state, GameRng.FromSeed(3), 1);

        Assert.True(result.CombatState.NeedsOverflowDiscard);
        Assert.Equal(1, result.CombatState.RequiredOverflowDiscardCount);
        Assert.Equal(5, result.CombatState.Player.Deck.Hand.Count);
        Assert.Contains(result.Events, e => e is DeckReshuffled);
        Assert.Contains(result.Events, e => e is ReshuffleFatigueApplied { DiscardCount: 1 });
    }

    [Fact]
    public void SecondReshuffle_RefillThenDiscard2()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: Cards("a", "b", "c", "d", "e", "f"), reshuffleCount: 1);

        var result = HandManager.Draw(state, GameRng.FromSeed(4), 1);

        Assert.Equal(2, result.CombatState.RequiredOverflowDiscardCount);
        Assert.Contains(result.Events, e => e is ReshuffleFatigueApplied { DiscardCount: 2 });
    }

    [Fact]
    public void DiscardIsCappedAtInitialHandSize()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: Cards("a", "b", "c", "d", "e"), reshuffleCount: 9);

        var result = HandManager.Draw(state, GameRng.FromSeed(8), 1);

        Assert.Equal(5, result.CombatState.RequiredOverflowDiscardCount);
    }

    [Fact]
    public void PlayerCannotActBeforeFatigueDiscardResolved()
    {
        var state = GameState.Initial with
        {
            Phase = GamePhase.Combat,
            CardDefinitions = PlaytestContent.CardDefinitions,
            Combat = CreateCombatState(draw: [], hand: [], discard: Cards("strike", "strike", "strike", "strike", "strike")),
        };

        var drawn = HandManager.Draw(state.Combat!, GameRng.FromSeed(9), 1).CombatState;
        var blockedState = state with { Combat = drawn };

        var (_, events) = GameReducer.Reduce(blockedState, new PlayCardAction(0));

        Assert.Contains(events, e => e is PlayCardRejected { Reason: PlayCardRejectionReason.ActionBlockedByPendingDiscard });
    }

    [Fact]
    public void EnemyDiscardIsDeterministic()
    {
        var combat = CreateCombatState(draw: [], hand: [], discard: Cards("enemy-attack", "enemy-attack", "enemy-attack")) with
        {
            Enemy = CreateEntity("enemy", [], [], Cards("x", "y", "z"), reshuffleCount: 1),
        };

        var result = EnemyController.DrawAtTurnStart(combat, GameRng.FromSeed(10), 1);

        Assert.Contains(result.Events, e => e is ReshuffleFatigueApplied { Owner: TurnOwner.Enemy, DiscardCount: 2 });
        Assert.Equal(2, result.CombatState.Enemy.Deck.DiscardPile.TakeLast(2).Count());
    }

    [Fact]
    public void BurnOnReshuffleNoLongerOccurs()
    {
        var deck = new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, Cards("a", "b", "c").ToImmutableList(), ImmutableList<CardInstance>.Empty, 0);

        var result = DeckCycleSystem.EnsureDrawAvailable(deck, GameRng.FromSeed(11), out var events);

        Assert.Empty(events.OfType<CardBurned>());
        Assert.Empty(result.Deck.BurnPile);
    }

    [Fact]
    public void ReshuffleCountResetsOnCombatStart()
    {
        var blueprint = new CombatBlueprint(
            Player: new CombatantBlueprint("player", 30, 30, 0, ImmutableDictionary<ResourceType, int>.Empty, [new CardId("strike")]),
            Enemy: new CombatantBlueprint("enemy", 10, 10, 0, ImmutableDictionary<ResourceType, int>.Empty, []));

        var started = GameReducer.Reduce(GameState.Initial, new BeginCombatAction(blueprint, PlaytestContent.CardDefinitions)).NewState;

        Assert.Equal(0, started.Combat!.Player.Deck.ReshuffleCount);
        Assert.Equal(0, started.Combat.Enemy.Deck.ReshuffleCount);
    }

    [Fact]
    public void PartialHandRefillThenDiscard()
    {
        var state = CreateCombatState(draw: [], hand: Cards("h1", "h2"), discard: Cards("a", "b", "c"));

        var result = HandManager.Draw(state, GameRng.FromSeed(12), 1);

        Assert.Equal(5, result.CombatState.Player.Deck.Hand.Count);
        Assert.Equal(1, result.CombatState.RequiredOverflowDiscardCount);
    }

    [Fact]
    public void EmptyDrawAndDiscardSafe()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: []);

        var result = HandManager.Draw(state, GameRng.FromSeed(13), 1);

        Assert.Empty(result.DrawnCards);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void FatigueSystemDeterministicForSeed()
    {
        var first = HandManager.Draw(CreateCombatState(draw: [], hand: [], discard: Cards("a", "b", "c", "d", "e")), GameRng.FromSeed(90), 1);
        var second = HandManager.Draw(CreateCombatState(draw: [], hand: [], discard: Cards("a", "b", "c", "d", "e")), GameRng.FromSeed(90), 1);

        Assert.Equal(first.CombatState.Player.Deck.Hand, second.CombatState.Player.Deck.Hand);
        Assert.Equal(first.CombatState.RequiredOverflowDiscardCount, second.CombatState.RequiredOverflowDiscardCount);
    }

    [Fact]
    public void PlayerDeck_ReshufflesMultipleTimes_BeforeEnemy()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: CardsRange("p", 1, 12)) with
        {
            Enemy = CreateEntity("enemy", [], [], CardsRange("e", 1, 12), reshuffleCount: 0),
        };

        var playerFirst = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, GameRng.FromSeed(501), out _);
        var playerSecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerFirst.Deck, "p", 100, 111), playerFirst.Rng, out _);
        var playerThird = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerSecond.Deck, "p", 200, 211), playerSecond.Rng, out _);

        Assert.Equal(0, state.Enemy.Deck.ReshuffleCount);
        Assert.Equal(1, playerFirst.Deck.ReshuffleCount);
        Assert.Equal(2, playerSecond.Deck.ReshuffleCount);
        Assert.Equal(3, playerThird.Deck.ReshuffleCount);
        Assert.Empty(playerThird.Deck.BurnPile);

        var enemyFirst = DeckCycleSystem.EnsureDrawAvailable(state.Enemy.Deck, playerThird.Rng, out _);
        Assert.Equal(1, enemyFirst.Deck.ReshuffleCount);
    }

    [Fact]
    public void EnemyDeck_ReshufflesMultipleTimes_BeforePlayer()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: CardsRange("p", 1, 12)) with
        {
            Enemy = CreateEntity("enemy", [], [], CardsRange("e", 1, 12), reshuffleCount: 0),
        };

        var enemyFirst = DeckCycleSystem.EnsureDrawAvailable(state.Enemy.Deck, GameRng.FromSeed(601), out _);
        var enemySecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(enemyFirst.Deck, "e", 300, 311), enemyFirst.Rng, out _);
        var enemyThird = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(enemySecond.Deck, "e", 400, 411), enemySecond.Rng, out _);

        Assert.Equal(0, state.Player.Deck.ReshuffleCount);
        Assert.Equal(1, enemyFirst.Deck.ReshuffleCount);
        Assert.Equal(2, enemySecond.Deck.ReshuffleCount);
        Assert.Equal(3, enemyThird.Deck.ReshuffleCount);
        Assert.Empty(enemyThird.Deck.BurnPile);

        var playerFirst = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, enemyThird.Rng, out _);
        Assert.Equal(1, playerFirst.Deck.ReshuffleCount);
    }

    [Fact]
    public void PlayerReshuffles_EnemyNeverReshuffles()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: CardsRange("p", 1, 12)) with
        {
            Enemy = CreateEntity("enemy", [], [], CardsRange("e", 1, 12), reshuffleCount: 0),
        };

        var playerFirst = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, GameRng.FromSeed(701), out _);
        var playerSecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerFirst.Deck, "p", 500, 511), playerFirst.Rng, out _);
        var playerThird = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerSecond.Deck, "p", 600, 611), playerSecond.Rng, out _);

        Assert.Equal(0, state.Enemy.Deck.ReshuffleCount);
        Assert.Equal(1, playerFirst.Deck.ReshuffleCount);
        Assert.Equal(2, playerSecond.Deck.ReshuffleCount);
        Assert.Equal(3, playerThird.Deck.ReshuffleCount);
        Assert.Empty(playerThird.Deck.BurnPile);
        Assert.Equal(12, state.Enemy.Deck.DiscardPile.Count);
    }

    [Fact]
    public void Reshuffle_WithEmptyDiscard_IsNoOp()
    {
        var state = CreateCombatState(draw: [], hand: [], discard: []);

        var cycle = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, GameRng.FromSeed(801), out var events);

        Assert.Equal(0, cycle.Deck.ReshuffleCount);
        Assert.Empty(cycle.Deck.DrawPile);
        Assert.Empty(cycle.Deck.DiscardPile);
        Assert.Empty(events);
    }

    [Fact]
    public void BurnCount_Truncates_WhenDeckTooSmall()
    {
        var deck = new DeckState(
            DrawPile: ImmutableList<CardInstance>.Empty,
            Hand: ImmutableList<CardInstance>.Empty,
            DiscardPile: Cards("c1", "c2").ToImmutableList(),
            BurnPile: ImmutableList<CardInstance>.Empty,
            ReshuffleCount: 5);

        var cycle = DeckCycleSystem.EnsureDrawAvailable(deck, GameRng.FromSeed(901), out var events);

        Assert.Equal(6, cycle.Deck.ReshuffleCount);
        Assert.Empty(events.OfType<CardBurned>());
        Assert.Empty(cycle.Deck.BurnPile);
        Assert.Equal(2, cycle.Deck.DrawPile.Count);
    }

    [Fact]
    public void IndependentDeckCycles_RemainIndependentAcrossMultipleTurns()
    {
        var firstRun = SimulateIndependentDeckCycles(seed: 1001);
        var secondRun = SimulateIndependentDeckCycles(seed: 1001);

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(3, firstRun.PlayerReshuffles);
        Assert.Equal(2, firstRun.EnemyReshuffles);
        Assert.Equal(0, firstRun.PlayerBurnPileCount);
        Assert.Equal(0, firstRun.EnemyBurnPileCount);
    }

    private static CombatState CreateCombatState(List<CardInstance> draw, List<CardInstance> hand, List<CardInstance> discard, int reshuffleCount = 0)
    {
        var player = CreateEntity("player", draw, hand, discard, reshuffleCount);
        var enemy = CreateEntity("enemy", [], [], [], 0);
        return new CombatState(TurnOwner.Player, player, enemy, false, 0);
    }

    private static CombatEntity CreateEntity(string id, List<CardInstance> draw, List<CardInstance> hand, List<CardInstance> discard, int reshuffleCount)
    {
        return new CombatEntity(
            id,
            10,
            10,
            0,
            ImmutableDictionary<ResourceType, int>.Empty,
            new DeckState(draw.ToImmutableList(), hand.ToImmutableList(), discard.ToImmutableList(), ImmutableList<CardInstance>.Empty, reshuffleCount));
    }

    private static DeckState PrepareNextReshuffle(DeckState deck, string prefix, int min, int max)
    {
        return deck with
        {
            DrawPile = ImmutableList<CardInstance>.Empty,
            DiscardPile = CardsRange(prefix, min, max).ToImmutableList(),
        };
    }

    private static (int PlayerReshuffles, int EnemyReshuffles, int PlayerBurnPileCount, int EnemyBurnPileCount) SimulateIndependentDeckCycles(int seed)
    {
        var state = CreateCombatState(draw: [], hand: [], discard: CardsRange("p", 1, 12)) with
        {
            Enemy = CreateEntity("enemy", [], [], CardsRange("e", 1, 12), 0),
        };
        var rng = GameRng.FromSeed(seed);

        var playerFirst = DeckCycleSystem.EnsureDrawAvailable(state.Player.Deck, rng, out _);
        var enemyFirst = DeckCycleSystem.EnsureDrawAvailable(state.Enemy.Deck, playerFirst.Rng, out _);
        var playerSecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerFirst.Deck, "p", 700, 711), enemyFirst.Rng, out _);
        var playerThird = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(playerSecond.Deck, "p", 800, 811), playerSecond.Rng, out _);
        var enemySecond = DeckCycleSystem.EnsureDrawAvailable(PrepareNextReshuffle(enemyFirst.Deck, "e", 900, 911), playerThird.Rng, out _);

        return (
            PlayerReshuffles: playerThird.Deck.ReshuffleCount,
            EnemyReshuffles: enemySecond.Deck.ReshuffleCount,
            PlayerBurnPileCount: playerThird.Deck.BurnPile.Count,
            EnemyBurnPileCount: enemySecond.Deck.BurnPile.Count);
    }

    private static List<CardInstance> Cards(params string[] ids) => ids.Select(id => new CardInstance(new CardId(id))).ToList();

    private static List<CardInstance> CardsRange(string prefix, int min, int max)
        => Enumerable.Range(min, max - min + 1).Select(i => new CardInstance(new CardId($"{prefix}{i}"))).ToList();
}
