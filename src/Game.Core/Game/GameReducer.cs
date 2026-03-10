using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Map;
using Game.Core.TimeSystem;
using Game.Core.Rewards;
using Game.Core.Decks;
using NodeId = Game.Core.Map.NodeId;
using CardId = Game.Core.Cards.CardId;
using System.Collections.Immutable;

namespace Game.Core.Game;

public static class GameReducer
{
    public static (GameState NewState, IReadOnlyList<GameEvent> Events) Reduce(GameState state, GameAction action)
    {
        if (IsBlockedByPendingCombatRequirement(state, action))
        {
            if (action is PlayCardAction)
            {
                return RejectPlayCard(state, PlayCardRejectionReason.ActionBlockedByPendingDiscard, "Discard overflow cards before playing more cards.");
            }

            return (state, Array.Empty<GameEvent>());
        }

        return action switch
        {
            SelectDeckAction selectDeckAction => SelectDeck(state, selectDeckAction),
            StartRunAction startRunAction => StartRun(state, startRunAction),
            BeginCombatAction beginCombatAction => BeginCombat(state, beginCombatAction),
            PlayCardAction playCardAction => PlayCard(state, playCardAction),
            EndTurnAction endTurnAction => EndTurn(state, endTurnAction),
            DiscardOverflowAction discardOverflowAction => DiscardOverflow(state, discardOverflowAction),
            MoveToNodeAction moveToNodeAction => MoveToNode(state, moveToNodeAction),
            ChooseRewardCardAction chooseRewardCardAction => ChooseRewardCard(state, chooseRewardCardAction),
            SkipRewardAction skipRewardAction => SkipReward(state, skipRewardAction),
            BeginDeckRemovalAction beginDeckRemovalAction => BeginDeckRemoval(state, beginDeckRemovalAction),
            RemoveCardFromDeckAction removeCardFromDeckAction => RemoveCardFromDeck(state, removeCardFromDeckAction),
            UseRestAction useRestAction => UseRest(state, useRestAction),
            UseShopRemovalAction useShopRemovalAction => UseShopRemoval(state, useShopRemovalAction),
            _ => (state, Array.Empty<GameEvent>()),
        };
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) SelectDeck(GameState state, SelectDeckAction action)
    {
        if (state.Phase != GamePhase.DeckSelect)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (!state.DeckDefinitions.ContainsKey(action.DeckId))
        {
            return (state, Array.Empty<GameEvent>());
        }

        return (state with { SelectedDeckId = action.DeckId }, new GameEvent[] { new DeckSelected(action.DeckId) });
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) StartRun(GameState state, StartRunAction action)
    {
        if (state.Phase != GamePhase.DeckSelect)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (state.SelectedDeckId is null || !state.DeckDefinitions.TryGetValue(state.SelectedDeckId, out var selectedDeck))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var mapState = SampleMapFactory.CreateDefaultState();
        var initializedRunDeck = selectedDeck.StartingDeck.Select(id => new CardInstance(id)).ToImmutableList();
        var newState = new GameState(
            GamePhase.MapExploration,
            GameRng.FromSeed(action.Seed),
            null,
            null,
            state.CardDefinitions,
            mapState,
            TimeState.Create(mapState),
            null,
            state.RewardCardPool,
            state.DeckDefinitions,
            state.AvailableDeckIds,
            state.SelectedDeckId,
            initializedRunDeck,
            null,
            selectedDeck.BaseMaxHp,
            selectedDeck.BaseMaxHp,
            null,
            state.EnemyDefinitions,
            state.Zone1SpawnTable);
        var events = new GameEvent[] { new RunStarted(action.Seed) };

        return (newState, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) BeginCombat(GameState state, BeginCombatAction action)
    {
        if (state.Combat is not null || state.Time.PlayerCaughtByTime)
        {
            return (state, Array.Empty<GameEvent>());
        }

        if (!IsBeginCombatAllowedPhase(state.Phase))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var stateWithDeck = EnsureRunDeckInitialized(state, action.Blueprint);
        var combatState = CreateCombatState(stateWithDeck, action.Blueprint, stateWithDeck.RunDeck);
        var drawResult = HandManager.Draw(combatState, state.Rng, 5);
        var events = new List<GameEvent> { new EnteredCombat(null, null) };
        events.AddRange(drawResult.Events);
        events.AddRange(drawResult.DrawnCards.Select(c => new CardDrawn(c)));

        return (stateWithDeck with { Phase = GamePhase.Combat, Combat = drawResult.CombatState, Rng = drawResult.Rng, CardDefinitions = action.CardDefinitions, RewardCardPool = ResolveRewardCardPool(action).ToImmutableList(), Reward = null, DeckEdit = null, NodeInteraction = null }, events);
    }

    private static IReadOnlyList<CardId> ResolveRewardCardPool(BeginCombatAction action)
    {
        if (action.RewardCardPool is { Count: > 0 })
        {
            return action.RewardCardPool;
        }

        return action.CardDefinitions.Keys
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsBeginCombatAllowedPhase(GamePhase phase)
    {
        return phase is GamePhase.DeckSelect or GamePhase.MapExploration;
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) PlayCard(GameState state, PlayCardAction action)
    {
        if (state is not { Phase: GamePhase.Combat, Combat: { } combatState })
        {
            return RejectPlayCard(state, PlayCardRejectionReason.NotInCombat, "You can only play cards while in combat.");
        }

        if (combatState.TurnOwner != TurnOwner.Player)
        {
            return RejectPlayCard(state, PlayCardRejectionReason.NotPlayerTurn, "Cards can only be played on the player's turn.");
        }

        if (action.HandIndex < 0 || action.HandIndex >= combatState.Player.Deck.Hand.Count)
        {
            return RejectPlayCard(state, PlayCardRejectionReason.InvalidHandIndex, $"Hand index {action.HandIndex} is out of range.");
        }

        var card = combatState.Player.Deck.Hand[action.HandIndex];
        if (!state.CardDefinitions.TryGetValue(card.DefinitionId, out var cardDefinition))
        {
            return RejectPlayCard(state, PlayCardRejectionReason.CardDefinitionMissing, $"Card definition '{card.DefinitionId.Value}' was not found.");
        }

        if (!CardEffectResolver.HasResolvableEffects(cardDefinition))
        {
            return RejectPlayCard(state, PlayCardRejectionReason.CardHasNoResolvableEffects, $"Card '{cardDefinition.Name}' has no resolvable effects.");
        }

        var events = new List<GameEvent>();
        if (!TryPayCost(combatState, cardDefinition, events, out var costPaidState))
        {
            return RejectPlayCard(state, PlayCardRejectionReason.CostNotPayable, $"Cost for '{cardDefinition.Name}' cannot be paid.");
        }

        combatState = costPaidState;
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

        var resolution = CardEffectResolver.Resolve(combatState, card, TurnOwner.Player, state.CardDefinitions, state.Rng);
        combatState = resolution.CombatState;

        if (cardDefinition.HasLabel("Attack"))
        {
            var before = combatState.Player.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
            var after = before + 1;
            combatState = combatState with
            {
                Player = combatState.Player with { Resources = combatState.Player.Resources.SetItem(ResourceType.Momentum, after) },
                AttacksPlayedThisTurn = combatState.AttacksPlayedThisTurn + 1,
                PlayedAttackThisTurn = true,
            };
            events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, before, after, "Attack label bonus"));
        }

        events.Add(new CardDiscarded(card));
        events.AddRange(resolution.Events);

        return ResolveCombatPhase(state with { Combat = combatState, Rng = resolution.Rng }, events);
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

        var events = new List<GameEvent>();
        if (!combatState.PlayedAttackThisTurn)
        {
            var before = combatState.Player.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
            var after = before / 2;
            if (before != after)
            {
                combatState = combatState with { Player = combatState.Player with { Resources = combatState.Player.Resources.SetItem(ResourceType.Momentum, after) } };
                events.Add(new MomentumDecayApplied(before, after));
                events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, before, after, "End turn decay"));
            }
        }

        combatState = combatState with { TurnOwner = TurnOwner.Enemy, PlayedAttackThisTurn = false, AttacksPlayedThisTurn = 0, AllAttacksBonusDamageThisTurn = 0, AllAttacksDoubleThisTurn = false, NextAttackBonusDamageThisTurn = 0, NextAttackDoubleThisTurn = false };
        events.Add(new TurnEnded(TurnOwner.Enemy));
        combatState = ApplyStartOfTurnStatuses(combatState, TurnOwner.Enemy, events);
        if (combatState.Enemy.HP <= 0)
        {
            return ResolveCombatPhase(state with { Combat = combatState, Rng = state.Rng }, events);
        }

        var enemyTurnStart = EnemyController.DrawAtTurnStart(combatState, state.Rng);
        combatState = enemyTurnStart.CombatState;
        events.AddRange(enemyTurnStart.Events);
        events.AddRange(enemyTurnStart.DrawnCards.Select(card => new CardDrawn(card)));

        var enemyResult = EnemyController.ExecuteTurn(combatState, enemyTurnStart.Rng, state.CardDefinitions);
        combatState = enemyResult.CombatState;
        events.AddRange(enemyResult.Events);

        if (combatState.Player.HP <= 0)
        {
            return ResolveCombatPhase(state with { Combat = combatState, Rng = enemyResult.Rng }, events);
        }

        combatState = combatState with { TurnOwner = TurnOwner.Player };
        events.Add(new TurnEnded(TurnOwner.Player));
        combatState = ApplyStartOfTurnStatuses(combatState, TurnOwner.Player, events);
        if (combatState.Player.HP <= 0)
        {
            return ResolveCombatPhase(state with { Combat = combatState, Rng = enemyResult.Rng }, events);
        }

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

        var timeAdvance = TimeAdvancer.AdvanceForMapMove(state.Time, movedMap.CurrentNodeId);
        if (timeAdvance.TimeAdvancedThisMove)
        {
            events.Add(new TimeAdvanced(timeAdvance.TimeState.CurrentStep));

            foreach (var collapsedNode in timeAdvance.NewlyCollapsedNodes)
            {
                events.Add(new NodeCollapsed(collapsedNode));
            }
        }

        if (timeAdvance.PlayerCaughtThisStep)
        {
            events.Add(new TimeCaughtPlayer(movedMap.CurrentNodeId, timeAdvance.TimeState.CurrentStep));
        }

        var movedState = state with { Map = movedMap, Time = timeAdvance.TimeState, NodeInteraction = null };

        // If Time catches the player on this movement step, we intentionally do not start
        // the destination encounter. This keeps the move->time resolution order explicit
        // and avoids entering normal node combat while already caught by Time.
        if (timeAdvance.PlayerCaughtThisStep)
        {
            return (movedState, events);
        }

        if (MapNodeEncounterSelector.IsCombatNode(node.Type) && !movedMap.ResolvedEncounterNodeIds.Contains(action.NodeId))
        {
            var selected = MapNodeEncounterSelector.TrySelect(
                node.Type,
                movedMap,
                movedState.Rng,
                movedState.EnemyDefinitions,
                movedState.Zone1SpawnTable,
                movedState.CardDefinitions,
                movedState.RewardCardPool,
                out var encounter,
                out var encounterRng);
            if (!selected)
            {
                return (movedState, events);
            }

            movedState = movedState with { Rng = encounterRng };
            movedState = EnsureRunDeckInitialized(movedState, encounter.Blueprint);
            var combatState = CreateCombatState(movedState, encounter.Blueprint, movedState.RunDeck);
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
                RewardCardPool = encounter.RewardCardPool.ToImmutableList(),
                ActiveCombatNodeId = action.NodeId,
                Reward = null,
                DeckEdit = null,
                NodeInteraction = null,
            };

            return (combatEnteredState, events);
        }

        if (movedMap.ResolvedEncounterNodeIds.Contains(action.NodeId))
        {
            events.Add(new EncounterAlreadyResolved(action.NodeId, node.Type));
        }
        else if (node.Type is NodeType.Rest or NodeType.Shop)
        {
            movedState = movedState with { NodeInteraction = CreateNodeInteraction(action.NodeId, node.Type) };
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
            RunDeck = state.RunDeck.Add(new CardInstance(action.CardId)),
            DeckEdit = null,
        }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) SkipReward(GameState state, SkipRewardAction action)
    {
        _ = action;

        if (state.Phase != GamePhase.RewardSelection || state.Reward is null)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var events = new GameEvent[]
        {
            new RewardSkipped(state.Reward.RewardType, state.Reward.SourceNodeId),
        };

        return (state with
        {
            Phase = GamePhase.MapExploration,
            Reward = null,
            Combat = null,
            DeckEdit = null,
        }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) BeginDeckRemoval(GameState state, BeginDeckRemovalAction action)
    {
        _ = action;

        if (state.Phase != GamePhase.MapExploration || state.RunDeck.Count == 0)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var deckEditState = DeckEditState.RemoveOneCard();
        var events = new GameEvent[]
        {
            new DeckRemovalBegan(deckEditState.RemainingRemovals),
        };

        return (state with { Phase = GamePhase.DeckRemoval, DeckEdit = deckEditState, Reward = null }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) UseRest(GameState state, UseRestAction action)
    {
        if (state.Phase != GamePhase.MapExploration ||
            state.NodeInteraction is not { NodeType: NodeType.Rest } interaction ||
            state.Map.CurrentNodeId != interaction.NodeId ||
            state.Map.ResolvedEncounterNodeIds.Contains(interaction.NodeId) ||
            action.Option != RestOption.Heal)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var healedHp = Math.Min(state.RunMaxHp, state.RunHp + GameState.RestHealAmount);
        var healedAmount = healedHp - state.RunHp;
        var resolution = EncounterResolver.Resolve(state.Map, interaction.NodeId);
        var events = new GameEvent[]
        {
            new RestUsed(interaction.NodeId, action.Option),
            new Healed(healedAmount, healedHp, state.RunMaxHp),
            new EncounterResolved(interaction.NodeId, NodeType.Rest),
        };

        return (state with
        {
            Map = resolution.MapState,
            RunHp = healedHp,
            NodeInteraction = null,
        }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) UseShopRemoval(GameState state, UseShopRemovalAction action)
    {
        if (state.Phase != GamePhase.MapExploration ||
            state.NodeInteraction is not { NodeType: NodeType.Shop } interaction ||
            state.Map.CurrentNodeId != interaction.NodeId ||
            state.Map.ResolvedEncounterNodeIds.Contains(interaction.NodeId))
        {
            return (state, Array.Empty<GameEvent>());
        }

        var stateWithDeck = EnsureRunDeckInitializedForNonCombatInteraction(state);
        var index = stateWithDeck.RunDeck.FindIndex(card => card.DefinitionId == action.CardId);
        if (index < 0)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var resolution = EncounterResolver.Resolve(stateWithDeck.Map, interaction.NodeId);
        var updatedDeck = stateWithDeck.RunDeck.RemoveAt(index);
        var events = new GameEvent[]
        {
            new ShopRemovalUsed(interaction.NodeId, action.CardId),
            new CardRemovedFromDeck(action.CardId),
            new EncounterResolved(interaction.NodeId, NodeType.Shop),
        };

        return (state with
        {
            Map = resolution.MapState,
            RunDeck = updatedDeck,
            NodeInteraction = null,
        }, events);
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) RemoveCardFromDeck(GameState state, RemoveCardFromDeckAction action)
    {
        if (state.Phase != GamePhase.DeckRemoval || state.DeckEdit is null || state.DeckEdit.RemainingRemovals <= 0)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var index = state.RunDeck.FindIndex(card => card.DefinitionId == action.CardId);
        if (index < 0)
        {
            return (state, Array.Empty<GameEvent>());
        }

        var updatedDeck = state.RunDeck.RemoveAt(index);
        var remainingRemovals = state.DeckEdit.RemainingRemovals - 1;
        var nextDeckEdit = remainingRemovals > 0 ? state.DeckEdit with { RemainingRemovals = remainingRemovals } : null;

        var events = new GameEvent[]
        {
            new CardRemovedFromDeck(action.CardId),
        };

        return (state with
        {
            RunDeck = updatedDeck,
            DeckEdit = nextDeckEdit,
            Phase = nextDeckEdit is null ? GamePhase.MapExploration : GamePhase.DeckRemoval,
        }, events);
    }

    private static CombatState CreateCombatState(GameState state, CombatBlueprint blueprint, IReadOnlyList<CardInstance> runDeck)
    {
        var playerBlueprint = blueprint.Player with
        {
            HP = state.RunHp,
            MaxHP = state.RunMaxHp,
            DrawPile = runDeck.Select(card => card.DefinitionId).ToArray(),
            Resources = ResolveStartingResources(state, blueprint.Player.Resources),
        };

        var player = CreateCombatEntity(playerBlueprint);
        var enemy = CreateCombatEntity(blueprint.Enemy);
        return new CombatState(TurnOwner.Player, player, enemy, false, 0);
    }

    private static IReadOnlyDictionary<ResourceType, int> ResolveStartingResources(GameState state, IReadOnlyDictionary<ResourceType, int> fallback)
    {
        if (state.SelectedDeckId is null || !state.DeckDefinitions.TryGetValue(state.SelectedDeckId, out var selectedDeck))
        {
            return fallback;
        }

        return selectedDeck.StartingResources;
    }

    private static GameState EnsureRunDeckInitialized(GameState state, CombatBlueprint blueprint)
    {
        if (state.RunDeck.Count > 0)
        {
            return state;
        }

        var initializedDeck = blueprint.Player.DrawPile
            .Select(id => new CardInstance(id))
            .ToImmutableList();

        return state with { RunDeck = initializedDeck };
    }

    private static GameState EnsureRunDeckInitializedForNonCombatInteraction(GameState state)
    {
        if (state.RunDeck.Count > 0)
        {
            return state;
        }

        if (state.SelectedDeckId is null || !state.DeckDefinitions.TryGetValue(state.SelectedDeckId, out var selectedDeck))
        {
            return state;
        }

        var initializedDeck = selectedDeck.StartingDeck
            .Select(id => new CardInstance(id))
            .ToImmutableList();

        return state with { RunDeck = initializedDeck };
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
            return (state with { Phase = GamePhase.RunEnded, Combat = null, ActiveCombatNodeId = null, Reward = null, RunHp = 0, NodeInteraction = null }, events);
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

            var rewardResult = RewardGenerator.CreateCardChoiceReward(state.CardDefinitions, state.RewardCardPool, state.Rng, sourceNodeId);
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
                RunHp = state.Combat.Player.HP,
                NodeInteraction = null,
            }, events.Concat(resolvedEvents).ToArray());
        }

        return (state, events);
    }

    private static bool TryPayCost(CombatState combatState, CardDefinition definition, List<GameEvent> events, out CombatState newState)
    {
        var gm = combatState.Player.Resources.GetValueOrDefault(ResourceType.Momentum, 0);
        var momentum = MomentumMath.DerivedMomentumFromGm(gm);
        var spentMomentum = 0;

        switch (definition.PlayCostOrDefault)
        {
            case RequireMomentumCost r when momentum < r.Minimum:
                newState = combatState;
                return false;
            case SpendMomentumCost s:
                if (momentum < s.Amount)
                {
                    newState = combatState;
                    return false;
                }

                var costGm = MomentumMath.Threshold(s.Amount);
                var afterSpend = Math.Max(0, gm - costGm);
                events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, gm, afterSpend, $"Spend {s.Amount} Momentum"));
                newState = combatState with { Player = combatState.Player with { Resources = combatState.Player.Resources.SetItem(ResourceType.Momentum, afterSpend) }, LastCardMomentumSpent = s.Amount };
                return true;
            case SpendAllMomentumCost:
                events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, gm, 0, "Spend all Momentum"));
                newState = combatState with { Player = combatState.Player with { Resources = combatState.Player.Resources.SetItem(ResourceType.Momentum, 0) }, LastCardMomentumSpent = momentum };
                return true;
            case SpendUpToMomentumCost u:
                spentMomentum = Math.Min(u.Max, momentum);
                var spendUpToGm = MomentumMath.Threshold(spentMomentum);
                var afterUpTo = Math.Max(0, gm - spendUpToGm);
                events.Add(new ResourceChanged(TurnOwner.Player, ResourceType.Momentum, gm, afterUpTo, $"Spend up to {u.Max} Momentum ({spentMomentum})"));
                newState = combatState with { Player = combatState.Player with { Resources = combatState.Player.Resources.SetItem(ResourceType.Momentum, afterUpTo) }, LastCardMomentumSpent = spentMomentum };
                return true;
            default:
                newState = combatState with { LastCardMomentumSpent = 0 };
                return true;
        }
    }

    private static (GameState NewState, IReadOnlyList<GameEvent> Events) RejectPlayCard(GameState state, PlayCardRejectionReason reason, string message)
    {
        return (state, new GameEvent[] { new PlayCardRejected(reason, message) });
    }

    private static bool IsBlockedByPendingCombatRequirement(GameState state, GameAction action)
    {
        return state.Combat is { NeedsOverflowDiscard: true } && action is not DiscardOverflowAction;
    }

    private static NodeInteractionState CreateNodeInteraction(NodeId nodeId, NodeType nodeType)
    {
        var options = nodeType switch
        {
            NodeType.Rest => ImmutableArray.Create(NodeInteractionOption.RestHeal),
            NodeType.Shop => ImmutableArray.Create(NodeInteractionOption.ShopRemoveCard),
            _ => ImmutableArray<NodeInteractionOption>.Empty,
        };

        return new NodeInteractionState(nodeId, nodeType, options);
    }

    private static CombatState ApplyStartOfTurnStatuses(CombatState combatState, TurnOwner turnOwner, ICollection<GameEvent> events)
    {
        if (turnOwner == TurnOwner.Player)
        {
            return TickBleed(combatState, turnOwner, events, affectsPlayer: true);
        }

        return TickBleed(combatState, turnOwner, events, affectsPlayer: false);
    }

    private static CombatState TickBleed(CombatState combatState, TurnOwner turnOwner, ICollection<GameEvent> events, bool affectsPlayer)
    {
        var entity = affectsPlayer ? combatState.Player : combatState.Enemy;
        if (entity.Bleed <= 0)
        {
            return combatState;
        }

        var beforeHp = entity.HP;
        var bleedDamage = Math.Min(entity.Bleed, entity.HP);
        var afterHp = Math.Max(0, beforeHp - entity.Bleed);
        var nextBleed = Math.Max(0, entity.Bleed - 1);
        events.Add(new StatusTriggered(turnOwner, "Bleed", bleedDamage, beforeHp, afterHp));
        if (nextBleed == 0)
        {
            events.Add(new StatusExpired(turnOwner, "Bleed"));
        }

        var updated = entity with { HP = afterHp, Bleed = nextBleed };
        return affectsPlayer
            ? combatState with { Player = updated }
            : combatState with { Enemy = updated };
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
