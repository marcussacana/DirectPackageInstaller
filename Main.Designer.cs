
namespace DirectPackageInstaller
{
    partial class Main
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique 
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            this.btnLoadUrl = new System.Windows.Forms.Button();
            this.tbURL = new System.Windows.Forms.TextBox();
            this.lblURL = new System.Windows.Forms.Label();
            this.StatusStrip = new System.Windows.Forms.StatusStrip();
            this.lblStatus = new System.Windows.Forms.ToolStripStatusLabel();
            this.IconBox = new System.Windows.Forms.PictureBox();
            this.ParamList = new System.Windows.Forms.ListView();
            this.ParamColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ValueColumn = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.SplitPanel = new System.Windows.Forms.SplitContainer();
            this.MenuStrip = new System.Windows.Forms.MenuStrip();
            this.miOptions = new System.Windows.Forms.ToolStripMenuItem();
            this.miProxyDownloads = new System.Windows.Forms.ToolStripMenuItem();
            this.miSegmentedDownloads = new System.Windows.Forms.ToolStripMenuItem();
            this.miAutoDetectPS4 = new System.Windows.Forms.ToolStripMenuItem();
            this.tbPS4IP = new System.Windows.Forms.ToolStripTextBox();
            this.miRestartServer = new System.Windows.Forms.ToolStripMenuItem();
            this.miPackages = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.miInstallAll = new System.Windows.Forms.ToolStripMenuItem();
            this.OpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.StatusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.IconBox)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.SplitPanel)).BeginInit();
            this.SplitPanel.Panel1.SuspendLayout();
            this.SplitPanel.Panel2.SuspendLayout();
            this.SplitPanel.SuspendLayout();
            this.MenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnLoadUrl
            // 
            this.btnLoadUrl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLoadUrl.Location = new System.Drawing.Point(802, 28);
            this.btnLoadUrl.Name = "btnLoadUrl";
            this.btnLoadUrl.Size = new System.Drawing.Size(90, 23);
            this.btnLoadUrl.TabIndex = 2;
            this.btnLoadUrl.Text = "Open";
            this.btnLoadUrl.UseVisualStyleBackColor = true;
            this.btnLoadUrl.Click += new System.EventHandler(this.btnLoadUrl_Click);
            // 
            // tbURL
            // 
            this.tbURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbURL.Location = new System.Drawing.Point(91, 30);
            this.tbURL.Name = "tbURL";
            this.tbURL.Size = new System.Drawing.Size(705, 22);
            this.tbURL.TabIndex = 1;
            this.tbURL.TextChanged += new System.EventHandler(this.tbURL_TextChanged);
            this.tbURL.DragDrop += new System.Windows.Forms.DragEventHandler(this.FileDragDrop);
            this.tbURL.DragEnter += new System.Windows.Forms.DragEventHandler(this.FileDragEnter);
            this.tbURL.KeyDown += new System.Windows.Forms.KeyEventHandler(this.TbUrlKeyDown);
            // 
            // lblURL
            // 
            this.lblURL.AutoSize = true;
            this.lblURL.Location = new System.Drawing.Point(12, 32);
            this.lblURL.Name = "lblURL";
            this.lblURL.Size = new System.Drawing.Size(73, 17);
            this.lblURL.TabIndex = 2;
            this.lblURL.Text = "PKG URL:";
            // 
            // StatusStrip
            // 
            this.StatusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.StatusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.lblStatus});
            this.StatusStrip.Location = new System.Drawing.Point(0, 389);
            this.StatusStrip.Name = "StatusStrip";
            this.StatusStrip.Size = new System.Drawing.Size(904, 26);
            this.StatusStrip.TabIndex = 3;
            this.StatusStrip.Text = "statusStrip1";
            // 
            // lblStatus
            // 
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(141, 20);
            this.lblStatus.Text = "No Package Loaded";
            // 
            // IconBox
            // 
            this.IconBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.IconBox.Location = new System.Drawing.Point(3, 3);
            this.IconBox.Name = "IconBox";
            this.IconBox.Size = new System.Drawing.Size(326, 320);
            this.IconBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.IconBox.TabIndex = 4;
            this.IconBox.TabStop = false;
            // 
            // ParamList
            // 
            this.ParamList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ParamList.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.ParamColumn,
            this.ValueColumn});
            this.ParamList.FullRowSelect = true;
            this.ParamList.GridLines = true;
            this.ParamList.HideSelection = false;
            this.ParamList.Location = new System.Drawing.Point(3, 3);
            this.ParamList.MultiSelect = false;
            this.ParamList.Name = "ParamList";
            this.ParamList.Size = new System.Drawing.Size(538, 317);
            this.ParamList.TabIndex = 5;
            this.ParamList.UseCompatibleStateImageBehavior = false;
            this.ParamList.View = System.Windows.Forms.View.Details;
            // 
            // ParamColumn
            // 
            this.ParamColumn.Text = "Param";
            this.ParamColumn.Width = 143;
            // 
            // ValueColumn
            // 
            this.ValueColumn.Text = "Value";
            this.ValueColumn.Width = 362;
            // 
            // SplitPanel
            // 
            this.SplitPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.SplitPanel.Location = new System.Drawing.Point(12, 58);
            this.SplitPanel.Name = "SplitPanel";
            // 
            // SplitPanel.Panel1
            // 
            this.SplitPanel.Panel1.Controls.Add(this.IconBox);
            // 
            // SplitPanel.Panel2
            // 
            this.SplitPanel.Panel2.Controls.Add(this.ParamList);
            this.SplitPanel.Size = new System.Drawing.Size(880, 323);
            this.SplitPanel.SplitterDistance = 332;
            this.SplitPanel.TabIndex = 6;
            // 
            // MenuStrip
            // 
            this.MenuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.MenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miOptions,
            this.miPackages});
            this.MenuStrip.Location = new System.Drawing.Point(0, 0);
            this.MenuStrip.Name = "MenuStrip";
            this.MenuStrip.Size = new System.Drawing.Size(904, 28);
            this.MenuStrip.TabIndex = 7;
            // 
            // miOptions
            // 
            this.miOptions.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.miProxyDownloads,
            this.miSegmentedDownloads,
            this.miAutoDetectPS4,
            this.miRestartServer});
            this.miOptions.Name = "miOptions";
            this.miOptions.Size = new System.Drawing.Size(75, 24);
            this.miOptions.Text = "Options";
            // 
            // miProxyDownloads
            // 
            this.miProxyDownloads.CheckOnClick = true;
            this.miProxyDownloads.Name = "miProxyDownloads";
            this.miProxyDownloads.Size = new System.Drawing.Size(247, 26);
            this.miProxyDownloads.Text = "Proxy Downloads";
            // 
            // miSegmentedDownloads
            // 
            this.miSegmentedDownloads.CheckOnClick = true;
            this.miSegmentedDownloads.Name = "miSegmentedDownloads";
            this.miSegmentedDownloads.Size = new System.Drawing.Size(247, 26);
            this.miSegmentedDownloads.Text = "Segmented Downloads";
            this.miSegmentedDownloads.CheckedChanged += new System.EventHandler(this.miSegmentedDownloads_CheckedChanged);
            // 
            // miAutoDetectPS4
            // 
            this.miAutoDetectPS4.CheckOnClick = true;
            this.miAutoDetectPS4.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tbPS4IP});
            this.miAutoDetectPS4.Name = "miAutoDetectPS4";
            this.miAutoDetectPS4.Size = new System.Drawing.Size(247, 26);
            this.miAutoDetectPS4.Text = "Auto Detect PS4";
            this.miAutoDetectPS4.Click += new System.EventHandler(this.miAutoDetectPS4_Click);
            // 
            // tbPS4IP
            // 
            this.tbPS4IP.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.tbPS4IP.Name = "tbPS4IP";
            this.tbPS4IP.Size = new System.Drawing.Size(145, 27);
            this.tbPS4IP.TextChanged += new System.EventHandler(this.tbPS4IP_TextChanged);
            // 
            // miRestartServer
            // 
            this.miRestartServer.Name = "miRestartServer";
            this.miRestartServer.Size = new System.Drawing.Size(247, 26);
            this.miRestartServer.Text = "Restart Server";
            this.miRestartServer.Click += new System.EventHandler(this.miRestartServer_Click);
            // 
            // miPackages
            // 
            this.miPackages.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripSeparator1,
            this.miInstallAll});
            this.miPackages.Name = "miPackages";
            this.miPackages.Size = new System.Drawing.Size(83, 24);
            this.miPackages.Text = "Packages";
            this.miPackages.Visible = false;
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(150, 6);
            // 
            // miInstallAll
            // 
            this.miInstallAll.Name = "miInstallAll";
            this.miInstallAll.Size = new System.Drawing.Size(153, 26);
            this.miInstallAll.Text = "Install All";
            this.miInstallAll.Click += new System.EventHandler(this.miInstallAll_Click);
            // 
            // OpenFileDialog
            // 
            this.OpenFileDialog.Filter = "All PKG Files|*.pkg";
            this.OpenFileDialog.FileOk += new System.ComponentModel.CancelEventHandler(this.OpenFileDialog_FileOk);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(904, 415);
            this.Controls.Add(this.SplitPanel);
            this.Controls.Add(this.StatusStrip);
            this.Controls.Add(this.MenuStrip);
            this.Controls.Add(this.lblURL);
            this.Controls.Add(this.tbURL);
            this.Controls.Add(this.btnLoadUrl);
            this.MainMenuStrip = this.MenuStrip;
            this.Name = "Main";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Direct Package Installer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Closing);
            this.StatusStrip.ResumeLayout(false);
            this.StatusStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.IconBox)).EndInit();
            this.SplitPanel.Panel1.ResumeLayout(false);
            this.SplitPanel.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.SplitPanel)).EndInit();
            this.SplitPanel.ResumeLayout(false);
            this.MenuStrip.ResumeLayout(false);
            this.MenuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnLoadUrl;
        private System.Windows.Forms.TextBox tbURL;
        private System.Windows.Forms.Label lblURL;
        private System.Windows.Forms.StatusStrip StatusStrip;
        private System.Windows.Forms.ToolStripStatusLabel lblStatus;
        private System.Windows.Forms.PictureBox IconBox;
        private System.Windows.Forms.ListView ParamList;
        private System.Windows.Forms.ColumnHeader ParamColumn;
        private System.Windows.Forms.ColumnHeader ValueColumn;
        private System.Windows.Forms.SplitContainer SplitPanel;
        private System.Windows.Forms.MenuStrip MenuStrip;
        private System.Windows.Forms.ToolStripMenuItem miOptions;
        private System.Windows.Forms.ToolStripMenuItem miProxyDownloads;
        private System.Windows.Forms.ToolStripMenuItem miAutoDetectPS4;
        private System.Windows.Forms.ToolStripTextBox tbPS4IP;
        private System.Windows.Forms.ToolStripMenuItem miRestartServer;
        private System.Windows.Forms.ToolStripMenuItem miPackages;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem miInstallAll;
        private System.Windows.Forms.OpenFileDialog OpenFileDialog;
        private System.Windows.Forms.ToolStripMenuItem miSegmentedDownloads;
    }
}

