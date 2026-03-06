using System.Collections.Immutable;
using Game.Core.Cards;
using Game.Core.Game;
using Game.Core.Map;
using Game.Core.TimeSystem;
using CardId = Game.Core.Cards.CardId;

namespace Game.Tests.Game;

public class NodeInteractionReducerTests
{
    [Fact]
    public void FirstVisit_RestNode_OffersRestInteraction()
    {
        var state = CreateMapExplorationState();

        var (newState, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("rest-1")));

        Assert.NotNull(newState.NodeInteraction);
        Assert.Equal(NodeType.Rest, newState.NodeInteraction!.NodeType);
        Assert.Contains(NodeInteractionOption.RestHeal, newState.NodeInteraction.Options);
        Assert.DoesNotContain(new NodeId("rest-1"), newState.Map.ResolvedEncounterNodeIds);
    }

    [Fact]
    public void RestHeal_IncreasesHp_WithoutExceedingMaxHp()
    {
        var state = CreateMapExplorationState() with { RunHp = 65, RunMaxHp = 80 };
        var (atRest, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("rest-1")));

        var (healed, events) = GameReducer.Reduce(atRest, new UseRestAction(RestOption.Heal));

        Assert.Equal(80, healed.RunHp);
        Assert.Contains(events, e => e is RestUsed { NodeId: var id, Option: RestOption.Heal } && id == new NodeId("rest-1"));
        Assert.Contains(events, e => e is Healed { Amount: 15, CurrentHp: 80, MaxHp: 80 });
        Assert.Contains(new NodeId("rest-1"), healed.Map.ResolvedEncounterNodeIds);
    }

    [Fact]
    public void RevisitingResolvedRestNode_DoesNotReOfferInteraction()
    {
        var state = CreateMapExplorationState() with { RunHp = 40, RunMaxHp = 80 };
        var atRest = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("rest-1"))).NewState;
        var resolvedRest = GameReducer.Reduce(atRest, new UseRestAction(RestOption.Heal)).NewState;
        var atShop = GameReducer.Reduce(resolvedRest, new MoveToNodeAction(new NodeId("shop-1"))).NewState;

        var (revisit, events) = GameReducer.Reduce(atShop, new MoveToNodeAction(new NodeId("rest-1")));

        Assert.Null(revisit.NodeInteraction);
        Assert.Contains(events, e => e is EncounterAlreadyResolved { NodeId: var id } && id == new NodeId("rest-1"));
    }

    [Fact]
    public void FirstVisit_ShopNode_OffersShopInteraction()
    {
        var state = CreateMapExplorationState();

        var (newState, _) = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("shop-1")));

        Assert.NotNull(newState.NodeInteraction);
        Assert.Equal(NodeType.Shop, newState.NodeInteraction!.NodeType);
        Assert.Contains(NodeInteractionOption.ShopRemoveCard, newState.NodeInteraction.Options);
        Assert.DoesNotContain(new NodeId("shop-1"), newState.Map.ResolvedEncounterNodeIds);
    }

    [Fact]
    public void ShopRemoval_RemovesCard_WhenUsed()
    {
        var state = CreateMapExplorationState() with
        {
            RunDeck = ImmutableList.Create(new CardInstance(new CardId("strike")), new CardInstance(new CardId("defend"))),
        };
        var atShop = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("shop-1"))).NewState;

        var (afterRemoval, events) = GameReducer.Reduce(atShop, new UseShopRemovalAction(new CardId("strike")));

        Assert.Single(afterRemoval.RunDeck);
        Assert.Equal(new CardId("defend"), afterRemoval.RunDeck[0].DefinitionId);
        Assert.Contains(events, e => e is ShopRemovalUsed { NodeId: var id, CardId: var cardId } && id == new NodeId("shop-1") && cardId == new CardId("strike"));
        Assert.Contains(new NodeId("shop-1"), afterRemoval.Map.ResolvedEncounterNodeIds);
    }

    [Fact]
    public void RevisitingResolvedShopNode_DoesNotReOfferInteraction()
    {
        var state = CreateMapExplorationState() with
        {
            RunDeck = ImmutableList.Create(new CardInstance(new CardId("strike"))),
        };
        var atShop = GameReducer.Reduce(state, new MoveToNodeAction(new NodeId("shop-1"))).NewState;
        var resolvedShop = GameReducer.Reduce(atShop, new UseShopRemovalAction(new CardId("strike"))).NewState;
        var atStart = GameReducer.Reduce(resolvedShop, new MoveToNodeAction(new NodeId("start"))).NewState;

        var (revisit, events) = GameReducer.Reduce(atStart, new MoveToNodeAction(new NodeId("shop-1")));

        Assert.Null(revisit.NodeInteraction);
        Assert.Contains(events, e => e is EncounterAlreadyResolved { NodeId: var id } && id == new NodeId("shop-1"));
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
}
