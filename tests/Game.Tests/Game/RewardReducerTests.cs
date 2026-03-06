using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Game;

public class RewardReducerTests
{
    [Fact]
    public void CombatVictory_TransitionsToRewardPhase()
    {
        var rewardState = CreateRewardSelectionState(seed: 42);

        Assert.Equal(GamePhase.RewardSelection, rewardState.Phase);
    }

    [Fact]
    public void RewardPhase_OffersThreeCards()
    {
        var rewardState = CreateRewardSelectionState(seed: 42);

        Assert.NotNull(rewardState.Reward);
        Assert.Equal(3, rewardState.Reward!.CardOptions.Count);
    }

    [Fact]
    public void RewardGeneration_IsDeterministic()
    {
        var first = CreateRewardSelectionState(seed: 1337);
        var second = CreateRewardSelectionState(seed: 1337);

        Assert.Equal(first.Reward!.CardOptions, second.Reward!.CardOptions);
    }

    [Fact]
    public void ChooseRewardCard_AddsCardToDeck()
    {
        var rewardState = CreateRewardSelectionState(seed: 17);
        var chosen = rewardState.Reward!.CardOptions[1];

        var (newState, events) = GameReducer.Reduce(rewardState, new ChooseRewardCardAction(chosen));

        Assert.Contains(newState.PlayerDeckDiscardPile, c => c.DefinitionId == chosen);
        Assert.Contains(events, e => e is CardAddedToDeck { CardId: var id } && id == chosen);
    }

    [Fact]
    public void ChooseRewardCard_ReturnsToMapExploration()
    {
        var rewardState = CreateRewardSelectionState(seed: 17);

        var result = GameReducer.Reduce(rewardState, new ChooseRewardCardAction(rewardState.Reward!.CardOptions[0]));

        Assert.Equal(GamePhase.MapExploration, result.NewState.Phase);
    }

    [Fact]
    public void InvalidRewardChoice_IsRejected()
    {
        var rewardState = CreateRewardSelectionState(seed: 17);

        var result = GameReducer.Reduce(rewardState, new ChooseRewardCardAction(new CardId("not-offered")));

        Assert.Equal(rewardState, result.NewState);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void RewardChoice_NotAllowedOutsideRewardPhase()
    {
        var map = SampleMapFactory.CreateDefaultState();
        var state = GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map),
        };

        var result = GameReducer.Reduce(state, new ChooseRewardCardAction(new CardId("strike")));

        Assert.Equal(state, result.NewState);
        Assert.Empty(result.Events);
    }


    [Fact]
    public void ClaimedRewardCard_IsIncludedInNextCombatDeck()
    {
        var rewardState = CreateRewardSelectionState(seed: 29);
        var chosen = rewardState.Reward!.CardOptions[0];

        var afterClaim = GameReducer.Reduce(rewardState, new ChooseRewardCardAction(chosen)).NewState;
        var (nextCombat, _) = GameReducer.Reduce(afterClaim, new MoveToNodeAction(new NodeId("elite-1")));

        Assert.Equal(GamePhase.Combat, nextCombat.Phase);

        var deck = nextCombat.Combat!.Player.Deck;
        var allCards = deck.DrawPile.Concat(deck.Hand).Concat(deck.DiscardPile).Concat(deck.BurnPile).ToArray();

        Assert.Contains(allCards, card => card.DefinitionId == chosen);
        Assert.Equal(11, allCards.Length);
    }

    [Fact]
    public void RewardState_ClearsAfterChoice()
    {
        var rewardState = CreateRewardSelectionState(seed: 21);

        var result = GameReducer.Reduce(rewardState, new ChooseRewardCardAction(rewardState.Reward!.CardOptions[0]));

        Assert.Null(result.NewState.Reward);
    }

    private static GameState CreateRewardSelectionState(int seed)
    {
        var map = SampleMapFactory.CreateDefaultState();
        var state = GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map),
            Rng = global::Game.Core.Common.GameRng.FromSeed(seed),
        };

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        var strikeIndex = afterEntry.Combat!.Player.Deck.Hand
            .Select((card, index) => (card, index))
            .First(tuple => tuple.card.DefinitionId.Value == "strike")
            .index;

        var lethal = afterEntry with
        {
            Combat = afterEntry.Combat with
            {
                Enemy = afterEntry.Combat.Enemy with { HP = 4 },
            },
        };

        return GameReducer.Reduce(lethal, new PlayCardAction(strikeIndex)).NewState;
    }
}
