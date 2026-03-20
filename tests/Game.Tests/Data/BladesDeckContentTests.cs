using System.Text.Json;
using Game.Core.Cards;
using Game.Core.Decks;
using Game.Data.Content;

namespace Game.Tests.Data;

[Trait("Lane", "unit")]
public sealed class BladesDeckContentTests
{

    [Fact]
    public void BladesCardsJson_UsesArrayCostModelWithoutLegacyCostField()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Content", "blades.cards.json")));

        foreach (var card in document.RootElement.EnumerateArray())
        {
            Assert.False(card.TryGetProperty("cost", out _), $"Card '{card.GetProperty("id").GetString()}' still uses legacy cost property.");
            Assert.True(card.TryGetProperty("costs", out var costs), $"Card '{card.GetProperty("id").GetString()}' is missing costs array.");
            Assert.Equal(JsonValueKind.Array, costs.ValueKind);
            Assert.NotEmpty(costs.EnumerateArray());
        }
    }
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

    [Fact]
    public void BladesContent_UsesAgreedMomentumAndStatusImplementations()
    {
        var content = StaticGameContentProvider.LoadDefault().CardDefinitions;

        AssertCard(content[new CardId("blades-feint")], "Feint", "Common", "Utility", "Apply Weak 1. Gain 2 gm.",
            [new NoCost()],
            [
                new ApplyStatusCardEffect(StatusKind.Weak, 1, CardTarget.Opponent),
                new GainGeneratedMomentumCardEffect(2, CardTarget.Self)
            ]);

        AssertCard(content[new CardId("blades-kill-window")], "Kill Window", "Rare", "Utility", "Requires Momentum 2. Apply Vulnerable 1 + 2 per current Momentum to a single enemy.",
            [new RequireMomentumCost(2)],
            [new ApplyStatusPerCurrentMomentumCardEffect(StatusKind.Vulnerable, 1, 2, CardTarget.Opponent)]);

        AssertCard(content[new CardId("blades-storm-blades")], "Storm Blades", "Rare", "Attack", "Spend 2 Momentum. Deal 3 damage per current Momentum.",
            [new SpendMomentumCost(2)],
            [new DealDamagePerCurrentMomentumCardEffect(3, CardTarget.Opponent)]);

        AssertCard(content[new CardId("blades-rising-tempo")], "Rising Tempo", "Rare", "Utility", "Gain 3 gm. Your next attack this turn deals double damage.",
            [new NoCost()],
            [
                new GainGeneratedMomentumCardEffect(3, CardTarget.Self),
                new NextAttackDoubleDamageThisTurnCardEffect(CardTarget.Self)
            ]);

        AssertCard(content[new CardId("blades-relentless-assault")], "Relentless Assault", "Rare", "Attack", "Requires Momentum 2. Deal 4 damage three times. Apply Weak 1.",
            [new RequireMomentumCost(2)],
            [
                new DamageNTimesCardEffect(4, 3, CardTarget.Opponent),
                new ApplyStatusCardEffect(StatusKind.Weak, 1, CardTarget.Opponent)
            ]);

        AssertCard(content[new CardId("blades-bleeding-cut")], "Bleeding Cut", "Uncommon", "Attack", "Deal 5 damage. Apply Bleed 3 + 1 per current Momentum.",
            [new NoCost()],
            [
                new DamageCardEffect(5, CardTarget.Opponent),
                new ApplyStatusPerCurrentMomentumCardEffect(StatusKind.Bleed, 3, 1, CardTarget.Opponent)
            ]);

        Assert.Equal(new DealDamageToAllEnemiesCardEffect(5), Assert.Single(content[new CardId("blades-pressure-storm")].Effects));

        AssertCard(content[new CardId("blades-blood-rush")], "Blood Rush", "Rare", "Utility", "All attacks this turn gain +2 damage.",
            [new NoCost()],
            [new TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect(2, CardTarget.Self)]);
    }

    private static void AssertCard(
        CardDefinition actual,
        string expectedName,
        string expectedRarity,
        string expectedLabel,
        string expectedRulesText,
        IReadOnlyList<CardCost> expectedCosts,
        IReadOnlyList<CardEffect> expectedEffects)
    {
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal("Blades", actual.DeckAffinity);
        Assert.Equal(expectedRarity, actual.Rarity);
        Assert.Equal(expectedRulesText, actual.RulesText);
        Assert.Contains(expectedLabel, actual.LabelsOrEmpty);
        Assert.Equal(expectedCosts, actual.PlayCostsOrDefault);
        Assert.Equal(expectedEffects, actual.Effects);
    }
}
