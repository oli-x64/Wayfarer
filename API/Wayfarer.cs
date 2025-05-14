using System.Collections.Generic;
using Wayfarer.Configuration;
using Wayfarer.Pathfinding;

namespace Wayfarer.API;

public sealed class Wayfarer
{
    private static readonly Dictionary<int, PathfinderInstance> instances = [];

    public static Handle CreatePathfindingInstance(NavMeshParameters navMeshParameters)
    {
        instances[0] = new PathfinderInstance(navMeshParameters);

        return new(0);
    }

    public static void RecalculatePath(Handle handle) => instances[handle.ID].Recalculate();

    // TODO: use more efficient pooling method over dictionary.
    internal static void Dispose(Handle handle)
    {
        instances[handle.ID].Dispose();
        instances.Remove(handle.ID);
    }
}
