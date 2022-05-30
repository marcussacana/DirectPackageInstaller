using DirectPackageInstaller.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Dialogs;
using DirectPackageInstaller.Tasks;
using Microsoft.CodeAnalysis;

namespace DirectPackageInstaller.Compression
{
    class SharpComp
    {

        Common CompCommon = new Common();

        public Dictionary<string, DecompressTaskInfo> Tasks = new Dictionary<string, DecompressTaskInfo>();

        public string? CreateUnrar(string Url, string? EntryName)
        {
            Stream[] Inputs;

            string? Password = null;
            
            if (URLAnalyzer.URLInfos.ContainsKey(Url))
            {
                var Info = URLAnalyzer.URLInfos[Url].Urls.SortRarFiles();
                Inputs = Info.Select(x => new DecompressorHelperStream(x.Stream(), CompCommon.MultipartHelper)).Cast<Stream>().ToArray();
                Password = Decompressor.Passwords.ContainsKey(Url) ?  Decompressor.Passwords[Url] : null;
            }
            else
            {
                Inputs = new Stream[] { new DecompressorHelperStream(new FileHostStream(Url, 1024 * 512), CompCommon.MultipartHelper) };
            }

            return CreateUnrar(Inputs, EntryName, Password);
        }

        private string? CreateUnrar(Stream[] Inputs, string? EntryName, string? Password)
        {
            var Archive = RarArchive.Open(Inputs, new SharpCompress.Readers.ReaderOptions()
            {
                Password = Password,
                DisableCheckIncomplete = true
            });

            IArchiveEntry PKG;
            var PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));

            if (PKGs.Count() == 1)
                PKG = PKGs.Single();
            else
                PKG = PKGs.Single(x => Path.GetFileName(x.Key) == EntryName);

            if (!StartDecompressor(PKG, Inputs))
                return null;

            return Path.GetFileName(PKG.Key);
        }

        public string? CreateUn7z(string Url, string? EntryName)
        {
            Stream[] Inputs;

            string? Password = null;
           
            if (URLAnalyzer.URLInfos.ContainsKey(Url))
            {
                var Info = URLAnalyzer.URLInfos[Url].Urls.Sort7zFiles();
                Inputs = Info.Select(x => new DecompressorHelperStream(x.Stream(), CompCommon.MultipartHelper)).Cast<Stream>().ToArray();
                Password = Decompressor.Passwords.ContainsKey(Url) ?  Decompressor.Passwords[Url] : null;
            }
            else
            {
                Inputs = new Stream[] { new DecompressorHelperStream(new FileHostStream(Url, 1024 * 512), CompCommon.MultipartHelper) };
            }
            
            return CreateUn7z(Inputs, EntryName, Password);
        }

        public string? CreateUn7z(Stream[] Inputs, string? EntryName, string? Password)
        {
            var Options = new SharpCompress.Readers.ReaderOptions()
            {
                Password = Password,
                DisableCheckIncomplete = true
            };

            var Archive = SevenZipArchive.Open(new MergedStream(Inputs), Options);

            IArchiveEntry PKG;
            var PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));

            if (PKGs.Count() == 1)
                PKG = PKGs.Single();
            else
                PKG = PKGs.Single(x => Path.GetFileName(x.Key) == EntryName);

            if (!StartDecompressor(PKG, Inputs))
                return null;

            return Path.GetFileName(PKG.Key);
        }

        private unsafe bool StartDecompressor(IArchiveEntry Entry, Stream[] Inputs)
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
                    PartsStream = Inputs.Cast<DecompressorHelperStream>().ToArray(),
                    Error = null
                };

                foreach (var Strm in Inputs.Cast<DecompressorHelperStream>())
                    Strm.Info = TaskInfo;

                var TaskKey = Path.GetFileName(Entry.Key);
                Tasks[TaskKey] = TaskInfo;

                TaskCompSrc.SetResult(true);

                try
                {
                    var Buffer = new byte[1024 * 1024 * 1];

                    int Readed;
                    do
                    {
                        Readed = Input.Read(Buffer, 0, Buffer.Length);
                        Output.Write(Buffer, 0, Readed);

                        *TaskInfo.TotalDecompressed += Readed;

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

                    foreach (var InBuffer in Inputs.Cast<DecompressorHelperStream>())
                    {
                        if (InBuffer.Base is SegmentedStream)
                            InBuffer.Base.Close();
                        
                        InBuffer.Dispose();
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

    unsafe struct DecompressTaskInfo
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
