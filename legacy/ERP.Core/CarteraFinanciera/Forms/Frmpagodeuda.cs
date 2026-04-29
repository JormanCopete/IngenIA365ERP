using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;


namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class Frmpagodeuda : Form
    {
        public string Codigoter;
        public string Lincred;
        public DateTime fecha;
        public System.Data.Odbc.OdbcConnection myconnect = new System.Data.Odbc.OdbcConnection();
        public DataTable DsDatapago = new DataTable();

        private DataGridViewCellStyle style2_Selec = new DataGridViewCellStyle();
        private DataGridViewCellStyle style1_Normal = new DataGridViewCellStyle();
        private bool ok;
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private Clscartera msgcop = new Clscartera();
        private DataTable DsDataSet = new DataTable();
        private DataSet DsDatSet = new DataSet();
        private double VlrSeguro = 0;

        private void Frmpagodeuda_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            msgparsys.ConfiguraForma(this, this.LblNomEmpresa.Text, this.txtServer.Text, this.TxtBd.Text, this.TxtNomForma.Text, "", this.TxtfechaSys.Text);
            //msgcop.BuscaAsociado(this.TxtCodigoter.Text, this.myconnect, this.LblNombre.Text);
            CargaDatosGrilla();
            CreaTablaAgregaPagos();
            style2_Selec.BackColor = Color.LightBlue;
            style1_Normal = this.DtgDatos.RowsDefaultCellStyle;
        }

        private void CargaDatosGrilla()
        {
            //DsDataSet = msgcop.CargaCreditosPagoTotal(this.TxtCodigoter.Text, this.Lblperiodo.Text, this.myconnect);
            if (DsDataSet.Rows.Count > 0)
            {
                CargaTotales(DsDataSet);
            }

            this.DtgDatos.AutoGenerateColumns = false;
            this.DtgDatos.DataSource = DsDataSet;
        }

        private void CargaTotales(DataTable DsDataSet)
        {
            int I = 0;
            double TotalCap = 0, Total = 0, TotalOtros = 0, TotalInt = 0;
            double TotalMora = 0, TotalSaldo = 0, Subtotal = 0;

            for (I = 0; I <= DsDataSet.Rows.Count - 1; I++)
            {
                Subtotal = 0;
                if (DsDataSet.Rows[I]["Capital"].ToString() != "")
                {
                    TotalCap = TotalCap + Convert.ToDouble(DsDataSet.Rows[I]["Capital"].ToString());
                }

                if (DsDataSet.Rows[I]["Interes"].ToString() != "")
                {
                    TotalInt = TotalInt + Convert.ToDouble(DsDataSet.Rows[I]["Interes"].ToString());
                    Subtotal += Convert.ToDouble(DsDataSet.Rows[I]["Interes"].ToString());
                }

                if (DsDataSet.Rows[I]["Mora"].ToString() != "")
                {
                    TotalMora = TotalMora + Convert.ToDouble(DsDataSet.Rows[I]["Mora"].ToString());
                    Subtotal += Convert.ToDouble(DsDataSet.Rows[I]["Mora"].ToString());
                }

                if (DsDataSet.Rows[I]["otros"].ToString() != "")
                {
                    TotalOtros = TotalOtros + Convert.ToDouble(DsDataSet.Rows[I]["otros"].ToString());
                    Subtotal += Convert.ToDouble(DsDataSet.Rows[I]["otros"].ToString());
                }

                if (DsDataSet.Rows[I]["saldo"].ToString() != "")
                {
                    TotalSaldo = TotalSaldo + Convert.ToDouble(DsDataSet.Rows[I]["saldo"].ToString());
                    Subtotal += Convert.ToDouble(DsDataSet.Rows[I]["saldo"].ToString());
                }

                Total = Total + Subtotal;
                DsDataSet.Rows[I]["total"] = Subtotal;
            }

            this.LblCapital.Text = Strings.FormatNumber(TotalCap, 0);
            this.LblInteres.Text = Strings.FormatNumber(TotalInt, 0);
            this.LblMora.Text = Strings.FormatNumber(TotalMora, 0);
            this.LblOtros.Text = Strings.FormatNumber(TotalOtros, 0);
            this.LblSaldo.Text = Strings.FormatNumber(TotalSaldo, 0);
            this.LblTotalPago.Text = Strings.FormatNumber(Total, 0);
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            switch (this.opcion.ButtonPressed)
            {
                case 1:
                    Salir();
                    break;
                case 2:
                    Salir();
                    break;
            }
        }

        private void cmbsalir_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void Salir()
        {
            this.Codigoter = null;
            this.Lincred = "0";
            this.Close();
        }

        private void SalirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void CmbGuardar_Click(object sender, EventArgs e)
        {
            ok = ValidaCampos();
            switch (ok)
            {
                case true:
                    AgregaPagos();
                    LimpiaCampos();
                    this.DtgDatos.CurrentRow.DefaultCellStyle = style2_Selec;
                    break;
            }
        }

        private void LimpiaCampos()
        {
            this.TxtLinea.Text = "";
            this.TxtNumero.Text = "";
            this.TxtValor.Text = "0";
            this.TxtInteres.Text = "0";
        }

        private void AgregaPagos()
        {
            //GrabaDeudas(this.TxtCodigoter.Text, this.TxtLinea.Text, this.TxtNumero.Text, this.TxtValor.Text, this.TxtInteres.Text, VlrSeguro);
        }

        private bool ValidaCampos()
        {
            //ok = msgparcop.BuscaLinea(this.TxtLinea.Text, this.myconnect);
            switch (ok)
            {
                case false:
                    MessageBox.Show("Linea de credito no esta creada !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtLinea.Text = null;
                    this.TxtLinea.Focus();
                    return false;
            }

            if (Information.IsNumeric(this.TxtNumero.Text) == false)
            {
                MessageBox.Show("Consecutivo de la linea es invalido !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtNumero.Text = null;
                this.TxtNumero.Focus();
                return false;
            }

            if (Information.IsNumeric(this.TxtValor.Text) == false)
            {
                MessageBox.Show("Valor a pagar es invalido !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtNumero.Text = null;
                this.TxtNumero.Focus();
                return false;
            }

            if (Information.IsNumeric(this.TxtInteres.Text) == false)
            {
                this.TxtInteres.Text = "0";
            }

            return true;
        }

        private void GrabaDeudas(string codigoter, int lincred, double Numero, double SaldoPagar, double interes, double seguro)
        {
            DsDatapago.Rows.Add(codigoter, lincred, Numero, SaldoPagar, interes, seguro);
        }

        private void CreaTablaAgregaPagos()
        {
            string Ststring = " ";
            double stdouble = 0;
            DsDatapago.TableName = "tblpagos";
            DsDatapago.Columns.Add("Codigoter", Ststring.GetType());
            DsDatapago.Columns.Add("lincred", Ststring.GetType());
            DsDatapago.Columns.Add("numero", stdouble.GetType());
            DsDatapago.Columns.Add("SaldoPagar", stdouble.GetType());
            DsDatapago.Columns.Add("InteresDev", stdouble.GetType());
            DsDatapago.Columns.Add("Seguro", stdouble.GetType());
        }

        private void DtgDatos_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (this.DtgDatos.RowCount > 0)
            {
                if (this.DtgDatos.CurrentRow.DefaultCellStyle.BackColor == style2_Selec.BackColor)
                {
                    MessageBox.Show("Esta obligacion ya fue seleccionada", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                VlrSeguro = 0;
                this.TxtLinea.Text = Convert.ToString(this.DtgDatos.CurrentRow.Cells["ClmLinea"].Value);
                this.TxtNumero.Text = Convert.ToString(this.DtgDatos.CurrentRow.Cells["ClmPagare"].Value);
                this.TxtValor.Text = Strings.FormatNumber(Convert.ToDouble(this.DtgDatos.CurrentRow.Cells["ClmTotal"].Value), 0);
                //this.TxtInteres.Text = Strings.FormatNumber(Convert.ToDouble(Math.Round(msgcop.VerificaDevolucionInt(this.TxtCodigoter.Text, this.TxtLinea.Text, this.TxtNumero.Text, fecha, myconnect, VlrSeguro))), 0);
                this.TxtLinea.Focus();
            }
        }

        private void DtgDatos_MouseUp(object sender, MouseEventArgs e)
        {
            Menu.Visible = false;
            if (this.DtgDatos.RowCount != 0)
            {
                if (e.Button == MouseButtons.Right)
                {
                    if (this.DtgDatos.CurrentRow != null)
                    {
                        Menu.Visible = true;
                        this.EliminarPagoToolStripMenuItem.Enabled = false;
                        if (this.DtgDatos.CurrentRow.DefaultCellStyle.BackColor == style2_Selec.BackColor)
                        {
                            this.EliminarPagoToolStripMenuItem.Enabled = true;
                        }
                        Menu.Show(DtgDatos, new Point(e.X, e.Y));
                    }
                }
            }
        }

        private void VerCuotasPendientesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int index = this.DtgDatos.CurrentRow.Index;
            try
            {
                //msgcop.CargarVentanaCuopen(this.TxtCodigoter.Text, this.DtgDatos.CurrentRow.Cells["ClmLinea"].Value, this.DtgDatos.CurrentRow.Cells["ClmPagare"].Value, this.Lblperiodo.Text, myconnect, this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void EliminarPagoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int fila = 0;
            int index = 0;
            while (fila < DsDatapago.Rows.Count)
            {
                if (Convert.ToString(DsDatapago.Rows[fila]["lincred"]) == Convert.ToString(this.DtgDatos.CurrentRow.Cells["ClmLinea"].Value) && Convert.ToString(DsDatapago.Rows[fila]["numero"]) == Convert.ToString(this.DtgDatos.CurrentRow.Cells["ClmPagare"].Value))
                {
                    DsDatapago.Rows.RemoveAt(fila);
                    this.DtgDatos.CurrentRow.DefaultCellStyle = style1_Normal;
                }
                fila += 1;
            }
        }
    }
}
