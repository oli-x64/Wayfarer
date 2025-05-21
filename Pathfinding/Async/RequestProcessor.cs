using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Wayfarer.API;
using Wayfarer.Data;
using Wayfarer.Edges;
using Microsoft.Xna.Framework;

namespace Wayfarer.Pathfinding.Async;

internal static class RequestProcessor
{
    private static readonly int MaxConcurrentTasks = Math.Max(Environment.ProcessorCount / 2, 1);

    private static readonly List<Task> processors = [];
    private static readonly ConcurrentQueue<AsyncRequest> navMeshRequests = [];
    private static readonly ConcurrentQueue<AsyncRequest> pathRequests = [];
    private static readonly ConcurrentDictionary<WayfarerHandle, Task<NavMesh>> navMeshes = [];

    private static CancellationTokenSource globalShutdownCts;

    private static bool initialised;

    public static void FirstTimeSetupIfNeeded()
    {
        if (initialised)
            return;

        globalShutdownCts = new();

        for (int i = 0; i < MaxConcurrentTasks; i++)
        {
            processors.Add(Task.Factory.StartNew(
                () => WorkerLoop(globalShutdownCts.Token),
                globalShutdownCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            );
        }

        initialised = true;
    }

    public static void Shutdown()
    {
        if (!initialised)
            return;

        globalShutdownCts?.Cancel();

        while (pathRequests.TryDequeue(out AsyncRequest request))
        {
            if (request is PathfindingRequest pathRequest)
                pathRequest.CompletionSource.TrySetCanceled();
        }
        while (navMeshRequests.TryDequeue(out AsyncRequest request))
        {
            if (request is NavMeshRequest navRequest)
                navRequest.CompletionSource.TrySetCanceled();
        }

        if (processors.Count != 0)
        {
            try
            {
                Task.WaitAll(processors.ToArray());
            }
            catch (OperationCanceledException) { }
        }

        navMeshes.Clear();
        globalShutdownCts?.Dispose();
        processors.Clear();

        initialised = false;
    }

    public static Task<NavMesh> RequestNavMeshAsync(
        WayfarerHandle source,
        NavMeshParameters navMeshParameters,
        NavigatorParameters navigatorParameters,
        bool forceRegenerate = true,
        CancellationToken token = default)
    {
        if (forceRegenerate)
        {
            NavMeshRequest request = new(source, navMeshParameters, navigatorParameters, token == default ? globalShutdownCts.Token : token);

            navMeshRequests.Enqueue(request);

            navMeshes.AddOrUpdate(
                source,
                addValueFactory: _ => request.CompletionSource.Task,
                updateValueFactory: (_, existingTask) => request.CompletionSource.Task
            );

            return request.CompletionSource.Task;
        }
        else
        {
            return navMeshes.GetOrAdd(source, (keyParams) =>
            {
                NavMeshRequest request = new(source, navMeshParameters, navigatorParameters, token == default ? globalShutdownCts.Token : token);

                navMeshRequests.Enqueue(request);

                return request.CompletionSource.Task;
            });
        }
    }

    public static Task<PathResult> RequestPathfindingAsync(
        WayfarerHandle source,
        NavMeshParameters navMeshKey,
        NavigatorParameters navigatorParams,
        Point[] starts,
        Action<PathResult> onComplete,
        CancellationToken token = default)
    {
        PathfindingRequest request = new(source, navMeshKey, navigatorParams, starts, onComplete, token == default ? globalShutdownCts.Token : token);

        pathRequests.Enqueue(request);

        return request.CompletionSource.Task;
    }

    public static NavMesh TryGetNavMesh(WayfarerHandle handle)
    {
        if (navMeshes.TryGetValue(handle, out Task<NavMesh> navMeshTask))
        {
            if (navMeshTask.IsCompletedSuccessfully)
                return navMeshTask.Result;
            else
                return null;
        }
        else
            return null;
    }

    private static async Task WorkerLoop(CancellationToken globalShutdownToken)
    {
        try
        {
            while (!globalShutdownToken.IsCancellationRequested)
            {
                AsyncRequest request = null;

                if (navMeshRequests.TryDequeue(out AsyncRequest navRequest))
                {
                    request = navRequest;
                }
                else if (pathRequests.TryDequeue(out AsyncRequest pathRequest))
                {
                    request = pathRequest;
                }

                if (request != null)
                {
                    using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(globalShutdownToken, request.Token);

                    CancellationToken linkedToken = linkedCts.Token;

                    try
                    {
                        linkedToken.ThrowIfCancellationRequested();

                        if (request is NavMeshRequest navMeshRequest)
                        {
                            await ConsumeNavMeshRequest(navMeshRequest, linkedToken).ConfigureAwait(false);
                        }
                        else if (request is PathfindingRequest pathfindingRequest)
                        {
                            await ConsumePathfindingRequest(pathfindingRequest, linkedToken).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (request is NavMeshRequest navMeshRequest)
                            navMeshRequest.CompletionSource.TrySetCanceled(request.Token.IsCancellationRequested ? request.Token : globalShutdownToken);
                        else if (request is PathfindingRequest pathfindingRequest)
                            pathfindingRequest.CompletionSource.TrySetCanceled(request.Token.IsCancellationRequested ? request.Token : globalShutdownToken);
                    }
                    catch (Exception e)
                    {
                        if (request is NavMeshRequest navMeshRequest)
                            navMeshRequest.CompletionSource.TrySetException(e);
                        else if (request is PathfindingRequest pathfindingRequest)
                            pathfindingRequest.CompletionSource.TrySetException(e);
                    }
                }
                else
                {
                    await Task.Delay(50, globalShutdownToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static async Task ConsumeNavMeshRequest(NavMeshRequest request, CancellationToken linkedToken)
    {
        try
        {
            NavMesh newMesh = new(request.NavMeshParameters, request.NavigatorParameters);
            newMesh.RegenerateNavMesh(linkedToken);

            linkedToken.ThrowIfCancellationRequested();

            request.CompletionSource.TrySetResult(newMesh);
        }
        catch (OperationCanceledException)
        {
            request.CompletionSource.TrySetCanceled(linkedToken);
            navMeshes.TryRemove(KeyValuePair.Create(request.Source, request.CompletionSource.Task));
        }
        catch (Exception e)
        {
            request.CompletionSource.TrySetException(e);
            navMeshes.TryRemove(KeyValuePair.Create(request.Source, request.CompletionSource.Task));
        }
    }

    private static async Task ConsumePathfindingRequest(PathfindingRequest request, CancellationToken linkedToken)
    {
        try
        {
            NavMesh navMesh = await RequestNavMeshAsync(request.Source, request.NavMeshParameters, request.NavigatorParameters, true, linkedToken);

            if (navMesh is null)
            {
                request.CompletionSource.TrySetResult(null);
                return;
            }

            linkedToken.ThrowIfCancellationRequested();

            bool successfulPath = RegeneratePath(linkedToken, request.Starts, out bool alreadyAtGoal, out List<PathEdge> traversal, navMesh);

            PathResult result = successfulPath ? new(traversal, alreadyAtGoal) : null;

            request.CompletionSource.TrySetResult(result);

            Main.QueueMainThreadAction(() => request.OnComplete.Invoke(request.CompletionSource.Task.Result));
        }
        catch (OperationCanceledException)
        {
            request.CompletionSource.TrySetCanceled(linkedToken);
        }
        catch (Exception e)
        {
            request.CompletionSource.TrySetException(e);
        }
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
