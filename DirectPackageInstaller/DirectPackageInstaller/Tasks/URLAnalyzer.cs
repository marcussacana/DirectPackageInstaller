using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DirectPackageInstaller.IO;
using Microsoft.CodeAnalysis;
using ReactiveUI;

namespace DirectPackageInstaller.Tasks;

public static class URLAnalyzer
{
    public static Dictionary<string, URLInfo> URLInfos = new Dictionary<string, URLInfo>();

    public static async Task<URLInfo> Analyze(string URL, bool Wait)
    {
        return await Analyze(new string[] {URL}, Wait);
    }
    public static async Task<URLInfo> Analyze(string[] URLs, bool Wait)
    {
        string MainURL = URLs.First();

        if (URLInfos.ContainsKey(MainURL))
        {
            var Info = URLInfos[MainURL];
            
            while (Wait && !Info.Ready & !Info.Failed)
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

        var BGTask = App.RunInNewThread(() =>
        {
            Parallel.For(0, URLs.Length, new ParallelOptions()
            {
                MaxDegreeOfParallelism = 4
            }, (i, loop) =>
            {
                if (loop.ShouldExitCurrentIteration)
                    return;
                
                ref var Info = ref URLInfos[MainURL].Urls[i];
                try
                {
                    string URL = Info.URL;
                    Info.Stream = () => new FileHostStream(URL);
                    
                    FileHostStream Head = Info.Stream();
                    Info.Filename = Head.Filename;
                    
                    if (Head.SingleConnection)
                    {
                        Info.SingleConnection = true;
                        Info.Stream = () => Head;
                    }
                    else
                    {
                        Head.Close();
                        Head.Dispose();
                    }
                    
                    Info.Verified = true;
                }
                catch (Exception ex)
                {
                    Info.Failed = true;
                    loop.Break();
                }
            });
        });

        if (Wait)
            await BGTask;
        
        return URLInfos[MainURL];
    }
    
    public struct URLInfo
    {
        public string MainURL;
        public URLInfoEntry[] Urls;
        public string[] Links => Urls.Select(x => x.URL).ToArray();
        public bool Ready => Urls.All(x => x.Verified);
        public bool Failed => Urls.Any(x => x.Failed);

        public int TotalVerified => Urls.Count(x => x.Verified);
        public string Progress => $"{TotalVerified}/{Urls.Length} ({(double)TotalVerified/Urls.Length:P0})";
    }

    public struct URLInfoEntry
    {
        public string URL;
        public bool SingleConnection;
        public bool Verified;
        public bool Failed;
        public Func<FileHostStream> Stream;
        public string Filename;
    }
}

