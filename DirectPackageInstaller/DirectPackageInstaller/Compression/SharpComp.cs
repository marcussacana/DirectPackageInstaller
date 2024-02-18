using DirectPackageInstaller.IO;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace DirectPackageInstaller.Compression
{
    class SharpComp
    {

        Common CompCommon = new Common();

        public Dictionary<string, DecompressTaskInfo> Tasks = new Dictionary<string, DecompressTaskInfo>();

        public string? CreateDecompressor(IArchive Decompressor, DecompressorHelperStream[] Volumes, string? EntryName)
        {
            IArchiveEntry PKG;
            var PKGs = Decompressor.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));

            if (PKGs.Count() == 1)
                PKG = PKGs.Single();
            else
                PKG = PKGs.Single(x => x.Key.EndsWith(EntryName, StringComparison.InvariantCultureIgnoreCase));

            for (int i = 0; i < Volumes.Length; i++)
                Volumes[i].OnRead = CompCommon.MultipartHelper;

            if (!StartDecompressor(PKG, Volumes))
                return null;

            return Path.GetFileName(PKG.Key);
        }

        private unsafe bool StartDecompressor(IArchiveEntry Entry, DecompressorHelperStream[] Inputs)
        {
            var TaskCompSrc = new TaskCompletionSource<bool>();
            using var BGWorker = new BackgroundWorker();
            BGWorker.DoWork += (sender, e) =>
            {
                var EntryName = Path.GetFileName(Entry.Key);
                if (Tasks.ContainsKey(EntryName)) {
                    TaskCompSrc.SetResult(true);
                    return;
                }
                
                var Input = Entry.OpenEntryStream();
                var TmpFile = TempHelper.GetTempFile(EntryName + "extract");
                
                if (File.Exists(TmpFile))
                    File.Delete(TmpFile);
                
                using Stream Output = File.Open(TmpFile, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);

                long TDecomp = 0;
                var InTrans = false;

                var TaskInfo = new DecompressTaskInfo() {
                    EntryName = EntryName,
                    TotalSize = Entry.Size,
                    Content = () => File.Open(TmpFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                    Running = true,
                    TotalDecompressed = &TDecomp,
                    InSegmentTranstion = &InTrans,
                    PartsStream = Inputs.ToArray(),
                    Error = null
                };

                bool First = true;
                foreach (var Strm in Inputs)
                {
                    Strm.Info = TaskInfo;
                    
                    if (!First && Strm.Base is PartialHttpStream strmBase)
                        strmBase.CloseConnection();
                    
                    First = false;
                }

                var TaskKey = Path.GetFileName(Entry.Key);
                Tasks[TaskKey] = TaskInfo;

                TaskCompSrc.SetResult(true);

                try
                {
                    int ReadTries = 0;
                    var Buffer = new byte[1024 * 1024 * 1];

                    int Readed;
                    do
                    {
                        Readed = Input.Read(Buffer, 0, Buffer.Length);
                        Output.Write(Buffer, 0, Readed);

                        *TaskInfo.TotalDecompressed += Readed;

                        if (Readed == 0 && Output.Length < Entry.Size && ReadTries++ < 3)
                        {
                            Readed = 1;
                            continue;
                        }

                    } while (Readed > 0);

                    Output.Flush();

                    *TaskInfo.TotalDecompressed = Output.Length;
                }
                catch (Exception ex)
                {
                    TaskInfo.Error = ex;
                }
                finally
                {
                    TaskInfo.Running = false;
                    Tasks[TaskKey] = TaskInfo;

                    for (int i = 0; i < Inputs.Length; i++)
                    {
                        ref var InBuffer = ref Inputs[i];
                        if (InBuffer.Base is SegmentedStream SegStream)
                        {
                            InBuffer.Base = SegStream.OpenSegment();
                            SegStream?.Close();
                            SegStream?.Dispose();
                        }
                    }
                }
                
            };

            BGWorker.RunWorkerCompleted += (sender, e) => {
                if (TaskCompSrc.Task.IsCompleted)
                    return;
                TaskCompSrc.SetResult(false);
            };

            BGWorker.RunWorkerAsync();

            return TaskCompSrc.Task.Result;
        }
    }

    public unsafe struct DecompressTaskInfo
    {
        public bool Running;
        public long* TotalDecompressed;
        public long TotalSize;
        public string EntryName;

        public Func<Stream> Content;

        public long SafeTotalDecompressed { get => *TotalDecompressed; set => *TotalDecompressed = value; }

        public double Progress => ((double)*TotalDecompressed / TotalSize) * 100.0;

        public bool Failed => !Running && *TotalDecompressed > 0 && *TotalDecompressed < TotalSize;

        public Exception? Error { get; internal set; }

        public DecompressorHelperStream[] PartsStream;

        public bool* InSegmentTranstion;
        public bool SafeInSegmentTranstion { get => *InSegmentTranstion; set => *InSegmentTranstion = value; }
    }

    enum CompressionFormat { 
        RAR, ZIP, SevenZip, None
    }
}
