using System;
using System.Windows.Forms;

namespace ERP.Core.Compartido.Forms
{
    public partial class FrmProgres : System.Windows.Forms.Form
    {
        public FrmProgres()
        {
            InitializeComponent();
        }

        private void FrmProgres_Load(object sender, EventArgs e)
        {
            this.CenterToParent();
        }

        private void Tiempo_Tick(object sender, EventArgs e)
        {
            Time1.Text = (Convert.ToInt32(Time1.Text) + 1).ToString();
        }
    }
}
