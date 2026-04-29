// Traduccion de: SolicitudCredito.vb (msgliqcre) -- Parte 1
using Microsoft.VisualBasic;
using System;
using System.ComponentModel;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class SolicitudCredito : Form
    {
        // --- Fields (non-Designer) ---
        private bool ok;
        public bool solant = false;
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private DataSet DsDatoSolicitud = new DataSet();
        private DataSet datasolante = new DataSet();
        public DataSet DsDatosProyeccion = new DataSet();
        private DataTable DsDataDeduciones = new DataTable();
        private DataSet DsDataCodeudores = new DataSet();
        private ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos msgliqcre = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.CDT.Services.ClsMsgCdats clsmsgcdat = new ERP.Core.CDT.Services.ClsMsgCdats();
        public bool Invocado = false;
        public string estado = "";
        private int Periodo;
        public DataSet dsbienes = new DataSet();
        public DataSet DsReferencia = new DataSet();
        public DataSet dsParViv = new DataSet();
        private DataSet dsdata = new DataSet();
        private double _salarioBasico;
        private double _IngresosVariables = 0;
        private double _IngArriendos = 0;
        private double _IngPension = 0;
        private double _DeudasTerceros = 0;
        private double _GASTO_FIJO_MES = 0;
        private double _DstoPension = 0;
        private string _cappagoPorcentaje = "L";
        private double _dstoGastosPerso = 0;
        private double _otroIngreso;
        private double _IngresoConyuge;
        private double _SaldoDuedaRecogida = 0;
        private double _CuotaDeudaRecogida = 0;
        private int controlCodedudores;
        private double _CuotaDeudaRecogidaNomina = 0;
        private double _CuotaDeudaRecogidaCaja = 0;
        private string _EstadoSolicitud = "";
        private double _Porc_Paga_Solic = 0;
        private string _tipodstoPagaduria = "";
        private double _varTxtGastosPnales = 0;
        private int _ciclo = 0;
        private int _periodicidad = 0;
        private double _salario_compania = 0;
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private double cuotasdeudas = 0;
        private double CuotaDeudasNomina = 0;
        private double CuotaDeudasCaja = 0;
        private double PorDscto = 0;
        private string TipoDscto = "0";
        private string TipoAsociado = "0";

        // --- Constructor ---
        public SolicitudCredito(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        // --- AbrirConexion ---
        public void AbrirConexion()
        {
            if (this.mycon.State != ConnectionState.Open)
            {
                this.mycon.Open();
            }
            Periodo = 999999;
            DateTime _fecIni = new DateTime(1950, 1, 1), _fecFin = new DateTime(1950, 1, 1);
            DateTime _fecValidar = new DateTime(1950, 1, 1);
            string _estado = "C";
            string _periodo = Periodo.ToString();
            msgcop.buscaPeriodo("copc", mycon, ref _fecIni, ref _fecFin, _fecValidar, ref _estado, ref _periodo, DateTime.Now.Year.ToString());
            if (int.TryParse(_periodo, out int _periodoResult)) Periodo = _periodoResult;
        }

        // --- ConfigDataCodeudores ---
        private void ConfigDataCodeudores()
        {
            double a = 0;
            string b = " ";
            this.DsDataCodeudores.Tables.Add("TblCodeudores");
            DataColumnCollection cols = this.DsDataCodeudores.Tables["TblCodeudores"].Columns;
            cols.Add("Codeudor", b.GetType());
            cols.Add("Salario", a.GetType());
            cols.Add("OtroIng", a.GetType());
            cols.Add("IngArriendo", a.GetType());
            cols.Add("IngVariable", a.GetType());
            cols.Add("DeudaEmp", a.GetType());
            cols.Add("DeudaTerc", a.GetType());
            cols.Add("OtroDsto", a.GetType());
            cols.Add("DispMes", a.GetType());
            cols.Add("IngPension", a.GetType());
            cols.Add("DstoPension", a.GetType());
            cols.Add("DstoParafiscales", a.GetType());
            cols.Add("cappagoPorcentaje", b.GetType());
            cols.Add("GastosPers", a.GetType());
            cols.Add("DeudaEmpCaja", a.GetType());
        }

        // --- SolicitudCredito_Load ---
        private void SolicitudCredito_Load(object sender, EventArgs e)
        {
            DataSet data = new DataSet();
            double DstoNomina = 0;
            double DstoCaja = 0;
            DataSet DsDatacompania = new DataSet();
            AbrirConexion();

            if (this.TxtCodigoter.Text.Trim() != "")
            {
                BuscaDatosAsociado();
                if (solant == true)
                {
                    string sqlsoliante = "select sol.* from cop_solcre sol where codigoter='" + TxtCodigoter.Text.Trim() + "' and sol.numero=" +
                                  "(select max(numero) from cop_solcre cr where codigoter='" + TxtCodigoter.Text.Trim() + "')";
                    if (this.connect.ExecuteQueryDataset(sqlsoliante, mycon, "SolicitudCredito_Load", ref datasolante, "solanterior") == true)
                    {
                        this.ChkIngreSolanterior.Enabled = true;
                    }
                }
                if (LblNumSolicitud.Text.Trim() != "00000000")
                {
                    double _vrlsaldoTotaTOTPAR = 0, _saldoTotal = 0;
                    msgcop.SaldoDeudasRecogidas(Convert.ToInt32(LblNumSolicitud.Text), this.mycon, ref cuotasdeudas, ref CuotaDeudasNomina, ref CuotaDeudasCaja, ref _vrlsaldoTotaTOTPAR, ref _saldoTotal);

                    this.ValorCuotaRecogida = cuotasdeudas;
                    this.CuotaRecogidaCaja = CuotaDeudasCaja;
                    this.CuotaRecogidaNomina = CuotaDeudasNomina;
                    if (cuotasdeudas > 0)
                    {
                        this.CbxRecDeudas.SelectedIndex = 1;
                        this.lblCuotaRecogida.Text = cuotasdeudas.ToString();
                    }
                    else
                    {
                        this.CbxRecDeudas.SelectedIndex = 0;
                        this.lblCuotaRecogida.Text = "0";
                    }
                    //aqui se tiene que separa las cuotas por caja y por nomina
                }
            }
            if (Invocado == false)
            {
                this.BtnRecDeudas.Visible = false;
                initializeCampos();
                this.msgliqcre.CalculaDeducciones(this.TxtCodigoter.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, ref DstoNomina, ref DstoCaja);
                this.TxtDesCajaEmpresa.Text = DstoCaja.ToString();
                this.TxtDesNomEmpresa.Text = DstoNomina.ToString();
                if (this.CbxRecDeudas.SelectedIndex == 1)
                {
                    this.lblCuotaRecogida.Text = this.ValorCuotaRecogida.ToString();
                }
            }
            ERP.Core.Compartido.Configuracion.ParamSys msgcongig = new ERP.Core.Compartido.Configuracion.ParamSys();
            DataSet datasetBuscarCompania_numcodecredit = new DataSet();
            string _codigoCompania = "0001";
            ok = msgcongig.BuscarCompania(ref _codigoCompania, ref datasetBuscarCompania_numcodecredit, mycon);
            switch (ok)
            {
                case true:
                    if (datasetBuscarCompania_numcodecredit.Tables["tblcompania"].Rows.Count > 0)
                    {
                        DataRow row = datasetBuscarCompania_numcodecredit.Tables["tblcompania"].Rows[0];
                        controlCodedudores = Convert.ToInt32(row["numcodecredit"].ToString());
                        switch (row["formapagare"].ToString())
                        {
                            case "2":
                                this.TxtNumPagare.Enabled = true;
                                break;
                            case "0":
                            case "1":
                                this.TxtNumPagare.Enabled = false;
                                break;
                        }
                        salario_compania = Convert.ToDouble(row["salario_minimo"]);
                    }
                    break;
                case false:
                    controlCodedudores = 0;
                    break;
            }

            BtnRecDeudas.Visible = false;
            ConfigDataCodeudores();
            BuscaLineaCredito();
            try
            {
                switch (this.Owner.Name)
                {
                    case "cop_fcreas01":
                    case "cop_faprobcred01":
                        BtnRecDeudas.Visible = true;
                        break;
                    default:
                        BtnRecDeudas.Visible = false;
                        break;
                }
            }
            catch (Exception)
            {
            }
            if (Information.IsNumeric(this.TxtDstoParafiscales.Text) == true && Convert.ToDouble(this.TxtDstoParafiscales.Text) == 0)
            {
                this.sumaParafiscales();
            }
            else
            {
                if (Information.IsNumeric(this.TxtDstoParafiscales.Text) == false)
                {
                    this.sumaParafiscales();
                }
            }
            this.lblCapacidadRiesgovlr.Text = msgliqcre.CalculaCapitalRiesgo(this.TxtCodigoter.Text, DateTime.Now.ToString("yyyyMM"), mycon, Convert.ToInt32(txtLincred.Text)).ToString();
            this.lblCapacidadRiesgovlr.Text = Convert.ToDouble(this.lblCapacidadRiesgovlr.Text).ToString("N0");
            this.CalculaDisponibleMes();
            CargarDatosSolvenciaActivosPasivos();

            //BuscaDatosSolicitud()
        }

        // --- BuscaLineaCredito ---
        private void BuscaLineaCredito()
        {
            int _lincred = Convert.ToInt32(this.txtLincred.Text);
            this.msgparcop.BuscaLinea(ref _lincred, ref dsdata, this.mycon);
            DataRow row = dsdata.Tables["tbllineas"].Rows[0];
            switch (row["FOGACLA"].ToString())
            {
                case "3":
                    this.BtnDatosVivienda.Enabled = true;
                    break;
                default:
                    this.BtnDatosVivienda.Enabled = false;
                    break;
            }
        }

        // --- Properties ---
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double salarioBasico
        {
            get { return _salarioBasico; }
            set { _salarioBasico = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double IngresosVariables
        {
            get { return _IngresosVariables; }
            set { _IngresosVariables = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double otroIngreso
        {
            get { return _otroIngreso; }
            set { _otroIngreso = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]

        public double otroIngresoConyuge
        {
            get { return _IngresoConyuge; }
            set { _IngresoConyuge = value; }
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double IngArriendos
        {
            get { return _IngArriendos; }
            set { _IngArriendos = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double IngPension
        {
            get { return _IngPension; }
            set { _IngPension = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double DeudasTerceros
        {
            get { return _DeudasTerceros; }
            set { _DeudasTerceros = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double GASTO_FIJO_MES
        {
            get { return _GASTO_FIJO_MES; }
            set { _GASTO_FIJO_MES = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double DstoPension
        {
            get { return _DstoPension; }
            set { _DstoPension = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string cappagoPorcentaje
        {
            get { return _cappagoPorcentaje; }
            set { _cappagoPorcentaje = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double dstoGastosPerso
        {
            get { return _dstoGastosPerso; }
            set { _dstoGastosPerso = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ValorsaldoRecogida
        {
            get { return _SaldoDuedaRecogida; }
            set { _SaldoDuedaRecogida = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double ValorCuotaRecogida
        {
            get { return _CuotaDeudaRecogida; }
            set { _CuotaDeudaRecogida = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double CuotaRecogidaNomina
        {
            get { return _CuotaDeudaRecogidaNomina; }
            set { _CuotaDeudaRecogidaNomina = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double CuotaRecogidaCaja
        {
            get { return _CuotaDeudaRecogidaCaja; }
            set { _CuotaDeudaRecogidaCaja = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string EstadoSolicitud
        {
            get { return _EstadoSolicitud; }
            set { _EstadoSolicitud = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double Porc_Paga_Solic
        {
            get { return _Porc_Paga_Solic; }
            set { _Porc_Paga_Solic = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string tipodstoPagaduria
        {
            get { return _tipodstoPagaduria; }
            set { _tipodstoPagaduria = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double varTxtGastosPnales
        {
            get { return _varTxtGastosPnales; }
            set { _varTxtGastosPnales = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int ciclo
        {
            get { return _ciclo; }
            set { _ciclo = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int periodicidad
        {
            get { return _periodicidad; }
            set { _periodicidad = value; }
        }
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double salario_compania
        {
            get { return _salario_compania; }
            set { _salario_compania = value; }
        }

        // --- BuscaDatosAsociado ---
        private void BuscaDatosAsociado()
        {
            string buscAsoSalario;
            string BuscaAsoOtroIngreso;
            double BuscaSalarioConyuge;
            double AsoIngVariables;
            double AsoIngArriendos;
            double AsoIngPension = 0;
            double AsoDeudasTerceros = 0;
            double AsoGASTO_FIJO_MES = 0;
            double AsoDstoPension;
            string AsocappagoPorcentaje = "";
            double AsodstoGastosPerso = 0;

            string _codigoter = this.TxtCodigoter.Text;
            ok = this.msgparcop.BuscaAsociado(ref _codigoter, ref this.DsDatoSolicitud, this.mycon);
            this.TxtCodigoter.Text = _codigoter;
            DataRow row = DsDatoSolicitud.Tables["tblasociados"].Rows[0];
            this.TxtApellido.Text = row["apellido"].ToString();
            this.TxtNombre.Text = row["nombre"].ToString();
            this.TxtCedula.Text = row["nit"].ToString();
            this.TxtDireccion.Text = row["direccion"].ToString();
            this.txtTelefono.Text = row["telefono1"].ToString();
            this.DtpFecNacimiento.Value = (row["fecnacem"] == DBNull.Value) ? DateTime.Now : Convert.ToDateTime(row["fecnacem"]);

            if (row["FECHA_REINGRESO"] != DBNull.Value && row["fecha_ingreso"] != DBNull.Value)
            {
                if (Convert.ToDateTime(row["FECHA_REINGRESO"]) > Convert.ToDateTime(row["fecha_ingreso"]))
                {
                    this.DtpFecCoop.Value = (row["FECHA_REINGRESO"] == DBNull.Value) ? DateTime.Now : Convert.ToDateTime(row["FECHA_REINGRESO"]);
                }
                else
                {
                    this.DtpFecCoop.Value = (row["fecha_ingreso"] == DBNull.Value) ? DateTime.Now : Convert.ToDateTime(row["fecha_ingreso"]);
                }
            }
            else
            {
                this.DtpFecCoop.Value = (row["fecha_ingreso"] == DBNull.Value) ? DateTime.Now : Convert.ToDateTime(row["fecha_ingreso"]);
            }

            this.CbxestadoCivil.SelectedIndex = (row["estado_civil"].ToString() == " ") ? 0 : Convert.ToInt32(row["estado_civil"]);
            this.TxtEmpresa.Text = row["empresa_labora"].ToString();
            this.DtpFechaIngEmpresa.Value = (row["feing_empresa"] == DBNull.Value) ? DateTime.Now : Convert.ToDateTime(row["feing_empresa"]);
            this.cbxTipoContrato.SelectedIndex = Convert.ToInt32(row["contracto"]);
            buscAsoSalario = row["salario"].ToString();
            BuscaAsoOtroIngreso = row["otro_ingreso"].ToString();
            this.TxtCesantias.Text = row["cesantias"].ToString();
            AsoIngVariables = Convert.ToDouble(row["IngVariables"]);
            AsoIngArriendos = Convert.ToDouble(row["IngArriendos"]);
            AsoIngPension = Convert.ToDouble(row["IngPension"]);
            AsoDeudasTerceros = Convert.ToDouble(row["DeudasTerceros"]);
            AsoGASTO_FIJO_MES = Convert.ToDouble(row["GASTO_FIJO_MES"]);
            AsoDstoPension = Convert.ToDouble(row["DstoPension"]);
            AsocappagoPorcentaje = row["cappagoPorcentaje"].ToString();
            AsodstoGastosPerso = Convert.ToDouble(row["dstoGastosPerso"]);

            if (this.cbxTipoContrato.SelectedIndex == -1)
            {
                this.cbxTipoContrato.SelectedIndex = 0;
            }
            if (Invocado == false)
            {
                this.TxtEmpDescuento.Text = row["Empresa"].ToString();
            }
            this.TxtNomConyuge.Text = row["CONYUGE"].ToString();
            this.TxtTelConyuge.Text = row["CONYTELEF"].ToString();
            this.TxtCiudadConyuge.Text = row["CONYCIUD"].ToString();
            this.TxtEmpLaboraconyuge.Text = row["CONYEMPR"].ToString();
            BuscaSalarioConyuge = Convert.ToDouble(row["CONYSALAR"]);
            //  Me.TxtSalConyuge.Text = .Item("CONYSALAR")
            this.TxtDirEmpresaConyuge.Text = row["CONYDIREC"].ToString();
            this.TxtIdCargo.Text = row["cargo"].ToString();
            this.TxtIdCiudad.Text = row["dpto_ciudad"].ToString();
            string _ciudad = this.TxtCiudad.Text;
            msgparcop.BuscaCiudad(row["dpto_ciudad"].ToString(), mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _ciudad);
            this.TxtCiudad.Text = _ciudad;
            string _cargo = this.TxtCargo.Text;
            string _cargoId = row["cargo"].ToString();
            msgparcop.BuscaCargos(ref _cargoId, mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _cargo);
            this.TxtCargo.Text = _cargo;
            string _dummy1 = "", _dummy2 = "", _dummy3 = "", _dummy4 = "", _dummy5 = "", _dummy6 = "", _dummy7 = "", _dummy8 = "", _dummy9 = "", _dummy10 = "";
            string _dummy11 = "", _dummy12 = "", _dummy13 = "", _dummy14 = "", _dummy15 = "", _dummy16 = "", _dummy17 = "", _dummy18 = "", _dummy19 = "", _dummy20 = "";
            string _porDsctoStr = PorDscto.ToString();
            string _tipoDsctoStr = TipoDscto;
            //msgparcop.BuscaEmpresa(row["Empresa"].ToString(), this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
            //    ref _dummy1, ref _dummy2, ref _dummy3, ref _dummy4, ref _dummy5, ref _dummy6, ref _dummy7,
            //    ref _dummy8, ref _dummy9, ref _dummy10, ref _dummy11, ref _dummy12, ref _dummy13, ref _dummy14,
            //    ref _dummy15, ref _dummy16, ref _dummy17, ref _dummy18, ref _dummy19, ref _dummy20,
            //    ref _porDsctoStr, ref _tipoDsctoStr);
            double.TryParse(_porDsctoStr, out PorDscto);
            TipoDscto = _tipoDsctoStr;

            string _tipoAsociadoStr = TipoAsociado;
            string _da1 = "", _da2 = "", _da3 = "", _da4 = "", _da5 = "", _da6 = "", _da7 = "", _da8 = "";
            string _da9 = "", _da10 = "", _da11 = "", _da12 = "", _da13 = "", _da14 = "", _da15 = "", _da16 = "";
            string _da17 = "";
            //msgparcop.BuscaAsociado(this.TxtCodigoter.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,
            //    ref _da1, ref _da2, ref _da3, ref _da4, ref _da5, ref _da6, ref _da7, ref _da8,
            //    ref _da9, ref _da10, ref _da11, ref _da12, ref _da13, ref _da14, ref _da15, ref _da16,
            //    ref _da17, ref _tipoAsociadoStr);
            TipoAsociado = _tipoAsociadoStr;

            if (Convert.ToDouble(buscAsoSalario) != this.salarioBasico && this.salarioBasico > 0)
            {
                this.TxtSalario.Text = this.salarioBasico.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtSalario.Text = buscAsoSalario;
                }
            }

            if (AsoIngVariables != this.IngresosVariables && this.IngresosVariables > 0)
            {
                this.TxtIngVariables.Text = this.IngresosVariables.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtIngVariables.Text = AsoIngVariables.ToString();
                }
            }

            if (AsoIngArriendos != this.IngArriendos && this.IngArriendos > 0)
            {
                this.TxtIngArriendos.Text = this.IngArriendos.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtIngArriendos.Text = AsoIngArriendos.ToString();
                }
            }

            if (AsoIngPension != this.IngPension && this.IngPension > 0)
            {
                this.TxtPensiones.Text = this.IngPension.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtPensiones.Text = AsoIngPension.ToString();
                }
            }

            if (AsoDeudasTerceros != this.DeudasTerceros && this.DeudasTerceros > 0)
            {
                this.TxtDeudasTerceros.Text = this.DeudasTerceros.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtDeudasTerceros.Text = AsoDeudasTerceros.ToString();
                }
            }

            if (AsoGASTO_FIJO_MES != this.GASTO_FIJO_MES && this.GASTO_FIJO_MES > 0)
            {
                this.TxtGastosMes.Text = this.GASTO_FIJO_MES.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtGastosMes.Text = AsoGASTO_FIJO_MES.ToString();
                }
            }
            if (AsoDstoPension != this.DstoPension && this.DstoPension > 0)
            {
                this.TxtDstoPension.Text = this.DstoPension.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtDstoPension.Text = AsoDstoPension.ToString();
                }
            }

            if (cappagoPorcentaje != "L" && Invocado == false)
            {
                switch (cappagoPorcentaje)
                {
                    case "N":
                        ChkPorcentaje.Checked = false;
                        break;
                    case "Y":
                        ChkPorcentaje.Checked = true;
                        break;
                }
            }
            else if (Invocado == false)
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    switch (AsocappagoPorcentaje)
                    {
                        case "N":
                            ChkPorcentaje.Checked = false;
                            break;
                        case "Y":
                            ChkPorcentaje.Checked = true;
                            break;
                    }
                }
            }

            if (AsodstoGastosPerso != this.dstoGastosPerso && this.dstoGastosPerso > 0)
            {
                this.TxtGastosPnales.Text = this.dstoGastosPerso.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtGastosPnales.Text = AsodstoGastosPerso.ToString();
                }
            }

            if (Convert.ToDouble(BuscaAsoOtroIngreso) != this.otroIngreso && this.otroIngreso > 0)
            {
                this.TxtOtrosIngresos.Text = this.otroIngreso.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtOtrosIngresos.Text = BuscaAsoOtroIngreso;
                }
            }
            if (BuscaSalarioConyuge != this.otroIngresoConyuge && this.otroIngresoConyuge > 0)
            {
                this.TxtSalConyuge.Text = this.otroIngresoConyuge.ToString();
                txtIngresoConyuge.Text = this.otroIngresoConyuge.ToString();
            }
            else
            {
                if (this.EstadoSolicitud.ToString().Trim() == "P")
                {
                    this.TxtSalConyuge.Text = BuscaSalarioConyuge.ToString();
                    txtIngresoConyuge.Text = BuscaSalarioConyuge.ToString();
                }
            }

            switch (this.EstadoSolicitud.ToString().Trim())
            {
                case "A":
                case "G":
                case "D":
                case "E":
                    this.LblPorcentajePagaduria.Text = this.Porc_Paga_Solic.ToString();
                    PorDscto = this.Porc_Paga_Solic;
                    TipoDscto = this.tipodstoPagaduria;
                    break;
                default:
                    this.LblPorcentajePagaduria.Text = PorDscto.ToString();
                    break;
            }
        }

        // --- ActualizaHojaVidaAsociado ---
        private void ActualizaHojaVidaAsociado()
        {
            DataSet dsdata = new DataSet();
            string ststring = "";
            DateTime stdate = DateTime.Now;
            double stdoub = 0;
            int stint = 0;
            string _cappagoPorcentajeLocal;
            dsdata.Tables.Add("TblActualizaAsoc");
            DataColumnCollection cols = dsdata.Tables["TblActualizaAsoc"].Columns;
            cols.Add("Direccion", ststring.GetType());
            cols.Add("telefono", ststring.GetType());
            cols.Add("estadocivil", ststring.GetType());
            cols.Add("FecNacimiento", stdate.GetType());
            cols.Add("EmpresaLabora", ststring.GetType());
            cols.Add("FeIngEmpresa", stdate.GetType());
            cols.Add("TipoContrato", stint.GetType());
            cols.Add("salario", stdoub.GetType());
            cols.Add("OTRO_INGRESO", stdoub.GetType());
            cols.Add("Nomconyuge", ststring.GetType());
            cols.Add("Teleconyuge", ststring.GetType());
            cols.Add("Dirempconyuge", ststring.GetType());
            cols.Add("Empconyuge", ststring.GetType());
            cols.Add("Ciuconyuge", ststring.GetType());
            cols.Add("salarconyuge", stdoub.GetType());
            cols.Add("cesantias", stdoub.GetType());
            cols.Add("idcargo", ststring.GetType());
            cols.Add("idciudad", ststring.GetType());
            cols.Add("TxtIngArriendos", ststring.GetType());
            cols.Add("TxtPensiones", ststring.GetType());
            cols.Add("TxtDeudasTerceros", ststring.GetType());
            cols.Add("TxtGastosMes", ststring.GetType());
            cols.Add("TxtDstoPension", ststring.GetType());
            cols.Add("ChkPorcentaje", ststring.GetType());
            cols.Add("TxtGastosPnales", ststring.GetType());
            cols.Add("TxtIngVariables", ststring.GetType());

            switch (ChkPorcentaje.Checked)
            {
                case false:
                    _cappagoPorcentajeLocal = "N";
                    break;
                case true:
                    _cappagoPorcentajeLocal = "Y";
                    break;
                default:
                    _cappagoPorcentajeLocal = "N";
                    break;
            }

            dsdata.Tables["TblActualizaAsoc"].Rows.Add(this.TxtDireccion.Text, this.txtTelefono.Text, this.CbxestadoCivil.SelectedIndex.ToString(), this.DtpFecNacimiento.Value, this.TxtEmpresa.Text, this.DtpFechaIngEmpresa.Value, this.cbxTipoContrato.SelectedIndex, this.TxtSalario.Text, this.TxtOtrosIngresos.Text,
                this.TxtNomConyuge.Text.Trim(), this.TxtTelConyuge.Text.Trim(), this.TxtDirEmpresaConyuge.Text.Trim(), this.TxtEmpLaboraconyuge.Text.Trim(), this.TxtCiudadConyuge.Text.Trim(), this.TxtSalConyuge.Text.Trim(), this.TxtCesantias.Text, this.TxtIdCargo.Text, this.TxtIdCiudad.Text, this.TxtIngArriendos.Text, this.TxtPensiones.Text, this.TxtDeudasTerceros.Text, this.TxtGastosMes.Text, this.TxtDstoPension.Text, _cappagoPorcentajeLocal, this.TxtGastosPnales.Text, this.TxtIngVariables.Text);
            this.msgcop.ActualizarAsociado(this.TxtCodigoter.Text, dsdata, this.mycon);
        }

        // --- initializeCampos ---
        private void initializeCampos()
        {
            this.CbxClaseGar.SelectedIndex = 0;
            this.TxtSalAportes1.Text = "0";
            this.TxtSalAportes2.Text = "0";
            this.TxtSalAportes3.Text = "0";
            this.TxtSalAportes4.Text = "0";
            this.TxtSalDeuda1.Text = "0";
            this.TxtSalDeuda2.Text = "0";
            this.TxtSalDeuda3.Text = "0";
            this.TxtSalDeuda4.Text = "0";
            this.TxtDeudaResp1.Text = "0";
            this.TxtDeudaResp2.Text = "0";
            this.TxtDeudaResp3.Text = "0";
            this.TxtDeudaResp4.Text = "0";
            this.CbxClaseGar.SelectedIndex = 0;
            this.TxtCodeu1.Text = "";
            this.TxtCodeu2.Text = "";
            this.TxtCodeu3.Text = "";
            this.TxtCodeu4.Text = "";
            this.TxtSalarioCode1.Text = "0";
            this.TxtSalarioCode2.Text = "0";
            this.TxtSalarioCode3.Text = "0";
            this.TxtSalarioCode4.Text = "0";
            this.TxtOtroIngCode1.Text = "0";
            this.TxtOtroIngCode2.Text = "0";
            this.TxtOtroIngCode3.Text = "0";
            this.TxtOtroIngCode4.Text = "0";
            this.TxtIngArrCode1.Text = "0";
            this.TxtIngArrCode2.Text = "0";
            this.TxtIngArrCode3.Text = "0";
            this.TxtIngArrCode4.Text = "0";
            this.TxtIngVarCode1.Text = "0";
            this.TxtIngVarCode2.Text = "0";
            this.TxtIngVarCode3.Text = "0";
            this.TxtIngVarCode4.Text = "0";
            this.TxtDstoEmpCode1.Text = "0";
            this.TxtDstoEmpCode2.Text = "0";
            this.TxtDstoEmpCode3.Text = "0";
            this.TxtDstoEmpCode4.Text = "0";
            this.TxtDeuTerCode1.Text = "0";
            this.TxtDeuTerCode2.Text = "0";
            this.TxtDeuTerCode3.Text = "0";
            this.TxtDeuTerCode4.Text = "0";
            this.TxtOtroDstocode1.Text = "0";
            this.TxtOtroDstocode2.Text = "0";
            this.TxtOtroDstocode3.Text = "0";
            this.TxtOtroDstocode4.Text = "0";
            this.LblDispCode1.Text = "0";
            this.LblDispCode2.Text = "0";
            this.LblDispCode3.Text = "0";
            this.LblDispCode4.Text = "0";
            chkGastoperCod1.Checked = false;
            txtGastoperCod1.Text = "0";
            chkGastoperCod2.Checked = false;
            txtGastoperCod2.Text = "0";
            chkGastoperCod3.Checked = false;
            txtGastoperCod3.Text = "0";
            chkGastoperCod4.Checked = false;
            txtGastoperCod4.Text = "0";
            TxtDstoEmpCajaCode1.Text = "0";
            TxtDstoEmpCajaCode2.Text = "0";
            TxtDstoEmpCajaCode3.Text = "0";
            TxtDstoEmpCajaCode4.Text = "0";
        }

        // --- salir_Click ---
        private void salir_Click(object sender, EventArgs e)
        {
            this.Close();
            this.Dispose();
        }

        // --- CalculaDisponibleMes ---
        private void CalculaDisponibleMes()
        {
            this.TxtSalario.Text = Convert.ToDouble(this.TxtSalario.Text).ToString("N0");
            this.TxtOtrosIngresos.Text = Convert.ToDouble(this.TxtOtrosIngresos.Text).ToString("N0");
            this.TxtIngArriendos.Text = Convert.ToDouble(this.TxtIngArriendos.Text).ToString("N0");
            this.TxtIngVariables.Text = Convert.ToDouble(this.TxtIngVariables.Text).ToString("N0");
            this.TxtPensiones.Text = Convert.ToDouble(this.TxtPensiones.Text).ToString("N0");
            this.TxtDesCajaEmpresa.Text = Convert.ToDouble(this.TxtDesCajaEmpresa.Text).ToString("N0");
            this.TxtDesNomEmpresa.Text = Convert.ToDouble(this.TxtDesNomEmpresa.Text).ToString("N0");
            this.TxtGastosMes.Text = Convert.ToDouble(this.TxtGastosMes.Text).ToString("N0");
            this.TxtDeudasTerceros.Text = Convert.ToDouble(this.TxtDeudasTerceros.Text).ToString("N0");
            this.TxtDstoPension.Text = Convert.ToDouble(this.TxtDstoPension.Text).ToString("N0");
            this.TxtDstoParafiscales.Text = Convert.ToDouble(this.TxtDstoParafiscales.Text).ToString("N0");
            this.TxtDesCajaEmpresa.Text = Convert.ToDouble(this.TxtDesCajaEmpresa.Text).ToString("N0");
            this.txtIngresoConyuge.Text = Convert.ToDouble(this.txtIngresoConyuge.Text).ToString("N0");
            this.TxtGastosPnales.Text = Convert.ToDouble(this.TxtGastosPnales.Text).ToString("N0");

            this.SumaIngresos();
            this.SumaEgresos();
            this.DisponibleparaMes();
            DisponibleNomina();
        }

        // --- LostFocus handlers for income/expense fields ---
        private void TxtSalario_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtSalario.Text))
            {
                this.TxtSalario.Text = "0";
            }
            sumaParafiscales();
            CalculaDisponibleMes();
        }

        private void TxtOtrosIngresos_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtOtrosIngresos.Text))
            {
                this.TxtOtrosIngresos.Text = "0";
            }
            sumaParafiscales();
            CalculaDisponibleMes();
        }

        private void TxtIngArriendos_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtIngArriendos.Text))
            {
                this.TxtIngArriendos.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void TTxtIngVariables_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtIngVariables.Text))
            {
                this.TxtIngVariables.Text = "0";
            }
            CalculaDisponibleMes();
            sumaParafiscales();
        }

        private void TxtPensiones_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtPensiones.Text))
            {
                this.TxtPensiones.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void txtIngresoConyuge_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.txtIngresoConyuge.Text))
            {
                this.txtIngresoConyuge.Text = "0";
            }
            if (Convert.ToDouble(this.txtIngresoConyuge.Text) > 0 && (Convert.ToDouble(txtIngresoConyuge.Text) > Convert.ToDouble(TxtSalConyuge.Text)))
            {
                this.TxtSalConyuge.Text = txtIngresoConyuge.Text;
            }
            CalculaDisponibleMes();
        }

        private void TxtDesCajaEmpresa_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtDesCajaEmpresa.Text))
            {
                this.TxtDesCajaEmpresa.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void TxtDesNomEmpresa_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtDesNomEmpresa.Text))
            {
                this.TxtDesNomEmpresa.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void TxtGastosMes_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtGastosMes.Text))
            {
                this.TxtGastosMes.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void TxtDeudasTerceros_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtDeudasTerceros.Text))
            {
                this.TxtDeudasTerceros.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void TxtDstoPension_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtDstoPension.Text))
            {
                this.TxtDstoPension.Text = "0";
            }
            CalculaDisponibleMes();
        }

        private void TxtGastosPnales_LostFocus(object sender, EventArgs e)
        {
            double gastospersonales = 0;

            if (!Information.IsNumeric(this.TxtGastosPnales.Text))
            {
                this.TxtGastosPnales.Text = "0";
            }

            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.TxtGastosPnales.Text))
                    {
                        if (Convert.ToDouble(this.TxtGastosPnales.Text) >= 0 && Convert.ToDouble(this.TxtGastosPnales.Text) <= 100)
                        {
                            gastospersonales = Convert.ToDouble(this.TxtSalario.Text) * (Convert.ToDouble(this.TxtGastosPnales.Text) / 100);
                        }
                        else
                        {
                            MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            gastospersonales = 0;
                            this.TxtGastosPnales.Text = "0";
                            this.TxtGastosPnales.Focus();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Los gastos personales deben ser un porcentaje.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        gastospersonales = 0;
                        this.TxtGastosPnales.Text = "0";
                        this.TxtGastosPnales.Focus();
                    }
                    break;
                case false:
                    if (this.TxtGastosPnales.Text == "0")
                    {
                        this.TxtGastosPnales.Text = varTxtGastosPnales.ToString();
                    }
                    this.TxtGastosPnales.Text = Convert.ToDouble(this.TxtGastosPnales.Text).ToString("N0");
                    break;
            }
            CalculaDisponibleMes();
        }

        private void TxtDstoParafiscales_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtDstoParafiscales.Text))
            {
                this.TxtDstoParafiscales.Text = "0";
            }
            CalculaDisponibleMes();
        }

        // --- guardar_Click ---
        private void guardar_Click(object sender, EventArgs e)
        {
            GuardarSolicitud();
        }

        // --- GuardarSolicitud ---
        private void GuardarSolicitud()
        {
            DataSet DsSolicitud = new DataSet();
            bool ImpFormatos = false;
            ERP.Core.CarteraFinanciera.Reportes.ImpreDoc ImpLibPagare = new ERP.Core.CarteraFinanciera.Reportes.ImpreDoc(this.usuario.Text);

            if (Validar() == true)
            {
                DsSolicitud = CargaDatosSolicitud();
                DsSolicitud.Tables.Add(this.DsDatosProyeccion.Tables["tblextras"].Copy());
                DsSolicitud.Tables.Add(this.DsDatosProyeccion.Tables["tbldeducciones"].Copy());
                DsSolicitud.Tables.Add(this.DsDataCodeudores.Tables["TblCodeudores"].Copy());
                if (this.dsbienes.Tables.Count > 0)
                {
                    DsSolicitud.Tables.Add(this.dsbienes.Tables["tblBienesRaices"].Copy());
                    DsSolicitud.Tables.Add(this.dsbienes.Tables["tblVehiculo"].Copy());
                }
                if (this.DsReferencia.Tables.Count > 0)
                {
                    DsSolicitud.Tables.Add(this.DsReferencia.Tables["TblReferencia"].Copy());
                }
                if (this.dsParViv.Tables.Count > 0)
                {
                    DsSolicitud.Tables.Add(this.dsParViv.Tables["tblsolparviv"].Copy());
                }

                if (Convert.ToDouble(this.LblNumSolicitud.Text) == 0)
                {
                    ImpFormatos = true;
                }

                this.LblNumSolicitud.Text = this.msgliqcre.GrabaSolicitudCredito(DsSolicitud, this.usuario.Text, this.mycon, this, Convert.ToDouble(this.LblNumSolicitud.Text), ref estado).ToString();
                ActualizaHojaVidaAsociado();
                this.DesactivaCampos();
                if (ImpFormatos == true)
                {
                    this.ImprimeSolicitud(Convert.ToDouble(this.LblNumSolicitud.Text));
                    ImpLibPagare.imp_libran_pagare(Convert.ToInt32(this.LblNumSolicitud.Text), mycon, "", 0, Convert.ToInt32(this.txtLincred.Text), this.TxtCodigoter.Text);
                }
            }
        }

        // --- Validar ---
        private bool Validar()
        {
            string codigoasociado = " ";
            if (this.CbxClaseGar.SelectedIndex == 0)
            {
                MessageBox.Show("Debe escoger un tipo de garantia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxClaseGar.Focus();
                return false;
            }
            if (this.CbxClaseGar.SelectedIndex == 10)
            {
                if (Information.IsNumeric(this.TxtNumCdat.Text))
                {
                    //ok = this.clsmsgcdat.BuscarCdats(this.TxtNumCdat.Text, this.mycon, ref codigoasociado);
                    switch (ok)
                    {
                        case false:
                            MessageBox.Show("Cdta no encontrado. Por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            this.TxtNumCdat.Text = "";
                            this.TxtNumCdat.Focus();
                            return false;
                        case true:
                            if (this.TxtCodigoter.Text != codigoasociado)
                            {
                                MessageBox.Show("Este Cdta no le pertenece. Por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                this.TxtNumCdat.Text = "";
                                this.TxtNumCdat.Focus();
                                return false;
                            }
                            break;
                    }
                }
                else
                {
                    MessageBox.Show("Debe ingresar un numero de cdta para la garantia.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtNumCdat.Text = "";
                    this.TxtNumCdat.Focus();
                    return false;
                }
            }
            if (Information.IsNumeric(this.TxtIngVariables.Text) == false)
            {
                MessageBox.Show("Los ingresos variables debe ser numerico.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtIngVariables.Text = "0";
                this.TxtIngVariables.Focus();
                return false;
            }
            if (Information.IsNumeric(this.TxtIngArriendos.Text) == false)
            {
                MessageBox.Show("Los ingresos Arriendos debe ser numerico.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtIngArriendos.Text = "0";
                this.TxtIngArriendos.Focus();
                return false;
            }
            if (Information.IsNumeric(this.TxtDeudasTerceros.Text) == false)
            {
                MessageBox.Show("Los deudas con terceros debe ser numerico.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtDeudasTerceros.Text = "0";
                this.TxtDeudasTerceros.Focus();
                return false;
            }
            if (this.TxtCodeudor1.Text.Trim() != "")
            {
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor1.Text, this.mycon);
                if (ok == false)
                {
                    MessageBox.Show("Codeudor 1 no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCodeudor1.Text = "";
                    this.TxtCodeudor1.Focus();
                    return false;
                }
            }
            if (this.TxtCodeudor2.Text.Trim() != "")
            {
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor2.Text, this.mycon);
                if (ok == false)
                {
                    MessageBox.Show("Codeudor 2 no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCodeudor2.Text = "";
                    this.TxtCodeudor2.Focus();
                    return false;
                }
            }
            if (this.TxtCodeudor3.Text.Trim() != "")
            {
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor3.Text, this.mycon);
                if (ok == false)
                {
                    MessageBox.Show("Codeudor 3 no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCodeudor3.Text = "";
                    this.TxtCodeudor3.Focus();
                    return false;
                }
            }
            if (this.TxtCodeudor4.Text.Trim() != "")
            {
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor4.Text, this.mycon);
                if (ok == false)
                {
                    MessageBox.Show("Codeudor 4 no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCodeudor4.Text = "";
                    this.TxtCodeudor4.Focus();
                    return false;
                }
            }
            //ok = this.msgparcop.BuscaCargos(this.TxtIdCargo.Text, mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno);
            if (ok == false)
            {
                MessageBox.Show("Cargo no existe, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtIdCargo.Focus();
                return false;
            }

            if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp1.Text) >= this.controlCodedudores)
            {
                MessageBox.Show(TxtNomCodeudor1.Text + "  no puede ser Codeudor  por  que ya paso el limete maximo   de los asociados  a los  cuales les puede servir de codeudor");
                this.TxtCodeudor1.Focus();
                return false;
            }
            if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp2.Text) >= this.controlCodedudores)
            {
                MessageBox.Show(TxtNomCodeudor2.Text + "  no puede ser Codeudor  por  que ya paso el limete maximo   de los asociados  a los  cuales les puede servir de codeudor");
                this.TxtCodeudor2.Focus();
                return false;
            }
            if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp3.Text) >= this.controlCodedudores)
            {
                MessageBox.Show(TxtNomCodeudor3.Text + "  no puede ser Codeudor  por  que ya paso el limete maximo   de los asociados  a los  cuales les puede servir de codeudor");
                this.TxtCodeudor3.Focus();
                return false;
            }
            if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp4.Text) >= this.controlCodedudores)
            {
                MessageBox.Show(TxtNomCodeudor4.Text + "  no puede ser Codeudor  por  que ya paso el limete maximo   de los asociados  a los  cuales les puede servir de codeudor");
                this.TxtCodeudor4.Focus();
                return false;
            }
            if (this.CbxRecDeudas.SelectedIndex == -1)
            {
                MessageBox.Show("Debe escoger una opcion en recoge deuda.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxRecDeudas.Focus();
                return false;
            }
            if (this.ChkPorcentaje.Checked == true)
            {
                if (Convert.ToDouble(this.TxtGastosPnales.Text) <= 0 || Convert.ToDouble(this.TxtGastosPnales.Text) >= 100)
                {
                    MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtGastosPnales.Text = "0";
                    this.TxtGastosPnales.Focus();
                    return false;
                }
            }

            return true;
        }

        // --- DesactivaCampos ---
        private void DesactivaCampos()
        {
            Panel1.Enabled = false;
            this.guardar.Enabled = false;
            this.imprimir.Enabled = true;
            //Me.imprimelibra.Enabled = True
            imprimelibra.Enabled = true;
        }

        // --- CargaDatosSolicitud ---
        private DataSet CargaDatosSolicitud()
        {
            double stdouble = 0;
            string ststring = " ";
            decimal Stdecimal = 0;
            DateTime stdate = DateTime.Now;
            int stinteger = 0;
            string CasaPropia = "N";
            string TieneVehiculo = "N";
            string GarAsefurado = "N";
            string TrabConyuge = "N";
            double VlrVpnExtra;
            double SalAportes;
            string agencia = " ";
            string CentroCosto = " ";
            double CuotaAdm = 0;
            double CuotaSeg = 0;
            double CuotaCapital = 0;
            double CuotaIcie = 0;
            double CuotaOtros = 0;
            int TipoIncie = 0;
            int TipoCap = 0;
            int TipoAdm = 0;
            int TipoSeg = 0;
            int TipoOtro = 0;
            int foradm = 0;
            int cptoAdm = 0;
            int CptoSeg = 0;
            int cptoOtro = 0;
            int tasaOtro = 0;
            int periodoGracia = 0;
            int perGraciaini = 0;
            int ciclopergracia = 0;
            int clades = 0;
            string cuexinmes = " ";
            string cuexintant = " ";
            string pag1cuo = " ";
            string tippag2 = " ";
            decimal Dtf = 0;
            decimal puntos = 0;
            int cicloLocal = 0;
            int Stperiodicidad = 0;
            int clacuo = 0;
            int claint = 0;
            decimal tasaadm = 0;
            decimal tasaseg = 0;
            decimal tasacpt = 0;
            string cappagoPorcentajeLocal = "";
            string MesesGracias = "0";
            string FormaGracia = "0";
            string CicloGracias = "0";
            DataSet DsSolicitud = new DataSet();
            DsSolicitud.Tables.Add("tblsolicitud");

            DataColumnCollection cols = DsSolicitud.Tables["tblsolicitud"].Columns;
            cols.Add("ValorSolicitud", stdouble.GetType());
            cols.Add("Lincred", stdouble.GetType());
            cols.Add("FecSolicitud", stdate.GetType());
            cols.Add("DescripcionLinea", ststring.GetType());
            cols.Add("TasaInt", Stdecimal.GetType());
            cols.Add("Plazo", stdouble.GetType());
            cols.Add("Cuota", stdouble.GetType());
            cols.Add("BaseCupo", stdouble.GetType());
            cols.Add("FechaDsto", stdate.GetType());
            cols.Add("CupoDisponible", stdouble.GetType());
            cols.Add("Codigoter", ststring.GetType());
            cols.Add("Apellido", ststring.GetType());
            cols.Add("Nombre", ststring.GetType());
            cols.Add("Cedula", ststring.GetType());
            cols.Add("Direccion", ststring.GetType());
            cols.Add("ciudad", ststring.GetType());
            cols.Add("Telefono", ststring.GetType());
            cols.Add("EstadoCivil", stinteger.GetType());
            cols.Add("FechaNac", stdate.GetType());
            cols.Add("FecIngCoop", stdate.GetType());
            cols.Add("CasaPropia", ststring.GetType());
            cols.Add("TieneVehiculo", ststring.GetType());
            cols.Add("Vehiculo", ststring.GetType());
            cols.Add("EmpresaLaboral", ststring.GetType());
            cols.Add("FecIngLaboral", stdate.GetType());
            cols.Add("TipoContracto", stinteger.GetType());
            cols.Add("CargoLaboral", ststring.GetType());
            cols.Add("Salario", stdouble.GetType());
            cols.Add("OtrosIngresos", stdouble.GetType());
            cols.Add("DstoEmpresa", stdouble.GetType());
            cols.Add("GastosFijos", stdouble.GetType());
            cols.Add("DisponibleMes", stdouble.GetType());
            cols.Add("ClaseGar", ststring.GetType());
            cols.Add("AvaluoComercial", stdouble.GetType());
            cols.Add("AvaluoCatastral", stdouble.GetType());
            cols.Add("DescGara", ststring.GetType());
            cols.Add("GaraAsegurado", ststring.GetType());
            cols.Add("Fecvenseg", stdate.GetType());
            cols.Add("porseg", Stdecimal.GetType());
            cols.Add("codeudor1", ststring.GetType());
            cols.Add("codeudor2", ststring.GetType());
            cols.Add("codeudor3", ststring.GetType());
            cols.Add("codeudor4", ststring.GetType());
            cols.Add("SalAportes1", stdouble.GetType());
            cols.Add("SalAportes2", stdouble.GetType());
            cols.Add("SalAportes3", stdouble.GetType());
            cols.Add("SalAportes4", stdouble.GetType());
            cols.Add("SalDeuda1", stdouble.GetType());
            cols.Add("SalDeuda2", stdouble.GetType());
            cols.Add("SalDeuda3", stdouble.GetType());
            cols.Add("SalDeuda4", stdouble.GetType());
            cols.Add("DeudaResp1", stdouble.GetType());
            cols.Add("DeudaResp2", stdouble.GetType());
            cols.Add("DeudaResp3", stdouble.GetType());
            cols.Add("DeudaResp4", stdouble.GetType());
            cols.Add("Nomconyuge", ststring.GetType());
            cols.Add("Empconyuge", ststring.GetType());
            cols.Add("Dirempconyuge", ststring.GetType());
            cols.Add("telconyuge", ststring.GetType());
            cols.Add("Trabconyuge", ststring.GetType());
            cols.Add("PerCargo", stdouble.GetType());
            cols.Add("SalConyuge", stdouble.GetType());
            cols.Add("DirEmpConyuge", ststring.GetType());
            cols.Add("CiudadConyuge", ststring.GetType());
            cols.Add("VlrVpnExtra", stdouble.GetType());
            cols.Add("SalAportes", stdouble.GetType());
            cols.Add("Agencia", ststring.GetType());
            cols.Add("CentroCosto", ststring.GetType());
            cols.Add("CuotaAdm", stdouble.GetType());
            cols.Add("CuotaSeg", stdouble.GetType());
            cols.Add("CuotaCapital", stdouble.GetType());
            cols.Add("CuotaIcie", stdouble.GetType());
            cols.Add("CuotaOtros", stdouble.GetType());
            cols.Add("TipoIntCie", stinteger.GetType());
            cols.Add("TipoCap", stinteger.GetType());
            cols.Add("TipoAdm", stinteger.GetType());
            cols.Add("Tiposeg", stinteger.GetType());
            cols.Add("TipoOtro", stinteger.GetType());
            cols.Add("foradm", stinteger.GetType());
            cols.Add("cptoadm", stinteger.GetType());
            cols.Add("cptoseg", stinteger.GetType());
            cols.Add("cptootro", stinteger.GetType());
            cols.Add("tasaotro", Stdecimal.GetType());
            cols.Add("pergracia", stinteger.GetType());
            cols.Add("pergraciaini", stinteger.GetType());
            cols.Add("ciclopergracia", stinteger.GetType());
            cols.Add("clades", stinteger.GetType());
            cols.Add("cuexinmes", ststring.GetType());
            cols.Add("cuexintant", ststring.GetType());
            cols.Add("pag1cuo", ststring.GetType());
            cols.Add("tippag2", ststring.GetType());
            cols.Add("Ciclo", ststring.GetType());
            cols.Add("periodicidad", ststring.GetType());
            cols.Add("clacuo", ststring.GetType());
            cols.Add("claint", ststring.GetType());
            cols.Add("TasaAdm", ststring.GetType());
            cols.Add("TasaSeg", ststring.GetType());
            cols.Add("TasaCpt", ststring.GetType());
            cols.Add("IngVariables", stdouble.GetType());
            cols.Add("IngArriendos", stdouble.GetType());
            cols.Add("DeudasTerceros", stdouble.GetType());
            cols.Add("numcdat", stdouble.GetType());
            cols.Add("nitaseguradora", ststring.GetType());
            cols.Add("nombreaseguradora", ststring.GetType());
            cols.Add("numpoliza", ststring.GetType());
            cols.Add("matricula", ststring.GetType());
            cols.Add("empdsto", ststring.GetType());
            cols.Add("NumPagare", stdouble.GetType());
            cols.Add("IngPension", stdouble.GetType());
            cols.Add("DstoPension", stdouble.GetType());
            cols.Add("DstoParafiscales", stdouble.GetType());
            cols.Add("DstoEmpresaNomina", stdouble.GetType());
            cols.Add("dtf", Stdecimal.GetType());
            cols.Add("puntos", Stdecimal.GetType());
            cols.Add("dtsGastosPersonales", Stdecimal.GetType());
            cols.Add("IngreseConyuge", Stdecimal.GetType());
            cols.Add("cappagoPorcentaje", ststring.GetType());
            cols.Add("cappagoRecDeudas", ststring.GetType());
            cols.Add("MesesGracia", ststring.GetType());
            cols.Add("FormaGracia", ststring.GetType());
            cols.Add("TxtSolActVivienda", stdouble.GetType());
            cols.Add("TxtSolActVehiculo", stdouble.GetType());
            cols.Add("TxtSolActOtros", stdouble.GetType());
            cols.Add("TxtSolActAportes", stdouble.GetType());
            cols.Add("TxtActCtaBanco", stdouble.GetType());
            cols.Add("TxtActCxC", stdouble.GetType());
            cols.Add("TxtSolActAhorros", stdouble.GetType());
            cols.Add("TxtSolActTotal", stdouble.GetType());
            cols.Add("TxtSolPasDeudas", stdouble.GetType());
            cols.Add("TxtSolPasOtros", stdouble.GetType());
            cols.Add("TxtPasObliBanca", stdouble.GetType());
            cols.Add("TxtPasObliHipot", stdouble.GetType());
            cols.Add("TxtSolPasTotal", stdouble.GetType());
            cols.Add("TxtSolPatrimonio", stdouble.GetType());
            cols.Add("TxtSolPasTotPyP", stdouble.GetType());
            cols.Add("capacNomina", stdouble.GetType());
            cols.Add("PorcNomina", stdouble.GetType());
            cols.Add("CapPago", stdouble.GetType());
            cols.Add("PorcCaja", stdouble.GetType());
            cols.Add("PorcentajePagaduria", stdouble.GetType());
            cols.Add("LblNomina", stdouble.GetType());
            cols.Add("LblCaja", stdouble.GetType());
            cols.Add("Descubierto", stdouble.GetType());
            cols.Add("NivelEndeudamiento", stdouble.GetType());
            cols.Add("NivelContingencia", stdouble.GetType());
            cols.Add("CapitalRiesgo", stdouble.GetType());
            cols.Add("capacdsto", stdouble.GetType());
            cols.Add("Porcdsto", stdouble.GetType());
            cols.Add("tipodstoPagaduria", ststring.GetType());

            TrabConyuge = this.ChkTrabConyuge.Checked ? "Y" : "N";
            TieneVehiculo = this.ChkVehiculo.Checked ? "Y" : "N";
            CasaPropia = this.ChkCasaPropia.Checked ? "Y" : "N";
            GarAsefurado = this.ChkAsegurado.Checked ? "Y" : "N";
            cappagoPorcentajeLocal = ChkPorcentaje.Checked ? "Y" : "N";

            DataRow projRow = this.DsDatosProyeccion.Tables["TbldatosCredito"].Rows[0];
            Stperiodicidad = Convert.ToInt32(projRow["periodicidad"]);
            VlrVpnExtra = Convert.ToDouble(projRow["VlrVpnExtra"]);
            SalAportes = Convert.ToDouble(projRow["SalAportes"]);
            agencia = projRow["Agencia"].ToString();
            CentroCosto = projRow["CentroCosto"].ToString();
            CuotaAdm = Convert.ToDouble(projRow["CuotaAdm"]);
            CuotaSeg = Convert.ToDouble(projRow["CuotaSeg"]);
            CuotaCapital = Convert.ToDouble(projRow["CuotaCapital"]);
            CuotaIcie = Convert.ToDouble(projRow["CuotaIcie"]);
            //CuotaOtros = .Item("CuotaCapital")
            TipoIncie = Convert.ToInt32(projRow["TipoIncie"]);
            TipoCap = Convert.ToInt32(projRow["Tipocap"]);
            TipoAdm = Convert.ToInt32(projRow["Tipoadm"]);
            TipoSeg = Convert.ToInt32(projRow["Tiposeg"]);
            TipoOtro = Convert.ToInt32(projRow["TipoOtro"]);
            foradm = Convert.ToInt32(projRow["foradm"]);
            cptoAdm = Convert.ToInt32(projRow["cptoadm"]);
            CptoSeg = Convert.ToInt32(projRow["cptoSeg"]);
            cptoOtro = Convert.ToInt32(projRow["cptoOtro"]);
            tasaOtro = Convert.ToInt32(projRow["tasaotro"]);
            //periodoGracia = .Item("cptoadm")
            clades = Convert.ToInt32(projRow["clades"]);
            clacuo = Convert.ToInt32(projRow["clacuo"]);
            claint = Convert.ToInt32(projRow["claint"]);
            tasaadm = Convert.ToDecimal(projRow["tasaadm"]);
            tasaseg = Convert.ToDecimal(projRow["tasaSeg"]);
            tasacpt = Convert.ToDecimal(projRow["tasacpt"]);
            cicloLocal = Convert.ToInt32(projRow["ciclo"]);
            Dtf = Convert.ToDecimal(projRow["dtf"]);
            puntos = Convert.ToDecimal(projRow["puntos"]);
            switch (this.Invocado)
            {
                case false:
                    MesesGracias = projRow["MesGracias"].ToString();
                    FormaGracia = projRow["FormaGracias"].ToString();
                    CicloGracias = projRow["CicloGracias"].ToString();
                    break;
                case true:
                    MesesGracias = projRow["PERGRAINI"].ToString();
                    FormaGracia = projRow["PERGRACIA"].ToString();
                    CicloGracias = projRow["CICLO_PERGRACIA"].ToString();
                    break;
            }
            if (!Information.IsNumeric(MesesGracias))
            {
                MesesGracias = "0";
            }
            perGraciaini = Convert.ToInt32(MesesGracias);
            if (!Information.IsNumeric(FormaGracia))
            {
                FormaGracia = "0";
            }
            periodoGracia = Convert.ToInt32(FormaGracia);
            if (!Information.IsNumeric(CicloGracias))
            {
                CicloGracias = "0";
            }
            ciclopergracia = Convert.ToInt32(CicloGracias);

            this.LblNivelEndeudamiento.Text = Math.Round(Convert.ToDouble(this.LblNivelEndeudamiento.Text), 3).ToString();
            this.LblNivelContingencia.Text = Math.Round(Convert.ToDouble(this.LblNivelContingencia.Text), 3).ToString();

            DsSolicitud.Tables["tblsolicitud"].Rows.Add(this.TxtvalorSolicitud.Text, this.txtLincred.Text, Convert.ToDateTime(LblFecSolicitud.Text), this.txtnombreLInea.Text, this.txtTasaInt.Text, this.TxtPlazo.Text, this.TxtCuota.Text,
                this.TxtBaseCupo.Text, this.Dtpfecdsto.Value, this.TxtCupoDisponible.Text, this.TxtCodigoter.Text, this.TxtApellido.Text, this.TxtNombre.Text, this.TxtCedula.Text,
                this.TxtDireccion.Text, this.TxtCiudad.Text, this.txtTelefono.Text, this.CbxestadoCivil.SelectedIndex, this.DtpFecNacimiento.Value,
                this.DtpFecCoop.Value, CasaPropia, TieneVehiculo, this.TxtVehiculo.Text, TxtEmpresa.Text, DtpFechaIngEmpresa.Value, this.cbxTipoContrato.SelectedIndex,
                this.TxtCargo.Text, this.TxtSalario.Text, this.TxtOtrosIngresos.Text, this.TxtDesCajaEmpresa.Text, TxtGastosMes.Text, TxtDisponibleMes.Text, this.CbxClaseGar.SelectedIndex,
                this.TxtAvaluoComercial.Text, this.TxtAvalCatastral.Text, this.txtDescripGara.Text, GarAsefurado, this.DtpFecVenSeguro.Value, this.TxtPorSeg.Text, this.TxtCodeudor1.Text, this.TxtCodeudor2.Text, this.TxtCodeudor3.Text,
                this.TxtCodeudor4.Text, this.TxtSalAportes1.Text, this.TxtSalAportes2.Text, this.TxtSalAportes3.Text, this.TxtSalAportes4.Text, this.TxtSalDeuda1.Text, this.TxtSalDeuda2.Text, this.TxtSalDeuda3.Text, this.TxtSalDeuda4.Text,
                this.TxtDeudaResp1.Text, this.TxtDeudaResp2.Text, this.TxtDeudaResp3.Text, this.TxtDeudaResp4.Text, this.TxtNomConyuge.Text, this.TxtEmpLaboraconyuge.Text, this.TxtDirEmpresaConyuge.Text,
                this.TxtTelConyuge.Text, TrabConyuge, this.TxtPercargo.Text, this.TxtSalConyuge.Text, this.TxtDirEmpresaConyuge.Text, this.TxtCiudadConyuge.Text, VlrVpnExtra, SalAportes, agencia, CentroCosto, CuotaAdm, CuotaSeg,
                CuotaCapital, CuotaIcie, CuotaOtros, TipoIncie, TipoCap, TipoAdm, TipoSeg, TipoOtro, foradm, cptoAdm, CptoSeg, cptoOtro, tasaOtro, periodoGracia, perGraciaini, ciclopergracia, clades, cuexinmes, cuexintant,
                pag1cuo, tippag2, cicloLocal, Stperiodicidad, clacuo, claint, tasaadm, tasaseg, tasacpt, Convert.ToDouble(this.TxtIngVariables.Text), Convert.ToDouble(this.TxtIngArriendos.Text), Convert.ToDouble(this.TxtDeudasTerceros.Text), this.TxtNumCdat.Text,
                this.TxtNit_aseguradora.Text, this.TxtNombre_aseguradora.Text, this.TxtNum_poliza.Text, this.TxtMatricula.Text, this.TxtEmpDescuento.Text, this.TxtNumPagare.Text, this.TxtPensiones.Text, this.TxtDstoPension.Text,
                this.TxtDstoParafiscales.Text, this.TxtDesNomEmpresa.Text, Dtf, puntos, this.TxtGastosPnales.Text, this.txtIngresoConyuge.Text, cappagoPorcentajeLocal, CbxRecDeudas.Text, MesesGracias, FormaGracia,
                Convert.ToDouble(TxtSolActVivienda.Text), Convert.ToDouble(TxtSolActVehiculo.Text), Convert.ToDouble(TxtSolActOtros.Text), Convert.ToDouble(TxtSolActAporte.Text), Convert.ToDouble(TxtActCtaBanco.Text), Convert.ToDouble(TxtActCxC.Text), Convert.ToDouble(TxtSolActAhorros.Text), Convert.ToDouble(TxtSolActTotal.Text),
                Convert.ToDouble(TxtSolPasDeuda.Text), Convert.ToDouble(TxtSolPasOtros.Text), Convert.ToDouble(TxtPasObliBanca.Text), Convert.ToDouble(TxtPasObliHipot.Text), Convert.ToDouble(TxtSolPasTotal.Text), Convert.ToDouble(TxtSolPatrimonio.Text), Convert.ToDouble(TxtSolPasTotPyP.Text),
                Convert.ToDouble(lblCapacidaNomina.Text), Convert.ToDouble(LblPorcNomina.Text), Convert.ToDouble(LblCapPago.Text), Convert.ToDouble(LblPorcCaja.Text), Convert.ToDouble(LblPorcentajePagaduria.Text), Convert.ToDouble(LblNomina.Text), Convert.ToDouble(LblCaja.Text), Convert.ToDouble(LblDescubierto.Text),
                Convert.ToDouble(LblNivelEndeudamiento.Text), Convert.ToDouble(LblNivelContingencia.Text), Convert.ToDouble(lblCapacidadRiesgovlr.Text), Convert.ToDouble(lblCapacDescuento.Text), Convert.ToDouble(lblPorcnDescuento.Text), TipoDscto);

            return DsSolicitud;
        }

        // --- imprimir_Click ---
        private void imprimir_Click(object sender, EventArgs e)
        {
            ImprimeSolicitud(Convert.ToDouble(LblNumSolicitud.Text));
        }

        // --- ImprimeSolicitud ---
        private void ImprimeSolicitud(double NumSolicitud)
        {
            ERP.Core.Creditos.Reportes.clsimpsol mscimpsolObj = new ERP.Core.Creditos.Reportes.clsimpsol();
            mscimpsolObj.ImprimeSolicitud(NumSolicitud, mycon, this, Convert.ToDouble(this.TxtDesCajaEmpresa.Text), Convert.ToDouble(this.TxtGastosMes.Text), this.cbxTipoContrato.SelectedIndex, Convert.ToDouble(this.TxtOtrosIngresos.Text), Convert.ToDouble(this.TxtDisponibleMes.Text));
        }

        // --- TxtCodeudor1_LostFocus ---
        private void TxtCodeudor1_LostFocus(object sender, EventArgs e)
        {
            CargarCodeudor1();
        }

        // --- CrearCodeudor ---
        private bool CrearCodeudor(string codigo)
        {
            //solido.cop_fasoma01 HojaVida = new solido.cop_fasoma01();
            if (MessageBox.Show("Codeudor no existe. Desea Crearlo?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.No)
            {
                return false;
            }
            else
            {
                //HojaVida.codigo.Text = codigo;
                //HojaVida.codigo.Focus();
                //HojaVida.ShowDialog(this);
                return true;
            }
        }

        // --- CargarCodeudor1 ---
        private void CargarCodeudor1()
        {
            double DstoNomina = 0;
            double DstoCaja = 0;
            if (this.TxtCodeudor1.Text.Trim() != "")
            {
                this.TxtCodeudor1.Text = Strings.Replace(this.TxtCodeudor1.Text.Trim(), " ", "");
                string _nomCodeudor1 = this.TxtNomCodeudor1.Text;
                string _salarioCode1 = this.TxtSalarioCode1.Text;
               // ok = msgparcop.BuscaAsociado(this.TxtCodeudor1.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _dummy, ref _dummy, ref _dummy, ref _nomCodeudor1, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _salarioCode1);
                this.TxtNomCodeudor1.Text = _nomCodeudor1;
                this.TxtSalarioCode1.Text = _salarioCode1;
                switch (ok)
                {
                    case true:
                        double _salAportes1 = 0, _salDeuda1 = 0, _deudaResp1 = 0, _numResp1 = 0;
                        msgliqcre.SaldoCodeudores(this.TxtCodeudor1.Text, Convert.ToDateTime(this.LblFecSolicitud.Text), mycon, ref _salAportes1, ref _salDeuda1, ref _deudaResp1, ref _numResp1);
                        this.TxtSalAportes1.Text = _salAportes1.ToString();
                        this.TxtSalDeuda1.Text = _salDeuda1.ToString();
                        this.TxtDeudaResp1.Text = _deudaResp1.ToString();
                        this.TxtNumResp1.Text = _numResp1.ToString();
                        this.msgliqcre.CalculaDeducciones(this.TxtCodeudor1.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, ref DstoNomina, ref DstoCaja);
                        this.TxtDstoEmpCode1.Text = DstoNomina.ToString();
                        this.TxtDstoEmpCajaCode1.Text = DstoCaja.ToString();

                        FormateaCamposCodeudor(this.TxtSalAportes1, this.TxtSalDeuda1, this.TxtDeudaResp1, this.TxtSalarioCode1);
                        if (this.TxtNumResp1.Text.Trim() == "")
                        {
                            TxtNumResp1.Text = "0";
                        }
                        else if (this.controlCodedudores > 0 && Convert.ToInt32(TxtNumResp1.Text) >= this.controlCodedudores)
                        {
                            MessageBox.Show("Esta persona no puede ser Codeudor porque ya paso el limite maximo de obligaciones a las cuales les puede servir de codeudor", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;
                    case false:
                        ok = CrearCodeudor(this.TxtCodeudor1.Text);
                        switch (ok)
                        {
                            case false:
                                this.TxtNomCodeudor1.Text = "Codeudor no existe, por favor crearlo";
                                break;
                            case true:
                                this.TxtCodeudor1.Focus();
                                break;
                        }
                        break;
                }
            }
        }

        // dummy field for ref params we don't care about
        private string _dummy = "";

        // --- FormateaCamposCodeudor ---
        private void FormateaCamposCodeudor(TextBox TxtAportes, TextBox txtDeuda, TextBox TxtDeudaResp, TextBox TxtSalario)
        {
            TxtAportes.Text = Convert.ToDouble(TxtAportes.Text).ToString("N0");
            txtDeuda.Text = Convert.ToDouble(txtDeuda.Text).ToString("N0");
            TxtDeudaResp.Text = Convert.ToDouble(TxtDeudaResp.Text).ToString("N0");
            TxtSalario.Text = Convert.ToDouble(TxtSalario.Text).ToString("N0");
        }

        // --- TxtCodeudor1_TextChanged ---
        private void TxtCodeudor1_TextChanged(object sender, EventArgs e)
        {
        }

        // --- TxtCodeudor2_LostFocus ---
        private void TxtCodeudor2_LostFocus(object sender, EventArgs e)
        {
            CargarCodeudor2();
        }

        // --- CargarCodeudor2 ---
        private void CargarCodeudor2()
        {
            double DstoNomina = 0;
            double DstoCaja = 0;
            if (this.TxtCodeudor2.Text.Trim() != "")
            {
                this.TxtCodeudor2.Text = Strings.Replace(this.TxtCodeudor2.Text.Trim(), " ", "");
                _dummy = "";
                string _nomCodeudor2 = this.TxtNomCodeudor2.Text;
                string _salarioCode2 = this.TxtSalarioCode2.Text;
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor2.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _dummy, ref _dummy, ref _dummy, ref _nomCodeudor2, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _salarioCode2);
                this.TxtNomCodeudor2.Text = _nomCodeudor2;
                this.TxtSalarioCode2.Text = _salarioCode2;
                switch (ok)
                {
                    case true:
                        double _salAportes2 = 0, _salDeuda2 = 0, _deudaResp2 = 0, _numResp2 = 0;
                        msgliqcre.SaldoCodeudores(this.TxtCodeudor2.Text, Convert.ToDateTime(this.LblFecSolicitud.Text), mycon, ref _salAportes2, ref _salDeuda2, ref _deudaResp2, ref _numResp2);
                        this.TxtSalAportes2.Text = _salAportes2.ToString();
                        this.TxtSalDeuda2.Text = _salDeuda2.ToString();
                        this.TxtDeudaResp2.Text = _deudaResp2.ToString();
                        this.TxtNumResp2.Text = _numResp2.ToString();
                        this.msgliqcre.CalculaDeducciones(this.TxtCodeudor2.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, ref DstoNomina, ref DstoCaja);
                        this.TxtDstoEmpCode2.Text = DstoNomina.ToString();
                        this.TxtDstoEmpCajaCode2.Text = DstoCaja.ToString();

                        FormateaCamposCodeudor(this.TxtSalAportes2, this.TxtSalDeuda2, this.TxtDeudaResp2, this.TxtSalarioCode2);
                        if (this.TxtNumResp2.Text.Trim() == "")
                        {
                            TxtNumResp2.Text = "0";
                        }
                        else if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp2.Text) >= this.controlCodedudores)
                        {
                            MessageBox.Show("Esta persona no puede ser Codeudor porque ya paso el limite maximo de obligaciones a las cuales les puede servir de codeudor", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;
                    case false:
                        ok = CrearCodeudor(this.TxtCodeudor2.Text);
                        switch (ok)
                        {
                            case false:
                                this.TxtNomCodeudor2.Text = "Codeudor no existe, por favor crearlo";
                                break;
                            case true:
                                this.TxtCodeudor2.Focus();
                                break;
                        }
                        break;
                }
            }
        }

        // --- TxtCodeudor2_TextChanged ---
        private void TxtCodeudor2_TextChanged(object sender, EventArgs e)
        {
        }

        // --- TxtCodeudor3_LostFocus ---
        private void TxtCodeudor3_LostFocus(object sender, EventArgs e)
        {
            CargarCodeudor3();
        }

        // --- CargarCodeudor3 ---
        private void CargarCodeudor3()
        {
            double DstoNomina = 0;
            double DstoCaja = 0;
            if (this.TxtCodeudor3.Text.Trim() != "")
            {
                this.TxtCodeudor3.Text = Strings.Replace(this.TxtCodeudor3.Text.Trim(), " ", "");
                _dummy = "";
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor3.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _dummy, ref _dummy, ref _dummy, ref this.TxtNomCodeudor3.Text, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref this.TxtSalarioCode3.Text);
                switch (ok)
                {
                    case true:
                        //msgliqcre.SaldoCodeudores(this.TxtCodeudor3.Text, Convert.ToDateTime(this.LblFecSolicitud.Text), mycon, ref this.TxtSalAportes3.Text, ref this.TxtSalDeuda3.Text, ref this.TxtDeudaResp3.Text, ref this.TxtNumResp3.Text);
                        this.msgliqcre.CalculaDeducciones(this.TxtCodeudor3.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, ref DstoNomina, ref DstoCaja);
                        this.TxtDstoEmpCode3.Text = DstoNomina.ToString();
                        this.TxtDstoEmpCajaCode3.Text = DstoCaja.ToString();

                        FormateaCamposCodeudor(this.TxtSalAportes3, this.TxtSalDeuda3, this.TxtDeudaResp3, this.TxtSalarioCode3);
                        if (this.TxtNumResp3.Text.Trim() == "")
                        {
                            TxtNumResp3.Text = "0";
                        }
                        else if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp3.Text) >= this.controlCodedudores)
                        {
                            MessageBox.Show("Esta persona no puede ser Codeudor porque ya paso el limite maximo de obligaciones a las cuales les puede servir de codeudor", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;
                    case false:
                        ok = CrearCodeudor(this.TxtCodeudor3.Text);
                        switch (ok)
                        {
                            case false:
                                this.TxtNomCodeudor3.Text = "Codeudor no existe, por favor crearlo";
                                break;
                            case true:
                                this.TxtCodeudor3.Focus();
                                break;
                        }
                        break;
                }
            }
        }

        // --- TxtCodeudor3_TextChanged ---
        private void TxtCodeudor3_TextChanged(object sender, EventArgs e)
        {
        }

        // --- TxtCodeudor4_LostFocus ---
        private void TxtCodeudor4_LostFocus(object sender, EventArgs e)
        {
            CargarCodeudor4();
        }

        // --- CargarCodeudor4 ---
        private void CargarCodeudor4()
        {
            double DstoNomina = 0;
            double DstoCaja = 0;
            if (this.TxtCodeudor4.Text.Trim() != "")
            {
                this.TxtCodeudor4.Text = Strings.Replace(this.TxtCodeudor4.Text.Trim(), " ", "");
                _dummy = "";
                //ok = msgparcop.BuscaAsociado(this.TxtCodeudor4.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref _dummy, ref _dummy, ref _dummy, ref this.TxtNomCodeudor4.Text, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref _dummy, ref this.TxtSalarioCode4.Text);
                switch (ok)
                {
                    case true:
                        //msgliqcre.SaldoCodeudores(this.TxtCodeudor4.Text, Convert.ToDateTime(this.LblFecSolicitud.Text), mycon, ref this.TxtSalAportes4.Text, ref this.TxtSalDeuda4.Text, ref this.TxtDeudaResp4.Text, ref this.TxtNumResp4.Text);
                        this.msgliqcre.CalculaDeducciones(this.TxtCodeudor4.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, ref DstoNomina, ref DstoCaja);
                        this.TxtDstoEmpCode4.Text = DstoNomina.ToString();
                        this.TxtDstoEmpCajaCode4.Text = DstoCaja.ToString();

                        FormateaCamposCodeudor(this.TxtSalAportes4, this.TxtSalDeuda4, this.TxtDeudaResp4, this.TxtSalarioCode4);
                        if (this.TxtNumResp4.Text.Trim() == "")
                        {
                            TxtNumResp4.Text = "0";
                        }
                        else if (this.controlCodedudores > 0 && Convert.ToInt32(this.TxtNumResp4.Text) >= this.controlCodedudores)
                        {
                            MessageBox.Show("Esta persona no puede ser Codeudor porque ya paso el limite maximo de obligaciones a las cuales les puede servir de codeudor", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        break;
                    case false:
                        ok = CrearCodeudor(this.TxtCodeudor4.Text);
                        switch (ok)
                        {
                            case false:
                                this.TxtNomCodeudor4.Text = "Codeudor no existe, por favor crearlo";
                                break;
                            case true:
                                this.TxtCodeudor4.Focus();
                                break;
                        }
                        break;
                }
            }
        }

        // --- TxtCodeudor4_TextChanged ---
        private void TxtCodeudor4_TextChanged(object sender, EventArgs e)
        {
        }

        // --- cuotas_ext_Click ---
        private void cuotas_ext_Click(object sender, EventArgs e)
        {
            DespliegaCuotasExtras();
        }

        // --- DespliegaCuotasExtras ---
        private void DespliegaCuotasExtras()
        {
            this.msgliqcre.DespliegaCuotasextras(this.DsDatosProyeccion.Tables["tblextras"], this);
        }

        // --- cred_recoge_Click ---
        private void cred_recoge_Click(object sender, EventArgs e)
        {
            //this.msgliqcre.ReCalculaDeduciones(this.LblNumSolicitud.Text, this, this.mycon, true, this.DsDatosProyeccion);
        }

        // --- HelpCodeudor clicks ---
        private void HelpCodeudor1_Click(object sender, EventArgs e)
        {
            //this.TxtCodeudor1.Text = this.msgparcop.HelpAsociado(this.mycon, this);
            this.TxtCodeudor1.Focus();
        }

        private void HelpCodeudor2_Click(object sender, EventArgs e)
        {
            //  this.TxtCodeudor2.Text = this.msgparcop.HelpAsociado(this.mycon, this);
            this.TxtCodeudor2.Focus();
        }

        private void HelpCodeudor3_Click(object sender, EventArgs e)
        {
            //this.TxtCodeudor3.Text = this.msgparcop.HelpAsociado(this.mycon, this);
            this.TxtCodeudor3.Focus();
        }

        private void HelpCodeudor4_Click(object sender, EventArgs e)
        {
            //this.TxtCodeudor4.Text = this.msgparcop.HelpAsociado(this.mycon, this);
            this.TxtCodeudor4.Focus();
        }

        // --- CbxClaseGar_Validated ---
        private void CbxClaseGar_Validated(object sender, EventArgs e)
        {
            this.TxtNumCdat.Text = "0";
            if (this.CbxClaseGar.SelectedIndex == 10)
            {
                this.TxtNumCdat.Enabled = true;
                this.TxtNumCdat.Focus();
            }
            else if (this.CbxClaseGar.SelectedIndex == 1 || this.CbxClaseGar.SelectedIndex == 11 || this.CbxClaseGar.SelectedIndex == 12)
            {
                this.TxtAvaluoComercial.Enabled = false;
                this.TxtAvalCatastral.Enabled = false;
                this.txtDescripGara.Enabled = false;
                this.ChkAsegurado.Enabled = false;
                this.DtpFecVenSeguro.Enabled = false;
                this.TxtPorSeg.Enabled = false;
                this.TxtNumCdat.Enabled = false;
                this.TxtNit_aseguradora.Enabled = false;
                this.TxtNombre_aseguradora.Enabled = false;
                this.TxtMatricula.Enabled = false;
                this.TxtNum_poliza.Enabled = false;
                this.TxtAvaluoComercial.Text = "0";
            }
            else if (this.CbxClaseGar.SelectedIndex != 0)
            {
                this.TxtAvaluoComercial.Enabled = true;
                this.TxtAvalCatastral.Enabled = true;
                this.txtDescripGara.Enabled = true;
                this.ChkAsegurado.Enabled = true;
                //Me.DtpFecVenSeguro.Enabled = True
                //Me.TxtPorSeg.Enabled = True
                this.TxtNumCdat.Enabled = false;
                this.TxtAvaluoComercial.Text = "0";
            }
        }

        // --- TxtNumCdat_LostFocus ---
        private void TxtNumCdat_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtNumCdat.Text) == true)
            {
                this.TxtAvaluoComercial.Enabled = false;
                string _cd1 = "", _cd2 = "", _cd3 = "", _cd4 = "", _cd5 = "", _cd6 = "", _cd7 = "", _cd8 = "";
                string _cd9 = "", _cd10 = "", _cd11 = "", _cd12 = "", _cd13 = "", _cd14 = "", _cd15 = "", _cd16 = "";
                string _cd17 = "", _cd18 = "", _cd19 = "", _cd20 = "", _cd21 = "", _cd22 = "", _cd23 = "", _cd24 = "";
                string _cd25 = "", _cd26 = "", _cd27 = "", _cd28 = "", _cd29 = "", _cd30 = "", _cd31 = "", _cd32 = "";
                string _cd33 = "";
                string avaluoText = this.TxtAvaluoComercial.Text;
                //ok = this.clsmsgcdat.BuscarCdats(this.TxtNumCdat.Text, this.mycon, ref _cd1, ref _cd2, ref _cd3, ref _cd4, ref _cd5, ref _cd6, ref _cd7, ref _cd8,
                //    ref _cd9, ref _cd10, ref _cd11, ref _cd12, ref _cd13, ref _cd14, ref _cd15, ref _cd16,
                //    ref _cd17, ref _cd18, ref _cd19, ref _cd20, ref _cd21, ref _cd22, ref _cd23, ref _cd24,
                //    ref _cd25, ref _cd26, ref _cd27, ref _cd28, ref _cd29, ref _cd30, ref _cd31, ref _cd32,
                //    ref _cd33, ref avaluoText);
                this.TxtAvaluoComercial.Text = avaluoText;
                switch (ok)
                {
                    case true:
                        this.TxtAvaluoComercial.Text = Convert.ToDouble(this.TxtAvaluoComercial.Text).ToString("N0");
                        this.TxtAvaluoComercial.Focus();
                        break;
                    case false:
                        this.TxtAvaluoComercial.Text = "0";
                        break;
                }
            }
        }

        // --- CbxClaseGar_SelectedIndexChanged ---
        private void CbxClaseGar_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        // --- ChkAsegurado_CheckedChanged ---
        private void ChkAsegurado_CheckedChanged(object sender, EventArgs e)
        {
            if (this.ChkAsegurado.Checked == true)
            {
                this.TxtNit_aseguradora.Enabled = true;
                this.TxtNombre_aseguradora.Enabled = true;
                this.TxtMatricula.Enabled = true;
                this.TxtNum_poliza.Enabled = true;
                this.DtpFecVenSeguro.Enabled = true;
                this.TxtPorSeg.Enabled = true;
            }
            else
            {
                this.TxtNit_aseguradora.Enabled = false;
                this.TxtNombre_aseguradora.Enabled = false;
                this.TxtMatricula.Enabled = false;
                this.TxtNum_poliza.Enabled = false;
                this.DtpFecVenSeguro.Enabled = false;
                this.TxtPorSeg.Enabled = false;
                this.TxtPorSeg.Text = "0";
            }
        }

        // --- TxtCodigoter_TextChanged ---
        private void TxtCodigoter_TextChanged(object sender, EventArgs e)
        {
        }

        // --- BtnRecDeudas_Click ---
        private void BtnRecDeudas_Click(object sender, EventArgs e)
        {
            RecogeCreditos();
        }

        // --- RecogeCreditos ---
        private void RecogeCreditos()
        {
            DateTime fechasol = new DateTime(Convert.ToInt32(Strings.Mid(Periodo.ToString(), 1, 4)), Convert.ToInt32(Strings.Mid(Periodo.ToString(), 5)), DateTime.Now.Day);
            double ValorCredito = 0;
            DataSet dsdatades = new DataSet();

            switch (this.estado)
            {
                case "A":
                case "D":
                case "G":
                    ValorCredito = Convert.ToDouble(this.TxtValAprobado.Text);
                    break;
                default:
                    ValorCredito = Convert.ToDouble(this.TxtvalorSolicitud.Text);
                    break;
            }
            //msgcop.CargaRecogeDeudas(this.LblNumSolicitud.Text, this.TxtCodigoter.Text, this.txtLincred.Text, ValorCredito, fechasol, this, mycon);
            //this.msgliqcre.BuscarDeducciones(this.LblNumSolicitud.Text, ref dsdatades, mycon);
            this.DsDatosProyeccion.Tables.Remove("tbldeducciones");
            this.DsDatosProyeccion.Tables.Add(dsdatades.Tables["tbldeducciones"].Copy());
        }

        // --- HelpEmpDescuento_Click ---
        private void HelpEmpDescuento_Click(object sender, EventArgs e)
        {
            //this.TxtEmpDescuento.Text = this.msgparcop.HelpEmpresa(this.mycon, this);
            this.TxtEmpDescuento.Focus();
        }

        // --- BtnSalir_Click ---
        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.PnlCodeudores.Visible = false;
        }

        // --- BtnGrabar_Click ---
        private void BtnGrabar_Click(object sender, EventArgs e)
        {
            CargaDatosCodeudores();
        }

        // --- CargaDatosCodeudores ---
        private void CargaDatosCodeudores()
        {
            try
            {
                this.DsDataCodeudores.Tables["TblCodeudores"].Rows.Clear();
            }
            catch (Exception)
            {
            }
            DataRowCollection rows = this.DsDataCodeudores.Tables["TblCodeudores"].Rows;
            if (this.TxtCodeu1.Text.Trim() != "")
            {
                rows.Add(this.TxtCodeu1.Text, Convert.ToDouble(this.TxtSalarioCode1.Text), Convert.ToDouble(this.TxtOtroIngCode1.Text), Convert.ToDouble(this.TxtIngArrCode1.Text), Convert.ToDouble(this.TxtIngVarCode1.Text), Convert.ToDouble(this.TxtDstoEmpCode1.Text), Convert.ToDouble(this.TxtDeuTerCode1.Text), Convert.ToDouble(this.TxtOtroDstocode1.Text), Convert.ToDouble(this.LblDispCode1.Text), Convert.ToDouble(this.TxtPensionesCod1.Text), Convert.ToDouble(this.TxtDstoPensionCod1.Text), Convert.ToDouble(this.TxtDstoParaFisCod1.Text), chkGastoperCod1.Checked ? "Y" : "N", Convert.ToDouble(txtGastoperCod1.Text), Convert.ToDouble(TxtDstoEmpCajaCode1.Text));
            }
            if (this.TxtCodeu2.Text.Trim() != "")
            {
                rows.Add(this.TxtCodeu2.Text, Convert.ToDouble(this.TxtSalarioCode2.Text), Convert.ToDouble(this.TxtOtroIngCode2.Text), Convert.ToDouble(this.TxtIngArrCode2.Text), Convert.ToDouble(this.TxtIngVarCode2.Text), Convert.ToDouble(this.TxtDstoEmpCode2.Text), Convert.ToDouble(this.TxtDeuTerCode2.Text), Convert.ToDouble(this.TxtOtroDstocode2.Text), Convert.ToDouble(this.LblDispCode2.Text), Convert.ToDouble(this.TxtPensionesCod2.Text), Convert.ToDouble(this.TxtDstoPensionCod2.Text), Convert.ToDouble(this.TxtDstoParaFisCod2.Text), chkGastoperCod2.Checked ? "Y" : "N", Convert.ToDouble(txtGastoperCod2.Text), Convert.ToDouble(TxtDstoEmpCajaCode2.Text));
            }
            if (this.TxtCodeu3.Text.Trim() != "")
            {
                rows.Add(this.TxtCodeu3.Text, Convert.ToDouble(this.TxtSalarioCode3.Text), Convert.ToDouble(this.TxtOtroIngCode3.Text), Convert.ToDouble(this.TxtIngArrCode3.Text), Convert.ToDouble(this.TxtIngVarCode3.Text), Convert.ToDouble(this.TxtDstoEmpCode3.Text), Convert.ToDouble(this.TxtDeuTerCode3.Text), Convert.ToDouble(this.TxtOtroDstocode3.Text), Convert.ToDouble(this.LblDispCode3.Text), Convert.ToDouble(this.TxtPensionesCod3.Text), Convert.ToDouble(this.TxtDstoPensionCod3.Text), Convert.ToDouble(this.TxtDstoParaFisCod3.Text), chkGastoperCod3.Checked ? "Y" : "N", Convert.ToDouble(txtGastoperCod3.Text), Convert.ToDouble(TxtDstoEmpCajaCode3.Text));
            }
            if (this.TxtCodeu4.Text.Trim() != "")
            {
                rows.Add(this.TxtCodeu4.Text, Convert.ToDouble(this.TxtSalarioCode4.Text), Convert.ToDouble(this.TxtOtroIngCode4.Text), Convert.ToDouble(this.TxtIngArrCode4.Text), Convert.ToDouble(this.TxtIngVarCode4.Text), Convert.ToDouble(this.TxtDstoEmpCode4.Text), Convert.ToDouble(this.TxtDeuTerCode4.Text), Convert.ToDouble(this.TxtOtroDstocode4.Text), Convert.ToDouble(this.LblDispCode4.Text), Convert.ToDouble(this.TxtPensionesCod4.Text), Convert.ToDouble(this.TxtDstoPensionCod4.Text), Convert.ToDouble(this.TxtDstoParaFisCod4.Text), chkGastoperCod4.Checked ? "Y" : "N", Convert.ToDouble(txtGastoperCod4.Text), Convert.ToDouble(TxtDstoEmpCajaCode4.Text));
            }
        }

        // --- CalculaDisponibleMesCodeudor ---
        private void CalculaDisponibleMesCodeudor(TextBox Salario, TextBox OtroIng, TextBox IngArriendosCtl, TextBox IngVariables, TextBox DesEmpresa,
            TextBox DeudasTercerosCtl, TextBox OtrosDstos, Label DisponibleMes, TextBox IngPensiones, TextBox DstoPensiones, TextBox DstoParafiscales,
            Label TotalIngreso, Label TotalEgresos, CheckBox chkGastoper, TextBox GastoperCod, TextBox DesEmpresaCaja)
        {
            double gastospersonales = 0;

            Salario.Text = Convert.ToDouble(Salario.Text).ToString("N0");
            OtroIng.Text = Convert.ToDouble(OtroIng.Text).ToString("N0");
            IngArriendosCtl.Text = Convert.ToDouble(IngArriendosCtl.Text).ToString("N0");
            IngVariables.Text = Convert.ToDouble(IngVariables.Text).ToString("N0");
            IngPensiones.Text = Convert.ToDouble(IngPensiones.Text).ToString("N0");
            DesEmpresa.Text = Convert.ToDouble(DesEmpresa.Text).ToString("N0");
            DeudasTercerosCtl.Text = Convert.ToDouble(DeudasTercerosCtl.Text).ToString("N0");
            OtrosDstos.Text = Convert.ToDouble(OtrosDstos.Text).ToString("N0");
            DstoPensiones.Text = Convert.ToDouble(DstoPensiones.Text).ToString("N0");
            DstoParafiscales.Text = Convert.ToDouble(DstoParafiscales.Text).ToString("N0");
            DesEmpresaCaja.Text = Convert.ToDouble(DesEmpresaCaja.Text).ToString("N0");

            switch (chkGastoper.Checked)
            {
                case true:
                    gastospersonales = Information.IsNumeric(GastoperCod.Text) == false ? 0 : (Convert.ToDouble(Salario.Text) * (Convert.ToDouble(GastoperCod.Text) / 100));
                    break;
                case false:
                    gastospersonales = Information.IsNumeric(GastoperCod.Text) == false ? 0 : Convert.ToDouble(GastoperCod.Text);
                    break;
            }
            GastoperCod.Text = Convert.ToDouble(GastoperCod.Text).ToString("N0");

            TotalIngreso.Text = (Convert.ToDouble(Salario.Text) + Convert.ToDouble(OtroIng.Text) + Convert.ToDouble(IngArriendosCtl.Text) + Convert.ToDouble(IngVariables.Text) + Convert.ToDouble(IngPensiones.Text)).ToString("N0");
            TotalEgresos.Text = ((Convert.ToDouble(DesEmpresa.Text) + Convert.ToDouble(DesEmpresaCaja.Text) + Convert.ToDouble(DeudasTercerosCtl.Text) + Convert.ToDouble(OtrosDstos.Text) + Convert.ToDouble(DstoPensiones.Text) + Convert.ToDouble(DstoParafiscales.Text)) + gastospersonales).ToString("N0");
            DisponibleMes.Text = (Convert.ToDouble(Salario.Text) + Convert.ToDouble(OtroIng.Text) + Convert.ToDouble(IngArriendosCtl.Text) + Convert.ToDouble(IngVariables.Text) + Convert.ToDouble(IngPensiones.Text) - Convert.ToDouble(DesEmpresa.Text) - Convert.ToDouble(DesEmpresaCaja.Text) - Convert.ToDouble(DeudasTercerosCtl.Text) - Convert.ToDouble(OtrosDstos.Text) - Convert.ToDouble(DstoPensiones.Text) - Convert.ToDouble(DstoParafiscales.Text) - gastospersonales).ToString("N0");
        }
    }
}
