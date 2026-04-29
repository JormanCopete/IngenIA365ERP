using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using ERP.Core.Compartido.Utilidades;
using ERP.Core.Compartido.Reportes;
using ERP.Core.Compartido.Controles;
using ERP.Core.CarteraFinanciera.Services.Cartera;
using ERP.Core.CarteraFinanciera.Models;
using ERP.Core.Compartido.Configuracion;
using ERP.Core.Compartido.Datos;

namespace ERP.Core.Creditos.Reportes
{
    public class clsimpsol
    {
        private ClsConect MyOdbcConet = new ClsConect();
        private ClsConect.odbcConect varini = new ClsConect.odbcConect();
        private ParamCop msgparcop = new ParamCop();
        private ParamSys msgsys = new ParamSys();
        private string stmysql = null;

        public clsimpsol()
        {
            MyOdbcConet.MyOdbcConect(ref varini);
        }

        ~clsimpsol()
        {
        }

        public void ImprimeSolicitud(double Idsolicitud, OdbcConnection myconnect, Form myforma, double DescMenEmpresa = 0, double GastosMes = 0, int tipoContrato = 0, double OtrosIngresos = 0, double Disponible = 0)
        {
            stmysql = "select codigoter,vlr_solicitud,cuota,cupo_disponible,cupo_disponible,clacuo,lincred,tasa_int,clasei,plazo,clades,fecdesc,periodd,tipo_garantia, avaluo_ccial,avaluo_catastro,descripcion,asegurado,fecven_seguro,por_seguro,codeudor1,codeudor2,codeudor3,codeudor4,fecha_soli, " +
                " conyuge, conyu_labora, TEL_CONYUGE,EMPRESA_LABORA,PER_ACARGO_CONYU,DIR_EMP_CONYU,CIUDAD_EMP_CONYU,SALARIO_ME,vehiculo,casa_propia,tiene_vehiculo from cop_solcre where numero = " + Idsolicitud;

            DataSet myRead = new DataSet();
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "Imprime Solicitud", ref myRead, "TblImpSolicitud");

            if (myRead.Tables["TblImpSolicitud"].Rows.Count > 0)
            {
                DataRow row = myRead.Tables["TblImpSolicitud"].Rows[0];
                ImprimeFormato(Idsolicitud, row["codigoter"].ToString(), Convert.ToDouble(row["vlr_solicitud"]), Convert.ToDouble(row["cuota"]), Convert.ToDouble(row["cupo_disponible"]), Convert.ToInt32(row["clacuo"]), Convert.ToInt32(row["lincred"]), 0, 0, Convert.ToDecimal(row["tasa_int"]), Convert.ToInt32(row["clasei"]), Convert.ToInt32(row["plazo"]), Convert.ToInt32(row["clades"]), Convert.ToDateTime(row["fecdesc"]), Convert.ToInt32(row["periodd"]), Convert.ToDouble(row["cupo_disponible"]), Convert.ToInt32(row["tipo_garantia"]),
                    Convert.ToDouble(row["avaluo_ccial"]), Convert.ToDouble(row["avaluo_catastro"]), row["descripcion"].ToString(), row["asegurado"].ToString(), Convert.ToDateTime(row["fecven_seguro"]), Convert.ToDecimal(row["por_seguro"]), row["codeudor1"].ToString(), row["codeudor2"].ToString(), row["codeudor3"].ToString(), row["codeudor4"].ToString(), tipoContrato, DescMenEmpresa, GastosMes, OtrosIngresos, Disponible, Convert.ToDateTime(row["fecha_soli"]),
                    row["conyuge"].ToString(), row["tel_conyuge"].ToString(), row["conyu_labora"].ToString(), row["EMPRESA_LABORA"].ToString(), row["PER_ACARGO_CONYU"].ToString(), row["DIR_EMP_CONYU"].ToString(), row["CIUDAD_EMP_CONYU"].ToString(), row["SALARIO_ME"].ToString(), row["vehiculo"].ToString(), row["casa_propia"].ToString(), row["tiene_vehiculo"].ToString(), myforma, myconnect);
            }
        }

        public void ImprimePlanpagosDirecto(DataSet dsdataset, Form myforma, double NumSolicitud = 0)
        {
            reporte ImpProyeccion = new reporte("cop_fprocre01", false);
            // imprimir IMP = new imprimir(); // ERROR: CS0246
            string sql;
            string COMPRONTE = "", Pagare = "", num_ob = "";
            double NUMERO_DOMTO = 0;
            OdbcConnection Myconodbc = new OdbcConnection(varini.pstMyconec);
            Myconodbc.Open();

            if (NumSolicitud != 0)
            {
                sql = "SELECT COMPRONTE as campo1,NUMERO_DOMTO as campo2,mae.filler1 as campo3, mov.numero as campo4 " +
                        " FROM cop_movimto mov inner join  cop_maecar mae ON mae.codigoter = mov.codigoter " +
                        " and mae.lincred = mov.lincred and mae.numero = mov.numero WHERE mov.idsolcre = " + NumSolicitud;
                string sNUMERO_DOMTO = NUMERO_DOMTO.ToString();
                MyOdbcConet.ExecuteQueryconec(sql, Myconodbc, "ImprimePlanpagosDirecto", ref COMPRONTE, ref sNUMERO_DOMTO, ref Pagare, ref num_ob);
                double.TryParse(sNUMERO_DOMTO, out NUMERO_DOMTO);
            }

            ImpProyeccion.SetDataSource(dsdataset);
            // ImpProyeccion.Subreports[0].SetDataSource(dsdataset); // ERROR: CS1061
            try
            {
                ImpProyeccion.SetParameterValue("COMPRONTE", COMPRONTE);
                ImpProyeccion.SetParameterValue("NUMERO_DOMTO", NUMERO_DOMTO);
            }
            catch (Exception) { }
            try
            {
                ImpProyeccion.SetParameterValue("Pagare", Pagare);
            }
            catch (Exception) { }
            try
            {
                ImpProyeccion.SetParameterValue("num_ob", num_ob);
            }
            catch (Exception) { }
            try
            {
                ImpProyeccion.SetParameterValue("Empresa", " ");

                string empresa = "", Nempresa = "";

                empresa = dsdataset.Tables["TbldatosCredito"].Rows[0]["Empresa"].ToString();

                sql = "SELECT nombre as campo1 FROM cop_empresa13 WHERE codigo_empresa = '" + empresa + "'";
                MyOdbcConet.ExecuteQueryconec(sql, Myconodbc, "ImprimePlanpagos", ref Nempresa);

                ImpProyeccion.SetParameterValue("Empresa", empresa);
                ImpProyeccion.SetParameterValue("NomEmpresa", Nempresa);
            }
            catch (Exception) { }

            Myconodbc.Close();
            Myconodbc.Dispose();

            // IMP.Visible = false; // ERROR: CS0103
            // IMP.CrystalReportViewer1.ReportSource = ImpProyeccion; // ERROR: CS0103
            // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS0103
        }

