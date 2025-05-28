using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Wayfarer.Data;
using Wayfarer.Pathfinding;
using Wayfarer.Pathfinding.Async;
using Terraria.ModLoader;
using Terraria.DataStructures;
using System.IO;
using Wayfarer.Edges;

namespace Wayfarer.API;

/// <summary>
/// All API functions of Wayfarer are interfaced with in this class.
/// </summary>
public static class WayfarerAPI
{
    private const int MaxInstances = 2048;

    private static readonly SparseSet<PathfinderInstance> pathfinders = new(1, 1);

    private static readonly PriorityQueue<int, int> freeIds = new();

    private static int nextIndex;

    /// <summary>
    /// Calling this method will create a pathfinding instance and return its handle.
    /// A pathfinding instance is responsible for managing all aspects of pathfinding, and commands can be sent to it using any function in this class using the <see cref="WayfarerHandle"/>.
    /// This method should only be called once per navigator; in entity applications, a hook such as <see cref="ModNPC.OnSpawn(IEntitySource)"/> is preferred.
    /// It is imperative to use <see cref="WayfarerHandle.Dispose"/> to dispose the pathfinder instance when it is no longer needed.
    /// </summary>
    /// <param name="navMeshParameters">The parameters used for the navmesh.</param>
    /// <param name="navigatorParameters">The parameters used for the navigator.</param>
    /// <param name="handle">The handle used to perform pathfinding tasks.</param>
    /// <returns>The handle used to perform pathfinding tasks.</returns>
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

