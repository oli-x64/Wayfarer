using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Pathfinding;

namespace Wayfarer.API;

public sealed class Wayfarer
{
    private const int MaxInstances = 2048;

    private static readonly SparseSet<PathfinderInstance> pathfinders = new(1, 1);

    private static readonly PriorityQueue<int, int> freeIds = new();

    private static int nextIndex;

    public static bool TryCreatePathfindingInstance(NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters, out Handle handle)
    {
        handle = Handle.Invalid;

        int newIndex;

        if (freeIds.Count > 0)
            newIndex = freeIds.Dequeue();
        else
        {
            newIndex = nextIndex++;

            if (nextIndex >= MaxInstances)
            {
                // Prevent the potential of overflow.
                nextIndex = MaxInstances;
                return false;
            }
        }

        // After nextId (permanently) reaches this cap, any new IDs will all come from freeIds as older handles are freed.
        pathfinders.Put(newIndex, new PathfinderInstance(navMeshParameters, navigatorParameters));
        handle = new(newIndex);

        return true;
    }

    public static void RecalculateNavMesh(Handle handle, NavMeshParameters newParameters = null)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.RecalculateNavMesh(newParameters);
    }

    internal static void Dispose(Handle handle)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot dispose invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Remove(handle.ID);
        path.Dispose();

        freeIds.Enqueue(handle.ID, handle.ID);
    }
}
