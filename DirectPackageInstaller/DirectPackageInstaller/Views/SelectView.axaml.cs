using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class SelectView : UserControl
    {
        private Action<string> OnSelectionChanged = null;
        private SelectViewModel? Model => (SelectViewModel?) DataContext;
        private Select Parent;
        public SelectView()
        {
            InitializeComponent();

            Items = this.Find<ComboBox>("Items");
            Items.SelectionChanged += ItemsOnSelectionChanged;
        }

        private void ItemsOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (e?.AddedItems.Count != 1)
                return;

            var Selected = e.AddedItems.Cast<string>().First();
            OnSelectionChanged?.Invoke(Selected);
            
            if (Model != null)
                Model.Result = DialogResult.OK;
        }

        public void Initialize(Select Parent, string[] Options, Action<string> OnSelectionChanged)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => Initialize(Parent, Options, this.OnSelectionChanged));
                return;
            }
            
            this.Parent = Parent;
            this.OnSelectionChanged = OnSelectionChanged;
            
            if (Model == null)
                return;

            Model.Options = Options;
        }
    }
}