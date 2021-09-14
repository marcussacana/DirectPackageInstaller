using System;
using System.Security.Cryptography;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Provides XTS decryption on an IMemoryReader
  /// </summary>
  public class XtsDecryptReader : IMemoryReader
  {
    private byte[] dataKey;
    private byte[] tweakKey;
    /// <summary>
    /// Size of each XEX sector
    /// </summary>
    private uint sectorSize;
    /// <summary>
    /// Sector at and after which the encryption is active
    /// </summary>
    private uint cryptStartSector;
    private IMemoryReader reader;
    private static byte[] zeroes = new byte[16];

    /// <summary>
    /// Creates an AES-XTS-128 stream.
    /// Reads will decrypt data.
    /// </summary>
    public XtsDecryptReader(
      IMemoryReader r,
      byte[] dataKey,
      byte[] tweakKey, uint startSector = 16, uint sectorSize = 0x1000)
    {
      cryptStartSector = startSector;
      this.sectorSize = sectorSize;
      this.dataKey = dataKey;
      this.tweakKey = tweakKey;
      reader = r;
    }

    public static unsafe void DecryptSector(
      Ctx context,
      byte[] sector,
      ulong sectorNum)
    {
      byte[] tweak = context.tweak,
        encryptedTweak = context.encryptedTweak,
        xor = context.xor;

      // Reset tweak to sector number
      Buffer.BlockCopy(BitConverter.GetBytes(sectorNum), 0, tweak, 0, 8);
      Buffer.BlockCopy(zeroes, 0, tweak, 8, 8);
      using (var tweakEncryptor = context.tweakCipher.CreateEncryptor())
      using (var decryptor = context.cipher.CreateDecryptor())
      {
        tweakEncryptor.TransformBlock(tweak, 0, 16, encryptedTweak, 0);
        for (int plaintextOffset = 0; plaintextOffset < sector.Length; plaintextOffset += 16)
        {
          fixed(byte* xor_ = xor)
          fixed(byte* encryptedTweak_ = encryptedTweak)
          fixed(byte* sector_ = &sector[plaintextOffset])
          {
            *((ulong*)xor_) = *((ulong*)sector_) ^ *((ulong*)encryptedTweak_);
            *((ulong*)xor_+1) = *((ulong*)sector_+1) ^ *((ulong*)encryptedTweak_+1);
          }
          decryptor.TransformBlock(xor, 0, 16, xor, 0);
          fixed (byte* xor_ = xor)
          fixed (byte* encryptedTweak_ = encryptedTweak)
          fixed (byte* sector_ = &sector[plaintextOffset])
          {
            *((ulong*)sector_) = *((ulong*)xor_) ^ *((ulong*)encryptedTweak_);
            *((ulong*)sector_ + 1) = *((ulong*)xor_ + 1) ^ *((ulong*)encryptedTweak_ + 1);
          }
          // GF-Multiply Tweak
          int feedback = 0;
          for (int k = 0; k < 16; k++)
          {
            byte tmp = encryptedTweak[k];
            encryptedTweak[k] = (byte)(2 * encryptedTweak[k] | feedback);
            feedback = (tmp & 0x80) >> 7;
          }
          if (feedback != 0)
            encryptedTweak[0] ^= 0x87;
        }
      }
    }

    public class Ctx
    {
      public SymmetricAlgorithm cipher;
      public SymmetricAlgorithm tweakCipher;
      public byte[] tweak;
      public byte[] xor;
      public byte[] encryptedTweak;
    }

    /// <summary>
    /// Precondition: activeSector is set
    /// Postconditions:
    /// - sectorOffset is reset to 0
    /// - sectorBuf[] is filled with decrypted sector
    /// - position is updated
    /// </summary>
    private void ReadSectorBuffer(Ctx ctx, int currentSector, byte[] sectorBuf)
    {
      reader.Read(currentSector * sectorSize, sectorBuf, 0, (int)sectorSize);
      if (currentSector >= cryptStartSector)
        DecryptSector(ctx, sectorBuf, (ulong)currentSector);
    }

    private Ctx MakeCtx() => new Ctx
    {
      cipher = new AesManaged
      {
        Mode = CipherMode.ECB,
        KeySize = 128,
        Key = dataKey,
        Padding = PaddingMode.None,
        BlockSize = 128,
      },
      tweakCipher = new AesManaged
      {
        Mode = CipherMode.ECB,
        KeySize = 128,
        Key = tweakKey,
        Padding = PaddingMode.None,
        BlockSize = 128,
      },
      xor = new byte[16],
      encryptedTweak = new byte[16],
      tweak = new byte[16]
    };

    public void Read(long position, byte[] buffer, int offset, int count)
    {
      var ctx = MakeCtx();
      var sectorBuf = new byte[sectorSize];
      var currentSector = (int)(position / sectorSize);
      var offsetIntoSector = (int)(position - (sectorSize * currentSector));
      ReadSectorBuffer(ctx, currentSector, sectorBuf);
      int totalRead = 0;
      while (count > 0)
      {
        if (offsetIntoSector >= sectorSize)
        {
          currentSector++;
          ReadSectorBuffer(ctx, currentSector, sectorBuf);
          offsetIntoSector = 0;
        }
        int bufferedRead = Math.Min((int)sectorSize - offsetIntoSector, count);
        Buffer.BlockCopy(sectorBuf, offsetIntoSector, buffer, offset, bufferedRead);
        count -= bufferedRead;
        offset += bufferedRead;
        totalRead += bufferedRead;
        offsetIntoSector += bufferedRead;
        position += bufferedRead;
      }
    }

    public void Dispose()
    {
      // We don't own the IMemoryReader, so do nothing.
    }
  }
}
