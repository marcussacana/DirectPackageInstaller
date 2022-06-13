using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace DirectPackageInstaller
{
    public static class TempHelper
    {
        static string GetHash(string Content) {
            SHA256 Hasher = SHA256.Create();
            var Hash = Hasher.ComputeHash(Encoding.UTF8.GetBytes(Content));
            return string.Join("", Hash.Select(x => x.ToString("X2")));
        }

        static Random random = new Random();
        public static string TempDir => Path.Combine(App.CacheBaseDirectory ?? App.WorkingDirectory, "Temp");
        public static string GetTempFile(string? ID)
        {
            if (ID == null)
                ID = random.Next().ToString() + random.Next().ToString() + random.Next().ToString();
            
            if (!Directory.Exists(TempDir))
                Directory.CreateDirectory(TempDir);

            return Path.Combine(TempDir, GetHash(ID) + ".tmp");
        }

        public static void Clear()
        {
            foreach (var Filepath in GetAllCachedFiles())
            {
                try
                {
                    File.Delete(Filepath);
                }
                catch { }
            }
        }

        public static bool CacheIsEmpty() => GetAllCachedFiles().Length == 0;

        private static string[] GetAllCachedFiles()
        {
            List<string> Files = new List<string>();
            
            if (Directory.Exists(TempDir))
                Files.AddRange(Directory.GetFiles(TempDir, "*.*", SearchOption.AllDirectories));

            if (App.AndroidCacheDir != null)
            {
                var InternalCache = Path.Combine(App.AndroidCacheDir, "Temp");
                if (Directory.Exists(InternalCache))
                    Files.AddRange(Directory.GetFiles(InternalCache, "*.*", SearchOption.AllDirectories));
            }

            if (App.AndroidSDCacheDir != null)
            {
                var SDCache = Path.Combine(App.AndroidSDCacheDir, "Temp");
                if (Directory.Exists(SDCache))
                    Files.AddRange(Directory.GetFiles(SDCache, "*.*", SearchOption.AllDirectories));
            }

            return Files.Distinct().ToArray();
        }
    }
}
