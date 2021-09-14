using System;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;

namespace LibOrbisPkg.Util
{
  public static class Crypto
  {
    /// <summary>
    /// From FPKG code:
    /// a common function to generate a final key for PFS
    /// </summary>
    public static byte[] PfsGenCryptoKey(byte[] ekpfs, byte[] seed, uint index)
    {
      byte[] d = new byte[4 + seed.Length];
      Array.Copy(BitConverter.GetBytes(index), d, 4);
      Array.Copy(seed, 0, d, 4, seed.Length);
      using (var hmac = new HMACSHA256(ekpfs))
      {
        return hmac.ComputeHash(d);
      }
    }

    /// <summary>
    /// Generates a (tweak, data) key pair for XTS
    /// </summary>
    public static Tuple<byte[], byte[]> PfsGenEncKey(byte[] ekpfs, byte[] seed, bool newCrypt = false)
    {
      var encKey = PfsGenCryptoKey(newCrypt ? new HMACSHA256(ekpfs).ComputeHash(seed) : ekpfs, seed, 1);
      var dataKey = new byte[16];
      var tweakKey = new byte[16];
      Buffer.BlockCopy(encKey, 0, tweakKey, 0, 16);
      Buffer.BlockCopy(encKey, 16, dataKey, 0, 16);
      return Tuple.Create(tweakKey, dataKey);
    }

    /// <summary>
    /// From FPKG code:
    /// asigning key generator based on EKPFS and PFS header seed
    /// </summary>
    public static byte[] PfsGenSignKey(byte[] ekpfs, byte[] seed)
    {
      return PfsGenCryptoKey(ekpfs, seed, 2);
    }

    /// <summary>
    /// From FPKG Code (sceSblPfsSetKeys): Turns the EEKPfs to an EKPfs
    /// </summary>
    public static byte[] DecryptEEKPfs(byte[] eekpfs, RSAKeyset keyset)
    {
      var @params = new RSAParameters
      {
        D = keyset.PrivateExponent,
        DP = keyset.Exponent1,
        DQ = keyset.Exponent2,
        Exponent = keyset.PublicExponent,
        InverseQ = keyset.Coefficient,
        Modulus = keyset.Modulus,
        P = keyset.Prime1,
        Q = keyset.Prime2
      };
      using(var rsa = RSA.Create())
      {
        rsa.KeySize = 2048;
        rsa.ImportParameters(@params);
        return rsa.DecryptValue(eekpfs);
      }
    }

    /// <summary>
    /// Encrypts the given hash with the given public key (modulus)
    /// </summary>
    /// <param name="modulus"></param>
    /// <param name="hash"></param>
    /// <param name="extra"></param>
    /// <returns></returns>
    public static byte[] RSA2048EncryptKey(byte[] modulus, byte[] hash)
    {
      // 1. Seed MT PRNG with hash of key and input hash
      var buffer = new byte[256 + 32];
      Buffer.BlockCopy(modulus, 0, buffer, 0, 256);
      Buffer.BlockCopy(hash, 0, buffer, 256, 32);
      var final_hash = Sha256(Sha256(buffer));
      var final_hash_ints = new uint[8];
      for (int i = 0; i < 32; i += 4)
      {
        final_hash_ints[i / 4] = ((uint)final_hash[0 + i] << 24) |
                                  ((uint)final_hash[1 + i] << 16) |
                                  ((uint)final_hash[2 + i] << 8) |
                                  ((uint)final_hash[3 + i] << 0);
      }
      var mt = new MersenneTwister(final_hash_ints);

      // 2. Pad the RSA input (header hash) using the Mersenne Twister PRNG
      var sha_source = new MemoryStream(48);
      var padded_input = new byte[256];
      padded_input[0] = 0;
      padded_input[1] = 2;
      padded_input[223] = 0;
      Buffer.BlockCopy(hash, 0, padded_input, 224, 32);
      for (int k = 2; k < 223;)
      {
        sha_source.Position = 0;
        for (int i = 0; i < 12; i++)
        {
          sha_source.WriteUInt32BE(mt.Int32());
        }
        var random = Sha256(sha_source);
        foreach (var r in random)
        {
          if (k >= 223)
            break;
          if (r != 0)
            padded_input[k++] = r;
        }
      }

      // 3. Encrypt the padded input with RSA 2048 (modular exponentiation)
      return RSA2048Encrypt(padded_input, modulus);
    }

