using Game.Core.Content;
using Game.Core.Cards;

namespace Game.Data.Content;

public static class StaticGameContentProvider
{
    public static GameContentBundle LoadDefault()
    {
        var cardDefinitions = PlaytestContent.CardDefinitions.ToDictionary(k => k.Key, v => v.Value);
        var deckDefinitions = new Dictionary<string, RunDeckDefinition>(StringComparer.Ordinal);
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

        if (File.Exists(Path.Combine(root, "blades.deck.json")))
        {
            var bladesDeck = BladesContentLoader.LoadDeck(root);
            deckDefinitions[bladesDeck.Id] = bladesDeck;
        }

        return new GameContentBundle(
            CardDefinitions: cardDefinitions,
            DeckDefinitions: deckDefinitions,
            RewardCardPool: cardDefinitions.Keys.OrderBy(k => k.Value, StringComparer.Ordinal).ToArray(),
            OpeningCombat: PlaytestContent.OpeningCombat);
    }
}
