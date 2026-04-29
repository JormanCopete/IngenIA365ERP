using System;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using Microsoft.VisualBasic;
#if CRYSTAL_LEGACY
using CrystalDecisions.CrystalReports.Engine;
#endif

namespace ERP.Core.CarteraFinanciera.Services.Cartera
{
    public partial class Clscartera
    {
        // Missing methods from VB lines 27309-32888

        #region GrabaPlanoDeudasPatronales (VB line 27309)

        private void GrabaPlanoDeudasPatronales(string NombreArchivo, string Delimitador, string TipoIden, string Cedula, double Saldo,
            double Provision, DateTime FechaContabilizacion, int diasMora)
        {
            using (StreamWriter StrDisp = File.AppendText(NombreArchivo))
            {
                StrDisp.Write(TipoIden + Delimitador);
                StrDisp.Write(Cedula + Delimitador);
                StrDisp.Write(Saldo + Delimitador);
                StrDisp.Write(Provision + Delimitador);
                StrDisp.Write(FechaContabilizacion.ToString("dd/MM/yyyy") + Delimitador);
                StrDisp.Write(diasMora);
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        #endregion

        #region BorrarDocumentoContabilidad (VB line 29048)

        private void BorrarDocumentoContabilidad(string Comprobanteborrarcnt, double NumeroBorrarcnta, Form Myforma, OdbcConnection myconnect)
        {
            if (Information.IsNumeric(NumeroBorrarcnta) == false || Information.IsNumeric(Comprobanteborrarcnt) == false)
            {
                MessageBox.Show("Debe digitar el documento a borrar", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Comprobanteborrarcnt = Strings.Right("0000" + Comprobanteborrarcnt, 4);

            DataSet DsMovimto = new DataSet();
            int TotReg = 0;
            int Contador = 0;
            string Mysql = "select secuencia, compronte, numero, periodo from cnt_movimto where compronte='" + Comprobanteborrarcnt + "' and numero=" + NumeroBorrarcnta + " order by secuencia desc";
            ERP.Core.Compartido.Controles.Barraprogress Barra = new ERP.Core.Compartido.Controles.Barraprogress("Borrando Movimientos del comprobante " + Comprobanteborrarcnt + "-" + NumeroBorrarcnta, Myforma);
            ok = this.OdbcConnect.ExecuteQueryDataset(Mysql, myconnect, "BtnBorrarcomprobante_Click", DsMovimto, "Movimto");
            OdbcDataAdapter adaptador = new OdbcDataAdapter(Mysql, myconnect);
            OdbcCommandBuilder sqlbuilder = new OdbcCommandBuilder(adaptador);
            Barra.Show();
            Barra.ValorMinimoMaximo(0, DsMovimto.Tables["Movimto"].Rows.Count);
            if (ok == true)
            {
                while (Contador < DsMovimto.Tables["Movimto"].Rows.Count)
                {
                    DataRow row = DsMovimto.Tables[0].Rows[Contador];
                    // this.msgcnt.BorraMovimiento(row["compronte"].ToString(), row["numero"].ToString(), row["secuencia"].ToString(), row["periodo"].ToString(), myconnect, varini.pstUsuario); // ERROR: CS1503
                    Barra.PerformStep();
                    Contador += 1;
                }
                Mysql = "delete from cnt_docmto where compronte='" + Comprobanteborrarcnt + "' and numero=" + NumeroBorrarcnta;
                ok = this.OdbcConnect.ExecuteQueryconec(Mysql, myconnect, "BtnBorrarcomprobante_Click");
            }
            else
            {
                MessageBox.Show("No existe el documento " + Comprobanteborrarcnt + "-" + NumeroBorrarcnta, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DsMovimto.Dispose();
            Barra.Close();
            Barra.Dispose();
        }

        #endregion

        #region OrganizaDatosCifinCdatAhorros (VB line 30307)

        private DataSet OrganizaDatosCifinCdatAhorros(string empresa, string tiporeporte, DateTime fechafinal, string paquete, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando Datos CIFIN", myforma);
            DataSet dsdatacifin = new DataSet();
            DataSet dsreporte = new DataSet();
            double fila = 0;
            string obligacion;
            string StFinExcenta = "";
            string fecapertura;
            string fectermino;
            string fecexcencion;

            MsgBarra.Show();

            creaDatasetIncluidosCifinCdtAhorro(ref dsreporte);

            switch (paquete)
            {
                case "23":
                    stbuilder.Append("select CASE maenit.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit,");
                    stbuilder.Append("maenit.NIT AS CedulaNit, maenit.APELLIDO, maenit.NOMBRE, CASE maenit.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza,");
                    stbuilder.Append("maenit.DIRECCION, maenit.TELEFONO1, maenit.EMPRESA,maenit.EMPRESA_LABORA,maenit.DIRECCION_ENVIO as DirEmpresa,maenit.telefono2 as TelEmpresa,");
                    stbuilder.Append("maenit.DPTO_CIUDAD,ciudad57.NOMBRE_CIUDAD, ciudad57.DPTO,maeahor.lincred,maeahor.num_cuenta as numero,maeahor.fec_crea as fecapertura,maeahor.Estado,maeahor.fec_novedad as fectermino,");
                    stbuilder.Append("maeahor.tipocuenta,maeahor.Excenta,maeahor.fec_exepcion, maeahor.cc_nit_firmareq1,maeahor.cc_nit_firmareq2,maeahor.cc_nit_firmareq3, maeahor.nom_firmareq1,maeahor.nom_firmareq2,maeahor.nom_firmareq3, ");
                    stbuilder.Append("'00' as plazocdt,'00' as numrenov ");
                    stbuilder.Append("from dep_maeahor maeahor ");
                    stbuilder.Append("inner join sys_maenit maenit on maenit.CODIGOTER=maeahor.codigoter ");
                    stbuilder.Append("INNER JOIN sys_ciudad57 ciudad57 ON maenit.DPTO_CIUDAD = ciudad57.CIUDAD ");
                    stbuilder.Append("where fec_crea<='" + fechafinal.ToString(varini.PstForFec) + "' ");
                    if (empresa.Trim() != "Todas")
                    {
                        stbuilder.Append(" and maenit.empresa='" + Strings.Right("0000" + empresa.Trim(), 4) + "' ");
                    }
                    stbuilder.Append("order by maeahor.CODIGOTER,maeahor.lincred,maeahor.num_cuenta ");
                    break;
                case "24":
                    stbuilder.Append("select CASE maenit.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit,");
                    stbuilder.Append("maenit.NIT AS CedulaNit, maenit.APELLIDO, maenit.NOMBRE, CASE maenit.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza,");
                    stbuilder.Append("maenit.DIRECCION, maenit.TELEFONO1, maenit.EMPRESA,maenit.EMPRESA_LABORA,maenit.DIRECCION_ENVIO as DirEmpresa,maenit.telefono2 as TelEmpresa,");
                    stbuilder.Append("maenit.DPTO_CIUDAD,ciudad57.NOMBRE_CIUDAD, ciudad57.DPTO,maecdt.lincred,maecdt.num_cdat as numero,maecdt.fec_crea as fecapertura,maecdt.Estado,maecdt.fecvence as fectermino,");
                    stbuilder.Append("maecdt.tipocdat as tipocuenta,' ' as Excenta,' ' as fec_exepcion, maecdt.cc_nit_firmareq1,maecdt.cc_nit_firmareq2,maecdt.cc_nit_firmareq3, maecdt.nom_firmareq1,maecdt.nom_firmareq2,maecdt.nom_firmareq3, ");
                    stbuilder.Append("maecdt.Plazo as plazocdt,CdtNov.cantrenov as numrenov ,CdtNov.fechaapertura ");
                    stbuilder.Append("from cdt_maecdats  maecdt ");
                    stbuilder.Append("inner join sys_maenit maenit on maenit.CODIGOTER=maecdt.codigoter ");
                    stbuilder.Append("INNER JOIN sys_ciudad57 ciudad57 ON maenit.DPTO_CIUDAD = ciudad57.CIUDAD ");
                    stbuilder.Append("LEFT JOIN  cdt_novcdats02_vw AS CdtNov ON maecdt.codigoter = CdtNov.codigoter and maecdt.lincred = CdtNov.lincred and maecdt.num_cdat = CdtNov.numcdat  ");
                    stbuilder.Append("where fec_crea<='" + fechafinal.ToString(varini.PstForFec) + "' ");
                    if (empresa.Trim() != "Todas")
                    {
                        stbuilder.Append(" and maenit.empresa='" + Strings.Right("0000" + empresa.Trim(), 4) + "' ");
                    }
                    stbuilder.Append("order by maecdt.CODIGOTER,maecdt.lincred,maecdt.num_cdat ");
                    break;
            }

            if (stbuilder.ToString().Trim() != "")
            {
                ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "OrganizaDatosCifinCdatAhorros", dsdatacifin, "tblcifin");

                if (ok == true)
                {
                    MsgBarra.ValorMinimoMaximo(0, dsdatacifin.Tables["tblcifin"].Rows.Count);

                    for (fila = 0; fila <= dsdatacifin.Tables["tblcifin"].Rows.Count - 1; fila++)
                    {
                        DataRow row = dsdatacifin.Tables["tblcifin"].Rows[(int)fila];
                        obligacion = Strings.Right("00000000000000" + row["CedulaNit"], 14) + Strings.Right("0000" + row["lincred"], 4) + Strings.Right("000000000000" + row["numero"], 12);
                        row["CedulaNit"] = Strings.Right("000000000000000" + row["CedulaNit"], 15);

                        fecapertura = "";
                        fectermino = "";
                        fecexcencion = "";

                        switch (paquete)
                        {
                            case "23":
                                if (row["fecapertura"] is DBNull)
                                    fecapertura = Strings.Space(8);
                                else
                                    fecapertura = Convert.ToDateTime(row["fecapertura"]).ToString("yyyyMMdd");

                                switch (row["Estado"].ToString())
                                {
                                    case "3":
                                        if (row["fectermino"] is DBNull)
                                            fectermino = Convert.ToDateTime(fechafinal).ToString("yyyyMMdd");
                                        else
                                            fectermino = Convert.ToDateTime(row["fectermino"]).ToString("yyyyMMdd");
                                        break;
                                    default:
                                        fectermino = Strings.Space(8);
                                        break;
                                }

                                switch (row["Estado"].ToString())
                                {
                                    case "2":
                                        row["Estado"] = "03"; // Inactiva
                                        break;
                                    case "3":
                                        row["Estado"] = "06"; // Cancelada
                                        break;
                                    default:
                                        row["Estado"] = "01"; // Normal
                                        break;
                                }

                                switch (row["tipocuenta"].ToString())
                                {
                                    case "0":
                                        row["tipocuenta"] = "03";
                                        break;
                                    case "1":
                                        row["tipocuenta"] = "02";
                                        break;
                                    case "2":
                                        row["tipocuenta"] = "01";
                                        break;
                                    case "3":
                                        row["tipocuenta"] = "04";
                                        break;
                                    default:
                                        row["tipocuenta"] = "03";
                                        break;
                                }
                                break;

                            case "24":
                                if (row["numrenov"] is DBNull)
                                {
                                    row["numrenov"] = "0";
                                }

                                if (row["fecapertura"] is DBNull)
                                {
                                    fecapertura = Strings.Space(8);
                                }
                                else
                                {
                                    if (row["Estado"].ToString() == "A" && Convert.ToDouble(row["numrenov"]) != 0)
                                    {
                                        if (row["fecapertura"] is DBNull)
                                            fecapertura = Convert.ToDateTime(row["fecapertura"]).ToString("yyyyMMdd");
                                        else
                                            fecapertura = Convert.ToDateTime(row["fechaapertura"]).ToString("yyyyMMdd");
                                    }
                                    else
                                    {
                                        fecapertura = Convert.ToDateTime(row["fecapertura"]).ToString("yyyyMMdd");
                                    }
                                }

                                switch (row["tipocuenta"].ToString())
                                {
                                    case "0":
                                        row["tipocuenta"] = "01";
                                        break;
                                    case "1":
                                        row["tipocuenta"] = "02";
                                        break;
                                    case "2":
                                        row["tipocuenta"] = "03";
                                        break;
                                    default:
                                        row["tipocuenta"] = "01";
                                        break;
                                }

                                switch (row["Estado"].ToString())
                                {
                                    case "C":
                                        if (row["fectermino"] is DBNull)
                                            fectermino = Convert.ToDateTime(fechafinal).ToString("yyyyMMdd");
                                        else
                                            fectermino = Convert.ToDateTime(row["fectermino"]).ToString("yyyyMMdd");
                                        break;
                                    default:
                                        fectermino = Strings.Space(8);
                                        break;
                                }

                                switch (row["Estado"].ToString())
                                {
                                    case "C":
                                        row["Estado"] = "04";
                                        break;
                                    case "A":
                                        if (Convert.ToDouble(row["numrenov"]) > 0)
                                            row["Estado"] = "03";
                                        else
                                            row["Estado"] = "01";
                                        break;
                                }

                                double plazoCdtVal = Convert.ToDouble(row["plazocdt"]);
                                if (plazoCdtVal > 360)
                                    row["plazocdt"] = "13";
                                else if (plazoCdtVal > 330)
                                    row["plazocdt"] = "12";
                                else if (plazoCdtVal > 300)
                                    row["plazocdt"] = "11";
                                else if (plazoCdtVal > 270)
                                    row["plazocdt"] = "10";
                                else if (plazoCdtVal > 240)
                                    row["plazocdt"] = "09";
                                else if (plazoCdtVal > 210)
                                    row["plazocdt"] = "08";
                                else if (plazoCdtVal > 180)
                                    row["plazocdt"] = "07";
                                else if (plazoCdtVal > 150)
                                    row["plazocdt"] = "06";
                                else if (plazoCdtVal > 120)
                                    row["plazocdt"] = "05";
                                else if (plazoCdtVal > 90)
                                    row["plazocdt"] = "04";
                                else if (plazoCdtVal > 60)
                                    row["plazocdt"] = "03";
                                else if (plazoCdtVal > 30)
                                    row["plazocdt"] = "02";
                                else if (plazoCdtVal > 0)
                                    row["plazocdt"] = "01";
                                break;
                        }

                        switch (row["Excenta"].ToString())
                        {
                            case "Y":
                                row["Excenta"] = "01";
                                break;
                            case "N":
                                row["Excenta"] = "02";
                                break;
                            default:
                                row["Excenta"] = "03";
                                break;
                        }

                        switch (row["Excenta"].ToString())
                        {
                            case "01":
                                if (row["fec_exepcion"] is DBNull)
                                    fecexcencion = Convert.ToDateTime(fechafinal).ToString("yyyyMMdd");
                                else
                                    fecexcencion = Convert.ToDateTime(row["fec_exepcion"]).ToString("yyyyMMdd");
                                break;
                            default:
                                fecexcencion = Strings.Space(8);
                                break;
                        }

                        StFinExcenta = Strings.Space(8);

                        dsreporte.Tables["incluidoscifin"].Rows.Add("000001", row["Tipo_nit"], row["CedulaNit"], row["APELLIDO"] + " " + row["NOMBRE"], obligacion, fecapertura, fectermino, row["tipocuenta"], row["Estado"],
                            fechafinal, row["Excenta"], fecexcencion, StFinExcenta, row["plazocdt"], row["numrenov"], row["DIRECCION"], row["TELEFONO1"], row["NOMBRE_CIUDAD"], row["DPTO"], row["EMPRESA_LABORA"],
                            row["DirEmpresa"], row["TelEmpresa"], row["NOMBRE_CIUDAD"], row["DPTO"]);

                        switch (row["tipocuenta"].ToString())
                        {
                            case "01":
                            case "02":
                                if (row["cc_nit_firmareq1"].ToString().Trim() != "" && row["cc_nit_firmareq1"].ToString().Trim() != "00000000000000")
                                {
                                    row["cc_nit_firmareq1"] = Strings.Right("000000000000000" + row["cc_nit_firmareq1"], 15);
                                    if (row["cc_nit_firmareq1"].ToString() != row["CedulaNit"].ToString())
                                    {
                                        dsreporte.Tables["incluidoscifin"].Rows.Add("000001", "01", row["cc_nit_firmareq1"], row["nom_firmareq1"], obligacion, fecapertura, fectermino, row["tipocuenta"], row["Estado"],
                                                                                    fechafinal, row["Excenta"], fecexcencion, StFinExcenta, row["plazocdt"], row["numrenov"], " ", " ", " ", " ", " ", " ", " ", " ", " ");
                                    }
                                }
                                if (row["cc_nit_firmareq2"].ToString().Trim() != "" && row["cc_nit_firmareq2"].ToString().Trim() != "00000000000000")
                                {
                                    row["cc_nit_firmareq2"] = Strings.Right("000000000000000" + row["cc_nit_firmareq2"], 15);
                                    if (row["cc_nit_firmareq2"].ToString() != row["CedulaNit"].ToString())
                                    {
                                        dsreporte.Tables["incluidoscifin"].Rows.Add("000001", "01", row["cc_nit_firmareq2"], row["nom_firmareq2"], obligacion, fecapertura, fectermino, row["tipocuenta"], row["Estado"],
                                                                                    fechafinal, row["Excenta"], fecexcencion, StFinExcenta, row["plazocdt"], row["numrenov"], " ", " ", " ", " ", " ", " ", " ", " ", " ");
                                    }
                                }
                                if (row["cc_nit_firmareq3"].ToString().Trim() != "" && row["cc_nit_firmareq3"].ToString().Trim() != "00000000000000")
                                {
                                    row["cc_nit_firmareq3"] = Strings.Right("000000000000000" + row["cc_nit_firmareq3"], 15);
                                    if (row["cc_nit_firmareq3"].ToString() != row["CedulaNit"].ToString())
                                    {
                                        dsreporte.Tables["incluidoscifin"].Rows.Add("000001", "01", row["cc_nit_firmareq3"], row["nom_firmareq3"], obligacion, fecapertura, fectermino, row["tipocuenta"], row["Estado"],
                                                                                    fechafinal, row["Excenta"], fecexcencion, StFinExcenta, row["plazocdt"], row["numrenov"], " ", " ", " ", " ", " ", " ", " ", " ", " ");
                                    }
                                }
                                break;
                        }

                        MsgBarra.PerformStep();
                    }
                }
            }

            MsgBarra.Close();
            MsgBarra.Dispose();

            return dsreporte;
        }

        #endregion

        #region creaDatasetIncluidosCifinCdtAhorro (VB line 30565)

        private void creaDatasetIncluidosCifinCdtAhorro(ref DataSet dsdata)
        {
            string ststring = " ";
            int stinteger = 0;
            double stdouble = 0;
            try
            {
                dsdata.Tables.Add("incluidoscifin");
            }
            catch (Exception ex)
            {
            }

            DataColumnCollection cols = dsdata.Tables["incluidoscifin"].Columns;
            cols.Add("sucursal", ststring.GetType());
            cols.Add("tipoid", ststring.GetType());
            cols.Add("numid", ststring.GetType());
            cols.Add("nombretitular", ststring.GetType());
            cols.Add("obligacion", ststring.GetType());
            cols.Add("fechaapertura", ststring.GetType());
            cols.Add("fechatermino", ststring.GetType());
            cols.Add("tipocuenta", ststring.GetType());
            cols.Add("estadocuenta", ststring.GetType());
            cols.Add("fechacorte", ststring.GetType());
            cols.Add("excenta", ststring.GetType());
            cols.Add("fechaexcenta", ststring.GetType());
            cols.Add("fechaprescripcion", ststring.GetType());
            cols.Add("plazocdt", ststring.GetType());
            cols.Add("cantrenovacion", ststring.GetType());
            cols.Add("direccioncasa", ststring.GetType());
            cols.Add("telefonocasa", ststring.GetType());
            cols.Add("ciudadcasa", ststring.GetType());
            cols.Add("deptocasa", ststring.GetType());
            cols.Add("nombreempresa", ststring.GetType());
            cols.Add("direccionempresa", ststring.GetType());
            cols.Add("telefonoempresa", ststring.GetType());
            cols.Add("ciudadempresa", ststring.GetType());
            cols.Add("deptoempresa", ststring.GetType());
        }

        #endregion

        #region GeneraPlanoCifinCdtAhorro (VB line 30601)

        private void GeneraPlanoCifinCdtAhorro(string paquete, string tiporeporte, string tipoentidad, string codigoentidad,
            DateTime fechafinal, string ClaveCifin, StreamWriter StArchivo, DataSet dsdata, System.Windows.Forms.Form myforma, string rutaCSV = "")
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo plano CIFIN", myforma);
            int fila = 0;
            string espacios60 = Strings.Space(60);
            decimal total = 0;

            barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidoscifin"].Rows.Count);
            barraprogress.Show();

            // Imprimimos el registro tipo 1. Control del reporte
            StArchivo.Write("1"); //Tipo Registro
            StArchivo.Write(Strings.Right("00" + paquete, 2)); //Codigo Paquete
            StArchivo.Write(Strings.Right("000" + tipoentidad, 3)); //tipo entidad
            StArchivo.Write(Strings.Right("0000" + codigoentidad, 4)); //tipo entidad
            StArchivo.Write(fechafinal.ToString("yyyyMMdd")); //fecha corte
            StArchivo.Write(Strings.Right(Strings.Space(15) + ClaveCifin, 15)); //clave cifin
            StArchivo.WriteLine(Strings.Space(447)); //relleno

            // Imprimimos el registro tipo 2. Detalles
            for (fila = 0; fila <= dsdata.Tables["incluidoscifin"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Tables["incluidoscifin"].Rows[fila];
                StArchivo.Write("2"); // Tipo Registro
                StArchivo.Write(row["sucursal"]); //sucursal
                StArchivo.Write(row["tipoid"]); //Tipo ID
                StArchivo.Write(Strings.Right("000000000000000" + row["numid"], 15)); //Numero ID
                StArchivo.Write(Strings.Left(row["nombretitular"] + espacios60, 60)); //Nombre Titular
                StArchivo.Write(row["obligacion"]); //Numero Obligacion
                StArchivo.Write(row["fechaapertura"]); //calidad
                StArchivo.Write(row["fechatermino"]); //calificacion
                StArchivo.Write(Strings.Right("00" + row["tipocuenta"], 2)); //estado del titular
                StArchivo.Write(row["estadocuenta"]); //estado de la obligacion
                StArchivo.Write(Convert.ToDateTime(row["fechacorte"]).ToString("yyyyMMdd")); //Edad de mora
                StArchivo.Write(row["excenta"]); //Anios de mora
                StArchivo.Write(row["fechaexcenta"]); //Fecha de corte
                StArchivo.Write(row["fechaprescripcion"]); //Fecha inicial obligacion
                StArchivo.Write(Strings.Right("00" + row["plazocdt"], 2)); //Fecha fin obligacion
                StArchivo.Write(Strings.Right("00" + row["cantrenovacion"], 2)); //Fecha exigibildad obligacion
                StArchivo.Write(Strings.Left(row["direccioncasa"] + espacios60, 60)); //Direccion Casa
                StArchivo.Write(Strings.Left(row["telefonocasa"] + espacios60, 12)); //Telefono Casa
                StArchivo.Write(Strings.Left(row["ciudadcasa"] + espacios60, 20)); //CIudad Casa
                StArchivo.Write(Strings.Left(row["deptocasa"].ToString().ToUpper() + espacios60, 20)); //Departamento Casa
                StArchivo.Write(Strings.Left(row["nombreempresa"] + espacios60, 60)); //Nombre empresa
                StArchivo.Write(Strings.Left(row["direccionempresa"] + espacios60, 60)); //Direccion empresa
                StArchivo.Write(Strings.Left(row["telefonoempresa"] + espacios60, 12)); //Telefono Empresa
                StArchivo.Write(Strings.Left(row["ciudadempresa"] + espacios60, 20)); //CIudad Empresa
                StArchivo.Write(Strings.Left(row["deptoempresa"].ToString().ToUpper() + espacios60, 20)); //Departamento Empresa
                StArchivo.WriteLine(Strings.Space(32)); // Codigo ramo

                barraprogress.PerformStep();
            }

            // Imprimimos el registro tipo 9. Control de fin de archivo
            total = dsdata.Tables["incluidoscifin"].Rows.Count + 2;
            total = Convert.ToDecimal(Strings.FormatNumber(Math.Round(total, 0), 0, TriState.False, TriState.False, TriState.False));
            StArchivo.Write("9"); //Tipo Registro
            StArchivo.Write(Strings.Right("0000000000" + total, 10)); //Numero de registros
            StArchivo.Write(Strings.Space(469)); //Numero de registros tipo 4
            StArchivo.Close();

            // generar archivo CSV (commented out in VB original)

            barraprogress.Close();
            barraprogress.Dispose();
        }

        #endregion

        #region OrganizaDatosDataCredito_Castigado (VB line 31260)

        private DataSet OrganizaDatosDataCredito_Castigado(DateTime fechafinal, string imprimecodeudores, int CodCiudad,
               string Renumera, System.Windows.Forms.Form myforma, OdbcConnection myconnect, DataSet dsreporte)
        {
            StringBuilder stbuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando Datos DataCredito Castigado", myforma);
            int totalregistros = 0;
            int fila = 0;
            int i = 0;
            OdbcCommand mycommand = new OdbcCommand();
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            DataSet dsdatacredito = new DataSet();
            DataSet dscompania = new DataSet();
            DataSet dsciudad = new DataSet();
            string where = "";
            string CuotasPactadas = "";
            string obligacion = "";
            string NombreTitular = "";
            string obligacionAnt = "";
            string MotivoPago = "";
            string reportar = "Y";
            string novedad = "";
            string fechamovto = "";
            DateTime FecLimPago;
            DateTime fechavemto;
            DateTime FechaIni;
            string Calificacion = "  ";

            FechaIni = new DateTime(Convert.ToInt32(fechafinal.ToString("yyyy")), Convert.ToInt32(fechafinal.ToString("MM")), 1);

            switch (varini.pstTipoBD.ToUpper())
            {
                case "SQL":
                case "MYSQL":
                    where = " and (datacred.diasmora = 0  or (datacred.diasmora>0 and datacred.cobrojur<>'N') or " +
                    "(datacred.diasmora>0 and {fn concat(datacred.codigoter,{fn concat(rtrim(datacred.lincred),rtrim(datacred.numero))})} in " +
                    "(select {fn concat(det.codigoter,{fn concat(rtrim(det.lincred),rtrim(det.numero))})} " +
                    "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                    break;
                case "ORACLE":
                    where = " and (datacred.diasmora = 0  or (datacred.diasmora>0 and datacred.cobrojur<>'N') or " +
                    "(datacred.diasmora>0 and concat(datacred.codigoter,concat(rtrim(datacred.lincred),rtrim(datacred.numero))) in " +
                    "(select concat(det.codigoter, concat(rtrim(det.lincred),rtrim(det.numero))) " +
                    "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                    break;
                case "DB2":
                    where = " and (datacred.diasmora = 0  or (datacred.diasmora>0 and datacred.cobrojur<>'N') or " +
                    "(datacred.diasmora>0 and (datacred.codigoter || datacred.lincred || datacred.numero) in " +
                    "(select det.codigoter || det.lincred || rtrim(det.numero) " +
                    "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                    break;
            }

            MsgBarra.Show();

            this.msgcofsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);
            // this.msgcofsys.BuscaCiudades(CodCiudad, myconnect, dsciudad); // ERROR: CS1501

            stbuilder.Append("select PERIODO, TipoNit, codigoter, nit, LINCRED, NUMERO, apellido, nombre, SituacionTitular,");
            stbuilder.Append("fechaapertura, fecvemto, TipoOblig, SubsidioHipo, TerminoContrato, motivopago, periodicidad,");
            stbuilder.Append("EstOrigenCta, FecEstOrigenCta, EstadoCuenta, FechaMovimiento, adjetivo, FecAdjetivo, TipoMoneda,");
            stbuilder.Append("TipoGarantia, DiasMora, valorob, Saldo, ValorDisponible, CUOTA, SaldoMora, cuotasenmora, Canceladas,");
            stbuilder.Append("PLAZO, PERIODD, ciudadresidencia, codciudadresidencia, DeptoResidencia, direccion, telefono1,");
            stbuilder.Append("ciudad_envio, ciudadcorreo, DeptoCorreo, email, movil, TipoNitCod1, Codeudor1, nitcod1, ApeCod1,");
            stbuilder.Append("NomCod1, ciudadresidenciacod1, codciudadresidenciaCod1, DeptoResidenciaCod1, DirCod1, TelCod1,");
            stbuilder.Append("Ciudad_envioCod1, ciudadcorreocod1, DeptoCorreocod1, emailcod1, movilcod1, TipoNitCod2, Codeudor2,");
            stbuilder.Append("nitcod2, ApeCod2, NomCod2, ciudadresidenciacod2, codciudadresidenciaCod2, DeptoResidenciaCod2,");
            stbuilder.Append("DirCod2, TelCod2, Ciudad_envioCod2, ciudadcorreocod2, DeptoCorreocod2, emailcod2, movilcod2,");
            stbuilder.Append("TipoNitCod3, Codeudor3, nitcod3, ApeCod3, NomCod3, ciudadresidenciacod3, codciudadresidenciaCod3,");
            stbuilder.Append("DeptoResidenciaCod3, DirCod3, TelCod3, Ciudad_envioCod3, ciudadcorreocod3, DeptoCorreocod3,");
            stbuilder.Append("emailcod3, movilcod3, TipoNitCod4, Codeudor4, nitcod4, ApeCod4, NomCod4, ciudadresidenciacod4,");
            stbuilder.Append("codciudadresidenciaCod4, DeptoResidenciaCod4, DirCod4, TelCod4, Ciudad_envioCod4, ciudadcorreocod4,");
            stbuilder.Append("DeptoCorreocod4, emailcod4, movilcod4,AutorizacionMC,AutorizacionHV,CICLOD,codigo_empresa,empresa,Calificacion,Castigo,saldo_inicial ");
            stbuilder.Append("from cop_datacredito02_vw datacred ");
            stbuilder.Append("where datacred.periodo = " + fechafinal.ToString("yyyyMM") + where);

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "Organiza Datos DataCredito", dsdatacredito, "TblDatosDataCredito");
            totalregistros = dsdatacredito.Tables["TblDatosDataCredito"].Rows.Count;

            MsgBarra.ValorMinimoMaximo(0, totalregistros);

            for (fila = 0; fila <= totalregistros - 1; fila++)
            {
                DataRow row = dsdatacredito.Tables["TblDatosDataCredito"].Rows[fila];

                switch (row["AutorizacionHV"].ToString())
                {
                    case "Y":
                        reportar = "Y";
                        break;
                    default:
                        switch (row["AutorizacionMC"].ToString())
                        {
                            case "N":
                                reportar = "N";
                                break;
                            case "Y":
                                reportar = "Y";
                                break;
                        }
                        break;
                }

                obligacion = Strings.Right("0000000000" + row["nit"].ToString(), 10) + row["lincred"] + Strings.Right("0000" + row["NUMERO"], 4);

                if (reportar == "Y")
                {
                    if (Renumera == "Y")
                    {
                        if (Convert.ToDateTime(row["fechaapertura"]) < FechaIni)
                        {
                            obligacionAnt = Strings.Right("00" + Strings.Mid(row["empresa"].ToString(), 3), 2) + Strings.Right("00000000" + row["codigo_empresa"].ToString(), 8) + Strings.Mid(row["lincred"].ToString(), 3) + Strings.Right("000000" + row["NUMERO"], 6);
                            dsreporte.Tables["RenumeraDataCredito"].Rows.Add(obligacionAnt, Strings.Right("00000000000" + row["nit"].ToString(), 11), row["TipoNit"].ToString(), obligacion);
                        }
                    }

                    NombreTitular = row["apellido"].ToString().ToUpper().Trim() + " " + row["nombre"].ToString().ToUpper().Trim();
                    NombreTitular = Strings.Replace(NombreTitular, ".", "");

                    if (row["FechaMovimiento"] is DBNull)
                        fechamovto = fechafinal.ToString("yyyyMMdd");
                    else
                        fechamovto = Convert.ToDateTime(row["FechaMovimiento"]).ToString("yyyyMMdd");

                    if (Convert.ToDouble(row["SaldoMora"]) > 0)
                    {
                        MotivoPago = "0";
                        fechamovto = fechafinal.ToString("yyyyMMdd");
                    }
                    else
                    {
                        string motivopagoStr = row["motivopago"].ToString().Trim();
                        if (motivopagoStr == "" || motivopagoStr == "0")
                            MotivoPago = "1";
                        else
                            MotivoPago = motivopagoStr;
                    }

                    // Aqui validamos el comportamiento de la obligacion. Establece la novedad
                    if (row["Castigo"].ToString() == "Y")
                    {
                        novedad = "13";
                        row["Saldo"] = row["saldo_inicial"];
                        row["SaldoMora"] = row["saldo_inicial"];
                    }
                    else
                    {
                        if (Convert.ToDouble(row["Saldo"]) <= 0)
                        {
                            novedad = "05";
                            row["Saldo"] = "0";
                        }
                        else
                        {
                            if (Convert.ToDouble(row["SaldoMora"]) == 0)
                            {
                                novedad = "01";
                            }
                            else
                            {
                                double diasMoraVal = Convert.ToDouble(row["DiasMora"]);
                                if (diasMoraVal < 30)
                                {
                                    novedad = "01";
                                    row["SaldoMora"] = 0;
                                }
                                else if (diasMoraVal < 60)
                                    novedad = "06";
                                else if (diasMoraVal < 90)
                                    novedad = "07";
                                else if (diasMoraVal < 120)
                                    novedad = "08";
                                else
                                    novedad = "09";
                            }
                        }
                    }

                    if (row["CICLOD"].ToString() != "5")
                    {
                        CuotasPactadas = (Convert.ToDouble(row["plazo"]) * Convert.ToDouble(row["Periodicidad"])).ToString();
                    }
                    else
                    {
                        if (row["Periodicidad"].ToString() == "4")
                            CuotasPactadas = Convert.ToInt32((Convert.ToDouble(row["plazo"]) * 52) / 12).ToString();
                        else
                            CuotasPactadas = (Convert.ToDouble(row["plazo"]) * Convert.ToDouble(row["Periodicidad"])).ToString();
                    }

                    row["Canceladas"] = Convert.ToDouble(CuotasPactadas) - Convert.ToDouble(row["Canceladas"]);
                    if (row["Canceladas"].ToString().Contains("-"))
                    {
                        row["Canceladas"] = "0";
                    }

                    if (row["fecvemto"] is DBNull)
                        fechavemto = Convert.ToDateTime(row["fechaapertura"]).AddMonths(Convert.ToInt32(row["plazo"]));
                    else
                        fechavemto = Convert.ToDateTime(row["fecvemto"]);

                    // FecLimPago = this.VerificaMaximaFechaCausacion(row["codigoter"], row["lincred"], row["numero"], row["periodo"], myconnect); // ERROR: CS1503

                    // if (FecLimPago == new DateTime(1950, 1, 1)) // ERROR: CS0165
                    {
                        FecLimPago = fechafinal;
                    }

                    row["direccion"] = row["direccion"].ToString().Replace("#", "No.");

                    switch (row["Calificacion"].ToString())
                    {
                        case "01":
                            Calificacion = "A ";
                            break;
                        case "02":
                            Calificacion = "B ";
                            break;
                        case "03":
                            Calificacion = "C ";
                            break;
                        case "04":
                            Calificacion = "D ";
                            break;
                        case "05":
                            Calificacion = "E ";
                            break;
                        default:
                            Calificacion = "A ";
                            break;
                    }

                    dsreporte.Tables["incluidosDataCredito"].Rows.Add(row["TipoNit"].ToString(), Strings.Right("00000000000" + row["nit"].ToString(), 11), obligacion, NombreTitular, row["SituacionTitular"].ToString(),
                        Convert.ToDateTime(row["fechaapertura"]).ToString("yyyyMMdd"), fechavemto.ToString("yyyyMMdd"), "00", row["TipoOblig"].ToString(), row["SubsidioHipo"].ToString(), "00000000", row["TerminoContrato"].ToString(),
                        MotivoPago, row["periodicidad"].ToString(), novedad, row["EstOrigenCta"].ToString(), Convert.ToDateTime(row["FecEstOrigenCta"]).ToString("yyyyMMdd"), row["EstadoCuenta"].ToString(), fechamovto, "0", "00000000", row["adjetivo"].ToString(), row["FecAdjetivo"].ToString(),
                        "0", "0", " ", row["TipoMoneda"].ToString(), row["TipoGarantia"].ToString(), Calificacion, "0", row["DiasMora"].ToString(), row["valorob"].ToString(), row["Saldo"].ToString(), row["ValorDisponible"].ToString(), row["CUOTA"].ToString(), row["SaldoMora"].ToString(),
                        CuotasPactadas, row["Canceladas"], row["cuotasenmora"].ToString(), "0", "00000000", FecLimPago.ToString("yyyyMMdd"), fechamovto, dscompania.Tables["tblcompania"].Rows[0]["nomres"].ToString(), dsciudad.Tables["tblciudad"].Rows[0]["nombre_ciudad"].ToString().ToUpper(),
                        CodCiudad, row["ciudadresidencia"].ToString(), row["codciudadresidencia"].ToString(), row["DeptoResidencia"].ToString(), row["direccion"].ToString(), row["telefono1"].ToString(), row["ciudadresidencia"].ToString(), row["codciudadresidencia"].ToString(), row["DeptoResidencia"].ToString(),
                        row["direccion"].ToString(), row["telefono1"].ToString(), row["ciudadcorreo"].ToString(), row["ciudad_envio"].ToString(), row["DeptoCorreo"].ToString(), row["direccion"].ToString(), row["email"].ToString(), row["movil"].ToString(), "0");

                    if (imprimecodeudores == "Y")
                    {
                        for (i = 1; i <= 4; i++)
                        {
                            if (!(row["nitcod" + i] is DBNull))
                            {
                                if (row["nitcod" + i].ToString() != "00999999999999")
                                {
                                    NombreTitular = row["ApeCod" + i].ToString().ToUpper().Trim() + " " + row["NomCod" + i].ToString().ToUpper().Trim();
                                    NombreTitular = Strings.Replace(NombreTitular, ".", "");

                                    row["DirCod" + i] = row["DirCod" + i].ToString().Replace("#", "No.");

                                    dsreporte.Tables["incluidosDataCredito"].Rows.Add(row["TipoNitCod" + i].ToString(), Strings.Right("00000000000" + row["nitcod" + i].ToString(), 11), obligacion, NombreTitular, row["SituacionTitular"].ToString(),
                                        Convert.ToDateTime(row["fechaapertura"]).ToString("yyyyMMdd"), fechavemto.ToString("yyyyMMdd"), "01", row["TipoOblig"].ToString(), row["SubsidioHipo"].ToString(), "00000000", row["TerminoContrato"].ToString(),
                                        MotivoPago, row["periodicidad"].ToString(), novedad, row["EstOrigenCta"].ToString(), Convert.ToDateTime(row["FecEstOrigenCta"]).ToString("yyyyMMdd"), row["EstadoCuenta"].ToString(), fechamovto, "0", "00000000", row["adjetivo"].ToString(), row["FecAdjetivo"].ToString(),
                                        "0", "0", " ", row["TipoMoneda"].ToString(), row["TipoGarantia"].ToString(), "  ", "0", row["DiasMora"].ToString(), row["valorob"].ToString(), row["Saldo"].ToString(), row["ValorDisponible"].ToString(), row["CUOTA"].ToString(), row["SaldoMora"].ToString(),
                                        CuotasPactadas, row["Canceladas"], row["cuotasenmora"].ToString(), "0", "00000000", FecLimPago.ToString("yyyyMMdd"), fechamovto, dscompania.Tables["tblcompania"].Rows[0]["nomres"].ToString(), dsciudad.Tables["tblciudad"].Rows[0]["nombre_ciudad"].ToString().ToUpper(),
                                        CodCiudad, row["ciudadresidenciacod" + i].ToString(), row["codciudadresidenciaCod" + i].ToString(), row["DeptoResidenciaCod" + i].ToString(), row["DirCod" + i].ToString(), row["TelCod" + i].ToString(), row["ciudadresidenciacod" + i].ToString(), row["codciudadresidenciaCod" + i].ToString(), row["DeptoResidenciaCod" + i].ToString(),
                                        row["DirCod" + i].ToString(), row["TelCod" + i].ToString(), row["ciudadcorreocod" + i].ToString(), row["Ciudad_envioCod" + i].ToString(), row["DeptoCorreocod" + i].ToString(), row["DirCod" + i].ToString(), row["emailcod" + i].ToString(), row["movilcod" + i].ToString(), "0");
                                }
                            }
                        }
                    }
                }

                MsgBarra.PerformStep();
            }
            MsgBarra.Close();
            MsgBarra.Dispose();
            return dsreporte;
        }

        #endregion

        #region GeneraPlanoDataCredito_csv (VB line 31909)

        private void GeneraPlanoDataCredito_csv(DateTime fechaCorte, string CodSuscriptor, string TipoCta,
                 StreamWriter StArchivo, string Renumera, StreamWriter StArchivoRenum, DataSet dsdata, System.Windows.Forms.Form myforma, string separadorLista)
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo plano DataCredito CSV", myforma);
            int fila = 0;
            string espacios60 = Strings.Space(60);
            decimal total = 0;
            string Valor = "";

            barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidosDataCredito"].Rows.Count);
            barraprogress.Show();

            // armaCabecera_PlanoDataCredito_csv(StArchivo, StArchivoRenum, separadorLista, Renumera); // ERROR: CS0103

            total = dsdata.Tables["incluidosDataCredito"].Rows.Count + 2;
            total = Convert.ToDecimal(Strings.FormatNumber(Math.Round(total, 0), 0, TriState.False, TriState.False, TriState.False));

            // Imprimimos el registro tipo 2. Detalles
            for (fila = 0; fila <= dsdata.Tables["incluidosDataCredito"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Tables["incluidosDataCredito"].Rows[fila];
                //CABECERA
                StArchivo.Write(Strings.Replace(Strings.Space(18), " ", "H") + separadorLista); //Indicador Registro Inicial
                StArchivo.Write(Strings.Right("000000" + CodSuscriptor, 6) + separadorLista); //Codigo Suscriptor
                StArchivo.Write(Strings.Right("00" + TipoCta, 2) + separadorLista); //tipo Cuenta
                StArchivo.Write(fechaCorte.ToString("yyyyMMdd") + separadorLista); //Fecha Corte
                StArchivo.Write("M" + separadorLista); //Ampliacion del milenio
                StArchivo.Write(" " + separadorLista); //Indicador valores en miles
                StArchivo.Write("T" + separadorLista); //tipo entrega
                StArchivo.Write("00000000" + separadorLista); //Fechainicioreporte
                StArchivo.Write("00000000" + separadorLista); //Fechafinalreporte
                StArchivo.Write(" " + separadorLista); //Indicador de partir
                StArchivo.Write("00000000000000000" + separadorLista); //filler
                //******FIN CABECERA**********************
                //*******INICIO CUERPO
                StArchivo.Write(row["tipoid"] + separadorLista); //Tipo ID
                StArchivo.Write(Strings.Right("00000000000" + row["numid"], 11) + separadorLista); //Numero ID
                StArchivo.Write(Strings.Left(row["obligacion"] + "000000000000000000", 18) + separadorLista); //Numero Obligacion
                //FIN PAGINA 3
                StArchivo.Write(Strings.Left(row["nombretitular"] + Strings.Space(45), 45) + separadorLista); //Nombre Titular
                StArchivo.Write(row["situaciontitular"] + separadorLista); //Situacion Titular
                StArchivo.Write(row["fechaapertura"] + separadorLista); //fechaapertura
                StArchivo.Write(row["fechavemto"] + separadorLista); //fecha vencimiento
                StArchivo.Write(Strings.Right("00" + row["calidad"], 2) + separadorLista); //calidad - deudor o codeudor
                StArchivo.Write(row["tipoobligacion"] + separadorLista); //tipo obligacion
                StArchivo.Write(row["subsidio"] + separadorLista); //subsidio
                StArchivo.Write(row["fechasubsidio"] + separadorLista); //fecha subsidio
                StArchivo.Write(row["terminocontrato"] + separadorLista); //terminocontrato
                StArchivo.Write(row["formapago"] + separadorLista); //formapago
                //FIN PAGINA 4
                StArchivo.Write(row["periodicidad"] + separadorLista); //periodicidad
                StArchivo.Write(Strings.Right("00" + row["novedad"], 2) + separadorLista); //novedad
                StArchivo.Write(row["estadoorigencta"] + separadorLista); //Fecha exigibildad obligacion
                StArchivo.Write(row["fechaestadoorigen"] + separadorLista); //Fecha prescripcion obligacion
                StArchivo.Write(Strings.Right("00" + row["estadocuenta"], 2) + separadorLista); //Fecha pago obligacion
                StArchivo.Write(row["fechaestadocuenta"] + separadorLista); //Modo extincion obligacion
                StArchivo.Write(row["estadoplastico"] + separadorLista); //tipo pago obligacion
                StArchivo.Write(row["fechaestadoplastico"] + separadorLista); //periodicidad pago obligacion
                StArchivo.Write(row["adjetivo"] + separadorLista); //probabilidad no pago
                //FIN PAGINA 5
                StArchivo.Write(row["fechaadjetivo"] + separadorLista); //Numero cuotas pagadas
                StArchivo.Write(row["clasetarj"] + separadorLista); //Numero cuotas pactadas
                StArchivo.Write(row["franquicia"] + separadorLista); // Valor Inicial
                StArchivo.Write(Strings.Left(row["nommarca"] + Strings.Space(30), 30) + separadorLista); // Cuotas en mora
                StArchivo.Write(row["tipomoneda"].ToString() + separadorLista); // Valor mora
                StArchivo.Write(row["tipogarantia"].ToString() + separadorLista); // Saldo Obligacion
                StArchivo.Write(Strings.Right("  " + row["calificacion"].ToString(), 2) + separadorLista); // Cuota Obligacion
                StArchivo.Write(Strings.Right("000" + row["probabilidadIncump"], 3) + separadorLista); // Cargo Fijo
                StArchivo.Write(Strings.Right("000" + row["edadmora"], 3) + separadorLista); // Linea de Credito
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorinicial"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Clausula de permanencia
                Valor = Strings.FormatNumber(Convert.ToDouble(row["saldodeuda"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // tipo contrato
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valordisponible"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Estado contrato
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorcuota"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Vigencia Contrato
                //FIN PAGINA 6
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorsaldomora"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Numero Meses Contrato
                StArchivo.Write(Strings.Right("000" + row["totalcuotas"], 3) + separadorLista); // Naturaleza Juridica
                StArchivo.Write(Strings.Right("000" + row["cuotascanceladas"], 3) + separadorLista); // Modalidad Credito
                StArchivo.Write(Strings.Right("000" + row["cuotasenmora"], 3) + separadorLista); // tipo moneda
                StArchivo.Write(Strings.Right("000" + row["clausulapermanencia"], 3) + separadorLista); // tipo garantia
                StArchivo.Write(row["fechaclausula"].ToString() + separadorLista); // valor garantia
                StArchivo.Write(row["fechalimitepago"] + separadorLista); //obligacion reestructurada
                StArchivo.Write(row["fechapago"] + separadorLista); //naturaleza reestructurada
                StArchivo.Write(Strings.Left(row["oficinaradicacion"] + Strings.Space(30), 30) + separadorLista); //numero reestructuraciones
                StArchivo.Write(Strings.Left(row["ciudadradicacion"] + Strings.Space(20), 20) + separadorLista); // Clase Tarjeta
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadradicacion"], 8) + separadorLista); // Cheques devueltos
                //FIN PAGINA 7
                StArchivo.Write(Strings.Left(row["ciudadresidencia"] + Strings.Space(20), 20) + separadorLista); // Categoria servicios
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadresidencia"], 8) + separadorLista); // Plazo
                StArchivo.Write(Strings.Left(row["deptoresidencia"] + Strings.Space(20), 20) + separadorLista); // Dias Cartera
                StArchivo.Write(Strings.Left(row["direccionresidencia"] + Strings.Space(60), 60) + separadorLista); // Tipo cuenta
                StArchivo.Write(Strings.Right("000000000000" + row["telefonoresidencia"], 12) + separadorLista); // Cupo sobregiro
                StArchivo.Write(Strings.Left(row["ciudadlaboral"] + Strings.Space(20), 20) + separadorLista); // Dias Autorizados
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadlaboral"], 8) + separadorLista); //Direccion Casa
                StArchivo.Write(Strings.Left(row["deptolaboral"] + Strings.Space(20), 20) + separadorLista); //Telefono Casa
                StArchivo.Write(Strings.Left(row["direccionlaboral"] + Strings.Space(60), 60) + separadorLista); //Codigo ciudad Casa
                StArchivo.Write(Strings.Right("000000000000" + row["telefonolaboral"], 12) + separadorLista); //CIudad Casa
                StArchivo.Write(Strings.Left(row["ciudadcorrespondencia"] + Strings.Space(20), 20) + separadorLista); //Codigo Departamento Casa
                //FIN PAGINA 8
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadcorrespon"].ToString(), 8) + separadorLista); //Departamento Casa
                StArchivo.Write(Strings.Left(row["deptocorrespondencia"] + Strings.Space(20), 20) + separadorLista); //Nombre empresa
                StArchivo.Write(Strings.Left(row["direccioncorrespondencia"] + Strings.Space(60), 60) + separadorLista); //Direccion empresa
                StArchivo.Write(Strings.Left(row["correo"] + Strings.Space(60), 60) + separadorLista); //Telefono Empresa
                StArchivo.Write(Strings.Right("000000000000" + row["celular"], 12) + separadorLista); //Codigo ciudad Empresa
                StArchivo.Write(Strings.Right("000000" + row["suscriptor"], 6) + separadorLista); //CIudad Empresa
                StArchivo.Write(" " + separadorLista);

                StArchivo.Write(Strings.Replace(Strings.Space(18), " ", "Z") + separadorLista); //Identificador
                StArchivo.Write(DateTime.Now.ToString("yyyyMMdd") + separadorLista);
                StArchivo.Write(Strings.Right("00000000" + total, 8) + separadorLista); //Numero de registros
                StArchivo.Write(Strings.Right("00000000" + (total - 2), 8) + separadorLista);
                StArchivo.Write("   " + separadorLista);
                StArchivo.WriteLine();

                barraprogress.PerformStep();
            }
            // Imprimimos el registro tipo 9. Control de fin de archivo

            StArchivo.Close();

            if (Renumera == "Y")
            {
                barraprogress.Titulo("Generando archivo plano renumeracion DataCredito");
                barraprogress.ValorMinimoMaximo(0, dsdata.Tables["RenumeraDataCredito"].Rows.Count);
                barraprogress.Show();

                for (fila = 0; fila <= dsdata.Tables["RenumeraDataCredito"].Rows.Count - 1; fila++)
                {
                    DataRow rowRenum = dsdata.Tables["RenumeraDataCredito"].Rows[fila];
                    StArchivoRenum.Write(rowRenum["cuentaanterior"] + separadorLista);
                    StArchivoRenum.Write(rowRenum["documento"] + separadorLista);
                    StArchivoRenum.Write(rowRenum["tipodoc"] + separadorLista);
                    StArchivoRenum.WriteLine(rowRenum["cuentanueva"] + separadorLista);

                    barraprogress.PerformStep();
                }
                StArchivoRenum.Close();
            }

            barraprogress.Close();
            barraprogress.Dispose();
        }

        #endregion

        #region GeneraPlanoDataCredito_csvResum (VB line 32085)

        private void GeneraPlanoDataCredito_csvResum(DateTime fechaCorte, string CodSuscriptor, string TipoCta,
                    StreamWriter StArchivo, string Renumera, StreamWriter StArchivoRenum, DataSet dsdata, System.Windows.Forms.Form myforma, string separadorLista)
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo plano DataCredito CSV", myforma);
            int fila = 0;
            string espacios60 = Strings.Space(60);
            decimal total = 0;
            string Valor = "";

            barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidosDataCredito"].Rows.Count);
            barraprogress.Show();

            // armaCabecera_PlanoDataCredito_csvResum(StArchivo, StArchivoRenum, separadorLista, Renumera); // ERROR: CS0103

            total = dsdata.Tables["incluidosDataCredito"].Rows.Count + 2;
            total = Convert.ToDecimal(Strings.FormatNumber(Math.Round(total, 0), 0, TriState.False, TriState.False, TriState.False));

            // Imprimimos el registro tipo 2. Detalles
            for (fila = 0; fila <= dsdata.Tables["incluidosDataCredito"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Tables["incluidosDataCredito"].Rows[fila];
                //*******INICIO CUERPO
                StArchivo.Write(row["tipoid"] + separadorLista); //Tipo ID
                StArchivo.Write(Strings.Right("00000000000" + row["numid"], 11) + separadorLista); //Numero ID
                StArchivo.Write(Strings.Left(row["obligacion"] + "000000000000000000", 18) + separadorLista); //Numero Obligacion
                //FIN PAGINA 3
                StArchivo.Write(Strings.Left(row["nombretitular"] + Strings.Space(45), 45) + separadorLista); //Nombre Titular
                StArchivo.Write(row["situaciontitular"] + separadorLista); //Situacion Titular
                StArchivo.Write(row["fechaapertura"] + separadorLista); //fechaapertura
                StArchivo.Write(row["fechavemto"] + separadorLista); //fecha vencimiento
                StArchivo.Write(Strings.Right("00" + row["calidad"], 2) + separadorLista); //calidad - deudor o codeudor
                StArchivo.Write(row["tipoobligacion"] + separadorLista); //tipo obligacion
                //.Write(.Item("subsidio") & separadorLista) 'subsidio - commented in VB
                StArchivo.Write(row["fechasubsidio"] + separadorLista); //fecha subsidio
                StArchivo.Write(row["terminocontrato"] + separadorLista); //terminocontrato
                StArchivo.Write(row["formapago"] + separadorLista); //formapago
                //FIN PAGINA 4
                StArchivo.Write(row["periodicidad"] + separadorLista); //periodicidad
                StArchivo.Write(Strings.Right("00" + row["novedad"], 2) + separadorLista); //novedad
                StArchivo.Write(row["estadoorigencta"] + separadorLista); //Fecha exigibildad obligacion
                //.Write(.Item("fechaestadoorigen") & separadorLista) - commented in VB
                StArchivo.Write(Strings.Right("00" + row["estadocuenta"], 2) + separadorLista); //Fecha pago obligacion
                //.Write(.Item("fechaestadocuenta") & separadorLista) - commented in VB
                //.Write(.Item("estadoplastico") & separadorLista) - commented in VB
                //.Write(.Item("fechaestadoplastico") & separadorLista) - commented in VB
                StArchivo.Write(row["adjetivo"] + separadorLista); //probabilidad no pago
                //FIN PAGINA 5
                //.Write(.Item("fechaadjetivo") & separadorLista) - commented in VB
                //.Write(.Item("clasetarj") & separadorLista) - commented in VB
                //.Write(.Item("franquicia") & separadorLista) - commented in VB
                //.Write(Left(.Item("nommarca") & Strings.Space(30), 30) & separadorLista) - commented in VB
                StArchivo.Write(row["tipomoneda"].ToString() + separadorLista); // Valor mora
                StArchivo.Write(row["tipogarantia"].ToString() + separadorLista); // Saldo Obligacion
                //.Write(Right("  " & .Item("calificacion").ToString, 2) & separadorLista) - commented in VB
                //.Write(Right("000" & .Item("probabilidadIncump"), 3) & separadorLista) - commented in VB
                StArchivo.Write(Strings.Right("000" + row["edadmora"], 3) + separadorLista); // Linea de Credito
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorinicial"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Clausula de permanencia
                Valor = Strings.FormatNumber(Convert.ToDouble(row["saldodeuda"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // tipo contrato
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valordisponible"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Estado contrato
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorcuota"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Vigencia Contrato
                //FIN PAGINA 6
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorsaldomora"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.ToString().Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11) + separadorLista); // Numero Meses Contrato
                StArchivo.Write(Strings.Right("000" + row["totalcuotas"], 3) + separadorLista); // Naturaleza Juridica
                StArchivo.Write(Strings.Right("000" + row["cuotascanceladas"], 3) + separadorLista); // Modalidad Credito
                StArchivo.Write(Strings.Right("000" + row["cuotasenmora"], 3) + separadorLista); // tipo moneda
                StArchivo.Write(Strings.Right("000" + row["clausulapermanencia"], 3) + separadorLista); // tipo garantia
                //.Write(.Item("fechaclausula").ToString & separadorLista) - commented in VB
                StArchivo.Write(row["fechalimitepago"] + separadorLista); //obligacion reestructurada
                StArchivo.Write(row["fechapago"] + separadorLista); //naturaleza reestructurada
                StArchivo.Write(Strings.Left(row["oficinaradicacion"] + Strings.Space(30), 30) + separadorLista); //numero reestructuraciones
                StArchivo.Write(Strings.Left(row["ciudadradicacion"] + Strings.Space(20), 20) + separadorLista); // Clase Tarjeta
                //.Write(Right("00000000" & .Item("codigociudadradicacion"), 8) & separadorLista) - commented in VB
                //FIN PAGINA 7
                StArchivo.Write(Strings.Left(row["ciudadresidencia"] + Strings.Space(20), 20) + separadorLista); // Categoria servicios
                //.Write(Right("00000000" & .Item("codigociudadresidencia"), 8) & separadorLista) - commented in VB
                //.Write(Left(.Item("deptoresidencia") & Strings.Space(20), 20) & separadorLista) - commented in VB
                //.Write(Left(.Item("direccionresidencia") & Strings.Space(60), 60) & separadorLista) - commented in VB
                StArchivo.Write(Strings.Right("000000000000" + row["telefonoresidencia"], 12) + separadorLista); // Cupo sobregiro
                //.Write(Left(.Item("ciudadlaboral") & Strings.Space(20), 20) & separadorLista) - commented in VB
                //.Write(Right("00000000" & .Item("codigociudadlaboral"), 8) & separadorLista) - commented in VB
                //.Write(Left(.Item("deptolaboral") & Strings.Space(20), 20) & separadorLista) - commented in VB
                //.Write(Left(.Item("direccionlaboral") & Strings.Space(60), 60) & separadorLista) - commented in VB
                //.Write(Right("000000000000" & .Item("telefonolaboral"), 12) & separadorLista) - commented in VB
                //.Write(Left(.Item("ciudadcorrespondencia") & Strings.Space(20), 20) & separadorLista) - commented in VB
                //FIN PAGINA 8
                //.Write(Right("00000000" & .Item("codigociudadcorrespon").ToString, 8) & separadorLista) - commented in VB
                //.Write(Left(.Item("deptocorrespondencia") & Strings.Space(20), 20) & separadorLista) - commented in VB
                StArchivo.Write(Strings.Left(row["direccioncorrespondencia"] + Strings.Space(60), 60) + separadorLista); //Direccion empresa
                StArchivo.Write(Strings.Left(row["correo"] + Strings.Space(60), 60) + separadorLista); //Telefono Empresa
                StArchivo.Write(Strings.Right("000000000000" + row["celular"], 12) + separadorLista); //Codigo ciudad Empresa
                //.Write(Right("000000" & .Item("suscriptor"), 6) & separadorLista) - commented in VB
                //.Write(" " & separadorLista) - commented in VB

                //.Write(Strings.Replace(Strings.Space(18), " ", "Z") & separadorLista) - commented in VB
                //.Write(Format(Now, "yyyyMMdd") & separadorLista) - commented in VB
                //.Write(Right("00000000" & total, 8) & separadorLista) - commented in VB
                //.Write(Right("00000000" & total - 2, 8) & separadorLista) - commented in VB
                //.Write("   " & separadorLista) - commented in VB
                StArchivo.WriteLine();

                barraprogress.PerformStep();
            }
            // Imprimimos el registro tipo 9. Control de fin de archivo

            StArchivo.Close();

            if (Renumera == "Y")
            {
                barraprogress.Titulo("Generando archivo plano renumeracion DataCredito");
                barraprogress.ValorMinimoMaximo(0, dsdata.Tables["RenumeraDataCredito"].Rows.Count);
                barraprogress.Show();

                for (fila = 0; fila <= dsdata.Tables["RenumeraDataCredito"].Rows.Count - 1; fila++)
                {
                    DataRow rowRenum = dsdata.Tables["RenumeraDataCredito"].Rows[fila];
                    StArchivoRenum.Write(rowRenum["cuentaanterior"] + separadorLista);
                    StArchivoRenum.Write(rowRenum["documento"] + separadorLista);
                    StArchivoRenum.Write(rowRenum["tipodoc"] + separadorLista);
                    StArchivoRenum.WriteLine(rowRenum["cuentanueva"] + separadorLista);

                    barraprogress.PerformStep();
                }
                StArchivoRenum.Close();
            }

            barraprogress.Close();
            barraprogress.Dispose();
        }

        #endregion

        #region GrabaPlanoDeudoresPorVentasBienesyServiciosContabilidad (VB line 32840)

        private void GrabaPlanoDeudoresPorVentasBienesyServiciosContabilidad(string NombreArchivo, string Delimitador, string TipoIden, string Cedula, string Cuenta, string CodigoInterno, string PaisDestino,
               string ClaseDocCobro, string NumeroDocumento, DateTime FechaContabilizacion, DateTime FechaCancelacion, string Plazo, double SaldoInicial, string NotasDebito, string NotasCredito,
               string Devoluciones, double Saldo, string TipoMoneda, string TasaCambio, string TasaInteres, string Morosidad, string ClaseGarantia, string SaldoInteres, string OtrosSaldos,
               string ProvisionCapital, string ProvisionIntYOtros, string Contingencia, string EstadoActual, string Grupo, string CodigoOficina)
        {
            using (StreamWriter StrDisp = File.AppendText(NombreArchivo))
            {
                StrDisp.Write(TipoIden + Delimitador);
                StrDisp.Write(Cedula + Delimitador);
                StrDisp.Write(Cuenta + Delimitador);
                StrDisp.Write(CodigoInterno + Delimitador);
                StrDisp.Write(PaisDestino + Delimitador);
                StrDisp.Write(ClaseDocCobro + Delimitador);
                StrDisp.Write(NumeroDocumento + Delimitador);
                StrDisp.Write(FechaContabilizacion.ToString("dd/MM/yyyy") + Delimitador);
                StrDisp.Write(FechaCancelacion.ToString("dd/MM/yyyy") + Delimitador);
                StrDisp.Write(Plazo + Delimitador);
                StrDisp.Write(SaldoInicial + Delimitador);
                StrDisp.Write(NotasDebito + Delimitador);
                StrDisp.Write(NotasCredito + Delimitador);
                StrDisp.Write(Devoluciones + Delimitador);
                StrDisp.Write(Saldo + Delimitador);
                StrDisp.Write(TipoMoneda + Delimitador);
                StrDisp.Write(TasaCambio + Delimitador);
                StrDisp.Write(TasaInteres + Delimitador);
                StrDisp.Write(Morosidad + Delimitador);
                StrDisp.Write(ClaseGarantia + Delimitador);
                StrDisp.Write(SaldoInteres + Delimitador);
                StrDisp.Write(OtrosSaldos + Delimitador);
                StrDisp.Write(ProvisionCapital + Delimitador);
                StrDisp.Write(ProvisionIntYOtros + Delimitador);
                StrDisp.Write(Contingencia + Delimitador);
                StrDisp.Write(EstadoActual + Delimitador);
                StrDisp.Write(Grupo + Delimitador);
                StrDisp.Write(CodigoOficina);
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        #endregion
    }
}
