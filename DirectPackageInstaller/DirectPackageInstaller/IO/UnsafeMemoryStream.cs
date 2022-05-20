using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DirectPackageInstaller.IO
{
    public unsafe class UnsafeMemoryStream : Stream
    {

        public bool Disposed { get; private set; } = false;

        public UnsafeMemoryStream(long Size)
        {
            long MaxValue = IntPtr.Size == 4 ? int.MaxValue : long.MaxValue;

            if (Size > MaxValue)
                throw new InternalBufferOverflowException();
            
            BasePointer = CurrentPointer = (byte*)Marshal.AllocHGlobal(new IntPtr(Size)).ToPointer();

            lock (InstanceCount)
            {
                if (!InstanceCount.ContainsKey(new IntPtr(BasePointer)))
                    InstanceCount[new IntPtr(BasePointer)] = 0;

                InstanceCount[new IntPtr(BasePointer)]++;
            }
            

            this.Size = Size;
        }

        public UnsafeMemoryStream(byte* Pointer, long Size)
        {
            long MaxValue = IntPtr.Size == 4 ? int.MaxValue : long.MaxValue;

            if (Size > MaxValue)
                throw new InternalBufferOverflowException();

            lock (InstanceCount)
            {
                if (!InstanceCount.ContainsKey(new IntPtr(Pointer)))
                    InstanceCount[new IntPtr(Pointer)] = 0;

                InstanceCount[new IntPtr(Pointer)]++;
            }
            

            BasePointer = CurrentPointer = Pointer;
            this.Size = Size;
        }

        private byte* CurrentPointer;
        public byte* BasePointer { get; private set; }
        public long Size { get; private set; }


        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => Size;

        public override long Position { get => (long)CurrentPointer - (long)BasePointer; set => CurrentPointer = BasePointer + value; }

        public override void Flush() { }
        
        static SemaphoreSlim innerLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim outerLock = new SemaphoreSlim(1, 1);
        static SemaphoreSlim ReadWriteLocker = new SemaphoreSlim(1, 1);
        static int ReadWriteCount = 0;
        
        static void MasterReadWriteLock()
        {
            ReadWriteLocker.Wait();
            outerLock.Wait();

            if (ReadWriteCount == 0)
                innerLock.Wait();

            ReadWriteCount++;

            outerLock.Release();
            ReadWriteLocker.Release();
        }

        static void MasterReadWriteUnlock()
        {
            ReadWriteLocker.Wait();
            ReadWriteCount--;

            if (ReadWriteCount == 0)
                innerLock.Release();

            ReadWriteLocker.Release();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            MasterReadWriteLock();
            try
            {
                if (Disposed || !InstanceCount.ContainsKey(new IntPtr(BasePointer)))
                    throw new ObjectDisposedException("UnsafeMemoryStream");

                if (Position + count > Length)
                    count = (int) (Length - Position);

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

                    uint* dwBuffer = (uint*) (pBuffer + Assert);
                    uint* dwPointer = (uint*) CurrentPointer;

                    for (int i = 0; i < count; i += 4, Readed += 4)
                        *dwBuffer++ = *dwPointer++;

                    CurrentPointer = (byte*) dwPointer;
                }

                return Readed;
            }
            finally
            {
                MasterReadWriteUnlock();
            }
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
            MasterReadWriteLock();
            
            try
            {
                if (Disposed)
                    throw new ObjectDisposedException("UnsafeMemoryStream");

                if (Position + count > Length)
                    count = (int) (Length - Position);

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

                    uint* dwBuffer = (uint*) (pBuffer + Assert);
                    uint* dwPointer = (uint*) CurrentPointer;

                    for (int i = 0; i < count; i += 4)
                        *dwPointer++ = *dwBuffer++;

                    CurrentPointer = (byte*) dwPointer;
                }
            }
            finally
            {
                MasterReadWriteUnlock();
            }
        }

        public static bool PointerDisposed(IntPtr Pointer)
        {
            if (InstanceCount.ContainsKey(Pointer) && InstanceCount[Pointer] > 0)
                return false;
            return true;
        }

        private static Dictionary<IntPtr, int> InstanceCount = new Dictionary<IntPtr, int>();

        protected override void Dispose(bool Disposing)
        {
            outerLock.Wait();
            innerLock.Wait();
            try
            {
                if (Disposed)
                    return;

                Disposed = true;

                if (InstanceCount.ContainsKey(new IntPtr(BasePointer)))
                {
                    if (InstanceCount[new IntPtr(BasePointer)] > 0)
                    {
                        base.Dispose(Disposing);

                        InstanceCount[new IntPtr(BasePointer)]--;

                        if (InstanceCount[new IntPtr(BasePointer)] > 0)
                            return;
                    }
                }
                else
                {
                    base.Dispose(Disposing);
                    return;
                }

                Marshal.FreeHGlobal(new IntPtr(BasePointer));
                InstanceCount.Remove(new IntPtr(BasePointer));

                base.Dispose(Disposing);
            }
            finally
            {
                outerLock.Release();
                innerLock.Release();
            }
        }
    }
}
