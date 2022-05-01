using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class Select : Window
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
        }

        public DialogResult ShowDialog()
        {
            ShowDialog(null);

            return ((SelectViewModel) DataContext).Result;
        }
    }
}