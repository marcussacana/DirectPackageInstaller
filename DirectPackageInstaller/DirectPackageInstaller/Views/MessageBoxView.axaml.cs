using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
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
    }

    private Window Window;
    public void Initialize(Window Window)
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
            var Text = Buttons[i];
            switch (i)
            {
                case 0:
                    ButtonA.Content = Text;
                    ButtonA.IsVisible = true;
                    break;
                case 1:
                    ButtonB.Content = Text;
                    ButtonB.IsVisible = true;
                    break;
                case 2:
                    ButtonC.Content = Text;
                    ButtonC.IsVisible = true;
                    break;
                case 3:
                    ButtonD.Content = Text;
                    ButtonD.IsVisible = true;
                    break; 
            }
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
        if (sender == null || Model == null)
            return;
        
        Button Btn = (Button) sender;

        switch (Btn.Content as string)
        {
            case OK:
                Model.Result = DialogResult.OK;
                break;
            case Yes:
                Model.Result = DialogResult.Yes;
                break;
            case No:
                Model.Result = DialogResult.No;
                break;
            case Cancel:
                Model.Result = DialogResult.Cancel;
                break;
            case Retry:
                Model.Result = DialogResult.Retry;
                break;
            case Ignore:
                Model.Result = DialogResult.Ignore;
                break;
            case Abort:
                Model.Result = DialogResult.Abort;
                break;
            default:
                return;
        }
        
        Window.Close();
    }
}