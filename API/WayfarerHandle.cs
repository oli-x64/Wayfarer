using System;
using Wayfarer.Data;
using Terraria.ModLoader;

namespace Wayfarer.API;

public struct WayfarerHandle : IDisposable
{
    /// <summary>
    /// Invalid handle. Passing this as a parameter to any function in <see cref="WayfarerAPI"/> will throw an exception.
    /// When a handle is disposed, its ID will become Invalid.
    /// </summary>
    public static readonly WayfarerHandle Invalid = new(-1);

    /// <summary>
    /// This is true if a handle has been properly initialized by <see cref="WayfarerAPI.TryCreatePathfindingInstance(Data.NavMeshParameters, Data.NavigatorParameters, out WayfarerHandle)"/>.
    /// </summary>
    public readonly bool Initialized;

    internal int ID;

    internal WayfarerHandle(int id)
    {
        ID = id;
        Initialized = true;
    }

    /// <summary>
    /// Call this when a pathfinding instance is no longer needed. You must dispose of handles!
    /// Failing to do this when they are no longer needed will eventually max out the system, and you won't be able to create new instances.
    /// In entity applications, handles should be disposed in hooks such as <see cref="ModNPC.OnKill"/>.
    /// </summary>
    public void Dispose()
    {
        if (Initialized)
        {
            WayfarerAPI.Dispose(this);
            ID = Invalid.ID;
        }
    }

    public static bool operator ==(WayfarerHandle h1, WayfarerHandle h2) => h1.ID == h2.ID;

    public static bool operator !=(WayfarerHandle h1, WayfarerHandle h2) => h1.ID != h2.ID;

    public override readonly bool Equals(object obj)
    {
        if (obj is WayfarerHandle h1)
            return h1 == this;

        return false;
    }

    public override readonly int GetHashCode() => ID.GetHashCode();
}
