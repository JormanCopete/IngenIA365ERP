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
        // =====================================================================
        // Methods from VB lines 23391-26795 that were missing from Part8.cs
        // =====================================================================

        private DataSet OrganizaDatosCifinCastigo(string empresa, string tiporeporte, DateTime fechafinal, DataSet dsreporte,
            string imprimecodeudores, string MoraNomina, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando Datos CIFIN - Castigo", myforma);
            OdbcCommand mycommand = new OdbcCommand();
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            string whereEmpresa = "";
            int totalregistros = 0;
            int fila = 0;
            int i = 0;
            DataSet dsdatacifin = new DataSet();
            string reportar = "Y";
            string NombreReportado = "";
            string NumObligacion = "";
            string wheretiporeporte = "";
            string CuotasPactadas = "";
            string lineadecredito = "";
            string codciudad = "";
            string coddpto = "";
            string where = "";
            double Cuotaspendientes = 0;
            DateTime dtpfecapro;
            DateTime dtpfecvemto;
            string saldo;
            string saldomora;

            MsgBarra.Show();

            switch (empresa.Trim())
            {
                case "Todas":
                    break;
                default:
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "DB2":
                            whereEmpresa = " and maenit.empresa='" + Strings.Right("0000" + empresa.Trim(), 4) + "' ";
                            break;
                        default:
                            whereEmpresa = " and cifin.empresa='" + Strings.Right("0000" + empresa.Trim(), 4) + "' ";
                            break;
                    }
                    break;
            }

            switch (tiporeporte)
            {
                case "02":
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "DB2":
                            wheretiporeporte = " and maecar.saldomora<=0 and maecar.CalifiEdadMora='00' ";
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
                            where = " and (maecar.diasmora = 0  or (maecar.diasmora>0 and maecar.cobrojur<>'N') or " +
                            "(maecar.diasmora>0 and (maecar.codigoter || rtrim(maecar.lincred) || rtrim(maecar.numero)) in " +
                            "(select (det.codigoter || rtrim(det.lincred) || rtrim(det.numero)) " +
                            "from cop_detcircobro det where det.periodo=" + fechafinal.ToString("yyyyMM") + " and det.lincred>=1000 group by det.codigoter,det.lincred,det.numero))) ";
                            break;
                    }
                    break;
            }

            switch (varini.pstTipoBD.ToUpper())
            {
                case "DB2":
                    stbuilder.Append("SELECT MAECAR.LINCRED, MAECAR.NUMERO, MAECAR.CODIGOTER, '000001' AS AGENCIA, ");
                    stbuilder.Append("CASE MAECAR.CLASEGAR WHEN '2' THEN '03' WHEN '3' THEN '04' ELSE '01' END AS CLASEGAR, ");
                    stbuilder.Append("CASE SALMAE.PERIODD WHEN '4' THEN '01' WHEN '2' THEN '04' WHEN '1' THEN '07' ELSE '23' END AS PERIODD, SALMAE.PERIODO, SALMAE.CUOTA, ");
                    stbuilder.Append("MAX(IFNULL(MAECAR.SALDOMORACIFIN, 0)) AS SALDOMORA, CASE MAX(IFNULL(MAECAR.DIASMORACIFIN, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(MAECAR.DIASMORACIFIN, 0)) END AS DIASMORA, MAX(IFNULL(MAECAR.CUOTASMORACIFIN, 0)) AS CUOTASENMORA, ");
                    stbuilder.Append("'001' AS TIPOCONTRATO, CASE IFNULL(SALMAE.SALDO_INICIAL, 0) WHEN 0 THEN '002' ELSE '001' END AS ESTADOCONTRATO, ");
                    stbuilder.Append("CASE IFNULL(CONCAT(CONCAT(RTRIM(YEAR(MAECAR.FECFACT)), RIGHT(CONCAT('00', RTRIM(MONTH(MAECAR.FECFACT))), 2)), RIGHT(CONCAT('00', ");
                    stbuilder.Append("RTRIM(DAY(MAECAR.FECFACT))), 2)), 1) WHEN 1 THEN '        ' ELSE CONCAT(CONCAT(RTRIM(YEAR(MAECAR.FECFACT)), RIGHT(CONCAT('00', ");
                    stbuilder.Append("RTRIM(MONTH(MAECAR.FECFACT))), 2)), RIGHT(CONCAT('00', RTRIM(DAY(MAECAR.FECFACT))), 2)) END AS FECAPROB, ");
                    stbuilder.Append("CASE IFNULL(CONCAT(CONCAT(RTRIM(YEAR(MAECAR.FECVEMTO)), RIGHT(CONCAT('00', RTRIM(MONTH(MAECAR.FECVEMTO))), 2)), RIGHT(CONCAT('00', ");
                    stbuilder.Append("RTRIM(DAY(MAECAR.FECVEMTO))), 2)), 1) WHEN 1 THEN '        ' ELSE CONCAT(CONCAT(RTRIM(YEAR(MAECAR.FECVEMTO)), RIGHT(CONCAT('00', ");
                    stbuilder.Append("RTRIM(MONTH(MAECAR.FECVEMTO))), 2)), RIGHT(CONCAT('00', RTRIM(DAY(MAECAR.FECVEMTO))), 2)) END AS FECVEMTO, ");
                    stbuilder.Append("CASE IFNULL(CONCAT(CONCAT(RTRIM(YEAR(MAX(MOVIMTO.FECHA_MOVTO))), RIGHT(CONCAT('00', RTRIM(MONTH(MAX(MOVIMTO.FECHA_MOVTO)))), 2)), ");
                    stbuilder.Append("RIGHT(CONCAT('00', RTRIM(DAY(MAX(MOVIMTO.FECHA_MOVTO)))), 2)), 1) ");
                    stbuilder.Append("WHEN 1 THEN '        ' ELSE CONCAT(CONCAT(RTRIM(YEAR(MAX(MOVIMTO.FECHA_MOVTO))), RIGHT(CONCAT('00', ");
                    stbuilder.Append("RTRIM(MONTH(MAX(MOVIMTO.FECHA_MOVTO)))), 2)), RIGHT(CONCAT('00', RTRIM(DAY(MAX(MOVIMTO.FECHA_MOVTO)))), 2)) END AS FechaPago, ");
                    stbuilder.Append("CASE IFNULL(SALMAE.SALDO_INICIAL, 0) WHEN 0 THEN '07' ELSE SOLIDO3.ESTADOCIFIN(CASE MAX(IFNULL(MAECAR.SALDOMORACIFIN, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(MAECAR.DIASMORACIFIN, 0)) END, CASE CAR12.FOGACLA WHEN 0 THEN '01' ELSE RIGHT(CONCAT('00', ");
                    stbuilder.Append("CAR12.FOGACLA), 2) END) END AS ESTADOOBLI, SOLIDO3.CALIFICACIFIN(CASE MAX(IFNULL(MAECAR.SALDOMORACIFIN, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(MAECAR.DIASMORACIFIN, 0)) END, CASE CAR12.FOGACLA WHEN 0 THEN '01' ELSE RIGHT(CONCAT('00', ");
                    stbuilder.Append("CAR12.FOGACLA), 2) END) AS CALIFICACION, SOLIDO3.CALIFIEDADMORACIFIN(CASE MAX(IFNULL(MAECAR.SALDOMORACIFIN, 0)) ");
                    stbuilder.Append("WHEN 0 THEN 0 ELSE MAX(IFNULL(MAECAR.DIASMORACIFIN, 0)) END) AS CALIFIEDADMORA, MAECAR.PLAZO, SALMAE.PERIODD AS PERIODICIDAD, ");
                    stbuilder.Append("SALMAE.CICLOD, IFNULL(SALMAE.SALDO_INICIAL, 0) AS SALDO, MAECAR.VALOROB, ");
                    stbuilder.Append("CASE CAR12.FOGACLA WHEN 0 THEN '01' ELSE RIGHT(CONCAT('00', CAR12.FOGACLA), 2) END AS FOGACLA, MAX(MOVIMTO.FECHA_MOVTO) ");
                    stbuilder.Append("AS FECHAMOVIMIENTO, RIGHT(CONCAT('00000000000000', MAECAR.CODEUDOR1), 14) AS CODEUDOR1, RIGHT(CONCAT('00000000000000', ");
                    stbuilder.Append("MAECAR.CODEUDOR2), 14) AS CODEUDOR2, RIGHT(CONCAT('00000000000000', MAECAR.CODEUDOR3), 14) AS CODEUDOR3, ");
                    stbuilder.Append("RIGHT(CONCAT('00000000000000', MAECAR.CODEUDOR4), 14) AS CODEUDOR4, ");
                    stbuilder.Append("CASE MAENIT.TIPO_NIT WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS TIPO_NIT, ");
                    stbuilder.Append("RIGHT(CONCAT('00000000000000', MAENIT.NIT), 14) AS CEDULANIT, MAENIT.APELLIDO, MAENIT.NOMBRE, ");
                    stbuilder.Append("CASE MAENIT.NATJUR WHEN '2' THEN '005' ELSE '000' END AS NATURALEZA, ");
                    stbuilder.Append("CASE COD1.TIPO_NIT WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS TIPO_NIT1, RIGHT(CONCAT('00000000000000', ");
                    stbuilder.Append("COD1.NIT), 14) AS CEDULANIT1, COD1.APELLIDO AS APECOD1, COD1.NOMBRE AS NOMCOD1, ");
                    stbuilder.Append("CASE COD1.NATJUR WHEN '2' THEN '005' ELSE '000' END AS NATURALEZA1, ");
                    stbuilder.Append("CASE COD2.TIPO_NIT WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS TIPO_NIT2, RIGHT(CONCAT('00000000000000', ");
                    stbuilder.Append("COD2.NIT), 14) AS CEDULANIT2, COD2.APELLIDO AS APECOD2, COD2.NOMBRE AS NOMCOD2, ");
                    stbuilder.Append("CASE COD2.NATJUR WHEN '2' THEN '005' ELSE '000' END AS NATURALEZA2, ");
                    stbuilder.Append("CASE COD3.TIPO_NIT WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS TIPO_NIT3, RIGHT(CONCAT('00000000000000', ");
                    stbuilder.Append("COD3.NIT), 14) AS CEDULANIT3, COD3.APELLIDO AS APECOD3, COD3.NOMBRE AS NOMCOD3, ");
                    stbuilder.Append("CASE COD3.NATJUR WHEN '2' THEN '005' ELSE '000' END AS NATURALEZA3, ");
                    stbuilder.Append("CASE COD4.TIPO_NIT WHEN 'C' THEN '01' WHEN 'N' THEN '02' WHEN 'E' THEN '03' ELSE '04' END AS TIPO_NIT4, RIGHT(CONCAT('00000000000000', ");
                    stbuilder.Append("COD4.NIT), 14) AS CEDULANIT4, COD4.APELLIDO AS APECOD4, COD4.NOMBRE AS NOMCOD4, ");
                    stbuilder.Append("CASE COD4.NATJUR WHEN '2' THEN '005' ELSE '000' END AS NATURALEZA4, MAX(IFNULL(FP.MOTIVOPAGO, '0')) AS MOTIVOPAGO, SALMAE.ESTADOCIFIN, ");
                    stbuilder.Append("MAECAR.AUTORICENTRALRIESGO AS AUTORIZACIONMC, MAENIT.EMPRESA, MAECAR.COBROJUR, MAENIT.DIRECCION, MAENIT.TELEFONO1, MAENIT.DPTO_CIUDAD, MAENIT.AUTORICENTRALRIESGO AS AUTORIZACIONHV, ");
                    stbuilder.Append("CIUDAD57.NOMBRE_CIUDAD, CIUDAD57.DPTO, COD1.DIRECCION AS DIR_COD1, COD1.TELEFONO1 AS TEL_COD1, COD1.DPTO_CIUDAD AS CIUDAD_COD1, ");
                    stbuilder.Append("CIUCOD1.NOMBRE_CIUDAD AS NOMCIUDADCOD1, CIUCOD1.DPTO AS DPTOCOD1, COD2.DIRECCION AS DIR_COD2, COD2.TELEFONO1 AS TEL_COD2, ");
                    stbuilder.Append("COD2.DPTO_CIUDAD AS CIUDAD_COD2, CIUCOD2.NOMBRE_CIUDAD AS NOMCIUDADCOD2, CIUCOD2.DPTO AS DPTOCOD2, COD3.DIRECCION AS DIR_COD3, ");
                    stbuilder.Append("COD3.TELEFONO1 AS TEL_COD3, COD3.DPTO_CIUDAD AS CIUDAD_COD3, CIUCOD3.NOMBRE_CIUDAD AS NOMCIUDADCOD3, CIUCOD3.DPTO AS DPTOCOD3, ");
                    stbuilder.Append("COD4.DIRECCION AS DIR_COD4, COD4.TELEFONO1 AS TEL_COD4, COD4.DPTO_CIUDAD AS CIUDAD_COD4, CIUCOD4.NOMBRE_CIUDAD AS NOMCIUDADCOD4, ");
                    stbuilder.Append("CIUCOD4.DPTO AS DPTOCOD4,'        ' AS FECHAEXIGIBILIDAD,'        ' AS FECHAPRESCRIPCION,'06' AS SITUACIONTITULAR,'  ' AS ANIOSMORA, ");
                    stbuilder.Append("'02' AS OBLIGREESTRUCTURADA,'  ' AS NATREESTRUCTURA,'   ' AS NUMREESTRUCTURA,MAENIT.EMPRESA_LABORA,COD1.EMPRESA_LABORA AS EMPRESACOD1, ");
                    stbuilder.Append("COD2.EMPRESA_LABORA AS EMPRESACOD2,COD3.EMPRESA_LABORA AS EMPRESACOD3,COD4.EMPRESA_LABORA AS EMPRESACOD4,MAENIT.DIRECCION_ENVIO AS DIREMPRESA, ");
                    stbuilder.Append("COD1.DIRECCION_ENVIO AS DIREMPRESACOD1,COD2.DIRECCION_ENVIO AS DIREMPRESACOD2,COD3.DIRECCION_ENVIO AS DIREMPRESACOD3,COD4.DIRECCION_ENVIO AS DIREMPRESACOD4, ");
                    stbuilder.Append("MAENIT.TELEFONO2 AS TELEMPRESA,COD1.TELEFONO2 AS TELEMPRESACOD1,COD2.TELEFONO2 AS TELEMPRESACOD2,COD3.TELEFONO2 AS TELEMPRESACOD3,COD4.TELEFONO2 AS TELEMPRESACOD4,MAECAR.CASTIGO, IFNULL(SALMAE.SALDO_INICIAL, 0) AS SALDO_INICIAL, ");
                    stbuilder.Append("(SELECT CUOPEN FROM cop_salmaecar B WHERE MAECAR.CODIGOTER=B.CODIGOTER AND MAECAR.LINCRED=B.LINCRED AND MAECAR.NUMERO=B.NUMERO ");
                    stbuilder.Append("AND B.PERIODO=SALMAE.PERIODO - CASE SUBSTRING(RTRIM(SALMAE.PERIODO),5,2) WHEN '01' THEN 89 ELSE 1 END) AS CANCELADAS,'01' as TipoPago,'0' as Probabilidad ");
                    stbuilder.Append("FROM COP_MAECAR MAECAR ");
                    stbuilder.Append("LEFT OUTER JOIN cop_movimto MOVIMTO ON MAECAR.LINCRED = MOVIMTO.LINCRED AND MAECAR.NUMERO = MOVIMTO.NUMERO AND ");
                    stbuilder.Append("MAECAR.CODIGOTER = MOVIMTO.CODIGOTER ");
                    stbuilder.Append("LEFT OUTER JOIN cop_docmto DOCMTO ON MOVIMTO.COMPRONTE = DOCMTO.COMPRONTE AND MOVIMTO.NUMERO_DOMTO = DOCMTO.NUMERO_DOMTO AND DOCMTO.ANULADO <> 'Y' ");
                    stbuilder.Append("LEFT OUTER JOIN sys_compro02 COMPRO02 ON COMPRO02.CODIGO = MOVIMTO.COMPRONTE ");
                    stbuilder.Append("INNER JOIN cop_salmaecar SALMAE ON SALMAE.CODIGOTER = MAECAR.CODIGOTER AND SALMAE.LINCRED = MAECAR.LINCRED AND SALMAE.NUMERO = MAECAR.NUMERO ");
                    stbuilder.Append("INNER JOIN SYS_MAENIT MAENIT ON MAENIT.CODIGOTER = MAECAR.CODIGOTER ");
                    stbuilder.Append("LEFT OUTER JOIN SYS_MAENIT COD1 ON COD1.CODIGOTER = MAECAR.CODEUDOR1 ");
                    stbuilder.Append("LEFT OUTER JOIN SYS_MAENIT COD2 ON COD2.CODIGOTER = MAECAR.CODEUDOR2 ");
                    stbuilder.Append("LEFT OUTER JOIN SYS_MAENIT COD3 ON COD3.CODIGOTER = MAECAR.CODEUDOR3 ");
                    stbuilder.Append("LEFT OUTER JOIN SYS_MAENIT COD4 ON COD4.CODIGOTER = MAECAR.CODEUDOR4 ");
                    stbuilder.Append("INNER JOIN cop_concar12 CAR12 ON CAR12.LINCRED = MAECAR.LINCRED ");
                    stbuilder.Append("LEFT OUTER JOIN sys_forpago FP ON FP.COMPRONTE = MOVIMTO.COMPRONTE AND FP.NUMERO_DOMTO = MOVIMTO.NUMERO_DOMTO ");
                    stbuilder.Append("INNER JOIN sys_ciudad57 CIUDAD57 ON MAENIT.DPTO_CIUDAD = CIUDAD57.CIUDAD ");
                    stbuilder.Append("LEFT OUTER JOIN sys_ciudad57 CIUCOD1 ON COD1.DPTO_CIUDAD = CIUCOD1.CIUDAD ");
                    stbuilder.Append("LEFT OUTER JOIN sys_ciudad57 CIUCOD2 ON COD2.DPTO_CIUDAD = CIUCOD2.CIUDAD ");
                    stbuilder.Append("LEFT OUTER JOIN sys_ciudad57 CIUCOD3 ON COD3.DPTO_CIUDAD = CIUCOD3.CIUDAD ");
                    stbuilder.Append("LEFT OUTER JOIN sys_ciudad57 CIUCOD4 ON COD4.DPTO_CIUDAD = CIUCOD4.CIUDAD ");
                    stbuilder.Append("WHERE MAECAR.LINCRED >= 1000 AND MAECAR.LINCRED<>9999 AND ");
                    stbuilder.Append("MAECAR.CODIGOTER <> '99999999999999' AND CAR12.EXPCIFIN='Y' AND ");
                    stbuilder.Append("SALMAE.SALDO=0 AND SALMAE.SALDO_INICIAL>0 AND (MAECAR.CASTIGO='Y' OR ESTADOCIFIN='06') " + whereEmpresa + wheretiporeporte + where);
                    stbuilder.Append("GROUP BY MAECAR.LINCRED, MAECAR.NUMERO, MAECAR.CODIGOTER, MAECAR.CLASEGAR, SALMAE.PERIODO, SALMAE.PERIODD, SALMAE.CUOTA, ");
                    stbuilder.Append("MAECAR.FECFACT, MAECAR.FECVEMTO, SALMAE.SALDO, MAECAR.VALOROB, MAECAR.CODEUDOR1, MAECAR.CODEUDOR2, MAECAR.CODEUDOR3, ");
                    stbuilder.Append("MAECAR.CODEUDOR4, CAR12.FOGACLA, MAECAR.PLAZO, SALMAE.CUOPEN, SALMAE.CICLOD, MAENIT.TIPO_NIT, MAENIT.NIT, MAENIT.APELLIDO, ");
                    stbuilder.Append("COD2.NOMBRE, COD2.NATJUR, COD3.TIPO_NIT, COD3.NIT, COD3.APELLIDO, COD3.NOMBRE, COD3.NATJUR, COD4.TIPO_NIT, COD4.NIT, COD4.APELLIDO, ");
                    stbuilder.Append("MAENIT.NOMBRE, MAENIT.NATJUR, COD1.TIPO_NIT, COD1.NIT, COD1.APELLIDO, COD1.NOMBRE, COD1.NATJUR, COD2.TIPO_NIT, COD2.NIT, COD2.APELLIDO, ");
                    stbuilder.Append("COD4.NOMBRE, COD4.NATJUR, SALMAE.ESTADOCIFIN, MAECAR.AUTORICENTRALRIESGO, MAECAR.COBROJUR, MAENIT.EMPRESA, MAENIT.DIRECCION, ");
                    stbuilder.Append("MAENIT.TELEFONO1, MAENIT.DPTO_CIUDAD, CIUDAD57.NOMBRE_CIUDAD, CIUDAD57.DPTO, COD1.DIRECCION, COD1.TELEFONO1, COD1.DPTO_CIUDAD, ");
                    stbuilder.Append("CIUCOD1.NOMBRE_CIUDAD, CIUCOD1.DPTO, COD2.DIRECCION, COD2.TELEFONO1, COD2.DPTO_CIUDAD, CIUCOD2.NOMBRE_CIUDAD, CIUCOD2.DPTO, ");
                    stbuilder.Append("COD3.DIRECCION, COD3.TELEFONO1, COD3.DPTO_CIUDAD, CIUCOD3.NOMBRE_CIUDAD, CIUCOD3.DPTO, COD4.DIRECCION, COD4.TELEFONO1, ");
                    stbuilder.Append("COD4.DPTO_CIUDAD, CIUCOD4.NOMBRE_CIUDAD, CIUCOD4.DPTO,MAENIT.EMPRESA_LABORA,COD1.EMPRESA_LABORA,COD2.EMPRESA_LABORA, ");
                    stbuilder.Append("COD3.EMPRESA_LABORA,COD4.EMPRESA_LABORA,MAENIT.DIRECCION_ENVIO,COD1.DIRECCION_ENVIO,COD2.DIRECCION_ENVIO,COD3.DIRECCION_ENVIO, ");
                    stbuilder.Append("COD4.DIRECCION_ENVIO, MAENIT.TELEFONO2, COD1.TELEFONO2, COD2.TELEFONO2, COD3.TELEFONO2, COD4.TELEFONO2, MAENIT.AUTORICENTRALRIESGO, MAECAR.CASTIGO, SALMAE.SALDO_INICIAL ");
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
                    stbuilder.Append("cifin.telempresacod4,cifin.AutorizacionHV,cifin.AutorizacionMC,cifin.COBROJUR,cifin.plazo,cifin.periodicidad,cifin.ciclod,cifin.castigo,cifin.saldo_inicial ");
                    stbuilder.Append("from cop_cifin02_vw cifin ");
                    stbuilder.Append("where cifin.lincred<>9999 and (cifin.castigo='Y' or cifin.estadocifin='06') " + whereEmpresa + wheretiporeporte + where);
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
                    stbuilder.Append("cifin.telempresacod4,cifin.AutorizacionHV,cifin.AutorizacionMC,cifin.COBROJUR,cifin.plazo,cifin.periodicidad,cifin.ciclod,cifin.castigo,cifin.saldo_inicial ");
                    stbuilder.Append("order by cifin.codigoter,cifin.lincred,cifin.numero");
                    break;
            }

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "Organiza Datos Cifin Castigo", dsdatacifin, "TblDatosCifin");
            totalregistros = dsdatacifin.Tables["TblDatosCifin"].Rows.Count;
            MsgBarra.ValorMinimoMaximo(0, totalregistros);

            try
            {
                for (fila = 0; fila <= totalregistros - 1; fila++)
                {
                    DataRow row = dsdatacifin.Tables["TblDatosCifin"].Rows[fila];
                    reportar = "Y";

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

                    if ((Convert.ToDouble(row["saldo"]) > 0 && Convert.ToDouble(row["saldo"]) < 1000))
                    {
                        dsreporte.Tables["NovedadesCIFIN"].Rows.Add("1", row["codigoter"], row["lincred"], row["numero"], row["saldo"], 0, 0);
                        reportar = "N";
                    }

                    switch (reportar)
                    {
                        case "Y":
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
                                switch (row["Periodicidad"].ToString())
                                {
                                    case "4":
                                        CuotasPactadas = Convert.ToInt32((Convert.ToDouble(row["plazo"]) * 52) / 12).ToString();
                                        break;
                                    default:
                                        CuotasPactadas = (Convert.ToDouble(row["plazo"]) * Convert.ToDouble(row["Periodicidad"])).ToString();
                                        break;
                                }
                            }

                            row["Canceladas"] = Convert.ToDouble(CuotasPactadas) - Convert.ToDouble(row["Canceladas"]);

                            if (row["Canceladas"].ToString().Contains("-"))
                            {
                                dsreporte.Tables["NovedadesCIFIN"].Rows.Add("4", row["codigoter"], row["lincred"], row["numero"], row["saldo"], 0, row["Canceladas"]);
                                row["Canceladas"] = "000";
                            }

                            switch (row["castigo"].ToString())
                            {
                                case "Y":
                                    row["Calificacion"] = "06";
                                    row["EstadoObli"] = "06";
                                    row["saldo"] = row["saldo_inicial"];
                                    row["saldomora"] = row["saldo_inicial"];
                                    row["cuotasenmora"] = "0";
                                    if (Convert.ToInt32(row["CalifiEdadMora"]) >= 13)
                                    {
                                        row["CalifiEdadMora"] = "13";
                                    }
                                    else
                                    {
                                        row["CalifiEdadMora"] = "12";
                                    }
                                    break;
                                case "N":
                                    row["Calificacion"] = "01";
                                    row["EstadoObli"] = "08";
                                    row["saldo"] = 0;
                                    row["saldomora"] = 0;
                                    row["cuotasenmora"] = "0";
                                    row["CalifiEdadMora"] = "00";
                                    row["cuota"] = 0;
                                    row["FechaPago"] = fechafinal.ToString("yyyyMMdd");
                                    break;
                            }

                            switch (row["EstadoObli"].ToString())
                            {
                                case "07":
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
                                    break;
                                default:
                                    if ((row["estadocifin"].ToString() == "05" && Convert.ToInt32(row["estadocifin"]) > Convert.ToInt32(row["EstadoObli"])))
                                    {
                                        row["EstadoObli"] = "05";
                                        row["Calificacion"] = "05";
                                    }
                                    if ((row["estadocifin"].ToString() == "04" && Convert.ToInt32(row["estadocifin"]) > Convert.ToInt32(row["EstadoObli"])))
                                    {
                                        row["EstadoObli"] = "04";
                                        row["Calificacion"] = "04";
                                    }
                                    break;
                            }

                            switch (MoraNomina)
                            {
                                case "N":
                                    row["saldomora"] = 0;
                                    row["cuotasenmora"] = "0";
                                    row["CalifiEdadMora"] = "00";
                                    break;
                            }

                            switch (row["motivopago"].ToString().Trim())
                            {
                                case "":
                                case "0":
                                    row["motivopago"] = "01";
                                    break;
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

                            switch (row["FOGACLA"].ToString())
                            {
                                case "03":
                                    lineadecredito = "014";
                                    break;
                                default:
                                    lineadecredito = "065";
                                    switch (row["FOGACLA"].ToString())
                                    {
                                        case "04":
                                            row["FOGACLA"] = "05";
                                            break;
                                        case "05":
                                            row["FOGACLA"] = "04";
                                            break;
                                    }
                                    break;
                            }
                            codciudad = Convert.ToInt32(Strings.Right(row["dpto_ciudad"].ToString(), 3)).ToString();
                            coddpto = row["dpto_ciudad"].ToString().Length > 4 ? Strings.Mid(row["dpto_ciudad"].ToString(), 1, 2) : Strings.Mid(row["dpto_ciudad"].ToString(), 1, 1);

                            if (Convert.ToDouble(row["saldo"]) != 0)
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

                            if (row["castigo"].ToString() == "Y")
                            {
                                Cuotaspendientes = Convert.ToDouble(CuotasPactadas) - Convert.ToDouble(row["Canceladas"]);
                                row["cuotasenmora"] = Cuotaspendientes;
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

                            switch (row["EstadoObli"].ToString())
                            {
                                case "07":
                                case "08":
                                    row["Canceladas"] = Convert.ToDouble(CuotasPactadas);
                                    break;
                            }

                            switch (row["EstadoObli"].ToString())
                            {
                                case "04":
                                case "05":
                                case "06":
                                    if (Convert.ToDouble(row["saldo"]) > 0 && (Convert.ToDouble(row["saldomora"]) == 0 || row["CalifiEdadMora"].ToString() == "00"))
                                    {
                                        dsreporte.Tables["NovedadesCIFIN"].Rows.Add("5", row["codigoter"], row["lincred"], row["numero"], saldo, saldomora, row["EstadoObli"]);
                                        row["Calificacion"] = "01";
                                        row["EstadoObli"] = "01";
                                        row["CalifiEdadMora"] = "00";
                                        dsreporte.Tables["CambioEstado"].Rows.Add(NumObligacion, row["agencia"], row["EstadoObli"], row["Calificacion"], row["CalifiEdadMora"], row["saldo"], row["saldomora"]);
                                    }
                                    break;
                            }

                            dsreporte.Tables["incluidoscifin"].Rows.Add(row["Tipo_nit"], row["CedulaNit"], NombreReportado, NumObligacion,
                                        row["agencia"], "P", row["Calificacion"], row["SituacionTitular"], row["EstadoObli"], row["CalifiEdadMora"],
                                        row["aniosmora"], fechafinal.ToString("yyyyMMdd"), row["fecaprob"], row["fecvemto"], row["fechaexigibilidad"], row["fechaprescripcion"],
                                        row["FechaPago"], row["motivopago"], row["TipoPago"], row["periodd"], row["Probabilidad"], row["Canceladas"], CuotasPactadas,
                                        row["cuotasenmora"], row["valorob"], row["saldomora"], row["saldo"], row["cuota"], lineadecredito, row["tipocontrato"], row["estadocontrato"],
                                        row["Naturaleza"], row["FOGACLA"], "01", row["clasegar"], "0", row["ObligReestructurada"], row["NatReestructura"], row["NumReestructura"],
                                        row["direccion"], row["telefono1"], codciudad, row["nombre_ciudad"], coddpto, row["dpto"], row["empresa_labora"], row["dirempresa"], row["telempresa"],
                                        codciudad, row["nombre_ciudad"], coddpto, row["dpto"]);

                            switch (row["EstadoObli"].ToString())
                            {
                                case "04":
                                case "05":
                                case "06":
                                case "07":
                                case "08":
                                    this.ActualizaEstadoCifin(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(fechafinal.ToString("yyyyMM")), row["EstadoObli"].ToString(), myconnect);
                                    break;
                            }

                            switch (imprimecodeudores)
                            {
                                case "Y":
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
                                    break;
                            }
                            break;
                    }
                    MsgBarra.PerformStep();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            MsgBarra.Close();
            MsgBarra.Dispose();
            return dsreporte;
        }

        // VB line 24332
        private DataSet OrganizaDatosLavadoActivos(DateTime FechaInicial, DateTime FechaFinal, System.Windows.Forms.Form forma, OdbcConnection myconnect)
        {
            double Total = 0;
            DataSet dsdatalav = new DataSet();

            stmysql = "delete from cop_tmplavado";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "OrganizaDatosLavadoActivos");

            // Total = this.BuscaDatosLavadoActivosCartera(FechaInicial, FechaFinal, forma, myconnect); // ERROR: CS1061
            // this.BuscaDatosLavadoActivosDepositos(FechaInicial, FechaFinal, Total, forma, myconnect); // ERROR: CS1061
            dsdatalav = this.OrganizaDatosPlanosLavado(forma, myconnect);
            return dsdatalav;
        }

        // VB line 24344
        private DataSet OrganizaDatosPlanosLavado(System.Windows.Forms.Form forma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress barra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando Datos Plano Efectivo", forma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsData = new DataSet();
            DataSet DsLavado = new DataSet();
            DataSet dsdataset = new DataSet();
            DateTime StFecha = new DateTime(1950, 1, 1);
            string StString = "";
            double StDouble = 0;
            double fila = 0;

            barra.Show(forma);

            DsLavado.Tables.Add("tbllavado");
            DsLavado.Tables["tbllavado"].Columns.Add("compronte", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("numero_domto", StDouble.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("fecha", StFecha.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("valor", StDouble.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("tipoproducto", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("tipotransaccion", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("numproducto", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("tipoid", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("numeroid", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("apellido1", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("apellido2", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("nombre1", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("nombre2", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("razonsocial", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("acteconomico", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("ingresomensual", StDouble.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("tipoidcliente", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("numidcliente", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("apellidocliente1", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("apellidocliente2", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("nombrecliente1", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("nombrecliente2", StString.GetType());
            DsLavado.Tables["tbllavado"].Columns.Add("NumSecuencia", StDouble.GetType());

            StBuilder.Append("select lava.codigoter,sum(lava.valor) as valor ");
            StBuilder.Append("from cop_tmplavado lava ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("group by lava.codigoter,cia.LIM_MES_LAVA_ACTI ");
            StBuilder.Append("having sum(lava.valor)>=cia.LIM_MES_LAVA_ACTI ");
            StBuilder.Append("order by lava.codigoter");
            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "OrganizaDatosPlanosLavado(Mes)", DsData, "tbllavames");

            StBuilder.Replace(StBuilder.ToString(), "");
            StBuilder.Append("select lava.codigoter,lava.valor,lava.id from cop_tmplavado lava ");
            StBuilder.Append("inner join sys_compania cia on cia.codigo='" + varini.sptCodEmpr + "' ");
            StBuilder.Append("where lava.valor>=cia.LIM_DIARIO_LAVA_ACTI and lava.codigoter not in ");
            StBuilder.Append("(select lava2.codigoter from cop_tmplavado lava2,sys_compania cia group by lava2.codigoter,cia.LIM_MES_LAVA_ACTI ");
            StBuilder.Append("having sum(lava2.valor)>=cia.LIM_MES_LAVA_ACTI) ");
            StBuilder.Append("order by lava.codigoter");
            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "OrganizaDatosPlanosLavado(Diario)", DsData, "tbllavadiario");

            StDouble = DsData.Tables["tbllavames"].Rows.Count + DsData.Tables["tbllavadiario"].Rows.Count;

            barra.ValorMinimoMaximo(0, (int)StDouble);

            for (fila = 0; fila <= DsData.Tables["tbllavames"].Rows.Count - 1; fila++)
            {
                DataRow row = DsData.Tables["tbllavames"].Rows[(int)fila];
                // dsdataset = this.BuscaDetalleLavado(row["codigoter"].ToString(), myconnect); // ERROR: CS1061
                this.AgregarDatosLavado(ref DsLavado, dsdataset);
                dsdataset.Tables.Clear();
                barra.PerformStep();
            }

            for (fila = 0; fila <= DsData.Tables["tbllavadiario"].Rows.Count - 1; fila++)
            {
                DataRow row = DsData.Tables["tbllavadiario"].Rows[(int)fila];
                // dsdataset = this.BuscaDetalleLavado(row["codigoter"].ToString(), myconnect, Convert.ToDouble(row["id"])); // ERROR: CS1061
                this.AgregarDatosLavado(ref DsLavado, dsdataset);
                dsdataset.Tables.Clear();
                barra.PerformStep();
            }

            barra.Close();
            barra.Dispose();

            return DsLavado;
        }

        // VB line 24428
        private void AgregarDatosLavado(ref DataSet dsdata, DataSet dsdataorigen)
        {
            double fila = 0;
            string TipoProducto = "";
            string TipoTrans = "";
            string TipoId = "";
            string ActEcono = "";
            string TipoOperaDecla = "";
            double VlrDecl = 0;
            string idcliente = "";
            string tipoidcliente = "";
            string NomCliente1 = "";
            string NomCliente2 = "";
            string ApellidoCliente1 = "";
            string ApellidoCliente2 = "";
            string RazonSocial = "";
            string Nombre1 = "";
            string nombre2 = "";
            string apellido1 = "";
            string apellido2 = "";

            for (fila = 0; fila <= dsdataorigen.Tables["tbldatalavado"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdataorigen.Tables["tbldatalavado"].Rows[(int)fila];

                if (Convert.ToDouble(row["idsecuencia"]) > 0)
                {
                    TipoProducto = "90";
                }
                else
                {
                    if (Convert.ToDouble(row["cdats"]) > 0)
                    {
                        TipoProducto = "91";
                    }
                    else
                    {
                        TipoProducto = "09";
                    }
                }

                switch (row["tipotrans"].ToString())
                {
                    case "R":
                        TipoTrans = "1";
                        break;
                    case "D":
                        TipoTrans = "2";
                        break;
                }

                switch (row["tipo_nit"].ToString())
                {
                    case "R":
                        TipoId = "11";
                        break;
                    case "T":
                        TipoId = "12";
                        break;
                    case "C":
                        TipoId = "13";
                        break;
                    case "E":
                        TipoId = "22";
                        break;
                    case "N":
                        TipoId = "31";
                        break;
                    default:
                        TipoId = "00";
                        break;
                }

                switch (row["tipo_nit"].ToString())
                {
                    case "N":
                        RazonSocial = row["apellido"].ToString() + " " + row["nombre"].ToString();
                        apellido1 = ""; apellido2 = ""; Nombre1 = ""; nombre2 = "";
                        break;
                    default:
                        RazonSocial = "";
                        // this.msgconfig.Separanombres(row["apellido"].ToString() + " " + row["nombre"].ToString(), ref apellido1, ref apellido2, ref Nombre1, ref nombre2); // ERROR: CS1061
                        break;
                }

                if (row["actividadeconomica"] is DBNull)
                {
                    ActEcono = row["nomprofesion"].ToString();
                }
                else
                {
                    ActEcono = row["actividadeconomica"].ToString();
                }

                if (row["idcliente"] is DBNull)
                {
                    idcliente = ""; tipoidcliente = ""; NomCliente1 = ""; NomCliente2 = "";
                    ApellidoCliente1 = ""; ApellidoCliente2 = "";
                }
                else
                {
                    idcliente = row["idcliente"].ToString();
                    switch (row["tipoidcliente"].ToString())
                    {
                        case "R":
                            tipoidcliente = "11";
                            break;
                        case "T":
                            tipoidcliente = "12";
                            break;
                        case "C":
                            tipoidcliente = "13";
                            break;
                        case "E":
                            tipoidcliente = "22";
                            break;
                        case "N":
                            tipoidcliente = "31";
                            break;
                        default:
                            tipoidcliente = "00";
                            break;
                    }
                    // this.msgconfig.Separanombres(row["apellidocliente"].ToString() + " " + row["nombrecliente"].ToString(), ref ApellidoCliente1, ref ApellidoCliente2, ref NomCliente1, ref NomCliente2); // ERROR: CS1061
                }

                dsdata.Tables["tbllavado"].Rows.Add(row["compronte"], row["numero_domto"], row["fecha_movto"], row["valor"], TipoProducto, TipoTrans, row["numproducto"], TipoId,
                    row["nit"], apellido1, apellido2, Nombre1, nombre2, RazonSocial, ActEcono, row["Ingresos"], tipoidcliente, idcliente, ApellidoCliente1, ApellidoCliente2, NomCliente1, NomCliente2, row["NumSecuencia"]);
            }
        }

        // VB line 24537
        private void GeneraArchivoPlanoLavado(DataTable dsdata, DateTime FechaFinal, int CodCiudad, string CodEntidad,
                string CodOficina, StreamWriter StArchivo, System.Windows.Forms.Form forma)
        {
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo Plano", forma);
            double fila = 0;

            msgbarra.ValorMinimoMaximo(0, dsdata.Rows.Count);
            msgbarra.Show(forma);

            StArchivo.Write(Strings.Right(Strings.Space(10) + "0", 10));
            StArchivo.Write(CodEntidad);
            StArchivo.Write(FechaFinal.ToString("yyyy-MM-dd"));
            StArchivo.Write(Strings.Right(Strings.Space(10) + dsdata.Rows.Count.ToString(), 10));
            StArchivo.WriteLine(Strings.Replace(Strings.Space(510), " ", "X"));

            for (fila = 0; fila <= dsdata.Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Rows[(int)fila];
                StArchivo.Write(Strings.Right(Strings.Space(10) + (fila + 1).ToString(), 10));
                StArchivo.Write(Convert.ToDateTime(row["fecha"]).ToString("yyyy-MM-dd"));
                StArchivo.Write(Strings.Right(Strings.Space(20) + Strings.FormatNumber(Convert.ToDouble(row["valor"]), 0, TriState.False, TriState.UseDefault, TriState.False), 20));
                StArchivo.Write("1");
                StArchivo.Write(Strings.Left(CodOficina + Strings.Space(15), 15));
                StArchivo.Write(CodCiudad.ToString());
                StArchivo.Write(row["tipoproducto"].ToString());
                StArchivo.Write(row["tipotransaccion"].ToString());
                StArchivo.Write(Strings.Left(row["numproducto"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(row["tipoid"].ToString());
                StArchivo.Write(Strings.Left(row["numeroid"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Left(row["apellido1"].ToString() + Strings.Space(40), 40));
                StArchivo.Write(Strings.Left(row["apellido2"].ToString() + Strings.Space(40), 40));
                StArchivo.Write(Strings.Left(row["nombre1"].ToString() + Strings.Space(40), 40));
                StArchivo.Write(Strings.Left(row["nombre2"].ToString() + Strings.Space(40), 40));
                StArchivo.Write(Strings.Left(row["razonsocial"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Left(row["acteconomico"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Right(Strings.Space(20) + Strings.FormatNumber(Convert.ToDouble(row["ingresomensual"]), 2, TriState.UseDefault, TriState.UseDefault, TriState.False), 20));
                StArchivo.Write(Strings.Right("  " + row["tipoidcliente"].ToString(), 2));
                StArchivo.Write(Strings.Left(row["numidcliente"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Left(row["apellidocliente1"].ToString() + Strings.Space(40), 40));
                StArchivo.Write(Strings.Left(row["apellidocliente2"].ToString() + Strings.Space(40), 40));
                StArchivo.Write(Strings.Left(row["nombrecliente1"].ToString() + Strings.Space(40), 40));
                StArchivo.WriteLine(Strings.Left(row["nombrecliente2"].ToString() + Strings.Space(40), 40));
                msgbarra.PerformStep();
            }

            StArchivo.Write(Strings.Right(Strings.Space(10) + "0", 10));
            StArchivo.Write(CodEntidad);
            StArchivo.Write(Strings.Right(Strings.Space(10) + dsdata.Rows.Count.ToString(), 10));
            StArchivo.WriteLine(Strings.Replace(Strings.Space(520), " ", "X"));

            StArchivo.Close();
            msgbarra.Close();
            msgbarra.Dispose();
        }

        // VB line 24600
        private void ImprimeReporteLavado(DataTable dsdata, DateTime fechainicial, DateTime fechafinal,
            System.Windows.Forms.Form forma, OdbcConnection myconnect)
        {
            DataSet dscompania = new DataSet();
            ERP.Core.Compartido.Reportes.reporte rep = new ERP.Core.Compartido.Reportes.reporte("cop_rinflavado");
            ERP.Core.Compartido.Reportes.config_report configrep = new ERP.Core.Compartido.Reportes.config_report();

            this.msgcofsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

            rep.SetDataSource(dsdata);
            rep.SetParameterValue("nombre_empresa", dscompania.Tables["tblcompania"].Rows[0]["nombre"]);
            rep.SetParameterValue("nit", dscompania.Tables["tblcompania"].Rows[0]["nit"]);
            rep.SetParameterValue("direccion", dscompania.Tables["tblcompania"].Rows[0]["Direccion"]);
            rep.SetParameterValue("telefono", dscompania.Tables["tblcompania"].Rows[0]["TELEFONO"]);
            rep.SetParameterValue("fechaini", fechainicial);
            rep.SetParameterValue("fechafin", fechafinal);
            configrep.confi_reportes(forma, rep);
        }

        // VB line 24663
        private void creaDatasetIncluidosDataCredito(ref DataSet dsdata, string Renumera)
        {
            string ststring = " ";
            int stinteger = 0;
            double stdouble = 0;
            try
            {
                dsdata.Tables.Add("incluidosDataCredito");
            }
            catch (Exception ex)
            {
            }
            dsdata.Tables["incluidosDataCredito"].Columns.Add("tipoid", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("numid", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("obligacion", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("nombretitular", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("situaciontitular", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechaapertura", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechavemto", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("calidad", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("tipoobligacion", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("subsidio", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechasubsidio", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("terminocontrato", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("formapago", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("periodicidad", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("novedad", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("estadoorigencta", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechaestadoorigen", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("estadocuenta", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechaestadocuenta", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("estadoplastico", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechaestadoplastico", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("adjetivo", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechaadjetivo", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("clasetarj", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("franquicia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("nommarca", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("tipomoneda", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("tipogarantia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("calificacion", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("probabilidadIncump", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("edadmora", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("valorinicial", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("saldodeuda", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("valordisponible", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("valorcuota", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("valorsaldomora", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("totalcuotas", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("cuotascanceladas", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("cuotasenmora", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("clausulapermanencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechaclausula", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechalimitepago", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("fechapago", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("oficinaradicacion", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("ciudadradicacion", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("codigociudadradicacion", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("ciudadresidencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("codigociudadresidencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("deptoresidencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("direccionresidencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("telefonoresidencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("ciudadlaboral", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("codigociudadlaboral", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("deptolaboral", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("direccionlaboral", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("telefonolaboral", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("ciudadcorrespondencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("codigociudadcorrespon", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("deptocorrespondencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("direccioncorrespondencia", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("correo", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("celular", ststring.GetType());
            dsdata.Tables["incluidosDataCredito"].Columns.Add("suscriptor", ststring.GetType());

            switch (Renumera)
            {
                case "Y":
                    try
                    {
                        dsdata.Tables.Add("RenumeraDataCredito");
                    }
                    catch (Exception ex)
                    {
                    }
                    dsdata.Tables["RenumeraDataCredito"].Columns.Add("cuentaanterior", ststring.GetType());
                    dsdata.Tables["RenumeraDataCredito"].Columns.Add("documento", ststring.GetType());
                    dsdata.Tables["RenumeraDataCredito"].Columns.Add("tipodoc", ststring.GetType());
                    dsdata.Tables["RenumeraDataCredito"].Columns.Add("cuentanueva", ststring.GetType());
                    break;
            }
        }

        // VB line 24752
        private DataSet OrganizaDatosDataCredito(DateTime fechafinal, string imprimecodeudores, int CodCiudad,
            string Renumera, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando Datos DataCredito", myforma);
            int totalregistros = 0;
            int fila = 0;
            int i = 0;
            OdbcCommand mycommand = new OdbcCommand();
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            DataSet dsdatacredito = new DataSet();
            DataSet dsreporte = new DataSet();
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
            this.creaDatasetIncluidosDataCredito(ref dsreporte, Renumera);

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
            stbuilder.Append("DeptoCorreocod4, emailcod4, movilcod4,AutorizacionMC,AutorizacionHV,CICLOD,codigo_empresa,empresa,Calificacion ");
            stbuilder.Append("from cop_datacredito_vw datacred ");
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

                obligacion = Strings.Right("0000000000" + row["nit"].ToString(), 10) + row["lincred"].ToString() + Strings.Right("0000" + row["NUMERO"].ToString(), 4);

                switch (reportar)
                {
                    case "Y":

                        switch (Renumera)
                        {
                            case "Y":
                                if (Convert.ToDateTime(row["fechaapertura"]) < FechaIni)
                                {
                                    obligacionAnt = Strings.Right("00" + Strings.Mid(row["empresa"].ToString(), 3), 2) + Strings.Right("00000000" + row["codigo_empresa"].ToString(), 8) + Strings.Mid(row["lincred"].ToString(), 3) + Strings.Right("000000" + row["NUMERO"].ToString(), 6);
                                    dsreporte.Tables["RenumeraDataCredito"].Rows.Add(obligacionAnt, Strings.Right("00000000000" + row["nit"].ToString(), 11), row["TipoNit"].ToString(), obligacion);
                                }
                                break;
                        }

                        NombreTitular = row["apellido"].ToString().ToUpper().Trim() + " " + row["nombre"].ToString().ToUpper().Trim();
                        NombreTitular = Strings.Replace(NombreTitular, ".", "");

                        if (row["FechaMovimiento"] is DBNull)
                        {
                            fechamovto = fechafinal.ToString("yyyyMMdd");
                        }
                        else
                        {
                            fechamovto = Convert.ToDateTime(row["FechaMovimiento"]).ToString("yyyyMMdd");
                        }

                        if (Convert.ToDouble(row["SaldoMora"]) > 0)
                        {
                            MotivoPago = "0";
                            fechamovto = fechafinal.ToString("yyyyMMdd");
                        }
                        else
                        {
                            switch (row["motivopago"].ToString().Trim())
                            {
                                case "":
                                case "0":
                                    MotivoPago = "1";
                                    break;
                                default:
                                    MotivoPago = row["motivopago"].ToString().Trim();
                                    break;
                            }
                        }

                        // Establece la novedad
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
                                if (Convert.ToDouble(row["DiasMora"]) < 30)
                                {
                                    novedad = "01";
                                    row["SaldoMora"] = 0;
                                }
                                else if (Convert.ToDouble(row["DiasMora"]) < 60)
                                {
                                    novedad = "06";
                                }
                                else if (Convert.ToDouble(row["DiasMora"]) < 90)
                                {
                                    novedad = "07";
                                }
                                else if (Convert.ToDouble(row["DiasMora"]) < 120)
                                {
                                    novedad = "08";
                                }
                                else
                                {
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
                            row["Canceladas"] = "0";
                        }

                        if (row["fecvemto"] is DBNull)
                        {
                            fechavemto = Convert.ToDateTime(row["fechaapertura"]).AddMonths(Convert.ToInt32(row["plazo"]));
                        }
                        else
                        {
                            fechavemto = Convert.ToDateTime(row["fecvemto"]);
                        }

                        FecLimPago = this.VerificaMaximaFechaCausacion(row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["periodo"]), myconnect);

                        if (FecLimPago == new DateTime(1950, 1, 1))
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

                        switch (imprimecodeudores)
                        {
                            case "Y":
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
                                break;
                        }

                        break;
                }

                MsgBarra.PerformStep();
            }
            MsgBarra.Close();
            MsgBarra.Dispose();
            return dsreporte;
        }

        // VB line 24997
        private void GeneraPlanoDataCredito(DateTime fechaCorte, string CodSuscriptor, string TipoCta,
             StreamWriter StArchivo, string Renumera, StreamWriter StArchivoRenum, DataSet dsdata, System.Windows.Forms.Form myforma)
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo plano DataCredito", myforma);
            int fila = 0;
            string espacios60 = Strings.Space(60);
            decimal total = 0;
            string Valor = "";

            barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidosDataCredito"].Rows.Count);
            barraprogress.Show();

            // Registro tipo 1. Control del reporte
            StArchivo.Write(Strings.Replace(Strings.Space(18), " ", "H"));
            StArchivo.Write(Strings.Right("000000" + CodSuscriptor, 6));
            StArchivo.Write(Strings.Right("00" + TipoCta, 2));
            StArchivo.Write(fechaCorte.ToString("yyyyMMdd"));
            StArchivo.Write("M");
            StArchivo.Write(" ");
            StArchivo.Write("T");
            StArchivo.Write("00000000");
            StArchivo.Write("00000000");
            StArchivo.Write(" ");
            StArchivo.WriteLine(Strings.Replace(Strings.Space(326), " ", "0") + Strings.Space(420));

            // Registro tipo 2. Detalles
            for (fila = 0; fila <= dsdata.Tables["incluidosDataCredito"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Tables["incluidosDataCredito"].Rows[fila];
                StArchivo.Write(row["tipoid"]);
                StArchivo.Write(Strings.Right("00000000000" + row["numid"].ToString(), 11));
                StArchivo.Write(Strings.Left(row["obligacion"].ToString() + "000000000000000000", 18));
                StArchivo.Write(Strings.Left(row["nombretitular"].ToString() + Strings.Space(45), 45));
                StArchivo.Write(row["situaciontitular"]);
                StArchivo.Write(row["fechaapertura"]);
                StArchivo.Write(row["fechavemto"]);
                StArchivo.Write(Strings.Right("00" + row["calidad"].ToString(), 2));
                StArchivo.Write(row["tipoobligacion"]);
                StArchivo.Write(row["subsidio"]);
                StArchivo.Write(row["fechasubsidio"]);
                StArchivo.Write(row["terminocontrato"]);
                StArchivo.Write(row["formapago"]);
                StArchivo.Write(row["periodicidad"]);
                StArchivo.Write(Strings.Right("00" + row["novedad"].ToString(), 2));
                StArchivo.Write(row["estadoorigencta"]);
                StArchivo.Write(row["fechaestadoorigen"]);
                StArchivo.Write(Strings.Right("00" + row["estadocuenta"].ToString(), 2));
                StArchivo.Write(row["fechaestadocuenta"]);
                StArchivo.Write(row["estadoplastico"]);
                StArchivo.Write(row["fechaestadoplastico"]);
                StArchivo.Write(row["adjetivo"]);
                StArchivo.Write(row["fechaadjetivo"]);
                StArchivo.Write(row["clasetarj"]);
                StArchivo.Write(row["franquicia"]);
                StArchivo.Write(Strings.Left(row["nommarca"].ToString() + Strings.Space(30), 30));
                StArchivo.Write(row["tipomoneda"].ToString());
                StArchivo.Write(row["tipogarantia"].ToString());
                StArchivo.Write(Strings.Right("  " + row["calificacion"].ToString(), 2));
                StArchivo.Write(Strings.Right("000" + row["probabilidadIncump"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["edadmora"].ToString(), 3));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorinicial"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["saldodeuda"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valordisponible"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorcuota"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorsaldomora"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                StArchivo.Write(Strings.Right("000" + row["totalcuotas"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["cuotascanceladas"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["cuotasenmora"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["clausulapermanencia"].ToString(), 3));
                StArchivo.Write(row["fechaclausula"].ToString());
                StArchivo.Write(row["fechalimitepago"]);
                StArchivo.Write(row["fechapago"]);
                StArchivo.Write(Strings.Left(row["oficinaradicacion"].ToString() + Strings.Space(30), 30));
                StArchivo.Write(Strings.Left(row["ciudadradicacion"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadradicacion"].ToString(), 8));
                StArchivo.Write(Strings.Left(row["ciudadresidencia"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadresidencia"].ToString(), 8));
                StArchivo.Write(Strings.Left(row["deptoresidencia"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Left(row["direccionresidencia"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Right("000000000000" + row["telefonoresidencia"].ToString(), 12));
                StArchivo.Write(Strings.Left(row["ciudadlaboral"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadlaboral"].ToString(), 8));
                StArchivo.Write(Strings.Left(row["deptolaboral"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Left(row["direccionlaboral"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Right("000000000000" + row["telefonolaboral"].ToString(), 12));
                StArchivo.Write(Strings.Left(row["ciudadcorrespondencia"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Right("00000000" + row["codigociudadcorrespon"].ToString(), 8));
                StArchivo.Write(Strings.Left(row["deptocorrespondencia"].ToString() + Strings.Space(20), 20));
                StArchivo.Write(Strings.Left(row["direccioncorrespondencia"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Left(row["correo"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Right("000000000000" + row["celular"].ToString(), 12));
                StArchivo.Write(Strings.Right("000000" + row["suscriptor"].ToString(), 6));
                StArchivo.WriteLine(Strings.Space(37));
                barraprogress.PerformStep();
            }
            // Registro tipo 9. Control de fin de archivo
            total = dsdata.Tables["incluidosDataCredito"].Rows.Count + 2;
            total = Convert.ToDecimal(Strings.FormatNumber(Math.Round(total, 0), 0, TriState.False, TriState.False, TriState.False));
            StArchivo.Write(Strings.Replace(Strings.Space(18), " ", "Z"));
            StArchivo.Write(DateTime.Now.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Right("00000000" + total.ToString(), 8));
            StArchivo.Write(Strings.Right("00000000" + (total - 2).ToString(), 8));
            StArchivo.Write(Strings.Space(758));
            StArchivo.Close();

            switch (Renumera)
            {
                case "Y":
                    barraprogress.Titulo("Generando archivo plano renumeracion DataCredito");
                    barraprogress.ValorMinimoMaximo(0, dsdata.Tables["RenumeraDataCredito"].Rows.Count);
                    barraprogress.Show();

                    for (fila = 0; fila <= dsdata.Tables["RenumeraDataCredito"].Rows.Count - 1; fila++)
                    {
                        DataRow row = dsdata.Tables["RenumeraDataCredito"].Rows[fila];
                        StArchivoRenum.Write(row["cuentaanterior"]);
                        StArchivoRenum.Write(row["documento"]);
                        StArchivoRenum.Write(row["tipodoc"]);
                        StArchivoRenum.WriteLine(row["cuentanueva"]);
                        barraprogress.PerformStep();
                    }
                    StArchivoRenum.Close();
                    break;
            }

            barraprogress.Close();
            barraprogress.Dispose();
        }

        // VB line 25148
        private void GeneraPlanoDataCreditoResum(DateTime fechaCorte, string CodSuscriptor, string TipoCta,
            StreamWriter StArchivo, string Renumera, StreamWriter StArchivoRenum, DataSet dsdata, System.Windows.Forms.Form myforma)
        {
            ERP.Core.Compartido.Controles.Barraprogress barraprogress = new ERP.Core.Compartido.Controles.Barraprogress("Generando Archivo plano DataCredito", myforma);
            int fila = 0;
            string espacios60 = Strings.Space(60);
            decimal total = 0;
            string Valor = "";

            barraprogress.ValorMinimoMaximo(0, dsdata.Tables["incluidosDataCredito"].Rows.Count);
            barraprogress.Show();

            // Registro tipo 2. Detalles (sin registro tipo 1 en Resum)
            for (fila = 0; fila <= dsdata.Tables["incluidosDataCredito"].Rows.Count - 1; fila++)
            {
                DataRow row = dsdata.Tables["incluidosDataCredito"].Rows[fila];
                StArchivo.Write(row["tipoid"]);
                StArchivo.Write(Strings.Right("00000000000" + row["numid"].ToString(), 11));
                StArchivo.Write(Strings.Left(row["obligacion"].ToString() + "000000000000000000", 18));
                StArchivo.Write(Strings.Left(row["nombretitular"].ToString() + Strings.Space(45), 45));
                StArchivo.Write(row["situaciontitular"]);
                StArchivo.Write(row["fechaapertura"]);
                StArchivo.Write(row["fechavemto"]);
                StArchivo.Write(Strings.Right("00" + row["calidad"].ToString(), 2));
                StArchivo.Write(row["tipoobligacion"]);
                //StArchivo.Write(row["subsidio"]); // commented in VB
                StArchivo.Write(row["fechasubsidio"]);
                StArchivo.Write(row["terminocontrato"]);
                StArchivo.Write(row["formapago"]);
                StArchivo.Write(row["periodicidad"]);
                StArchivo.Write(Strings.Right("00" + row["novedad"].ToString(), 2));
                StArchivo.Write(row["estadoorigencta"]);
                //StArchivo.Write(row["fechaestadoorigen"]); // commented in VB
                StArchivo.Write(Strings.Right("00" + row["estadocuenta"].ToString(), 2));
                //StArchivo.Write(row["fechaestadocuenta"]); // commented in VB
                //StArchivo.Write(row["estadoplastico"]); // commented in VB
                //StArchivo.Write(row["fechaestadoplastico"]); // commented in VB
                StArchivo.Write(row["adjetivo"]);
                //StArchivo.Write(row["fechaadjetivo"]); // commented in VB
                //StArchivo.Write(row["clasetarj"]); // commented in VB
                //StArchivo.Write(row["franquicia"]); // commented in VB
                //StArchivo.Write(Strings.Left(row["nommarca"].ToString() + Strings.Space(30), 30)); // commented in VB
                StArchivo.Write(row["tipomoneda"].ToString());
                StArchivo.Write(row["tipogarantia"].ToString());
                //StArchivo.Write(Strings.Right("  " + row["calificacion"].ToString(), 2)); // commented in VB
                //StArchivo.Write(Strings.Right("000" + row["probabilidadIncump"].ToString(), 3)); // commented in VB
                StArchivo.Write(Strings.Right("000" + row["edadmora"].ToString(), 3));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorinicial"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["saldodeuda"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valordisponible"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorcuota"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valorsaldomora"]), 0, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("00000000000" + Valor, 11);
                StArchivo.Write(Strings.Right("00000000000" + Valor, 11));
                StArchivo.Write(Strings.Right("000" + row["totalcuotas"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["cuotascanceladas"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["cuotasenmora"].ToString(), 3));
                StArchivo.Write(Strings.Right("000" + row["clausulapermanencia"].ToString(), 3));
                //StArchivo.Write(row["fechaclausula"].ToString()); // commented in VB
                StArchivo.Write(row["fechalimitepago"]);
                StArchivo.Write(row["fechapago"]);
                StArchivo.Write(Strings.Left(row["oficinaradicacion"].ToString() + Strings.Space(30), 30));
                StArchivo.Write(Strings.Left(row["ciudadradicacion"].ToString() + Strings.Space(20), 20));
                //StArchivo.Write(Strings.Right("00000000" + row["codigociudadradicacion"].ToString(), 8)); // commented in VB
                StArchivo.Write(Strings.Left(row["ciudadresidencia"].ToString() + Strings.Space(20), 20));
                //StArchivo.Write(Strings.Right("00000000" + row["codigociudadresidencia"].ToString(), 8)); // commented in VB
                //StArchivo.Write(Strings.Left(row["deptoresidencia"].ToString() + Strings.Space(20), 20)); // commented in VB
                //StArchivo.Write(Strings.Left(row["direccionresidencia"].ToString() + Strings.Space(60), 60)); // commented in VB
                StArchivo.Write(Strings.Right("000000000000" + row["telefonoresidencia"].ToString(), 12));
                //StArchivo.Write(Strings.Left(row["ciudadlaboral"].ToString() + Strings.Space(20), 20)); // commented in VB
                //StArchivo.Write(Strings.Right("00000000" + row["codigociudadlaboral"].ToString(), 8)); // commented in VB
                //StArchivo.Write(Strings.Left(row["deptolaboral"].ToString() + Strings.Space(20), 20)); // commented in VB
                //StArchivo.Write(Strings.Left(row["direccionlaboral"].ToString() + Strings.Space(60), 60)); // commented in VB
                //StArchivo.Write(Strings.Right("000000000000" + row["telefonolaboral"].ToString(), 12)); // commented in VB
                //StArchivo.Write(Strings.Left(row["ciudadcorrespondencia"].ToString() + Strings.Space(20), 20)); // commented in VB
                //StArchivo.Write(Strings.Right("00000000" + row["codigociudadcorrespon"].ToString(), 8)); // commented in VB
                //StArchivo.Write(Strings.Left(row["deptocorrespondencia"].ToString() + Strings.Space(20), 20)); // commented in VB
                StArchivo.Write(Strings.Left(row["direccioncorrespondencia"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Left(row["correo"].ToString() + Strings.Space(60), 60));
                StArchivo.Write(Strings.Right("000000000000" + row["celular"].ToString(), 12));
                //StArchivo.Write(Strings.Right("000000" + row["suscriptor"].ToString(), 6)); // commented in VB
                StArchivo.WriteLine(Strings.Space(37));
                barraprogress.PerformStep();
            }
            // Registro tipo 9. Control de fin de archivo
            total = dsdata.Tables["incluidosDataCredito"].Rows.Count + 2;
            total = Convert.ToDecimal(Strings.FormatNumber(Math.Round(total, 0), 0, TriState.False, TriState.False, TriState.False));
            StArchivo.Write(Strings.Replace(Strings.Space(18), " ", "Z"));
            StArchivo.Write(DateTime.Now.ToString("yyyyMMdd"));
            StArchivo.Write(Strings.Right("00000000" + total.ToString(), 8));
            StArchivo.Write(Strings.Right("00000000" + (total - 2).ToString(), 8));
            StArchivo.Write(Strings.Space(758));
            StArchivo.Close();

            switch (Renumera)
            {
                case "Y":
                    barraprogress.Titulo("Generando archivo plano renumeracion DataCredito");
                    barraprogress.ValorMinimoMaximo(0, dsdata.Tables["RenumeraDataCredito"].Rows.Count);
                    barraprogress.Show();

                    for (fila = 0; fila <= dsdata.Tables["RenumeraDataCredito"].Rows.Count - 1; fila++)
                    {
                        DataRow row = dsdata.Tables["RenumeraDataCredito"].Rows[fila];
                        StArchivoRenum.Write(row["cuentaanterior"]);
                        StArchivoRenum.Write(row["documento"]);
                        StArchivoRenum.Write(row["tipodoc"]);
                        StArchivoRenum.WriteLine(row["cuentanueva"]);
                        barraprogress.PerformStep();
                    }
                    StArchivoRenum.Close();
                    break;
            }

            barraprogress.Close();
            barraprogress.Dispose();
        }

        // VB line 25310
        private void CreaDatasetCGBatch1(ref DataSet DsData)
        {
            string StBuilder = " ";
            double StDouble = 0;
            DsData.Tables.Add("tblCG1");
            DsData.Tables["tblCG1"].Columns.Add("lapso", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("codempresa", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("cencosto", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("compronte", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("lote", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("registros", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("fecha", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("usuario", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("debitos", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("creditos", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("detalle1", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("detalle2", StBuilder.GetType());
            DsData.Tables["tblCG1"].Columns.Add("estado", StBuilder.GetType());
        }

        // VB line 25330
        private void CreaDatasetCGBatch2(ref DataSet DsData)
        {
            string StBuilder = " ";
            double StDouble = 0;
            DsData.Tables.Add("tblCG2");
            DsData.Tables["tblCG2"].Columns.Add("cuenta", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("beneficiario", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("cencosto", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("lapso", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("codempresa", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("cencostolote", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("compronte", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("numlote", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("consecutivo", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("tipodocto", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("numdocto", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("fecmovto", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("tipotransa", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("valor", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("detalle1", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("detalle2", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("destino", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("docconciliacion", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("numdocconcialia", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("vlrbase", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("doccruce", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("numdoccruce", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("fecvemto", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("nit", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("nombre", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("clase", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("direccion", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("ciudad", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("dpto", StBuilder.GetType());
            DsData.Tables["tblCG2"].Columns.Add("telefono", StBuilder.GetType());
        }

        // VB line 25367
        private DataSet OrganizaDatosCGBatch1(string lapso, string CodEmpresa, string Cencosto, string NumLote,
                DateTime FecIni, DateTime FecFin, string usuario, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando datos CGBATCH1", myforma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            DataSet DsCGBacth1 = new DataSet();
            double totalregistros = 0;
            double fila = 0;
            OdbcCommand mycommand = new OdbcCommand();
            OdbcDataAdapter Myread = new OdbcDataAdapter();

            MsgBarra.Show();

            StBuilder.Append("select compro02.CUENTA_EQUIVA as tipoCpte,compro02.NOMBRE,sum(movto.vlr_debito) as Debito,sum(movto.vlr_credito) as Credito,");
            StBuilder.Append("count(movto.secuencia) as NumRegistros,max(doc.fecha) as Fecha ");
            StBuilder.Append("from cop_movimto movto ");
            StBuilder.Append("inner join cop_docmto doc on doc.compronte=movto.compronte and doc.numero_domto=movto.numero_domto ");
            StBuilder.Append("inner join sys_compro02 compro02 on doc.compronte=compro02.codigo ");
            StBuilder.Append("where doc.cerrado='Y' and compro02.ACTUALIZA_CONTA='0' and ");
            StBuilder.Append("doc.fecha between '" + FecIni.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "' ");
            StBuilder.Append("group by compro02.CUENTA_EQUIVA,compro02.NOMBRE ");

            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "Organiza Datos CGBatch1", dsdata, "TblDatosCG1");
            totalregistros = dsdata.Tables["TblDatosCG1"].Rows.Count;

            MsgBarra.ValorMinimoMaximo(0, (int)totalregistros);

            this.CreaDatasetCGBatch1(ref DsCGBacth1);

            for (fila = 0; fila <= totalregistros - 1; fila++)
            {
                DataRow row = dsdata.Tables["TblDatosCG1"].Rows[(int)fila];
                DsCGBacth1.Tables["tblCG1"].Rows.Add(Strings.Mid(lapso, 3), CodEmpresa, Cencosto, row["tipoCpte"], NumLote, row["NumRegistros"], Convert.ToDateTime(row["Fecha"]).ToString("yyMMdd"), usuario, row["Debito"], row["Credito"], row["Nombre"], "TRASLADO AUTOMATICO CARTERA", "0");
                MsgBarra.PerformStep();
            }
            MsgBarra.Close();
            MsgBarra.Dispose();
            return DsCGBacth1;
        }

        // VB line 25417
        private DataSet OrganizaDatosCGBatch2(string lapso, string CodEmpresa, string Cencosto, string NumLote,
                DateTime FecIni, DateTime FecFin, string CenUtilidad, string CodCedula, string usuario, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Organizando datos CGBATCH2", myforma);
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            DataSet DsCGBacth2 = new DataSet();
            double totalregistros = 0;
            double fila = 0;
            OdbcCommand mycommand = new OdbcCommand();
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            double ValorTransaccion = 0;
            string TipoTransa = "";
            double Consecutivo = 1;
            string Detalle = "";
            string CentroUtilidad = "";
            string DocConcialia = "";
            string NumConcilia = "";
            string clasebenef = "C";
            string TipoCpte = "";
            string NitBenef = "";
            string cadenacuenta1 = "";
            string CuentaPub = "";
            string VarTipoNit = "";
            string FecVemto = "";
            string cade1 = "";
            string cade2 = "";
            string cade3 = "";
            string cade4 = "";
            string cade5 = "";
            string cade6 = "";
            string cade7 = "";
            string cade8 = "";

            MsgBarra.Show();

            StBuilder.Append("select movto.cuenta,movto.nit,compro02.codigo as codigocompro,compro02.CUENTA_EQUIVA as tipocpte,compro02.DOCONCI_CUENBACA as LoteEqui,compro02.documento,");
            StBuilder.Append("doc.numero_domto,movto.fecha_movto,movto.vlr_debito,movto.vlr_credito,movto.detalle as detallemovto,doc.detalle as detalledoc,movto.fecvence,");
            StBuilder.Append("cen.filler1 as cencosdestino,movto.base_reten as base,movto.domto_cruce,movto.num_doc_cruce,maenit.apellido,maenit.nombre,maenit.codigo_empresa,");
            StBuilder.Append("maenit.direccion,maenit.telefono1,maenit.tipo_nit,ciu.nombre_ciudad,ciu.dpto,maecuen.mane_cencos,maecuen.tercero,maecuen.consi_banca,maecuen.AUX_DOMTO ");
            StBuilder.Append("from cop_movimto movto ");
            StBuilder.Append("inner join cop_docmto doc on doc.compronte=movto.compronte and doc.numero_domto=movto.numero_domto ");
            StBuilder.Append("inner join sys_compro02 compro02 on doc.compronte=compro02.codigo ");
            StBuilder.Append("inner join cnt_maecuen maecuen on movto.cuenta=maecuen.cuenta ");
            StBuilder.Append("left join sys_maenit maenit on movto.nit = maenit.nit ");
            StBuilder.Append("left join sys_ciudad57 ciu on maenit.dpto_ciudad=ciu.ciudad ");
            StBuilder.Append("left join cnt_cencos cen on maecuen.cencos=cen.ccosto ");
            StBuilder.Append("where doc.cerrado='Y' and compro02.ACTUALIZA_CONTA='0' and ");
            StBuilder.Append("doc.fecha between '" + FecIni.ToString(varini.PstForFec) + "' and '" + FecFin.ToString(varini.PstForFec) + "' ");
            StBuilder.Append("order by doc.compronte,doc.numero_domto ");

            this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "Organiza Datos CGBatch2", dsdata, "TblDatosCG2");
            totalregistros = dsdata.Tables["TblDatosCG2"].Rows.Count;

            MsgBarra.ValorMinimoMaximo(0, (int)totalregistros);

            this.CreaDatasetCGBatch2(ref DsCGBacth2);

            switch (CenUtilidad)
            {
                case "1":
                    CentroUtilidad = Cencosto;
                    break;
                default:
                    CentroUtilidad = CodEmpresa;
                    break;
            }

            for (fila = 0; fila <= totalregistros - 1; fila++)
            {
                DataRow row = dsdata.Tables["TblDatosCG2"].Rows[(int)fila];
                if (fila == 0)
                {
                    TipoCpte = row["tipocpte"].ToString();
                    Consecutivo = 1;
                }
                else
                {
                    if (TipoCpte != row["tipocpte"].ToString())
                    {
                        Consecutivo = 1;
                    }
                }

                if (Convert.ToDouble(row["vlr_debito"]) != 0)
                {
                    TipoTransa = "D";
                    ValorTransaccion = Convert.ToDouble(row["vlr_debito"]);
                }
                else
                {
                    TipoTransa = "C";
                    ValorTransaccion = Convert.ToDouble(row["vlr_credito"]);
                }

                if (row["detallemovto"].ToString().Trim() != "")
                {
                    Detalle = row["detallemovto"].ToString();
                }
                else
                {
                    Detalle = row["detalledoc"].ToString();
                }

                if (row["mane_cencos"].ToString() == "N")
                {
                    row["cencosdestino"] = " ";
                }

                switch (row["consi_banca"].ToString())
                {
                    case "N":
                        DocConcialia = " ";
                        NumConcilia = "0";
                        break;
                    case "Y":
                        switch (row["codigocompro"].ToString())
                        {
                            case "0001":
                                DocConcialia = "CG";
                                NumConcilia = row["numero_domto"].ToString();
                                break;
                            case "0005":
                            case "0007":
                                DocConcialia = "CH";
                                NumConcilia = row["numero_domto"].ToString();
                                break;
                            case "0003":
                            case "0004":
                                DocConcialia = "NC";
                                NumConcilia = row["numero_domto"].ToString();
                                break;
                            default:
                                DocConcialia = " ";
                                NumConcilia = "0";
                                break;
                        }
                        break;
                }

                switch (CodCedula)
                {
                    case "0":
                        if (row["nit"] is DBNull)
                        {
                            NitBenef = "99999999999999";
                        }
                        else
                        {
                            NitBenef = row["nit"].ToString();
                        }
                        if (row["tipo_nit"] is DBNull)
                        {
                            VarTipoNit = "N";
                        }
                        switch (VarTipoNit)
                        {
                            case "N":
                                clasebenef = "N";
                                break;
                            default:
                                clasebenef = "C";
                                break;
                        }
                        break;
                    default:
                        NitBenef = row["codigo_empresa"].ToString();
                        clasebenef = "I";
                        break;
                }

                switch (row["AUX_DOMTO"].ToString())
                {
                    case "1":
                    case "2":
                        if (row["fecvence"] is DBNull)
                        {
                            FecVemto = Convert.ToDateTime(row["fecha_movto"]).ToString("yyMMdd");
                        }
                        else
                        {
                            if (Convert.ToDateTime(row["fecvence"]) <= new DateTime(1960, 1, 1))
                            {
                                FecVemto = Convert.ToDateTime(row["fecha_movto"]).ToString("yyMMdd");
                            }
                            else
                            {
                                FecVemto = Convert.ToDateTime(row["fecvence"]).ToString("yyMMdd");
                            }
                        }

                        if (row["domto_cruce"] is DBNull)
                        {
                            if (row["documento"] is DBNull)
                            {
                                row["domto_cruce"] = "NC";
                            }
                            else
                            {
                                if (row["documento"].ToString().Trim() == "")
                                {
                                    row["domto_cruce"] = "NC";
                                }
                                else
                                {
                                    row["domto_cruce"] = row["documento"];
                                }
                            }
                        }
                        else
                        {
                            if (row["domto_cruce"].ToString().Trim() == "")
                            {
                                if (row["documento"] is DBNull)
                                {
                                    row["domto_cruce"] = "NC";
                                }
                                else
                                {
                                    if (row["documento"].ToString().Trim() == "")
                                    {
                                        row["domto_cruce"] = "NC";
                                    }
                                    else
                                    {
                                        row["domto_cruce"] = row["documento"];
                                    }
                                }
                            }
                        }

                        if (!(row["num_doc_cruce"] is DBNull))
                        {
                            if (row["num_doc_cruce"].ToString() == "0")
                            {
                                row["num_doc_cruce"] = row["numero_domto"];
                            }
                        }
                        else
                        {
                            row["num_doc_cruce"] = row["numero_domto"];
                        }
                        break;
                    default:
                        row["domto_cruce"] = " ";
                        row["num_doc_cruce"] = "0";
                        FecVemto = Strings.Space(6);
                        break;
                }

                if (NitBenef == "99999999999999")
                {
                    NitBenef = " "; row["Apellido"] = " ";
                    row["Nombre"] = " "; clasebenef = " ";
                    row["direccion"] = " "; row["nombre_ciudad"] = " ";
                    row["dpto"] = " "; row["telefono1"] = " ";
                }
                cadenacuenta1 = row["cuenta"].ToString();

                cade1 = Strings.Mid(cadenacuenta1, 1, 1);
                cade2 = Strings.Mid(cadenacuenta1, 2, 1);
                cade3 = Strings.Mid(cadenacuenta1, 3, 1);
                cade4 = Strings.Mid(cadenacuenta1, 4, 1);
                cade5 = Strings.Mid(cadenacuenta1, 5, 1);
                cade6 = Strings.Mid(cadenacuenta1, 6, 1);
                cade7 = Strings.Mid(cadenacuenta1, 7, 1);
                cade8 = Strings.Mid(cadenacuenta1, 8, 1);
                if (cade8 == "0" && cade7 == "0")
                {
                    cade8 = " ";
                    cade7 = " ";
                }
                CuentaPub = cade1 + cade2 + cade3 + cade4 + cade5 + cade6 + cade7 + cade8;

                DsCGBacth2.Tables["tblCG2"].Rows.Add(Strings.Mid(CuentaPub, 1, 8), NitBenef, CentroUtilidad, Strings.Mid(lapso, 3), CodEmpresa,
                                 CentroUtilidad, row["tipocpte"], NumLote, Consecutivo, row["LoteEqui"], row["numero_domto"],
                                 Convert.ToDateTime(row["fecha_movto"]).ToString("yyMMdd"), TipoTransa, ValorTransaccion, Detalle, "TRASLADO AUTOMATICO CARTERA",
                                 row["cencosdestino"], DocConcialia, NumConcilia, row["base"], row["domto_cruce"], row["num_doc_cruce"], FecVemto,
                                 NitBenef, row["Apellido"].ToString() + " " + row["Nombre"].ToString(), clasebenef, row["direccion"], row["nombre_ciudad"], row["dpto"], row["telefono1"]);

                Consecutivo += 1;
                MsgBarra.PerformStep();
            }
            MsgBarra.Close();
            MsgBarra.Dispose();
            return DsCGBacth2;
        }

        // VB line 25662
        private void GeneraPlanoCGBATCH(string Direccion, DataSet DsCG1, DataSet DsCG2, System.Windows.Forms.Form myforma)
        {
            ERP.Core.Compartido.Controles.Barraprogress MsgBarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando plano CGBATCH", myforma);
            double fila = 0;
            TextWriter ArcCGBat1;
            TextWriter ArcCGBat2;
            string Valor = "";

            MsgBarra.Show();
            MsgBarra.ValorMinimoMaximo(0, DsCG1.Tables["tblCG1"].Rows.Count + DsCG2.Tables["tblCG2"].Rows.Count);
            try
            {
                FileSystem.MkDir(Direccion + "\\archivos planos\\export\\");
            }
            catch (Exception ex)
            {
            }

            ArcCGBat1 = File.CreateText(Direccion + "\\archivos planos\\export\\CGBATCH1.dat");
            ArcCGBat2 = File.CreateText(Direccion + "\\archivos planos\\export\\CGBATCH2.dat");

            // Archivo CGBATCH1.DAT
            for (fila = 0; fila <= DsCG1.Tables["tblCG1"].Rows.Count - 1; fila++)
            {
                DataRow row = DsCG1.Tables["tblCG1"].Rows[(int)fila];
                ArcCGBat1.Write(row["lapso"]);
                ArcCGBat1.Write(Strings.Right("  " + row["codempresa"].ToString(), 2));
                ArcCGBat1.Write(Strings.Right("  " + row["cencosto"].ToString(), 2));
                ArcCGBat1.Write(Strings.Right("  " + row["compronte"].ToString(), 2));
                ArcCGBat1.Write(row["lote"]);
                ArcCGBat1.Write(Strings.Right("0000" + row["registros"].ToString(), 4));
                ArcCGBat1.Write(row["fecha"]);
                ArcCGBat1.Write(Strings.Left(row["usuario"].ToString() + Strings.Space(15), 15));
                Valor = Strings.FormatNumber(Convert.ToDouble(row["debitos"]), 2, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("0000000000000" + Valor, 13);
                ArcCGBat1.Write(Valor + "+");
                Valor = Strings.FormatNumber(Convert.ToDouble(row["debitos"]), 2, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("0000000000000" + Valor, 13);
                ArcCGBat1.Write(Valor + "+");
                ArcCGBat1.Write(Strings.Left(row["detalle1"].ToString() + Strings.Space(30), 30));
                ArcCGBat1.Write(Strings.Left(row["detalle2"].ToString() + Strings.Space(30), 30));
                ArcCGBat1.Write(row["estado"]);
                ArcCGBat1.WriteLine(Strings.Space(16));
                MsgBarra.PerformStep();
            }
            ArcCGBat1.Close();
            ArcCGBat1.Dispose();

            // Archivo CGBATCH2.DAT
            for (fila = 0; fila <= DsCG2.Tables["tblCG2"].Rows.Count - 1; fila++)
            {
                DataRow row = DsCG2.Tables["tblCG2"].Rows[(int)fila];
                ArcCGBat2.Write(row["cuenta"]);
                ArcCGBat2.Write(Strings.Left(row["beneficiario"].ToString() + Strings.Space(9), 9));
                ArcCGBat2.Write(Strings.Right("  " + row["cencosto"].ToString(), 2));
                ArcCGBat2.Write(row["lapso"]);
                ArcCGBat2.Write(Strings.Right("  " + row["codempresa"].ToString(), 2));
                ArcCGBat2.Write(Strings.Right("  " + row["cencostolote"].ToString(), 2));
                ArcCGBat2.Write(Strings.Right("  " + row["compronte"].ToString(), 2));
                ArcCGBat2.Write(row["numlote"]);
                ArcCGBat2.Write(Strings.Right("0000" + row["consecutivo"].ToString(), 4));
                ArcCGBat2.Write(Strings.Right("  " + row["tipodocto"].ToString(), 2));
                ArcCGBat2.Write(Strings.Right("00000" + row["numdocto"].ToString(), 5));
                ArcCGBat2.Write(row["fecmovto"]);
                ArcCGBat2.Write(row["tipotransa"]);
                Valor = Strings.FormatNumber(Convert.ToDouble(row["valor"]), 2, TriState.UseDefault, TriState.UseDefault, TriState.False);
                Valor = Valor.Replace(".", "");
                Valor = Strings.Right("0000000000000" + Valor, 13);
                ArcCGBat2.Write(Valor + "+");
                ArcCGBat2.Write(Strings.Left(row["detalle1"].ToString() + Strings.Space(40), 40));
                ArcCGBat2.Write(Strings.Left(row["detalle2"].ToString() + Strings.Space(40), 40));
                ArcCGBat2.Write(Strings.Left(row["destino"].ToString() + Strings.Space(8), 8));
                ArcCGBat2.Write(Strings.Right("  " + row["docconciliacion"].ToString(), 2));
                ArcCGBat2.Write(Strings.Right("0000" + row["numdocconcialia"].ToString(), 4));
                if (Convert.ToDouble(row["vlrbase"]) >= 0)
                {
                    Valor = Strings.FormatNumber(Convert.ToDouble(row["vlrbase"]), 2, TriState.UseDefault, TriState.UseDefault, TriState.False);
                    Valor = Valor.Replace(".", "");
                    Valor = Strings.Right("00000000000" + Valor, 11) + "+";
                }
                else
                {
                    Valor = Strings.FormatNumber(Convert.ToDouble(row["vlrbase"]), 2, TriState.UseDefault, TriState.UseDefault, TriState.False);
                    Valor = Valor.Replace(".", "");
                    Valor = Valor.Replace("-", "");
                    Valor = Strings.Right("00000000000" + Valor, 11) + "-";
                }
                ArcCGBat2.Write(Valor);
                ArcCGBat2.Write("000000000+");
                ArcCGBat2.Write(Strings.Right("  " + row["doccruce"].ToString(), 2));
                ArcCGBat2.Write(Strings.Right("00000" + row["numdoccruce"].ToString(), 5));
                ArcCGBat2.Write("00");
                ArcCGBat2.Write(row["fecvemto"]);
                ArcCGBat2.Write("000");
                ArcCGBat2.Write(Strings.Left(row["nit"].ToString() + Strings.Space(11), 11));
                ArcCGBat2.Write(Strings.Left(row["nombre"].ToString() + Strings.Space(40), 40));
                ArcCGBat2.Write(Strings.Space(12));
                ArcCGBat2.Write(row["clase"]);
                ArcCGBat2.Write(Strings.Left(row["direccion"].ToString() + Strings.Space(25), 25));
                ArcCGBat2.Write(Strings.Left(row["ciudad"].ToString() + Strings.Space(12), 12));
                ArcCGBat2.Write(Strings.Left(row["dpto"].ToString() + Strings.Space(12), 12));
                ArcCGBat2.Write(Strings.Space(10));
                ArcCGBat2.Write(Strings.Left(row["telefono"].ToString() + Strings.Space(15), 15));
                ArcCGBat2.Write(Strings.Space(48));
                ArcCGBat2.Write("000000000");
                ArcCGBat2.Write("0000");
                ArcCGBat2.Write(Strings.Space(12));
                ArcCGBat2.Write("000");
                ArcCGBat2.Write(Strings.Space(180));
                ArcCGBat2.Write("00000000000+");
                ArcCGBat2.Write(Strings.Space(17));
                ArcCGBat2.WriteLine("00000000");
                MsgBarra.PerformStep();
            }
            ArcCGBat2.Close();
            ArcCGBat2.Dispose();

            MsgBarra.Close();
            MsgBarra.Dispose();
        }

        // VB line 26136
        public void despliegarVentanaSipla(OdbcConnection myconnect, string codigoter, string linea, string numero, ref bool accion, ref string observaciones)
        {
            // frmsiplainusuales frmSiplaInusual = new frmsiplainusuales(myconnect); // ERROR: CS0246
            // frmSiplaInusual.Codigoter = codigoter; // ERROR: CS0103
            // frmSiplaInusual.linea = linea; // ERROR: CS0103
            // frmSiplaInusual.numero = numero; // ERROR: CS0103
            // frmSiplaInusual.ShowDialog(); // ERROR: CS0103
            // accion = frmSiplaInusual.ok; // ERROR: CS0103
            // observaciones = frmSiplaInusual.obervaciones; // ERROR: CS0103
        }

        // VB line 26525
        public void exporta_Auxilio_por_Asociado(string where)
        {
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("SELECT paraux.codigo,paraux.nombre as nomlinea,paraux.comite,parcomite.nombre AS NomComite,");
            StBuilder.Append("solaux.codigoter,maenit.APELLIDO,maenit.nombre,maenit.TELEFONO1,maenit.MOVIL,solaux.fecha_sol,solaux.vlr_solicitado,solaux.fecha_aprobado,");
            StBuilder.Append("solaux.vlr_aprobado,solaux.estado,solaux.aprobado,Cerrado,solaux.IdBenef,benef.nombre AS NomBeneficiario,");
            StBuilder.Append("nit.nombre AS NomBenefCnt,parent.nombre as Parentesco,solaux.Idsolaux,emp.codigo_empresa,emp.nombre_resum,cencos.CCOSTO,cencos.NOMBRE as cenconom,");
            StBuilder.Append("ciu57.CIUDAD,ciu57.NOMBRE_CIUDAD,solaux.observaciones_aprobado ");
            StBuilder.Append("FROM  cop_solaux solaux  ");
            StBuilder.Append("INNER JOIN cop_auxilio paraux ON paraux.codigo=solaux.linea ");
            StBuilder.Append("INNER JOIN sys_maenit maenit ON solaux.codigoter=maenit.CODIGOTER ");
            StBuilder.Append("inner join cop_comite parcomite on paraux.comite=parcomite.codigo ");
            StBuilder.Append("left join cop_benef benef on benef.cedula = solaux.idbenef and solaux.codigoter=benef.codigoter ");
            StBuilder.Append("left join cnt_nit nit on nit.nit = solaux.idbenef ");
            StBuilder.Append("inner join cop_empresa13 emp on maenit.EMPRESA = emp.codigo_empresa ");
            StBuilder.Append("inner join sys_cencos cencos on maenit.CENCOSTO = cencos.CCOSTO ");
            StBuilder.Append("inner join sys_ciudad57 ciu57 on maenit.DPTO_CIUDAD = ciu57.CIUDAD ");
            StBuilder.Append("left join sys_parent51 parent on benef.CodParentesco = parent.codigo ");
            StBuilder.Append(where);

            Clipboard.SetDataObject(StBuilder.ToString());
            // msgconfig.ArmaExcel("", "", "", "", "", StBuilder.ToString()); // ERROR: CS1061
        }
    }
}
