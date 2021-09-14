using LibOrbisPkg.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Contains the functionality to construct a PFS disk image.
  /// </summary>
  public class PfsBuilder
  {
    static int CeilDiv(int a, int b) => a / b + (a % b == 0 ? 0 : 1);
    static long CeilDiv(long a, long b) => a / b + (a % b == 0 ? 0 : 1);

    private PfsHeader hdr;
    private List<inode> inodes;
    private List<PfsDirent> super_root_dirents;

    private inode super_root_ino, fpt_ino, cr_ino;

    private List<FSDir> allDirs;
    private List<FSFile> allFiles;
    private List<FSNode> allNodes;

    private FlatPathTable fpt;
    private CollisionResolver colResolver;

    private PfsProperties properties;

    private int emptyBlock = 0x4;
    const int xtsSectorSize = 0x1000;

    private struct BlockSigInfo
    {
      public long Block;
      public long SigOffset;
      public int Size;
      public BlockSigInfo(long block, long offset, int size = 0x10000)
      {
        Block = block;
        SigOffset = offset;
        Size = size;
      }
    }
    private Stack<BlockSigInfo> final_sigs = new Stack<BlockSigInfo>();
    private Stack<BlockSigInfo> data_sigs = new Stack<BlockSigInfo>();

    Action<string> logger;
    private void Log(string s) => logger?.Invoke(s);

    /// <summary>
    /// Constructs a PfsBuilder with the given properties and logger.
    /// </summary>
    /// <param name="p">Properties for the image to be built</param>
    /// <param name="logger">Function that is called to report realtime PFS build status.</param>
    public PfsBuilder(PfsProperties p, Action<string> logger = null)
    {
      this.logger = logger;
      properties = p;
      Setup();
    }

    /// <summary>
    /// Computes the final size of this image as it will be written to disk.
    /// </summary>
    /// <returns>PFS Image size</returns>
    public long CalculatePfsSize()
    {
      return hdr.Ndblock * hdr.BlockSize;
    }

    /// <summary>
    /// This gets called by the constructor.
    /// </summary>
    void Setup()
    {
      // TODO: Combine the superroot-specific stuff with the rest of the data block writing.
      // I think this is as simple as adding superroot and flat_path_table to allNodes

      // Insert header digest to be calculated with the rest of the digests
      final_sigs.Push(new BlockSigInfo(0, 0x380, 0x5A0));
      hdr = new PfsHeader {
        BlockSize = properties.BlockSize,
        ReadOnly = 1,
        Mode = (properties.Sign ? PfsMode.Signed : 0) 
             | (properties.Encrypt ? PfsMode.Encrypted : 0)
             | PfsMode.UnknownFlagAlwaysSet,
        UnknownIndex = 1,
        Seed = properties.Encrypt || properties.Sign ? properties.Seed : null
      };
      inodes = new List<inode>();

      Log("Setting up filesystem structure...");
      allDirs = properties.root.GetAllChildrenDirs();
      allFiles = properties.root.GetAllChildrenFiles().Where(f => f.Parent?.name != "sce_sys" || !PKG.EntryNames.NameToId.ContainsKey(f.name)).ToList();
      allNodes = new List<FSNode>(allDirs.OrderBy(d => d.FullPath()).ToList());
      allNodes.AddRange(allFiles);

      SetupRootStructure(FlatPathTable.HasCollision(allNodes));

      Log($"Creating inodes ({allDirs.Count} dirs and {allFiles.Count} files)...");
      addDirInodes();
      addFileInodes();
      
      (fpt, colResolver) = FlatPathTable.Create(allNodes);

      Log("Calculating data block layout...");
      allNodes.Insert(0, properties.root);
      CalculateDataBlockLayout();
    }

    private void WriteData(Stream stream)
    {
      Log("Writing data...");
      hdr.WriteToStream(stream);
      WriteInodes(stream);
      WriteSuperrootDirents(stream);

      allNodes.Insert(0, new FSFile(s => fpt.WriteToStream(s), "flat_path_table", fpt.Size)
      {
        ino = fpt_ino
      });
      if (colResolver != null)
      {
        allNodes.Insert(1, new FSFile(s => colResolver.WriteToStream(s), "collision_resolver", colResolver.Size)
        {
          ino = cr_ino
        });
      }

      for (var x = 0; x < allNodes.Count; x++)
      {
        var f = allNodes[x];
        stream.Position = f.ino.StartBlock * hdr.BlockSize;
        WriteFSNode(stream, f);
      }
    }

    /// <summary>
    /// Enumerates the sectors that should be encrypted with AES-XTS
    /// </summary>
    /// <returns>Sector indices</returns>
    private IEnumerable<long> XtsSectorGen()
    {
      long totalSectors = (CalculatePfsSize() + 0xFFF) / xtsSectorSize;
      long xtsSector = 16;
      while (xtsSector < totalSectors)
      {
        if (xtsSector / 0x10 == emptyBlock)
        {
          xtsSector += 16;
        }
        yield return xtsSector;
        xtsSector += 1;
      }
    }

    /// <summary>
    /// Writes the PFS image using a memory mapped file. This allows for parallelization of signing and encrypting.
    /// </summary>
    /// <param name="file">The memory mapped file</param>
    /// <param name="offset">Start offset of the PFS image in the file</param>
    public void WriteImage(MemoryMappedFile file, long offset)
    {
      using (var viewStream = file.CreateViewStream(offset, CalculatePfsSize(), MemoryMappedFileAccess.ReadWrite))
      {
        WriteData(viewStream);
      }
      using (var view = file.CreateViewAccessor(offset, CalculatePfsSize(), MemoryMappedFileAccess.ReadWrite))
      {
        if (hdr.Mode.HasFlag(PfsMode.Signed))
        {
          Log("Signing in parallel...");
          var signKey = Crypto.PfsGenSignKey(properties.EKPFS, hdr.Seed);
          // We can do the actual data blocks in parallel
          Parallel.ForEach(
            data_sigs,
            () => Tuple.Create(new byte[properties.BlockSize], new HMACSHA256(signKey)),
            (sig, status, local) =>
            {
              var (sig_buffer, hmac) = local;
              var position = sig.Block * sig_buffer.Length;
              view.ReadArray(position, sig_buffer, 0, sig_buffer.Length);
              position = sig.SigOffset;
              view.WriteArray(position, Crypto.HmacSha256(signKey, sig_buffer), 0, 32);
              view.Write(position + 32, (int)sig.Block);
              return local;
            },
            local => local.Item2.Dispose());
          // The indirect blocks must be done after, since they rely on data block signatures
          foreach (var sig in final_sigs)
          {
            var sig_buffer = new byte[sig.Size];
            var position = sig.Block * properties.BlockSize;
            view.ReadArray(position, sig_buffer, 0, sig_buffer.Length);
            position = sig.SigOffset;
            view.WriteArray(position, Crypto.HmacSha256(signKey, sig_buffer), 0, 32);
            view.Write(position + 32, (int)sig.Block);
          }
        }

        if (hdr.Mode.HasFlag(PfsMode.Encrypted))
        {
          Log("Encrypting in parallel...");
          var (tweakKey, dataKey) = Crypto.PfsGenEncKey(properties.EKPFS, hdr.Seed);
          Parallel.ForEach(
            // generates sector indices for each sector to be encrypted
            XtsSectorGen(),
            // generates thread-local data
            () => Tuple.Create(new XtsBlockTransform(dataKey, tweakKey), new byte[xtsSectorSize]),
            // Loop body
            (xtsSector, loopState, localData) =>
            {
              var (transformer, sectorBuffer) = localData;
              var sectorOffset = xtsSector * xtsSectorSize;
              view.ReadArray(sectorOffset, sectorBuffer, 0, xtsSectorSize);
              transformer.EncryptSector(sectorBuffer, (ulong)xtsSector);
              view.WriteArray(sectorOffset, sectorBuffer, 0, xtsSectorSize);
              return localData;
            },
            // Finalizer
            local => { });
        }
      }
    }

    /// <summary>
    /// Writes the PFS image to the given stream
    /// </summary>
    public void WriteImage(Stream stream)
    {
      WriteData(stream);

      if (hdr.Mode.HasFlag(PfsMode.Signed))
      {
        Log("Signing...");
        var signKey = Crypto.PfsGenSignKey(properties.EKPFS, hdr.Seed);
        foreach (var sig in data_sigs.Concat(final_sigs))
        {
          var sig_buffer = new byte[sig.Size];
          stream.Position = sig.Block * properties.BlockSize;
          stream.Read(sig_buffer, 0, sig.Size);
          stream.Position = sig.SigOffset;
          stream.Write(Crypto.HmacSha256(signKey, sig_buffer), 0, 32);
          stream.WriteLE((int)sig.Block);
        }
      }

      if (hdr.Mode.HasFlag(PfsMode.Encrypted))
      {
        Log("Encrypting...");
        var (tweakKey,dataKey) = Crypto.PfsGenEncKey(properties.EKPFS, hdr.Seed);
        var transformer = new XtsBlockTransform(dataKey, tweakKey);
        byte[] sectorBuffer = new byte[xtsSectorSize];
        foreach (var xtsSector in XtsSectorGen())
        {
          stream.Position = xtsSector * xtsSectorSize;
          stream.Read(sectorBuffer, 0, xtsSectorSize);
          transformer.EncryptSector(sectorBuffer, (ulong)xtsSector);
          stream.Position = xtsSector * xtsSectorSize;
          stream.Write(sectorBuffer, 0, xtsSectorSize);
        }
      }
    }

    /// <summary>
    /// Adds inodes for each dir.
    /// </summary>
    void addDirInodes()
    {
      inodes.Add(properties.root.ino);
      foreach (var dir in allDirs.OrderBy(x => x.FullPath()))
      {
        var ino = MakeInode(
          Mode: InodeMode.dir | inode.RXOnly,
          Number: (uint)inodes.Count,
          Blocks: 1,
          Size: 65536,
          Flags: InodeFlags.@readonly,
          Nlink: 2 // 1 link each for its own dirent and its . dirent
        );
        dir.ino = ino;
        dir.Dirents.Add(new PfsDirent { Name = ".", InodeNumber = ino.Number, Type = DirentType.Dot });
        dir.Dirents.Add(new PfsDirent { Name = "..", InodeNumber = dir.Parent.ino.Number, Type = DirentType.DotDot });

        var dirent = new PfsDirent { Name = dir.name, InodeNumber = (uint)inodes.Count, Type = DirentType.Directory };
        dir.Parent.Dirents.Add(dirent);
        dir.Parent.ino.Nlink++;
        inodes.Add(ino);
      }
    }

    /// <summary>
    /// Adds inodes for each file.
    /// </summary>
    void addFileInodes()
    {
      foreach (var file in allFiles.OrderBy(x => x.FullPath()))
      {
        var ino = MakeInode(
          Mode: InodeMode.file | inode.RXOnly,
          Size: file.Size,
          SizeCompressed: file.CompressedSize,
          Number: (uint)inodes.Count,
          Blocks: (uint)CeilDiv(file.Size, hdr.BlockSize),
          Flags: InodeFlags.@readonly | (file.Compress ? InodeFlags.compressed : 0)
        );
        if (properties.Sign) // HACK: Outer PFS images don't use readonly?
        {
          ino.Flags &= ~InodeFlags.@readonly;
        }
        file.ino = ino;
        var dirent = new PfsDirent { Name = file.name, Type = DirentType.File, InodeNumber = (uint)inodes.Count };
        file.Parent.Dirents.Add(dirent);
        inodes.Add(ino);
      }
    }

    long roundUpSizeToBlock(long size) => CeilDiv(size, hdr.BlockSize) * hdr.BlockSize;
    long calculateIndirectBlocks(long size)
    {
      var sigs_per_block = hdr.BlockSize / 36;
      var blocks = CeilDiv(size, hdr.BlockSize);
      var ib = 0L;
      if (blocks > 12)
      {
        blocks -= 12;
        ib++;
      }
      if (blocks > sigs_per_block)
      {
        blocks -= sigs_per_block;
        ib += 1 + CeilDiv(blocks, sigs_per_block);
      }
      return ib;
    }

    ///<summary>
    ///Given an inode number and an index into the db[] array, returns the absolute offset of that array value
    ///</summary>
    long inoNumberToOffset(uint number, int db = 0)
      => hdr.BlockSize + (DinodeS32.SizeOf * number) + 0x64 + (36 * db);

    /// <summary>
    /// Sets the data blocks. Also updates header for total number of data blocks.
    /// </summary>
    void CalculateDataBlockLayout()
    {
      // TODO: Consolidate of all this duplicate code
      if (properties.Sign)
      {
        // Include the header block in the total count
        hdr.Ndblock = 1;
        var inodesPerBlock = hdr.BlockSize / DinodeS32.SizeOf;
        hdr.DinodeCount = inodes.Count;
        hdr.DinodeBlockCount = CeilDiv(inodes.Count, inodesPerBlock);
        hdr.InodeBlockSig.Blocks = (uint)hdr.DinodeBlockCount;
        hdr.InodeBlockSig.Size = hdr.DinodeBlockCount * hdr.BlockSize;
        hdr.InodeBlockSig.SizeCompressed = hdr.DinodeBlockCount * hdr.BlockSize;
        hdr.InodeBlockSig.SetTime(properties.FileTime);
        hdr.InodeBlockSig.Flags = 0;
        for (var i = 0; i < hdr.DinodeBlockCount; i++)
        {
          hdr.InodeBlockSig.SetDirectBlock(i, 1 + i);
          final_sigs.Push(new BlockSigInfo(1 + i, 0xB8 + (36 * i)));
        }
        hdr.Ndblock += hdr.DinodeBlockCount;
        super_root_ino.SetDirectBlock(0, (int)(hdr.DinodeBlockCount + 1));
        final_sigs.Push(new BlockSigInfo(super_root_ino.StartBlock, inoNumberToOffset(super_root_ino.Number)));
        hdr.Ndblock += super_root_ino.Blocks;

        // flat path table
        fpt_ino.SetDirectBlock(0, super_root_ino.StartBlock + 1);
        fpt_ino.Size = fpt.Size;
        fpt_ino.SizeCompressed = fpt.Size;
        fpt_ino.Blocks = (uint)CeilDiv(fpt.Size, hdr.BlockSize);
        final_sigs.Push(new BlockSigInfo(fpt_ino.StartBlock, inoNumberToOffset(fpt_ino.Number)));

        for (int i = 1; i < fpt_ino.Blocks && i < 12; i++)
        {
          fpt_ino.SetDirectBlock(i, (int)hdr.Ndblock++);
          final_sigs.Push(new BlockSigInfo(fpt_ino.StartBlock, inoNumberToOffset(fpt_ino.Number, i)));
        }

        // DATs I've found include an empty block after the FPT
        hdr.Ndblock++;
        // HACK: outer PFS has a block of zeroes that is not encrypted???
        emptyBlock = (int)hdr.Ndblock;
        hdr.Ndblock++;

        var ibStartBlock = hdr.Ndblock;
        hdr.Ndblock += allNodes.Select(s => calculateIndirectBlocks(s.Size)).Sum();

        var sigs_per_block = hdr.BlockSize / 36;
        // Fill in DB/IB pointers
        foreach (var n in allNodes)
        {
          var blocks = CeilDiv(n.Size, hdr.BlockSize);
          n.ino.SetDirectBlock(0, (int)hdr.Ndblock);
          n.ino.Blocks = (uint)blocks;
          n.ino.Size = n is FSDir ? roundUpSizeToBlock(n.Size) : n.Size;
          if (n.ino.SizeCompressed == 0)
            n.ino.SizeCompressed = n.ino.Size;

          for (var i = 0; (blocks - i) > 0 && i < 12; i++)
          {
            data_sigs.Push(new BlockSigInfo((int)hdr.Ndblock++, inoNumberToOffset(n.ino.Number, i)));
          }
          if(blocks > 12)
          {
            // More than 12 blocks -> use 1 indirect block
            // ib[0]
            final_sigs.Push(new BlockSigInfo(ibStartBlock, inoNumberToOffset(n.ino.Number, 12)));
            for(int i = 12, pointerOffset = 0; (blocks - i) > 0 && i < (12 + sigs_per_block); i++, pointerOffset += 36)
            {
              // ib[0][i]
              data_sigs.Push(new BlockSigInfo((int)hdr.Ndblock++, ibStartBlock * hdr.BlockSize + pointerOffset));
            }
            ibStartBlock++;
          }
          if(blocks > 12 + sigs_per_block)
          {
            uint blockSigsDone = 12 + sigs_per_block;
            // More than 12 + one block of pointers -> use 1 doubly-indirect block + any number of indirect blocks
            // ib[1] = signature for block of signatures for block of signatures for data blocks
            final_sigs.Push(new BlockSigInfo(ibStartBlock, inoNumberToOffset(n.ino.Number, 13)));
            var ib_1_block = ibStartBlock;
            for(var i = 0; i < sigs_per_block && blockSigsDone < blocks; i++)
            {
              // ib[1][i] = signature for block of signatures for data blocks
              final_sigs.Push(new BlockSigInfo((int)++ibStartBlock, ib_1_block * hdr.BlockSize + i*36));
              for (int j = 0; j < sigs_per_block && blockSigsDone < blocks; j++, blockSigsDone++)
              {
                // ib[1][i][j] = signature for data block
                data_sigs.Push(new BlockSigInfo((int)hdr.Ndblock++, ibStartBlock * hdr.BlockSize + (j*36)));
              }
            }
          }
        }
      }
      else
      {
        // Include the header block in the total count
        hdr.Ndblock = 1;
        var inodesPerBlock = hdr.BlockSize /DinodeD32.SizeOf;
        hdr.DinodeCount = inodes.Count;
        hdr.DinodeBlockCount = CeilDiv(inodes.Count, inodesPerBlock);
        hdr.InodeBlockSig.Blocks = (uint)hdr.DinodeBlockCount;
        hdr.InodeBlockSig.Size = hdr.DinodeBlockCount * hdr.BlockSize;
        hdr.InodeBlockSig.SizeCompressed = hdr.DinodeBlockCount * hdr.BlockSize;
        hdr.InodeBlockSig.SetDirectBlock(0, (int)hdr.Ndblock++);
        hdr.InodeBlockSig.SetTime(properties.FileTime);
        for (var i = 1; i < hdr.DinodeBlockCount; i++)
        {
          if(i < 12)
            hdr.InodeBlockSig.SetDirectBlock(i, -1);
          hdr.Ndblock++;
        }
        super_root_ino.SetDirectBlock(0, (int)hdr.Ndblock);
        hdr.Ndblock += super_root_ino.Blocks;

        // flat path table
        fpt_ino.SetDirectBlock(0, (int)hdr.Ndblock++);
        fpt_ino.Size = fpt.Size;
        fpt_ino.SizeCompressed = fpt.Size;
        fpt_ino.Blocks = (uint)CeilDiv(fpt.Size, hdr.BlockSize);

        for (int i = 1; i < fpt_ino.Blocks && i < 12; i++)
          fpt_ino.SetDirectBlock(i, (int)hdr.Ndblock++);

        // DATs I've found include an empty block after the FPT if there's no collision resolver
        if(cr_ino == null)
        {
          hdr.Ndblock++;
        }
        else
        {
          // collision resolver
          cr_ino.SetDirectBlock(0, (int)hdr.Ndblock++);
          cr_ino.Size = colResolver.Size;
          cr_ino.SizeCompressed = colResolver.Size;
          cr_ino.Blocks = (uint)CeilDiv(colResolver.Size, hdr.BlockSize);

          for (int i = 1; i < cr_ino.Blocks && i < 12; i++)
            cr_ino.SetDirectBlock(i, (int)hdr.Ndblock++);
        }

        // Calculate length of all dirent blocks
        foreach (var n in allNodes)
        {
          var blocks = CeilDiv(n.Size, hdr.BlockSize);
          n.ino.SetDirectBlock(0, (int)hdr.Ndblock);
          n.ino.Blocks = (uint)blocks;
          n.ino.Size = n is FSDir ? roundUpSizeToBlock(n.Size) : n.Size;
          if(n.ino.SizeCompressed == 0)
            n.ino.SizeCompressed = n.ino.Size;
          for (int i = 1; i < blocks && i < 12; i++)
          {
            n.ino.SetDirectBlock(i, -1);
          }
          hdr.Ndblock += blocks;
        }
      }
      // Hack: set a minimum size for the PFS image.
      hdr.Ndblock = Math.Max(hdr.Ndblock, properties.MinBlocks);
    }

    inode MakeInode(InodeMode Mode, uint Blocks, long Size = 0, long SizeCompressed = 0, ushort Nlink = 1, uint Number = 0, InodeFlags Flags = 0)
    {
      inode ret;
      if (properties.Sign)
      {
        ret = new DinodeS32()
        {
          Mode = Mode,
          Blocks = Blocks,
          Size = Size,
          SizeCompressed = SizeCompressed,
          Nlink = Nlink,
          Number = Number,
          Flags = Flags | InodeFlags.unk2 | InodeFlags.unk3,
        };
      }
      else
      {
        ret = new DinodeD32()
        {
          Mode = Mode,
          Blocks = Blocks,
          Size = Size,
          SizeCompressed = SizeCompressed,
          Nlink = Nlink,
          Number = Number,
          Flags = Flags
        };
      }
      ret.SetTime(properties.FileTime);
      return ret;
    }

    /// <summary>
    /// Creates inodes and dirents for superroot, flat_path_table, and uroot.
    /// Also, creates the root node for the FS tree.
    /// </summary>
    void SetupRootStructure(bool hasCollision)
    {
      var inodeNum = 0u;
      inodes.Add(super_root_ino = MakeInode(
        Mode: InodeMode.dir | inode.RXOnly,
        Blocks: 1,
        Size: 65536,
        SizeCompressed: 65536,
        Nlink: 1,
        Number: inodeNum++,
        Flags: InodeFlags.@internal | InodeFlags.@readonly
      ));
      inodes.Add(fpt_ino = MakeInode(
        Mode: InodeMode.file | inode.RXOnly,
        Blocks: 1,
        Number: inodeNum++,
        Flags: InodeFlags.@internal | InodeFlags.@readonly
      ));
      if(hasCollision)
      {
        inodes.Add(cr_ino = MakeInode(
          Mode: InodeMode.file | inode.RXOnly,
          Blocks: 1,
          Number: inodeNum++,
          Flags: InodeFlags.@internal | InodeFlags.@readonly
        ));
      }
      var uroot_ino = MakeInode(
        Mode: InodeMode.dir | inode.RXOnly,
        Number: inodeNum++,
        Size: 65536,
        SizeCompressed: 65536,
        Blocks: 1,
        Flags: InodeFlags.@readonly,
        Nlink: 3
      );

      super_root_dirents = new List<PfsDirent>
      {
        new PfsDirent { InodeNumber = fpt_ino.Number, Name = "flat_path_table", Type = DirentType.File },
      };
      if(hasCollision)
      {
        super_root_dirents.Add(
          new PfsDirent { InodeNumber = cr_ino.Number, Name = "collision_resolver", Type = DirentType.File });
      }
      super_root_dirents.Add(
        new PfsDirent { InodeNumber = uroot_ino.Number, Name = "uroot", Type = DirentType.Directory });

      properties.root.name = "uroot";
      properties.root.ino = uroot_ino;
      properties.root.Dirents = new List<PfsDirent>
      {
        new PfsDirent { Name = ".", Type = DirentType.Dot, InodeNumber = uroot_ino.Number },
        new PfsDirent { Name = "..", Type = DirentType.DotDot, InodeNumber = uroot_ino.Number }
      };
      if(properties.Sign) // HACK: Outer PFS lacks readonly flags
      {
        super_root_ino.Flags &= ~InodeFlags.@readonly;
        fpt_ino.Flags &= ~InodeFlags.@readonly;
        uroot_ino.Flags &= ~InodeFlags.@readonly;
      }
    }

    /// <summary>
    /// Writes all the inodes to the image file. 
    /// </summary>
    /// <param name="s"></param>
    void WriteInodes(Stream s)
    {
      s.Position = hdr.BlockSize;
      foreach (var di in inodes)
      {
        di.WriteToStream(s);
        if (s.Position % hdr.BlockSize > hdr.BlockSize - (properties.Sign ? DinodeS32.SizeOf : DinodeD32.SizeOf))
        {
          s.Position += hdr.BlockSize - (s.Position % hdr.BlockSize);
        }
      }
    }

    /// <summary>
    /// Writes the dirents for the superroot, which precede the flat_path_table.
    /// </summary>
    /// <param name="stream"></param>
    void WriteSuperrootDirents(Stream stream)
    {
      stream.Position = hdr.BlockSize * (hdr.DinodeBlockCount + 1);
      foreach (var d in super_root_dirents)
      {
        d.WriteToStream(stream);
      }
    }

    /// <summary>
    /// Writes all the data blocks.
    /// </summary>
    /// <param name="s"></param>
    void WriteFSNode(Stream s, FSNode f)
    {
      if (f is FSDir)
      {
        var dir = (FSDir)f;
        var startBlock = f.ino.StartBlock;
        foreach (var d in dir.Dirents)
        {
          d.WriteToStream(s);
          if (s.Position % hdr.BlockSize > hdr.BlockSize - PfsDirent.MaxSize)
          {
            s.Position = (++startBlock * hdr.BlockSize);
          }
        }
      }
      else if (f is FSFile)
      {
        var file = (FSFile)f;
        file.Write(s);
      }
    }
  }
}