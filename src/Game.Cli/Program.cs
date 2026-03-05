using Game.Core.Cards;
using Game.Core.Game;

var state = GameState.Initial;
var eventLog = new List<GameEvent>();

Console.WriteLine("Timefall CLI combat (start <seed>, hand, play <index>, end, discard <i1> <i2> ..., quit)");
RenderState(state, eventLog);

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (input is null)
    {
        break;
    }

    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
    {
        continue;
    }

    var command = parts[0].ToLowerInvariant();

    if (command is "quit" or "exit")
    {
        break;
    }

    if (command == "hand")
    {
        RenderHand(state);
        continue;
    }

    var newEvents = new List<GameEvent>();

    switch (command)
    {
        case "start":
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out var seed))
            {
                Console.WriteLine("Usage: start <seed>");
                continue;
            }

            (state, var startedEvents) = GameReducer.Reduce(state, new StartRunAction(seed));
            newEvents.AddRange(startedEvents);
            (state, var combatEvents) = GameReducer.Reduce(state, new BeginCombatAction());
            newEvents.AddRange(combatEvents);
            break;
        }

        case "play":
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out var index))
            {
                Console.WriteLine("Usage: play <index>");
                continue;
            }

            (state, var events) = GameReducer.Reduce(state, new PlayCardAction(index));
            newEvents.AddRange(events);
            break;
        }

        case "end":
        {
            (state, var events) = GameReducer.Reduce(state, new EndTurnAction());
            newEvents.AddRange(events);
            break;
        }

        case "discard":
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: discard <i1> <i2> ...");
                continue;
            }

            var indexes = new List<int>();
            var valid = true;
            for (var i = 1; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out var idx))
                {
                    valid = false;
                    break;
                }

                indexes.Add(idx);
            }

            if (!valid)
            {
                Console.WriteLine("Usage: discard <i1> <i2> ...");
                continue;
            }

            (state, var events) = GameReducer.Reduce(state, new DiscardOverflowAction(indexes.ToArray()));
            newEvents.AddRange(events);
            break;
        }

        default:
            Console.WriteLine("Unknown command.");
            continue;
    }

    eventLog.AddRange(newEvents);
    RenderState(state, eventLog);
}

static void RenderState(GameState state, IReadOnlyList<GameEvent> eventLog)
{
    if (state.Combat is null)
    {
        Console.WriteLine($"Phase: {state.Phase}");
        Console.WriteLine("No combat active.");
        Console.WriteLine("Event log: (empty)");
        return;
    }

    Console.WriteLine($"Phase: {state.Phase} | Turn: {state.Combat.TurnOwner}");
    Console.WriteLine($"Player HP/Armor: {state.Combat.Player.HP}/{state.Combat.Player.Armor}");
    Console.WriteLine($"Enemy  HP/Armor: {state.Combat.Enemy.HP}/{state.Combat.Enemy.Armor}");
    Console.WriteLine($"Enemy hand size: {state.Combat.Enemy.Deck.Hand.Count}");

    if (state.Combat.NeedsOverflowDiscard)
    {
        Console.WriteLine($"Overflow discard required: {state.Combat.RequiredOverflowDiscardCount}");
    }

    Console.WriteLine("Event log:");
    if (eventLog.Count == 0)
    {
        Console.WriteLine("- (empty)");
        return;
    }

    foreach (var gameEvent in eventLog.TakeLast(10))
    {
        Console.WriteLine($"- {FormatEvent(gameEvent)}");
    }
}

static void RenderHand(GameState state)
{
    if (state.Combat is null)
    {
        Console.WriteLine("No combat active.");
        return;
    }

    Console.WriteLine("Hand:");
    for (var i = 0; i < state.Combat.Player.Deck.Hand.Count; i++)
    {
        var card = state.Combat.Player.Deck.Hand[i];
        Console.WriteLine($"{i}: {GetCardName(card)}");
    }
}

static string FormatEvent(GameEvent gameEvent)
{
    return gameEvent switch
    {
        RunStarted e => $"Run started (seed {e.Seed})",
        EnteredCombat => "Entered combat",
        CardDrawn e => $"Card drawn: {GetCardName(e.Card)}",
        CardDiscarded e => $"Card discarded: {GetCardName(e.Card)}",
        PlayerStrikePlayed e => $"Strike deals {e.Damage} (enemy HP {e.EnemyHpAfterHit})",
        EnemyAttackPlayed e => $"Enemy attack deals {e.Damage} (player HP {e.PlayerHpAfterHit})",
        TurnEnded e => $"Turn ended -> {e.NextTurnOwner}",
        DeckReshuffled => "Deck reshuffled",
        CardBurned e => $"Card burned: {GetCardName(e.Card)}",
        _ => gameEvent.GetType().Name,
    };
}

static string GetCardName(Game.Core.Combat.CardInstance card)
{
    return ContentRegistry.CardDefinitions.TryGetValue(card.DefinitionId, out var definition)
        ? definition.Name
        : card.DefinitionId.Value;
}
