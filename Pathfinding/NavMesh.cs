using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

internal class NavMesh
{
    private readonly NavMeshParameters navMeshParameters;
    private readonly NavigatorParameters navigatorParameters;

    public Dictionary<Point, int> PointToNodeId;
    public Dictionary<int, Point> NodeIdToPoint;
    public Dictionary<int, List<Edge>> AdjacencyMap;

    public HashSet<Point> ValidNodes;

    public NavMesh(NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters)
    {
        this.navMeshParameters = navMeshParameters;
        this.navigatorParameters = navigatorParameters;

        PointToNodeId = [];
        NodeIdToPoint = [];
        AdjacencyMap = [];
        ValidNodes = [];
    }

    public NavMesh Clone()
    {
        return new NavMesh(navMeshParameters, navigatorParameters)
        {
            PointToNodeId = new(PointToNodeId),
            NodeIdToPoint = new(NodeIdToPoint),
            AdjacencyMap = new(AdjacencyMap),
            ValidNodes = new(ValidNodes)
        };
    }

    public void RegenerateNavMesh()
    {
        PointToNodeId.Clear();
        NodeIdToPoint.Clear();
        AdjacencyMap.Clear();
        ValidNodes.Clear();

        int minX = Math.Clamp(navMeshParameters.CentralTile.X - navMeshParameters.TileRadius, 0, Main.maxTilesX);
        int minY = Math.Clamp(navMeshParameters.CentralTile.Y - navMeshParameters.TileRadius, 0, Main.maxTilesY);
        int maxX = Math.Clamp(navMeshParameters.CentralTile.X + navMeshParameters.TileRadius, 0, Main.maxTilesX);
        int maxY = Math.Clamp(navMeshParameters.CentralTile.Y + navMeshParameters.TileRadius, 0, Main.maxTilesY);

        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                Point node = new(x, y);

                if (navMeshParameters.IsValidNode.Invoke(node, navigatorParameters.NavigatorHitbox))
                {
                    int nodeId = (int)Main.tile[x, y].TileId;

                    PointToNodeId[node] = nodeId;
                    NodeIdToPoint[nodeId] = node;

                    ValidNodes.Add(node);
                }
            }
        }

        RegenerateAdjacencyMap();
    }

    private void RegenerateAdjacencyMap()
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

                    Span<Point> validPoints = edgeType.Value.PopulatePointSpan(validTile, navigatorParameters, ValidNodes);

                    foreach (Point destination in validPoints)
                    {
                        float cost = edgeType.Value.CostFunction(validTile, destination);

                        AdjacencyMap[nodeId].Add(new Edge(PointToNodeId[validTile], PointToNodeId[destination], edgeId, cost));
                    }
                }
            }
        }
    }
}
