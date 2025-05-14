using System.Collections.Concurrent;
using Wayfarer.Configuration;

namespace Wayfarer.Pathfinding;

internal sealed class Wayfarer
{
    private static readonly ConcurrentDictionary<int, PathfinderInstance> instances = [];

    public static Handle CreatePathfindingInstance(NavMeshParameters navMeshParameters)
    {
        instances[0] = new PathfinderInstance(navMeshParameters);

        return new(0);
    }
}
