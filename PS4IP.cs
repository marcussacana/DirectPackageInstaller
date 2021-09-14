using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    class PS4IP : Form
    {
        public string IP { get; set; }
        public PS4IP() {
            DialogResult = DialogResult.Cancel;
            InitializeComponent();
        }
        public PS4IP(string IP) : this() {
            this.IP = IP;
            Shown += PS4IP_Shown;
        }

        private void PS4IP_Shown(object sender, EventArgs e)
        {
            Text += " (Default: " + IP + ")";
            tbIP.Text = IP;
        }

        private TextBox tbIP;
        private Button btnOK;
        private Label lblIP;

        private void InitializeComponent()
        {
            this.lblIP = new System.Windows.Forms.Label();
            this.tbIP = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblIP
            // 
            this.lblIP.AutoSize = true;
            this.lblIP.Location = new System.Drawing.Point(12, 9);
            this.lblIP.Name = "lblIP";
            this.lblIP.Size = new System.Drawing.Size(54, 17);
            this.lblIP.TabIndex = 0;
            this.lblIP.Text = "PS4 IP:";
            // 
            // tbIP
            // 
            this.tbIP.Location = new System.Drawing.Point(72, 6);
            this.tbIP.Name = "tbIP";
            this.tbIP.Size = new System.Drawing.Size(365, 22);
            this.tbIP.TabIndex = 1;
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
            this.Controls.Add(this.tbIP);
            this.Controls.Add(this.lblIP);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "PS4IP";
            this.Text = "Confirm your PS4 IP";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (!IPAddress.TryParse(tbIP.Text, out _)) {
                MessageBox.Show("Invalid IP Address", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DialogResult = DialogResult.OK;
            IP = tbIP.Text;
            Close();
        }
    }
}
