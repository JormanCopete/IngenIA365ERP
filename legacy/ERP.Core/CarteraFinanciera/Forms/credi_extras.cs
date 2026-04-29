using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class credi_extras : Form
    {
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private int Ancho = 0;

        public credi_extras()
        {
            InitializeComponent();
        }

        private void credi_extras_Load(object sender, EventArgs e)
        {
            for (int i = 0; i <= this.DgwExtras.Columns.Count - 1; i++)
            {
                Ancho += this.DgwExtras.Columns[i].Width;
            }
            this.DgwExtras.Width = Ancho;
            this.Width = Ancho + 50;
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
