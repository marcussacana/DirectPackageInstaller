using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DirectPackageInstaller.Controls;
using DirectPackageInstaller.Others;
using DirectPackageInstaller.ViewModels;
using DynamicData;

namespace DirectPackageInstaller.Views;

public partial class FilePicker : UserControl
{
    public List<string> SelectedFiles { get; set; } = new List<string>();
    private FilePickerModel Model => (FilePickerModel)DataContext!;
    public FilePicker()
    {
        InitializeComponent();

        DataContext ??= new FilePickerModel();

        btnBack = this.Find<Button>("btnBack");
        btnNext = this.Find<Button>("btnNext");
        btnCancel = this.Find<Button>("btnCancel");
        
        tbFolder = this.Find<AutoCompleteBox>("tbFolder");
        lbEntries = this.Find<ItemsControl>("lbEntries");
        
        btnBack.Click += BtnBackOnClick;
        btnNext.Click += BtnNextOnClick;
        btnCancel.Click += BtnCancelOnClick;
        
        tbFolder.TextChanged += TbFolderOnTextChanged;
        
        Model.Result = DialogResult.Cancel;
    }

    private void BtnCancelOnClick(object? sender, RoutedEventArgs e)
    {
        Model.Result = DialogResult.Cancel;
        SelectedFiles.Clear();
        SingleView.ReturnView(this);
    }

    private void TbFolderOnTextChanged(object? sender, EventArgs e)
    {
        OpenDir(tbFolder.Text);
    }

    private string? LastDir;

    public Stack<string> BckDirs = new Stack<string>();
    public Stack<string> NxtDirs = new Stack<string>();
    
    public void OpenDir(string Path)
    {
        if (!Directory.Exists(Path))
            return;

        if (!Path.EndsWith("\\") && !Path.EndsWith("/"))
            tbFolder.SelectedItem = Path += System.IO.Path.DirectorySeparatorChar;

        if (Path == LastDir)
            return;

        SelectedFiles.Clear();
        Model.InMultiselect = false;
        
        string[] SubDirs = Directory.GetDirectories(Path);
        string[] Files = Directory.GetFiles(Path, "*", SearchOption.TopDirectoryOnly);

        List<FileEntry> Entries = new List<FileEntry>();

        Entries.AddRange(SubDirs.Select(x => new FileEntry(x, true)).OrderBy(x => x.Name));
        Entries.AddRange(Files.Select(x => new FileEntry(x, false)).OrderBy(x => x.Name));


        if (Path != LastDir && LastDir != null)
        {
            if (!NxtDirs.TryPeek(out var NextDir) || NextDir != LastDir)
            {
                if (!BckDirs.TryPeek(out var RetDir) || RetDir != LastDir)
                {
                    BckDirs.Push(LastDir);
                    NxtDirs.Clear();
                }
            }
        }

        Model.CurrentDir = LastDir = Path;
        
        Model.CurrentDirEntries.Clear();
        Model.CurrentDirEntries.AddRange(Entries);

        App.Callback(() => Model.CurrentSubDirs = null);
    }

    private void BtnNextOnClick(object? sender, RoutedEventArgs e)
    {
        if (NxtDirs.Count > 0)
        {
            LastDir = Model.CurrentDir;
            BckDirs.Push(LastDir);
            OpenDir(NxtDirs.Pop());
        }
    }

    private void BtnBackOnClick(object? sender, RoutedEventArgs e)
    {
        if (BckDirs.Count > 0)
        {
            LastDir = Model.CurrentDir;
            NxtDirs.Push(LastDir);
            OpenDir(BckDirs.Pop());
        }
    }

    public void FileEntry_OnClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not HoldToToggleButton Button)
            return;

        if (SelectedFiles.Count > 0)
        {
            Button.Toggle();
            return;
        }

        if (Button.DataContext is not FileEntry Entry)
            return;

        if (Entry.IsPKG)
        {
            Model.Result = DialogResult.OK;
            SelectedFiles = new List<string>(new [] { Entry.FullPath });
            SingleView.ReturnView(this);
            return;
        }

        if (Entry.IsDirectory)
            OpenDir(Entry.FullPath);
    }

    private void FileEntry_OnChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not HoldToToggleButton Button)
            return;

        if (Button.DataContext is FileEntry {IsPKG: true} Entry)
            SelectedFiles.Add(Entry.FullPath);
        else
            Button.Toggle();

        Model.InMultiselect = SelectedFiles.Count > 0;
    }

    private void FileEntry_OnUnchecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not HoldToToggleButton Button)
            return;
        
        if (Button.DataContext is FileEntry Entry && SelectedFiles.Contains(Entry.FullPath))
            SelectedFiles.Remove(Entry.FullPath);
        
        Model.InMultiselect = SelectedFiles.Count > 0;
    }

    private void Open_OnClick(object? sender, RoutedEventArgs e)
    {
        Model.Result = DialogResult.OK;
        SingleView.ReturnView(this);
    }
}