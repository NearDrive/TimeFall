using Game.Core.Cards;
using Game.Core.Combat;
using Game.Core.Common;
using Game.Core.Game;

namespace Game.Tests.Game;

public class StateHasherReplayTests
{
    private static readonly GameContentBundle Content = StaticGameContentProvider.LoadDefault();

    [Fact]
    public void Replay_WithSameActionSequence_ProducesMatchingHashAtEveryStep()
    {
        var actions = new GameAction[]
        {
            new BeginCombatAction(Content.OpeningCombat),
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
    public void Replay_WithDifferentSeeds_DivergesInExpectedStepHash()
    {
        var sharedActions = new GameAction[]
        {
            new BeginCombatAction(Content.OpeningCombat),
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
        var stateA = CreateStateWithResourceInsertionOrder(ResourceType.Energy, ResourceType.Shield);
        var stateB = CreateStateWithResourceInsertionOrder(ResourceType.Shield, ResourceType.Energy);

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

    private static GameState CreateStateWithResourceInsertionOrder(ResourceType first, ResourceType second)
    {
        var resources = new Dictionary<ResourceType, int>
        {
            [first] = 2,
            [second] = 1,
        };

        var deck = new DeckState(
            DrawPile: new List<CardInstance> { new(new CardId("strike")), new(new CardId("defend")) },
            Hand: new List<CardInstance>(),
            DiscardPile: new List<CardInstance>(),
            BurnPile: new List<CardInstance>());

        var player = new CombatEntity("player", 20, 20, 0, resources, deck);
        var enemy = new CombatEntity(
            "enemy",
            10,
            10,
            0,
            new Dictionary<ResourceType, int>(),
            new DeckState(new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>(), new List<CardInstance>()));

        return new GameState(
            Phase: GamePhase.Combat,
            Rng: Game.Core.Common.GameRng.FromSeed(99),
            Combat: new CombatState(TurnOwner.Player, 0, player, enemy, false, 0));
    }
}
