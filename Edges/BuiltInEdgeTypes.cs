using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.Data;

namespace Wayfarer.Edges;

public sealed class Walk : EdgeType
{
    private static readonly Point[] Directions = [new(-1, -1), new(-1, 1), new(1, 1), new(1, -1), new(1, 0), new(-1, 0)];

    private const float WalkCost = 1;

    public override float CostFunction(Point start, Point end)
    {
        int chebyshev = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));

        return WalkCost * chebyshev;
    }

    protected override void CalculateValidDestinationsFrom(Point node, NavigatorParameters navigatorParameters, IReadOnlySet<Point> existingNodes)
    {
        int currentXOffset = 0;
        int currentYOffset = 0;

        foreach (Point offset in Directions)
        {
            currentXOffset += offset.X;
            currentYOffset += offset.Y;

            Point neighbour = new(node.X + currentXOffset, node.Y + currentYOffset);

            int chebyshev = Math.Max(Math.Abs(currentXOffset), Math.Abs(currentYOffset));

            while (existingNodes.Contains(neighbour))
            {
                AddNode(neighbour);

                currentXOffset += offset.X;
                currentYOffset += offset.Y;
                neighbour = new(node.X + currentXOffset, node.Y + currentYOffset);
            }

            currentXOffset = 0;
            currentYOffset = 0;
        }
    }
}

public sealed class Fall : EdgeType
{
    private static readonly int[] Directions = [-1, 1];

    private const float FallCost = 2;
    private const int MaxDropTiles = 64;

    public override float CostFunction(Point start, Point end) => FallCost;

    protected override void CalculateValidDestinationsFrom(Point node, NavigatorParameters navigatorParameters, IReadOnlySet<Point> existingNodes)
    {
        foreach (int direction in Directions)
        {
            for (int yDrop = 0; yDrop < MaxDropTiles; yDrop++)
            {
                Point neighbouringPoint = new(direction + node.X, yDrop + node.Y);

                // End the downward raycast if it encounters a standable tile or exits the world.
                if (!WorldGen.InWorld(neighbouringPoint.X, neighbouringPoint.Y))
                    break;

                if (HitboxCanStandOnTile(neighbouringPoint.X, neighbouringPoint.Y))
                {
                    if (yDrop >= 2 && existingNodes.Contains(neighbouringPoint))
                    {
                        // TODO: make falling account for hitboxes.
                        AddNode(neighbouringPoint);
                    }

                    break;
                }
            }
        }
    }

    private static bool HitboxCanStandOnTile(int x, int y)
    {
        if (!WorldGen.InWorld(x, y))
            return false;

        Tile tile = Main.tile[x, y];

        if (!tile.HasTile)
            return false;
        else if (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])
        {
            return true;
        }

        return false;
    }
}

public sealed class Jump : EdgeType
{
    private const float JumpCost = 4;

    public override float CostFunction(Point start, Point end)
    {
        float jumpDistanceSq = Vector2.DistanceSquared(new(start.X, start.Y), new(end.X, end.Y));

        return Math.Max(JumpCost, jumpDistanceSq);
    }

    protected override void CalculateValidDestinationsFrom(Point node, NavigatorParameters navigatorParameters, IReadOnlySet<Point> existingNodes)
    {
        int maxHorizontalJumpDistance = navigatorParameters.MaxJumpRanges.X;
        int maxVerticalJumpHeight = navigatorParameters.MaxJumpRanges.Y;

        Rectangle hitbox = navigatorParameters.NavigatorHitbox;

        int minimumXDistanceToRequireJump = (int)Math.Ceiling((float)hitbox.Width / 16);

        for (int yOffset = -maxVerticalJumpHeight; yOffset <= maxVerticalJumpHeight; yOffset++)
        {
            for (int xOffset = -maxHorizontalJumpDistance; xOffset <= maxHorizontalJumpDistance; xOffset++)
            {
                if (Math.Abs(xOffset) < minimumXDistanceToRequireJump)
                    continue;

                Point candidate = new(node.X + xOffset, node.Y + yOffset);

                if (existingNodes.Contains(candidate) && IsJumpPossible(node, candidate, navigatorParameters))
                {
                    AddNode(candidate);
                }
            }
        }
    }

    private static bool IsJumpPossible(Point start, Point end, NavigatorParameters navigatorParameters)
    {
        Rectangle hitbox = navigatorParameters.NavigatorHitbox;

        Vector2 startVector = new Vector2(start.X * 16, start.Y * 16) + new Vector2(8, 0);
        Vector2 endVector = new Vector2(end.X * 16, end.Y * 16) + new Vector2(8, 0);

        Vector2 u = navigatorParameters.JumpFunction.Invoke(startVector, endVector, navigatorParameters.GravityFunction);

        float dx = endVector.X - startVector.X;
        float dy = endVector.Y - startVector.Y;

        // We cannot jump into the floor.
        if (u.Y > 0)
            return false;

        if (dy > 0 && (2 * Math.Abs(dx) < Math.Abs(dy)))
            return false;

        float npcGravity = navigatorParameters.GravityFunction.Invoke();
        float discriminant = (u.Y * u.Y) + (2 * npcGravity * dy);

        if (discriminant < 0)
            return false;

        float tof = (-u.Y + MathF.Sqrt(discriminant)) / npcGravity;

        float tileSize = 16;

        float dt = tileSize / (u.Length() * MathF.Sqrt(2));

        for (float t = dt; t <= tof; t += dt)
        {
            float x = startVector.X + (u.X * t);
            float y = startVector.Y + (u.Y * t) + (0.5f * npcGravity * t * t);

            int tileX = (int)Math.Floor(x / 16);
            int tileY = (int)Math.Floor(y / 16);

            // Don't check collision involving the start or end node, we know they're valid already.
            if ((tileX == start.X && tileY == start.Y) || (tileX == end.X && tileY == end.Y))
            {
                continue;
            }

            if (!HitboxCanIntersectTile(tileX, tileY))
                return false;

            // If the test point is very close to the destination, be lenient with checks.
            if (Vector2.Distance(endVector, new(x, y)) < hitbox.Width * 2)
                continue;

            // tileX and tileY indicate the bottom-left of the checked area.
            int heightInTiles = (int)Math.Ceiling(hitbox.Height / 16f);

            // Check tiles above the standing tile to make sure the NPC's hitbox can fit.
            for (int yOffsetTiles = 1; yOffsetTiles < heightInTiles + 1; yOffsetTiles++)
            {
                int offsetTileY = tileY - yOffsetTiles;

                if (!HitboxCanIntersectTile(tileX, offsetTileY))
                    return false;
            }
        }

        return true;
    }

    private static bool HitboxCanIntersectTile(int x, int y)
    {
        if (!WorldGen.InWorld(x, y))
            return false;

        Tile tile = Main.tile[x, y];

        if (!tile.HasTile)
            return true;
        else if (Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
        {
            return false;
        }

        return true;
    }
}