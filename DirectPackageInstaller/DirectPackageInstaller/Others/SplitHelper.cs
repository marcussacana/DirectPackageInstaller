using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DirectPackageInstaller.IO;
using Newtonsoft.Json;

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
            SplitPkgInfo Info = JsonConvert.DeserializeObject<SplitPkgInfo>(JSON);
            Stream[] Urls = (from x in Info.pieces orderby x.fileOffset ascending select new FileHostStream(x.url)).ToArray();
            long[] Sizes = (from x in Info.pieces orderby x.fileOffset ascending select x.fileSize).ToArray();
            return new MergedStream(Urls, Sizes);
        }

        struct SplitPkgInfo
        {
            public long originalFileSize;
            public string packageDigest;
            public int numberOfSplitFiles;
            public PkgPiece[] pieces;
        }

        struct PkgPiece
        {
            public string url;
            public long fileOffset;
            public long fileSize;
            public string hashValue;
        }
    }
}
