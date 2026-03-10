using System.Collections.Immutable;

namespace Game.Core.Map;

public sealed class MapGraph
{
    private readonly ImmutableDictionary<NodeId, Node> _nodes;
    private readonly ImmutableDictionary<NodeId, ImmutableHashSet<NodeId>> _adjacency;

    public MapGraph(IEnumerable<Node> nodes, IEnumerable<(NodeId A, NodeId B)> connections)
    {
        _nodes = nodes.ToImmutableDictionary(node => node.Id);
        var adjacencyBuilder = _nodes.Keys.ToDictionary(id => id, _ => ImmutableHashSet.CreateBuilder<NodeId>());

        foreach (var (a, b) in connections)
        {
            if (!_nodes.ContainsKey(a) || !_nodes.ContainsKey(b))
            {
                throw new ArgumentException("All connections must reference known nodes.");
            }

            adjacencyBuilder[a].Add(b);
            adjacencyBuilder[b].Add(a);
        }

        _adjacency = adjacencyBuilder.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToImmutable());
    }

    public IReadOnlyCollection<Node> Nodes => _nodes.Values.OrderBy(node => node.Id.Value, StringComparer.Ordinal).ToArray();

    public bool ContainsNode(NodeId nodeId) => _nodes.ContainsKey(nodeId);

    public bool IsAdjacent(NodeId from, NodeId to)
    {
        return _adjacency.TryGetValue(from, out var neighbors) && neighbors.Contains(to);
    }

    public bool TryGetNode(NodeId nodeId, out Node? node)
    {
        var success = _nodes.TryGetValue(nodeId, out var foundNode);
        node = foundNode;
        return success;
    }

    public IReadOnlyCollection<NodeId> GetNeighbors(NodeId nodeId)
    {
        if (!_adjacency.TryGetValue(nodeId, out var neighbors))
        {
            return Array.Empty<NodeId>();
        }

        return neighbors.OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
    }

    public ImmutableDictionary<NodeId, int> GetDistancesFrom(NodeId startNodeId)
    {
        if (!_nodes.ContainsKey(startNodeId))
        {
            throw new ArgumentException("Start node must exist in graph.", nameof(startNodeId));
        }

        var distances = new Dictionary<NodeId, int>
        {
            [startNodeId] = 0,
        };
        var queue = new Queue<NodeId>();
        queue.Enqueue(startNodeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var nextDistance = distances[current] + 1;

            foreach (var neighbor in GetNeighbors(current))
            {
                if (distances.ContainsKey(neighbor))
                {
                    continue;
                }

                distances[neighbor] = nextDistance;
                queue.Enqueue(neighbor);
            }
        }

        return distances.ToImmutableDictionary();
    }

    public bool TryGetShortestPathDistance(NodeId from, NodeId to, out int distance)
    {
        distance = -1;
        if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to))
        {
            return false;
        }

        var distances = GetDistancesFrom(from);
        if (!distances.TryGetValue(to, out var foundDistance))
        {
            return false;
        }

        distance = foundDistance;
        return true;
    }
}