        private void ImprimeFormato(double Idsolicitud, string Codigoter, double VlrSolicitud, double Cuota, double Vlrcupo, int ClaseCuota, int lincred,
            double TotDescEmp, double VlrAtrasado, decimal Tasai, int ClaseInt, int plazo, int Clades, DateTime Fecdsto, int periodicidad, double CupoDisponible, int ClaseGarantia,
            double AvaluoComercial, double AvaluoCatastral, string DetalleGarantia, string Asegurado, DateTime FecVenSeg, decimal PorSeguro, string Codeudor1, string Codeudor2, string Codeudor3, string Codeudor4,
            int TipoContrato, double TotalDescEmpresa, double GastosMes, double OtrosIngresos, double Disponible, DateTime FecSolicitud, string NombreConyuge, string TelConyuge, string TrabajaConyuge,
            string EmpresaConyuge, string PersCargoConyuge, string DirConyuge, string CiudadConyuge, string SalmesConyuge, string vehiculo, string CasaPropia, string TieneVehiculo, Form Myforma, OdbcConnection Myconnect)
        {
            reporte report = new reporte("cop_repor_impri");
            string NombreCompania = " ", Nombrelinea = " ", periodd = " ";
            string Apellido = " ", Nombre = " ", Cedula = " ", Direccion = " ", Ciudad = " ", NomCiudad = " ", Telefono = " ";
            int EstadoCivil = 0;
            DateTime FecNacimiento = DateTime.MinValue, FecIngreso = DateTime.MinValue;
            string EmpresaLaboral = " ", NomEmpLabora = " ", Cargo = " ", NomCargo = " ";
            DateTime FecIngEmp = DateTime.MinValue;
            double Salario = 0;
            string NombreCodeudor1 = "", NombreCodeudor2 = " ", NombreCodeudor3 = " ", NombreCodeudor4 = " ";
            double SaldoAporCod1 = 0, SaldoAporCod2 = 0, SaldoAporCod3 = 0;
            double saldodeudaCod1 = 0, saldodeudaCod2 = 0, saldodeudaCod3 = 0, SaldoMoraCod1 = 0, SaldoMoraCod2 = 0, SaldoMoraCod3 = 0;
            string CedulaCodeu1 = " ", CedulaCodeu2 = " ", CedulaCodeu3 = " ", CedulaCodeu4 = " ";
            DataSet DatSetextras = new DataSet();
            int I = 0;
            string k = "";
            int Numextras = 0;
            string NomGarantia = " ";
            string NomInt = " ", DescEstadoCivil = " ", Clacuo = " ", DescContrato = " ";

            // imprimir EditImpresion = new imprimir(); // ERROR: CS0246

            string _codigoter = Codigoter;
            string _apellidoNombre = "", _nit = "", _agencia = "", _digche = "", _tipoDoc = "";
            string _tel2 = "", _fax = "", _movil = "", _email = "";
            string _cencosto = "", _clase = "", _seguroRiesgo = "";
            string _cuentaBanco = "", _codBanco = "", _tipoCuenta = "", _sexo = "";
            string _estadoCivil = "", _tipoSalario = "", _cesantiasAcu = "", _tasaAportes = "";
            string _seccion = "", _codigoInterno = "", _dirEnvio = "", _envioCorre = "", _tipoVivienda = "";
            bool _tieneVehiculo = false;
            string _ciudadEnvio = "", _zona = "", _diaCorte = "", _estado = "", _capadeuda = "", _otroingreso = "";
            string _fechaRetiro = "", _saldoAportes = "", _saldoDeuda = "", _diasMora = "";
            string _saldoMora = "", _fecUltCierre = "", _fecUltPago = "", _califiCategoria = "";
            string _fecultcredito = "", _fecUltmora = "", _peridoDescto = "", _clades = "";
            string _conyuge = "", _conycedu = "", _conydirec = "", _conyempr = "";
            string _conytelef = "", _conyciud = "", _conycargo = "", _conysalar = "";
            string _referido = "", _asesor = "", _expedida = "", _naturalJuridico = "";
            string _escalafon = "", _tipoContrato = "", _venceContrato = "", _activos = "";
            string _ciudadCuenta = "", _motivoRetiro = "", _estadoAnterior = "", _fechaReingreso = "";
            string _calman = "", _fecExpedicion = "", _codCIU = "", _desOtroIng = "";
            string _estrato = "", _fondoCesantia = "", _recibeFactura = "", _tipoPago = "";
            string _idtipoZona = "", _idZona = "", _idComite = "", _fechaModificacion = "";
            string _cabezaFamilia = "", _jornadaLaboral = "", _autorizacion = "";
            string _conyfecnacem = "", _conytiponit = "", _conyexpedida = "", _conyfecexpedicion = "";
            string _conysexo = "", _conyfax = "", _conyenviodir = "", _conyendircor = "";
            string _conyempresa = "", _conyagencia = "", _conyseccion = "", _conyfecingreemp = "";
            string _conynivacade = "", _conytiposalario = "", _conycesantias = "", _conyotroing = "";
            string _profesion = "", _conydesotroing = "", _conyprofe = "", _empleado = "";
            string _admrecupub = "", _pagareunico = "", _acierta = "";
            string _saldodeudaexterna = "", _cuotadeudaexterna = "", _scoreCifin = "";
            string _califidatacredito = "", _salMorDatacredito = "", _clasecupoPos = "";
            double _valorClaseCupo = 0;
            string _cpAdmon = "", _cpAptos = "", _cpLocal = "", _cpComision = "";
            string _cenutilidad = "", _pignora_aport = "", _exoneradoSipa = "";
            DateTime _fechaexonerado = new DateTime(1950, 1, 1);
            string _usuariosipla = "", _ingVariables = "", _ingArriendos = "", _ingPension = "";
            string _deudasTerceros = "", _gastoFijoMes = "", _dstoPension = "";
            string _cappagoPorcentaje = "", _dstoGastosPerso = "";
            int _tipoCorreo = 0, _tipoVehiculo2 = 0;

            string _salarioBasico = Salario.ToString();
            string _fechaIngreso = FecIngreso == DateTime.MinValue ? "" : FecIngreso.ToString();
            string _fecIngEmp = FecIngEmp == DateTime.MinValue ? "" : FecIngEmp.ToString();
            string _fechaNace = FecNacimiento == DateTime.MinValue ? "" : FecNacimiento.ToString();
            string _nivelAcademico = "";

            msgparcop.BuscaAsociado(ref _codigoter, Myconnect, ParamCop.Navega.Ninguno,
                ref Nombre, ref Apellido,
                ref _nit, ref _apellidoNombre,
                ref _agencia, ref EmpresaLaboral,
                ref Direccion, ref _digche,
                ref _tipoDoc, ref Telefono,
                ref _tel2, ref _fax,
                ref _movil, ref _email,
                ref Ciudad, ref _cencosto,
                ref _clase, ref _seguroRiesgo,
                ref _cuentaBanco, ref _codBanco,
                ref _tipoCuenta, ref _sexo,
                ref _estadoCivil, ref _fechaIngreso,
                ref _fecIngEmp, ref EmpresaLaboral,
                ref _tipoSalario, ref _salarioBasico,
                ref _cesantiasAcu, ref _tasaAportes,
                ref _seccion, ref _codigoInterno,
                ref _dirEnvio, ref _envioCorre,
                ref _tipoVivienda, ref _tieneVehiculo,
                ref _ciudadEnvio, ref _zona,
                ref _diaCorte, ref _estado,
                ref _profesion, ref Cargo,
                ref _capadeuda, ref _otroingreso,
                ref _fechaRetiro, ref _saldoAportes,
                ref _saldoDeuda, ref _diasMora,
                ref _saldoMora, ref _fecUltCierre,
                ref _fecUltPago, ref _califiCategoria,
                ref _fecultcredito, ref _fecUltmora,
                ref _peridoDescto, ref _clades,
                ref _conyuge, ref _conycedu,
                ref _conydirec, ref _conyempr,
                ref _conytelef, ref _conyciud,
                ref _conycargo, ref _conysalar,
                ref _referido, ref _asesor,
                ref _fechaNace, ref _nivelAcademico,
                ref _expedida, ref _naturalJuridico,
                ref _escalafon, ref _tipoContrato,
                ref _venceContrato, ref _activos,
                ref _ciudadCuenta, ref _motivoRetiro,
                ref _estadoAnterior, ref _fechaReingreso,
                ref _calman, ref _fecExpedicion,
                ref _tipoCorreo, ref _tipoVehiculo2,
                ref _codCIU, ref _desOtroIng,
                ref _estrato, ref _fondoCesantia,
                ref _recibeFactura, ref _tipoPago,
                ref _idtipoZona, ref _idZona,
                ref _idComite, ref _fechaModificacion,
                ref _cabezaFamilia, ref _jornadaLaboral,
                ref _autorizacion, ref _conyfecnacem,
                ref _conytiponit, ref _conyexpedida,
                ref _conyfecexpedicion, ref _conysexo,
                ref _conyfax, ref _conyenviodir,
                ref _conyendircor, ref _conyempresa,
                ref _conyagencia, ref _conyseccion,
                ref _conyfecingreemp, ref _conynivacade,
                ref _conytiposalario, ref _conycesantias,
                ref _conyotroing, ref _conydesotroing,
                ref _conyprofe, ref _empleado,
                ref _admrecupub, ref _pagareunico,
                ref _acierta, ref _saldodeudaexterna,
                ref _cuotadeudaexterna, ref _scoreCifin,
                ref _califidatacredito, ref _salMorDatacredito,
                ref _clasecupoPos, ref _valorClaseCupo,
                ref _cpAdmon, ref _cpAptos,
                ref _cpLocal, ref _cpComision,
                ref _cenutilidad, ref _pignora_aport,
                ref _exoneradoSipa, ref _fechaexonerado,
                ref _usuariosipla,
                ref _ingVariables, ref _ingArriendos, ref _ingPension,
                ref _deudasTerceros, ref _gastoFijoMes, ref _dstoPension,
                ref _cappagoPorcentaje, ref _dstoGastosPerso);

            double.TryParse(_salarioBasico, out Salario);
            DateTime.TryParse(_fechaIngreso, out FecIngreso);
            DateTime.TryParse(_fecIngEmp, out FecIngEmp);
            DateTime.TryParse(_fechaNace, out FecNacimiento);
            int.TryParse(_estadoCivil, out EstadoCivil);
            msgparcop.BuscaCiudad(Ciudad, Myconnect, ParamCop.Navega.Ninguno, ref NomCiudad);
            msgparcop.BuscaEmpresa(ref EmpresaLaboral, Myconnect, ref NomEmpLabora);
            msgparcop.BuscaCargos(ref Cargo, Myconnect, ParamCop.Navega.Ninguno, ref NomCargo);
            msgparcop.BuscarAsociado(Codeudor1, Myconnect, ref NombreCodeudor1); CedulaCodeu1 = Codeudor1;
            msgparcop.BuscarAsociado(Codeudor2, Myconnect, ref NombreCodeudor2); CedulaCodeu2 = Codeudor2;
            msgparcop.BuscarAsociado(Codeudor3, Myconnect, ref NombreCodeudor3); CedulaCodeu3 = Codeudor3;
            msgparcop.BuscarAsociado(Codeudor4, Myconnect, ref NombreCodeudor4); CedulaCodeu4 = Codeudor4;
            msgparcop.BuscaLinea(ref lincred, Myconnect, ref Nombrelinea);

            string _cuentaUtilidad = "", _cptoCap = "", _calculaSaldo = "", _cptoAfavor = "";
            int _tipoLiq = 0, _diasGracia = 0, _baseLiq = 0, _ctrlConse = 0, _opRecDeuda = 0, _numPagare = 0, _convEnpacto = 0;
            decimal _tasaMora = 0m, _tasaUsura = 0m;
            double _conseCreditos = 0, _conseCdat = 0, _consedep = 0, _vlrConsulta = 0;
            double _limDiario = 0, _limMes = 0, _valorRetenaux = 0, _porcenRetenaux = 0;
            string _cptoRevapo = "", _nitCia = "", _direccionCia = "", _nomres = "";
            string _cptoServicios = "", _cptoExt = "", _cptoAho = "", _cptoApo = "", _cptoRetFte = "";
            string _undred = "", _cptoCdats = "", _cptoIntCdats = "";
            string _telefoniaCia = "", _cpto4Mil = "", _cptoIntAhorro = "", _cptoAnticipo = "";
            string _cpteAnticipo = "", _ciudadCia = "", _genCobroConsulta = "", _cpteConsulta = "";
            string _nombreResumido = "", _cptoIntAnt = "", _cptoint = "", _cptoMor = "";
            string _manEstudio = "", _cobracodeudor = "", _porcEndeudamiento = "", _manCaptaciones = "";
            string _serverSmtp = "", _passwordEnvio = "", _correoEnvio = "", _tipoNomina = "";
            string _cptoIntAnticipados = "", _depto = "", _jefeCartera = "", _cpto4MilCheque = "", _cpteFavor = "";
            char _retenaux = ' ', _conciliabanca = ' ';
            string _cuentaRetenaux = "", _consecFactura = "", _numcodecredit = "", _modifCuota = "";
            string _pagareNotas = "", _formaPagare = "", _controlaConseDep = "", _paramcausacion = "";
            string _disableTasaICdat = "", _foraplCaja = "", _feec = "";

            msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect,
                ref _cuentaUtilidad, ref _cptoCap, ref _calculaSaldo, ref _cptoAfavor,
                ref _tipoLiq, ref _tasaMora, ref _diasGracia, ref _tasaUsura,
                ref _baseLiq, ref _ctrlConse, ref _conseCreditos, ref _cptoRevapo,
                ref _nitCia, ref _direccionCia, ref _nomres, ref _cptoServicios,
                ref _cptoExt, ref _cptoAho, ref _cptoApo, ref _cptoRetFte,
                ref _undred, ref _cptoCdats, ref _cptoIntCdats, ref NombreCompania,
                ref _telefoniaCia, ref _cpto4Mil, ref _cptoIntAhorro, ref _cptoAnticipo,
                ref _conseCdat, ref _consedep, ref _opRecDeuda, ref _cpteAnticipo,
                ref _ciudadCia, ref _genCobroConsulta, ref _vlrConsulta, ref _cpteConsulta,
                ref _nombreResumido, ref _cptoIntAnt, ref _cptoint, ref _cptoMor,
                ref _limDiario, ref _limMes, ref _manEstudio, ref _cobracodeudor,
                ref _porcEndeudamiento, ref _manCaptaciones, ref _serverSmtp, ref _passwordEnvio,
                ref _correoEnvio, ref _tipoNomina, ref _cptoIntAnticipados, ref _depto,
                ref _jefeCartera, ref _cpto4MilCheque, ref _cpteFavor, ref _retenaux,
                ref _valorRetenaux, ref _porcenRetenaux, ref _cuentaRetenaux, ref _consecFactura,
                ref _numcodecredit, ref _modifCuota, ref _conciliabanca, ref _numPagare,
                ref _pagareNotas, ref _formaPagare, ref _controlaConseDep, ref _paramcausacion,
                ref _disableTasaICdat, ref _foraplCaja, ref _convEnpacto, ref _feec);
            SaldoCodeudores(Codeudor1, FecSolicitud, Myconnect, ref SaldoAporCod1, ref saldodeudaCod1, ref SaldoMoraCod1);
            SaldoCodeudores(Codeudor2, FecSolicitud, Myconnect, ref SaldoAporCod2, ref saldodeudaCod2, ref SaldoMoraCod2);
            SaldoCodeudores(Codeudor3, FecSolicitud, Myconnect, ref SaldoAporCod3, ref saldodeudaCod3, ref SaldoMoraCod3);

