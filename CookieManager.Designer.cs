
namespace DirectPackageInstaller
{
    partial class CookieManager
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CookieManager));
            this.tbCookies = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // tbCookies
            // 
            this.tbCookies.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbCookies.Location = new System.Drawing.Point(12, 12);
            this.tbCookies.Multiline = true;
            this.tbCookies.Name = "tbCookies";
            this.tbCookies.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.tbCookies.Size = new System.Drawing.Size(776, 426);
            this.tbCookies.TabIndex = 0;
            this.tbCookies.Text = resources.GetString("tbCookies.Text");
            this.tbCookies.WordWrap = false;
            this.tbCookies.TextChanged += new System.EventHandler(this.tbCookies_TextChanged);
            // 
            // CookieManager
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.tbCookies);
            this.Name = "CookieManager";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Cookie Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.CookieManager_FormClosing);
            this.Shown += new System.EventHandler(this.CookieManager_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbCookies;
    }
}