using Game.Core.Map;

namespace Game.Cli;

internal sealed class MapDisplayIds
{
    private readonly IReadOnlyDictionary<NodeId, string> _displayIdByNodeId;
    private readonly IReadOnlyDictionary<string, NodeId> _nodeIdByDisplayId;

    private MapDisplayIds(
        IReadOnlyDictionary<NodeId, string> displayIdByNodeId,
        IReadOnlyDictionary<string, NodeId> nodeIdByDisplayId)
    {
        _displayIdByNodeId = displayIdByNodeId;
        _nodeIdByDisplayId = nodeIdByDisplayId;
    }

    public static MapDisplayIds Create(MapState map)
    {
        var byNode = new Dictionary<NodeId, string>();
        var byDisplay = new Dictionary<string, NodeId>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in map.Graph.Nodes)
        {
            var displayId = GetDisplayId(node, map.DistanceFromStart, map.BossNodeId);
            byNode[node.Id] = displayId;
            byDisplay[displayId] = node.Id;
        }

        return new MapDisplayIds(byNode, byDisplay);
    }

    public string Get(NodeId nodeId)
    {
        return _displayIdByNodeId.TryGetValue(nodeId, out var displayId)
            ? displayId
            : nodeId.Value;
    }

    public bool TryResolve(string displayIdOrNodeId, out NodeId nodeId)
    {
        if (_nodeIdByDisplayId.TryGetValue(displayIdOrNodeId, out nodeId))
        {
            return true;
        }

        foreach (var pair in _displayIdByNodeId)
        {
            if (string.Equals(pair.Key.Value, displayIdOrNodeId, StringComparison.Ordinal))
            {
                nodeId = pair.Key;
                return true;
            }
        }

        nodeId = default;
        return false;
    }

    private static string GetDisplayId(Node node, IReadOnlyDictionary<NodeId, int> distanceFromStart, NodeId? bossNodeId)
    {
        if (node.Type == NodeType.Start)
        {
            return "Start";
        }

        if ((bossNodeId is { } boss && node.Id == boss) || node.Type == NodeType.Boss)
        {
            return "Boss";
        }

        if (!distanceFromStart.TryGetValue(node.Id, out var depth) || depth < 1)
        {
            return node.Id.Value;
        }

        var sameDepthOrder = distanceFromStart
            .Where(entry => entry.Value == depth)
            .Select(entry => entry.Key)
            .Where(id => id != bossNodeId)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray();
        var index = Array.IndexOf(sameDepthOrder, node.Id);
        if (index < 0)
        {
            return node.Id.Value;
        }

        return $"{GetDepthPrefix(depth)}{index + 1}";
    }

    private static string GetDepthPrefix(int depth)
    {
        var n = depth;
        var chars = new Stack<char>();
        while (n > 0)
        {
            n--;
            chars.Push((char)('A' + (n % 26)));
            n /= 26;
        }

        return new string(chars.ToArray());
    }
}
