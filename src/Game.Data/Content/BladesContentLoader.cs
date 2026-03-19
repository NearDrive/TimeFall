using System.Text.Json;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;

namespace Game.Data.Content;

internal static class BladesContentLoader
{
    public static IReadOnlyDictionary<CardId, CardDefinition> LoadCards(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, "blades.cards.json"));
        var cards = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
        var result = new Dictionary<CardId, CardDefinition>();
        foreach (var card in cards)
        {
            var id = new CardId(GetRequiredString(card, "id"));
            var name = GetRequiredString(card, "name");
            var rarity = GetRequiredString(card, "rarity");
            var deckAffinity = GetRequiredString(card, "deckAffinity");
            var rulesText = GetRequiredString(card, "rulesText");
            var labels = card.GetProperty("labels").EnumerateArray().Select(x => x.GetString() ?? throw new JsonException("Card labels must be strings.")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var costs = ParseCosts(card);
            var effects = card.GetProperty("effects").EnumerateArray().Select(ParseEffect).ToArray();
            result.Add(id, new CardDefinition(id, name, 0, effects, costs, labels, deckAffinity, rarity, rulesText));
        }

        return result;
    }

    public static RunDeckDefinition LoadDeck(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, "blades.deck.json"));
        var deck = JsonSerializer.Deserialize<JsonElement>(json);

        var id = deck.GetProperty("id").GetString()!;
        var name = deck.GetProperty("name").GetString()!;
        var resourceType = Enum.Parse<ResourceType>(deck.GetProperty("resourceType").GetString()!, ignoreCase: true);
        var baseMaxHp = deck.GetProperty("baseMaxHp").GetInt32();
        var startingDeck = deck.GetProperty("startingDeck").EnumerateArray().Select(x => new CardId(x.GetString()!)).ToArray();
        var rewardPool = deck.GetProperty("cardPool").EnumerateObject()
            .SelectMany(bucket => bucket.Value.EnumerateArray())
            .Select(x => new CardId(x.GetString()!))
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();

        var startingResources = new Dictionary<ResourceType, int>
        {
            [resourceType] = resourceType == ResourceType.Momentum ? 0 : 3,
        };

        return new RunDeckDefinition(
            Id: id,
            Name: name,
            Description: null,
            ResourceType: resourceType,
            BaseMaxHp: baseMaxHp,
            StartingCombatDeckCardIds: startingDeck,
            RewardPoolCardIds: rewardPool,
            StartingResources: startingResources);
    }

    private static IReadOnlyList<CardCost> ParseCosts(JsonElement card)
    {
        if (card.TryGetProperty("costs", out var costsElement))
        {
            if (costsElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Card property 'costs' must be an array.");
            }

            return costsElement.EnumerateArray().Select(ParseCost).ToArray();
        }

        if (card.TryGetProperty("cost", out var costElement))
        {
            return costElement.ValueKind switch
            {
                JsonValueKind.Array => costElement.EnumerateArray().Select(ParseCost).ToArray(),
                JsonValueKind.Object => [ParseCost(costElement)],
                _ => throw new JsonException("Card property 'cost' must be an object or array."),
            };
        }

        return [new NoCost()];
    }

    private static CardCost ParseCost(JsonElement e)
    {
        var t = GetRequiredString(e, "type");
        return t switch
        {
            "None" => new NoCost(),
            "RequireMomentum" => new RequireMomentumCost(GetRequiredInt32(e, "minimum")),
            "SpendMomentum" => new SpendMomentumCost(GetRequiredInt32(e, "amount")),
            "SpendAllMomentum" => new SpendAllMomentumCost(),
            "SpendUpToMomentum" => new SpendUpToMomentumCost(GetRequiredInt32(e, "max")),
            _ => throw new JsonException($"Unsupported card cost type '{t}'."),
        };
    }

    private static CardEffect ParseEffect(JsonElement e)
    {
        var t = GetRequiredString(e, "type");
        var target = ParseTarget(e);
        return t switch
        {
            "DealDamage" => new DamageCardEffect(GetRequiredInt32(e, "amount"), target),
            "DealDamageIgnoringArmor" => new DamageIgnoringArmorCardEffect(GetRequiredInt32(e, "amount"), target),
            "DealDamageNTimes" => new DamageNTimesCardEffect(GetRequiredInt32(e, "amount"), GetRequiredInt32(e, "times"), target),
            "DealDamagePerMomentumSpent" => new DealDamagePerMomentumSpentCardEffect(GetRequiredInt32(e, "damagePerMomentum"), target),
            "DealDamagePerAllMomentumSpent" => new DealDamagePerAllMomentumSpentCardEffect(GetRequiredInt32(e, "damagePerMomentum"), target),
            "DealDamagePerCurrentMomentum" => new DealDamagePerCurrentMomentumCardEffect(GetRequiredInt32(e, "damagePerMomentum"), target),
            "DealDamageToAllEnemies" => new DealDamageToAllEnemiesCardEffect(GetRequiredInt32(e, "amount")),
            "DealDamageAndDrawPerCurrentMomentum" => new DealDamageAndDrawPerCurrentMomentumCardEffect(GetRequiredInt32(e, "damagePerMomentum"), GetRequiredInt32(e, "drawPerMomentum"), target),
            "DamageWithAttackCountScaling" => new DamageWithAttackCountScalingCardEffect(GetRequiredInt32(e, "baseAmount"), GetRequiredInt32(e, "damagePerAttackPlayedThisTurn"), target),
            "GainArmor" => new GainArmorCardEffect(GetRequiredInt32(e, "amount"), target),
            "DrawCards" => new DrawCardsCardEffect(GetRequiredInt32(e, "amount"), target),
            "Heal" => new HealCardEffect(GetRequiredInt32(e, "amount"), target),
            "ApplyStatus" => new ApplyStatusCardEffect(ParseStatus(e), GetRequiredInt32(e, "amount"), target),
            "ApplyBleed" => new ApplyBleedCardEffect(GetRequiredInt32(e, "amount"), target),
            "ApplyStatusPerCurrentMomentum" => new ApplyStatusPerCurrentMomentumCardEffect(ParseStatus(e), GetRequiredInt32(e, "baseAmount"), GetRequiredInt32(e, "amountPerCurrentMomentum"), target),
            "GainGeneratedMomentum" => new GainGeneratedMomentumCardEffect(GetRequiredInt32(e, "amount"), target),
            "ReflectNextEnemyAttackDamage" => new ReflectNextEnemyAttackDamageCardEffect(GetRequiredInt32(e, "amount"), target),
            "AttackCountThisTurnToGm" => new AttackCountThisTurnToGmCardEffect(target),
            "RemoveEnemyArmor" => new RemoveEnemyArmorCardEffect(target),
            "RemoveAllArmor" => new RemoveAllArmorCardEffect(target),
            "NextAttackBonusDamageThisTurn" => new NextAttackBonusDamageThisTurnCardEffect(GetRequiredInt32(e, "amount"), target),
            "NextAttackDoubleThisTurn" => new NextAttackDoubleThisTurnCardEffect(target),
            "NextAttackDoubleDamageThisTurn" => new NextAttackDoubleDamageThisTurnCardEffect(target),
            "AllAttacksBonusDamageThisTurn" => new AllAttacksBonusDamageThisTurnCardEffect(GetRequiredInt32(e, "amount"), target),
            "TemporaryBuffAllAttacksPlusDamageThisTurn" => new TemporaryBuffAllAttacksPlusDamageThisTurnCardEffect(GetRequiredInt32(e, "amount"), target),
            "AllAttacksDoubleDamageThisTurn" => new AllAttacksDoubleDamageThisTurnCardEffect(target),
            "TemporaryBuffAllAttacksDoubleDamageThisTurn" => new TemporaryBuffAllAttacksDoubleDamageThisTurnCardEffect(target),
            "ConditionalGainArmorIfMomentumAtLeast" => new ConditionalGainArmorIfMomentumAtLeastCardEffect(GetRequiredInt32(e, "minimumMomentum"), GetRequiredInt32(e, "amount"), target),
            "LifestealPercentOfDamageDealt" => new LifestealPercentOfDamageDealtCardEffect(GetRequiredInt32(e, "percent"), target),
            "RepeatEffectsPerCurrentMomentum" => new RepeatEffectsPerCurrentMomentumCardEffect(ParseNestedEffects(e), target),
            _ => throw new JsonException($"Unsupported card effect type '{t}'."),
        };
    }

    private static IReadOnlyList<CardEffect> ParseNestedEffects(JsonElement effect)
    {
        if (!effect.TryGetProperty("effects", out var nestedEffects) || nestedEffects.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Composite card effects must contain an 'effects' array.");
        }

        return nestedEffects.EnumerateArray().Select(ParseEffect).ToArray();
    }

    private static CardTarget ParseTarget(JsonElement e)
    {
        if (!e.TryGetProperty("target", out var targetElement))
        {
            return CardTarget.Opponent;
        }

        return GetRequiredString(targetElement) switch
        {
            "Self" => CardTarget.Self,
            "Opponent" => CardTarget.Opponent,
            var target => throw new JsonException($"Unsupported card target '{target}'."),
        };
    }

    private static StatusKind ParseStatus(JsonElement e)
        => Enum.Parse<StatusKind>(GetRequiredString(e, "status"), ignoreCase: true);

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Missing required property '{propertyName}'.");
        }

        return GetRequiredString(property);
    }

    private static string GetRequiredString(JsonElement element)
        => element.GetString() ?? throw new JsonException("Expected a non-null string value.");

    private static int GetRequiredInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Missing required property '{propertyName}'.");
        }

        return property.GetInt32();
    }
}
