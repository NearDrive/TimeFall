using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;

namespace Game.Cli;

internal static class CliRenderer
{
    public static void RenderHelp()
    {
        Console.WriteLine("Commands:");
        Console.WriteLine("  start <seed>        Start deterministic run");
        Console.WriteLine("  state | status      Show run summary");
        Console.WriteLine("  help                Show commands");
        Console.WriteLine("  map                 Show current node and neighbors");
        Console.WriteLine("  move <nodeId>       Move to adjacent node");
        Console.WriteLine("  hand                Show combat hand");
        Console.WriteLine("  play <index> [t]    Play card from hand");
        Console.WriteLine("  end                 End player turn");
        Console.WriteLine("  reward              Show reward options");
        Console.WriteLine("  choose <id|index>   Claim reward card");
        Console.WriteLine("  skip                Skip active reward");
        Console.WriteLine("  rest                Use rest heal at rest node");
        Console.WriteLine("  shop                Show shop interaction summary");
        Console.WriteLine("  remove <id|index>   Remove card (shop/deck removal)");
        Console.WriteLine("  deck                Show run deck");
        Console.WriteLine("  discard             Show combat discard pile");
        Console.WriteLine("  quit                Exit CLI");
    }

    public static void RenderState(GameState state, IReadOnlyList<GameEvent> recentEvents, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Phase: {state.Phase}");
        Console.WriteLine($"Run HP/Armor: {state.RunHp}/{state.RunMaxHp}");
        Console.WriteLine($"Node: {state.Map.CurrentNodeId}");

        var neighbors = state.Map.Graph.GetNeighbors(state.Map.CurrentNodeId).Select(n => n.Value);
        Console.WriteLine($"Adjacent: {(neighbors.Any() ? string.Join(", ", neighbors) : "(none)")}");

        var collapsed = state.Time.CollapsedNodeIds.Select(n => n.Value).ToArray();
        Console.WriteLine($"Collapsed: {(collapsed.Length > 0 ? string.Join(", ", collapsed) : "(none)")}");
        Console.WriteLine($"Time step: {state.Time.CurrentStep} | Caught: {state.Time.PlayerCaughtByTime}");

        if (state.Combat is { } combat)
        {
            RenderCombat(combat, cardDefinitions);
        }

        if (state.Reward is { } reward)
        {
            Console.WriteLine($"Reward: {reward.RewardType}");
            for (var i = 0; i < reward.CardOptions.Count; i++)
            {
                var cardId = reward.CardOptions[i];
                Console.WriteLine($"  [{i}] {GetCardName(cardId, cardDefinitions)} ({cardId.Value})");
            }
        }

        if (state.NodeInteraction is { } interaction)
        {
            Console.WriteLine($"Interaction: {interaction.NodeType} at {interaction.NodeId}");
        }

        Console.WriteLine("Recent events:");
        if (recentEvents.Count == 0)
        {
            Console.WriteLine("  (none)");
            return;
        }

        foreach (var gameEvent in recentEvents.TakeLast(8))
        {
            Console.WriteLine($"  - {FormatEvent(gameEvent, cardDefinitions)}");
        }
    }

    public static void RenderMap(GameState state)
    {
        Console.WriteLine($"Current node: {state.Map.CurrentNodeId}");
        foreach (var neighborId in state.Map.Graph.GetNeighbors(state.Map.CurrentNodeId))
        {
            state.Map.Graph.TryGetNode(neighborId, out var node);
            var collapsed = state.Time.CollapsedNodeIds.Contains(neighborId) ? " [collapsed]" : string.Empty;
            var resolved = state.Map.ResolvedEncounterNodeIds.Contains(neighborId) ? " [resolved]" : string.Empty;
            Console.WriteLine($"- {neighborId} ({node?.Type}){collapsed}{resolved}");
        }
    }

    public static void RenderHand(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        if (state.Combat is not { } combat)
        {
            Console.WriteLine("No active combat.");
            return;
        }

        Console.WriteLine("Hand:");
        for (var i = 0; i < combat.Player.Deck.Hand.Count; i++)
        {
            var card = combat.Player.Deck.Hand[i];
            Console.WriteLine($"[{i}] {GetCardName(card.DefinitionId, cardDefinitions)}");
        }
    }

