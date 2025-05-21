using System;
using Microsoft.Xna.Framework;

namespace Wayfarer.Data;

public sealed class NavMeshParameters(Point centralTile, int tileRadius, Func<Point, Rectangle, bool> isValidNode)
{
    public readonly Point CentralTile = centralTile;
    public readonly int TileRadius = tileRadius;
    public readonly Func<Point, Rectangle, bool> IsValidNode = isValidNode;
}
