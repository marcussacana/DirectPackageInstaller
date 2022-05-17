using DirectPackageInstaller.IO;
using System;
using System.IO;
using System.Runtime.InteropServices;

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

        public unsafe void MultipartHelper((DecompressorHelperStream This, long Readed) Args)
        {
            if (Args.This.Info == null)
                return;

            var DecompressInfo = Args.This.Info ?? default;

            bool Buffering = Args.This.Base is SegmentedStream;

            bool CantBuffer = !Buffering && Args.This.Base is FileHostStream && (Args.This.Base as FileHostStream).SingleConnection;
            
            if (CantBuffer || !App.Config.SegmentedDownload)
                return;

            if (Buffering)
            {
                SegmentedStream Strm = Args.This.Base as SegmentedStream;

                while (Strm.Position + 1024 * 1024 * 2 < Strm.ScanProgress && !Strm.Finished)
                     System.Threading.Tasks.Task.Delay(30).Wait();
            }

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

                    foreach (var Ptr in Args.This.UnmanagedPointers)
                        try { Marshal.FreeHGlobal(Ptr); } catch { }

                    Args.This.UnmanagedPointers.Clear();
                }

                var FileStream = Args.This.Base as FileHostStream;

                var Position = FileStream.Position;
                var Url = FileStream.PageUrl;

                var TempFile = TempHelper.GetTempFile(Url + DecompressInfo.EntryName + "PartBuffer");

                try
                {
                    Func<Stream> Buffer = null;

                    if (MemoryInfo.GetAvaiablePhysicalMemory() > (ulong)FileStream.Length + (1024ul * 1024 * 500))
                    {
                        try
                        {
                            var Stream = new UnsafeMemoryStream(FileStream.Length);

                            Buffer = () =>
                            {
                                var NewStream =  new UnsafeMemoryStream(Stream.BasePointer, FileStream.Length);           
                                Args.This.Instances.Add(NewStream);
                                
                                if (!Stream.Disposed)
                                    Stream.Dispose();
                                
                                return NewStream;
                            };

                            Args.This.Instances.Add(Stream);
                        }
                        catch { }
                    }

                    if (Buffer == null) {
                        Buffer = () =>
                        {
                            var Stream = new FileStream(TempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 1024 * 1024 * 2, FileOptions.RandomAccess | FileOptions.WriteThrough);
                            Args.This.Instances.Add(Stream);
                            return Stream;
                        };
                    }

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
