using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    static class TempHelper
    {
        static string GetHash(string Content) {
            SHA512 Hasher = SHA512.Create();
            var Hash = Hasher.ComputeHash(Encoding.UTF8.GetBytes(Content));
            return string.Join("", Hash.Select(x => x.ToString("X2")));
        }

        static Random random = new Random();
        public static string TempDir => Path.Combine(Program.WorkingDirectory, "Temp");
        public static string GetTempFile(string ID)
        {
            if (ID == null)
                ID = random.Next().ToString() + random.Next().ToString() + random.Next().ToString();
            
            if (!Directory.Exists(TempDir))
                Directory.CreateDirectory(TempDir);

            return Path.Combine(TempDir, GetHash(ID) + ".tmp");
        }

        public static void Clear()
        {
            if (!Directory.Exists(TempDir))
                return;

            foreach (var Filepath in Directory.GetFiles(TempDir, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    File.Delete(Filepath);
                }
                catch { }
            }
        }
    }
}
