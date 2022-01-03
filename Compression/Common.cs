using DirectPackageInstaller.IO;
using System;
using System.IO;

namespace DirectPackageInstaller.Compression
{
    class Common
    {
        public static CompressionFormat DetectCompressionFormat(byte[] Magic)
        {
            if (BitConverter.ToUInt32(Magic, 0) == 0x544E437F)//PKG Header
                return CompressionFormat.None;
            if (BitConverter.ToUInt32(Magic, 0) == 0x21726152)
                return CompressionFormat.RAR;
            if (BitConverter.ToUInt32(Magic, 0) == 0xAFBC7A37)
                return CompressionFormat.SevenZip;
            return CompressionFormat.None;
        }

        public void MultipartHelper((DecompressorHelperStream This, long Readed) Args)
        {
            if (Args.This.Info == null)
                return;

            var DecompressInfo = Args.This.Info ?? default;

            bool Buffering = Args.This.Base is SegmentedStream;

            if (!Buffering && Args.This.TotalReaded > 1024 * 1024 * 2)
            {
                if (DecompressInfo.SafeInSegmentTranstion)
                    return;

                DecompressInfo.SafeInSegmentTranstion = true;

                foreach (var Part in DecompressInfo.PartsStream)
                {
                    if (!(Part.Base is SegmentedStream))
                        continue;

                    SegmentedStream Strm = Part.Base as SegmentedStream;
                    Strm.Flush();

                    var NewStrm = Strm.OpenSegment();
                    NewStrm.Position = Strm.Position;
                    Part.Base = NewStrm;

                    Strm.Close();
                }

                var FileStream = Args.This.Base as FileHostStream;

                var Position = FileStream.Position;
                var Url = FileStream.PageUrl;

                bool BufferToMemory = FileStream.Length < int.MaxValue && (ulong)FileStream.Length + (1024 * 1024 * 300) < MemoryInfo.GetAvaiablePhysicalMemory();

                //if (!BufferToMemory)
                //    return;

                var TempFile = TempHelper.GetTempFile(Url + DecompressInfo.EntryName + "PartBuffer");

                try
                {
                    Stream Buffer = null;

                    if (BufferToMemory)
                    {
                        try
                        {
                            Buffer = new MemoryStream();
                            Buffer.SetLength(FileStream.Length);
                        }
                        catch
                        {
                            BufferToMemory = false;
                        }
                    }

                    if (!BufferToMemory)
                    {
                        Buffer = new FileStream(TempFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite, 1024 * 1024 * 2, FileOptions.DeleteOnClose | FileOptions.RandomAccess | FileOptions.WriteThrough);
                        //Buffer = File.Open(TempFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite);
                        Buffer.SetLength(FileStream.Length);
                    }

                    if (Buffer == null)
                        throw new InternalBufferOverflowException();

                    var SegStream = new SegmentedStream(() => new FileHostStream(Url, 1024 * 1024), Buffer, 1024 * 1024, true);
                    SegStream.Position = Position;
                    Args.This.Base = SegStream;

                    FileStream?.Flush();
                    FileStream?.Close();
                }
                catch
                {
                    return;
                }
                finally
                {
                    DecompressInfo.SafeInSegmentTranstion = false;
                }
            }

        }
    }
}
