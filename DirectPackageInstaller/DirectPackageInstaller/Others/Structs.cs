using System;
using System.IO;
using System.Reflection.Metadata;
using System.Windows.Input;
using Avalonia.Controls;

namespace DirectPackageInstaller.Others;

public class FileEntry
{
    public FileEntry(string FullPath, bool IsDirectory)
    {
        if (IsDirectory && !FullPath.EndsWith("\\") && !FullPath.EndsWith("/"))
            FullPath += Path.DirectorySeparatorChar;
        
        this.FullPath = FullPath;
    }

    public bool IsPKG => !IsDirectory && Name.EndsWith(".pkg", StringComparison.InvariantCultureIgnoreCase);
    public bool IsDirectory => FullPath.EndsWith("\\") || FullPath.EndsWith("/");
    public string Name => Path.GetFileName(FullPath.TrimEnd('/', '\\'));
    public string FullPath { get; }
}