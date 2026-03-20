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

    [Fact]
    public void BladesContent_UsesAgreedMomentumAndStatusImplementations()
    {
        var content = StaticGameContentProvider.LoadDefault().CardDefinitions;

        Assert.Equal(
            new CardDefinition(
                new CardId("blades-feint"),
                "Feint",
                0,
                [
                    new ApplyStatusCardEffect(StatusKind.Weak, 1, CardTarget.Opponent),
                    new GainGeneratedMomentumCardEffect(2, CardTarget.Self)
                ],
                new NoCost(),
                new HashSet<string> { "Utility" },
                RulesText: "Apply Weak 1. Gain 2 gm."),
            content[new CardId("blades-feint")]);

        Assert.Equal(
            new CardDefinition(
                new CardId("blades-kill-window"),
                "Kill Window",
                0,
                [new ApplyStatusPerCurrentMomentumCardEffect(StatusKind.Vulnerable, 1, 2, CardTarget.Opponent)],
                new RequireMomentumCost(2),
                new HashSet<string> { "Utility" },
                RulesText: "Requires Momentum 2. Apply Vulnerable 1 + 2 per current Momentum to a single enemy."),
            content[new CardId("blades-kill-window")]);

        Assert.Equal(
            new CardDefinition(
                new CardId("blades-storm-blades"),
                "Storm Blades",
                0,
                [new DealDamagePerCurrentMomentumCardEffect(3, CardTarget.Opponent)],
                new SpendMomentumCost(2),
                new HashSet<string> { "Attack" },
                RulesText: "Spend 2 Momentum. Deal 3 damage per current Momentum."),
            content[new CardId("blades-storm-blades")]);

        Assert.Equal(
            new CardDefinition(
                new CardId("blades-rising-tempo"),
                "Rising Tempo",
                0,
                [
                    new GainGeneratedMomentumCardEffect(3, CardTarget.Self),
                    new NextAttackDoubleDamageThisTurnCardEffect(CardTarget.Self)
                ],
                new NoCost(),
                new HashSet<string> { "Utility" },
                RulesText: "Gain 3 gm. Your next attack this turn deals double damage."),
            content[new CardId("blades-rising-tempo")]);

        Assert.Equal(
            new CardDefinition(
                new CardId("blades-relentless-assault"),
                "Relentless Assault",
                0,
                [
                    new DamageNTimesCardEffect(4, 3, CardTarget.Opponent),
                    new ApplyStatusCardEffect(StatusKind.Weak, 1, CardTarget.Opponent)
                ],
                new RequireMomentumCost(2),
                new HashSet<string> { "Attack" },
                RulesText: "Requires Momentum 2. Deal 4 damage three times. Apply Weak 1."),
            content[new CardId("blades-relentless-assault")]);

        Assert.Equal(
            new CardDefinition(
                new CardId("blades-bleeding-cut"),
                "Bleeding Cut",
                0,
                [
                    new DamageCardEffect(5, CardTarget.Opponent),
                    new ApplyStatusPerCurrentMomentumCardEffect(StatusKind.Bleed, 1, 1, CardTarget.Opponent)
                ],
                new NoCost(),
                new HashSet<string> { "Attack" },
                RulesText: "Deal 5 damage. Apply Bleed 1 + 1 per current Momentum."),
            content[new CardId("blades-bleeding-cut")]);

        Assert.Equal(new DealDamageToAllEnemiesCardEffect(5), Assert.Single(content[new CardId("blades-pressure-storm")].Effects));
    }
}
