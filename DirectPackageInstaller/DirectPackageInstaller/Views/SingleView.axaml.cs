using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views;

public partial class SingleView : UserControl
{
    public SingleView()
    {
        InitializeComponent();
        Initialized += OnInitialized;

        Content = this.Find<Panel>("Content");

        LifetimeView = this;
    }

    public MainView Main;

    private async void OnInitialized(object? sender, EventArgs e)
    {
        Main = new MainView() {
            DataContext = new MainViewModel()
        };
        
        _ = CallView(Main);
        
        await Main.OnShown(null);
    }

    static SingleView LifetimeView;
    private Stack<UserControl> ViewStack = new Stack<UserControl>();
    private Stack<TaskCompletionSource> AwaitStack = new Stack<TaskCompletionSource>();

    public static async Task CallView(UserControl View)
    {
        var CompletionSource = new TaskCompletionSource();
        
        LifetimeView.AwaitStack.Push(CompletionSource);
        
        LifetimeView.ViewStack.Push(View);
        
        LifetimeView.Content.Children.Clear();
        LifetimeView.Content.Children.Add(View);

        await CompletionSource.Task;
    }

    public static void ReturnView(UserControl View)
    {
        UserControl CurrentView = null;
        while (!Equals(View, CurrentView))
        {
            if (LifetimeView.ViewStack.Count == 0)
            {
                LifetimeView.Content.Children.Clear();
                LifetimeView.Content.Children.Add(LifetimeView.Main);
                return;
            }
            
            LifetimeView.ViewStack.Pop();
            
            CurrentView = LifetimeView.ViewStack.Peek();
            
            LifetimeView.Content.Children.Clear();
            LifetimeView.Content.Children.Add(CurrentView);

            LifetimeView.AwaitStack.Pop().SetResult();
        }
    }

}