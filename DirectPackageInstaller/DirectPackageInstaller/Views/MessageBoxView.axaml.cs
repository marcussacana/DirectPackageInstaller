using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views;

public partial class MessageBoxView : UserControl
{ 
    const string OK = "Ok"; 
    const string Yes = "Yes"; 
    const string No = "No"; 
    const string Cancel = "Cancel"; 
    const string Retry = "Retry"; 
    const string Ignore = "Ignore"; 
    const string Abort = "Abort"; 
    private DialogModel? Model => (DialogModel?) DataContext;

    public MessageBoxView()
    {
        InitializeComponent();

        ButtonA = this.Find<Button>("ButtonA");
        ButtonB = this.Find<Button>("ButtonB");
        ButtonC = this.Find<Button>("ButtonC");
        ButtonD = this.Find<Button>("ButtonD");

        Icon = this.Find<Image>("Icon");
        
        ButtonA.Click += BtnClicked;
        ButtonB.Click += BtnClicked;
        ButtonC.Click += BtnClicked;
        ButtonD.Click += BtnClicked;

        KeyDown += OnKeyDown;

        AttachedToVisualTree += (sender, args) =>
        {
            ((UserControl?)sender)?.Focus();
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Model!.Buttons.HasFlag(MessageBoxButtons.OK))
            {
                e.Handled = true;
                Model.Result = DialogResult.OK;
                Close();
                return;
            }
            if (Model!.Buttons.HasFlag(MessageBoxButtons.Yes))
            {
                e.Handled = true;
                Model.Result = DialogResult.Yes;
                Close();
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            if (Model!.Buttons.HasFlag(MessageBoxButtons.Cancel))
            {
                e.Handled = true;
                Model.Result = DialogResult.Cancel;
                Close();
                return;
            }
            
            if (Model!.Buttons.HasFlag(MessageBoxButtons.No))
            {
                e.Handled = true;
                Model.Result = DialogResult.No;
                Close();
                return;
            }
        }
    }

    private Window? Window;
    public void Initialize(Window? Window)
    {
        ButtonA.IsVisible = false;
        ButtonB.IsVisible = false;
        ButtonC.IsVisible = false;
        ButtonD.IsVisible = false;
        
        if (Model == null)
            return;

        this.Window = Window;

        List<string> Buttons = new List<string>();
        if (Model.Buttons.HasFlag(MessageBoxButtons.OK))
            Buttons.Add(OK);
        if (Model.Buttons.HasFlag(MessageBoxButtons.Yes))
            Buttons.Add(Yes);
        if (Model.Buttons.HasFlag(MessageBoxButtons.No))
            Buttons.Add(No);
        if (Model.Buttons.HasFlag(MessageBoxButtons.Cancel))
            Buttons.Add(Cancel);
        if (Model.Buttons.HasFlag(MessageBoxButtons.Retry))
            Buttons.Add(Retry);
        if (Model.Buttons.HasFlag(MessageBoxButtons.Ignore))
            Buttons.Add(Ignore);
        if (Model.Buttons.HasFlag(MessageBoxButtons.Abort))
            Buttons.Add(Abort);
        
       Buttons.Reverse(); 

        if (Buttons.Count >= 4)
            throw new Exception("Too Many Buttons");

        for (int i = 0; i < Buttons.Count; i++)
        {
            var Text = Translate(Buttons[i]);
            var Result = GetResult(Buttons[i]);

            Button Target = i switch
            {
                0 => ButtonA,
                1 => ButtonB,
                2 => ButtonC,
                3 => ButtonD,
                _ => throw new Exception("Too Many Buttons")
            };

            Target.Content = Text;
            Target.Tag = Result;
            Target.IsVisible = true;
        }

        Icon.Classes.Clear();
        
        switch (Model.Icon)
        {
            case MessageBoxIcon.Stop:
                Icon.Classes.Add("Stop");
                break;
            case MessageBoxIcon.Error:
                Icon.Classes.Add("Error");
                break;
            case MessageBoxIcon.Information:
                Icon.Classes.Add("Information");
                break;
            case MessageBoxIcon.Question:
                Icon.Classes.Add("Question");
                break;
            case MessageBoxIcon.Warning:
                Icon.Classes.Add("Warning");
                break;
            case MessageBoxIcon.None:
                Icon.Classes.Add("None");
                break;
        }
    }
    
    private void BtnClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button Btn || Model == null)
            return;

        if (Btn.Tag is not DialogResult Result)
            return;

        Model.Result = Result;

        Close();
    }

    static string Translate(string InternalCaption) => InternalCaption switch
    {
        OK => Language.L("Button.Ok"),
        Yes => Language.L("Button.Yes"),
        No => Language.L("Button.No"),
        Cancel => Language.L("Button.Cancel"),
        Retry => Language.L("Button.Retry"),
        Ignore => Language.L("Button.Ignore"),
        Abort => Language.L("Button.Abort"),
        _ => InternalCaption
    };

    static DialogResult GetResult(string InternalCaption) => InternalCaption switch
    {
        OK => DialogResult.OK,
        Yes => DialogResult.Yes,
        No => DialogResult.No,
        Cancel => DialogResult.Cancel,
        Retry => DialogResult.Retry,
        Ignore => DialogResult.Ignore,
        Abort => DialogResult.Abort,
        _ => throw new Exception($"Unknown Dialog Caption: {InternalCaption}")
    };

    private void Close()
    {
        App.Callback(() =>
        {
            if (Window == null)
            {
                SingleView.ReturnView(this);
            }
            else
            {
                Window.Hide();
                App.Callback(Window.Close);
            }
        });
    }
}