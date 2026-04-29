using System;
using ERP.Core.Inventario.Services;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.ComponentModel;

namespace ERP.Core.Inventario.Forms
{
    public partial class Inv_frmforpag : Form
    {
        public struct FormaPago
        {
            public bool Efectivo;
            public bool Cheque;
            public bool Tarjeta;
            public bool DifCuotas;
            public int CodTransa;
            public double secuencia;
            public int Idpunto;
            public int IdTurno;
            public int clades;
            public string Nit;
            public string Usuario;
            public DateTime FecMovto;
            public string CpteCartera;
            public string MovtoPos;
            public double VlrTotal;
            public double vlrSubTotal;
            public double VlrDsto;
            public double VlrIva;
            public string Estado;
            public double VlrArqueo;
            public string detalle;
            public double Factura;
            public DateTime fecvence;
            public double VlrRetfte;
            public double vlrica;
            public int idVendedor;
            public int idtipomovdev;
            public double secuenciadev;
        }

        public FormaPago FormPago;
        public string pstmyconect;

        private msginv msginv = new msginv();
        private ERP.Core.Compartido.Utilidades.Ayuda msgayuda = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private DataSet DsDataSet = new DataSet();
        private ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos msgliqcred = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
        private DataTable DsDataExtras = new DataTable();

        private string LblTotalPrestamo;
        private string LblCargosAdicionales;
        private int CbxCicloDsto;
        private string LblTasaSegRiesgo = "0";
        private int Antigueda;
        private DateTime fechaReing = new DateTime(1950, 1, 1);
        private decimal TasaInteres, _TasaLinea = 0;
        private decimal Dtf = 0;
        private double VlrNetoTemporal = 0;
        private bool ok;
        private string soloundescuento = "N";

        private string empresa_;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string empresa
        {
            get { return empresa_; }
            set { empresa_ = value; }
        }

        public Inv_frmforpag(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void Inv_frmforpag_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Dispose();
                this.Close();
            }
        }

        private void Inv_frmforpag_Load(object sender, EventArgs e)
        {
            InitializaCampos();
        }

        private void InitializaCampos()
        {
            string TrasladaCont = "N";
            if (this.FormPago.Nit == null || this.FormPago.Nit == "99999999999999")
            {
                this.CmbDifCuotas.Enabled = false;
            }
            else
            {
                // this.msginvconfig.BuscaTipomovto(this.FormPago.CodTransa, this.mycon, "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref TrasladaCont); // ERROR: CS7036
                switch (TrasladaCont)
                {
                    case "Y":
                        this.CmbDifCuotas.Enabled = true;
                        break;
                    case "N":
                        this.CmbDifCuotas.Enabled = false;
                        break;
                    case "R":
                        this.CmbDifCuotas.Enabled = true;
                        this.CmbEfectivo.Enabled = false;
                        this.CmbCheque.Enabled = false;
                        this.Cmbtarjeta.Enabled = false;
                        break;
                }
            }

            this.txtEfectivo.Text = "0";
            this.txtValbanco.Text = "0";
            this.txtValorTarjeta.Text = "0";
            this.txtCuota.Text = "0";
            this.TxtPlazo.Text = "0";
            this.CbxPeriodicidad.SelectedIndex = 0;
            this.txttasa1.Text = "0";
            LblTotalPrestamo = "0";
            LblCargosAdicionales = "0";
            CbxCicloDsto = 0;
            LblTasaSegRiesgo = "0";
            Antigueda = 0;
            fechaReing = new DateTime(1950, 1, 1);
            TasaInteres = 0;
            _TasaLinea = 0;
            Dtf = 0;
            VlrNetoTemporal = 0;
            if (this.CmbEfectivo.Enabled == true)
            {
                this.txtEfectivo.Text = this.txtTotal.Text;
            }
        }

        private void CmbEfectivo_Click(object sender, EventArgs e)
        {
            FormPago.Efectivo = true;
            FormPago.Cheque = false;
            FormPago.Tarjeta = false;
            FormPago.DifCuotas = false;

            this.GrbEfectivo.Visible = true;
            this.GrpCheque.Visible = false;
            this.GrpCuotas.Visible = false;
            this.Grptarjeta.Visible = false;

            GrbEfectivo.Location = new Point(12, 12);
            this.txtEfectivo.Focus();
            Cambio();
        }

        private void CmbCheque_Click(object sender, EventArgs e)
        {
            FormPago.Cheque = true;
            FormPago.Efectivo = false;
            FormPago.Tarjeta = false;
            FormPago.DifCuotas = false;

            this.GrbEfectivo.Visible = false;
            this.Grptarjeta.Visible = false;
            this.GrpCheque.Visible = true;
            this.GrpCheque.Location = GrbEfectivo.Location;
            this.GrpCuotas.Visible = false;
            this.txtbanco.Text = "9999";
            this.txtbanco.Focus();
            Cambio();
        }

