using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DirectPackageInstaller
{
    public class LanguageInfo
    {
        /// <summary>Pack id, always the file name without extension.</summary>
        public string Name = null!;
        /// <summary>Name as written by its own speakers, shown in the menu.</summary>
        public string NativeName = null!;
        /// <summary>The [Meta] Name, only used to tell same named packs apart.</summary>
        public string EnglishName = null!;
        public string File = null!;

        public override string ToString() => NativeName;
    }

    /// <summary>
    /// Plain INI based localization, the language packs are loose files inside the
    /// "language" folder next to the Settings.ini, this way the user can add, remove
    /// or edit any translation without recompiling the app. Nothing is hardcoded here,
    /// dropping a new *.ini into the folder is all it takes to get a new language.
    ///
    /// The file layout is:
    /// [Meta]
    /// Name=English
    /// NativeName=English
    ///
    /// [Language]
    /// Menu.Options=Options
    /// </summary>
    public static class Language
    {
        /// <summary>The only language that isn't discovered, it is the fallback of every other pack.</summary>
        public const string Default = "English";
        public const string DirectoryName = "language";

        const string Group = "Language";
        const string Meta = "Meta";

        static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string? Current { get; private set; }

        /// <summary>
        /// Raised after <see cref="Load"/> applied a new language, used to re-apply
        /// the strings that are already on the screen.
        /// </summary>
        public static event EventHandler? Loaded;

        public static string Directory
        {
            get
            {
                try
                {
                    return Path.Combine(App.WorkingDirectory, DirectoryName);
                }
                catch
                {
                    return Path.Combine(AppContext.BaseDirectory, DirectoryName);
                }
            }
        }

        public static string? LanguagePath => Current == null ? null : GetLanguageFile(Current);

        public static string GetLanguageFile(string? Name)
        {
            if (string.IsNullOrWhiteSpace(Name))
                Name = Default;

            return Path.Combine(Directory, $"{Name!.Trim()}.ini");
        }

        /// <summary>
        /// Every *.ini file inside the language folder. The folder is scanned on each
        /// call, so a pack dropped in while the app is running is picked up without a
        /// restart. A file that carries no translated strings is not a pack, so it is
        /// skipped instead of showing up as a bogus entry.
        /// </summary>
        public static List<LanguageInfo> Available
        {
            get
            {
                List<LanguageInfo> Result = new List<LanguageInfo>();

                string Path = Directory;

                if (!System.IO.Directory.Exists(Path))
                    return Result;

                foreach (string File in System.IO.Directory.GetFiles(Path, "*.ini").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    //The file name is the pack id, so a translator that copies a pack
                    //and forgets to update [Meta] Name still gets a working language.
                    var Name = System.IO.Path.GetFileNameWithoutExtension(File);

                    try
                    {
                        var Ini = new Ini(File, Group);

                        //A pack without a single translated string has nothing to show,
                        //so it's treated as not a pack, same as plain text or a stray
                        //settings backup that happens to sit in the folder.
                        if (Ini.GetValues(Group)?.Count > 0 != true)
                            continue;

                        var MetaName = Ini.GetValue("Name", Meta);
                        var NativeName = Ini.GetValue("NativeName", Meta);

                        Result.Add(new LanguageInfo()
                        {
                            Name = Name,
                            EnglishName = MetaName ?? Name,
                            NativeName = NativeName ?? MetaName ?? Name,
                            File = File
                        });
                    }
                    catch { }
                }

                return Result;
            }
        }

        /// <summary>
        /// The available packs together with the label to show for each of them. Two
        /// packs may declare the same NativeName, the pack id is appended to the extra
        /// ones so the entries stay distinguishable and map back unambiguously.
        /// </summary>
        public static List<KeyValuePair<string, LanguageInfo>> Labeled()
        {
            var Result = new List<KeyValuePair<string, LanguageInfo>>();

            var Taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var Entry in Available)
            {
                string Label = Entry.NativeName;

                if (Taken.Contains(Label))
                    Label = $"{Label} ({Entry.Name})";

                Taken.Add(Label);

                Result.Add(new KeyValuePair<string, LanguageInfo>(Label, Entry));
            }

            return Result;
        }

        static Language()
        {
            Load(null, true);
        }

        /// <summary>
        /// Switches the app to the given language, a null/empty/invalid name falls
        /// back to the default english pack. Returns the name actually loaded.
        /// </summary>
        public static string? Load(string? Name, bool Silent = false)
        {
            if (string.IsNullOrWhiteSpace(Name))
                Name = Default;

            Name = Name!.Trim();

            if (!File.Exists(GetLanguageFile(Name)))
                Name = Default;

            var LanguageFile = GetLanguageFile(Name);

            try
            {
                var Ini = new Ini(LanguageFile, Group);
                var Entries = Ini.GetValues(Group) ?? new Dictionary<string, string>();

                lock (Strings)
                {
                    Strings.Clear();
                    foreach (var Entry in Entries)
                        Strings[Entry.Key] = Unescape(Entry.Value);
                }

                if (!Name.Equals(Default, StringComparison.OrdinalIgnoreCase))
                    LoadFallback();
                else
                    lock (Fallback)
                    {
                        Fallback.Clear();
                        foreach (var Entry in Strings)
                            Fallback[Entry.Key] = Entry.Value;
                    }

                Current = Name;

                if (!Silent)
                    Loaded?.Invoke(null, EventArgs.Empty);

                return Name;
            }
            catch
            {
                return Current;
            }
        }

        static void LoadFallback()
        {
            try
            {
                var Ini = new Ini(GetLanguageFile(Default), Group);
                var Entries = Ini.GetValues(Group) ?? new Dictionary<string, string>();

                lock (Fallback)
                {
                    Fallback.Clear();
                    foreach (var Entry in Entries)
                        Fallback[Entry.Key] = Unescape(Entry.Value);
                }
            }
            catch { }
        }

        /// <summary>
        /// Returns the string of the given key, missing keys fall back to english and,
        /// when the key is unknown everywhere, the key itself is returned so the missing
        /// translation is easy to spot.
        /// </summary>
        public static string L(string Key)
        {
            if (string.IsNullOrWhiteSpace(Key))
                return string.Empty;

            lock (Strings)
            {
                if (Strings.TryGetValue(Key, out string? Value) && Value.Length > 0)
                    return Value;
            }

            lock (Fallback)
            {
                if (Fallback.TryGetValue(Key, out string? Value) && Value.Length > 0)
                    return Value;
            }

            return Key;
        }

        /// <summary>
        /// Same as <see cref="L"/> but applies the {0}, {1}... placeholders of the
        /// translated string, so the argument order can be adapted per language.
        /// </summary>
        public static string F(string Key, params object?[] Args)
        {
            string Text = L(Key);

            if (Args.Length == 0)
                return Text;

            try
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, Text, Args);
            }
            catch
            {
                return Text;
            }
        }

        static string Unescape(string? Value)
        {
            if (string.IsNullOrEmpty(Value))
                return string.Empty;

            return Value!.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
        }
    }
}
