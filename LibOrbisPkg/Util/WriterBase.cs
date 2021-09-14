using System.IO;
using System.Runtime.InteropServices;

namespace LibOrbisPkg.Util
{
  public class WriterBase
  {
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    private unsafe struct storage
    {
      [FieldOffset(0)]
      public byte u8;
      [FieldOffset(0)]
      public sbyte s8;
      [FieldOffset(0)]
      public ushort u16;
      [FieldOffset(0)]
      public short s16;
      [FieldOffset(0)]
      public uint u32;
      [FieldOffset(0)]
      public int s32;
      [FieldOffset(0)]
      public ulong u64;
      [FieldOffset(0)]
      public long s64;
      [FieldOffset(0)]
      public float f32;
      [FieldOffset(0)]
      public double f64;
      [FieldOffset(0)]
      public fixed byte buf[8];
    }

    private storage buffer;
    protected Stream s;
    protected bool flipEndian;
    protected WriterBase(bool flipEndian, Stream stream)
    {
      s = stream;
      this.flipEndian = flipEndian;
    }
    private void WriteEndian(int count)
    {
      unsafe
      {
        if (flipEndian)
          for (int i = count - 1; i >= 0; i--) s.WriteByte(buffer.buf[i]);
        else
          for (int i = 0; i < count; i++) s.WriteByte(buffer.buf[i]);
      }
    }
    protected void Write(byte b) => s.WriteByte(b);
    protected void Write(sbyte sb) => Write((byte)sb);
    protected void Write(ushort u)
    {
      buffer.u16 = u;
      WriteEndian(2);
    }
    protected void Write(short u)
    {
      buffer.s16 = u;
      WriteEndian(2);
    }
    protected void Write(uint u)
    {
      buffer.u32 = u;
      WriteEndian(4);
    }
    protected void Write(int u)
    {
      buffer.s32 = u;
      WriteEndian(4);
    }
    protected void Write(ulong u)
    {
      buffer.u64 = u;
      WriteEndian(8);
    }
    protected void Write(long u)
    {
      buffer.s64 = u;
      WriteEndian(8);
    }
    protected void Write(byte[] b)
    {
      if (b == null)
        return;
      s.Write(b, 0, b.Length);
    }
    //protected unsafe void Write(byte* b, int count)
    //{
    //  for (var i = 0; i < count; i++)
    //  {
    //    s.WriteByte(b[i]);
    //  }
    //}
  }
}
