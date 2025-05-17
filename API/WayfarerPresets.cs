using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.Data;

namespace Wayfarer.API;

public static class WayfarerPresets
{
    /// <summary>
    /// A preset method intended for use as <see cref="NavMeshParameters.IsValidNode"/>. This will return true for solid tiles with enough vertical clearance
    /// in the tiles above them to accommodate the hitbox. This method does not check for horizontal clearance!
    /// </summary>
    /// <param name="tile"></param>
    /// <param name="hitbox"></param>
    /// <returns></returns>
    public static bool ValidWalkableTile(Point tile, Rectangle hitbox)
    {
        int heightInTiles = (int)Math.Ceiling(hitbox.Height / 16f);

        Tile standingTile = Main.tile[tile];

        // Eliminate tiles that are either non-existent or cannot be stood on.
        if (!standingTile.HasTile || !Main.tileSolid[standingTile.TileType])
        {
            return false;
        }

        // Check tiles above the standing tile to make sure the NPC's hitbox can fit.
        for (int yOffsetTiles = 1; yOffsetTiles < heightInTiles + 1; yOffsetTiles++)
        {
            int offsetTileY = tile.Y - yOffsetTiles;

            Tile offsetTile = Main.tile[tile.X, offsetTileY];

            // Hitbox area is obstructed.
            if (offsetTile.HasTile && Main.tileSolid[offsetTile.TileType])
            {
                return false;
            }
        }

        return true;
    }
}
