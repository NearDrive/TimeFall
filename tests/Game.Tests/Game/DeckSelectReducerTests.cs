using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Data.Content;

namespace Game.Tests.Game;

public sealed class DeckSelectReducerTests
{
    [Fact]
    public void AvailableDecks_AreListedFromContent()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var state = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);

        Assert.Contains("deck-blades", state.AvailableDeckIds);
    }

    [Fact]
    public void SelectDeckAction_SelectsValidDeck()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var state = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);

        var (newState, events) = GameReducer.Reduce(state, new SelectDeckAction("deck-blades"));

        Assert.Equal("deck-blades", newState.SelectedDeckId);
        Assert.Contains(events, e => e is DeckSelected { DeckId: "deck-blades" });
    }

    [Fact]
    public void SelectDeckAction_RejectsUnknownDeck()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var state = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);

        var (newState, events) = GameReducer.Reduce(state, new SelectDeckAction("unknown"));

        Assert.Same(state, newState);
        Assert.Empty(events);
    }

    [Fact]
    public void SelectDeckAction_RejectedOutsideDeckSelectPhase()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var state = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable) with { Phase = GamePhase.MapExploration };

        var (newState, events) = GameReducer.Reduce(state, new SelectDeckAction("deck-blades"));

        Assert.Same(state, newState);
        Assert.Empty(events);
    }

    [Fact]
    public void StartRun_RequiresSelectedDeck()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var state = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);

        var (newState, events) = GameReducer.Reduce(state, new StartRunAction(123));

        Assert.Same(state, newState);
        Assert.Empty(events);
    }

    [Fact]
    public void StartRun_UsesSelectedDeckDefinition()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var initial = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);
        var (selected, _) = GameReducer.Reduce(initial, new SelectDeckAction("deck-blades"));
        var (started, _) = GameReducer.Reduce(selected, new StartRunAction(123));
        var (combatState, _) = GameReducer.Reduce(started, new BeginCombatAction(content.OpeningCombat, content.CardDefinitions, content.RewardCardPool));

        Assert.Equal(120, started.RunMaxHp);
        Assert.Equal(120, started.RunHp);
        Assert.Equal(content.DeckDefinitions["deck-blades"].StartingDeck.Count, started.RunDeck.Count);
        Assert.All(started.RunDeck.Select(c => c.DefinitionId), id => Assert.Contains(id, content.DeckDefinitions["deck-blades"].StartingDeck));
        Assert.Equal(ResourceType.Momentum, content.DeckDefinitions["deck-blades"].ResourceType);
        Assert.NotNull(combatState.Combat);
        Assert.Equal(0, combatState.Combat!.Player.Resources.GetValueOrDefault(ResourceType.Momentum));
    }
}
