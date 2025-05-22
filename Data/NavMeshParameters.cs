using System;
using Microsoft.Xna.Framework;
using Wayfarer.API;

namespace Wayfarer.Data;

/// <summary>
/// A set of parameters to determine the properties of a navmesh.
/// </summary>
/// <param name="centralTile">The center of the navmesh. The area in a radius of <paramref name="tileRadius"/> around this point will be computed.</param>
/// <param name="tileRadius">The radius in tiles of the navmesh, from <paramref name="centralTile"/>.</param>
/// <param name="isValidNode">The function used to check if a node is valid. For ground navigators, this can typically be <see cref="WayfarerPresets.DefaultIsTileValid(Point, Rectangle)"/></param>
public sealed class NavMeshParameters(Point centralTile, int tileRadius, Func<Point, Rectangle, bool> isValidNode)
{
    public readonly Point CentralTile = centralTile;
    public readonly int TileRadius = tileRadius;
    public readonly Func<Point, Rectangle, bool> IsValidNode = isValidNode;
}