            switch (ClaseCuota)
            {
                case 1:
                    Clacuo = "Fija";
                    break;
                case 2:
                    Clacuo = "Variable";
                    break;
            }

            switch (TipoContrato)
            {
                case 0:
                    DescContrato = "Independiente";
                    break;
                case 1:
                    DescContrato = "Indefinido";
                    break;
                case 2:
                    DescContrato = "Termino Fijo";
                    break;
                case 3:
                    DescContrato = "Destajo";
                    break;
                case 4:
                    DescContrato = "Servicios";
                    break;
                case 5:
                    DescContrato = "CTA";
                    break;
                case 6:
                    DescContrato = "Pensionados";
                    break;
                case 7:
                    DescContrato = "Labor determinada";
                    break;
            }

            switch (ClaseGarantia)
            {
                case 1:
                    NomGarantia = "Personal";
                    break;
                case 2:
                    NomGarantia = "Hipotecaria";
                    break;
                case 3:
                    NomGarantia = "Prendaria";
                    break;
                case 4:
                    NomGarantia = "Aportes";
                    break;
                case 5:
                    NomGarantia = "Avales";
                    break;
                case 6:
                    NomGarantia = "Fiduciaria";
                    break;
                case 7:
                    NomGarantia = "Pignoracion";
                    break;
                case 8:
                    NomGarantia = "Rentas en dacion";
                    break;
                case 9:
                    NomGarantia = "Otras Admisibles";
                    break;
                case 10:
                    NomGarantia = "CDATS";
                    break;
                case 11:
                    NomGarantia = "Sin Garantia";
                    break;
                case 12:
                    NomGarantia = "Carta Tripartita";
                    break;
            }

