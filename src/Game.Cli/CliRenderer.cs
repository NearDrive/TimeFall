using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;

namespace Game.Cli;

internal static class CliRenderer
{
    public static void RenderHelp(GamePhase phase)
    {
        Console.WriteLine($"Commands ({phase}):");
        if (phase == GamePhase.MainMenu)
        {
            Console.WriteLine("  continue            Continue active saved run");
            Console.WriteLine("  new                 Enter New Run menu");
            Console.WriteLine("  sandbox [seed]      Enter official sandbox mode");
            Console.WriteLine("  help                Show commands");
            Console.WriteLine("  quit                Exit CLI");
            return;
        }

        if (phase == GamePhase.NewRunMenu)
        {
            Console.WriteLine("  select-deck         Open deck selection");
            Console.WriteLine("  edit-deck           Open deck editing for selected deck");
            Console.WriteLine("  start [seed]        Start run with selected deck (default seed 1337)");
            Console.WriteLine("  back                Return to main menu");
            Console.WriteLine("  help                Show commands");
            Console.WriteLine("  quit                Exit CLI");
            return;
        }

        if (phase == GamePhase.DeckSelect)
        {
            Console.WriteLine("  decks               List available run decks");
            Console.WriteLine("  select <id|index>   Select run deck");
            Console.WriteLine("  back                Return to New Run menu");
            return;
        }

        if (phase == GamePhase.DeckEdit)
        {
            Console.WriteLine("  enabled             List enabled reward-pool cards");
            Console.WriteLine("  disabled            List disabled reward-pool cards");
            Console.WriteLine("  enable <id|index>   Enable reward card");
            Console.WriteLine("  disable <id|index>  Disable reward card");
            Console.WriteLine("  toggle <id|index>   Toggle reward card");
            Console.WriteLine("  enable-all          Enable all compatible cards");
            Console.WriteLine("  disable-all         Disable all compatible cards");
            Console.WriteLine("  autofill-min        Deterministically fill to minimum");
            Console.WriteLine("  autofill-max        Deterministically fill to maximum");
            Console.WriteLine("  done                Validate + apply reward pool");
            Console.WriteLine("  back                Cancel and return to New Run menu");
            return;
        }

        if (phase == GamePhase.SandboxDeckSelect)
        {
            Console.WriteLine("  sandbox-decks       List available decks");
            Console.WriteLine("  select-sandbox-deck <id|index> Select sandbox deck");
            Console.WriteLine("  back                Leave sandbox and return to main menu");
            Console.WriteLine("  help                Show commands");
            Console.WriteLine("  quit                Exit CLI");
            return;
        }

        if (phase == GamePhase.SandboxDeckEdit)
        {
            Console.WriteLine("  cards               List allowed cards + equipped loadout");
            Console.WriteLine("  equip <id|index>    Equip card into sandbox loadout");
            Console.WriteLine("  unequip <id|index>  Unequip card from sandbox loadout");
            Console.WriteLine("  clear-loadout       Remove all equipped cards");
            Console.WriteLine("  enemies             List enemies");
            Console.WriteLine("  select-enemy <id|index> Select enemy");
            Console.WriteLine("  change-enemy        Move to enemy selection phase");
            Console.WriteLine("  start               Start sandbox combat (if valid)");
            Console.WriteLine("  back                Leave sandbox and return to main menu");
            return;
        }

        if (phase == GamePhase.SandboxEnemySelect)
        {
            Console.WriteLine("  enemies             List enemies");
            Console.WriteLine("  select-enemy <id|index> Select enemy");
            Console.WriteLine("  start               Start sandbox combat");
            Console.WriteLine("  setup               Return to loadout setup");
            Console.WriteLine("  change-deck         Leave sandbox (then re-enter to pick a deck)");
            Console.WriteLine("  back                Leave sandbox and return to main menu");
            return;
        }

        if (phase == GamePhase.SandboxCombat)
        {
            Console.WriteLine("  state | status      Show combat summary");
            Console.WriteLine("  hand                Show combat hand");
            Console.WriteLine("  play <index> [t]    Play card from hand (0-based index, optional target index)");
            Console.WriteLine("  end                 End player turn");
            Console.WriteLine("  discard <index...>  Discard overflow cards (0-based)");
            Console.WriteLine("  discardpile         Show combat discard pile");
            Console.WriteLine("  help                Show commands");
            Console.WriteLine("  quit                Exit CLI");
            return;
        }

        if (phase == GamePhase.SandboxPostCombat)
        {
            Console.WriteLine("  repeat              Start another combat with same setup");
            Console.WriteLine("  setup               Return to loadout setup");
            Console.WriteLine("  change-enemy        Return to enemy selection");
            Console.WriteLine("  change-deck         Leave sandbox (then re-enter to pick a deck)");
            Console.WriteLine("  cards               Show loadout");
            Console.WriteLine("  enemies             Show enemy list");
            Console.WriteLine("  back                Leave sandbox and return to main menu");
            return;
        }

        Console.WriteLine("  state | status      Show run summary");
        Console.WriteLine("  map                 Show current node and neighbors");
        Console.WriteLine("  zone                Show full zone map (layered + connections)");
        Console.WriteLine("  move <displayId|i>  Move to adjacent node (displayId or 0-based adjacent index)");
        Console.WriteLine("  hand                Show combat hand");
        Console.WriteLine("  play <index> [t]    Play card from hand (0-based index, optional target index)");
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
        Console.WriteLine("  help                Show commands");
        Console.WriteLine("  quit                Exit CLI");
    }

