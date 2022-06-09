using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views;

public partial class SingleView : UserControl
{
    public SingleView()
    {
        InitializeComponent();
        Initialized += OnInitialized;

        Content = this.Find<DockPanel>("Content");
        Popup = this.Find<Border>("Popup");
        PopupContent = this.Find<DockPanel>("PopupContent");

        LifetimeView = this;
    }

    public MainView Main;

    private async void OnInitialized(object? sender, EventArgs e)
    {
        Main = new MainView() {
            DataContext = new MainViewModel()
        };
        
        _ = CallView(Main, false);
        
        await Main.OnShown(null);
    }

    static SingleView LifetimeView;
    private Stack<UserControl> ViewStack = new Stack<UserControl>();
    private Stack<TaskCompletionSource> AwaitStack = new Stack<TaskCompletionSource>();
    private Stack<bool> PopupStack = new Stack<bool>();

    public static async Task CallView(UserControl View, bool Popup)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await CallView(View, Popup));
            return;
        }
        
        var CompletionSource = new TaskCompletionSource();
        
        LifetimeView.AwaitStack.Push(CompletionSource);
        LifetimeView.PopupStack.Push(Popup);
        LifetimeView.ViewStack.Push(View);

        if (Popup)
        {
            LifetimeView.Popup.IsVisible = true;
            LifetimeView.PopupContent.Children.Clear();
            LifetimeView.PopupContent.Children.Add(View);
        }
        else
        {
            LifetimeView.Popup.IsVisible = false;
            LifetimeView.Content.Children.Clear();
            LifetimeView.Content.Children.Add(View);
        }

        await CompletionSource.Task;
    }

    public static void ReturnView(UserControl View)
    {
        if (!LifetimeView.ViewStack.Contains(View))
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.InvokeAsync(() => ReturnView(View)).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
        }
        
        UserControl DroppedView = null;
        while (!Equals(View, DroppedView))
        {
            if (LifetimeView.ViewStack.Count <= 1)
            {
                LifetimeView.Content.Children.Clear();
                LifetimeView.Content.Children.Add(LifetimeView.Main);
                return;
            }
            
            DroppedView = LifetimeView.ViewStack.Pop();
            LifetimeView.PopupStack.Pop();
            
            var NextView = LifetimeView.ViewStack.Peek();
            var Popup = LifetimeView.PopupStack.Peek();
            var Completation = LifetimeView.AwaitStack.Pop();

            if (Popup)
            {
                LifetimeView.Popup.IsVisible = true;
                LifetimeView.PopupContent.Children.Clear();
                LifetimeView.PopupContent.Children.Add(NextView);

                for (int i = LifetimeView.ViewStack.Count - 1; i >= 0; i--)
                {
                    if (LifetimeView.PopupStack.ElementAt(i))
                        continue;
                    
                    var BGView = LifetimeView.ViewStack.ElementAt(i);
                    LifetimeView.Content.Children.Clear();
                    LifetimeView.Content.Children.Add(BGView);
                    break;
                }
            }
            else
            {
                LifetimeView.Popup.IsVisible = false;
                LifetimeView.Content.Children.Clear();
                LifetimeView.Content.Children.Add(NextView);
            }

            Completation.SetResult();
        }
    }

}