using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Map;
using Game.Core.TimeSystem;
using Game.Core.Rewards;
using NodeId = Game.Core.Map.NodeId;
using System.Collections.Immutable;

namespace Game.Core.Game;

public static class GameReducer
{
    public static (GameState NewState, IReadOnlyList<GameEvent> Events) Reduce(GameState state, GameAction action)
    {
        if (IsBlockedByPendingCombatRequirement(state, action))
        {
            return (state, Array.Empty<GameEvent>());
        }

        return action switch
        {
            StartRunAction startRunAction => StartRun(state, startRunAction),
            BeginCombatAction beginCombatAction => BeginCombat(state, beginCombatAction),
            PlayCardAction playCardAction => PlayCard(state, playCardAction),
            EndTurnAction endTurnAction => EndTurn(state, endTurnAction),
            DiscardOverflowAction discardOverflowAction => DiscardOverflow(state, discardOverflowAction),
            MoveToNodeAction moveToNodeAction => MoveToNode(state, moveToNodeAction),
            ChooseRewardCardAction chooseRewardCardAction => ChooseRewardCard(state, chooseRewardCardAction),
            _ => (state, Array.Empty<GameEvent>()),
        };
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) StartRun(GameState state, StartRunAction action)
    {
        var mapState = SampleMapFactory.CreateDefaultState();
        var newState = new GameState(
            GamePhase.MapExploration,
            GameRng.FromSeed(action.Seed),
            null,
            null,
            state.CardDefinitions,
            mapState,
            TimeState.Create(mapState),
            null,
            ImmutableList<CardInstance>.Empty);
        var events = new GameEvent[] { new RunStarted(action.Seed) };

        return (newState, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) BeginCombat(GameState state, BeginCombatAction action)
    {
        if (state.Combat is not null)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (!IsBeginCombatAllowedPhase(state.Phase))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var combatState = CreateCombatState(action.Blueprint, state.PlayerDeckDiscardPile);
        var drawResult = HandManager.Draw(combatState, state.Rng, 5);
        var events = new List<GameEvent> { new EnteredCombat(null, null) };
        events.AddRange(drawResult.Events);
        events.AddRange(drawResult.DrawnCards.Select(c => new CardDrawn(c)));

        return (state with { Phase = GamePhase.Combat, Combat = drawResult.CombatState, Rng = drawResult.Rng, CardDefinitions = action.CardDefinitions, Reward = null }, events);
    }

    private static bool IsBeginCombatAllowedPhase(GamePhase phase)
    {
        return phase is GamePhase.DeckSelect or GamePhase.MapExploration;
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) PlayCard(GameState state, PlayCardAction action)
    {
        if (state is not { Phase: GamePhase.Combat, Combat: { } combatState })
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (combatState.TurnOwner != TurnOwner.Player)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (action.HandIndex < 0 || action.HandIndex >= combatState.Player.Deck.Hand.Count)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var card = combatState.Player.Deck.Hand[action.HandIndex];
        if (!CardEffectResolver.HasResolvableEffects(card, state.CardDefinitions))
        {
            return (state, Array.Empty<GameEvent>());
        }

        combatState = combatState with
        {
            Player = combatState.Player with
            {
                Deck = combatState.Player.Deck with
                {
                    Hand = combatState.Player.Deck.Hand.RemoveAt(action.HandIndex),
                    DiscardPile = combatState.Player.Deck.DiscardPile.Add(card),
                },
            },
        };

        var resolution = CardEffectResolver.Resolve(combatState, card, TurnOwner.Player, state.CardDefinitions);
        combatState = resolution.CombatState;

        var events = new List<GameEvent>
        {
            new CardDiscarded(card),
        };
        events.AddRange(resolution.Events);

        return ResolveCombatPhase(state with { Combat = combatState }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) EndTurn(GameState state, EndTurnAction action)
    {
        _ = action;

        if (state is not { Phase: GamePhase.Combat, Combat: { } combatState })
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (combatState.TurnOwner == TurnOwner.Enemy)
        {
            return (state, Array.Empty<GameEvent>());
        }

        combatState = combatState with { TurnOwner = TurnOwner.Enemy };
        var events = new List<GameEvent> { new TurnEnded(TurnOwner.Enemy) };

        var enemyResult = EnemyController.ExecuteTurn(combatState, state.Rng, state.CardDefinitions);
        combatState = enemyResult.CombatState;
        events.AddRange(enemyResult.Events);

        if (combatState.Player.HP <= 0)
        {
            return ResolveCombatPhase(state with { Combat = combatState, Rng = enemyResult.Rng }, events);
        }

        combatState = combatState with { TurnOwner = TurnOwner.Player };
        events.Add(new TurnEnded(TurnOwner.Player));

        var playerTurnStart = StartPlayerTurn(combatState, enemyResult.Rng);
        combatState = playerTurnStart.CombatState;
        events.AddRange(playerTurnStart.Events);

        return ResolveCombatPhase(state with { Combat = combatState, Rng = playerTurnStart.Rng }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) MoveToNode(GameState state, MoveToNodeAction action)
    {
        if (state.Phase != GamePhase.MapExploration || state.Time.PlayerCaughtByTime)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (state.Time.CollapsedNodeIds.Contains(action.NodeId))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var traversalResult = MapTraversal.MoveToNode(state.Map, action.NodeId);
        if (!traversalResult.IsSuccess)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var traversal = traversalResult.Value;
        var movedMap = traversal.MapState;
        if (!movedMap.Graph.TryGetNode(action.NodeId, out var node) || node is null)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var events = new List<GameEvent>
        {
            new MovedToNode(action.NodeId),
        };

        if (traversal.EncounterStatus == EncounterLifecycleStatus.Triggered)
        {
            events.Add(new EncounterTriggered(action.NodeId, node.Type));
        }

        var timeAdvance = TimeAdvancer.Advance(state.Time, movedMap.CurrentNodeId);
        events.Add(new TimeAdvanced(timeAdvance.TimeState.CurrentStep));

        foreach (var collapsedNode in timeAdvance.NewlyCollapsedNodes)
        {
            events.Add(new NodeCollapsed(collapsedNode));
        }

        if (timeAdvance.PlayerCaughtThisStep)
        {
            events.Add(new TimeCaughtPlayer(movedMap.CurrentNodeId, timeAdvance.TimeState.CurrentStep));
        }

        var movedState = state with { Map = movedMap, Time = timeAdvance.TimeState };

        if (MapNodeEncounterSelector.IsCombatNode(node.Type) && !movedMap.ResolvedEncounterNodeIds.Contains(action.NodeId))
        {
            var selected = MapNodeEncounterSelector.TrySelect(node.Type, out var encounter);
            if (!selected)
            {
                return (movedState, events);
            }

            var combatState = CreateCombatState(encounter.Blueprint, movedState.PlayerDeckDiscardPile);
            var drawResult = HandManager.Draw(combatState, movedState.Rng, 5);
            events.Add(new EnteredCombat(action.NodeId, node.Type));
            events.AddRange(drawResult.Events);
            events.AddRange(drawResult.DrawnCards.Select(c => new CardDrawn(c)));

            var combatEnteredState = movedState with
            {
                Phase = GamePhase.Combat,
                Combat = drawResult.CombatState,
                Rng = drawResult.Rng,
                CardDefinitions = encounter.CardDefinitions,
                ActiveCombatNodeId = action.NodeId,
                Reward = null,
            };

            return (combatEnteredState, events);
        }

        if (movedMap.ResolvedEncounterNodeIds.Contains(action.NodeId))
        {
            events.Add(new EncounterAlreadyResolved(action.NodeId, node.Type));
        }
        else if (!MapNodeEncounterSelector.IsCombatNode(node.Type))
        {
            var resolution = EncounterResolver.Resolve(movedMap, action.NodeId);
            movedState = movedState with { Map = resolution.MapState };
            events.Add(new EncounterResolved(action.NodeId, node.Type));
        }
        else
        {
            events.Add(new EncounterAlreadyResolved(action.NodeId, node.Type));
        }

        return (movedState, events);
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


    private static (GameState NewState, IReadOnlyList<GameEvent> Events) ChooseRewardCard(GameState state, ChooseRewardCardAction action)
    {
        if (state.Phase != GamePhase.RewardSelection || state.Reward is null)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (state.Reward.RewardType != RewardType.CardChoice || state.Reward.IsClaimed)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (!state.Reward.CardOptions.Contains(action.CardId))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var events = new GameEvent[]
        {
            new RewardChosen(state.Reward.RewardType, action.CardId, state.Reward.SourceNodeId),
            new CardAddedToDeck(action.CardId),
        };

        return (state with
        {
            Phase = GamePhase.MapExploration,
            Reward = null,
            Combat = null,
            PlayerDeckDiscardPile = state.PlayerDeckDiscardPile.Add(new CardInstance(action.CardId)),
        }, events);
    }

    private static CombatState CreateCombatState(CombatBlueprint blueprint, IReadOnlyList<CardInstance> rewardedCards)
    {
        var playerBlueprint = blueprint.Player with
        {
            DrawPile = blueprint.Player.DrawPile.Concat(rewardedCards.Select(card => card.DefinitionId)).ToArray(),
        };

        var player = CreateCombatEntity(playerBlueprint);
        var enemy = CreateCombatEntity(blueprint.Enemy);
        return new CombatState(TurnOwner.Player, player, enemy, false, 0);
    }

    private static CombatEntity CreateCombatEntity(CombatantBlueprint blueprint)
    {
        return new CombatEntity(
            EntityId: blueprint.EntityId,
            HP: blueprint.HP,
            MaxHP: blueprint.MaxHP,
            Armor: blueprint.Armor,
            Resources: blueprint.Resources.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Deck: new DeckState(
                DrawPile: blueprint.DrawPile.Select(id => new CardInstance(id)).ToImmutableList(),
                Hand: ImmutableList<CardInstance>.Empty,
                DiscardPile: ImmutableList<CardInstance>.Empty,
                BurnPile: ImmutableList<CardInstance>.Empty,
                ReshuffleCount: 0));
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) ResolveCombatPhase(GameState state, IReadOnlyList<GameEvent> events)
    {
        if (state.Combat is null)
        {
            return (state, events);
        }

        if (state.Combat.Player.HP <= 0)
        {
            return (state with { Phase = GamePhase.RunEnded, Combat = null, ActiveCombatNodeId = null, Reward = null }, events);
        }

        if (state.Combat.Enemy.HP <= 0)
        {
            NodeId? sourceNodeId = state.ActiveCombatNodeId;
            NodeType? sourceNodeType = null;
            var mapState = state.Map;
            var resolvedEvents = new List<GameEvent>();

            if (state.ActiveCombatNodeId is { } nodeId && state.Map.Graph.TryGetNode(nodeId, out var node) && node is not null)
            {
                sourceNodeType = node.Type;
                var resolution = EncounterResolver.Resolve(state.Map, nodeId);
                mapState = resolution.MapState;
                resolvedEvents.Add(new EncounterResolved(nodeId, node.Type));
            }

            var rewardResult = RewardGenerator.CreateCardChoiceReward(state.CardDefinitions, state.Rng, sourceNodeId);
            var rewardState = rewardResult.RewardState;

            resolvedEvents.Insert(0, new CombatEnded(sourceNodeId, sourceNodeType, true));
            resolvedEvents.Insert(0, new CombatVictory(sourceNodeId, sourceNodeType));
            resolvedEvents.Add(new RewardOffered(rewardState.RewardType, rewardState.CardOptions, rewardState.SourceNodeId));

            return (state with
            {
                Phase = GamePhase.RewardSelection,
                Combat = null,
                ActiveCombatNodeId = null,
                Map = mapState,
                Rng = rewardResult.Rng,
                Reward = rewardState,
            }, events.Concat(resolvedEvents).ToArray());
        }

        return (state, events);
    }

    private static bool IsBlockedByPendingCombatRequirement(GameState state, GameAction action)
    {
        return state.Combat is { NeedsOverflowDiscard: true } && action is not DiscardOverflowAction;
    }

    private static (CombatState CombatState, GameRng Rng, IReadOnlyList<GameEvent> Events) StartPlayerTurn(CombatState combatState, GameRng rng)
    {
        var drawResult = HandManager.Draw(combatState, rng, 1);
        var events = new List<GameEvent>();
        events.AddRange(drawResult.Events);
        events.AddRange(drawResult.DrawnCards.Select(card => new CardDrawn(card)));

        return (drawResult.CombatState, drawResult.Rng, events);
    }
}
