using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DirectPackageInstaller.IO;
using System.Text.Json;

namespace DirectPackageInstaller
{
    static class SplitHelper
    {
        public static Stream OpenRemoteJSON(string URL) {
            using (Stream Downloader = new PartialHttpStream(URL))
            using (MemoryStream Buffer = new MemoryStream())
            {
                Downloader.CopyTo(Buffer);
                var JSON = Encoding.UTF8.GetString(Buffer.ToArray());
                return OpenJSON(JSON);
            }
        }
        
        public static Stream OpenLocalJSON(string FilePath) {
            using (Stream Reader = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            using (MemoryStream Buffer = new MemoryStream())
            {
                Reader.CopyTo(Buffer);
                var JSON = Encoding.UTF8.GetString(Buffer.ToArray());
                return OpenJSON(JSON);
            }
        }

        private static Stream OpenJSON(string JSON)
        {
            PKGManifest Info = JsonSerializer.Deserialize<PKGManifest>(JSON);
            Stream[] Urls = (from x in Info.pieces orderby x.fileOffset ascending select new FileHostStream(x.url)).ToArray();
            long[] Sizes = (from x in Info.pieces orderby x.fileOffset ascending select x.fileSize).ToArray();
            return new MergedStream(Urls, Sizes);
        }

        public struct PKGManifest
        {
            public long originalFileSize { get; set; }
            public string packageDigest { get; set; }
            public int numberOfSplitFiles { get; set; }
            public PkgPiece[] pieces { get; set; }
        }

        public struct PkgPiece
        {
            public string url { get; set; }
            public long fileOffset { get; set; }
            public long fileSize { get; set; }
            public string hashValue { get; set; }
        }
    }
}
