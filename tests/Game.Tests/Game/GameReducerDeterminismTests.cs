using Game.Core.Game;

namespace Game.Tests.Game;

public class GameReducerDeterminismTests
{
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

        Assert.Equal(first.NewState, second.NewState);
        Assert.Equal(first.Events, second.Events);
    }
}
