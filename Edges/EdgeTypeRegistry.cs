using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Wayfarer.Data;

namespace Wayfarer.Edges;

internal static class EdgeTypeRegistry
{
    public static IReadOnlyDictionary<int, EdgeType> EdgeTypesById => edgeTypesById;

    public static IReadOnlyDictionary<Type, int> IdByEdgeType => idByEdgeType;

    private static int count;

    private static readonly Dictionary<int, EdgeType> edgeTypesById;
    private static readonly Dictionary<Type, int> idByEdgeType;

    public static void Register<T>() where T : EdgeType, new()
    {
        if (idByEdgeType.ContainsKey(typeof(T)))
            throw new ArgumentException($"Edge type {typeof(T)} is already registered!");

        T edgeType = new();

        edgeTypesById[count] = edgeType;
        idByEdgeType[typeof(T)] = count;

        count++;
    }

    static EdgeTypeRegistry()
    {
        edgeTypesById = [];
        idByEdgeType = [];

        Register<Walk>();
        Register<Fall>();
        Register<Jump>();
    }
}

public static class EdgeExtensions
{
    public static bool Is<T>(this PathEdge edge) where T : EdgeType => edge.EdgeType == EdgeTypeRegistry.IdByEdgeType[typeof(T)];
}

/// <summary>
/// Extend this type to create a custom edge definition. Paths are made of edges, each of which has a different type, and the type determines how your navigator should traverse that segment of the path.
/// Wayfarer predefines 3 edge types intended for ground navigators: <see cref="Walk"/>, <see cref="Fall"/>, and <see cref="Jump"/>.
/// </summary>
public abstract class EdgeType
{
    /// <summary>
    /// When pathfinding, Wayfarer will use this to connect a given node to other nodes using this edge definition. Iterate <paramref name="existingNodes"/> and call <see cref="AddNode(Point)"/> on nodes that are valid destinations from the <paramref name="node"/>.
    /// </summary>
    /// <param name="node">The start node currently being tested.</param>
    /// <param name="navigatorParameters">Parameters representing a path navigator.</param>
    /// <param name="existingNodes">The set of all valid nodes in the navmesh.</param>
    protected abstract void CalculateValidDestinationsFrom(Point node, NavigatorParameters navigatorParameters, IReadOnlySet<Point> existingNodes);

    /// <summary>
    /// This function determines the cost of an edge. A higher cost means this node will be LESS preferable when pathfinding.
    /// </summary>
    /// <param name="start">The start point of an edge.</param>
    /// <param name="end">The end point of an edge.</param>
    public abstract float CostFunction(Point start, Point end);

    /// <summary>
    ///  Since we don't want to allocate a new array for every list of valid points (potentially thousands of lists), this array's length is kept at the maximum possible destination count,
    ///  which is the total number of points. <see cref="PopulatePointSpan(Point, NavigatorParameters, IReadOnlySet{Point})"/> populates and returns the populated slice of the array.
    /// </summary>
    internal static Point[] PointPool = [];

    internal static object PoolLock = new();

    private int count;

    /// <summary>
    /// This method should only be called inside <see cref="CalculateValidDestinationsFrom(Point, NavigatorParameters, IReadOnlySet{Point})"/>. When called, a new edge is created linking the node being checked and <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">The candidate node.</param>
    protected void AddNode(Point destination)
    {
        PointPool[count] = destination;
        count++;

        if (count > PointPool.Length)
            throw new IndexOutOfRangeException($"Tried to add too many points! This only happens if you gave a node more destinations than there are valid nodes.");
    }

    internal Span<Point> PopulatePointSpan(Point node, NavigatorParameters navigatorParameters, IReadOnlySet<Point> existingNodes)
    {
        CalculateValidDestinationsFrom(node, navigatorParameters, existingNodes);

        Span<Point> result = new Span<Point>(PointPool).Slice(0, count);

        count = 0;

        return result;
    }
}
