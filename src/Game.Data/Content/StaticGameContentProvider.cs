using Game.Core.Content;

namespace Game.Data.Content;

public static class StaticGameContentProvider
{
    public static GameContentBundle LoadDefault()
    {
        return new GameContentBundle(
            CardDefinitions: PlaytestContent.CardDefinitions,
            RewardCardPool: PlaytestContent.RewardCardPool,
            OpeningCombat: PlaytestContent.OpeningCombat);
    }
}
