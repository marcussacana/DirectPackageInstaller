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

    public bool IsPhone { get; set; }
    public bool IsSDCard { get; set; }

    public bool IsPKG => !IsDirectory && (Name.EndsWith(".pkg", StringComparison.InvariantCultureIgnoreCase) ||
                                          Name.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase));
    public bool IsDirectory => FullPath.EndsWith("\\") || FullPath.EndsWith("/");

    public string Name
    {
        get
        {
            if (IsPhone)
                return "Internal Memory";
            if (IsSDCard)
                return "SD Card";
            
            return Path.GetFileName(FullPath.TrimEnd('/', '\\'));
        }
    }

    public string FullPath { get; }
}