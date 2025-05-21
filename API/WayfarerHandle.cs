using System;

namespace Wayfarer.API;

public struct WayfarerHandle : IDisposable
{
    public static readonly WayfarerHandle Invalid = new(-1);

    public readonly bool Initialized;

    internal readonly int ID;

    internal bool IsDisposed;

    internal WayfarerHandle(int id)
    {
        ID = id;
        Initialized = true;
    }

    public void Dispose()
    {
        if (Initialized)
        {
            WayfarerAPI.Dispose(this);
            IsDisposed = true;
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

    public override readonly int GetHashCode() => ID;
}