            report.SetParameterValue("nombre_empresa", NombreCompania);
            report.SetParameterValue("num_solicitud", Idsolicitud);
            report.SetParameterValue("valor_solicitud", VlrSolicitud);
            report.SetParameterValue("cuota_mensual", Cuota);
            report.SetParameterValue("vlr_disponible", Vlrcupo);
            report.SetParameterValue("clase_cuota", Clacuo);
            report.SetParameterValue("lincred", lincred);
            report.SetParameterValue("descu_mes", TotalDescEmpresa);
            report.SetParameterValue("vlr_atraso", "0");
            report.SetParameterValue("desc_lincred", Nombrelinea);
            report.SetParameterValue("tasai", Tasai);
            report.SetParameterValue("plazo", plazo);
            report.SetParameterValue("clades", Clades);
            report.SetParameterValue("fecdsto", Fecdsto.ToString("yyyy-MM-dd"));

            switch (periodicidad)
            {
                case 1:
                    periodd = "Mensual";
                    break;
                case 2:
                    periodd = "Quincenal";
                    break;
                case 3:
                    periodd = "Decadal";
                    break;
                case 4:
                    periodd = "Semanal";
                    break;
            }

            switch (ClaseInt)
            {
                case 1:
                    NomInt = "Ven.";
                    break;
                case 2:
                    NomInt = "Ant.";
                    break;
            }

