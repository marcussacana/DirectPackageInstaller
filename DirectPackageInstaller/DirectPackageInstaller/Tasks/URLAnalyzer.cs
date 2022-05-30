using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DirectPackageInstaller.IO;
using Microsoft.CodeAnalysis;

namespace DirectPackageInstaller.Tasks;

public static class URLAnalyzer
{
    public static Dictionary<string, URLInfo> URLInfos = new Dictionary<string, URLInfo>();

    public static async Task<URLInfo> Analyze(string URL)
    {
        return await Analyze(new string[] {URL});
    }
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

        await App.RunInNewThread(() =>
        {
            Parallel.For(0, URLs.Length, (i, loop) =>
            {
                try
                {
                    ref var Info = ref URLInfos[MainURL].Urls[i];

                    string URL = Info.URL;
                    Info.Stream = () => new FileHostStream(URL);

                    using (FileHostStream Head = Info.Stream())
                        Info.Filename = Head.Filename;

                    Info.Verified = true;
                }
                catch
                {
                    URLInfos[MainURL].SetFailed();
                    loop.Break();
                }
            });
        });


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
        public Func<FileHostStream> Stream;
        public string Filename;
    }
}

