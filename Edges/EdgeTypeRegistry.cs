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

public abstract class EdgeType
{
    protected abstract void CalculateValidDestinationsFrom(Point node, NavigatorParameters navigatorParameters, IReadOnlySet<Point> existingNodes);

    public abstract float CostFunction(Point start, Point end);

    /// <summary>
    ///  Since we don't want to allocate a new array for every list of valid points (potentially thousands of lists), this array's length is kept at the maximum possible destination count,
    ///  which is the total number of points. <see cref="PopulatePointSpan(Point, IReadOnlySet{Point})"/> populates and returns the populated slice of the array.
    /// </summary>
    internal static Point[] PointPool = [];

    internal static object PoolLock = new object();

    private int count;

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