    /// <summary>
    /// Sign the given SHA-256 hash with PKCS1 padding
    /// </summary>
    /// <param name="sha256Hash">Hash</param>
    /// <param name="keyset">Keys to use</param>
    /// <returns>RSA 2048 signature of the hash</returns>
    public static byte[] RSA2048SignSha256(byte[] sha256Hash, RSAKeyset keyset)
    {
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
      rsa.ImportParameters(new RSAParameters
      {
        P = keyset.Prime1,
        Q = keyset.Prime2,
        Exponent = keyset.PublicExponent,
        Modulus = keyset.Modulus,
        DP = keyset.Exponent1,
        DQ = keyset.Exponent2,
        InverseQ = keyset.Coefficient,
        D = keyset.PrivateExponent
      });
      return rsa.SignHash(sha256Hash, CryptoConfig.MapNameToOID("SHA256"));
    }

    public static bool RSA2048VerifySha256(byte[] sha256Hash, byte[] signature, RSAKeyset keyset)
    {
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
      rsa.ImportParameters(new RSAParameters
      {
        P = keyset.Prime1,
        Q = keyset.Prime2,
        Exponent = keyset.PublicExponent,
        Modulus = keyset.Modulus,
        DP = keyset.Exponent1,
        DQ = keyset.Exponent2,
        InverseQ = keyset.Coefficient,
        D = keyset.PrivateExponent
      });
      return rsa.VerifyHash(sha256Hash, CryptoConfig.MapNameToOID("SHA256"), signature);
    }

    /// <summary>
    /// Encrypts the value with 2048 bit RSA.
    /// Accepts and returns Big-Endian values
    /// </summary>
    /// <param name="value"></param>
    /// <param name="mod"></param>
    /// <returns></returns>
    public static byte[] RSA2048Encrypt(byte[] value, byte[] mod, int exp = 65537)
    {
      var message = new BigInteger(value.Reverse().ToArray());
      var modulus = new BigInteger(mod.Reverse().Concat(new byte[] { 0 }).ToArray());
      var exponent = new BigInteger(exp);
      var leResult = BigInteger.ModPow(message, exponent, modulus).ToByteArray().Take(256);
      return leResult
        .Concat(Enumerable.Range(0, 256 - leResult.Count()).Select(x => (byte)0))
        .Reverse()
        .ToArray();
    }

    public static byte[] RSA2048Decrypt(byte[] ciphertext, RSAKeyset keyset)
    {
      RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
      rsa.ImportParameters(new RSAParameters
      {
        P = keyset.Prime1,
        Q = keyset.Prime2,
        Exponent = keyset.PublicExponent,
        Modulus = keyset.Modulus,
        DP = keyset.Exponent1,
        DQ = keyset.Exponent2,
        InverseQ = keyset.Coefficient,
        D = keyset.PrivateExponent
      });
      return rsa.Decrypt(ciphertext, false);
    }

    // TODO
    public static int AesCbcCfb128Encrypt(byte[] @out, byte[] @in, int size, byte[] key, byte[] iv)
    {
      var cipher = new AesManaged
      {
        Mode = CipherMode.CBC,
        KeySize = 128,
        Key = key,
        IV = iv,
        Padding = PaddingMode.None,
        BlockSize = 128,
      };
      var tmp = new byte[size];
      using (var pt_stream = new MemoryStream(@in))
      using (var ct_stream = new MemoryStream(tmp))
      using (var dec = cipher.CreateEncryptor(key, iv))
      using (var s = new CryptoStream(ct_stream, dec, CryptoStreamMode.Write))
      {
        pt_stream.CopyTo(s);
      }
      Buffer.BlockCopy(tmp, 0, @out, 0, tmp.Length);
      return 0;
    }
    public static int AesCbcCfb128Decrypt(byte[] @out, byte[] @in, int size, byte[] key, byte[] iv)
    {
      var cipher = new AesManaged
      {
        Mode = CipherMode.CBC,
        KeySize = 128,
        Key = key,
        IV = iv,
        Padding = PaddingMode.None,
        BlockSize = 128,
      };
      var tmp = new byte[size];
      using (var ct_stream = new MemoryStream(@in))
      using (var pt_stream = new MemoryStream(tmp))
      using (var dec = cipher.CreateDecryptor(key, iv))
      using (var s = new CryptoStream(ct_stream, dec, CryptoStreamMode.Read))
      {
        s.CopyTo(pt_stream);
      }
      Buffer.BlockCopy(tmp, 0, @out, 0, tmp.Length);
      return 0;
    }

