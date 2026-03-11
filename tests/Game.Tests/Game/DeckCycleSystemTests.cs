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

    private static List<CardInstance> Cards(params string[] ids) => ids.Select(id => new CardInstance(new CardId(id))).ToList();
}
