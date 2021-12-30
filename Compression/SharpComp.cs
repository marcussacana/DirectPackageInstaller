using DirectPackageInstaller.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using SharpCompress.Archives.Zip;
using SharpCompress.Common.SevenZip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DirectPackageInstaller.Compression
{
    class SharpComp
    {

        Common CompCommon = new Common();

        public Dictionary<string, DecompressTaskInfo> Tasks = new Dictionary<string, DecompressTaskInfo>();

        public async Task<string> CreateUnrar(string Url, string Entry)
        {
            string EntryName = Entry;
            Stream[] Inputs;
            string[] Links;

            string Password = null;
            if (Main.CompressInfo.ContainsKey(Url))
            {
                var Info = Main.CompressInfo[Url];
                Inputs = Info.Links.Select(x => new DecompressorHelperStream(new FileHostStream(x, 1024 * 100), CompCommon.MultipartHelper)).ToArray();
                Password = Info.Password;
                Links = Info.Links;
            }
            else {
                Inputs = new Stream[] { new DecompressorHelperStream(new FileHostStream(Url, 1024 * 100), CompCommon.MultipartHelper) };
                Links = new string[] { Url };
            }

            var Archive = RarArchive.Open(Inputs, new global::SharpCompress.Readers.ReaderOptions() {
                Password = Password,
                DisableCheckIncomplete = true
            });
            
            IArchiveEntry PKG;
            var PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));

            if (PKGs.Count() == 1)
                PKG = PKGs.Single();
            else
                PKG = PKGs.Where(x => Path.GetFileName(x.Key) == EntryName).Single();

            if (!StartDecompressor(PKG, Inputs, Links))
                return null;

            return Path.GetFileName(PKG.Key);
        }

        public async Task<string> CreateUn7z(string Url, string Entry)
        {
            string EntryName = Entry;
            Stream[] Inputs;
            string[] Links;

            string Password = null;
            if (Main.CompressInfo.ContainsKey(Url))
            {
                var Info = Main.CompressInfo[Url];
                Inputs = Info.Links.Select(x => new DecompressorHelperStream(new FileHostStream(x, 1024 * 100), CompCommon.MultipartHelper)).ToArray();

                Password = Info.Password;
                Links = Info.Links;
            }
            else
            {
                Inputs = new Stream[] { new DecompressorHelperStream(new FileHostStream(Url, 1024 * 100), CompCommon.MultipartHelper) };
                Links = new string[] { Url };
            }

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
                PKG = PKGs.Where(x => Path.GetFileName(x.Key) == EntryName).Single();

            if (!StartDecompressor(PKG, Inputs, Links))
                return null;

            return Path.GetFileName(PKG.Key);
        }

        public unsafe bool StartDecompressor(IArchiveEntry Entry, Stream[] Inputs, string[] Links)
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
                
                using Stream Output = File.Open(TmpFile, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);

                long TDecomp = 0;
                bool InTrans = false;

                var TaskInfo = new DecompressTaskInfo() {
                    EntryName = EntryName,
                    TotalSize = Entry.Size,
                    Content = () => File.Open(TmpFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                    Running = true,
                    TotalDecompressed = &TDecomp,
                    InSegmentTranstion = &InTrans,
                    PartsStream = Inputs.Cast<DecompressorHelperStream>().ToArray(),
                    PartsLinks = Links
                };

                foreach (var Strm in Inputs.Cast<DecompressorHelperStream>())
                    Strm.Info = TaskInfo;

                var TaskKey = Path.GetFileName(Entry.Key);
                Tasks[TaskKey] = TaskInfo;

                TaskCompSrc.SetResult(true);

                try
                {
                    byte[] Buffer = new byte[1024 * 1024 * 1];
                    
                    int Readed;
                    do
                    {
                        Readed = Input.Read(Buffer, 0, Buffer.Length);
                        Output.Write(Buffer, 0, Readed);

                        *TaskInfo.TotalDecompressed += Readed;

                    } while (Readed > 0);
                }
                catch (Exception ex){
                    TaskInfo.Error = ex;
                }
                finally
                {
                    TaskInfo.Running = false;
                    Tasks[TaskKey] = TaskInfo;
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

        public Exception Error { get; internal set; }

        public string[] PartsLinks;
        public DecompressorHelperStream[] PartsStream;

        public bool* InSegmentTranstion;
        public bool SafeInSegmentTranstion { get => *InSegmentTranstion; set => *InSegmentTranstion = value; }
    }
    enum CompressionFormat { 
        RAR, ZIP, SevenZip, None
    }
}
