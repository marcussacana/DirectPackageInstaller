using System;
using System.Security.Cryptography;

namespace LibOrbisPkg.Util
{
  public class XtsBlockTransform
  {
    // Used on the plaintext XORed with the encrypted sector number
    private SymmetricAlgorithm cipher;
    // Used to encrypt the tweak
    private SymmetricAlgorithm tweakCipher;

    private byte[] tweak = new byte[16];
    private byte[] xor = new byte[16];
    private byte[] xor2 = new byte[16];
    private byte[] encryptedTweak = new byte[16];

    /// <summary>
    /// Creates an AES-XTS-128 transformer
    /// </summary>
    public XtsBlockTransform(byte[] dataKey, byte[] tweakKey)
    {
      cipher = new AesManaged
      {
        Mode = CipherMode.ECB,
        KeySize = 128,
        Key = dataKey,
        Padding = PaddingMode.None,
        BlockSize = 128,
      };
      tweakCipher = new AesManaged
      {
        Mode = CipherMode.ECB,
        KeySize = 128,
        Key = tweakKey,
        Padding = PaddingMode.None,
        BlockSize = 128,
      };
    }

    public void EncryptSector(byte[] sector, ulong sectorNum) => CryptSector(sector, sectorNum, true);
    public void DecryptSector(byte[] sector, ulong sectorNum) => CryptSector(sector, sectorNum, false);

    /// <summary>
    /// Encrypts or decrypts the given sector with XEX.
    /// </summary>
    /// <param name="sector">Sector plain/ciphertext</param>
    /// <param name="sectorNum">Sector index number</param>
    /// <param name="encrypt">If this is set to true, encrypt the sector</param>
    public void CryptSector(byte[] sector, ulong sectorNum, bool encrypt = false)
    {
      // Reset tweak to sector number
      Buffer.BlockCopy(BitConverter.GetBytes(sectorNum), 0, tweak, 0, 8);
      for (int x = 8; x < 16; x++)
        tweak[x] = 0;
      using (var tweakEncryptor = tweakCipher.CreateEncryptor())
      using (var cryptor = encrypt ? cipher.CreateEncryptor() : cipher.CreateDecryptor())
      {
        tweakEncryptor.TransformBlock(tweak, 0, 16, encryptedTweak, 0);
        for (int destOffset = 0; destOffset < sector.Length; destOffset += 16)
        {
          for (var x = 0; x < 16; x++)
          {
            xor[x] = (byte)(sector[x + destOffset] ^ encryptedTweak[x]);
          }
          cryptor.TransformBlock(xor, 0, 16, xor, 0);
          for (var x = 0; x < 16; x++)
          {
            sector[x + destOffset] = (byte)(xor[x] ^ encryptedTweak[x]);
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
  }
}
