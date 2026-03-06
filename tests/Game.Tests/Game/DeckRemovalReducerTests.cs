using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Game;

public class DeckRemovalReducerTests
{
    [Fact]
    public void RemoveCardFromDeck_RemovesExactlyOneMatchingCard()
    {
        var strike = new CardId("strike");
        var state = CreateMapStateWithRunDeck([strike, strike, new CardId("defend")]);

        var entered = GameReducer.Reduce(state, new BeginDeckRemovalAction()).NewState;
        var result = GameReducer.Reduce(entered, new RemoveCardFromDeckAction(strike));

        Assert.Equal(2, result.NewState.RunDeck.Count);
        Assert.Equal(1, result.NewState.RunDeck.Count(c => c.DefinitionId == strike));
    }

    [Fact]
    public void RemoveCardFromDeck_IsRejected_WhenCardNotPresent()
    {
        var state = CreateMapStateWithRunDeck([new CardId("strike"), new CardId("defend")]);

        var entered = GameReducer.Reduce(state, new BeginDeckRemovalAction()).NewState;
        var result = GameReducer.Reduce(entered, new RemoveCardFromDeckAction(new CardId("bash")));

        Assert.Equal(entered, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void RemoveCardFromDeck_UpdatesRunDeckState()
    {
        var state = CreateMapStateWithRunDeck([new CardId("strike"), new CardId("defend")]);

        var entered = GameReducer.Reduce(state, new BeginDeckRemovalAction()).NewState;
        var result = GameReducer.Reduce(entered, new RemoveCardFromDeckAction(new CardId("defend")));

        Assert.Single(result.NewState.RunDeck);
        Assert.Equal(new CardId("strike"), result.NewState.RunDeck[0].DefinitionId);
    }

    [Fact]
    public void BeginDeckRemoval_IsRejected_InInvalidPhase()
    {
        var state = CreateMapStateWithRunDeck([new CardId("strike")]) with
        {
            Phase = GamePhase.Combat,
            Combat = new CombatState(
                TurnOwner.Player,
                new CombatEntity("player", 20, 20, 0, ImmutableDictionary<ResourceType, int>.Empty,
                    new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
                new CombatEntity("enemy", 10, 10, 0, ImmutableDictionary<ResourceType, int>.Empty,
                    new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
                false,
                0),
        };

        var result = GameReducer.Reduce(state, new BeginDeckRemovalAction());

        Assert.Equal(state, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void RemovalFlow_CannotRemoveWithoutEnteringRemovalState()
    {
        var state = CreateMapStateWithRunDeck([new CardId("strike"), new CardId("defend")]);

        var result = GameReducer.Reduce(state, new RemoveCardFromDeckAction(new CardId("strike")));

        Assert.Equal(state, result.NewState);
        Assert.Empty(result.Events);
    }

    private static GameState CreateMapStateWithRunDeck(CardId[] cardIds)
    {
        var map = SampleMapFactory.CreateDefaultState();
        return GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map),
            Rng = global::Game.Core.Common.GameRng.FromSeed(5),
            RunDeck = cardIds.Select(id => new CardInstance(id)).ToImmutableList(),
            DeckEdit = null,
            Reward = null,
            Combat = null,
        };
    }
}
