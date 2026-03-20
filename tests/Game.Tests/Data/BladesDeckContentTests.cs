using Game.Core.Cards;
using Game.Core.Decks;
using Game.Data.Content;

namespace Game.Tests.Data;

[Trait("Lane", "unit")]
public sealed class BladesDeckContentTests
{
    [Fact]
    public void BladesRewardPool_ResolvesEveryConfiguredCardId()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var deck = content.DeckDefinitions["deck-blades"];

        Assert.NotEmpty(deck.RewardPoolCardIds);
        Assert.All(deck.RewardPoolCardIds, cardId => Assert.True(content.CardDefinitions.ContainsKey(cardId), $"Missing card definition for '{cardId.Value}'."));
        Assert.Equal(deck.RewardPoolCardIds.Count, deck.RewardPoolCardIds.Distinct().Count());
    }

    [Fact]
    public void BladesRewardPool_SupportsEditRulesBoundaries()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var deck = content.DeckDefinitions["deck-blades"];

        Assert.True(deck.RewardPoolCardIds.Count > RewardPoolRules.MaxEnabledCards);
        Assert.True(RewardPoolRules.TryValidate(deck.RewardPoolCardIds.Take(RewardPoolRules.MinEnabledCards).ToArray(), deck.RewardPoolCardIds.Count, out _));
        Assert.False(RewardPoolRules.TryValidate(deck.RewardPoolCardIds.Take(RewardPoolRules.MinEnabledCards - 1).ToArray(), deck.RewardPoolCardIds.Count, out _));
        Assert.False(RewardPoolRules.TryValidate(deck.RewardPoolCardIds.Take(RewardPoolRules.MaxEnabledCards + 1).ToArray(), deck.RewardPoolCardIds.Count, out _));
    }

    [Fact]
    public void BladesStartingDeck_UsesFeintInsteadOfFocus()
    {
        var content = StaticGameContentProvider.LoadDefault();
        var deck = content.DeckDefinitions["deck-blades"];
        var grouped = deck.StartingCombatDeckCardIds.GroupBy(id => id.Value).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(8, deck.StartingCombatDeckCardIds.Count);
        Assert.Equal(5, grouped["blades-strike"]);
        Assert.Equal(2, grouped["blades-guard"]);
        Assert.Equal(1, grouped["blades-feint"]);
        Assert.DoesNotContain(deck.StartingCombatDeckCardIds, id => id == new CardId("blades-focus"));
    }
}
