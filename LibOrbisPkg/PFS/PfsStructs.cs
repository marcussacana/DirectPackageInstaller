using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// PFS mode flags.
  /// </summary>
  [Flags]
  public enum PfsMode : ushort
  {
    None = 0,
    Signed = 0x1,
    Is64Bit = 0x2,
    Encrypted = 0x4,
    UnknownFlagAlwaysSet = 0x8,
  }
  /// <summary>
  /// Represents a PFS image suberblock.
  /// </summary>
  public class PfsHeader
  {
    public long Version = 1; // 1
    public long Magic = 20130315; // 20130315 (march 15 2013???)
    public long Id = 0;
    public byte Fmode = 0;
    public byte Clean = 0;
    public byte ReadOnly = 0;
    public byte Rsv = 0;
    public PfsMode Mode = PfsMode.UnknownFlagAlwaysSet;
    public ushort Unk1 = 0;
    public uint BlockSize = 0x10000;
    public uint NBackup = 0;
    /// <summary>
    /// This is always 1 for some reason.
    /// </summary>
    public long NBlock = 1;
    public long DinodeCount = 0;
    public long Ndblock = 0;
    public long DinodeBlockCount = 0;
    public DinodeS64 InodeBlockSig = new DinodeS64()
    {
      Mode = 0,
      Nlink = 1,
      Flags = InodeFlags.@readonly,
      Size = 0x10000,
      SizeCompressed = 0x10000,
      Blocks = 1,
    };
    public int UnknownIndex = 0;
    public byte[] Seed;

    public void WriteToStream(Stream s)
    {
      var start = s.Position;
      s.WriteInt64LE(Version);
      s.WriteInt64LE(Magic);
      s.WriteInt64LE(Id);
      s.WriteByte(Fmode);
      s.WriteByte(Clean);
      s.WriteByte(ReadOnly);
      s.WriteByte(Rsv);
      s.WriteUInt16LE((ushort)Mode);
      s.WriteUInt16LE(Unk1);
      s.WriteUInt32LE(BlockSize);
      s.WriteUInt32LE(NBackup);
      s.WriteInt64LE(NBlock);
      s.WriteInt64LE(DinodeCount);
      s.WriteInt64LE(Ndblock);
      s.WriteInt64LE(DinodeBlockCount);
      s.WriteInt64LE(0);
      InodeBlockSig.WriteToStream(s);
      if (Seed != null)
      {
        s.Position = start + 0x36C;
        s.WriteInt32LE(UnknownIndex);
        s.Write(Seed, 0, Seed.Length);
      }
      else
      {
        s.Position = start + 0x368;
        s.WriteInt32LE(1);
      }
    }

    public static PfsHeader ReadFromStream(System.IO.Stream s)
    {
      var start = s.Position;
      var hdr = new PfsHeader
      {
        Version = s.ReadInt64LE(),
        Magic = s.ReadInt64LE(),
        Id = s.ReadInt64LE(),
        Fmode = s.ReadUInt8(),
        Clean = s.ReadUInt8(),
        ReadOnly = s.ReadUInt8(),
        Rsv = s.ReadUInt8(),
        Mode = (PfsMode)s.ReadUInt16LE(),
        Unk1 = s.ReadUInt16LE(),
        BlockSize = s.ReadUInt32LE(),
        NBackup = s.ReadUInt32LE(),
        NBlock = s.ReadInt64LE(),
        DinodeCount = s.ReadInt64LE(),
        Ndblock = s.ReadInt64LE(),
        DinodeBlockCount = s.ReadInt64LE(),
      };
      s.Position += 8; // skip a 64-bit zero
      hdr.InodeBlockSig = DinodeS64.ReadFromStream(s);
      if (hdr.Version != 1 || hdr.Magic != 20130315)
      {
        throw new InvalidDataException($"Invalid PFS superblock version ({hdr.Version}) or magic ({hdr.Magic})");
      }
      s.Position = start + 0x370;
      hdr.Seed = s.ReadBytes(16);
      return hdr;
    }
  }

  /// <summary>
  /// Inode mode flags including user/group/other permissions.
  /// </summary>
  [Flags]
  public enum InodeMode : ushort
  {
    o_read = 1,
    o_write = 2,
    o_execute = 4,
    g_read = 8,
    g_write = 16,
    g_execute = 32,
    u_read = 64,
    u_write = 128,
    u_execute = 256,
    dir = 16384,
    file = 32768,
  }

  /// <summary>
  /// Inode flags for special PFS features
  /// </summary>
  [Flags]
  public enum InodeFlags : uint
  {
    compressed = 0x1,
    unk1 = 0x2,
    unk2 = 0x4,
    unk3 = 0x8,
    @readonly = 0x10,
    unk4 = 0x20,
    unk5 = 0x40,
    unk6 = 0x80,
    unk7 = 0x100,
    unk8 = 0x200,
    unk9 = 0x400,
    unk10 = 0x800,
    unk11 = 0x1000,
    unk12 = 0x2000,
    unk13 = 0x4000,
    unk14 = 0x8000,
    unk15 = 0x10000,
    @internal = 0x20000
  }

  /// <summary>
  /// Base class for inodes. Inodes can be signed or unsigned, and 32 or 64 bit.
  /// </summary>
  public abstract class inode
  {
    public const InodeMode RXOnly =
      InodeMode.o_read | InodeMode.o_execute |
      InodeMode.g_read | InodeMode.g_execute |
      InodeMode.u_read | InodeMode.u_execute;

    public inode()
    {
      SetTime((long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
    }
    /// <summary>
    /// The index of this inode in the block of inodes.
    /// </summary>
    public uint Number;
    /// <summary>
    /// Default is 555 octal.
    /// </summary>
    public InodeMode Mode = (InodeMode)0x16D;
    /// <summary>
    /// Number of links to this file in the filesystem.
    /// 1 for regular files, 1 + 1 for every subdirectory for dirs.
    /// </summary>
    public ushort Nlink;
    public InodeFlags Flags;
    public long Size;
    public long SizeCompressed;
    public long Time1_sec;
    public long Time2_sec;
    public long Time3_sec;
    public long Time4_sec;
    public uint Time1_nsec;
    public uint Time2_nsec;
    public uint Time3_nsec;
    public uint Time4_nsec;
    public uint Uid;
    public uint Gid;
    public ulong Unk1;
    public ulong Unk2;
    public uint Blocks;
    public abstract int StartBlock { get; }
    public abstract IList<int> DirectBlocks { get; }
    public abstract IList<int> IndirectBlocks { get; }

    public abstract void SetDirectBlock(int idx, int block);
    public abstract void WriteToStream(Stream s);
    public inode SetTime(long time)
    {
      Time1_sec = time;
      Time2_sec = time;
      Time3_sec = time;
      Time4_sec = time;
      return this;
    }
  };

  /// <summary>
  /// 32-bit unsigned inodes
  /// </summary>
  public class DinodeD32 : inode
  {
    public const long SizeOf = 0xA8;
    public int[] db = new int[12];
    public int[] ib = new int[5];

    public override int StartBlock => db[0];
    public override IList<int> DirectBlocks => db;
    public override IList<int> IndirectBlocks => ib;

    public override void SetDirectBlock(int idx, int block)
    {
      db[idx] = block;
    }
    public override void WriteToStream(Stream s)
    {
      s.WriteLE((ushort)Mode);
      s.WriteLE(Nlink);
      s.WriteLE((uint)Flags);
      s.WriteLE(Size);
      s.WriteLE(SizeCompressed);
      s.WriteLE(Time1_sec);
      s.WriteLE(Time2_sec);
      s.WriteLE(Time3_sec);
      s.WriteLE(Time4_sec);
      s.WriteLE(Time1_nsec);
      s.WriteLE(Time2_nsec);
      s.WriteLE(Time3_nsec);
      s.WriteLE(Time4_nsec);
      s.WriteLE(Uid);
      s.WriteLE(Gid);
      s.WriteLE(Unk1);
      s.WriteLE(Unk2);
      s.WriteLE(Blocks);
      foreach (var x in db) s.WriteLE(x);
      foreach (var x in ib) s.WriteLE(x);
    }

    public static DinodeD32 ReadFromStream(Stream s)
    {
      var di = new DinodeD32
      {
        Mode = (InodeMode)s.ReadUInt16LE(),
        Nlink = s.ReadUInt16LE(),
        Flags = (InodeFlags)s.ReadUInt32LE(),
        Size = s.ReadInt64LE(),
        SizeCompressed = s.ReadInt64LE(),
        Time1_sec = s.ReadInt64LE(),
        Time2_sec = s.ReadInt64LE(),
        Time3_sec = s.ReadInt64LE(),
        Time4_sec = s.ReadInt64LE(),
        Time1_nsec = s.ReadUInt32LE(),
        Time2_nsec = s.ReadUInt32LE(),
        Time3_nsec = s.ReadUInt32LE(),
        Time4_nsec = s.ReadUInt32LE(),
        Uid = s.ReadUInt32LE(),
        Gid = s.ReadUInt32LE(),
        Unk1 = s.ReadUInt64LE(),
        Unk2 = s.ReadUInt64LE(),
        Blocks = s.ReadUInt32LE()
      };
      for (var i = 0; i < 12; i++) di.db[i] = s.ReadInt32LE();
      for (var i = 0; i < 5; i++) di.ib[i] = s.ReadInt32LE();
      return di;
    }
  };
  /// <summary>
  /// Data structure used in signed 32 bit PFS images
  /// </summary>
  public struct block_sig
  {
    public byte[] sig;
    public int block;
  }
  /// <summary>
  /// Data structure used in signed 64 bit PFS images, and in any signed PFS header.
  /// </summary>
  public struct block_sig64
  {
    public byte[] sig;
    public long block;
  }
  /// <summary>
  /// Signed 32-bit inode
  /// </summary>
  public class DinodeS32 : inode
  {
    public const long SizeOf = 0x2C8;
    public DinodeS32()
    {
      db = new block_sig[12];
      ib = new block_sig[5];
      for(var i = 0; i < 12; i++)
      {
        db[i].sig = new byte[32];
        if (i < 5) ib[i].sig = new byte[32];
      }
    }
    public block_sig[] db;
    public block_sig[] ib;
    public override int StartBlock => db[0].block;
    public override IList<int> DirectBlocks => db.Select(d => d.block).ToList();
    public override IList<int> IndirectBlocks => ib.Select(d => d.block).ToList();

    public override void SetDirectBlock(int idx, int block)
    {
      db[idx].block = block;
    }
    public override void WriteToStream(Stream s)
    {
      s.WriteLE((ushort)Mode);
      s.WriteLE(Nlink);
      s.WriteLE((uint)Flags);
      s.WriteLE(Size);
      s.WriteLE(SizeCompressed);
      s.WriteLE(Time1_sec);
      s.WriteLE(Time2_sec);
      s.WriteLE(Time3_sec);
      s.WriteLE(Time4_sec);
      s.WriteLE(Time1_nsec);
      s.WriteLE(Time2_nsec);
      s.WriteLE(Time3_nsec);
      s.WriteLE(Time4_nsec);
      s.WriteLE(Uid);
      s.WriteLE(Gid);
      s.WriteLE(Unk1);
      s.WriteLE(Unk2);
      s.WriteLE(Blocks);
      foreach (var x in db) {
        s.Write(x.sig, 0, 32);
        s.WriteLE(x.block);
      }
      foreach (var x in ib)
      {
        s.Write(x.sig, 0, 32);
        s.WriteLE(x.block);
      }
    }
    public static DinodeS32 ReadFromStream(Stream s)
    {
      var di = new DinodeS32
      {
        Mode = (InodeMode)s.ReadUInt16LE(),
        Nlink = s.ReadUInt16LE(),
        Flags = (InodeFlags)s.ReadUInt32LE(),
        Size = s.ReadInt64LE(),
        SizeCompressed = s.ReadInt64LE(),
        Time1_sec = s.ReadInt64LE(),
        Time2_sec = s.ReadInt64LE(),
        Time3_sec = s.ReadInt64LE(),
        Time4_sec = s.ReadInt64LE(),
        Time1_nsec = s.ReadUInt32LE(),
        Time2_nsec = s.ReadUInt32LE(),
        Time3_nsec = s.ReadUInt32LE(),
        Time4_nsec = s.ReadUInt32LE(),
        Uid = s.ReadUInt32LE(),
        Gid = s.ReadUInt32LE(),
        Unk1 = s.ReadUInt64LE(),
        Unk2 = s.ReadUInt64LE(),
        Blocks = s.ReadUInt32LE(),
        db = new block_sig[12],
        ib = new block_sig[5],
      };
      for (var i = 0; i < 12; i++) di.db[i] = new block_sig
      {
        sig = s.ReadBytes(32),
        block = s.ReadInt32LE()
      };
      for (var i = 0; i < 5; i++) di.ib[i] = new block_sig
      {
        sig = s.ReadBytes(32),
        block = s.ReadInt32LE()
      };
      return di;
    }
  };

  /// <summary>
  /// Signed 64-bit inode
  /// </summary>
  public class DinodeS64 : inode
  {
    public const long SizeOf = 0x310;
    public DinodeS64()
    {
      db = new block_sig64[12];
      ib = new block_sig64[5];
      for (var i = 0; i < 12; i++)
      {
        db[i].sig = new byte[32];
        if (i < 5) ib[i].sig = new byte[32];
      }
    }
    public block_sig64[] db;
    public block_sig64[] ib;
    public override int StartBlock => (int)db[0].block;
    public override IList<int> DirectBlocks => db.Select(d => (int)d.block).ToList();
    public override IList<int> IndirectBlocks => ib.Select(d => (int)d.block).ToList();

    public override void SetDirectBlock(int idx, int block)
    {
      db[idx].block = block;
    }
    public override void WriteToStream(Stream s)
    {
      s.WriteLE((ushort)Mode);
      s.WriteLE(Nlink);
      s.WriteLE((uint)Flags);
      s.WriteLE(Size);
      s.WriteLE(SizeCompressed);
      s.WriteLE(Time1_sec);
      s.WriteLE(Time2_sec);
      s.WriteLE(Time3_sec);
      s.WriteLE(Time4_sec);
      s.WriteLE(Time1_nsec);
      s.WriteLE(Time2_nsec);
      s.WriteLE(Time3_nsec);
      s.WriteLE(Time4_nsec);
      s.WriteLE(Uid);
      s.WriteLE(Gid);
      s.WriteLE(Unk1);
      s.WriteLE(Unk2);
      s.WriteLE(Blocks);
      s.WriteLE((int)0); // 4 bytes padding to fake 64-bit block size
      foreach (var x in db)
      {
        s.Write(x.sig, 0, 32);
        s.WriteLE(x.block);
      }
      foreach (var x in ib)
      {
        s.Write(x.sig, 0, 32);
        s.WriteLE(x.block);
      }
    }
    public static DinodeS64 ReadFromStream(Stream s)
    {
      var di = new DinodeS64
      {
        Mode = (InodeMode)s.ReadUInt16LE(),
        Nlink = s.ReadUInt16LE(),
        Flags = (InodeFlags)s.ReadUInt32LE(),
        Size = s.ReadInt64LE(),
        SizeCompressed = s.ReadInt64LE(),
        Time1_sec = s.ReadInt64LE(),
        Time2_sec = s.ReadInt64LE(),
        Time3_sec = s.ReadInt64LE(),
        Time4_sec = s.ReadInt64LE(),
        Time1_nsec = s.ReadUInt32LE(),
        Time2_nsec = s.ReadUInt32LE(),
        Time3_nsec = s.ReadUInt32LE(),
        Time4_nsec = s.ReadUInt32LE(),
        Uid = s.ReadUInt32LE(),
        Gid = s.ReadUInt32LE(),
        Unk1 = s.ReadUInt64LE(),
        Unk2 = s.ReadUInt64LE(),
        Blocks = (uint)s.ReadInt64LE(),
        db = new block_sig64[12],
        ib = new block_sig64[5],
      };
      for (var i = 0; i < 12; i++) di.db[i] = new block_sig64
      {
        sig = s.ReadBytes(32),
        block = s.ReadInt64LE()
      };
      for (var i = 0; i < 5; i++) di.ib[i] = new block_sig64
      {
        sig = s.ReadBytes(32),
        block = s.ReadInt64LE()
      };
      return di;
    }
  };

  /// <summary>
  /// Represents a PFS dirent. Directories are stored ondisk as blocks of dirents.
  /// </summary>
  public class PfsDirent
  {
    public static int MaxSize = 280;

    public uint InodeNumber;
    public DirentType Type;
    public int NameLength;
    public int EntSize;

    public string Name
    {
      get { return name; }
      set
      {
        name = value;
        NameLength = name.Length;
        EntSize = CalculateEntSize();
      }
    }

    public int CalculateEntSize()
    {
      var EntSize = NameLength + 17;
      if (EntSize % 8 != 0)
        EntSize += 8 - (EntSize % 8);
      return EntSize;
    }

    private string name;

    public void WriteToStream(Stream s)
    {
      var pos = s.Position;
      s.WriteLE(InodeNumber);
      s.WriteLE((int)Type);
      s.WriteLE(NameLength);
      s.WriteLE(EntSize);
      s.Write(Encoding.ASCII.GetBytes(Name), 0, NameLength);
      var remaining = (int)(EntSize - (s.Position - pos));
      s.Write(new byte[remaining], 0, remaining);
    }

    public static PfsDirent ReadFromStream(Stream s)
    {
      var pos = s.Position;
      var d = new PfsDirent
      {
        InodeNumber = s.ReadUInt32LE(),
        Type = (DirentType)s.ReadInt32LE(),
        NameLength = s.ReadInt32LE(),
        EntSize = s.ReadInt32LE(),
      };
      d.name = s.ReadASCIINullTerminated(d.NameLength);
      s.Position = pos + d.EntSize;
      return d;
    }
  }
  /// <summary>
  /// Describes the nature of the dirent
  /// </summary>
  public enum DirentType : int
  {
    /// <summary>
    /// A regular file
    /// </summary>
    File = 2,
    /// <summary>
    /// A directory
    /// </summary>
    Directory = 3,
    /// <summary>
    /// The special "." file that points to the current dir
    /// </summary>
    Dot = 4,
    /// <summary>
    /// The special ".." file that points to the parent dir
    /// </summary>
    DotDot = 5
  }
}
