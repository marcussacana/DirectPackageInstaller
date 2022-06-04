using DirectPackageInstaller.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DirectPackageInstaller.Views;
using SharpCompress.Common;
using ReaderOptions = SharpCompress.Readers.ReaderOptions;

namespace DirectPackageInstaller.Tasks
{
    static class Decompressor
    {
        public static Dictionary<string, string?> Passwords = new Dictionary<string, string?>();
        
        public static async Task<ArchiveDataInfo?> UnrarPKG(Stream Volume, string FirstUrl, Action<string> ProgressChanged, string? EntryName = null, bool Seekable = true, string? Password = null) => await UnrarPKG(new Stream[] { Volume }, FirstUrl, ProgressChanged, EntryName, Seekable, Password);
        public static async Task<ArchiveDataInfo?> UnrarPKG(Stream[] Volumes, string FirstUrl, Action<string> ProgressChanged, string? EntryName = null, bool Seekable = true, string? Password = null)
        {
            IArchive CreateUnrar(Stream[] Parts, string Pass)
            {
                var Options = new ReaderOptions() { Password = Pass, DisableCheckIncomplete = true };

                return RarArchive.Open(Parts, Options);
            }

            return await CommonDecompress(Volumes, FirstUrl, CreateUnrar, Source.RAR, ProgressChanged, EntryName, Seekable, Password);
        }

        public static async Task<ArchiveDataInfo?> Un7zPKG(Stream Volume, string FirstUrl, Action<string> ProgressChanged, string? EntryName = null, bool Seekable = true, string? Password = null) => await Un7zPKG(new Stream[] { Volume }, FirstUrl, ProgressChanged, EntryName, Seekable, Password);
        private static async Task<ArchiveDataInfo?> Un7zPKG(Stream[] Volumes, string FirstUrl, Action<string> ProgressChanged, string? EntryName = null, bool Seekable = true, string? Password = null)
        {

            bool Silent = EntryName != null;
            
            
            IArchive CreateUn7z(Stream[] Parts, string Pass)
            {
                var Options = new ReaderOptions()
                {
                    Password = Pass,
                    DisableCheckIncomplete = Parts.Length > 1
                };

                return SevenZipArchive.Open(new MergedStream(Parts), Options);
            }

            return await CommonDecompress(Volumes, FirstUrl, CreateUn7z, Source.SevenZip, ProgressChanged, EntryName, Seekable, Password);
        }
        
