using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Pathfinding;

namespace Wayfarer.API;

public sealed class WayfarerAPI
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

            // After nextId (permanently) reaches this cap, any new IDs will all come from freeIds as older handles are freed.
            if (nextIndex >= MaxInstances)
            {
                // Prevent the potential of overflow.
                nextIndex = MaxInstances;
                return false;
            }
        }

        PathfinderInstance instance = new(navMeshParameters, navigatorParameters);

        pathfinders.Put(newIndex, instance);

        handle = new(newIndex);

        return true;
    }

    public static void RecalculateNavMesh(Handle handle, Point? newCentre = null)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.RecalculateNavMesh(newCentre);
    }

    public static void RecalculatePath(Handle handle, Point[] starts, Action<PathResult> onComplete)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        if (starts.Length == 0)
            throw new ArgumentException($"No starting points specified!");

        var path = pathfinders.Get(handle.ID);

        path.RecalculatePathfinding(starts, onComplete);
    }

    public static bool IsCurrentlyPathfinding(Handle handle)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot get pathfinding status with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        return path.IsRecalculating;
    }

    public static bool PointIsInNavMesh(Handle handle, Point node)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot check navmesh with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        return path.IsValidNode(node);
    }

    public static void DebugRenderNavMesh(Handle handle, SpriteBatch spriteBatch)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot debug render with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.DebugRender(spriteBatch);
    }

    public static void DebugRenderPath(Handle handle, SpriteBatch spriteBatch, PathResult result)
    {
        if (handle == Handle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot debug render with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.DebugRenderPath(spriteBatch, result);
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
