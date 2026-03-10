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
        Console.WriteLine("  decks               List available run decks");
        Console.WriteLine("  select <id|index>   Select run deck in DeckSelect phase");
        Console.WriteLine("  start [seed]        Start run (default seed 1337)");
        Console.WriteLine("  state | status      Show run summary");
        Console.WriteLine("  help                Show commands");
        Console.WriteLine("  map                 Show current node and neighbors");
        Console.WriteLine("  move <nodeId|i>     Move to adjacent node (nodeId or 0-based adjacent index)");
        Console.WriteLine("  hand                Show combat hand");
        Console.WriteLine("  play <index> [t]    Play card from hand (0-based index)");
        Console.WriteLine("  end                 End player turn");
        Console.WriteLine("  reward              Show reward options");
        Console.WriteLine("  choose <id|index>   Claim reward card (index is 0-based)");
        Console.WriteLine("  skip                Skip active reward");
        Console.WriteLine("  rest                Use rest heal at rest node");
        Console.WriteLine("  shop                Show shop interaction summary");
        Console.WriteLine("  remove <id|index>   Remove card (shop/deck removal)");
        Console.WriteLine("  deck                Show run deck");
        Console.WriteLine("  discard <index...>  Discard overflow cards from hand (0-based)");
        Console.WriteLine("  discardpile         Show combat discard pile");
        Console.WriteLine("  quit                Exit CLI");
    }

    public static void RenderState(GameState state, IReadOnlyList<GameEvent> recentEvents, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Phase: {state.Phase}");
        if (state.Phase == GamePhase.DeckSelect)
        {
            RenderDeckSelection(state);
        }
        Console.WriteLine($"Run HP: {state.RunHp}/{state.RunMaxHp}");
        Console.WriteLine($"Node: {state.Map.CurrentNodeId}");

        var neighbors = state.Map.Graph.GetNeighbors(state.Map.CurrentNodeId).ToArray();
        var adjacentSummary = neighbors.Length == 0
            ? "(none)"
            : string.Join(", ", neighbors.Select((nodeId, index) => $"[{index}] {nodeId.Value}"));
        Console.WriteLine($"Adjacent: {adjacentSummary}");

        var collapsed = state.Time.CollapsedNodeIds.Select(n => n.Value).ToArray();
        Console.WriteLine($"Collapsed: {(collapsed.Length > 0 ? string.Join(", ", collapsed) : "(none)")}");
        Console.WriteLine($"Time step: {state.Time.CurrentStep} | Progress: {state.Time.MapTurnsSinceTimeAdvance}/{state.Time.TimeAdvanceInterval} | Caught: {state.Time.PlayerCaughtByTime}");

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
                Console.WriteLine($"  [{i}] {FormatCardSummary(cardId, cardDefinitions)} ({cardId.Value})");
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
        var neighbors = state.Map.Graph.GetNeighbors(state.Map.CurrentNodeId).ToArray();
        for (var i = 0; i < neighbors.Length; i++)
        {
            var neighborId = neighbors[i];
            state.Map.Graph.TryGetNode(neighborId, out var node);
            var collapsed = state.Time.CollapsedNodeIds.Contains(neighborId) ? " [collapsed]" : string.Empty;
            var resolved = state.Map.ResolvedEncounterNodeIds.Contains(neighborId) ? " [resolved]" : string.Empty;
            Console.WriteLine($"- [{i}] {neighborId} ({node?.Type}){collapsed}{resolved}");
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
            Console.WriteLine($"[{i}] {FormatCardSummary(card.DefinitionId, cardDefinitions)}");
        }
    }

    public static void RenderDeck(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Run deck ({state.RunDeck.Count} cards):");
        for (var i = 0; i < state.RunDeck.Count; i++)
        {
            var card = state.RunDeck[i];
            Console.WriteLine($"[{i}] {FormatCardSummary(card.DefinitionId, cardDefinitions)} ({card.DefinitionId.Value})");
        }
    }

    public static void RenderDecks(GameState state)
    {
        if (state.AvailableDeckIds.Count == 0)
        {
            Console.WriteLine("No decks available in loaded content.");
            return;
        }

        for (var i = 0; i < state.AvailableDeckIds.Count; i++)
        {
            var deckId = state.AvailableDeckIds[i];
            var deck = state.DeckDefinitions[deckId];
            Console.WriteLine($"[{i}] {deckId} — {deck.Name} (HP {deck.BaseMaxHp}, Resource: {deck.ResourceType})");
        }

        Console.WriteLine(state.SelectedDeckId is null
            ? "Selected deck: (none)"
            : $"Selected deck: {state.SelectedDeckId}");
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
            Console.WriteLine($"- {FormatCardSummary(card.DefinitionId, cardDefinitions)}");
        }
    }

    private static void RenderCombat(CombatState combat, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Combat turn: {combat.TurnOwner}");
        Console.WriteLine($"Combat Player HP: {combat.Player.HP}");
        Console.WriteLine($"Combat Player Armor: {combat.Player.Armor}");
        var gm = combat.Player.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
        Console.WriteLine($"Momentum: {MomentumMath.DerivedMomentumFromGm(gm)} (gm: {gm})");
        Console.WriteLine($"Combat Enemy HP: {combat.Enemy.HP}");
        Console.WriteLine($"Combat Enemy Armor: {combat.Enemy.Armor}");
        Console.WriteLine($"Player piles: draw {combat.Player.Deck.DrawPile.Count} | hand {combat.Player.Deck.Hand.Count} | discard {combat.Player.Deck.DiscardPile.Count} | burn {combat.Player.Deck.BurnPile.Count}");
        Console.WriteLine($"Enemy piles: draw {combat.Enemy.Deck.DrawPile.Count} | hand {combat.Enemy.Deck.Hand.Count} | discard {combat.Enemy.Deck.DiscardPile.Count} | burn {combat.Enemy.Deck.BurnPile.Count}");

        if (combat.NeedsOverflowDiscard)
        {
            Console.WriteLine($"Overflow discard required: {combat.RequiredOverflowDiscardCount}");
        }

        Console.WriteLine("Hand preview:");
        for (var i = 0; i < combat.Player.Deck.Hand.Count; i++)
        {
            Console.WriteLine($"  [{i}] {FormatCardSummary(combat.Player.Deck.Hand[i].DefinitionId, cardDefinitions)}");
        }
    }

    public static string FormatEvent(GameEvent gameEvent, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        return gameEvent switch
        {
            RunStarted e => $"Run started (seed {e.Seed})",
            DeckSelected e => $"Deck selected: {e.DeckId}",
            TurnEnded e => $"---------------- {e.NextTurnOwner} turn begins ----------------",
            ResourceChanged e => $"{e.Owner} {e.ResourceType}: {e.Before} -> {e.After} ({e.Reason})",
            MomentumDecayApplied e => $"Momentum decay: gm {e.BeforeGm} -> {e.AfterGm}",
            EnteredCombat e => $"Entered combat at {e.NodeId?.Value ?? "unknown"} ({e.NodeType?.ToString() ?? "n/a"})",
            CardDrawn e => $"Draws {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            CardDiscarded e => $"Plays/discards {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            PlayerStrikePlayed e => $"[Player] Uses {GetCardName(e.Card.DefinitionId, cardDefinitions)} -> Enemy HP {e.EnemyHpBeforeHit} -> {e.EnemyHpAfterHit}, Armor {e.EnemyArmorBeforeHit} -> {e.EnemyArmorAfterHit} ({e.Damage} incoming, {e.DamageBlockedByArmor} blocked)",
            EnemyAttackPlayed e => $"[Enemy] Uses {GetCardName(e.Card.DefinitionId, cardDefinitions)} -> Player HP {e.PlayerHpBeforeHit} -> {e.PlayerHpAfterHit}, Armor {e.PlayerArmorBeforeHit} -> {e.PlayerArmorAfterHit} ({e.Damage} incoming, {e.DamageBlockedByArmor} blocked)",
            DeckReshuffled => "Deck reshuffled",
            CardBurned e => $"Burns {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
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

    private static string FormatCardSummary(CardId id, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        if (!cardDefinitions.TryGetValue(id, out var definition))
        {
            return id.Value;
        }

        return $"{definition.Name} — {FormatCardRulesText(definition)}";
    }

    private static string FormatCardRulesText(CardDefinition definition)
    {
        if (definition.Effects.Count == 0)
        {
            return "No effect";
        }

        return string.Join(", ", definition.Effects.Select(effect => effect switch
        {
            DamageCardEffect damage => $"Deal {damage.Amount} damage{FormatTargetSuffix(damage.Target)}",
            GainArmorCardEffect armor => $"Gain {armor.Amount} armor{FormatTargetSuffix(armor.Target)}",
            DrawCardsCardEffect draw => $"Draw {draw.Amount} card{(draw.Amount == 1 ? string.Empty : "s")}{FormatTargetSuffix(draw.Target)}",
            _ => "Special effect",
        }));
    }

    private static string FormatTargetSuffix(CardTarget target)
    {
        return target switch
        {
            CardTarget.Self => " (self)",
            CardTarget.Opponent => string.Empty,
            _ => string.Empty,
        };
    }

    private static string GetCardName(CardId id, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        return cardDefinitions.TryGetValue(id, out var def) ? def.Name : id.Value;
    }

    private static void RenderDeckSelection(GameState state)
    {
        Console.WriteLine("Deck selection:");
        RenderDecks(state);
        if (state.SelectedDeckId is null)
        {
            Console.WriteLine("No deck selected. Use 'decks' and 'select <id|index>' before 'start'.");
        }
    }

}
