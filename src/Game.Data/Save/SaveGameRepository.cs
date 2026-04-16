using System.Collections.Immutable;
using System.Text.Json;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Decks;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.Rewards;
using Game.Core.TimeSystem;
using Game.Data.Content;
using CardsCardId = Game.Core.Cards.CardId;
using MapNodeId = Game.Core.Map.NodeId;
using CommonGameRng = Game.Core.Common.GameRng;

namespace Game.Data.Save;

public sealed class SaveGameRepository
{
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SaveGameRepository(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "active-run.json");
    }

    public bool Exists() => File.Exists(_path);

    public void Save(GameState state)
    {
        var dto = ToDto(state);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(dto, _jsonOptions));
    }

    public bool TryLoad(GameContentBundle content, out GameState state)
    {
        state = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);
        if (!Exists())
        {
            return false;
        }

        var json = File.ReadAllText(_path);
        var dto = JsonSerializer.Deserialize<SaveGameDto>(json, _jsonOptions);
        if (dto is null || dto.Version != SaveGameDto.CurrentVersion)
        {
            return false;
        }

        state = FromDto(dto, content);
        return true;
    }

    public void Delete()
    {
        if (Exists())
        {
            File.Delete(_path);
        }
    }

    private static SaveGameDto ToDto(GameState state)
    {
        var nodes = state.Map.Graph.Nodes
            .Select(n => new MapNodeDto(n.Id.Value, (int)n.Type))
            .ToArray();
        var edges = state.Map.Graph.Nodes
            .SelectMany(n => state.Map.Graph.GetNeighbors(n.Id)
                .Where(neighbor => string.CompareOrdinal(n.Id.Value, neighbor.Value) < 0)
                .Select(neighbor => new MapEdgeDto(n.Id.Value, neighbor.Value)))
            .ToArray();

        return new SaveGameDto(
            SaveGameDto.CurrentVersion,
            state.Phase,
            new RngDto(state.Rng.Seed, state.Rng.State),
            state.ActiveCombatNodeId?.Value,
            new MapDto(
                nodes,
                edges,
                state.Map.DistanceFromStart.ToDictionary(k => k.Key.Value, v => v.Value),
                state.Map.StartNodeId.Value,
                state.Map.CurrentNodeId.Value,
                state.Map.VisitedNodeIds.Select(n => n.Value).ToArray(),
                state.Map.TriggeredEncounterNodeIds.Select(n => n.Value).ToArray(),
                state.Map.ResolvedEncounterNodeIds.Select(n => n.Value).ToArray(),
                state.Map.BossNodeId?.Value),
            new TimeDto(
                state.Time.CurrentStep,
                state.Time.CurrentAct,
                state.Time.MapTurnsSinceTimeAdvance,
                state.Time.TimeAdvanceInterval,
                state.Time.CollapsedNodeIds.Select(n => n.Value).ToArray(),
                state.Time.CollapseOrder.Select(n => n.Value).ToArray(),
                state.Time.CollapseCursor,
                state.Time.PlayerCaughtByTime,
                state.Time.TimeBossTriggerPending),
            state.Combat is null ? null : ToDto(state.Combat),
            state.Reward is null ? null : new RewardDto((int)state.Reward.RewardType, state.Reward.CardOptions.Select(c => c.Value).ToArray(), state.Reward.IsClaimed, state.Reward.SourceNodeId?.Value),
            state.DeckEdit is null ? null : new DeckEditDto(state.DeckEdit.RemainingRemovals),
            state.NodeInteraction is null ? null : new NodeInteractionDto(state.NodeInteraction.NodeId.Value, (int)state.NodeInteraction.NodeType, state.NodeInteraction.Options.Select(o => (int)o).ToArray()),
            state.SelectedDeckId,
            state.RunDeck.Select(c => c.DefinitionId.Value).ToArray(),
            state.RunHp,
            state.RunMaxHp);
    }

    private static CombatDto ToDto(CombatState state)
    {
        return new CombatDto(
            (int)state.TurnOwner,
            ToDto(state.Player),
            state.Enemies.Select(ToDto).ToArray(),
            state.NeedsOverflowDiscard,
            state.RequiredOverflowDiscardCount,
            state.PendingDiscardIsFatigue,
            state.AttacksPlayedThisTurn,
            state.PlayedAttackThisTurn,
            state.NextAttackBonusDamageThisTurn,
            state.NextAttackDamageMultiplierThisTurn,
            state.AllAttacksBonusDamageThisTurn,
            state.AllAttacksDamageMultiplierThisTurn,
            state.LastCardMomentumSpent,
            state.LastCardDamageDealt);
    }

    private static CombatEntityDto ToDto(CombatEntity entity)
    {
        return new CombatEntityDto(
            entity.EntityId,
            entity.HP,
            entity.MaxHP,
            entity.Armor,
            entity.Resources.ToDictionary(k => (int)k.Key, v => v.Value),
            new DeckStateDto(
                entity.Deck.DrawPile.Select(c => c.DefinitionId.Value).ToArray(),
                entity.Deck.Hand.Select(c => c.DefinitionId.Value).ToArray(),
                entity.Deck.DiscardPile.Select(c => c.DefinitionId.Value).ToArray(),
                entity.Deck.BurnPile.Select(c => c.DefinitionId.Value).ToArray(),
                entity.Deck.ReshuffleCount),
            entity.Bleed,
            entity.ReflectNextEnemyAttackDamage,
            entity.Weak,
            entity.Vulnerable,
            entity.IsImmortal);
    }

    private static GameState FromDto(SaveGameDto dto, GameContentBundle content)
    {
        var initial = GameState.CreateInitial(content.CardDefinitions, content.DeckDefinitions, content.RewardCardPool, content.EnemyDefinitions, content.Zone1SpawnTable);

        var nodes = dto.Map.Nodes.Select(n => new Node(new MapNodeId(n.NodeId), (NodeType)n.NodeType)).ToArray();
        var connections = dto.Map.Connections.Select(c => (new MapNodeId(c.A), new MapNodeId(c.B))).ToArray();
        var graph = new MapGraph(nodes, connections);
        var map = new MapState(
            graph,
            new MapNodeId(dto.Map.StartNodeId),
            new MapNodeId(dto.Map.CurrentNodeId),
            dto.Map.DistanceFromStart.ToImmutableDictionary(k => new MapNodeId(k.Key), v => v.Value),
            dto.Map.VisitedNodeIds.Select(id => new MapNodeId(id)).ToImmutableSortedSet(MapState.NodeIdComparer),
            dto.Map.TriggeredEncounterNodeIds.Select(id => new MapNodeId(id)).ToImmutableSortedSet(MapState.NodeIdComparer),
            dto.Map.ResolvedEncounterNodeIds.Select(id => new MapNodeId(id)).ToImmutableSortedSet(MapState.NodeIdComparer),
            dto.Map.BossNodeId is null ? null : new MapNodeId(dto.Map.BossNodeId));

        var time = new TimeState(
            dto.Time.CurrentStep,
            dto.Time.CurrentAct,
            dto.Time.MapTurnsSinceTimeAdvance,
            dto.Time.TimeAdvanceInterval,
            dto.Time.CollapsedNodeIds.Select(id => new MapNodeId(id)).ToImmutableSortedSet(MapState.NodeIdComparer),
            dto.Time.CollapseOrder.Select(id => new MapNodeId(id)).ToImmutableList(),
            dto.Time.CollapseCursor,
            dto.Time.PlayerCaughtByTime,
            dto.Time.TimeBossTriggerPending);

        return initial with
        {
            Phase = dto.Phase,
            Rng = new CommonGameRng(dto.Rng.Seed, dto.Rng.State),
            ActiveCombatNodeId = dto.ActiveCombatNodeId is null ? null : new MapNodeId(dto.ActiveCombatNodeId),
            Map = map,
            Time = time,
            Combat = dto.Combat is null ? null : FromDto(dto.Combat),
            Reward = dto.Reward is null
                ? null
                : new RewardState((RewardType)dto.Reward.RewardType, dto.Reward.CardOptions.Select(c => new CardsCardId(c)).ToImmutableList(), dto.Reward.IsClaimed, dto.Reward.SourceNodeId is null ? null : new MapNodeId(dto.Reward.SourceNodeId)),
            DeckEdit = dto.DeckEdit is null ? null : new DeckEditState(dto.DeckEdit.RemainingRemovals),
            NodeInteraction = dto.NodeInteraction is null
                ? null
                : new NodeInteractionState(new MapNodeId(dto.NodeInteraction.NodeId), (NodeType)dto.NodeInteraction.NodeType, dto.NodeInteraction.Options.Select(o => (NodeInteractionOption)o).ToImmutableArray()),
            SelectedDeckId = dto.SelectedDeckId,
            RunDeck = dto.RunDeck.Select(card => new CardInstance(new CardsCardId(card))).ToImmutableList(),
            RunHp = dto.RunHp,
            RunMaxHp = dto.RunMaxHp,
        };
    }

    private static CombatState FromDto(CombatDto dto)
    {
        return new CombatState(
            (TurnOwner)dto.TurnOwner,
            FromDto(dto.Player),
            dto.Enemies.Select(FromDto).ToImmutableList(),
            dto.NeedsOverflowDiscard,
            dto.RequiredOverflowDiscardCount,
            dto.PendingDiscardIsFatigue,
            dto.AttacksPlayedThisTurn,
            dto.PlayedAttackThisTurn,
            dto.NextAttackBonusDamageThisTurn,
            dto.NextAttackDamageMultiplierThisTurn,
            dto.AllAttacksBonusDamageThisTurn,
            dto.AllAttacksDamageMultiplierThisTurn,
            dto.LastCardMomentumSpent,
            dto.LastCardDamageDealt);
    }

    private static CombatEntity FromDto(CombatEntityDto dto)
    {
        return new CombatEntity(
            dto.EntityId,
            dto.HP,
            dto.MaxHP,
            dto.Armor,
            dto.Resources.ToImmutableDictionary(k => (ResourceType)k.Key, v => v.Value),
            new DeckState(
                dto.Deck.DrawPile.Select(c => new CardInstance(new CardsCardId(c))).ToImmutableList(),
                dto.Deck.Hand.Select(c => new CardInstance(new CardsCardId(c))).ToImmutableList(),
                dto.Deck.DiscardPile.Select(c => new CardInstance(new CardsCardId(c))).ToImmutableList(),
                dto.Deck.BurnPile.Select(c => new CardInstance(new CardsCardId(c))).ToImmutableList(),
                dto.Deck.ReshuffleCount),
            dto.Bleed,
            dto.ReflectNextEnemyAttackDamage,
            dto.Weak,
            dto.Vulnerable,
            dto.IsImmortal);
    }
}
