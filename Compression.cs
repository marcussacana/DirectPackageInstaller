using DirectPackageInstaller.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DirectPackageInstaller
{
    class Compression
    {
        public static CompressionFormat DetectCompressionFormat(byte[] Magic) {
            if (BitConverter.ToUInt32(Magic, 0) == 0x21726152)
                return CompressionFormat.RAR;
            return CompressionFormat.None;
        }

        public Dictionary<string, DecompressTaskInfo> Tasks = new Dictionary<string, DecompressTaskInfo>();

        public async Task<string> CreateUnrar(string Url, string Entry)
        {
            string EntryName = Entry;
            Stream[] Inputs;

            string Password = null;
            if (Main.RARInfo.ContainsKey(Url))
            {
                var Info = Main.RARInfo[Url];
                Inputs = Info.Links.Select(x => new FileHostStream(x, 1024 * 100)).ToArray();
                Password = Info.Password;
            }
            else Inputs = new Stream[] { new FileHostStream(Url, 1024 * 100) };

            var Archive = RarArchive.Open(Inputs, new SharpCompress.Readers.ReaderOptions() {
                Password = Password
            });
            
            IArchiveEntry PKG;
            var PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));
            
            if (PKGs.Count() == 1)
                PKG = PKGs.Single();
            else
                PKG = PKGs.Where(x => Path.GetFileName(x.Key) == EntryName).Single();

            if (!StartDecompressor(PKG))
                return null;

            return Path.GetFileName(PKG.Key);
        }
        public unsafe bool StartDecompressor(IArchiveEntry Entry)
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

                var TaskInfo = new DecompressTaskInfo() {
                    EntryName = EntryName,
                    TotalSize = Entry.Size,
                    Content = () => File.Open(TmpFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                    Running = true,
                    TotalDecompressed = &TDecomp
                };

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
    }
    enum CompressionFormat { 
        RAR, ZIP, SevenZip, None
    }
}
