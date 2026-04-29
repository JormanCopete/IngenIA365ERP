using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Cartera
{
    public partial class Clscartera
    {

        // Continuation from ~line 9000 of Clscartera.vb

        public void CargaRecDeudas(string Codigoter, int Lincred, string Nombre, string ValorCredito, DateTime Fecha, int Periodo, double NumeroSolicitud, OdbcConnection Myconnet, Form Myforma)
        {
            string DescLinea = " ";
            // this.BuscaLinea(Lincred, Myconnet, 0, 0, " ", " ", " ", " ", " ", " ", " ", " ", " ", ref DescLinea); // ERROR: CS7036

            // frmRecDeudas FrmRecDeudas = new frmRecDeudas(); // ERROR: CS0246
            // FrmRecDeudas.txtCodigoter.Text = Codigoter; // ERROR: CS0103
            // FrmRecDeudas.txtLinea.Text = Lincred.ToString(); // ERROR: CS0103
            // FrmRecDeudas.LblNombre.Text = Nombre; // ERROR: CS0103
            // FrmRecDeudas.LblDescLinea.Text = DescLinea; // ERROR: CS0103
            // FrmRecDeudas.LblCredito.Text = ValorCredito; // ERROR: CS0103
            // FrmRecDeudas.DtpFecha.Value = Fecha; // ERROR: CS0103
            // FrmRecDeudas.periodo = Periodo; // ERROR: CS0103
            // FrmRecDeudas.DatGriCreditos.DataSource = CargaCreditos(Codigoter, Fecha.ToString("yyyyMM"), Myconnet); // ERROR: CS0103
            // FrmRecDeudas.Tag = NumeroSolicitud; // ERROR: CS0103
            // FrmRecDeudas.ShowDialog(Myforma); // ERROR: CS0103
            // FrmRecDeudas.Close(); // ERROR: CS0103
            // FrmRecDeudas.Dispose(); // ERROR: CS0103
        }

        public DataTable CargaCreditos(string Codigoter, string Periodo, OdbcConnection Myconnect)
        {
            DataSet Datset = new DataSet();
            stmysql = "select copmae.lincred as linea, copmae.numero as Numero, salmae.saldo as Saldo, SaldoCapital + SaldoExtra as Capital,SaldoInteres as Interes,"
                     + "SaldoMora as Mora,SaldoSeguro + SaldoAdmon + SaldoOtros as Otros, case copmae.VALOROB when 0 then 0 else (((copmae.VALOROB - salmae.saldo) / copmae.VALOROB) * 100)end  porcentaje "
                     + "FROM cop_maecar copmae inner join cop_salMaecar SalMae on copmae.codigoter = Salmae.codigoter and copmae.lincred = salmae.lincred inner join cop_concar12 parame12 "
                     + "on copmae.lincred = parame12.lincred and copmae.numero = salmae.numero and salmae.periodo = '" + Periodo + "' "
                     + "left join cop_copmora_vw copmora on copmae.codigoter = copmora.codigoter and copmae.lincred = copmora.lincred and "
                     + "copmae.numero = copmora.numero And periodo_contable = " + Periodo
                     + " where copmae.codigoter = '" + Codigoter + "' and  ((parame12.consal = 'Y' and salmae.saldo <> 0) or  "
                     + " (parame12.estcta = 'Y' and (SaldoCapital + SaldoExtra + SaldoInteres + SaldoSeguro + SaldoAdmon + SaldoOtros) <> 0 ))";

            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnect, "CargaCreditos", ref Datset, "TblCopmae");
            return Datset.Tables[0];
        }

        public bool BuscaDeudaRecogida(double NumeroSolicitud, string codigoter, int Lincred, double NumCredito, OdbcConnection Myconnect, ref double Valorpago, ref double IntAdicional, ref string TotalPagar)
        {
            stmysql = "select valor_pago as campo1, inte_adicional as campo2,totpar as campo3 from cop_solrecr where numero = " + NumeroSolicitud + " and codigoter = '" + codigoter + "' and lincred = " + Lincred + " and nume_cred = " + NumCredito;
            string _p1 = Valorpago.ToString(); string _p2 = IntAdicional.ToString(); string _p3 = TotalPagar;
            // ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaDeudaRecogida", ref _p1, ref _p2, ref _p3); // ERROR: CS1501
            double.TryParse(_p1, out Valorpago); double.TryParse(_p2, out IntAdicional); TotalPagar = _p3;
            return ok;
        }
        public bool BuscaDeudaRecogida(double NumeroSolicitud, string codigoter, int Lincred, double NumCredito, OdbcConnection Myconnect)
        {
            double _v = 0; double _i = 0; string _t = "T";
            return BuscaDeudaRecogida(NumeroSolicitud, codigoter, Lincred, NumCredito, Myconnect, ref _v, ref _i, ref _t);
        }

        public void GrabaDeudaRecogida(double NumeroSolicitud, string codigoter, int Lincred, double NumCredito, double Saldorec, double Interes, string TotalPagar, int Periodo, OdbcConnection Myconnect)
        {
            double Saldo = 0; string TotPar = "T";
            double SaldoCapNom = 0, SaldoextNom = 0, SaldoCapCaj = 0, SaldoextCaj = 0; int OpRecDeuda = 0;

            string _opRec = "0";
            // this.msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref _opRec); // ERROR: CS7036
            int.TryParse(_opRec, out OpRecDeuda);

            if (Lincred >= 1000)
            {
                this.BuscaSaldoObligacion(codigoter, Lincred, NumCredito, Periodo, Myconnect, ref Saldo);

                switch (OpRecDeuda)
                {
                    case 0:
                        if (Convert.ToDouble(Saldorec) < Saldo) { TotPar = "P"; }
                        break;
                    case 1:
                        this.BuscaCuotasClades(codigoter, Lincred, NumCredito, Periodo.ToString(), Cladesto.Caja, Myconnect, ref SaldoCapCaj, ref SaldoextCaj);
                        if (Convert.ToDouble(Saldorec) + SaldoCapCaj + SaldoextCaj < Saldo) { TotPar = "P"; }
                        break;
                    case 2:
                        this.BuscaCuotasClades(codigoter, Lincred, NumCredito, Periodo.ToString(), Cladesto.Nomina, Myconnect, ref SaldoCapNom, ref SaldoextNom);
                        if (Convert.ToDouble(Saldorec) + SaldoCapNom + SaldoextNom < Saldo) { TotPar = "P"; }
                        break;
                }
            }

            ok = this.BuscaDeudaRecogida(NumeroSolicitud, codigoter, Lincred, NumCredito, Myconnect);
            if (!ok)
            {
                stmysql = "insert into cop_solrecr(NUMERO,CODIGOTER,LINCRED,NUME_CRED,VALOR_PAGO,INTE_ADICIONAL,TOTPAR) values('"
                       + NumeroSolicitud + "','" + codigoter + "','" + Lincred + "','" + NumCredito + "','" + Saldorec + "','" + Interes + "','" + TotPar + "')";
                this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "GrabaDeudaRecogida");
            }
            else
            {
                stmysql = "update cop_solrecr set VALOR_PAGO = '" + Saldorec + "',INTE_ADICIONAL = '" + Interes + "',TOTPAR='" + TotPar + "' "
                        + "where numero = " + NumeroSolicitud + " and codigoter = '" + codigoter + "' and lincred = " + Lincred + " and nume_cred = " + NumCredito;
                this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "GrabaDeudaRecogida");
            }
        }

        public void BuscaCuotasClades(string codigoter, int lincred, double Numcredito, string periodo, Cladesto Clades, OdbcConnection myconnect,
            ref double SaldoCapital, ref double SaldoExtra, ref double SaldoInteres, ref double SaldoMora,
            ref double SaldoSeguro, ref double SaldoAdmon, ref double SaldoOtros, ref double TotalCuota)
        {
            string stClades = "";
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            switch (Clades)
            {
                case Cladesto.Nomina:
                    stClades = " and coppen.clades = '1' ";
                    break;
                case Cladesto.Caja:
                    stClades = " and coppen.clades = '2' ";
                    break;
            }

            StBuilder.Append("select sum(copmora.SaldoCapital) AS SaldoCapital,sum(copmora.SaldoExtra) AS SaldoExtra,sum(copmora.SaldoInteres) AS SaldoInteres,");
            StBuilder.Append("sum(copmora.SaldoMora) AS SaldoMora,sum(copmora.SaldoSeguro) AS SaldoSeguro, sum(copmora.SaldoAdmon) AS SaldoAdmon,sum(copmora.SaldoOtros) AS SaldoOtros ");
            StBuilder.Append("from cop_copmora copmora ");
            StBuilder.Append("inner join cop_cuopen coppen on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and ");
            StBuilder.Append("copmora.numero = coppen.numero and copmora.periodo_causa = coppen.periodo_causa ");
            StBuilder.Append("where copmora.codigoter = '" + codigoter + "' and copmora.lincred = " + lincred + " and copmora.numero = " + Numcredito + " and copmora.periodo_contable = " + periodo + stClades);
            StBuilder.Append(" and (copmora.SaldoCapital + copmora.SaldoExtra + copmora.SaldoInteres +copmora.SaldoMora + copmora.SaldoSeguro + copmora.SaldoAdmon + copmora.SaldoOtros) <> 0 ");
            StBuilder.Append("group by copmora.codigoter,copmora.lincred,copmora.numero,copmora.periodo_contable");

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaCuotasClades", ref dsdata, "tblcuopen");
            if (ok)
            {
                DataRow row = dsdata.Tables["tblcuopen"].Rows[0];
                SaldoCapital = Convert.ToDouble(row["SaldoCapital"]);
                SaldoExtra = Convert.ToDouble(row["Saldoextra"]);
                SaldoInteres = Convert.ToDouble(row["SaldoInteres"]);
                SaldoMora = Convert.ToDouble(row["SaldoMora"]);
                SaldoSeguro = Convert.ToDouble(row["SaldoSeguro"]);
                SaldoAdmon = Convert.ToDouble(row["SaldoAdmon"]);
                SaldoOtros = Convert.ToDouble(row["SaldoOtros"]);
            }
            else
            {
                SaldoCapital = 0; SaldoExtra = 0; SaldoInteres = 0; SaldoMora = 0;
                SaldoSeguro = 0; SaldoAdmon = 0; SaldoOtros = 0;
            }
            TotalCuota = SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon + SaldoOtros;
        }
        public void BuscaCuotasClades(string codigoter, int lincred, double Numcredito, string periodo, Cladesto Clades, OdbcConnection myconnect,
            ref double SaldoCapital, ref double SaldoExtra)
        {
            double _si = 0, _sm = 0, _ss = 0, _sa = 0, _so = 0, _tc = 0;
            BuscaCuotasClades(codigoter, lincred, Numcredito, periodo, Clades, myconnect, ref SaldoCapital, ref SaldoExtra, ref _si, ref _sm, ref _ss, ref _sa, ref _so, ref _tc);
        }

        public void LeeCodigobarras(string idcodigo, ref string Ai415, ref string Ai8020, ref string Aicedula, ref string Ai3900, ref string Ai96)
        {
            Ai415 = idcodigo.Substring(2, 14);
            Ai8020 = idcodigo.Substring(20, 10);
            Aicedula = idcodigo.Substring(30, 14);
            Ai3900 = idcodigo.Substring(48, 10);
            Ai96 = idcodigo.Substring(60, 8);
        }
        public void LeeCodigobarras(string idcodigo)
        {
            string _a = " ", _b = " ", _c = " ", _d = " ", _e = " ";
            LeeCodigobarras(idcodigo, ref _a, ref _b, ref _c, ref _d, ref _e);
        }

        public void CargaRecaudofacturas(Form myforma, string NomCompania, ref string Cedula, ref double Valorpagar, ref double ValorFactura, ref string NumeroFactura)
        {
            // frmrecaudo frmRecFacturas = new frmrecaudo(); // ERROR: CS0246
            // frmRecFacturas.TsysEmpresa.Text = NomCompania; // ERROR: CS0103
            // frmRecFacturas.ShowDialog(myforma); // ERROR: CS0103
            // Cedula = frmRecFacturas.TxtCedula.Text; // ERROR: CS0103
            // Valorpagar = Convert.ToDouble(frmRecFacturas.TxtPagar.Text); // ERROR: CS0103
            // ValorFactura = Convert.ToDouble(frmRecFacturas.Txtvalor.Text); // ERROR: CS0103
            // NumeroFactura = frmRecFacturas.TxtFactura.Text; // ERROR: CS0103
        }

        public void CargaRecaudoTarjetacredito(Form myforma, string codigoter, string periodo, string NomCompania, ref string Cedula, OdbcConnection mycon, ref double Valorpagar)
        {
            // frmrectarj01 frmRecFacturas = new frmrectarj01(mycon); // ERROR: CS0246
            // frmRecFacturas.TsysEmpresa.Text = NomCompania; // ERROR: CS0103
            // frmRecFacturas.TxtCodigoter.Text = Cedula; // ERROR: CS0103
            // frmRecFacturas.TsysEmpresa.Text = NomCompania; // ERROR: CS0103
            // frmRecFacturas.Txtperiodo.Text = periodo; // ERROR: CS0103
            // frmRecFacturas.TxtCodigoter.Text = codigoter; // ERROR: CS0103
            // if (frmRecFacturas.ShowDialog(myforma) == DialogResult.OK) // ERROR: CS0103
            {
                // Cedula = frmRecFacturas.TxtCodigoter.Text; // ERROR: CS0103
                // Valorpagar = Convert.ToDouble(frmRecFacturas.TxtPagar.Text); // ERROR: CS0103
            }
        }

        public bool BuscaSolicitudCredito(double NumSolicitud, OdbcConnection Myconnect,
            ref DateTime FecProgracion, ref string FecSolicitud, ref string Codigoter, ref int Lincred,
            ref double VlrSolicitado, ref double TasaInt, ref double Plazo, ref double Cuota,
            ref double CupoDisponible, ref double Salario, ref double OtroIngreso, ref double DsctoMensEmp,
            ref double GastosFijosMens, ref double CupoDispMens, ref string TipoContrato, ref string TipoGarantia,
            ref string DescripcionGarantia, ref double AvaluoCcial, ref double AvaluoCatastral, ref string GarantiaAsegurada,
            ref double PorcentajeSeguro, ref string FecVemtoSeguro, ref string Codeudor1, ref string Codeudor2,
            ref string Codeudor3, ref string Codeudor4, ref double VlrAprobado, ref string FecAprobacion,
            ref string NumActa, ref double AporteAdicional, ref string Estado, ref string Usuario,
            ref string Clades, ref double SalarioConyuge, ref double IngVariables, ref double IngArriendos,
            ref double DeudasTerceros, ref double IngPension, ref double DstoPension, ref double DstoParafiscales,
            ref double DstoEntidadNomina, ref double ingConyuge, ref double dstoGastosPerso, ref string cappagoPorcentaje, ref string cappagoRecDeudas,
            ref double ActVivienda, ref double ActVehiculo, ref double ActOtros, ref double ActAportes,
            ref double ActCajaBanco, ref double ActCxC, ref double ActAhorros, ref double TotalAct,
            ref double PasDeudas, ref double PasOtros, ref double PasObliBanca, ref double PasObliHipoteca,
            ref double TotalPas, ref double Patrimonio, ref double TotalPyP, ref double PorcentajePagaduria,
            ref string tipodstoPagaduria, ref string CICLOD, ref string PERIODD)
        {
            string Fecpro = " ";
            string _p2 = FecSolicitud, _p3 = Codigoter, _p4 = Lincred.ToString();
            stmysql = "select FECHA_PROGRAMADA as campo1, FECHA_SOLI as campo2, codigoter as campo3, lincred as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref Fecpro, ref _p2, ref _p3, ref _p4);
            FecSolicitud = _p2; Codigoter = _p3; int.TryParse(_p4, out Lincred);

            if (!Information.IsDate(Fecpro))
                FecProgracion = new DateTime(1950, 1, 1);
            else
                FecProgracion = Convert.ToDateTime(Fecpro);

            // VlrSolicitado, TasaInt, Plazo, Cuota
            string _s1 = VlrSolicitado.ToString(), _s2 = TasaInt.ToString(), _s3 = Plazo.ToString(), _s4 = Cuota.ToString();
            stmysql = "select VLR_SOLICITUD as campo1, TASA_INT as campo2, PLAZO as campo3, CUOTA as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out VlrSolicitado); double.TryParse(_s2, out TasaInt); double.TryParse(_s3, out Plazo); double.TryParse(_s4, out Cuota);

            _s1 = CupoDisponible.ToString(); _s2 = Salario.ToString(); _s3 = OtroIngreso.ToString(); _s4 = DsctoMensEmp.ToString();
            stmysql = "select CUPO_DISPONIBLE as campo1, SALARIO as campo2, OTRO_INGRESO as campo3, DSCTO_MES_EMP as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out CupoDisponible); double.TryParse(_s2, out Salario); double.TryParse(_s3, out OtroIngreso); double.TryParse(_s4, out DsctoMensEmp);

            _s1 = GastosFijosMens.ToString(); _s2 = CupoDispMens.ToString(); _s3 = TipoContrato; _s4 = TipoGarantia;
            stmysql = "select GASTO_FIJO_MES as campo1, CUPO_DISMES as campo2, TIPO_CONTRATO as campo3, TIPO_GARANTIA as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out GastosFijosMens); double.TryParse(_s2, out CupoDispMens); TipoContrato = _s3; TipoGarantia = _s4;

            _s1 = DescripcionGarantia; _s2 = AvaluoCcial.ToString(); _s3 = AvaluoCatastral.ToString(); _s4 = GarantiaAsegurada;
            stmysql = "select DESCRIPCION as campo1, AVALUO_CCIAL as campo2, AVALUO_CATASTRO as campo3, ASEGURADO as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            DescripcionGarantia = _s1; double.TryParse(_s2, out AvaluoCcial); double.TryParse(_s3, out AvaluoCatastral); GarantiaAsegurada = _s4;

            _s1 = PorcentajeSeguro.ToString(); _s2 = FecVemtoSeguro; _s3 = Codeudor1; _s4 = Codeudor2;
            stmysql = "select POR_SEGURO as campo1, FECVEN_SEGURO as campo2, CODEUDOR1 as campo3, CODEUDOR2 as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out PorcentajeSeguro); FecVemtoSeguro = _s2; Codeudor1 = _s3; Codeudor2 = _s4;

            _s1 = Codeudor3; _s2 = Codeudor4; _s3 = VlrAprobado.ToString(); _s4 = FecAprobacion;
            stmysql = "select CODEUDOR3 as campo1, CODEUDOR4 as campo2, VALOR_APROBADO as campo3, FECHA_APROBA as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            Codeudor3 = _s1; Codeudor4 = _s2; double.TryParse(_s3, out VlrAprobado); FecAprobacion = _s4;

            _s1 = NumActa; _s2 = AporteAdicional.ToString(); _s3 = Estado; _s4 = Usuario;
            stmysql = "select NUMERO_ACTA as campo1, APORTE_ADICIONAL as campo2, ESTADO as campo3, USUARIO as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            NumActa = _s1; double.TryParse(_s2, out AporteAdicional); Estado = _s3; Usuario = _s4;

            _s1 = Clades; _s2 = SalarioConyuge.ToString(); _s3 = IngVariables.ToString(); _s4 = IngArriendos.ToString();
            stmysql = "select clades as campo1,SALARIO_ME as campo2, ingvariables as campo3, ingarriendos as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            Clades = _s1; double.TryParse(_s2, out SalarioConyuge); double.TryParse(_s3, out IngVariables); double.TryParse(_s4, out IngArriendos);

            _s1 = DeudasTerceros.ToString(); _s2 = IngPension.ToString(); _s3 = DstoPension.ToString(); _s4 = DstoParafiscales.ToString();
            stmysql = "select deudasterceros as campo1,IngPension as campo2,DstoPension as campo3,DstoParafiscales as campo4 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out DeudasTerceros); double.TryParse(_s2, out IngPension); double.TryParse(_s3, out DstoPension); double.TryParse(_s4, out DstoParafiscales);

            _s1 = DstoEntidadNomina.ToString(); _s2 = ingConyuge.ToString(); _s3 = dstoGastosPerso.ToString();
            string _dummy = "0";
            stmysql = "select dscto_mes_emp_nomina as campo1,ingConyuge as campo2,dstoGastosPerso as campo3 from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _dummy);
            double.TryParse(_s1, out DstoEntidadNomina); double.TryParse(_s2, out ingConyuge); double.TryParse(_s3, out dstoGastosPerso);

            _s1 = cappagoPorcentaje; _s2 = cappagoRecDeudas;
            stmysql = "select cappagoPorcentaje as campo1,cappagoRecDeudas as campo2   from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2);
            cappagoPorcentaje = _s1; cappagoRecDeudas = _s2;

            _s1 = ActVivienda.ToString(); _s2 = ActVehiculo.ToString(); _s3 = ActOtros.ToString(); _s4 = ActAportes.ToString();
            stmysql = "select ActVivienda as campo1,ActVehiculo as campo2,ActOtros  as campo3, ActAportes as campo4  from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out ActVivienda); double.TryParse(_s2, out ActVehiculo); double.TryParse(_s3, out ActOtros); double.TryParse(_s4, out ActAportes);

            _s1 = ActCajaBanco.ToString(); _s2 = ActCxC.ToString(); _s3 = ActAhorros.ToString(); _s4 = TotalAct.ToString();
            stmysql = "select ActCajaBanco as campo1, ActCxC as campo2, ActAhorros as campo3, TotalAct as campo4  from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out ActCajaBanco); double.TryParse(_s2, out ActCxC); double.TryParse(_s3, out ActAhorros); double.TryParse(_s4, out TotalAct);

            _s1 = PasDeudas.ToString(); _s2 = PasOtros.ToString(); _s3 = PasObliBanca.ToString(); _s4 = PasObliHipoteca.ToString();
            stmysql = "select PasDeudas as campo1, PasOtros  as campo2, PasObliBanca as campo3, PasObliHipoteca  as campo4  from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out PasDeudas); double.TryParse(_s2, out PasOtros); double.TryParse(_s3, out PasObliBanca); double.TryParse(_s4, out PasObliHipoteca);

            _s1 = TotalPas.ToString(); _s2 = Patrimonio.ToString(); _s3 = TotalPyP.ToString(); _s4 = PorcentajePagaduria.ToString();
            stmysql = "select TotalPas as campo1, Patrimonio  as campo2, TotalPyP  as campo3,PorcentajePagaduria as campo4  from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _s4);
            double.TryParse(_s1, out TotalPas); double.TryParse(_s2, out Patrimonio); double.TryParse(_s3, out TotalPyP); double.TryParse(_s4, out PorcentajePagaduria);

            _s1 = tipodstoPagaduria; _s2 = CICLOD; _s3 = PERIODD;
            stmysql = "select tipodstoPagaduria as campo1,CICLOD as campo2 , PERIODD as campo3  from cop_solcre where numero = " + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "BuscaSolicitudCredito", ref _s1, ref _s2, ref _s3, ref _dummy);
            tipodstoPagaduria = _s1; CICLOD = _s2; PERIODD = _s3;

            return ok;
        }

        public double BuscaIntMes(string codigoter, int lincred, double Numcredito, DateTime FecSolicitud, double Saldo, OdbcConnection myconnect, bool DevolIntereses)
        {
            string FecUltCau = " "; DateTime FecPriDesc = DateTime.MinValue; DateTime FecCredito = DateTime.MinValue;
            int DiaMes = 0; decimal TasaInt = 0; DateTime Fecprogramacion = DateTime.MinValue;
            int ClaseInt = 0, Clacuo = 0, Cuota = 0, Periode = 0;
            double Intemes = 0; double NumSolicitud = 0; decimal TasaIntLiquidada = 0;
            DateTime FecIntPro = new DateTime(1950, 1, 1); DateTime StFecFin = new DateTime(1950, 1, 1);
            string StPeriodoCausa = "999999"; string Periodicidad = "99"; double fila = 0; int Sw1 = 0; string StCicloDes = "5";
            DataSet dsdata = new DataSet();

            // this.BuscaObligacion(codigoter, lincred, Numcredito, myconnect, 0, ref TasaInt, ref ClaseInt, ref Clacuo, ref Cuota, 0, " ", ref Periode, " ", ref FecPriDesc, " ", " ", " ", ref NumSolicitud, " ", " ", " ", " ", " ", " ", " ", " ", ref FecCredito, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", ref FecIntPro); // ERROR: CS7036

            string _fuc = FecUltCau;
            stmysql = "select max(fecha_movto) as campo1 from cop_copmora copmora inner join cop_cuopen coppen "
                    + "on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and coppen.numero = copmora.numero and copmora.periodo_causa = coppen.periodo_causa"
                    + " where copmora.codigoter = '" + codigoter + "' and copmora.lincred =  " + lincred + " and copmora.numero = " + Numcredito
                    + " group by copmora.codigoter, copmora.lincred, copmora.numero";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaIntMes", ref _fuc);
            FecUltCau = _fuc;

            if (FecIntPro != new DateTime(1950, 1, 1))
            {
                if (FecIntPro > FecCredito) { FecCredito = FecIntPro; }
            }

            if (FecUltCau.Trim() == "0" || FecUltCau.Trim() == "")
            {
                FecUltCau = FecCredito.ToString(varini.PstForFec);
            }

            if (Convert.ToDateTime(FecCredito.ToString(varini.PstForFec)) != Convert.ToDateTime(Convert.ToDateTime(FecUltCau).ToString(varini.PstForFec)))
            {
                string _til = TasaIntLiquidada.ToString(); string _spc = StPeriodoCausa; string _per = Periodicidad;
                stmysql = "select TASA_INTE as campo1,copmora.periodo_causa as campo2,coppen.PERCIDAD as campo3 from cop_copmora copmora inner join cop_cuopen coppen "
                    + "on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and coppen.numero = copmora.numero and copmora.periodo_causa = coppen.periodo_causa"
                    + " where copmora.codigoter = '" + codigoter + "' and copmora.lincred =  " + lincred + " and copmora.numero = " + Numcredito
                    + " and coppen.fecha_movto='" + Convert.ToDateTime(FecUltCau).ToString(varini.PstForFec) + "' ";
                // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaIntMes", ref _til, ref _spc, ref _per); // ERROR: CS1501
                decimal.TryParse(_til, out TasaIntLiquidada); StPeriodoCausa = _spc; Periodicidad = _per;

                if (TasaIntLiquidada > 0) { TasaInt = TasaIntLiquidada; }

                stmysql = "select caunov.periodo_causa,caunov.periodd,caunov.tipo_novedad,caunov.cuotas,caunov.interes,caunov.estado,maecar.CICLOD  "
                        + " from cop_caunov caunov "
                        + " inner join cop_maecar maecar on caunov.codigoter= maecar.codigoter and caunov.lincred =maecar.lincred and caunov.numero =maecar.numero "
                        + " where caunov.codigoter='" + codigoter + "' and caunov.lincred =" + lincred + " and caunov.numero =" + Numcredito
                        + " and caunov.periodo_causa>=" + StPeriodoCausa + " and caunov.periodd='" + Periodicidad + "' "
                        + " order by caunov.fecha_sys,caunov.periodo_causa ";

                ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscaIntMes", ref dsdata, "tbldata");
                if (ok)
                {
                    if (dsdata.Tables["tbldata"].Rows[0]["estado"].ToString() == "A")
                    {
                        if (dsdata.Tables["tbldata"].Rows[0]["tipo_novedad"].ToString() != "5")
                        {
                            if (dsdata.Tables["tbldata"].Rows[0]["tipo_novedad"].ToString() == "2" && dsdata.Tables["tbldata"].Rows[0]["interes"].ToString() == "Y")
                            {
                                Sw1 = 1;
                            }

                            if (Sw1 == 0)
                            {
                                fila = dsdata.Tables["tbldata"].Rows.Count - 1;
                                StPeriodoCausa = dsdata.Tables["tbldata"].Rows[(int)fila]["periodo_causa"].ToString();
                                StCicloDes = dsdata.Tables["tbldata"].Rows[(int)fila]["CICLOD"].ToString();

                                // this.CalcuFecProximoCiclo(StPeriodoCausa, Periodicidad, StCicloDes, DateTime.MinValue, ref StFecFin); // ERROR: CS1503, CS1620

                                if (StFecFin != new DateTime(1950, 1, 1))
                                {
                                    if (StFecFin > Convert.ToDateTime(FecUltCau))
                                    {
                                        FecUltCau = StFecFin.ToString(varini.PstForFec);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            DiaMes = CalculaDias(Convert.ToDateTime(FecSolicitud.ToString("yyyy/MM/dd")), Convert.ToDateTime(FecUltCau));

            if (lincred >= 1000)
            {
                TasaInt = TasaInt / 100;
                // Intemes = calcula_interes(Saldo, (double)TasaInt, Cuota, ClaseInt, Clacuo, 0); // ERROR: CS1503
                Intemes = Intemes / 30 * DiaMes;
            }
            else
            {
                Intemes = 0;
            }
            return Intemes;
        }
        public double BuscaIntMes(string codigoter, int lincred, double Numcredito, DateTime FecSolicitud, double Saldo, OdbcConnection myconnect)
        {
            return BuscaIntMes(codigoter, lincred, Numcredito, FecSolicitud, Saldo, myconnect, false);
        }

        public ArrayList CargaDeudasRecogidas(double NumSolicitud, OdbcConnection myconect)
        {
            ArrayList DeudasRecogidas = new ArrayList();
            int canreg = 0, fila2 = 0;
            stmysql = "select * from cop_solrecr where numero = '" + NumSolicitud + "'";

            DataSet myReader = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconect, "CargaDeudasRecogidas", ref myReader, "TblDeudasRec");
            canreg = myReader.Tables["TblDeudasRec"].Rows.Count;
            while (fila2 < canreg)
            {
                DeudasRecogidas.Add(myReader.Tables["TblDeudasRec"].Rows[fila2]["lincred"]);
                DeudasRecogidas.Add(myReader.Tables["TblDeudasRec"].Rows[fila2]["nume_cred"]);
                DeudasRecogidas.Add(Strings.FormatNumber(Convert.ToDouble(myReader.Tables["TblDeudasRec"].Rows[fila2]["valor_pago"]), 0));
                DeudasRecogidas.Add(Strings.FormatNumber(Convert.ToDouble(myReader.Tables["TblDeudasRec"].Rows[fila2]["inte_adicional"]), 0));
                DeudasRecogidas.Add(myReader.Tables["TblDeudasRec"].Rows[fila2]["totpar"]);
                fila2 += 1;
            }
            myReader.Dispose();
            return DeudasRecogidas;
        }

        public bool PlanoReciboDatos(string NombreArchivo, string Comprobante, double ConseCpte, DateTime FechaMovto, Form Myforma,
                       string detalle, int TipoGeneracion, OdbcConnection myconnect)
        {
            string empresa = " ", ficha = " "; int linea = 0; int numero = 0; double debito = 0, credito = 0;
            string usuario = " "; int plazo = 0, periodd = 0, ciclodsto = 0; string TipoMovto = " "; double dsto = 0;
            string ciclo_caus = "99999"; double cuota = 0; string fecha_priDsto = " "; double tasaI = 0; string codigoter = " ";
            string TipoTransaccion = "99"; int clasecuota = 0, clasei = 0; string agencia = "9999", ccosto = "9999";
            string nit = " ";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Recibiendo y Actualizando Datos", Myforma);
            decimal TotReg; string line; bool okk; ArrayList arreglo = new ArrayList(); bool NoExisteFicha; string codigo = " ";
            object obj; bool recCuotas = true; bool Sobreescribe = false; DateTime fecDescuento = DateTime.MinValue; DateTime feccreacion = DateTime.MinValue; string Estado = " ";
            bool CargaRetirados = false;
            string CtaContra = ""; string nitBenef = ""; string TipoDocCruce = ""; string NumDocCruce = ""; string FecVenceFact = "";
            DataSet DsDatalinea = new DataSet(); string ForSeg = "";
            DateTime fecVenceCredito = new DateTime(1950, 1, 1);

            okk = false;
            NoExisteFicha = false;

            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();
            strStreamReader.Close();
            try
            {
                ok = this.ValidarPlano(NombreArchivo, Myforma, TipoGeneracion, myconnect);
                if (ok)
                {
                    if (MessageBox.Show("Si la obligacion ya existe, desea sobreescribir los parametros actuales con los que vienen en este archivo plano?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        Sobreescribe = true;
                    else
                        Sobreescribe = false;

                    if (MessageBox.Show("Si el asociado esta retirado le carga los datos?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        CargaRetirados = true;
                    else
                        CargaRetirados = false;

                    TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                    // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                    BarraProgreso.Show();

                    FileSystem.FileOpen(1, NombreArchivo, OpenMode.Input);

                    while (!FileSystem.EOF(1))
                    {
                        object _emp = empresa; FileSystem.Input(1, ref _emp); empresa = _emp.ToString();
                        object _fic = ficha; FileSystem.Input(1, ref _fic); ficha = _fic.ToString();
                        object _lin = linea; FileSystem.Input(1, ref _lin); linea = Convert.ToInt32(_lin);
                        object _num = numero; FileSystem.Input(1, ref _num); numero = Convert.ToInt32(_num);
                        object _deb = debito; FileSystem.Input(1, ref _deb); debito = Convert.ToDouble(_deb);
                        object _cre = credito; FileSystem.Input(1, ref _cre); credito = Convert.ToDouble(_cre);
                        object _usr = usuario; FileSystem.Input(1, ref _usr); usuario = _usr.ToString();
                        object _plz = plazo; FileSystem.Input(1, ref _plz); plazo = Convert.ToInt32(_plz);
                        object _pdd = periodd; FileSystem.Input(1, ref _pdd); periodd = Convert.ToInt32(_pdd);
                        object _cds = ciclodsto; FileSystem.Input(1, ref _cds); ciclodsto = Convert.ToInt32(_cds);
                        object _tmv = TipoMovto; FileSystem.Input(1, ref _tmv); TipoMovto = _tmv.ToString();
                        object _dst = dsto; FileSystem.Input(1, ref _dst); dsto = Convert.ToDouble(_dst);
                        object _cuo = cuota; FileSystem.Input(1, ref _cuo); cuota = Convert.ToDouble(_cuo);
                        object _cc = ciclo_caus; FileSystem.Input(1, ref _cc); ciclo_caus = _cc.ToString();
                        object _fpd = fecha_priDsto; FileSystem.Input(1, ref _fpd); fecha_priDsto = _fpd.ToString();
                        object _ti = tasaI; FileSystem.Input(1, ref _ti); tasaI = Convert.ToDouble(_ti);
                        try
                        {
                            object _cta = CtaContra; FileSystem.Input(1, ref _cta); CtaContra = _cta.ToString();
                            object _nb = nitBenef; FileSystem.Input(1, ref _nb); nitBenef = _nb.ToString();
                            object _tdc = TipoDocCruce; FileSystem.Input(1, ref _tdc); TipoDocCruce = _tdc.ToString();
                            object _ndc = NumDocCruce; FileSystem.Input(1, ref _ndc); NumDocCruce = _ndc.ToString();
                            object _fvf = FecVenceFact; FileSystem.Input(1, ref _fvf); FecVenceFact = _fvf.ToString();
                        }
                        catch
                        {
                            CtaContra = ""; nitBenef = ""; TipoDocCruce = ""; NumDocCruce = ""; FecVenceFact = "19500101";
                        }

                        if (debito != 0 || credito != 0)
                        {
                            ficha = ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + ("00000000" + ficha).Substring(("00000000" + ficha).Length - 8);
                            // ok = this.BuscarAsociadoPorFicha(ficha, myconnect, ref codigoter, " ", ref Estado); // ERROR: CS1620
                            if (!ok)
                            {
                                MessageBox.Show("La ficha No. " + ficha + " no esta asignada a ningun asociado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            else
                            {
                                if (!CargaRetirados)
                                {
                                    if (Estado == "R" || Estado == "T") goto NoSube;
                                }
                                DateTime fecha_Dsto = new DateTime(Convert.ToInt32(fecha_priDsto.Substring(0, 4)), Convert.ToInt32(fecha_priDsto.Substring(4, 2)), Convert.ToInt32(fecha_priDsto.Substring(6)));
                                DateTime fecha_Vence = new DateTime(Convert.ToInt32(FecVenceFact.Substring(0, 4)), Convert.ToInt32(FecVenceFact.Substring(4, 2)), Convert.ToInt32(FecVenceFact.Substring(6)));

                                if (TipoMovto.Trim() == "")
                                    this.BuscaLineaTipoMovto(linea, myconnect, ref TipoTransaccion);
                                else
                                    TipoTransaccion = TipoMovto;

                                // ok = this.BuscaObligacion(codigoter, linea, numero, myconnect, 0, 0, 0, 0, 0, 0, " ", 0, " ", ref fecDescuento, " ", " ", " ", 0, " ", " ", " ", " ", " ", " ", " ", " ", ref feccreacion); // ERROR: CS7036
                                okk = true;
                                recCuotas = (ciclo_caus.Trim() != "999999");
                                if (!ok)
                                {
                                    // this.BuscaAsociado(codigoter, myconnect, " ", ref nit, ref agencia); // ERROR: CS7036
                                    if (linea >= 1000)
                                    {
                                        // fecVenceCredito = this.CalculaFechaVence("0", "9999", fecha_Dsto, plazo, myconnect, periodd, ciclodsto); // ERROR: CS1503
                                    }

                                    // this.msgconfig.BuscaLinea(linea, ref DsDatalinea, myconnect); // ERROR: CS1615, CS1620
                                    DataRow drLin = DsDatalinea.Tables[0].Rows[0];
                                    clasecuota = Convert.ToInt32(drLin["clacuo"]);
                                    clasei = Convert.ToInt32(drLin["clasei"]);
                                    ccosto = drLin["centroco"].ToString();
                                    ForSeg = drLin["POAPEN"].ToString();
                                    DsDatalinea.Tables.Clear();

                                    // this.GrabaNuevoCredito(codigoter, linea, numero, FechaMovto, FechaMovto, FechaMovto, fecha_Dsto, nit, plazo, 0, debito, 0, cuota, // ERROR: CS1503
                                                          // tasaI, ciclodsto, periodd, clasecuota, clasei, usuario, DateTime.Now, agencia, ccosto, FechaMovto.ToString("yyyyMM"), myconnect, " ", dsto, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", fecVenceCredito, " ", " ", ForSeg); // ERROR: CS1503

                                    // this.GrabaMovimiento(Comprobante, ConseCpte, codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), TipoTransaccion, FechaMovto, debito, credito, detalle, usuario, myconnect, ciclo_caus, CtaContra, nitBenef, TipoDocCruce + "-" + NumDocCruce, nitBenef, " ", " ", recCuotas, TipoDocCruce, NumDocCruce, detalle, fecha_Vence); // ERROR: CS1503, CS1620
                                    // GrabaCuaotaSalmaecar(codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), myconnect, TipoActualizacion.Todos, cuota, tasaI, ciclodsto, dsto, periodd); // ERROR: CS1503, CS1620
                                }
                                else
                                {
                                    if (Sobreescribe)
                                    {
                                        // this.BuscaAsociado(codigoter, myconnect, " ", ref nit, ref agencia); // ERROR: CS7036
                                        // this.msgconfig.BuscaLinea(linea, ref DsDatalinea, myconnect); // ERROR: CS1615, CS1620
                                        DataRow drLin2 = DsDatalinea.Tables[0].Rows[0];
                                        clasecuota = Convert.ToInt32(drLin2["clacuo"]);
                                        clasei = Convert.ToInt32(drLin2["clasei"]);
                                        ccosto = drLin2["centroco"].ToString();
                                        ForSeg = drLin2["POAPEN"].ToString();
                                        DsDatalinea.Tables.Clear();

                                        // this.GrabaNuevoCredito(codigoter, linea, numero, feccreacion, feccreacion, feccreacion, fecDescuento, nit, plazo, 0, debito, 0, cuota, // ERROR: CS1503
                                                                  // tasaI, ciclodsto, periodd, clasecuota, clasei, usuario, DateTime.Now, agencia, ccosto, FechaMovto.ToString("yyyyMM"), myconnect, " ", dsto, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", ForSeg); // ERROR: CS1503
                                        // GrabaCuaotaSalmaecar(codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), myconnect, TipoActualizacion.Todos, cuota, tasaI, ciclodsto, dsto, periodd); // ERROR: CS1503, CS1620
                                    }
                                    // this.GrabaMovimiento(Comprobante, ConseCpte, codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), TipoTransaccion, FechaMovto, debito, credito, detalle, usuario, myconnect, ciclo_caus, CtaContra, nitBenef, TipoDocCruce + "-" + NumDocCruce, nitBenef, " ", " ", recCuotas, TipoDocCruce, NumDocCruce, detalle, fecha_Vence); // ERROR: CS1503, CS1620
                                }
                            }
                        NoSube:
                            BarraProgreso.PerformStep();
                        }
                    }
                    BarraProgreso.Close();
                    BarraProgreso.Dispose();
                    FileSystem.FileClose(1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                FileSystem.FileClose(1);
            }
            return okk;
        }

        public bool ValidarPlano(string NombreArchivo, Form Myforma, int TipoGeneracion, OdbcConnection myconnect)
        {
            string empresa = " ", ficha = " ", linea = "0", TipoMovto = "", codigoter = "", numero = "0", debito = "0", credito = "0";
            string usuario = " ", plazo = "0", periodd = "0", ciclodsto = "0", dsto = "0";
            string ciclo_caus = "99999", cuota = "0", fecha_priDsto = " ", tasaI = "0";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Archivo Plano", Myforma);
            string mensaje = "";
            decimal TotReg; string line; bool okk; ArrayList arreglo = new ArrayList(); bool NoExisteFicha = false; string codigo = " ";
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\FichaSinAso.txt";
            DialogResult Respuesta = DialogResult.No;
            string CtaContra = ""; string nitBenef = ""; string TipoDocCruce = ""; string NumDocCruce = ""; string FecVenceFact = ""; double registro = 1;

            okk = true;
            strStreamWriter = new StreamWriter(nomruta, false);
            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();
            Respuesta = MessageBox.Show("Desea importar los conceptos asi el asociado tenga novedad ?", "SOLIDO", MessageBoxButtons.YesNo);
            try
            {
                TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                arreglo.Clear();

                // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                BarraProgreso.Show();

                switch (TipoGeneracion)
                {
                    case 0:
                        arreglo.Add("empresa");
                        arreglo.Add("Ficha");
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                        while (line.Trim().Length > 20)
                        {
                            mensaje = "";
                            arreglo.Clear();
                            arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                            empresa = arreglo[0].ToString();
                            ficha = arreglo[1].ToString();
                            linea = arreglo[2].ToString();
                            numero = arreglo[3].ToString();
                            debito = arreglo[4].ToString();
                            credito = arreglo[5].ToString();
                            usuario = arreglo[6].ToString();
                            plazo = arreglo[7].ToString();
                            periodd = arreglo[8].ToString();
                            ciclodsto = arreglo[9].ToString();
                            TipoMovto = arreglo[10].ToString();
                            dsto = arreglo[11].ToString();
                            cuota = arreglo[12].ToString();
                            ciclo_caus = arreglo[13].ToString();
                            fecha_priDsto = arreglo[14].ToString();
                            tasaI = arreglo[15].ToString();
                            try
                            {
                                CtaContra = arreglo[16].ToString();
                                nitBenef = arreglo[17].ToString();
                                TipoDocCruce = arreglo[18].ToString();
                                NumDocCruce = arreglo[19].ToString();
                                FecVenceFact = arreglo[20].ToString();
                            }
                            catch
                            {
                                CtaContra = ""; nitBenef = ""; TipoDocCruce = ""; NumDocCruce = ""; FecVenceFact = "19500101";
                            }

                            arreglo.Clear();
                            arreglo.Add(empresa);
                            arreglo.Add(ficha);
                            codigo = ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + ("00000000" + ficha).Substring(("00000000" + ficha).Length - 8);
                            this.VerificarDatos(codigo, linea, numero, debito, credito, usuario, plazo, periodd, ciclodsto, TipoMovto, dsto, cuota, ciclo_caus, fecha_priDsto,
                                tasaI, TipoGeneracion, CtaContra, nitBenef, TipoDocCruce, NumDocCruce, FecVenceFact, ref arreglo, myconnect, ref mensaje, Respuesta, registro);

                            if (mensaje.Trim() != "")
                            {
                                NoExisteFicha = true;
                                arreglo.Add(mensaje);
                                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "         "));
                            }

                            line = strStreamReader.ReadLine();
                            if (line == null) break;
                            BarraProgreso.PerformStep();
                            registro += 1;
                        }
                        break;
                    case 1:
                        arreglo.Add("cedula");
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                        while (line.Trim().Length > 20)
                        {
                            mensaje = "";
                            arreglo.Clear();
                            arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                            codigoter = arreglo[0].ToString();
                            linea = arreglo[1].ToString();
                            numero = arreglo[2].ToString();
                            debito = arreglo[3].ToString();
                            credito = arreglo[4].ToString();
                            usuario = arreglo[5].ToString();
                            plazo = arreglo[6].ToString();
                            periodd = arreglo[7].ToString();
                            ciclodsto = arreglo[8].ToString();
                            TipoMovto = arreglo[9].ToString();
                            dsto = arreglo[10].ToString();
                            cuota = arreglo[11].ToString();
                            ciclo_caus = arreglo[12].ToString();
                            fecha_priDsto = arreglo[13].ToString();
                            tasaI = arreglo[14].ToString();
                            try
                            {
                                CtaContra = arreglo[15].ToString();
                                nitBenef = arreglo[16].ToString();
                                TipoDocCruce = arreglo[17].ToString();
                                NumDocCruce = arreglo[18].ToString();
                                FecVenceFact = arreglo[19].ToString();
                            }
                            catch
                            {
                                CtaContra = ""; nitBenef = ""; TipoDocCruce = ""; NumDocCruce = ""; FecVenceFact = "19500101";
                            }

                            arreglo.Clear();
                            arreglo.Add(codigoter);
                            this.VerificarDatos(codigoter, linea, numero, debito, credito, usuario, plazo, periodd, ciclodsto, TipoMovto, dsto, cuota, ciclo_caus, fecha_priDsto,
                                                        tasaI, TipoGeneracion, CtaContra, nitBenef, TipoDocCruce, NumDocCruce, FecVenceFact, ref arreglo, myconnect, ref mensaje, Respuesta, registro);

                            if (mensaje.Trim() != "")
                            {
                                NoExisteFicha = true;
                                arreglo.Add(mensaje);
                                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "         "));
                            }
                            line = strStreamReader.ReadLine();
                            if (line == null) break;
                            BarraProgreso.PerformStep();
                            registro += 1;
                        }
                        break;
                }

                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                if (NoExisteFicha)
                {
                    MessageBox.Show("El archivo plano tiene los siguientes errores.", "SOLIDO", MessageBoxButtons.OK);
                    System.Diagnostics.Process.Start(nomruta);
                    okk = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
            }
            return okk;
        }

        public bool ValidarPlanoConceptosFijos(string NombreArchivo, Form Myforma, OdbcConnection myconnect)
        {
            string empresa = " ", ficha = " ", linea = "0", TipoMovto = "", codigoter = "", numero = "0", Valor = "0", credito = "0";
            string usuario = " ", plazo = "0", periodd = "0", ciclodsto = "0", dsto = "0";
            string ciclo_caus = "99999", cuota = "0", fecha_priDsto = " ", tasaI = "0";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Archivo Plano", Myforma);
            string mensaje = ""; double fila2 = 0;
            decimal TotReg; string line; bool okk; ArrayList arreglo = new ArrayList(); bool NoExisteFicha = false; string codigo = " ";
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\ConceptoSinCuota.txt";

            okk = true;
            strStreamWriter = new StreamWriter(nomruta, false);
            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();

            try
            {
                TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                arreglo.Clear();

                // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                BarraProgreso.Show();

                arreglo.Add("Estos son los siguientes errores presentados en el archivo");
                arreglo.Add("  ");
                arreglo.Add("  ");
                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                while (line.Trim().Length > 20)
                {
                    mensaje = "";
                    arreglo.Clear();
                    arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                    codigoter = arreglo[0].ToString();
                    linea = arreglo[1].ToString();
                    numero = arreglo[2].ToString();
                    Valor = arreglo[3].ToString();
                    usuario = arreglo[4].ToString();

                    arreglo.Clear();
                    arreglo.Add(empresa);
                    arreglo.Add(ficha);
                    this.VerificarDatosConcepFijos(codigoter, linea, numero, Valor, usuario, ref arreglo, myconnect, ref mensaje);

                    fila2 += 1;

                    if (mensaje.Trim() != "")
                    {
                        NoExisteFicha = true;
                        arreglo.Add("Fila " + fila2 + "-->" + mensaje);
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "         "));
                    }

                    line = strStreamReader.ReadLine();
                    if (line == null) break;
                    BarraProgreso.PerformStep();
                }

                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                if (NoExisteFicha)
                {
                    MessageBox.Show("El archivo plano tiene los siguientes errores.", "SOLIDO", MessageBoxButtons.OK);
                    System.Diagnostics.Process.Start(nomruta);
                    okk = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
            }
            return okk;
        }

        public void VerificarDatosConcepFijos(string codigo, string linea, string numero, string valor, string usuario, ref ArrayList arreglo, OdbcConnection myconnect, ref string mensaje)
        {
            if (Information.IsNumeric(codigo))
            {
                codigo = ("00000000000000" + codigo).Substring(("00000000000000" + codigo).Length - 14);
                // ok = this.BuscaAsociado(codigo, myconnect); // ERROR: CS1620
                if (!ok) { mensaje = "Cedula no existe. -->" + Convert.ToInt32(codigo).ToString(); }
            }
            else
            {
                mensaje = "La Cedula deben ser numerica.-->" + codigo;
            }

            if (Information.IsNumeric(linea))
            {
                ok = this.BuscaLinea(Convert.ToInt32(linea), myconnect);
                if (!ok) { mensaje += "Linea no existe.--> " + linea; }
            }
            else
            {
                mensaje += "Linea debe ser numerica.--> " + linea;
            }
            if (!Information.IsNumeric(numero)) { mensaje += "El numero de la obligacion debe ser numerico.--> " + numero; }
            if (!Information.IsNumeric(valor)) { mensaje += "El valor de la cuota debe ser numerico.--> " + valor.ToString(); }
            if (usuario.Trim() == "")
            {
                mensaje += "Campo Usuario esta vacio. ";
            }
            else
            {
                // ok = this.msgcofsys.BuscaUsuario(usuario, myconnect); // ERROR: CS1501
                if (!ok) { mensaje += "Usuario no existe. "; }
            }

            if (Information.IsNumeric(numero) && Information.IsNumeric(codigo) && Information.IsNumeric(linea))
            {
                if (!this.BuscaLinea(Convert.ToInt32(linea), myconnect))
                {
                    mensaje += "La Linea no existe";
                }
                else
                {
                    if (!this.BuscaObligacion(codigo, Convert.ToInt32(linea), Convert.ToDouble(numero), myconnect))
                    {
                        mensaje += "Concepto no esta cargado para el asociado en cartera";
                    }
                }
            }
        }

        public bool ActualizarCuotasConcepFijos(string NombreArchivo, Form Myforma, OdbcConnection myconnect, int periodo)
        {
            string empresa = " ", ficha = " ", linea = "0", TipoMovto = "", codigoter = "", numero = "0", Valor = "0", credito = "0";
            string usuario = " ", plazo = "0", periodd = "0", ciclodsto = "0", dsto = "0";
            string ciclo_caus = "99999", cuota = "0", fecha_priDsto = " ", tasaI = "0";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Archivo Plano", Myforma);
            string mensaje = "";
            decimal TotReg; string line; bool okk; ArrayList arreglo = new ArrayList(); bool NoExisteFicha = false; string codigo = " ";
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\ConceptoSinCuota.txt";
            string mysql;
            okk = true;
            strStreamWriter = new StreamWriter(nomruta, false);
            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();

            try
            {
                TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                arreglo.Clear();

                // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                BarraProgreso.Show();

                arreglo.Add("Estos son los siguientes errores presentados en el archivo");
                arreglo.Add("  ");
                arreglo.Add("  ");
                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                while (line.Trim().Length > 10)
                {
                    mensaje = "";
                    arreglo.Clear();
                    arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                    codigoter = arreglo[0].ToString();
                    linea = arreglo[1].ToString();
                    numero = arreglo[2].ToString();
                    Valor = arreglo[3].ToString();
                    usuario = arreglo[4].ToString();

                    arreglo.Clear();

                    mysql = "update cop_maecar set cuota=" + Convert.ToDouble(Valor) + " where codigoter='" + ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14) + "' and lincred=" + Convert.ToDouble(linea) + " and numero=" + Convert.ToDouble(numero);
                    this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "ActualizarCuotasConcepFijos");

                    this.ModificaCuotasalmecar(("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14), Convert.ToInt32(linea), Convert.ToDouble(numero), Convert.ToDouble(Valor), periodo, myconnect);

                    line = strStreamReader.ReadLine();
                    if (line == null) break;
                    BarraProgreso.PerformStep();
                }

                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
            }
            return okk;
        }

        public bool ActualizarEmpresaDescuento(string NombreArchivo, Form Myforma, OdbcConnection myconnect, int opcion)
        {
            string empresa = " ", ficha = " ", linea = "0", TipoMovto = "", codigoter = "", numero = "0", Valor = "0", credito = "0";
            string usuario = " ", plazo = "0", periodd = "0", ciclodsto = "0", dsto = "0";
            string ciclo_caus = "99999", cuota = "0", fecha_priDsto = " ", tasaI = "0";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Archivo Plano", Myforma);
            string mensaje = "";
            decimal TotReg; string line; bool okk; ArrayList arreglo = new ArrayList(); bool NoExisteFicha = false; string codigo = " ";
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\CodigosinEmpresa.txt";
            string mysql;
            okk = true;
            strStreamWriter = new StreamWriter(nomruta, false);
            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();

            switch (opcion)
            {
                case 1:
                    try
                    {
                        TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                        arreglo.Clear();
                        // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                        BarraProgreso.Show();
                        arreglo.Add("Estos son los siguientes errores presentados en el archivo");
                        arreglo.Add("  ");
                        arreglo.Add("  ");
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                        while (line.Trim().Length > 10)
                        {
                            mensaje = "";
                            arreglo.Clear();
                            arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                            codigoter = arreglo[0].ToString();
                            empresa = arreglo[1].ToString();
                            arreglo.Clear();

                            mysql = "update cop_maecar set empdsto='" + ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + "' where codigoter='" + ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14) + "'";
                            this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "ActualizarEmpresaDescuento");

                            line = strStreamReader.ReadLine();
                            if (line == null) break;
                            BarraProgreso.PerformStep();
                        }
                        strStreamWriter.Close(); strStreamWriter.Dispose();
                        strStreamReader.Close();
                        BarraProgreso.Close(); BarraProgreso.Dispose();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        strStreamWriter.Close(); strStreamWriter.Dispose();
                        BarraProgreso.Close(); BarraProgreso.Dispose();
                        okk = false;
                    }
                    break;
                case 2:
                    try
                    {
                        TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                        arreglo.Clear();
                        // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                        BarraProgreso.Show();
                        arreglo.Add("Estos son los siguientes errores presentados en el archivo");
                        arreglo.Add("  ");
                        arreglo.Add("  ");
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                        while (line.Trim().Length > 10)
                        {
                            mensaje = "";
                            arreglo.Clear();
                            arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                            codigoter = arreglo[0].ToString();
                            linea = arreglo[1].ToString();
                            numero = arreglo[2].ToString();
                            empresa = arreglo[3].ToString();
                            arreglo.Clear();

                            mysql = "update cop_maecar set empdsto='" + ("0000" + empresa).Substring(("0000" + empresa).Length - 4) + "' where codigoter='" + ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14) + "' and lincred=" + Convert.ToDouble(linea) + " and numero=" + Convert.ToDouble(numero);
                            this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "ActualizarEmpresaDescuento");

                            line = strStreamReader.ReadLine();
                            if (line == null) break;
                            BarraProgreso.PerformStep();
                        }
                        strStreamWriter.Close(); strStreamWriter.Dispose();
                        strStreamReader.Close();
                        BarraProgreso.Close(); BarraProgreso.Dispose();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        strStreamWriter.Close(); strStreamWriter.Dispose();
                        BarraProgreso.Close(); BarraProgreso.Dispose();
                        okk = false;
                    }
                    break;
            }
            return okk;
        }

        public void VerificarDatos(string codigo, string linea, string numero, string debito, string credito,
            string usuario, string plazo, string periodd, string ciclodsto, string TipoMovto, string dsto, string cuota, string ciclo_caus, string fecha_priDsto,
            string tasaI, int Tipo, string CtaContra, string NitBenef, string TipoDocCruce, string NumDocCruce, string FecVenCruce, ref ArrayList arreglo, OdbcConnection myconnect,
            ref string mensaje, DialogResult Respuesta, double registro)
        {
            DateTime FecMaxNov; DateTime FecDstoArchivo; string codigoasociado = "99999999999999";

            ok = false;
            switch (Tipo)
            {
                case 0:
                    if (Information.IsNumeric(codigo))
                    {
                        // ok = this.BuscarAsociadoPorFicha(codigo, myconnect, ref codigoasociado); // ERROR: CS7036
                        if (!ok) { mensaje = "Ficha no existe. "; }
                    }
                    else
                    {
                        mensaje = "La empresa y la ficha deben ser numericas. ";
                    }
                    break;
                case 1:
                    if (Information.IsNumeric(codigo))
                    {
                        codigo = ("00000000000000" + codigo).Substring(("00000000000000" + codigo).Length - 14);
                        // ok = this.BuscaAsociado(codigo, myconnect); // ERROR: CS1620
                        codigoasociado = codigo;
                        if (!ok) { mensaje = "Cedula no existe. "; }
                    }
                    else
                    {
                        mensaje = "La Cedula deben ser numerica. ";
                    }
                    break;
            }

            if (!ok) { codigoasociado = ""; }

            if (Information.IsNumeric(linea))
            {
                ok = this.BuscaLinea(Convert.ToInt32(linea), myconnect);
                if (!ok) { mensaje += "Linea no existe. "; }
            }
            else
            {
                mensaje += "Linea debe ser numerica. ";
            }
            if (!Information.IsNumeric(numero)) { mensaje += "El numero de la obligacion debe ser numerico. "; }
            if (!Information.IsNumeric(debito)) { mensaje += "El valor debito debe ser numerico. "; }
            if (!Information.IsNumeric(credito)) { mensaje += "El valor credito debe ser numerico. "; }
            if (usuario.Trim() == "")
            {
                mensaje += "Campo Usuario esta vacio. ";
            }
            else
            {
                // ok = this.msgcofsys.BuscaUsuario(usuario, myconnect); // ERROR: CS1501
                if (!ok) { mensaje += "Usuario no existe. "; }
            }
            if (!Information.IsNumeric(plazo)) { mensaje += "El plazo debe ser numerico. "; }
            if (!Information.IsNumeric(periodd)) { mensaje += "La periodicidad debe ser numerico. "; }
            if (!Information.IsNumeric(ciclodsto)) { mensaje += "El ciclo de descuento debe ser numerico. "; }
            if (TipoMovto.Trim() != "")
            {
                if (Information.IsNumeric(TipoMovto))
                {
                    // ok = this.BuscaTipoMovto(TipoMovto, myconnect); // ERROR: CS7036
                    if (!ok) { mensaje += "Tipo Movto no existe. "; }
                }
                else
                {
                    mensaje += "Tipo Movto no es numerico. ";
                }
            }
            if (!Information.IsNumeric(dsto)) { mensaje += "Tipo de Descuento debe ser numerico. "; }
            if (!Information.IsNumeric(cuota)) { mensaje += "Valor cuota debe ser numerico. "; }
            if (ciclo_caus.Trim() != "")
            {
                if (!Information.IsNumeric(ciclo_caus))
                {
                    mensaje += "Ciclo causacion debe ser numerico. ";
                }
                else if (ciclo_caus.Trim().Length != 6)
                {
                    mensaje += "Ciclo causacion debe tener 6 caracteres. ";
                }
            }
            else
            {
                mensaje += "Ciclo causacion es un campo obligatorio. ";
            }

            if (!Information.IsNumeric(fecha_priDsto))
            {
                mensaje += "El formato de la fecha de primer descuento es el siguinte (aaaaMMdd) ej: 20070730. ";
            }
            else
            {
                if (fecha_priDsto.Length != 8)
                {
                    mensaje += "El formato de la fecha esta errado. Formato (aaaaMMdd) ej: 20070920";
                }
                else
                {
                    if (codigoasociado.Trim() != "")
                    {
                        ok = this.BuscaObligacion(codigoasociado, Convert.ToInt32(linea), Convert.ToDouble(numero), myconnect);
                        if (!ok)
                        {
                            if (Respuesta == DialogResult.No)
                            {
                                FecDstoArchivo = new DateTime(Convert.ToInt32(fecha_priDsto.Substring(0, 4)), Convert.ToInt32(fecha_priDsto.Substring(4, 2)), Convert.ToInt32(fecha_priDsto.Substring(6)));
                                FecMaxNov = DateTime.MinValue;
                                // this.BuscaFecMaxNovedad(codigoasociado, myconnect, ref FecMaxNov); // ERROR: CS1503, CS1620
                                if (FecDstoArchivo <= FecMaxNov)
                                {
                                    mensaje += "Asociado esta en novedad hasta : " + FecMaxNov;
                                }
                            }
                        }
                    }
                }
            }
            if (!Information.IsNumeric(tasaI)) { mensaje += "La tasa de interes debe ser numerico. "; }

            if (Tipo == 1)
            {
                string StTercero = " ", StNivel = "0", StTipAux = "0", Stestado = "0";
                if (codigo.Trim() == "99999999999999" && linea.Trim() == "9999")
                {
                    if (!Information.IsNumeric(CtaContra))
                    {
                        mensaje += "La cuenta debe ser numerica. ";
                    }
                    else
                    {
                        // ok = this.msgcnt.BuscarCuenta(CtaContra, myconnect, ref StTercero, " ", " ", " ", ref StNivel, " ", " ", " ", " ", ref StTipAux, ref Stestado); // ERROR: CS7036
                        if (!ok)
                        {
                            mensaje += "La cuenta no existe " + CtaContra + ".";
                        }
                        else
                        {
                            if (Stestado == "1") { mensaje += "La cuenta esta inactiva " + CtaContra + "."; }
                            if (StNivel != "6") { mensaje += "La cuenta no es de movimiento " + CtaContra + "."; }
                            if (StTercero == "Y")
                            {
                                if (NitBenef.Trim() == "")
                                {
                                    mensaje += "Debe Ingresar el nit del beneficiario. ";
                                }
                                else
                                {
                                    // ok = this.msgcnt.BuscarTercero(NitBenef, myconnect, " ", " ", " ", " ", " ", " ", " ", ref Stestado); // ERROR: CS7036
                                    if (!ok)
                                    {
                                        mensaje += "Nit del beneficiario no existe. ";
                                    }
                                    else
                                    {
                                        if (Stestado == "1") { mensaje += "Nit del beneficiario esta inactivo. "; }
                                    }
                                }
                            }

                            if (StTipAux == "1" || StTipAux == "2")
                            {
                                if (TipoDocCruce.Trim() == "") { mensaje += "Debe indicar el tipo doc. Cruce Ej: FC, NC, PR. "; }
                                if (NumDocCruce.Trim() == "") { mensaje += "Debe indicar el numero del Doc. Cruce. "; }

                                if (!Information.IsNumeric(FecVenCruce))
                                {
                                    mensaje += "El formato de la fecha de vencimiento es el siguinte (aaaaMMdd) ej: 20070730. ";
                                }
                                else
                                {
                                    if (FecVenCruce.Length != 8)
                                    {
                                        mensaje += "El formato de la fecha de vencimiento esta errado. Formato (aaaaMMdd) ej: 20070920";
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos msgliqcred = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            bool validarciclo = true;
            DateTime fecha_Dsto2 = new DateTime(Convert.ToInt32(fecha_priDsto.Substring(0, 4)), Convert.ToInt32(fecha_priDsto.Substring(4, 2)), Convert.ToInt32(fecha_priDsto.Substring(6)));

            // msgliqcred.CalculaCiclo(Convert.ToInt32(ciclodsto), Convert.ToInt32(periodd), fecha_Dsto2, false, ref validarciclo); // ERROR: CS7036
            if (!validarciclo)
            {
                mensaje += "Fecha no corresponde al periodo. Verifique que la fecha del primer descuento corresponda al ciclo  descuento.  ";
            }

            if (mensaje.Trim() != "")
            {
                mensaje = "Error en fila " + registro + "." + mensaje;
            }
        }

        public bool PlanoReciboDatosCedula(string NombreArchivo, string Comprobante, double ConseCpte, DateTime FechaMovto, Form Myforma,
                        string detalle, int TipoGeneracion, OdbcConnection myconnect)
        {
            int linea = 0; double numero = 0, debito = 0, credito = 0;
            string usuario = " "; int plazo = 0, periodd = 0, ciclodsto = 0; string TipoMovto = " "; double dsto = 0;
            string ciclo_caus = "99999"; double cuota = 0; string fecha_priDsto = " "; double tasaI = 0; string codigoter = " ";
            string TipoTransaccion = "99"; int clasecuota = 0, clasei = 0; string agencia = "9999", ccosto = "9999";
            string nit = " ";
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Recibiendo y Actualizando Datos", Myforma);
            decimal TotReg; string line; bool okk; ArrayList arreglo = new ArrayList(); bool NoExisteFicha = false; string codigo = " ";
            object obj; bool recDeudas = true; bool Sobreescribe = false; DateTime FecCreacion = DateTime.MinValue; DateTime fecDescuento = DateTime.MinValue; string Estado = " ";
            bool CargaRetirados = false;
            string CtaContra = ""; string nitBenef = ""; string TipoDocCruce = ""; string NumDocCruce = ""; string FecVenceFact = "";
            DataSet DsDatalinea = new DataSet(); string ForSeg = "";
            DateTime fecVenceCredito = new DateTime(1950, 1, 1);

            okk = false;
            NoExisteFicha = false;

            strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();
            strStreamReader.Close();
            try
            {
                ok = this.ValidarPlano(NombreArchivo, Myforma, TipoGeneracion, myconnect);
                if (ok)
                {
                    if (MessageBox.Show("Si la obligacion ya existe, desea sobreescribir los parametros actuales con los que vienen en este archivo plano?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        Sobreescribe = true;
                    else
                        Sobreescribe = false;

                    if (MessageBox.Show("Si el asociado esta retirado le carga los datos?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        CargaRetirados = true;
                    else
                        CargaRetirados = false;

                    TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (line.Length + 2)));
                    // BarraProgreso.ValorMinimoMaximo(0, TotReg); // ERROR: CS1503
                    BarraProgreso.Show();

                    FileSystem.FileOpen(1, NombreArchivo, OpenMode.Input);

                    while (!FileSystem.EOF(1))
                    {
                        object _cod = codigoter; FileSystem.Input(1, ref _cod); codigoter = _cod.ToString();
                        object _lin = linea; FileSystem.Input(1, ref _lin); linea = Convert.ToInt32(_lin);
                        object _num = numero; FileSystem.Input(1, ref _num); numero = Convert.ToDouble(_num);
                        object _deb = debito; FileSystem.Input(1, ref _deb); debito = Convert.ToDouble(_deb);
                        object _cre = credito; FileSystem.Input(1, ref _cre); credito = Convert.ToDouble(_cre);
                        object _usr = usuario; FileSystem.Input(1, ref _usr); usuario = _usr.ToString();
                        object _plz = plazo; FileSystem.Input(1, ref _plz); plazo = Convert.ToInt32(_plz);
                        object _pdd = periodd; FileSystem.Input(1, ref _pdd); periodd = Convert.ToInt32(_pdd);
                        object _cds = ciclodsto; FileSystem.Input(1, ref _cds); ciclodsto = Convert.ToInt32(_cds);
                        object _tmv = TipoMovto; FileSystem.Input(1, ref _tmv); TipoMovto = _tmv.ToString();
                        object _dst = dsto; FileSystem.Input(1, ref _dst); dsto = Convert.ToDouble(_dst);
                        object _cuo = cuota; FileSystem.Input(1, ref _cuo); cuota = Convert.ToDouble(_cuo);
                        object _cc = ciclo_caus; FileSystem.Input(1, ref _cc); ciclo_caus = _cc.ToString();
                        object _fpd = fecha_priDsto; FileSystem.Input(1, ref _fpd); fecha_priDsto = _fpd.ToString();
                        object _ti = tasaI; FileSystem.Input(1, ref _ti); tasaI = Convert.ToDouble(_ti);
                        try
                        {
                            object _cta = CtaContra; FileSystem.Input(1, ref _cta); CtaContra = _cta.ToString();
                            object _nb = nitBenef; FileSystem.Input(1, ref _nb); nitBenef = _nb.ToString();
                            object _tdc = TipoDocCruce; FileSystem.Input(1, ref _tdc); TipoDocCruce = _tdc.ToString();
                            object _ndc = NumDocCruce; FileSystem.Input(1, ref _ndc); NumDocCruce = _ndc.ToString();
                            object _fvf = FecVenceFact; FileSystem.Input(1, ref _fvf); FecVenceFact = _fvf.ToString();
                        }
                        catch
                        {
                            CtaContra = " "; nitBenef = " "; TipoDocCruce = ""; NumDocCruce = ""; FecVenceFact = "19500101";
                        }
                        // Finally block equivalent
                        if (FecVenceFact.Trim() == "" || FecVenceFact.Trim() == "0") FecVenceFact = "19500101";
                        if (nitBenef.Trim() == "") nitBenef = " ";
                        if (CtaContra.Trim() == "" || CtaContra.Trim() == "0") CtaContra = " ";
                        if (NumDocCruce.Trim() == "" || NumDocCruce.Trim() == "0") NumDocCruce = "";

                        try
                        {
                            if (debito != 0 || credito != 0)
                            {
                                codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);
                                // ok = this.BuscaAsociado(codigoter, myconnect, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", ref Estado); // ERROR: CS1620
                                if (!ok)
                                {
                                    MessageBox.Show("La Cedula No. " + codigoter + " no esta asignada a ningun asociado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                                else
                                {
                                    if (codigoter != "99999999999999")
                                    {
                                        if (!CargaRetirados)
                                        {
                                            if (Estado == "R" || Estado == "T") goto NoSube;
                                        }
                                    }

                                    DateTime fecha_Dsto = new DateTime(Convert.ToInt32(fecha_priDsto.Substring(0, 4)), Convert.ToInt32(fecha_priDsto.Substring(4, 2)), Convert.ToInt32(fecha_priDsto.Substring(6)));
                                    DateTime fecha_Vence = new DateTime(Convert.ToInt32(FecVenceFact.Substring(0, 4)), Convert.ToInt32(FecVenceFact.Substring(4, 2)), Convert.ToInt32(FecVenceFact.Substring(6)));

                                    if (TipoMovto.Trim() == "")
                                        this.BuscaLineaTipoMovto(linea, myconnect, ref TipoTransaccion);
                                    else
                                        TipoTransaccion = TipoMovto;

                                    // ok = this.BuscaObligacion(codigoter, linea, numero, myconnect, 0, 0, 0, 0, 0, 0, " ", 0, " ", ref fecDescuento, " ", " ", " ", 0, " ", " ", " ", " ", " ", " ", " ", " ", ref FecCreacion); // ERROR: CS7036
                                    okk = true;
                                    recDeudas = (ciclo_caus.Trim() != "999999");
                                    if (!ok)
                                    {
                                        // this.BuscaAsociado(codigoter, myconnect, " ", ref nit, ref agencia); // ERROR: CS7036
                                        if (linea >= 1000)
                                        {
                                            // fecVenceCredito = this.CalculaFechaVence("0", "9999", fecha_Dsto, plazo, myconnect, periodd, ciclodsto); // ERROR: CS1503
                                        }

                                        // this.msgconfig.BuscaLinea(linea, ref DsDatalinea, myconnect); // ERROR: CS1615, CS1620
                                        DataRow drLin = DsDatalinea.Tables[0].Rows[0];
                                        clasecuota = Convert.ToInt32(drLin["clacuo"]);
                                        clasei = Convert.ToInt32(drLin["clasei"]);
                                        ccosto = drLin["centroco"].ToString();
                                        ForSeg = drLin["POAPEN"].ToString();
                                        DsDatalinea.Tables.Clear();

                                        // this.GrabaNuevoCredito(codigoter, linea, numero, FechaMovto, FechaMovto, FechaMovto, fecha_Dsto, nit, plazo, 0, debito, 0, cuota, // ERROR: CS1503
                                                                  // tasaI, ciclodsto, periodd, clasecuota, clasei, usuario, DateTime.Now, agencia, ccosto, FechaMovto.ToString("yyyyMM"), myconnect, " ", dsto, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", fecVenceCredito, " ", " ", ForSeg); // ERROR: CS1503
                                        // this.GrabaMovimiento(Comprobante, ConseCpte, codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), TipoTransaccion, FechaMovto, debito, credito, detalle, usuario, myconnect, ciclo_caus, CtaContra, nitBenef, TipoDocCruce + "-" + NumDocCruce, nitBenef, " ", " ", recDeudas, TipoDocCruce, NumDocCruce, detalle, fecha_Vence); // ERROR: CS1503, CS1620
                                        // GrabaCuaotaSalmaecar(codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), myconnect, TipoActualizacion.Todos, cuota, tasaI, ciclodsto, dsto, periodd); // ERROR: CS1503, CS1620
                                    }
                                    else
                                    {
                                        if (Sobreescribe)
                                        {
                                            // this.BuscaAsociado(codigoter, myconnect, " ", ref nit, ref agencia); // ERROR: CS7036
                                            // this.msgconfig.BuscaLinea(linea, ref DsDatalinea, myconnect); // ERROR: CS1615, CS1620
                                            DataRow drLin2 = DsDatalinea.Tables[0].Rows[0];
                                            clasecuota = Convert.ToInt32(drLin2["clacuo"]);
                                            clasei = Convert.ToInt32(drLin2["clasei"]);
                                            ccosto = drLin2["centroco"].ToString();
                                            ForSeg = drLin2["POAPEN"].ToString();
                                            DsDatalinea.Tables.Clear();

                                            // this.GrabaNuevoCredito(codigoter, linea, numero, FecCreacion, FecCreacion, FecCreacion, fecDescuento, nit, plazo, 0, debito, 0, cuota, // ERROR: CS1503
                                                                          // tasaI, ciclodsto, periodd, clasecuota, clasei, usuario, DateTime.Now, agencia, ccosto, FechaMovto.ToString("yyyyMM"), myconnect, " ", dsto, " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", " ", ForSeg); // ERROR: CS1503
                                            // GrabaCuaotaSalmaecar(codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), myconnect, TipoActualizacion.Todos, cuota, tasaI, ciclodsto, dsto, periodd); // ERROR: CS1503, CS1620
                                        }
                                        // this.GrabaMovimiento(Comprobante, ConseCpte, codigoter, linea, numero, FechaMovto.ToString("yyyyMM"), TipoTransaccion, FechaMovto, debito, credito, detalle, usuario, myconnect, ciclo_caus, CtaContra, nitBenef, TipoDocCruce + "-" + NumDocCruce, nitBenef, " ", " ", recDeudas, TipoDocCruce, NumDocCruce, detalle, fecha_Vence); // ERROR: CS1503, CS1620
                                    }
                                }
                            NoSube:
                                BarraProgreso.PerformStep();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error " + ex.ToString() + " --> " + "codigoter:" + codigoter + " linea:" + linea + " numero:" + numero + " debito:" + debito + " credito:" + credito + " usuario:" + usuario + "\r\n"
                             + " plazo:" + plazo + " periodd:" + periodd + " ciclodsto:" + ciclodsto + " TipoMovto:" + TipoMovto + " dsto:" + dsto + " cuota:" + cuota + " ciclo_caus:" + ciclo_caus + "\r\n"
                             + " fecha_priDsto:" + fecha_priDsto + " tasaI:" + tasaI + " CtaContra:" + CtaContra + " nitBenef:" + nitBenef + " TipoDocCruce:" + TipoDocCruce + " NumDocCruce:" + NumDocCruce + " FecVenceFact: " + FecVenceFact + " TipoTransaccion:" + TipoTransaccion);
                        }
                    }
                    BarraProgreso.Close();
                    BarraProgreso.Dispose();
                    FileSystem.FileClose(1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                FileSystem.FileClose(1);
            }
            return okk;
        }

        public bool CausaInteresAnticipado(string Comprobante, double ConseCpte, DateTime FechaMovto, Form Myforma,
                     string detalle, int Lincred, string usuario, OdbcConnection myconnect)
        {
            double saldo = 0; decimal Tasa = 0; double VlrLiq = 0; string CptoIntant = "9999"; string CptoInt = "9999";
            ERP.Core.Compartido.Controles.Barraprogress barraproges = new ERP.Core.Compartido.Controles.Barraprogress("Procesando datos ..por favor espere", Myforma);
            int reg = 0, tfila = 0;
            // msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref CptoIntant, ref CptoInt); // ERROR: CS7036

            stmysql = "select copmae.codigoter, copmae.lincred, copmae.numero,salmae.saldo,salmae.tasaint from cop_maecar copmae inner join cop_salmaecar salmae "
                    + "on copmae.codigoter = salmae.codigoter and copmae.lincred = salmae.lincred and copmae.numero = salmae.numero And periodo = " + FechaMovto.ToString("yyyyMM")
                    + " where copmae.lincred = " + Lincred;

            barraproges.DefineMaximo(stmysql, myconnect);
            barraproges.Show();

            DataSet myread = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CausaInteresAnticipado", ref myread, "TblCauIntAnt");
            reg = myread.Tables["TblCauIntAnt"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myread.Tables["TblCauIntAnt"].Rows[tfila];
                saldo = Convert.ToDouble(row["saldo"]);
                Tasa = Convert.ToDecimal(row["tasaint"]);
                VlrLiq = saldo * ((double)Tasa / 100);

                if (VlrLiq > 0)
                {
                    // this.GrabaMovimiento(Comprobante, ConseCpte, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), FechaMovto.ToString("yyyyMM"), CptoIntant, FechaMovto, VlrLiq, 0, detalle, usuario, myconnect, " ", " ", " ", " ", " ", " ", " ", false); // ERROR: CS1503, CS1620
                    // this.GrabaMovimiento(Comprobante, ConseCpte, row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), FechaMovto.ToString("yyyyMM"), CptoInt, FechaMovto, 0, VlrLiq, detalle, usuario, myconnect, " ", " ", " ", " ", " ", " ", " ", false); // ERROR: CS1503, CS1620
                }
                tfila += 1;
                barraproges.PerformStep();
            }

            barraproges.Close();
            myread.Dispose();
            return false;
        }

        public bool GrabarGarantia(string codigoter, int lincred, double numero, string matricula, string TipoGarantia,
            string Descripcion, double AvaluoCatastral, double AvaluoComercial, string TieneSeguro, string NumeroPoliza,
            DateTime FecIniGarantia, DateTime FecCancGarantia, DateTime FecVemtoGarantia, string NitAseguradora, string NombreAseguradora,
            string EstadoGarantia, string usuario, double Porcentaje, double NumCdat, OdbcConnection myconnect,
            ref string CodFiador1, ref string CodFiador2, ref string CodFiador3, ref string CodFiador4, ref int Plazo, ref double Saldo)
        {
            codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);
            ok = this.BuscaObligacion(codigoter, lincred, numero, myconnect);
            if (ok)
            {
                // ok = this.BuscarGarantia(codigoter, lincred, numero, myconnect); // ERROR: CS7036
                if (!ok)
                {
                    stmysql = "insert into cop_garantia (codigoter,lincred,numero,matricula,tipo_garantia,descripcion_garantia,avaluo_catastral,avaluo_ccial,tiene_seguro,"
                            + "nro_poliza,fecini_garantia,fec_cancel_garantia,fecha_vemto,nit_aseguradora,nombre_asegu,estado_garantia,usuario,por_segurado,NumCdat) values "
                            + "('" + codigoter + "'," + lincred + "," + numero + ",'" + matricula + "','" + TipoGarantia + "','" + Descripcion + "'," + AvaluoCatastral
                            + "," + AvaluoComercial + ",'" + TieneSeguro + "','" + NumeroPoliza + "','" + Strings.Format(FecIniGarantia, varini.PstForFec) + "','" + Strings.Format(FecCancGarantia, varini.PstForFec)
                            + "','" + Strings.Format(FecVemtoGarantia, varini.PstForFec) + "','" + NitAseguradora + "','" + NombreAseguradora + "','" + EstadoGarantia + "','"
                            + usuario + "'," + Porcentaje + "," + NumCdat + ")";
                }
                else
                {
                    stmysql = "update cop_garantia set matricula = '" + matricula + "',tipo_garantia ='" + TipoGarantia + "',descripcion_garantia='" + Descripcion + "',avaluo_catastral=" + AvaluoCatastral
                            + ",avaluo_ccial=" + AvaluoComercial + ",tiene_seguro='" + TieneSeguro + "',nro_poliza='" + NumeroPoliza + "',fecini_garantia='" + Strings.Format(FecIniGarantia, varini.PstForFec)
                            + "',fec_cancel_garantia='" + Strings.Format(FecCancGarantia, varini.PstForFec) + "',fecha_vemto='" + Strings.Format(FecVemtoGarantia, varini.PstForFec) + "',nit_aseguradora='" + NitAseguradora
                            + "',nombre_asegu='" + NombreAseguradora + "',estado_garantia='" + EstadoGarantia + "',usuario='" + usuario + "',por_segurado=" + Porcentaje + ",NumCdat=" + NumCdat
                            + " where codigoter ='" + codigoter + "' and lincred =" + lincred + " and numero =" + numero;
                }
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarGarantia");
                if (ok)
                {
                    stmysql = "update cop_maecar set clasegar='" + TipoGarantia + "' where codigoter ='" + codigoter + "' and lincred =" + lincred + " and numero =" + numero;
                    this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarGarantia (Actualiza Maecar)");
                }
            }
            else
            {
                MessageBox.Show("La obligacion no existe, por favor revise.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return ok;
        }

        public bool EliminarGarantia(string codigoter, int lincred, double numero, OdbcConnection myconnect)
        {
            codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);
            stmysql = " delete from cop_garantia where codigoter ='" + codigoter + "' and lincred =" + lincred + " and numero =" + numero;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarGarantia");
            return ok;
        }

        public bool BuscarParametrosCobranza(string usuario, int periodo, OdbcConnection myconnect, ref string codigoter, ref int DiaIni, ref int DiaFin,
             ref int LineaIni, ref int LineaFin, ref string ClaDsto, ref string Orden, ref string EmpresaIni,
             ref string EmpresaFin, ref string cobjuridico)
        {
            string _p1 = codigoter, _p2 = DiaIni.ToString(), _p3 = DiaFin.ToString(), _p4 = LineaIni.ToString();
            stmysql = "select ultcodigoter as campo1,dia_ini as campo2,dia_fin as campo3,lincred_ini as campo4 from cop_gespara where usuario ='" + usuario + "' and periodo=" + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarParametrosCobranza", ref _p1, ref _p2, ref _p3, ref _p4);
            codigoter = _p1; int.TryParse(_p2, out DiaIni); int.TryParse(_p3, out DiaFin); int.TryParse(_p4, out LineaIni);

            _p1 = LineaFin.ToString(); _p2 = ClaDsto; _p3 = Orden; _p4 = EmpresaIni;
            stmysql = "select lincred_fin as campo1,clades as campo2,Orden as campo3,EmpresaIni as campo4 from cop_gespara where usuario ='" + usuario + "' and periodo=" + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarParametrosCobranza", ref _p1, ref _p2, ref _p3, ref _p4);
            int.TryParse(_p1, out LineaFin); ClaDsto = _p2; Orden = _p3; EmpresaIni = _p4;

            _p1 = EmpresaFin; _p2 = cobjuridico;
            stmysql = "select EmpresaFin as campo1,cobjuridico as Campo2 from cop_gespara where usuario ='" + usuario + "' and periodo=" + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarParametrosCobranza", ref _p1, ref _p2);
            EmpresaFin = _p1; cobjuridico = _p2;
            return ok;
        }
        public bool BuscarParametrosCobranza(string usuario, int periodo, OdbcConnection myconnect)
        {
            string _c = " "; int _d1 = 0, _d2 = 0, _l1 = 0, _l2 = 0; string _cl = " ", _o = "0", _ei = "0", _ef = "9999", _cj = "N";
            return BuscarParametrosCobranza(usuario, periodo, myconnect, ref _c, ref _d1, ref _d2, ref _l1, ref _l2, ref _cl, ref _o, ref _ei, ref _ef, ref _cj);
        }

        public bool GrabarParametrosCobranza(string usuario, int periodo, string codigoter, int DiaIni, int DiaFin,
            int LineaIni, int LineaFin, string ClaDsto, string Orden, string EmpresaIni, string EmpresaFin, string cobjuridico, OdbcConnection myconnect)
        {
            EmpresaIni = ("0000" + EmpresaIni).Substring(("0000" + EmpresaIni).Length - 4);
            EmpresaFin = ("0000" + EmpresaFin).Substring(("0000" + EmpresaFin).Length - 4);
            ok = BuscarParametrosCobranza(usuario, periodo, myconnect);
            if (!ok)
            {
                stmysql = "insert into cop_gespara (periodo,usuario,ultcodigoter,dia_ini,dia_fin,lincred_ini,lincred_fin,clades,orden,EmpresaIni,EmpresaFin,CobJuridico) values "
                            + "(" + periodo + " ,'" + usuario + "','" + codigoter + "'," + DiaIni + "," + DiaFin + "," + LineaIni + "," + LineaFin + ",'" + ClaDsto + "','" + Orden + "','" + EmpresaIni + "','" + EmpresaFin + "','" + cobjuridico + "')";
            }
            else
            {
                stmysql = "update cop_gespara set ultcodigoter = '" + codigoter + "' , dia_ini = " + DiaIni + " , dia_fin = " + DiaFin + " , lincred_ini =" + LineaIni + ", lincred_fin = "
                        + LineaFin + " , clades = '" + ClaDsto + "',Orden='" + Orden + "',Empresaini='" + EmpresaIni + "',Empresafin='" + EmpresaFin + "',cobjuridico='" + cobjuridico + "' where usuario = '" + usuario.Trim() + "' and periodo = " + periodo;
            }

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarParametrosCobranza");
            return ok;
        }

        public bool EliminarParametroCobranza(string usuario, int periodo, OdbcConnection myconnect)
        {
            stmysql = "delete from cop_gespara where usuario = '" + usuario.Trim() + "' and periodo = " + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarParametroCobranza");
            return ok;
        }

        public bool ActuaParamCobranza(string usuario, int periodo, string codigoter, OdbcConnection myconnect)
        {
            codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);
            stmysql = "update cop_gespara set ultcodigoter = '" + codigoter + "' where usuario = '" + usuario.Trim() + "' and periodo = " + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActuaParamCobranza");
            return ok;
        }

        public bool BuscarMaestroGestionCobranza(int periodo, string codigoter, OdbcConnection myconnect, ref int IdGesCob,
            ref string estado, ref string usuario, ref string TipoGestion, ref string detalle, ref string FecGestion)
        {
            string _p1 = IdGesCob.ToString(), _p2 = estado, _p3 = usuario, _p4 = detalle;
            stmysql = "select secuencia as campo1,estado as campo2, usuario as campo3,detalle as campo4 from cop_maegescob where codigoter='" + codigoter + "'"
                    + " and periodo=" + periodo + " and secuencia = (select max(secuencia) from cop_maegescob "
                    + " where codigoter='" + codigoter + "'" + (TipoGestion.Trim() == "" ? "" : " and estado='" + TipoGestion + "'") + " and periodo=" + periodo + ")";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarMaestroGestionCobranza", ref _p1, ref _p2, ref _p3, ref _p4);
            int.TryParse(_p1, out IdGesCob); estado = _p2; usuario = _p3; detalle = _p4;

            string _fg = FecGestion;
            stmysql = "select fechagestion as campo1 from cop_maegescob where codigoter='" + codigoter + "'"
                    + " and periodo=" + periodo + " and secuencia = (select max(secuencia) from cop_maegescob "
                    + " where codigoter='" + codigoter + "'" + (TipoGestion.Trim() == "" ? "" : " and estado='" + TipoGestion + "'") + " and periodo=" + periodo + ")";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarMaestroGestionCobranza", ref _fg);
            FecGestion = _fg;
            return ok;
        }

        public virtual DataSet BuscarMaestroGestionCobranza(double CodGestion, OdbcConnection myconnect)
        {
            DataSet dataset = new DataSet();
            stmysql = "select LineaIni, LineaFin, DiaIni, DiaFin, clades,empresaini, empresaFin,cobjuridico,estado,detalle,fechagestion "
                    + " from cop_maegescob where secuencia=" + CodGestion;
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarMaestroGestionCobranza", ref dataset, "TblMaestroCobranza");
            return dataset;
        }

        // NOTE: The remaining methods (GrabarMaestroGestionCobranza, BuscarGestionCobranza, GrabarGestionCobranza,
        // BuscaSiguienteAsoc, Buscahuella, BuscarASocGestionado, CargarDatosCobranza, AgregaColumnasLiquidacion,
        // CargaArregloLiquidacion, LimpiaDatasetLiquidacion, ImprimirReporte, CambiaCedula, CreaActualizaAsociadoTodo,
        // and all the Revisa* / Graba* helper subs, plus CambiarTasaInteres, CambiarTasaSeguro, CambiarCuotasFijas,
        // CreaObligacionesFijas, CalculaCuotaCredito, CreaDescuentosFijos, CargarClasifCart, BuscarEstudioCredito)
        // are continued below as part of this partial class.

        public bool GrabarMaestroGestionCobranza(int periodo, string codigoter, string usuario, DateTime fecha_pago,
            string detalle, double ValorTotalAtrasado, string estado, DateTime fechainicio, OdbcConnection myconnect,
            ref string TipoGestion, ref bool ProcesoAutomatico, double codgestion)
        {
            DataSet dataset = new DataSet();
            int diaini = 0, idgestion = 0, diafin = 0, lineaini = 0; string cobjuridico = "N";
            int lineafin = 0; string clades = "0", empresaini = "0", empresafin = "9999";

            if (ProcesoAutomatico)
            {
                this.BuscarParametrosCobranza(usuario, periodo, myconnect, ref codigoter, ref diaini, ref diafin, ref lineaini, ref lineafin, ref clades, ref empresaini, ref empresaini, ref empresafin, ref cobjuridico);
            }
            else
            {
                dataset = this.BuscarMaestroGestionCobranza(codgestion, myconnect);
                if (dataset.Tables["TblMaestroCobranza"].Rows.Count > 0)
                {
                    DataRow drMC = dataset.Tables["TblMaestroCobranza"].Rows[0];
                    diaini = Convert.ToInt32(drMC["diaini"]); diafin = Convert.ToInt32(drMC["diafin"]);
                    lineaini = Convert.ToInt32(drMC["LineaIni"]); lineafin = Convert.ToInt32(drMC["LineaFin"]);
                    empresaini = drMC["empresaini"].ToString(); empresafin = drMC["empresafin"].ToString();
                    clades = drMC["clades"].ToString(); cobjuridico = drMC["cobjuridico"].ToString();
                }
                else
                {
                    return false;
                }
            }

            if (TipoGestion.Trim() == "")
            {
                stmysql = "insert into cop_maegescob (Periodo, Codigoter, FechaGestion, Usuario, FechaCompromiso,  Detalle, estado, "
                                        + " ValorTotalAtrasado, LineaIni, LineaFin, DiaIni, DiaFin, clades,FechaInicioGestion, empresaini, empresaFin) values (" + periodo + ",'" + codigoter + "','" + Strings.Format(DateTime.Now, varini.pstForfecyHora)
                                        + "','" + usuario + "','" + Strings.Format(fecha_pago, varini.PstForFec) + "','" + detalle + "','" + estado + "'," + ValorTotalAtrasado
                                        + "," + lineaini + "," + lineafin + "," + diaini + "," + diafin + ",'" + clades + "','" + Strings.Format(Convert.ToDateTime(Strings.Format(fechainicio, varini.PstForFec) + " " + Strings.Format(fechainicio, varini.pstForHora)), varini.pstForfecyHora) + "','" + empresaini + "','" + empresafin + "')";
            }
            else
            {
                if (ProcesoAutomatico)
                {
                    string _u1 = " ", _u2 = " ", _u3 = " ";
                    ok = BuscarMaestroGestionCobranza(periodo, codigoter, myconnect, ref idgestion, ref _u1, ref _u2, ref TipoGestion, ref _u3, ref _u3);
                }
                else
                {
                    idgestion = (int)codgestion;
                    ok = true;
                }

                if (!ok) { return false; }

                detalle = detalle + "\r\n" + "Fecha de modificacion " + Strings.Format(DateTime.Now, varini.pstForfecyHora) + "\r\n";
                stmysql = "update cop_maegescob set detalle='" + detalle + "',FechaCompromiso='" + Strings.Format(fecha_pago, varini.PstForFec) + "' where secuencia=" + idgestion;
            }
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarMaestroGestionCobranza");
            return ok;
        }

        public bool BuscarGestionCobranza(int periodo, string codigoter, int lincred, double numero, int IdGesCob, OdbcConnection myconnect,
            ref string usuario, ref DateTime fecha_gestion, ref DateTime fecha_pago,
            ref double saldot, ref double capatr, ref double intatr, ref double segatr, ref double admatr, ref double moracum,
            ref double cuota, ref double diasmora, ref double ExtraAtra, ref double OtrosAtra)
        {
            string _p1 = usuario, _p2 = fecha_gestion.ToString(), _p3 = fecha_pago.ToString(), _p4 = OtrosAtra.ToString();
            stmysql = "select usuario as campo1,fecha_gestion as campo2,fecha_pago as campo3,OtrosAtr as campo4 from cop_gesmaes where codigoter ='" + codigoter + "' and periodo=" + periodo + " and lincred= " + lincred + " and numero=" + numero + " and IdCodmaeges=" + IdGesCob;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGestionCobranza", ref _p1, ref _p2, ref _p3, ref _p4);
            usuario = _p1; DateTime.TryParse(_p2, out fecha_gestion); DateTime.TryParse(_p3, out fecha_pago); double.TryParse(_p4, out OtrosAtra);

            _p1 = ExtraAtra.ToString(); _p2 = saldot.ToString(); _p3 = capatr.ToString(); _p4 = intatr.ToString();
            stmysql = "select ExtraAtr as campo1, saldot as campo2,capatr as campo3,intatr as campo4 from cop_gesmaes where codigoter ='" + codigoter + "' and periodo=" + periodo + " and lincred= " + lincred + " and numero=" + numero + " and IdCodmaeges=" + IdGesCob;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGestionCobranza", ref _p1, ref _p2, ref _p3, ref _p4);
            double.TryParse(_p1, out ExtraAtra); double.TryParse(_p2, out saldot); double.TryParse(_p3, out capatr); double.TryParse(_p4, out intatr);

            _p1 = segatr.ToString(); _p2 = admatr.ToString(); _p3 = moracum.ToString(); _p4 = cuota.ToString();
            stmysql = "select segatr as campo1,admatr as campo2,moracum as campo3,cuota as campo4 from cop_gesmaes where codigoter ='" + codigoter + "' and periodo=" + periodo + " and lincred= " + lincred + " and numero=" + numero + " and IdCodmaeges=" + IdGesCob;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGestionCobranza", ref _p1, ref _p2, ref _p3, ref _p4);
            double.TryParse(_p1, out segatr); double.TryParse(_p2, out admatr); double.TryParse(_p3, out moracum); double.TryParse(_p4, out cuota);

            _p1 = diasmora.ToString();
            stmysql = "select diasmora as campo1  from cop_gesmaes where codigoter ='" + codigoter + "' and periodo=" + periodo + " and lincred= " + lincred + " and numero=" + numero + " and IdCodmaeges=" + IdGesCob;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarGestionCobranza", ref _p1);
            double.TryParse(_p1, out diasmora);

            return ok;
        }

        public void AgregaColumnasLiquidacion()
        {
            string a = " "; double b = 0; int c = 0;
            DateTime FechaLiquidacion = DateTime.MinValue;
            DatLiquidacion.Tables.Add("DatosLiq");
            DataColumnCollection cols = DatLiquidacion.Tables[0].Columns;
            cols.Add("lincred", c.GetType());
            cols.Add("num_cuenta", b.GetType());
            cols.Add("codigoter", a.GetType());
            cols.Add("Nombre", a.GetType());
            cols.Add("base", b.GetType());
            cols.Add("interes", b.GetType());
            cols.Add("retfte", b.GetType());
            cols.Add("Total", b.GetType());
            cols.Add("TasaInt", b.GetType());
            cols.Add("DiasLiq", b.GetType());
            cols.Add("FechaUltimaliq", FechaLiquidacion.GetType());
            cols.Add("ValidaLiq", a.GetType());
        }

        public void CargaArregloLiquidacion(int lincred, double num_cuenta, string codigoter, string nombre,
                  double baseVal, double interes, double retfte, double Total, double TasaInt,
                  double DiasLiq, DateTime FechaUltimaliq, string ValidaLiq)
        {
            lista.Clear();
            lista.Add(lincred);
            lista.Add(num_cuenta);
            lista.Add(codigoter);
            lista.Add(nombre);
            lista.Add(baseVal);
            lista.Add(interes);
            lista.Add(retfte);
            lista.Add(Total);
            lista.Add(TasaInt);
            lista.Add(DiasLiq);
            lista.Add(FechaUltimaliq);
            lista.Add(ValidaLiq);

            this.DatLiquidacion.Tables["DatosLiq"].Rows.Add(lista.ToArray());
        }

        public void LimpiaDatasetLiquidacion()
        {
            this.DatLiquidacion.Tables[0].Rows.Clear();
        }

        public void ImprimirReporte(Form pertenese, int periodo, string empresa, string descripcion, string nit, string direccion, string telefono, string opcion)
        {
            ERP.Core.Compartido.Reportes.reporte r = new ERP.Core.Compartido.Reportes.reporte("cop_rliquidahor", false);
            ERP.Core.Compartido.Reportes.config_report confi_report = new ERP.Core.Compartido.Reportes.config_report();
            r.SetDataSource(DatLiquidacion.Tables["DatosLiq"]);
            r.SetParameterValue("Empresa", empresa);
            r.SetParameterValue("Periodo", periodo);
            r.SetParameterValue("nit", nit);
            r.SetParameterValue("direccion", direccion);
            r.SetParameterValue("telefono", telefono);
            r.SetParameterValue("opcion", opcion);
            r.SetParameterValue("descripcion", descripcion);
            confi_report.confi_reportes(pertenese, r);
        }

        public void CambiaCedula(string Codigoter, string CodigoNuevo, DateTime Fecmovto, string Cpte, double Consecutivo, string Detalle, string Usuario, OdbcConnection myconnect, int metodo, Form myforma)
        {
            string Cptoext = "9999", CodSer = "9999", CodAho = "9999", CodApo = "9999", CodCdat = "9999", CodntCdat = "9999";
            string CodCap = "9999", CodInt = "9999", Codmor = "9999";
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Cambiando Cedula...", myforma, ProgressBarStyle.Blocks, "Espere que se actualice la nueva cedula...");
            msgbarra.ValorMinimoMaximo(0, 16);

            msgbarra.Show(myforma);
            msgbarra.PerformStep();
            // msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, "", ref CodCap, "", "", "", "", "", "", "", "", "", "", "", "", "", ref CodSer, ref Cptoext, ref CodAho, ref CodApo, "", "", ref CodCdat, ref CodntCdat, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", ref CodInt, ref Codmor); // ERROR: CS7036
            msgbarra.PerformStep();

            CreaActualizaAsociadoTodo(Codigoter, CodigoNuevo, "sys_maenit", dsdataset, myconnect, metodo); msgbarra.PerformStep();
            // RevisaCuotasPendientes(Codigoter, CodigoNuevo, Fecmovto, Cpte, Consecutivo, Detalle, Usuario, Convert.ToInt32(CodApo), Convert.ToInt32(Cptoext), Convert.ToInt32(CodAho), Convert.ToInt32(CodSer), Convert.ToInt32(CodCap), Convert.ToInt32(CodCdat), CodInt, Codmor, myconnect); msgbarra.PerformStep(); // ERROR: CS0103
            // RevisaGrabaCuotasExtras(Codigoter, CodigoNuevo, Fecmovto, Cpte, Consecutivo, Detalle, Usuario, Convert.ToInt32(Cptoext), myconnect); msgbarra.PerformStep(); // ERROR: CS0103
            // RevisaSaldos(Codigoter, CodigoNuevo, Fecmovto, Cpte, Consecutivo, Detalle, Usuario, Convert.ToInt32(CodApo), Convert.ToInt32(Cptoext), Convert.ToInt32(CodAho), Convert.ToInt32(CodSer), Convert.ToInt32(CodCap), Convert.ToInt32(CodCdat), myconnect); msgbarra.PerformStep(); // ERROR: CS0103
            RevisaCuentasAhorro(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaCdats(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaMaestroPlasticos(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaAficionesAsociado(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaActividadesAsociado(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaBeneficiariosAsociado(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaHuellafirma(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaNovedadesCausacion(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaGarantias(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaSegurosyBenefSeguros(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            // RevisaCuotasAnteriores(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep(); // ERROR: CS0103
            // if (tienemora) { RevisaMora(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep(); } // ERROR: CS0103
            // RevisaParVivienda(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep(); // ERROR: CS0103
            RevisaConsultaEnLinea(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaDondeEsCodeudor(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();
            RevisaSysReferencias(Codigoter, CodigoNuevo, Fecmovto, Detalle, Usuario, myconnect); msgbarra.PerformStep();

            ActualizaCuuotasalmaecarQuery(CodigoNuevo, Fecmovto.ToString("yyyyMM"), myconnect); msgbarra.PerformStep();

            msgbarra.Close();
        }

        public void RevisaCuentasAhorro(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update dep_maeahor set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaCuentasAhorro");
        }

        public void RevisaMaestroPlasticos(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update deb_maetarj set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "' and estado = 'A'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaMaestroPlasticos");
        }

        public void RevisaCdats(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cdt_maecdats set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaCdats");
            stmysql = "update cdt_novcdats set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaCdats");
        }

        public void RevisaAficionesAsociado(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cop_actiaso set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaAficionesAsociado");
        }

        public void RevisaActividadesAsociado(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cop_actirecrea set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaActividadesAsociado");
        }

        public void RevisaBeneficiariosAsociado(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cop_benef set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaBeneficiariosAsociado");
        }

        public void RevisaHuellafirma(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cop_huellafirma set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaHuellafirma");
        }

        public void RevisaNovedadesCausacion(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            string whereClause = "";
            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    whereClause = "  AND (rtrim(lincred) || rtrim(numero)) IN (SELECT  (rtrim(lincred) || rtrim(numero)) FROM cop_maecar WHERE CODIGOTER = '" + CodigoNuevo + "')";
                    break;
                case "SQL":
                    whereClause = "  AND {fn concat(rtrim(lincred),rtrim(numero))} IN (SELECT  {fn concat(rtrim(lincred),rtrim(numero))} FROM cop_maecar WHERE CODIGOTER = '" + CodigoNuevo + "')";
                    break;
                case "MYSQL":
                case "ORACLE":
                case "POSTGRESQL":
                    whereClause = "  AND concat(rtrim(lincred),rtrim(numero)) IN (SELECT  concat(rtrim(lincred),rtrim(numero)) FROM cop_maecar WHERE CODIGOTER = '" + CodigoNuevo + "')";
                    break;
            }
            try
            {
                stmysql = "update cop_caunov set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "' " + whereClause;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaNovedadesCausacion");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Se presente un problema revisando novedades de causacion." + "\r\n" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void RevisaGarantias(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            string whereClause = "";
            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    whereClause = "  AND (rtrim(lincred) || rtrim(numero)) IN (SELECT  (rtrim(lincred) || rtrim(numero)) FROM cop_maecar WHERE CODIGOTER = '" + CodigoNuevo + "')";
                    break;
                case "SQL":
                    whereClause = "  AND {fn concat(rtrim(lincred),rtrim(numero))} IN (SELECT  {fn concat(rtrim(lincred),rtrim(numero))} FROM cop_maecar WHERE CODIGOTER = '" + CodigoNuevo + "')";
                    break;
                case "MYSQL":
                case "ORACLE":
                case "POSTGRESQL":
                    whereClause = "  AND concat(rtrim(lincred),rtrim(numero)) IN (SELECT  concat(rtrim(lincred),rtrim(numero)) FROM cop_maecar WHERE CODIGOTER = '" + CodigoNuevo + "')";
                    break;
            }
            try
            {
                stmysql = "update cop_garantia set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'  " + whereClause;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaGarantias");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Se presente un problema revisando GARANTIAS." + "\r\n" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void RevisaSegurosyBenefSeguros(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = " select segu.* from cop_seguros segu inner join cop_maecar mae on "
                + "segu.codigoter = mae.codigoter And segu.lincred = mae.lincred And segu.numero = mae.numero and mae.codigoter= '" + Codigoter + "'";
            CreaActualizaAsociadoTodo(Codigoter, CodigoNuevo, "cop_seguros", dsdataset, myconnect, 3, true, stmysql);
            stmysql = "update cop_benefseg set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaSegurosyBenefSeguros");
            stmysql = "delete from cop_seguros where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaSegurosyBenefSeguros");
        }

        public void RevisaParVivienda(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            try
            {
                stmysql = "update cop_parviv set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaParVivienda");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Se presente un problema revisando creditos de vivienda." + "\r\n" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void RevisaConsultaEnLinea(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            try
            {
                stmysql = "update lin_consulta set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaConsultaEnLinea");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Se presente un problema revisando consulta en linea." + "\r\n" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void RevisaDondeEsCodeudor(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set codeudor1 = '" + CodigoNuevo + "' where codeudor1 = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaDondeEsCodeudor");
            stmysql = "update cop_maecar set codeudor2 = '" + CodigoNuevo + "' where codeudor2 = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaDondeEsCodeudor");
            stmysql = "update cop_maecar set codeudor3 = '" + CodigoNuevo + "' where codeudor3 = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaDondeEsCodeudor");
            stmysql = "update cop_maecar set codeudor4 = '" + CodigoNuevo + "' where codeudor4 = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaDondeEsCodeudor");
        }

        public void RevisaSysReferencias(string Codigoter, string CodigoNuevo, DateTime fecmovto, string Detalle, string Usuario, OdbcConnection myconnect)
        {
            stmysql = "update sys_referencia set codigoter = '" + CodigoNuevo + "' where codigoter = '" + Codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RevisaSysReferencias");
        }

        public bool EliminarGarantia_Overload(string codigoter, int lincred, double numero, OdbcConnection myconnect, string placeholder)
        {
            // placeholder overload - same as EliminarGarantia
            return EliminarGarantia(codigoter, lincred, numero, myconnect);
        }

        public bool ActualizaTasaSeguro(string codigoter, int lincred, double numero, double TasaInt, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set tasaseg = " + TasaInt + " where lincred = " + lincred
                      + " and numero = " + numero + " and codigoter = '" + codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaTasaSeguro");
            return ok;
        }

        public bool ActualizaTasaInteres(string codigoter, int lincred, double numero, double TasaInt, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set tasaint = " + TasaInt + " where lincred = " + lincred
                      + " and numero = " + numero + " and codigoter = '" + codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaTasaInteres");
            return ok;
        }

        public bool ActualizaCuotasFijas(string codigoter, int lincred, double numero, double Cuota, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set Cuota = " + Cuota + " where lincred = " + lincred
                      + " and numero = " + numero + " and codigoter = '" + codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaTasaInteres");
            return ok;
        }

        // -----------------------------------------------------------------------
        // CreaActualizaAsociadoTodo  (VB lines 11325-11800)
        // -----------------------------------------------------------------------

        public bool CreaActualizaAsociadoTodo(string codigoter, string codigonuevo, string nombretabla, DataSet DsDataset11, OdbcConnection myconect)
        {
            return CreaActualizaAsociadoTodo(codigoter, codigonuevo, nombretabla, DsDataset11, myconect, 1, false, "");
        }

        public bool CreaActualizaAsociadoTodo(string codigoter, string codigonuevo, string nombretabla, DataSet DsDataset11, OdbcConnection myconect, int metodo)
        {
            return CreaActualizaAsociadoTodo(codigoter, codigonuevo, nombretabla, DsDataset11, myconect, metodo, false, "");
        }

        public bool CreaActualizaAsociadoTodo(string codigoter, string codigonuevo, string nombretabla, DataSet DsDataset11, OdbcConnection myconect, int metodo, bool TraeQuery, string query)
        {
            string mysql = "", mysql2 = "", codigo = "", NombreColumna = "", TipoDato = "";
            int varcol = 0, varfil = 0, cuentacolumnas = 0, cuentafilas = 0;
            DataSet DsDataset2 = new DataSet();
            bool estanulo = false;
            string cade = " ";
            DateTime fecha = new DateTime(1950, 1, 1);
            string nit = " ";
            DataSet DsDataset = new DataSet();

            if (TraeQuery == false)
            {
                mysql = "select * from " + nombretabla + " where codigoter = '" + codigoter + "'";
                mysql2 = "select * from " + nombretabla + " where codigoter = '" + codigonuevo + "'";
            }
            else
            {
                mysql = query;
                mysql2 = query;
            }

            ok = this.OdbcConnect.ExecuteQueryDataset(mysql, myconect, "CreaActualizaAsociadoTodo", ref DsDataset, "tbl" + nombretabla);
            ok = this.OdbcConnect.ExecuteQueryDataset(mysql2, myconect, "CreaActualizaAsociadoTodo", ref DsDataset2, "tbl" + nombretabla);

            OdbcDataAdapter adaptador = new OdbcDataAdapter(mysql, myconect);
            OdbcDataAdapter adaptador2 = new OdbcDataAdapter(mysql2, myconect);
            OdbcCommandBuilder sqlbuilder = new OdbcCommandBuilder(adaptador);
            OdbcCommandBuilder sqlbuilder2 = new OdbcCommandBuilder(adaptador2);
            adaptador.Fill(DsDataset);
            adaptador2.Fill(DsDataset2);

            try
            {
                switch (metodo)
                {
                    case 1:
                    {
                        DsDataset2.AcceptChanges();

                        if (nombretabla == "sys_maenit")
                        {
                            mysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigonuevo + "'";
                            this.OdbcConnect.ExecuteQueryconec(mysql, myconect, "CreaActualizaAsociadoTodo", ref nit);
                        }

                        cuentafilas    = DsDataset.Tables[0].Rows.Count;
                        cuentacolumnas = DsDataset.Tables[0].Columns.Count;

                        for (varfil = 0; varfil <= cuentafilas - 1; varfil++)
                        {
                            System.Text.StringBuilder Stbuilder = new System.Text.StringBuilder();
                            Stbuilder.Append("update " + nombretabla + " set ");

                            for (varcol = 0; varcol <= cuentacolumnas - 2; varcol++)
                            {
                                estanulo      = DsDataset.Tables[0].Rows[varfil].IsNull(varcol);
                                NombreColumna = DsDataset.Tables[0].Columns[varcol].ColumnName.ToString();
                                TipoDato      = DsDataset.Tables[0].Columns[varcol].DataType.ToString();

                                if (estanulo == true)
                                {
                                    switch (TipoDato)
                                    {
                                        case "System.String":
                                            if (NombreColumna == "codigoter" || NombreColumna == "CODIGOTER")
                                            {
                                                Stbuilder.Append(NombreColumna + "= '" + codigonuevo + "',");
                                            }
                                            else
                                            {
                                                switch (NombreColumna.ToUpper())
                                                {
                                                    case "FECINTPROP":
                                                        fecha = new DateTime(1950, 1, 1);
                                                        Stbuilder.Append(NombreColumna + "='" + Strings.Format(fecha, varini.PstForFec) + "',");
                                                        NombreColumna = "";
                                                        break;
                                                    default:
                                                        Stbuilder.Append(NombreColumna + "= '0',");
                                                        break;
                                                }
                                            }
                                            break;
                                        case "System.Decimal":
                                        case "System.Int32":
                                        case "System.Int16":
                                        case "system.Int64":
                                            Stbuilder.Append(NombreColumna + "='0',");
                                            break;
                                        case "System.DateTime":
                                        case "System.DateTime2":
                                        case "System.Date":
                                            fecha = new DateTime(1950, 1, 1);
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECHASYS":
                                                    Stbuilder.Append(NombreColumna + "= '" + Strings.Format(fecha, varini.pstForfecyHora) + "',");
                                                    break;
                                                default:
                                                    Stbuilder.Append(NombreColumna + "= '" + Strings.Format(fecha, varini.PstForFec) + "',");
                                                    break;
                                            }
                                            break;
                                        default:
                                            Stbuilder.Append(NombreColumna + "=" + DsDataset.Tables[0].Rows[varfil][varcol] + ",");
                                            break;
                                    }
                                }
                                else
                                {
                                    switch (TipoDato)
                                    {
                                        case "System.String":
                                            if (NombreColumna == "codigoter" || NombreColumna == "CODIGOTER")
                                            {
                                                Stbuilder.Append(NombreColumna + "= '" + codigonuevo + "',");
                                            }
                                            else
                                            {
                                                switch (NombreColumna.ToUpper())
                                                {
                                                    case "FECINTPROP":
                                                        if (!Information.IsDate(DsDataset.Tables[0].Rows[varfil][varcol]))
                                                            fecha = new DateTime(1950, 1, 1);
                                                        else
                                                            fecha = Convert.ToDateTime(DsDataset.Tables[0].Rows[varfil][varcol]);
                                                        NombreColumna = "";
                                                        Stbuilder.Append(NombreColumna + "='" + Strings.Format(fecha, varini.PstForFec) + "',");
                                                        break;
                                                    default:
                                                        Stbuilder.Append(NombreColumna + "= '" + DsDataset.Tables[0].Rows[varfil][varcol] + "',");
                                                        break;
                                                }
                                            }
                                            break;
                                        case "System.Decimal":
                                        case "System.Int32":
                                        case "System.Int16":
                                        case "system.Int64":
                                            Stbuilder.Append(NombreColumna + "=" + DsDataset.Tables[0].Rows[varfil][varcol] + ",");
                                            break;
                                        case "System.DateTime":
                                        case "System.DateTime2":
                                        case "System.Date":
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECHASYS":
                                                    Stbuilder.Append(NombreColumna + "= '" + Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.pstForfecyHora) + "',");
                                                    break;
                                                default:
                                                    Stbuilder.Append(NombreColumna + "= '" + Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.PstForFec) + "',");
                                                    break;
                                            }
                                            break;
                                        default:
                                            Stbuilder.Append(NombreColumna + "=" + DsDataset.Tables[0].Rows[varfil][varcol] + ",");
                                            break;
                                    }
                                }
                            } // end for varcol

                            // last column (no trailing comma — appends WHERE clause)
                            estanulo      = DsDataset.Tables[0].Rows[varfil].IsNull(varcol);
                            NombreColumna = DsDataset.Tables[0].Columns[varcol].ColumnName.ToString();
                            TipoDato      = DsDataset.Tables[0].Columns[varcol].DataType.ToString();

                            if (estanulo == true)
                            {
                                switch (TipoDato)
                                {
                                    case "System.String":
                                        if (NombreColumna == "codigoter" || NombreColumna == "CODIGOTER")
                                        {
                                            Stbuilder.Append(NombreColumna + "= '" + codigonuevo + "' where codigoter='" + codigonuevo + "'");
                                        }
                                        else
                                        {
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECINTPROP":
                                                    fecha = new DateTime(1950, 1, 1);
                                                    Stbuilder.Append(NombreColumna + "='" + Strings.Format(fecha, varini.PstForFec) + "'  where codigoter='" + codigonuevo + "'");
                                                    NombreColumna = "";
                                                    break;
                                                default:
                                                    Stbuilder.Append(NombreColumna + "= '0' where codigoter='" + codigonuevo + "'");
                                                    break;
                                            }
                                        }
                                        break;
                                    case "System.Decimal":
                                    case "System.Int32":
                                    case "System.Int16":
                                    case "system.Int64":
                                        Stbuilder.Append(NombreColumna + "=0 where codigoter='" + codigonuevo + "'");
                                        break;
                                    case "System.DateTime":
                                    case "System.DateTime2":
                                    case "System.Date":
                                        fecha = new DateTime(1950, 1, 1);
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECHASYS":
                                                Stbuilder.Append(NombreColumna + "= '" + Strings.Format(fecha, varini.pstForfecyHora) + "' where codigoter='" + codigonuevo + "'");
                                                break;
                                            default:
                                                Stbuilder.Append(NombreColumna + "= '" + Strings.Format(fecha, varini.PstForFec) + "' where codigoter='" + codigonuevo + "'");
                                                break;
                                        }
                                        break;
                                    default:
                                        Stbuilder.Append(NombreColumna + "=" + DsDataset.Tables[0].Rows[varfil][varcol] + " where codigoter='" + codigonuevo + "'");
                                        break;
                                }
                            }
                            else
                            {
                                switch (TipoDato)
                                {
                                    case "System.String":
                                        if (NombreColumna == "codigoter" || NombreColumna == "CODIGOTER")
                                        {
                                            Stbuilder.Append(NombreColumna + "= '" + codigonuevo + "' where codigoter='" + codigonuevo + "'");
                                        }
                                        else
                                        {
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECINTPROP":
                                                    if (!Information.IsDate(DsDataset.Tables[0].Rows[varfil][varcol]))
                                                        fecha = new DateTime(1950, 1, 1);
                                                    else
                                                        fecha = Convert.ToDateTime(DsDataset.Tables[0].Rows[varfil][varcol]);
                                                    NombreColumna = "";
                                                    Stbuilder.Append(NombreColumna + "='" + Strings.Format(fecha, varini.PstForFec) + "' where codigoter='" + codigonuevo + "'");
                                                    break;
                                                default:
                                                    Stbuilder.Append(NombreColumna + "= '" + DsDataset.Tables[0].Rows[varfil][varcol] + "' where codigoter='" + codigonuevo + "'");
                                                    break;
                                            }
                                        }
                                        break;
                                    case "System.Decimal":
                                    case "System.Int32":
                                    case "System.Int16":
                                    case "system.Int64":
                                        Stbuilder.Append(NombreColumna + "=" + DsDataset.Tables[0].Rows[varfil][varcol] + " where codigoter='" + codigonuevo + "'");
                                        break;
                                    case "System.DateTime":
                                    case "System.DateTime2":
                                    case "System.Date":
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECHASYS":
                                                Stbuilder.Append(NombreColumna + "= '" + Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.pstForfecyHora) + "' where codigoter='" + codigonuevo + "'");
                                                break;
                                            default:
                                                Stbuilder.Append(NombreColumna + "= '" + Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.PstForFec) + "' where codigoter='" + codigonuevo + "'");
                                                break;
                                        }
                                        break;
                                    default:
                                        Stbuilder.Append(NombreColumna + "=" + DsDataset.Tables[0].Rows[varfil][varcol] + " where codigoter='" + codigonuevo + "'");
                                        break;
                                }
                            }

                            ok = this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconect, "CreaActualizaAsociadoTodo");
                            if (nombretabla == "sys_maenit")
                            {
                                mysql = "update sys_maenit set nit=" + Convert.ToInt32(nit) + " where codigoter = '" + codigonuevo + "'";
                                this.OdbcConnect.ExecuteQueryconec(mysql, myconect, "CreaActualizaAsociadoTodo");
                            }
                        } // end for varfil
                        break;
                    } // end case 1

                    case 2:
                    {
                        // Copy rows from DsDataset into DsDataset2 then insert with new codigoter
                        DsDataset2.Clear();
                        DsDataset2.AcceptChanges();
                        DsDataset2 = DsDataset.Copy();

                        cuentafilas    = DsDataset.Tables[0].Rows.Count;
                        cuentacolumnas = DsDataset.Tables[0].Columns.Count;

                        for (varfil = 0; varfil <= cuentafilas - 1; varfil++)
                        {
                            System.Text.StringBuilder Stbuilder = new System.Text.StringBuilder();
                            Stbuilder.Append("insert into " + nombretabla + " values ('");

                            for (varcol = 0; varcol <= cuentacolumnas - 2; varcol++)
                            {
                                DsDataset.Tables[0].Rows[varfil]["codigoter"] = codigonuevo;
                                estanulo      = DsDataset.Tables[0].Rows[varfil].IsNull(varcol);
                                TipoDato      = DsDataset.Tables[0].Columns[varcol].DataType.ToString();
                                NombreColumna = DsDataset.Tables[0].Columns[varcol].ColumnName.ToString();

                                if (estanulo == true)
                                {
                                    switch (TipoDato)
                                    {
                                        case "System.String":
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECINTPROP":
                                                    fecha = new DateTime(1950, 1, 1);
                                                    Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                                                    NombreColumna = "";
                                                    break;
                                                default:
                                                    Stbuilder.Append("0" + "','");
                                                    break;
                                            }
                                            break;
                                        case "System.Decimal":
                                        case "System.Int32":
                                        case "System.Int16":
                                        case "system.Int64":
                                            Stbuilder.Append("0" + "','");
                                            break;
                                        case "System.DateTime":
                                        case "System.DateTime2":
                                        case "System.Date":
                                            fecha = new DateTime(1950, 1, 1);
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECHASYS":
                                                    Stbuilder.Append(Strings.Format(fecha, varini.pstForfecyHora) + "','");
                                                    break;
                                                default:
                                                    Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                                                    break;
                                            }
                                            break;
                                        default:
                                            Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "','");
                                            break;
                                    }
                                }
                                else
                                {
                                    if (TipoDato == "System.DateTime" || TipoDato == "System.DateTime2" || TipoDato == "System.Date")
                                    {
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECHASYS":
                                                Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.pstForfecyHora) + "','");
                                                break;
                                            default:
                                                Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.PstForFec) + "','");
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECINTPROP":
                                                if (!Information.IsDate(DsDataset.Tables[0].Rows[varfil][varcol]))
                                                    fecha = new DateTime(1950, 1, 1);
                                                else
                                                    fecha = Convert.ToDateTime(DsDataset.Tables[0].Rows[varfil][varcol]);
                                                NombreColumna = "";
                                                Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                                                break;
                                            default:
                                                Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "','");
                                                break;
                                        }
                                    }
                                }
                            } // end for varcol

                            // last column (closes insert values)
                            estanulo      = DsDataset.Tables[0].Rows[varfil].IsNull(varcol);
                            TipoDato      = DsDataset.Tables[0].Columns[varcol].DataType.ToString();
                            NombreColumna = DsDataset.Tables[0].Columns[varcol].ColumnName.ToString();

                            if (estanulo == true)
                            {
                                switch (TipoDato)
                                {
                                    case "System.String":
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECINTPROP":
                                                fecha = new DateTime(1950, 1, 1);
                                                Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "')");
                                                NombreColumna = "";
                                                break;
                                            default:
                                                Stbuilder.Append("0" + "')");
                                                break;
                                        }
                                        break;
                                    case "System.Decimal":
                                    case "System.Int32":
                                    case "System.Int16":
                                    case "system.Int64":
                                        Stbuilder.Append("0" + "')");
                                        break;
                                    case "System.DateTime":
                                    case "System.DateTime2":
                                    case "System.Date":
                                        fecha = new DateTime(1950, 1, 1);
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECHASYS":
                                                Stbuilder.Append(Strings.Format(fecha, varini.pstForfecyHora) + "')");
                                                break;
                                            default:
                                                Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "')");
                                                break;
                                        }
                                        break;
                                    default:
                                        Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "')");
                                        break;
                                }
                            }
                            else
                            {
                                if (TipoDato == "System.DateTime" || TipoDato == "System.DateTime2" || TipoDato == "System.Date")
                                {
                                    switch (NombreColumna.ToUpper())
                                    {
                                        case "FECHASYS":
                                            Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.pstForfecyHora) + "')");
                                            break;
                                        default:
                                            Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.PstForFec) + "')");
                                            break;
                                    }
                                }
                                else
                                {
                                    switch (NombreColumna.ToUpper())
                                    {
                                        case "FECINTPROP":
                                            if (!Information.IsDate(DsDataset.Tables[0].Rows[varfil][varcol]))
                                                fecha = new DateTime(1950, 1, 1);
                                            else
                                                fecha = Convert.ToDateTime(DsDataset.Tables[0].Rows[varfil][varcol]);
                                            NombreColumna = "";
                                            Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "')");
                                            break;
                                        default:
                                            Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "')");
                                            break;
                                    }
                                }
                            }

                            ok = this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconect, "CreaActualizaAsociadoTodo");
                            if (nombretabla == "sys_maenit")
                            {
                                mysql = "update sys_maenit set nit=" + Convert.ToInt32(codigonuevo) + " where codigoter = '" + codigonuevo + "'";
                                this.OdbcConnect.ExecuteQueryconec(mysql, myconect, "CreaActualizaAsociadoTodo");
                            }
                        } // end for varfil
                        break;
                    } // end case 2

                    case 3:
                    {
                        DsDataset.AcceptChanges();
                        cuentafilas    = DsDataset.Tables[0].Rows.Count;
                        cuentacolumnas = DsDataset.Tables[0].Columns.Count;

                        for (varfil = 0; varfil <= cuentafilas - 1; varfil++)
                        {
                            System.Text.StringBuilder Stbuilder = new System.Text.StringBuilder();
                            Stbuilder.Append("insert into " + nombretabla + " values ('");

                            for (varcol = 0; varcol <= cuentacolumnas - 2; varcol++)
                            {
                                DsDataset.Tables[0].Rows[varfil]["codigoter"] = codigonuevo;
                                estanulo      = DsDataset.Tables[0].Rows[varfil].IsNull(varcol);
                                TipoDato      = DsDataset.Tables[0].Columns[varcol].DataType.ToString();
                                NombreColumna = DsDataset.Tables[0].Columns[varcol].ColumnName.ToString();

                                if (estanulo == true)
                                {
                                    switch (TipoDato)
                                    {
                                        case "System.String":
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECINTPROP":
                                                    fecha = new DateTime(1950, 1, 1);
                                                    Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                                                    NombreColumna = "";
                                                    break;
                                                default:
                                                    Stbuilder.Append("0" + "','");
                                                    break;
                                            }
                                            break;
                                        case "System.Decimal":
                                        case "System.Int32":
                                        case "System.Int16":
                                        case "system.Int64":
                                            Stbuilder.Append("0" + "','");
                                            break;
                                        case "System.DateTime":
                                        case "System.DateTime2":
                                        case "System.Date":
                                            fecha = new DateTime(1950, 1, 1);
                                            switch (NombreColumna.ToUpper())
                                            {
                                                case "FECHASYS":
                                                    Stbuilder.Append(Strings.Format(fecha, varini.pstForfecyHora) + "','");
                                                    break;
                                                default:
                                                    Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                                                    break;
                                            }
                                            break;
                                        default:
                                            Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "','");
                                            break;
                                    }
                                }
                                else
                                {
                                    if (TipoDato == "System.DateTime" || TipoDato == "System.DateTime2" || TipoDato == "System.Date")
                                    {
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECHASYS":
                                                Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.pstForfecyHora) + "','");
                                                break;
                                            default:
                                                Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.PstForFec) + "','");
                                                break;
                                        }
                                    }
                                    else
                                    {
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECINTPROP":
                                                if (!Information.IsDate(DsDataset.Tables[0].Rows[varfil][varcol]))
                                                    fecha = new DateTime(1950, 1, 1);
                                                else
                                                    fecha = Convert.ToDateTime(DsDataset.Tables[0].Rows[varfil][varcol]);
                                                NombreColumna = "";
                                                Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "','");
                                                break;
                                            default:
                                                Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "','");
                                                break;
                                        }
                                    }
                                }
                                cade = Stbuilder.ToString();
                            } // end for varcol

                            // last column (closes insert values)
                            estanulo      = DsDataset.Tables[0].Rows[varfil].IsNull(varcol);
                            TipoDato      = DsDataset.Tables[0].Columns[varcol].DataType.ToString();
                            NombreColumna = DsDataset.Tables[0].Columns[varcol].ColumnName.ToString();

                            if (estanulo == true)
                            {
                                switch (TipoDato)
                                {
                                    case "System.String":
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECINTPROP":
                                                fecha = new DateTime(1950, 1, 1);
                                                Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "')");
                                                NombreColumna = "";
                                                break;
                                            default:
                                                Stbuilder.Append("0" + "')");
                                                break;
                                        }
                                        break;
                                    case "System.Decimal":
                                    case "System.Int32":
                                    case "System.Int16":
                                    case "system.Int64":
                                        Stbuilder.Append("0" + "')");
                                        break;
                                    case "System.DateTime":
                                    case "System.DateTime2":
                                    case "System.Date":
                                        // VB uses existing fecha (already set to 1950/01/01 from null case above)
                                        switch (NombreColumna.ToUpper())
                                        {
                                            case "FECHASYS":
                                                Stbuilder.Append(Strings.Format(fecha, varini.pstForfecyHora) + "')");
                                                break;
                                            default:
                                                Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "')");
                                                break;
                                        }
                                        break;
                                    default:
                                        Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "')");
                                        break;
                                }
                            }
                            else
                            {
                                if (TipoDato == "System.DateTime" || TipoDato == "System.DateTime2" || TipoDato == "System.Date")
                                {
                                    switch (NombreColumna.ToUpper())
                                    {
                                        case "FECHASYS":
                                            Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.pstForfecyHora) + "')");
                                            break;
                                        default:
                                            Stbuilder.Append(Strings.Format(DsDataset.Tables[0].Rows[varfil][varcol], varini.PstForFec) + "')");
                                            break;
                                    }
                                }
                                else
                                {
                                    switch (NombreColumna.ToUpper())
                                    {
                                        case "FECINTPROP":
                                            if (!Information.IsDate(DsDataset.Tables[0].Rows[varfil][varcol]))
                                                fecha = new DateTime(1950, 1, 1);
                                            else
                                                fecha = Convert.ToDateTime(DsDataset.Tables[0].Rows[varfil][varcol]);
                                            NombreColumna = "";
                                            Stbuilder.Append(Strings.Format(fecha, varini.PstForFec) + "')");
                                            break;
                                        default:
                                            Stbuilder.Append(DsDataset.Tables[0].Rows[varfil][varcol] + "')");
                                            break;
                                    }
                                }
                            }

                            cade = Stbuilder.ToString();
                            ok = this.OdbcConnect.ExecuteQueryconec(Stbuilder.ToString(), myconect, "CreaActualizaAsociadoTodo");
                            // Commented out in VB source:
                            // if (nombretabla == "sys_maenit") { ... }
                        } // end for varfil
                        break;
                    } // end case 3

                    // case False in VB (default/no-op)
                    default:
                        break;

                } // end switch metodo
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            DsDataset2.Dispose();
            DsDataset.Dispose();

            return ok;
        }

    } // end partial class Clscartera
} // end namespace msgcop
