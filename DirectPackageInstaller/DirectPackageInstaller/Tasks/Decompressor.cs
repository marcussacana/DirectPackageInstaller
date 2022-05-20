using DirectPackageInstaller.IO;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives.SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using DirectPackageInstaller.Views;
using ReaderOptions = SharpCompress.Readers.ReaderOptions;

namespace DirectPackageInstaller.Tasks
{
    static class Decompressor
    {
        public static readonly Dictionary<string, (string[] Links, string? Password)> CompressInfo = new Dictionary<string, (string[] Links, string? Password)>();
        
        public static ArchiveDataInfo? UnrarPKG(Stream Volume, string FirstUrl, string? EntryName = null, bool Seekable = true, string? Password = null) => UnrarPKG(new Stream[] { Volume }, FirstUrl, EntryName, Seekable, Password);
        public static ArchiveDataInfo? UnrarPKG(Stream[] Volumes, string FirstUrl, string? EntryName = null, bool Seekable = true, string? Password = null)
        {
            bool Silent = EntryName != null;
            var Archive = RarArchive.Open(Volumes, new ReaderOptions()
            {
                Password = Password,
                DisableCheckIncomplete = true
            });

            bool Encrypted = Archive.Entries.Any(x => x.IsEncrypted);

            if (Archive.IsMultipartVolume() && Volumes.Count() == 1)
            {
                Archive.Dispose();

                if (CompressInfo.ContainsKey(FirstUrl))
                {
                    var List = CompressInfo[FirstUrl];
                    return UnrarPKG(List.Links.Select(x => new FileHostStream(x)).ToArray(), FirstUrl, EntryName, Seekable, List.Password);
                }
                else
                {
                    var List = new LinkList(true, Encrypted, FirstUrl);
                    if (List.ShowDialogSync() != DialogResult.OK)
                        throw new Exception();

                    CompressInfo[FirstUrl] = (List.Links, List.Password)!;

                    return UnrarPKG(List.Links.Select(x => new FileHostStream(x)).ToArray(), FirstUrl, EntryName, Seekable, List.Password);
                }
            }
            else if (Encrypted && Password == null && !Archive.IsMultipartVolume())
            {
                if (CompressInfo.ContainsKey(FirstUrl))
                {
                    if (CompressInfo[FirstUrl].Password == null)
                    {
                        var List = new LinkList(false, Encrypted, FirstUrl);
                        if (List.ShowDialogSync() != DialogResult.OK)
                            throw new Exception();

                        CompressInfo[FirstUrl] = (CompressInfo[FirstUrl].Links, List.Password);

                        var Volume = Volumes.Single();
                        Volume.Seek(0, SeekOrigin.Begin);

                        return UnrarPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                    }
                    else
                    {
                        var List = CompressInfo[FirstUrl];

                        var Volume = Volumes.Single();
                        Volume.Seek(0, SeekOrigin.Begin);

                        return UnrarPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                    }
                }
                else
                {
                    var List = new LinkList(false, Encrypted, FirstUrl);
                    if (List.ShowDialogSync() != DialogResult.OK)
                        throw new Exception();

                    CompressInfo[FirstUrl] = (List.Links ?? new string[] { FirstUrl }, List.Password);

                    var Volume = Volumes.Single();
                    Volume.Seek(0, SeekOrigin.Begin);

                    return UnrarPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                }
            }

            if (!Archive.IsComplete)
            {
                if (!Silent)
                    MessageBox.ShowSync("Corrupted, missing or RAR parts with wrong sorting.", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            return UnArchive(Archive, Silent, EntryName, Seekable);
        }

        public static ArchiveDataInfo? Un7zPKG(Stream Volume, string FirstUrl, string? EntryName = null, bool Seekable = true, string? Password = null) => Un7zPKG(new Stream[] { Volume }, FirstUrl, EntryName, Seekable, Password);

        private static ArchiveDataInfo? Un7zPKG(Stream[] Volumes, string FirstUrl, string? EntryName = null, bool Seekable = true, string? Password = null)
        {
            bool Silent = EntryName != null;
            var Options = new ReaderOptions()
            {
                Password = Password,
                DisableCheckIncomplete = Volumes.Length > 1
            };

            var Archive = SevenZipArchive.Open(new MergedStream(Volumes), Options);

            bool Encrypted = false;
            bool Multipart = false;

            if (Volumes.First() is FileHostStream || Volumes.First() is PartialHttpStream)
                Multipart = ((PartialHttpStream)Volumes.First()).Filename.EndsWith("1");

            try
            {
                Encrypted = Archive.Entries.Any(x => x.IsEncrypted);
            }
            catch { }

            if (Multipart && Volumes.Count() == 1)
            {
                Archive.Dispose();

                if (CompressInfo.ContainsKey(FirstUrl))
                {
                    var List = CompressInfo[FirstUrl];
                    return Un7zPKG(List.Links.Select(x => new FileHostStream(x)).ToArray(), FirstUrl, EntryName, Seekable, List.Password);
                }
                else
                {
                    var List = new LinkList(true, Encrypted, FirstUrl);
                    if (List.ShowDialogSync() != DialogResult.OK)
                        throw new Exception();

                    CompressInfo[FirstUrl] = (List.Links, List.Password)!;

                    return Un7zPKG(List.Links.Select(x => new FileHostStream(x)).ToArray(), FirstUrl, EntryName, Seekable, List.Password);
                }
            }
            else if (Encrypted && Password == null && !Multipart)
            {
                if (CompressInfo.ContainsKey(FirstUrl))
                {
                    if (CompressInfo[FirstUrl].Password == null)
                    {
                        var List = new LinkList(false, Encrypted, FirstUrl);
                        if (List.ShowDialogSync() != DialogResult.OK)
                            throw new Exception();

                        CompressInfo[FirstUrl] = (CompressInfo[FirstUrl].Links, List.Password);

                        var Volume = Volumes.Single();
                        Volume.Seek(0, SeekOrigin.Begin);

                        return Un7zPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                    }
                    else
                    {
                        var List = CompressInfo[FirstUrl];

                        var Volume = Volumes.Single();
                        Volume.Seek(0, SeekOrigin.Begin);

                        return Un7zPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                    }
                }
                else
                {
                    var List = new LinkList(false, Encrypted, FirstUrl);
                    if (List.ShowDialogSync() != DialogResult.OK)
                        throw new Exception();

                    CompressInfo[FirstUrl] = (List.Links ?? new string[] { FirstUrl }, List.Password);

                    var Volume = Volumes.Single();
                    Volume.Seek(0, SeekOrigin.Begin);

                    return Un7zPKG(Volume, FirstUrl, EntryName, Seekable, List.Password);
                }
            }

            if (Encrypted && Password == null)
            {
                var List = new LinkList(false, Encrypted, FirstUrl);
                if (List.ShowDialogSync() != DialogResult.OK)
                    throw new Exception();

                CompressInfo[FirstUrl] = (List.Links ?? new string[] { FirstUrl }, List.Password)!;

                foreach (var Volume in Volumes)
                    Volume.Seek(0, SeekOrigin.Begin);

                return Un7zPKG(Volumes, FirstUrl, EntryName, Seekable, List.Password);
            }

            if (!Archive.IsComplete)
            {
                if (!Silent)
                    MessageBox.ShowSync("Corrupted, missing or 7z parts with wrong sorting", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            return UnArchive(Archive, Silent, EntryName, Seekable);
        }

        public static ArchiveDataInfo? UnArchive(IArchive Archive, bool Silent, string? EntryName, bool Seekable)
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

                return new (Archive, FileStream, Path.GetFileName(Entry.Key), new[] { Entry.Key }, Entry.Size);
            }

            var Files = PKGs.Select(x => Path.GetFileName(x.Key)).ToArray();

            if (!Silent)
            {
                var ChoiceBox = new Select(Files);
                if (ChoiceBox.ShowDialogSync() != DialogResult.OK)
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

            return new (Archive, Stream, SelectedFile, Files, SelectedEntry.Size);
        }
    }

    public struct ArchiveDataInfo
    {
        public ArchiveDataInfo(IArchive Archive, Stream Buffer, string Filename, string[] PKGList, long Length)
        {
            this.Archive = Archive;
            this.Buffer = Buffer;
            this.Filename = Filename;
            this.PKGList = PKGList;
            this.Length = Length;
        }
        
        public readonly IArchive Archive;
        public readonly Stream Buffer;
        public readonly string Filename;
        public readonly string[] PKGList;
        public readonly long Length;
    }
}
