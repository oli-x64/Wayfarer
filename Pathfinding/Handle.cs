using System;

namespace Wayfarer.Pathfinding;

public readonly struct Handle : IDisposable
{
    public static readonly Handle Invalid = new(-1);

    internal readonly int ID;

    internal Handle(int id)
    {
        ID = id;
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
