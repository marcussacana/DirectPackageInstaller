using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PKG
{
  public class Pkg
  {
    // 0x0 - 0x5A0
    public Header Header;
    // 0xFE0 - 0xFFF
    public byte[] HeaderDigest;
    // 0x1000 - 0x10FF
    public byte[] HeaderSignature;
    // 0x2000 - 0x27FF
    public KeysEntry EntryKeys;
    // 0x2800 - 0x28FF
    public GenericEntry ImageKey;
    // 0x2900 - 0x2A7F
    public GeneralDigestsEntry GeneralDigests;
    // 0x2A80 - 0x2xxx
    public MetasEntry Metas;
    // variable...
    public GenericEntry Digests;
    public NameTableEntry EntryNames;
    public Rif.LicenseDat LicenseDat;
    public GenericEntry LicenseInfo;
    public SfoEntry ParamSfo;
    public GenericEntry PsReservedDat;
    public PlayGo.ChunkDat ChunkDat;
    public GenericEntry ChunkSha;
    public GenericEntry ChunkXml;

    public List<Entry> Entries;

    // Constants
    const uint PKG_FLAG_FINALIZED = 1u << 31;
    const ulong PKG_PFS_FLAG_NESTED_IMAGE = 0x8000000000000000UL;
    public const int PKG_TABLE_ENTRY_SIZE = 0x20;
    public const int PKG_ENTRY_KEYSET_SIZE = 0x20;
    public const int HASH_SIZE = 0x20;
    public const string MAGIC = "\u007FCNT";

    const string PKG_ENTRY_NAME__PARAM_SFO = "param.sfo";
    const string PKG_ENTRY_NAME__SHAREPARAM_JSON = "shareparam.json";

    const uint PKG_SC_ENTRY_ID_START = (uint)EntryId.LICENSE_DAT;

    const int PKG_MAX_ENTRY_KEYS = 7;
    const int PKG_CONTENT_ID_HASH_SIZE = HASH_SIZE;
    const int PKG_ENTRY_KEYS_XHASHES_SIZE = (PKG_MAX_ENTRY_KEYS * HASH_SIZE);
    const int PKG_PASSCODE_KEY_SIZE = 0x100;
    const int PKG_IMAGE_KEY_SIZE = 0x100;
    const int PKG_ENTRY_KEY_SIZE = 0x100;

    const int PKG_PLAYGO_CHUNK_HASH_TABLE_OFFSET = 0x40;
    const int PKG_PLAYGO_CHUNK_HASH_SIZE = 0x4;
    const int PKG_PLAYGO_PFS_CHUNK_SIZE = 0x10000;

    const int PKG_SHAREPARAM_FILE_VERSION_MAJOR = 1;
    const int PKG_SHAREPARAM_FILE_VERSION_MINOR = 10;

    public const int PKG_CONTENT_ID_SIZE = 0x30;
    public const int PKG_HEADER_SIZE = 0x5A0;
    public const int PKG_ENTRY_KEYSET_ENC_SIZE = 0x100;

    /// <summary>
    /// Decrypts the EKPFS for a fake PKG. Will not work on non-fake PKGs.
    /// </summary>
    /// <param name="pkg"></param>
    /// <param name="passcode"></param>
    /// <returns>The EKPFS if successful; null otherwise</returns>
    public byte[] GetEkpfs()
    {
      try
      {
        var dk3 = Crypto.RSA2048Decrypt(EntryKeys.Keys[3].key, RSAKeyset.PkgDerivedKey3Keyset);
        var iv_key = Crypto.Sha256(ImageKey.meta.GetBytes().Concat(dk3).ToArray());
        var imageKeyDecrypted = ImageKey.FileData.Clone() as byte[];
        Crypto.AesCbcCfb128Decrypt(
          imageKeyDecrypted,
          imageKeyDecrypted,
          imageKeyDecrypted.Length,
          iv_key.Skip(16).Take(16).ToArray(),
          iv_key.Take(16).ToArray());
        return Crypto.RSA2048Decrypt(imageKeyDecrypted, RSAKeyset.FakeKeyset);
      }
      catch
      {
        return null;
      }
    }

    /// <summary>
    /// Checks if the given passcode is valid for this pkg
    /// </summary>
    /// <param name="passcode"></param>
    /// <returns>True if the passcode is correct</returns>
    public bool CheckPasscode(string passcode)
    {
      if (passcode == null || passcode.Length != 32) return false;
      var dk0 = Crypto.ComputeKeys(Header.content_id, passcode, 0);
      var digest0 = Crypto.Sha256(dk0).Xor(dk0);
      return digest0.SequenceEqual(EntryKeys.Keys[0].digest);
    }

    public bool CheckDerivedKey(byte[] dk, int index)
    {
      if(index < 0 || index > 6)
      {
        throw new ArgumentException("Invalid derived key index: " + index);
      }
      if (dk == null || dk.Length != 32)
        return false;
      var digest = Crypto.Sha256(dk).Xor(dk);
      return digest.SequenceEqual(EntryKeys.Keys[index].digest);

    }

    public bool CheckEkpfs(byte[] dk1) => CheckDerivedKey(dk1, 1);

    string CreateMajorParamString()
    {
      var sfo = ParamSfo.ParamSfo;
      var majorParamString = "ATTRIBUTE" + sfo["ATTRIBUTE"];
      if (sfo["ATTRIBUTE2"] is SFO.Value v)
        majorParamString += "ATTRIBUTE2" + v;
      majorParamString += "CATEGORY" + sfo["CATEGORY"];
      majorParamString += "FORMAT" + sfo["FORMAT"];
      majorParamString += "PUBTOOLVER" + sfo["PUBTOOLVER"];
      return majorParamString;
    }

    internal byte[] ComputeContentDigest()
    {
      byte[] ContentDigest;
      var majorParamDigest = Crypto.Sha256(Encoding.ASCII.GetBytes(CreateMajorParamString()));
      using (var ms = new MemoryStream())
      {
        ms.Write(Encoding.ASCII.GetBytes(Header.content_id), 0, 36);
        ms.Write(new byte[12], 0, 12);
        ms.WriteInt32BE((int)Header.drm_type);
        ms.WriteInt32BE((int)Header.content_type);

        if (Header.content_type == ContentType.AC
          || Header.content_type == ContentType.GD
          || Header.content_flags.HasFlag(ContentFlags.GD_AC))
        {
          ms.Write(Header.pfs_image_digest, 0, 32);
        }
        ms.Write(majorParamDigest, 0, 32);
        ContentDigest = Crypto.Sha256(ms);
      }
      return ContentDigest;
    }

    internal byte[] ComputeHeaderDigest()
    {
      byte[] headerDigest;
      using (var ms = new MemoryStream())
      {
        new PkgWriter(ms).WriteHeader(Header);
        using (var hash = SHA256.Create())
        {
          ms.Position = 0;
          hash.TransformBlock(ms.ReadBytes(64), 0, 64, null, 0);
          ms.Position = 0x400;
          hash.TransformFinalBlock(ms.ReadBytes(128), 0, 128);
          headerDigest = hash.Hash;
        }
      }
      return headerDigest;
    }

    internal byte[] ComputeMajorParamDigest()
    {
      return Crypto.Sha256(Encoding.ASCII.GetBytes(CreateMajorParamString()));
    }

    /// <summary>
    /// Calculates the digests for the GeneralDigests entry.
    /// Preconditions: the following are set
    ///   - ParamSfo
    ///   - Content ID
    ///   - drm_type
    ///   - content_type
    ///   - pfs_image_digest (if there is a pfs image)
    ///   - all header values from 0x0 to 0x40
    ///   - all header values from 0x400 to 0x480
    /// </summary>
    public Dictionary<GeneralDigest, byte[]> CalcGeneralDigests()
    {
      var digests = new Dictionary<GeneralDigest, byte[]>
      {
        { GeneralDigest.HeaderDigest, ComputeHeaderDigest() },
        { GeneralDigest.GameDigest, Header.pfs_image_digest },
        { GeneralDigest.ContentDigest, ComputeContentDigest() },
        { GeneralDigest.MajorParamDigest, ComputeMajorParamDigest() },
        { GeneralDigest.ParamDigest, Crypto.Sha256(ParamSfo.ParamSfo.Serialize()) },
      };
      if (Header.content_type == ContentType.AL)
      {
        digests.Remove(GeneralDigest.GameDigest);
      }
      return digests;
    }
  }



  public struct Header
  {
    public string CNTMagic;
    public PKGFlags flags;
    public uint unk_0x08;
    public uint unk_0x0C; /* 0xF */
    public uint entry_count;
    public ushort sc_entry_count;
    public ushort entry_count_2; /* same as entry_count */
    public uint entry_table_offset;
    public uint main_ent_data_size;
    public ulong body_offset;
    public ulong body_size;
    public string content_id; // Length = PKG_CONTENT_ID_SIZE
    public DrmType drm_type;
    public ContentType content_type;
    public ContentFlags content_flags;
    public uint promote_size;
    public uint version_date;
    public uint version_hash;
    public uint unk_0x88; /* for delta patches only? */
    public uint unk_0x8C; /* for delta patches only? */
    public uint unk_0x90; /* for delta patches only? */
    public uint unk_0x94; /* for delta patches only? */
    public IROTag iro_tag;
    public uint ekc_version; /* drm type version */
    public byte[] sc_entries1_hash;
    public byte[] sc_entries2_hash;
    public byte[] digest_table_hash;
    public byte[] body_digest;

    // TODO: i think these fields are actually members of element of container array
    public uint unk_0x400;
    public uint pfs_image_count;
    public ulong pfs_flags;
    public ulong pfs_image_offset;
    public ulong pfs_image_size;
    public ulong mount_image_offset;
    public ulong mount_image_size;
    public ulong package_size;
    public uint pfs_signed_size;
    public uint pfs_cache_size;
    public byte[] pfs_image_digest;
    public byte[] pfs_signed_digest;
    public ulong pfs_split_size_nth_0;
    public ulong pfs_split_size_nth_1;
  }
}
