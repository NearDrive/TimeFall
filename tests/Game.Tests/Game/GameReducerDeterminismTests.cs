using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Game;

[ReplayLane]
public class GameReducerDeterminismTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void StartRunAction_UsesProvidedSeedInRng()
    {
        var initialState = GameState.Initial;

        var (newState, events) = GameReducer.Reduce(initialState, new StartRunAction(1337));

        Assert.Equal(1337, newState.Rng.Seed);
        var runStarted = Assert.Single(events);
        Assert.Equal(new RunStarted(1337), runStarted);
    }

    [Fact]
    public void Reduce_IsDeterministic_ForSameInitialStateAndAction()
    {
        var initialState = GameState.Initial;
        var action = new StartRunAction(42);

        var first = GameReducer.Reduce(initialState, action);
        var second = GameReducer.Reduce(initialState, action);

        AssertGameStateEquivalent(first.NewState, second.NewState);
        Assert.Equal(first.Events, second.Events);
    }

    [Fact]
    public void BeginCombat_IsDeterministic_ForSameSeedAndLoadedContent()
    {
        var (runA, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(2024));
        var (runB, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(2024));

        var first = GameReducer.Reduce(runA, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));
        var second = GameReducer.Reduce(runB, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        AssertGameStateEquivalent(first.NewState, second.NewState);
        Assert.Equal(first.Events, second.Events);
    }

    private static void AssertGameStateEquivalent(GameState expected, GameState actual)
    {
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.Rng, actual.Rng);
        Assert.Equal(expected.CardDefinitions.Count, actual.CardDefinitions.Count);

        foreach (var expectedDefinition in expected.CardDefinitions)
        {
            Assert.True(actual.CardDefinitions.TryGetValue(expectedDefinition.Key, out var actualDefinition));
            Assert.Equal(expectedDefinition.Value.Id, actualDefinition!.Id);
            Assert.Equal(expectedDefinition.Value.Name, actualDefinition.Name);
            Assert.Equal(expectedDefinition.Value.Cost, actualDefinition.Cost);
            Assert.Equal(expectedDefinition.Value.Effects, actualDefinition.Effects);
        }

        if (expected.Combat is null)
        {
            Assert.Null(actual.Combat);
            return;
        }

        Assert.NotNull(actual.Combat);
        AssertCombatStateEquivalent(expected.Combat, actual.Combat!);
    }

    private static void AssertCombatStateEquivalent(CombatState expected, CombatState actual)
    {
        Assert.Equal(expected.TurnOwner, actual.TurnOwner);
        Assert.Equal(expected.ReshuffleCount, actual.ReshuffleCount);
        Assert.Equal(expected.NeedsOverflowDiscard, actual.NeedsOverflowDiscard);
        Assert.Equal(expected.RequiredOverflowDiscardCount, actual.RequiredOverflowDiscardCount);

        AssertCombatEntityEquivalent(expected.Player, actual.Player);
        AssertCombatEntityEquivalent(expected.Enemy, actual.Enemy);
    }

    private static void AssertCombatEntityEquivalent(CombatEntity expected, CombatEntity actual)
    {
        Assert.Equal(expected.EntityId, actual.EntityId);
        Assert.Equal(expected.HP, actual.HP);
        Assert.Equal(expected.MaxHP, actual.MaxHP);
        Assert.Equal(expected.Armor, actual.Armor);

        var expectedResources = expected.Resources.OrderBy(kvp => kvp.Key).ToArray();
        var actualResources = actual.Resources.OrderBy(kvp => kvp.Key).ToArray();
        Assert.Equal(expectedResources, actualResources);

        Assert.Equal(expected.Deck.DrawPile, actual.Deck.DrawPile);
        Assert.Equal(expected.Deck.Hand, actual.Deck.Hand);
        Assert.Equal(expected.Deck.DiscardPile, actual.Deck.DiscardPile);
        Assert.Equal(expected.Deck.BurnPile, actual.Deck.BurnPile);
    }
}
