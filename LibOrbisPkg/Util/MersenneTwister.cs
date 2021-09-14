using System;

namespace LibOrbisPkg.Util
{
  /// <summary>
  /// Mersenne Twister PRNG
  /// </summary>
  public class MersenneTwister
  {
    public const int N = 624;
    const uint M = 397;
    const uint DefaultSeed = 0x12BD6AA;
    const uint MatrixA = 0x9908b0df;
    const uint UpperMask = 0x80000000;
    const uint LowerMask = 0x7fffffff;
    const uint Constant1 = 0x6C078965;
    const uint Constant2 = 0x19660D;
    const uint Constant3 = 0x5D588B65;
    const uint Constant4 = 0x9d2c5680;
    const uint Constant5 = 0xefc60000;

    public uint[] mt = new uint[N];
    uint Mask(int val) => ~((~0u) << val);
    uint TwoToThe(int val) => 1u << val;

    public MersenneTwister(uint seed = DefaultSeed)
    {
      mt[0] = seed;
      for (mti = 1; mti < N; mti++)
      {
        mt[mti] = mti + Constant1 * (mt[mti - 1] ^ (mt[mti - 1] >> 30));
      }
    }

    public MersenneTwister(uint[] seed) : this(DefaultSeed)
    {
      uint stateIdx = 1, seedIdx = 0;
      for (int length = Math.Max(N, seed.Length); length > 0; length--)
      {
        mt[stateIdx] = (mt[stateIdx] ^ ((mt[stateIdx - 1] ^ (mt[stateIdx - 1] >> 30)) * Constant2)) + seed[seedIdx] + seedIdx;
        stateIdx++;
        seedIdx++;
        if (stateIdx >= N) { mt[0] = mt[N - 1]; stateIdx = 1; }
        if (seedIdx >= seed.Length) seedIdx = 0;
      }
      for (int length = 0; length < N - 1; length++)
      {
        mt[stateIdx] = (mt[stateIdx] ^ ((mt[stateIdx - 1] ^ (mt[stateIdx - 1] >> 30)) * Constant3)) - stateIdx;
        stateIdx++;
        if (stateIdx >= N) { mt[0] = mt[N - 1]; stateIdx = 1; }
      }
      mt[0] = (1u << 31); /* MSB is 1; assuring non-zero initial array */
    }

    uint mti = 0;
    public uint Int32()
    {
      var mag01 = new uint[] { 0, MatrixA };
      uint y;
      if (mti >= N)
      {
        /* generate N words all at once */
        uint kk;
        for (kk = 0; kk < N - M; kk++)
        {
          y = (mt[kk] & UpperMask) | (mt[kk + 1] & LowerMask);
          mt[kk] = mt[kk + M] ^ ((y >> 1) & Mask(31)) ^ mag01[y & 1];
        }
        for (; kk < N - 1; kk++)
        {
          y = (mt[kk] & UpperMask) | (mt[kk + 1] & LowerMask);
          mt[kk] = mt[kk + M - N] ^ ((y >> 1) & Mask(31)) ^ mag01[y & 1];
        }
        y = (mt[N - 1] & UpperMask) | (mt[0] & LowerMask);
        mt[N - 1] = mt[M - 1] ^ ((y >> 1) & Mask(31)) ^ mag01[y & 1];
        mti = 0;
      }

      y = mt[mti++];
      /* Tempering */
      y ^= (y >> 11) & Mask(21);
      y ^= (y << 7) & Constant4;
      y ^= (y << 15) & Constant5;
      y ^= (y >> 18) & Mask(14);
      return y;
    }

    public uint Int31() => Int32() & Mask(31);
  }
}
