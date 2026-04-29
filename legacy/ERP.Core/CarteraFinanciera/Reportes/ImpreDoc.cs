using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
#if CRYSTAL_LEGACY
using CrystalDecisions.Shared;
#endif

namespace ERP.Core.CarteraFinanciera.Reportes
{
    // Traducción de: Public Class ImpreDoc (ImpreDoc.vb)
    public class ImpreDoc
    {
        private string pstAsterisco = "XXXXXXXXXXXXX XXXXXXXXXXXXXXXXX XXXXXXXXXXXXXX XXXXXXXXXXX XXXXXXXXX XXXXXXXXXX";
        private string UsuarioDoc;
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos liqcre = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();

        public ImpreDoc(string user)
        {
            VarIni.sptCodEmpr = "0001";
        }

        ~ImpreDoc()
        {
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers privados para llamadas a VB DLLs con muchos params opcionales
        // ─────────────────────────────────────────────────────────────────────

        // BuscarCompania string overload — extrae p15=nit, p16=dir, p17=nomres, p27=telefono
        private void BuscarCompaniaHelper(string codigo, OdbcConnection myconnect,
            ref string compaNit, ref string direccion, ref string nomres, ref string telefono)
        {
            string _u3 = " ", _u4 = "00", _u5 = "N", _u6 = "0";
            int    _u7 = 0, _u9 = 0, _u11 = 0, _u12 = 0, _u13 = 0;
            decimal _u8 = 0m, _u10 = 0m;
            string _u14 = "00";
            string _u18 = "9999", _u19 = "9999", _u20 = "9999", _u21 = "9999",
                   _u22 = "9999", _u23 = "", _u24 = "", _u25 = "9999", _u26 = " ";
            //msgparsys.BuscarCompania(codigo, myconnect,
                //ref _u3, ref _u4, ref _u5, ref _u6,
                //ref _u7, ref _u8, ref _u9, ref _u10,
                //ref _u11, ref _u12, ref _u13, ref _u14,
                //ref compaNit, ref direccion, ref nomres,
                //ref _u18, ref _u19, ref _u20, ref _u21,
                //ref _u22, ref _u23, ref _u24, ref _u25, ref _u26,
                //ref telefono);
        }

        // BuscaComprobante — extrae p21=validadora, p22=FormaImp, p24=NumCopiasImp
        private void BuscaComprobanteHelper(ref string combte, ref double compNume, OdbcConnection conect,
            ref string validadora, ref string formaImp, ref string numCopiasImp)
        {
            string  _u5 = " ", _u6 = " ";
            double  _u7 = 0, _u8 = 0, _u11 = 0;
            string  _u9 = "N", _u10 = "N", _u12 = " ", _u13 = "NC", _u14 = " ";
            DateTime _u15 = new DateTime(1950, 1, 1);
            string  _u16 = " ", _u17 = "99999999999999", _u18 = "99999999",
                    _u19 = "999999999999", _u20 = "N";
            msgparsys.BuscaComprobante(ref combte, ref compNume, false, conect,
                ref _u5, ref _u6, ref _u7, ref _u8, ref _u9, ref _u10, ref _u11, ref _u12,
                ref _u13, ref _u14, ref _u15, ref _u16, ref _u17, ref _u18, ref _u19, ref _u20,
                ref validadora, ref formaImp, "N", ref numCopiasImp);
        }

        // ExecuteQueryconec con 0 params extra
        private void ExecSql(string sql, OdbcConnection conn, string proc)
        {
            string _a = " ", _b = " ", _c = " ", _d = " ";
            connect.ExecuteQueryconec(sql, conn, proc, ref _a, ref _b, ref _c, ref _d);
        }

        // ─────────────────────────────────────────────────────────────────────
        // CargaEmpresa
        // ─────────────────────────────────────────────────────────────────────
        private void CargaEmpresa(OdbcConnection conect)
        {
            DataSet datos = new DataSet();
            string _codigo = VarIni.sptCodEmpr;
            msgparsys.BuscarCompania(ref _codigo, ref datos, conect);

            if (datos.Tables["tblcompania"].Rows.Count > 0)
            {
                DataRow r = datos.Tables["tblcompania"].Rows[0];
                VarIni.pstCptocuan = r["sobra"].ToString();
                VarIni.stnit       = r["nit"].ToString();
                VarIni.pstdiremp   = r["direccion"].ToString();
                VarIni.psttelemp   = r["telefono"].ToString();
                VarIni.pstciuemp   = r["ciudad"].ToString();
                VarIni.pstRepre    = r["REPRE"].ToString();
                VarIni.pstRev_fis  = r["REV_FIS"].ToString();
                VarIni.pstMat_rev  = r["MAT_REV"].ToString();
                VarIni.pstconta    = r["CONTA"].ToString();
                VarIni.pstMat_con  = r["MAT_CON"].ToString();
                VarIni.pstUndred   = r["UNI_RED"].ToString();
                VarIni.pstEmpresa  = r["nombre"].ToString();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // impre — overload 1 (documento individual)
        // ─────────────────────────────────────────────────────────────────────
        public object impre(string combte, string comp_nume, bool copia, string tipo, OdbcConnection conect)
        {
            string FormaImp = "0";
            try
            {
                VarIni.sptCodEmpr = "0001";
                CargaEmpresa(conect);

                double valor = 0, ValorCuentaOC = 0;
                string codigot = "", nombres = "", nit = "", lincred = "", nume_cre = "", tipodoc = "";
                string NumCopiasImp = "1", validadora = "0";
                DateTime fecha = DateTime.MinValue;
                int canreg = 0, fila = 0;
                StringBuilder StBuilder = new StringBuilder();

                StBuilder.Append("SELECT d.nombre,case f.tipo_persona when  'N' then f.nombre else f.razon_social end as nomprove,b.nit as nit,a.cerrado,d.apellido,a.compronte,a.NUMERO_DOMTO,a.detalle,a.fecha,a.doctipo, ");
                StBuilder.Append("b.cuenta,a.idbenef,b.codigoter,b.lincred,b.numero,c.descripcion,b.vlr_debito,b.vlr_credito,b.usuario,codmov.tipo_movto,maecar.fecfact ");
                StBuilder.Append("FROM cop_docmto a ");
                StBuilder.Append("left join cop_movimto b on b.compronte = a.compronte and b.NUMERO_DOMTO = a.NUMERO_DOMTO ");
                StBuilder.Append("inner join cop_concar12 c on c.lincred = b.lincred ");
                StBuilder.Append("inner join sys_maenit d on d.codigoter = b.codigoter ");
                StBuilder.Append("left join cnt_nit f on f.nit = b.nit ");
                StBuilder.Append("left join cop_codmov codmov on b.cod_movto = codmov.cod_movto ");
                StBuilder.Append("left join cop_maecar maecar on b.codigoter = maecar.codigoter and b.lincred=maecar.lincred and b.numero=maecar.numero ");
                StBuilder.Append("where a.COMPRONTE ='" + Microsoft.VisualBasic.Strings.Right("0000" + combte, 4) + "' and a.NUMERO_DOMTO = '" + comp_nume.Trim() + "' ");
                StBuilder.Append("order by b.lincred desc ");

                DataSet read = new DataSet();
                connect.ExecuteQueryDataset(StBuilder.ToString(), conect, "impre", ref read, "TblImpreDoc");
                canreg = read.Tables["TblImpreDoc"].Rows.Count;

                while (fila < canreg)
                {
                    DataRow row = read.Tables["TblImpreDoc"].Rows[fila];
                    if (row["cerrado"].ToString() != "Y")
                    {
                        MessageBox.Show("El documento no esta cerrado.", "SOLIDO", MessageBoxButtons.OKCancel);
                        return null;
                    }
                    tipodoc = row["doctipo"].ToString();
                    if (tipo.Trim() == "")
                        tipo = tipodoc;

                    if (row["codigoter"].ToString().Trim() != "99999999999999" && row["lincred"].ToString().Trim() != "9999")
                    {
                        switch (tipo)
                        {
                            case "OC":
                                if (Convert.ToDouble(row["vlr_debito"]) != 0)
                                {
                                    if (row["tipo_movto"] != DBNull.Value)
                                    {
                                        if (row["tipo_movto"].ToString() == "1")
                                        {
                                            if (Convert.ToDateTime(row["fecha"]) == Convert.ToDateTime(row["fecfact"]))
                                            {
                                                codigot   = row["codigoter"].ToString();
                                                lincred   = row["lincred"].ToString();
                                                nume_cre  = row["numero"].ToString();
                                                fecha     = Convert.ToDateTime(row["fecha"]);
                                                UsuarioDoc = row["usuario"].ToString();
                                            }
                                        }
                                    }
                                }
                                break;
                            default:
                                codigot   = row["codigoter"].ToString();
                                lincred   = row["lincred"].ToString();
                                nume_cre  = row["numero"].ToString();
                                fecha     = Convert.ToDateTime(row["fecha"]);
                                UsuarioDoc = row["usuario"].ToString();
                                nit       = row["nit"].ToString();
                                nombres   = row["nomprove"].ToString();
                                break;
                        }
                    }

                    if (tipo == "OC")
                    {
                        if (row["codigoter"].ToString().Trim() == "99999999999999" && row["lincred"].ToString().Trim() == "9999")
                        {
                            valor += Convert.ToDouble(row["vlr_debito"]) - Convert.ToDouble(row["vlr_credito"]);
                            nit     = row["nit"].ToString();
                            nombres = row["nomprove"].ToString();
                            string cuentaStr = row["cuenta"].ToString();
                            if (cuentaStr.Length >= 2 && cuentaStr.Substring(0, 2) == "24")
                                ValorCuentaOC += Convert.ToDouble(row["vlr_debito"]) - Convert.ToDouble(row["vlr_credito"]);
                        }
                    }
                    else
                    {
                        valor += Convert.ToDouble(row["vlr_debito"]);
                    }
                    fila++;
                }

                if (codigot == "")
                    return null;

                read.Dispose();

                string _combte = combte;
                double _compNume = 0;
                double.TryParse(comp_nume, out _compNume);
                BuscaComprobanteHelper(ref _combte, ref _compNume, conect, ref validadora, ref FormaImp, ref NumCopiasImp);

                int lincredInt   = int.TryParse(lincred,  out int _li)   ? _li   : 0;
                double numCreDbl = double.TryParse(nume_cre, out double _nc) ? _nc : 0;

                switch (tipo)
                {
                    case "OC":
                        //Ordencomercio orden = new Ordencomercio();
                        if (valor < 0) valor = valor * -1;
                        if (ValorCuentaOC < 0) ValorCuentaOC = ValorCuentaOC * -1;
                        //orden.conect           = conect;
                        //orden.fecha.Text       = fecha.ToShortDateString();
                        //orden.valor.Text       = valor.ToString();
                        //orden.prove.Text       = nit;
                        //orden.benef.Text       = codigot;
                        //orden.LblCuenta24.Text = ValorCuentaOC.ToString();
                        //orden.carga();
                        //if (orden.ShowDialog() == DialogResult.OK)
                        //{
                        //    nit     = VarIni.Pstcodigo;
                        //    nombres = VarIni.Pstnombre;
                        //    ImprimeORdenComercio(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, ValorCuentaOC, NumCopiasImp, validadora);
                        //}
                        if (MessageBox.Show("Desea imprimir la nota de cartera?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            ImprimeNotaCartera(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora);
                        break;

                    case "RC":
                        switch (FormaImp)
                        {
                            case "0":
                                ImprimeReciboCaja(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora);
                                break;
                            case "1":
                                ImprimeResumenReciboCaja(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora);
                                break;
                        }
                        break;

                    case "NC":
                    case "AC":
                    case "MP":
                        switch (FormaImp)
                        {
                            case "0":
                                ImprimeNotaCartera(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora);
                                break;
                            case "1":
                                ImprimeResumenNotaCartera(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora);
                                break;
                        }
                        break;

                    case "FC":
                        ImprimeFactura(combte, comp_nume, NumCopiasImp, validadora);
                        break;
                }
            }
            finally
            {
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // impre — overload 2 (bloque / rango de documentos)
        // ─────────────────────────────────────────────────────────────────────
        public object impre(string combte, string comp_nume, bool copia, string tipo,
            OdbcConnection conect, string comp_nume_fin, Form Myforma)
        {
            string FormaImp = "0";
            try
            {
                VarIni.sptCodEmpr = "0001";
                CargaEmpresa(conect);

                double valor = 0;
                string codigot = "", nombres = "", nit = "", lincred = "", nume_cre = "", tipodoc = "";
                string NumCopiasImp = "1", validadora = "0";
                int canreg = 0, fila = 0;
                StringBuilder StBuilder = new StringBuilder();
                ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Impresion por bloques", Myforma);
                string queryconsulta = "";

                if (tipo == "RC")
                    queryconsulta = ",case f.tipo_persona when  'N' then f.nombre else f.razon_social end as nomprove,b.nit as nit,b.codigoter ";

                StBuilder.Append("SELECT DISTINCT a.compronte,a.NUMERO_DOMTO,a.doctipo,b.usuario,");
                StBuilder.Append("compro.validadora,compro.forma_imprimir,compro.PUERT_VALIDADORA,");
                StBuilder.Append("(select SUM(movimo.VLR_CREDITO) from cop_movimto movimo where movimo.COMPRONTE = a.compronte and movimo.NUMERO_DOMTO  = a.NUMERO_DOMTO)  as vlr_debito  " + queryconsulta);
                StBuilder.Append(" FROM cop_docmto a ");
                StBuilder.Append("left join cop_movimto b on b.compronte = a.compronte and b.NUMERO_DOMTO = a.NUMERO_DOMTO ");
                StBuilder.Append("inner join cop_concar12 c on c.lincred = b.lincred ");
                StBuilder.Append("inner join sys_maenit d on d.codigoter = b.codigoter ");
                StBuilder.Append("inner join  sys_compro02 compro on a.compronte = compro.CODIGO  ");
                StBuilder.Append("left join cnt_nit f on f.nit = b.nit ");
                StBuilder.Append("left join cop_codmov codmov on b.cod_movto = codmov.cod_movto ");
                StBuilder.Append("left join cop_maecar maecar on b.codigoter = maecar.codigoter and b.lincred=maecar.lincred and b.numero=maecar.numero ");
                StBuilder.Append("where a.COMPRONTE ='" + Microsoft.VisualBasic.Strings.Right("0000" + combte, 4) + "' and a.NUMERO_DOMTO  between " + comp_nume.Trim() + " and " + comp_nume_fin.Trim());
                StBuilder.Append(" and  a.cerrado =  'Y' and  b.codigoter <>   '99999999999999'   AND b.lincred <> 9999 ");
                StBuilder.Append(" order by a.compronte,a.NUMERO_DOMTO  ");

                DataSet read = new DataSet();
                connect.ExecuteQueryDataset(StBuilder.ToString(), conect, "impre", ref read, "TblImpreDoc");
                canreg = read.Tables["TblImpreDoc"].Rows.Count;

                msgbarra.ValorMinimoMaximo(0, canreg);
                msgbarra.Show();

                while (fila < canreg)
                {
                    DataRow row = read.Tables["TblImpreDoc"].Rows[fila];
                    tipodoc = row["doctipo"].ToString();
                    if (tipo.Trim() == "")
                        tipo = tipodoc;

                    if (tipo == "RC")
                    {
                        codigot = row["codigoter"].ToString();
                        nombres = row["nomprove"].ToString();
                        nit     = row["nit"].ToString();
                    }

                    UsuarioDoc  = row["usuario"].ToString();
                    valor      += Convert.ToDouble(row["vlr_debito"]);
                    validadora  = "1";
                    FormaImp    = row["forma_imprimir"].ToString();
                    NumCopiasImp = row["PUERT_VALIDADORA"].ToString();
                    comp_nume   = row["NUMERO_DOMTO"].ToString();

                    msgbarra.Comentario("Imprimiendo Comprobante " + combte + "-" + comp_nume);

                    int lincredInt   = int.TryParse(lincred,  out int _li)   ? _li   : 0;
                    double numCreDbl = double.TryParse(nume_cre, out double _nc) ? _nc : 0;

                    switch (tipo)
                    {
                        case "OC":
                            break;
                        case "RC":
                            if (codigot.Trim() != "")
                            {
                                switch (FormaImp)
                                {
                                    case "0":
                                        ImprimeReciboCaja(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora, "Y");
                                        break;
                                    case "1":
                                        ImprimeResumenReciboCaja(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora, "Y");
                                        break;
                                }
                            }
                            break;
                        case "NC":
                        case "AC":
                        case "MP":
                            switch (FormaImp)
                            {
                                case "0":
                                    ImprimeNotaCartera(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora, "Y");
                                    break;
                                case "1":
                                    ImprimeResumenNotaCartera(nit, codigot, nombres, lincredInt, numCreDbl, valor, combte, comp_nume, copia, conect, NumCopiasImp, validadora, "Y");
                                    break;
                            }
                            break;
                        case "FC":
                            // ImprimeFactura(combte, comp_nume, NumCopiasImp, validadora);
                            break;
                    }

                    msgbarra.PerformStep();
                    fila++;
                    valor = 0;
                }
                read.Dispose();
                msgbarra.Close();
            }
            finally
            {
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ImprimeORdenComercio
        // ─────────────────────────────────────────────────────────────────────
        private void ImprimeORdenComercio(string nit, string codigot, string nombres,
            int lincred, double nume_cre, double valor, string combte, string comp_nume,
            bool copia, double VlrCuenta24,
            string NumCopiasImp = "1", string validadora = "0")
        {
            //MsgSas.Numeros_A_Letras num = new MsgSas.Numeros_A_Letras();
            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cop_rordencomercio");
            R.SetParameterValue("lincred", lincred);
            R.SetParameterValue("nume_cre", (int)nume_cre);
            R.SetParameterValue("empresa", VarIni.pstEmpresa);
            string stNumeletras = ""; // num.Num_a_Letras(valor) + "MLC";
            int pad = Math.Max(0, 140 - stNumeletras.Length);
            R.SetParameterValue("vlor_letras", stNumeletras + pstAsterisco.Substring(0, Math.Min(pad, pstAsterisco.Length)));
            R.SetParameterValue("codigoter", codigot);
            R.SetParameterValue("nombre_ter", nombres);
            R.SetParameterValue("ValorDoc", valor);
            R.SetParameterValue("tipo", VarIni.membrete ? 1 : 0);
            R.SetParameterValue("nit_ter", nit);
            try
            {
                R.SetParameterValue("VlrCuenta24", VlrCuenta24);
                stNumeletras = ""; // num.Num_a_Letras(VlrCuenta24) + "MLC";
                pad = Math.Max(0, 140 - stNumeletras.Length);
                R.SetParameterValue("VlrLetrasCuenta24", stNumeletras + pstAsterisco.Substring(0, Math.Min(pad, pstAsterisco.Length)));
            }
            catch { }
            try
            {
                R.SetParameterValue("compronte", Microsoft.VisualBasic.Strings.Right("0000" + combte, 4));
                R.SetParameterValue("numero_domto", Convert.ToDouble(comp_nume));
            }
            catch { }

            if (!Microsoft.VisualBasic.Information.IsNumeric(validadora))
                validadora = "0";

            if (validadora == "1")
            {
                System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                R.PrintOptions.PrinterName = instance.PrinterName;
                if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp))
                    NumCopiasImp = "0";
                R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
            }
            else
            {
                ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                IMP.Visible = false;
                //IMP.CrystalReportViewer1.ReportSource = R;
                //IMP.CrystalReportViewer1.PrintReport();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ImprimeReciboCaja
        // ─────────────────────────────────────────────────────────────────────
        private void ImprimeReciboCaja(string nit, string codigot, string nombres,
            int lincred, double nume_cre, double valor, string combte, string comp_nume,
            bool copia, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            //MsgSas.Numeros_A_Letras num = new MsgSas.Numeros_A_Letras();
            string Direccion = " ", Nomres = " ", Telefono = " ", CompaNit = " ", nomusu = " ";
            BuscarCompaniaHelper(VarIni.sptCodEmpr, Myconnect, ref CompaNit, ref Direccion, ref Nomres, ref Telefono);
            string _login = UsuarioDoc;
            //msgparsys.BuscaUsuario(ref _login, Myconnect, ERP.Core.Compartido.Configuracion.ParamSys.Navega.Ninguno, ref nomusu);

            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cop_recibocaja");
            R.SetParameterValue("compronte", Microsoft.VisualBasic.Strings.Right("0000" + combte, 4));
            R.SetParameterValue("numero_domto", Convert.ToDouble(comp_nume));
            R.SetParameterValue("total", valor);
            string stNumeletras = ""; // num.Num_a_Letras(valor) + "MLC";
            int pad = Math.Max(0, 140 - stNumeletras.Length);
            R.SetParameterValue("val_letras", stNumeletras + pstAsterisco.Substring(0, Math.Min(pad, pstAsterisco.Length)));
            R.SetParameterValue("codigoter", codigot);
            R.SetParameterValue("nombre", nombres);
            R.SetParameterValue("copia", copia ? "1" : "0");
            R.SetParameterValue("usuario", UsuarioDoc);
            R.SetParameterValue("nit", nit);
            R.SetParameterValue("empresa", VarIni.pstEmpresa);
            try { R.SetParameterValue("direccion", Direccion); } catch { }
            try { R.SetParameterValue("telefono", Telefono); } catch { }
            try
            {
                R.SetParameterValue("CompaNit", CompaNit);
                R.SetParameterValue("nomusu", nomusu);
            }
            catch { }

            if (ImprimeBloque == "N")
            {
                if (MessageBox.Show("Inserte el comprobante de ingreso : " + Convert.ToDouble(comp_nume) + "\r\n\r\nPresione SI cuando este Preparado...",
                    "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    if (!Microsoft.VisualBasic.Information.IsNumeric(validadora)) validadora = "0";
                    if (validadora == "1")
                    {
                        System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                        R.PrintOptions.PrinterName = instance.PrinterName;
                        if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                        R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
                    }
                    else
                    {
                        ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                        IMP.Visible = false;
                        //IMP.CrystalReportViewer1.ReportSource = R;
                        //IMP.CrystalReportViewer1.PrintReport();
                    }
                }
            }
            else
            {
                System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                R.PrintOptions.PrinterName = instance.PrinterName;
                if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ImprimeResumenReciboCaja
        // ─────────────────────────────────────────────────────────────────────
        private void ImprimeResumenReciboCaja(string nit, string codigot, string nombres,
            int lincred, double nume_cre, double valor, string combte, string comp_nume,
            bool copia, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
           // MsgSas.Numeros_A_Letras num = new MsgSas.Numeros_A_Letras();
            string Direccion = " ", Nomres = " ", Telefono = " ", CompaNit = " ", nomusu = " ";
            BuscarCompaniaHelper(VarIni.sptCodEmpr, Myconnect, ref CompaNit, ref Direccion, ref Nomres, ref Telefono);
            string _login = UsuarioDoc;
            //msgparsys.BuscaUsuario(ref _login, Myconnect, ERP.Core.Compartido.Configuracion.ParamSys.Navega.Ninguno, ref nomusu);
            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cop_recibocaja02");
            R.SetParameterValue("compronte", Microsoft.VisualBasic.Strings.Right("0000" + combte, 4));
            R.SetParameterValue("numero_domto", Convert.ToDouble(comp_nume));
            R.SetParameterValue("total", valor);
            string stNumeletras = ""; // num.Num_a_Letras(valor) + "MLC";
            int pad = Math.Max(0, 140 - stNumeletras.Length);
            R.SetParameterValue("val_letras", stNumeletras + pstAsterisco.Substring(0, Math.Min(pad, pstAsterisco.Length)));
            R.SetParameterValue("codigoter", codigot);
            R.SetParameterValue("nombre", nombres);
            R.SetParameterValue("copia", copia ? "1" : "0");
            R.SetParameterValue("usuario", UsuarioDoc);
            R.SetParameterValue("nit", nit);
            R.SetParameterValue("empresa", VarIni.pstEmpresa);
            try { R.SetParameterValue("direccion", Direccion); } catch { }
            try { R.SetParameterValue("telefono", Telefono); } catch { }
            try
            {
                R.SetParameterValue("CompaNit", CompaNit);
                R.SetParameterValue("nomusu", nomusu);
            }
            catch { }

            if (ImprimeBloque == "N")
            {
                if (MessageBox.Show("Inserte el comprobante de ingreso : " + Convert.ToDouble(comp_nume) + "\r\n\r\nPresione SI cuando este Preparado...",
                    "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    if (!Microsoft.VisualBasic.Information.IsNumeric(validadora)) validadora = "0";
                    if (validadora == "1")
                    {
                        System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                        R.PrintOptions.PrinterName = instance.PrinterName;
                        if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                        R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
                    }
                    else
                    {
                        ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                        IMP.Visible = false;
                        //IMP.CrystalReportViewer1.ReportSource = R;
                        //IMP.CrystalReportViewer1.PrintReport();
                    }
                }
            }
            else
            {
                System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                R.PrintOptions.PrinterName = instance.PrinterName;
                if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ImprimeNotaCartera
        // ─────────────────────────────────────────────────────────────────────
        private void ImprimeNotaCartera(string nit, string codigot, string nombres,
            int lincred, double nume_cre, double valor, string combte, string comp_nume,
            bool copia, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            //MsgSas.Numeros_A_Letras num = new MsgSas.Numeros_A_Letras();
            string Direccion = " ", Nomres = " ", Telefono = " ", CompaNit = " ";
            BuscarCompaniaHelper(VarIni.sptCodEmpr, Myconnect, ref CompaNit, ref Direccion, ref Nomres, ref Telefono);

            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cop_rnotacartera");
            R.SetParameterValue("compro", Microsoft.VisualBasic.Strings.Right("0000" + combte, 4));
            R.SetParameterValue("numero", Convert.ToDouble(comp_nume));
            R.SetParameterValue("empresa", VarIni.pstEmpresa);
            string stNumeletras = ""; // num.Num_a_Letras(valor) + "MLC";
            int pad = Math.Max(0, 140 - stNumeletras.Length);
            R.SetParameterValue("letras", stNumeletras + pstAsterisco.Substring(0, Math.Min(pad, pstAsterisco.Length)));
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
                if (MessageBox.Show("Inserte la nota : " + Convert.ToDouble(comp_nume) + "\r\n\r\nPresione SI cuando este Preparado...",
                    "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    if (!Microsoft.VisualBasic.Information.IsNumeric(validadora)) validadora = "0";
                    if (validadora == "1")
                    {
                        System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                        R.PrintOptions.PrinterName = instance.PrinterName;
                        if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                        R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
                    }
                    else
                    {
                        ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                        IMP.Visible = false;
                        //IMP.CrystalReportViewer1.ReportSource = R;
                        //IMP.CrystalReportViewer1.PrintReport();
                    }
                }
            }
            else
            {
                System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                R.PrintOptions.PrinterName = instance.PrinterName;
                if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ImprimeResumenNotaCartera
        // ─────────────────────────────────────────────────────────────────────
        private void ImprimeResumenNotaCartera(string nit, string codigot, string nombres,
            int lincred, double nume_cre, double valor, string combte, string comp_nume,
            bool copia, OdbcConnection Myconnect,
            string NumCopiasImp = "1", string validadora = "0", string ImprimeBloque = "N")
        {
            //MsgSas.Numeros_A_Letras num = new MsgSas.Numeros_A_Letras();
            string Direccion = " ", Nomres = " ", Telefono = " ", CompaNit = " ";
            BuscarCompaniaHelper(VarIni.sptCodEmpr, Myconnect, ref CompaNit, ref Direccion, ref Nomres, ref Telefono);

            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("cop_rnotacartera02");
            R.SetParameterValue("compro", Microsoft.VisualBasic.Strings.Right("0000" + combte, 4));
            R.SetParameterValue("numero", Convert.ToDouble(comp_nume));
            R.SetParameterValue("empresa", VarIni.pstEmpresa);
            string stNumeletras = ""; // num.Num_a_Letras(valor) + "MLC";
            int pad = Math.Max(0, 140 - stNumeletras.Length);
            R.SetParameterValue("letras", stNumeletras + pstAsterisco.Substring(0, Math.Min(pad, pstAsterisco.Length)));
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
                if (MessageBox.Show("Inserte la nota : " + Convert.ToDouble(comp_nume) + "\r\n\r\nPresione SI cuando este Preparado...",
                    "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    if (!Microsoft.VisualBasic.Information.IsNumeric(validadora)) validadora = "0";
                    if (validadora == "1")
                    {
                        System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                        R.PrintOptions.PrinterName = instance.PrinterName;
                        if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                        R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
                    }
                    else
                    {
                        ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                        IMP.Visible = false;
                        //IMP.CrystalReportViewer1.ReportSource = R;
                        //IMP.CrystalReportViewer1.PrintReport();
                    }
                }
            }
            else
            {
                System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                R.PrintOptions.PrinterName = instance.PrinterName;
                if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp)) NumCopiasImp = "0";
                R.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ImprimeFactura
        // ─────────────────────────────────────────────────────────────────────
        public object ImprimeFactura(string Compronte, string NumeroDomto,
            string NumCopiasImp = "1", string validadora = "0")
        {
            //CrystalDecisions.CrystalReports.Engine.ReportDocument factura =
            //    new ERP.Core.Compartido.Reportes.reporte("cop_factura1");
            //factura.SetParameterValue("compronte", Compronte);
            //factura.SetParameterValue("numero", Convert.ToDouble(NumeroDomto));

            if (!Microsoft.VisualBasic.Information.IsNumeric(validadora))
                validadora = "0";

            if (validadora == "1")
            {
                System.Drawing.Printing.PrinterSettings instance = new System.Drawing.Printing.PrinterSettings();
                //factura.PrintOptions.PrinterName = instance.PrinterName;
                if (!Microsoft.VisualBasic.Information.IsNumeric(NumCopiasImp))
                    NumCopiasImp = "0";
                //factura.PrintToPrinter((int)double.Parse(NumCopiasImp) + 1, false, 0, 0);
            }
            else
            {
                ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
                IMP.Visible = false;
                //IMP.CrystalReportViewer1.ReportSource = factura;
                //IMP.CrystalReportViewer1.PrintReport();
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // imp_libran_pagare
        // ─────────────────────────────────────────────────────────────────────
        public void imp_libran_pagare(int num_solicitud, OdbcConnection Myconnect,
            string clades = "0", double NumPagareCart = 0,
            int lincred = -1, string codigoter = "", int Numero = -1)
        {
            ERP.Core.Compartido.Forms.imprimir IMP = new ERP.Core.Compartido.Forms.imprimir();
            //MsgSas.Numeros_A_Letras num = new MsgSas.Numeros_A_Letras();
            string VALOR, VlrSolicitud;
            double NumPagare = 0, libranza = 0;
            string cladescuen, SPagare;
            string sql;
            IMP.Visible = false;

            StringBuilder StBuilder = new StringBuilder();
            DataSet DsSolic = new DataSet(), DsCompania = new DataSet();
            bool Ok = false;
            bool export = false;
            SaveFileDialog archi = new SaveFileDialog();
            string imprimirMode = "Imprimir";
            string NomPagador = "";
            bool solcred;

            StBuilder.Append("select a.clades,a.vlr_solicitud,a.plazo,a.cuota,a.fecha_programada,a.numpagare,a.numlibranza,b.filler1,a.VALOR_APROBADO ");
            StBuilder.Append("from cop_solcre a ");
            StBuilder.Append("left join cop_maecar b on a.numero=b.numero_soli ");
            StBuilder.Append("where a.numero=" + num_solicitud);

            Ok = connect.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "imp_libran_pagare", ref DsSolic, "tblsolic");

            if (!Ok)
            {
                solcred = false;
                StBuilder = new StringBuilder();
                StBuilder.Append("select a.clades,a.valorob as vlr_solicitud,a.plazo,a.cuota,a.FECFACT as fecha_programada,0 as numpagare,0 as numlibranza,a.filler1,0 as VALOR_APROBADO");
                StBuilder.Append(" from cop_maecar a ");
                StBuilder.Append(" WHERE a.numero=" + Numero);
                StBuilder.Append(" and a.codigoter='" + codigoter);
                StBuilder.Append("' and a.lincred=" + lincred);
                connect.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "imp_libran_pagare", ref DsSolic, "tblsolic");
            }
            else
            {
                solcred = true;
            }

            //CrystalDecisions.CrystalReports.Engine.ReportDocument r =
            //    new CrystalDecisions.CrystalReports.Engine.ReportDocument();

            DataRow rowSolic = DsSolic.Tables["tblsolic"].Rows[0];

            if (Convert.ToDouble(rowSolic["VALOR_APROBADO"]) != 0)
                VALOR = ""; // num.Num_a_Letras(Convert.ToDouble(rowSolic["VALOR_APROBADO"])).ToUpper();
            else
                VALOR = ""; // num.Num_a_Letras(Convert.ToDouble(rowSolic["vlr_solicitud"])).ToUpper();

            VlrSolicitud = VALOR;
            NumPagare = Convert.ToDouble(rowSolic["numpagare"]);

            if (NumPagare == 0)
            {
                if (Microsoft.VisualBasic.Information.IsNumeric(rowSolic["filler1"]))
                    NumPagare = Convert.ToDouble(rowSolic["filler1"]);
            }

            if (MessageBox.Show("DESEA EXPORTAR LOS REPORTES A FORMATO PDF ?", "SOLIDO",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                export = true;
                imprimirMode = "Exportar";
            }

            cladescuen = "1";

            if (cladescuen == "1")
            {
                if (MessageBox.Show("Desea " + imprimirMode + " el pagaré?", "SOLIDO",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    string _codEmpr = VarIni.sptCodEmpr;
                    msgparsys.BuscarCompania(ref _codEmpr, ref DsCompania, Myconnect);

                    string formaPagare = DsCompania.Tables["tblcompania"].Rows[0]["formapagare"].ToString();
                    switch (formaPagare)
                    {
                        case "1":
                            if (NumPagare <= 0)
                            {
                                if (rowSolic["filler1"] != DBNull.Value)
                                {
                                    try { NumPagare = Convert.ToDouble(rowSolic["filler1"]); }
                                    catch { }
                                }
                            }
                            break;
                        case "2":
                            SPagare = Microsoft.VisualBasic.Interaction.InputBox(
                                "Ingrese el numero del pagaré", "",
                                (NumPagare == 0 ? num_solicitud : (int)NumPagare).ToString());
                            if (!Microsoft.VisualBasic.Information.IsNumeric(SPagare))
                                NumPagare = 0;
                            else
                                NumPagare = Convert.ToDouble(SPagare);
                            if (solcred)
                            {
                                sql = "update cop_solcre set numpagare=" + NumPagare + " where numero=" + num_solicitud;
                                ExecSql(sql, Myconnect, "Actualiza Libranza (ImpreDoc)");
                            }
                            break;
                    }

                    if (NumPagare > 0 || NumPagareCart > 0)
                    {
                        //r = new ERP.Core.Compartido.Reportes.reporte("cop_pagare");
                        //try { r.SetParameterValue("letras", VlrSolicitud); } catch { }

                        //r.SetParameterValue("numero", num_solicitud.ToString().Trim());
                        //r.SetParameterValue("pagare", NumPagare);
                        try
                        {
                            VALOR = ""; // num.Num_a_Letras(Convert.ToDouble(DateTime.Now.Day)).ToUpper();
                            VALOR = VALOR.Replace("DE ", "").Replace("PESOS", "");
                            //r.SetParameterValue("dia", VALOR);
                        }
                        catch { }

                        try
                        {
                            VALOR = ""; // num.Num_a_Letras(double.Parse(DateTime.Now.Year.ToString().Substring(2))).ToUpper();
                            VALOR = VALOR.Replace("DE ", "").Replace("PESOS", "");
                            //r.SetParameterValue("año", VALOR);
                        }
                        catch { }

                        try
                        {
                            //r.SetParameterValue("codigoter", codigoter);
                            //r.SetParameterValue("lincred", lincred);
                            //r.SetParameterValue("numCredito", Numero);
                        }
                        catch { }

                        if (export)
                        {
                            //if (archi.ShowDialog() != DialogResult.OK)
                            //    goto SkipPagareExport;
                           // r.ExportToDisk(ExportFormatType.WordForWindows,
                            //    archi.FileName + " - Pagare " + num_solicitud + ".doc");
                            //SkipPagareExport:;
                        }
                        else
                        {
                            //IMP.CrystalReportViewer1.ReportSource = r;
                            //IMP.CrystalReportViewer1.PrintReport();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Obligación o Solicitud de Crédito no tiene pagaré asignado",
                            "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                if (MessageBox.Show("Desea " + imprimirMode + " la Libranza?", "SOLIDO",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    //r = new ERP.Core.Compartido.Reportes.reporte("cop_rlibranza");
                    //r.SetParameterValue("numero", num_solicitud.ToString().Trim());

                    if (solcred)
                    {
                        libranza = Convert.ToDouble(rowSolic["numlibranza"]) != 0
                            ? Convert.ToDouble(rowSolic["numlibranza"])
                            : num_solicitud;
                    }
                    else
                    {
                        libranza = Convert.ToDouble(rowSolic["numpagare"]) != 0
                            ? Convert.ToDouble(rowSolic["numpagare"])
                            : Numero;
                    }

                    string inputLibranza = Microsoft.VisualBasic.Interaction.InputBox(
                        "Ingrese el numero de la libranza", "SOLIDO", libranza.ToString());
                    if (!string.IsNullOrEmpty(inputLibranza) && double.TryParse(inputLibranza, out double newLibranza))
                        libranza = newLibranza;

                    if (solcred)
                    {
                        sql = "update cop_solcre set numlibranza=" + libranza + " where numero=" + num_solicitud;
                        ExecSql(sql, Myconnect, "Actualiza Libranza (ImpreDoc)");
                    }

                    //r.SetParameterValue("libranza", libranza);
                    //try { r.SetParameterValue("letras", VALOR); } catch { }

                    VALOR = "";// num.Num_a_Letras(Convert.ToDouble(rowSolic["plazo"])).ToUpper();
                    VALOR = VALOR.Replace("DE ", "").Replace("PESOS", "");
                    //try { r.SetParameterValue("letraplazo", VALOR); } catch { }

                    //VALOR = num.Num_a_Letras(Convert.ToDouble(rowSolic["CUOTA"])).ToUpper();
                    //try { r.SetParameterValue("CUOTA", VALOR); } catch { }

                    try
                    {
                        //if (rowSolic["FECHA_PROGRAMADA"] != DBNull.Value)
                        //    VALOR = num.Num_a_Letras(Convert.ToDouble(Convert.ToDateTime(rowSolic["FECHA_PROGRAMADA"]).Day)).ToUpper();
                        //else
                        //    VALOR = " ";
                        //VALOR = VALOR.Replace("DE ", "").Replace("PESOS", "");
                        //r.SetParameterValue("dia", VALOR);
                    }
                    catch { }

                    try
                    {
                        //r.SetParameterValue("pagador_Nomb", "");
                        //NomPagador = Microsoft.VisualBasic.Interaction.InputBox("Ingrese el Nombre del Pagador ", "SOLIDO");
                        //r.SetParameterValue("pagador_Nomb", NomPagador);
                    }
                    catch { }

                    try
                    {
                        //r.SetParameterValue("codigoter", codigoter);
                        //r.SetParameterValue("lincred", lincred);
                        //r.SetParameterValue("numCredito", Numero);
                    }
                    catch { }

                    //try { r.SetParameterValue("pagare", NumPagare); } catch { }

                    try
                    {
                        //r.SetParameterValue("interes", 0);
                        //DataSet datos = new DataSet();
                        //double interes = datasetProyeccion(num_solicitud, Myconnect, ref datos);
                        //r.SetParameterValue("interes", interes);
                        //r.SetParameterValue("DetInteres", "");
                        //double conta = 0;
                        //string intereses = null;
                        //while (datos.Tables[0].Rows.Count > conta)
                        //{
                        //    if (Convert.ToDouble(datos.Tables[0].Rows[(int)conta]["CuotaExtra"]) != 0)
                        //        intereses += Microsoft.VisualBasic.Strings.Right("0000000000" + datos.Tables[0].Rows[(int)conta]["interes"].ToString(), 10);
                        //    conta += 1;
                        //}
                        //r.SetParameterValue("DetInteres", intereses);
                    }
                    catch { }

                    //if (export)
                    //{
                    //    if (archi.ShowDialog() != DialogResult.OK)
                    //        goto SkipLibranzaExport;
                    //    r.ExportToDisk(ExportFormatType.WordForWindows,
                    //        archi.FileName + " - Libranza " + libranza + ".doc");
                    //    SkipLibranzaExport:;
                    //}
                    //else
                    //{
                    //    IMP.CrystalReportViewer1.ReportSource = r;
                    //    IMP.CrystalReportViewer1.PrintReport();
                    //}
                }
            }

            if (MessageBox.Show("Desea " + imprimirMode + " la carta de instrucciones.?", "SOLIDO",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
            {
                if (NumPagare != 0 || NumPagareCart != 0)
                {
                    //r = new ERP.Core.Compartido.Reportes.reporte("cop_cartainstru");
                    //try
                    //{
                    //    r.SetParameterValue("Numero", num_solicitud);
                    //    r.SetParameterValue("letras", VlrSolicitud);
                    //}
                    //catch { }
                    //try
                    //{
                    //    r.SetParameterValue("codigoter", codigoter);
                    //    r.SetParameterValue("lincred", lincred);
                    //    r.SetParameterValue("numCredito", Numero);
                    //}
                    //catch { }
                    //try
                    //{
                    //    r.SetParameterValue("Interes", 0);
                    //    DataSet datos = new DataSet();
                    //    double interes = datasetProyeccion(num_solicitud, Myconnect, ref datos);
                    //    r.SetParameterValue("interes", interes);
                    //    double neto = Convert.ToDouble(datos.Tables["TbldatosCredito"].Rows[0]["valorCredito"])
                    //                - Convert.ToDouble(datos.Tables["TbldatosCredito"].Rows[0]["ValMenos"]);
                    //    r.SetParameterValue("Neto", neto);
                    //}
                    //catch { }

                    //if (export)
                    //{
                    //    if (archi.ShowDialog() != DialogResult.OK)
                    //        goto SkipCartaExport;
                    //    r.ExportToDisk(ExportFormatType.WordForWindows,
                    //        archi.FileName + " - Carta Instrucciones " + num_solicitud + ".doc");
                    //    SkipCartaExport:;
                    //}
                    //else
                    //{
                    //    IMP.CrystalReportViewer1.ReportSource = r;
                    //    IMP.CrystalReportViewer1.PrintReport();
                    //}
                }
                else
                {
                    MessageBox.Show("Obligación o Solicitud de Crédito no tiene pagaré asignado",
                        "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // datasetProyeccion
        // ─────────────────────────────────────────────────────────────────────
        public double datasetProyeccion(double NumSolicitud, OdbcConnection myconnect,
            ref DataSet DataPoyeccion)
        {
            DataSet  dsdeduccion = new DataSet(), dsextras = new DataSet(), dsproyeccion;
            DataTable dssolicitud;
            double NumeroSol = -1;
            bool PideVal = true;
            DataSet Dsparametros = new DataSet();
            double VlrInteresCierre = 0, VlrAprobado = 0;

            try
            {
                //dssolicitud = liqcre.BuscaSolicitudesCredito(NumSolicitud, myconnect);
                //dsdeduccion = liqcre.BuscarDeducciones(NumSolicitud, myconnect);
                //liqcre.BuscarExtras(NumSolicitud, ref dsextras, myconnect);

                //DataRow rowSolic = dssolicitud.Rows[0];
                //int _lin = Convert.ToInt32(rowSolic["lincred"]);
                //msgparcop.BuscaLinea(ref _lin, ref Dsparametros, myconnect);
                //VlrAprobado = Convert.ToDouble(rowSolic["valor_aprobado"]);

                //if (Dsparametros.Tables["tbllineas"].Rows[0]["intcie"].ToString() == "3")
                //{
                //    if (Convert.ToDouble(rowSolic["VLR_SOLICITUD"]) == VlrAprobado)
                //    {
                //        VlrInteresCierre = Convert.ToDouble(rowSolic["cuota_icie"]);
                //        rowSolic["valor_aprobado"] = Convert.ToDouble(rowSolic["valor_aprobado"]) - Convert.ToDouble(rowSolic["cuota_icie"]);
                //    }
                //}

                //if (Dsparametros.Tables["tbllineas"].Rows[0]["sumaga"].ToString() == "2")
                //{
                //    if (Convert.ToDouble(rowSolic["VLR_SOLICITUD"]) == VlrAprobado)
                //    {
                //        if (Dsparametros.Tables["tbllineas"].Rows[0]["poapen"] == DBNull.Value)
                //            rowSolic["valor_aprobado"] = Convert.ToDouble(rowSolic["valor_aprobado"]) - Convert.ToDouble(rowSolic["cuota_seg"]);
                //        else if (Dsparametros.Tables["tbllineas"].Rows[0]["poapen"].ToString() != "7")
                //            rowSolic["valor_aprobado"] = Convert.ToDouble(rowSolic["valor_aprobado"]) - Convert.ToDouble(rowSolic["cuota_seg"]);

                //        if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"] == DBNull.Value)
                //            rowSolic["valor_aprobado"] = Convert.ToDouble(rowSolic["valor_aprobado"]) - Convert.ToDouble(rowSolic["cuota_adm"]);
                //        else if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"].ToString() != "7")
                //            rowSolic["valor_aprobado"] = Convert.ToDouble(rowSolic["valor_aprobado"]) - Convert.ToDouble(rowSolic["cuota_adm"]);

                //        rowSolic["valor_aprobado"] = Convert.ToDouble(rowSolic["valor_aprobado"]) - Convert.ToDouble(rowSolic["cuota_cptl"]);
                //    }
                //}

                //if (rowSolic["fecha_programada"] == DBNull.Value)
                //    rowSolic["fecha_programada"] = rowSolic["fecha_soli"];
                //if (Convert.ToDouble(rowSolic["valor_aprobado"]) == 0)
                //    rowSolic["valor_aprobado"] = rowSolic["vlr_solicitud"];

                //DataTable extrasTable = dsextras.Tables["tblextras"];
                //double _u15 = 0, _u16 = 0, _u22 = 0;
                //dsproyeccion = liqcre.GeneraProyeccion(
                //    rowSolic["codigoter"].ToString(),
                //    Convert.ToInt32(rowSolic["lincred"]),
                //    Convert.ToDateTime(rowSolic["fecha_programada"]),
                //    Convert.ToDecimal(rowSolic["plazo"]),
                //    (ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos.Periodicidad)Convert.ToInt32(rowSolic["periodd"]),
                //    Convert.ToInt32(rowSolic["clades"]),
                //    Convert.ToInt32(rowSolic["ciclod"]),
                //    Convert.ToDecimal(rowSolic["tasa_int"]),
                //    Convert.ToDateTime(rowSolic["fecdesc"]),
                //    Convert.ToDouble(rowSolic["valor_aprobado"]),
                //    ref extrasTable,
                //    dsdeduccion.Tables["tbldeducciones"],
                //    rowSolic["pergraini"].ToString(),
                //    myconnect,
                //    ref _u15, ref _u16,
                //    Convert.ToInt32(rowSolic["clacuo"]),
                //    false, "",
                //    NumeroSol, PideVal,
                //    ref _u22,
                //    ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos.OpcionProyeccion.Solicitud);

                //DataPoyeccion = dsproyeccion;

                //if (DataPoyeccion.Tables["Tblproyeccion"].Rows.Count > 0)
                //    VlrInteresCierre = Convert.ToDouble(DataPoyeccion.Tables["Tblproyeccion"].Compute("sum(Interes)", ""));

                //return VlrInteresCierre;
            }
            catch (Exception ex)
            {
                MessageBox.Show("ImpreDoc-datasetProyeccion: " + ex.Message, "SOLIDO",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            return 0;
        }
    }
}
