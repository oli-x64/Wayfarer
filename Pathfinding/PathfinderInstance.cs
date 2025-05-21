using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Wayfarer.API;
using Wayfarer.Data;
using Wayfarer.Edges;
using Wayfarer.Pathfinding.Async;

namespace Wayfarer.Pathfinding;

internal sealed class PathfinderInstance : IDisposable
{
    private readonly WayfarerHandle handle;

    private volatile NavMeshParameters navMeshParameters;
    private readonly NavigatorParameters navigatorParameters;

    private readonly CancellationTokenSource cancellationTokenSource;

    public PathfinderInstance(WayfarerHandle handle, NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters)
    {
        this.handle = handle;
        this.navMeshParameters = navMeshParameters;
        this.navigatorParameters = navigatorParameters;

        cancellationTokenSource = new();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }

    public void RecalculateNavMesh(Point? newCentre = null)
    {
        if (newCentre is not null)
            navMeshParameters = new(newCentre.Value, navMeshParameters.TileRadius, navMeshParameters.IsValidNode);

        RequestProcessor.RequestNavMeshAsync(handle, navMeshParameters, navigatorParameters, true, cancellationTokenSource.Token);
    }

    public void RecalculatePathfinding(Point[] starts, Action<PathResult> onComplete)
    {
        RequestProcessor.RequestPathfindingAsync(handle, navMeshParameters, navigatorParameters, starts, onComplete, cancellationTokenSource.Token);
    }

    public bool IsValidNode(Point node)
    {
        NavMesh navMesh = RequestProcessor.TryGetNavMesh(handle);

        return navMesh is not null && navMesh.ValidNodes.Contains(node);
    }

    public void DebugRender(SpriteBatch spriteBatch)
    {
        NavMesh navMesh = RequestProcessor.TryGetNavMesh(handle);

        if (navMesh is null)
            return;

        foreach (int nodeId in navMesh.AdjacencyMap.Keys)
        {
            List<Edge> adjacent = navMesh.AdjacencyMap[nodeId];

            foreach (Edge edge in adjacent)
            {
                DrawEdge(spriteBatch, new PathEdge(navMesh.NodeIdToPoint[edge.From], navMesh.NodeIdToPoint[edge.To], edge.EdgeType), navMesh);
            }
        }
    }

    public void DebugRenderPath(SpriteBatch spriteBatch, PathResult path)
    {
        NavMesh navMesh = RequestProcessor.TryGetNavMesh(handle);

        if (navMesh is null)
            return;

        IEnumerable<PathEdge> edges = path.Path.AsEnumerable<PathEdge>();

        foreach (PathEdge edge in edges)
        {
            DrawEdge(spriteBatch, edge, navMesh);
        }
    }

    private void DrawEdge(SpriteBatch spriteBatch, PathEdge edge, NavMesh navMesh)
    {
        if (!navMesh.ValidNodes.Contains(edge.From) || !navMesh.ValidNodes.Contains(edge.To))
            return;

        Point origin = edge.From;
        Point adjacent = edge.To;

        Vector2 originWorld = new Vector2(origin.X * 16, origin.Y * 16) + new Vector2(8);

        if (edge.Is<Jump>())
        {
            Vector2 adjacentWorld = new Vector2(adjacent.X * 16, adjacent.Y * 16) + new Vector2(8);

            Color color = originWorld.Y > adjacentWorld.Y ? Color.Green : Color.Blue;

            List<Vector2> points = GenerateDebugDrawJumpPoints(originWorld, adjacentWorld);

            if (points.Count < 2)
                return;

            for (int i = 0; i < points.Count; i++)
            {
                if (i == 0)
                    continue;

                Vector2 prev = points[i - 1];
                Vector2 next = points[i];

                Utils.DrawLine(spriteBatch, prev, next, color, color, 1);
            }
        }
        else
        {
            Vector2 adjacentWorld = new Vector2(adjacent.X * 16, adjacent.Y * 16) + new Vector2(8);

            Color color = edge.Is<Walk>() ? Color.Yellow : Color.Orange;

            Utils.DrawLine(spriteBatch, originWorld, adjacentWorld, color, color, 1);
        }

        Vector2 screen = originWorld - Main.screenPosition;

        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)screen.X - 2, (int)screen.Y - 2, 4, 4), Color.Red);
    }

    private List<Vector2> GenerateDebugDrawJumpPoints(Vector2 start, Vector2 end)
    {
        int samplePointCount = (int)(Vector2.Distance(start, end) / 8);

        // If negative, start is larger in Y, which means Y is lower.
        bool endIsOver2BlocksHigherThanStart = (start.Y - end.Y) / 16 >= 2 && Math.Abs(end.X - start.X) / 16 <= 2;

        Vector2 midpoint;

        // Jump to a higher position.
        if (endIsOver2BlocksHigherThanStart)
        {
            // Choose the highest Y of the two as the Y middle point.
            float y = Math.Min(start.Y, end.Y);

            // Choose the X coordinate of the point with the highest Y as the X middle point.
            // If the start is lower than the end, then it's start.X.
            midpoint = new(start.X, end.Y);
        }
        // Jump to a lower or equal position.
        else
        {
            float yImpulse = Math.Min(Math.Abs(start.X - end.X), navigatorParameters.MaxJumpRanges.Y * 16);

            midpoint = new((start.X + end.X) / 2, start.Y - yImpulse);
        }

        Vector2[] controlPoints = [start, midpoint, end];

        return BezierCurve.GetPoints(controlPoints, samplePointCount);
    }
}
