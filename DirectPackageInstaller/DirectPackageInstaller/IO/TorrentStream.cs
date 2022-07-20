using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;

namespace DirectPackageInstaller.IO;

public class TorrentStream : Stream
{
    static readonly ClientEngine Engine = new ClientEngine();
    static readonly Dictionary<string, Stream> StreamMap = new Dictionary<string, Stream>();

    private TorrentManager? Manager = null;

    private Torrent CurrentTorrent;
    private TorrentFile CurrentFile;
        
    private Stream? CurrentStream;
    
    public TorrentStream(string TorrentUrl, string? EntryName = null) : this(Torrent.Load(new Uri(TorrentUrl), TempHelper.GetTempFile(null)), EntryName) {}

    public TorrentStream(byte[] TorrentData, string? EntryName = null) : this(Torrent.Load(TorrentData), EntryName) { }

    public TorrentStream(Torrent? Torrent, string? EntryName = null)
    {
        if (Torrent == null)
            throw new Exception("Failed to Load the Torrent");
     
        CurrentTorrent = Torrent;
        
        if (EntryName == null)
        {
            var PKGEntries = Torrent.Files.Where(x => Path.GetExtension(x.Path).ToLowerInvariant() == ".pkg").ToArray();

            if (!PKGEntries.Any()) 
                throw new Exception("No PKG Found in the given Torrent");

            CurrentFile = PKGEntries.Single();
            return;
        }

        var Entries = Torrent.Files.Where(x => Path.GetFileName(x.Path).Equals(EntryName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
        CurrentFile = Entries.Single();
    }

    ~TorrentStream()
    {
        Manager?.StopAsync();
        CurrentStream?.Dispose();
        
        if (CurrentFile != null && StreamMap.ContainsKey(CurrentFile.Path))
            StreamMap.Remove(CurrentFile.Path);
    }

    public override void Flush()
    {
        CurrentStream?.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (CurrentStream == null)
            await OpenTorrent(cancellationToken);
        
        if (Manager!.State == TorrentState.Stopped)
            await Manager.StartAsync();
        
        return await CurrentStream!.ReadAsync(buffer, offset, count, cancellationToken);
    }

    private async Task OpenTorrent(CancellationToken cancellationToken)
    {
        if (!StreamMap.ContainsKey(CurrentFile.Path))
        {
            try
            {
                CurrentStream = StreamMap[CurrentFile.Path];
                CurrentStream.Position = 0;
                return;
            }
            catch
            {
                
            }
        }

        var OpenTorrents = Engine.Torrents.Where(x => x.InfoHash.ToHex() == CurrentTorrent.InfoHash.ToHex()).ToArray();
        var TempDir = TempHelper.GetTempFile(null) + Path.DirectorySeparatorChar;
        Manager = OpenTorrents.Any() ? OpenTorrents.Single() : (await Engine.AddStreamingAsync(CurrentTorrent, TempDir));
        await Manager.StartAsync();
        foreach (var File in Manager.Files)
        {
            if (File.Path == CurrentFile.Path)
            {
                await Manager.SetFilePriorityAsync(File, Priority.Highest);

                var Cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Cancel.CancelAfter(TimeSpan.FromMinutes(5));

                if (!StreamMap.ContainsKey(CurrentFile.Path))
                    StreamMap[CurrentFile.Path] = await Manager.StreamProvider.CreateStreamAsync(File, Cancel.Token);
                
                CurrentStream = StreamMap[CurrentFile.Path];
                continue;
            }

            if (File.Priority == Priority.Normal)
                await Manager.SetFilePriorityAsync(File, Priority.DoNotDownload);
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return CurrentStream?.Seek(offset, origin) ?? throw new Exception("Torrent not Initialized");
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public static string[] GetTorrentFiles(byte[] TorrentData) => GetTorrentFiles(Torrent.Load(TorrentData));
    public static string[] GetTorrentFiles(Torrent Torrent)
    {
        return Torrent.Files.ToArray().Select(x => x.Path).ToArray();
    }

    public override bool CanRead { get => true; }
    public override bool CanSeek { get => true; }
    public override bool CanWrite { get => false; }
    public override long Length { get => CurrentFile.Length; }
    public override long Position
    {
        get => CurrentStream?.Position ?? 0;
        set
        {
         
            if (CurrentStream != null) 
                CurrentStream.Position = value;
            else if (value > 0)
                throw new Exception("Torrent not Initialized");
        }
}
}