using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

internal sealed class PathfinderInstance(NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters) : IDisposable
{
    private readonly CancellationTokenSource cancellationTokenSource = new();

    private volatile NavMeshParameters navMeshParameters = navMeshParameters;

    private readonly NavigatorParameters navigatorParameters = navigatorParameters;

    private Task pathfindingWorker;

    private volatile bool recalculateNavMesh = true;
    private volatile bool recalculatePathResult;

    private readonly ConcurrentDictionary<Point, int> pointToNodeId = [];
    private readonly ConcurrentDictionary<int, Point> nodeIdToPoint = [];
    private readonly ConcurrentDictionary<int, List<Edge>> adjacencyMap = [];

    private readonly HashSet<Point> validNodes = [];

    private void StartWorkerThread()
    {
        pathfindingWorker = Task.Run(WorkerThread, cancellationTokenSource.Token);
    }

    private void WorkerThread()
    {
        while (!cancellationTokenSource.IsCancellationRequested)
        {
            HandleNavMesh();
            HandlePathfinding();
        }
    }

    private void RegenerateNavMesh()
    {
        pointToNodeId.Clear();
        nodeIdToPoint.Clear();
        adjacencyMap.Clear();
        validNodes.Clear();

        int minX = Math.Clamp(navMeshParameters.CentralTile.X - navMeshParameters.TileRadius, 0, Main.maxTilesX);
        int minY = Math.Clamp(navMeshParameters.CentralTile.Y - navMeshParameters.TileRadius, 0, Main.maxTilesY);
        int maxX = Math.Clamp(navMeshParameters.CentralTile.X + navMeshParameters.TileRadius, 0, Main.maxTilesX);
        int maxY = Math.Clamp(navMeshParameters.CentralTile.Y + navMeshParameters.TileRadius, 0, Main.maxTilesY);

        int centreX = navMeshParameters.CentralTile.X;
        int centreY = navMeshParameters.CentralTile.Y;

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                Point node = new(x + centreX, y + centreY);

                if (navMeshParameters.IsValidNode.Invoke(node, navigatorParameters.NavigatorHitbox))
                {
                    int nodeId = (int)Main.tile[node.X, node.Y].TileId;

                    pointToNodeId[node] = nodeId;
                    nodeIdToPoint[nodeId] = node;

                    validNodes.Add(node);
                }
            }
        }

        RegenerateAdjacencyMap();
    }

    private void RegenerateAdjacencyMap()
    {
        lock (EdgeType.PointPool)
        {
            EdgeType.PointPool = new Point[pointToNodeId.Count];

            foreach (Point validTile in pointToNodeId.Keys)
            {
                int nodeId = pointToNodeId[validTile];
                adjacencyMap[nodeId] = [];

                foreach (var edgeType in EdgeTypeRegistry.EdgeTypesById)
                {
                    int edgeId = edgeType.Key;

                    Span<Point> validPoints = edgeType.Value.PopulatePointSpan(validTile, navigatorParameters, validNodes);

                    foreach (Point destination in validPoints)
                    {
                        float cost = edgeType.Value.CostFunction(validTile, destination);

                        adjacencyMap[nodeId].Add(new Edge(pointToNodeId[validTile], pointToNodeId[destination], edgeId, cost));
                    }
                }
            }
        }
    }

    public void RegeneratePath()
    {

    }

    public void Dispose()
    {
        cancellationTokenSource.Cancel();
    }

    public void RecalculateNavMesh(NavMeshParameters newParameters = null)
    {
        if (newParameters is not null)
            navMeshParameters = newParameters;

        recalculateNavMesh = true;
    }

    public void RecalculatePath()
    {
        recalculatePathResult = true;
    }

    private void HandleNavMesh()
    {
        if (!recalculateNavMesh)
            return;

        RegenerateNavMesh();

        recalculateNavMesh = false;
    }

    private void HandlePathfinding()
    {
        if (!recalculatePathResult)
            return;

        RegeneratePath();

        recalculatePathResult = false;
    }
}
