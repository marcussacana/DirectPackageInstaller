using System;
using System.IO;
using System.Runtime.InteropServices;

namespace DirectPackageInstaller.IO
{
    public unsafe class UnsafeMemoryStream : Stream
    {

        bool Disposed = false;

        public UnsafeMemoryStream(long Size)
        {
            long MaxValue = IntPtr.Size == 4 ? int.MaxValue : long.MaxValue;

            if (Size > MaxValue)
                throw new InternalBufferOverflowException();

            BasePointer = CurrentPointer = (byte*)Marshal.AllocHGlobal(new IntPtr(Size)).ToPointer();
            this.Size = Size;
        }

        public byte* CurrentPointer;
        public byte* BasePointer;
        public long Size;


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => Size;

        public override long Position { get => (long)CurrentPointer - (long)BasePointer; set => CurrentPointer = BasePointer + value; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (Disposed)
                throw new ObjectDisposedException("UnsafeMemoryStream");

            if (Position + count > Length)
                count = (int)(Length - Position);

            if (count + offset > buffer.Length)
                count = buffer.Length - offset;

            if (count < 0)
                count = 0;

            if (count == 0)
                return 0;

            int Readed = 0;

            fixed (byte* pBuffer = &buffer[offset])
            {
                int Assert = count % 4;

                for (int i = 0; i < Assert; i++)
                     pBuffer[i] = *CurrentPointer++;
                
                count -= Assert;
                Readed += Assert;

                uint* dwBuffer = (uint*)(pBuffer + Assert);
                uint* dwPointer = (uint*)CurrentPointer;

                for (int i = 0; i < count; i += 4, Readed += 4)
                    *dwBuffer++ = *dwPointer++;

                CurrentPointer = (byte*)dwPointer;
            }

            return Readed;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (offset < 0 || offset > Length)
                throw new Exception("Invalid Position");

            switch (origin)
            {
                case SeekOrigin.Begin:
                    CurrentPointer = BasePointer + offset;
                    break;
                case SeekOrigin.Current:
                    if (Position + offset > Length)
                        throw new Exception("Out of Range");
                    CurrentPointer += offset;
                    break;
                case SeekOrigin.End:
                    long Pos = Length - offset;
                    if (Pos < 0)
                        throw new Exception("Out of Range");
                    CurrentPointer = BasePointer + Pos;
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
            if (Disposed)
                throw new ObjectDisposedException("UnsafeMemoryStream");

            if (Position + count > Length)
                count = (int)(Length - Position);

            if (count + offset > buffer.Length)
                count = buffer.Length - offset;

            if (count < 0)
                count = 0;

            if (count == 0)
                return;

            fixed (byte* pBuffer = &buffer[offset])
            {
                int Assert = count % 4;

                for (int i = 0; i < Assert; i++)
                    *CurrentPointer++ = pBuffer[i];

                count -= Assert;

                uint* dwBuffer = (uint*)(pBuffer + Assert);
                uint* dwPointer = (uint*)CurrentPointer;

                for (int i = 0; i < count; i += 4)
                    *dwPointer++ = *dwBuffer++;

                CurrentPointer = (byte*)dwPointer;
            }
        }

        protected override void Dispose(bool Disposing)
        {
            if (Disposed)
                return;

            Disposed = true;
            Marshal.FreeHGlobal(new IntPtr(BasePointer));
            base.Dispose(Disposing);
        }
    }
}
