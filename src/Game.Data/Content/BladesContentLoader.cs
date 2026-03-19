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
            var id = new CardId(card.GetProperty("id").GetString()!);
            var name = card.GetProperty("name").GetString()!;
            var rarity = card.GetProperty("rarity").GetString()!;
            var deckAffinity = card.GetProperty("deckAffinity").GetString()!;
            var rulesText = card.GetProperty("rulesText").GetString()!;
            var labels = card.GetProperty("labels").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var costs = ParseCosts(card.GetProperty("cost"));
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

    private static IReadOnlyList<CardCost> ParseCosts(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Array)
        {
            return e.EnumerateArray().Select(ParseCost).ToArray();
        }

        return [ParseCost(e)];
    }

    private static CardCost ParseCost(JsonElement e)
    {
        var t = e.GetProperty("type").GetString();
        return t switch
        {
            "None" => new NoCost(),
            "RequireMomentum" => new RequireMomentumCost(e.GetProperty("minimum").GetInt32()),
            "SpendMomentum" => new SpendMomentumCost(e.GetProperty("amount").GetInt32()),
            "SpendAllMomentum" => new SpendAllMomentumCost(),
            "SpendUpToMomentum" => new SpendUpToMomentumCost(e.GetProperty("max").GetInt32()),
            _ => new NoCost(),
        };
    }

    private static CardEffect ParseEffect(JsonElement e)
    {
        var t = e.GetProperty("type").GetString();
        var target = e.TryGetProperty("target", out var te) && te.GetString() == "Self" ? CardTarget.Self : CardTarget.Opponent;
        return t switch
        {
            "DealDamage" => new DamageCardEffect(e.GetProperty("amount").GetInt32(), target),
            "DealDamageIgnoringArmor" => new DamageIgnoringArmorCardEffect(e.GetProperty("amount").GetInt32(), target),
            "GainArmor" => new GainArmorCardEffect(e.GetProperty("amount").GetInt32(), target),
            "DrawCards" => new DrawCardsCardEffect(e.GetProperty("amount").GetInt32(), target),
            "Heal" => new HealCardEffect(e.GetProperty("amount").GetInt32(), target),
            "ApplyBleed" => new ApplyBleedCardEffect(e.GetProperty("amount").GetInt32(), target),
            "GainGeneratedMomentum" => new GainGeneratedMomentumCardEffect(e.GetProperty("amount").GetInt32(), target),
            "DealDamageNTimes" => new DamageNTimesCardEffect(e.GetProperty("amount").GetInt32(), e.GetProperty("times").GetInt32(), target),
            "AttackCountThisTurnToGm" => new AttackCountThisTurnToGmCardEffect(target),
            "ReflectNextEnemyAttackDamage" => new ReflectNextEnemyAttackDamageCardEffect(e.GetProperty("amount").GetInt32(), target),
            "NextAttackBonusDamageThisTurn" => new NextAttackBonusDamageThisTurnCardEffect(e.GetProperty("amount").GetInt32(), target),
            "ConditionalGainArmorIfMomentumAtLeast" => new ConditionalGainArmorIfMomentumAtLeastCardEffect(e.GetProperty("minimumMomentum").GetInt32(), e.GetProperty("amount").GetInt32(), target),
            "DealDamagePerMomentumSpent" => new DealDamagePerMomentumSpentCardEffect(e.GetProperty("damagePerMomentum").GetInt32(), target),
            _ => new DamageCardEffect(0, CardTarget.Opponent),
        };
    }
}
