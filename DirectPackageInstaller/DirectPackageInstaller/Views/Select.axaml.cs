using System;
using Avalonia.Controls;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class Select : DialogWindow
    {
        private string[] Choices;
        public string Choice { get; private set; }

        public Select(string[] Choices) : this()
        {
            this.Choices = Choices;
        }

        public Select()
        {
            InitializeComponent();
            
            if (DataContext == null)
                DataContext = new SelectViewModel();

            View = this.Find<SelectView>("View");
            View.DataContext = DataContext;
            
            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            View.Initialize(this, Choices, (Item) => Choice = Item);
        }
    }
}