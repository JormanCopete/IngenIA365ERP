using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_lineacobjur01 : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();

        public cop_lineacobjur01(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void cop_lineacobjur01_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Dispose();
                this.Close();
            }
        }

        private void cop_lineacobjur01_Load(object sender, EventArgs e)
        {
            DataSet DtDatos = new DataSet();
            int i = 0;
            DataGridViewCellStyle style = new DataGridViewCellStyle();
            DataGridViewCellStyle style2 = new DataGridViewCellStyle();

            style.BackColor = Color.Red;
            style2.BackColor = Color.Yellow;
            this.TxtCodigoter.Text = ("00000000000000" + this.TxtCodigoter.Text).Substring(("00000000000000" + this.TxtCodigoter.Text).Length - 14);
            this.LblNombre.Text = " ";

            string lblNombreText = this.LblNombre.Text;
            string _cod = this.TxtCodigoter.Text;
            DtDatos = this.msgcop.CargaDatosObligacionesDeudores(_cod, 0, int.Parse(this.LblPeriodo.Text), this.mycon);
            this.msgcop.BuscaAsociado(ref _cod, this.mycon);
            this.LblNombre.Text = _cod;

            this.DtgDocs.AutoGenerateColumns = false;
            this.DtgDocs.Rows.Clear();
            this.DtgDocs.DataSource = DtDatos.Tables[0];
            this.DtgDocs.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            while (i < this.DtgDocs.RowCount)
            {
                if (Convert.ToString(this.DtgDocs[3, i].Value) == "Y")
                {
                    this.DtgDocs.Rows[i].DefaultCellStyle = style;
                }
                if (Convert.ToDouble(this.DtgDocs[2, i].Value) > 0 && Convert.ToString(this.DtgDocs[3, i].Value) != "Y")
                {
                    this.DtgDocs.Rows[i].DefaultCellStyle = style2;
                }
                i = i + 1;
            }

            this.CenterToScreen();
            this.BtnAceptar.Focus();
        }

        private void BtnAceptar_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
