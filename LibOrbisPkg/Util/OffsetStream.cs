using System.IO;

namespace LibOrbisPkg.Util
{
  /// <summary>
  /// A stream that acts as a window to another stream at the given offset.
  /// Seek operations will be relative to the offset. Size operations will
  /// pretend any part of the stream previous doesn't exist.
  /// </summary>
  public class OffsetStream : Stream
  {
    /// <summary>
    /// Creates an offset stream. Does not own the parent stream.
    /// </summary>
    /// <param name="src"></param>
    /// <param name="offset"></param>
    public OffsetStream(Stream s, long offset)
    {
      src = s;
      if(src.Length < offset)
      {
        src.SetLength(offset);
      }
      this.offset = offset;
    }
    private Stream src;
    private long offset;

    public override bool CanRead => src.CanRead;

    public override bool CanSeek => src.CanSeek;

    public override bool CanWrite => src.CanWrite;

    public override long Length => src.Length - offset;

    public override long Position { get => src.Position - offset; set => src.Position = value + offset; }

    public override void Flush() => src.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
      return src.Read(buffer, offset, count);
    }

    public override long Seek(long where, SeekOrigin origin)
    {
      switch (origin)
      {
        case SeekOrigin.Begin:
          src.Seek(where + offset, origin);
          break;
        case SeekOrigin.Current:
          src.Seek(where, origin);
          break;
        case SeekOrigin.End:
          src.Seek(where, origin);
          break;
        default:
          break;
      }
      return Position;
    }

    public override void SetLength(long value)
    {
      src.SetLength(value + offset);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
      src.Write(buffer, offset, count);
    }
  }
}
