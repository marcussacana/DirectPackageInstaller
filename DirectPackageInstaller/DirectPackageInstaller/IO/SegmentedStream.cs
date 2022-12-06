using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DirectPackageInstaller.IO
{
    public class SegmentedStream : Stream
    {
        public static int DefaultConcurrency = 4;
        
        public int BufferSize;

        private bool CloseBuffer;

        Func<Stream> OpenBuffer;
        Stream ReaderStream;

        List<SegmentProcessor> Processors = new List<SegmentProcessor>();

        List<Stream> Streams = new List<Stream>();

        List<VirtualStream> Segments = new List<VirtualStream>();
        List<long> SegmentProgress = new List<long>();

        public long ScanProgress
        {
            get
            {
                lock (SegmentProgress)
                {
                    long Total = 0;
                    for (int i = 0; i < SegmentProgress.Count; i++)
                    {
                        var SegId = GetSegmentByOffset(Total);
                        if (SegId == -1)
                            return 0;
                        Total += SegmentProgress[SegId];
                        if (ReamingSegmentLength(SegId) > 0)
                            break;
                    }
                    return Total;
                }
            }
        }

        public long TotalProgress {
            get
            {
                lock (SegmentProgress) {
                    return SegmentProgress.Sum();
                }
            } 
        }

        public bool InProgess => TotalConcurrency > 0;
        public bool Finished => TotalProgress >= TotalSize && !InProgess;

        BackgroundWorker Worker;

        public Func<Stream> OpenSegment { get; private set; }

        public int Concurrency;

        public long TotalSize { get; private set; }

        int Connections => Segments.Select(x => x.Position > 0).Count();

        long TotalBuffered => Segments.Select(x => x.Position).Sum();

        long TotalConcurrency => Segments.Count(x => x.Position < x.Length);

        int BiggestSegment => Segments.Select((x, i) => (Reaming: ReamingSegmentLength(i), ID: i)).MaxBy(x => x.Reaming).ID;

        int? CriticalSegment = null;

        public SegmentedStream(Func<Stream> OpenConnection, Func<Stream> OpenBuffer, int BufferSize = 1024 * 1024, bool CloseBuffer = false, int? Concurrency = null)
        {
            Stream Buffer;
            if (OpenBuffer == null)
            {
                var TempFile = TempHelper.GetTempFile(null);
                
                if (File.Exists(TempFile))
                    File.Delete(TempFile);
                
                Buffer           = new FileStream(TempFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite, BufferSize, FileOptions.RandomAccess | FileOptions.WriteThrough);
                OpenBuffer = () => new FileStream(TempFile, FileMode.Open,      FileAccess.ReadWrite, FileShare.ReadWrite, BufferSize, FileOptions.RandomAccess | FileOptions.WriteThrough);

                ReaderStream = OpenBuffer();
            }
            else
            {
                Buffer = OpenBuffer();
                ReaderStream = OpenBuffer();
            }

            Streams.Add(Buffer);
            Streams.Add(ReaderStream);

            this.OpenBuffer = OpenBuffer;


            this.BufferSize = BufferSize;
            this.CloseBuffer = CloseBuffer;
            OpenSegment = OpenConnection;
            var First = OpenSegment();

            TotalSize = First.Length;
            
            if (Buffer.Length != TotalSize)
                Buffer.SetLength(TotalSize);

            lock (SegmentProgress)
            {
                Segments.Add(new VirtualStream(new BufferedStream(First), 0, First.Length)
                {
                    ForceAmount = true
                });
                SegmentProgress.Add(0);
            }

            this.Concurrency = Concurrency ?? DefaultConcurrency;

            Worker = new BackgroundWorker();
            Worker.DoWork += ConcurrencyRead;
            Worker.WorkerSupportsCancellation = true;
            Worker.RunWorkerAsync();

        }

        ~SegmentedStream()
        {
            Close();
        }

        private async void ConcurrencyRead(object sender, DoWorkEventArgs e)
        {
            SegmentBuffer(0, null, 0);
            while (TotalBuffered < TotalSize && !e.Cancel)
            {
                try
                {
                    while (TotalConcurrency < Concurrency && !e.Cancel)
                    {
                        if (Connections < TotalConcurrency)
                            break;

                        int NextSegment = BiggestSegment;
                        long Reaming = ReamingSegmentLength(NextSegment); //Segment.Length - Segment.Position

                        long OldReaming = Reaming / 2;
                        long NewReaming = OldReaming;

                        bool MustAssert = Reaming % 2 != 0;

                        if (MustAssert)
                            NewReaming += 1;

                        //if (OldReaming + NewReaming != Reaming)
                        //    break;

                        if (Reaming < 1024 * 1024 * 2)
                            break;

                        var OldSegment = Segments[NextSegment];

                        long NewSize = OldSegment.Length - NewReaming;

                        long NewSegOffset = OldSegment.FilePos + NewSize;

                        //if (NewSegOffset + NewReaming != OldSegment.Length + OldSegment.FilePos)
                        //    break;

                        lock (SegmentProgress)
                        {
                            Segments.Add(new VirtualStream(new BufferedStream(OpenSegment()), NewSegOffset, NewReaming)
                            {
                                ForceAmount = true
                            });
                            SegmentProgress.Add(0);
                        }

                        

                        SegmentBuffer(Segments.Count() - 1, OldSegment, NewSize);
                    }

                    await Task.Delay(500);
                }
                catch
                {
                    Cancel();
                }
            }
        }

        int SecondsLost = 0;

        private void SegmentBuffer(int ThisID, Stream? OldSegment, long OldSegmentedNewSize)
        {
            int ID = ThisID;
            var NewBuffer = OpenBuffer();
            Streams.Add(NewBuffer);

            Stream? OldStream = OldSegment;
            long OldStreamNewSize = OldSegmentedNewSize;
            
            Processors.Add(new SegmentProcessor(Segments[ID], NewBuffer, this, BufferSize, async (Readed) => {
                lock (SegmentProgress)
                {
                    SegmentProgress[ID] += Readed;
                    
                    if (OldStream is not null && Readed > 0)
                    {
                        OldStream.SetLength(OldStreamNewSize);
                        OldStream = null;
                    }
                }

                int MaxSeconds = 3;
                while (CriticalSegment is not null && CriticalSegment != ID && MaxSeconds-- > 0)
                {
                    await Task.Delay(1000);
                }
            }));
        }

        long ReamingSegmentLength(int ID)
        {
            return Segments[ID].Length - Segments[ID].Position;
        }

        int GetSegmentByOffset(long Offset)
        {
            for (int i = 0; i < Segments.Count; i++)
            {
                var Segment = Segments[i];
                if (Segment.FilePos <= Offset && Segment.FilePos + Segment.Length > Offset)
                    return i;
            }

            return -1;
        }

        (long SegmentOffset, long Ready, long ReadyOffset, int ID) SegmentReadyOffset(int ID)
        {
            if (ID == -1)
                return (-1, -1, -1, -1);

            lock (SegmentProgress)
            {
                return (Segments[ID].FilePos, SegmentProgress[ID], Segments[ID].FilePos + SegmentProgress[ID], ID);
            }
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => TotalSize;

        long CurrentPos = 0;
        public override long Position { get => CurrentPos; set => CurrentPos = value; }

        public override void Flush()
        {
            ReaderStream?.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count <= 0)
                return 0;
            
            (long SegmentOffset, long Ready, long ReadyOffset, int ID) ReadyInfo;
           
            //We should try stop reading of the lastest downloaded bytes because the Reader stream
            //can try buffer empty data, when the download is finished then we can allow read it.
            
            int AntiBuffering() => Finished ? 0 : BufferSize * 2;
            
            (long SegmentOffset, long Ready, long ReadyOffset, int ID) GetCurrentSegmentInfo() => SegmentReadyOffset(GetSegmentByOffset(Position));

            DateTime WaitBegin = DateTime.Now;

            while (((ReadyInfo = GetCurrentSegmentInfo()).ReadyOffset <= Position + AntiBuffering()) || ReadyInfo.ReadyOffset == -1 || Position < ReadyInfo.SegmentOffset)
            {
                if (ReamingSegmentLength(ReadyInfo.ID) == 0)
                {
                    break;
                }

                if (ReadyInfo.ReadyOffset == -1)
                {
                    Task.Delay(1000).Wait();
                    
                    if (GetCurrentSegmentInfo().ReadyOffset == -1)
                        return 0;
                }

                if ((DateTime.Now - WaitBegin).TotalSeconds > 10)
                    CriticalSegment = ReadyInfo.ID;

                Flush();
                Task.Delay(100).Wait();
            }

            CriticalSegment = null;

            int AntiBuffer = AntiBuffering();

            if (ReamingSegmentLength(ReadyInfo.ID) == 0)
                AntiBuffer = 0;

            if (Position + count + AntiBuffer > ReadyInfo.ReadyOffset)
            {
                count = (int) (ReadyInfo.ReadyOffset - Position) - AntiBuffer;
                
                if (count <= 0)
                    count = 1;
            }

            if (ReaderStream == null)
            {
                ReaderStream = OpenBuffer();
                Streams.Add(ReaderStream);
            }
            

            lock (this)
            {
                ReaderStream.Seek(Position, SeekOrigin.Begin);
                ReaderStream.Flush();

                int Readed = ReaderStream.Read(buffer, offset, count);

                Position += Readed;
                return Readed;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;

                case SeekOrigin.Current:
                    Position += offset;
                    break;

                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
            }

            return Position;
        }
        
        public void Cancel()
        {
            Worker?.CancelAsync();
            foreach (var Segment in Processors)
                Segment.Cancel();
        }
        
        protected override void Dispose(bool Disposing)
        {
            Cancel();
            
            if (CloseBuffer)
            {
                ReaderStream?.Close();

                foreach (var Stream in Streams)
                    Stream?.Close();

                Streams.Clear();
            }

            base.Dispose(Disposing);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }

    class SegmentProcessor
    {
        object Locker;

        long OutputOffset;

        int BufferSize;
        Stream StreamBuffer;

        Func<int, Task> ProgressCallback;
        BackgroundWorker Worker;

        VirtualStream Input;

        public SegmentProcessor(VirtualStream Segment, Stream Buffer, object Locker, int BufferSize, Func<int, Task> ProgressCallback)
        {
            Input = Segment;
            StreamBuffer = Buffer;
            OutputOffset = Input.FilePos;

            this.Locker = Locker;
            this.BufferSize = BufferSize;
            this.ProgressCallback = ProgressCallback;

            Worker = new BackgroundWorker();
            Worker.DoWork += Worker_DoWork;
            Worker.WorkerSupportsCancellation = true;
            Worker.RunWorkerAsync();
        }

        ~SegmentProcessor()
        {
            Worker?.CancelAsync();
        }

        public void Cancel() => Worker?.CancelAsync();

        private async void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            byte[] Buffer = new byte[BufferSize];
            int Readed;

            long ReadBeginPos = 0;
            do
            {
                try
                {
                    ReadBeginPos = Input.Position;

                    Readed = await Input.ReadAsync(Buffer, 0, Buffer.Length);

                    if (e.Cancel)
                        break;

                    if (StreamBuffer is UnsafeMemoryStream {Disposed: true})
                        break;
                    
                    if (Readed > 0)
                    {
                        lock (Locker)
                        {
                            long WritePos = OutputOffset + ReadBeginPos;

                            if (StreamBuffer.Position != WritePos)
                                StreamBuffer.Seek(WritePos, SeekOrigin.Begin);
                            
                            StreamBuffer.Write(Buffer, 0, Readed);
                            StreamBuffer.Flush();
                        }
                    }

                    await ProgressCallback(Readed);
                }
                catch
                {
                    Input.Position = ReadBeginPos;
                    await Task.Delay(5000);
                }

            } while (Input.Position < Input.Length && !e.Cancel);
            
            if (Input.Base is PartialHttpStream inputBase)
                inputBase.CloseConnection();

            Input.Close();
        }
    }

    unsafe struct SegmentInfo
    {
        public SegmentInfo(long Offset, long Length)
        {
            this.Offset = Offset;
            this.Length = &Length;
        }

        public long Offset;
        long* Length;

        public long SafeLength
        {
            get
            {
                return *Length;
            }
            set
            {
                *Length = value;
            }
        }
    }
}
