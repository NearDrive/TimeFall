using Game.Core.Game;
using Game.Data.Content;

namespace Game.Tests;

internal static class GameStateTestFactory
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    public static GameState CreateInitialWithContent()
    {
        return GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool);
    }

    public static GameState CreateStartedRun(int seed = 1337)
    {
        var initial = CreateInitialWithContent();
        var selected = GameReducer.Reduce(initial, new SelectDeckAction(initial.AvailableDeckIds[0])).NewState;
        return GameReducer.Reduce(selected, new StartRunAction(seed)).NewState;
    }
}
