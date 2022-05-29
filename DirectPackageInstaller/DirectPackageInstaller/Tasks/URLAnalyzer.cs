using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DirectPackageInstaller.IO;

namespace DirectPackageInstaller.Tasks;

public static class URLAnalyzer
{
    public static Dictionary<string, URLInfo> URLInfos = new Dictionary<string, URLInfo>();

    public static async Task<URLInfo> Analyze(string[] URLs)
    {
        string MainURL = URLs.First();

        if (URLInfos.ContainsKey(MainURL))
        {
            var Info = URLInfos[MainURL];
            
            while (!Info.Ready & !Info.Failed)
                await Task.Delay(100);
            
            return Info;
        }

        URLInfos[MainURL] = new URLInfo()
        {
            MainURL = MainURL,
            Urls = URLs.Select(x=> new URLInfoEntry()
            {
                URL = x
            }).ToArray()
        };

        var Result = Parallel.For(0, URLs.Length, (i, loop) =>
        {
            try
            {
                ref var Info = ref URLInfos[MainURL].Urls[i];
                Info.Stream = new FileHostStream(Info.URL, 1024 * 512);
                _ = Info.Stream.Filename;
                Info.Verified = true;
            }
            catch
            {
                URLInfos[MainURL].SetFailed();
                loop.Break();
            }
        });

        while (!Result.IsCompleted)
            await Task.Delay(100);

        return URLInfos[MainURL];
    }
    
    public struct URLInfo
    {
        public string MainURL;
        public URLInfoEntry[] Urls;
        public string[] Links => Urls.Select(x => x.URL).ToArray();
        public bool Ready => Urls.All(x => x.Verified);
        public bool Failed;

        internal void SetFailed()
        {
            Failed = true;
        }
    }

    public struct URLInfoEntry
    {
        public string URL;
        public bool Verified;
        public FileHostStream Stream;
        public string Filename => Stream.Filename;
    }
}