    public static void RenderState(GameState state, IReadOnlyList<GameEvent> recentEvents, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Phase: {state.Phase}");
        if (state.Phase == GamePhase.MainMenu)
        {
            RenderMainMenu(state);
            return;
        }
        if (state.Phase == GamePhase.NewRunMenu)
        {
            RenderNewRunMenu(state);
            return;
        }
        if (state.Phase == GamePhase.DeckSelect)
        {
            RenderDeckSelection(state);
            return;
        }

        if (state.Phase == GamePhase.DeckEdit)
        {
            if (state.SelectedDeckId is null || !state.DeckDefinitions.TryGetValue(state.SelectedDeckId, out var selectedDeck))
            {
                Console.WriteLine("No deck selected. Use select-deck first.");
                return;
            }

            var working = state.RewardPoolEdit?.WorkingEnabledCardIds ?? state.EnabledRewardPoolCardIds;
            Console.WriteLine($"Deck edit: {selectedDeck.Name} ({selectedDeck.Id})");
            Console.WriteLine($"Enabled reward cards: {working.Count}/{selectedDeck.RewardPoolCardIds.Count} (min 20 when available, max 30)");
            Console.WriteLine("Use 'enabled' / 'disabled' to inspect cards, then 'done' to apply or 'back' to cancel.");
            return;
        }
        if (state.Phase == GamePhase.SandboxDeckSelect)
        {
            Console.WriteLine("Sandbox setup: choose a deck.");
            RenderDecks(state);
            Console.WriteLine("Commands: sandbox-decks, select-sandbox-deck <id|index>, back");
            return;
        }
        if (state.Phase == GamePhase.SandboxDeckEdit)
        {
            RenderSandboxSetupSummary(state, cardDefinitions);
            Console.WriteLine("Commands: cards, equip, unequip, clear-loadout, enemies, select-enemy, start");
            return;
        }
        if (state.Phase == GamePhase.SandboxEnemySelect)
        {
            Console.WriteLine("Sandbox enemy selection");
            RenderSandboxSetupSummary(state, cardDefinitions, includeCards: false);
            RenderSandboxEnemies(state);
            Console.WriteLine("Commands: select-enemy <id|index>, start, setup");
            return;
        }
        if (state.Phase == GamePhase.SandboxPostCombat)
        {
            Console.WriteLine("Sandbox post-combat");
            RenderSandboxSetupSummary(state, cardDefinitions, includeCards: false);
            if (state.Sandbox is { } sandbox)
            {
                Console.WriteLine($"Last combat: seed {sandbox.LastCombatSeed?.ToString() ?? "(n/a)"} | won: {sandbox.LastCombatWon?.ToString() ?? "(n/a)"}");
            }

            Console.WriteLine("Commands: repeat, setup, change-enemy, cards");
            return;
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
        var displayIds = MapDisplayIds.Create(state.Map);
        Console.WriteLine($"Current node: {displayIds.Get(state.Map.CurrentNodeId)}");
        var neighbors = state.Map.Graph.GetNeighbors(state.Map.CurrentNodeId).ToArray();
        for (var i = 0; i < neighbors.Length; i++)
        {
            var neighborId = neighbors[i];
            state.Map.Graph.TryGetNode(neighborId, out var node);
            var collapsed = state.Time.CollapsedNodeIds.Contains(neighborId) ? " [collapsed]" : string.Empty;
            var resolved = state.Map.ResolvedEncounterNodeIds.Contains(neighborId) ? " [resolved]" : string.Empty;
            Console.WriteLine($"- [{i}] {displayIds.Get(neighborId)} ({node?.Type}){collapsed}{resolved}");
        }
    }


    public static void RenderZone(GameState state)
    {
        var displayIds = MapDisplayIds.Create(state.Map);
        Console.WriteLine("Zone Map");
        Console.WriteLine();

        if (!state.Map.Graph.TryGetNode(state.Map.StartNodeId, out _))
        {
            Console.WriteLine("[?:missing-start]");
            return;
        }

        var nodesByDepth = state.Map.Graph.Nodes
            .Select(node => (Node: node, Depth: state.Map.DistanceFromStart.GetValueOrDefault(node.Id, int.MaxValue)))
            .GroupBy(entry => entry.Depth)
            .OrderBy(group => group.Key)
            .ToArray();

        foreach (var depthGroup in nodesByDepth)
        {
            var nodes = depthGroup
                .Select(entry => entry.Node)
                .OrderBy(node => displayIds.Get(node.Id), StringComparer.Ordinal)
                .Select(node => FormatZoneNode(node, state.Map.CurrentNodeId, state.Time.CollapsedNodeIds, displayIds))
                .ToArray();
            Console.WriteLine($"Depth {depthGroup.Key}: {string.Join(" ", nodes)}");
        }

        Console.WriteLine();
        Console.WriteLine("Connections:");
        foreach (var node in state.Map.Graph.Nodes
                     .OrderBy(node => state.Map.DistanceFromStart.GetValueOrDefault(node.Id, int.MaxValue))
                     .ThenBy(node => displayIds.Get(node.Id), StringComparer.Ordinal))
        {
            if (!state.Map.DistanceFromStart.TryGetValue(node.Id, out var fromDepth))
            {
                continue;
            }

            var outwardNeighbors = state.Map.Graph.GetNeighbors(node.Id)
                .Where(neighbor => state.Map.DistanceFromStart.TryGetValue(neighbor, out var toDepth) && toDepth > fromDepth)
                .OrderBy(neighbor => state.Map.DistanceFromStart.GetValueOrDefault(neighbor, int.MaxValue))
                .ThenBy(neighbor => displayIds.Get(neighbor), StringComparer.Ordinal)
                .ToArray();

            if (outwardNeighbors.Length == 0)
            {
                continue;
            }

            Console.WriteLine($"{displayIds.Get(node.Id)} -> {string.Join(", ", outwardNeighbors.Select(displayIds.Get))}");
        }

        Console.WriteLine();
        Console.WriteLine($"Current node: {displayIds.Get(state.Map.CurrentNodeId)}");
        var bossDistance = state.Map.BossNodeId is { } bossNodeId && state.Map.Graph.TryGetShortestPathDistance(state.Map.CurrentNodeId, bossNodeId, out var distance)
            ? distance.ToString()
            : "unreachable";
        Console.WriteLine($"Boss distance: {bossDistance}");
        Console.WriteLine($"Collapsed nodes: {state.Time.CollapsedNodeIds.Count}");
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

    public static void RenderSandboxCards(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        if (state.Sandbox?.SelectedDeckId is not { } selectedDeckId || !state.DeckDefinitions.TryGetValue(selectedDeckId, out var deck))
        {
            Console.WriteLine("No sandbox deck selected.");
            return;
        }

        var equipped = state.Sandbox.EquippedCardIds.ToHashSet();
        var cards = deck.StartingCombatDeckCardIds.Distinct().OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
        Console.WriteLine($"Sandbox cards for {deck.Name} ({deck.Id}):");
        for (var i = 0; i < cards.Length; i++)
        {
            var marker = equipped.Contains(cards[i]) ? "*" : " ";
            Console.WriteLine($"[{i}] {marker} {FormatCardSummary(cards[i], cardDefinitions)} ({cards[i].Value})");
        }

        Console.WriteLine($"Equipped: {state.Sandbox.EquippedCardIds.Count} card(s)");
    }

    public static void RenderSandboxEnemies(GameState state)
    {
        var enemyIds = SandboxEnemyCatalog.GetEnemyIds(state.EnemyDefinitions);
        if (enemyIds.Length == 0)
        {
            Console.WriteLine("No enemies available.");
            return;
        }

        var selectedEnemyId = state.Sandbox?.SelectedEnemyId;
        Console.WriteLine("Sandbox enemies:");
        for (var i = 0; i < enemyIds.Length; i++)
        {
            var marker = enemyIds[i] == selectedEnemyId ? "*" : " ";
            Console.WriteLine($"[{i}] {marker} {enemyIds[i]}");
        }
    }

    private static void RenderCombat(CombatState combat, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        Console.WriteLine($"Combat turn: {combat.TurnOwner}");
        Console.WriteLine($"Combat Player HP: {combat.Player.HP}");
        Console.WriteLine($"Combat Player Armor: {combat.Player.Armor}");
        var gm = combat.Player.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
        Console.WriteLine($"Momentum: {MomentumMath.DerivedMomentumFromGm(gm)} (gm: {gm})");
        Console.WriteLine($"Player statuses: {FormatStatuses(combat.Player)}");
        Console.WriteLine($"Player piles: draw {combat.Player.Deck.DrawPile.Count} | hand {combat.Player.Deck.Hand.Count} | discard {combat.Player.Deck.DiscardPile.Count} | burn {combat.Player.Deck.BurnPile.Count}");
        Console.WriteLine($"Shuffle Fatigue: {combat.Player.Deck.ReshuffleCount}");
        Console.WriteLine($"Next reshuffle discard: {Math.Min(combat.Player.Deck.ReshuffleCount + 1, HandManager.InitialHandSize)}");
        Console.WriteLine("Enemies:");
        for (var i = 0; i < combat.Enemies.Count; i++)
        {
            var enemy = combat.Enemies[i];
            Console.WriteLine($"  [{i}] {enemy.EntityId} — HP {enemy.HP}, Armor {enemy.Armor}");
            Console.WriteLine($"      Statuses: {FormatStatuses(enemy)}");
            Console.WriteLine($"      Piles: draw {enemy.Deck.DrawPile.Count} | hand {enemy.Deck.Hand.Count} | discard {enemy.Deck.DiscardPile.Count} | burn {enemy.Deck.BurnPile.Count}");
            Console.WriteLine($"      Shuffle Fatigue: {enemy.Deck.ReshuffleCount}");
        }

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
            SandboxCombatStarted e => $"Sandbox combat started (seed {e.Seed})",
            DeckSelected e => $"Deck selected: {e.DeckId}",
            RewardPoolEditRejected e => $"Reward pool edit rejected: {e.Message}",
            RewardPoolEditConfirmed e => $"Reward pool edit confirmed ({e.EnabledCount} enabled)",
            TurnEnded e => $"---------------- {e.NextTurnOwner} turn begins ----------------",
            ResourceChanged e when e.ResourceType == ResourceType.Momentum
                => $"{e.Owner} GM: {e.Before} -> {e.After}; Momentum: {MomentumMath.DerivedMomentumFromGm(e.Before)} -> {MomentumMath.DerivedMomentumFromGm(e.After)} ({e.Reason})",
            ResourceChanged e => $"{e.Owner} {e.ResourceType}: {e.Before} -> {e.After} ({e.Reason})",
            MomentumDecayApplied e => $"Momentum decay: gm {e.BeforeGm} -> {e.AfterGm}",
            EnteredCombat e => $"Entered combat at {e.NodeId?.Value ?? "unknown"} ({e.NodeType?.ToString() ?? "n/a"})",
            CardDrawn e => $"Draws {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            CardDiscarded e => $"Plays/discards {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            PlayerStrikePlayed e => $"[Player] Uses {GetCardName(e.Card.DefinitionId, cardDefinitions)} -> Enemy HP {e.EnemyHpBeforeHit} -> {e.EnemyHpAfterHit}, Armor {e.EnemyArmorBeforeHit} -> {e.EnemyArmorAfterHit} (Base {e.BaseDamage} + Momentum {e.MomentumBonus}, {e.DamageBlockedByArmor} blocked)",
            EnemyAttackPlayed e => $"[Enemy] Uses {GetCardName(e.Card.DefinitionId, cardDefinitions)} -> Player HP {e.PlayerHpBeforeHit} -> {e.PlayerHpAfterHit}, Armor {e.PlayerArmorBeforeHit} -> {e.PlayerArmorAfterHit} ({e.Damage} incoming, {e.DamageBlockedByArmor} blocked)",
            StatusApplied e => $"[{e.Source}] Applies {e.StatusName} {e.Amount} to {e.Target}",
            StatusTriggered e => $"[{e.Target}] {e.StatusName} triggers for {e.Amount} damage (HP {e.HpBefore} -> {e.HpAfter})",
            StatusExpired e => $"[{e.Target}] {e.StatusName} expires",
            DeckReshuffled => "Deck reshuffled",
            CardBurned e => $"Burns {GetCardName(e.Card.DefinitionId, cardDefinitions)}",
            ReshuffleFatigueApplied e => $"Reshuffle Fatigue {e.DiscardCount}: discard {e.DiscardCount} cards",
            FatigueDiscardResolved e => $"{e.Owner} discards {e.DiscardCount} cards due to fatigue",
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


    private static string FormatZoneNode(Node node, NodeId currentNodeId, ISet<NodeId> collapsedNodeIds, MapDisplayIds displayIds)
    {
        var marker = node.Type switch
        {
            NodeType.Start => "S",
            NodeType.Boss => "B",
            NodeType.Combat => "C",
            NodeType.Elite => "E",
            NodeType.Rest => "R",
            NodeType.Shop => "$",
            NodeType.Event => "?",
            _ => "?",
        };

        var suffix = string.Empty;
        if (node.Id == currentNodeId)
        {
            suffix += "@";
        }

        var collapsed = collapsedNodeIds.Contains(node.Id) ? " X" : string.Empty;

        return $"[{marker}:{displayIds.Get(node.Id)}{suffix}{collapsed}]";
    }

    public static void RenderRewardPool(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions, bool enabled)
    {
        if (state.SelectedDeckId is null || !state.DeckDefinitions.TryGetValue(state.SelectedDeckId, out var deck))
        {
            Console.WriteLine("No selected deck.");
            return;
        }

        var working = state.RewardPoolEdit?.WorkingEnabledCardIds ?? state.EnabledRewardPoolCardIds;
        var enabledSet = working.ToHashSet();
        var orderedPool = deck.RewardPoolCardIds.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
        var filtered = orderedPool
            .Select((cardId, index) => (cardId, index))
            .Where(entry => enabled ? enabledSet.Contains(entry.cardId) : !enabledSet.Contains(entry.cardId))
            .ToArray();
        Console.WriteLine(enabled ? "Enabled reward cards:" : "Disabled reward cards:");
        foreach (var entry in filtered)
        {
            Console.WriteLine($"  [{entry.index}] {FormatCardSummary(entry.cardId, cardDefinitions)} ({entry.cardId.Value})");
        }

        if (filtered.Length == 0)
        {
            Console.WriteLine("  (none)");
        }
    }

    private static string FormatCardSummary(CardId id, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        if (!cardDefinitions.TryGetValue(id, out var definition))
        {
            return $"[?] {id.Value}";
        }

        return $"{CardRarityFormatter.FormatPrefix(definition)} {definition.Name} — {CardRulesTextFormatter.GetReadableRulesText(definition)}";
    }


    private static string GetCardName(CardId id, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions)
    {
        return cardDefinitions.TryGetValue(id, out var def) ? def.Name : id.Value;
    }

    private static string FormatStatuses(CombatEntity entity)
    {
        var statuses = new List<(string Name, int Value)>();
        if (entity.Bleed > 0)
        {
            statuses.Add(("Bleed", entity.Bleed));
        }

        if (entity.ReflectNextEnemyAttackDamage > 0)
        {
            statuses.Add(("Reflect", entity.ReflectNextEnemyAttackDamage));
        }

        if (entity.Vulnerable > 0)
        {
            statuses.Add(("Vulnerable", entity.Vulnerable));
        }

        if (entity.Weak > 0)
        {
            statuses.Add(("Weak", entity.Weak));
        }

        if (statuses.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", statuses.OrderBy(s => s.Name, StringComparer.Ordinal).Select(s => $"{s.Name} {s.Value}"));
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

    private static void RenderMainMenu(GameState state)
    {
        Console.WriteLine("Main Menu");
        Console.WriteLine(state.HasActiveRunSave ? "- continue" : "- continue (unavailable: no active save)");
        Console.WriteLine("- new");
        Console.WriteLine("- quit");
    }

    private static void RenderNewRunMenu(GameState state)
    {
        Console.WriteLine("New Run Menu");
        Console.WriteLine($"Selected deck: {state.SelectedDeckId ?? "(none)"}");
        Console.WriteLine("Options:");
        Console.WriteLine("- select-deck");
        Console.WriteLine("- edit-deck");
        Console.WriteLine("- start");
        Console.WriteLine("- back");
    }

    private static void RenderSandboxSetupSummary(GameState state, IReadOnlyDictionary<CardId, CardDefinition> cardDefinitions, bool includeCards = true)
    {
        var sandbox = state.Sandbox;
        if (sandbox is null)
        {
            Console.WriteLine("Sandbox state unavailable.");
            return;
        }

        Console.WriteLine($"Deck: {sandbox.SelectedDeckId ?? "(none)"}");
        Console.WriteLine($"Enemy: {sandbox.SelectedEnemyId ?? "(none)"}");
        Console.WriteLine($"Combats played: {sandbox.CombatCount}");

        if (sandbox.SelectedDeckId is not { } selectedDeckId || !state.DeckDefinitions.TryGetValue(selectedDeckId, out var deck))
        {
            Console.WriteLine("Loadout valid: no (select deck)");
            return;
        }

        var allowed = deck.StartingCombatDeckCardIds.Distinct().ToHashSet();
        var equipped = sandbox.EquippedCardIds.Where(allowed.Contains).ToArray();
        Console.WriteLine($"Loadout valid: {(equipped.Length > 0 && sandbox.SelectedEnemyId is not null ? "yes" : "no")}");
        Console.WriteLine($"Equipped cards: {equipped.Length}");

        if (!includeCards)
        {
            return;
        }

        foreach (var cardId in equipped)
        {
            Console.WriteLine($"  - {FormatCardSummary(cardId, cardDefinitions)} ({cardId.Value})");
        }
    }

}
