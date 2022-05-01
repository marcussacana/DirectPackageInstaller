using Avalonia.Controls;
using DirectPackageInstaller.ViewModels;

namespace DirectPackageInstaller.Views
{
    public partial class LinkList : Window
    {
        public string[] Links { get; private set; }
        public string Password { get; private set; }
        public LinkList(bool Multipart, bool Encrypted, string FirstUrl) : this()
        {
            // tbLinks.Enabled = Multipart;
            // tbPassword.Enabled = Encrypted;
            // MainUrl = FirstUrl;
            // DialogResult = DialogResult.Cancel;
        }

        public LinkList()
        {
            InitializeComponent();
        }

        public DialogResult ShowDialog()
        {
            ShowDialog(null);
            return ((LinkListViewModel)DataContext).Result;
        }
    }
}