        private void CmbAceptar_Click(object sender, EventArgs e)
        {
            if (this.FormPago.MovtoPos == "Y")
            {
                if (Convert.ToDouble(this.txtTotal.Text.Trim()) > 0 && this.FormPago.secuencia == 0)
                {
                    msginv.BuscaSecuencia(this.FormPago.CodTransa, this.mycon, ref this.FormPago.secuencia);
                }
            }
            ok = GrabaFormaPago(this.FormPago.CodTransa, this.FormPago.secuencia);
            if (!ok)
            {
                return;
            }
            this.Close();
        }

        private bool GrabaFormaPago(int IdTipoMovto, double Secuencia)
        {
            int ForPago = 0;
            double valDebito = 0, valcredito = 0;
            double Total = 0;
            string Periodo = "999999", Descripcion = " ";
            double Credito = 0;
            string ConseCartera = Convert.ToDouble(this.FormPago.Idpunto.ToString() + this.FormPago.IdTurno.ToString() + this.FormPago.FecMovto.ToString("yyMMdd")).ToString();
            int Clacuo = 0, ClaseI = 0;
            string Cencos = " ";
            decimal TasaInt = 0;
            int Foradmon = 0;
            int CptoCap = 0;
            string CuentaCpte = "999999999999";
            double cambio = 0;
            DateTime Fecdes = new DateTime(1950, 1, 1);
            int CtrlExistencia = 0;
            int k = 0, NumCuota = 0;
            string Codigoter = Microsoft.VisualBasic.Strings.Right("00000000000000" + this.FormPago.Nit, 14);

            if (this.FormPago.Efectivo)
            {
                ForPago = 0;
            }

            if (this.FormPago.Cheque)
            {
                ForPago = 1;
            }

            if (this.FormPago.Tarjeta)
            {
                ForPago = 2;
            }

            if (this.FormPago.DifCuotas)
            {
                ForPago = 3;
                Credito = Convert.ToDouble(this.txtNeto.Text);
            }
            else
            {
                Credito = 0;
            }

            if (this.RbTarjDebito.Checked)
            {
                valDebito = Convert.ToDouble(this.txtValorTarjeta.Text);
            }
            else
            {
                valcredito = Convert.ToDouble(this.txtValorTarjeta.Text);
            }

            if (!Information.IsNumeric(this.txtcant.Text.Trim()))
            {
                this.txtcant.Text = "0";
            }

            this.Cambio(ref Total);

            if (Total < Convert.ToDouble(this.txtNeto.Text) && this.FormPago.DifCuotas == false && Total > 0)
            {
                MessageBox.Show("Total de la factura no esta cancelado");
                return false;
            }

            cambio = Math.Round(Convert.ToDouble(Total) - Convert.ToDouble(this.txtNeto.Text), 0);

            if (this.FormPago.DifCuotas)
            {
                // this.msginvconfig.buscaPeriodo("post", this.mycon, "", "", this.FormPago.FecMovto, "", ref Periodo); // ERROR: CS7036

                // this.msginvconfig.BuscaTipomovto(this.FormPago.CodTransa, this.mycon, ref Descripcion, "", "", "", "", ref CtrlExistencia); // ERROR: CS7036

                if (CtrlExistencia == 0)
                {
                    goto SkipDifCuotas;
                }

                if (!Information.IsNumeric(this.txtCuota.Text))
                {
                    MessageBox.Show("Cuota debe ser numerica", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                else
                {
                    if (Convert.ToDouble(this.txtCuota.Text) <= 0)
                    {
                        MessageBox.Show("Cuota debe ser mayor a cero", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return false;
                    }
                }

                if (Convert.ToDouble(this.TxtPlazo.Text) <= 0)
                {
                    MessageBox.Show("Plazo debe ser mayor a cero", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                string _p4 = "";
                // msgparsys.BuscarCompania(msgcop.varini.sptCodEmpr, this.mycon, "", ref CptoCap); // ERROR: CS7036
                // ok = msgcop.BuscaAsociado(this.FormPago.Nit, this.mycon); // ERROR: CS1620
                if (ok)
                {
                    string _cencos = Cencos;
                    int _clacuo = Clacuo;
                    int _claseI = ClaseI;
                    decimal _tasaInt = TasaInt;
                    int _foradmon = Foradmon;
                    // msgcop.BuscaLinea(this.TxtLinea.Text, mycon, ref _clacuo, ref _claseI, "", ref _cencos, "", ref _tasaInt, "", "", ref _foradmon); // ERROR: CS7036
                    Clacuo = _clacuo;
                    ClaseI = _claseI;
                    Cencos = _cencos;
                    TasaInt = _tasaInt;
                    Foradmon = _foradmon;

                    string _cuentaCpte = CuentaCpte;
                    // msgcop.BuscaComprobante(FormPago.CpteCartera, 0, false, this.mycon, "", "", "", "", "", "", "", "", "", "", "", "", "", ref _cuentaCpte); // ERROR: CS1620
                    CuentaCpte = _cuentaCpte;

                    // msgcop.GrabaNuevoCredito(this.FormPago.Nit, this.TxtLinea.Text, Secuencia, this.FormPago.FecMovto, this.FormPago.FecMovto, this.FormPago.FecMovto, dtpFechapri.Value, this.FormPago.Nit, this.TxtPlazo.Text, this.txtNeto.Text, this.txtNeto.Text, 0, this.txtCuota.Text, TasaInteres, 5, // ERROR: CS1503
                                                     // this.CbxPeriodicidad.SelectedIndex, Clacuo, ClaseI, this.FormPago.Usuario, DateTime.Now, "9999", Cencos, Periodo, this.mycon, "", this.FormPago.clades); // ERROR: CS1503

                    for (k = 0; k <= DsDataExtras.Rows.Count - 1; k++)
                    {
                        DataRow row = DsDataExtras.Rows[k];
                        NumCuota += 1;
                        // msgliqcred.GrabarCuotasExtrasCredito(Codigoter, this.TxtLinea.Text, Secuencia, NumCuota, row["valor"], row["FORPAG"], row["FECHA"], FormPago.FecMovto.ToString("yyyyMM"), row["tipoextra"], mycon); // ERROR: CS1503
                    }

                    // msgcop.GrabaMovimiento(FormPago.CpteCartera, ConseCartera, this.FormPago.Nit, this.TxtLinea.Text, Secuencia, Periodo, CptoCap, this.FormPago.FecMovto, this.txtNeto.Text, 0, Descripcion, this.FormPago.Usuario, this.mycon, "", "", this.FormPago.Nit, "", this.FormPago.Nit); // ERROR: CS1503, CS1620
                }
                else
                {
                    MessageBox.Show("Asociado no existe ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
            }
            SkipDifCuotas:

            // msginv.GrabaFormaPago(IdTipoMovto, Secuencia, ForPago, this.txtEfectivo.Text, this.txtValbanco.Text, valDebito, valcredito, this.txtbanco.Text, this.txtcant.Text, this.txtcuenta.Text, Credito, this.CbxPeriodicidad.SelectedIndex, this.TxtPlazo.Text, this.FormPago.clades, dtpFechapri.Value, this.txtCuota.Text, TasaInt, // ERROR: CS7036
            // this.FormPago.Nit, this.FormPago.FecMovto, LblCambio.Text, this.FormPago.MovtoPos, this.FormPago.Usuario, this.FormPago.VlrTotal, this.FormPago.vlrSubTotal, this.FormPago.VlrDsto, this.FormPago.VlrIva, this.FormPago.Estado, this.FormPago.Idpunto, this.FormPago.IdTurno, "", "", this.FormPago.Factura); // ERROR: CS7036
            return true;
        }

        private void Label8_Click(object sender, EventArgs e)
        {
        }

        private void Cmbtarjeta_Click(object sender, EventArgs e)
        {
            FormPago.Cheque = false;
            FormPago.Efectivo = false;
            FormPago.Tarjeta = true;
            FormPago.DifCuotas = false;

            this.GrbEfectivo.Visible = false;
            this.GrpCheque.Visible = false;
            this.Grptarjeta.Visible = true;
            this.GrpCuotas.Visible = false;
            this.Grptarjeta.Location = GrbEfectivo.Location;
            this.txtNoTarjeta.Focus();
            Cambio();
        }

        private void txtEfectivo_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.txtEfectivo.Text))
            {
                this.txtEfectivo.Text = "0";
            }
        }

        private void Cambio()
        {
            double dummy = 0;
            Cambio(ref dummy);
        }

        private void Cambio(ref double total)
        {
            if (!Information.IsNumeric(this.txtEfectivo.Text))
            {
                this.txtEfectivo.Text = "0";
            }
            if (!Information.IsNumeric(this.txtNeto.Text))
            {
                this.txtNeto.Text = "0";
            }
            if (!Information.IsNumeric(this.txtValbanco.Text))
            {
                this.txtValbanco.Text = "0";
            }
            if (!Information.IsNumeric(this.txtValorTarjeta.Text))
            {
                this.txtValorTarjeta.Text = "0";
            }

            this.txtTotal.Text = Strings.FormatNumber(Convert.ToDouble(this.txtEfectivo.Text) + Convert.ToDouble(this.txtValbanco.Text) + Convert.ToDouble(this.txtValorTarjeta.Text), 2);
            this.LblCambio.Text = Strings.FormatNumber(Convert.ToDouble(this.txtTotal.Text) - Convert.ToDouble(this.txtNeto.Text), 2);
            total = Convert.ToDouble(this.txtTotal.Text);
        }

        private void txtValorTarjeta_Validated(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.txtValorTarjeta.Text))
            {
                this.txtValorTarjeta.Text = "0";
            }
            Cambio();
        }

        private void HelpBanco_Click(object sender, EventArgs e)
        {
            this.txtbanco.Text = msgayuda.CargaAyuda("sys_banco03", "CODIGO_BANCO", "NOMBRE", "NOMRES", this.mycon, this);
            this.txtbanco.Focus();
        }

        private void CmbDifCuotas_Click(object sender, EventArgs e)
        {
            BuscaDatosDifCuotas();
        }

        private void BuscaDatosDifCuotas()
        {
            ERP.Core.Contabilidad.Services.ClsContabilidad Msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            int TipoTercero = 0;
            string ClientePatronal = "N", StPeriodd = "0";
            DataSet datasetcompania = new DataSet();
            // this.msgparsys.BuscarCompania("0001", ref datasetcompania, mycon); // ERROR: CS1620

            DataRow compRow = datasetcompania.Tables["tblcompania"].Rows[0];
            switch (compRow["r_tasa"].ToString().Trim())
            {
                case "Y":
                    this.txttasa1.Enabled = false;
                    break;
                default:
                    this.txttasa1.Enabled = true;
                    break;
            }

            FormPago.Cheque = false;
            FormPago.Efectivo = false;
            FormPago.Tarjeta = false;
            FormPago.DifCuotas = true;
            FormPago.CpteCartera = "9999";
            this.TxtLinea.Text = "9999";

            string _p5 = "";
            string _p6 = "";
            string _p7 = "";
            string _p8 = "";
            int _tipoTercero = TipoTercero;
            string _clientePatronal = ClientePatronal;
            // ok = Msgcnt.BuscarTercero(this.FormPago.Nit, mycon, ref _p5, ref _p6, ref _p7, ref _p8, ref _tipoTercero, "", ref _clientePatronal); // ERROR: CS7036
            TipoTercero = _tipoTercero;
            ClientePatronal = _clientePatronal;
            if (!ok)
            {
                MessageBox.Show("Cliente no existe como tercero, por favor comuniquese con el administrador", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string codigoterc = this.FormPago.Nit;
            // ok = msgcop.BuscaAsociado(codigoterc, this.mycon, "", "", "", "", "", "", "", "", "", "", "", "", "", ref StPeriodd); // ERROR: CS7036
            if (!ok)
            {
                MessageBox.Show("Cliente no existe como asociado o afiliado, por favor comuniquese con el administrador", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            else
            {
                DateTime FecMaxNov = default(DateTime);
                DialogResult Resul = DialogResult.Yes;
                // msgcop.BuscaFecMaxNovedad(codigoterc, this.mycon, ref FecMaxNov); // ERROR: CS1503, CS1620

                if (this.FormPago.FecMovto <= FecMaxNov)
                {
                    Resul = MessageBox.Show("Asociado esta en novedad hasta : " + FecMaxNov.ToString() + "\r\n" + "Desea registrarlos de todos modos ?", "SOLIDO", MessageBoxButtons.YesNo);
                    if (Resul == DialogResult.No)
                    {
                        return;
                    }
                }
            }

            DataSet datasetasociado = new DataSet();
            double salario = 0;
            string clasecupo = "";
            double valorCupo = 0;
            double valorTotalCupo = 0;
            string saldoCredito = "  ";
            string periodo = "";
            string codigoter = this.FormPago.Nit;
            // this.msginvconfig.buscaPeriodo("post", this.mycon, "", "", this.FormPago.FecMovto, "", ref periodo); // ERROR: CS7036
            // ok = msgparcop.BuscaAsociado(codigoter, ref datasetasociado, this.mycon); // ERROR: CS1620
            if (ok)
            {
                if (datasetasociado.Tables["tblasociados"].Rows.Count > 0)
                {
                    DataRow asocRow = datasetasociado.Tables["tblasociados"].Rows[0];
                    salario = Convert.ToDouble(asocRow["salario"].ToString());
                    clasecupo = asocRow["clasecupoPos"].ToString();
                    valorCupo = Convert.ToDouble(asocRow["valorClaseCupo"].ToString());
                    LblTasaSegRiesgo = asocRow["SeguroRiesgo"].ToString();
                    empresa = asocRow["empresa"].ToString();

                    if (!Convert.IsDBNull(asocRow["FECHA_REINGRESO"]))
                    {
                        fechaReing = Convert.ToDateTime(asocRow["FECHA_REINGRESO"]);
                    }
                    if (fechaReing != Convert.ToDateTime("01/01/1950") && fechaReing > Convert.ToDateTime(asocRow["fecha_ingreso"]))
                    {
                        Antigueda = (int)Microsoft.VisualBasic.DateAndTime.DateDiff(DateInterval.Month, fechaReing, this.FormPago.FecMovto);
                    }
                    else
                    {
                        Antigueda = (int)Microsoft.VisualBasic.DateAndTime.DateDiff(DateInterval.Month, Convert.ToDateTime(asocRow["fecha_ingreso"]), this.FormPago.FecMovto);
                    }
                }
                if (valorCupo > 0)
                {
                    // msginv.BuscarSaldoCredito(this.mycon, this.FormPago.Nit, periodo, this.FormPago.CodTransa, ref saldoCredito); // ERROR: CS1503
                    if (Information.IsNumeric(saldoCredito) == false)
                    {
                        saldoCredito = "0";
                    }
                    switch (clasecupo)
                    {
                        case "P":
                            valorTotalCupo = ((salario * valorCupo) / 100) - (Convert.ToDouble(saldoCredito) + Convert.ToDouble(txtTotal.Text));
                            break;
                        case "V":
                            valorTotalCupo = Convert.ToDouble(valorCupo) - (Convert.ToDouble(saldoCredito) + Convert.ToDouble(txtTotal.Text));
                            break;
                    }
                    if (valorTotalCupo <= 0)
                    {
                        MessageBox.Show("No tiene cupo para poder diferir en cuotas");
                        return;
                    }
                }
            }

            if (Information.IsNumeric(StPeriodd))
            {
                CbxPeriodicidad.SelectedIndex = Convert.ToInt32(StPeriodd);
            }
            else
            {
                CbxPeriodicidad.SelectedIndex = 0;
            }

            this.TxtPlazo.Text = "0";
            if (FormPago.IdTurno > 0)
            {
                switch (TipoTercero)
                {
                    case 7:
                        // msginvconfig.BuscaDatosFacturacion(1, this.mycon, "", "", "", "", "", "", "", "", "", "", "", "", ref FormPago.CpteCartera, ref this.TxtLinea.Text, ref FormPago.clades, ref this.TxtPlazo.Text, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref soloundescuento); // ERROR: CS0206
                        break;
                    default:
                        if (ClientePatronal == "Y")
                        {
                            // msginvconfig.BuscaDatosFacturacion(1, this.mycon, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref FormPago.CpteCartera, ref this.TxtLinea.Text, ref FormPago.clades, ref this.TxtPlazo.Text, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref soloundescuento); // ERROR: CS0206
                        }
                        else
                        {
                            // msginvconfig.BuscaDatosFacturacion(1, this.mycon, "", "", "", "", "", "", "", "", ref FormPago.CpteCartera, ref this.TxtLinea.Text, ref FormPago.clades, ref this.TxtPlazo.Text, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref soloundescuento); // ERROR: CS0206
                        }
                        break;
                }
            }
            else
            {
                // msginvconfig.BuscaDatosFacturacion(2, this.mycon, "", "", "", "", "", "", "", "", "", ref this.TxtLinea.Text, ref FormPago.clades, ref this.TxtPlazo.Text, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref soloundescuento); // ERROR: CS0206
            }

            if (this.CbxPeriodicidad.SelectedIndex > 0)
            {
                GrpCuotas.Enabled = false;
                if (Information.IsNumeric(this.TxtPlazo.Text))
                {
                    if (Convert.ToDouble(this.TxtPlazo.Text) == 0)
                    {
                        this.txtCuota.Text = "0";
                        GrpCuotas.Enabled = true;
                        this.TxtLinea.Enabled = false;
                        this.txtCuota.Enabled = false;
                        this.HelpLinea.Enabled = false;
                        this.CbxPeriodicidad.Enabled = false;
                        this.TxtPlazo.Enabled = true;
                    }
                }
            }
            else
            {
                GrpCuotas.Enabled = true;
            }

            DateTime Fecdes2 = new DateTime(1950, 1, 1);
            this.GrbEfectivo.Visible = false;
            this.Grptarjeta.Visible = false;
            this.GrpCheque.Visible = false;
            this.GrpCuotas.Visible = true;
            Fecdes2 = Convert.ToDateTime(this.FormPago.FecMovto.ToString("yyyy") + "/" + this.FormPago.FecMovto.ToString("MM") + "/15");
            Fecdes2 = Fecdes2.AddMonths(1);
            dtpFechapri.Value = Fecdes2;
            // msgparcop.BuscaLinea(this.TxtLinea.Text, ref this.DsDataSet, mycon); // ERROR: CS1615, CS1620
            DataRow lineaRow = DsDataSet.Tables["tbllineas"].Rows[0];
            _TasaLinea = Math.Round(Convert.ToDecimal(lineaRow["tasai"]), 4);
            this.txttasa1.Text = _TasaLinea.ToString();
            Dtf = Convert.ToDecimal(lineaRow["Dtf"]);
            VlrNetoTemporal = Convert.ToDouble(this.txtNeto.Text);
            this.TxtLinea.Focus();
            FinanciarCuotas();
        }

        private void txtValbanco_TextChanged(object sender, EventArgs e)
        {
        }

        private void HelpLinea_Click(object sender, EventArgs e)
        {
            this.TxtLinea.Text = msgayuda.CargaAyuda("cop_concar12", "LINCRED", "DESCRIPCION", "", this.mycon, this, "Descripcion", "         ");
            this.TxtLinea.Focus();
        }

        private void CbxPeriodicidad_LostFocus(object sender, EventArgs e)
        {
            FinanciarCuotas();
        }

        private void FinanciarCuotas()
        {
            decimal TasaSeg = 0, TasaAdmon = 0;
            int Period = CbxPeriodicidad.SelectedIndex;
            int Forseg = 0;
            decimal IntSeg = 0;
            int forAdmon = 0;
            decimal IntAdm = 0;
            int tipointeres = 0;
            decimal TasaInt = 0;
            this.txtCuota.Text = "0";
            double VLRCREDITO = Convert.ToDouble(this.txtNeto.Text);
            double VALSEGMIN = 0;
            double VALSEGMAX = 999999999;

            DataRow lineaRow = DsDataSet.Tables["tbllineas"].Rows[0];
            Forseg = Convert.ToInt32(lineaRow["poapen"]);
            TasaSeg = Convert.ToDecimal(lineaRow["seguro"]);
            forAdmon = Convert.ToInt32(lineaRow["claAdmon"]);
            TasaAdmon = Convert.ToDecimal(lineaRow["tasadm"]);
            tipointeres = Convert.ToInt32(lineaRow["tipointeres"]);

            VALSEGMIN = Convert.ToDouble(lineaRow["valsegMin"]);
            VALSEGMAX = Convert.ToDouble(lineaRow["valsegMax"]);

            if (Convert.ToDouble(Information.IsNumeric(LblTasaSegRiesgo) ? LblTasaSegRiesgo : "0") > 0)
            {
                TasaSeg = Convert.ToDecimal(LblTasaSegRiesgo);
            }
            else
            {
                TasaSeg = Convert.ToDecimal(lineaRow["seguro"]);
            }

            if (Forseg == 3)
            {
                if (VLRCREDITO >= VALSEGMIN && VLRCREDITO <= VALSEGMAX)
                {
                    IntSeg = Math.Round((TasaSeg / 100) / Period, 8);
                }
                else
                {
                    IntSeg = 0;
                }
            }

            if (forAdmon == 2)
            {
                IntAdm = Math.Round((TasaAdmon / 100) / Period, 8);
            }

            TasaInt = CalculatasaInteres(tipointeres, Period);

            string _txtCuota = this.txtCuota.Text;
            string _txtPlazo = this.TxtPlazo.Text;
            // this.msginv.CalculaCuotas(this.TxtLinea.Text, this.CbxPeriodicidad.SelectedIndex, this.txtNeto.Text, this.mycon, ref _txtCuota, ref _txtPlazo, TasaInt, IntSeg, IntAdm, soloundescuento); // ERROR: CS1503, CS1615
            this.txtCuota.Text = _txtCuota;
            this.TxtPlazo.Text = _txtPlazo;

            DateTime fechadsto = default(DateTime);
            string cladsto = " ";
            int ciclodsto = 0;

            switch (this.FormPago.clades)
            {
                case 1:
                    cladsto = "N";
                    break;
                case 2:
                    cladsto = "C";
                    break;
            }
            switch (this.CbxPeriodicidad.SelectedIndex)
            {
                case 1:
                    ciclodsto = 0;
                    break;
                case 2:
                    ciclodsto = 2;
                    break;
                case 3:
                    ciclodsto = 3;
                    break;
                case 4:
                    ciclodsto = 0;
                    break;
            }

            if (msgliqcred.BuscarPeriocidadEmpresa(this.mycon, empresa, this.CbxPeriodicidad.SelectedIndex, ciclodsto, cladsto, DateTime.Now, ref fechadsto))
            {
                dtpFechapri.Value = fechadsto;
                this.dtpFechapri.Enabled = false;
            }
            else
            {
                this.dtpFechapri.Enabled = true;
            }
        }

        private void CbxPeriodicidad_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void TxtPlazo_LostFocus(object sender, EventArgs e)
        {
            decimal TasaPlazo = 0;
            double PlazoMaximo = 0;
            double MontoMaximo = 0;

            if (!Information.IsNumeric(this.TxtPlazo.Text))
            {
                this.TxtPlazo.Text = "0";
            }

            if (Information.IsNumeric(this.TxtLinea.Text))
            {
                // TasaPlazo = msgparcop.BuscarTasasporPlazos(this.TxtLinea.Text, this.TxtPlazo.Text, this.txtNeto.Text, Antigueda, mycon, ref PlazoMaximo, ref MontoMaximo, 0); // ERROR: CS1061
                if (TasaPlazo > 0)
                {
                    TasaInteres = Math.Round(TasaPlazo, 4);
                }
                else
                {
                    TasaInteres = _TasaLinea;
                }
                // TasaInteres = msgliqcred.CalculaTasaPeriodica(this.TxtLinea.Text, this.TxtPlazo.Text, TasaInteres, mycon); // ERROR: CS1503
                this.txttasa1.Text = TasaInteres.ToString();
            }
            this.FinanciarCuotas();
        }

        private void dtpFechapri_LostFocus(object sender, EventArgs e)
        {
        }

        private void dtpFechapri_ValueChanged(object sender, EventArgs e)
        {
        }

        private void btnExtras_Click(object sender, EventArgs e)
        {
            LiquidaCargosAdicionales();
            CargaCuotasextras();
        }

        private void LiquidaCargosAdicionales()
        {
            double Vlradmon = 0, Vlrseguro = 0, VlrPrestamo = 0;
            double TasaSeg = 0;
            DataRow lineaRow = DsDataSet.Tables["tbllineas"].Rows[0];
            VlrPrestamo = Convert.ToDouble(this.txtNeto.Text);

            // Vlradmon = msgliqcred.LiquidaAdmon(lineaRow["foradmon"], lineaRow["claAdmon"], lineaRow["tasadm"], lineaRow["valadmin"], lineaRow["valadmax"], VlrPrestamo, 0, 0); // ERROR: CS1503
            if (Convert.ToInt32(lineaRow["sumaga"]) == 2)
            {
                VlrPrestamo += Vlradmon;
            }
            if (Convert.ToDouble(Information.IsNumeric(LblTasaSegRiesgo) ? LblTasaSegRiesgo : "0") > 0)
            {
                TasaSeg = Convert.ToDouble(LblTasaSegRiesgo);
            }
            else
            {
                TasaSeg = Convert.ToDouble(lineaRow["seguro"]);
            }
            // Vlrseguro = msgliqcred.LiquidaSeguro(lineaRow["poapen"], Convert.ToDouble(this.txtNeto.Text), TasaSeg, this.TxtPlazo.Text, lineaRow["previv"]); // ERROR: CS1503
            if (Convert.ToInt32(lineaRow["sumaga"]) == 2)
            {
                VlrPrestamo += Vlrseguro;
            }

            LblTotalPrestamo = Strings.FormatNumber(VlrPrestamo, 0);
            LblCargosAdicionales = Strings.FormatNumber(Vlradmon + Vlrseguro, 0);
        }

        private void CargaCuotasextras()
        {
            DateTime fechaPrimerDescuento = dtpFechapri.Value;
            DateTime fechaFinal = default(DateTime);
            CbxCicloDsto = 5;
            switch (CbxPeriodicidad.SelectedIndex)
            {
                case 2:
                    CbxCicloDsto = 2;
                    if (CbxCicloDsto == 2)
                    {
                        DataSet CicloFecha = new DataSet();
                        CicloFecha.Tables.Add("Ciclofechas");
                        CicloFecha.Tables["Ciclofechas"].Columns.Add("fecha", fechaFinal.GetType());
                        // msgliqcred.CambiaCicloaFecha(this.dtpFechapri.Value, CbxPeriodicidad.SelectedIndex, Convert.ToInt32(this.TxtPlazo.Text), CicloFecha, 2, 1); // ERROR: CS1503, CS1620

                        int finalregistro = CicloFecha.Tables["Ciclofechas"].Rows.Count - 1;
                        fechaFinal = Convert.ToDateTime(CicloFecha.Tables["Ciclofechas"].Rows[finalregistro]["fecha"]);
                    }
                    else
                    {
                        fechaFinal = fechaPrimerDescuento.AddMonths(Convert.ToInt32(this.TxtPlazo.Text) - 1);
                    }
                    break;
                default:
                    fechaFinal = fechaPrimerDescuento.AddMonths(Convert.ToInt32(this.TxtPlazo.Text) - 1);
                    break;
            }

            // DsDataExtras = msgliqcred.cargaCuotasextras(LblTotalPrestamo, this.DsDataSet.Tables["tbllineas"].Rows[0]["tasaex"], DsDataExtras, this, "", fechaFinal, dtpFechapri.Value); // ERROR: CS1503

            if (DsDataExtras.Rows.Count >= 1)
            {
                GeneraExtras();
            }
        }

        private void GeneraExtras()
        {
            string Codigoter = this.FormPago.Nit;
            Codigoter = Microsoft.VisualBasic.Strings.Right("00000000000000" + Codigoter, 14);
            string CicloPriDsto;
            int Period = CbxPeriodicidad.SelectedIndex;
            double VlrVpn = 0;
            int Clacuo = 0;
            decimal TasaInt = 0;
            int tipointeres = 0;
            decimal Plazo = Convert.ToDecimal(this.TxtPlazo.Text);
            decimal TasaSeg = 0;
            decimal IntSeg = 0;
            int Forseg = 0;
            int forAdmon = 0;
            decimal TasaAdmon = 0;
            decimal IntAdm = 0;
            double MesGracias;
            double txtNeto_tmp = 0;

            double VLRCREDITO = Convert.ToDouble(this.txtNeto.Text);
            double VALSEGMIN = 0;
            double VALSEGMAX = 999999999;

            try
            {
                if (DsDataExtras.Rows.Count == 0)
                {
                    CreaTablaExtras(DsDataExtras, Codigoter);
                }
            }
            catch (Exception)
            {
                CreaTablaExtras(DsDataExtras, Codigoter);
            }

            DataRow lineaRow = DsDataSet.Tables["tbllineas"].Rows[0];
            Clacuo = Convert.ToInt32(lineaRow["CLACUO"]);
            tipointeres = Convert.ToInt32(lineaRow["tipointeres"]);
            Forseg = Convert.ToInt32(lineaRow["poapen"]);
            TasaSeg = Convert.ToDecimal(lineaRow["seguro"]);
            forAdmon = Convert.ToInt32(lineaRow["claAdmon"]);
            TasaAdmon = Convert.ToDecimal(lineaRow["tasadm"]);
            MesGracias = Convert.ToDouble(lineaRow["mesgracia"]);
            VALSEGMIN = Convert.ToDouble(lineaRow["valsegMin"]);
            VALSEGMAX = Convert.ToDouble(lineaRow["valsegMax"]);

            if (Convert.ToDouble(Information.IsNumeric(LblTasaSegRiesgo) ? LblTasaSegRiesgo : "0") > 0)
            {
                TasaSeg = Convert.ToDecimal(LblTasaSegRiesgo);
            }
            else
            {
                TasaSeg = Convert.ToDecimal(lineaRow["seguro"]);
            }

            TasaInt = CalculatasaInteres(tipointeres, Period);

            if (Forseg == 3)
            {
                if (VLRCREDITO >= VALSEGMIN && VLRCREDITO <= VALSEGMAX)
                {
                    IntSeg = Math.Round((TasaSeg / 100) / Period, 8);
                }
                else
                {
                    IntSeg = 0;
                }
            }

            if (forAdmon == 2)
            {
                IntAdm = Math.Round((TasaAdmon / 100) / Period, 8);
            }

            // CicloPriDsto = msgliqcred.CalculaCiclo(5, CbxPeriodicidad.SelectedIndex, dtpFechapri.Value); // ERROR: CS1503
            // VlrVpn = msgliqcred.CalculaVp(DsDataExtras, Period, this.FormPago.FecMovto, Clacuo, TasaInt + IntSeg + IntAdm, this.mycon, (double)(Plazo + (decimal)MesGracias), CicloPriDsto); // ERROR: CS1503

            if (VlrVpn < Convert.ToDouble(this.txtNeto.Text))
            {
                txtNeto_tmp = VlrNetoTemporal - VlrVpn;
                this.txtCuota.Text = "0";
                string _txtCuota = this.txtCuota.Text;
                string _txtPlazo = this.TxtPlazo.Text;
                // this.msginv.CalculaCuotas(this.TxtLinea.Text, this.CbxPeriodicidad.SelectedIndex, txtNeto_tmp, this.mycon, ref _txtCuota, ref _txtPlazo, TasaInt, IntSeg, IntAdm); // ERROR: CS7036
                this.txtCuota.Text = _txtCuota;
                this.TxtPlazo.Text = _txtPlazo;
            }
            else
            {
                FinanciarCuotas();
            }
        }

        private void CreaTablaExtras(DataTable DsTablaExtras, string codigoter)
        {
            string ststring = " ";
            int StInteger = 0;
            double StDouble = 0;
            DateTime Stdate = default(DateTime);

            try
            {
                DsTablaExtras.TableName = "tblextras";
                DsTablaExtras.Columns.Add("FECHA", Stdate.GetType());
                DsTablaExtras.Columns.Add("VALOR", StInteger.GetType());
                DsTablaExtras.Columns.Add("DESCPAG", ststring.GetType());
                DsTablaExtras.Columns.Add("FORPAG", StInteger.GetType());
                DsTablaExtras.Columns.Add("tipoextra", ststring.GetType());
                DsTablaExtras.Rows.Add(DateTime.Now, 0, 0, 0, " ");
            }
            catch (Exception)
            {
            }
        }

        private decimal CalculatasaInteres(int tipointeres, int Period)
        {
            decimal TasaInt = 0;
            decimal PuntosAdic = 0;

            switch (tipointeres)
            {
                case 1:
                    PuntosAdic = TasaInteres;
                    TasaInteres = msgliqcred.ConversionDTFaNMV(Dtf, PuntosAdic);
                    break;
            }

            TasaInt = Math.Round((TasaInteres / Period) / 100, 8);

            if (CbxPeriodicidad.SelectedIndex == 4)
            {
                TasaInt = Math.Round((TasaInteres / 30) / 100, 8);
            }

            return TasaInt;
        }

        private void GrpCuotas_Enter(object sender, EventArgs e)
        {
        }
    }
}
