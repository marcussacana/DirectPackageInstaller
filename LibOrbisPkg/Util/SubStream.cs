using System;
using System.IO;

namespace LibOrbisPkg.Util
{
  public class SubStream : Stream
  {
    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length { get; }

    public override long Position
    {
      get
      {
        return position;
      }

      set
      {
        Seek(value, SeekOrigin.Begin);
      }
    }

    /// <summary>
    /// Creates a non-owning read-only window into a stream
    /// </summary>
    public SubStream(Stream s, long offset, long length)
    {
      this.parent = s;
      this.offset = offset;
      Length = length;
    }

    private Stream parent;
    private long offset;
    private long position;

    public override int Read(byte[] buffer, int offset, int count)
    {
      parent.Seek(this.offset + Position, SeekOrigin.Begin);
      if (count + Position > Length)
      {
        count = (int)(Length - Position);
      }
      int bytes_read = parent.Read(buffer, offset, count);
      position += bytes_read;
      return bytes_read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
      switch (origin)
      {
        case SeekOrigin.Begin:
          break;
        case SeekOrigin.Current:
          offset += position;
          break;
        case SeekOrigin.End:
          offset += Length;
          break;
      }
      if (offset > Length)
      {
        offset = Length;
      }
      else if (offset < 0)
      {
        offset = 0;
      }
      position = offset;
      return position;
    }

    #region Not Supported
    public override void Flush()
    {
      throw new NotSupportedException();
    }
    public override void SetLength(long value)
    {
      throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException();
    }
    #endregion
  }
}
