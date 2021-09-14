using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PKG
{
  /// <summary>
  /// Represents the data of an entry
  /// </summary>
  public abstract class Entry
  {
    public abstract EntryId Id { get; }
    public abstract uint Length { get; }
    public abstract string Name { get; }
    public abstract void Write(Stream s);
    public MetaEntry meta;

    /// <summary>
    /// Writes the entry in an encrypted form to the given stream.
    /// </summary>
    public void WriteEncrypted(Stream s, string contentId, string passcode)
    {
      var iv_key = Crypto.Sha256(
            meta.GetBytes()
            .Concat(Crypto.ComputeKeys(contentId, passcode, meta.KeyIndex))
            .ToArray());
      var tmp = new byte[Length];
      using (var ms = new MemoryStream(tmp))
      {
        Write(ms);
      }
      Crypto.AesCbcCfb128Encrypt(tmp, tmp, tmp.Length, iv_key.Skip(16).Take(16).ToArray(), iv_key.Take(16).ToArray());
      s.Write(tmp, 0, tmp.Length);
    }

    private static byte[] Decrypt(byte[] entryBytes, byte[] keySeed, MetaEntry meta)
    {
      var iv_key = Crypto.Sha256(
             meta.GetBytes()
             .Concat(keySeed)
             .ToArray());
      var tmp = new byte[entryBytes.Length];
      Crypto.AesCbcCfb128Decrypt(tmp, entryBytes, tmp.Length, iv_key.Skip(16).Take(16).ToArray(), iv_key.Take(16).ToArray());
      return tmp;
    }

    /// <summary>
    /// Decrypts the given bytes using the entry encryption.
    /// </summary>
    public static byte[] Decrypt(byte[] entryBytes, string contentId, string passcode, MetaEntry meta)
    {
      return Decrypt(entryBytes, Crypto.ComputeKeys(contentId, passcode, meta.KeyIndex), meta);
    }

    /// <summary>
    /// Decrypts the given entry using the entry encryption.
    /// Throws an exception if it can't be decrypted.
    /// </summary>
    public static byte[] Decrypt(byte[] entryBytes, Pkg pkg, MetaEntry meta)
    {
      if(meta.KeyIndex != 3)
      {
        throw new Exception("We only have the key for encryption key 3");
      }
      return Decrypt(entryBytes, Crypto.RSA2048Decrypt(pkg.EntryKeys.Keys[3].key, RSAKeyset.PkgDerivedKey3Keyset), meta);
    }
  }

  /// <summary>
  /// The representation of an entry in the PKG entry table.
  /// </summary>
  public class MetaEntry
  {
    public EntryId id;
    public uint NameTableOffset;
    public uint Flags1;
    public uint Flags2;
    public uint DataOffset;
    public uint DataSize;
    // public ulong Pad; // zero-pad

    public void Write(Stream s)
    {
      s.WriteUInt32BE((uint)id);
      s.WriteUInt32BE(NameTableOffset);
      s.WriteUInt32BE(Flags1);
      s.WriteUInt32BE(Flags2);
      s.WriteUInt32BE(DataOffset);
      s.WriteUInt32BE(DataSize);
      s.Position += 8; // pad
    }
    public static MetaEntry Read(Stream s)
    {
      var ret = new MetaEntry();
      ret.id = (EntryId)s.ReadUInt32BE();
      ret.NameTableOffset = s.ReadUInt32BE();
      ret.Flags1 = s.ReadUInt32BE();
      ret.Flags2 = s.ReadUInt32BE();
      ret.DataOffset = s.ReadUInt32BE();
      ret.DataSize = s.ReadUInt32BE();
      s.Position += 8;
      return ret;
    }
    public uint KeyIndex => (Flags2 & 0xF000) >> 12;
    public bool Encrypted => (Flags1 & 0x80000000) != 0;

    public byte[] GetBytes()
    {
      var buf = new byte[32];
      using (var ms = new MemoryStream(buf))
      {
        Write(ms);
      }
      return buf;
    }
  }
  
  /// <summary>
  /// param.sfo file entry
  /// </summary>
  public class SfoEntry : Entry
  {
    public readonly SFO.ParamSfo ParamSfo;
    public SfoEntry(SFO.ParamSfo paramSfo)
    {
      ParamSfo = paramSfo;
    }
    public override EntryId Id => EntryId.PARAM_SFO;
    public override string Name => "param.sfo";
    public override uint Length => (uint)ParamSfo.FileSize;
    public override void Write(Stream s)
    {
      var sfoBytes = ParamSfo.Serialize();
      s.Write(sfoBytes, 0, sfoBytes.Length);
    }
  }

  /// <summary>
  /// Generic entry, for when all you need is a bunch o' bytes
  /// </summary>
  public class GenericEntry : Entry
  {
    public GenericEntry(EntryId id, string name = null)
    {
      Id = id;
      Name = name;
    }

    public byte[] FileData;
    public override EntryId Id { get; }
    public override string Name { get; }
    public override uint Length => (uint)(FileData?.Length ?? 0);
    public override void Write(Stream s)
    {
      s.Write(FileData, 0, FileData.Length);
    }
  }

  /// <summary>
  /// FileEntry, which lets you use a thunk to write the entry's data when it's needed.
  /// </summary>
  public class FileEntry : Entry
  {
    /// <summary>
    /// FileEntry constructor abstraction for any file
    /// </summary>
    /// <param name="id">ID of the file entry</param>
    /// <param name="writer">Thunk to write the file to a stream</param>
    /// <param name="length">Length of the file</param>
    public FileEntry(EntryId id, Action<Stream> writer, uint length)
    {
      Id = id;
      Name = EntryNames.IdToName[id];
      Length = length;
      Writer = writer;
    }
    /// <summary>
    /// FileEntry constructor for a file on the hard drive
    /// </summary>
    /// <param name="id">Entry ID</param>
    /// <param name="path">Path to the file</param>
    public FileEntry(EntryId id, string path)
      : this(id, s => { using (var f = File.OpenRead(path)) f.CopyTo(s); }, (uint)new FileInfo(path).Length)
    { }
    private Action<Stream> Writer;
    public override EntryId Id { get; }
    public override string Name { get; }
    public override uint Length { get; }
    public override void Write(Stream s) => Writer(s);
  }

  /// <summary>
  /// A single RSA-encrypted key and its digest.
  /// </summary>
  public class PkgEntryKey
  {
    public byte[] digest = new byte[32];
    public byte[] key = new byte[256];
  }

  /// <summary>
  /// The ENTRY_KEYS entry.
  /// </summary>
  public class KeysEntry : Entry
  {
    public KeysEntry(byte[] digest, PkgEntryKey[] keys)
    {
      seedDigest = digest;
      Keys = keys;
    }
    public KeysEntry(string contentId, string passcode)
    {
      Keys = new PkgEntryKey[7];
      seedDigest = Crypto.Sha256(Encoding.ASCII.GetBytes(contentId.PadRight(48, '\0')));
      for (uint i = 0; i < 7; i++)
      {
        var passcodeKey = Crypto.ComputeKeys(contentId, passcode, i);
        Keys[i] = new PkgEntryKey
        {
          digest = Crypto.Sha256(passcodeKey).Xor(passcodeKey),
          key = Crypto.RSA2048EncryptKey(Util.Keys.PkgPublicKeys[i], passcodeKey)
        };
      }
      Keys[0].key = Crypto.RSA2048EncryptKey(Util.Keys.PkgPublicKeys[0], Encoding.ASCII.GetBytes(passcode));
    }
    public byte[] seedDigest;
    public PkgEntryKey[] Keys;
    public override EntryId Id => EntryId.ENTRY_KEYS;
    public override string Name => null;
    public override uint Length => 2048;
    public override void Write(Stream s)
    {
      s.Write(seedDigest, 0, 32);
      foreach(var key in Keys)
      {
        s.Write(key.digest, 0, 32);
      }
      foreach(var key in Keys)
      {
        s.Write(key.key, 0, 256);
      }
    }
    public static KeysEntry Read(MetaEntry e, Stream pkg)
    {
      pkg.Position = e.DataOffset;
      var seedDigest = pkg.ReadBytes(32);
      var digests = new byte[7][];
      var keys = new PkgEntryKey[7];
      for(var x = 0; x < 7; x++)
      {
        digests[x] = pkg.ReadBytes(32);
      }
      for(var x = 0; x < 7; x++)
      {
        keys[x] = new PkgEntryKey
        {
          digest = digests[x],
          key = pkg.ReadBytes(256)
        };
      }
      return new KeysEntry(seedDigest, keys) { meta = e };
    }
  }

  /// <summary>
  /// The table of names for entries that have filenames.
  /// </summary>
  public class NameTableEntry : Entry
  {
    /// <summary>
    /// Default constructor, intended for a new PKG
    /// </summary>
    public NameTableEntry() { }
    /// <summary>
    /// Constructor intended to be used when reading from a PKG
    /// </summary>
    public NameTableEntry(List<string> names)
    {
      int len = 0;
      Names = new Dictionary<string, int>();
      nameList = names;
      foreach(var n in names)
      {
        Names.Add(n, len);
        len += n.Length + 1;
      }
    }
    private int length = 1;
    private Dictionary<string, int> Names = new Dictionary<string, int> { { "", 0 } };
    private List<string> nameList = new List<string> { "" };
    /// <summary>
    /// Gets the offset of a name, adding it to the table if it's not here already.
    /// </summary>
    public uint GetOffset(string name)
    {
      if (name == null || name == "") return 0;
      if(!Names.ContainsKey(name))
      {
        nameList.Add(name);
        Names[name] = length;
        length += name.Length + 1;
      }
      return (uint)Names[name];
    }

    public string GetName(uint offset)
    {
      int s = 0;
      foreach(var n in nameList)
      {
        if (s == offset) return n;
        s += n.Length + 1;
      }
      return null;
    }

    public override EntryId Id => EntryId.ENTRY_NAMES;
    public override string Name => null;
    public override uint Length => (uint)length;
    public override void Write(Stream s)
    {
      foreach(var k in nameList)
      {
        var bytes = Encoding.ASCII.GetBytes(k);
        s.Write(bytes, 0, bytes.Length);
        s.WriteByte(0);
      }
    }

    public static NameTableEntry Read(MetaEntry e, Stream pkg)
    {
      var sz = 0;
      var names = new List<string>();
      pkg.Position = e.DataOffset;
      while(sz < e.DataSize)
      {
        var name = pkg.ReadASCIINullTerminated((int)e.DataSize);
        names.Add(name);
        sz += name.Length + 1;
      }
      return new NameTableEntry(names) { meta = e };
    }
  }

  [Flags]
  public enum GeneralDigest : int
  {
    DigestMetaData = 1 << 0, // Never set.
    ContentDigest = 1 << 1,
    GameDigest = 1 << 2,
    HeaderDigest = 1 << 3,
    SystemDigest = 1 << 4,
    MajorParamDigest = 1 << 5,
    ParamDigest = 1 << 6,
    PlaygoDigest = 1 << 7,
    TrophyDigest = 1 << 8,
    ManualDigest = 1 << 9,
    KeymapDigest = 1 << 10,
    OriginDigest = 1 << 11,
    TargetDigest = 1 << 12,
    OriginGameDigest = 1 << 13,
    TargetGameDigest = 1 << 14,
  }
  /// <summary>
  /// The GENERAL_DIGESTS entry.
  /// </summary>
  public class GeneralDigestsEntry : Entry
  {
    public ushort unk1 = 0xD256;
    public ushort type = 0x100;
    public GeneralDigest set_digests = 0;
    public Dictionary<GeneralDigest, byte[]> Digests = new Dictionary<GeneralDigest, byte[]>
    {
      { GeneralDigest.ContentDigest, new byte[32] },
      { GeneralDigest.GameDigest, new byte[32] },
      { GeneralDigest.HeaderDigest, new byte[32] },
      { GeneralDigest.SystemDigest, new byte[32] },
      { GeneralDigest.MajorParamDigest, new byte[32] },
      { GeneralDigest.ParamDigest, new byte[32] },
      { GeneralDigest.PlaygoDigest, new byte[32] },
      { GeneralDigest.TrophyDigest, new byte[32] },
      { GeneralDigest.ManualDigest, new byte[32] },
      { GeneralDigest.KeymapDigest, new byte[32] },
      { GeneralDigest.OriginDigest, new byte[32] },
      { GeneralDigest.TargetDigest, new byte[32] },
      { GeneralDigest.OriginGameDigest, new byte[32] },
      { GeneralDigest.TargetGameDigest, new byte[32] },
    };

    public void Set(GeneralDigest flag, byte[] value)
    {
      Buffer.BlockCopy(value, 0, Digests[flag], 0, 32);
      set_digests |= flag;
    }

    public override EntryId Id => EntryId.GENERAL_DIGESTS;
    public override uint Length => 
      type == 0x100 ? 0x180u 
      : type == 0x101 ? 0x1C0u
      : 0x1E0u;
    public override string Name => null;

    public override void Write(Stream s)
    {
      s.WriteUInt16BE(unk1);
      s.WriteUInt16BE(type);
      s.Position += 24;
      s.WriteInt32BE((int)set_digests);
      s.Write(Digests[GeneralDigest.ContentDigest], 0, 32); 
      s.Write(Digests[GeneralDigest.GameDigest], 0, 32); 
      s.Write(Digests[GeneralDigest.HeaderDigest], 0, 32); 
      s.Write(Digests[GeneralDigest.SystemDigest], 0, 32); 
      s.Write(Digests[GeneralDigest.MajorParamDigest], 0, 32); 
      s.Write(Digests[GeneralDigest.ParamDigest], 0, 32);
      if(type > 0x100)
      {
        s.Write(Digests[GeneralDigest.PlaygoDigest], 0, 32);
        s.Write(Digests[GeneralDigest.TrophyDigest], 0, 32);
        s.Write(Digests[GeneralDigest.ManualDigest], 0, 32);
        s.Write(Digests[GeneralDigest.KeymapDigest], 0, 32);
      }
      else
      {
        s.Position += 0xA0;
      }
    }

    public static GeneralDigestsEntry Read(Stream s)
    {
      var ret = new GeneralDigestsEntry();
      ret.unk1 = s.ReadUInt16BE();
      ret.type = s.ReadUInt16BE();
      s.Position += 24;
      ret.set_digests = (GeneralDigest)s.ReadUInt32BE();
      for(var d = GeneralDigest.ContentDigest; (int)d < 1 << 15; d = (GeneralDigest)((int)d << 1))
      {
        s.Read(ret.Digests[d], 0, 32);
      }
      return ret;
    }
  }

  /// <summary>
  /// The table of meta entries that points to the rest of the entries.
  /// </summary>
  public class MetasEntry : Entry
  {
    public List<MetaEntry> Metas = new List<MetaEntry>();
    public override EntryId Id => EntryId.METAS;
    public override uint Length => (uint) Metas.Count * 32;
    public override string Name => null;
    public override void Write(Stream s)
    {
      foreach(var entry in Metas)
      {
        entry.Write(s);
      }
    }
  }

}
