using Microsoft.Xna.Framework;

namespace Wayfarer.Edges;

internal readonly struct Edge(int from, int to, int edgeType, float cost)
{
    public readonly int From = from;
    public readonly int To = to;
    public readonly int EdgeType = edgeType;
    public readonly float Cost = cost;
}

public readonly struct PathEdge(Point from, Point to, int edgeType)
{
    public readonly Point From = from;
    public readonly Point To = to;
    public readonly int EdgeType = edgeType;
}
