using System.IO;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Writes a PFSC header to to a stream. Doesn't actually do compression or anything interesting.
  /// </summary>
  class PFSCWriter
  {
    const int BlockSize = 0x10000;
    /*
     * PFSC Header
     * NUM_BLOCKS = CEIL(size / BLOCK_SZ)
     * 0x000 : PFSC Magic (4 bytes)
     * 0x004 : Unknown (8 bytes)
     * 0x00C : Block Size (4 bytes)
     * 0x010 : Block Size (8 bytes)
     * 0x018 : Block offsets pointer (4 bytes)
     * 0x020 : Data start (8 bytes)
     * 0x028 : Data length (8 bytes)
     * 0x400 : Blocks (8 bytes * NUM_BLOCKS)
     * 0x10000 : Data (variable)
     */
    private long num_blocks;
    public PFSCWriter(long size)
    {
      num_blocks = (size + BlockSize - 1) / BlockSize;
      var pointer_table_size = 8 + num_blocks * 8;
      var additional_pointer_blocks = ((pointer_table_size - 0xFC00) + 0xFFFF) / 0x10000;
      HeaderSize = 0x10000 + (additional_pointer_blocks > 0 ? BlockSize * additional_pointer_blocks : 0);
    }

    public readonly long HeaderSize;
    public void WritePFSCHeader(Stream s)
    {
      var start = s.Position;
      s.WriteInt32BE(0x50465343); // 'PFSC'
      s.WriteLE(0);
      s.WriteLE(6);
      s.WriteLE(BlockSize);
      s.WriteLE((long)BlockSize);
      s.WriteLE(0x400L);
      s.WriteLE(HeaderSize);
      s.WriteLE(num_blocks * BlockSize);
      s.Position = start + 0x400L;
      for(long i = 0; i <= num_blocks; i++)
      {
        s.WriteLE(HeaderSize + (i * BlockSize));
      }
      s.Position = start + HeaderSize;
    }
  }
}
