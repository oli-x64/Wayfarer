using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Terraria;

namespace Wayfarer.Tiles;

internal unsafe struct TileDataSnapshot<T> : IDisposable where T : unmanaged, ITileData
{
    private T[] data;

    private T* ptr;

    private GCHandle handle;

    // These are stored so that accessing a tile-coordinate space point can be offset to access the data in this array.
    private readonly uint minX;
    private readonly uint minY;
    private readonly uint maxX;

    private readonly uint height;

    public TileDataSnapshot(uint minX, uint minY, uint maxX, uint maxY)
    {
        this.minX = minX;
        this.minY = minY;
        this.maxX = maxX;

        height = maxY - minY;

        uint length = (maxX - minX) * (maxY - minY);

        SetLength(length);
        CopyData();
    }

    private unsafe void SetLength(uint length)
    {
        if (data != null)
            handle.Free();

        data = new T[length];
        handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        ptr = (T*)handle.AddrOfPinnedObject().ToPointer();
    }

    private readonly unsafe void CopyData()
    {
        // sizeof(T) * height is the width of one row-slice of tile data.
        long slice = sizeof(T) * height;

        uint srcHeight = Main.tile.Height;
        uint src = minY + (minX * Main.tile.Height);

        uint dstHeight = height;
        uint dst = 0;

        for (uint x = minX; x < maxX; x++)
        {
            Buffer.MemoryCopy(TileData<T>.ptr + src, ptr + dst, slice, slice);

            src += srcHeight;
            dst += dstHeight;
        }
    }

    public void Dispose()
    {
        if (data != null)
        {
            handle.Free();
            data = null;
        }
    }

    // Tile ID is calculated from (uint)(y + (x * Height)).
    // The data array is indexed using this same formula, but with offsets since we don't store the whole tilemap.
    public ref T Get(Point tile) => ref ptr[tile.Y - minY + ((tile.X - minX) * height)];
}