            switch (EstadoCivil)
            {
                case 0:
                    DescEstadoCivil = "Soltero(a)";
                    break;
                case 1:
                    DescEstadoCivil = "Casado(a)";
                    break;
                case 2:
                    DescEstadoCivil = "Viudo(a)";
                    break;
                case 3:
                    DescEstadoCivil = "Union Libre";
                    break;
                case 4:
                    DescEstadoCivil = "Separado(a)";
                    break;
                case 5:
                    DescEstadoCivil = "N/A";
                    break;
            }

            report.SetParameterValue("periodd", periodd);
            report.SetParameterValue("clase_inte", NomInt);
            report.SetParameterValue("codigoter", Codigoter);
            report.SetParameterValue("apellido", Apellido);
            report.SetParameterValue("nombre", Nombre);
            report.SetParameterValue("cedula", Cedula);
            report.SetParameterValue("direccion", Direccion);
            report.SetParameterValue("ciudad", NomCiudad);
            report.SetParameterValue("telefono", Telefono);
            report.SetParameterValue("estado_civil", DescEstadoCivil);
            report.SetParameterValue("fecha_nace", FecNacimiento);
            report.SetParameterValue("fecha_ing", FecIngreso);

            switch (CasaPropia)
            {
                case "Y":
                    report.SetParameterValue("casa_propia", "Si");
                    break;
                case "N":
                    report.SetParameterValue("casa_propia", "No");
                    break;
            }

            switch (TieneVehiculo)
            {
                case "Y":
                    report.SetParameterValue("Tiene_vehiculo", "Si");
                    break;
                case "N":
                    report.SetParameterValue("Tiene_vehiculo", "No");
                    break;
            }

            report.SetParameterValue("vehiculo", vehiculo);
            report.SetParameterValue("empresa", NomEmpLabora);
            report.SetParameterValue("fecha_ing_empresa", FecIngEmp);
            report.SetParameterValue("tipo_contrato", DescContrato);
            report.SetParameterValue("cargo", NomCargo);
            report.SetParameterValue("salario", Salario);
            report.SetParameterValue("otros_ingresos", OtrosIngresos);
            report.SetParameterValue("total_desc_empresa", TotalDescEmpresa);
            report.SetParameterValue("gastos_mes", GastosMes);
            report.SetParameterValue("disponible", Disponible);
            report.SetParameterValue("clase_garan", NomGarantia);
            report.SetParameterValue("avaluo_comercial", AvaluoComercial);
            report.SetParameterValue("avaluo_catastral", AvaluoCatastral);
            report.SetParameterValue("detalle", DetalleGarantia);

