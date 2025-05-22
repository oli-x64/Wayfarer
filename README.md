# Wayfarer

Wayfarer is a highly flexible library that supports high-performance multithreaded pathfinding on Terraria's tilemap. It is NOT distributed on the workshop, and requires either building + packaging the dll as a `dllReference`, or git submoduling this repo. Though AGPL, copying this implementation is not recommended as maintainability is more difficult outside of the two options presented. If functionality is missing, please create an issue on this repo.

# What Wayfarer DOES do:
- Allows creation of up to 2048 pathfinding instances that operate off the game's main thread.
- Maintains navigation meshes for each instance, and returns results to the main thread once done.
- Allows users to define custom edge types, alongside the in-built `Walk`, `Jump` and `Fall` edges.
- Allows users to control parameters such as navigator hitbox, navigation mesh size, and many others.
- Provides results that can be generalised to any navigator (e.g. entities) that operate on Terraria's tilemap.

# What Wayfarer DOES NOT do:
- Does not handle network synchronisation of pathfinding results.
- Does not tie pathfinding to a specific entity type such as NPCs.
- Does not handle movement code for you - it only provides the route. Traversing it is up to the user.
- Does not have in-built edge types for flying navigators (but may later if the feature is in demand).

# Usage
> [!NOTE]
> The XML docs of this library provide supplementary info and can be viewed in an IDE. For any extra help, please join the discord: https://discord.gg/xcjK9hFuj7

## Including in a Mod
This library is not distributed on the workshop, so you can't reference it via `modReferences`. The two best options are either to use `dllReferences`, meaning building the library from source/using a release to obtain the dll, or by `git submodule`ing this repo in your project (although you may need to adjust the csproj, as the project expects to be in the `ModSources/Wayfarer` directory).

> [!NOTE]
> This project uses an AGPL 3.0 open-source license. This means that if you use its source, your project must also carry the same license!

## Initialization
To use Wayfarer, a 'pathfinding instance' must be created. The user cannot directly access the instance, but instead commands are relayed via the `WayfarerAPI` static class using a `WayfarerHandle`. When a Wayfarer function is called for the first time, worker threads will be started. This example will use a `ModNPC`:
```cs
    // In the ModNPC class.
    WayfarerHandle handle;

    public override void OnSpawn(IEntitySource source)
    {
        base.OnSpawn(source);

        NavMeshParameters navMeshParameters = new(
            NPC.Center.ToTileCoordinates(),
            100,
            WayfarerPresets.DefaultIsTileValid
        );
        NavigatorParameters navigatorParameters = new(
            NPC.Hitbox,
            WayfarerPresets.DefaultJumpFunction,
            new(8, 10),
            () => NPC.gravity,
            SelectDestination
        );

        WayfarerAPI.TryCreatePathfindingInstance(navMeshParameters, navigatorParameters, out handle);
    }
```
This code creates a pathfinding instance and stores the `WayfarerHandle` handle in the `ModNPC` class. The parameters passed in all have XML docs, but essentially the navigation mesh radius here is 100 tiles and is centered on the NPC. The built-in functions `WayfarerPresets.DefaultIsTileValid` and `WayfarerPresets.DefaultJumpFunction` are shortcuts that account for basic ground navigator functionality.

Though the method's return value is not used here, `WayfarerAPI.TryCreatePathfindingInstance` will return true if an instance was successfully created, and false if the limit is reached. Reaching the limit is very unlikely if instances are disposed properly when not used.

## Cleanup
Handles **MUST** be disposed after they are no longer needed. For entities, the best way to do this is to dispose the handle in `OnKill`:
```cs
    // In the ModNPC class.
    public override void OnKill()
    {
        handle.Dispose();
    }
```
As well as this, when the entire mod is finished using Wayfarer, the worker threads must be cleaned up. This can be done by calling `WayfarerAPI.Shutdown()` in mod unloading. Again, you **MUST** do this if you have called any Wayfarer API function:
```cs
    // In the Mod class.
    public override void Unload()
    {
        WayfarerAPI.Shutdown();
    }
```

## Pathfinding
To calculate a path, `WayfarerAPI.RecalculatePath` should be called. `WayfarerAPI.RecalculateNavMesh` can also be called to rebuild the navigation mesh, and should be done if the navigator ever leaves the navigation mesh, which can be checked with `WayfarerAPI.PointIsInNavMesh`. However, if no navigation mesh has been built, pathfinding will automatically build one the first time it is called for a handle.

> [!CAUTION]
> Calling `WayfarerAPI.RecalculatePath` or `WayfarerAPI.RecalculateNavMesh` several times will queue several of the same task. The best way to ensure only one request is made when using Wayfarer with entities is to make a request, and then make the entity enter a 'Waiting' AI state which it only exits after the `onComplete` callback of `WayfarerAPI.RecalculatePath` is invoked. If you do this, there should be no issues.

An example of requesting a path is as follows:
```cs
    // In the ModNPC class.
    private void StartPathing()
    {
        Point[] tiles = /* Get tiles below NPC's feet */.

        if (tiles.Length < 1)
            return;

        WayfarerAPI.RecalculateNavMesh(handle, NPC.Center.ToTileCoordinates());
        WayfarerAPI.RecalculatePath(handle, tiles, NewPathFound);
    }

    private PathResult path;

    private void NewPathFound(PathResult result)
    {
        if (/* AI state is NOT 'Waiting' */)
            return;

        // Optionally, you could check if result is null here, meaning no path is found. If this is the case, you could handle it by despawning the NPC, teleporting it to a random tile, etc.

        path = result;

        /* Transition state out of 'Waiting' and into state that can traverse the path. */
    }
```

Once `NewPathFound` is called, the NPC will switch to a normal AI state that lets it follow the path. An example of this might be as follows:
```cs
        // In NON-WAITING STATE NPC AI.
        // Will be true if path is null, or if the path does not have any edges left.
        if (path is null || !path.HasPath)
        {
            /* Transition back into 'Waiting' state; re-request path.*/
            StartPathing();
            return;
        }

        PathEdge edge = path.Current;

        if (edge.Is<Walk>())
        {
            /* Move towards edge.To. */

            bool goalReachedCondition = /* NPC is close enough to edge.To to count as having 'reached it'. */

            if (goalReachedCondition)
            {
                // Calling path.Advance changes path.Current to the next edge.
                path.Advance(out bool atGoal);

                // atGoal is true if there are no next edges, i.e. the path is completed.
                if (atGoal)
                {
                    /* Transition to AI state responsible for doing things at the destination */
                    return;
                }
            }
        }

        // Later in AI...
        // This code checks if 1. there are tiles below the NPC's hitbox and 2. if they are in the nav mesh.

        Point[] tiles = /* Get tiles below NPC's feet */.

        bool contains = false;

        foreach (Point node in tiles)
        {
            if (WayfarerAPI.PointIsInNavMesh(handle, node))
            {
                contains = true;
            }
        }

        // If this is true, then the NPC is standing on the ground but there are no valid tiles in the nav mesh below them; they have left the nav mesh.
        if (tiles.Length > 0 && !contains)
        {
            /* Transition back into 'Waiting' state; re-request path.*/
            StartPathing();
            return;
        }
```
These code snippets should give an idea of how to use Wayfarer with an NPC. Again, movement code is not handled by the library, so it is up to you to decide how your navigator will interpret the path results.

# Report Bugs or Request Features
Please report bugs or request features with an issue in this repo if possible, as it is easier to keep track of. However, they can be made in the discord as well.
