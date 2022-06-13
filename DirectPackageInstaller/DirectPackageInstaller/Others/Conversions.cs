using System;
using System.Collections.Generic;
using System.Linq;

namespace DirectPackageInstaller;

internal static class Conversions
{
    internal static int IndexOf(this IEnumerable<byte> Buffer, byte[] Content)
    {
        int Offset = 0;
        int Count = Buffer.Count() - Content.Length;
        for (int i = 0, x = 0; i < Count; i++)
        {
            if (Buffer.ElementAt(i) != Content[x])
            {
                x = 0;
                continue;
            }

            if (x == 0)
                Offset = i;
                
            x++;
            if (x >= Content.Length)
                return Offset;
        }

        return -1;
    }


    internal static string ToFileSize(this long Value) => ToFileSize((double)Value);
    static string ToFileSize(this double value)
    {
        string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            if (value <= (Math.Pow(1024, i + 1)))
            {
                return ThreeNonZeroDigits(value / Math.Pow(1024, i)) + " " + suffixes[i];
            }
        }

        return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
    }

    static string ThreeNonZeroDigits(double value)
    {
        if (value >= 100)
            return value.ToString("0,0");

        if (value >= 10)
            return value.ToString("0.0");
            
        return value.ToString("0.00");
    }
}