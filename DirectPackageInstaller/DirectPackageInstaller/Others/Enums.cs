﻿using System;

namespace DirectPackageInstaller
{
    [Flags]
    public enum Source : ulong
    {
        NONE      = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000000,
        URL       = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000001,
        JSON      = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000010,
        RAR       = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00000100,
        File      = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00001000,
        Proxy     = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00010000,
        SevenZip  = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_00100000,
        DiskCache = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_01000000,
        Segmented = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000000_10000000,
        Torrent   = 0b00000000_00000000_00000000_00000000_00000000_00000000_00000001_00000000
    }
}
