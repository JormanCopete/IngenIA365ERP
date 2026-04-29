using ERP.Core.Contabilidad.Helpers;
using ERP.Core.Contabilidad.Services;
// Traducción de: ImpreDoc.vb  →  ImpreDoc.cs
// Proyecto: MsgImpCnt  (VB.NET 2005 → C# .NET 3.5)
using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing.Printing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Contabilidad.Reportes
{
    public class ImpreDoc
    {
        // ----------------------------------------------------------------
        // Campos privados
        // ----------------------------------------------------------------
        private string pstAsterisco = "XXXXXXXXXXXXX XXXXXXXXXXXXXXXXX XXXXXXXXXXXXXX XXXXXXXXXXX XXXXXXXXX XXXXXXXXXX";
        private string UsuarioDoc;
        private ERP.Core.CarteraFinanciera.Models.ParamCop sas = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------
        public ImpreDoc(string user)
        {
            VarIni.sptCodEmpr = "0001";
        }

        ~ImpreDoc()
        {
        }

        // ----------------------------------------------------------------
        // CargaEmpresa — usa el overload DataSet de BuscarCompania
        // ----------------------------------------------------------------
        private void CargaEmpresa(OdbcConnection Myconect)
        {
            DataSet datos = new DataSet();
            string _codEmpr = VarIni.sptCodEmpr;
            msgparsys.BuscarCompania(ref _codEmpr, ref datos, Myconect);

            if (datos.Tables["tblcompania"].Rows.Count > 0)
            {
                DataRow row = datos.Tables["tblcompania"].Rows[0];
                VarIni.pstCptocuan = row["sobra"].ToString();
                VarIni.stnit       = row["nit"].ToString();
                VarIni.pstdiremp   = row["direccion"].ToString();
                VarIni.psttelemp   = row["telefono"].ToString();
                VarIni.pstciuemp   = row["ciudad"].ToString();
                VarIni.pstRepre    = row["REPRE"].ToString();
                VarIni.pstRev_fis  = row["REV_FIS"].ToString();
                VarIni.pstMat_rev  = row["MAT_REV"].ToString();
                VarIni.pstconta    = row["CONTA"].ToString();
                VarIni.pstMat_con  = row["MAT_CON"].ToString();
                VarIni.pstUndred   = row["UNI_RED"].ToString();
                VarIni.pstEmpresa  = row["nombre"].ToString();
            }
        }

        // ================================================================
        // impre — primer overload (un documento)
        // ================================================================
        public object impre(string combte, string comp_nume, bool copia, string tipo,
            OdbcConnection Myconect,
            string StBanco = "", string forma_imp = "0", string CantCopias = "0",
            bool DesdeAhorros = false, string NumCuentasAhorros = "")
        {
            try
            {
                VarIni.sptCodEmpr = "0001";
                CargaEmpresa(Myconect);

                double valor_deb = 0, valor_cre = 0, cheque = 0;
                string cuentabanco = " ", PideImpresora = " ";
                string codigot = "", nombres = "", nit = "", banco = "", doctipo = "";
                DateTime fecha = DateTime.MinValue;
                int canreg = 0, fila = 0;

                string sql =
                    "SELECT f.nombre,case d.tipo_persona when  'N' then d.nombre else d.razon_social end as nombreter" +
                    " ,a.cerrado,a.compronte,a.domto,a.banco,a.cheque,a.NUMERO,a.detalle,a.fecha" +
                    " ,b.cuenta,a.idbenef as nit,a.idbenefcheque ,d.nit as codi,c.nombre as nombre_cuenta,b.vlr_debito,b.vlr_credito,b.usuario" +
                    " FROM cnt_docmto a left join cnt_movimto b" +
                    " on b.compronte = a.compronte and" +
                    " b.NUMERO = a.NUMERO inner join cnt_maecuen c" +
                    " on c.cuenta = b.cuenta left join cnt_nit d" +
                    " on d.nit = b.nit left join cnt_nit f" +
                    " on f.nit = a.idbenef" +
                    " WHERE (a.COMPRONTE ='" + Strings.Right("0000" + combte, 4) + "') AND (a.NUMERO = '" + comp_nume.Trim() + "')" +
                    " order by b.cuenta asc";

                codigot = "";
                nombres = "";

                DataSet read = new DataSet();
                connect.ExecuteQueryDataset(sql, Myconect, "impre", ref read, "TblImpre");
                canreg = read.Tables["TblImpre"].Rows.Count;

                while (fila < canreg)
                {
                    DataRow r = read.Tables["TblImpre"].Rows[fila];

                    if (r["cerrado"].ToString() != "Y")
                    {
                        MessageBox.Show("El documento no esta cerrado.", "SOLIDO", MessageBoxButtons.OKCancel);
                        return null;
                    }

                    if (tipo == "CP" || tipo == "PA")
                    {
                        if (Strings.Right("0000" + r["banco"].ToString(), 4) != "0000")
                        {
                            string _banco = r["banco"].ToString();
                            BuscaBancoGetCuenta(ref _banco, Myconect, ref cuentabanco);
                            if (cuentabanco == r["cuenta"].ToString())
                                valor_cre += Convert.ToDouble(r["vlr_credito"]);
                        }
                    }
                    else
                    {
                        valor_deb += Convert.ToDouble(r["vlr_debito"]);
                    }

                    UsuarioDoc = r["usuario"].ToString();
                    nit = DesdeAhorros
                        ? r["idbenefcheque"].ToString().Trim()
                        : r["nit"].ToString().Trim();
                    codigot = nit;
                    nombres = r["nombre"].ToString();
                    banco   = Strings.Right("0000" + r["banco"].ToString(), 4);
                    fecha   = Convert.ToDateTime(r["fecha"]);

                    if (!Information.IsNumeric(r["cheque"].ToString()))
                        cheque = 0;
                    else
                        cheque = Convert.ToDouble(r["cheque"]);

                    doctipo = r["domto"].ToString();

                    if (r["nit"].ToString().Trim() == "")
                    {
                        nit     = r["codi"].ToString().Trim();
                        nombres = r["nombreter"].ToString();
                        codigot = r["codi"].ToString().Trim();
                    }

                    fila++;
                }
                read.Dispose();

                if (forma_imp == "0")
                    BuscaBancoGetFormaImpresion(banco, Myconect, ref forma_imp, ref PideImpresora);

                if (tipo == "")
                    tipo = doctipo;

                string NumCopiasImp = "1", validadora = "0";
                BuscaComprobanteHelper(combte, comp_nume, Myconect, ref validadora, ref NumCopiasImp);

                if (tipo == "CP" || tipo == "PA")
                {
                    if (nit != "")
                    {
                        if (StBanco != "")
                            banco = StBanco;
                        ImprimeComproEgreso(combte, comp_nume, copia, nit, fecha, cheque, valor_cre,
                            Myconect, banco, forma_imp, CantCopias, PideImpresora,
                            NumCuentasAhorros, NumCopiasImp, validadora);
                    }
                    return null;
                }

                switch (tipo)
                {
                    case "RC":
                        ImprimeReciboCaja(nit, codigot, nombres, valor_deb, combte, comp_nume,
                            copia, Myconect, NumCopiasImp, validadora);
                        break;
                    case "NC":
                    case "AC":
                    case "MP":
                    case "TR":
                        ImprimeNotaContable(nit, codigot, nombres, valor_deb, combte, comp_nume,
                            copia, Myconect, NumCopiasImp, validadora);
                        break;
                    case "FC":
                        ImprimeFactura(combte, comp_nume, Myconect, NumCopiasImp, validadora);
                        break;
                }
            }
            finally
            {
            }
            return null;
        }

        // ================================================================
        // ImprimeComproEgreso
        // ================================================================
        private void ImprimeComproEgreso(string combte, string comp_nume, bool copia,
            string nit, DateTime fecha, double cheque, double valor, OdbcConnection Myconnect,
            string Banco = "", string forma_imp = "0", string CantCopias = "0",
            string PedirImpresora = "N", string NumCuentasAhorros = "",
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras num = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();
            string stNumeletras;

            // BuscarCompania: p15=CompaNit, p16=Direccion, p17=Nomres, p22=Telefono(CptoRetFte-bug)
            string CompaNit = " ", Direccion = " ", Nomres = " ", Telefono = " ";
            BuscarCompaniaEgreso(VarIni.sptCodEmpr, Myconnect,
                out CompaNit, out Direccion, out Nomres, out Telefono);

            ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();

            if (copia)
            {
                // --- Copia ---
                ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("forma_cheque" + Banco);

                R.SetParameterValue("banco", Banco);
                R.SetParameterValue("nit", nit);
                stNumeletras = num.Num_a_Letras(valor) + "MLC";
                R.SetParameterValue("valor_letras",
                    "        " + stNumeletras + Strings.Left(pstAsterisco, 140 - stNumeletras.Length));
                R.SetParameterValue("fec_programa", fecha.ToShortDateString());
                R.SetParameterValue("user", UsuarioDoc);
                R.SetParameterValue("comprobante", Strings.Right("0000" + combte, 4));
                R.SetParameterValue("numero", comp_nume.Trim());
                R.SetParameterValue("valor", valor);
                R.SetParameterValue("NomCompania", Nomres);
                R.SetParameterValue("CompaNit", CompaNit);
                R.SetParameterValue("Telefono", Telefono);
                R.SetParameterValue("DireCompañia", Direccion);

                if (forma_imp != "1")
                {
                    if (ImprimeBloque == "N")
                    {
                        if (MessageBox.Show("Desea imprimir el cheque?.", "SOLIDO",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            R.SetParameterValue("impre", 1);

                            if (!Information.IsNumeric(validadora))
                                validadora = "0";

                            if (validadora == "1")
                            {
                                string impresoraPredt = new PrinterSettings().PrinterName;
                                R.PrintOptions.PrinterName = impresoraPredt;
                                if (!Information.IsNumeric(NumCopiasImp))
                                    NumCopiasImp = "0";
                                R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
                            }
                            else
                            {
                                IMP.Visible = false;
                                // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                                // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                            }
                        }
                    }
                    else
                    {
                        R.SetParameterValue("impre", 1);
                        string impresoraPredt = new PrinterSettings().PrinterName;
                        R.PrintOptions.PrinterName = impresoraPredt;
                        if (!Information.IsNumeric(NumCopiasImp))
                            NumCopiasImp = "0";
                        R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
                    }
                }

                try { R.SetParameterValue("NumCuentasAhorros", NumCuentasAhorros); }
                catch { }

                R.SetParameterValue("impre", 2);

                if (!Information.IsNumeric(validadora))
                    validadora = "0";

                if (ImprimeBloque == "Y")
                    validadora = "1";

                if (validadora == "1")
                {
                    string impresoraPredt = new PrinterSettings().PrinterName;
                    R.PrintOptions.PrinterName = impresoraPredt;
                    if (!Information.IsNumeric(NumCopiasImp))
                        NumCopiasImp = "0";
                    R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
                }
                else
                {
                    IMP.Visible = false;
                    // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                    // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                }
            }
            else
            {
                // --- Original ---
                ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("forma_cheque" + Banco);

                R.SetParameterValue("banco", Banco);
                R.SetParameterValue("nit", nit);
                stNumeletras = num.Num_a_Letras(valor) + "MLC";
                R.SetParameterValue("valor_letras",
                    "        " + stNumeletras + Strings.Left(pstAsterisco, 140 - stNumeletras.Length));
                R.SetParameterValue("fec_programa", fecha.ToShortDateString());
                R.SetParameterValue("user", UsuarioDoc);
                R.SetParameterValue("comprobante", Strings.Right("0000" + combte, 4));
                R.SetParameterValue("numero", comp_nume.Trim());
                R.SetParameterValue("valor", valor);
                R.SetParameterValue("NomCompania", Nomres);
                R.SetParameterValue("CompaNit", CompaNit);
                R.SetParameterValue("Telefono", Telefono);
                R.SetParameterValue("DireCompañia", Direccion);
                R.SetParameterValue("impre", 1);

                try { R.SetParameterValue("NumCuentasAhorros", NumCuentasAhorros); }
                catch { }

                if (forma_imp == "1")
                {
                    if (MessageBox.Show(
                        "Inserte el Comprobante: " + comp_nume.Trim() + "\r\n\r\nPresione Si cuando este Preparado...",
                        "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        R.SetParameterValue("impre", 2);

                        int cantCopias = 0;
                        int.TryParse(CantCopias, out cantCopias);
                        cantCopias += 1;

                        switch (PedirImpresora)
                        {
                            case "Y":
                                IMP.Visible = false;
                                // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                                // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                                break;
                            default:
                                R.PrintToPrinter(cantCopias, false, 1, 1);
                                break;
                        }
                    }
                }
                else
                {
                    if (MessageBox.Show(
                        "Inserte el cheque a imprimir y prepare el comprobante: " + comp_nume.Trim() +
                        "\r\n\r\nPresione SI cuando este Preparado...",
                        "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        R.SetParameterValue("impre", 1);

                        switch (PedirImpresora)
                        {
                            case "Y":
                                IMP.Visible = false;
                                // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                                // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                                break;
                            default:
                                R.PrintToPrinter(1, false, 1, 1);
                                break;
                        }

                        MessageBox.Show(
                            "Inserte el Comprobante: " + comp_nume.Trim() +
                            "\r\n\r\nPresione Aceptar cuando este Preparado...",
                            "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        R.SetParameterValue("impre", 2);

                        int cantCopias = 0;
                        int.TryParse(CantCopias, out cantCopias);
                        cantCopias += 1;

                        switch (PedirImpresora)
                        {
                            case "Y":
                                IMP.Visible = false;
                                // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                                // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                                break;
                            default:
                                R.PrintToPrinter(cantCopias, false, 1, 1);
                                break;
                        }
                    }
                }
            }
        }

        // ================================================================
        // ImprimeReciboCaja
        // ================================================================
        private void ImprimeReciboCaja(string nit, string codigot, string nombres,
            double valor, string combte, string comp_nume, bool copia, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras num = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();

            // BuscarCompania: p15=CompaNit, p16=Direccion, p17=Nomres, p26=Telefono(nombre-bug)
            string CompaNit = " ", Direccion = " ", Nomres = " ", Telefono = " ";
            BuscarCompaniaRC(VarIni.sptCodEmpr, Myconnect,
                out CompaNit, out Direccion, out Nomres, out Telefono);

            string nomusu = " ";
            BuscaUsuarioHelper(Myconnect, ref nomusu);

            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cnt_recibocaja");
            R.SetParameterValue("compronte", Strings.Right("0000" + combte, 4));
            R.SetParameterValue("numero_domto", Convert.ToDouble(comp_nume));
            R.SetParameterValue("total", valor);
            string stNumeletras = num.Num_a_Letras(valor) + "MLC";
            R.SetParameterValue("val_letras",
                stNumeletras + Strings.Left(pstAsterisco, 140 - stNumeletras.Length));
            R.SetParameterValue("codigoter", codigot);
            R.SetParameterValue("nombre", nombres);
            R.SetParameterValue("copia", copia ? "1" : "0");
            R.SetParameterValue("usuario", UsuarioDoc);
            R.SetParameterValue("nit", nit);
            R.SetParameterValue("empresa", VarIni.pstEmpresa);

            try
            {
                R.SetParameterValue("CompaNit", CompaNit);
                R.SetParameterValue("nomusu", nomusu);
            }
            catch { }
            try { R.SetParameterValue("direccion", Direccion); }
            catch { }
            try { R.SetParameterValue("telefono", Telefono); }
            catch { }

            if (ImprimeBloque == "N")
            {
                if (MessageBox.Show(
                    "Inserte el comprobante de ingreso : " + Convert.ToDouble(comp_nume) +
                    "\r\n\r\nPresione SI cuando este Preparado...",
                    "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    if (!Information.IsNumeric(validadora))
                        validadora = "0";

                    if (validadora == "1")
                    {
                        string impresoraPredt = new PrinterSettings().PrinterName;
                        R.PrintOptions.PrinterName = impresoraPredt;
                        if (!Information.IsNumeric(NumCopiasImp))
                            NumCopiasImp = "0";
                        R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
                    }
                    else
                    {
                        ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                        IMP.Visible = false;
                        // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                        // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                    }
                }
            }
            else
            {
                string impresoraPredt = new PrinterSettings().PrinterName;
                R.PrintOptions.PrinterName = impresoraPredt;
                if (!Information.IsNumeric(NumCopiasImp))
                    NumCopiasImp = "0";
                R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
            }
        }

        // ================================================================
        // ImprimeNotaContable
        // ================================================================
        private void ImprimeNotaContable(string nit, string codigot, string nombres,
            double valor, string combte, string comp_nume, bool copia, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            ERP.Core.Compartido.Utilidades.Numeros_A_Letras num = new ERP.Core.Compartido.Utilidades.Numeros_A_Letras();

            // BuscarCompania: p15=CompaNit, p16=Direccion, p26=Nomres(nombre), p27=Telefono(telefono)
            string CompaNit = " ", Direccion = " ", Nomres = " ", Telefono = " ";
            BuscarCompaniaNota(VarIni.sptCodEmpr, Myconnect,
                out CompaNit, out Direccion, out Nomres, out Telefono);

            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cnt_rnotacontable");
            R.SetParameterValue("compro", Strings.Right("0000" + combte, 4));
            R.SetParameterValue("numero", Convert.ToDouble(comp_nume));
            R.SetParameterValue("empresa", Nomres);
            string stNumeletras = num.Num_a_Letras(valor) + "MLC";
            int lenRest = 140 - stNumeletras.Length;
            R.SetParameterValue("letras",
                stNumeletras + Strings.Left(pstAsterisco, lenRest < 0 ? 2 : lenRest));
            R.SetParameterValue("usuario", UsuarioDoc);
            R.SetParameterValue("copia", copia ? "1" : "0");

            try
            {
                R.SetParameterValue("CompaNit", CompaNit);
                R.SetParameterValue("Direccion", Direccion);
                R.SetParameterValue("Telefono", Telefono);
            }
            catch { }

            if (ImprimeBloque == "N")
            {
                if (MessageBox.Show(
                    "Inserte la nota : " + Convert.ToDouble(comp_nume) +
                    "\r\n\r\nPresione SI cuando este Preparado...",
                    "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    if (!Information.IsNumeric(validadora))
                        validadora = "0";

                    if (validadora == "1")
                    {
                        string impresoraPredt = new PrinterSettings().PrinterName;
                        R.PrintOptions.PrinterName = impresoraPredt;
                        if (!Information.IsNumeric(NumCopiasImp))
                            NumCopiasImp = "0";
                        R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
                    }
                    else
                    {
                        ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                        IMP.Visible = false;
                        // IMP.CrystalReportViewer1.ReportSource = R; // ERROR: CS1061
                        // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                    }
                }
            }
            else
            {
                string impresoraPredt = new PrinterSettings().PrinterName;
                R.PrintOptions.PrinterName = impresoraPredt;
                if (!Information.IsNumeric(NumCopiasImp))
                    NumCopiasImp = "0";
                R.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
            }
        }

        // ================================================================
        // ImprimeFactura
        // ================================================================
        private void ImprimeFactura(string combte, string comp_nume, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            string nit = " ", Direccion = " ", Telefono = " ", Nombre = " ", Ciudad = " ";
            string Resolucion = " ", Prefijo = " ";
            DateTime Fecresol = new DateTime(1950, 1, 1);
            int Regimen = 0, NumInicial = 0, NumFinal = 0;
            string IdCtrlFactura = " ";

            // Obtiene IdCtrlFactura desde sys_compro02
            string stmysql = "select CodfacturaCont as campo1 from sys_compro02 where codigo = '" +
                Strings.Right("0000" + combte, 4) + "'";
            string _campo2 = "", _campo3 = "", _campo4 = "";
            connect.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFactura",
                ref IdCtrlFactura, ref _campo2, ref _campo3, ref _campo4);

            // BuscarCompania: p15=nit, p16=Direccion, p26=Nombre, p27=Telefono, p35=Ciudad
            BuscarCompaniaFactura(VarIni.sptCodEmpr, Myconnect,
                out nit, out Direccion, out Nombre, out Telefono, out Ciudad);

            // BuscaDatosFacturacion
            string _resol = Resolucion, _prefijo = Prefijo;
            string _numIni = " ", _numFin = " ";
            DateTime _fecresol = Fecresol;
            int _regimen = Regimen;
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcntObj = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            BuscaDatosFacturacionHelper(msgcntObj, IdCtrlFactura, Myconnect,
                ref _resol, ref _fecresol, ref _regimen, ref _prefijo, ref _numIni, ref _numFin);
            Resolucion = _resol;
            Fecresol   = _fecresol;
            Regimen    = _regimen;
            Prefijo    = _prefijo;
            int.TryParse(_numIni.Trim().Length > 0 ? _numIni.Trim() : "0", out NumInicial);
            int.TryParse(_numFin.Trim().Length > 0 ? _numFin.Trim() : "0", out NumFinal);

            string NomRegimen;
            switch (Regimen)
            {
                case 0: NomRegimen = "Regimen Comun"; break;
                case 1: NomRegimen = "Regimen Simplificado"; break;
                default: NomRegimen = ""; break;
            }

            ERP.Core.Compartido.Reportes.reporte factura = new ERP.Core.Compartido.Reportes.reporte("cnt_factura");
            factura.SetParameterValue("IdTipoMovto", Convert.ToInt32(combte));
            factura.SetParameterValue("Secuencia",   Convert.ToDouble(comp_nume));
            factura.SetParameterValue("nit",         nit);
            factura.SetParameterValue("regimen",     Regimen);
            factura.SetParameterValue("direccion",   Direccion);
            factura.SetParameterValue("telefono",    Telefono);
            factura.SetParameterValue("Nombre",      Nombre);
            factura.SetParameterValue("resolucion",  Resolucion);
            factura.SetParameterValue("Fecresol",    Fecresol);
            factura.SetParameterValue("telefono",    Telefono);
            factura.SetParameterValue("NumInicial",  NumInicial);
            factura.SetParameterValue("numfinal",    NumFinal);
            factura.SetParameterValue("Prefijo",     Prefijo);
            factura.SetParameterValue("Ciudad",      Ciudad);

            if (!Information.IsNumeric(validadora))
                validadora = "0";

            if (ImprimeBloque == "N")
            {
                if (validadora == "1")
                {
                    string impresoraPredt = new PrinterSettings().PrinterName;
                    factura.PrintOptions.PrinterName = impresoraPredt;
                    if (!Information.IsNumeric(NumCopiasImp))
                        NumCopiasImp = "0";
                    factura.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
                }
                else
                {
                    ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                    IMP.Visible = false;
                    // IMP.CrystalReportViewer1.ReportSource = factura; // ERROR: CS1061
                    // IMP.CrystalReportViewer1.PrintReport(); // ERROR: CS1061
                }
            }
            else
            {
                string impresoraPredt = new PrinterSettings().PrinterName;
                factura.PrintOptions.PrinterName = impresoraPredt;
                if (!Information.IsNumeric(NumCopiasImp))
                    NumCopiasImp = "0";
                factura.PrintToPrinter((int.Parse(NumCopiasImp) + 1), false, 0, 0);
            }
        }

        // ================================================================
        // impre — segundo overload (impresión por bloques)
        // ================================================================
        public object impre(string combte, string comp_nume, bool copia, string tipo,
            OdbcConnection Myconect, string comp_nume_fin, Form Myforma,
            string StBanco = "", string forma_imp = "0", string CantCopias = "0",
            bool DesdeAhorros = false, string NumCuentasAhorros = "")
        {
            double valor_deb = 0, valor_cre = 0, cheque = 0;
            string cuentabanco = " ", PideImpresora = " ";
            string codigot = "", nombres = "", nit = "", banco = "", doctipo = "";
            DateTime fecha = DateTime.MinValue;
            int canreg = 0, fila = 0;
            string sql;
            string NumCopiasImp, validadora;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Impresion por bloques", Myforma);
            int numeroRegistro = 0;

            try
            {
                VarIni.sptCodEmpr = "0001";
                CargaEmpresa(Myconect);

                DataSet datasetImprimeBloques = new DataSet();

                sql = " select docto.COMPRONTE,docto.NUMERO,compro.PUERT_VALIDADORA from cnt_docmto docto" +
                      " inner join  sys_compro02 compro on docto.compronte = compro.CODIGO" +
                      " where docto.cerrado='Y' and  docto.COMPRONTE ='" + Strings.Right("0000" + combte, 4) + "' AND" +
                      " docto.NUMERO between " + comp_nume + "  and " + comp_nume_fin;

                connect.ExecuteQueryDataset(sql, Myconect, "impreBloques", ref datasetImprimeBloques, "TblImpreBloq");
                numeroRegistro = datasetImprimeBloques.Tables["TblImpreBloq"].Rows.Count;

                msgbarra.ValorMinimoMaximo(0, numeroRegistro);
                msgbarra.Show();

                for (int indice = 0; indice < datasetImprimeBloques.Tables["TblImpreBloq"].Rows.Count; indice++)
                {
                    DataRow bloqRow = datasetImprimeBloques.Tables["TblImpreBloq"].Rows[indice];

                    validadora  = "1";
                    NumCopiasImp = bloqRow["PUERT_VALIDADORA"].ToString();
                    sql        = "";
                    comp_nume  = bloqRow["NUMERO"].ToString();
                    valor_deb  = 0;
                    valor_cre  = 0;
                    fila       = 0;
                    canreg     = 0;

                    sql = "SELECT f.nombre, case d.tipo_persona when  'N' then d.nombre else d.razon_social end as nombreter" +
                          " ,a.cerrado,a.compronte,a.domto,a.banco,a.cheque,a.NUMERO,a.detalle,a.fecha" +
                          " ,b.cuenta,a.idbenef as nit,a.idbenefcheque ,d.nit as codi,c.nombre as nombre_cuenta,b.vlr_debito,b.vlr_credito,b.usuario" +
                          " FROM cnt_docmto a left join cnt_movimto b" +
                          " on b.compronte = a.compronte and" +
                          " b.NUMERO = a.NUMERO inner join cnt_maecuen c" +
                          " on c.cuenta = b.cuenta left join cnt_nit d" +
                          " on d.nit = b.nit left join cnt_nit f" +
                          " on f.nit = a.idbenef" +
                          " WHERE (a.COMPRONTE ='" + Strings.Right("0000" + combte, 4) + "') AND (a.NUMERO = '" + bloqRow["NUMERO"].ToString().Trim() + "')" +
                          " order by b.cuenta asc";

                    codigot = "";
                    nombres = "";

                    DataSet read = new DataSet();
                    connect.ExecuteQueryDataset(sql, Myconect, "impre", ref read, "TblImpre");
                    canreg = read.Tables["TblImpre"].Rows.Count;

                    while (fila < canreg)
                    {
                        DataRow r = read.Tables["TblImpre"].Rows[fila];

                        if (tipo == "CP" || tipo == "PA")
                        {
                            if (Strings.Right("0000" + r["banco"].ToString(), 4) != "0000")
                            {
                                string _banco = r["banco"].ToString();
                                BuscaBancoGetCuenta(ref _banco, Myconect, ref cuentabanco);
                                if (cuentabanco == r["cuenta"].ToString())
                                    valor_cre += Convert.ToDouble(r["vlr_credito"]);
                            }
                        }
                        else
                        {
                            valor_deb += Convert.ToDouble(r["vlr_debito"]);
                        }

                        UsuarioDoc = r["usuario"].ToString();
                        nit = DesdeAhorros
                            ? r["idbenefcheque"].ToString().Trim()
                            : r["nit"].ToString().Trim();
                        codigot = nit;
                        nombres = r["nombre"].ToString();
                        banco   = Strings.Right("0000" + r["banco"].ToString(), 4);
                        fecha   = Convert.ToDateTime(r["fecha"]);

                        if (!Information.IsNumeric(r["cheque"].ToString()))
                            cheque = 0;
                        else
                            cheque = Convert.ToDouble(r["cheque"]);

                        doctipo = r["domto"].ToString();

                        if (r["nit"].ToString().Trim() == "")
                        {
                            nit     = r["codi"].ToString().Trim();
                            nombres = r["nombreter"].ToString();
                            codigot = r["codi"].ToString().Trim();
                        }

                        fila++;
                    }
                    read.Dispose();

                    msgbarra.Comentario("Imprimiendo Comprobante " + combte + "-" + comp_nume);

                    if (forma_imp == "0")
                        BuscaBancoGetFormaImpresion(banco, Myconect, ref forma_imp, ref PideImpresora);

                    if (tipo == "")
                        tipo = doctipo;

                    if (tipo == "CP" || tipo == "PA")
                    {
                        if (nit != "")
                        {
                            if (StBanco != "")
                                banco = StBanco;
                            ImprimeComproEgreso(combte, comp_nume, copia, nit, fecha, cheque, valor_cre,
                                Myconect, banco, forma_imp, CantCopias, PideImpresora,
                                NumCuentasAhorros, NumCopiasImp, validadora, "Y");
                        }
                    }

                    switch (tipo)
                    {
                        case "RC":
                            ImprimeReciboCaja(nit, codigot, nombres, valor_deb, combte, comp_nume,
                                copia, Myconect, NumCopiasImp, validadora, "Y");
                            break;
                        case "NC":
                        case "AC":
                        case "MP":
                        case "TR":
                            ImprimeNotaContable(nit, codigot, nombres, valor_deb, combte, comp_nume,
                                copia, Myconect, NumCopiasImp, validadora, "Y");
                            break;
                        case "FC":
                            ImprimeFactura(combte, comp_nume, Myconect, NumCopiasImp, validadora, "Y");
                            break;
                    }

                    msgbarra.PerformStep();
                }
                msgbarra.Close();
            }
            finally
            {
            }

            return null;
        }

        // ================================================================
        // HELPERS PRIVADOS — encapsulan llamadas a métodos VB con muchos
        // parámetros ByRef opcionales
        // ================================================================

        /// <summary>
        /// BuscarCompania — versión larga. Extrae los campos por posición según cada caller.
        /// p15=nit, p16=direccion, p17=nomres, p22=cptoRetFte, p26=nombre, p27=telefono, p35=ciudad
        /// </summary>
        private void BuscarCompaniaFullHelper(string codEmpr, OdbcConnection myconnect,
            out string nit,       // p15
            out string direccion, // p16
            out string nomres,    // p17
            out string cptoRet,   // p22  (CptoRetFte — usado erróneamente como Telefono en ImprimeComproEgreso)
            out string nombre,    // p26
            out string telefono,  // p27
            out string ciudad)    // p35
        {
            string _cuentaUtil = " ", _cptoCap = "00", _calcSaldo = "N", _cptoAfavor = "0";
            int _tipoLiq = 0; decimal _tasaMora = 0; int _diasGracia = 0; decimal _tasaUsura = 0;
            int _baseLiq = 0; int _ctrlConse = 0; int _conseCredit = 0; string _cptoRevapo = "00";
            string _nit = " ", _direccion = " ", _nomres = " ";
            string _cptoServ = "9999", _cptoExt = "9999", _cptoAho = "9999", _cptoApo = "9999";
            string _cptoRetFte = "9999", _undred = "", _cptoCdats = "", _cptoIntCdats = "9999";
            string _nombre = " ", _telefono = "0";
            string _cpto4Mil = "9999", _cptoIntAhorro = "99", _cptoAnticipo = "9999";
            double _conseCdat = 0, _conseDep = 0; int _opRecDeuda = 0; string _cpteAnticipo = "9999";
            string _ciudad = " ", _genCobro = " "; double _vlrConsulta = 0; string _cpteConsulta = " ";
            string _nomResum = "", _cptoIntAnt = "9999", _cptoint = "9999", _cptomor = "9999";
            double _limDiarioLava = 0, _limMesLava = 0;
            string _manEstudio = "N", _cobraCod = "N", _porcEndeu = "0", _manCap = "0";
            string _serverSmtp = "", _passEnvio = "", _correoEnvio = "", _tipoNomina = "", _cptoIntAntic = " ";
            string _depto = " ", _jefeCartera = " ", _cpto4MilCheque = "9999", _cpteFavor = "9999";
            char _retenaux = 'N'; double _valorRetenaux = 0, _porcenRetenaux = 0;
            string _cuentaRetenaux = "999999999999", _consecFact = " ", _numCodeCredit = "   ";
            string _modifCuota = " "; char _conciliaBanca = 'N'; int _numPagare = 0;
            string _pagareNotas = "N", _formaPagare = "0", _controlaDep = "Y";
            string _paramcausacion = "Y", _disableTasaI = "N", _forapl = "1";
            int _convEnpacto = 0; string _feec = "EST";

            // new ERP.Core.Compartido.Configuracion.ParamSys().BuscarCompania(codEmpr, myconnect, // ERROR: CS1503
                // ref _cuentaUtil, ref _cptoCap, ref _calcSaldo, ref _cptoAfavor, // ERROR: CS1503
                // ref _tipoLiq, ref _tasaMora, ref _diasGracia, ref _tasaUsura, // ERROR: CS1503
                // ref _baseLiq, ref _ctrlConse, ref _conseCredit, ref _cptoRevapo, // ERROR: CS1503
                // ref _nit,        // p15 // ERROR: CS1503
                // ref _direccion,  // p16 // ERROR: CS1503
                // ref _nomres,     // p17 // ERROR: CS1503
                // ref _cptoServ, ref _cptoExt, ref _cptoAho, ref _cptoApo, // ERROR: CS1503
                // ref _cptoRetFte, // p22 // ERROR: CS1503
                // ref _undred, ref _cptoCdats, ref _cptoIntCdats, // ERROR: CS1503
                // ref _nombre,     // p26 // ERROR: CS1503
                // ref _telefono,   // p27 // ERROR: CS1503
                // ref _cpto4Mil, ref _cptoIntAhorro, ref _cptoAnticipo, // ERROR: CS1503
                // ref _conseCdat, ref _conseDep, ref _opRecDeuda, ref _cpteAnticipo, // ERROR: CS1503
                // ref _ciudad,     // p35 // ERROR: CS1503
                // ref _genCobro, ref _vlrConsulta, ref _cpteConsulta, // ERROR: CS1503
                // ref _nomResum, ref _cptoIntAnt, ref _cptoint, ref _cptomor, // ERROR: CS1503
                // ref _limDiarioLava, ref _limMesLava, ref _manEstudio, ref _cobraCod, // ERROR: CS1503
                // ref _porcEndeu, ref _manCap, ref _serverSmtp, ref _passEnvio, // ERROR: CS1503
                // ref _correoEnvio, ref _tipoNomina, ref _cptoIntAntic, // ERROR: CS1503
                // ref _depto, ref _jefeCartera, ref _cpto4MilCheque, ref _cpteFavor, // ERROR: CS1503
                // ref _retenaux, ref _valorRetenaux, ref _porcenRetenaux, ref _cuentaRetenaux, // ERROR: CS1503
                // ref _consecFact, ref _numCodeCredit, ref _modifCuota, ref _conciliaBanca, // ERROR: CS1503
                // ref _numPagare, ref _pagareNotas, ref _formaPagare, ref _controlaDep, // ERROR: CS1503
                // ref _paramcausacion, ref _disableTasaI, ref _forapl, ref _convEnpacto, ref _feec); // ERROR: CS1503

            nit       = _nit;
            direccion = _direccion;
            nomres    = _nomres;
            cptoRet   = _cptoRetFte;
            nombre    = _nombre;
            telefono  = _telefono;
            ciudad    = _ciudad;
        }

        /// <summary>
        /// Para ImprimeComproEgreso: p15=CompaNit, p16=Direccion, p17=Nomres, p22=Telefono(bug)
        /// </summary>
        private void BuscarCompaniaEgreso(string codEmpr, OdbcConnection myconnect,
            out string CompaNit, out string Direccion, out string Nomres, out string Telefono)
        {
            string _nomres, _cptoRet, _nombre, _telefono, _ciudad;
            BuscarCompaniaFullHelper(codEmpr, myconnect,
                out CompaNit, out Direccion, out _nomres, out _cptoRet,
                out _nombre, out _telefono, out _ciudad);
            Nomres   = _nomres;
            Telefono = _cptoRet; // p22=CptoRetFte — bug del original replicado
        }

        /// <summary>
        /// Para ImprimeReciboCaja: p15=CompaNit, p16=Direccion, p17=Nomres, p26=Telefono(nombre-bug)
        /// </summary>
        private void BuscarCompaniaRC(string codEmpr, OdbcConnection myconnect,
            out string CompaNit, out string Direccion, out string Nomres, out string Telefono)
        {
            string _cptoRet, _nombre, _telefono, _ciudad;
            BuscarCompaniaFullHelper(codEmpr, myconnect,
                out CompaNit, out Direccion, out Nomres, out _cptoRet,
                out _nombre, out _telefono, out _ciudad);
            Telefono = _nombre; // p26=nombre — bug del original replicado
        }

        /// <summary>
        /// Para ImprimeNotaContable: p15=CompaNit, p16=Direccion, p26=Nomres(nombre), p27=Telefono
        /// </summary>
        private void BuscarCompaniaNota(string codEmpr, OdbcConnection myconnect,
            out string CompaNit, out string Direccion, out string Nomres, out string Telefono)
        {
            string _nomres, _cptoRet, _nombre, _ciudad;
            BuscarCompaniaFullHelper(codEmpr, myconnect,
                out CompaNit, out Direccion, out _nomres, out _cptoRet,
                out _nombre, out Telefono, out _ciudad);
            Nomres = _nombre; // p26=nombre
        }

        /// <summary>
        /// Para ImprimeFactura: p15=nit, p16=Direccion, p26=Nombre, p27=Telefono, p35=Ciudad
        /// </summary>
        private void BuscarCompaniaFactura(string codEmpr, OdbcConnection myconnect,
            out string nit, out string Direccion, out string Nombre, out string Telefono, out string Ciudad)
        {
            string _nomres, _cptoRet;
            BuscarCompaniaFullHelper(codEmpr, myconnect,
                out nit, out Direccion, out _nomres, out _cptoRet,
                out Nombre, out Telefono, out Ciudad);
        }

        /// <summary>
        /// BuscaBanco — extrae cuentabanco (p11).
        /// Nota: Codigo es ByRef en VB, se pasa como ref.
        /// </summary>
        private void BuscaBancoGetCuenta(ref string banco, OdbcConnection myconnect, ref string cuentabanco)
        {
            string _nombre = "", _nomRes = "", _codCuenta = "", _numCom = "", _codTraslado = "";
            string _claseCuenta = "", _digitoChequeo = "", _ultimoCheque = "";
            string _formaImp = "0"; double _grabamen = 0; int _copias = 0;
            string _estruDisp = "0", _cobraComision = "N", _cuentaComision = "0";
            int _formaComision = 0; double _valorComision = 0;
            string _pideImpresora = "N", _ctrlConsec = "N";

            // sas.BuscaBanco(ref banco, myconnect, // ERROR: CS1002, CS1026, CS1525
                // ref _nombre, ref _nomRes, ref _codCuenta, ref _numCom, // ERROR: CS1002, CS1026, CS1525
                // ref _codTraslado, ref _claseCuenta, ref _digitoChequeo, ref _ultimoCheque, // ERROR: CS1002, CS1026, CS1525
                // ref cuentabanco,                      // p11 // ERROR: CS1002, CS1026, CS1525
                // ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,    // p12 ByVal // ERROR: CS1002, CS1026, CS1525
                // ref _formaImp, ref _grabamen, ref _copias, ref _estruDisp, // ERROR: CS1503
                // ref _cobraComision, ref _cuentaComision, ref _formaComision, ref _valorComision, // ERROR: CS1503
                // ref _pideImpresora, ref _ctrlConsec); // ERROR: CS1503
        }

        /// <summary>
        /// BuscaBanco — extrae forma_imp (p13) y pideImpresora (p21).
        /// </summary>
        private void BuscaBancoGetFormaImpresion(string banco, OdbcConnection myconnect,
            ref string formaImp, ref string pideImpresora)
        {
            string _nombre = "", _nomRes = "", _codCuenta = "", _numCom = "", _codTraslado = "";
            string _claseCuenta = "", _digitoChequeo = "", _ultimoCheque = "", _cuentaContable = "";
            double _grabamen = 0; int _copias = 0;
            string _estruDisp = "0", _cobraComision = "N", _cuentaComision = "0";
            int _formaComision = 0; double _valorComision = 0;
            string _ctrlConsec = "N";

            // sas.BuscaBanco(ref banco, myconnect, // ERROR: CS1002, CS1026, CS1525
                // ref _nombre, ref _nomRes, ref _codCuenta, ref _numCom, // ERROR: CS1002, CS1026, CS1525
                // ref _codTraslado, ref _claseCuenta, ref _digitoChequeo, ref _ultimoCheque, // ERROR: CS1002, CS1026, CS1525
                // ref _cuentaContable,                  // p11 // ERROR: CS1002, CS1026, CS1525
                // ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno,    // p12 ByVal // ERROR: CS1002, CS1026, CS1525
                // ref formaImp,                         // p13 // ERROR: CS1002, CS1026, CS1525
                // ref _grabamen, ref _copias, ref _estruDisp, // ERROR: CS1503
                // ref _cobraComision, ref _cuentaComision, ref _formaComision, ref _valorComision, // ERROR: CS1503
                // ref pideImpresora,                    // p21 // ERROR: CS1503
                // ref _ctrlConsec); // ERROR: CS1503
        }

        /// <summary>
        /// BuscaComprobante — extrae validadora (p21) y NumCopiasImp (p24).
        /// </summary>
        private void BuscaComprobanteHelper(string combte, string comp_nume, OdbcConnection myconect,
            ref string validadora, ref string numCopiasImp)
        {
            string _combte = combte;
            double _compNumeD = 0;
            double.TryParse(comp_nume.Trim().Length > 0 ? comp_nume.Trim() : "0", out _compNumeD);

            string _controlConse = " ", _tipoDoc = " ";
            double _debitos = 0, _creditos = 0;
            string _cerrado = "N", _anulado = "N";
            double _diferencia = 0;
            string _detalle = " ", _doctipo = "NC", _nodoc = " ";
            DateTime _fechaMovto = new DateTime(1950, 1, 1);
            string _nombre = " ", _idbenef = "99999999999999", _cencCpte = "99999999";
            string _cuenta = "999999999999", _afecta3xmil = "N";
            string _formaImp = "0";

            msgparsys.BuscaComprobante(
                ref _combte, ref _compNumeD, false, myconect,
                ref _controlConse, ref _tipoDoc, ref _debitos, ref _creditos,
                ref _cerrado, ref _anulado, ref _diferencia, ref _detalle,
                ref _doctipo, ref _nodoc, ref _fechaMovto, ref _nombre,
                ref _idbenef, ref _cencCpte, ref _cuenta, ref _afecta3xmil,
                ref validadora,   // p21
                ref _formaImp,    // p22
                "N",              // p23 ByVal Actualizaconcep
                ref numCopiasImp  // p24
            );
        }

        /// <summary>
        /// BuscaUsuario — extrae nomusu (p4).
        /// </summary>
        private void BuscaUsuarioHelper(OdbcConnection myconnect, ref string nomusu)
        {
            string _login = UsuarioDoc;
            double _creditoMin = 0, _creditoMax = 0;
            object _fecVence = new DateTime(1950, 1, 1);
            int _tipoAcceso = 0;
            string _passW = "", _grupo = "", _estatus = "";
            object _fecCrea = new DateTime(1950, 1, 1);
            object _fecCambio = new DateTime(1950, 1, 1);
            double _grabMin = 0, _grabMax = 0;
            string _cedula = "";
            bool _grabaRetirados = false, _sobreGiro = false, _validaComp = false;
            double _autoriMin = 0, _autoriMax = 0;
            bool _cancelaCdats = false, _grabaCancelaPAP = false, _grabaFacVencidas = false;

            // new ERP.Core.Compartido.Configuracion.ParamSys().BuscaUsuario( // ERROR: CS1501
                // ref _login, myconnect, ERP.Core.Compartido.Configuracion.ParamSys.Navega.Ninguno, // ERROR: CS1501
                // ref nomusu,           // p4 // ERROR: CS1501
                // ref _creditoMin, ref _creditoMax, ref _fecVence, // ERROR: CS1501
                // ref _tipoAcceso, ref _passW, ref _grupo, ref _estatus, // ERROR: CS1501
                // ref _fecCrea, ref _fecCambio, ref _grabMin, ref _grabMax, // ERROR: CS1501
                // ref _cedula, ref _grabaRetirados, ref _sobreGiro, ref _validaComp, // ERROR: CS1501
                // ref _autoriMin, ref _autoriMax, ref _cancelaCdats, // ERROR: CS1501
                // ref _grabaCancelaPAP, ref _grabaFacVencidas); // ERROR: CS1501
        }

        /// <summary>
        /// BuscaDatosFacturacion — extrae Resolucion, FecResol, RegimenIva, Prefijo, NumInicial, NumFinal.
        /// </summary>
        private void BuscaDatosFacturacionHelper(ERP.Core.Contabilidad.Services.ClsContabilidad msgcntObj,
            string idCtrlFactura, OdbcConnection myconnect,
            ref string resolucion, ref DateTime fecResol, ref int regimenIva,
            ref string prefijo, ref string numInicial, ref string numFinal)
        {
            string _idCtrl = idCtrlFactura;
            double _consefact = 0;
            string _consecompronte = "C";

            // msgcntObj.BuscaDatosFacturacion( // ERROR: CS1061
                // ref _idCtrl, myconnect, // ERROR: CS1061
                // ref resolucion,    // p3 // ERROR: CS1061
                // ref fecResol,      // p4 // ERROR: CS1061
                // ref regimenIva,    // p5 // ERROR: CS1061
                // ref prefijo,       // p6 // ERROR: CS1061
                // ref numInicial,    // p7 // ERROR: CS1061
                // ref numFinal,      // p8 // ERROR: CS1061
                // ref _consefact,                          // p9 // ERROR: CS1061
                // ERP.Core.Contabilidad.Services.ClsContabilidad.Navega.Ninguno,   // p10 ByVal // ERROR: CS1061
                // ref _consecompronte);     // p11 // ERROR: CS1061
        }
    }
}
