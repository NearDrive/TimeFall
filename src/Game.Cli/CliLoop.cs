using Game.Core.Cards;
using Game.Core.Game;
using Game.Application;
using Game.Data.Content;
using Game.Data.Save;
using Game.Core.Map;

namespace Game.Cli;


internal enum PersistenceTransition
{
    None,
    Save,
    Delete,
}

internal sealed class CliLoop
{
    private readonly GameContentBundle _content = StaticGameContentProvider.LoadDefault();
    private readonly SaveGameRepository _saveRepository = new(Path.Combine(AppContext.BaseDirectory, ".timefall", "active-run.json"));

    public void Run()
    {
        var eventLog = new List<GameEvent>();
        var initialState = GameState.CreateInitial(_content.CardDefinitions, _content.DeckDefinitions, _content.RewardCardPool, _content.EnemyDefinitions, _content.Zone1SpawnTable);
        var hasActiveSave = _saveRepository.TryLoad(_content, out var savedState);
        var session = new GameSession(initialState, hasActiveSave ? savedState : null);
        var state = session.State;

        Console.WriteLine("Timefall CLI playtest harness. Type 'help' for commands.");

        CliRenderer.RenderState(state, eventLog, _content.CardDefinitions);

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine();
            if (input is null)
            {
                break;
            }

            if (!CliCommandParser.TryParse(input, out var command, out var parseError))
            {
                Console.WriteLine(parseError);
                continue;
            }

            if (command.IsExit)
            {
                break;
            }

            if (command.View is { } view)
            {
                RenderView(view, session.State, eventLog);
                continue;
            }

            state = session.State;
            var action = ResolveContextualAction(command, state) ?? command.Action;
            if (action is ContinueRunAction)
            {
                if (savedState is null)
                {
                    Console.WriteLine("No active run save to continue.");
                    continue;
                }
            }

            if (action is StartRunAction && state.SelectedDeckId is null)
            {
                Console.WriteLine("Select a deck first using 'select <deckId|index>'.");
                continue;
            }
            if (action is EnterSandboxModeAction && state.Phase != GamePhase.MainMenu)
            {
                Console.WriteLine("Sandbox can only be entered from Main Menu.");
                continue;
            }
            if (action is OpenDeckEditAction && state.SelectedDeckId is null)
            {
                Console.WriteLine("No deck selected. Use 'select-deck' then 'select <deckId|index>' first.");
                continue;
            }
            if (action is null)
            {
                Console.WriteLine("Command could not be resolved in current state.");
                continue;
            }
            if (!IsSandboxActionAvailable(action, state))
            {
                Console.WriteLine($"Command '{command.Name}' is not available during phase {state.Phase}.");
                continue;
            }
            if (action is ToggleSandboxLoadoutCardAction && state.Phase is not (GamePhase.SandboxDeckEdit or GamePhase.SandboxPostCombat))
            {
                Console.WriteLine("Loadout can only be edited in sandbox setup.");
                continue;
            }
            if (action is DiscardOverflowAction discardOverflowAction && !TryValidateOverflowDiscard(discardOverflowAction, state, out var overflowError))
            {
                Console.WriteLine(overflowError);
                continue;
            }
            var previousState = state;
            var newEvents = session.ApplyPlayerAction(action);
            state = session.State;
            var rejection = newEvents.OfType<PlayCardRejected>().LastOrDefault();
            if (rejection is not null)
            {
                Console.WriteLine($"Play rejected: {rejection.Reason} ({rejection.Message})");
                continue;
            }

            if (ReferenceEquals(previousState, state) && newEvents.Count == 0)
            {
                Console.WriteLine("Action rejected by game rules for current phase/state.");
                continue;
            }

            eventLog.AddRange(newEvents);
            PersistStableRunState(previousState, state, newEvents);
            hasActiveSave = _saveRepository.Exists();
            savedState = hasActiveSave && _saveRepository.TryLoad(_content, out var loadedAfterPersistence)
                ? loadedAfterPersistence
                : null;
            eventLog.AddRange(session.SetSavedRunState(savedState));
            state = session.State;
            CliRenderer.RenderState(state, eventLog, _content.CardDefinitions);
        }
    }

    private void PersistStableRunState(GameState previousState, GameState state, IReadOnlyList<GameEvent> newEvents)
    {
        var transition = DeterminePersistenceTransition(previousState, state, newEvents);
        if (transition == PersistenceTransition.Save)
        {
            _saveRepository.Save(state);
        }
        else if (transition == PersistenceTransition.Delete)
        {
            _saveRepository.Delete();
        }
    }

    internal static PersistenceTransition DeterminePersistenceTransition(GameState previousState, GameState state, IReadOnlyList<GameEvent> newEvents)
    {
        if (previousState.Phase == GamePhase.Combat && state.Phase == GamePhase.MainMenu)
        {
            return PersistenceTransition.Delete;
        }

        var rewardResolved = newEvents.OfType<RewardChosen>().Any() || newEvents.OfType<RewardSkipped>().Any();
        if (rewardResolved)
        {
            var sourceNodeId = previousState.Reward?.SourceNodeId;
            var sourceType = sourceNodeId is null || !previousState.Map.Graph.TryGetNode(sourceNodeId.Value, out var sourceNode)
                ? (NodeType?)null
                : sourceNode?.Type;

            if (sourceType == NodeType.Boss && state.Phase == GamePhase.MainMenu)
            {
                return PersistenceTransition.Delete;
            }

            if (state.Phase == GamePhase.MapExploration)
            {
                return PersistenceTransition.Save;
            }
        }

        var resolvedStableNodeTypes = new HashSet<NodeType> { NodeType.Rest, NodeType.Shop, NodeType.Event };
        var resolvedStableEncounter = newEvents
            .OfType<EncounterResolved>()
            .Any(e => resolvedStableNodeTypes.Contains(e.NodeType));

        return resolvedStableEncounter && state.Phase == GamePhase.MapExploration
            ? PersistenceTransition.Save
            : PersistenceTransition.None;
    }


    private static bool TryValidateOverflowDiscard(DiscardOverflowAction action, GameState state, out string error)
    {
        if (state.Phase != GamePhase.Combat || state.Combat is null || !state.Combat.NeedsOverflowDiscard)
        {
            error = "No overflow discard is currently required.";
            return false;
        }

        if (action.Indexes.Length != state.Combat.RequiredOverflowDiscardCount)
        {
            error = $"Overflow discard requires exactly {state.Combat.RequiredOverflowDiscardCount} index(es).";
            return false;
        }

        var uniqueCount = action.Indexes.Distinct().Count();
        if (uniqueCount != action.Indexes.Length)
        {
            error = "Overflow discard indexes must be unique.";
            return false;
        }

        if (action.Indexes.Any(i => i < 0 || i >= state.Combat.Player.Deck.Hand.Count))
        {
            error = $"Overflow discard indexes must be within 0..{state.Combat.Player.Deck.Hand.Count - 1}.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal static GameAction? ResolveContextualAction(ParsedCommand command, GameState state)
    {
        if (command.Action is ReturnToMainMenuAction)
        {
            return state.Phase switch
            {
                GamePhase.NewRunMenu => new ReturnToMainMenuAction(),
                GamePhase.DeckSelect or GamePhase.DeckEdit => new ReturnToNewRunMenuAction(),
                GamePhase.SandboxDeckSelect or GamePhase.SandboxDeckEdit or GamePhase.SandboxEnemySelect or GamePhase.SandboxPostCombat => new LeaveSandboxModeAction(),
                _ => command.Action,
            };
        }

        if (command.Argument == "change-deck")
        {
            return new LeaveSandboxModeAction();
        }

        if (command.Action is StartRunAction && state.Mode == GameMode.Sandbox)
        {
            return new StartSandboxCombatAction();
        }

        if (command.Argument is null)
        {
            return null;
        }

        if (int.TryParse(command.Argument, out var index))
        {
            if (state.Phase == GamePhase.DeckSelect)
            {
                if (index < 0 || index >= state.AvailableDeckIds.Count)
                {
                    return null;
                }

                return new SelectDeckAction(state.AvailableDeckIds[index]);
            }

            if (state.Phase is GamePhase.SandboxDeckSelect or GamePhase.SandboxDeckEdit or GamePhase.SandboxEnemySelect or GamePhase.SandboxPostCombat)
            {
                if (command.Name == "select-sandbox-deck")
                {
                    if (index < 0 || index >= state.AvailableDeckIds.Count)
                    {
                        return null;
                    }

                    return new SelectSandboxDeckAction(state.AvailableDeckIds[index]);
                }

                if (command.Name == "select-enemy")
                {
                    var enemyIds = SandboxEnemyCatalog.GetEnemyIds(state.EnemyDefinitions);
                    if (index < 0 || index >= enemyIds.Length)
                    {
                        return null;
                    }

                    return new SelectSandboxEnemyAction(enemyIds[index]);
                }
            }

            if (command.Action is MoveToNodeAction && state.Phase == GamePhase.MapExploration)
            {
                var adjacentNodes = state.Map.Graph.GetNeighbors(state.Map.CurrentNodeId).ToArray();
                if (index >= 0 && index < adjacentNodes.Length)
                {
                    return new MoveToNodeAction(adjacentNodes[index]);
                }
            }

            if (state.Phase == GamePhase.RewardSelection && state.Reward is { } reward)
            {
                if (index < 0 || index >= reward.CardOptions.Count)
                {
                    return null;
                }

                return new ChooseRewardCardAction(reward.CardOptions[index]);
            }

            if (state.Phase == GamePhase.DeckEdit && state.SelectedDeckId is { } selectedDeckId && state.DeckDefinitions.TryGetValue(selectedDeckId, out var deckDefinition))
            {
                var orderedPool = deckDefinition.RewardPoolCardIds.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
                if (index < 0 || index >= orderedPool.Length)
                {
                    return null;
                }

                var deckEditCardId = orderedPool[index];
                return command.Action switch
                {
                    EnableRewardPoolCardAction => new EnableRewardPoolCardAction(deckEditCardId),
                    DisableRewardPoolCardAction => new DisableRewardPoolCardAction(deckEditCardId),
                    ToggleRewardPoolCardAction => new ToggleRewardPoolCardAction(deckEditCardId),
                    _ => command.Action,
                };
            }

            if (state.Mode == GameMode.Sandbox &&
                state.Sandbox?.SelectedDeckId is { } sandboxDeckId &&
                state.DeckDefinitions.TryGetValue(sandboxDeckId, out var sandboxDeck) &&
                (command.Name == "equip" || command.Name == "unequip"))
            {
                var allowed = sandboxDeck.StartingCombatDeckCardIds
                    .Distinct()
                    .OrderBy(id => id.Value, StringComparer.Ordinal)
                    .ToArray();
                if (index < 0 || index >= allowed.Length)
                {
                    return null;
                }

                return new ToggleSandboxLoadoutCardAction(allowed[index]);
            }

            if (index < 0 || index >= state.RunDeck.Count)
            {
                return null;
            }

            var cardId = state.RunDeck[index].DefinitionId;
            if (state.Phase == GamePhase.MapExploration && state.NodeInteraction?.NodeType == Game.Core.Map.NodeType.Shop)
            {
                return new UseShopRemovalAction(cardId);
            }

            return new RemoveCardFromDeckAction(cardId);
        }

        if (command.Action is MoveToNodeAction && state.Phase == GamePhase.MapExploration)
        {
            var displayIds = MapDisplayIds.Create(state.Map);
            if (displayIds.TryResolve(command.Argument, out var displayNodeId))
            {
                return new MoveToNodeAction(displayNodeId);
            }
        }

        var id = new CardId(command.Argument);
        if (command.Action is RemoveCardFromDeckAction)
        {
            if (state.Phase == GamePhase.MapExploration && state.NodeInteraction?.NodeType == Game.Core.Map.NodeType.Shop)
            {
                return new UseShopRemovalAction(id);
            }

            return new RemoveCardFromDeckAction(id);
        }

        if (command.Action is ChooseRewardCardAction)
        {
            return new ChooseRewardCardAction(id);
        }

        if (command.Action is SelectDeckAction)
        {
            return new SelectDeckAction(command.Argument);
        }

        if (command.Action is SelectSandboxDeckAction)
        {
            return new SelectSandboxDeckAction(command.Argument);
        }

        if (command.Action is ToggleSandboxLoadoutCardAction toggleSandboxLoadoutCardAction)
        {
            if (command.Argument.StartsWith("-", StringComparison.Ordinal))
            {
                return null;
            }

            var normalizedCardId = toggleSandboxLoadoutCardAction.CardId;
            if (state.Mode == GameMode.Sandbox &&
                state.Sandbox?.SelectedDeckId is { } sandboxDeckId &&
                state.DeckDefinitions.TryGetValue(sandboxDeckId, out var sandboxDeck))
            {
                var allowed = sandboxDeck.StartingCombatDeckCardIds.ToHashSet();
                if (!allowed.Contains(normalizedCardId))
                {
                    return null;
                }
            }

            var isEquipped = state.Sandbox?.EquippedCardIds.Contains(normalizedCardId) == true;
            if (command.Name == "equip" && isEquipped)
            {
                return null;
            }

            if (command.Name == "unequip" && !isEquipped)
            {
                return null;
            }

            return toggleSandboxLoadoutCardAction;
        }

        if (command.Action is SelectSandboxEnemyAction)
        {
            return new SelectSandboxEnemyAction(command.Argument);
        }

        if (command.Action is EnableRewardPoolCardAction)
        {
            return new EnableRewardPoolCardAction(id);
        }

        if (command.Action is DisableRewardPoolCardAction)
        {
            return new DisableRewardPoolCardAction(id);
        }

        if (command.Action is ToggleRewardPoolCardAction)
        {
            return new ToggleRewardPoolCardAction(id);
        }

        return command.Action;
    }

    private void RenderView(CliView view, GameState state, IReadOnlyList<GameEvent> eventLog)
    {
        switch (view)
        {
            case CliView.Help:
                CliRenderer.RenderHelp(state.Phase);
                break;
            case CliView.State:
            case CliView.Status:
                CliRenderer.RenderState(state, eventLog, _content.CardDefinitions);
                break;
            case CliView.Map:
                CliRenderer.RenderMap(state);
                break;
            case CliView.Zone:
                CliRenderer.RenderZone(state);
                break;
            case CliView.Hand:
                CliRenderer.RenderHand(state, _content.CardDefinitions);
                break;
            case CliView.Reward:
                if (state.Reward is null)
                {
                    Console.WriteLine("No active reward.");
                }
                else
                {
                    CliRenderer.RenderState(state, eventLog, _content.CardDefinitions);
                }
                break;
            case CliView.Deck:
                CliRenderer.RenderDeck(state, _content.CardDefinitions);
                break;
            case CliView.Enabled:
                CliRenderer.RenderRewardPool(state, _content.CardDefinitions, enabled: true);
                break;
            case CliView.Disabled:
                CliRenderer.RenderRewardPool(state, _content.CardDefinitions, enabled: false);
                break;
            case CliView.Decks:
                CliRenderer.RenderDecks(state);
                break;
            case CliView.Discard:
                CliRenderer.RenderDiscard(state, _content.CardDefinitions);
                break;
            case CliView.Shop:
                Console.WriteLine(state.NodeInteraction?.NodeType == Game.Core.Map.NodeType.Shop
                    ? "At shop: use 'remove <cardId|deckIndex>' to remove one card."
                    : "Not currently at a shop node.");
                break;
            case CliView.SandboxCards:
                CliRenderer.RenderSandboxCards(state, _content.CardDefinitions);
                break;
            case CliView.SandboxEnemies:
                CliRenderer.RenderSandboxEnemies(state);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(view), view, null);
        }
    }

    private static bool IsSandboxActionAvailable(GameAction action, GameState state)
    {
        return action switch
        {
            EnterSandboxModeAction => state.Phase == GamePhase.MainMenu,
            LeaveSandboxModeAction => state.Mode == GameMode.Sandbox,
            SelectSandboxDeckAction => state.Phase is GamePhase.SandboxDeckSelect or GamePhase.SandboxDeckEdit or GamePhase.SandboxEnemySelect or GamePhase.SandboxPostCombat,
            OpenSandboxDeckEditAction => state.Phase is GamePhase.SandboxEnemySelect or GamePhase.SandboxPostCombat or GamePhase.SandboxDeckEdit,
            ToggleSandboxLoadoutCardAction or ClearSandboxLoadoutAction => state.Phase is GamePhase.SandboxDeckEdit or GamePhase.SandboxPostCombat,
            OpenSandboxEnemySelectAction => state.Phase is GamePhase.SandboxDeckEdit or GamePhase.SandboxPostCombat or GamePhase.SandboxEnemySelect,
            SelectSandboxEnemyAction => state.Phase is GamePhase.SandboxEnemySelect or GamePhase.SandboxPostCombat,
            StartSandboxCombatAction => state.Phase is GamePhase.SandboxEnemySelect or GamePhase.SandboxPostCombat,
            RepeatSandboxCombatAction => state.Phase == GamePhase.SandboxPostCombat,
            _ => true,
        };
    }
}
