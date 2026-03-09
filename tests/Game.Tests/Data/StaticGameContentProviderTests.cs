using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Data;

[Trait("Lane", "unit")]
public class StaticGameContentProviderTests
{
    [Fact]
    public void LoadDefault_ContainsExpectedCardDefinitions()
    {
        var content = StaticGameContentProvider.LoadDefault();

        Assert.True(content.CardDefinitions.Count >= 8);
        Assert.Equal("Strike", content.CardDefinitions[new CardId("strike")].Name);
        Assert.Equal("Guard", content.CardDefinitions[new CardId("guard")].Name);
        Assert.Equal("Quick Draw", content.CardDefinitions[new CardId("quick-draw")].Name);
        Assert.Equal("Heavy Attack", content.CardDefinitions[new CardId("heavy-attack")].Name);
        Assert.Equal("Feint", content.CardDefinitions[new CardId("feint")].Name);
    }

    [Fact]
    public void LoadDefault_ProvidesDeterministicOpeningCombatBlueprint()
    {
        var first = StaticGameContentProvider.LoadDefault();
        var second = StaticGameContentProvider.LoadDefault();

        AssertCombatBlueprintEquivalent(first.OpeningCombat, second.OpeningCombat);
        Assert.Equal(first.RewardCardPool, second.RewardCardPool);
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

    private static void AssertCombatBlueprintEquivalent(CombatBlueprint expected, CombatBlueprint actual)
    {
        AssertCombatantEquivalent(expected.Player, actual.Player);
        AssertCombatantEquivalent(expected.Enemy, actual.Enemy);
    }

    private static void AssertCombatantEquivalent(CombatantBlueprint expected, CombatantBlueprint actual)
    {
        Assert.Equal(expected.EntityId, actual.EntityId);
        Assert.Equal(expected.HP, actual.HP);
        Assert.Equal(expected.MaxHP, actual.MaxHP);
        Assert.Equal(expected.Armor, actual.Armor);
        Assert.Equal(expected.DrawPile, actual.DrawPile);

        var expectedResources = expected.Resources.OrderBy(kvp => kvp.Key).ToArray();
        var actualResources = actual.Resources.OrderBy(kvp => kvp.Key).ToArray();
        Assert.Equal(expectedResources, actualResources);
    }
}
