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

        private void creaDatasetNoIncluidosCifin(ref DataSet dsdata)
        {
            string ststring = " ";
            int stinteger = 0;
            double stdouble = 0;
            try
            {
                dsdata.Tables.Add("NovedadesCIFIN");
            }
            catch (Exception ex)
            {
            }
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("tipo", ststring.GetType());
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("codigoter", ststring.GetType());
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("linea", stinteger.GetType());
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("numero", stdouble.GetType());
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("saldo", stdouble.GetType());
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("saldomora", stdouble.GetType());
            dsdata.Tables["NovedadesCIFIN"].Columns.Add("canceladas", stdouble.GetType());
        }

        private void creaDatasetRenumeracion(ref DataSet dsdata)
        {
            string ststring = " ";
            int stinteger = 0;
            double stdouble = 0;
            try
            {
                dsdata.Tables.Add("Renumeracion");
            }
            catch (Exception ex)
            {
            }
            dsdata.Tables["Renumeracion"].Columns.Add("ObligacionAnterior", ststring.GetType());
            dsdata.Tables["Renumeracion"].Columns.Add("SucursalAnterior", ststring.GetType());
            dsdata.Tables["Renumeracion"].Columns.Add("ObligacionNueva", ststring.GetType());
            dsdata.Tables["Renumeracion"].Columns.Add("SucursalNueva", ststring.GetType());
        }

        private void creaDatasetCambioEstado(ref DataSet dsdata)
        {
            string ststring = " ";
            int stinteger = 0;
            double stdouble = 0;
            try
            {
                dsdata.Tables.Add("CambioEstado");
            }
            catch (Exception ex)
            {
            }
            dsdata.Tables["CambioEstado"].Columns.Add("Obligacion", ststring.GetType());
            dsdata.Tables["CambioEstado"].Columns.Add("Sucursal", ststring.GetType());
            dsdata.Tables["CambioEstado"].Columns.Add("Estado", ststring.GetType());
            dsdata.Tables["CambioEstado"].Columns.Add("Calificacion", ststring.GetType());
            dsdata.Tables["CambioEstado"].Columns.Add("EdadMora", ststring.GetType());
            dsdata.Tables["CambioEstado"].Columns.Add("Saldo", ststring.GetType());
            dsdata.Tables["CambioEstado"].Columns.Add("SaldoMora", ststring.GetType());
        }

        private void creaDatasetIncluidosCifin(ref DataSet dsdata)
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
            cols.Add("tipoid", ststring.GetType());
            cols.Add("numid", ststring.GetType());
            cols.Add("nombretitular", ststring.GetType());
            cols.Add("obligacion", ststring.GetType());
            cols.Add("sucursal", ststring.GetType());
            cols.Add("calidad", ststring.GetType());
            cols.Add("calificacion", ststring.GetType());
            cols.Add("estadotitular", ststring.GetType());
            cols.Add("estado", ststring.GetType());
            cols.Add("edadmora", ststring.GetType());
            cols.Add("aniosmora", ststring.GetType());
            cols.Add("fechacorte", ststring.GetType());
            cols.Add("fechainicial", ststring.GetType());
            cols.Add("fechafin", ststring.GetType());
            cols.Add("fechaexigibildad", ststring.GetType());
            cols.Add("fechaprescripcion", ststring.GetType());
            cols.Add("fechapago", ststring.GetType());
            cols.Add("modoextincion", ststring.GetType());
            cols.Add("tipopago", ststring.GetType());
            cols.Add("periodicidad", ststring.GetType());
            cols.Add("probabilidadnopago", ststring.GetType());
            cols.Add("cuotaspagas", ststring.GetType());
            cols.Add("cuotaspactadas", ststring.GetType());
            cols.Add("cuotasenmora", ststring.GetType());
            cols.Add("valorinicial", ststring.GetType());
            cols.Add("valormora", ststring.GetType());
            cols.Add("saldo", ststring.GetType());
            cols.Add("cuota", ststring.GetType());
            cols.Add("lineacredito", ststring.GetType());
            cols.Add("tipocontrato", ststring.GetType());
            cols.Add("estadocontrato", ststring.GetType());
            cols.Add("naturalezajuridica", ststring.GetType());
            cols.Add("modalidadcredito", ststring.GetType());
            cols.Add("tipomoneda", ststring.GetType());
            cols.Add("tipogarantia", ststring.GetType());
            cols.Add("valorgarantia", ststring.GetType());
            cols.Add("obligacionreestructurada", ststring.GetType());
            cols.Add("naturalezareestructura", ststring.GetType());
            cols.Add("numeroreestructura", ststring.GetType());
            cols.Add("direccioncasa", ststring.GetType());
            cols.Add("telefonocasa", ststring.GetType());
            cols.Add("codigociudadcasa", ststring.GetType());
            cols.Add("ciudadcasa", ststring.GetType());
            cols.Add("codigodptocasa", ststring.GetType());
            cols.Add("deptocasa", ststring.GetType());
            cols.Add("nombreempresa", ststring.GetType());
            cols.Add("direccionempresa", ststring.GetType());
            cols.Add("telefonoempresa", ststring.GetType());
            cols.Add("codigociudadempresa", ststring.GetType());
            cols.Add("ciudadempresa", ststring.GetType());
            cols.Add("codigodptoempresa", ststring.GetType());
            cols.Add("deptoempresa", ststring.GetType());
        }

        private DataSet OrganizaDatosCifin(string empresa, string tiporeporte, DateTime fechafinal,
            string imprimecodeudores, string renumeracion, string MoraNomina, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando Datos CIFIN", myforma);
            string whereEmpresa = "", wheretiporeporte = "";
            int totalregistros = 0, fila = 0, i = 0;
            OdbcCommand mycommand = new OdbcCommand();
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            DataSet dsdatacifin = new DataSet(), dsreporte = new DataSet();
            string reportar = "Y", NombreReportado = "", NumObligacion = "";
            string CuotasPactadas = "", lineadecredito = "", codciudad = "", coddpto = "";
            string where = "";
            double Cuotaspendientes = 0;
            DateTime dtpfecapro, dtpfecvemto;
            string saldo, saldomora;

            if (empresa.Trim() != "Todas")
            {
                whereEmpresa = " and cifin.empresa='" + Strings.Right("0000" + empresa.Trim(), 4) + "' ";
            }

            switch (tiporeporte)
            {
                case "02":
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "DB2":
                            wheretiporeporte = " and maecar.SaldoMoraCifin<=0 and solido3.CalifiEdadMoraCifin(CASE (IFNULL(maecar.saldoMoraCifin, 0)) WHEN 0 THEN 0 ELSE (IFNULL(maecar.DiasMoraCifin, 0)) END)='00' ";
                            break;
                        default:
                            wheretiporeporte = " and cifin.saldomora<=0 and cifin.CalifiEdadMora='00' ";
                            break;
                    }
                    break;
                case "01":
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "SQL":
                        case "MYSQL":
                            where = " and (cifin.diasmora = 0  or (cifin.diasmora>0 and cifin.cobrojur<>'N') or " +
                            "(cifin.diasmora>0 and {fn concat(cifin.codigoter,{fn concat(rtrim(cifin.lincred),rtrim(cifin.numero))})} in " +
                            "(select {fn concat(det.codigoter,{fn concat(rtrim(det.lincred),rtrim(det.numero))})} " +
                            "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                            break;
                        case "ORACLE":
                            where = " and (cifin.diasmora = 0  or (cifin.diasmora>0 and cifin.cobrojur<>'N') or " +
                            "(cifin.diasmora>0 and concat(cifin.codigoter,concat(rtrim(cifin.lincred),rtrim(cifin.numero))) in " +
                            "(select concat(det.codigoter, concat(rtrim(det.lincred),rtrim(det.numero))) " +
                            "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                            break;
                        case "DB2":
                            where = " and maecar.castigo<>'Y'  and (maecar.diasmora = 0  or (maecar.diasmora>0 and maecar.cobrojur<>'N') " +
                            "or (maecar.diasmora>0 and concat(maecar.codigoter,concat(rtrim(maecar.lincred),rtrim(maecar.numero))) " +
                            "in (select concat(det.codigoter,concat(rtrim(det.lincred),rtrim(det.numero))) " +
                            "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                            break;
                    }
                    break;
            }

            MsgBarra.Show();

            this.creaDatasetNoIncluidosCifin(ref dsreporte);
            this.creaDatasetIncluidosCifin(ref dsreporte);
            this.creaDatasetRenumeracion(ref dsreporte);
            this.creaDatasetCambioEstado(ref dsreporte);

            // codigo para cambiar la consulta cuando sea DB2
            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    stbuilder.Append("SELECT maecar.LINCRED, maecar.NUMERO, maecar.CODIGOTER, '000001' AS agencia,'01' as TipoPago, '0' as Probabilidad,");
                    stbuilder.Append("CASE maecar.clasegar WHEN '2' THEN '03' WHEN '3' THEN '04' ELSE '01' END AS clasegar, ");
                    stbuilder.Append("CASE salmae.periodd WHEN '4' THEN '01' WHEN '2' THEN '04' WHEN '1' THEN '07' ELSE '23' END AS periodd, salmae.PERIODO, salmae.CUOTA, ");
                    stbuilder.Append("MAX(IFNULL(maecar.SaldoMoraCifin, 0)) AS saldomora, CASE MAX(IFNULL(maecar.DiasMoraCifin, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(maecar.DiasMoraCifin, 0)) END AS diasmora, MAX(IFNULL(maecar.CuotasMoraCifin, 0)) AS cuotasenmora, ");
                    stbuilder.Append("'001' AS tipocontrato, CASE IFNULL(salmae.saldo, 0) WHEN 0 THEN '002' ELSE '001' END AS EstadoContrato, ");
                    stbuilder.Append("CASE IFNULL( concat( concat(rtrim(year(maecar.fecfact)), RIGHT( concat('00', rtrim(month(maecar.fecfact))) , 2)) , RIGHT( concat('00', ");
                    stbuilder.Append("rtrim(day(maecar.fecfact))) , 2)) , 1) WHEN 1 THEN '        ' ELSE  concat( concat(rtrim(year(maecar.fecfact)), RIGHT( concat('00', ");
                    stbuilder.Append("rtrim(month(maecar.fecfact))) , 2)) , RIGHT( concat('00', rtrim(day(maecar.fecfact))) , 2))  END AS fecaprob, ");
                    stbuilder.Append("CASE IFNULL( concat( concat(rtrim(year(maecar.fecvemto)), RIGHT( concat('00', rtrim(month(maecar.fecvemto))) , 2)) , RIGHT( concat('00', ");
                    stbuilder.Append("rtrim(day(maecar.fecvemto))) , 2)) , 1) WHEN 1 THEN '        ' ELSE  concat( concat(rtrim(year(maecar.fecvemto)), RIGHT( concat('00', ");
                    stbuilder.Append("rtrim(month(maecar.fecvemto))) , 2)) , RIGHT( concat('00', rtrim(day(maecar.fecvemto))) , 2))  END AS fecvemto, ");
                    stbuilder.Append("CASE IFNULL( concat( concat(rtrim(year(MAX(movimto.fecha_movto))), RIGHT( concat('00', rtrim(month(MAX(movimto.fecha_movto)))) , 2)) , ");
                    stbuilder.Append("RIGHT( concat('00', rtrim(day(MAX(movimto.fecha_movto)))) , 2)) , 1) ");
                    stbuilder.Append("WHEN 1 THEN '        ' ELSE  concat( concat(rtrim(year(MAX(movimto.fecha_movto))), RIGHT( concat('00', ");
                    stbuilder.Append("rtrim(month(MAX(movimto.fecha_movto)))) , 2)) , RIGHT( concat('00', rtrim(day(MAX(movimto.fecha_movto)))) , 2))  END AS FechaPago, ");
                    stbuilder.Append("CASE IFNULL(salmae.saldo, 0) WHEN 0 THEN '07' ELSE solido3.EstadoCifin(CASE MAX(IFNULL(maecar.saldoMoraCifin, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(maecar.DiasMoraCifin, 0)) END, CASE car12.FOGACLA WHEN 0 THEN '01' ELSE RIGHT( concat('00', ");
                    stbuilder.Append("car12.FOGACLA) , 2) END) END AS EstadoObli, solido3.CalificaCifin(CASE MAX(IFNULL(maecar.saldoMoraCifin, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(maecar.DiasMoraCifin, 0)) END, CASE car12.FOGACLA WHEN 0 THEN '01' ELSE RIGHT( concat('00', ");
                    stbuilder.Append("car12.FOGACLA) , 2) END) AS Calificacion, solido3.CalifiEdadMoraCifin(CASE MAX(IFNULL(maecar.saldoMoraCifin, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(maecar.DiasMoraCifin, 0)) END) AS CalifiEdadMora, maecar.PLAZO, salmae.PERIODD AS Periodicidad, ");
                    stbuilder.Append("salmae.CICLOD, IFNULL(MIN(salmae.cuopen), 0) AS Canceladas, IFNULL(salmae.SALDO, 0) AS saldo, maecar.VALOROB, ");
                    stbuilder.Append("CASE car12.FOGACLA WHEN 0 THEN '01' ELSE RIGHT( concat('00', car12.FOGACLA) , 2) END AS FOGACLA, MAX(movimto.FECHA_MOVTO) ");
                    stbuilder.Append("AS FechaMovimiento, RIGHT( CONCAT('00000000000000', maecar.CODEUDOR1) , 14) AS codeudor1, RIGHT( CONCAT('00000000000000', ");
                    stbuilder.Append("maecar.CODEUDOR2) , 14) AS codeudor2, RIGHT( CONCAT('00000000000000', maecar.CODEUDOR3) , 14) AS codeudor3, ");
                    stbuilder.Append("RIGHT( CONCAT('00000000000000', maecar.CODEUDOR4) , 14) AS codeudor4, ");
                    stbuilder.Append("CASE maenit.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit, ");
                    stbuilder.Append("RIGHT( CONCAT('00000000000000', maenit.NIT) , 14) AS CedulaNit, maenit.APELLIDO, maenit.NOMBRE,");
                    stbuilder.Append("CASE maenit.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza, ");
                    stbuilder.Append("CASE cod1.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit1, RIGHT( CONCAT('00000000000000', ");
                    stbuilder.Append("cod1.NIT) , 14) AS CedulaNit1, cod1.APELLIDO as ApeCod1, cod1.NOMBRE AS NomCod1, ");
                    stbuilder.Append("CASE cod1.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza1, ");
                    stbuilder.Append("CASE cod2.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit2, RIGHT( CONCAT('00000000000000', ");
                    stbuilder.Append("cod2.NIT) , 14) AS CedulaNit2, cod2.APELLIDO as ApeCod2, cod2.NOMBRE AS NomCod2, ");
                    stbuilder.Append("CASE cod2.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza2, ");
                    stbuilder.Append("CASE cod3.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit3, RIGHT( CONCAT('00000000000000', ");
                    stbuilder.Append("cod3.NIT) , 14) AS CedulaNit3, cod3.APELLIDO as ApeCod3, cod3.NOMBRE AS NomCod3, ");
                    stbuilder.Append("CASE cod3.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza3, ");
                    stbuilder.Append("CASE cod4.tipo_nit WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS Tipo_nit4, RIGHT( CONCAT('00000000000000', ");
                    stbuilder.Append("cod4.NIT) , 14) AS CedulaNit4, cod4.APELLIDO as ApeCod4, cod4.NOMBRE AS NomCod4,");
                    stbuilder.Append("CASE cod4.natjur WHEN '2' THEN '005' ELSE '000' END AS Naturaleza4, MAX(IFNULL(fp.MotivoPago, '0')) AS motivopago, salmae.estadocifin, ");
                    stbuilder.Append("maecar.autoricentralriesgo as AutorizacionMC, maenit.EMPRESA, maecar.COBROJUR, maenit.DIRECCION, maenit.TELEFONO1, maenit.DPTO_CIUDAD, maenit.autoricentralriesgo as AutorizacionHV,");
                    stbuilder.Append("ciudad57.NOMBRE_CIUDAD, ciudad57.DPTO, cod1.DIRECCION AS dir_cod1, cod1.TELEFONO1 AS tel_cod1, cod1.DPTO_CIUDAD AS ciudad_cod1, ");
                    stbuilder.Append("ciucod1.NOMBRE_CIUDAD AS NomCiudadCod1, ciucod1.DPTO AS DptoCod1, cod2.DIRECCION AS dir_cod2, cod2.TELEFONO1 AS tel_cod2, ");
                    stbuilder.Append("cod2.DPTO_CIUDAD AS ciudad_cod2, ciucod2.NOMBRE_CIUDAD AS NomCiudadCod2, ciucod2.DPTO AS DptoCod2, cod3.DIRECCION AS dir_cod3, ");
                    stbuilder.Append("cod3.TELEFONO1 AS tel_cod3, cod3.DPTO_CIUDAD AS ciudad_cod3, ciucod3.NOMBRE_CIUDAD AS NomCiudadCod3, ciucod3.DPTO AS DptoCod3, ");
                    stbuilder.Append("cod4.DIRECCION AS dir_cod4, cod4.TELEFONO1 AS tel_cod4, cod4.DPTO_CIUDAD AS ciudad_cod4, ciucod4.NOMBRE_CIUDAD AS NomCiudadCod4, ");
                    stbuilder.Append("ciucod4.DPTO AS DptoCod4,'        ' as FechaExigibilidad,'        ' as FechaPrescripcion,'06' as SituacionTitular,'00' as aniosmora,");
                    stbuilder.Append("'02' as ObligReestructurada,'  ' as NatReestructura,'   ' as NumReestructura,maenit.EMPRESA_LABORA,cod1.EMPRESA_LABORA as EmpresaCod1,");
                    stbuilder.Append("cod2.EMPRESA_LABORA as EmpresaCod2,cod3.EMPRESA_LABORA as EmpresaCod3,cod4.EMPRESA_LABORA as EmpresaCod4,maenit.DIRECCION_ENVIO as DirEmpresa,");
                    stbuilder.Append("cod1.DIRECCION_ENVIO as DirEmpresaCod1,cod2.DIRECCION_ENVIO as DirEmpresaCod2,cod3.DIRECCION_ENVIO as DirEmpresaCod3,cod4.DIRECCION_ENVIO as DirEmpresaCod4,");
                    stbuilder.Append("maenit.telefono2 as TelEmpresa,cod1.telefono2 as TelEmpresaCod1,cod2.telefono2 as TelEmpresaCod2,cod3.telefono2 as TelEmpresaCod3,cod4.telefono2 as TelEmpresaCod4,maecar.Castigo, IFNULL(salmae.saldo_inicial, 0) AS saldo_inicial,salmae.clades,maenit.calman ");
                    stbuilder.Append("FROM cop_maecar AS maecar LEFT OUTER JOIN ");
                    stbuilder.Append("cop_movimto AS movimto ON maecar.LINCRED = movimto.LINCRED AND maecar.NUMERO = movimto.NUMERO AND ");
                    stbuilder.Append(" maecar.CODIGOTER = movimto.CODIGOTER AND movimto.FECHA_MOVTO BETWEEN maecar.FechaIniCifin AND ");
                    stbuilder.Append("maecar.FechaFinCifin LEFT OUTER JOIN ");
                    stbuilder.Append("cop_docmto AS docmto ON movimto.COMPRONTE = docmto.COMPRONTE AND movimto.NUMERO_DOMTO = docmto.NUMERO_DOMTO AND ");
                    stbuilder.Append("docmto.ANULADO <> 'Y' LEFT OUTER JOIN ");
                    stbuilder.Append("sys_compro02 AS compro02 ON compro02.CODIGO = movimto.COMPRONTE INNER JOIN ");
                    stbuilder.Append("cop_salmaecar AS salmae ON salmae.CODIGOTER = maecar.CODIGOTER AND salmae.LINCRED = maecar.LINCRED AND ");
                    stbuilder.Append("salmae.NUMERO = maecar.NUMERO AND salmae.PERIODO =  CONCAT(RTRIM(YEAR(maecar.FechaIniCifin)), RIGHT( CONCAT('00', ");
                    stbuilder.Append("RTRIM(MONTH(maecar.FechaIniCifin))) , 2))  INNER JOIN ");
                    stbuilder.Append("sys_maenit AS maenit ON maenit.CODIGOTER = maecar.CODIGOTER LEFT OUTER JOIN ");
                    stbuilder.Append("sys_maenit AS cod1 ON cod1.CODIGOTER = maecar.CODEUDOR1 LEFT OUTER JOIN ");
                    stbuilder.Append("sys_maenit AS cod2 ON cod2.CODIGOTER = maecar.CODEUDOR2 LEFT OUTER JOIN ");
                    stbuilder.Append("sys_maenit AS cod3 ON cod3.CODIGOTER = maecar.CODEUDOR3 LEFT OUTER JOIN ");
                    stbuilder.Append("sys_maenit AS cod4 ON cod4.CODIGOTER = maecar.CODEUDOR4 INNER JOIN ");
                    stbuilder.Append("cop_concar12 AS car12 ON car12.LINCRED = maecar.LINCRED LEFT OUTER JOIN ");
                    stbuilder.Append("sys_forpago AS fp ON fp.compronte = movimto.COMPRONTE AND fp.numero_domto = movimto.NUMERO_DOMTO INNER JOIN ");
                    stbuilder.Append("sys_ciudad57 AS ciudad57 ON maenit.DPTO_CIUDAD = ciudad57.CIUDAD LEFT OUTER JOIN ");
                    stbuilder.Append("sys_ciudad57 AS ciucod1 ON cod1.DPTO_CIUDAD = ciucod1.CIUDAD LEFT OUTER JOIN ");
                    stbuilder.Append("sys_ciudad57 AS ciucod2 ON cod2.DPTO_CIUDAD = ciucod2.CIUDAD LEFT OUTER JOIN ");
                    stbuilder.Append("sys_ciudad57 AS ciucod3 ON cod3.DPTO_CIUDAD = ciucod3.CIUDAD LEFT OUTER JOIN ");
                    stbuilder.Append("sys_ciudad57 AS ciucod4 ON cod4.DPTO_CIUDAD = ciucod4.CIUDAD ");
                    stbuilder.Append("WHERE     (maecar.LINCRED >= 1000) AND (maecar.LINCRED <> 9999) AND (salmae.SALDO_INICIAL = 0) AND (salmae.vlr_debito <> 0) AND ");
                    stbuilder.Append("(maecar.CODIGOTER <> '99999999999999') AND (car12.expcifin = 'Y') OR ");
                    stbuilder.Append("(maecar.LINCRED >= 1000) AND (maecar.LINCRED <> 9999) AND (salmae.SALDO_INICIAL = 0) AND (maecar.CODIGOTER <> '99999999999999') AND  ");
                    stbuilder.Append("(car12.expcifin = 'Y') AND (salmae.vlr_credito <> 0) OR ");
                    stbuilder.Append("(maecar.LINCRED >= 1000) AND (maecar.LINCRED <> 9999) AND (salmae.SALDO_INICIAL > 0) AND (maecar.CODIGOTER <> '99999999999999') AND  ");
                    stbuilder.Append("(car12.expcifin = 'Y') ");
                    stbuilder.Append(whereEmpresa + wheretiporeporte + where);
                    stbuilder.Append("GROUP BY maecar.LINCRED, maecar.NUMERO, maecar.CODIGOTER, maecar.CLASEGAR, salmae.PERIODO, salmae.PERIODD, salmae.CUOTA,  ");
                    stbuilder.Append("  maecar.FECFACT, maecar.FECVEMTO, salmae.SALDO, maecar.VALOROB, maecar.CODEUDOR1, maecar.CODEUDOR2, maecar.CODEUDOR3,  ");
                    stbuilder.Append("maecar.CODEUDOR4, car12.FOGACLA, maecar.PLAZO, salmae.cuopen, salmae.CICLOD, maenit.TIPO_NIT, maenit.NIT, maenit.APELLIDO,  ");
                    stbuilder.Append("maenit.NOMBRE, maenit.NATJUR, cod1.TIPO_NIT, cod1.NIT, cod1.APELLIDO, cod1.NOMBRE, cod1.NATJUR, cod2.TIPO_NIT, cod2.NIT, cod2.APELLIDO,  ");
                    stbuilder.Append("cod2.NOMBRE, cod2.NATJUR, cod3.TIPO_NIT, cod3.NIT, cod3.APELLIDO, cod3.NOMBRE, cod3.NATJUR, cod4.TIPO_NIT, cod4.NIT, cod4.APELLIDO,  ");
                    stbuilder.Append("cod4.NOMBRE, cod4.NATJUR, salmae.estadocifin, maecar.autoricentralriesgo, maecar.COBROJUR, maenit.EMPRESA, maenit.DIRECCION,  ");
                    stbuilder.Append("maenit.TELEFONO1, maenit.DPTO_CIUDAD, ciudad57.NOMBRE_CIUDAD, ciudad57.DPTO, cod1.DIRECCION, cod1.TELEFONO1, cod1.DPTO_CIUDAD,  ");
                    stbuilder.Append("ciucod1.NOMBRE_CIUDAD, ciucod1.DPTO, cod2.DIRECCION, cod2.TELEFONO1, cod2.DPTO_CIUDAD, ciucod2.NOMBRE_CIUDAD, ciucod2.DPTO,  ");
                    stbuilder.Append("cod3.DIRECCION, cod3.TELEFONO1, cod3.DPTO_CIUDAD, ciucod3.NOMBRE_CIUDAD, ciucod3.DPTO, cod4.DIRECCION, cod4.TELEFONO1,  ");
                    stbuilder.Append("cod4.DPTO_CIUDAD, ciucod4.NOMBRE_CIUDAD, ciucod4.DPTO,maenit.EMPRESA_LABORA,cod1.EMPRESA_LABORA,cod2.EMPRESA_LABORA, ");
                    stbuilder.Append("cod3.EMPRESA_LABORA,cod4.EMPRESA_LABORA,maenit.DIRECCION_ENVIO,cod1.DIRECCION_ENVIO,cod2.DIRECCION_ENVIO,cod3.DIRECCION_ENVIO, ");
                    stbuilder.Append("cod4.DIRECCION_ENVIO, maenit.telefono2, cod1.telefono2, cod2.telefono2, cod3.telefono2, cod4.telefono2, maenit.autoricentralriesgo, maecar.Castigo, salmae.saldo_inicial, salmae.clades,maenit.calman ");
                    break;

                default:
                    stbuilder.Append("select cifin.Tipo_nit,cifin.CedulaNit,cifin.apellido,cifin.Nombre,cifin.codigoter,");
                    stbuilder.Append("cifin.lincred,cifin.numero,cifin.agencia,cifin.Calificacion,cifin.SituacionTitular,");
                    stbuilder.Append("cifin.EstadoObli,cifin.estadocifin,cifin.CalifiEdadMora,cifin.aniosmora,cifin.fecaprob,");
                    stbuilder.Append("cifin.fecvemto,cifin.fechaexigibilidad,cifin.fechaprescripcion,max(cifin.fecha_movto) as FechaPago,");
                    stbuilder.Append("cifin.motivopago,'01' as TipoPago,cifin.periodd,'0' as Probabilidad,cifin.Canceladas,");
                    stbuilder.Append("cifin.cuotasenmora,cifin.valorob,cifin.saldomora,cifin.saldo,cifin.cuota,cifin.FOGACLA,");
                    stbuilder.Append("cifin.tipocontrato,cifin.estadocontrato,cifin.Naturaleza,cifin.FOGACLA,cifin.clasegar,");
                    stbuilder.Append("cifin.ObligReestructurada,cifin.NatReestructura,cifin.NumReestructura,cifin.direccion,");
                    stbuilder.Append("cifin.telefono1,cifin.dpto_ciudad,cifin.nombre_ciudad,cifin.dpto,cifin.empresa_labora,");
                    stbuilder.Append("cifin.dirempresa,cifin.telempresa,cifin.dpto_ciudad,cifin.nombre_ciudad,cifin.dpto,");
                    stbuilder.Append("cifin.tipo_nit1,cifin.cedulanit1,cifin.apecod1,cifin.nomcod1,cifin.naturaleza1,cifin.dir_cod1,");
                    stbuilder.Append("cifin.tel_cod1,cifin.ciudad_cod1,cifin.NomCiudadCod1,cifin.DptoCod1,cifin.empresacod1,cifin.dirempresacod1,");
                    stbuilder.Append("cifin.telempresacod1,cifin.tipo_nit2,cifin.cedulanit2,cifin.apecod2,cifin.nomcod2,cifin.naturaleza2,cifin.dir_cod2,");
                    stbuilder.Append("cifin.tel_cod2,cifin.ciudad_cod2,cifin.NomCiudadCod2,cifin.DptoCod2,cifin.empresacod2,cifin.dirempresacod2,");
                    stbuilder.Append("cifin.telempresacod2,cifin.tipo_nit3,cifin.cedulanit3,cifin.apecod3,cifin.nomcod3,cifin.naturaleza3,cifin.dir_cod3,");
                    stbuilder.Append("cifin.tel_cod3,cifin.ciudad_cod3,cifin.NomCiudadCod3,cifin.DptoCod3,cifin.empresacod3,cifin.dirempresacod3,");
                    stbuilder.Append("cifin.telempresacod3,cifin.tipo_nit4,cifin.cedulanit4,cifin.apecod4,cifin.nomcod4,cifin.naturaleza4,cifin.dir_cod4,");
                    stbuilder.Append("cifin.tel_cod4,cifin.ciudad_cod4,cifin.NomCiudadCod4,cifin.DptoCod4,cifin.empresacod4,cifin.dirempresacod4,");
                    stbuilder.Append("cifin.telempresacod4,cifin.AutorizacionHV,cifin.AutorizacionMC,cifin.COBROJUR,cifin.plazo,cifin.periodicidad,cifin.ciclod,cifin.castigo,cifin.saldo_inicial,cifin.empresa,cifin.clades,cifin.calman ");
                    stbuilder.Append("from cop_cifin_vw cifin ");
                    stbuilder.Append("where cifin.lincred<>9999 and cifin.castigo<>'Y' " + whereEmpresa + wheretiporeporte + where);
                    stbuilder.Append("group by cifin.Tipo_nit,cifin.CedulaNit,cifin.apellido,cifin.Nombre,cifin.codigoter,");
                    stbuilder.Append("cifin.lincred,cifin.numero,cifin.agencia,cifin.Calificacion,cifin.SituacionTitular,");
                    stbuilder.Append("cifin.EstadoObli,cifin.estadocifin,cifin.CalifiEdadMora,cifin.aniosmora,cifin.fecaprob,");
                    stbuilder.Append("cifin.fecvemto,cifin.fechaexigibilidad,cifin.fechaprescripcion,cifin.motivopago,cifin.periodd,");
                    stbuilder.Append("cifin.Canceladas,cifin.cuotasenmora,cifin.valorob,cifin.saldomora,cifin.saldo,cifin.cuota,");
                    stbuilder.Append("cifin.FOGACLA,cifin.tipocontrato,cifin.estadocontrato,cifin.Naturaleza,cifin.FOGACLA,cifin.clasegar,");
                    stbuilder.Append("cifin.ObligReestructurada,cifin.NatReestructura,cifin.NumReestructura,cifin.direccion,");
                    stbuilder.Append("cifin.telefono1,cifin.dpto_ciudad,cifin.nombre_ciudad,cifin.dpto,cifin.empresa_labora,");
                    stbuilder.Append("cifin.dirempresa,cifin.telempresa,cifin.dpto_ciudad,cifin.nombre_ciudad,cifin.dpto,");
                    stbuilder.Append("cifin.tipo_nit1,cifin.cedulanit1,cifin.apecod1,cifin.nomcod1,cifin.naturaleza1,cifin.dir_cod1,");
                    stbuilder.Append("cifin.tel_cod1,cifin.ciudad_cod1,cifin.NomCiudadCod1,cifin.DptoCod1,cifin.empresacod1,cifin.dirempresacod1,");
                    stbuilder.Append("cifin.telempresacod1,cifin.tipo_nit2,cifin.cedulanit2,cifin.apecod2,cifin.nomcod2,cifin.naturaleza2,cifin.dir_cod2,");
                    stbuilder.Append("cifin.tel_cod2,cifin.ciudad_cod2,cifin.NomCiudadCod2,cifin.DptoCod2,cifin.empresacod2,cifin.dirempresacod2,");
                    stbuilder.Append("cifin.telempresacod2,cifin.tipo_nit3,cifin.cedulanit3,cifin.apecod3,cifin.nomcod3,cifin.naturaleza3,cifin.dir_cod3,");
                    stbuilder.Append("cifin.tel_cod3,cifin.ciudad_cod3,cifin.NomCiudadCod3,cifin.DptoCod3,cifin.empresacod3,cifin.dirempresacod3,");
                    stbuilder.Append("cifin.telempresacod3,cifin.tipo_nit4,cifin.cedulanit4,cifin.apecod4,cifin.nomcod4,cifin.naturaleza4,cifin.dir_cod4,");
                    stbuilder.Append("cifin.tel_cod4,cifin.ciudad_cod4,cifin.NomCiudadCod4,cifin.DptoCod4,cifin.empresacod4,cifin.dirempresacod4,");
                    stbuilder.Append("cifin.telempresacod4,cifin.AutorizacionHV,cifin.AutorizacionMC,cifin.COBROJUR,cifin.plazo,cifin.periodicidad,cifin.ciclod,cifin.castigo,cifin.saldo_inicial,cifin.empresa,cifin.clades,cifin.calman ");
                    stbuilder.Append("order by cifin.codigoter,cifin.lincred,cifin.numero");
                    break;
            }

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "Organiza Datos Cifin", dsdatacifin, "TblDatosCifin");

            totalregistros = dsdatacifin.Tables["TblDatosCifin"].Rows.Count;
            MsgBarra.ValorMinimoMaximo(0, totalregistros);

            int lineareproceso = 0;
            string NumOblig = "";

            for (fila = 0; fila <= totalregistros - 1; fila++)
            {
                DataRow row = dsdatacifin.Tables["TblDatosCifin"].Rows[fila];
                reportar = "Y";

                if (row["AutorizacionHV"].ToString() == "Y")
                {
                    reportar = "Y";
                }
                else
                {
                    switch (row["AutorizacionMC"].ToString())
                    {
                        case "N":
                            reportar = "N";
                            break;
                        case "Y":
                            reportar = "Y";
                            break;
                    }
                }

                if (Convert.ToDouble(row["saldo"]) > 0 && Convert.ToDouble(row["saldo"]) < 1000)
                {
                    dsreporte.Tables["NovedadesCIFIN"].Rows.Add("1", row["codigoter"], row["lincred"], row["numero"], row["saldo"], 0, 0);
                    reportar = "N";
                }

                if (reportar == "Y")
                {
                    if (Convert.ToDouble(row["saldomora"]) != 0 && Convert.ToDouble(row["saldomora"]) < 1000 && Convert.ToDouble(row["saldo"]) != 0)
                    {
                        dsreporte.Tables["NovedadesCIFIN"].Rows.Add("2", row["codigoter"], row["lincred"], row["numero"], row["saldo"], row["saldomora"], 0);
                        row["saldomora"] = 1000;
                    }
                    if (Convert.ToDouble(row["saldo"]) < 0)
                    {
                        dsreporte.Tables["NovedadesCIFIN"].Rows.Add("3", row["codigoter"], row["lincred"], row["numero"], row["saldo"], 0, 0);
                        row["saldo"] = 0;
                        row["EstadoObli"] = "07";
                        row["FechaPago"] = fechafinal.ToString("yyyyMMdd");
                    }

                    if (row["CICLOD"].ToString() != "5")
                    {
                        CuotasPactadas = (Convert.ToDouble(row["plazo"]) * Convert.ToDouble(row["Periodicidad"])).ToString();
                    }
                    else
                    {
                        if (row["Periodicidad"].ToString() == "4")
                        {
                            CuotasPactadas = Convert.ToInt32((Convert.ToDouble(row["plazo"]) * 52) / 12).ToString();
                        }
                        else
                        {
                            CuotasPactadas = (Convert.ToDouble(row["plazo"]) * Convert.ToDouble(row["Periodicidad"])).ToString();
                        }
                    }

                    row["Canceladas"] = Convert.ToDouble(CuotasPactadas) - Convert.ToDouble(row["Canceladas"]);

                    if (row["Canceladas"].ToString().Contains("-"))
                    {
                        dsreporte.Tables["NovedadesCIFIN"].Rows.Add("4", row["codigoter"], row["lincred"], row["numero"], row["saldo"], 0, row["Canceladas"]);
                        row["Canceladas"] = "000";
                    }

                    if (row["castigo"].ToString() == "Y")
                    {
                        row["Calificacion"] = "06";
                        row["EstadoObli"] = "06";
                        row["saldo"] = row["saldo_inicial"];
                        row["saldomora"] = row["saldo_inicial"];
                        if (Convert.ToInt32(row["CalifiEdadMora"]) >= 13)
                        {
                            row["CalifiEdadMora"] = "13";
                        }
                        else
                        {
                            row["CalifiEdadMora"] = "12";
                        }
                    }

                    if (row["EstadoObli"].ToString() == "07")
                    {
                        if (row["estadocifin"].ToString() == "04" || row["estadocifin"].ToString() == "05")
                        {
                            row["EstadoObli"] = "08";
                            row["Calificacion"] = row["estadocifin"];
                        }
                        else
                        {
                            row["Calificacion"] = "01";
                        }
                        row["saldomora"] = 0;
                        row["cuotasenmora"] = "0";
                        row["CalifiEdadMora"] = "00";
                        row["cuota"] = 0;
                    }
                    else
                    {
                        if (row["estadocifin"].ToString() == "05" && Convert.ToInt32(row["estadocifin"]) > Convert.ToInt32(row["EstadoObli"]))
                        {
                            row["EstadoObli"] = "05";
                            row["Calificacion"] = "05";
                        }
                        if (row["estadocifin"].ToString() == "04" && Convert.ToInt32(row["estadocifin"]) > Convert.ToInt32(row["EstadoObli"]))
                        {
                            row["EstadoObli"] = "04";
                            row["Calificacion"] = "04";
                        }
                    }

                    if (MoraNomina == "N")
                    {
                        if (row["clades"].ToString() == "1")
                        {
                            row["saldomora"] = 0;
                            row["cuotasenmora"] = "0";
                            row["CalifiEdadMora"] = "00";
                        }
                    }

                    if (row["motivopago"].ToString().Trim() == "" || row["motivopago"].ToString().Trim() == "0")
                    {
                        row["motivopago"] = "01";
                    }

                    row["apellido"] = Strings.Left(row["apellido"].ToString() + Strings.Space(30), 30);
                    row["Nombre"] = Strings.Left(row["Nombre"].ToString() + Strings.Space(30), 30);

                    switch (row["Tipo_nit"].ToString())
                    {
                        case "01":
                        case "03":
                        case "04":
                        case "05":
                            NombreReportado = Strings.Mid(row["apellido"].ToString(), 1, 15) + Strings.Mid(row["apellido"].ToString(), 16, 15) + Strings.Mid(row["Nombre"].ToString(), 1, 15) + Strings.Mid(row["Nombre"].ToString(), 16, 15);
                            break;
                        default:
                            NombreReportado = row["apellido"].ToString() + row["Nombre"].ToString();
                            break;
                    }

                    NumObligacion = Strings.Right("0000000000" + row["CedulaNit"].ToString(), 10) + Strings.Right("0000" + row["lincred"].ToString(), 4) + Strings.Right("000000" + row["numero"].ToString(), 6);

                    if (Convert.ToDouble(row["saldomora"]) != 0)
                    {
                        row["FechaPago"] = "        ";
                    }

                    if (row["FechaPago"].ToString().Trim() == "")
                    {
                        row["motivopago"] = "  ";
                        row["TipoPago"] = "  ";
                    }

                    if (row["FOGACLA"].ToString() == "03")
                    {
                        lineadecredito = "014";
                    }
                    else
                    {
                        lineadecredito = "065";
                        if (row["FOGACLA"].ToString() == "04")
                        {
                            row["FOGACLA"] = "05";
                        }
                        else if (row["FOGACLA"].ToString() == "05")
                        {
                            row["FOGACLA"] = "04";
                        }
                    }

                    codciudad = Convert.ToInt32(Strings.Right(row["dpto_ciudad"].ToString(), 3)).ToString();
                    coddpto = row["dpto_ciudad"].ToString().Length > 4 ? Strings.Mid(row["dpto_ciudad"].ToString(), 1, 2) : Strings.Mid(row["dpto_ciudad"].ToString(), 1, 1);

                    if (Convert.ToDouble(row["saldo"]) != 0)
                    {
                        if (Convert.ToDouble(row["cuota"]) < 1000)
                        {
                            if (Convert.ToDouble(row["cuota"]) == 0)
                            {
                                double cantextras = 0, SaldoExtra = 0, ExtrasPactadas = 0;

                                SaldoExtra = this.BuscaSaldoExtrasPactadas(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), fechafinal, myconnect, ref cantextras);

                                if (SaldoExtra >= Convert.ToDouble(row["saldo"]))
                                {
                                    stmysql = "select count(num_extra) as campo1 from cop_extras where codigoter = '" + row["codigoter"] + "' and lincred = " + row["lincred"] + " and numero = " + row["numero"] + " and VALOR<>0 ";
                                    // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaExtrasCifin", ref ExtrasPactadas); // ERROR: CS1503
                                    CuotasPactadas = ExtrasPactadas.ToString();
                                    row["Canceladas"] = ExtrasPactadas - cantextras;
                                }
                                else
                                {
                                    CuotasPactadas = "1";
                                    row["Canceladas"] = 0;
                                }

                                row["periodd"] = "23";
                                row["cuota"] = row["saldo"];

                                if (row["calman"].ToString() == "A")
                                {
                                    row["saldomora"] = 0;
                                    row["cuotasenmora"] = "0";
                                    row["CalifiEdadMora"] = "00";
                                    row["Calificacion"] = "01";
                                    row["EstadoObli"] = "01";
                                }
                            }
                            else
                            {
                                Cuotaspendientes = Convert.ToDouble(CuotasPactadas) - Convert.ToDouble(row["Canceladas"]);
                                if (Cuotaspendientes <= 0)
                                {
                                    row["cuota"] = "1000";
                                }
                                else
                                {
                                    row["cuota"] = (Convert.ToDouble(row["saldo"]) / Cuotaspendientes);
                                    if (Convert.ToDouble(row["cuota"]) < 1000)
                                    {
                                        row["cuota"] = "1000";
                                    }
                                }
                            }
                        }
                    }

                    saldo = row["saldo"].ToString();
                    saldomora = row["saldomora"].ToString();

                    row["valorob"] = Strings.Right("000000000000" + Strings.FormatNumber(Math.Round(Convert.ToDouble(row["valorob"]) / 1000, 0), 0, TriState.False, TriState.False, TriState.False), 12);
                    row["saldomora"] = Strings.Right("000000000000" + Strings.FormatNumber(Math.Round(Convert.ToDouble(row["saldomora"]) / 1000, 0), 0, TriState.False, TriState.False, TriState.False), 12);
                    row["saldo"] = Strings.Right("000000000000" + Strings.FormatNumber(Math.Round(Convert.ToDouble(row["saldo"]) / 1000, 0), 0, TriState.False, TriState.False, TriState.False), 12);
                    row["cuota"] = Strings.Right("000000000000" + Strings.FormatNumber(Math.Round(Convert.ToDouble(row["cuota"]) / 1000, 0), 0, TriState.False, TriState.False, TriState.False), 12);

                    if (row["fecvemto"].ToString().Trim() == "")
                    {
                        dtpfecapro = new DateTime(Convert.ToInt32(Strings.Mid(row["fecaprob"].ToString(), 1, 4)), Convert.ToInt32(Strings.Mid(row["fecaprob"].ToString(), 5, 2)), Convert.ToInt32(Strings.Mid(row["fecaprob"].ToString(), 7)));
                        dtpfecvemto = dtpfecapro.AddMonths(Convert.ToInt32(row["plazo"]));
                        row["fecvemto"] = dtpfecvemto.ToString("yyyyMMdd");
                    }
                    else
                    {
                        dtpfecapro = new DateTime(Convert.ToInt32(Strings.Mid(row["fecaprob"].ToString(), 1, 4)), Convert.ToInt32(Strings.Mid(row["fecaprob"].ToString(), 5, 2)), Convert.ToInt32(Strings.Mid(row["fecaprob"].ToString(), 7)));
                        dtpfecvemto = new DateTime(Convert.ToInt32(Strings.Mid(row["fecvemto"].ToString(), 1, 4)), Convert.ToInt32(Strings.Mid(row["fecvemto"].ToString(), 5, 2)), Convert.ToInt32(Strings.Mid(row["fecvemto"].ToString(), 7)));
                        if (dtpfecvemto < dtpfecapro)
                        {
                            dtpfecvemto = dtpfecapro.AddMonths(Convert.ToInt32(row["plazo"]));
                            row["fecvemto"] = dtpfecvemto.ToString("yyyyMMdd");
                        }
                    }

                    if (Convert.ToDouble(row["valorob"]) < Convert.ToDouble(row["saldo"]))
                    {
                        row["valorob"] = row["saldo"];
                    }

                    if (row["EstadoObli"].ToString() == "07" || row["EstadoObli"].ToString() == "08")
                    {
                        row["Canceladas"] = Convert.ToDouble(CuotasPactadas);
                    }

                    if (row["EstadoObli"].ToString() == "04" || row["EstadoObli"].ToString() == "05" || row["EstadoObli"].ToString() == "06")
                    {
                        if (Convert.ToDouble(row["saldo"]) > 0 && (Convert.ToDouble(row["saldomora"]) == 0 || row["CalifiEdadMora"].ToString() == "00"))
                        {
                            dsreporte.Tables["NovedadesCIFIN"].Rows.Add("5", row["codigoter"], row["lincred"], row["numero"], saldo, saldomora, row["EstadoObli"]);
                            row["Calificacion"] = "01";
                            row["EstadoObli"] = "01";
                            row["CalifiEdadMora"] = "00";
                            dsreporte.Tables["CambioEstado"].Rows.Add(NumObligacion, row["agencia"], row["EstadoObli"], row["Calificacion"], row["CalifiEdadMora"], row["saldo"], row["saldomora"]);
                        }
                    }

                    dsreporte.Tables["incluidoscifin"].Rows.Add(row["Tipo_nit"], row["CedulaNit"], NombreReportado, NumObligacion,
                        row["agencia"], "P", row["Calificacion"], row["SituacionTitular"], row["EstadoObli"], row["CalifiEdadMora"],
                        row["aniosmora"], fechafinal.ToString("yyyyMMdd"), row["fecaprob"], row["fecvemto"], row["fechaexigibilidad"], row["fechaprescripcion"],
                        row["FechaPago"], row["motivopago"], row["TipoPago"], row["periodd"], row["Probabilidad"], row["Canceladas"], CuotasPactadas,
                        row["cuotasenmora"], row["valorob"], row["saldomora"], row["saldo"], row["cuota"], lineadecredito, row["tipocontrato"], row["estadocontrato"],
                        row["Naturaleza"], row["FOGACLA"], "01", row["clasegar"], "0", row["ObligReestructurada"], row["NatReestructura"], row["NumReestructura"],
                        row["direccion"], row["telefono1"], codciudad, row["nombre_ciudad"], coddpto, row["dpto"], row["empresa_labora"], row["dirempresa"], row["telempresa"],
                        codciudad, row["nombre_ciudad"], coddpto, row["dpto"]);

                    if (row["EstadoObli"].ToString() == "04" || row["EstadoObli"].ToString() == "05" || row["EstadoObli"].ToString() == "06" || row["EstadoObli"].ToString() == "07" || row["EstadoObli"].ToString() == "08")
                    {
                        this.ActualizaEstadoCifin(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(fechafinal.ToString("yyyyMM")), row["EstadoObli"].ToString(), myconnect);
                    }

                    switch (renumeracion)
                    {
                        case "1":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Right("000000000000" + row["codigoter"].ToString(), 12) + Strings.Right("00" + row["lincred"].ToString(), 2) + Strings.Right("000000" + row["numero"].ToString(), 6), "", NumObligacion, "000001");
                            break;
                        case "2":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Left(Strings.Right("00" + row["lincred"].ToString(), 2) + Strings.Right("0000000000" + row["numero"].ToString(), 10) + Strings.Space(20), 20), "000001", NumObligacion, "000001");
                            break;
                        case "3":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Left(Strings.Right("0000" + row["lincred"].ToString(), 4) + Strings.Right("0000000000" + row["numero"].ToString(), 10) + Strings.Space(20), 20), "000001", NumObligacion, "000001");
                            break;
                        case "4":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Left(Strings.Right("0000000000" + "00000000111CR" + Strings.Right("00" + row["lincred"].ToString(), 2) + row["numero"].ToString(), 20) + Strings.Space(20), 20), "000111", NumObligacion, "000001");
                            break;
                        case "5":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Right("0000" + row["empresa"].ToString(), 4) + Strings.Right("00000000" + row["codigoter"].ToString(), 8) + Strings.Right("00" + row["lincred"].ToString(), 2) + Strings.Right("000000" + row["numero"].ToString(), 6), "000001", NumObligacion, "000001");
                            break;
                        case "6":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Left(Strings.Right("00" + row["lincred"].ToString(), 2) + Strings.Right("00000000000000" + row["numero"].ToString(), 14) + Strings.Space(20), 20), "000001", NumObligacion, "000001");
                            break;
                        case "7":
                            dsreporte.Tables["Renumeracion"].Rows.Add(Strings.Right("00" + row["lincred"].ToString(), 2) + Strings.Right("00000000" + row["numero"].ToString(), 8) + Strings.Right("0000000000" + row["codigoter"].ToString(), 10), "000001", NumObligacion, "000001");
                            break;
                    }

                    if (imprimecodeudores == "Y")
                    {
                        for (i = 1; i <= 4; i++)
                        {
                            if (!(row["cedulanit" + i] is DBNull))
                            {
                                if (row["cedulanit" + i].ToString().Trim() != "")
                                {
                                    row["apecod" + i] = Strings.Left(row["apecod" + i].ToString() + Strings.Space(30), 30);
                                    row["nomcod" + i] = Strings.Left(row["nomcod" + i].ToString() + Strings.Space(30), 30);

                                    switch (row["tipo_nit" + i].ToString())
                                    {
                                        case "01":
                                        case "03":
                                        case "04":
                                        case "05":
                                            NombreReportado = Strings.Mid(row["apecod" + i].ToString(), 1, 15) + Strings.Mid(row["apecod" + i].ToString(), 16, 15) + Strings.Mid(row["nomcod" + i].ToString(), 1, 15) + Strings.Mid(row["nomcod" + i].ToString(), 16, 15);
                                            break;
                                        default:
                                            NombreReportado = row["apecod" + i].ToString() + row["nomcod" + i].ToString();
                                            break;
                                    }

                                    codciudad = Convert.ToInt32(Strings.Right(row["ciudad_cod" + i].ToString(), 3)).ToString();
                                    coddpto = row["ciudad_cod" + i].ToString().Length > 4 ? Strings.Mid(row["ciudad_cod" + i].ToString(), 1, 2) : Strings.Mid(row["ciudad_cod" + i].ToString(), 1, 1);

                                    dsreporte.Tables["incluidoscifin"].Rows.Add(row["tipo_nit" + i], row["cedulanit" + i], NombreReportado, NumObligacion,
                                        row["agencia"], "C", row["Calificacion"], row["SituacionTitular"], row["EstadoObli"], row["CalifiEdadMora"],
                                        row["aniosmora"], fechafinal.ToString("yyyyMMdd"), row["fecaprob"], row["fecvemto"], row["fechaexigibilidad"], row["fechaprescripcion"],
                                        row["FechaPago"], row["motivopago"], row["TipoPago"], row["periodd"], row["Probabilidad"], row["Canceladas"], CuotasPactadas,
                                        row["cuotasenmora"], row["valorob"].ToString(), row["saldomora"].ToString(), row["saldo"].ToString(), row["cuota"].ToString(), lineadecredito, row["tipocontrato"], row["estadocontrato"],
                                        row["naturaleza" + i], row["FOGACLA"], "01", row["clasegar"], "0", row["ObligReestructurada"], row["NatReestructura"], row["NumReestructura"],
                                        row["dir_cod" + i], row["tel_cod" + i], codciudad, row["NomCiudadCod" + i], coddpto, row["DptoCod" + i], row["empresacod" + i], row["dirempresacod" + i], row["telempresacod" + i],
                                        codciudad, row["NomCiudadCod" + i], coddpto, row["DptoCod" + i]);
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

        private void ActualizaEstadoCifin(string codigoter, int lincred, double numero, int periodo, string estado, OdbcConnection myconnect)
        {
            stmysql = "update cop_salmaecar set estadocifin='" + estado + "' where codigoter='" + codigoter + "' " +
                      "and lincred=" + lincred + " and numero=" + numero + " and periodo=" + periodo;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaEstadoCifin");
        }

        public bool ValidaNulos(ref DataTable Tabla)
        {
            int CuentaFilas = 0, CuentaColumnas = 0, IndiceColum = 0, IndiceFila = 0;
            bool EstaNulo = false;
            string TipoDato = "";

            try
            {
                if (Tabla.Rows.Count > 0)
                {
                    CuentaFilas = Tabla.Rows.Count;
                    CuentaColumnas = Tabla.Columns.Count;
                    for (IndiceFila = 0; IndiceFila <= CuentaFilas - 1; IndiceFila++)
                    {
                        for (IndiceColum = 0; IndiceColum <= CuentaColumnas - 1; IndiceColum++)
                        {
                            EstaNulo = Tabla.Rows[IndiceFila].IsNull(IndiceColum);
                            TipoDato = Tabla.Columns[IndiceColum].DataType.ToString();
                            if (EstaNulo == true)
                            {
                                switch (TipoDato)
                                {
                                    case "System.String":
                                        Tabla.Rows[IndiceFila][IndiceColum] = "0";
                                        break;
                                    case "System.Decimal":
                                    case "System.Int32":
                                    case "System.Int16":
                                    case "System.Int64":
                                        Tabla.Rows[IndiceFila][IndiceColum] = "0";
                                        break;
                                    case "System.DateTime":
                                        Tabla.Rows[IndiceFila][IndiceColum] = new DateTime(1950, 1, 1);
                                        break;
                                    default:
                                        Tabla.Rows[IndiceFila][IndiceColum] = "0";
                                        break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            return false;
        }

        public bool ValidarLavadoActivos(string codigoter, string compronte, double numero_domto, string tipomovto,
            double valor, System.Windows.Forms.Form forma, string usuario, string modulo, OdbcConnection myconnect, bool impOrigenFond = true)
        {
            // msgcop.frmlavado frmlavactivo = new msgcop.frmlavado(); // ERROR: CS0234
            string ValidaLavado = "N", tipodoc = "NN";

            if (modulo == "dep")
            {
                if (tipomovto == "1")
                {
                    // ok = this.BuscaComprobante(compronte, 0, false, myconnect, "", ref tipodoc, 0, 0, "", 0, 0, "", "", "", DateTime.MinValue, "", "", "", "", "", ref ValidaLavado); // ERROR: CS7036
                    if (ok == true)
                    {
                        if (ValidaLavado == "N" && tipodoc == "NC")
                        {
                            return false;
                        }
                    }
                }
            }

            // frmlavactivo.TxtIdAsociado.Text = codigoter; // ERROR: CS0103
            // frmlavactivo.myconexion = myconnect; // ERROR: CS0103
            // frmlavactivo.LblValorTransaccion.Text = Strings.FormatNumber(valor, 0); // ERROR: CS0103
            // frmlavactivo.CbxTipoOperacion.SelectedIndex = (tipomovto == "1") ? 1 : 0; // ERROR: CS0103
            // frmlavactivo.Usuario = usuario; // ERROR: CS0103
            // frmlavactivo.compronte = compronte; // ERROR: CS0103
            // frmlavactivo.numero_domto = numero_domto; // ERROR: CS0103
            // frmlavactivo.impOrigenFond = impOrigenFond; // ERROR: CS0103
            switch (modulo)
            {
                case "dep":
                    // frmlavactivo.IdTransaccion = this.BuscaConsecTransaccion(compronte, numero_domto, myconnect); // ERROR: CS0103
                    break;
                case "cop":
                    // frmlavactivo.IdTransaccion = 0; // ERROR: CS0103
                    break;
            }
            // frmlavactivo.ShowDialog(forma); // ERROR: CS0103
            return false;
        }

        public double BuscaConsecTransaccion(string compronte, double numero_domto, OdbcConnection myconnect)
        {
            double StConsec = 0;

            stmysql = "select max(secuencia) as campo1 from cop_movimto where codigoter<>'99999999999999' and compronte='" + compronte + "' and numero_domto=" + numero_domto;
            // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaConsecTransaccion", ref StConsec); // ERROR: CS1503
            return StConsec;
        }

        public bool BuscaDeclaracionLavadoActivos(double idlavado, OdbcConnection myconnect, ref DataSet dsdata)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdatasetLocal = new DataSet();

            StBuilder.Append("select idlavado,fecha,codigoter,ActividadEconomica,compronte,numero_domto,");
            StBuilder.Append("secuencia,tipooperacion,detalleoperacion,productoafecta,Valor,idcliente,tipoidcliente,");
            StBuilder.Append("nombrecliente,apellidocliente,direccion,telefono,observaciones ");
            StBuilder.Append("from cop_declavado ");
            StBuilder.Append("where idlavado = " + idlavado);

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscaDeclaracionLavadoActivos", dsdatasetLocal, "tbldeclaracion");
            if (ok == true)
            {
                if (dsdata != null)
                {
                    dsdata = dsdatasetLocal.Copy();
                }
            }
            return ok;
        }

        public bool BuscaDeclaracionLavadoActivos(double idlavado, OdbcConnection myconnect)
        {
            DataSet dsdata = null;
            return BuscaDeclaracionLavadoActivos(idlavado, myconnect, ref dsdata);
        }

        public bool GrabarDeclaracionLavadoActivos(double IdLavado, DateTime fecha, string codigoter, string actividad,
            string comprobante, double numero, double idtransaccion, string tipooperacion, string detalleoperacion,
            string producto, double valor, string idcliente, string tipoidcliente, string nombrecliente, string apellidocliente,
            string direccion, string telefono, string observaciones, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            ok = this.BuscaDeclaracionLavadoActivos(IdLavado, myconnect);
            if (ok == false)
            {
                StBuilder.Append("insert into cop_declavado (idlavado,fecha,codigoter,ActividadEconomica,");
                StBuilder.Append("compronte,numero_domto,secuencia,tipooperacion,detalleoperacion,");
                StBuilder.Append("productoafecta,Valor,idcliente,tipoidcliente,nombrecliente,apellidocliente,direccion,telefono,observaciones) values(");
                StBuilder.Append(IdLavado + ",'" + Strings.Format(fecha, varini.PstForFec) + "','" + codigoter + "','" + actividad + "','");
                StBuilder.Append(comprobante + "'," + numero + "," + idtransaccion + ",'" + tipooperacion + "','" + detalleoperacion + "','");
                StBuilder.Append(producto + "'," + valor + ",'" + idcliente + "','" + tipoidcliente + "','" + nombrecliente + "','" + apellidocliente + "','");
                StBuilder.Append(direccion + "','" + telefono + "','" + observaciones + "')");
            }
            else
            {
                StBuilder.Append("update cop_declavado set fecha='" + Strings.Format(fecha, varini.PstForFec) + "',");
                StBuilder.Append("codigoter='" + codigoter + "',");
                StBuilder.Append("ActividadEconomica='" + actividad + "',");
                StBuilder.Append("compronte='" + comprobante + "',");
                StBuilder.Append("numero_domto=" + numero + ",");
                StBuilder.Append("secuencia=" + idtransaccion + ",");
                StBuilder.Append("tipooperacion='" + tipooperacion + "',");
                StBuilder.Append("detalleoperacion='" + detalleoperacion + "',");
                StBuilder.Append("productoafecta='" + producto + "',");
                StBuilder.Append("Valor=" + valor + ",");
                StBuilder.Append("idcliente='" + idcliente + "',");
                StBuilder.Append("tipoidcliente='" + tipoidcliente + "',");
                StBuilder.Append("nombrecliente='" + nombrecliente + "',");
                StBuilder.Append("apellidocliente='" + apellidocliente + "',");
                StBuilder.Append("direccion='" + direccion + "',");
                StBuilder.Append("telefono='" + telefono + "',");
                StBuilder.Append("observaciones='" + observaciones + "' ");
                StBuilder.Append("where idlavado=" + IdLavado);
            }
            ok = this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabarDeclaracionLavadoActivos");
            return ok;
        }

        public bool ValidarClienteenListaNegra(string codigoter, bool Restringe, OdbcConnection myconnect)
        {
            string lista = "0", NomLista = " ", fechaVence = "  ";
            string concaFechaVence = "   ";
            ok = this.BuscarAsociadoEnListas(codigoter, myconnect, ref lista, ref fechaVence);
            if (ok == true)
            {
                // this.msgcofsys.BuscarListaNegra(lista, myconnect, ref NomLista); // ERROR: CS1061

                if (fechaVence.Trim() != "")
                {
                    concaFechaVence = "Hasta  " + fechaVence;
                }

                MessageBox.Show("Esta Persona esta en la siguiente lista negra:" + "\r\n" + lista + "-" + NomLista + "\r\n" + " " + concaFechaVence, "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (Restringe)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public void ActualizaNombreTerminalTDLinea(string codigoter, int lincred, double numero, string NomTerminal, OdbcConnection myconnect)
        {
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            stmysql = "update cop_maecar set filler2='" + Strings.Mid(NomTerminal, 1, 20) + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaNombreTerminalTDLinea");
        }

        public bool EliminarDeudaRecogida(double Numsolicitud, string codigoter, int lincred, double numero, OdbcConnection myconnect)
        {
            stmysql = "delete from cop_solrecr where numero = " + Numsolicitud + " and codigoter = '" + codigoter + "' and lincred = " + lincred + " and nume_cred = " + numero;
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarDeudaRecogida");
            return ok;
        }

        private void ReviveSolicitudCredito(double NumSolicitud, string codigoter, int lincred, double numero, OdbcConnection myconnect)
        {
            DataTable dsdata = new DataTable();
            DataSet dscompania = new DataSet();
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcreditos = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();
            string StEstado = "A";

            this.msgcofsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

            // dsdata = clsliqcreditos.BuscaSolicitudesCredito(NumSolicitud, myconnect); // ERROR: CS1503

            if (dsdata.Rows.Count > 0)
            {
                if (dscompania.Tables["tblcompania"].Rows[0]["ManEstudio"].ToString() == "Y")
                {
                    StEstado = "D";
                }
                else
                {
                    StEstado = "A";
                }

                stmysql = "update cop_solcre set estado='" + StEstado + "' where numero=" + NumSolicitud;
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ReviveSolicitudCredito (cop_solcre)");

                stmysql = "update cop_maecar set numero_soli=0 where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ReviveSolicitudCredito (cop_maecar)");
            }
        }

        private void ReviveFecCausacionCdats(string Codigoter, int Lincred, double numcdat, DateTime FecCausacion, OdbcConnection myconnect, string estado = "")
        {
            StringBuilder stbuilder = new StringBuilder();
            string StFecha = "01-01-1900";
            int Sw1 = 0;

            stbuilder.Append("select max(feccausacdtas) as campo1 from cop_movimto ");
            stbuilder.Append(" where codigoter='" + Codigoter + "' and lincred=" + Lincred + " and numero  = " + numcdat);
            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "ReviveFecCausacionCdats", ref StFecha);

            if (Information.IsDate(StFecha))
            {
                if (Convert.ToDateTime(StFecha) != new DateTime(1900, 1, 1) && Convert.ToDateTime(StFecha).ToString("dd-MM-yyyy").CompareTo(FecCausacion.ToString("dd-MM-yyyy")) > 0)
                {
                    Sw1 = 1;
                }
            }

            if (Sw1 == 0)
            {
                stbuilder = new StringBuilder();

                stbuilder.Append("Update cdt_maecdats set fec_causacion = '" + Strings.Format(FecCausacion, varini.PstForFec) + "' ");
                if (estado.Trim() != "")
                {
                    stbuilder.Append(",estado='" + estado + "' ");
                }
                stbuilder.Append(" where codigoter='" + Codigoter + "' and num_cdat  = " + numcdat);
                this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "ReviveFecCausacionCdats");
            }
        }

        public virtual bool BuscaSaldoExtrasPactadas(string codigoter, int lincred, double numero, double num_extra, int periodo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            double salextpactadas = 0;

            stbuilder.Append("select sum(salext.saldo) as campo1 ");
            stbuilder.Append("from cop_extras ext ");
            stbuilder.Append("inner join cop_salextras salext on ext.codigoter=salext.codigoter and ext.lincred=salext.lincred ");
            stbuilder.Append("and ext.numero=salext.numero and ext.num_extra=salext.num_extra ");
            stbuilder.Append("where salext.periodo=" + periodo + " and ext.codigoter='" + codigoter + "' ");
            stbuilder.Append("and ext.lincred=" + lincred + " and ext.numero=" + numero + " and ext.num_extra=" + num_extra + "  group by ext.num_extra");

            // ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscaSaldoExtrasPactadas", ref salextpactadas); // ERROR: CS1503
            return ok;
        }

        public bool BuscarSecuenciaDepositoSipla(OdbcConnection Myconect, string comprobante, string ConseCpte, string codigoter, ref string consecutivo)
        {
            stmysql = "Select a.secuencia  as campo1"
                + ",a.factura,a.cuenta,a.nit,a.cencos from cop_movimto a left join cop_codmov b on a.cod_movto = b.cod_movto left join sys_maenit c on a.codigoter = c.codigoter where compronte='"
                + comprobante + "' and  numero_domto = '" + ConseCpte + "'  and   a.codigoter='" + codigoter + "'"
                + " order by secuencia desc";

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscarSecuenciaDepositoSipla", ref consecutivo);
            return ok;
        }

        public string BuscaUltimaCalificacionMoraLinea(OdbcConnection myconnect, string codigoter, string lincred, int numero, int periodo)
        {
            string calificacion = "   ";
            stmysql = " select codigoter,periodo_contable,catego as campo1 from cop_copclas" +
                      " where codigoter ='" + codigoter + "' and lincred = '" + lincred + "' and numero=" + numero +
                      " and periodo_contable = (select max(periodo_contable) from cop_copclas" +
                      " where codigoter ='" + codigoter + "' and lincred = '" + lincred + "' and numero=" + numero + " and periodo_contable<=" + periodo + ")";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaUltimaCalificacionMoraLinea", ref calificacion);
            return calificacion;
        }

        public DataSet BuscarBienes(string codigoter, TipoBien tipoBien, OdbcConnection myconnect)
        {
            DataSet dataset = new DataSet();
            bool ok;
            switch (tipoBien)
            {
                case Clscartera.TipoBien.Raiz:
                    stmysql = "select CLASE,case CLASE when '0' then 'Casa' when '1' then 'Apartamento' when '2' then 'Finca' when '3' then 'Lote' end as NOMCLASE,DIRECCION,a.CIUDAD,VALOR,b.nombre_ciudad as NOMCIUDAD " +
                              "from cop_maenitbienes a inner join sys_ciudad57 b on a.ciudad=b.ciudad where a.codigoter='" + codigoter + "' and bien='R'";
                    ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Raices)", dataset, "tblBienesRaices");
                    break;

                case Clscartera.TipoBien.Vehiculo:
                    stmysql = "select CLASE,case CLASE when '0' then 'Particular' when '1' then 'Publico' end as NOMCLASE,MARCA,MODELO,VALOR " +
                             "from cop_maenitbienes where codigoter='" + codigoter + "' and bien='V'";
                    ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Vehiculo)", dataset, "tblVehiculo");
                    break;

                case Clscartera.TipoBien.Todos:
                    stmysql = "select CLASE,case CLASE when '0' then 'Casa' when '1' then 'Apartamento' when '2' then 'Finca' when '3' then 'Lote' end as NOMCLASE,DIRECCION,a.CIUDAD,VALOR,b.nombre_ciudad as NOMCIUDAD " +
                               "from cop_maenitbienes a inner join sys_ciudad57 b on a.ciudad=b.ciudad where a.codigoter='" + codigoter + "' and a.bien='R'";
                    ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Raices)", dataset, "tblBienesRaices");

                    stmysql = "select CLASE,case CLASE when '0' then 'Particular' when '1' then 'Publico' end as NOMCLASE,MARCA,MODELO,VALOR " +
                              "from cop_maenitbienes where codigoter='" + codigoter + "' and bien='V'";
                    ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Vehiculo)", dataset, "tblVehiculo");
                    break;
            }
            return dataset;
        }

        public void GrabarBienes(DataSet dsdata, string codigoter, OdbcConnection myconnect, string borrarRegistro = "SI")
        {
            int i = 0;
            string stmysqlLocal = "";
            if (dsdata.Tables.Contains("tblBienesRaices") == true)
            {
                if (borrarRegistro == "SI")
                {
                    stmysqlLocal = "delete from cop_maenitbienes where codigoter='" + codigoter + "' and bien='R'";
                    this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "GrabarBienes(Raices-Borrando)");
                }

                if (dsdata.Tables["tblBienesRaices"].Rows.Count > 0)
                {
                    while (i < dsdata.Tables["tblBienesRaices"].Rows.Count)
                    {
                        DataRow row = dsdata.Tables["tblBienesRaices"].Rows[i];
                        if (borrarRegistro == "SI")
                        {
                            stmysqlLocal = "insert into cop_maenitbienes (codigoter,bien,clase,direccion,ciudad,valor) values " +
                                      "('" + codigoter + "','R','" + row["CLASE"] + "','" + row["DIRECCION"] + "'," + row["CIUDAD"] + "," + row["VALOR"] + ")";
                            this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "GrabarBienes(Raices)");
                        }
                        else
                        {
                            ok = buscarRegistroBienes(myconnect, codigoter, "R", row["CLASE"].ToString(), Convert.ToDouble(row["VALOR"]), "0", "0", row["DIRECCION"].ToString(), Convert.ToInt32(row["CIUDAD"]));
                            if (ok == false)
                            {
                                stmysqlLocal = "insert into cop_maenitbienes (codigoter,bien,clase,direccion,ciudad,valor) values " +
                                                                    "('" + codigoter + "','R','" + row["CLASE"] + "','" + row["DIRECCION"] + "'," + row["CIUDAD"] + "," + row["VALOR"] + ")";
                                this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "GrabarBienes(Raices)");
                            }
                        }
                        i += 1;
                    }
                }
            }
            i = 0;
            if (dsdata.Tables.Contains("tblVehiculo") == true)
            {
                if (borrarRegistro == "SI")
                {
                    stmysqlLocal = "delete from cop_maenitbienes where codigoter='" + codigoter + "' and bien='V'";
                    this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "GrabarBienes(Vehiculo-Borrando)");
                }

                if (dsdata.Tables["tblVehiculo"].Rows.Count > 0)
                {
                    while (i < dsdata.Tables["tblVehiculo"].Rows.Count)
                    {
                        DataRow row = dsdata.Tables["tblVehiculo"].Rows[i];
                        if (borrarRegistro == "SI")
                        {
                            stmysqlLocal = "insert into cop_maenitbienes (codigoter,bien,clase,marca,modelo,valor) values " +
                                      "('" + codigoter + "','V','" + row["CLASE"] + "','" + row["MARCA"] + "','" + row["MODELO"] + "'," + row["VALOR"] + ")";
                            this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "GrabarBienes(Vehiculo)");
                        }
                        else
                        {
                            ok = buscarRegistroBienes(myconnect, codigoter, "V", row["CLASE"].ToString(), Convert.ToDouble(row["VALOR"]), row["MARCA"].ToString(), row["MODELO"].ToString(), "0", 0);
                            if (ok == false)
                            {
                                stmysqlLocal = "insert into cop_maenitbienes (codigoter,bien,clase,marca,modelo,valor) values " +
                                                                    "('" + codigoter + "','V','" + row["CLASE"] + "','" + row["MARCA"] + "','" + row["MODELO"] + "'," + row["VALOR"] + ")";
                                this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "GrabarBienes(Vehiculo)");
                            }
                        }
                        i += 1;
                    }
                }
            }
        }

        private bool buscarRegistroBienes(OdbcConnection myconnect, string codigoter, string bien, string clase, double valor, string marca, string modelo, string direccion, int ciudad)
        {
            bool ok1;
            string stmysqlLocal = "";
            switch (bien)
            {
                case "V":
                    stmysqlLocal = "SELECT codigoter,bien,clase,marca,valor FROM cop_maenitbienes" +
                   " WHERE  codigoter ='" + codigoter + "' AND  bien ='" + bien + "'  AND " +
                   " clase='" + clase + "'  and  marca='" + marca + "' AND modelo='" + modelo + "' and  valor= " + valor;
                    break;
                case "R":
                    stmysqlLocal = "SELECT codigoter,bien,clase,direccion,ciudad,valor FROM cop_maenitbienes" +
                      " WHERE  codigoter = '" + codigoter + "' AND  bien ='" + bien + "'  AND " +
                       " clase='" + clase + "'  and direccion='" + direccion + "' and ciudad=" + ciudad +
                       " and  valor= " + valor;
                    break;
            }

            ok1 = this.OdbcConnect.ExecuteQueryconec(stmysqlLocal, myconnect, "buscarRegistroBienes");
            return ok1;
        }

    } // end partial class Clscartera
} // end namespace msgcop
