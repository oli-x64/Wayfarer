using Microsoft.Xna.Framework;

namespace Wayfarer.Configuration;

public readonly struct NavMeshParameters(Point centralTile, int tileRadius)
{
    public readonly Point CentralTile = centralTile;
    public readonly int TileRadius = tileRadius;
}
