using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;

namespace DirectPackageInstaller
{
    
    public class ReadSeekableStream : Stream, IDisposable
    {
        private readonly Stream Buffer;
        public readonly Stream InputStream;

        public string TempFile { get; private set; } = TempHelper.GetTempFile(null);
        public ReadSeekableStream(Stream Input, string TempFile)
        {

            if (!Input.CanRead)
                throw new Exception("Provided stream " + Input + " is not readable");

            InputStream = Input;

            Buffer = new FileStream(this.TempFile, FileMode.OpenOrCreate, FileSystemRights.FullControl, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
        }
        public ReadSeekableStream(Stream Input)
        {
            if (!Input.CanRead)
                throw new Exception("Provided stream " + Input + " is not readable");

            InputStream = Input;

            Buffer = new FileStream(TempFile, FileMode.OpenOrCreate, FileSystemRights.FullControl, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (ReadPosition < Buffer.Length) {
                Buffer.Position = ReadPosition;
                int bReaded = Buffer.Read(buffer, offset, count);
                ReadPosition += bReaded;

                if (InputPosition < ReadPosition) {
                    long MustSkip = ReadPosition - InputPosition;

                    if (InputStream.CanSeek)
                        InputStream.Seek(MustSkip, SeekOrigin.Current);
                    else
                    {
                        int sReaded;
                        byte[] buff = new byte[1024 * 8];
                        do
                        {
                            sReaded = InputStream.Read(buff, 0, (int)(MustSkip > buff.Length ? buff.Length : MustSkip));
                            MustSkip -= sReaded;
                        } while (sReaded > 0);
                    }
                }
                return bReaded;
            }

            if (ReadPosition > InputPosition) {
                long Skiped = ReadPosition - InputPosition;
                Buffer.Seek(0, SeekOrigin.End);
                if (Buffer.Position != InputPosition)
                    System.Diagnostics.Debugger.Break();
                Skiped = Copy(InputStream, Buffer, Skiped);
                InputPosition += Skiped;
            }

            if (ReadPosition != InputPosition)
                throw new Exception("Bad Buffer Offset");
            
            int Readed = InputStream.Read(buffer, offset, count);
            Buffer.Seek(0, SeekOrigin.End);
            if (Buffer.Position != InputPosition)
                System.Diagnostics.Debugger.Break();
            Buffer.Write(buffer, offset, Readed);
            Buffer.Flush();

            ReadPosition += Readed;
            InputPosition += Readed;

            return Readed;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long AbsoluteOffset = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => ReadPosition + offset,
                SeekOrigin.End => InputStream.Length + offset,
                _ => throw new Exception("Invalid Seek Origin")
            };

            return ReadPosition = AbsoluteOffset;
        }

        long Copy(Stream From, Stream To, long Length) {

            byte[] Buffer = new byte[1024 * 100];
            long TotalReaded = 0;
            int Readed;
            do
            {
                long Reaming = Length - TotalReaded;
                Readed = From.Read(Buffer, 0, Reaming > Buffer.Length ? Buffer.Length : (int)Reaming);
                To.Write(Buffer, 0, Readed);
                TotalReaded += Readed;
            } while (Readed > 0);

            To.Flush();
            return TotalReaded;
        }

        long ReadPosition = 0;
        long InputPosition = 0;
        public override long Position
        {
            get { return ReadPosition;  }
            set { ReadPosition = value; }
        }

        public override void Close()
        {
            Buffer?.Close();
            Buffer?.Dispose();

            if (File.Exists(TempFile))
                File.Delete(TempFile);

            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                InputStream.Close();
            
            Buffer?.Close();
            Buffer?.Dispose();

            if (File.Exists(TempFile))
                File.Delete(TempFile);
            
            base.Dispose(disposing);
        }

        public override bool CanTimeout { get { return InputStream.CanTimeout; } }
        public override bool CanWrite { get { return InputStream.CanWrite; } }
        public override long Length { get { return InputStream.Length; } }
        public override void SetLength(long value) { InputStream.SetLength(value); }
        public override void Write(byte[] buffer, int offset, int count) { InputStream.Write(buffer, offset, count); }
        public override void Flush() { InputStream.Flush(); }   
    }
}
