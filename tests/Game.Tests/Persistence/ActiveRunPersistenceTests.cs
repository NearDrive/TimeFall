using System.Collections.Immutable;
using Game.Cli;
using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.Rewards;
using Game.Core.TimeSystem;
using Game.Data.Content;
using Game.Data.Save;
using CardsCardId = Game.Core.Cards.CardId;
using MapNodeId = Game.Core.Map.NodeId;
using CommonGameRng = Game.Core.Common.GameRng;
using CommonStateHasher = Game.Core.Common.StateHasher;

namespace Game.Tests.Persistence;

public sealed class ActiveRunPersistenceTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void ActiveRun_CanBeSerializedAndRestored()
    {
        var path = CreateSavePath();
        var repository = new SaveGameRepository(path);
        var state = CreateCombatState(seed: 51);

        repository.Save(state);

        var loaded = repository.TryLoad(Content, out var restored);

        Assert.True(loaded);
        Assert.Equal(CommonStateHasher.Hash(state), CommonStateHasher.Hash(restored));
    }

    [Fact]
    public void RestoredRun_PreservesDeterministicState()
    {
        var path = CreateSavePath();
        var repository = new SaveGameRepository(path);
        var state = CreateCombatState(seed: 99);
        repository.Save(state);
        repository.TryLoad(Content, out var restored);

        var fromOriginal = GameReducer.Reduce(state, new EndTurnAction()).NewState;
        var fromRestored = GameReducer.Reduce(restored, new EndTurnAction()).NewState;

        Assert.Equal(CommonStateHasher.Hash(fromOriginal), CommonStateHasher.Hash(fromRestored));
    }

    [Fact]
    public void Autosave_OccursAfterEncounterResolution()
    {
        var previous = GameStateTestFactory.CreateStartedRun();
        var current = previous;
        var events = new GameEvent[] { new EncounterResolved(new MapNodeId("rest-1"), NodeType.Rest) };

        var transition = CliLoop.DeterminePersistenceTransition(previous, current, events);

        Assert.Equal(PersistenceTransition.Save, transition);
    }

    [Fact]
    public void Autosave_DoesNotOccurMidCombat()
    {
        var state = CreateCombatState(seed: 123);
        var result = GameReducer.Reduce(state, new EndTurnAction());

        var transition = CliLoop.DeterminePersistenceTransition(state, result.NewState, result.Events);

        Assert.NotEqual(PersistenceTransition.Save, transition);
    }

    [Fact]
    public void PlayerDefeat_ClearsActiveSave_AndReturnsToDeckSelect()
    {
        var state = CreateLethalEnemyTurnState();

        var result = GameReducer.Reduce(state, new EndTurnAction());
        var transition = CliLoop.DeterminePersistenceTransition(state, result.NewState, result.Events);

        Assert.Equal(GamePhase.MainMenu, result.NewState.Phase);
        Assert.Equal(PersistenceTransition.Delete, transition);
    }

    [Fact]
    public void BossVictory_AfterRewardResolution_ClearsActiveSave_AndReturnsToDeckSelect()
    {
        var baseState = GameStateTestFactory.CreateStartedRun();
        var rewardState = baseState with
        {
            Phase = GamePhase.RewardSelection,
            Reward = new RewardState(RewardType.CardChoice, ImmutableList.Create(new CardsCardId("blades-strike"), new CardsCardId("blades-guard"), new CardsCardId("blades-quick-draw")), false, baseState.Map.BossNodeId),
        };

        var result = GameReducer.Reduce(rewardState, new SkipRewardAction());
        var transition = CliLoop.DeterminePersistenceTransition(rewardState, result.NewState, result.Events);

        Assert.Equal(GamePhase.MainMenu, result.NewState.Phase);
        Assert.Equal(PersistenceTransition.Delete, transition);
    }

    [Fact]
    public void LoadingActiveRun_RestoresMapTimeRunAndCombatState()
    {
        var path = CreateSavePath();
        var repository = new SaveGameRepository(path);
        var state = CreateCombatState(seed: 77);

        repository.Save(state);
        var loaded = repository.TryLoad(Content, out var restored);

        Assert.True(loaded);
        Assert.Equal(state.Map.CurrentNodeId, restored.Map.CurrentNodeId);
        Assert.Equal(state.Time.CurrentStep, restored.Time.CurrentStep);
        Assert.Equal(state.RunDeck.Count, restored.RunDeck.Count);
        Assert.NotNull(restored.Combat);
        Assert.Equal(state.Combat!.Player.HP, restored.Combat!.Player.HP);
    }

    [Fact]
    public void SaveFormat_HasVersionField()
    {
        var path = CreateSavePath();
        var repository = new SaveGameRepository(path);

        repository.Save(GameStateTestFactory.CreateStartedRun());

        var json = File.ReadAllText(path);
        Assert.Contains("\"Version\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinueCommandOrAutoLoad_BehavesCorrectly()
    {
        var path = CreateSavePath();
        var repository = new SaveGameRepository(path);
        var state = CreateCombatState(seed: 144);
        repository.Save(state);

        var loaded = repository.TryLoad(Content, out var restored);

        Assert.True(loaded);
        Assert.Equal(GamePhase.Combat, restored.Phase);
    }

    private static GameState CreateCombatState(int seed)
    {
        var started = GameStateTestFactory.CreateStartedRun(seed);
        var nodeId = started.Map.Graph.GetNeighbors(started.Map.CurrentNodeId).First();
        var moved = GameReducer.Reduce(started, new MoveToNodeAction(nodeId)).NewState;
        return moved;
    }

    private static GameState CreateLethalEnemyTurnState()
    {
        return new GameState(
            GamePhase.Combat,
            CommonGameRng.FromSeed(10),
            new CombatState(
                TurnOwner: TurnOwner.Player,
                Player: new CombatEntity(
                    EntityId: "player",
                    HP: 4,
                    MaxHP: 10,
                    Armor: 0,
                    Resources: ImmutableDictionary<ResourceType, int>.Empty,
                    Deck: new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList.Create(new CardInstance(new CardsCardId("guard"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
                Enemy: new CombatEntity(
                    EntityId: "enemy",
                    HP: 10,
                    MaxHP: 10,
                    Armor: 0,
                    Resources: ImmutableDictionary<ResourceType, int>.Empty,
                    Deck: new DeckState(ImmutableList.Create(new CardInstance(new CardsCardId("enemy-attack"))), ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, 0)),
                NeedsOverflowDiscard: false,
                RequiredOverflowDiscardCount: 0),
            null,
            Content.CardDefinitions,
            SampleMapFactory.CreateDefaultState(),
            TimeState.Create(SampleMapFactory.CreateDefaultState()),
            null,
            Content.RewardCardPool.ToImmutableList(),
            Content.DeckDefinitions,
            Content.DeckDefinitions.Keys.OrderBy(x => x, StringComparer.Ordinal).ToImmutableList(),
            "deck-blades",
            false,
            ImmutableList<CardInstance>.Empty,
            null,
            null,
            4,
            10,
            null,
            Content.EnemyDefinitions,
            Content.Zone1SpawnTable);
    }

    private static string CreateSavePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "timefall-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "active-run.json");
    }
}
