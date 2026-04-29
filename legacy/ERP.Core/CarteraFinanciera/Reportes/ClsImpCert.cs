using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using ERP.Core.CarteraFinanciera.Services.Cartera;
using ERP.Core.Compartido.Configuracion;
using ERP.Core.CarteraFinanciera.Models;
using ERP.Core.Compartido.Utilidades;
using ERP.Core.Compartido.Reportes;
using ERP.Core.Compartido.Datos;

namespace ERP.Core.CarteraFinanciera.Reportes
{
    public class ClsImpCert
    {
        private Clscartera msgcop = new Clscartera();
        private ClsConect MyOdbcConet = new ClsConect();
        private ClsConect.odbcConect varini = new ClsConect.odbcConect();
        private config_report config = new config_report();
        private ParamSys paramsys = new ParamSys();
        private ParamCop paramcop = new ParamCop();
        private string stmysql = null;
        private bool ok;

        public ClsImpCert()
        {
            MyOdbcConet.MyOdbcConect(varini);
        }

        ~ClsImpCert()
        {
        }

        public void ImprimirCertificados(string codigoter, int Lincred, double numero, int periodo,
            Form forma, double valoradicional, DateTime fechavigencia, string Cuenta,
            string banco, string email, OdbcConnection myconnect)
        {
            int TipoLinea = 0;
            if (Lincred == 0 && numero == 0)
            {
                TipoLinea = 4;
            }
            else
            {
                this.msgcop.BuscaLinea(Lincred, myconnect, ref TipoLinea);
            }

            switch (TipoLinea)
            {
                case 1:
                    // Certificados de aportes
                    break;
                case 2:
                    // Certificados de ahorro
                    break;
                case 4:
                    BuscarInfoCertificadoSaldo(codigoter, Lincred, numero, periodo, forma, valoradicional, fechavigencia, Cuenta, banco, email, myconnect);
                    break;
                case 6:
                    // Certificados de cdats
                    break;
            }
        }

        private void ImprimirCertificadoSaldo(string cedula, string nombreaso, string libranza, double SaldoaCap, double CuotaMenObli,
            double Aportes, double CuotaMensFija, Form forma, string nit, string direccion, string telefono, string nombreemp,
            string ciudad, string depto, string jefecartera, DateTime FechaVigencia, string Cuenta,
            string banco, string email)
        {
            string val_letras;
            Numeros_A_Letras num = new Numeros_A_Letras();
            reporte rep = new reporte("cop_rcertsaldo");

            rep.SetParameterValue("cedula", cedula);
            rep.SetParameterValue("nombre", nombreaso);
            rep.SetParameterValue("libranza", libranza.Split(','));
            rep.SetParameterValue("saldo", SaldoaCap);
            rep.SetParameterValue("cuota", CuotaMenObli);
            rep.SetParameterValue("Aportes", Aportes);
            rep.SetParameterValue("Cuotafija", CuotaMensFija);
            rep.SetParameterValue("nit", nit);
            rep.SetParameterValue("direccion", direccion);
            rep.SetParameterValue("telefono", telefono);
            rep.SetParameterValue("empresa", nombreemp);
            rep.SetParameterValue("ciudad", ciudad);
            rep.SetParameterValue("depto", depto);
            rep.SetParameterValue("jefecartera", jefecartera);
            val_letras = num.Num_a_Letras(Convert.ToDouble(SaldoaCap)) + "M/CTE.";
            rep.SetParameterValue("saldoletras", val_letras);
            val_letras = num.Num_a_Letras(Convert.ToDouble(CuotaMenObli)) + "M/CTE.";
            rep.SetParameterValue("cuotaletras", val_letras);
            val_letras = num.Num_a_Letras(Convert.ToDouble(Aportes)) + "M/CTE.";
            rep.SetParameterValue("Aportesletras", val_letras);
            val_letras = num.Num_a_Letras(Convert.ToDouble(CuotaMensFija)) + "M/CTE.";
            rep.SetParameterValue("fijosletras", val_letras);
            rep.SetParameterValue("FechaVigencia", FechaVigencia);
            rep.SetParameterValue("Cuenta", Cuenta);
            rep.SetParameterValue("Banco", banco);
            rep.SetParameterValue("Email", email);

            this.config.confi_reportes(forma, rep);
        }

