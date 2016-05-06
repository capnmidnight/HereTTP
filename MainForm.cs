using System;
using System.Windows.Forms;

namespace HereTTP
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        public string BrowserPath
        {
            get
            {
                return this.browserPathTextBox.Text;
            }
            set
            {
                this.browserPathTextBox.Text = value;
            }
        }

        public int ServerPort
        {
            get
            {
                return (int)this.numericUpDown1.Value;
            }
            set
            {
                this.numericUpDown1.Value = value;
            }
        }
    }
}
