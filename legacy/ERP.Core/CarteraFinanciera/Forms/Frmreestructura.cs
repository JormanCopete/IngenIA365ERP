using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;


namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class Frmreestructura : Form
    {
        public string Codigoter;
        public string Lincred;
        public string Numero;
        public System.Data.Odbc.OdbcConnection myconnect = new System.Data.Odbc.OdbcConnection();
        public DataTable DsDataReest = new DataTable();
        public DateTime FechaDomto;
        public bool Reest = false;
        public bool VariasReest = false;

        private bool ok;
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private Clscartera msgcop = new Clscartera();
        private DataTable DsDataSet = new DataTable();
        private DataSet DsDatSet = new DataSet();

        private void CargaDatosGrilla()
        {
            //DsDataSet = msgcop.CargaSoloCreditos(this.TxtCodigoter.Text, this.Lblperiodo.Text, this.myconnect);

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
                if (DsDataSet.Rows[I]["intpro"].ToString() != "")
                {
                   // DsDataSet.Rows[I]["intpro"] = Convert.ToDouble(Math.Round(msgcop.VerificaDevolucionInt(this.TxtCodigoter.Text, DsDataSet.Rows[I]["linea"], DsDataSet.Rows[I]["numero"], FechaDomto, this.myconnect), 0));
                    TotalCap = TotalCap + Convert.ToDouble(DsDataSet.Rows[I]["intpro"].ToString());
                    Subtotal += Convert.ToDouble(DsDataSet.Rows[I]["intpro"].ToString());
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

            this.LblCapital.Text = string.Format("{0:###,###,###}", TotalCap);
            this.LblInteres.Text = string.Format("{0:###,###,###}", TotalInt);
            this.LblMora.Text = string.Format("{0:###,###,###}", TotalMora);
            this.LblOtros.Text = string.Format("{0:###,###,###}", TotalOtros);
            this.LblSaldo.Text = string.Format("{0:###,###,###}", TotalSaldo);
            this.LblTotalPago.Text = string.Format("{0:###,###,###}", Total);
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            switch (this.opcion.ButtonPressed)
            {
                case 1:
                case 2:
                    Salir();
                    break;
            }
        }

        private void Salir()
        {
            this.Codigoter = null;
            this.Lincred = "0";
            this.Numero = "0";
            this.Close();
        }

        private void Frmreestructura_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
            msgparsys.ConfiguraForma(this, this.LblNomEmpresa.Text, this.txtServer.Text, this.TxtBd.Text, this.TxtNomForma.Text, "", this.TxtfechaSys.Text);
            //msgcop.BuscaAsociado(this.TxtCodigoter.Text, this.myconnect, this.LblNombre.Text);
            CargaDatosGrilla();
            VariasReest = false;
        }

        private void CmbGuardar_Click(object sender, EventArgs e)
        {
            ok = ValidaCampos();
            switch (ok)
            {
                case true:
                    //msgcop.CreaTablaAgregaDeudasReest(DsDataReest);
                    AgregaDeudas();
                    break;
            }
        }

        private bool ValidaCampos()
        {
            //ok = msgparcop.BuscaLinea(this.TxtIdLincred.Text, this.myconnect);
            switch (ok)
            {
                case false:
                    MessageBox.Show("Linea de credito no esta creada !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtIdLincred.Text = null;
                    this.TxtIdLincred.Focus();
                    return false;
            }

            if (Information.IsNumeric(this.TxtIdNUmero.Text) == false)
            {
                MessageBox.Show("Consecutivo de la linea es invalido !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtIdNUmero.Text = null;
                this.TxtIdNUmero.Focus();
                return false;
            }

            if (Information.IsNumeric(this.TxtValor.Text) == false)
            {
                MessageBox.Show("Valor del credito es invalido !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtValor.Text = null;
                this.TxtValor.Focus();
                return false;
            }

            if (!Information.IsNumeric(this.LblValRecogido.Text))
            {
                this.LblValRecogido.Text = "0";
            }

            if (Convert.ToDouble(this.LblValRecogido.Text) <= 0)
            {
                MessageBox.Show("No ha seleccionado creditos para reestructurar", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (Convert.ToDouble(this.TxtValor.Text) < Convert.ToDouble(this.LblValRecogido.Text))
            {
                MessageBox.Show("Valor del credito es menor al total de deudas recogidas !!!", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtValor.Text = null;
                this.TxtValor.Focus();
                return false;
            }

            return true;
        }

        private void AgregaDeudas()
        {
            int fila = 0;
            DateTime StFecfin = DateTime.MinValue;
            int DiasVen = 0;
            string categoria;
            double cantveces = 0, veces = 0;
            int sw1 = 0;

            //msgparsys.buscaPeriodo("copc", myconnect, "", ref StFecfin, "", "", this.Lblperiodo.Text);
            Reest = false;
            VariasReest = false;

            while (fila < DtgDatos.Rows.Count)
            {
                if (Convert.ToBoolean(DtgDatos.Rows[fila].Cells["ClmRestructurado"].Value) == true)
                {
                    categoria = "A";
                    cantveces = 0;
                    sw1 = 0;
                    if (Convert.ToDouble(DtgDatos.Rows[fila].Cells[2].Value) >= 1000)
                    {
                        //Reest = msgcop.ValidaSiAplicaReest(this.TxtCodigoter.Text, DtgDatos.Rows[fila].Cells[2].Value, DtgDatos.Rows[fila].Cells[3].Value, StFecfin, myconnect, ref categoria);
                    }

                    if (string.Compare(categoria, "B") >= 0)
                    {
                        Reest = true;
                        sw1 = 1;
                    }
                    else
                    {
                        if (Convert.ToString(DtgDatos.Rows[fila].Cells["clmreest"].Value) == "Y")
                        {
                            Reest = true;
                            categoria = Convert.ToString(DtgDatos.Rows[fila].Cells["clmcalif"].Value);
                            sw1 = 1;
                        }
                    }

                    if (sw1 == 1)
                    {
                        //cantveces = msgcop.BuscaVecesReEstructurados(this.TxtCodigoter.Text, DtgDatos.Rows[fila].Cells["ClmLinea"].Value, DtgDatos.Rows[fila].Cells["ClmPagare"].Value, myconnect);
                        cantveces += 1;
                        if (cantveces > veces)
                        {
                            veces = cantveces;
                        }
                    }

                    if (DtgDatos.Rows[fila].Cells["Clmintpro"].Value is DBNull)
                    {
                        DtgDatos.Rows[fila].Cells["Clmintpro"].Value = "0";
                    }
                    GrabaDeudas(this.TxtCodigoter.Text, DtgDatos.Rows[fila].Cells["ClmLinea"].Value, DtgDatos.Rows[fila].Cells["ClmPagare"].Value, DtgDatos.Rows[fila].Cells["ClmTotal"].Value, this.TxtCodigoter.Text, this.TxtIdLincred.Text, this.TxtIdNUmero.Text, this.TxtValor.Text, categoria, DtgDatos.Rows[fila].Cells["Clmintpro"].Value);
                }
                fila += 1;
            }

            if (!Reest)
            {
                if (MessageBox.Show("Ninguno de los creditos a recoger aplica para reestructurar. Desea realizar una novacion?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
                {
                    DsDataReest.Rows.Clear();
                    return;
                }
            }
            else
            {
                if (veces >= 2)
                {
                    VariasReest = true;
                }
            }

            this.Close();
        }

        private void GrabaDeudas(object codigoter, object lincred, object Numero, object Saldo, object Idcodigoter, object Idlincred, object Idnumero, object Valor, object Categoria, object IntProporcional)
        {
            DsDataReest.Rows.Add(codigoter, lincred, Numero, Saldo, Categoria, Idcodigoter, Idlincred, Idnumero, Valor, IntProporcional);
        }

        private void HelpCptos_Click(object sender, EventArgs e)
        {
            //this.TxtIdLincred.Text = msgparcop.HelpLIneas(myconnect, this, ERP.Core.CarteraFinanciera.Models.ParamCop.LineasCredito.Cartera);
            this.TxtIdLincred.Focus();
        }

        private void cmbsalir_Click(object sender, EventArgs e)
        {
            Salir();
        }

        private void TxtValor_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtValor.Text))
            {
                this.TxtValor.Text = "0";
            }
            this.TxtValor.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtValor.Text), 2);
        }

        private void DtgDatos_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            double fila = 0, Recogido = 0;
            for (fila = 0; fila <= DtgDatos.RowCount - 1; fila++)
            {
                if (Convert.ToBoolean(DtgDatos.Rows[(int)fila].Cells["ClmRestructurado"].Value) == true)
                {
                    Recogido = Recogido + Convert.ToDouble(DtgDatos.Rows[(int)fila].Cells["ClmTotal"].Value);
                }
            }

            this.LblValRecogido.Text = Strings.FormatNumber(Recogido, 2);
        }
    }
}
