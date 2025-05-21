using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Pathfinding;
using Wayfarer.Pathfinding.Async;

namespace Wayfarer.API;

public static class WayfarerAPI
{
    private const int MaxInstances = 2048;

    private static readonly SparseSet<PathfinderInstance> pathfinders = new(1, 1);

    private static readonly PriorityQueue<int, int> freeIds = new();

    private static int nextIndex;

    public static bool TryCreatePathfindingInstance(NavMeshParameters navMeshParameters, NavigatorParameters navigatorParameters, out WayfarerHandle handle)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        handle = WayfarerHandle.Invalid;

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

        handle = new(newIndex);

        PathfinderInstance instance = new(handle, navMeshParameters, navigatorParameters);

        pathfinders.Put(newIndex, instance);

        return true;
    }

    public static void RecalculateNavMesh(WayfarerHandle handle, Point? newCentre = null)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.RecalculateNavMesh(newCentre);
    }

    public static void RecalculatePath(WayfarerHandle handle, Point[] starts, Action<PathResult> onComplete)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        if (starts is null || starts.Length == 0)
            throw new ArgumentException($"No starting points specified!");

        var path = pathfinders.Get(handle.ID);

        path.RecalculatePathfinding(starts, onComplete);
    }

    public static bool PointIsInNavMesh(WayfarerHandle handle, Point node)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot check navmesh with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        return path.IsValidNode(node);
    }

    public static void DebugRenderNavMesh(WayfarerHandle handle, SpriteBatch spriteBatch)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot debug render with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.DebugRender(spriteBatch);
    }

    public static void DebugRenderPath(WayfarerHandle handle, SpriteBatch spriteBatch, PathResult result)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot debug render with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.DebugRenderPath(spriteBatch, result);
    }

    public static void Shutdown() => RequestProcessor.Shutdown();

    internal static void Dispose(WayfarerHandle handle)
    {
        if (handle == WayfarerHandle.Invalid || handle.IsDisposed)
            throw new ArgumentException($"Cannot dispose invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Remove(handle.ID);

        path.Dispose();

        freeIds.Enqueue(handle.ID, handle.ID);
    }
}
