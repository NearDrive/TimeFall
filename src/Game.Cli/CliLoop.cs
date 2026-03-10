using Game.Core.Cards;
using Game.Core.Game;
using Game.Data.Content;

namespace Game.Cli;

internal sealed class CliLoop
{
    private readonly GameContentBundle _content = StaticGameContentProvider.LoadDefault();

    public void Run()
    {
        var state = GameState.Initial;
        var eventLog = new List<GameEvent>();

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
                RenderView(view, state, eventLog);
                continue;
            }

            var action = command.Action ?? ResolveContextualAction(command, state);
            if (action is DiscardOverflowAction discardOverflowAction && !TryValidateOverflowDiscard(discardOverflowAction, state, out var overflowError))
            {
                Console.WriteLine(overflowError);
                continue;
            }
            if (action is null)
            {
                Console.WriteLine("Command could not be resolved in current state.");
                continue;
            }

            var previousState = state;
            (state, var newEvents) = GameReducer.Reduce(state, action);
            if (ReferenceEquals(previousState, state) && newEvents.Count == 0)
            {
                Console.WriteLine("Action rejected by game rules for current phase/state.");
                continue;
            }

            eventLog.AddRange(newEvents);
            CliRenderer.RenderState(state, eventLog, _content.CardDefinitions);
        }
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
        if (command.Argument is null)
        {
            return null;
        }

        if (int.TryParse(command.Argument, out var index))
        {
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

        return command.Action;
    }

    private void RenderView(CliView view, GameState state, IReadOnlyList<GameEvent> eventLog)
    {
        switch (view)
        {
            case CliView.Help:
                CliRenderer.RenderHelp();
                break;
            case CliView.State:
            case CliView.Status:
                CliRenderer.RenderState(state, eventLog, _content.CardDefinitions);
                break;
            case CliView.Map:
                CliRenderer.RenderMap(state);
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
            case CliView.Discard:
                CliRenderer.RenderDiscard(state, _content.CardDefinitions);
                break;
            case CliView.Shop:
                Console.WriteLine(state.NodeInteraction?.NodeType == Game.Core.Map.NodeType.Shop
                    ? "At shop: use 'remove <cardId|deckIndex>' to remove one card."
                    : "Not currently at a shop node.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(view), view, null);
        }
    }
}
