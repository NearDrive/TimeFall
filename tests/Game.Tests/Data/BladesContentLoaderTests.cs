using System.Text.Json;
using Game.Core.Cards;

namespace Game.Tests.Data;

[Trait("Lane", "unit")]
public sealed class BladesContentLoaderTests
{
    [Fact]
    public void LoadCards_ParsesCostsArrayAndExpandedEffects()
    {
        using var tempDir = new TempContentDirectory(
            """
            [
              {
                "id": "combo-card",
                "name": "Combo Card",
                "deckAffinity": "Blades",
                "rarity": "Rare",
                "labels": ["Attack", "Utility"],
                "costs": [
                  { "type": "RequireMomentum", "minimum": 3 },
                  { "type": "SpendMomentum", "amount": 2 }
                ],
                "rulesText": "",
                "effects": [
                  { "type": "DealDamage", "amount": 5, "target": "Opponent" },
                  { "type": "DealDamageIgnoringArmor", "amount": 7, "target": "Opponent" },
                  { "type": "DealDamageNTimes", "amount": 2, "times": 3, "target": "Opponent" },
                  { "type": "DealDamagePerMomentumSpent", "damagePerMomentum": 4, "target": "Opponent" },
                  { "type": "DealDamagePerAllMomentumSpent", "damagePerMomentum": 6, "target": "Opponent" },
                  { "type": "DealDamagePerCurrentMomentum", "damagePerMomentum": 2, "target": "Opponent" },
                  { "type": "DealDamageToAllEnemies", "amount": 3 },
                  { "type": "DrawCards", "amount": 2, "target": "Self" },
                  { "type": "GainArmor", "amount": 8, "target": "Self" },
                  { "type": "Heal", "amount": 4, "target": "Self" },
                  { "type": "ApplyBleed", "amount": 2, "target": "Opponent" },
                  { "type": "GainGeneratedMomentum", "amount": 5, "target": "Self" },
                  { "type": "ReflectNextEnemyAttackDamage", "amount": 6, "target": "Self" },
                  { "type": "AttackCountThisTurnToGm", "target": "Self" },
                  { "type": "RemoveEnemyArmor", "target": "Opponent" },
                  { "type": "RemoveAllArmor", "target": "Opponent" },
                  { "type": "NextAttackBonusDamageThisTurn", "amount": 3, "target": "Self" },
                  { "type": "NextAttackDoubleDamageThisTurn", "target": "Self" },
                  { "type": "AllAttacksBonusDamageThisTurn", "amount": 2, "target": "Self" },
                  { "type": "AllAttacksDoubleDamageThisTurn", "target": "Self" },
                  { "type": "LifestealPercentOfDamageDealt", "percent": 50, "target": "Self" },
                  { "type": "ApplyStatus", "status": "Weak", "amount": 1, "target": "Opponent" },
                  {
                    "type": "RepeatEffectsPerCurrentMomentum",
                    "target": "Self",
                    "effects": [
                      { "type": "GainGeneratedMomentum", "amount": 1, "target": "Self" },
                      { "type": "DrawCards", "amount": 1, "target": "Self" }
                    ]
                  }
                ]
              }
            ]
            """);

        var cards = BladesContentLoader.LoadCards(tempDir.Root);
        var card = Assert.Single(cards).Value;

        Assert.Collection(
            card.PlayCostsOrDefault,
            cost => Assert.Equal(new RequireMomentumCost(3), cost),
            cost => Assert.Equal(new SpendMomentumCost(2), cost));

        Assert.Collection(
            card.Effects,
            effect => Assert.Equal(new DamageCardEffect(5, CardTarget.Opponent), effect),
            effect => Assert.Equal(new DamageIgnoringArmorCardEffect(7, CardTarget.Opponent), effect),
            effect => Assert.Equal(new DamageNTimesCardEffect(2, 3, CardTarget.Opponent), effect),
            effect => Assert.Equal(new DealDamagePerMomentumSpentCardEffect(4, CardTarget.Opponent), effect),
            effect => Assert.Equal(new DealDamagePerAllMomentumSpentCardEffect(6, CardTarget.Opponent), effect),
            effect => Assert.Equal(new DealDamagePerCurrentMomentumCardEffect(2, CardTarget.Opponent), effect),
            effect => Assert.Equal(new DealDamageToAllEnemiesCardEffect(3), effect),
            effect => Assert.Equal(new DrawCardsCardEffect(2, CardTarget.Self), effect),
            effect => Assert.Equal(new GainArmorCardEffect(8, CardTarget.Self), effect),
            effect => Assert.Equal(new HealCardEffect(4, CardTarget.Self), effect),
            effect => Assert.Equal(new ApplyBleedCardEffect(2, CardTarget.Opponent), effect),
            effect => Assert.Equal(new GainGeneratedMomentumCardEffect(5, CardTarget.Self), effect),
            effect => Assert.Equal(new ReflectNextEnemyAttackDamageCardEffect(6, CardTarget.Self), effect),
            effect => Assert.Equal(new AttackCountThisTurnToGmCardEffect(CardTarget.Self), effect),
            effect => Assert.Equal(new RemoveEnemyArmorCardEffect(CardTarget.Opponent), effect),
            effect => Assert.Equal(new RemoveAllArmorCardEffect(CardTarget.Opponent), effect),
            effect => Assert.Equal(new NextAttackBonusDamageThisTurnCardEffect(3, CardTarget.Self), effect),
            effect => Assert.Equal(new NextAttackDoubleDamageThisTurnCardEffect(CardTarget.Self), effect),
            effect => Assert.Equal(new AllAttacksBonusDamageThisTurnCardEffect(2, CardTarget.Self), effect),
            effect => Assert.Equal(new AllAttacksDoubleDamageThisTurnCardEffect(CardTarget.Self), effect),
            effect => Assert.Equal(new LifestealPercentOfDamageDealtCardEffect(50, CardTarget.Self), effect),
            effect => Assert.Equal(new ApplyStatusCardEffect(StatusKind.Weak, 1, CardTarget.Opponent), effect),
            effect => Assert.Equal(
                new RepeatEffectsPerCurrentMomentumCardEffect(
                    [
                        new GainGeneratedMomentumCardEffect(1, CardTarget.Self),
                        new DrawCardsCardEffect(1, CardTarget.Self),
                    ],
                    CardTarget.Self),
                effect));
    }

