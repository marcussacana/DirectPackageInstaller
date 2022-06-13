using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using DirectPackageInstaller.Others;
using ReactiveUI;

namespace DirectPackageInstaller.ViewModels;

public class FilePickerModel : DialogModel
{
    private bool _Multiselect = false;
    public bool InMultiselect
    {
        get => _Multiselect;
        set => this.RaiseAndSetIfChanged(ref _Multiselect, value);
    }
    public ObservableCollection<FileEntry> CurrentDirEntries { get; } = new ObservableCollection<FileEntry>();

    public string[]? CurrentSubDirs
    {
        get => CurrentDirEntries?.Where(x => x.IsDirectory).Select(x => x.FullPath).ToArray();
        set => this.RaisePropertyChanged();
    }

    private string? _CurrentDir;
    public string? CurrentDir
    {
        get => _CurrentDir;
        set => this.RaiseAndSetIfChanged(ref _CurrentDir, value);
    }
}