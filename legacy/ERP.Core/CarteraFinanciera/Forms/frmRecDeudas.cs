using ERP.Core.CarteraFinanciera.Services.Creditos;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmRecDeudas : Form
    {
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera Msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private OdbcConnection mycon = new OdbcConnection();
        private ClsLiqcreditos msgliqcre = new ClsLiqcreditos();
        private double Vlrdsto = 0;
        public DataSet DsdataRecdeuda = new DataSet();
        public string periodo;
        private bool ok;
        private int index = -1;
        private System.Windows.Forms.DataGridViewCellStyle style2 = new System.Windows.Forms.DataGridViewCellStyle();
        private System.Windows.Forms.DataGridViewCellStyle style1 = new System.Windows.Forms.DataGridViewCellStyle();

        public frmRecDeudas(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void CmbSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void frmRecDeudas_Load(object sender, EventArgs e)
        {
            int StInteger = 0;
            double StDouble = 0;
            string ststring = " ";
            int i = 0;

            style2.BackColor = System.Drawing.Color.LightBlue;
            style1.BackColor = System.Drawing.Color.White;

            this.CenterToScreen();
            if (DsdataRecdeuda.Tables.Count == 0 || DsdataRecdeuda.Tables[0].TableName != "tbldeducciones")
            {
                DsdataRecdeuda.Tables.Add("tbldeducciones");
                DataColumnCollection cols = DsdataRecdeuda.Tables["tbldeducciones"].Columns;
                cols.Add("CEDULA", ststring.GetType());
                cols.Add("LINCRED", StInteger.GetType());
                cols.Add("NUMERO", StDouble.GetType());
                cols.Add("DESCRIPCION", ststring.GetType());
                cols.Add("VALOR", StDouble.GetType());
                cols.Add("INTERES", StDouble.GetType());
                cols.Add("TOTAL", ststring.GetType());
                cols.Add("TOTALDEDUCIR", StDouble.GetType());
                cols.Add("CUOTA", StDouble.GetType());   // para calcular cuota recogida por nomina
                cols.Add("CLADES", StInteger.GetType()); // clades si es nomina o caja
                Vlrdsto = 0;
            }
            else
            {
                DataTable tbl = DsdataRecdeuda.Tables["tbldeducciones"];
                while (StInteger < tbl.Rows.Count)
                {
                    while (i < DatGriCreditos.RowCount)
                    {
                        if (tbl.Rows[StInteger]["LINCRED"].ToString() == DatGriCreditos["ClmLinea", i].Value.ToString()
                            && tbl.Rows[StInteger]["NUMERO"].ToString() == DatGriCreditos["ClmNumero", i].Value.ToString())
                        {
                            DatGriCreditos.Rows[i].DefaultCellStyle = style2;
                            this.LblDesembolso.Text = (Convert.ToDouble(this.LblDesembolso.Text)
                                - (Convert.ToDouble(tbl.Rows[StInteger]["VALOR"]) + Convert.ToDouble(tbl.Rows[StInteger]["INTERES"]))).ToString();
                            Vlrdsto += Convert.ToDouble(tbl.Rows[StInteger]["VALOR"]) + Convert.ToDouble(tbl.Rows[StInteger]["INTERES"]);
                            break;
                        }
                        i = i + 1;
                    }
                    i = 0;
                    StInteger = StInteger + 1;
                }
            }
        }

        private void TxtNumRec_LostFocus(object sender, EventArgs e)
        {
            double cuota = 0;
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtNumRec.Text) && Microsoft.VisualBasic.Information.IsNumeric(this.TxtLineaRec.Text))
            {
               // ok = Msgcop.BuscaObligacion(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text, this.mycon, ref cuota);
                if (ok)
                {
                    InitializeCampos();
                    if (Convert.ToDouble(this.TxtLineaRec.Text) >= 1000)
                    {
                        BuscaSaldoRecoger();
                    }
                    else
                    {
                        this.TxtValorRec.Text = cuota.ToString("N2");
                        this.TxtIntRec.Text = "0";
                    }
                    this.TxtValorRec.Enabled = true;
                    this.TxtIntRec.Enabled = false;
                    this.cmbgrabar.Enabled = true;
                    TxtValorRec.Focus();
                }
                else
                {
                    MessageBox.Show("Credito no esta creado ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    InitializeDatosCredito();
                    this.TxtLineaRec.Focus();
                    return;
                }
            }
        }

        private void BuscaSaldoRecoger()
        {
            double saldo = 0;
            int OpRecDeuda = 0;
            double SaldoCapNom = 0, SaldoextNom = 0, SaldoIntNom = 0, SaldoMorNom = 0, SaldoSegNom = 0, SaldoAdmNom = 0, SaldoOtrNom = 0;
            double SaldoCapCaj = 0, SaldoextCaj = 0, SaldoIntCaj = 0, SaldoMorCaj = 0, SaldoSegCaj = 0, SaldoAdmCaj = 0, SaldoOtrCaj = 0;
            double TotRec = 0, Intmes = 0;

            //msgsys.BuscarCompania(Msgcop.varini.sptCodEmpr, this.mycon, ref OpRecDeuda);
            this.TxtIntRec.Tag = "0";

            //Msgcop.BuscaSaldoObligacion(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text,
            //    this.DtpFecha.Value.ToString("yyyyMM"), this.mycon, ref saldo);

            switch (OpRecDeuda)
            {
                case 0:
                    //Msgcop.BuscaCuotasClades(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text,
                    //    this.DtpFecha.Value.ToString("yyyyMM"), OpRecDeuda, this.mycon,
                    //    ref SaldoCapNom, ref SaldoextNom, ref SaldoIntNom, ref SaldoMorNom, ref SaldoSegNom, ref SaldoAdmNom, ref SaldoOtrNom);
                    TotRec = saldo + SaldoIntNom + SaldoMorNom + SaldoSegNom + SaldoAdmNom + SaldoOtrNom;
                    break;
                case 1:
                    //Msgcop.BuscaCuotasClades(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text,
                    //    this.DtpFecha.Value.ToString("yyyyMM"), OpRecDeuda, this.mycon,
                    //    ref SaldoCapNom, ref SaldoextNom, ref SaldoIntNom, ref SaldoMorNom, ref SaldoSegNom, ref SaldoAdmNom, ref SaldoOtrNom);
                    //Msgcop.BuscaCuotasClades(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text,
                    //    this.DtpFecha.Value.ToString("yyyyMM"), (int)ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Caja, this.mycon,
                    //    ref SaldoCapCaj, ref SaldoextCaj);
                    TotRec = saldo + SaldoIntNom + SaldoMorNom + SaldoSegNom + SaldoAdmNom + SaldoOtrNom;
                    TotRec = TotRec - SaldoCapCaj - SaldoextCaj;
                    this.TxtIntRec.Tag = SaldoCapCaj + SaldoextCaj;
                    break;
                case 2:
                    // Msgcop.BuscaCuotasClades(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text, // ERROR: CS7036
                        // this.DtpFecha.Value.ToString("yyyyMM"), OpRecDeuda, this.mycon, // ERROR: CS7036
                        // ref SaldoCapCaj, ref SaldoextCaj, ref SaldoIntCaj, ref SaldoMorCaj, ref SaldoSegCaj, ref SaldoAdmCaj, ref SaldoOtrCaj); // ERROR: CS7036
                    // Msgcop.BuscaCuotasClades(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text, // ERROR: CS7036
                        // this.DtpFecha.Value.ToString("yyyyMM"), (int)ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, this.mycon, // ERROR: CS7036
                        // ref SaldoCapNom, ref SaldoextNom, ref SaldoIntNom, ref SaldoMorNom, ref SaldoSegNom, ref SaldoAdmNom, ref SaldoOtrNom); // ERROR: CS7036
                    TotRec = saldo + SaldoIntCaj + SaldoMorCaj + SaldoSegCaj + SaldoAdmCaj + SaldoOtrCaj;
                    TotRec = TotRec - SaldoCapNom - SaldoextNom;
                    this.TxtIntRec.Tag = SaldoCapNom + SaldoextNom;
                    break;
            }

            // Intmes = Math.Round(Msgcop.BuscaIntMes(this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text, // ERROR: CS1503
                // this.DtpFecha.Value, saldo, this.mycon), 0); // ERROR: CS1503
            this.TxtValorRec.Text = TotRec.ToString();
            this.TxtIntRec.Text = Intmes.ToString();
        }

        private void InitializeCampos()
        {
            this.TxtValorRec.Text = "0";
            this.TxtIntRec.Text = "0";
            this.TxtIntRec.Tag = "0";
        }

        private void InitializeDatosCredito()
        {
            this.TxtLineaRec.Text = null;
            this.TxtNumRec.Text = null;
        }

        private void TxtNumRec_TextChanged(object sender, EventArgs e)
        {
        }

        private void cmbgrabar_Click(object sender, EventArgs e)
        {
            GrabaDeudaRecogida();
        }

        private void GrabaDeudaRecogida()
        {
            string Descripcion = " ";
            // Msgcop.BuscaLinea(this.TxtLineaRec.Text, this.mycon, ref Descripcion); // ERROR: CS1503
            GrabaDeudas(this.DsdataRecdeuda, this.txtCodigoter.Text, this.TxtLineaRec.Text,
                this.TxtNumRec.Text, Descripcion, this.TxtValorRec.Text, this.TxtIntRec.Text);

            InitializeCampos();
            InitializeDatosCredito();
            this.TxtValorRec.Enabled = false;
            this.TxtIntRec.Enabled = false;
            this.cmbgrabar.Enabled = false;
            this.TxtLineaRec.Focus();
        }

        private void GrabaDeudas(DataSet dsdataset, string Codigoter, string Concepto, string numero,
            string Descripcion, string valor, string Interes)
        {
            double dsto = 0;
            int StTotal = 0, indice = 0;
            double total = 0, SALDO = 0;
            string TotPar = "T";
            double cuota = 0;
            int clades = 0;
            int CICLOD = 0;
            int periodd = 0;

            double dValor = Convert.ToDouble(valor);
            double dInteres = Convert.ToDouble(Interes);
            int iConcepto = Convert.ToInt32(Concepto);
            double dNumero = Convert.ToDouble(numero);

            if (dsdataset.Tables["tbldeducciones"].Rows.Count > 0)
            {
                StTotal = dsdataset.Tables["tbldeducciones"].Select(
                    "CEDULA='" + Codigoter + "' AND LINCRED=" + iConcepto + " AND NUMERO=" + dNumero, "NUMERO").Length;
                if (StTotal > 0)
                {
                    while (indice < dsdataset.Tables["tbldeducciones"].Rows.Count)
                    {
                        DataRow row = dsdataset.Tables["tbldeducciones"].Rows[indice];
                        if (row["CEDULA"].ToString() == Codigoter
                            && Convert.ToInt32(row["LINCRED"]) == iConcepto
                            && Convert.ToDouble(row["NUMERO"]) == dNumero)
                        {
                            total = Convert.ToDouble(row["valor"]) + Convert.ToDouble(row["interes"]);
                            Vlrdsto -= total;
                            dsdataset.Tables["tbldeducciones"].Rows.RemoveAt(indice);
                            break;
                        }
                        indice += 1;
                    }
                }
            }

            dsto = Math.Round(Convert.ToDouble(LblCredito.Text) - (dValor + Vlrdsto + dInteres), 0);
            if (dsto < 0)
            {
                MessageBox.Show("Descuentos superan el prestamo");
                return;
            }

            // Msgcop.BuscaObligacion(Codigoter, Concepto, numero, mycon, // ERROR: CS7036
                // ref cuota, ref periodd, ref clades, ref CICLOD); // ERROR: CS7036

            if (iConcepto >= 1000)
            {
                // Msgcop.BuscaSaldoObligacion(Codigoter, Concepto, numero, // ERROR: CS1503
                    // this.DtpFecha.Value.ToString("yyyyMM"), mycon, ref SALDO); // ERROR: CS1503
                if (!Microsoft.VisualBasic.Information.IsNumeric(this.TxtIntRec.Tag))
                    this.TxtIntRec.Tag = "0";
                if ((dValor + Convert.ToDouble(this.TxtIntRec.Tag)) < SALDO)
                    TotPar = "P";
                if (CICLOD == 5)
                    cuota = periodd * cuota;
            }
            else
            {
                cuota = 0;
            }

            dsdataset.Tables["tbldeducciones"].Rows.Add(
                Codigoter, iConcepto, dNumero, Descripcion, dValor, dInteres, TotPar,
                dValor + dInteres, cuota, clades);

            Vlrdsto += dValor + dInteres;

            this.LblDesembolso.Text = (Convert.ToDouble(LblCredito.Text) - Vlrdsto).ToString();
            if (index >= 0)
            {
                if ((dValor + dInteres) != 0)
                    this.DatGriCreditos.Rows[index].DefaultCellStyle = style2;
                else
                    this.DatGriCreditos.Rows[index].DefaultCellStyle = style1;
            }
            index = -1;
        }

        private void DatGriCreditos_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DatGriCreditos_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            this.TxtLineaRec.Text = this.DatGriCreditos.SelectedCells[0].Value.ToString();
            this.TxtNumRec.Text = this.DatGriCreditos.SelectedCells[1].Value.ToString();
            index = this.DatGriCreditos.CurrentRow.Index;
            this.TxtLineaRec.Focus();
        }

        private void TxtValorRec_LostFocus(object sender, EventArgs e)
        {
            string StTotPar = "T";
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtValorRec.Text))
            {
                this.TxtValorRec.Text = Convert.ToDouble(this.TxtValorRec.Text).ToString("N2");
                // StTotPar = this.msgliqcre.VerificaValoraRecoger( // ERROR: CS1503
                    // this.txtCodigoter.Text, this.TxtLineaRec.Text, this.TxtNumRec.Text, // ERROR: CS1503
                    // this.DtpFecha.Value.ToString("yyyyMM"), this.TxtValorRec.Text, this.mycon); // ERROR: CS1503
                if (StTotPar == "P")
                {
                    this.TxtIntRec.Text = "0";
                }
            }
            else
            {
                this.TxtValorRec.Text = "0";
            }
        }

        private void TxtIntRec_LostFocus(object sender, EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtIntRec.Text))
            {
                this.TxtIntRec.Text = Convert.ToDouble(this.TxtIntRec.Text).ToString("N2");
            }
            else
            {
                this.TxtIntRec.Text = "0";
            }
        }

        private void TxtIntRec_TextChanged(object sender, EventArgs e)
        {
        }

        private void TxtValorRec_TextChanged(object sender, EventArgs e)
        {
        }

        private void DatGriCreditos_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.DatGriCreditos.CurrentRow.Index > -1 && e.Button == MouseButtons.Right)
            {
                this.CtmMenuPendientes.Show(DatGriCreditos, e.Location);
            }
        }

        private void VerCuotasPendientesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string SrLincred = this.DatGriCreditos.Rows[this.DatGriCreditos.CurrentRow.Index].Cells[0].Value.ToString();
            double NumeroObli = Convert.ToDouble(this.DatGriCreditos.Rows[this.DatGriCreditos.CurrentRow.Index].Cells[1].Value);
            // Msgcop.CargarVentanaCuopen(this.txtCodigoter.Text, SrLincred, NumeroObli, // ERROR: CS1503
                // this.DtpFecha.Value.ToString("yyyyMM"), this.mycon, this); // ERROR: CS1503
        }

        private void IncluirConceptoAdicionalToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int linea = 0;
            double numero = 0;
            double valor = 0;

            // this.msgliqcre.CargaVentanaCptosAdicionales(this.txtCodigoter.Text, // ERROR: CS1503
                // this.DtpFecha.Value.ToString("yyyyMM"), this.mycon, this, ref linea, ref numero, ref valor); // ERROR: CS1503

            if (linea > 0)
            {
                this.TxtLineaRec.Text = linea.ToString();
                this.TxtNumRec.Text = numero.ToString();
                this.TxtValorRec.Text = valor.ToString();
                this.TxtIntRec.Text = "0";
            }
        }
    }
}
