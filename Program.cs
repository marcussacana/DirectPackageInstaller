using LibOrbisPkg.SFO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
            UnlockHeaders();
            ServicePointManager.DefaultConnectionLimit = 100;
            TempHelper.Clear();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.Run(new Main());
        }

        /// <summary>
        /// We aren't kids microsoft, we shouldn't need this
        /// </summary>
        public static void UnlockHeaders()
        {
            var tHashtable = typeof(WebHeaderCollection).Assembly.GetType("System.Net.HeaderInfoTable")
                            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
                            .Where(x => x.FieldType.Name == "Hashtable").Single();

            var Table = (Hashtable)tHashtable.GetValue(null);
            foreach (var Key in Table.Keys.Cast<string>().ToArray())
            {
                var HeaderInfo = Table[Key];
                HeaderInfo.GetType().GetField("IsRequestRestricted", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(HeaderInfo, false);
                HeaderInfo.GetType().GetField("IsResponseRestricted", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(HeaderInfo, false);
                Table[Key] = HeaderInfo;
            }

            tHashtable.SetValue(null, Table);
        }

        public static bool HasName(this ParamSfo This, string name)
        {
            foreach (var v in This.Values)
            {
                if (v.Name == name) return true;
            }
            return false;
        }

        public static string Substring(this string String, string Substring)
        {
            return String.Substring(String.IndexOf(Substring) + Substring.Length);
        }
        public static string Substring(this string String, string SubstringA, string SubStringB)
        {
            var BIndex = String.IndexOf(SubstringA);
            if (BIndex == -1 || !String.Contains(SubStringB))
                throw new Exception("Substring Not Found");

            BIndex += SubstringA.Length;
            var EIndex = String.IndexOf(SubStringB, BIndex);
            return String.Substring(BIndex, EIndex - BIndex);
        }
    }
}
