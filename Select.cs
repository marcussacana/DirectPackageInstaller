using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    public partial class Select : Form
    {
        public string Choice => ChoiceBox.SelectedItem.ToString();
        public Select(string[] Options)
        {
            InitializeComponent();
            foreach (var Option in Options)
                ChoiceBox.Items.Add(Option);
            DialogResult = DialogResult.Cancel;
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