    [Fact]
    public void LoadCards_SupportsLegacySingleCostProperty()
    {
        using var tempDir = new TempContentDirectory(
            """
            [
              {
                "id": "legacy-card",
                "name": "Legacy Card",
                "deckAffinity": "Blades",
                "rarity": "Common",
                "labels": ["Attack"],
                "cost": { "type": "SpendUpToMomentum", "max": 4 },
                "rulesText": "",
                "effects": [
                  { "type": "DealDamage", "amount": 3, "target": "Opponent" }
                ]
              }
            ]
            """);

        var cards = BladesContentLoader.LoadCards(tempDir.Root);
        var card = Assert.Single(cards).Value;

        Assert.Collection(card.PlayCostsOrDefault, cost => Assert.Equal(new SpendUpToMomentumCost(4), cost));
    }

    [Fact]
    public void LoadCards_SupportsLegacyEnemyTargetAlias()
    {
        using var tempDir = new TempContentDirectory(
            """
            [
              {
                "id": "enemy-target-card",
                "name": "Enemy Target Card",
                "deckAffinity": "Blades",
                "rarity": "Common",
                "labels": ["Attack"],
                "costs": [
                  { "type": "None" }
                ],
                "rulesText": "",
                "effects": [
                  { "type": "DealDamage", "amount": 3, "target": "Enemy" }
                ]
              }
            ]
            """);

        var cards = BladesContentLoader.LoadCards(tempDir.Root);
        var card = Assert.Single(cards).Value;

        Assert.Collection(card.Effects, effect => Assert.Equal(new DamageCardEffect(3, CardTarget.Opponent), effect));
    }

    [Fact]
    public void LoadCards_InvalidOrIncompleteJsonThrowsJsonException()
    {
        using var missingAmountDir = new TempContentDirectory(
            """
            [
              {
                "id": "broken-card",
                "name": "Broken Card",
                "deckAffinity": "Blades",
                "rarity": "Rare",
                "labels": ["Attack"],
                "costs": [
                  { "type": "RequireMomentum", "minimum": 2 }
                ],
                "rulesText": "",
                "effects": [
                  { "type": "DealDamage", "target": "Opponent" }
                ]
              }
            ]
            """);

        var missingAmount = Assert.Throws<JsonException>(() => BladesContentLoader.LoadCards(missingAmountDir.Root));
        Assert.Contains("amount", missingAmount.Message, StringComparison.OrdinalIgnoreCase);

        using var unknownEffectDir = new TempContentDirectory(
            """
            [
              {
                "id": "unknown-card",
                "name": "Unknown Card",
                "deckAffinity": "Blades",
                "rarity": "Rare",
                "labels": ["Utility"],
                "costs": [
                  { "type": "None" }
                ],
                "rulesText": "",
                "effects": [
                  { "type": "NotARealEffect", "target": "Self" }
                ]
              }
            ]
            """);

        var unknownEffect = Assert.Throws<JsonException>(() => BladesContentLoader.LoadCards(unknownEffectDir.Root));
        Assert.Contains("Unsupported card effect type", unknownEffect.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempContentDirectory : IDisposable
    {
        public TempContentDirectory(string cardJson)
        {
            Root = Path.Combine(Path.GetTempPath(), $"timefall-blades-loader-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "blades.cards.json"), cardJson);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
