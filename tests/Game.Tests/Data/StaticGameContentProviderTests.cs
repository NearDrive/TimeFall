using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Tests.Data;

public class StaticGameContentProviderTests
{
    [Fact]
    public void LoadDefault_ContainsExpectedCardDefinitions()
    {
        var content = StaticGameContentProvider.LoadDefault();

        Assert.Equal(4, content.CardDefinitions.Count);
        Assert.Equal("Strike", content.CardDefinitions[new CardId("strike")].Name);
        Assert.Contains(content.CardDefinitions[new CardId("strike")].Effects, e => e is DamageCardEffect { Amount: 4, Target: CardTarget.Opponent });
        Assert.Equal("Defend", content.CardDefinitions[new CardId("defend")].Name);
        Assert.Contains(content.CardDefinitions[new CardId("defend")].Effects, e => e is GainArmorCardEffect { Amount: 3, Target: CardTarget.Self });
        Assert.Equal("Focus", content.CardDefinitions[new CardId("focus")].Name);
        Assert.Equal("Attack", content.CardDefinitions[new CardId("attack")].Name);
    }

    [Fact]
    public void LoadDefault_ProvidesDeterministicOpeningCombatBlueprint()
    {
        var first = StaticGameContentProvider.LoadDefault();
        var second = StaticGameContentProvider.LoadDefault();

        AssertCombatBlueprintEquivalent(first.OpeningCombat, second.OpeningCombat);

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
