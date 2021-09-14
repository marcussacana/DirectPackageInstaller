using System;
using System.IO;
using System.Text;
using LibOrbisPkg.PKG;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.Rif
{
  public class LicenseDat : Entry
  {
    public LicenseDat() { }
    /// <summary>
    /// Constructs a signed debug license.dat
    /// </summary>
    public LicenseDat(string ContentId, ContentType contentType, byte[] EntitlementKey = null)
    {
      this.ContentId = ContentId;
      this.ContentType = contentType;
      this.SecretIv = new byte[16];
      this.Secret = new byte[144];

      var contentId = new byte[48];
      Buffer.BlockCopy(Encoding.ASCII.GetBytes(ContentId), 0, contentId, 0, 36);
      var tmp = Crypto.Sha256(contentId);
      Buffer.BlockCopy(tmp, 0, SecretIv, 0, 16);
      Buffer.BlockCopy(tmp, 16, Secret, 0, 16);
      if(EntitlementKey != null && EntitlementKey.Length == 16) Buffer.BlockCopy(EntitlementKey, 0, Secret, 0x70, 16);
      if(ContentType == ContentType.GD)
      {
        SkuFlag = 3; // this is needed according to ShellCore
      }
      EncryptSecretWithDebugKey();
      Sign();
    }

    public short Version = 1;
    public short Unknown = -1;
    public ulong PsnAccountId = 0;
    public long StartTime = 1364222275L;
    public long EndTime = long.MaxValue;
    public string ContentId;
    public LicenseType LicenseType = LicenseType.Debug_0;
    public DrmType DrmType = DrmType.PS4;
    public ContentType ContentType = ContentType.AC;
    public short SkuFlag = 0;
    public int Flags = 0;
    public int Unk_5C = 0;
    public int Unk_60 = 0;
    public int Unk_64 = 1;
    public int Unk_Flag = 0;
    public byte[] DiscKey = new byte[32];
    public byte[] SecretIv;
    public byte[] Secret;
    public byte[] Signature = new byte[256];

    public override EntryId Id => EntryId.LICENSE_DAT;

    public override uint Length => 0x400;

    public override string Name => null; // Not saved with a name in the PKG

    public void DecryptSecretWithDebugKey()
    {
      Crypto.AesCbcCfb128Decrypt(Secret, Secret, Secret.Length, Keys.rif_debug_key, SecretIv);
    }

    public void EncryptSecretWithDebugKey()
    {
      Crypto.AesCbcCfb128Encrypt(Secret, Secret, Secret.Length, Keys.rif_debug_key, SecretIv);
    }

    public void Sign()
    {
      using (var ms = new MemoryStream())
      {
        new LicenseDatWriter(ms).Write(this);
        var hash = Crypto.Sha256(ms, 0, 0x300);
        Signature = Crypto.RSA2048SignSha256(hash, RSAKeyset.DebugRifKeyset);
      }
    }

    public override void Write(Stream s)
    {
      new LicenseDatWriter(s).Write(this);
    }
  }

  public enum LicenseType
  {
    // Incomplete listing from psdevwiki
    KDS_1 = 0,
    KDS_2 = 1,
    KDS_3 = 2,
    Isolated_1 = 0x101,
    Isolated_2 = 0x302,
    Disc = 0x102,
    Debug_0 = 0x200,
    Debug_1 = 0x201,
    Debug_2 = 0x202,
    CEX = 0x303,
    Unknown = 0x304,
    DEX = 0x305
  }

  public class LicenseDatReader : ReaderBase
  {
    public LicenseDatReader(Stream stream) : base(true, stream) { }
    public LicenseDat Read()
    {
      if (Int() != 0x52494600)
        throw new Exception("License did not have expected RIF header");
      var license = new LicenseDat()
      {
        Version = Short(),
        Unknown = Short(),
        PsnAccountId = ULong(),
        StartTime = Long(),
        EndTime = Long(),
        ContentId = Encoding.ASCII.GetString(ReadBytes(48)).Substring(0, 36),
        LicenseType = (LicenseType)Short(),
        DrmType = (DrmType)Short(),
        ContentType = (ContentType)Short(),
        SkuFlag = Short(),
        Flags = Int(),
        Unk_5C = Int(),
        Unk_60 = Int(),
        Unk_64 = Int(),
        Unk_Flag = Int(),
      };
      s.Position += 468;
      license.DiscKey = ReadBytes(32);
      license.SecretIv = ReadBytes(16);
      license.Secret = ReadBytes(144);
      license.Signature = ReadBytes(256);
      return license;
    }
  }

  public class LicenseDatWriter : WriterBase
  {
    public LicenseDatWriter(Stream stream) : base(true, stream) { }

    public void Write(LicenseDat dat)
    {
      Write(0x52494600); // "RIF\0";
      Write(dat.Version);
      Write(dat.Unknown);
      Write(dat.PsnAccountId);
      Write(dat.StartTime);
      Write(dat.EndTime);
      Write(Encoding.ASCII.GetBytes(dat.ContentId));
      Write(new byte[12]);
      Write((short)dat.LicenseType);
      Write((short)dat.DrmType);
      Write((short)dat.ContentType);
      Write(dat.SkuFlag);
      Write(dat.Flags);
      Write(dat.Unk_5C);
      Write(dat.Unk_60);
      Write(dat.Unk_64);
      Write(dat.Unk_Flag);
      s.Position += 468;
      Write(dat.DiscKey);
      Write(dat.SecretIv);
      Write(dat.Secret);
      Write(dat.Signature);
    }
  }
}
