using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

/// <summary>
/// Represents a completed pathfinding result.
/// </summary>
public sealed class PathResult
{
    private int index;
    private readonly List<PathEdge> path;

    /// <summary>
    /// Returns true when pathfinding determines the navigator was already at its ideal destination.
    /// </summary>
    public bool IsAlreadyAtGoal { get; private set; }

    /// <summary>
    /// Returns true if the path has not yet advanced to the final edge. This is never true if <see cref="IsAlreadyAtGoal"/> is true.
    /// </summary>
    public bool HasPath => !IsAlreadyAtGoal && index <= path.Count - 1;
    /// <summary> The index of the current path edge. </summary>
    public int Index => Index;
    /// <summary> The current path edge. </summary>
    public PathEdge? Current => index < path.Count ? path[index] : null;
    /// <summary> The next path edge. </summary>
    public PathEdge? Next => (index + 1) < path.Count ? path[index + 1] : null;
    /// <summary> All the computed edges. </summary>
    public ReadOnlySpan<PathEdge> Edges => CollectionsMarshal.AsSpan(path);

    internal PathResult(List<PathEdge> path, bool alreadyAtGoal)
    {
        this.path = path;
        
        IsAlreadyAtGoal = alreadyAtGoal;

        if (alreadyAtGoal)
        {
            path.Clear();
        }
    }

    /// <summary>
    /// Advances <see cref="Current"/> to the next edge. <paramref name="atGoal"/> is true when there are no more valid edges left in the path.
    /// </summary>
    /// <param name="atGoal">Will be true if there are no more edges left to advance to.</param>
    public void Advance(out bool atGoal)
    {
        atGoal = false;

        if (path.Count == 0)
        {
            atGoal = true;
        }

        index++;

        if (index > path.Count - 1)
        {
            index = path.Count;
            atGoal = true;
        }
    }
}