    /// <summary>
    /// Recalculates the navmesh of a pathinding instance. This does not recalculate a path, but can be used for rebuilding the navmesh around a new origin tile, <paramref name="newCentre"/>.
    /// </summary>
    /// <param name="handle">The handle of the pathfinding instance, acquired from <see cref="TryCreatePathfindingInstance(NavMeshParameters, NavigatorParameters, out WayfarerHandle)"/></param>
    /// <param name="newCentre"></param>
    /// <exception cref="ArgumentException">Thrown when the handle is Invalid.</exception>
    public static void RecalculateNavMesh(WayfarerHandle handle, Point? newCentre = null)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.RecalculateNavMesh(newCentre);
    }

    /// <summary>
    /// Calculates a path using <paramref name="starts"/> as potential starting points, and <see cref="NavigatorParameters.FindIdealEndNodeFunction"/> to get the end point, and invokes <paramref name="onComplete"/> with the path result.
    /// A path can only be calculated inside the current navmesh. If you want a navigator to move far distances, consider also calling <see cref="RecalculateNavMesh(WayfarerHandle, Point?)"/> alongside any instances where this method is called.
    /// WARNING: calling this method over and over will spam the system with pathfinding requests. It is highly recommended to wait for <paramref name="onComplete"/> to be invoked before making any new requests with this handle.
    /// </summary>
    /// <param name="handle">The handle of the pathfinding instance, acquired from <see cref="TryCreatePathfindingInstance(NavMeshParameters, NavigatorParameters, out WayfarerHandle)"/></param>
    /// <param name="starts">Potential starting points for pathfinding. This allows supplying multiple in case a navigator is occupying multiple starting points.</param>
    /// <param name="onComplete">Invokes with the results of the pathfinding operation when completed. <see cref="PathResult"/> will be null if no path is found.</param>
    /// <exception cref="ArgumentException">Thrown when the handle is Invalid.</exception>
    public static void RecalculatePath(WayfarerHandle handle, Point[] starts, Action<PathResult> onComplete)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid)
            throw new ArgumentException($"Cannot recalculate with invalid or disposed handle! Handle: {handle}");

        if (starts is null || starts.Length == 0)
            throw new ArgumentException($"No starting points specified!");

        var path = pathfinders.Get(handle.ID);

        path.RecalculatePathfinding(starts, onComplete);
    }

    /// <summary>
    /// Returns true if the given point is in this pathfinding instance's navmesh.
    /// This method should be used to test if a navigator has left the known mesh, and if so, the user may recalculate the navmesh.
    /// </summary>
    /// <param name="handle">The handle of the pathfinding instance, acquired from <see cref="TryCreatePathfindingInstance(NavMeshParameters, NavigatorParameters, out WayfarerHandle)"/></param>
    /// <param name="node">The node being tested.</param>
    /// <exception cref="ArgumentException">Thrown when the handle is Invalid.</exception>
    public static bool PointIsInNavMesh(WayfarerHandle handle, Point node)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid)
            throw new ArgumentException($"Cannot check navmesh with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        return path.IsValidNode(node);
    }

    /// <summary>
    /// Renders the entire navmesh and all its edges in the world. This is very laggy!
    /// </summary>
    /// <param name="handle">The handle of the pathfinding instance, acquired from <see cref="TryCreatePathfindingInstance(NavMeshParameters, NavigatorParameters, out WayfarerHandle)"/></param>
    /// <param name="spriteBatch">The <see cref="SpriteBatch"/> instance used to render the edges.</param>
    /// <exception cref="ArgumentException">Thrown when the handle is Invalid.</exception>
    public static void DebugRenderNavMesh(WayfarerHandle handle, SpriteBatch spriteBatch)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid)
            throw new ArgumentException($"Cannot debug render with invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Get(handle.ID);

        path.DebugRender(spriteBatch);
    }

    /// <summary>
    /// Renders only the given <see cref="PathResult"/> in the world. This method will not do anything if <paramref name="result"/> is null.
    /// </summary>
    /// <param name="handle">The handle of the pathfinding instance, acquired from <see cref="TryCreatePathfindingInstance(NavMeshParameters, NavigatorParameters, out WayfarerHandle)"/></param>
    /// <param name="spriteBatch">The <see cref="SpriteBatch"/> instance used to render the edges.</param>
    /// <param name="result">The path result being rendered.</param>
    /// <exception cref="ArgumentException">Thrown when the handle is Invalid.</exception>
    public static void DebugRenderPath(WayfarerHandle handle, SpriteBatch spriteBatch, PathResult result)
    {
        RequestProcessor.FirstTimeSetupIfNeeded();

        if (handle == WayfarerHandle.Invalid)
            throw new ArgumentException($"Cannot debug render with invalid or disposed handle! Handle: {handle}");
        if (result is null)
            return;

        var path = pathfinders.Get(handle.ID);

        path.DebugRenderPath(spriteBatch, result);
    }

    /// <summary>
    /// Cancels all worker tasks related to pathfinding and cleans up memory for all instances.
    /// This should be called when the user's mod is completely finished using the library, i.e. in <see cref="Mod.Unload"/> or <see cref="ModSystem.OnModUnload"/>
    /// </summary>
    public static void Shutdown() => RequestProcessor.Shutdown();

    internal static void Dispose(WayfarerHandle handle)
    {
        if (handle == WayfarerHandle.Invalid)
            throw new ArgumentException($"Cannot dispose invalid or disposed handle! Handle: {handle}");

        var path = pathfinders.Remove(handle.ID);

        path.Dispose();

        freeIds.Enqueue(handle.ID, handle.ID);
    }

    /// <summary>
    /// Allows a <see cref="PathResult"/> to be written to a <see cref="BinaryWriter"/> for networking purposes.
    /// </summary>
    /// <param name="result">The target result.</param>
    /// <param name="writer">The writer used for networking.</param>
    public static void WriteResultTo(PathResult result, BinaryWriter writer)
    {
        bool isAlreadyAtGoal = result.IsAlreadyAtGoal;
        List<PathEdge> edges = result.Path;

        writer.Write(isAlreadyAtGoal);
        writer.Write(edges.Count);

        foreach (PathEdge edge in edges)
        {
            writer.Write(edge.From.X);
            writer.Write(edge.From.Y);
            writer.Write(edge.To.X);
            writer.Write(edge.To.Y);
            writer.Write(edge.EdgeType);
        }
    }

    /// <summary>
    /// Allows a <see cref="PathResult"/> to be read from a <see cref="BinaryReader"/> for networking purposes.
    /// </summary>
    /// <param name="reader">The reader used for networking.</param>
    /// <returns>The target result.</returns>
    public static PathResult ReadResultFrom(BinaryReader reader)
    {
        bool isAlreadyAtGoal = reader.ReadBoolean();
        int count = reader.ReadInt32();

        List<PathEdge> edges = [];

        for (int i = 0; i < count; i++)
        {
            int fromX = reader.ReadInt32();
            int fromY = reader.ReadInt32();
            int toX = reader.ReadInt32();
            int toY = reader.ReadInt32();
            int edgeType = reader.ReadInt32();

            edges.Add(new(new(fromX, fromY), new(toX, toY), edgeType));
        }

        return new PathResult(edges, isAlreadyAtGoal);
    }
}
