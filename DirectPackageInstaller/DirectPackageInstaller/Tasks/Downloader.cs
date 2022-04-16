using DirectPackageInstaller.IO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace DirectPackageInstaller.Tasks
{
    static class Downloader 
    {
        public static Dictionary<string, DownloaderTask> Tasks = new Dictionary<string, DownloaderTask>();

        public static DownloaderTask CreateTask(string URL, Stream Input = null)
        {
            if (Tasks.ContainsKey(URL))
            {
                var OldTask = Tasks[URL];
                if (!OldTask.Failed)
                {
                    return OldTask;
                }

                File.WriteAllText("Cache.error.log", OldTask.Error.ToString());
                Tasks.Remove(URL);
            }

            string TempFile = TempHelper.GetTempFile(URL + "DownTask");

            var NewTask = new DownloaderTask(URL, TempFile, () => File.Open(TempFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            NewTask.Running = true;

            BackgroundWorker Worker = new BackgroundWorker();

            if (Input == null)
                Worker.DoWork += DoSegmentedDownload;
            else
                Worker.DoWork += DoDownload;

            Worker.WorkerSupportsCancellation = true; 
            Tasks[URL] = NewTask;
            Worker.RunWorkerAsync(new object[] { NewTask, Input });

            while (Input == null ? (NewTask.SegmentedRead?.Length ?? 0) == 0 : NewTask.SafeLength == 0)
            {
                NewTask = Tasks[URL];
                System.Threading.Thread.Sleep(10);
            }

            return Tasks[URL];
        }

        private unsafe static void DoDownload(object sender, DoWorkEventArgs e)
        {
            var Worker = (BackgroundWorker)sender;
            var This = (DownloaderTask)((object[])e.Argument)[0];
            var Stream = (Stream)((object[])e.Argument)[1] ?? new FileHostStream(This.Url);

            This.OpenRead = () => File.Open(This.TempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            try
            {
                using (Stream Output = File.Open(This.TempFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite))
                using (Stream Input = new BufferedStream(Stream))
                {
                    byte[] Buffer = new byte[1024 * 1024 * 5];

                    int Readed;
                    do
                    {
                        Readed = Input.Read(Buffer, 0, Buffer.Length);
                        Output.Write(Buffer, 0, Readed);

                        if (*This.Length == 0)
                            *This.Length = Input.Length;

                        *This.ReadyLength += Readed;

                    } while (Readed > 0 && !Worker.CancellationPending);

                    Output.Flush();

                    *This.ReadyLength = Output.Length;
                }

            }
            catch (Exception ex)
            {
                This.Error = ex;
            }
            finally
            {
                This.Running = false;
                Tasks[This.Url] = This;
            }
        }

        private unsafe static void DoSegmentedDownload(object sender, DoWorkEventArgs e)
        {
            var Worker = (BackgroundWorker)sender;
            var This = (DownloaderTask)((object[])e.Argument)[0];

            try
            {
                This.SegmentedRead = new SegmentedStream(() => new FileHostStream(This.Url), null);
                This.OpenRead = () => This.SegmentedRead;
            }
            catch (Exception ex)
            {
                This.Error = ex;
            }
            finally
            {
                Tasks[This.Url] = This;
            }
        }
    }

    internal unsafe struct DownloaderTask
    {
        public string TempFile;
        public string Url;
        public Func<Stream> OpenRead;
        public SegmentedStream SegmentedRead;
        public bool Running;

        public DownloaderTask(string url, string tempFile, Func<Stream> openRead)
        {
            SegmentedRead = null;
            Url = url;
            TempFile = tempFile;
            OpenRead = openRead;
            Running = false;
            Error = null;

            var AddressA = (long*)Marshal.AllocHGlobal(sizeof(long) * 2).ToPointer();
            var AddressB = AddressA + 1;

            *AddressA = 0;
            *AddressB = 0;

            ReadyLength = AddressA;
            Length = AddressB;
        }

        public long* Length;
        public long* ReadyLength;

        public long SafeLength
        {
            get
            {
                if (SegmentedRead == null)
                    return *Length;

                return SegmentedRead?.Length ?? 0;
            }
            set => *Length = value;
        }

        public long SafeReadyLength
        {
            get
            {
                if (SegmentedRead == null)
                    return *ReadyLength;

                return SegmentedRead?.ScanProgress ?? 0;
            }
            set => *ReadyLength = value;
        }

        public double Progress => ((double)SafeReadyLength / SafeLength) * 100.0;

        public bool Failed => !Running && SafeReadyLength > 0 && SafeReadyLength < SafeLength;

        public Exception Error { get; internal set; }
    }
}
