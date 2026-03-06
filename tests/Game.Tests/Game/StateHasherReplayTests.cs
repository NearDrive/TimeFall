using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;
using CardsCardId = Game.Core.Cards.CardId;
using System.Collections.Immutable;

namespace Game.Tests.Game;

public class StateHasherReplayTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void Replay_WithSameActionSequence_ProducesMatchingHashAtEveryStep()
    {
        var actions = new GameAction[]
        {
            new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions),
            new EndTurnAction(),
            new PlayCardAction(0),
            new EndTurnAction(),
        };

        var firstHashes = ReplayHashes(actions);
        var secondHashes = ReplayHashes(actions);

        Assert.Equal(firstHashes.Length, secondHashes.Length);
        for (var i = 0; i < firstHashes.Length; i++)
        {
            Assert.Equal(firstHashes[i], secondHashes[i]);
        }
    }

    [Fact]
    public void Reducer_DoesNotMutatePreviousStateInstances()
    {
        var (started, _) = GameReducer.Reduce(GameState.Initial, new StartRunAction(2024));
        var (combat, _) = GameReducer.Reduce(started, new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions));

        var previous = combat;
        var previousHash = StateHasher.Hash(previous);
        var previousHandCount = previous.Combat!.Player.Deck.Hand.Count;

        var (nextState, _) = GameReducer.Reduce(combat, new EndTurnAction());

        Assert.Equal(previousHash, StateHasher.Hash(previous));
        Assert.Equal(previousHandCount, previous.Combat!.Player.Deck.Hand.Count);
        Assert.NotEqual(previousHash, StateHasher.Hash(nextState));
    }

    [Fact]
    public void Replay_MultiTurnHashSnapshots_RemainDeterministic()
    {
        var actions = new List<GameAction> { new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions) };
        for (var turn = 0; turn < 6; turn++)
        {
            actions.Add(new EndTurnAction());
            actions.Add(new EndTurnAction());
        }

        var runA = ReplayHashes(actions, seed: 777);
        var runB = ReplayHashes(actions, seed: 777);

        Assert.Equal(runA, runB);
        Assert.Equal(actions.Count + 2, runA.Length);
    }

    [Fact]
    public void Replay_WithDifferentSeeds_DivergesInExpectedStepHash()
    {
        var sharedActions = new GameAction[]
        {
            new BeginCombatAction(Content.OpeningCombat, Content.CardDefinitions),
            new EndTurnAction(),
            new EndTurnAction(),
        };

        var seed2024Hashes = ReplayHashes(sharedActions, 2024);
        var seed7Hashes = ReplayHashes(sharedActions, 7);

        Assert.NotEqual(seed2024Hashes[1], seed7Hashes[1]);
        Assert.NotEqual(seed2024Hashes.Last(), seed7Hashes.Last());
    }

    [Fact]
    public void Hash_IgnoresDictionaryInsertionOrder_ForEquivalentState()
    {
        var stateA = CreateStateWithResourceInsertionOrder(ResourceType.Energy, ResourceType.Generic);
        var stateB = CreateStateWithResourceInsertionOrder(ResourceType.Generic, ResourceType.Energy);

        var hashA = StateHasher.Hash(stateA);
        var hashB = StateHasher.Hash(stateB);

        Assert.Equal(hashA, hashB);
    }

    private static string[] ReplayHashes(IReadOnlyList<GameAction> actions, int seed = 2024)
    {
        var state = GameState.Initial;
        var hashes = new List<string> { StateHasher.Hash(state) };

        var (startedState, _) = GameReducer.Reduce(state, new StartRunAction(seed));
        state = startedState;
        hashes.Add(StateHasher.Hash(state));

        foreach (var action in actions)
        {
            var (newState, _) = GameReducer.Reduce(state, action);
            state = newState;
            hashes.Add(StateHasher.Hash(state));
        }

        return hashes.ToArray();
    }


    private static int ResourceValue(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Energy => 2,
            ResourceType.Generic => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(resourceType), resourceType, "Unsupported resource type for test."),
        };
    }

    private static GameState CreateStateWithResourceInsertionOrder(ResourceType first, ResourceType second)
    {
        var resources = new Dictionary<ResourceType, int>();
        resources[first] = ResourceValue(first);
        resources[second] = ResourceValue(second);

        var deck = new DeckState(
            DrawPile: new[] { new CardInstance(new CardsCardId("strike")), new CardInstance(new CardsCardId("defend")) }.ToImmutableList(),
            Hand: ImmutableList<CardInstance>.Empty,
            DiscardPile: ImmutableList<CardInstance>.Empty,
            BurnPile: ImmutableList<CardInstance>.Empty);

        var player = new CombatEntity("player", 20, 20, 0, resources.ToImmutableDictionary(), deck);
        var enemy = new CombatEntity(
            "enemy",
            10,
            10,
            0,
            ImmutableDictionary<ResourceType, int>.Empty,
            new DeckState(ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty, ImmutableList<CardInstance>.Empty));

        return new GameState(
            Phase: GamePhase.Combat,
            Rng: global::Game.Core.Common.GameRng.FromSeed(99),
            Combat: new CombatState(TurnOwner.Player, 0, player, enemy, false, 0),
            CardDefinitions: Content.CardDefinitions);
    }
}
