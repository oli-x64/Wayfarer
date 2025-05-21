using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Wayfarer.API;
using Wayfarer.Data;

namespace Wayfarer.Pathfinding.Async;

internal abstract class AsyncRequest(WayfarerHandle source, CancellationToken token)
{
    public readonly WayfarerHandle Source = source;
    public readonly CancellationToken Token = token;
}

internal class NavMeshRequest : AsyncRequest
{
    public readonly NavMeshParameters NavMeshParameters;
    public readonly NavigatorParameters NavigatorParameters;
    public readonly TaskCompletionSource<NavMesh> CompletionSource;

    public NavMeshRequest(
        WayfarerHandle source,
        NavMeshParameters navMeshParameters,
        NavigatorParameters navigatorParams,
        CancellationToken token
    ) : base(source, token)
    {
        NavMeshParameters = navMeshParameters;
        NavigatorParameters = navigatorParams;
        CompletionSource = new TaskCompletionSource<NavMesh>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

internal class PathfindingRequest : AsyncRequest
{
    public readonly NavMeshParameters NavMeshParameters;
    public readonly NavigatorParameters NavigatorParameters;
    public readonly TaskCompletionSource<PathResult> CompletionSource;
    public readonly Point[] Starts;
    public readonly Action<PathResult> OnComplete;

    public PathfindingRequest(
        WayfarerHandle source,
        NavMeshParameters navMeshParameters,
        NavigatorParameters navigatorParams,
        Point[] starts,
        Action<PathResult> onComplete,
        CancellationToken token
    ) : base(source, token)
    {
        NavMeshParameters = navMeshParameters;
        NavigatorParameters = navigatorParams;
        CompletionSource = new TaskCompletionSource<PathResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Starts = starts;
        OnComplete = onComplete;
    }
}