            switch (Asegurado)
            {
                case "Y":
                    report.SetParameterValue("asegurado", "Si");
                    break;
                case "N":
                    report.SetParameterValue("asegurado", "No");
                    break;
            }

            report.SetParameterValue("fech_ven_seguro", FecVenSeg);
            report.SetParameterValue("por_segurado", PorSeguro);
            report.SetParameterValue("codeudor1", CedulaCodeu1);
            report.SetParameterValue("codeudor2", CedulaCodeu2);
            report.SetParameterValue("codeudor3", CedulaCodeu3);
            report.SetParameterValue("nom_codeu1", NombreCodeudor1);
            report.SetParameterValue("nom_codeu2", NombreCodeudor2);
            report.SetParameterValue("nom_codeu3", NombreCodeudor3);
            report.SetParameterValue("saldoapor1", SaldoAporCod1);
            report.SetParameterValue("saldoapor2", SaldoAporCod2);
            report.SetParameterValue("saldoapor3", SaldoAporCod3);
            report.SetParameterValue("saldodeuda1", saldodeudaCod1);
            report.SetParameterValue("saldodeuda2", saldodeudaCod2);
            report.SetParameterValue("saldodeuda3", saldodeudaCod3);
            report.SetParameterValue("saldomora1", SaldoMoraCod1);
            report.SetParameterValue("saldomora2", SaldoMoraCod2);
            report.SetParameterValue("saldomora3", SaldoMoraCod3);

            report.SetParameterValue("nombre_conyu", NombreConyuge);
            report.SetParameterValue("telefono_conyu", TelConyuge);

            switch (TrabajaConyuge)
            {
                case "Y":
                    report.SetParameterValue("trabaja_conyu", "Si");
                    break;
                case "N":
                    report.SetParameterValue("trabaja_conyu", "No");
                    break;
            }

            report.SetParameterValue("empresa_conyu", EmpresaConyuge);
            report.SetParameterValue("a_cargo_conyu", PersCargoConyuge);
            report.SetParameterValue("dir_emp_conyu", DirConyuge);
            report.SetParameterValue("ciudad_emp_conyu", CiudadConyuge);
            report.SetParameterValue("sal_mes", SalmesConyuge);

            DatSetextras = CargaCuotasExtras(Idsolicitud, Myconnect);
            Numextras = DatSetextras.Tables[0].Rows.Count - 1;
            if (Numextras >= 10)
            {
                Numextras = 9;
            }

            for (I = 0; I <= Numextras; I++)
            {
                if (I != 0)
                {
                    k = I.ToString();
                }

                report.SetParameterValue("No" + k, DatSetextras.Tables[0].Rows[I][0].ToString());
                report.SetParameterValue("fecha" + k, DatSetextras.Tables[0].Rows[I][1]);
                report.SetParameterValue("val" + k, DatSetextras.Tables[0].Rows[I][2]);
                int formaPago = Convert.ToInt32(DatSetextras.Tables[0].Rows[I][3]);
                report.SetParameterValue("for_pago" + k, formaPago == 1 ? "Nomi" : (formaPago == 2 ? "Caja" : ""));
            }

            for (; I <= 9; I++)
            {
                if (I != 0)
                {
                    k = I.ToString();
                }

                report.SetParameterValue("No" + k, "");
                report.SetParameterValue("fecha" + k, "");
                report.SetParameterValue("val" + k, "");
                report.SetParameterValue("for_pago" + k, "");
            }

            try
            {
                report.SetParameterValue("fecsolicitud", FecSolicitud.ToString("dd-MM-yyyy"));
            }
            catch (Exception)
            {
            }

