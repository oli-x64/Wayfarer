using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Wayfarer.API;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

/// <summary>
/// Represents a completed pathfinding result.
/// </summary>
public sealed class PathResult
{
    /// <summary>
    /// Returns true if the path has not yet advanced to the final edge. This is never true if <see cref="IsAlreadyAtGoal"/> is true.
    /// </summary>
    public bool HasPath => !IsAlreadyAtGoal && index <= path.Count - 1;

    /// <summary>
    /// The current path edge.
    /// </summary>
    public PathEdge Current => path[index];

    private int index;

    private readonly List<PathEdge> path;

    internal List<PathEdge> Path => path;

    /// <summary>
    /// Returns true when pathfinding determines the navigator was already at its ideal destination.
    /// </summary>
    public bool IsAlreadyAtGoal {  get; private set; } 

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
