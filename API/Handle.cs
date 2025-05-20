using System;

namespace Wayfarer.API;

public struct Handle : IDisposable
{
    public static readonly Handle Invalid = new(-1);

    public readonly bool Initialized;

    internal readonly int ID;

    internal bool IsDisposed;

    internal Handle(int id)
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

    public static bool operator ==(Handle h1, Handle h2) => h1.ID == h2.ID;

    public static bool operator !=(Handle h1, Handle h2) => h1.ID != h2.ID;

    public override readonly bool Equals(object obj)
    {
        if (obj is Handle h1)
            return h1 == this;

        return false;
    }

    public override readonly int GetHashCode() => ID;
}
