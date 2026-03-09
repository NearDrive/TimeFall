using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;

namespace Game.Tests.Game;

public class ContentSlicePlaytestTests
{
    [Fact]
    public void StarterDeck_IsLoadedForRun()
    {
        var state = CreateMapExplorationState();

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        var deckIds = afterEntry.RunDeck.Select(card => card.DefinitionId.Value).ToArray();
        Assert.Equal(new[] { "strike", "strike", "strike", "strike", "strike", "guard", "guard", "guard", "quick-draw", "quick-draw" }, deckIds);
    }

    [Fact]
    public void CombatNode_UsesStandardEnemy()
    {
        var state = CreateMapExplorationState();

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));

        Assert.Equal("enemy-standard-blade-raider", afterEntry.Combat!.Enemy.EntityId);
    }

    [Fact]
    public void EliteNode_UsesEliteEnemy()
    {
        var state = ClaimFirstRewardCard(ForceCompleteCombatAtCurrentNode(GameReducer.Reduce(CreateMapExplorationState(), new MoveToNodeAction(new NodeId("combat-1"))).NewState));

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("elite-1")));

        Assert.Equal("enemy-elite-duelist-captain", afterEntry.Combat!.Enemy.EntityId);
    }

    [Fact]
    public void BossNode_UsesBossEnemy()
    {
        var state = CreateMapExplorationState();
        var (toShop, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("shop-1")));
        var (toRest, _) = GameReducer.Reduce(toShop, new MoveToNodeAction(new NodeId("rest-1")));

        var (afterEntry, _) = GameReducer.Reduce(toRest, new MoveToNodeAction(new NodeId("boss-1")));

        Assert.Equal("enemy-boss-iron-warden", afterEntry.Combat!.Enemy.EntityId);
    }

    [Fact]
    public void RewardCards_ComeFromConfiguredPool()
    {
        var rewardState = CreateRewardSelectionState(seed: 77);

        var pool = new HashSet<CardId>
        {
            new("strike"),
            new("guard"),
            new("quick-draw"),
            new("heavy-attack"),
            new("feint"),
        };

        Assert.All(rewardState.Reward!.CardOptions, cardId => Assert.Contains(cardId, pool));
    }

    [Fact]
    public void ContentSlice_IsDeterministic()
    {
        var first = Snapshot(seed: 321);
        var second = Snapshot(seed: 321);

        Assert.Equal(first, second);
    }

    private static string Snapshot(int seed)
    {
        var rewardState = CreateRewardSelectionState(seed);
        return string.Join("|", rewardState.RunDeck.Select(c => c.DefinitionId.Value)) +
               "::" +
               string.Join("|", rewardState.Reward!.CardOptions.Select(c => c.Value));
    }

    private static GameState CreateRewardSelectionState(int seed)
    {
        var map = SampleMapFactory.CreateDefaultState();
        var state = GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map),
            Rng = global::Game.Core.Common.GameRng.FromSeed(seed),
        };

        var (afterEntry, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("combat-1")));
        return ForceCompleteCombatAtCurrentNode(afterEntry);
    }

    private static GameState CreateMapExplorationState()
    {
        var map = SampleMapFactory.CreateDefaultState();
        return GameState.Initial with
        {
            Phase = GamePhase.MapExploration,
            Map = map,
            Time = TimeState.Create(map),
        };
    }

    private static GameState ClaimFirstRewardCard(GameState rewardState)
    {
        var selected = rewardState.Reward!.CardOptions[0];
        return GameReducer.Reduce(rewardState, new ChooseRewardCardAction(selected)).NewState;
    }

    private static GameState ForceCompleteCombatAtCurrentNode(GameState combatState)
    {
        var strikeIndex = combatState.Combat!.Player.Deck.Hand
            .Select((card, index) => (card, index))
            .First(tuple => tuple.card.DefinitionId.Value == "strike")
            .index;

        var lethal = combatState with
        {
            Combat = combatState.Combat with
            {
                Enemy = combatState.Combat.Enemy with { HP = 4 },
            },
        };

        return GameReducer.Reduce(lethal, new PlayCardAction(strikeIndex)).NewState;
    }
}
