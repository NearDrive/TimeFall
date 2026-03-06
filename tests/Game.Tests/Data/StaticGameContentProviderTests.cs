using Game.Core.Cards;
using Game.Core.Combat;

namespace Game.Tests.Data;

public class StaticGameContentProviderTests
{
    [Fact]
    public void LoadDefault_ContainsExpectedCardDefinitions()
    {
        var content = StaticGameContentProvider.LoadDefault();

        Assert.Equal(4, content.CardDefinitions.Count);
        Assert.Equal("Strike", content.CardDefinitions[new CardId("strike")].Name);
        Assert.Equal("Defend", content.CardDefinitions[new CardId("defend")].Name);
        Assert.Equal("Focus", content.CardDefinitions[new CardId("focus")].Name);
        Assert.Equal("Attack", content.CardDefinitions[new CardId("attack")].Name);
    }

    [Fact]
    public void LoadDefault_ProvidesDeterministicOpeningCombatBlueprint()
    {
        var first = StaticGameContentProvider.LoadDefault();
        var second = StaticGameContentProvider.LoadDefault();

        Assert.Equal(first.OpeningCombat, second.OpeningCombat);

        var firstDefinitions = first.CardDefinitions.OrderBy(kvp => kvp.Key.Value).ToArray();
        var secondDefinitions = second.CardDefinitions.OrderBy(kvp => kvp.Key.Value).ToArray();
        Assert.Equal(firstDefinitions, secondDefinitions);
    }

    [Fact]
    public void LoadDefault_OpeningCombatReferencesKnownCardsOnly()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var knownCards = content.CardDefinitions.Keys.ToHashSet();

        Assert.All(content.OpeningCombat.Player.DrawPile, cardId => Assert.Contains(cardId, knownCards));
        Assert.All(content.OpeningCombat.Enemy.DrawPile, cardId => Assert.Contains(cardId, knownCards));
        Assert.Equal(3, content.OpeningCombat.Player.Resources[ResourceType.Energy]);
    }
}