            // EditImpresion.CrystalReportViewer1.ReportSource = report; // ERROR: CS0103
            // EditImpresion.Show(Myforma); // ERROR: CS0103
        }

        private void SaldoCodeudores(string codigoter, DateTime Fecha, OdbcConnection Myconnect, ref double SaldoAportes, ref double SaldoDeuda, ref double DeudaRespaldada)
        {
            string _sSaldoAportes = "0", _sSaldoDeuda = "0", _sDeudaRespaldada = "0";
            string _dummy1 = "", _dummy2 = "", _dummy3 = "";

            stmysql = " select sum(a.saldo) as campo1 from cop_salmaecar a inner join cop_concar12 b " +
                   " on b.lincred = a.lincred where b.CODAHOR = '1' and a.periodo= " + Fecha.ToString("yyyyMM") + " and codigoter ='" + codigoter + "'";
            MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFormato", ref _sSaldoAportes, ref _dummy1, ref _dummy2, ref _dummy3);
            double.TryParse(_sSaldoAportes, out SaldoAportes);

            stmysql = " select sum(a.saldo) as campo1 from cop_salmaecar a inner join cop_concar12 b " +
                   " on b.lincred = a.lincred where b.CODAHOR = '4' and a.periodo= " + Fecha.ToString("yyyyMM") + " and codigoter ='" + codigoter + "'";
            MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFormato", ref _sSaldoDeuda, ref _dummy1, ref _dummy2, ref _dummy3);
            double.TryParse(_sSaldoDeuda, out SaldoDeuda);

            stmysql = " select sum(a.saldo) as campo1 from cop_salmaecar a inner join cop_maecar b " +
                      " on b.lincred = a.lincred and b.numero = a.numero and b.codigoter = a.codigoter where  a.periodo= " +
                      Fecha.ToString("yyyyMM") + "  and (codeudor1='" + codigoter + "' or codeudor2='" + codigoter + "'" +
                      " or codeudor3='" + codigoter + "' or codeudor4='" + codigoter + "') and a.saldo<>0 and a.lincred >= 1000 and b.codigoter<>'" + codigoter + "'";
            MyOdbcConet.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFormato", ref _sDeudaRespaldada, ref _dummy1, ref _dummy2, ref _dummy3);
            double.TryParse(_sDeudaRespaldada, out DeudaRespaldada);
        }

        public DataSet CargaCuotasExtras(double NumeroSolicitud, OdbcConnection Myconnect)
        {
            DataSet DaSet = new DataSet();

            stmysql = "select Numero_cuota,Fecha,valor,forma_pago from cop_extrasoli where numero = " + NumeroSolicitud;

            MyOdbcConet.ExecuteQueryDataset(stmysql, Myconnect, "CargaCuotasExtras", ref DaSet, "Tblextras");
            return DaSet;
        }

        public object ImprimirEstudio(double NroSolicitud, string empresa, string Codigoter,
             bool EsCodeudor, Form myforma, OdbcConnection myconnect, DateTime fecha = default(DateTime))
        {
            if (fecha == default(DateTime))
            {
                fecha = new DateTime(1950, 1, 1);
            }

            reporte rep = new reporte("cop_restcredito");
            // imprimir EditImpresion = new imprimir(); // ERROR: CS0246

            rep.SetParameterValue("nombre_empresa", empresa);
            rep.SetParameterValue("numsolicitud", NroSolicitud);
            rep.SetParameterValue("Codigoter", Codigoter);
            rep.SetParameterValue("EstCodeudor", EsCodeudor ? "CODEUDOR" : "DEUDOR");
            try
            {
                rep.SetParameterValue("periodo", fecha.ToString("yyyyMM"));
            }
            catch (Exception) { }

            // EditImpresion.CrystalReportViewer1.ReportSource = rep; // ERROR: CS0103
            // EditImpresion.Show(myforma); // ERROR: CS0103
            return null;
        }

        public void ImprimeDatosSolicitudes(DataTable DsDatatable, OdbcConnection Myconnect, Form Myforma)
        {
            reporte report = new reporte("cop_fgracre");
            // imprimir EditImpresion = new imprimir(); // ERROR: CS0246
            string Nitemp = " ", Diremp = " ", NomCompania = " ", TelComp = " ";
            string _d_cuentaUtilidad = "", _d_cptoCap = "", _d_calculaSaldo = "", _d_cptoAfavor = "";
            int _d_tipoLiq = 0, _d_diasGracia = 0, _d_baseLiq = 0, _d_ctrlConse = 0, _d_opRecDeuda = 0, _d_numPagare = 0, _d_convEnpacto = 0;
            decimal _d_tasaMora = 0m, _d_tasaUsura = 0m;
            double _d_conseCreditos = 0, _d_conseCdat = 0, _d_consedep = 0, _d_vlrConsulta = 0;
            double _d_limDiario = 0, _d_limMes = 0, _d_valorRetenaux = 0, _d_porcenRetenaux = 0;
            string _d_cptoRevapo = "", _d_nomres = "", _d_cptoServicios = "", _d_cptoExt = "";
            string _d_cptoAho = "", _d_cptoApo = "", _d_cptoRetFte = "", _d_undred = "";
            string _d_cptoCdats = "", _d_cptoIntCdats = "", _d_cpto4Mil = "", _d_cptoIntAhorro = "";
            string _d_cptoAnticipo = "", _d_cpteAnticipo = "", _d_ciudadCia = "";
            string _d_genCobroConsulta = "", _d_cpteConsulta = "", _d_nombreResumido = "";
            string _d_cptoIntAnt = "", _d_cptoint = "", _d_cptoMor = "";
            string _d_manEstudio = "", _d_cobracodeudor = "", _d_porcEndeudamiento = "", _d_manCaptaciones = "";
            string _d_serverSmtp = "", _d_passwordEnvio = "", _d_correoEnvio = "", _d_tipoNomina = "";
            string _d_cptoIntAnticipados = "", _d_depto = "", _d_jefeCartera = "", _d_cpto4MilCheque = "", _d_cpteFavor = "";
            char _d_retenaux = ' ', _d_conciliabanca = ' ';
            string _d_cuentaRetenaux = "", _d_consecFactura = "", _d_numcodecredit = "", _d_modifCuota = "";
            string _d_pagareNotas = "", _d_formaPagare = "", _d_controlaConseDep = "", _d_paramcausacion = "";
            string _d_disableTasaICdat = "", _d_foraplCaja = "", _d_feec = "";

            msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect,
                ref _d_cuentaUtilidad, ref _d_cptoCap, ref _d_calculaSaldo, ref _d_cptoAfavor,
                ref _d_tipoLiq, ref _d_tasaMora, ref _d_diasGracia, ref _d_tasaUsura,
                ref _d_baseLiq, ref _d_ctrlConse, ref _d_conseCreditos, ref _d_cptoRevapo,
                ref Nitemp, ref Diremp, ref _d_nomres, ref _d_cptoServicios,
                ref _d_cptoExt, ref _d_cptoAho, ref _d_cptoApo, ref _d_cptoRetFte,
                ref _d_undred, ref _d_cptoCdats, ref _d_cptoIntCdats, ref NomCompania,
                ref TelComp, ref _d_cpto4Mil, ref _d_cptoIntAhorro, ref _d_cptoAnticipo,
                ref _d_conseCdat, ref _d_consedep, ref _d_opRecDeuda, ref _d_cpteAnticipo,
                ref _d_ciudadCia, ref _d_genCobroConsulta, ref _d_vlrConsulta, ref _d_cpteConsulta,
                ref _d_nombreResumido, ref _d_cptoIntAnt, ref _d_cptoint, ref _d_cptoMor,
                ref _d_limDiario, ref _d_limMes, ref _d_manEstudio, ref _d_cobracodeudor,
                ref _d_porcEndeudamiento, ref _d_manCaptaciones, ref _d_serverSmtp, ref _d_passwordEnvio,
                ref _d_correoEnvio, ref _d_tipoNomina, ref _d_cptoIntAnticipados, ref _d_depto,
                ref _d_jefeCartera, ref _d_cpto4MilCheque, ref _d_cpteFavor, ref _d_retenaux,
                ref _d_valorRetenaux, ref _d_porcenRetenaux, ref _d_cuentaRetenaux, ref _d_consecFactura,
                ref _d_numcodecredit, ref _d_modifCuota, ref _d_conciliabanca, ref _d_numPagare,
                ref _d_pagareNotas, ref _d_formaPagare, ref _d_controlaConseDep, ref _d_paramcausacion,
                ref _d_disableTasaICdat, ref _d_foraplCaja, ref _d_convEnpacto, ref _d_feec);

            report.SetDataSource(DsDatatable);
            report.SetParameterValue("empresa", NomCompania);
            report.SetParameterValue("nit", Nitemp);
            report.SetParameterValue("direccion", Diremp);
            report.SetParameterValue("telefono", TelComp);

            // EditImpresion.CrystalReportViewer1.ReportSource = report; // ERROR: CS0103
            // EditImpresion.Show(Myforma); // ERROR: CS0103
        }

        private void ImprimeRespaldoEstudio(double Idsolicitud, Form Myforma)
        {
            reporte report = new reporte("cop_rimpestudio");
            // imprimir EditImpresion = new imprimir(); // ERROR: CS0246

            report.SetParameterValue("numsolicitud", Idsolicitud);

            // EditImpresion.CrystalReportViewer1.ReportSource = report; // ERROR: CS0103
            // EditImpresion.Show(Myforma); // ERROR: CS0103
        }

        public void ImprimePlanpagos(DataSet dsdataset, Form myforma, double NumSolicitud = 0)
        {
            reporte ImpProyeccion = new reporte("cop_fprocre01", false);
            config_report confi_reportes = new config_report();
            string sql;
            string COMPRONTE = "", Pagare = "", num_ob = "";
            double NUMERO_DOMTO = 0;
            OdbcConnection Myconodbc = new OdbcConnection(varini.pstMyconec);
            Myconodbc.Open();

            if (NumSolicitud != 0)
            {
                sql = "SELECT COMPRONTE as campo1,NUMERO_DOMTO as campo2,mae.filler1 as campo3, mov.numero as campo4 " +
                        " FROM cop_movimto mov inner join  cop_maecar mae ON mae.codigoter = mov.codigoter " +
                        " and mae.lincred = mov.lincred and mae.numero = mov.numero WHERE mov.idsolcre = " + NumSolicitud;
                string sNUMERO_DOMTO = NUMERO_DOMTO.ToString();
                MyOdbcConet.ExecuteQueryconec(sql, Myconodbc, "ImprimePlanpagos", ref COMPRONTE, ref sNUMERO_DOMTO, ref Pagare, ref num_ob);
                double.TryParse(sNUMERO_DOMTO, out NUMERO_DOMTO);
            }

            ImpProyeccion.SetDataSource(dsdataset);
            // ImpProyeccion.Subreports[0].SetDataSource(dsdataset); // ERROR: CS1061
            try
            {
                ImpProyeccion.SetParameterValue("COMPRONTE", COMPRONTE);
                ImpProyeccion.SetParameterValue("NUMERO_DOMTO", NUMERO_DOMTO);
            }
            catch (Exception) { }
            try
            {
                ImpProyeccion.SetParameterValue("Pagare", Pagare);
            }
            catch (Exception) { }
            try
            {
                ImpProyeccion.SetParameterValue("num_ob", num_ob);
            }
            catch (Exception) { }

            try
            {
                ImpProyeccion.SetParameterValue("Empresa", " ");

                string empresa = "", Nempresa = "";

                empresa = dsdataset.Tables["TbldatosCredito"].Rows[0]["Empresa"].ToString();

                sql = "SELECT nombre as campo1 FROM cop_empresa13 WHERE codigo_empresa = '" + empresa + "'";
                MyOdbcConet.ExecuteQueryconec(sql, Myconodbc, "ImprimePlanpagos", ref Nempresa);

                ImpProyeccion.SetParameterValue("Empresa", empresa);
                ImpProyeccion.SetParameterValue("NomEmpresa", Nempresa);
            }
            catch (Exception) { }

            Myconodbc.Close();
            Myconodbc.Dispose();
            confi_reportes.confi_reportes(myforma, ImpProyeccion, true);
        }

        public void imprimiractaComite(Form myforma, string tipoComite, string no_acta, int informe)
        {
            string nameinforme = "";
            switch (informe)
            {
                case 0:
                    nameinforme = "cop_racta";
                    break;
                case 1:
                    nameinforme = "cop_ractares";
                    break;
            }

            reporte rep = new reporte(nameinforme);
            // imprimir EditImpresion = new imprimir(); // ERROR: CS0246
            rep.SetParameterValue("numero_acta", no_acta);
            rep.SetParameterValue("tipoActa", tipoComite);

            // EditImpresion.CrystalReportViewer1.ReportSource = rep; // ERROR: CS0103
            // EditImpresion.Show(myforma); // ERROR: CS0103
        }
    }
}
