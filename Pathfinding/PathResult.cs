using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Wayfarer.API;
using Wayfarer.Edges;

namespace Wayfarer.Pathfinding;

public sealed class PathResult
{
    public bool HasPath => path.Count > 0;

    public PathEdge Current => path[index];

    private int index;

    private readonly List<PathEdge> path;

    internal List<PathEdge> Path => path;

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
            index = path.Count - 1;
            atGoal = true;
        }
    }
}
