using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

internal sealed class NavMesh
{
    public readonly NavMeshParameters NavMeshParameters;
    public readonly NavigatorParameters NavigatorParameters;

    public Dictionary<Point, int> PointToNodeId;
    public Dictionary<int, Point> NodeIdToPoint;
    public Dictionary<int, List<Edge>> AdjacencyMap;

    public HashSet<Point> ValidNodes;

    public NavMesh(NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters)
    {
        NavMeshParameters = navMeshParameters;
        NavigatorParameters = navigatorParameters;

        PointToNodeId = [];
        NodeIdToPoint = [];
        AdjacencyMap = [];
        ValidNodes = [];
    }

    public NavMesh Clone()
    {
        return new NavMesh(NavMeshParameters, NavigatorParameters)
        {
            PointToNodeId = new(PointToNodeId),
            NodeIdToPoint = new(NodeIdToPoint),
            AdjacencyMap = new(AdjacencyMap),
            ValidNodes = new(ValidNodes)
        };
    }

    // TODO: make async so that it can be cancelled more easily.
    public void RegenerateNavMesh(CancellationToken token)
    {
        PointToNodeId.Clear();
        NodeIdToPoint.Clear();
        AdjacencyMap.Clear();
        ValidNodes.Clear();

        int minX = Math.Clamp(NavMeshParameters.CentralTile.X - NavMeshParameters.TileRadius, 0, Main.maxTilesX);
        int minY = Math.Clamp(NavMeshParameters.CentralTile.Y - NavMeshParameters.TileRadius, 0, Main.maxTilesY);
        int maxX = Math.Clamp(NavMeshParameters.CentralTile.X + NavMeshParameters.TileRadius, 0, Main.maxTilesX);
        int maxY = Math.Clamp(NavMeshParameters.CentralTile.Y + NavMeshParameters.TileRadius, 0, Main.maxTilesY);

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                Point node = new(x, y);

                if (NavMeshParameters.IsValidNode.Invoke(node, NavigatorParameters.NavigatorHitbox))
                {
                    int nodeId = (int)Main.tile[x, y].TileId;

                    PointToNodeId[node] = nodeId;
                    NodeIdToPoint[nodeId] = node;

                    ValidNodes.Add(node);
                }
            }

            token.ThrowIfCancellationRequested();
        }

        RegenerateAdjacencyMap(token);
    }

    private void RegenerateAdjacencyMap(CancellationToken token)
    {
        lock (EdgeType.PoolLock)
        {
            EdgeType.PointPool = new Point[PointToNodeId.Count];

            foreach (Point validTile in PointToNodeId.Keys)
            {
                int nodeId = PointToNodeId[validTile];
                AdjacencyMap[nodeId] = [];

                foreach (var edgeType in EdgeTypeRegistry.EdgeTypesById)
                {
                    int edgeId = edgeType.Key;

                    Span<Point> validPoints = edgeType.Value.PopulatePointSpan(validTile, NavigatorParameters, ValidNodes);

                    foreach (Point destination in validPoints)
                    {
                        float cost = edgeType.Value.CostFunction(validTile, destination);

                        AdjacencyMap[nodeId].Add(new Edge(PointToNodeId[validTile], PointToNodeId[destination], edgeId, cost));
                    }
                }

                token.ThrowIfCancellationRequested();
            }
        }
    }
}
