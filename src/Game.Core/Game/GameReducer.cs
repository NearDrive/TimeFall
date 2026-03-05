using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;

namespace Game.Core.Game;

public static class GameReducer
{
    private static readonly CardId StrikeCardId = new("strike");
    private const int StrikeDamage = 4;

    public static (GameState NewState, IReadOnlyList<GameEvent> Events) Reduce(GameState state, GameAction action)
    {
        return action switch
        {
            StartRunAction startRunAction => StartRun(state, startRunAction),
            BeginCombatAction beginCombatAction => BeginCombat(state, beginCombatAction),
            PlayCardAction playCardAction => PlayCard(state, playCardAction),
            EndTurnAction endTurnAction => EndTurn(state, endTurnAction),
            DiscardOverflowAction discardOverflowAction => DiscardOverflow(state, discardOverflowAction),
            _ => (state, Array.Empty<GameEvent>()),
        };
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) StartRun(GameState state, StartRunAction action)
    {
        _ = state;

        var newState = new GameState(GamePhase.DeckSelect, GameRng.FromSeed(action.Seed), null);
        var events = new GameEvent[] { new RunStarted(action.Seed) };

        return (newState, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) BeginCombat(GameState state, BeginCombatAction action)
    {
        _ = action;

        var combatState = CreateCombatState();
        var drawResult = HandManager.Draw(combatState, state.Rng, 5);
        var events = new List<GameEvent> { new EnteredCombat() };
        events.AddRange(drawResult.Events);
        events.AddRange(drawResult.DrawnCards.Select(c => new CardDrawn(c)));

        return (state with { Phase = GamePhase.Combat, Combat = drawResult.CombatState, Rng = drawResult.Rng }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) PlayCard(GameState state, PlayCardAction action)
    {
        if (state.Phase != GamePhase.Combat || state.Combat is null || state.Combat.NeedsOverflowDiscard)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (state.Combat.TurnOwner != TurnOwner.Player)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (action.HandIndex < 0 || action.HandIndex >= state.Combat.Player.Deck.Hand.Count)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var card = state.Combat.Player.Deck.Hand[action.HandIndex];
        if (card.DefinitionId != StrikeCardId)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var combatState = Clone(state.Combat);
        combatState.Player.Deck.Hand.RemoveAt(action.HandIndex);
        combatState.Player.Deck.DiscardPile.Add(card);

        var hitResult = DamageSystem.ApplyHit(combatState.Enemy, StrikeDamage);
        combatState = combatState with { Enemy = hitResult.UpdatedEntity };

        var events = new GameEvent[]
        {
            new CardDiscarded(card),
            new PlayerStrikePlayed(card, StrikeDamage, hitResult.UpdatedEntity.HP),
        };

        return (state with { Combat = combatState }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) EndTurn(GameState state, EndTurnAction action)
    {
        _ = action;

        if (state.Phase != GamePhase.Combat || state.Combat is null || state.Combat.NeedsOverflowDiscard)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (state.Combat.TurnOwner == TurnOwner.Enemy)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var combatState = state.Combat with { TurnOwner = TurnOwner.Enemy };
        var events = new List<GameEvent> { new TurnEnded(TurnOwner.Enemy) };

        var enemyResult = EnemyController.ExecuteTurn(combatState, state.Rng);
        combatState = enemyResult.CombatState;
        events.AddRange(enemyResult.Events);

        combatState = combatState with { TurnOwner = TurnOwner.Player };
        events.Add(new TurnEnded(TurnOwner.Player));

        var drawResult = HandManager.Draw(combatState, enemyResult.Rng, 1);
        combatState = drawResult.CombatState;
        events.AddRange(drawResult.Events);
        events.AddRange(drawResult.DrawnCards.Select(c => new CardDrawn(c)));

        return (state with { Combat = combatState, Rng = drawResult.Rng }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) DiscardOverflow(GameState state, DiscardOverflowAction action)
    {
        if (state.Phase != GamePhase.Combat || state.Combat is null || !state.Combat.NeedsOverflowDiscard)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var requiredCount = state.Combat.RequiredOverflowDiscardCount;
        if (action.Indexes.Length != requiredCount)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var uniqueIndexes = action.Indexes.Distinct().ToArray();
        if (uniqueIndexes.Length != action.Indexes.Length)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (uniqueIndexes.Any(i => i < 0 || i >= state.Combat.Player.Deck.Hand.Count))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var discardedCards = uniqueIndexes.OrderBy(i => i).Select(i => state.Combat.Player.Deck.Hand[i]).ToArray();
        var combatState = HandManager.ApplyDiscard(state.Combat, uniqueIndexes);
        var events = discardedCards.Select(c => (GameEvent)new CardDiscarded(c)).ToArray();

        return (state with { Combat = combatState }, events);
    }

    private static CombatState CreateCombatState()
    {
        var drawPile = new List<CardInstance>
        {
            new(new CardId("strike")),
            new(new CardId("defend")),
            new(new CardId("strike")),
            new(new CardId("defend")),
            new(new CardId("focus")),
            new(new CardId("strike")),
            new(new CardId("defend")),
            new(new CardId("focus")),
            new(new CardId("strike")),
            new(new CardId("defend")),
        };

        var player = new CombatEntity(
            EntityId: "player",
            HP: 80,
            MaxHP: 80,
            Armor: 0,
            Resources: new Dictionary<ResourceType, int> { [ResourceType.Energy] = 3 },
            Deck: new DeckState(drawPile, new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>()));

        var enemyDeck = new List<CardInstance>
        {
            new(new CardId("attack")),
            new(new CardId("attack")),
            new(new CardId("attack")),
            new(new CardId("defend")),
        };

        var enemy = new CombatEntity(
            EntityId: "enemy-1",
            HP: 30,
            MaxHP: 30,
            Armor: 0,
            Resources: new Dictionary<ResourceType, int>(),
            Deck: new DeckState(enemyDeck, new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>()));

        return new CombatState(TurnOwner.Player, 0, player, enemy, false, 0);
    }

    private static CombatState Clone(CombatState combatState)
    {
        var playerDeck = combatState.Player.Deck;
        var enemyDeck = combatState.Enemy.Deck;

        return combatState with
        {
            Player = combatState.Player with
            {
                Resources = new Dictionary<ResourceType, int>(combatState.Player.Resources),
                Deck = playerDeck with
                {
                    DrawPile = new List<CardInstance>(playerDeck.DrawPile),
                    Hand = new List<CardInstance>(playerDeck.Hand),
                    DiscardPile = new List<CardInstance>(playerDeck.DiscardPile),
                    BurnPile = new List<CardInstance>(playerDeck.BurnPile),
                },
            },
            Enemy = combatState.Enemy with
            {
                Resources = new Dictionary<ResourceType, int>(combatState.Enemy.Resources),
                Deck = enemyDeck with
                {
                    DrawPile = new List<CardInstance>(enemyDeck.DrawPile),
                    Hand = new List<CardInstance>(enemyDeck.Hand),
                    DiscardPile = new List<CardInstance>(enemyDeck.DiscardPile),
                    BurnPile = new List<CardInstance>(enemyDeck.BurnPile),
                },
            },
        };
    }
}
