using Game.Core.Game;
using Game.Data.Content;

namespace Game.Tests;

internal static class GameStateTestFactory
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    public static GameState CreateInitialWithContent()
    {
        return GameState.CreateInitial(Content.CardDefinitions, Content.DeckDefinitions, Content.RewardCardPool, Content.EnemyDefinitions, Content.Zone1SpawnTable);
    }

    public static GameState CreateStartedRun(int seed = 1337)
    {
        var initial = CreateInitialWithContent();
        var newRun = GameReducer.Reduce(initial, new EnterNewRunMenuAction()).NewState;
        var deckSelect = GameReducer.Reduce(newRun, new OpenDeckSelectAction()).NewState;
        var selected = GameReducer.Reduce(deckSelect, new SelectDeckAction(deckSelect.AvailableDeckIds[0])).NewState;
        var returned = GameReducer.Reduce(selected, new ReturnToNewRunMenuAction()).NewState;
        return GameReducer.Reduce(returned, new StartRunAction(seed)).NewState;
    }
}
