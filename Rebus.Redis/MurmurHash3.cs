using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Rebus.Redis;

// Included from https://github.com/JeremyEspresso/MurmurHash

// MIT License
//
// Copyright (c) 2022 JeremyEspresso
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

internal static class MurmurHash3
{
    /// <summary>
    /// Hashes the <paramref name="bytes"/> into a MurmurHash3 as a <see cref="uint"/>.
    /// </summary>
    /// <param name="bytes">The span.</param>
    /// <param name="seed">The seed for this algorithm.</param>
    /// <returns>The MurmurHash3 as a <see cref="uint"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash32(ref ReadOnlySpan<byte> bytes, uint seed)
    {
        ref byte bp = ref MemoryMarshal.GetReference(bytes);
        ref uint endPoint = ref Unsafe.Add(ref Unsafe.As<byte, uint>(ref bp), bytes.Length >> 2);
        if (bytes.Length >= 4)
        {
            do
            {
                seed = RotateLeft(seed ^ RotateLeft(Unsafe.ReadUnaligned<uint>(ref bp) * 3432918353U, 15) * 461845907U, 13) * 5 - 430675100;
                bp = ref Unsafe.Add(ref bp, 4);
            } while (Unsafe.IsAddressLessThan(ref Unsafe.As<byte, uint>(ref bp), ref endPoint));
        }

        var remainder = bytes.Length & 3;
        if (remainder > 0)
        {
            uint num = 0;
            if (remainder > 2)
            {
                num ^= Unsafe.Add(ref endPoint, 2) << 16;
            }

            if (remainder > 1)
            {
                num ^= Unsafe.Add(ref endPoint, 1) << 8;
            }

            num ^= endPoint;

            seed ^= RotateLeft(num * 3432918353U, 15) * 461845907U;
        }

        seed ^= (uint)bytes.Length;
        seed = (uint)((seed ^ (seed >> 16)) * -2048144789);
        seed = (uint)((seed ^ (seed >> 13)) * -1028477387);
        return seed ^ seed >> 16;
    }
    
    // RotateLeft copied from .NET source 
    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Numerics/BitOperations.cs
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint RotateLeft(uint value, int offset) => (value << offset) | (value >> (32 - offset));
}