namespace Wayfarer.Pathfinding;

public struct Edge(int to, int from, EdgeType edgeType, float cost)
{
    public int To = to;
    public int From = from;
    public EdgeType EdgeType = edgeType;
    public float Cost = cost;
}

public enum EdgeType
{
    Walk,
    Fall,
    Jump
}