        public static async Task<ArchiveDataInfo?> CommonDecompress(Stream[] Volumes, string FirstUrl, Func<Stream[], string, IArchive> Creator, Source CompType, Action<string> ProgressChanged, string? EntryName = null, bool Seekable = true, string? Password = null)
        {
            URLAnalyzer.URLInfo? Info = null;

            if (URLAnalyzer.URLInfos.ContainsKey(FirstUrl))
            { 
                Info = await URLAnalyzer.Analyze(FirstUrl, true);
                if (Info?.Failed ?? true)
                    return null;

                foreach (var Volume in Volumes)
                    Volume.Close();


                if (CompType.HasFlag(Source.RAR))
                    Volumes = Info!.Value.Urls.SortRarFiles().Select(x => x.Stream()).Cast<Stream>().ToArray();
                else if (CompType.HasFlag(Source.SevenZip))
                    Volumes = Info!.Value.Urls.Sort7zFiles().Select(x => x.Stream()).Cast<Stream>().ToArray();
                    
            }

            bool Ready = false;
            int LastAccess = 0;
            
            for (int i = 0; i < Volumes.Length; i++)
            {
                var Volume = Volumes[i];
                if (Volume is DecompressorHelperStream Helper)
                    Volume = Helper.Base;

                var Index = i;
                Volumes[i] = new DecompressorHelperStream(Volume, Info =>
                {
                    if (Ready)
                        return;
                    
                    if (Index > LastAccess)
                    {
                        LastAccess = Index;
                        App.Callback(() => ProgressChanged($"Decompressing... {Index}/{Volumes.Length} ({(double)Index/Volumes.Length:P0})"));
                    }
                } );
            }

            try
            {
                bool Silent = EntryName != null;

                IArchive? Archive = null;

                if (await App.RunInNewThread(() => Archive = Creator(Volumes, Password)))
                    return null;

                bool Multipart = false;

                bool? Encrypted = null;
                if (await App.RunInNewThread(() => Encrypted = Archive.Entries.Any(x => x.IsEncrypted)))
                {
                    var FN = Volumes.First().TryGetRemoteFileName();
                    Multipart = FN.EndsWith("0") ||
                                FN.Contains(".part", StringComparison.InvariantCultureIgnoreCase);

                    if (Multipart && Volumes.Length > 1)
                        return null;
                }
                else
                {
                    var FN = Volumes.First().TryGetRemoteFileName();
                    Multipart = FN.EndsWith("0") || FN.Contains(".part", StringComparison.InvariantCultureIgnoreCase);
                }

                bool MustReload = false;

                if (Multipart && URLAnalyzer.URLInfos.ContainsKey(FirstUrl) &&
                    URLAnalyzer.URLInfos[FirstUrl].Urls.Length == 1)
                    URLAnalyzer.URLInfos.Remove(FirstUrl);

                if (Password == null && Passwords.ContainsKey(FirstUrl) && Passwords[FirstUrl] != null)
                {
                    Password = Passwords[FirstUrl];
                    MustReload = true;
                }

                bool MissingData = Multipart && Volumes.Count() == 1;

                if (URLAnalyzer.URLInfos.ContainsKey(FirstUrl))
                {
                    Info = URLAnalyzer.URLInfos[FirstUrl];
                    if (Info?.Failed ?? true)
                        return null;

                    MissingData = false;

                    if (Volumes.Length != Info?.Urls.Length)
                        MustReload = true;
                }

                MissingData |= (Encrypted ?? false) && Password == null;

                if (MissingData)
                {
                    var List = new LinkList(Multipart && (Info == null || Info?.Urls.Length == 1), Encrypted, FirstUrl);
                    List.SetInitialInfo(Info?.Links, Password);

                    if (await List.ShowDialogAsync() != DialogResult.OK)
                        throw new Exception();

                    Passwords[FirstUrl] = List.Password;

                    Info = await URLAnalyzer.Analyze(List.Links!, false);

                    while (!Info.Value.Ready && !Info.Value.Failed)
                    {
                        App.Callback(() => ProgressChanged($"Analyzing Urls... {Info?.Progress}"));
                        await Task.Delay(100);
                    }

                    Password = List.Password;
                    MustReload = true;
                }

                if (MustReload)
                {
                    Archive.Dispose();

                    foreach (var Volume in Volumes)
                        Volume.Close();

                    Volumes = Info!.Value.Urls.SortRarFiles().Select(x => x.Stream()).Cast<Stream>().ToArray();

                    return await CommonDecompress(Volumes, FirstUrl, Creator, CompType, ProgressChanged, EntryName, Seekable,
                        Password);
                }

                if (!Archive.IsComplete)
                {
                    if (!Silent)
                        await MessageBox.ShowAsync("Corrupted, missing or RAR parts with wrong sorting.",
                            "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return null;
                }

                var Result = await UnArchive(Archive, Volumes.Cast<DecompressorHelperStream>().ToArray(), Silent, EntryName, Seekable);

                foreach (var Volume in Volumes)
                {
                    if (Volume is PartialHttpStream volStream)
                        volStream.CloseConnection();
                }

                return Result;
            }
            finally
            {
                Ready = true;
            }
        }

        public static IEnumerable<URLAnalyzer.URLInfoEntry> SortRarFiles(this IEnumerable<URLAnalyzer.URLInfoEntry> Entries)
        {
            var AllEntries = Entries.ToArray();
            var AllNames = Entries.Select(x => x.Filename).ToArray();
            
            Array.Sort(AllNames, AllEntries);

            var ListEntries = AllEntries.ToList();
            var ListNames = AllNames.ToList();

            int? RarExtPart = null;
            for (int i = 0; i < AllEntries.Length; i++)
            {
                var CurrentName = ListNames[i];
                if (CurrentName == null || CurrentName.Contains(".part", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                
                if (CurrentName.EndsWith(".r00", StringComparison.InvariantCultureIgnoreCase))
                {
                    RarExtPart = i;
                    continue;
                }

                if (CurrentName.EndsWith(".rar", StringComparison.InvariantCultureIgnoreCase) && RarExtPart != null)
                {
                    ListEntries.MoveItem(i, RarExtPart.Value);
                    ListNames.MoveItem(i, RarExtPart.Value);
                    RarExtPart = null;
                    continue;
                }
            }

            bool Verify(string Name)
            {
                if (Name == null)
                    return true;
                
                var Ext = Path.GetExtension(Name);
                if (Ext.Equals(".rar", StringComparison.InvariantCultureIgnoreCase))
                    return true;

                if (Ext.StartsWith(".r", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (int.TryParse(Ext.Substring(2), out int _))
                        return true;
                }

                return false;
            }

            return ListEntries.Where(x => Verify(x.Filename));
        }
        
        public static IEnumerable<URLAnalyzer.URLInfoEntry> Sort7zFiles(this IEnumerable<URLAnalyzer.URLInfoEntry> Entries)
        {
            var AllEntries = Entries.ToArray();
            var AllNames = Entries.Select(x => x.Filename).ToArray();
            
            Array.Sort(AllNames, AllEntries);

            var ListEntries = AllEntries.ToList();
            var ListNames = AllNames.ToList();
            
            bool Verify(string Name)
            {
                if (Name == null)
                    return true;
                
                var Ext = Path.GetExtension(Name);
                if (Ext.Equals(".7z", StringComparison.InvariantCultureIgnoreCase))
                    return true;

                if (int.TryParse(Ext.Substring(1), out int _))
                    return true;

                return false;
            }

            return ListEntries.Where(x => Verify(x.Filename));
        }
        

        public static void MoveItem<T>(this List<T> List, int From, int To)
        {
            T Item = List[From];
            List.RemoveAt(From);
            
            if (To > From)
                To--;
            
            List.Insert(To, Item);
        }

        public static async Task<ArchiveDataInfo?> UnArchive(IArchive Archive, DecompressorHelperStream[] Volumes, bool Silent, string? EntryName, bool Seekable)
        {

            var Compressions = Archive.Entries.Where(x => Path.GetExtension(x.Key).StartsWith(".r", StringComparison.OrdinalIgnoreCase) || x.Key.Contains(".7z"));

            var PKGs = Archive.Entries.Where(x => x.Key.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase));

            if (!PKGs.Any())
            {
                if (!Silent)
                    MessageBox.ShowSync("No PKG Found in the given file" + (Compressions.Any() ? "\nIt looks like this file has been redundantly compressed." : ""), "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            if (PKGs.Count() == 1)
            {
                var Entry = PKGs.Single();

                var FileStream = Entry.OpenEntryStream();

                if (Seekable)
                    FileStream = new ReadSeekableStream(FileStream, TempHelper.GetTempFile(null)) { ReportLength = Entry.Size };

                return new (Archive, FileStream, Volumes, Path.GetFileName(Entry.Key), new[] { Entry.Key }, Entry.Size);
            }

            var Files = PKGs.Select(x => Path.GetFileName(x.Key)).ToArray();

            if (!Silent)
            {
                var ChoiceBox = new Select(Files);
                if (await ChoiceBox.ShowDialogAsync() != DialogResult.OK)
                    return null;
                EntryName = ChoiceBox.Choice;
            }
            else if (string.IsNullOrEmpty(EntryName))
                return null;

            var SelectedEntry = PKGs.Single(x => Path.GetFileName(x.Key) == EntryName);
            var SelectedFile = Path.GetFileName(SelectedEntry.Key);

            var Stream = SelectedEntry.OpenEntryStream();

            if (Seekable)
                Stream = new ReadSeekableStream(Stream, TempHelper.GetTempFile(null)) { ReportLength = SelectedEntry.Size };

            return new (Archive, Stream, Volumes, SelectedFile, Files, SelectedEntry.Size);
        }
    }

    public struct ArchiveDataInfo
    {
        public ArchiveDataInfo(IArchive Archive, Stream Buffer, DecompressorHelperStream[] Volumes, string Filename, string[] PKGList, long Length)
        {
            this.Archive = Archive;
            this.Buffer = Buffer;
            this.Volumes = Volumes;
            this.Filename = Filename;
            this.PKGList = PKGList;
            this.Length = Length;
        }
        
        public readonly IArchive Archive;
        public readonly Stream Buffer;
        public readonly DecompressorHelperStream[] Volumes;
        public readonly string Filename;
        public readonly string[] PKGList;
        public readonly long Length;
    }
}
