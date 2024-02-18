using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using DirectPackageInstaller.IO;
using LibOrbisPkg.SFO;

namespace DirectPackageInstaller;

public static class Extensions
{
    [DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr GetHandle(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern long Int64_objc_msgSend_IntPtr(
        IntPtr receiver,
        IntPtr selector,
        IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void Void_objc_msgSend(
        IntPtr receiver,
        IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr IntPtr_objc_msgSend(IntPtr receiver, IntPtr selector);

    public static void ShowDialogSync(this Window window, Window owner)
    {
        window.ShowDialogSync<object>(owner);
    }

    [return: MaybeNull]
    public static T ShowDialogSync<T>(this Window window, Window owner)
    {
        if (window.TryGetPlatformHandle() is IMacOSTopLevelPlatformHandle handle)
        {
            var nsAppStaticClass = objc_getClass("NSApplication");
            var sharedApplicationSelector = GetHandle("sharedApplication");
            var sharedApplication = IntPtr_objc_msgSend(nsAppStaticClass, sharedApplicationSelector);
            var runModalForSelector = GetHandle("runModalForWindow:");
            var stopModalSelector = GetHandle("stopModal");

            void DialogClosed(object sender, EventArgs e)
            {
                Void_objc_msgSend(sharedApplication, stopModalSelector);
                window.Closed -= DialogClosed;
            }

            window.Closed += DialogClosed;
            var task = window.ShowDialog<T>(owner);
            Int64_objc_msgSend_IntPtr(sharedApplication, runModalForSelector, handle.NSWindow);
            return task.Result;
        }
        else
        {
            using var source = new CancellationTokenSource();
            var result = default(T);
            window.ShowDialog<T>(owner).ContinueWith(
                t =>
                {
                    if (t.IsCompletedSuccessfully)
                        result = t.Result;
                    source.Cancel();
                },
                TaskScheduler.FromCurrentSynchronizationContext());
            Dispatcher.UIThread.MainLoop(source.Token);
            return result;
        }
    }

    public static T CreateInstance<T>(object DataContext) where T : UserControl, new()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(() => CreateInstance<T>(DataContext)).Result;

        return new T()
        {
            DataContext = DataContext
        };
    }

    public static bool IsValidURL(this string URL)
    {
        var Escaped = URL.Replace("[", "%5B").Replace("]", "%5D");
        return Uri.IsWellFormedUriString(Escaped, UriKind.Absolute);
    }
    public static bool HasName(this ParamSfo This, string name)
    {
        foreach (var v in This.Values)
        {
            if (v.Name == name) return true;
        }
        return false;
    }

    public static string? TryGetRemoteFileName(this Stream Stream)
    {
        var Source = Stream;
        if (Source is DecompressorHelperStream decomp)
            Source = decomp.Base;

        if (Source is PartialHttpStream httpStream)
            return httpStream.Filename ?? Path.GetFileName(httpStream.FinalURL);
        return null;
    }

    public static bool IsFilePath(this string Path)
    {
        return Path.Length > 2 && (Path[1] == ':' || Path[0] == '/' || Path.StartsWith("\\\\"));
    }

    public static string? Substring(this string String, string Substring)
    {
        if (!String.Contains(Substring))
            return null;

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