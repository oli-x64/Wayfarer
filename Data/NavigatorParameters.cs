using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Wayfarer.Edges;

namespace Wayfarer.Data;

/// <summary>
/// A set of parameters to determine the properties of a navigator. A navigator is any agent with a hitbox that will be navigating a path.
/// </summary>
/// <param name="navigatorHitbox">The hitbox of the navigator.</param>
/// <param name="jumpFunction">The function used for simulating jumps in calculations pertaining to <see cref="Jump"/>.</param>
/// <param name="maxJumpRanges">The max jump range (X, Y) of the navigator, used for simulating jumps in calculations pertaining to <see cref="Jump"/>. Tiles outside this range will not be considered as potential jump targets. </param>
/// <param name="gravityFunction">The gravity function of the navigator, used for simulating jumps in calculations pertaining to <see cref="Jump"/>.</param>
/// <param name="findIdealEndNodeFunction">The function used to find a desirable destination node. For example, targets pathfinding to an enemy should iterate the set of nodes and return the closest node to that enemy's position.</param>
public sealed class NavigatorParameters(
    Rectangle navigatorHitbox,
    Func<Vector2, Vector2, Func<float>, Vector2> jumpFunction,
    Point maxJumpRanges,
    Func<float> gravityFunction,
    Func<IReadOnlySet<Point>, Point> findIdealEndNodeFunction)
{
    public readonly Rectangle NavigatorHitbox = navigatorHitbox;
    public readonly Func<Vector2, Vector2, Func<float>, Vector2> JumpFunction = jumpFunction;
    public readonly Point MaxJumpRanges = maxJumpRanges;
    public readonly Func<float> GravityFunction = gravityFunction;
    public readonly Func<IReadOnlySet<Point>, Point> FindIdealEndNodeFunction = findIdealEndNodeFunction;
}
