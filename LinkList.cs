using System;
using System.Linq;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    public partial class LinkList : Form
    {
        string MainUrl;
        public LinkList(bool Multipart, bool Encrypted, string FirstUrl)
        {
            InitializeComponent();
            tbLinks.Enabled = Multipart;
            tbPassword.Enabled = Encrypted;
            MainUrl = FirstUrl;
            DialogResult = DialogResult.Cancel;
        }

        public string[] Links { get; private set; }
        public string Password { get; private set; }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (tbLinks.Enabled) {
                
                var Lines = new string[] { MainUrl }.Concat(
                    tbLinks.Lines.Where(x => !string.IsNullOrWhiteSpace(x) 
                                          && !MainUrl.Equals(x, StringComparison.OrdinalIgnoreCase)
                                       )
                    ).Distinct().ToArray();

                foreach (var Link in Lines)
                {
                    if (!Uri.IsWellFormedUriString(Link, UriKind.Absolute))
                    {
                        MessageBox.Show("Invalid URL:\n" + Link, "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                Links = Lines;
            }

            if (tbPassword.Enabled && string.IsNullOrEmpty(tbPassword.Text))
            {
                MessageBox.Show("Invalid Password", "DirectPackageInstaller", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Password = tbPassword.Text;
            DialogResult = DialogResult.OK;
        }
    }
}
