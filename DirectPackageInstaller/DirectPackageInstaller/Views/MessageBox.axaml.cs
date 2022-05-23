using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using DirectPackageInstaller.UIBase;
using DirectPackageInstaller.ViewModels;
using Microsoft.CodeAnalysis;

namespace DirectPackageInstaller.Views
{
    public partial class MessageBox : DialogWindow
    {
        private DialogModel? Model => (DialogModel?) DataContext;
        public MessageBox() 
        {
            InitializeComponent();

            View = this.Find<MessageBoxView>("View");
        }

        public static void ShowSync(string Message) =>
            ShowSync(Message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult ShowSync(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon) => 
            ShowSync(null, Message, Title, Buttons, Icon);
        
        public static DialogResult ShowSync(Window? Parent, string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)
        {
            MessageBox MB = new MessageBox() {
                DataContext = new DialogModel()
                {
                    Icon = Icon,
                    Buttons = Buttons,
                    Message = Message,
                    Title = Title
                }
            };
            
            MB.View.DataContext = MB.DataContext;
            MB.View.Initialize(MB);
            
            MB.ShowDialogSync(Parent);

            return MB.Model?.Result ?? DialogResult.Cancel;
        }

        public static async Task ShowAsync(string Message) =>
            await ShowAsync(Message, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static async Task<DialogResult> ShowAsync(string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon) => 
            await ShowAsync(null, Message, Title, Buttons, Icon);
        
        public static async Task<DialogResult> ShowAsync(Window? Parent, string Message, string Title, MessageBoxButtons Buttons, MessageBoxIcon Icon)
        {
            MessageBox MB = new MessageBox() {
                DataContext = new DialogModel()
                {
                    Icon = Icon,
                    Buttons = Buttons,
                    Message = Message,
                    Title = Title,
                    Result = DialogResult.Cancel
                }
            };
            
            MB.View.DataContext = MB.DataContext;
            MB.View.Initialize(MB);
            
            await MB.ShowDialogAsync(Parent);
            
            return MB.Model?.Result ?? DialogResult.Cancel;
        }
    }
}