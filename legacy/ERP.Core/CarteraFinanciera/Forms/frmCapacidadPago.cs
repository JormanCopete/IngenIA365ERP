using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.ComponentModel;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmCapacidadPago : Form
    {
        ERP.Core.CarteraFinanciera.Models.ParamCop ParamCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos ClsCarteraLiq = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
        ERP.Core.Compartido.Configuracion.ParamSys Foco = new ERP.Core.Compartido.Configuracion.ParamSys();

        //Dim conexion As New Modu
        double StDouble = 0;
        bool sbolean;
        string strstring = "";
        double sumaingresos;
        double sumaegresos;
        private string _TipoAsociado;
        double _cuotasdeudas = 0, _saldoDeudaRecogida = 0;
        string TipoDscto = "0";
        double PorDscto = 0;
        ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera clscartera = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        ERP.Core.CarteraFinanciera.Models.ParamCop msgparacop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private string _codigoAsociado;
        private string _codigoEmpresa;
        private string _NombreUsuario;
        private string _Empresa;
        private double _valorsolicitado;
        public DataSet DsdataCapaPago = new DataSet();
        double TxtSolPasDeudas;
        double TxtSolActAportes;
        double SaldoAohorroSuper = 0;
        double ValorDeudasRecogida;
        ListView Lstdeudasrec = new ListView();
        public DataSet dsbienesCapacidaPago = new DataSet();
        double lineaCredito_;
        double _cuotasdeudasCaja = 0;
        double _totalCuotaRecogida = 0;
        double _cuotasNuevoCredito = 0;
        double _salario_compania = 0;

        public frmCapacidadPago(OdbcConnection conexion)
        {
            InitializeComponent();
            myconnect = conexion;
        }

        public void AbrirConexion()
        {
            if (this.myconnect.State != ConnectionState.Open)
            {
                this.myconnect.Open();
            }
            //Periodo = 999999
            //msgcop.buscaPeriodo("copc", mycon, , , , , Periodo, Now.Year)
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TipoAsociado
        {
            get { return _TipoAsociado; }
            set
            {
                if (value == null)
                {
                    _TipoAsociado = "0";
                }
                else
                {
                    _TipoAsociado = value;
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string codigoAsociado
        {
            get { return _codigoAsociado; }
            set { _codigoAsociado = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string codigoEmpresa
        {
            get { return _codigoEmpresa; }
            set
            {
                if (value == null)
                {
                    _codigoEmpresa = "9999";
                }
                else
                {
                    _codigoEmpresa = value;
                }
            }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string NombreUsuario 
        {
            get { return _NombreUsuario; }
            set { _NombreUsuario = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string NombreEmpresa 
        {
            get { return _Empresa; }
            set { _Empresa = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double valorSolicitado   
        {
            get { return _valorsolicitado; }
            set { _valorsolicitado = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double CuotaRecogida
        {
            get { return _cuotasdeudas; }
            set { _cuotasdeudas = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double saldoDeudaRecogida
        {
            get { return _saldoDeudaRecogida; }
            set { _saldoDeudaRecogida = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int lineaCredito
        {
            get { return (int)lineaCredito_; }
            set { lineaCredito_ = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double CuotaRecogidaCaja
        {
            get { return _cuotasdeudasCaja; }
            set { _cuotasdeudasCaja = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double TotalCuotaRecogida
        {
            get { return _totalCuotaRecogida; }
            set { _totalCuotaRecogida = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double cuotasNuevoCredito
        {
            get { return _cuotasNuevoCredito; }
            set { _cuotasNuevoCredito = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double salario_compania
        {
            get { return _salario_compania; }
            set { _salario_compania = value; }
        }

        private void frmCapacidadPago_KeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
        {
            //Foco.ejecutarFocusFormulario(sender, e, this);
        }

        private void frmCapacidadPago_Load(object sender, System.EventArgs e)
        {
            this.KeyPreview = true;
            DataSet DsDatacompania = new DataSet();
            AbrirConexion();
            Foco.BuscarCompania("0001", DsDatacompania, myconnect);
            if (DsDatacompania.Tables["tblcompania"].Rows.Count > 0)
            {
                salario_compania = Convert.ToDouble(DsDatacompania.Tables["tblcompania"].Rows[0]["salario_minimo"]);
            }
            else
            {
                salario_compania = 0;
            }
            this.TxtIngBasico.Focus();
            this.UserControlInferiorFormularios1.obtenerDatosTxtInferiores(this);
            this.UserControlInferiorFormularios1.NombreUsuario = this.NombreUsuario;
            this.LblNomEmpresa.Text = this.NombreEmpresa;
            //msgparacop.BuscaEmpresa(codigoEmpresa, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref PorDscto, ref TipoDscto);
            this.LblPorcentajePagaduria.Text = Convert.ToDouble(PorDscto).ToString("N2");
            //clscartera.SaldoDeudasRecogidas(codigoAsociado, myconnect, cuotasdeudas)

            if (this.TotalCuotaRecogida > 0)
            {
                CbxRecDeudas.SelectedIndex = 1;
                CbxRecDeudas_Validated(null, null);
            }
            else
            {
                CbxRecDeudas.SelectedIndex = 0;
                CbxRecDeudas_Validated(null, null);
            }

            //this.lblCapacidadRiesgovlr.Text = ClsCarteraLiq.CalculaCapitalRiesgo(this.codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect, this.lineaCredito);
            this.lblCapacidadRiesgovlr.Text = Convert.ToDouble(this.lblCapacidadRiesgovlr.Text).ToString("N0");



            CargarDatosClasificacion();
            calcularSeguridadSocial();
            calcularIngresosEgresos();
            CargarDatosSolvenciaActivosPasivos();


        }

        private void CmbGuardar_Click(object sender, System.EventArgs e)
        {
            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    if (Convert.ToDouble(this.TxtGastosPnales.Text) <= 0 || Convert.ToDouble(this.TxtGastosPnales.Text) >= 100)
                    {
                        MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "Informacion", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.TxtGastosPnales.Text = "0";
                        this.TxtGastosPnales.Focus();
                        return;
                    }
                    break;
            }

            DsdataCapaPago.Tables.Add("DsdataCapa");
            DataColumnCollection cols = DsdataCapaPago.Tables["DsdataCapa"].Columns;
            cols.Add("Salario", StDouble.GetType());
            cols.Add("otro_ingreso", StDouble.GetType());
            cols.Add("CONYSALAR", StDouble.GetType());
            cols.Add("IngVariables", StDouble.GetType());
            cols.Add("IngArriendos", StDouble.GetType());
            cols.Add("IngPension", StDouble.GetType());
            cols.Add("DeudasTerceros", StDouble.GetType());
            cols.Add("DstoParafiscales", StDouble.GetType());
            cols.Add("DstoPension", StDouble.GetType());
            cols.Add("Dsctos", StDouble.GetType());
            cols.Add("dsGastoper", StDouble.GetType());
            cols.Add("DeuCoopNomi", StDouble.GetType());
            cols.Add("DeuCoopCaja", StDouble.GetType());
            cols.Add("Porcn", sbolean.GetType());
            cols.Add("CbxRecDeudas", strstring.GetType());

            cols.Add("TxtSolActVivienda", StDouble.GetType());
            cols.Add("TxtSolActVehiculo", StDouble.GetType());
            cols.Add("TxtSolActOtros", StDouble.GetType());
            cols.Add("TxtActCtaBanco", StDouble.GetType());
            cols.Add("TxtActCxC", StDouble.GetType());

            cols.Add("TxtSolPasOtros", StDouble.GetType());
            cols.Add("TxtPasObliBanca", StDouble.GetType());
            cols.Add("TxtPasObliHipot", StDouble.GetType());

            DsdataCapaPago.Tables["DsdataCapa"].Rows.Add(this.TxtIngBasico.Text, this.TxtOtrosIng.Text, this.TxtIngConyugue.Text, this.TxtIngVariables.Text, this.TxtIngArriendos.Text, this.TxtIngPensiones.Text
             , this.TxtDeuTerceros.Text, this.TxtDstoParafiscales.Text, this.TxtDstoPension.Text, this.TxtDsctos.Text, this.TxtGastosPnales.Text, TxtDeuCoopNomi.Text
             , TxtDeuCoopCaja.Text, this.ChkPorcentaje.Checked, this.CbxRecDeudas.Text
             , TxtSolActVivienda.Text, TxtSolActVehiculo.Text, TxtSolActOtros.Text, TxtActCtaBanco.Text, TxtActCxC.Text
             , TxtSolPasOtros.Text, TxtPasObliBanca.Text, TxtPasObliHipot.Text);

            if (dsbienesCapacidaPago.Tables.Count > 0)
            {
                DsdataCapaPago.Tables.Add(this.dsbienesCapacidaPago.Tables["tblBienesRaices"].Copy());
                DsdataCapaPago.Tables.Add(this.dsbienesCapacidaPago.Tables["tblVehiculo"].Copy());
            }

            this.Close();
        }


        void validaringresos()
        {
            if (TxtIngBasico.Text.Length == 0 || TxtIngBasico.Text == "")
            {
                this.TxtIngBasico.Text = "0";
            }
            if (this.TxtIngConyugue.Text.Length == 0 || this.TxtIngConyugue.Text == "")
            {
                this.TxtIngConyugue.Text = "0";
            }
            if (this.TxtOtrosIng.Text.Length == 0 || this.TxtOtrosIng.Text == "")
            {
                this.TxtOtrosIng.Text = "0";
            }

            if (this.TxtIngVariables.Text.Length == 0 || this.TxtIngVariables.Text == "")
            {
                this.TxtIngVariables.Text = "0";
            }
            if (this.TxtIngArriendos.Text.Length == 0 || this.TxtIngArriendos.Text == "")
            {
                this.TxtIngArriendos.Text = "0";
            }


            if (this.TxtIngPensiones.Text.Length == 0 || this.TxtIngPensiones.Text == "")
            {
                this.TxtIngPensiones.Text = "0";
            }

            if (this.TxtTotIngresos.Text.Length == 0 || this.TxtTotIngresos.Text == "")
            {
                this.TxtTotIngresos.Text = "0";
            }
        }

        void formatearTexbos()
        {
            TxtIngBasico.Text = Convert.ToDouble(this.TxtIngBasico.Text).ToString("N0");
            TxtIngConyugue.Text = Convert.ToDouble(this.TxtIngConyugue.Text).ToString("N0");
            TxtOtrosIng.Text = Convert.ToDouble(this.TxtOtrosIng.Text).ToString("N0");
            TxtIngVariables.Text = Convert.ToDouble(this.TxtIngVariables.Text).ToString("N0");
            TxtIngArriendos.Text = Convert.ToDouble(this.TxtIngArriendos.Text).ToString("N0");
            TxtIngPensiones.Text = Convert.ToDouble(this.TxtIngPensiones.Text).ToString("N0");
            TxtTotIngresos.Text = Convert.ToDouble(this.TxtTotIngresos.Text).ToString("N0");
            TxtDeuTerceros.Text = Convert.ToDouble(this.TxtDeuTerceros.Text).ToString("N0");
            TxtDstoParafiscales.Text = Convert.ToDouble(this.TxtDstoParafiscales.Text).ToString("N0");
            TxtDsctos.Text = Convert.ToDouble(this.TxtDsctos.Text).ToString("N0");
            TxtDstoPension.Text = Convert.ToDouble(this.TxtDstoPension.Text).ToString("N0");
            TxtDeuCoopNomi.Text = Convert.ToDouble(this.TxtDeuCoopNomi.Text).ToString("N0");
            TxtDeuCoopCaja.Text = Convert.ToDouble(this.TxtDeuCoopCaja.Text).ToString("N0");
            TxtGastosPnales.Text = Convert.ToDouble(this.TxtGastosPnales.Text).ToString("N0");
            lblCuotaRecogida.Text = Convert.ToDouble(this.lblCuotaRecogida.Text).ToString("N0");
            TxtTotEgresos.Text = Convert.ToDouble(this.TxtTotEgresos.Text).ToString("N0");
        }

        void validarEgresos()
        {
            if (this.TxtDeuTerceros.Text.Length == 0 || this.TxtDeuTerceros.Text == "")
            {
                this.TxtDeuTerceros.Text = "0";
            }
            if (this.TxtDstoParafiscales.Text.Length == 0 || this.TxtDstoParafiscales.Text == "")
            {
                this.TxtDstoParafiscales.Text = "0";
            }
            if (this.TxtDsctos.Text.Length == 0 || this.TxtDsctos.Text == "")
            {
                this.TxtDsctos.Text = "0";
            }


            if (this.TxtDstoPension.Text.Length == 0 || this.TxtDstoPension.Text == "")
            {
                this.TxtDstoPension.Text = "0";
            }
            if (this.TxtDeuCoopNomi.Text.Length == 0 || this.TxtDeuCoopNomi.Text == "")
            {
                TxtDeuCoopNomi.Text = "0";
            }
            if (this.TxtDeuCoopCaja.Text.Length == 0 || this.TxtDeuCoopCaja.Text == "")
            {
                TxtDeuCoopCaja.Text = "0";
            }
            if (this.TxtGastosPnales.Text.Length == 0 || this.TxtGastosPnales.Text == "")
            {
                TxtGastosPnales.Text = "0";
            }
            if (TxtTotEgresos.Text.Length == 0 || this.TxtGastosPnales.Text == "")
            {
                TxtTotEgresos.Text = "0";
            }
            if (this.lblCuotaRecogida.Text.Length == 0 || this.lblCuotaRecogida.Text == "")
            {
                lblCuotaRecogida.Text = "0";
            }
        }

        private void TxtIngBasico_LostFocus(object sender, System.EventArgs e)
        {
            calcularSeguridadSocial();
            calcularIngresosEgresos();
        }

        private void TxtIngConyugue_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtIngVariables_LostFocus(object sender, System.EventArgs e)
        {
            calcularSeguridadSocial();
            calcularIngresosEgresos();
        }

        private void TxtIngArriendos_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtIngPensiones_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtOtrosIng_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtTotIngresos_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtDeuTerceros_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtDstoParafiscales_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtDsctos_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtDstoPension_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        // VB: Handles TxtDeuCoopCaja.LostFocus (method named TxtDeuCoopNomi_LostFocus)
        private void TxtDeuCoopNomi_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        // VB: Handles TxtDeuCoopNomi.LostFocus (method named TxtDeuCoopCaja_LostFocus)
        private void TxtDeuCoopCaja_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void TxtGastosPnales_LostFocus(object sender, System.EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.TxtGastosPnales.Text))
                    {
                        if (Convert.ToDouble(this.TxtGastosPnales.Text) >= 0 && Convert.ToDouble(this.TxtGastosPnales.Text) <= 100)
                        {
                            gastospersonales = Convert.ToDouble(this.TxtIngBasico.Text) * (Convert.ToDouble(this.TxtGastosPnales.Text) / 100);
                        }
                        else
                        {
                            MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "Informacion", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            gastospersonales = 0;
                            this.TxtGastosPnales.Text = "0";
                            this.TxtGastosPnales.Focus();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Los gastos personales deben ser un porcentaje.", "Informacion", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        gastospersonales = 0;
                        this.TxtGastosPnales.Text = "0";
                        this.TxtGastosPnales.Focus();
                    }
                    this.TxtTotEgresos.Text = (Convert.ToDouble(this.TxtDeuTerceros.Text) + Convert.ToDouble(this.TxtDeuCoopCaja.Text) + Convert.ToDouble(this.TxtDeuCoopNomi.Text) + Convert.ToDouble(this.TxtDsctos.Text) + gastospersonales + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text)).ToString("N0");
                    this.TxtGastosPnales.Text = Convert.ToDouble(this.TxtGastosPnales.Text).ToString("N0");
                    break;
                case false:
                    this.TxtTotEgresos.Text = (Convert.ToDouble(this.TxtDeuTerceros.Text) + Convert.ToDouble(this.TxtDeuCoopCaja.Text) + Convert.ToDouble(this.TxtDeuCoopNomi.Text) + Convert.ToDouble(this.TxtDsctos.Text) + (Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : Convert.ToDouble(this.TxtGastosPnales.Text)) + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text)).ToString("N0");
                    this.TxtGastosPnales.Text = Convert.ToDouble(this.TxtGastosPnales.Text).ToString("N0");
                    break;
            }

            calcularIngresosEgresos();
        }

        private void TxtTotEgresos_LostFocus(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        void calcularSeguridadSocial()
        {
            if (salario_compania > Convert.ToDouble(this.TxtIngBasico.Text))
            {
                this.TxtDstoParafiscales.Text = ((salario_compania + Convert.ToDouble(this.TxtIngVariables.Text)) * 0.08).ToString();
            }
            else
            {
                this.TxtDstoParafiscales.Text = ((Convert.ToDouble(this.TxtIngBasico.Text) + Convert.ToDouble(this.TxtIngVariables.Text)) * 0.08).ToString();
            }
        }

        void calcularIngresosEgresos()
        {
            validarEgresos();
            validaringresos();
            formatearTexbos();
            double gastospersonal = 0, deudas;
            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    gastospersonal = Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : (Convert.ToDouble(this.TxtIngBasico.Text) * (Convert.ToDouble(this.TxtGastosPnales.Text) / 100));
                    break;
                case false:
                    gastospersonal = Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : Convert.ToDouble(this.TxtGastosPnales.Text);
                    break;
            }
            deudas = this.CbxRecDeudas.Text == "No" ? 0 : (this.CbxRecDeudas.Text == "Si" ? Convert.ToDouble(lblCuotaRecogida.Text) : 0);

            this.TxtTotIngresos.Text = (Convert.ToDouble(this.TxtIngBasico.Text) + Convert.ToDouble(this.TxtIngArriendos.Text) + Convert.ToDouble(this.TxtIngConyugue.Text) + Convert.ToDouble(this.TxtIngVariables.Text) + Convert.ToDouble(this.TxtOtrosIng.Text) + Convert.ToDouble(this.TxtIngPensiones.Text)).ToString("N0");
            this.TxtTotEgresos.Text = (Convert.ToDouble(this.TxtDeuCoopCaja.Text) + Convert.ToDouble(this.TxtDeuCoopNomi.Text) + Convert.ToDouble(this.TxtDeuTerceros.Text) + Convert.ToDouble(this.TxtDsctos.Text) + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) + gastospersonal - deudas).ToString("N0");


            switch ((int)(Convert.ToDouble(this.TxtIngBasico.Text) > 0 ? 1 : 0))
            {
                case 1:
                    TxtSolActAportes = Convert.ToDouble(ClsCarteraLiq.CalculaSaldoAportes(codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect));
                    TxtSolPasDeudas = Convert.ToDouble(ClsCarteraLiq.CalculaTotalDeuda(codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect, "N"));
                    SaldoAohorroSuper = Convert.ToDouble(ClsCarteraLiq.CalculaSaldoAhorrosEquisuper(codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect));
                    ValorDeudasRecogida = saldoDeudaRecogida;
                    this.LblDescubierto.Text = (((TxtSolPasDeudas + (Convert.ToDouble(this.valorSolicitado) - ValorDeudasRecogida)) - (TxtSolActAportes + SaldoAohorroSuper)) / Convert.ToDouble(this.TxtIngBasico.Text)).ToString("N3");
                    break;
                default:
                    this.LblDescubierto.Text = "0";
                    break;
            }
            CalcularCapacidadNomina();
        }

        void CalcularCapacidadNomina()
        {
            double MedioSal = 0, DeducionesNomina = 0, IngresosNomina = 0;

            double deudas = 0;
            //cuotaRecogida son las deduccion por  nomina
            deudas = this.CbxRecDeudas.Text == "No" ? 0 : (this.CbxRecDeudas.Text == "Si" ? this.CuotaRecogida : 0);

            DeducionesNomina = Convert.ToDouble(this.TxtDeuCoopNomi.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDsctos.Text) - deudas;
            IngresosNomina = Convert.ToDouble(this.TxtIngBasico.Text) + Convert.ToDouble(this.TxtIngVariables.Text) + Convert.ToDouble(this.TxtIngPensiones.Text);


            this.LblDispNomina.Text = (IngresosNomina - DeducionesNomina).ToString("N0");

            this.LblCapPago.Text = (Convert.ToDouble(this.TxtTotIngresos.Text) - Convert.ToDouble(this.TxtTotEgresos.Text)).ToString("N0");


            this.lblCapacDescuento.Text = ((Convert.ToDouble(this.TxtTotIngresos.Text) * (PorDscto / 100)) - (DeducionesNomina + this.cuotasNuevoCredito)).ToString("N0");

            switch (TipoDscto)
            {
                case "0":
                    MedioSal = (Convert.ToDouble(this.TxtIngBasico.Text) * (PorDscto / 100));
                    break;
                case "1":
                    MedioSal = (Convert.ToDouble(this.TxtIngBasico.Text) - PorDscto);
                    break;
            }

            if (TipoAsociado == "1")
            {
                this.LblCaja.Text = this.LblCapPago.Text;
                this.LblNomina.Text = "0";
            }
            else
            {
                if (DeducionesNomina >= MedioSal)
                {
                    this.LblCaja.Text = this.LblCapPago.Text;
                    this.LblNomina.Text = "0";
                }
                else
                {
                    this.LblNomina.Text = (Convert.ToDouble(MedioSal) - Convert.ToDouble(DeducionesNomina)).ToString("N0");
                    this.LblCaja.Text = (Convert.ToDouble(this.LblCapPago.Text) - Convert.ToDouble(this.LblNomina.Text)).ToString("N0");
                }
            }

            this.lblCuotaNuevoCredito.Text = this.cuotasNuevoCredito.ToString("N0");
            this.lblCapacDescuento.Text = (MedioSal - (DeducionesNomina + this.cuotasNuevoCredito)).ToString("N0");



            if (Convert.ToDouble(this.TxtTotIngresos.Text) > 0)
            {
                this.LblNivelContingencia.Text = (((Convert.ToDouble(this.TxtTotIngresos.Text) - Convert.ToDouble(this.TxtTotEgresos.Text)) / Convert.ToDouble(this.TxtTotIngresos.Text)) * 100).ToString();
            }

        }

        private void CbxRecDeudas_Validated(object sender, System.EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.TxtGastosPnales.Text))
                    {
                        gastospersonales = Convert.ToDouble(this.TxtIngBasico.Text) * (Convert.ToDouble(this.TxtGastosPnales.Text) / 100);
                    }
                    else
                    {
                        gastospersonales = 0;
                    }
                    break;
                case false:
                    gastospersonales = Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : Convert.ToDouble(this.TxtGastosPnales.Text);
                    break;
            }
            if (this.CbxRecDeudas.Text.Trim() != "")
            {
                if (this.CbxRecDeudas.Text == "No")
                {
                    this.TxtTotEgresos.Text = (Convert.ToDouble(this.TxtDeuTerceros.Text) + Convert.ToDouble(this.TxtDeuCoopCaja.Text) + Convert.ToDouble(this.TxtDeuCoopNomi.Text) + Convert.ToDouble(this.TxtDsctos.Text) + gastospersonales + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text)).ToString("N0");
                }
                else if (this.CbxRecDeudas.Text == "Si")
                {
                    // Me.lblCuotaRecogida.Text = CuotaRecogida
                    // Me.TotalCuotaRecogida  = CuotaRecogida +  Me.CuotaRecogidaCaja
                    this.lblCuotaRecogida.Text = this.TotalCuotaRecogida.ToString();
                    this.TxtTotEgresos.Text = (Convert.ToDouble(this.TxtDeuTerceros.Text) + Convert.ToDouble(this.TxtDeuCoopCaja.Text) + Convert.ToDouble(this.TxtDeuCoopNomi.Text) + Convert.ToDouble(this.TxtDsctos.Text) + gastospersonales + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) - Convert.ToDouble(this.lblCuotaRecogida.Text)).ToString("N0");
                }
                CalcularCapacidadNomina();
            }
        }

        void CargarDatosClasificacion()
        {
            DataSet DtDatos = new DataSet();
            string PeriodoIni = "0";
            int i = 0, NumAnio = 0;
            string mes = "0", catego = "";
            PeriodoIni = DateTime.Now.ToString("yyyy");
            PeriodoIni = (Convert.ToInt32(PeriodoIni) - 4).ToString();
           // DtDatos = this.clscartera.CargarClasifCart(this.codigoAsociado, PeriodoIni, DateTime.Now.ToString("yyyy"), myconnect);
            this.TbcClasCart.TabPages[0].Text = PeriodoIni;
            this.TbcClasCart.TabPages[1].Text = (Convert.ToInt32(PeriodoIni) + 1).ToString();
            this.TbcClasCart.TabPages[2].Text = (Convert.ToInt32(PeriodoIni) + 2).ToString();
            this.TbcClasCart.TabPages[3].Text = (Convert.ToInt32(PeriodoIni) + 3).ToString();
            this.TbcClasCart.TabPages[4].Text = (Convert.ToInt32(PeriodoIni) + 4).ToString();
            this.TbcClasCart.SelectedTab = this.TbcClasCart.TabPages[4];
            DataRowCollection rows = DtDatos.Tables[0].Rows;
            while (i < rows.Count)
            {
                string periodo = rows[i]["periodo_contable"].ToString();
                string anioStr = periodo.Substring(0, 4);
                if (anioStr == PeriodoIni)
                    NumAnio = 1;
                else if (anioStr == (Convert.ToInt32(PeriodoIni) + 1).ToString())
                    NumAnio = 2;
                else if (anioStr == (Convert.ToInt32(PeriodoIni) + 2).ToString())
                    NumAnio = 3;
                else if (anioStr == (Convert.ToInt32(PeriodoIni) + 3).ToString())
                    NumAnio = 4;
                else if (anioStr == (Convert.ToInt32(PeriodoIni) + 4).ToString())
                    NumAnio = 5;
                mes = periodo.Substring(4);
                catego = rows[i]["Categoria"].ToString();
                CategoriaAnioMes(NumAnio, mes, catego);
                i = i + 1;
            }
        }

        void CategoriaAnioMes(int NumAnio, string mes, string clasificacion)
        {
            switch (mes)
            {
                case "01":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaEne1.Text = clasificacion; break;
                        case 2: this.LblClaEne2.Text = clasificacion; break;
                        case 3: this.LblClaEne3.Text = clasificacion; break;
                        case 4: this.LblClaEne4.Text = clasificacion; break;
                        case 5: this.LblClaEne5.Text = clasificacion; break;
                    }
                    break;
                case "02":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaFeb1.Text = clasificacion; break;
                        case 2: this.LblClaFeb2.Text = clasificacion; break;
                        case 3: this.LblClaFeb3.Text = clasificacion; break;
                        case 4: this.LblClaFeb4.Text = clasificacion; break;
                        case 5: this.LblClaFeb5.Text = clasificacion; break;
                    }
                    break;
                case "03":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaMar1.Text = clasificacion; break;
                        case 2: this.LblClaMar2.Text = clasificacion; break;
                        case 3: this.LblClaMar3.Text = clasificacion; break;
                        case 4: this.LblClaMar4.Text = clasificacion; break;
                        case 5: this.LblClaMar5.Text = clasificacion; break;
                    }
                    break;
                case "04":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaAbr1.Text = clasificacion; break;
                        case 2: this.LblClaAbr2.Text = clasificacion; break;
                        case 3: this.LblClaAbr3.Text = clasificacion; break;
                        case 4: this.LblClaAbr4.Text = clasificacion; break;
                        case 5: this.LblClaAbr5.Text = clasificacion; break;
                    }
                    break;
                case "05":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaMay1.Text = clasificacion; break;
                        case 2: this.LblClaMay2.Text = clasificacion; break;
                        case 3: this.LblClaMay3.Text = clasificacion; break;
                        case 4: this.LblClaMay4.Text = clasificacion; break;
                        case 5: this.LblClaMay5.Text = clasificacion; break;
                    }
                    break;
                case "06":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaJun1.Text = clasificacion; break;
                        case 2: this.LblClaJun2.Text = clasificacion; break;
                        case 3: this.LblClaJun3.Text = clasificacion; break;
                        case 4: this.LblClaJun4.Text = clasificacion; break;
                        case 5: this.LblClaJun5.Text = clasificacion; break;
                    }
                    break;
                case "07":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaJul1.Text = clasificacion; break;
                        case 2: this.LblClaJul2.Text = clasificacion; break;
                        case 3: this.LblClaJul3.Text = clasificacion; break;
                        case 4: this.LblClaJul4.Text = clasificacion; break;
                        case 5: this.LblClaJul5.Text = clasificacion; break;
                    }
                    break;
                case "08":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaAgo1.Text = clasificacion; break;
                        case 2: this.LblClaAgo2.Text = clasificacion; break;
                        case 3: this.LblClaAgo3.Text = clasificacion; break;
                        case 4: this.LblClaAgo4.Text = clasificacion; break;
                        case 5: this.LblClaAgo5.Text = clasificacion; break;
                    }
                    break;
                case "09":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaSep1.Text = clasificacion; break;
                        case 2: this.LblClaSep2.Text = clasificacion; break;
                        case 3: this.LblClaSep3.Text = clasificacion; break;
                        case 4: this.LblClaSep4.Text = clasificacion; break;
                        case 5: this.LblClaSep5.Text = clasificacion; break;
                    }
                    break;
                case "10":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaOct1.Text = clasificacion; break;
                        case 2: this.LblClaOct2.Text = clasificacion; break;
                        case 3: this.LblClaOct3.Text = clasificacion; break;
                        case 4: this.LblClaOct4.Text = clasificacion; break;
                        case 5: this.LblClaOct5.Text = clasificacion; break;
                    }
                    break;
                case "11":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaNov1.Text = clasificacion; break;
                        case 2: this.LblClaNov2.Text = clasificacion; break;
                        case 3: this.LblClaNov3.Text = clasificacion; break;
                        case 4: this.LblClaNov4.Text = clasificacion; break;
                        case 5: this.LblClaNov5.Text = clasificacion; break;
                    }
                    break;
                case "12":
                    switch (NumAnio)
                    {
                        case 1: this.LblClaDic1.Text = clasificacion; break;
                        case 2: this.LblClaDic2.Text = clasificacion; break;
                        case 3: this.LblClaDic3.Text = clasificacion; break;
                        case 4: this.LblClaDic4.Text = clasificacion; break;
                        case 5: this.LblClaDic5.Text = clasificacion; break;
                    }
                    break;
            }
        }

        private void CmbImprime_Click(object sender, System.EventArgs e)
        {
            DataSet dsdataCapaPagoImp = new DataSet();
            string apellido = "", nombre = "", telefono = "", direccion = "", cedula = "";
            calcularIngresosEgresos();
            dsdataCapaPagoImp.Tables.Add("DsdataCapa");
            DataColumnCollection cols2 = dsdataCapaPagoImp.Tables["DsdataCapa"].Columns;
            cols2.Add("Salario", StDouble.GetType());
            cols2.Add("otro_ingreso", StDouble.GetType());
            cols2.Add("CONYSALAR", StDouble.GetType());
            cols2.Add("IngVariables", StDouble.GetType());
            cols2.Add("IngArriendos", StDouble.GetType());
            cols2.Add("IngPension", StDouble.GetType());
            cols2.Add("DeudasTerceros", StDouble.GetType());
            cols2.Add("DstoParafiscales", StDouble.GetType());
            cols2.Add("DstoPension", StDouble.GetType());
            cols2.Add("Dsctos", StDouble.GetType());
            cols2.Add("dsGastoper", StDouble.GetType());
            cols2.Add("DeuCoopNomi", StDouble.GetType());
            cols2.Add("DeuCoopCaja", StDouble.GetType());
            cols2.Add("Porcn", sbolean.GetType());
            cols2.Add("LblDispNomina", StDouble.GetType());
            cols2.Add("LblPorcentajePagaduria", StDouble.GetType());
            cols2.Add("LblNomina", StDouble.GetType());
            cols2.Add("LblCaja", StDouble.GetType());
            cols2.Add("LblCapPago", StDouble.GetType());
            cols2.Add("LblDescubierto", StDouble.GetType());
            cols2.Add("CbxRecDeudas", strstring.GetType());
            cols2.Add("lblCuotaRecogida", StDouble.GetType());
            cols2.Add("TxtTotIngresos", StDouble.GetType());
            cols2.Add("TxtTotEgresos", StDouble.GetType());
            cols2.Add("TabPage4", strstring.GetType());
            cols2.Add("LblClaEne4", strstring.GetType());
            cols2.Add("LblClaFeb4", strstring.GetType());
            cols2.Add("LblClaMar4", strstring.GetType());
            cols2.Add("LblClaAbr4", strstring.GetType());
            cols2.Add("LblClaMay4", strstring.GetType());
            cols2.Add("LblClaJun4", strstring.GetType());
            cols2.Add("LblClaJul4", strstring.GetType());
            cols2.Add("LblClaAgo4", strstring.GetType());
            cols2.Add("LblClaSep4", strstring.GetType());
            cols2.Add("LblClaOct4", strstring.GetType());
            cols2.Add("LblClaNov4", strstring.GetType());
            cols2.Add("LblClaDic4", strstring.GetType());
            cols2.Add("TabPage5", strstring.GetType());
            cols2.Add("LblClaEne5", strstring.GetType());
            cols2.Add("LblClaFeb5", strstring.GetType());
            cols2.Add("LblClaMar5", strstring.GetType());
            cols2.Add("LblClaAbr5", strstring.GetType());
            cols2.Add("LblClaMay5", strstring.GetType());
            cols2.Add("LblClaJun5", strstring.GetType());
            cols2.Add("LblClaJul5", strstring.GetType());
            cols2.Add("LblClaAgo5", strstring.GetType());
            cols2.Add("LblClaSep5", strstring.GetType());
            cols2.Add("LblClaOct5", strstring.GetType());
            cols2.Add("LblClaNov5", strstring.GetType());
            cols2.Add("LblClaDic5", strstring.GetType());
            cols2.Add("NombreAsociado", strstring.GetType());
            cols2.Add("CedulaAsociado", strstring.GetType());
            cols2.Add("TelefonoAsociado", strstring.GetType());
            cols2.Add("DireccionAsociado", strstring.GetType());
            cols2.Add("PorNomina", StDouble.GetType());
            cols2.Add("PorCaja", StDouble.GetType());

            cols2.Add("CuotaRecogida", StDouble.GetType()); //cuota Recogida  de nomina
            cols2.Add("CuotaRecogidaCaja", StDouble.GetType()); //cuota Recogida  por caja

            cols2.Add("TxtSolActVivienda", StDouble.GetType());
            cols2.Add("TxtSolActVehiculo", StDouble.GetType());
            cols2.Add("TxtSolActOtros", StDouble.GetType());
            cols2.Add("TxtSolActAporte", StDouble.GetType());
            cols2.Add("TxtActCtaBanco", StDouble.GetType());
            cols2.Add("TxtActCxC", StDouble.GetType());
            cols2.Add("TxtSolActAhorros", StDouble.GetType());
            cols2.Add("TxtSolActTotal", StDouble.GetType());

            cols2.Add("TxtSolPasDeuda", StDouble.GetType());
            cols2.Add("TxtSolPasOtros", StDouble.GetType());
            cols2.Add("TxtPasObliBanca", StDouble.GetType());
            cols2.Add("TxtPasObliHipot", StDouble.GetType());
            cols2.Add("TxtSolPasTotal", StDouble.GetType());
            cols2.Add("TxtSolPatrimonio", StDouble.GetType());
            cols2.Add("TxtSolPasTotPyP", StDouble.GetType());
            cols2.Add("LblNivelEndeudamiento", StDouble.GetType());
            cols2.Add("LblNivelContingencia", StDouble.GetType());
            cols2.Add("lblCapacidadRiesgovlr", StDouble.GetType());
            cols2.Add("lblCapacDescuento", StDouble.GetType());
            cols2.Add("lblPorcnDescuento", StDouble.GetType());
            cols2.Add("CuotaNuevocredito", StDouble.GetType());

            //ParamCop.BuscaAsociado(this.codigoAsociado, myconnect, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref nombre, ref apellido, ref cedula, "", "", "", ref direccion, "", "", ref telefono);
            //dsdataCapaPagoImp.Tables["DsdataCapa"].Rows.Add(this.TxtIngBasico.Text, this.TxtOtrosIng.Text, this.TxtIngConyugue.Text, this.TxtIngVariables.Text, this.TxtIngArriendos.Text, this.TxtIngPensiones.Text
            // , this.TxtDeuTerceros.Text, this.TxtDstoParafiscales.Text, this.TxtDstoPension.Text, this.TxtDsctos.Text, this.TxtGastosPnales.Text, TxtDeuCoopNomi.Text
            // , TxtDeuCoopCaja.Text, this.ChkPorcentaje.Checked, LblDispNomina.Text, LblPorcentajePagaduria.Text, LblNomina.Text, LblCaja.Text, LblCapPago.Text
            // , LblDescubierto.Text, this.CbxRecDeudas.Text, lblCuotaRecogida.Text, TxtTotIngresos.Text, TxtTotEgresos.Text
            // , this.TbcClasCart.TabPages[3].Text, LblClaEne4.Text, LblClaFeb4.Text, LblClaMar4.Text, LblClaAbr4.Text, LblClaMay4.Text
            // , LblClaJun4.Text, LblClaJul4.Text, LblClaAgo4.Text, LblClaSep4.Text, LblClaOct4.Text, LblClaNov4.Text, LblClaDic4.Text
            // , this.TbcClasCart.TabPages[4].Text, LblClaEne5.Text, LblClaFeb5.Text, LblClaMar5.Text, LblClaAbr5.Text, LblClaMay5.Text
            // , LblClaJun5.Text, LblClaJul5.Text, LblClaAgo5.Text, LblClaSep5.Text, LblClaOct5.Text, LblClaNov5.Text, LblClaDic5.Text
            // , apellido + " " + nombre, cedula, telefono, direccion, this.LblPorcNomina.Text, this.LblPorcCaja.Text, CuotaRecogida, CuotaRecogidaCaja
            // , TxtSolActVivienda.Text, TxtSolActVehiculo.Text, TxtSolActOtros.Text, TxtSolActAporte.Text, TxtActCtaBanco.Text, TxtActCxC.Text, TxtSolActAhorros.Text
            // , TxtSolActTotal.Text, TxtSolPasDeuda.Text, TxtSolPasOtros.Text, TxtPasObliBanca.Text, TxtPasObliHipot.Text, TxtSolPasTotal.Text
            // , TxtSolPatrimonio.Text, TxtSolPasTotPyP.Text, LblNivelEndeudamiento.Text, LblNivelContingencia.Text, lblCapacidadRiesgovlr.Text, lblCapacDescuento.Text, lblPorcnDescuento.Text, this.cuotasNuevoCredito);

            //dsdataCapaPagoImp.WriteXmlSchema("C:\esquemacapacidad.xml")
            ERP.Core.Compartido.Reportes.reporte Informe = new ERP.Core.Compartido.Reportes.reporte("cop_rcapicidadPago");
            ERP.Core.Compartido.Reportes.config_report configreport = new ERP.Core.Compartido.Reportes.config_report();
            string nitcomp = " ", diremp = " ", nomemp = " ", telemp = " ";

           // Foco.BuscarCompania(Foco.varini.sptCodEmpr, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", ref nitcomp, ref diremp, "", "", "", "", "", "", "", "", "", ref nomemp, ref telemp);
            Informe.SetDataSource(dsdataCapaPagoImp);
            Informe.SetParameterValue("empresa", nomemp);
            Informe.SetParameterValue("nit", nitcomp);
            Informe.SetParameterValue("direccion", diremp);
            Informe.SetParameterValue("telefono", telemp);
            configreport.confi_reportes(this, Informe);
        }

        void CargarDatosSolvenciaActivosPasivos()
        {
            //this.TxtSolActAporte.Text = ClsCarteraLiq.CalculaSaldoAportes(codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect);
            //this.TxtSolPasDeuda.Text = ClsCarteraLiq.CalculaTotalDeuda(codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect, "N");
            //this.TxtSolActAhorros.Text = ClsCarteraLiq.CalculaSaldoAhorros(codigoAsociado, DateTime.Now.ToString("yyyyMM"), myconnect);

            if (Information.IsNumeric(this.TxtSolActAporte.Text) == true)
            {
                CalcularSolvenciaActivos();
            }
            if (Information.IsNumeric(this.TxtSolActAhorros.Text) == true)
            {
                CalcularSolvenciaActivos();
            }
            if (Information.IsNumeric(this.TxtSolPasDeuda.Text) == true)
            {
                CalcularSolvenciaPasivos();
            }
        }

        private void LblDispNomina_Click(object sender, System.EventArgs e)
        {
            if (LblDispNomina.Text.Trim() != "" && TxtIngBasico.Text.Trim() != "" && TxtIngVariables.Text.Trim() != "" && TxtIngPensiones.Text.Trim() != "" && LblDispNomina.Text.Trim() != "0")
            {
                if ((Convert.ToDouble(TxtIngBasico.Text.Trim()) + Convert.ToDouble(TxtIngVariables.Text.Trim()) + Convert.ToDouble(TxtIngPensiones.Text.Trim())) != 0)
                {
                    LblPorcNomina.Text = ((Convert.ToDouble(LblDispNomina.Text.Trim()) / (Convert.ToDouble(TxtIngBasico.Text.Trim()) + Convert.ToDouble(TxtIngVariables.Text.Trim()) + Convert.ToDouble(TxtIngPensiones.Text.Trim()))) * 100).ToString("N2");
                }
                else
                {
                    LblPorcNomina.Text = "0";
                }
            }
        }

        private void LblCapPago_Click(object sender, System.EventArgs e)
        {
            if (LblCapPago.Text.Trim() != "" && TxtTotIngresos.Text.Trim() != "" && LblCapPago.Text.Trim() != "0")
            {
                if (Convert.ToDouble(TxtTotIngresos.Text.Trim()) != 0)
                {
                    LblPorcCaja.Text = ((Convert.ToDouble(LblCapPago.Text.Trim()) / Convert.ToDouble(TxtTotIngresos.Text.Trim())) * 100).ToString("N2");
                }
                else
                {
                    LblPorcCaja.Text = "0";
                }
            }
        }


        private void BtnBienes_Click(object sender, System.EventArgs e)
        {
            CargarVentanaBienes();
        }

        void CargarVentanaBienes()
        {
            DataSet dsraices = new DataSet(), dsvehiculos = new DataSet();
            double Raices = 0, vehiculo = 0;
            int i = 0;
            if (dsbienesCapacidaPago.Tables.Count == 0)
            {
                dsraices.Tables.Add("tblnada");
                dsvehiculos.Tables.Add("tblnada");
            }
            else
            {
                dsraices.Tables.Add(dsbienesCapacidaPago.Tables["tblBienesRaices"].Copy());
                dsvehiculos.Tables.Add(dsbienesCapacidaPago.Tables["tblVehiculo"].Copy());
            }

            dsbienesCapacidaPago = ClsCarteraLiq.CargaBienes(this.LblNomEmpresa.Text, dsraices, dsvehiculos, this, this.myconnect, codigoAsociado);


            if (dsbienesCapacidaPago.Tables.Contains("tblBienesRaices") == true)
            {
                if (dsbienesCapacidaPago.Tables["tblBienesRaices"].Rows.Count > 0)
                {
                    while (i < dsbienesCapacidaPago.Tables["tblBienesRaices"].Rows.Count)
                    {
                        Raices += Convert.ToDouble(dsbienesCapacidaPago.Tables["tblBienesRaices"].Rows[i]["VALOR"]);
                        i += 1;
                    }
                }
            }
            i = 0;
            if (dsbienesCapacidaPago.Tables.Contains("tblVehiculo") == true)
            {
                if (dsbienesCapacidaPago.Tables["tblVehiculo"].Rows.Count > 0)
                {
                    while (i < dsbienesCapacidaPago.Tables["tblVehiculo"].Rows.Count)
                    {
                        vehiculo += Convert.ToDouble(dsbienesCapacidaPago.Tables["tblVehiculo"].Rows[i]["VALOR"]);
                        i += 1;
                    }
                }
            }
            if (Raices > 0)
            {
                this.TxtSolActVivienda.Text = Raices.ToString();
            }
            if (vehiculo > 0)
            {
                this.TxtSolActVehiculo.Text = vehiculo.ToString();
            }

            this.TxtSolActVivienda.Text = Convert.ToDouble(this.TxtSolActVivienda.Text).ToString("N0");
            this.TxtSolActVehiculo.Text = Convert.ToDouble(this.TxtSolActVehiculo.Text).ToString("N0");
            TxtSolActVivienda_LostFocus(null, null);
            TxtSolActVehiculo_LostFocus(null, null);
        }

        private void TxtSolActVivienda_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActVivienda.Text) == true && Convert.ToDouble(this.TxtSolActVivienda.Text) >= 0)
            {
                this.TxtSolActVivienda.Text = Convert.ToDouble(this.TxtSolActVivienda.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActVivienda.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolActVehiculo_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActVehiculo.Text) == true && Convert.ToDouble(this.TxtSolActVehiculo.Text) >= 0)
            {
                this.TxtSolActVehiculo.Text = Convert.ToDouble(this.TxtSolActVehiculo.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActVehiculo.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolActOtros_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActOtros.Text) == true && Convert.ToDouble(this.TxtSolActOtros.Text) >= 0)
            {
                this.TxtSolActOtros.Text = Convert.ToDouble(this.TxtSolActOtros.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActOtros.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolActAhorros_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActAhorros.Text) == true && Convert.ToDouble(this.TxtSolActAhorros.Text) >= 0)
            {
                this.TxtSolActAhorros.Text = Convert.ToDouble(this.TxtSolActAhorros.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActAhorros.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtActCtaBanco_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtActCtaBanco.Text) == true && Convert.ToDouble(this.TxtActCtaBanco.Text) >= 0)
            {
                this.TxtActCtaBanco.Text = Convert.ToDouble(this.TxtActCtaBanco.Text).ToString("N0");
            }
            else
            {
                this.TxtActCtaBanco.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtActCxC_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtActCxC.Text) == true && Convert.ToDouble(this.TxtActCxC.Text) >= 0)
            {
                this.TxtActCxC.Text = Convert.ToDouble(this.TxtActCxC.Text).ToString("N0");
            }
            else
            {
                this.TxtActCxC.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolPasOtros_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolPasOtros.Text) == true && Convert.ToDouble(this.TxtSolPasOtros.Text) >= 0)
            {
                this.TxtSolPasOtros.Text = Convert.ToDouble(this.TxtSolPasOtros.Text).ToString("N0");
            }
            else
            {
                this.TxtSolPasOtros.Text = "0";
            }
            this.CalcularSolvenciaPasivos();
        }

        private void TxtPasObliBanca_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPasObliBanca.Text) == true && Convert.ToDouble(this.TxtPasObliBanca.Text) >= 0)
            {
                this.TxtPasObliBanca.Text = Convert.ToDouble(this.TxtPasObliBanca.Text).ToString("N0");
            }
            else
            {
                this.TxtPasObliBanca.Text = "0";
            }
            this.CalcularSolvenciaPasivos();
        }

        private void TxtPasObliHipot_LostFocus(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPasObliHipot.Text) == true && Convert.ToDouble(this.TxtPasObliHipot.Text) >= 0)
            {
                this.TxtPasObliHipot.Text = Convert.ToDouble(this.TxtPasObliHipot.Text).ToString("N0");
            }
            else
            {
                this.TxtPasObliHipot.Text = "0";
            }
            this.CalcularSolvenciaPasivos();
        }

        void CalcularSolvenciaActivos()
        {
            this.TxtSolActTotal.Text = (Convert.ToDouble(this.TxtSolActAporte.Text) + Convert.ToDouble(this.TxtSolActOtros.Text) + Convert.ToDouble(this.TxtSolActVehiculo.Text) + Convert.ToDouble(this.TxtSolActVivienda.Text) + Convert.ToDouble(this.TxtActCtaBanco.Text) + Convert.ToDouble(this.TxtActCxC.Text) + Convert.ToDouble(this.TxtSolActAhorros.Text)).ToString();
            this.TxtSolActTotal.Text = Convert.ToDouble(this.TxtSolActTotal.Text).ToString("N0");

            this.TxtSolPatrimonio.Text = (Convert.ToDouble(this.TxtSolActTotal.Text) - Convert.ToDouble(this.TxtSolPasTotal.Text)).ToString();
            this.TxtSolPatrimonio.Text = Convert.ToDouble(this.TxtSolPatrimonio.Text).ToString("N0");

            if (Convert.ToInt32(Convert.ToDouble(this.TxtSolActTotal.Text)) == 0)
            {
                this.LblNivelEndeudamiento.Text = "0";
            }
            else
            {
                this.LblNivelEndeudamiento.Text = ((Convert.ToDouble(this.TxtSolPasTotal.Text) / Convert.ToDouble(this.TxtSolActTotal.Text)) * 100).ToString();
            }
            this.TxtSolPasTotPyP.Text = (Convert.ToDouble(this.TxtSolPasTotal.Text) + Convert.ToDouble(this.TxtSolPatrimonio.Text)).ToString("N0");
            CalcularSolvenciaPasivos();
        }

        void CalcularSolvenciaPasivos()
        {
            this.TxtSolPasTotal.Text = (Convert.ToDouble(this.TxtSolPasDeuda.Text) + Convert.ToDouble(this.TxtSolPasOtros.Text) + Convert.ToDouble(this.TxtPasObliBanca.Text) + Convert.ToDouble(this.TxtPasObliHipot.Text)).ToString();
            this.TxtSolPasTotal.Text = Convert.ToDouble(this.TxtSolPasTotal.Text).ToString("N0");

            this.TxtSolPatrimonio.Text = (Convert.ToDouble(this.TxtSolActTotal.Text) - Convert.ToDouble(this.TxtSolPasTotal.Text)).ToString();
            this.TxtSolPatrimonio.Text = Convert.ToDouble(this.TxtSolPatrimonio.Text).ToString("N0");

            this.TxtSolPasTotPyP.Text = (Convert.ToDouble(this.TxtSolPasTotal.Text) + Convert.ToDouble(this.TxtSolPatrimonio.Text)).ToString("N0");
            if (Convert.ToInt32(Convert.ToDouble(this.TxtSolActTotal.Text)) == 0)
            {
                this.LblNivelEndeudamiento.Text = "0";
            }
            else
            {
                this.LblNivelEndeudamiento.Text = ((Convert.ToDouble(this.TxtSolPasTotal.Text) / Convert.ToDouble(this.TxtSolActTotal.Text)) * 100).ToString();
            }
            formatearSolvenciaPasivos();
        }

        void formatearSolvenciaPasivos()
        {
            this.TxtSolPasDeuda.Text = Convert.ToDouble(this.TxtSolPasDeuda.Text).ToString("N0");
            this.TxtSolPasOtros.Text = Convert.ToDouble(this.TxtSolPasOtros.Text).ToString("N0");
            this.TxtPasObliBanca.Text = Convert.ToDouble(this.TxtPasObliBanca.Text).ToString("N0");
            this.TxtPasObliHipot.Text = Convert.ToDouble(this.TxtPasObliHipot.Text).ToString("N0");
            this.TxtSolActAporte.Text = Convert.ToDouble(this.TxtSolActAporte.Text).ToString("N0");
            this.TxtSolActVivienda.Text = Convert.ToDouble(this.TxtSolActVivienda.Text).ToString("N0");
            this.TxtSolActVehiculo.Text = Convert.ToDouble(this.TxtSolActVehiculo.Text).ToString("N0");
            this.TxtActCtaBanco.Text = Convert.ToDouble(this.TxtActCtaBanco.Text).ToString("N0");
            this.TxtActCxC.Text = Convert.ToDouble(this.TxtActCxC.Text).ToString("N0");
            this.TxtSolActOtros.Text = Convert.ToDouble(this.TxtSolActOtros.Text).ToString("N0");
        }

        private void ChkPorcentaje_CheckedChanged(object sender, System.EventArgs e)
        {
            calcularIngresosEgresos();
        }

        private void lblPorcnDescuento_TextChanged(object sender, System.EventArgs e)
        {
            if (Information.IsNumeric(TxtIngBasico.Text) == true && Information.IsNumeric(lblCapacDescuento.Text) == true)
            {
                lblPorcnDescuento.Text = (PorDscto - ((Convert.ToDouble(lblCapacDescuento.Text) / Convert.ToDouble(TxtIngBasico.Text)) * 100)).ToString("N2");
            }
            else
            {
                lblPorcnDescuento.Text = "0";
            }
        }
    }
}
