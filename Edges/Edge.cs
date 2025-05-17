using Microsoft.Xna.Framework;

namespace Wayfarer.Edges;

public readonly struct Edge(int to, int from, int edgeType, float cost)
{
    public readonly int To = to;
    public readonly int From = from;
    public readonly int EdgeType = edgeType;
    public readonly float Cost = cost;
}
