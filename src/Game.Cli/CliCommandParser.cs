using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;

namespace Game.Cli;

internal enum CliView
{
    Help,
    State,
    Map,
    Hand,
    Reward,
    Deck,
    Discard,
    Status,
    Shop,
}

internal sealed record ParsedCommand(GameAction? Action, CliView? View, string? Argument = null, bool IsExit = false);

internal static class CliCommandParser
{
    internal const int DefaultSeed = 1337;

    public static bool TryParse(string input, out ParsedCommand command, out string error)
    {
        command = new ParsedCommand(null, null);
        error = string.Empty;

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = "Enter a command. Use 'help' for a list.";
            return false;
        }

        var name = parts[0].ToLowerInvariant();
        switch (name)
        {
            case "quit":
            case "exit":
                command = new ParsedCommand(null, null, IsExit: true);
                return true;
            case "help": command = new ParsedCommand(null, CliView.Help); return true;
            case "state": command = new ParsedCommand(null, CliView.State); return true;
            case "status": command = new ParsedCommand(null, CliView.Status); return true;
            case "map": command = new ParsedCommand(null, CliView.Map); return true;
            case "hand": command = new ParsedCommand(null, CliView.Hand); return true;
            case "reward": command = new ParsedCommand(null, CliView.Reward); return true;
            case "deck": command = new ParsedCommand(null, CliView.Deck); return true;
            case "discard":
                if (parts.Length < 2)
                {
                    error = "Usage: discard <index...>";
                    return false;
                }

                var indexes = new int[parts.Length - 1];
                for (var i = 1; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i], out var index))
                    {
                        error = "Usage: discard <index...>";
                        return false;
                    }

                    indexes[i - 1] = index;
                }

                command = new ParsedCommand(new DiscardOverflowAction(indexes), null);
                return true;
            case "discardpile": command = new ParsedCommand(null, CliView.Discard); return true;
            case "shop": command = new ParsedCommand(null, CliView.Shop); return true;
            case "start":
                if (parts.Length == 1)
                {
                    command = new ParsedCommand(new StartRunAction(DefaultSeed), null);
                    return true;
                }

                if (parts.Length != 2 || !int.TryParse(parts[1], out var seed))
                {
                    error = "Usage: start [seed]";
                    return false;
                }

                command = new ParsedCommand(new StartRunAction(seed), null);
                return true;
            case "move":
                if (parts.Length != 2)
                {
                    error = "Usage: move <nodeId>";
                    return false;
                }

                command = new ParsedCommand(new MoveToNodeAction(new NodeId(parts[1])), null);
                return true;
            case "play":
                if (parts.Length < 2 || parts.Length > 3 || !int.TryParse(parts[1], out var handIndex))
                {
                    error = "Usage: play <index> [target]";
                    return false;
                }

                command = new ParsedCommand(new PlayCardAction(handIndex), null);
                return true;
            case "end": command = new ParsedCommand(new EndTurnAction(), null); return true;
            case "choose":
                if (parts.Length != 2)
                {
                    error = "Usage: choose <cardId|optionIndex>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new ChooseRewardCardAction(new CardId(parts[1])), null, parts[1]);
                return true;
            case "skip": command = new ParsedCommand(new SkipRewardAction(), null); return true;
            case "rest": command = new ParsedCommand(new UseRestAction(RestOption.Heal), null); return true;
            case "remove":
                if (parts.Length != 2)
                {
                    error = "Usage: remove <cardId|deckIndex>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new RemoveCardFromDeckAction(new CardId(parts[1])), null, parts[1]);
                return true;
            default:
                error = $"Unknown command '{name}'. Use 'help'.";
                return false;
        }
    }
}
