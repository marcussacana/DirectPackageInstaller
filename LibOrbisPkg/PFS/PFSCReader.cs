using System;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.IO.Compression;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// This wraps a Memory mapped file view of a PFSC file so that you can access it
  /// as though it were uncompressed.
  /// </summary>
  public class PFSCReader : IMemoryReader
  {
    public const int Magic = 0x43534650;
    private IMemoryAccessor _accessor;
    private PFSCHdr hdr;
    private long[] sectorMap;

    /// <summary>
    /// Creates a PFSCReader
    /// </summary>
    /// <param name="va">An IMemoryAccessor containing the PFSC file</param>
    /// <exception cref="ArgumentException">Thrown when the accessor is not a view of a PFSC file.</exception>
    public PFSCReader(IMemoryAccessor va)
    {
      _accessor = va;
      _accessor.Read(0, out hdr);
      if (hdr.Magic != Magic)
        throw new ArgumentException("Not a PFSC file: missing PFSC magic");
      if (hdr.Unk4 != 0)
        throw new ArgumentException($"Not a PFSC file: unknown data at 0x4 (expected 0, got {hdr.Unk4})");
      //if (hdr.Unk8 != 6)
      //  throw new ArgumentException($"Not a PFSC file: unknown data at 0x8 (expected 6, got {hdr.Unk8})");
      if (hdr.BlockSz != (int)hdr.BlockSz2)
        throw new ArgumentException("Not a PFSC file: block size mismatch");

      var num_blocks = (int)(hdr.DataLength / hdr.BlockSz2);
      sectorMap = new long[num_blocks + 1];
      _accessor.ReadArray(hdr.BlockOffsets, sectorMap, 0, num_blocks + 1);
    }

    /// <summary>
    /// Creates a PFSCReader
    /// </summary>
    /// <param name="va">A ViewAccessor containing the PFSC file</param>
    /// <exception cref="ArgumentException">Thrown when the accessor is not a view of a PFSC file.</exception>
    public PFSCReader(MemoryMappedViewAccessor va) : this(new MemoryMappedViewAccessor_(va))
    { }

    public PFSCReader(IMemoryReader r) : this(new MemoryAccessor(r))
    { }

    public int SectorSize => hdr.BlockSz;
    
    /// <summary>
    /// Reads the sector at the given index into the given byte array.
    /// </summary>
    /// <param name="idx">sector index (multiply by SectorSize to get the byte offset)</param>
    /// <param name="output">byte array where sector will be written</param>
    public void ReadSector(int idx, byte[] output)
    {
      if (idx < 0 || idx > sectorMap.Length - 1)
        throw new ArgumentException("Invalid index", nameof(idx));

      var sectorOffset = sectorMap[idx];
      var sectorSize = sectorMap[idx + 1] - sectorOffset;

      if(sectorSize == hdr.BlockSz2)
      {
        // fast case: uncompressed sector
        _accessor.Read(sectorOffset, output, 0, hdr.BlockSz);
      }
      else if (sectorSize > hdr.BlockSz2)
      {
        Array.Clear(output, 0, hdr.BlockSz);
      }
      else
      {
        // slow case: compressed sector
        var sectorBuf = new byte[(int)sectorSize - 2];
        _accessor.Read(sectorOffset + 2, sectorBuf, 0, (int)sectorSize - 2);
        using (var bufStream = new MemoryStream(sectorBuf))
        using (var ds = new DeflateStream(bufStream, CompressionMode.Decompress))
        {
          ds.Read(output, 0, hdr.BlockSz);
        }
      }
    }
    
    private void Read(long src, long count, Action<byte[],int,int> Write)
    {
      if (src + count > hdr.DataLength)
        throw new ArgumentException("Attempt to read beyond end of file");
      var sectorSize = hdr.BlockSz;
      var sectorBuffer = new byte[sectorSize];
      var currentSector = (int)(src / sectorSize);
      var offsetIntoSector = (int)(src - (sectorSize * currentSector));
      ReadSector(currentSector, sectorBuffer);
      while (count > 0 && src < hdr.DataLength)
      {
        if (offsetIntoSector >= sectorSize)
        {
          currentSector++;
          ReadSector(currentSector, sectorBuffer);
          offsetIntoSector = 0;
        }
        int bufferedRead = (int)Math.Min(sectorSize - offsetIntoSector, count);
        Write(sectorBuffer, offsetIntoSector, bufferedRead);
        count -= bufferedRead;
        offsetIntoSector += bufferedRead;
        src += bufferedRead;
      }
    }

    /// <summary>
    /// Read `count` bytes at location `src` into the writeable Stream `dest`
    /// </summary>
    /// <param name="src">Byte offset into PFSC</param>
    /// <param name="count">Number of bytes to read</param>
    /// <param name="dest">Output stream</param>
    public void Read(long src, long count, Stream dest)
    {
      Read(src, count, dest.Write);
    }

    /// <summary>
    /// Read `count` bytes at location `src` into the byte array at offset `offset`
    /// </summary>
    /// <param name="src">Byte offset into PFSC</param>
    /// <param name="buffer">Output byte array</param>
    /// <param name="offset">Offset into byte array</param>
    /// <param name="count">Number of bytes to read</param>
    public void Read(long src, byte[] buffer, int offset, int count)
    {
      Read(src, count, (sectorBuffer, offsetIntoSector, bufferedRead) =>
      {
        Buffer.BlockCopy(sectorBuffer, offsetIntoSector, buffer, offset, bufferedRead);
        offset += bufferedRead;
      });
    }

    public void Dispose()
    {
      _accessor.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0x30)]
    private struct PFSCHdr
    {
      public int Magic;
      public int Unk4;
      public int Unk8;
      public int BlockSz;
      public long BlockSz2;
      public long BlockOffsets;
      public ulong DataStart;
      public long DataLength;
    }
  }
}
