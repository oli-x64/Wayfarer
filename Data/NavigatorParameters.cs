using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace Wayfarer.Data;

public sealed class NavigatorParameters(Rectangle navigatorHitbox,
    Func<Vector2, Vector2, Vector2> jumpFunction,
    Point maxJumpRanges,
    Func<float> gravityFunction,
    Func<List<Point>, Point> findIdealEndNodeFunction)
{
    public readonly Rectangle NavigatorHitbox = navigatorHitbox;
    public readonly Func<Vector2, Vector2, Vector2> JumpFunction = jumpFunction;
    public readonly Point MaxJumpRanges = maxJumpRanges;
    public readonly Func<float> GravityFunction = gravityFunction;
    public readonly Func<List<Point>, Point> FindIdealEndNodeFunction = findIdealEndNodeFunction;
}
