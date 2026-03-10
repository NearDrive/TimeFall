using System.Text.Json;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Map;

namespace Game.Data.Content;

internal static class Zone1EnemyContentLoader
{
    public static IReadOnlyDictionary<CardId, CardDefinition> LoadEnemyCards(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, "enemy.cards.zone1.json"));
        var cards = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
        var result = new Dictionary<CardId, CardDefinition>();

        foreach (var card in cards)
        {
            var id = new CardId(card.GetProperty("id").GetString()!);
            var name = card.GetProperty("name").GetString()!;
            var rulesText = card.GetProperty("rulesText").GetString()!;
            var labels = card.GetProperty("labels").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var effects = card.GetProperty("effects").EnumerateArray().Select(ParseEffect).ToArray();
            result.Add(id, new CardDefinition(id, name, 0, effects, new NoCost(), labels, "Enemy", "Common", rulesText));
        }

        return result;
    }

    public static IReadOnlyDictionary<string, EnemyDefinition> LoadEnemies(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, "enemies.zone1.json"));
        var items = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? [];
        var result = new Dictionary<string, EnemyDefinition>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            var enemy = new EnemyDefinition(
                Id: item.GetProperty("id").GetString()!,
                Name: item.GetProperty("name").GetString()!,
                Zone: item.GetProperty("zone").GetInt32(),
                Tier: item.GetProperty("tier").GetString()!,
                Category: item.GetProperty("category").GetString()!,
                Role: item.GetProperty("role").GetString()!,
                Hp: item.GetProperty("hp").GetInt32(),
                StartingArmor: item.GetProperty("startingArmor").GetInt32(),
                Deck: item.GetProperty("deck").EnumerateArray().Select(x => new CardId(x.GetString()!)).ToArray(),
                Tags: item.TryGetProperty("tags", out var tags) ? tags.EnumerateArray().Select(x => x.GetString()!).ToArray() : null,
                Notes: item.TryGetProperty("notes", out var notes) ? notes.GetString() : null);

            result.Add(enemy.Id, enemy);
        }

        return result;
    }

    public static ZoneSpawnTable LoadSpawnTable(string root)
    {
        var json = File.ReadAllText(Path.Combine(root, "spawn-table.zone1.json"));
        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        var normal = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<WeightedEnemy>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var band in doc.GetProperty("normalEncounterPools").EnumerateObject())
        {
            var tiers = new Dictionary<string, IReadOnlyList<WeightedEnemy>>(StringComparer.Ordinal);
            foreach (var tier in band.Value.EnumerateObject())
            {
                var pool = tier.Value.EnumerateArray().Select(ParseWeightedEnemy).ToArray();
                tiers[tier.Name] = pool;
            }

            normal[band.Name] = tiers;
        }

        var elite = new Dictionary<string, IReadOnlyList<WeightedEnemy>>(StringComparer.Ordinal);
        foreach (var tier in doc.GetProperty("eliteEncounterPools").EnumerateObject())
        {
            elite[tier.Name] = tier.Value.EnumerateArray().Select(ParseWeightedEnemy).ToArray();
        }

        return new ZoneSpawnTable(
            Zone: doc.GetProperty("zone").GetInt32(),
            NormalEncounterPools: normal,
            EliteEncounterPools: elite,
            BossEnemyId: doc.GetProperty("bossEncounter").GetProperty("enemyId").GetString()!);
    }

    public static void ValidateReferences(
        IReadOnlyDictionary<CardId, CardDefinition> enemyCards,
        IReadOnlyDictionary<string, EnemyDefinition> enemies,
        ZoneSpawnTable spawnTable)
    {
        var cardIds = enemyCards.Keys.ToHashSet();
        foreach (var enemy in enemies.Values)
        {
            foreach (var cardId in enemy.Deck)
            {
                if (!cardIds.Contains(cardId))
                {
                    throw new InvalidOperationException($"Enemy '{enemy.Id}' references unknown card id '{cardId.Value}'.");
                }
            }
        }

        foreach (var entry in spawnTable.NormalEncounterPools.Values.SelectMany(x => x.Values).SelectMany(x => x))
        {
            if (!enemies.ContainsKey(entry.EnemyId))
            {
                throw new InvalidOperationException($"Spawn table normal pool references unknown enemy id '{entry.EnemyId}'.");
            }
        }

        foreach (var entry in spawnTable.EliteEncounterPools.Values.SelectMany(x => x))
        {
            if (!enemies.ContainsKey(entry.EnemyId))
            {
                throw new InvalidOperationException($"Spawn table elite pool references unknown enemy id '{entry.EnemyId}'.");
            }
        }

        if (!enemies.ContainsKey(spawnTable.BossEnemyId))
        {
            throw new InvalidOperationException($"Spawn table boss encounter references unknown enemy id '{spawnTable.BossEnemyId}'.");
        }
    }

    private static WeightedEnemy ParseWeightedEnemy(JsonElement item)
    {
        return new WeightedEnemy(
            EnemyId: item.GetProperty("enemyId").GetString()!,
            Weight: item.GetProperty("weight").GetInt32());
    }

    private static CardEffect ParseEffect(JsonElement e)
    {
        var t = e.GetProperty("type").GetString();
        var target = e.TryGetProperty("target", out var te) && te.GetString() == "Self" ? CardTarget.Self : CardTarget.Opponent;
        return t switch
        {
            "DealDamage" => new DamageCardEffect(e.GetProperty("amount").GetInt32(), target),
            "GainArmor" => new GainArmorCardEffect(e.GetProperty("amount").GetInt32(), target),
            "DrawCards" => new DrawCardsCardEffect(e.GetProperty("amount").GetInt32(), target),
            "Heal" => new HealCardEffect(e.GetProperty("amount").GetInt32(), target),
            "ApplyBleed" => new ApplyBleedCardEffect(e.GetProperty("amount").GetInt32(), target),
            "ReflectNextEnemyAttackDamage" => new ReflectNextEnemyAttackDamageCardEffect(e.GetProperty("amount").GetInt32(), target),
            "NextAttackBonusDamageThisTurn" => new NextAttackBonusDamageThisTurnCardEffect(e.GetProperty("amount").GetInt32(), target),
            _ => throw new InvalidOperationException($"Unsupported enemy card effect type '{t}'."),
        };
    }
}
