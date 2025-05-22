using Microsoft.Xna.Framework;

namespace Wayfarer.Edges;

internal readonly struct Edge(int from, int to, int edgeType, float cost)
{
    public readonly int From = from;
    public readonly int To = to;
    public readonly int EdgeType = edgeType;
    public readonly float Cost = cost;
}

/// <summary>
/// Represents a segment of a calculated path.
/// </summary>
/// <param name="from">The origin of this edge.</param>
/// <param name="to">The destination of this edge.</param>
/// <param name="edgeType">The edge type. You can call <see cref="EdgeExtensions.Is{T}(PathEdge)"/> on an edge to check its edgeType against the corresponding class.</param>
public readonly struct PathEdge(Point from, Point to, int edgeType)
{
    public readonly Point From = from;
    public readonly Point To = to;
    public readonly int EdgeType = edgeType;
}
