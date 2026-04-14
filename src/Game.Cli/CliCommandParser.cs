using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;

namespace Game.Cli;

internal enum CliView
{
    Help,
    State,
    Map,
    Zone,
    Hand,
    Reward,
    Deck,
    Enabled,
    Disabled,
    Decks,
    Discard,
    Status,
    Shop,
    SandboxCards,
    SandboxEnemies,
}

internal sealed record ParsedCommand(GameAction? Action, CliView? View, string? Argument = null, bool IsExit = false, string? Name = null);

internal static class CliCommandParser
{
    internal const int DefaultSeed = 1337;

    public static bool TryParse(string input, out ParsedCommand command, out string error)
    {
        command = new ParsedCommand(null, null, Name: string.Empty);
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
                command = new ParsedCommand(null, null, IsExit: true, Name: name);
                return true;
            case "help": command = new ParsedCommand(null, CliView.Help, Name: name); return true;
            case "state": command = new ParsedCommand(null, CliView.State, Name: name); return true;
            case "status": command = new ParsedCommand(null, CliView.Status, Name: name); return true;
            case "map": command = new ParsedCommand(null, CliView.Map, Name: name); return true;
            case "zone": command = new ParsedCommand(null, CliView.Zone, Name: name); return true;
            case "hand": command = new ParsedCommand(null, CliView.Hand, Name: name); return true;
            case "reward": command = new ParsedCommand(null, CliView.Reward, Name: name); return true;
            case "deck": command = new ParsedCommand(null, CliView.Deck, Name: name); return true;
            case "enabled": command = new ParsedCommand(null, CliView.Enabled, Name: name); return true;
            case "disabled": command = new ParsedCommand(null, CliView.Disabled, Name: name); return true;
            case "decks": command = new ParsedCommand(null, CliView.Decks, Name: name); return true;
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

                command = new ParsedCommand(new DiscardOverflowAction(indexes), null, Name: name);
                return true;
            case "discardpile": command = new ParsedCommand(null, CliView.Discard, Name: name); return true;
            case "shop": command = new ParsedCommand(null, CliView.Shop, Name: name); return true;
            case "start":
                if (parts.Length == 1)
                {
                    command = new ParsedCommand(new StartRunAction(DefaultSeed), null, Name: name);
                    return true;
                }

                if (parts.Length != 2 || !int.TryParse(parts[1], out var seed))
                {
                    error = "Usage: start [seed]";
                    return false;
                }

                command = new ParsedCommand(new StartRunAction(seed), null, Name: name);
                return true;
            case "continue":
                command = new ParsedCommand(new ContinueRunAction(GameState.Initial), null, Name: name);
                return true;
            case "new":
                command = new ParsedCommand(new EnterNewRunMenuAction(), null, Name: name);
                return true;
            case "sandbox":
                if (parts.Length == 1)
                {
                    command = new ParsedCommand(new EnterSandboxModeAction(DefaultSeed), null, Name: name);
                    return true;
                }

                if (parts.Length != 2 || !int.TryParse(parts[1], out var sandboxSeed))
                {
                    error = "Usage: sandbox [seed]";
                    return false;
                }

                command = new ParsedCommand(new EnterSandboxModeAction(sandboxSeed), null, Name: name);
                return true;
            case "back":
                command = new ParsedCommand(new ReturnToMainMenuAction(), null, Name: name);
                return true;
            case "select-deck":
                command = new ParsedCommand(new OpenDeckSelectAction(), null, Name: name);
                return true;
            case "edit-deck":
                command = new ParsedCommand(new OpenDeckEditAction(), null, Name: name);
                return true;
            case "enable-all":
                command = new ParsedCommand(new EnableAllRewardPoolCardsAction(), null, Name: name);
                return true;
            case "disable-all":
                command = new ParsedCommand(new DisableAllRewardPoolCardsAction(), null, Name: name);
                return true;
            case "autofill-min":
                command = new ParsedCommand(new AutofillMinRewardPoolAction(), null, Name: name);
                return true;
            case "autofill-max":
                command = new ParsedCommand(new AutofillMaxRewardPoolAction(), null, Name: name);
                return true;
            case "done":
                command = new ParsedCommand(new ConfirmRewardPoolAction(), null, Name: name);
                return true;
            case "enable":
            case "disable":
            case "toggle":
                if (parts.Length != 2)
                {
                    error = $"Usage: {name} <cardId|index>";
                    return false;
                }

                GameAction? deckEditAction = null;
                if (!int.TryParse(parts[1], out _))
                {
                    var cardId = new CardId(parts[1]);
                    deckEditAction = name switch
                    {
                        "enable" => new EnableRewardPoolCardAction(cardId),
                        "disable" => new DisableRewardPoolCardAction(cardId),
                        _ => new ToggleRewardPoolCardAction(cardId),
                    };
                }

                command = new ParsedCommand(deckEditAction, null, parts[1], Name: name);
                return true;
            case "select":
                if (parts.Length != 2)
                {
                    error = "Usage: select <deckId|index>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new SelectDeckAction(parts[1]), null, parts[1], Name: name);
                return true;
            case "sandbox-decks":
                command = new ParsedCommand(null, CliView.Decks, Name: name);
                return true;
            case "select-sandbox-deck":
                if (parts.Length != 2)
                {
                    error = "Usage: select-sandbox-deck <deckId|index>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new SelectSandboxDeckAction(parts[1]), null, parts[1], Name: name);
                return true;
            case "cards":
                command = new ParsedCommand(null, CliView.SandboxCards, Name: name);
                return true;
            case "equip":
            case "unequip":
                if (parts.Length != 2)
                {
                    error = $"Usage: {name} <cardId|index>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new ToggleSandboxLoadoutCardAction(new CardId(parts[1])), null, parts[1], Name: name);
                return true;
            case "clear-loadout":
                command = new ParsedCommand(new ClearSandboxLoadoutAction(), null, Name: name);
                return true;
            case "enemies":
                command = new ParsedCommand(null, CliView.SandboxEnemies, Name: name);
                return true;
            case "select-enemy":
                if (parts.Length != 2)
                {
                    error = "Usage: select-enemy <enemyId|index>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new SelectSandboxEnemyAction(parts[1]), null, parts[1], Name: name);
                return true;
            case "setup":
                command = new ParsedCommand(new OpenSandboxDeckEditAction(), null, Name: name);
                return true;
            case "change-enemy":
                command = new ParsedCommand(new OpenSandboxEnemySelectAction(), null, Name: name);
                return true;
            case "change-deck":
                command = new ParsedCommand(null, null, "change-deck", Name: name);
                return true;
            case "repeat":
                command = new ParsedCommand(new RepeatSandboxCombatAction(), null, Name: name);
                return true;
            case "move":
                if (parts.Length != 2)
                {
                    error = "Usage: move <displayId|adjacentIndex>";
                    return false;
                }

                command = new ParsedCommand(new MoveToNodeAction(new NodeId(parts[1])), null, parts[1], Name: name);
                return true;
            case "play":
                if (parts.Length < 2 || parts.Length > 3 || !int.TryParse(parts[1], out var handIndex))
                {
                    error = "Usage: play <index> [targetIndex]";
                    return false;
                }

                int? targetIndex = null;
                if (parts.Length == 3)
                {
                    if (!int.TryParse(parts[2], out var parsedTargetIndex))
                    {
                        error = "Usage: play <index> [targetIndex]";
                        return false;
                    }

                    targetIndex = parsedTargetIndex;
                }

                command = new ParsedCommand(new PlayCardAction(handIndex, targetIndex), null, Name: name);
                return true;
            case "end": command = new ParsedCommand(new EndTurnAction(), null, Name: name); return true;
            case "choose":
                if (parts.Length != 2)
                {
                    error = "Usage: choose <cardId|optionIndex>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new ChooseRewardCardAction(new CardId(parts[1])), null, parts[1], Name: name);
                return true;
            case "skip": command = new ParsedCommand(new SkipRewardAction(), null, Name: name); return true;
            case "rest": command = new ParsedCommand(new UseRestAction(RestOption.Heal), null, Name: name); return true;
            case "remove":
                if (parts.Length != 2)
                {
                    error = "Usage: remove <cardId|deckIndex>";
                    return false;
                }

                command = new ParsedCommand(int.TryParse(parts[1], out _)
                    ? null
                    : new RemoveCardFromDeckAction(new CardId(parts[1])), null, parts[1], Name: name);
                return true;
            default:
                error = $"Unknown command '{name}'. Use 'help'.";
                return false;
        }
    }
}
