using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DirectPackageInstaller.IO
{
    public class SegmentedStream : Stream
    {
        public int BufferSize = 1024 * 1024;

        bool CloseBuffer;

        Stream StreamBuffer;

        List<SegmentProcessor> Processors = new List<SegmentProcessor>();

        List<VirtualStream> Segments = new List<VirtualStream>();
        List<long> SegmentProgress = new List<long>();

        public long TotalProgress {
            get
            {
                lock (SegmentProgress) {
                    return SegmentProgress.Sum();
                }
            } 
        }
        public bool Finished => TotalProgress >= TotalSize && TotalConcurrency == 0;

        BackgroundWorker Worker;

        public Func<Stream> OpenSegment { get; private set; }

        public int Concurrency;

        public long TotalSize { get; private set; }

        int Connections => Segments.Select(x => x.Position > 0).Count();

        long TotalBuffered => Segments.Select(x => x.Position).Sum();

        long TotalConcurrency => Segments.Where(x => x.Position < x.Length).Count();

        int BiggestSegment => Segments.Select((x, i) => (Reaming: ReamingSegmentLength(i), ID: i))
                                              .OrderByDescending(x => x.Reaming).First().ID;

        public SegmentedStream(Func<Stream> Open, Stream Buffer, int BufferSize = 1024 * 1024, bool CloseBuffer = false, int Concurrency = 4)
        {
            this.BufferSize = BufferSize;
            this.CloseBuffer = CloseBuffer;
            OpenSegment = Open;
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

            this.Concurrency = Concurrency;


            StreamBuffer = Buffer;

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
            SegmentBuffer(0);

            while (TotalBuffered < TotalSize && !e.Cancel)
            {
                while (TotalConcurrency < Concurrency && !e.Cancel)
                {
                    if (Connections < TotalConcurrency)
                        break;

                    int NextSegment = BiggestSegment;
                    long Reaming = ReamingSegmentLength(NextSegment);//Segment.Length - Segment.Position

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

                    OldSegment.SetLength(NewSize);

                    SegmentBuffer(Segments.Count() - 1);
                }
                await Task.Delay(500);
            }
        }

        private void SegmentBuffer(int ID)
        {
            Processors.Add(new SegmentProcessor(Segments[ID], StreamBuffer, BufferSize, (Readed) => {
                lock (SegmentProgress)
                {
                    SegmentProgress[ID] += Readed;
                }
            }));
        }

        long ReamingSegmentLength(int ID)
        {
            return Segments[ID].Length - Segments[ID].Position;
        }

        int GetSegmentByOffset(long Offset, int Retry = 0)
        {
            List<int> IDs = new List<int>();
            for (int i = 0; i < Segments.Count; i++)
            {
                var Segment = Segments[i];
                if (Segment.FilePos <= Offset && Segment.FilePos + Segment.Length > Offset)
                    IDs.Add(i);
            }

            if (IDs.Count == 0)
                return -1;

            return IDs.Single();
        }

        long SegmentReadyOffset(int ID)
        {
            if (ID == -1)
                return -1;

            lock (SegmentProgress)
            {
                return Segments[ID].FilePos + SegmentProgress[ID];
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

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long ReadyOffset;
            while ((ReadyOffset = SegmentReadyOffset(GetSegmentByOffset(Position))) <= CurrentPos)
            {
                if (ReadyOffset == -1)
                    return 0;

                Task.Delay(30).Wait();
            }

            if (CurrentPos + count > ReadyOffset)
                count = (int)(ReadyOffset - CurrentPos);

            lock (StreamBuffer)
            {
                StreamBuffer.Position = CurrentPos;
                int Readed = StreamBuffer.Read(buffer, offset, count);
                CurrentPos += Readed;
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

        public override void Close()
        {
            foreach (var Segment in Processors)
                Segment.Cancel();

            Worker?.CancelAsync();

            if (CloseBuffer)
                StreamBuffer?.Close();

            base.Close();
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
        int BufferSize;
        Stream StreamBuffer;

        Action<long> ProgressCallback;
        BackgroundWorker Worker;

        VirtualStream Input;

        public SegmentProcessor(VirtualStream Segment, Stream Buffer, int BufferSize, Action<long> ProgressCallback)
        {
            Input = Segment;
            StreamBuffer = Buffer;
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

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            byte[] Buffer = new byte[BufferSize];
            int Readed;

            long UndoPos = 0;
            do
            {
                try
                {
                    UndoPos = Input.Position;
                    long WritePos = Input.FilePos + Input.Position;

                    Readed = Input.Read(Buffer, 0, Buffer.Length);

                    if (e.Cancel)
                        break;

                    lock (StreamBuffer)
                    {
                        if (StreamBuffer.Position != WritePos)
                            StreamBuffer.Seek(WritePos, SeekOrigin.Begin);

                        StreamBuffer.Write(Buffer, 0, Readed);
                        StreamBuffer.Flush();
                    }

                    ProgressCallback(Readed);
                }
                catch
                {
                    Input.Position = UndoPos;
                }

            } while (Input.Position < Input.Length && !e.Cancel);

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
