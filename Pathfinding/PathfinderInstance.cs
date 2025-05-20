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

namespace Wayfarer.Pathfinding;

internal sealed class PathfinderInstance : IDisposable
{
    private volatile NavMeshParameters navMeshParameters;
    private readonly NavigatorParameters navigatorParameters;

    private Task<NavMesh> recalculateNavMesh;
    private Task recalculatePath;

    private readonly CancellationTokenSource cancellationTokenSource;

    private static readonly SemaphoreSlim pathSemaphore = new(Math.Max(Environment.ProcessorCount / 4, 1));

    public bool IsRecalculating => recalculatePath != null && !recalculatePath.IsCompleted;

    public PathfinderInstance(NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters)
    {
        this.navMeshParameters = navMeshParameters;
        this.navigatorParameters = navigatorParameters;

        cancellationTokenSource = new();

        RecalculateNavMesh();
    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }

    public void RecalculateNavMesh(Point? newCentre = null)
    {
        if (recalculateNavMesh is null || recalculateNavMesh.IsCompleted)
        {
            recalculateNavMesh = Task.Run(async () =>
            {
                await pathSemaphore.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    return NavMeshTask(newCentre);
                }
                finally
                {
                    pathSemaphore.Release();
                }
            }, cancellationTokenSource.Token);
        }
    }

    public void RecalculatePathfinding(Point[] starts, Action<PathResult> onComplete)
    {
        if (recalculatePath is null || recalculatePath.IsCompleted)
        {
            recalculatePath = Task.Run(async () =>
            {
                await pathSemaphore.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    await PathTask(starts, onComplete);
                }
                finally
                {
                    pathSemaphore.Release();
                }
            }, cancellationTokenSource.Token);
        }
    }

    private NavMesh NavMeshTask(Point? newCentre = null)
    {
        if (newCentre is not null)
            navMeshParameters = new(newCentre.Value, navMeshParameters.TileRadius, navMeshParameters.IsValidNode);

        NavMesh newMesh = new(navMeshParameters, navigatorParameters);

        newMesh.RegenerateNavMesh();

        return newMesh;
    }

    private async Task PathTask(Point[] starts, Action<PathResult> onComplete)
    {
        NavMesh navMesh;

        try
        {
            navMesh = await recalculateNavMesh;
        }
        // Throws if task is cancelled.
        catch
        {
            return;
        }

        bool successfulPath = RegeneratePath(starts, out bool alreadyAtGoal, out List<PathEdge> traversal, navMesh);

        PathResult resultCopy = successfulPath ? new(traversal, alreadyAtGoal, this) : null;

        Main.QueueMainThreadAction(() => onComplete.Invoke(resultCopy));
    }

    private bool RegeneratePath(Point[] starts, out bool alreadyAtGoal, out List<PathEdge> traversal, NavMesh navMesh)
    {
        alreadyAtGoal = false;
        traversal = [];

        Point start;

        bool successfulNode = false;

        int startNodeId = -1;
        int endNodeId = -1;

        foreach (Point potentialStart in starts)
        {
            successfulNode = TryGetStartAndEndNodes(potentialStart, out startNodeId, out endNodeId);

            if (successfulNode)
            {
                start = potentialStart;
                break;
            }
        }

        if (!successfulNode)
            return false;

        if (startNodeId == endNodeId)
        {
            alreadyAtGoal = true;
            return true;
        }

        List<PathEdge> path = AStar.RunAStar(startNodeId, endNodeId, navMesh.AdjacencyMap, navMesh.NodeIdToPoint);

        if (path is null)
            return false;

        traversal = path;

        return traversal.Count > 0;
    }

    private bool TryGetStartAndEndNodes(Point start, out int startNodeId, out int endNodeId)
    {
        startNodeId = endNodeId = -1;

        NavMesh navMesh = recalculateNavMesh.Result;

        if (!navMesh.ValidNodes.Contains(start))
        {
            return false;
        }

        HashSet<Point> accessibleNodes = GetAccessibleNodesBFS(start);

        Point end = navigatorParameters.FindIdealEndNodeFunction.Invoke(accessibleNodes);

        if (end == Point.Zero)
        {
            return false;
        }

        startNodeId = navMesh.PointToNodeId[start];
        endNodeId = navMesh.PointToNodeId[end];

        return true;
    }

    private HashSet<Point> GetAccessibleNodesBFS(Point start)
    {
        HashSet<Point> traversal = [];

        Queue<int> queue = [];

        HashSet<int> visited = [];

        NavMesh navMesh = recalculateNavMesh.Result;

        int source = navMesh.PointToNodeId[start];

        visited.Add(source);
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            if (navMesh.ValidNodes.Contains(navMesh.NodeIdToPoint[current]))
                traversal.Add(navMesh.NodeIdToPoint[current]);

            foreach (Edge edge in navMesh.AdjacencyMap[current])
            {
                int neighbour = edge.To;

                if (!visited.Contains(neighbour))
                {
                    visited.Add(neighbour);

                    if (navMesh.AdjacencyMap.ContainsKey(neighbour))
                        queue.Enqueue(neighbour);
                }
            }
        }

        return traversal;
    }

    public bool IsValidNode(Point node)
    {
        if (recalculateNavMesh is null || !recalculateNavMesh.IsCompleted)
            return false;

        return recalculateNavMesh.Result.ValidNodes.Contains(node);
    }

    public void DebugRender(SpriteBatch spriteBatch)
    {
        if (recalculateNavMesh is null || !recalculateNavMesh.IsCompleted)
            return;

        NavMesh navMesh = recalculateNavMesh.Result;

        foreach (int nodeId in navMesh.AdjacencyMap.Keys)
        {
            List<Edge> adjacent = navMesh.AdjacencyMap[nodeId];

            foreach (Edge edge in adjacent)
            {
                DrawEdge(spriteBatch, new PathEdge(navMesh.NodeIdToPoint[edge.From], navMesh.NodeIdToPoint[edge.To], edge.EdgeType));
            }
        }
    }

    public void DebugRenderPath(SpriteBatch spriteBatch, PathResult path)
    {
        if (recalculateNavMesh is null || !recalculateNavMesh.IsCompleted)
            return;

        IEnumerable<PathEdge> edges = path.Path.AsEnumerable<PathEdge>();

        foreach (PathEdge edge in edges)
        {
            DrawEdge(spriteBatch, edge);
        }
    }

    private void DrawEdge(SpriteBatch spriteBatch, PathEdge edge)
    {
        NavMesh navMesh = recalculateNavMesh.Result;

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
