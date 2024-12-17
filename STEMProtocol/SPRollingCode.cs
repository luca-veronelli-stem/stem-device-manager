using System;
using System.Threading;

namespace Stem_Protocol;

public static class RollingCodeGenerator
{
    private static int RollingIndex = 0; // Usa int

    public static byte GetIndex()
    {
        int currentIndex;
        do
        {
            currentIndex = RollingIndex;
            int nextIndex = currentIndex < 7 ? currentIndex + 1 : 0;
            if (Interlocked.CompareExchange(ref RollingIndex, nextIndex, currentIndex) == currentIndex)
            {
                return (byte)nextIndex; // Cast a byte
            }
        } while (true);
    }
}