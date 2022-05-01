using System;
using Avalonia.Controls;

namespace DirectPackageInstaller.Views
{
    public partial class MessageBox : Window
    {
        public MessageBox()
        {
            InitializeComponent();
        }

        public static DialogResult Show(string Message) =>
            Show(Message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon) => 
            Show(null, Message, Title, Buttons, Icon);
        
        public static DialogResult Show(Window Parent, string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)
        {
            throw new NotImplementedException();
        }
    }
}