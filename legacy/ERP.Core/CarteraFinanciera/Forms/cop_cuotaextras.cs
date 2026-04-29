using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_cuotaextras : Form
    {
        public DataSet DsDataextras = new DataSet();
        public decimal Porext = 0;
        public double ValPrestamo = 0;
        public DateTime fecpridescuento;
        double Total;
        bool ok;

        private void cop_cuotaextras_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Delete:
                    if (this.CmbGuardar.Enabled)
                    {
                        if (this.Dtgextras.SelectedRows.Count > 0)
                        {
                            EliminaItem();
                        }
                    }
                    break;
            }
        }

        private void cop_extras_Load(object sender, System.EventArgs e)
        {
            int StInteger = 0;
            string StString = " ";
            DateTime Stdate = default(DateTime);
            Total = 0;
            if (DsDataextras.Tables[0].TableName != "tblextras")
            {
                DsDataextras.Tables.Add("tblextras");
                DataColumnCollection cols = DsDataextras.Tables["tblextras"].Columns;
                cols.Add("FECHA", Stdate.GetType());
                cols.Add("VALOR", StInteger.GetType());
                cols.Add("DESCPAG", StString.GetType());
                cols.Add("FORPAG", StInteger.GetType());
                cols.Add("tipoextra", StString.GetType());
            }
            else
            {
                this.Dtgextras.AutoGenerateColumns = false;
                this.Dtgextras.DataSource = DsDataextras.Tables["tblextras"];
                if (!this.CmbGuardar.Enabled)
                {
                    this.Dtgextras.AllowUserToDeleteRows = false;
                }
                Total = Convert.ToDouble(DsDataextras.Tables["tblextras"].Compute("sum(valor)", ""));
            }
            this.Left = 500;
        }

        private void AgregaCuotaExtra(DateTime Fecha, double valor, int Forpag, ref double Vlrtotal)
        {
            string Descpago = null;
            double Vlext = 0, Vlrcuotas = 0;

            switch (Forpag)
            {
                case 1:
                    Descpago = "Nomina";
                    break;
                case 2:
                    Descpago = "Caja";
                    break;
            }

            Vlext = Math.Round(this.ValPrestamo * (Convert.ToDouble(this.Porext) / 100), 0);
            Vlrcuotas = Vlrtotal + valor;

            if (Vlrcuotas > Vlext)
            {
                MessageBox.Show("Total de cuotas extras supera el porcentaje admitido para esta linea", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string tipoextra = this.CbxClaExtra.Text.Length >= 1 ? this.CbxClaExtra.Text.Substring(0, 1) : "";
            DsDataextras.Tables["tblextras"].Rows.Add(Fecha, valor, Descpago, Forpag, tipoextra);
            Vlrtotal += valor;
        }

        private void CmbGuardar_Click(object sender, System.EventArgs e)
        {
            ok = Validacampos();
            if (ok)
            {
                AgregaCuotaExtra(this.Dtpfecha.Value, Convert.ToDouble(this.Txtvalor.Text), this.cbxformaDsto.SelectedIndex, ref Total);
                this.Dtgextras.AutoGenerateColumns = false;
                this.Dtgextras.DataSource = DsDataextras.Tables["tblextras"];
                this.Dtgextras.Focus();
            }
        }

        private bool Validacampos()
        {
            if (this.cbxformaDsto.SelectedIndex < 0)
            {
                MessageBox.Show("Forma de descuento incorrecta", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (!Microsoft.VisualBasic.Information.IsNumeric(this.Txtvalor.Text))
            {
                MessageBox.Show("Falta valor de la cuota", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (this.CbxClaExtra.Text.Trim() == "")
            {
                MessageBox.Show("Clase extra incorrecta", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (Convert.ToDateTime(this.Dtpfecha.Value.ToString("dd/MM/yyyy")) > Convert.ToDateTime(this.dtpVali.Value.ToString("dd/MM/yyyy")))
            {
                MessageBox.Show("La fecha de la extra no debe ser mayor a la fecha de la ultima cuota ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (Convert.ToDateTime(this.Dtpfecha.Value.ToString("dd/MM/yyyy")) < Convert.ToDateTime(fecpridescuento.ToString("dd/MM/yyyy")))
            {
                MessageBox.Show("La fecha de la extra no debe ser menor a la  fecha de primer descuento ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if ((this.Dtgextras.RowCount) >= 0)
            {
                for (int i = this.Dtgextras.Rows.Count - 1; i >= 0; i--)
                {
                    DataGridViewRow row = this.Dtgextras.Rows[i];
                    if (Convert.ToDateTime(this.Dtpfecha.Value.ToString("dd/MM/yyyy")) == Convert.ToDateTime(Convert.ToDateTime(row.Cells[0].Value).ToString("dd/MM/yyyy")))
                    {
                        MessageBox.Show("Ya hay programada una cuota extra en esta fecha", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                }
            }

            if (Convert.ToDouble(this.Txtvalor.Text) <= 0)
            {
                MessageBox.Show("El valor de la extra debe ser mayor a cero", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Txtvalor.Focus();
                return false;
            }

            return true;
        }

        private void Salir()
        {
            this.Close();
            this.Dispose();
        }

        private void CmbSalir_Click(object sender, System.EventArgs e)
        {
            if (this.DsDataextras.Tables["tblextras"].Rows.Count == 0)
            {
                this.DsDataextras.Tables["tblextras"].Rows.Add(DateTime.Now, 0, 0, 0, " ");
            }
            this.Salir();
        }

        private void EliminaItem()
        {
            if ((this.Dtgextras.RowCount - 1) >= 0)
            {
                for (int i = this.Dtgextras.SelectedRows.Count - 1; i >= 0; i--)
                {
                    DataGridViewRow selRow = this.Dtgextras.SelectedRows[i];
                    Total = Total - Convert.ToDouble(selRow.Cells["Clmvalor"].Value);
                    DsDataextras.Tables["tblextras"].Rows.RemoveAt(this.Dtgextras.CurrentRow.Index);
                    this.Dtgextras.DataSource = DsDataextras.Tables["tblextras"];
                }
            }
        }

        private void Txtvalor_LostFocus(object sender, System.EventArgs e)
        {
            if (!Microsoft.VisualBasic.Information.IsNumeric(this.Txtvalor.Text))
            {
                this.Txtvalor.Text = "0";
            }
            else
            {
                this.Txtvalor.Text = Strings.FormatNumber(Convert.ToDouble(this.Txtvalor.Text), 0);
            }
        }
    }
}
