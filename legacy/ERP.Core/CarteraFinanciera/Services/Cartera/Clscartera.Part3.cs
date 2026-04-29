using ERP.Core.CarteraFinanciera.Forms;
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

        public void GrabaCapitalCdats(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, string usuario = "", string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoCdat = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;

            string _p1 = CptoCdat, _p2 = TipoMov.ToString(), _p3 = Nomres;
            string _p4 = Cuenta;

            if (CodMovto == "0")
            {
                stmysql = "select CptoCdats as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_SERVI = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoCdat; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalCdats", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoCdat = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }
            else
            {
                CptoCdat = CodMovto;
            }

            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2 from cop_codmov codmov where cod_movto = '" + CptoCdat + "'";
            _p1 = TipoMov.ToString(); _p2 = Nomres;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalCdats", ref _p1, ref _p2);
            int.TryParse(_p1, out TipoMov); Nomres = _p2;

            stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos; _p2 = Cuenta;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalCdats", ref _p1, ref _p2);
            cencos = _p1; Cuenta = _p2;

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            if (!ok)
            {
                MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Linea: " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalCdats", ref _p1);
            nit = _p1;

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoCdat, Ciclo, usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, Detalle, myconnect, "", "", "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro);

            GrabaSaldos(codigoter, LINCRED.ToString(), ConseCredito, periodo, debito, credito, myconnect);
        }

        public void GrabaInteresesCdats(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, string usuario = "", DateTime feccausacdtas = default(DateTime), string TrasladoCpto = " ")
        {
            if (feccausacdtas == default(DateTime)) feccausacdtas = new DateTime(1900, 1, 1);

            string stmysql;
            string CptoCdat = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CptoCdats as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_SERVI = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoCdat; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresesCdats", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoCdat = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }
            else
            {
                CptoCdat = CodMovto;
            }

            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2,cuenta as campo3 from cop_codmov codmov where cod_movto = '" + CptoCdat + "'";
            _p1 = TipoMov.ToString(); _p2 = Nomres;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresesCdats", ref _p1, ref _p2);
            int.TryParse(_p1, out TipoMov); Nomres = _p2;

            stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos; _p2 = Cuenta;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresesCdats", ref _p1, ref _p2);
            cencos = _p1; Cuenta = _p2;

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Linea: " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresesCdats", ref _p1);
            nit = _p1;

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoCdat, Ciclo, usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, Detalle, myconnect, "", "", "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", "", feccausacdtas);

            GrabaSaldos(codigoter, LINCRED.ToString(), ConseCredito, periodo, debito, credito, myconnect);
        }

        public void GrabaRetencion(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo,
            string Detalle, OdbcConnection myconnect, string CodMovto = "0", bool CambiaCpto = false, string Desprendible = " ",
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double VlrBase = 0, string Usuario = " ", string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoCdat = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CptoCdats as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_SERVI = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoCdat; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaRetencionCdats", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoCdat = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }
            else
            {
                CptoCdat = CodMovto;
            }

            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2, cuenta as campo3 from cop_codmov codmov where cod_movto = '" + CptoCdat + "'";
            _p1 = TipoMov.ToString(); _p2 = Nomres; _p3 = Cuenta;
            //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaRetencionCdats", ref _p1, ref _p2, ref _p3);
            int.TryParse(_p1, out TipoMov); Nomres = _p2; Cuenta = _p3;

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaRetencionCdats", ref _p1);
            cencos = _p1;

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no existe enla tabla de movimientos." + "\n" + " Linea: " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaRetencionCdats", ref _p1);
            nit = _p1;

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoCdat, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", VlrBase, nit, 0, Detalle, myconnect, Desprendible, "", "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro);
        }

        public void GrabaCapitalServicios(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, string CodMovto = "0", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ", string Marca_reliquidacion = "N")
        {
            string stmysql;
            string CptoApor = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_SERVI as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_SERVI = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoApor; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalServicios", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoApor = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }
            else
            {
                CptoApor = CodMovto;
            }

            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2 from cop_codmov codmov where cod_movto = '" + CptoApor + "'";
            _p1 = TipoMov.ToString(); _p2 = Nomres;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1, ref _p2);
            int.TryParse(_p1, out TipoMov); Nomres = _p2;

            stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos; _p2 = Cuenta;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalServicios", ref _p1, ref _p2);
            cencos = _p1; Cuenta = _p2;

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Linea: " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalServicios", ref _p1);
            nit = _p1;

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoApor, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, Detalle, myconnect, "", "", "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", "", default(DateTime), "", secuenciadepende, Marca_reliquidacion);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "C", Ciclo, periodo, myconnect);
            }

            GrabaSaldos(codigoter, LINCRED.ToString(), ConseCredito, periodo, debito, credito, myconnect);
        }

        public void GrabaCapital(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double IdSolCredito = 0,
            double secuenciadepende = 0, string TrasladoCpto = " ", string Marca_reliquidacion = "N")
        {
            string stmysql;
            string CptoCap = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre;
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;
            int CuoPen = 0;
            int plazo = 0;

            decimal Tasaint = 0;
            int Clacuo = 0;
            double Cuota = 0;
            int Periodicidad = 0;
            int Ciclodsto = 0;
            double Saldo = 0;
            DateTime FecUltPago = default(DateTime);
            decimal tasaseg = 0;
            decimal tasaadm = 0;
            string forseg = "0";
            string foradmon = "0";
            string Baseadmon = "0";
            double CuotaSeg = 0;
            double CuotaAdm = 0;
            double VLRCREDITO = 0;
            double VALSEGMIN = 0;
            double VALSEGMAX = 99999999;

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_CAPITAL as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_CAPITAL = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoCap; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoCap = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_CAPITAL = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = "0";
                //this.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1);
                debcre = _p1;
            }
            else
            {
                CptoCap = CodMovto;
            }

            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2 from cop_codmov codmov where cod_movto = '" + CptoCap + "'";
            _p1 = TipoMov.ToString(); _p2 = Nomres;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1, ref _p2);
            int.TryParse(_p1, out TipoMov); Nomres = _p2;

            stmysql = "select CENTROCO as campo1, CUENTA as campo2,FORADMON as campo3,CLAADMON as campo4 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos; _p2 = Cuenta; _p3 = Baseadmon; _p4 = foradmon;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1, ref _p2, ref _p3, ref _p4);
            cencos = _p1; Cuenta = _p2; Baseadmon = _p3; foradmon = _p4;

            stmysql = "select valsegMin as campo1, valsegMax as campo2   from cop_concar12 where lincred = " + LINCRED;
            _p1 = VALSEGMIN.ToString(); _p2 = VALSEGMAX.ToString();
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1, ref _p2);
            double.TryParse(_p1, out VALSEGMIN); double.TryParse(_p2, out VALSEGMAX);

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no  existe en el maestro de cuentas" + "\n" + " Linea : " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _p1);
            nit = _p1;

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            //this.BuscaObligacion(codigoter, LINCRED, ConseCredito, myconnect, "", ref Tasaint, 0, ref Clacuo, ref Cuota, ref tasaseg, ref tasaadm, ref Periodicidad, ref plazo, 0, 0, ref Ciclodsto, ref VLRCREDITO, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ref FecUltPago, 0, 0, 0, ref CuotaAdm, ref CuotaSeg, 0, 0, ref forseg);

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, (double)Tasaint, CptoCap, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, Detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", IdSolCredito, default(DateTime), "", secuenciadepende, Marca_reliquidacion);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "C", Ciclo, periodo, myconnect);
            }

            if (TipoMov == 1)
            {
                GrabaSaldos(codigoter, LINCRED.ToString(), ConseCredito, periodo, debito, credito, myconnect);
                if (FecUltPago < Fechamovto)
                {
                    this.GrabaFecUltPago(codigoter, (int)LINCRED, ConseCredito, Fechamovto, myconnect);
                }
            }

            this.BuscaSaldoObligacion(codigoter, (int)LINCRED, ConseCredito, periodo, myconnect, ref Saldo);

            //CuoPen = this.CalculaCuotasPagar(codigoter, LINCRED, ConseCredito, Periodicidad, Ciclodsto, Clacuo, Tasaint, Saldo, Cuota, periodo, forseg, tasaseg, CuotaSeg, foradmon, Baseadmon, tasaadm, CuotaAdm, myconnect, VLRCREDITO, VALSEGMIN, VALSEGMAX);
            //this.GrabaNumeroCuotas(codigoter, LINCRED, ConseCredito, periodo, CuoPen, CuoPen, myconnect);
        }

        private void GrabaExtra(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, int NUmextra, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoExt = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string cencos = "99999999";
            string nit = " ";
            DateTime FecUltPago = default(DateTime);

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_extra as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_extra = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoExt; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaExtra", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoExt = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }
            else
            {
                CptoExt = CodMovto;
            }

            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2 from cop_codmov codmov where cod_movto = '" + CptoExt + "'";
            _p1 = TipoMov.ToString(); _p2 = Nomres;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaExtra", ref _p1, ref _p2);
            int.TryParse(_p1, out TipoMov); Nomres = _p2;

            stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos; _p2 = Cuenta;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaExtra", ref _p1, ref _p2);
            cencos = _p1; Cuenta = _p2;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaExtra", ref _p1);
            nit = _p1;

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            // Only need FecUltPago from BuscaObligacion
            decimal _dummy_dec = 0; int _dummy_int = 0; double _dummy_dbl = 0;
            decimal _dummy_dec2 = 0; decimal _dummy_dec3 = 0; int _dummy_int2 = 0; int _dummy_int3 = 0; int _dummy_int4 = 0; double _dummy_dbl2 = 0;
            string _dummy_str = "0";
            //this.BuscaObligacion(codigoter, LINCRED, ConseCredito, myconnect, "", ref _dummy_dec, 0, ref _dummy_int, ref _dummy_dbl, ref _dummy_dec2, ref _dummy_dec3, ref _dummy_int2, ref _dummy_int3, 0, 0, ref _dummy_int4, ref _dummy_dbl2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, ref FecUltPago);

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoExt, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, NUmextra, Detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", 0, default(DateTime), "", secuenciadepende);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "EX", Ciclo, periodo, myconnect, NUmextra);
            }

            if (TipoMov == 10)
            {
                GrabaSaldos(codigoter, LINCRED.ToString(), ConseCredito, periodo, debito, credito, myconnect);
                GrabaSaldoExtra(codigoter, (int)LINCRED, ConseCredito, NUmextra, periodo, debito, credito, myconnect);
                if (FecUltPago < Fechamovto)
                {
                    //this.GrabaFecUltPago(codigoter, LINCRED, ConseCredito, Fechamovto, myconnect);
                }
            }
        }

        private void GrabaSaldoExtra(string codigoter, int lincred, double ConseCredito, int NumExtra, int periodo, double debito, double credito, OdbcConnection myconect)
        {
            bool ok=true;
            string stmysql;
            //ok = this.BuscaCuotaExtra(codigoter, lincred, ConseCredito, NumExtra, periodo, myconect);
            if (ok)
            {
                stmysql = "update cop_salextras set SALDO = SALDO_INICIAL + (vlr_debito + " + debito + ") - (vlr_credito + " + credito + ")"
                        + ", vlr_debito = vlr_debito + " + debito + ", vlr_credito = vlr_credito + " + credito
                        + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and Num_Extra = " + NumExtra + " and periodo = " + periodo;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaSaldoExtra");
            }
            else
            {
                stmysql = "insert into cop_salextras(codigoter, lincred, NUMERO, Num_Extra, PERIODO,saldo_inicial,saldo,vlr_debito, vlr_credito) values("
                    + "'" + codigoter + "','" + lincred + "','" + ConseCredito + "','" + NumExtra + "','" + periodo + "','" + "0" + "','" + (debito - credito) + "','" + debito + "','" + credito + "')";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaSaldoExtra");
            }
        }

        private void GrabaInteres(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoInt = "0";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre;
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;
            string CtaInt = "999999999999";

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_INTERES as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INTERES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoInt; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoInt = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INTERES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = "0";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
                debcre = _p1;
            }
            else
            {
                stmysql = "select cod_movto as campo1, tipo_movto as campo2, nomres as campo3, cuenta as campo4 from cop_codmov where cod_movto = '" + CodMovto + "'";
                _p1 = CptoInt; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoInt = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }

            stmysql = "select ctaintin as campo1  from cop_concar12 where lincred = " + LINCRED;
            _p1 = CtaInt;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            CtaInt = _p1;

            if (CtaInt != "999999999999" && CtaInt.Trim() != "" && CtaInt.Trim() != "0")
            {
                Cuenta = CtaInt;
            }

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            if (!ok)
            {
                MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Tipo Movimiento: " + CptoInt, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            cencos = _p1;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            nit = _p1;

           // this.InsertMovto(Comprobante, ConseComprobante, codigoter, (int)LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoInt, Ciclo.ToString(), Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "I", Ciclo, periodo, myconnect);
            }
        }

        private void GrabaInteresAnticipado(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoInt = "0";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre;
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;
            string CtaIntAnt = "999999999999";

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_INTERES as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INTERES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoInt; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresAnticipado", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoInt = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INTERES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = "0";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresAnticipado", ref _p1);
                debcre = _p1;
            }
            else
            {
                stmysql = "select cod_movto as campo1, tipo_movto as campo2, nomres as campo3, cuenta as campo4 from cop_codmov where cod_movto = '" + CodMovto + "'";
                _p1 = CptoInt; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresAnticipado", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoInt = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }

            stmysql = "select CTAINTAN as campo1  from cop_concar12 where lincred = " + LINCRED;
            _p1 = CtaIntAnt;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresAnticipado", ref _p1);
            CtaIntAnt = _p1;

            if (CtaIntAnt != "999999999999" && CtaIntAnt.Trim() != "" && CtaIntAnt.Trim() != "0")
            {
                Cuenta = CtaIntAnt;
            }

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            if (!ok)
            {
                MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Tipo Movimiento: " + CptoInt, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresAnticipado", ref _p1);
            cencos = _p1;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteresAnticipado", ref _p1);
            nit = _p1;

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoInt, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", 0, default(DateTime), "", secuenciadepende);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "I", Ciclo, periodo, myconnect);
            }
        }

        public void GrabaGmf(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoGmf = "0";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre;
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select Cpto4Mil as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INTERES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoGmf; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaGmf", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoGmf = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INTERES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = "0";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaGmf", ref _p1);
                debcre = _p1;
            }
            else
            {
                stmysql = "select cod_movto as campo1, tipo_movto as campo2, nomres as campo3, cuenta as campo4 from cop_codmov where cod_movto = '" + CodMovto + "'";
                _p1 = CptoGmf; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaGmf", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoGmf = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Tipo Movimiento: " + CptoGmf, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaGmf", ref _p1);
            cencos = _p1;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaGmf", ref _p1);
            nit = _p1;

           // this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoGmf, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro);
        }

        private void GrabaMora(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoMor = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre = " ";
            string cencos = "99999999";
            string nit = " ";
            bool ok = false;
            string CtaMora = "999999999999";

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_INMO as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INMO = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoMor; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaMora", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoMor = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_INMO = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = debcre;
                //this.ExecuteQueryconec(stmysql, myconnect, "GrabaMora", ref _p1);
                debcre = _p1;
            }
            else
            {
                stmysql = "select cod_movto as campo1, tipo_movto as campo2, nomres as campo3, cuenta as campo4 from cop_codmov where cod_movto = '" + CodMovto + "'";
                _p1 = CptoMor; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaMora", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoMor = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }

            stmysql = "select CTAINTMO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = CtaMora;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaMora", ref _p1);
            CtaMora = _p1;

            if (CtaMora != "999999999999" && CtaMora.Trim() != "" && CtaMora.Trim() != "0")
            {
                Cuenta = CtaMora;
            }

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            if (!ok)
            {
                MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Tipo Movimiento: " + CptoMor, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaMora", ref _p1);
            cencos = _p1;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaMora", ref _p1);
            nit = _p1;

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoMor, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", 0, default(DateTime), "", secuenciadepende);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "M", Ciclo, periodo, myconnect);
            }
        }

        private void GrabaAdmon(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoAdmon = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre;
            string cencos = "99999999";
            string nit = " ";
            bool ok;
            string CtaAdmon = "999999999999";

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_ADMON as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_ADMON = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoAdmon; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoAdmon = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_ADMON = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = "0";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
                debcre = _p1;
            }
            else
            {
                stmysql = "select cod_movto as campo1, tipo_movto as campo2, nomres as campo3, cuenta as campo4 from cop_codmov where cod_movto = '" + CodMovto + "'";
                _p1 = CptoAdmon; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoAdmon = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }

            stmysql = "select CTAINTOC as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = CtaAdmon;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaSeguro", ref _p1);
            CtaAdmon = _p1;

            if (CtaAdmon != "999999999999" && CtaAdmon.Trim() != "" && CtaAdmon.Trim() != "0")
            {
                Cuenta = CtaAdmon;
            }

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Tipo Movimiento: " + CptoAdmon, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            cencos = _p1;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            nit = _p1;

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoAdmon, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", 0, default(DateTime), "", secuenciadepende);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "A", Ciclo, periodo, myconnect);
            }
        }

        private void GrabaSeguro(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string detalle,
            OdbcConnection myconnect, string CodMovto = "0",
            string Idbenef = "99999999999999", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoSeguro = "00";
            int TipoMov = 0;
            string Nomres = " ";
            string Cuenta = "999999999999";
            string debcre;
            string cencos = "99999999";
            string nit = " ";
            string CtaSeguro = "999999999999";

            string _p1, _p2, _p3, _p4;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_SEGURO as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_SEGURO = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = CptoSeguro; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaSeguro", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoSeguro = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;

                stmysql = "select debcre as campo1 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_SEGURO = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                _p1 = "0";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaSeguro", ref _p1);
                debcre = _p1;
            }
            else
            {
                stmysql = "select cod_movto as campo1, tipo_movto as campo2, nomres as campo3, cuenta as campo4 from cop_codmov where cod_movto = '" + CodMovto + "'";
                _p1 = CptoSeguro; _p2 = TipoMov.ToString(); _p3 = Nomres; _p4 = Cuenta;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaSeguro", ref _p1, ref _p2, ref _p3, ref _p4);
                CptoSeguro = _p1; int.TryParse(_p2, out TipoMov); Nomres = _p3; Cuenta = _p4;
            }

            stmysql = "select CTAINTOD as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = CtaSeguro;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaSeguro", ref _p1);
            CtaSeguro = _p1;

            if (CtaSeguro != "999999999999" && CtaSeguro.Trim() != "" && CtaSeguro.Trim() != "0")
            {
                Cuenta = CtaSeguro;
            }

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            //if (!ok)
            //{
            //    MessageBox.Show("Cuenta contable no existe en el maestro de cuentas" + "\n" + " Tipo Movimiento: " + CptoSeguro, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return;
            //}

            stmysql = "select CENTROCO as campo1 from cop_concar12 where lincred = " + LINCRED;
            _p1 = cencos;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            cencos = _p1;

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            _p1 = nit;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaInteres", ref _p1);
            nit = _p1;

            //this.InsertMovto(Comprobante, ConseComprobante, codigoter, LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoSeguro, Ciclo, Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", 0, 0, 0, cencos, " ", 0, nit, 0, detalle, myconnect, "", Idbenef, "", "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "", 0, default(DateTime), "", secuenciadepende);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                GrabaCopmora(codigoter, (int)LINCRED, ConseCredito, debito, credito, "S", Ciclo, periodo, myconnect);
            }
        }

        public void GrabaSaldos(string codigoter, string lincred, double ConseCredito, int periodo, double Debito, double credito, OdbcConnection MyConnect)
        {
            string stmysql;
            bool ok;
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "select SALDO, SALDO_INICIAL,vlr_debito,vlr_credito,cuopen from cop_salmaecar where codigoter = '" + codigoter + "'"
                      + " and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo = " + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, MyConnect, "GrabaSaldos");
            if (ok)
            {
                stmysql = " update cop_salmaecar set saldo = saldo_inicial + (vlr_debito + " + Debito + ") - (vlr_credito + " + credito + "),"
                        + " Vlr_debito = vlr_debito + " + Debito + ", vlr_credito = vlr_credito + " + credito
                        + " where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + ConseCredito + " and PERIODO = " + periodo;
                this.OdbcConnect.ExecuteQueryconec(stmysql, MyConnect, "GrabaSaldos");
            }
            else
            {
                stmysql = "insert into  cop_salmaecar(codigoter, lincred,NUMERO,PERIODO,saldo_inicial,saldo,vlr_debito, vlr_credito) values("
                    + "'" + codigoter + "','" + lincred + "','" + ConseCredito + "','" + periodo + "','" + "0" + "','" + (Debito - credito) + "','" + Debito + "','" + credito + "')";
                this.OdbcConnect.ExecuteQueryconec(stmysql, MyConnect, "GrabaSaldos");
            }
            try
            {
                this.ActualizarCuaotaSalmaecar(codigoter, lincred, ConseCredito, periodo, MyConnect);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() + "Procedimiento Origen :  GrabaSaldos" + "\n" + "Asociado: " + codigoter + "-" + lincred + "-" + ConseCredito + " por favor mirar las columnas cuota,tasaint,ciclod,clades,periodd no esten nulos o tenga espacio ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void GrabaAjusteCausacion(string Comprobante, double ConseComprobante, string codigoter, int lincred, double ConseCredito, double debito, double credito, int TipoMovto, string Codmovto, int periodo_causa, int periodo_contable, DateTime FechaMovto, string Detalle, OdbcConnection myconect, int NumExtra = 0, string Usuario = null)
        {
            string Marca_reliquidacion = "N";
            switch (TipoMovto)
            {
                case 1:
                    if (lincred >= 1000 && credito > 0)
                    {
                        Marca_reliquidacion = "Y";
                    }
                    GrabaCapital(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, "99999999999999", Usuario, "99999999999999", " ", 0, 0, 0, " ", Marca_reliquidacion);
                    break;
                case 2:
                    GrabaInteres(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, "99999999999999", Usuario);
                    break;
                case 3:
                    GrabaMora(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, "99999999999999", Usuario);
                    break;
                case 4:
                    GrabaCapitalAportes(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, Usuario);
                    break;
                case 5:
                    GrabaSeguro(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, "99999999999999", Usuario);
                    break;
                case 6:
                    GrabaAdmon(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, "99999999999999", Usuario);
                    break;
                case 7:
                    this.GrabaCapitalAhorros(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, Usuario);
                    break;
                case 9:
                    this.GrabaCapitalServicios(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, Codmovto, Usuario);
                    break;
                case 10:
                    this.GrabaExtra(Comprobante, ConseComprobante, codigoter, lincred, ConseCredito, debito, credito, periodo_contable, FechaMovto, periodo_causa, Detalle, myconect, NumExtra, Codmovto, "99999999999999", Usuario);
                    break;
            }
        }

        public bool BuscaNovedadCausa(string codigoter, int lincred, double numero, int perido_casusac, OdbcConnection myconnect,
            ref string tipo_novedad, ref string motivo, ref string periodd, ref double cuotas, ref string capital, ref string interes,
            ref string extras, ref string cuotas_fijas, ref string autorizacion, ref DateTime fecha, ref string observa, ref string estado,
            ref DateTime FecCaduca, bool TodasObligaciones, ref string AplicaExtras, ref string Ahorros, ref string Servicios)
        {
            string whereClause = "";

            if (TodasObligaciones)
            {
                whereClause = "where codigoter ='" + codigoter + "' and periodo_causa = '" + perido_casusac + "'";
            }
            else
            {
                whereClause = "where codigoter ='" + codigoter + "' and lincred = '" + lincred + "' and numero = '" + numero + "' and periodo_causa = '" + perido_casusac + "'";
            }

            string _p1, _p2, _p3, _p4;

            stmysql = "select tipo_novedad as campo1,motivo as campo2,periodd as campo3,cuotas as campo4 from cop_caunov " + whereClause;
            _p1 = tipo_novedad; _p2 = motivo; _p3 = periodd; _p4 = cuotas.ToString();
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaNovedadCausa", ref _p1, ref _p2, ref _p3, ref _p4);
            tipo_novedad = _p1; motivo = _p2; periodd = _p3; double.TryParse(_p4, out cuotas);

            stmysql = "select capital as campo1,interes as campo2,extras as campo3,cuotas_fijas as campo4 from cop_caunov " + whereClause;
            _p1 = capital; _p2 = interes; _p3 = extras; _p4 = cuotas_fijas;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaNovedadCausa", ref _p1, ref _p2, ref _p3, ref _p4);
            capital = _p1; interes = _p2; extras = _p3; cuotas_fijas = _p4;

            stmysql = "select autorizacion as campo1,fecha as campo2,observa as campo3,estado as campo4 from cop_caunov " + whereClause;
            _p1 = autorizacion; _p2 = fecha.ToString(); _p3 = observa; _p4 = estado;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaNovedadCausa", ref _p1, ref _p2, ref _p3, ref _p4);
            autorizacion = _p1; try { fecha = Convert.ToDateTime(_p2); } catch { } observa = _p3; estado = _p4;

            stmysql = "select FecCaduca as campo1,aplicaExtras as campo2,ahorros as campo3,servicios as campo4 from cop_caunov " + whereClause;
            _p1 = FecCaduca.ToString(); _p2 = AplicaExtras; _p3 = Ahorros; _p4 = Servicios;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaNovedadCausa", ref _p1, ref _p2, ref _p3, ref _p4);
            try { FecCaduca = Convert.ToDateTime(_p1); } catch { } AplicaExtras = _p2; Ahorros = _p3; Servicios = _p4;

            if (motivo.Trim() == null || motivo.Trim() == "")
            {
                motivo = "0";
            }

            return ok;
        }

        // Overload without ref params
        public bool BuscaNovedadCausa(string codigoter, int lincred, double numero, int perido_casusac, OdbcConnection myconnect)
        {
            string tipo_novedad = " ", motivo = " ", periodd = " ", capital = " ", interes = " ";
            string extras = " ", cuotas_fijas = " ", autorizacion = " ", observa = " ", estado = " ";
            string AplicaExtras = " ", Ahorros = " ", Servicios = " ";
            double cuotas = 0;
            DateTime fecha = new DateTime(1950, 1, 1);
            DateTime FecCaduca = new DateTime(1950, 1, 1);
            return BuscaNovedadCausa(codigoter, lincred, numero, perido_casusac, myconnect,
                ref tipo_novedad, ref motivo, ref periodd, ref cuotas, ref capital, ref interes,
                ref extras, ref cuotas_fijas, ref autorizacion, ref fecha, ref observa, ref estado,
                ref FecCaduca, false, ref AplicaExtras, ref Ahorros, ref Servicios);
        }

        public bool GrabaNovedadCausa(string codigoter, int lincred, double numero, int perido_casusac, string tipo_novedad, string motivo,
            string periodd, double cuotas, string capital, string interes, string extras, string cuotas_fijas,
            DateTime fecha, string observa, string estado, DateTime FecCaduca, string usuario, OdbcConnection myconnect, string AplicaExtras, string ahorros, string servicios)
        {
            string nomusu = " ";

//            msgcofsys.BuscaUsuario(usuario, myconnect, "", ref nomusu);
            ok = this.BuscaNovedadCausa(codigoter, lincred, numero, perido_casusac, myconnect);

            if (!ok)
            {
                stmysql = "insert into cop_caunov (codigoter,lincred,numero,periodo_causa,fecha,observa,estado,capital,interes,extras,cuotas_fijas,tipo_novedad,cuotas,motivo,periodd,FecCaduca,usuario,nomusu,fecha_sys,aplicaExtras,ahorros,servicios) "
                    + "values ('" + codigoter + "'," + lincred + "," + numero + "," + perido_casusac + ",'" + Strings.Format(fecha, varini.PstForFec) + "','" + observa + "','" + estado + "','" + capital
                    + "','" + interes + "','" + extras + "','" + cuotas_fijas + "','" + tipo_novedad + "','" + cuotas + "','" + motivo + "','" + periodd + "','" + Strings.Format(FecCaduca, varini.PstForFec)
                    + "','" + usuario + "','" + nomusu + "','" + Strings.Format(DateTime.Now, varini.pstForfecyHora) + "','" + AplicaExtras + "','" + ahorros + "','" + servicios + "')";
            }
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaNovedadCausa");
            return ok;
        }

        public bool EliminarNovedadCausa(string codigoter, int lincred, double numero, int perido_casusac, OdbcConnection myconnect, string usuario, ref bool TodasObligaciones)
        {
            string mysql;
            string nomusu = " ";
//            msgcofsys.BuscaUsuario(usuario, myconnect, "", ref nomusu);
            if (TodasObligaciones)
            {
                stmysql = "delete from cop_caunov where codigoter ='" + codigoter + "' and periodo_causa = '" + perido_casusac + "' and estado='O'";
                mysql = "update cop_caunovaud set usuario_act='" + usuario + "', nomusu_act='" + nomusu + "' where codigoter ='" + codigoter + "' and periodo_causa = '" + perido_casusac + "'";
            }
            else
            {
                stmysql = "delete from cop_caunov where codigoter ='" + codigoter + "' and lincred = '" + lincred
                    + "' and numero = '" + numero + "' and periodo_causa = '" + perido_casusac + "' and estado='O'";
                mysql = "update cop_caunovaud set usuario_act='" + usuario + "', nomusu_act='" + nomusu + "' where codigoter ='" + codigoter + "' and lincred = '" + lincred
                    + "' and numero = '" + numero + "' and periodo_causa = '" + perido_casusac + "'";
            }
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarNovedadCausa");
            if (ok)
            {
                try
                {
                    this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "EliminarNovedadCausa(Actualiza Auditoria)");
                }
                catch (Exception ex)
                {
                }
            }
            return ok;
        }

        public bool GrabarNovedadxEmpresa(string codigo, int periodo, int periodo_causacion, string tipo_novedad, string motivo,
            string periodd, double cuotas, string capital, string interes, string extras, string cuotas_fijas,
            DateTime fecha, string observa, string estado, DateTime FecCaduca, string usuario, OdbcConnection myconnect,
            string AplicaExtras, string Cladesto, DataSet datos, string ahorros, string servicios)
        {
            StringBuilder stbuilder = new StringBuilder();
            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            string codigoterAnt = "", estadoAct = "";

            stbuilder.Append("select maecar.codigoter,maecar.lincred,maecar.numero,salmae.periodd from cop_maecar maecar ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred = car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter = maenit.codigoter ");
            stbuilder.Append("inner join cop_salmaecar salmae on maecar.codigoter = salmae.codigoter and maecar.lincred = salmae.lincred ");
            stbuilder.Append("and maecar.numero = salmae.numero and salmae.periodo = '" + periodo + "' ");
            stbuilder.Append("where maecar.empdsto='" + Strings.Right("0000" + codigo, 4) + "' and  car12.compri ='Y' ");
            stbuilder.Append("and ((maecar.lincred>=1000 and salmae.saldo>0) or (maecar.lincred<1000 and (salmae.cuota<>0 or salmae.saldo<>0))) ");
            stbuilder.Append(Cladesto.Trim() != "0" ? " and salmae.clades='" + Cladesto.Trim() + "'" : "");
            stbuilder.Append(" ORDER BY maecar.codigoter");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "GrabarNovedadxEmpresa", ref myReader02, "TblNovEmpresa");
            reg = myReader02.Tables["TblNovEmpresa"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myReader02.Tables["TblNovEmpresa"].Rows[tfila];
                if (codigoterAnt != row["codigoter"].ToString())
                {
                    codigoterAnt = row["codigoter"].ToString();
                    //msgconfig.BuscarEstadoAsociadoXPeriodo(row["codigoter"].ToString(), periodo, myconnect, "", ref estadoAct);
                    if (estadoAct == "R")
                    {
                        MessageBox.Show("Proceso NO realizado al Asociado " + row["codigoter"] + "\r\n" + " Motivo RETIRADO para periodo " + periodo, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                if (estadoAct != "R")
                {
                    if (datos.Tables.Count == 0)
                    {
                        ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), cuotas, capital,
                            interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, AplicaExtras, ahorros, servicios);
                    }
                    else
                    {
                        DataRow[] drFilter = datos.Tables[0].Select(" lincred = " + row["lincred"]);
                        int can = drFilter.Length;
                        if (can > 0)
                        {
                            string num = drFilter[0]["NumCuotas"].ToString();
                            if (drFilter[0]["NumCuotas"].ToString().Trim() != "")
                            {
                                ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), Convert.ToDouble(drFilter[0]["NumCuotas"].ToString()), capital,
                                    interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, drFilter[0]["extras"].ToString().ToUpper(), ahorros, servicios);
                            }
                        }
                    }
                }
                tfila += 1;
            }
            myReader02.Dispose();
            return ok;
        }

        public bool GrabarNovedadxAgencia(string Agencia, int periodo, int periodo_causacion, string tipo_novedad, string motivo,
            string periodd, double cuotas, string capital, string interes, string extras, string cuotas_fijas,
            DateTime fecha, string observa, string estado, DateTime FecCaduca, string usuario, OdbcConnection myconnect,
            string AplicaExtras, string Cladesto, DataSet datos, string ahorros, string servicios)
        {
            StringBuilder stbuilder = new StringBuilder();
            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            string codigoterAnt = "", estadoAct = "";

            stbuilder.Append("select maecar.codigoter,maecar.lincred,maecar.numero,salmae.periodd from cop_maecar maecar ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred = car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter = maenit.codigoter ");
            stbuilder.Append("inner join cop_salmaecar salmae on maecar.codigoter = salmae.codigoter and maecar.lincred = salmae.lincred ");
            stbuilder.Append("and maecar.numero = salmae.numero and salmae.periodo = '" + periodo + "' ");
            stbuilder.Append("where maenit.agencia='" + Strings.Right("0000" + Agencia, 4) + "' and  car12.compri ='Y' ");
            stbuilder.Append("and ((maecar.lincred>=1000 and salmae.saldo>0) or (maecar.lincred<1000 and (salmae.cuota<>0 or salmae.saldo<>0))) ");
            stbuilder.Append(Cladesto.Trim() != "0" ? " and salmae.clades='" + Cladesto.Trim() + "'" : "");
            stbuilder.Append(" ORDER BY maecar.codigoter");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "GrabarNovedadxAgencia", ref myReader02, "TblNovAgencia");
            reg = myReader02.Tables["TblNovAgencia"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myReader02.Tables["TblNovAgencia"].Rows[tfila];
                if (codigoterAnt != row["codigoter"].ToString())
                {
                    codigoterAnt = row["codigoter"].ToString();
                    //msgconfig.BuscarEstadoAsociadoXPeriodo(row["codigoter"].ToString(), periodo, myconnect, "", ref estadoAct);
                    if (estadoAct == "R")
                    {
                        MessageBox.Show("Proceso NO realizado al Asociado " + row["codigoter"] + "\r\n" + " Motivo RETIRADO para periodo " + periodo, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                if (estadoAct != "R")
                {
                    if (datos.Tables.Count == 0)
                    {
                        ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), cuotas, capital,
                            interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, AplicaExtras, ahorros, servicios);
                    }
                    else
                    {
                        DataRow[] drFilter = datos.Tables[0].Select(" lincred = " + row["lincred"]);
                        int can = drFilter.Length;
                        if (can > 0)
                        {
                            string num = drFilter[0]["NumCuotas"].ToString();
                            ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), Convert.ToDouble(drFilter[0]["NumCuotas"].ToString()), capital,
                                interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, drFilter[0]["extras"].ToString().ToUpper(), ahorros, servicios);
                        }
                    }
                }
                tfila += 1;
            }
            myReader02.Dispose();
            return ok;
        }

        public bool GrabarNovedadxCcosto(string Ccosto, int periodo, int periodo_causacion, string tipo_novedad, string motivo,
            string periodd, double cuotas, string capital, string interes, string extras, string cuotas_fijas,
            DateTime fecha, string observa, string estado, DateTime FecCaduca, string usuario, OdbcConnection myconnect,
            string AplicaExtras, string Cladesto, DataSet datos, string ahorros, string servicios)
        {
            StringBuilder stbuilder = new StringBuilder();
            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            string codigoterAnt = "", estadoAct = "";

            stbuilder.Append("select maecar.codigoter,maecar.lincred,maecar.numero,salmae.periodd from cop_maecar maecar ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred = car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter = maenit.codigoter ");
            stbuilder.Append("inner join cop_salmaecar salmae on maecar.codigoter = salmae.codigoter and maecar.lincred = salmae.lincred ");
            stbuilder.Append("and maecar.numero = salmae.numero and salmae.periodo = '" + periodo + "' ");
            stbuilder.Append("where maenit.cencosto='" + Strings.Right("00000000" + Ccosto, 8) + "' and  car12.compri ='Y' ");
            stbuilder.Append("and ((maecar.lincred>=1000 and salmae.saldo>0) or (maecar.lincred<1000 and (salmae.cuota<>0 or salmae.saldo<>0))) ");
            stbuilder.Append(Cladesto.Trim() != "0" ? " and salmae.clades='" + Cladesto.Trim() + "'" : "");
            stbuilder.Append(" ORDER BY maecar.codigoter");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "GrabarNovedadxCcosto", ref myReader02, "TblNovCCosto");
            reg = myReader02.Tables["TblNovCCosto"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myReader02.Tables["TblNovCCosto"].Rows[tfila];
                if (codigoterAnt != row["codigoter"].ToString())
                {
                    codigoterAnt = row["codigoter"].ToString();
                    //msgconfig.BuscarEstadoAsociadoXPeriodo(row["codigoter"].ToString(), periodo, myconnect, "", ref estadoAct);
                    if (estadoAct == "R")
                    {
                        MessageBox.Show("Proceso NO realizado al Asociado " + row["codigoter"] + "\r\n" + " Motivo RETIRADO para periodo " + periodo, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                if (estadoAct != "R")
                {
                    if (datos.Tables.Count == 0)
                    {
                        ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), cuotas, capital,
                            interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, AplicaExtras, ahorros, servicios);
                    }
                    else
                    {
                        DataRow[] drFilter = datos.Tables[0].Select(" lincred = " + row["lincred"]);
                        int can = drFilter.Length;
                        if (can > 0)
                        {
                            string num = drFilter[0]["NumCuotas"].ToString();
                            if (drFilter[0]["NumCuotas"].ToString().Trim() != "")
                            {
                                ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), Convert.ToDouble(drFilter[0]["NumCuotas"].ToString()), capital,
                                    interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, drFilter[0]["extras"].ToString().ToUpper(), ahorros, servicios);
                            }
                        }
                    }
                }
                tfila += 1;
            }
            myReader02.Dispose();
            return ok;
        }

        public bool GrabarNovedadxAsociado(string Codigoter, int periodo, int periodo_causacion, string tipo_novedad, string motivo,
            string periodd, double cuotas, string capital, string interes, string extras, string cuotas_fijas,
            DateTime fecha, string observa, string estado, DateTime FecCaduca, string usuario, OdbcConnection myconnect,
            string AplicaExtras, string Cladesto, DataSet datos, string ahorros, string servicios)
        {
            int sw1 = 0;
            StringBuilder stbuilder = new StringBuilder();
            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            string codigoterAnt = "", estadoAct = "";

            stbuilder.Append("select maecar.codigoter,maecar.lincred,maecar.numero,salmae.periodd,car12.codahor,salmae.saldo from cop_maecar maecar ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred = car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter = maenit.codigoter ");
            stbuilder.Append("inner join cop_salmaecar salmae on maecar.codigoter = salmae.codigoter and maecar.lincred = salmae.lincred ");
            stbuilder.Append("and maecar.numero = salmae.numero and salmae.periodo = '" + periodo + "' ");
            stbuilder.Append("where maecar.codigoter='" + Strings.Right("00000000000000" + Codigoter, 14) + "' and  car12.compri ='Y' ");
            stbuilder.Append("and ((maecar.lincred>=1000 and salmae.saldo>0) or (maecar.lincred<1000 and (salmae.cuota<>0 or salmae.saldo<>0))) ");
            stbuilder.Append(Cladesto.Trim() != "0" ? " and salmae.clades='" + Cladesto.Trim() + "'" : "");
            stbuilder.Append(" ORDER BY maecar.codigoter");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "GrabarNovedadxAsociado", ref myReader02, "TblNovAsociado");
            reg = myReader02.Tables["TblNovAsociado"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myReader02.Tables["TblNovAsociado"].Rows[tfila];
                if (codigoterAnt != row["codigoter"].ToString())
                {
                    codigoterAnt = row["codigoter"].ToString();
                    //msgconfig.BuscarEstadoAsociadoXPeriodo(row["codigoter"].ToString(), periodo, myconnect, "", ref estadoAct);
                    if (estadoAct == "R")
                    {
                        MessageBox.Show("Proceso NO realizado al Asociado " + row["codigoter"] + "\r\n" + " Motivo RETIRADO para periodo " + periodo, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                if (estadoAct != "R")
                {
                    sw1 = 0;
                    if (row["Codahor"].ToString() == "4")
                    {
                        if (Convert.ToDouble(row["SALDO"]) == 0)
                        {
                            sw1 = 1;
                        }
                    }

                    if (sw1 == 0)
                    {
                        if (datos.Tables.Count == 0)
                        {
                            ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), cuotas, capital,
                                interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, AplicaExtras, ahorros, servicios);
                        }
                        else
                        {
                            DataRow[] drFilter = datos.Tables[0].Select(" lincred = '" + row["lincred"] + "'");
                            int can = drFilter.Length;
                            if (can > 0)
                            {
                                string num = drFilter[0]["NumCuotas"].ToString();
                                if (drFilter[0]["NumCuotas"].ToString().Trim() != "")
                                {
                                    ok = this.GrabaNovedadCausa(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo_causacion, tipo_novedad, motivo, row["periodd"].ToString(), Convert.ToDouble(drFilter[0]["NumCuotas"].ToString()), capital,
                                        interes, extras, cuotas_fijas, fecha, observa, estado, FecCaduca, usuario, myconnect, drFilter[0]["extras"].ToString().ToUpper(), ahorros, servicios);
                                }
                            }
                        }
                    }
                }
                tfila += 1;
            }
            myReader02.Dispose();
            return ok;
        }

        public void GrabaCopmora(string codigoter, int lincred, double ConseCredito, double debito, double credito, string ClaseMovto, int periodo_causa, int periodo_contable, OdbcConnection myconect, int NumExtra = 0, int DiasMora = 0, DateTime FecLiqMora = default(DateTime))
        {
            if (FecLiqMora == default(DateTime)) FecLiqMora = new DateTime(1950, 1, 1);

            string stmysql;
            bool ok;
            string empresa = "99", agencia = "9999", ccosto = "99999999";
            bool okk;
            string PERIODD = "0", FechaFinal = "99999999";
            DateTime fechaVence;
            int NumeroExtra = NumExtra;

            string _p1, _p2, _p3, _p4;

            stmysql = "select codigoter as campo1,Nume_extra as campo2 from cop_copmora where codigoter = '" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
            _p1 = "0"; _p2 = NumExtra.ToString();
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora", ref _p1, ref _p2);
            int.TryParse(_p2, out NumExtra);

            if (NumExtra == 0)
            {
                NumExtra = NumeroExtra;
            }

            if (ok)
            {
                switch (ClaseMovto)
                {
                    case "C":
                        stmysql = "update cop_copmora  set SaldoCapital = Saldo_AntCapital + (Capital_causado + " + debito + ") - ( capital_abono + " + credito + "), capital_abono = capital_abono + " + credito + ", Capital_causado = Capital_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "I":
                        stmysql = "update cop_copmora  set  SaldoInteres = Saldo_AntInteres  + (Interes_causado + " + debito + ") - ( Interes_abono + " + credito + "), Interes_abono = Interes_abono + " + credito + ",Interes_causado = Interes_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "M":
                        stmysql = "update cop_copmora  set SaldoMora = Saldo_AntMora + (Mora_causado + " + debito + ") - ( Mora_abono + " + credito + "), Mora_abono = Mora_abono + " + credito + ", Mora_causado = Mora_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);

                        if (DiasMora > 0)
                        {
                            stmysql = "update cop_copmora  set DiasMora = DiasMora + " + DiasMora + ", FecUltLiq = '" + Strings.Format(FecLiqMora, varini.PstForFec) + "'"
                                + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                            this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        }
                        break;
                    case "S":
                        stmysql = "update cop_copmora  set SaldoSeguro = Saldo_AntSeguro + (Seguro_causado + " + debito + ") - ( Seguro_abono + " + credito + "), Seguro_abono = Seguro_abono + " + credito + ", Seguro_causado = Seguro_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "A":
                        stmysql = "update cop_copmora  set SaldoAdmon = Saldo_AntAdmon + (Admon_causado + " + debito + ") - ( Admon_abono + " + credito + "), Admon_abono = Admon_abono + " + credito + ",Admon_causado = Admon_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "0":
                        stmysql = "update cop_copmora  set SaldoOtros = Saldo_AntOtros + (Otros_causado + " + debito + ") - ( Otros_abono + " + credito + "), Otros_abono = Otros_abono + " + credito + ",Otros_causado = Otros_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "EX":
                        stmysql = "update cop_copmora  set SaldoExtra = Saldo_AntExtra + (Extra_causado + " + debito + ") - ( Extra_abono + " + credito + "), Extra_abono = Extra_abono + " + credito + ",Extra_causado = Extra_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable + " and Nume_extra =" + NumExtra;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                }
            }
            else
            {
                stmysql = "select maenit.empresa as campo1, maenit.agencia campo2, maenit.CENCOSTO as campo3, salmae.PERIODD as campo4 from cop_maecar copmae inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter "
                    + " inner join COP_SALMAECAR  salmae on salmae.codigoter = copmae.codigoter  and salmae.lincred=copmae.lincred  and salmae.numero=copmae.numero  and salmae.Periodo =" + periodo_contable
                    + " where copmae.codigoter = '" + codigoter + "' and copmae.lincred = " + lincred + " and copmae.numero = " + ConseCredito;
                _p1 = empresa; _p2 = agencia; _p3 = ccosto; _p4 = PERIODD;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora", ref _p1, ref _p2, ref _p3, ref _p4);
                empresa = _p1; agencia = _p2; ccosto = _p3; PERIODD = _p4;

                fechaVence = DateTime.Now;

                switch (ClaseMovto)
                {
                    case "C":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Capital_causado,capital_abono, SaldoCapital)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "I":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Interes_causado,Interes_abono, SaldoInteres)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "M":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Mora_causado,Mora_abono, SaldoMora)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "S":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Seguro_causado,Seguro_abono, SaldoSeguro)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "A":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Admon_causado,Admon_abono, SaldoAdmon)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                    case "0":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Otros_causado,Otros_abono, SaldoOtros)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto : " + ClaseMovto);
                        break;
                    case "EX":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Extra_causado,Extra_abono, SaldoExtra,Nume_extra)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + "," + NumExtra + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto : " + ClaseMovto);
                        break;
                }
                try
                {
                    this.GrabaCuopen(codigoter, lincred, ConseCredito, debito, credito, ClaseMovto, periodo_causa, periodo_contable, myconect, NumExtra);
                }
                catch (Exception ex)
                {
                }
            }
        }

        public void GrabaMoraEnDescuentos(string codigoter, int lincred, double ConseCredito, double debito, double credito, string ClaseMovto, int periodo_causa, int periodo_contable, OdbcConnection myconect, int NumExtra = 0, int DiasMora = 0, DateTime FecLiqMora = default(DateTime))
        {
            if (FecLiqMora == default(DateTime)) FecLiqMora = new DateTime(1950, 1, 1);

            string stmysql;
            bool ok;
            string empresa = "99", agencia = "9999", ccosto = "99999999";
            bool okk;
            string PERIODD = "0", FechaFinal = "99999999";
            DateTime fechaVence;
            int NumeroExtra = NumExtra;

            string _p1, _p2, _p3, _p4;

            stmysql = "select codigoter as campo1,Nume_extra as campo2 from cop_nomdes where codigoter = '" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
            _p1 = "0"; _p2 = NumExtra.ToString();
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora", ref _p1, ref _p2);
            int.TryParse(_p2, out NumExtra);

            if (NumExtra == 0)
            {
                NumExtra = NumeroExtra;
            }

            if (ok)
            {
                switch (ClaseMovto)
                {
                    case "M":
                        stmysql = "update cop_copmora  set SaldoMora = Saldo_AntMora + (Mora_causado + " + debito + ") - ( Mora_abono + " + credito + "), Mora_abono = Mora_abono + " + credito + ", Mora_causado = Mora_causado + " + debito
                            + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);

                        if (DiasMora > 0)
                        {
                            stmysql = "update cop_copmora  set DiasMora = DiasMora + " + DiasMora + ", FecUltLiq = '" + Strings.Format(FecLiqMora, varini.PstForFec) + "'"
                                + " where codigoter ='" + codigoter + "' and lincred = " + lincred + " and numero = " + ConseCredito + " and periodo_causa = " + periodo_causa + " and Periodo_contable = " + periodo_contable;
                            this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        }
                        break;
                }
            }
            else
            {
                stmysql = "select maenit.empresa as campo1, maenit.agencia campo2, maenit.CENCOSTO as campo3, salmae.PERIODD as campo4 from cop_maecar copmae inner join sys_maenit maenit on copmae.codigoter = maenit.codigoter "
                    + " inner join COP_SALMAECAR  salmae on salmae.codigoter = copmae.codigoter  and salmae.lincred=copmae.lincred  and salmae.numero=copmae.numero  and salmae.Periodo =" + periodo_contable
                    + " where copmae.codigoter = '" + codigoter + "' and copmae.lincred = " + lincred + " and copmae.numero = " + ConseCredito;
                _p1 = empresa; _p2 = agencia; _p3 = ccosto; _p4 = PERIODD;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora", ref _p1, ref _p2, ref _p3, ref _p4);
                empresa = _p1; agencia = _p2; ccosto = _p3; PERIODD = _p4;

                fechaVence = DateTime.Now;

                switch (ClaseMovto)
                {
                    case "M":
                        stmysql = "insert into cop_copmora(codigoter,lincred,numero,periodo_causa,periodo_contable,Mora_causado,Mora_abono, SaldoMora)"
                            + " values ('" + codigoter + "'," + lincred + "," + ConseCredito + "," + periodo_causa + "," + periodo_contable
                            + ", " + debito + "," + credito + ", " + (debito - credito) + ")";
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCopmora-ClaseMovto :" + ClaseMovto);
                        break;
                }
            }
        }

        private void GrabaCuopen(string codigoter, int lincred, double ConseCredito, double debito, double credito, string ClaseMovto, int periodo_causa, int periodo_contable,
            OdbcConnection myconect, int NumExtra = 0)
        {
            int Periode = 0;
            string FecFinal = " ";
            int mes = 0;
            string empresa = " ", CCOSTO = " ";
            ERP.Core.CarteraFinanciera.Models.ParamCop msgcarte = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            int clades = 0;
            int dias = 0;

            string _p1;

            //msgcarte.BuscaAsociado(codigoter, myconect, "", "", "", "", "", "", ref empresa, "", "", "", "", "", "", "", "", "", ref CCOSTO);
            ok = BuscaCuopen(codigoter, lincred, ConseCredito, periodo_causa, myconect);

            if (!ok)
            {
                //this.BuscaSaldoObligacion(codigoter, lincred, ConseCredito, periodo_contable, myconect, ref dummy_double, ref dummy_double2, ref dummy_double3, ref dummy_double4, ref Periode, ref clades);

                ok = BuscaParCausacion(periodo_causa.ToString(), Periode, myconect, "", ref FecFinal);
                if (!ok)
                {
                    if (Convert.ToInt32(periodo_causa.ToString().Substring(4, 2)) > 1)
                    {
                        mes = Convert.ToInt32(periodo_causa.ToString().Substring(4, 2)) / Periode;
                        mes = (int)Math.Round((double)mes, MidpointRounding.ToEven);
                    }
                    else
                    {
                        mes = 1;
                    }

                    double CicloDes, Resultado;

                    switch (Periode)
                    {
                        case 1: // MENSUAL
                            dias = DateTime.DaysInMonth(Convert.ToInt32(periodo_causa.ToString().Substring(0, 4)), mes);
                            break;
                        case 2: // QUINCENAL
                            CicloDes = Convert.ToInt32(periodo_causa.ToString().Substring(4, 2));
                            Resultado = CicloDes / 2;
                            if ((int)CicloDes % 2 != 0)
                            {
                                Resultado = Math.Round(Resultado + 0.1);
                                dias = 15;
                            }
                            else
                            {
                                Resultado = Math.Round(Resultado);
                                dias = DateTime.DaysInMonth(Convert.ToInt32(periodo_causa.ToString().Substring(0, 4)), (int)Resultado);
                            }
                            mes = (int)Resultado;
                            break;
                        case 3: // DECADAL
                            CicloDes = Convert.ToInt32(periodo_causa.ToString().Substring(4, 2));
                            Resultado = CicloDes / 3;
                            if (Resultado != Math.Round(Resultado))
                            {
                                Resultado += 0.5;
                            }
                            Resultado = Math.Round(Resultado);
                            mes = (int)Resultado;
                            switch ((int)CicloDes % 3)
                            {
                                case 1:
                                    dias = 10;
                                    break;
                                case 2:
                                    dias = 20;
                                    break;
                                case 0:
                                    dias = DateTime.DaysInMonth(Convert.ToInt32(periodo_causa.ToString().Substring(0, 4)), (int)Resultado);
                                    break;
                            }
                            break;
                        default:
                            dias = DateTime.DaysInMonth(Convert.ToInt32(periodo_causa.ToString().Substring(0, 4)), mes);
                            break;
                    }

                    FecFinal = new DateTime(Convert.ToInt32(periodo_causa.ToString().Substring(0, 4)), mes, dias).ToString();
                }

                ok = BuscaCuopen(codigoter, lincred, ConseCredito, periodo_causa, myconect);
                if (!ok)
                {
                    stmysql = "insert  into cop_cuopen(codigoter,lincred,numero,empresa,periodo_causa,PERIODO_CONTABLE,USUARIO,FECHA_SYSTEMA,FECHA_MOVTO,DETALLE,PERCIDAD,CICLO,CCOSTO,clades)"
                        + "values ('" + codigoter + "'," + lincred + "," + ConseCredito + ",'" + empresa + "'," + periodo_causa + "," + periodo_contable + ",'"
                        + varini.pstUsuario + "','" + Strings.Format(DateTime.Now, varini.PstForFec) + "','" + Strings.Format(Convert.ToDateTime(FecFinal), varini.PstForFec) + "','','" + Periode + "','" + periodo_causa + "','" + CCOSTO + "','" + clades + "')";
                    try
                    {
                        this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "GrabaCuopen");
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        public bool BuscaParCausacion(string periodo, int Periodicidad, OdbcConnection myconect, string FechaInicial, ref string FechaFinal)
        {
            string _p1 = FechaInicial ?? "";
            string _p2 = FechaFinal ?? "";
            stmysql = "select fecha_inicial as campo1,fecha_final as campo2  from cop_percau  where periodd  =" + Periodicidad + " and periodo_causa  =" + periodo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaParCausacion", ref _p1, ref _p2);
            FechaInicial = _p1; FechaFinal = _p2;
            return ok;
        }

        private bool BuscaCuopen(string codigoter, int lincred, double ConseCredito, int periodo_causa, OdbcConnection myconect)
        {
            stmysql = "select codigoter as  campo1  from cop_cuopen  where codigoter = '" + codigoter + "' and lincred = " + lincred + " and numero  = " + ConseCredito + " and periodo_causa = '" + periodo_causa + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "BuscaCuopen");
            return ok;
        }

        public void CargaPrioridades(DataGridView GrillaPrioridades, ref Array Prioridades, OdbcConnection myconnect, bool SoloCreditos = false)
        {
            int item;
            int Index = 0;
            string[] PrioTitulos = new string[11];
            PrioridadesAtomar Tipoprioridad;

            if (SoloCreditos)
            {
                Tipoprioridad = PrioridadesAtomar.SoloCreditos;
            }
            else
            {
                Tipoprioridad = PrioridadesAtomar.Todos;
            }

            Prioridades = BuscaPrioridad(myconnect, Tipoprioridad);
            GrillaPrioridades.Columns.Clear();
            GrillaPrioridades.Columns.Add("Linea", "Linea");
            GrillaPrioridades.Columns.Add("Obligacion", "Obligacion");
            GrillaPrioridades.Columns.Add("ForPag", "F");
            GrillaPrioridades.Columns.Add("Saldo", "Saldo");
            GrillaPrioridades.Columns.Add("Vlr Total", "Vlr Total");

            for (item = Prioridades.GetLowerBound(0); item <= Prioridades.GetUpperBound(0); item++)
            {
                if (((string)Prioridades.GetValue(item)) != "")
                {
                    GrillaPrioridades.Columns.Add((string)Prioridades.GetValue(item), (string)Prioridades.GetValue(item));
                    PrioTitulos[Index] = (string)Prioridades.GetValue(item);
                    Index += 1;
                }
            }

            Array.Clear(Prioridades, 0, Prioridades.GetUpperBound(0));
            Array.Copy(PrioTitulos, Prioridades, Index);

            Index = Array.FindIndex(PrioTitulos, 0, BuscaMora);

            GrillaPrioridades.Columns[1].Width = 110;
            GrillaPrioridades.Columns[2].Width = 20;
            GrillaPrioridades.Columns[3].Width = 130;
            GrillaPrioridades.Columns[4].Width = 110;
            ConfiguraGrilla(GrillaPrioridades);
            GrillaPrioridades.Columns[0].Width = 50;
            GrillaPrioridades.Columns[GrillaPrioridades.Columns.Count - 1].Width = 80;
        }

        private static bool BuscaMora(string s)
        {
            if (s != null && s.Trim() == "Capital")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ConfiguraGrilla(ref DataGridView GrillaMovtoCpte)
        {
            GrillaMovtoCpte.BorderStyle = BorderStyle.Fixed3D;
            GrillaMovtoCpte.EditMode = DataGridViewEditMode.EditOnEnter;
            GrillaMovtoCpte.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font(GrillaMovtoCpte.Font, System.Drawing.FontStyle.Bold);
            GrillaMovtoCpte.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            GrillaMovtoCpte.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        // Overload without ref for internal calls
        private void ConfiguraGrilla(DataGridView GrillaMovtoCpte)
        {
            GrillaMovtoCpte.BorderStyle = BorderStyle.Fixed3D;
            GrillaMovtoCpte.EditMode = DataGridViewEditMode.EditOnEnter;
            GrillaMovtoCpte.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font(GrillaMovtoCpte.Font, System.Drawing.FontStyle.Bold);
            GrillaMovtoCpte.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            GrillaMovtoCpte.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        public void CargaMovtoCpte(DataGridView GrillaMovtoCpte, string comprobante, string ConseCpte, OdbcConnection Myconect, Form forma)
        {
            string stmysql;
            double item = 0;
            string Orden = "asc";
            string Marca_reliquidacion = "N";
            ERP.Core.Compartido.Controles.Barraprogress barraprog = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Movimientos", forma);
            ConfiguraGrilla(GrillaMovtoCpte);

            //if (OdbcConnect.odbcConect.ordenatransaccion)
            //{
            //    Orden = "desc";
            //}

            stmysql = "Select a.codigoter, a.lincred, a.numero, a.cod_movto,a.ciclos,a.vlr_debito,a.vlr_credito,a.secuencia,a.nro_extra,c.apellido,c.nombre, b.NOMRES "
                + ",a.factura,a.cuenta,a.nit,a.cencos,a.Marca_reliquidacion  from cop_movimto a left join cop_codmov b on a.cod_movto = b.cod_movto left join sys_maenit c on a.codigoter = c.codigoter where compronte='"
                + comprobante + "' and numero_domto = '" + ConseCpte + "'"
                + " order by secuencia " + Orden;

            barraprog.ValorMinimoMaximo(0, 10);
            barraprog.Show();

            Application.DoEvents();
            GrillaMovtoCpte.Rows.Clear();
            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconect, "CargaMovtoCpte", ref myReader02, "TblCargaMovto");
            reg = myReader02.Tables["TblCargaMovto"].Rows.Count;

            barraprog.ValorMinimoMaximo(0, reg);
            if (Orden == "desc")
            {
                item = reg + 1;
            }

            for (tfila = 0; tfila <= reg - 1; tfila++)
            {
                DataRow row = myReader02.Tables["TblCargaMovto"].Rows[tfila];
                if (Orden == "desc")
                {
                    item = item - 1;
                }
                else
                {
                    item = item + 1;
                }

                if (Convert.IsDBNull(row["Marca_reliquidacion"]))
                {
                    Marca_reliquidacion = "N";
                }
                else
                {
                    Marca_reliquidacion = row["Marca_reliquidacion"].ToString();
                }

                GrillaMovtoCpte.Rows.Add(item, row["codigoter"], row["apellido"].ToString() + row["nombre"].ToString(), row["lincred"],
                    row["numero"], row["cod_movto"], row["nomres"], row["ciclos"], Strings.FormatNumber(Convert.ToDouble(row["vlr_debito"])),
                    Strings.FormatNumber(Convert.ToDouble(row["vlr_credito"])), row["secuencia"], row["nro_extra"], row["cuenta"], row["factura"],
                    row["nit"], row["cencos"], Marca_reliquidacion);

                if (Marca_reliquidacion == "Y")
                {
                    GrillaMovtoCpte.Rows[tfila].DefaultCellStyle.BackColor = System.Drawing.Color.LemonChiffon;
                }

                barraprog.PerformStep();
            }

            myReader02.Dispose();
            barraprog.Dispose();
            barraprog.Close();
        }

        public object Barraprogress(string Sql, string Titulo, FrmProgres pro, OdbcConnection myconect, string Comentario = "Espere por favor.")
        {
            pro.lblTittulo.Visible = true;
            pro.ControlBox = false;
            pro.Progress.Visible = true;
            pro.lblTittulo.Text = Titulo;
            pro.Progress.Minimum = 0;
            pro.Progress.Step = 1;
            DataSet ds = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(Sql, myconect, "Barraprogress", ref ds, "new");
            pro.Progress.Maximum = ds.Tables["new"].Rows.Count;
            return pro;
        }

        public Array BuscaPrioridad(OdbcConnection appconnect, PrioridadesAtomar TipoPrioridad = PrioridadesAtomar.Todos)
        {
            string stmysql;
            string[] PrioAplicar = new string[11];
            int PriCapi = 0, PriInt = 0, Priserv = 0, PriMora = 0, PriAdmon = 0, PriSeg = 0;
            int PriAportes = 0, PriAhorros = 0, PrioAfiliacion = 0;

            string _p1, _p2, _p3, _p4;

            stmysql = "select prior_capital as campo1, PRIOR_INTERES as campo2,PRIOR_SERVICIOS as campo3,PRIOR_MORA as campo4"
                + " FROM SYS_COMPANIA WHERE CODIGO = '" + varini.sptCodEmpr + "'";
            _p1 = PriCapi.ToString(); _p2 = PriInt.ToString(); _p3 = Priserv.ToString(); _p4 = PriMora.ToString();
            this.OdbcConnect.ExecuteQueryconec(stmysql, appconnect, " PagoAutomatico", ref _p1, ref _p2, ref _p3, ref _p4);
            int.TryParse(_p1, out PriCapi); int.TryParse(_p2, out PriInt); int.TryParse(_p3, out Priserv); int.TryParse(_p4, out PriMora);

            stmysql = "select PRIOR_ADMON as campo1,PRIOR_SEGURO as campo2,PRIOR_APORTES as campo3,PRIOR_AHORROS as campo4,PRIOR_AFILIACION as campo5"
                + " FROM SYS_COMPANIA WHERE CODIGO = '" + varini.sptCodEmpr + "'";
            _p1 = PriAdmon.ToString(); _p2 = PriSeg.ToString(); _p3 = PriAportes.ToString(); _p4 = PriAhorros.ToString();
            this.OdbcConnect.ExecuteQueryconec(stmysql, appconnect, " PagoAutomatico", ref _p1, ref _p2, ref _p3, ref _p4);
            int.TryParse(_p1, out PriAdmon); int.TryParse(_p2, out PriSeg); int.TryParse(_p3, out PriAportes); int.TryParse(_p4, out PriAhorros);

            stmysql = "select PRIOR_AFILIACION as campo1"
                + " FROM SYS_COMPANIA WHERE CODIGO = '" + varini.sptCodEmpr + "'";
            _p1 = PrioAfiliacion.ToString();
            this.OdbcConnect.ExecuteQueryconec(stmysql, appconnect, " PagoAutomatico", ref _p1);
            int.TryParse(_p1, out PrioAfiliacion);

            switch (TipoPrioridad)
            {
                case PrioridadesAtomar.Todos:
                    PrioAplicar[PriCapi] = "Capital";
                    PrioAplicar[PriInt] = "Interes";
                    PrioAplicar[PriMora] = "Mora";
                    PrioAplicar[PriAdmon] = "Admon";
                    PrioAplicar[PriSeg] = "Seguro";
                    PrioAplicar[PriAportes] = "Aportes";
                    PrioAplicar[PriAhorros] = "Ahorros";
                    PrioAplicar[Priserv] = "Servicio";
                    PrioAplicar[PrioAfiliacion] = "Afiliacion";
                    break;
                case PrioridadesAtomar.SoloAhorros:
                    PrioAplicar[PriAhorros] = "Ahorros";
                    break;
                case PrioridadesAtomar.SoloAportes:
                    PrioAplicar[PriAportes] = "Aportes";
                    break;
                case PrioridadesAtomar.SoloServicios:
                    PrioAplicar[Priserv] = "Servicio";
                    break;
                case PrioridadesAtomar.SoloCreditos:
                    PrioAplicar[PriCapi] = "Capital";
                    PrioAplicar[PriInt] = "Interes";
                    PrioAplicar[PriMora] = "Mora";
                    PrioAplicar[PriAdmon] = "Admon";
                    PrioAplicar[PriSeg] = "Seguro";
                    break;
            }

            return PrioAplicar;
        }

        public bool TrasladaContabilidad(string comprobante, double Consecutivo, OdbcConnection myconect, string usuario, string IdBenefCierre = "99999999999999", string banco = "9999", string NumCheque = "0", string IdBenefcheque = "99999999999999", bool ActualizaFacturas = true)
        {
            string ActContab = "1";
            DateTime FechaMovto = DateTime.Now;
            bool okter = false;
            bool ok;
            bool okk;
            double Periodo;
            double debito = 0, credito = 0;
            bool OkCierre = false;
            double debitos = 0, creditos = 0, dif = 0;
            string estado = "C";
            string Detalle = " ", Cuenta = "0", IdBenef = "99999999999999", NitIdbenef = "99999999999999";
            string ClaseDocAux = " ", NumDocAux = " ";
            DateTime FecVence = new DateTime(1950, 1, 1);
            string Detallefact = " ";
            string BenefCheque = "99999999999999";
            ERP.Core.Tesoreria.Services.clstesoreria msgtes = new ERP.Core.Tesoreria.Services.clstesoreria(usuario);
            string ApliTes = "N";
            string factura;
            cop_cuadredoc myform = new cop_cuadredoc(myconect);
            DialogResult MSGOK;
            string Cencosto = "99999999";
            string BancoConciliacion = "9999", TipoDConciliacion = "", NumDConciliacion_str = "0";
            double NumDConciliacion = 0, ValorConci = 0;
            char DebCred = ' ';
            string EmpresaConciliaBanca = "N", ConsiBanca = "N";
            bool GrabaConsiBanca = false;
            double SecuenciaMovto = 0;
            ERP.Core.CarteraFinanciera.Services.Debitos.ClsMsgDeb msgdeb = new ERP.Core.CarteraFinanciera.Services.Debitos.ClsMsgDeb();

            //ok = validarNitTrasladoConta(comprobante, Consecutivo, myconect);
            //if (ok)
            //{
            //    return false;
            //}

            //ok = validarCuentaContabilidad(comprobante, Consecutivo, myconect);
            //if (ok)
            //{
            //    return false;
            //}

            //this.BuscaComprobante(comprobante, Consecutivo, false, myconect, "", "", ref debitos, ref creditos, "", "", ref dif, ref Detalle, "", "", ref FechaMovto, ref ActContab, "", ref Cuenta, ref IdBenef, "", "", "", ref Cencosto);

            if (Cuenta != "999999999999" && dif != 0)
            {
                if (dif > 0)
                {
                    debito = dif;
                }
                else
                {
                    credito = dif * -1;
                }

                if (IdBenefCierre != "99999999999999")
                {
                    NitIdbenef = IdBenefCierre;
                }
                else
                {
                 //   this.BuscaAsociado(IdBenef, myconect, "", ref NitIdbenef);
                    if (NitIdbenef.Trim() == "")
                    {
                        return false;
                    }
                }

               // ok = msgcnt.BuscarCuenta(Cuenta, myconect, "", "", "", "", "", "", ref ApliTes, "", "", "", "", "", ref ConsiBanca, ref BancoConciliacion);
                if (ApliTes == "Y")
                {
                    //msgcnt.CargaDocAuxliliar(NitIdbenef, Cuenta, FechaMovto.ToString("yyyyMM"), myform, debito, credito, myconect, ref ClaseDocAux, ref NumDocAux, ref FecVence, ref Detallefact, Cencosto);
                    if (ClaseDocAux == "")
                    {
                        return false;
                    }
                }

                if (ConsiBanca == "Y")
                {
                    if (msgcnt.CargaTipodocConciliacion(myform, ref TipoDConciliacion, ref NumDConciliacion))
                    {
                        GrabaConsiBanca = true;
                    }
                    else
                    {
                        GrabaConsiBanca = false;
                        return false;
                    }
                }

                if (Detallefact == null || Detallefact.Trim() == "")
                {
                    Detallefact = Detalle;
                }

                //this.GrabaMovimiento(comprobante, Consecutivo, "99999999999999", "9999", 0, FechaMovto.ToString("yyyyMM"), 2, FechaMovto, debito, credito, Detalle, usuario, myconect, 0, Cuenta, NitIdbenef, ClaseDocAux + "-" + NumDocAux, "", "", "", "", ClaseDocAux, NumDocAux, Detallefact, FecVence, "", "", "", "", Cencosto);

                if (GrabaConsiBanca)
                {
                    if (NumDocAux == null || NumDocAux.Trim() == "")
                    {
                        NumDocAux = "0";
                    }
                    if (BuscarSecuenciaMovto(comprobante, Consecutivo, Cuenta, "9999", FechaMovto.ToString("yyyyMM"), NitIdbenef, Cencosto, FechaMovto, Detallefact,
                        NumDocAux, debito, credito, usuario, ClaseDocAux, myconect, ref SecuenciaMovto))
                    {
                        if (debito > 0)
                        {
                            ValorConci = debito;
                            DebCred = 'C';
                        }
                        else if (creditos > 0)
                        {
                            ValorConci = creditos;
                            DebCred = 'D';
                        }

                        //msgcnt.GrabaDocumentoConciliacion(Cuenta, BancoConciliacion, FechaMovto.ToString("yyyyMM"), TipoDConciliacion, NumDConciliacion, Detallefact,
                        //    ValorConci, DebCred, SecuenciaMovto, FechaMovto, myconect, "", "", "cop");
                    }
                }
            }

           // this.BuscaComprobante(comprobante, Consecutivo, false, myconect, "", "", ref debitos, ref creditos, "", "", ref dif, ref Detalle, "", "", ref FechaMovto, ref ActContab, "", ref Cuenta);

            if (dif != 0)
            {
                CuadreDocumento(comprobante, Consecutivo, usuario, myconect, Detalle);
            }

            //this.BuscaComprobante(comprobante, Consecutivo, false, myconect, "", "", ref debitos, ref creditos, "", "", ref dif, ref Detalle, "", "", ref FechaMovto, ref ActContab, "", "", ref IdBenef);

            if (dif != 0)
            {
                MessageBox.Show("Comprobante continua con diferencias, por favor revisar y traslade de nuevo ...", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            switch (ActContab)
            {
                case "0":
                case "1":
                case "2":
                    CierreDocumento(comprobante, Consecutivo, myconect);
                    return true;
            }

           // this.buscaPeriodo("cont", myconect, "", "", ref FechaMovto, ref estado, "", FechaMovto.ToString("yyyy"));

            switch (estado)
            {
                case "P":
                    MSGOK = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    if (MSGOK == DialogResult.No)
                    {
                        this.AbrirDocumento(comprobante, Consecutivo, myconect);
                        return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo de contabilidad esta cerrado ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.AbrirDocumento(comprobante, Consecutivo, myconect);
                    return false;
            }

            //this.BuscaAsociado(IdBenef, myconect, "", ref NitIdbenef);

            //ok = msgcnt.BuscaComprobante(comprobante, Consecutivo, false, myconect);
            //if (ok)
            //{
            //    MessageBox.Show("Comprobante ya existe en contabiidad, por favor revise y intente de nuevo", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //    return false;
            //}

            Periodo = Convert.ToDouble(FechaMovto.ToString("yyyyMM"));
            okter = RegistrosExigenTercero(comprobante, Consecutivo.ToString(), Periodo, FechaMovto, usuario, myconect, ref debito, ref credito, Detalle, NitIdbenef, ActualizaFacturas);
            ok = RegistrosExigenDocAuxiliar(comprobante, Consecutivo.ToString(), Periodo, FechaMovto, usuario, myconect, ref debito, ref credito, Detalle, NitIdbenef, ActualizaFacturas);
            okk = RegistroResumidos(comprobante, Consecutivo.ToString(), Periodo, FechaMovto, usuario, myconect, ref debito, ref credito, Detalle, NitIdbenef, ActualizaFacturas);

            if (IdBenefcheque != "99999999999999")
            {
                BenefCheque = IdBenefcheque;
            }
            else
            {
                BenefCheque = NitIdbenef;
            }

            if (ok || okk || okter)
            {
                msgcnt.GrabaDatosDocumentos(comprobante, Consecutivo, myconect, banco, NumCheque, BenefCheque);
                OkCierre = CierreDocumento(comprobante, Consecutivo, myconect);
            }

            //msgcnt.BuscaComprobante(comprobante, Consecutivo, false, myconect, "", "", "", "", "", "", ref dif);

            if (dif == 0)
            {
                msgcnt.CierreDocumento(comprobante, Consecutivo, myconect);
            }

            DataSet Dsdata = new DataSet();
            ok = this.msgcofsys.BuscarCompania(ref varini.sptCodEmpr, ref Dsdata, myconect);
            if (ok)
            {
                switch (Dsdata.Tables["tblcompania"].Rows[0]["tipconv"].ToString())
                {
                    case "1":
                    case "2":
                        msgdeb.InvocaTransaccionConvenioEnpactoCartera(comprobante, Consecutivo, usuario, myform, myconect);
                        break;
                }
            }

            return OkCierre;
        }

        private bool CierreDocumento(string comprobante, double Consecutivo, OdbcConnection myconnect)
        {
            double debitos = 0, creditos = 0, dif = 0;
            //this.BuscaComprobante(comprobante, Consecutivo, false, myconnect, "", "", ref debitos, ref creditos, "", "", ref dif);
            if (dif == 0)
            {
                string stmsql = " update cop_docmto set CERRADO = 'Y' where COMPRONTE = '" + comprobante + "' and NUMERO_DOMTO = " + Consecutivo;
                this.OdbcConnect.ExecuteQueryconec(stmsql, myconnect, "CierreDocumento");
                return true;
            }
            return false;
        }

        private bool AbrirDocumento(string comprobante, double Consecutivo, OdbcConnection myconnect)
        {
            double debitos = 0, creditos = 0, dif = 0;
            //this.BuscaComprobante(comprobante, Consecutivo, false, myconnect, "", "", ref debitos, ref creditos, "", "", ref dif);
            if (dif == 0)
            {
                string stmsql = " update cop_docmto set CERRADO = 'N' where COMPRONTE = " + comprobante + " and NUMERO_DOMTO = " + Consecutivo;
                this.OdbcConnect.ExecuteQueryconec(stmsql, myconnect, "AbrirDocumento");
                return true;
            }
            return false;
        }

        private bool AnulaDocumento(string comprobante, double Consecutivo, OdbcConnection myconnect, string DetalleAnulacion = " ")
        {
            double debitos = 0, creditos = 0, dif = 0;
            string ActualizaConta = "1";
            //this.BuscaComprobante(comprobante, Consecutivo, false, myconnect, "", "", ref debitos, ref creditos, "", "", ref dif, "", "", "", default(DateTime), ref ActualizaConta);

            if (dif == 0)
            {
                string stmsql = " update cop_docmto set ANULADO = 'Y', detalle = '" + DetalleAnulacion + "' where COMPRONTE = " + comprobante + " and NUMERO_DOMTO = " + Consecutivo;
                this.OdbcConnect.ExecuteQueryconec(stmsql, myconnect, "AnulaDocumento");
                if (ActualizaConta == "3")
                {
                    stmsql = " update cnt_docmto set ANULADO = 'Y', detalle = '" + DetalleAnulacion + "' where COMPRONTE = " + comprobante + " and NUMERO = " + Consecutivo;
                    this.OdbcConnect.ExecuteQueryconec(stmsql, myconnect, "AnulaDocumento");
                }
                return true;
            }
            return false;
        }

        public bool BuscarSecuenciaMovto(string compronte, double numero, string cuenta,
            string agencia, string periodo, string nit, string cencosto,
            DateTime FechaMovto, string detalle, string domto_auxiliar, double debito,
            double credito, string Usuario,
            string ClaseAux, OdbcConnection myconect, ref double secuencia)
        {
            stmysql = "select max(secuencia) as campo1 from cop_movimto where cuenta='" + cuenta + "' and COMPRONTE='" + compronte + "' and numero_domto=" + numero + " and agencia='" + agencia
                + "' and CENCOS='" + cencosto + "' and nit='" + nit + "' and periodo=" + periodo + " and fecha_movto='" + Strings.Format(FechaMovto, varini.PstForFec) + "' and detalle='" + detalle + "' and DOMTO_CRUCE='" + ClaseAux
                + "' and num_doc_cruce='" + domto_auxiliar + "' and VLR_DEBITO=" + debito + " and VLR_CREDITO=" + credito + " and Usuario='" + Usuario + "' group by secuencia";

            string _p1 = secuencia.ToString();
            //ok = this.ExecuteQueryconec(stmysql, myconect, "BuscarSecuenciaMovto", ref _p1);
            double.TryParse(_p1, out secuencia);
            return ok;
        }

        public void CuadreDocumento(string comprobante, double Consecutivo, string Usuario, OdbcConnection myconect, string detalleDocumento)
        {
            cop_cuadredoc CuadreDoc = new cop_cuadredoc(myconect);
            double debitos = 0, creditos = 0, dif = 0;
            DateTime fecha = new DateTime(1950, 1, 1);
            string detalle = " ";

            //this.BuscaComprobante(comprobante, Consecutivo, false, myconect, "", "", ref debitos, ref creditos, "", "", ref dif, ref detalle, "", "", ref fecha);

            //CuadreDoc.txtComprobante.Text = comprobante;
            //CuadreDoc.TxtConseCpte.Text = Consecutivo.ToString();
            //CuadreDoc.TxtDiferencia.Text = dif.ToString();
            //CuadreDoc.txtDebitos.Text = debitos.ToString();
            //CuadreDoc.TxtCreditos.Text = creditos.ToString();
            //CuadreDoc.DtpFecha.Value = fecha;
            //CuadreDoc.DtpFecha.Enabled = false;
            //CuadreDoc.Tag = Usuario;
            //if (dif > 0)
            //{
            //    CuadreDoc.txtDebito.Text = dif.ToString();
            //}
            //else
            //{
            //    CuadreDoc.txtCredito.Text = dif.ToString();
            //}
            //CuadreDoc.DetalleDocumento = detalleDocumento;
            //CuadreDoc.ShowDialog();
        }

        private bool RegistrosExigenDocAuxiliar(string comprobante, string ConseComprobante, double Periodo, DateTime fecha_movto, string Usuario, OdbcConnection AppadoConect,
            ref double Debito, ref double Credito, string detalle = "Compronte de Cartera", string Idbenef = "99999999999999", bool ActualizaFacturas = true)
        {
            string stmysql;
            bool ok = false;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();

            stmysql = "select cntmaec.cuenta, tercero,aux_domto,domto_cruce,num_doc_cruce,copmov.factura,copmov.detalle,copmov.fecvence, copmov.nit as nit, copmov.codigoter, copmov.agencia,copmov.cencos ,sum(VLR_DEBITO) as debito, sum(VLR_CREDITO) as credito, sum(base_reten) as base "
                + " from  cop_movimto copmov inner join cop_docmto copdoc on copmov.compronte = copdoc.compronte  and copmov.NUMERO_DOMTO =  copdoc.NUMERO_DOMTO inner join cnt_maecuen cntmaec on copmov.cuenta = cntmaec.cuenta "
                + " inner join sys_maenit maenit on copmov.codigoter = maenit.codigoter"
                + " where tercero = 'Y' and (aux_domto <> '0' AND aux_domto <> ' ') AND copmov.COMPRONTE = '" + comprobante + "' and copmov.NUMERO_DOMTO = " + ConseComprobante
                + " group by cntmaec.cuenta, tercero,aux_domto,domto_cruce,num_doc_cruce,copmov.factura,copmov.detalle,copmov.fecvence, copmov.codigoter, copmov.nit,  copmov.agencia, copmov.cencos "
                + " order by cntmaec.cuenta, tercero,aux_domto,domto_cruce,num_doc_cruce,copmov.factura,copmov.detalle,copmov.fecvence, copmov.codigoter, copmov.nit,copmov.agencia, copmov.cencos ";

            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            this.OdbcConnect.ExecuteQueryDataset(stmysql, AppadoConect, "RegistrosExigenDocAuxiliar", ref myReader02, "TblRegExiDocAux");

            for (tfila = 0; tfila <= myReader02.Tables["TblRegExiDocAux"].Rows.Count - 1; tfila++)
            {
                DataRow row = myReader02.Tables["TblRegExiDocAux"].Rows[tfila];
                //msgcntLocal.GrabaMovimiento(comprobante, ConseComprobante, row["cuenta"].ToString(), row["agencia"].ToString(), Periodo, row["nit"].ToString(), fecha_movto, detalle, row["num_doc_cruce"].ToString(), Convert.ToDouble(row["debito"].ToString()), Convert.ToDouble(row["credito"].ToString()), Convert.ToDouble(row["base"].ToString()), Usuario, AppadoConect, 0, row["factura"].ToString(), Idbenef, row["cencos"].ToString(), "", "", "", "", row["domto_cruce"].ToString(), row["fecvence"], row["detalle"].ToString(), "", "", "", "COP", ActualizaFacturas);
                ok = true;
                Debito = Debito + Convert.ToDouble(row["debito"].ToString());
                Credito = Credito + Convert.ToDouble(row["credito"].ToString());
            }

            myReader02.Dispose();
            return ok;
        }

        private bool RegistrosExigenTercero(string comprobante, string ConseComprobante, double Periodo, DateTime fecha_movto, string Usuario, OdbcConnection AppadoConect,
            ref double Debito, ref double Credito, string detalle = "Compronte de Cartera", string Idbenef = "99999999999999", bool ActualizaFacturas = true)
        {
            string stmysql;
            bool ok = false;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();

            stmysql = "select cntmaec.cuenta, tercero, copmov.nit as nit, copmov.codigoter, copmov.agencia,copmov.cencos ,sum(VLR_DEBITO) as debito, sum(VLR_CREDITO) as credito, sum(base_reten) as base"
                + " from  cop_movimto copmov  inner join cop_docmto copdoc on copmov.compronte = copdoc.compronte  and copmov.NUMERO_DOMTO =  copdoc.NUMERO_DOMTO "
                + " inner join cnt_maecuen cntmaec on copmov.cuenta = cntmaec.cuenta "
                + " inner join sys_maenit maenit on copmov.codigoter = maenit.codigoter"
                + " where tercero = 'Y' and (aux_domto = '0' or aux_domto = ' ') AND copmov.COMPRONTE = '" + comprobante + "' and copmov.NUMERO_DOMTO = " + Convert.ToDouble(ConseComprobante)
                + " group by cntmaec.cuenta, tercero, copmov.codigoter, copmov.nit,  copmov.agencia, copmov.cencos order by cntmaec.cuenta, tercero, copmov.codigoter, copmov.nit,copmov.agencia, copmov.cencos ";

            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            this.OdbcConnect.ExecuteQueryDataset(stmysql, AppadoConect, "RegistrosExigenTercero", ref myReader02, "TblRegExiTercero");

            for (tfila = 0; tfila <= myReader02.Tables["TblRegExiTercero"].Rows.Count - 1; tfila++)
            {
                DataRow row = myReader02.Tables["TblRegExiTercero"].Rows[tfila];
                //msgcntLocal.GrabaMovimiento(comprobante, ConseComprobante, row["cuenta"].ToString(), row["agencia"].ToString(), Periodo, row["nit"].ToString(), fecha_movto, detalle, " ", Convert.ToDouble(row["debito"].ToString()), Convert.ToDouble(row["credito"].ToString()), Convert.ToDouble(row["base"].ToString()), Usuario, AppadoConect, 0, "", Idbenef, row["cencos"].ToString(), "", "", "copc", "", "", default(DateTime), "", "", "", "", "COP", ActualizaFacturas);
                ok = true;
                Debito = Debito + Convert.ToDouble(row["debito"].ToString());
                Credito = Credito + Convert.ToDouble(row["credito"].ToString());
            }

            myReader02.Dispose();
            return ok;
        }

        private bool RegistroResumidos(string comprobante, string ConseComprobante, double Periodo, DateTime fecha_movto, string Usuario, OdbcConnection AppadoConect,
            ref double Debito, ref double Credito, string detalle = "Compronte de Cartera", string Idbenef = "99999999999999", bool ActualizaFacturas = true)
        {
            string stmysql;
            bool ok = false;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntLocal = new ERP.Core.Contabilidad.Services.ClsContabilidad();

            stmysql = "select cntmaec.cuenta, copmov.agencia, copmov.cencos ,sum(VLR_DEBITO) as debito, sum(VLR_CREDITO) as credito, sum(base_reten) as base "
                + " from  cop_movimto copmov inner join cnt_maecuen cntmaec on copmov.cuenta = cntmaec.cuenta "
                + " inner join sys_maenit maenit on copmov.codigoter = maenit.codigoter where  tercero = 'N' "
                + " AND COMPRONTE = '" + comprobante + "' and NUMERO_DOMTO = " + Convert.ToDouble(ConseComprobante) + " group by cntmaec.cuenta,copmov.agencia, copmov.cencos order by cntmaec.cuenta,copmov.agencia,copmov.cencos";

            int reg = 0, tfila = 0;
            DataSet myReader02 = new DataSet("Cartera");
            this.OdbcConnect.ExecuteQueryDataset(stmysql, AppadoConect, "RegistroResumidos", ref myReader02, "TblRegResumido");

            for (tfila = 0; tfila <= myReader02.Tables["TblRegResumido"].Rows.Count - 1; tfila++)
            {
                DataRow row = myReader02.Tables["TblRegResumido"].Rows[tfila];
                Application.DoEvents();
                //msgcntLocal.GrabaMovimiento(comprobante, ConseComprobante, row["cuenta"].ToString(), row["agencia"].ToString(), Periodo, " ", fecha_movto, detalle, " ", Convert.ToDouble(row["debito"].ToString()), Convert.ToDouble(row["credito"].ToString()), Convert.ToDouble(row["base"].ToString()), Usuario, AppadoConect, 0, "", Idbenef, row["cencos"].ToString(), "", "", "copc", "", "", default(DateTime), "", "", "", "", "COP", ActualizaFacturas);
                ok = true;
                Debito = Debito + Convert.ToDouble(row["debito"].ToString());
                Credito = Credito + Convert.ToDouble(row["credito"].ToString());
            }

            myReader02.Dispose();
            return ok;
        }

        public bool ValidaTipoMOvimiento(int TipoTransacion, int Lincred, OdbcConnection myconnect)
        {
            int Tipo_movto = 0;
            int TipoLinea = 0;
            //this.BuscaTipoMovto(TipoTransacion, myconnect, ref Tipo_movto);
            //this.BuscaLinea(Lincred, myconnect, "", "", "", "", ref TipoLinea);

            switch (TipoLinea)
            {
                case 1:
                    if (Tipo_movto != 4 && Tipo_movto != 3 && Tipo_movto != 9)
                    {
                        MessageBox.Show("Tipo de movimiento no permitido, para esta linea" + Lincred);
                        return false;
                    }
                    break;
                case 2:
                    if (Tipo_movto != 7 && Tipo_movto != 3 && Tipo_movto != 8 && Tipo_movto != 9 && Tipo_movto != 13)
                    {
                        MessageBox.Show("Tipo de movimiento no permitido, para esta linea" + Lincred);
                        return false;
                    }
                    break;
                case 3:
                    if (Tipo_movto != 1 && Tipo_movto != 3)
                    {
                        MessageBox.Show("Tipo de movimiento no permitido, para esta linea" + Lincred);
                        return false;
                    }
                    break;
                case 4:
                    if (Tipo_movto != 1 && Tipo_movto != 2 && Tipo_movto != 3 && Tipo_movto != 5 && Tipo_movto != 6 && Tipo_movto != 10 && Tipo_movto != 14)
                    {
                        MessageBox.Show("Tipo de movimiento no permitido, para esta linea" + Lincred);
                        return false;
                    }
                    break;
            }
            return true;
        }

        // Constructor duplicado eliminado (original en Clscartera.cs:34)
        // Destructor duplicado eliminado

    } // end partial class Clscartera
} // end namespace msgcop
