using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Class allowing parallel readonly access to a PFS archive
  /// </summary>
  public class PfsReader
  {
    /// <summary>
    /// Represents a file or directory in a PFS image.
    /// </summary>
    public abstract class Node
    {
      public Dir parent;
      public string name;
      public long offset;
      public long size;
      public long compressed_size;
      public uint ino;
      public string FullName => parent != null ? parent.FullName + "/" + name : name;
    }
    /// <summary>
    /// Represents a directory in a PFS image.
    /// </summary>
    public class Dir : Node
    {
      public List<Node> children = new List<Node>();
      public Node Get(string name)
        => children.Where(x => x.name == name).FirstOrDefault();
      public Node GetPath(string name)
      {
        var breadcrumbs = name.Split('/');
        Node n = this;
        var bc = 0;
        while(n != null && bc < breadcrumbs.Length)
        {
          n = (n as Dir)?.Get(breadcrumbs[bc]);
          bc++;
        }
        if(bc < breadcrumbs.Length)
        {
          return null;
        }
        return n;
      }
      public IEnumerable<File> GetAllFiles()
      {
        foreach(var n in children)
        {
          if (n is File f) yield return f;
          if (n is Dir d)
            foreach (var x in d.GetAllFiles())
              yield return x;
        }
      }
    }
    /// <summary>
    /// Represents a file in a PFS image.
    /// </summary>
    public class File : Node
    {
      public int[] blocks;
      private IMemoryReader reader;
      public File(IMemoryReader r) { reader = r; }
      public IMemoryReader GetView()
      {
        if (blocks != null)
          return new ChunkedMemoryReader(reader, 0x10000, blocks);
        return new MemoryAccessor(reader, offset);
      }
      public void Save(string path, bool decompress = false)
      {
        var buf = new byte[0x10000];
        using (var file = System.IO.File.OpenWrite(path))
        {
          var sz = size;
          file.SetLength(sz);
          long pos = 0;
          var reader = GetView();
          if (decompress && size != compressed_size)
          {
            sz = compressed_size;
            reader = new PFSCReader(reader);
          }
          while (sz > 0)
          {
            var toRead = (int)Math.Min(sz, buf.Length);
            reader.Read(pos, buf, 0, toRead);
            file.Write(buf, 0, toRead);
            pos += toRead;
            sz -= toRead;
          }
        }
      }
    }

    // Private state for the PfsReader class
    private IMemoryReader reader;
    private PfsHeader hdr;
    private inode[] dinodes;
    private Dir root;
    private Dir uroot;
    private byte[] sectorBuf;
    private Stream sectorStream;

    public PfsReader(MemoryMappedViewAccessor r, ulong pfs_flags = 0, byte[] ekpfs = null, byte[] tweak = null, byte[] data = null)
    : this(new MemoryMappedViewAccessor_(r), pfs_flags, ekpfs, tweak, data)
    { }
    public PfsReader(IMemoryReader r, ulong pfs_flags = 0, byte[] ekpfs = null, byte[] tweak = null, byte[] data = null)
    {
      reader = r;
      var buf = new byte[0x400];
      reader.Read(0, buf, 0, 0x400);

      using (var ms = new MemoryStream(buf))
      {
        hdr = PfsHeader.ReadFromStream(ms);
      }
      int dinodeSize;
      Func<Stream, inode> dinodeReader;
      if (hdr.Mode.HasFlag(PfsMode.Signed))
      {
        dinodes = new DinodeS32[hdr.DinodeCount];
        dinodeReader = DinodeS32.ReadFromStream;
        dinodeSize = 0x2C8;
      }
      else
      {
        dinodes = new DinodeD32[hdr.DinodeCount];
        dinodeReader = DinodeD32.ReadFromStream;
        dinodeSize = 0xA8;
      }
      if (hdr.Mode.HasFlag(PfsMode.Encrypted))
      {
        const int XtsSectorSize = 0x1000;
        uint XtsStartSector = hdr.BlockSize / XtsSectorSize;
        if (ekpfs == null && (tweak == null || data == null))
          throw new ArgumentException("PFS image is encrypted but no decryption key was provided");
        if (ekpfs != null)
        {
          var (tweakKey, dataKey) = Crypto.PfsGenEncKey(ekpfs, hdr.Seed, (pfs_flags & 0x2000000000000000UL) != 0);
          reader = new XtsDecryptReader(reader, dataKey, tweakKey, XtsStartSector, XtsSectorSize);
        }
        else
        {
          reader = new XtsDecryptReader(reader, data, tweak, XtsStartSector, XtsSectorSize);
        }
      }
      var total = 0;

      var maxPerSector = hdr.BlockSize / dinodeSize;
      sectorBuf = new byte[hdr.BlockSize];
      sectorStream = new MemoryStream(sectorBuf);
      for (var i = 0; i < hdr.DinodeBlockCount; i++)
      {
        var position = hdr.BlockSize + hdr.BlockSize * i;
        reader.Read(position, sectorBuf, 0, sectorBuf.Length);
        sectorStream.Position = 0;
        for (var j = 0; j < maxPerSector && total < hdr.DinodeCount; j++)
          dinodes[total++] = dinodeReader(sectorStream);
      }
      root = LoadDir(0, null, "");
      uroot = root.Get("uroot") as Dir;
      if (uroot == null)
        throw new Exception("Invalid PFS image (no uroot)");
      uroot.name = "uroot";
    }

    public PfsHeader Header => hdr;

    public File GetFile(string fullPath)
    {
      return uroot.GetPath(fullPath) as File;
    }

    public IEnumerable<File> GetAllFiles()
    {
      return uroot.GetAllFiles();
    }

    public Dir GetURoot()
    {
      return uroot;
    }

    public Dir GetSuperRoot()
    {
      return root;
    }

    private Dir LoadDir(uint dinode, Dir parent, string name)
    {
      // 100M blocks is enough for a 6TB file.
      const int MAX_BLOCKS = 100_000_000;
      var ret = new Dir() { name = name, parent = parent };
      var ino = dinodes[dinode];
      var postLoad = new List<Func<Dir>>();
      var blocks = (int)ino.Blocks;
      if (blocks < 1 || ino.StartBlock < 1 || ino.StartBlock > MAX_BLOCKS || blocks > MAX_BLOCKS)
      {
        throw new Exception($"inode {dinode} is corrupt. ");
      }
      foreach (var x in Enumerable.Range(ino.StartBlock, blocks))
      {
        var position = hdr.BlockSize * x;
        reader.Read(position, sectorBuf, 0, sectorBuf.Length);
        sectorStream.Position = 0;
        while (position < hdr.BlockSize * (x + 1))
        {
          var dirent = PfsDirent.ReadFromStream(sectorStream);
          if (dirent.EntSize == 0) break;
          switch (dirent.Type)
          {
            case DirentType.File:
              ret.children.Add(LoadFile(dirent.InodeNumber, ret, dirent.Name));
              break;
            case DirentType.Directory:
              postLoad.Add(() => LoadDir(dirent.InodeNumber, ret, dirent.Name));
              break;
            case DirentType.Dot:
              break;
            case DirentType.DotDot:
              break;
            default:
              break;
          }
          position += dirent.EntSize;
        }
      }
      foreach (var p in postLoad)
      {
        ret.children.Add(p());
      }
      return ret;
    }

    private File LoadFile(uint dinode, Dir parent, string name)
    {
      int[] blocks = null;
      if(dinodes[dinode].Blocks > 1 && dinodes[dinode].DirectBlocks[1] != -1)
      {
        if (!hdr.Mode.HasFlag(PfsMode.Signed))
        {
          throw new Exception("Unsigned PFS images probably shouldn't have noncontiguous blocks");
        }
        blocks = new int[dinodes[dinode].Blocks];
        var remainingBlocks = (long)dinodes[dinode].Blocks;
        var sigsPerBlock = hdr.BlockSize / 36;
        for (int i = 0; i < 12 && i < remainingBlocks; i++)
        {
          blocks[i] = dinodes[dinode].DirectBlocks[i];
        }

        var bufferedReader = new BufferedMemoryReader(reader, 0x10000);
        remainingBlocks -= 12;
        long blockIndexOffset = 12;
        for (int i = 0; i < remainingBlocks && i < sigsPerBlock; i++)
        {
          bufferedReader.Read(dinodes[dinode].IndirectBlocks[0] * hdr.BlockSize + (i * 36) + 32, out blocks[i + blockIndexOffset]);
        }
        remainingBlocks -= sigsPerBlock;
        blockIndexOffset += sigsPerBlock;
        for (int j = 0; j * sigsPerBlock < remainingBlocks; j++)
        {
          bufferedReader.Read(dinodes[dinode].IndirectBlocks[1] * hdr.BlockSize + (j * 36) + 32, out int indirectBlockOffset);
          for (int i = 0; i < sigsPerBlock && i + (j * sigsPerBlock) < remainingBlocks; i++)
          {
            bufferedReader.Read(indirectBlockOffset * hdr.BlockSize + (i * 36) + 32, out blocks[i + blockIndexOffset]);
          }
          blockIndexOffset += sigsPerBlock;
        }
        bool contiguous = true;
        int last = blocks[0] - 1;
        for(int i = 1; i < blocks.Length; i++)
        {
          if(blocks[i - 1] + 1 != blocks[i])
          {
            contiguous = false;
            break;
          }
        }
        if (contiguous)
          blocks = null;
      }
      return new File(reader)
      {
        name = name,
        parent = parent,
        offset = dinodes[dinode].StartBlock * hdr.BlockSize,
        size = dinodes[dinode].Size,
        compressed_size = dinodes[dinode].SizeCompressed,
        ino = dinode,
        blocks = blocks,
      };
    }
  }
}
