using LibOrbisPkg.SFO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    static class Program
    {
        public static bool IsUnix => (int)Environment.OSVersion.Platform == 4 || (int)Environment.OSVersion.Platform == 6 || (int)Environment.OSVersion.Platform == 128;


        /// <summary>
        /// Ponto de entrada principal para o aplicativo.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ServicePointManager.DefaultConnectionLimit = 100;
            TempHelper.Clear();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.Run(new Main());
        }


        public static bool HasName(this ParamSfo This, string name)
        {
            foreach (var v in This.Values)
            {
                if (v.Name == name) return true;
            }
            return false;
        }
    }
}
