using System;
using Terraria;
using Wayfarer.Configuration;
using Wayfarer.Tiles;

namespace Wayfarer.Pathfinding;

internal sealed class PathfinderInstance(NavMeshParameters navMeshParameters) : IDisposable
{
    private readonly NavMeshParameters navMeshParameters = navMeshParameters;

    private TileDataSnapshot<TileWallWireStateData> wallWireStateSnapshot;
    private TileDataSnapshot<LiquidData> liquidSnapshot;
    private TileDataSnapshot<TileTypeData> typeSnapshot;

    public void Recalculate()
    {
        CopyTileData(navMeshParameters);
    }

    /// <summary>
    /// In order for the pathfinding system to determine if nodes are valid, it needs access to tile data. Since we don't want to read Main.tile on our worker thread 
    /// due to possibility of main thread writes, a copy of the relevant region needs to be made. The included data types are <see cref="TileWallWireStateData"/>,
    /// <see cref="LiquidData"/>, and <see cref="TileTypeData"/>.
    /// </summary>
    /// <param name="navMeshParameters"></param>
    private void CopyTileData(NavMeshParameters navMeshParameters)
    {
        // Ensure the bounding box is never outside the world.
        uint minX = (uint)Math.Clamp(navMeshParameters.CentralTile.X - navMeshParameters.TileRadius, 0, Main.maxTilesX - 1);
        uint minY = (uint)Math.Clamp(navMeshParameters.CentralTile.Y - navMeshParameters.TileRadius, 0, Main.maxTilesY - 1);
        uint maxX = (uint)Math.Clamp(navMeshParameters.CentralTile.X + navMeshParameters.TileRadius, 0, Main.maxTilesX - 1);
        uint maxY = (uint)Math.Clamp(navMeshParameters.CentralTile.Y + navMeshParameters.TileRadius, 0, Main.maxTilesY - 1);

        wallWireStateSnapshot = new TileDataSnapshot<TileWallWireStateData>(minX, minY, maxX, maxY);
        liquidSnapshot = new TileDataSnapshot<LiquidData>(minX, minY, maxX, maxY);
        typeSnapshot = new TileDataSnapshot<TileTypeData>(minX, minY, maxX, maxY);
    }

    public void Dispose()
    {
        // TODO: threaded operations will need to be cancelled here.

        wallWireStateSnapshot.Dispose();
        liquidSnapshot.Dispose();
        typeSnapshot.Dispose();
    }
}
