using System;
using System.IO;

namespace DirectPackageInstaller.IO
{
    public class MergedStream : Stream
    {
        public MergedStream(Stream[] Streams, long[] Sizes)
        {
            this.Streams = Streams;
            PartSize = Sizes;
            PartOffset = new long[Streams.Length];

            TotalLen = 0;
            for (int i = 0; i < Streams.Length; i++)
            {
                PartOffset[i] = i > 0 ? PartOffset[i - 1] + PartSize[i - 1] : 0;

                TotalLen += PartSize[i];
            }

            SIndex = 0;
        }

        public MergedStream(params Stream[] Streams)
        {
            this.Streams = Streams;
            PartSize = new long[Streams.Length];
            PartOffset = new long[Streams.Length];

            TotalLen = 0;
            for (int i = 0; i < Streams.Length; i++) {
                PartSize[i] = Streams[i].Length;
                PartOffset[i] = i > 0 ? PartOffset[i - 1] + PartSize[i - 1] : 0;

                TotalLen += PartSize[i];
            }

            SIndex = 0;
        }

        ~MergedStream()
        {
            Dispose();
        }
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        long TotalLen = -1;
        public override long Length => TotalLen;

        int SIndex;

        long[] PartSize;
        long[] PartOffset;
        Stream[] Streams;
        long CurrentPartOffset => PartOffset[SIndex];
        long CurrentPartMaxOffset => PartOffset[SIndex] + PartSize[SIndex];
        Stream CurrentStream => Streams[SIndex];

        public override long Position
        {
            get => CurrentPartOffset + CurrentStream.Position;
            set
            {
                for (int i = 0; i < Streams.Length; i++)
                {
                    if (value >= PartOffset[i] && value < PartOffset[i] + PartSize[i])
                    {
                        SIndex = i;
                        CurrentStream.Position = value - PartOffset[i];
                        break;
                    }
                }
            }
        }

        public override void Flush()
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int Readed = CurrentStream.Read(buffer, offset, count);
            if (Position >= CurrentPartMaxOffset) {
                if (SIndex + 1 < Streams.Length)
                {
                    SIndex++;

                    if (CurrentStream.Position != 0)
                        CurrentStream.Position = 0;

                    if (Readed < count)
                        return Readed + Read(buffer, offset + Readed, count - Readed);
                }
            }
            return Readed;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;

                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            foreach (var Stream in Streams)
            {
                Stream?.Close();
            }
        }

        public new void Dispose()
        {
            foreach (var Stream in Streams)
            {
                try
                {
                    Stream?.Close();
                    Stream?.Dispose();
                }
                catch { }
            }
        }
    }
}
