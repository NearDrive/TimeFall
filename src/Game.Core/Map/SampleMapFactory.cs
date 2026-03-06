namespace Game.Core.Map;

public static class SampleMapFactory
{
    public static MapState CreateDefaultState()
    {
        var start = new Node(new NodeId("start"), NodeType.Start);
        var combat = new Node(new NodeId("combat-1"), NodeType.Combat);
        var shop = new Node(new NodeId("shop-1"), NodeType.Shop);
        var elite = new Node(new NodeId("elite-1"), NodeType.Elite);
        var rest = new Node(new NodeId("rest-1"), NodeType.Rest);
        var boss = new Node(new NodeId("boss-1"), NodeType.Boss);

        var nodes = new[] { start, combat, shop, elite, rest, boss };
        var connections = new (NodeId A, NodeId B)[]
        {
            (start.Id, combat.Id),
            (start.Id, shop.Id),
            (combat.Id, elite.Id),
            (shop.Id, rest.Id),
            (elite.Id, boss.Id),
            (rest.Id, boss.Id),
        };

        var graph = new MapGraph(nodes, connections);
        return MapState.Create(graph, start.Id, boss.Id);
    }
}
