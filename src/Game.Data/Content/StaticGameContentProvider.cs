using Game.Core.Content;
using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;

namespace Game.Data.Content;

public static class StaticGameContentProvider
{
    public static GameContentBundle LoadDefault()
    {
        var cardDefinitions = PlaytestContent.CardDefinitions.ToDictionary(k => k.Key, v => v.Value);
        var deckDefinitions = new Dictionary<string, RunDeckDefinition>(StringComparer.Ordinal);
        var enemyDefinitions = new Dictionary<string, EnemyDefinition>(StringComparer.Ordinal);
        ZoneSpawnTable? zone1SpawnTable = null;

        var root = Path.Combine(AppContext.BaseDirectory, "Content");
        if (!Directory.Exists(root))
        {
            root = Path.Combine(Directory.GetCurrentDirectory(), "src", "Game.Data", "Content");
        }

        if (File.Exists(Path.Combine(root, "blades.cards.json")))
        {
            foreach (var kvp in BladesContentLoader.LoadCards(root))
            {
                cardDefinitions[kvp.Key] = kvp.Value;
            }
        }

        if (File.Exists(Path.Combine(root, "enemy.cards.zone1.json")))
        {
            foreach (var kvp in Zone1EnemyContentLoader.LoadEnemyCards(root))
            {
                cardDefinitions[kvp.Key] = kvp.Value;
            }
        }

        if (File.Exists(Path.Combine(root, "blades.deck.json")))
        {
            var bladesDeck = BladesContentLoader.LoadDeck(root);
            deckDefinitions[bladesDeck.Id] = bladesDeck;
        }

        if (File.Exists(Path.Combine(root, "enemies.zone1.json")))
        {
            foreach (var kvp in Zone1EnemyContentLoader.LoadEnemies(root))
            {
                enemyDefinitions[kvp.Key] = kvp.Value;
            }
        }

        if (File.Exists(Path.Combine(root, "spawn-table.zone1.json")))
        {
            zone1SpawnTable = Zone1EnemyContentLoader.LoadSpawnTable(root);
        }

        if (enemyDefinitions.Count > 0 && zone1SpawnTable is not null)
        {
            var enemyCards = cardDefinitions
                .Where(x => x.Key.Value.StartsWith("enemy-", StringComparison.Ordinal))
                .ToDictionary(x => x.Key, x => x.Value);
            Zone1EnemyContentLoader.ValidateReferences(enemyCards, enemyDefinitions, zone1SpawnTable);
        }

        return new GameContentBundle(
            CardDefinitions: cardDefinitions,
            DeckDefinitions: deckDefinitions,
            RewardCardPool: cardDefinitions.Keys.OrderBy(k => k.Value, StringComparer.Ordinal).ToArray(),
            OpeningCombat: PlaytestContent.OpeningCombat,
            EnemyDefinitions: enemyDefinitions,
            Zone1SpawnTable: zone1SpawnTable);
    }
}
