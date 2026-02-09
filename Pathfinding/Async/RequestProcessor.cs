using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.API;
using Wayfarer.Data;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding.Async;

internal static class RequestProcessor
{
    private static readonly ConcurrentDictionary<WayfarerHandle, TaskCompletionSource<NavMesh>> navMeshes = [];

    private static CancellationTokenSource globalShutdownCts;

    private static bool initialised;

    public static void FirstTimeSetupIfNeeded()
    {
        if (initialised)
            return;

        globalShutdownCts = new();
        initialised = true;
    }

    public static void Shutdown()
    {
        if (!initialised)
            return;

        globalShutdownCts?.Cancel();

        navMeshes.Clear();
        globalShutdownCts?.Dispose();

        initialised = false;
    }

    public static async Task<NavMesh> RequestNavMeshAsync(
        WayfarerHandle handle,
        NavMeshParameters navMeshParams,
        NavigatorParameters navigatorParams,
        CancellationToken token = default
    )
    {
        using var linkedCts = token != default ? CancellationTokenSource.CreateLinkedTokenSource(globalShutdownCts.Token, token) : default;
        var linkedToken = token != default ? linkedCts.Token : globalShutdownCts.Token;
        var tcs = new TaskCompletionSource<NavMesh>();
        var pair = KeyValuePair.Create(handle, tcs);

        try
        {
            navMeshes.AddOrUpdate(
                handle,
                addValueFactory: _ => tcs,
                updateValueFactory: (_, existingTask) =>
                {
                    //tcs.Task.GetAwaiter().GetResult();
                    return tcs;
                }
            );

            var result = await ComputeNavMeshAsync(navMeshParams, navigatorParams, linkedToken);
            tcs.SetResult(result);

            return result;
        }
        catch (OperationCanceledException)
        {
            tcs.SetCanceled(token);
            navMeshes.TryRemove(pair);
            return null;
        }
        catch (Exception e)
        {
            tcs.SetException(e);
            navMeshes.TryRemove(pair);
            return null;
        }
    }

    public static async Task<PathResult> RequestPathfindingAsync(
        WayfarerHandle handle,
        NavMeshParameters navMeshParams,
        NavigatorParameters navigatorParams,
        Point[] starts,
        Action<PathResult> onComplete,
        CancellationToken token = default
    )
    {
        using var linkedCts = token != default ? CancellationTokenSource.CreateLinkedTokenSource(globalShutdownCts.Token, token) : default;
        var linkedToken = token != default ? linkedCts.Token : globalShutdownCts.Token;

        try
        {
            var result = await ComputePathAsync(handle, navMeshParams, navigatorParams, starts, onComplete, linkedToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    public static NavMesh TryGetNavMesh(WayfarerHandle handle)
    {
        if (navMeshes.TryGetValue(handle, out TaskCompletionSource<NavMesh> navMeshTcs))
        {
            if (navMeshTcs.Task.IsCompletedSuccessfully)
                return navMeshTcs.Task.Result;
            else
                return null;
        }
        else
            return null;
    }

    private static async Task<NavMesh> ComputeNavMeshAsync(NavMeshParameters navMeshParams, NavigatorParameters navigatorParams, CancellationToken linkedToken)
    {
        NavMesh newMesh = new(navMeshParams, navigatorParams);
        newMesh.RegenerateNavMesh(linkedToken);

        linkedToken.ThrowIfCancellationRequested();
        return newMesh;
    }

    private static async Task<PathResult> ComputePathAsync(
        WayfarerHandle handle,
        NavMeshParameters navMeshParams,
        NavigatorParameters navigatorParams,
        Point[] starts,
        Action<PathResult> onComplete,
        CancellationToken linkedToken
    )
    {
        NavMesh navMesh = await RequestNavMeshAsync(handle, navMeshParams, navigatorParams, linkedToken);

        if (navMesh is null)
        {
            return null;
        }

        linkedToken.ThrowIfCancellationRequested();

        bool successfulPath = RegeneratePath(linkedToken, starts, out bool alreadyAtGoal, out List<PathEdge> traversal, navMesh);
        PathResult result = successfulPath ? new(traversal, alreadyAtGoal) : null;

        Main.QueueMainThreadAction(() => onComplete(result));

        return result;
    }

    private static bool RegeneratePath(CancellationToken token, Point[] starts, out bool alreadyAtGoal, out List<PathEdge> traversal, NavMesh navMesh)
    {
        alreadyAtGoal = false;
        traversal = [];

        Point start;

        bool successfulNode = false;

        int startNodeId = -1;
        int endNodeId = -1;

        foreach (Point potentialStart in starts)
        {
            successfulNode = TryGetStartAndEndNodes(potentialStart, out startNodeId, out endNodeId, navMesh);

            if (successfulNode)
            {
                start = potentialStart;
                break;
            }

            token.ThrowIfCancellationRequested();
        }

        if (!successfulNode)
            return false;

        if (startNodeId == endNodeId)
        {
            alreadyAtGoal = true;
            return true;
        }

        List<PathEdge> path = AStar.RunAStar(token, startNodeId, endNodeId, navMesh.AdjacencyMap, navMesh.NodeIdToPoint);

        if (path is null)
            return false;

        traversal = path;

        return traversal.Count > 0;
    }

    private static bool TryGetStartAndEndNodes(Point start, out int startNodeId, out int endNodeId, NavMesh navMesh)
    {
        startNodeId = endNodeId = -1;

        if (!navMesh.ValidNodes.Contains(start))
        {
            return false;
        }

        HashSet<Point> accessibleNodes = GetAccessibleNodesBFS(start, navMesh);

        Point end = navMesh.NavigatorParameters.FindIdealEndNodeFunction.Invoke(accessibleNodes);

        if (end == Point.Zero)
        {
            return false;
        }

        startNodeId = navMesh.PointToNodeId[start];
        endNodeId = navMesh.PointToNodeId[end];

        return true;
    }

    private static HashSet<Point> GetAccessibleNodesBFS(Point start, NavMesh navMesh)
    {
        HashSet<Point> traversal = [];

        Queue<int> queue = [];

        HashSet<int> visited = [];

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
}
