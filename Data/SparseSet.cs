using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Wayfarer.Data;

// Adapted from https://github.com/Mirsario/TerrariaOverhaul/blob/46e6f6bd8eebfdac0eee65affb132990194eb660/Core/Data/SparseSet.cs.
internal sealed class SparseSet<TData>
{
    private const int Invalid = -1;

    private int[] sparse = [];
    private TData[] dense = [];

    public int Count { get; private set; }

    public SparseSet(int sparseCapacity, int denseCapacity)
    {
        dense = denseCapacity > 0 ? new TData[denseCapacity] : [];
        sparse = sparseCapacity > 0 ? new int[sparseCapacity] : [];

        for (int i = 0; i < sparseCapacity; i++) sparse[i] = Invalid;
    }

    public bool Has(int index) => index < sparse.Length && sparse[index] != Invalid;

    public ref TData Get(int index)
    {
        Debug.Assert(Has(index));

        return ref dense[sparse[index]];
    }

    public ref TData Put(int index, in TData value)
    {
        if (index >= sparse.Length)
        {
            int oldLength = sparse.Length;
            int newLength = (int)BitOperations.RoundUpToPowerOf2((uint)(index + 1));

            Array.Resize(ref sparse, newLength);

            for (int i = oldLength; i < newLength; i++) sparse[i] = Invalid;
        }

        int denseIndex = sparse[index];

        if (denseIndex == Invalid)
        {
            sparse[index] = denseIndex = Count++;

            if (denseIndex >= dense.Length)
                Array.Resize(ref dense, (int)BitOperations.RoundUpToPowerOf2((uint)(denseIndex + 1)));
        }

        dense[denseIndex] = value;

        return ref dense[denseIndex];
    }
    public TData Remove(int index)
    {
        Debug.Assert(Has(index));

        ref var address = ref dense[sparse[index]];
        var result = address;

        sparse[index] = Invalid;

        address = default;

        return result;
    }
}
