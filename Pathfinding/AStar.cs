using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

internal static class AStar
{
    public static List<PathEdge> RunAStar(int startId, int endId, IReadOnlyDictionary<int, List<Edge>> adjacencyMap, IReadOnlyDictionary<int, Point> nodeIdToPoint)
    {
        PriorityQueue<int, float> frontier = new();
        HashSet<int> openSet = [];

        // For a node N, cameFrom[n] is the Edge preceding it on the cheapest path from the start to N currently known.
        // With edges, cameFrom[edge.To] = edge.
        Dictionary<int, Edge> cameFrom = [];

        Dictionary<int, float> gScore = [];
        Dictionary<int, float> fScore = [];

        foreach (int node in adjacencyMap.Keys)
        {
            gScore[node] = float.PositiveInfinity;
            fScore[node] = float.PositiveInfinity;
        }

        gScore[startId] = 0;
        fScore[startId] = Heuristic(startId, endId, 1, nodeIdToPoint);

        frontier.Enqueue(startId, fScore[startId]);
        openSet.Add(startId);

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();

            openSet.Remove(current);

            if (current == endId)
                return Reconstruct(cameFrom, endId, nodeIdToPoint);

            List<Edge> neighbours = adjacencyMap[current];

            foreach (Edge edge in neighbours)
            {
                int neighbouringNode = edge.To;

                float tentativeG = gScore[current] + edge.Cost;

                if (tentativeG < gScore[neighbouringNode])
                {
                    cameFrom[neighbouringNode] = edge;
                    gScore[neighbouringNode] = tentativeG;
                    fScore[neighbouringNode] = tentativeG + Heuristic(neighbouringNode, endId, edge.Cost, nodeIdToPoint);

                    if (!openSet.Contains(neighbouringNode))
                    {
                        frontier.Enqueue(neighbouringNode, fScore[neighbouringNode]);
                        openSet.Add(neighbouringNode);
                    }
                }
            }
        }

        return null;
    }

    private static List<PathEdge> Reconstruct(Dictionary<int, Edge> cameFrom, int current, IReadOnlyDictionary<int, Point> nodeIdToPoint)
    {
        List<PathEdge> sequence = [];

        while (cameFrom.ContainsKey(current))
        {
            Edge edge = cameFrom[current];
            PathEdge pathEdge = new(nodeIdToPoint[edge.From], nodeIdToPoint[edge.To], edge.EdgeType);

            sequence.Add(pathEdge);

            current = cameFrom[current].From;
        }

        sequence.Reverse();

        return sequence;
    }

    private static float Heuristic(int start, int end, float cost, IReadOnlyDictionary<int, Point> nodeIdToPoint)
    {
        Point nodePoint = nodeIdToPoint[start];
        Point goalPoint = nodeIdToPoint[end];

        float dx = nodePoint.X - goalPoint.X;
        float dy = nodePoint.Y - goalPoint.Y;

        float distance = Math.Abs(dx) + Math.Abs(dy);

        return distance * cost;
    }
}