    /// <summary>
    /// Computes the SHA256 hash of the given data.
    /// </summary>
    public static byte[] Sha256(byte[] data) => SHA256.Create().ComputeHash(data);
    public static byte[] Sha256(Stream data)
    {
      data.Position = 0;
      return SHA256.Create().ComputeHash(data);
    }
    /// <summary>
    /// Computes the SHA256 hash of the data in the stream between (start) and (start+length)
    /// </summary>
    public static byte[] Sha256(Stream data, long start, long length)
    {
      using (var s = new SubStream(data, start, length))
      {
        return Sha256(s);
      }
    }

    public static byte[] HmacSha256(byte[] key, byte[] data)
      => new HMACSHA256(key).ComputeHash(data);
    public static byte[] HmacSha256(byte[] key, Stream data)
    {
      data.Position = 0;
      return new HMACSHA256(key).ComputeHash(data);
    }
    public static byte[] HmacSha256(byte[] key, Stream data, long start, long length)
    {
      using (var s = new SubStream(data, start, length))
      {
        return HmacSha256(key, s);
      }
    }

    /// <summary>
    /// Computes keys for the package.
    /// The key is the result of a SHA256 hash of the concatenation of:
    ///  - The SHA256 hash of the index (4 bytes big-endian)
    ///  - The SHA256 hash of the Contend ID (36 bytes padded to 48 with nulls)
    ///  - The passcode
    /// The EKPFS is Index 1. 
    /// </summary>
    public static byte[] ComputeKeys(string ContentId, string Passcode, uint Index)
    {
      if (ContentId.Length != 36)
        throw new Exception("Content ID must be 36 characters long");
      if (Passcode.Length != 32)
        throw new Exception("Passcode must be 32 characters long");
            
      byte[] data = new byte[96];
      Buffer.BlockCopy(Sha256(BitConverter.GetBytes(Index).Reverse().ToArray()), 0, data, 0, 32);
      Buffer.BlockCopy(Sha256(Encoding.ASCII.GetBytes(ContentId.PadRight(48, '\0'))), 0, data, 32, 32);
      Buffer.BlockCopy(Encoding.ASCII.GetBytes(Passcode), 0, data, 64, 32);

      return Sha256(data);
    }

    public static byte[] CreateKeystone(string passcode)
    {
      var keystoneHeader = "6b657973746f6e65020001000000000000000000000000000000000000000000".FromHexCompact();
      var fingerprint = HmacSha256(Keys.keystone_hmac_key, Encoding.ASCII.GetBytes(passcode));
      var final = HmacSha256(Keys.keystone_mac_data, keystoneHeader.Concat(fingerprint).ToArray());
      return keystoneHeader.Concat(fingerprint).Concat(final).ToArray();
    }

    /// <summary>
    /// XORs a with b and stores the result in a
    /// </summary>
    public static byte[] Xor(this byte[] a, byte[] b)
    {
      for(var i = 0; i < a.Length; i++)
      {
        a[i] ^= b[i];
      }
      return a;
    }
    public static string AsHexCompact(this byte[] k)
    {
      StringBuilder sb = new StringBuilder(k.Length * 2);
      foreach(var b in k)
      {
        sb.AppendFormat("{0:X2}", b);
      }
      return sb.ToString();
    }

    public static byte[] FromHexCompact(this string k)
    {
      var b = new List<byte>();
      var key = k.Replace(" ", "");
      for (var x = 0; x < key.Length - 1;)
      {
        byte result = 0;
        int sub;
        for(var i = 0; i < 2; i++, x++)
        {
          result <<= 4;
          if (key[x] >= '0' && key[x] <= '9')
            sub = '0';
          else if (key[x] >= 'a' && key[x] <= 'f')
            sub = 'a' - 10;
          else if (key[x] >= 'A' && key[x] <= 'F')
            sub = 'A' - 10;
          else
            continue;
          result |= (byte)(key[x] - sub);
        }
        b.Add(result);
      }
      return b.ToArray();
    }

    // System.String.GetHashCode(): http://referencesource.microsoft.com/#mscorlib/system/string.cs,0a17bbac4851d0d4
    // System.Web.Util.StringUtil.GetStringHashCode(System.String): http://referencesource.microsoft.com/#System.Web/Util/StringUtil.cs,c97063570b4e791a
    public static int CombineHashCodes(params int[] hashCodes)
    {
      int hash1 = (5381 << 16) + 5381;
      int hash2 = hash1;

      int i = 0;
      foreach (var hashCode in hashCodes)
      {
        if (i % 2 == 0)
          hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ hashCode;
        else
          hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ hashCode;

        ++i;
      }

      return hash1 + (hash2 * 1566083941);
    }
  }
}
