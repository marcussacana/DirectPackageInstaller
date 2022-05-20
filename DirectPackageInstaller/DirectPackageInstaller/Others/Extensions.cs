using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Reflection;
using LibOrbisPkg.SFO;

namespace DirectPackageInstaller;

public static class Extensions
{
        /// <summary>
        /// We aren't kids microsoft, we shouldn't need this
        /// </summary>
        public static void UnlockHeaders()
        {
            try
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
            catch { }
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
            var BIndex = SubstringA == null ? 0 : String.IndexOf(SubstringA);
            if (BIndex == -1 || !String.Contains(SubStringB))
                throw new Exception("SubstringB Not Found");

            BIndex += SubstringA?.Length ?? 0;
            var EIndex = String.IndexOf(SubStringB, BIndex);
            return String.Substring(BIndex, EIndex - BIndex);
        }
}