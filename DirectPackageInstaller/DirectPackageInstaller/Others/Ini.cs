using System.Collections.Generic;
using System.Linq;

namespace DirectPackageInstaller
{
    class Ini
    {
        public string DefaultGroup;
        public string File;
        public string[] IniLines;
        public Ini(string File, string DefaultGroup)
        {
            this.File = File;
            this.DefaultGroup = DefaultGroup;

            if (System.IO.File.Exists(File))
                IniLines = System.IO.File.ReadAllLines(File);
            else
                IniLines = new string[0];
        }

        public Ini(string[] Lines)
        {
            IniLines = Lines;
        }
        ~Ini() => Save();
        public string GetValue(string Name, string Group = null)
        {
            if (Group == null)
                Group = DefaultGroup;

            string CurrentGroup = null;
            foreach (string Line in IniLines)
            {
                if (Line.StartsWith("//") || Line.StartsWith(";") || Line.StartsWith("!"))
                    continue;

                if (Line.StartsWith("[") && Line.EndsWith("]"))
                {
                    CurrentGroup = Line.Substring(1, Line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }

                if (!Line.Contains("="))
                    continue;

                if (CurrentGroup != Group.Trim().ToLowerInvariant())
                    continue;

                string CurrentName = Line.Substring(0, Line.IndexOf('=')).Trim().ToLowerInvariant();
                string CurrentValue = Line.Substring(Line.IndexOf('=') + 1).Trim();

                if (CurrentName != Name.Trim().ToLowerInvariant())
                    continue;

                return CurrentValue;
            }

            return null;
        }

        public bool GetBooleanValue(string Name, string Group = null)
        {
            var Value = GetValue(Name, Group)?.ToLowerInvariant();
            return Value is "true" or "1" or "yes";
        }

        public Dictionary<string, string> GetValues(string Group)
        {
            var Result = new Dictionary<string, string>();
            string CurrentGroup = null;
            foreach (string Line in IniLines)
            {
                if (Line.StartsWith("//") || Line.StartsWith(";") || Line.StartsWith("!"))
                    continue;

                if (Line.StartsWith("[") && Line.EndsWith("]"))
                {
                    CurrentGroup = Line.Substring(1, Line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }

                if (!Line.Contains("="))
                    continue;

                if (CurrentGroup != Group.Trim().ToLowerInvariant())
                    continue;

                string CurrentName = Line.Substring(0, Line.IndexOf('=')).Trim().ToLowerInvariant();
                string CurrentValue = Line.Substring(Line.IndexOf('=') + 1).Trim();

                Result[CurrentName] = CurrentValue;
            }

            if (Result.Count == 0)
                return null;

            return Result;
        }

        public void SetValues(string Group, Dictionary<string, string> Entries)
        {
            foreach (KeyValuePair<string, string> Entry in Entries)
            {
                SetValue(Group, Entry.Key, Entry.Value);
            }
        }

        public void SetValue(string Name, string Value) => SetValue(DefaultGroup, Name, Value);
        public void SetValue(string Group, string Name, string Value)
        {
            Updated = false;
            int GroupBegin = -1;
            int GroupEnd = IniLines.Length;

            string CurrentGroup = null;
            for (int i = 0; i < IniLines.Length; i++)
            {
                string Line = IniLines[i];
                if (Line.StartsWith("//") || Line.StartsWith(";") || Line.StartsWith("!"))
                    continue;

                if (Line.StartsWith("[") && Line.EndsWith("]"))
                {
                    CurrentGroup = Line.Substring(1, Line.Length - 2).Trim().ToLowerInvariant();
                    if (CurrentGroup == Group.ToLowerInvariant())
                    {
                        GroupBegin = i;
                    }
                    continue;
                }

                if (!Line.Contains("="))
                    continue;

                if (CurrentGroup != Group.Trim().ToLowerInvariant())
                    continue;

                string CurrentName = Line.Substring(0, Line.IndexOf('=')).Trim().ToLowerInvariant();

                GroupEnd = i;

                if (CurrentName != Name.Trim().ToLowerInvariant())
                    continue;

                IniLines[i] = $"{Name}={Value}";
                return;
            }

            List<string> NewLines = new List<string>(IniLines);
            if (GroupBegin >= 0)
            {
                if (GroupEnd + 1 < IniLines.Length)
                    NewLines.Insert(GroupEnd + 1, $"{Name}={Value}");
                else
                    NewLines.Add($"{Name}={Value}");
            }
            else
            {
                if (NewLines.Count > 0 && NewLines.Last().Trim() != string.Empty)
                    NewLines.Add(string.Empty);

                NewLines.Add($"[{Group}]");
                NewLines.Add($"{Name}={Value}");
            }
            IniLines = NewLines.ToArray();
        }

        bool Updated = true;
        public void Save() {
            if (Updated)
                return;

            Updated = true;
            System.IO.File.WriteAllLines(File, IniLines);
        }
    }
}
