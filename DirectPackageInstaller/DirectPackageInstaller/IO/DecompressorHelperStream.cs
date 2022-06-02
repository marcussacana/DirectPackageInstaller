using DirectPackageInstaller.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DirectPackageInstaller.IO
{
    public class DecompressorHelperStream : Stream
    {
        public DecompressTaskInfo? Info = null;

        public Stream Base;

        public readonly List<Stream> Instances = new List<Stream>();

        public Action<(DecompressorHelperStream This, long Readed)> OnRead;

        public DecompressorHelperStream(Stream Base, Action<(DecompressorHelperStream This, long Readed)> OnRead)
        {
            this.Base = Base;
            this.OnRead = OnRead;
        }

        ~DecompressorHelperStream()
        {
            Dispose();
        }

        public long TotalReaded = 0;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => Base.Length;

        public override long Position { get => Base.Position; set => Base.Position = value; }

        public override void Flush()
        {
            Base.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var Readed = Base.Read(buffer, offset, count);
            TotalReaded += Readed;
            OnRead?.Invoke((this, Readed));
            return Readed;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Base.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Base.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Base.Write(buffer, offset, count);
        }
    }
}