    public static void RenderDeck(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Run deck ({state.RunDeck.Count} cards):");
        for (var i = 0; i < state.RunDeck.Count; i++)
        {
            var card = state.RunDeck[i];
            Console.WriteLine($"[{i}] {GetCardName(card.DefinitionId, cardDefinitions)} ({card.DefinitionId.Value})");
        }
    }

    public static void RenderDiscard(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        if (state.Combat is not { } combat)
        {
            Console.WriteLine("No active combat.");
            return;
        }

        Console.WriteLine($"Discard pile ({combat.Player.Deck.DiscardPile.Count}):");
        foreach (var card in combat.Player.Deck.DiscardPile)
        {
            Console.WriteLine($"- {GetCardName(card.DefinitionId, cardDefinitions)}");
        }
    }

    private static void RenderCombat(CombatState combat, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Combat turn: {combat.TurnOwner}");
        Console.WriteLine($"Player HP/Armor: {combat.Player.HP}/{combat.Player.Armor}");
        Console.WriteLine($"Enemy HP/Armor: {combat.Enemy.HP}/{combat.Enemy.Armor}");
        Console.WriteLine($"Enemy visible hand size: {combat.Enemy.Deck.Hand.Count}");
        Console.WriteLine($"Hand size: {combat.Player.Deck.Hand.Count}");

        if (combat.NeedsOverflowDiscard)
        {
            Console.WriteLine($"Overflow discard required: {combat.RequiredOverflowDiscardCount}");
        }

        Console.WriteLine("Hand preview:");
        for (var i = 0; i < combat.Player.Deck.Hand.Count; i++)
        {
            Console.WriteLine($"  [{i}] {GetCardName(combat.Player.Deck.Hand[i].DefinitionId, cardDefinitions)}");
        }
    }

    public static string FormatEvent(GameEvent gameEvent, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        return gameEvent switch
        {
            RunStarted e => $"Run started (seed {e.Seed})",
            EnteredCombat e => $"Entered combat at {e.NodeId?.Value ?? "unknown"} ({e.NodeType?.ToString() ?? "n/a"})",
            CardDrawn e => $"Drew {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            CardDiscarded e => $"Discarded {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            PlayerStrikePlayed e => $"Strike for {e.Damage} (enemy HP {e.EnemyHpAfterHit})",
            EnemyAttackPlayed e => $"Enemy attack for {e.Damage} (player HP {e.PlayerHpAfterHit})",
            TurnEnded e => $"Turn -> {e.NextTurnOwner}",
            DeckReshuffled => "Deck reshuffled",
            CardBurned e => $"Burned {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            MovedToNode e => $"Moved to {e.NodeId.Value}",
            EncounterTriggered e => $"Encounter triggered at {e.NodeId.Value} ({e.NodeType})",
            EncounterResolved e => $"Encounter resolved at {e.NodeId.Value} ({e.NodeType})",
            EncounterAlreadyResolved e => $"Encounter already resolved at {e.NodeId.Value} ({e.NodeType})",
            CombatVictory => "Combat victory",
            CombatEnded e => $"Combat ended (won={e.PlayerWon})",
            RewardOffered e => $"Reward offered ({e.RewardType})",
            RewardChosen e => $"Reward chosen {e.CardId.Value}",
            RewardSkipped => "Reward skipped",
            CardAddedToDeck e => $"Added {e.CardId.Value} to deck",
            DeckRemovalBegan e => $"Deck removal started ({e.RemainingRemovals} remove)",
            CardRemovedFromDeck e => $"Removed {e.CardId.Value} from deck",
            RestUsed e => $"Rest used at {e.NodeId.Value}",
            Healed e => $"Healed {e.Amount} (HP {e.CurrentHp}/{e.MaxHp})",
            ShopRemovalUsed e => $"Shop removal used at {e.NodeId.Value} on {e.CardId.Value}",
            TimeAdvanced e => $"Time advanced to {e.Step}",
            NodeCollapsed e => $"Node collapsed {e.NodeId.Value}",
            TimeCaughtPlayer e => $"Time caught player at {e.NodeId.Value} (step {e.Step})",
            _ => gameEvent.GetType().Name,
        };
    }

    private static string GetCardName(CardId id, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        return cardDefinitions.TryGetValue(id, out var def) ? def.Name : id.Value;
    }
}
