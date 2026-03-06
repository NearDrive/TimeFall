namespace Game.Core.Map;

public readonly record struct NodeId(string Value)
{
    public override string ToString() => Value;
}

public sealed record Node(NodeId Id, NodeType Type);
