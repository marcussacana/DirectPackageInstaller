using System;
using System.Net;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    class InputWindow : Form
    {
        public string Value { get; set; }
        
        InputWindow() {
            DialogResult = DialogResult.Cancel;
            InitializeComponent();
        }

        public InputWindow(string Title, string Label) : this() {
            Text = Title;
            lblValue.Text = Label;

            Shown += InputWindow_Shown;
        }

        private void InputWindow_Shown(object sender, EventArgs e)
        {
            tbValue.Text = Value;
            Focus();
        }

        private TextBox tbValue;
        private Button btnOK;
        private Label lblValue;

        private void InitializeComponent()
        {
            this.lblValue = new System.Windows.Forms.Label();
            this.tbValue = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblValue
            // 
            this.lblValue.AutoSize = true;
            this.lblValue.Location = new System.Drawing.Point(12, 9);
            this.lblValue.Name = "lblValue";
            this.lblValue.Size = new System.Drawing.Size(54, 17);
            this.lblValue.TabIndex = 0;
            // 
            // tbValue
            // 
            this.tbValue.Location = new System.Drawing.Point(72, 6);
            this.tbValue.Name = "tbValue";
            this.tbValue.Size = new System.Drawing.Size(365, 22);
            this.tbValue.TabIndex = 1;
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(443, 6);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 2;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // PS4IP
            // 
            this.ClientSize = new System.Drawing.Size(530, 44);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbValue);
            this.Controls.Add(this.lblValue);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "InputWindow";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Value = tbValue.Text;
            Close();
        }

        public static string AskIP(string Message) {
            using var Window = new PS4IP();
            Window.Text = Message; 
            if (Window.ShowDialog() == DialogResult.OK)
                return Window.IP;
            return null;
        }
    }
}