        private void BuscarInfoCertificadoSaldo(string codigoter, int Lincred, double numero, int periodo, Form forma,
             double valoradicional, DateTime fechavigencia, string Cuenta,
            string banco, string email, OdbcConnection myconnect)
        {
            DataSet dtdatos = new DataSet();
            string Nit = "0", NomEmp = " ", JefeCartera = " ";
            string direccion = "0", telefono = "0", ciudad = " ", dpto = " ";
            string Libranza = "";
            double SaldoMoraCapital = 0, MoraCuotaFijos = 0, Aportes = 0, CuotaMens = 0;
            double cuota = 0, saldo = 0;
            string nombre = " ", cedula = "0";
            double SaldoACapital = 0, PagoMens = 0;
            int i = 0;

            stmysql = "select maecar.NUMERO as Libranza,saldo.cuota,saldo.saldo,saldo.periodd," +
                "sum(mora.SaldoExtra+mora.SaldoInteres+mora.SaldoMora+mora.SaldoSeguro+mora.SaldoAdmon+mora.SaldoOtros) as MoraObligacion," +
                "(select sum(fijos.Saldocapital+fijos.SaldoExtra+fijos.SaldoInteres+fijos.SaldoMora+fijos.SaldoSeguro+fijos.SaldoAdmon+fijos.SaldoOtros) " +
                "from cop_copmora_vw fijos where maecar.codigoter=fijos.codigoter and fijos.lincred<1000 and fijos.periodo_contable=" + periodo + ") as MoraFijos," +
                "(select sum(salmaecar.cuota*salmaecar.periodd) from cop_salmaecar salmaecar where maecar.codigoter = salmaecar.codigoter and salmaecar.lincred<1000  and salmaecar.periodo=" + periodo + ") as CuotaDstoFijo," +
                "(select sum(sal.saldo) from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor='1' " +
                " where maecar.codigoter =sal.codigoter and sal.periodo=" + periodo + ")  as saldoAporte " +
                "from cop_maecar maecar " +
                "left join cop_salmaecar saldo on maecar.codigoter=saldo.codigoter and " +
                "maecar.lincred = saldo.lincred And maecar.numero = saldo.numero And saldo.periodo =" + periodo +
                " left join cop_copmora_vw mora on maecar.codigoter=mora.codigoter and " +
                "maecar.lincred = mora.lincred And maecar.numero = mora.numero And mora.periodo_contable =" + periodo +
                " where maecar.codigoter='" + codigoter + "'" + (Lincred == 0 ? " and maecar.lincred>=1000 " : " and maecar.lincred=" + Lincred + " and maecar.numero=" + numero) + " and saldo.saldo<>0 " +
                " group by maecar.codigoter,maecar.NUMERO,saldo.cuota,saldo.saldo,saldo.periodd";

            ok = this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "BuscarInfoCertificadoSaldo", dtdatos, "TblCertificado");

            if (ok)
            {
                //this.paramsys.BuscarCompania(varini.sptCodEmpr, myconnect, ref NomEmp, ref Nit, ref direccion, ref telefono, ref ciudad, ref dpto, ref JefeCartera);
                //this.paramcop.BuscaAsociado(codigoter, myconnect, ParamCop.Navega.Ninguno, ref cedula, ref nombre);

                DataTable table = dtdatos.Tables["TblCertificado"];
                while (i < table.Rows.Count)
                {
                    Libranza = Libranza + (table.Rows[i]["Libranza"] == DBNull.Value ? "" : table.Rows[i]["Libranza"].ToString() + ",");

                    double cuotaVal = table.Rows[i]["cuota"] == DBNull.Value ? 0 : Convert.ToDouble(table.Rows[i]["cuota"]);
                    double perioddVal = 1;
                    if (table.Rows[i]["periodd"] != DBNull.Value)
                    {
                        double.TryParse(table.Rows[i]["periodd"].ToString(), out perioddVal);
                        if (perioddVal == 0) perioddVal = 1;
                    }
                    cuota += cuotaVal * perioddVal;

                    saldo += table.Rows[i]["saldo"] == DBNull.Value ? 0 : Convert.ToDouble(table.Rows[i]["saldo"]);
                    SaldoMoraCapital += table.Rows[i]["MoraObligacion"] == DBNull.Value ? 0 : Convert.ToDouble(table.Rows[i]["MoraObligacion"]);
                    MoraCuotaFijos = table.Rows[i]["MoraFijos"] == DBNull.Value ? 0 : Convert.ToDouble(table.Rows[i]["MoraFijos"]);
                    Aportes = table.Rows[i]["saldoAporte"] == DBNull.Value ? 0 : Convert.ToDouble(table.Rows[i]["saldoAporte"]);
                    CuotaMens = table.Rows[i]["CuotaDstoFijo"] == DBNull.Value ? 0 : Convert.ToDouble(table.Rows[i]["CuotaDstoFijo"]);
                    i = i + 1;
                }

                if (Libranza.Length > 0)
                {
                    Libranza = Libranza.Substring(0, Libranza.Length - 1);
                }
                MoraCuotaFijos = MoraCuotaFijos < 0 ? MoraCuotaFijos * -1 : MoraCuotaFijos;
                Aportes = Aportes < 0 ? Aportes * -1 : Aportes;
                SaldoMoraCapital = SaldoMoraCapital < 0 ? SaldoMoraCapital * -1 : SaldoMoraCapital;
                saldo = saldo < 0 ? saldo * -1 : saldo;
                saldo = saldo + cuota + CuotaMens + valoradicional;
                SaldoACapital = saldo + SaldoMoraCapital + MoraCuotaFijos;
                PagoMens = cuota + CuotaMens;

                ImprimirCertificadoSaldo(cedula, nombre, Libranza, SaldoACapital, PagoMens, Aportes, CuotaMens, forma, Nit, direccion, telefono, NomEmp, ciudad, dpto, JefeCartera, fechavigencia, Cuenta, banco, email);
            }
        }
    }
}
