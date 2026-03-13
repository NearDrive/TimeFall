using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Game;

namespace Game.Tests.Game;

public sealed class RewardPoolEditReducerTests
{
    [Fact]
    public void CannotEnterEditDeckWithoutSelectedDeck()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;

        var edited = GameReducer.Reduce(newRun, new OpenDeckEditAction()).NewState;

        Assert.Equal(GamePhase.NewRunMenu, edited.Phase);
    }

    [Fact]
    public void BladesStartingDeck_IsExactPredefinedComposition()
    {
        var started = GameStateTestFactory.CreateStartedRun();
        var grouped = started.RunDeck.GroupBy(card => card.DefinitionId.Value).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(8, started.RunDeck.Count);
        Assert.Equal(5, grouped["blades-strike"]);
        Assert.Equal(2, grouped["blades-guard"]);
        Assert.Equal(1, grouped["blades-focus"]);
        Assert.Equal(3, grouped.Count);
    }

    [Fact]
    public void EnabledRewardPool_StaysUniqueWhenEnablingDuplicate()
    {
        var state = EnterDeckEdit();
        var deck = state.DeckDefinitions[state.SelectedDeckId!];
        var cardId = deck.RewardPoolCardIds[0];

        var once = GameReducer.Reduce(state, new EnableRewardPoolCardAction(cardId)).NewState;
        var twice = GameReducer.Reduce(once, new EnableRewardPoolCardAction(cardId)).NewState;

        var occurrences = twice.RewardPoolEdit!.WorkingEnabledCardIds.Count(id => id == cardId);
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void ConfirmRejectsBelow20WhenCompatiblePoolAtLeast20()
    {
        var state = EnterDeckEdit();
        var reduced = GameReducer.Reduce(state, new DisableAllRewardPoolCardsAction()).NewState;

        var (afterConfirm, events) = GameReducer.Reduce(reduced, new ConfirmRewardPoolAction());

        Assert.Equal(GamePhase.DeckEdit, afterConfirm.Phase);
        Assert.Contains(events, e => e is RewardPoolEditRejected);
    }

    [Fact]
    public void ConfirmAllowsBelow20WhenCompatiblePoolBelow20()
    {
        var state = EnterDeckEditWithSmallPool(totalPoolCards: 10, enabledCards: 5);

        var (afterConfirm, events) = GameReducer.Reduce(state, new ConfirmRewardPoolAction());

        Assert.Equal(GamePhase.NewRunMenu, afterConfirm.Phase);
        Assert.Equal(5, afterConfirm.EnabledRewardPoolCardIds.Count);
        Assert.Contains(events, e => e is RewardPoolEditConfirmed);
    }

    [Fact]
    public void ConfirmRejectsAbove30EnabledCards()
    {
        var state = EnterDeckEdit();
        var allEnabled = GameReducer.Reduce(state, new EnableAllRewardPoolCardsAction()).NewState;

        var (afterConfirm, events) = GameReducer.Reduce(allEnabled, new ConfirmRewardPoolAction());

        Assert.Equal(GamePhase.DeckEdit, afterConfirm.Phase);
        Assert.Contains(events, e => e is RewardPoolEditRejected);
    }

    [Fact]
    public void EditDeckOnlyChangesRewardPool_NotStartingCombatDeck()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;
        var deckSelect = GameReducer.Reduce(newRun, new OpenDeckSelectAction()).NewState;
        var selected = GameReducer.Reduce(deckSelect, new SelectDeckAction("deck-blades")).NewState;
        var newRunWithDeck = GameReducer.Reduce(selected, new ReturnToNewRunMenuAction()).NewState;

        var editing = GameReducer.Reduce(newRunWithDeck, new OpenDeckEditAction()).NewState;
        var disabledAll = GameReducer.Reduce(editing, new DisableAllRewardPoolCardsAction()).NewState;
        var autofilledMin = GameReducer.Reduce(disabledAll, new AutofillMinRewardPoolAction()).NewState;
        var confirmed = GameReducer.Reduce(autofilledMin, new ConfirmRewardPoolAction()).NewState;
        var started = GameReducer.Reduce(confirmed, new StartRunAction(42)).NewState;

        Assert.Equal(20, confirmed.EnabledRewardPoolCardIds.Count);
        Assert.Equal(8, started.RunDeck.Count);
        Assert.Equal(5, started.RunDeck.Count(c => c.DefinitionId.Value == "blades-strike"));
        Assert.Equal(2, started.RunDeck.Count(c => c.DefinitionId.Value == "blades-guard"));
        Assert.Equal(1, started.RunDeck.Count(c => c.DefinitionId.Value == "blades-focus"));
    }

    [Fact]
    public void AutofillMinAndMax_AreDeterministic()
    {
        var firstMin = GameReducer.Reduce(GameReducer.Reduce(EnterDeckEdit(), new DisableAllRewardPoolCardsAction()).NewState, new AutofillMinRewardPoolAction()).NewState;
        var secondMin = GameReducer.Reduce(GameReducer.Reduce(EnterDeckEdit(), new DisableAllRewardPoolCardsAction()).NewState, new AutofillMinRewardPoolAction()).NewState;
        Assert.Equal(firstMin.RewardPoolEdit!.WorkingEnabledCardIds, secondMin.RewardPoolEdit!.WorkingEnabledCardIds);

        var firstMax = GameReducer.Reduce(EnterDeckEdit(), new AutofillMaxRewardPoolAction()).NewState;
        var secondMax = GameReducer.Reduce(EnterDeckEdit(), new AutofillMaxRewardPoolAction()).NewState;
        Assert.Equal(firstMax.RewardPoolEdit!.WorkingEnabledCardIds, secondMax.RewardPoolEdit!.WorkingEnabledCardIds);
    }

    private static GameState EnterDeckEdit()
    {
        var initial = GameStateTestFactory.CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;
        var deckSelect = GameReducer.Reduce(newRun, new OpenDeckSelectAction()).NewState;
        var selected = GameReducer.Reduce(deckSelect, new SelectDeckAction("deck-blades")).NewState;
        var back = GameReducer.Reduce(selected, new ReturnToNewRunMenuAction()).NewState;
        return GameReducer.Reduce(back, new OpenDeckEditAction()).NewState;
    }

    private static GameState EnterDeckEditWithSmallPool(int totalPoolCards, int enabledCards)
    {
        var baseState = EnterDeckEdit();
        var deck = baseState.DeckDefinitions["deck-blades"];
        var smallPool = deck.RewardPoolCardIds.Take(totalPoolCards).ToImmutableList();
        var modifiedDeck = deck with { RewardPoolCardIds = smallPool };
        var modifiedState = baseState with
        {
            DeckDefinitions = baseState.DeckDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Key == "deck-blades" ? modifiedDeck : kvp.Value),
            RewardPoolEdit = new Game.Core.Decks.RewardPoolEditState(smallPool.Take(enabledCards).ToImmutableList()),
            EnabledRewardPoolCardIds = smallPool.Take(enabledCards).ToImmutableList(),
        };

        return modifiedState;
    }
}
