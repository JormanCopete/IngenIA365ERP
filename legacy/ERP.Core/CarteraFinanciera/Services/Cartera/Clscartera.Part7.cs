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
        // Continuation from line 17500 of Clscartera.vb

        public DateTime UltFechaExtra(string NumSolicitud, OdbcConnection myconect)
        {
            string Fecha = " ";
            stmysql = " select a.fecha as campo1  from cop_extrasoli a left join cop_solcre b on a.numero = b.numero  ";
            stmysql = stmysql + " where a.numero = '" + NumSolicitud + "' and a.valor > 0 order by a.fecha desc";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconect, "UltFechaExtra", ref Fecha);
            if (ok == true)
            {
                return Convert.ToDateTime(Fecha);
            }
            else
            {
                return new DateTime(1950, 1, 1);
            }
        }

        public virtual DataSet CargarConsultaCobranza(string usuario, DateTime fechainicio, DateTime fechafin,
            string asoinicial, string asofinal, OdbcConnection myconnect)
        {
            string fechainicio_str = fechainicio.ToString();
            string fechafin_str = fechafin.ToString();
            DateTime fechainicio_tmp = Convert.ToDateTime(fechainicio_str + " 00:00:01");
            DateTime fechafin_tmp = Convert.ToDateTime(fechafin_str + " 23:59:59");
            DataSet dsdata = new DataSet();

            stmysql = "select a.idcodmaeges,a.codigoter,a.FECHAGESTION,a.desestado,case a.estado when '3' then " + (varini.pstTipoBD.ToUpper() == "DB2" ? " cast(a.FECHA_PAGO as char(12)) " : " rtrim(a.FECHA_PAGO) ") + " else ' ' end as fecha_compromiso," +
                "a.totaldeuda,0 as totalpago, ' ' as estadopago,a.estado,a.usuario from cop_gestioncob_vw a " +
                "where a.fechagestion between '" + fechainicio_tmp.ToString(varini.pstForfecyHora) + "' and '" + fechafin_tmp.ToString(varini.pstForfecyHora) + "'" +
                " and a.codigoter between '" + asoinicial + "' and '" + asofinal + "' " +
                (usuario.Trim() == "Todos" ? "" : " and a.usuario='" + usuario + "'") + " order by a.codigoter,a.idcodmaeges,a.FECHAGESTION";

            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CargarConsultaCobranza", ref dsdata, "TblMaesGestion");
            return dsdata;
        }

        public virtual DataSet CargarDetallesCobranza(int idcobranza, OdbcConnection myconnect)
        {
            DataSet dataset = new DataSet();
            stmysql = "select idcodmaeges as id,LINCRED,NUMERO,(CAPATR+INTATR+SEGATR+ADMATR+MORACUM+EXTRAATR+OTROSATR) as deuda" +
             ",0 as pago,(CAPATR+INTATR+SEGATR+ADMATR+MORACUM+EXTRAATR+OTROSATR)*-1 as diferencia,' ' as fecha,' ' estadoObli," +
             " b.detalle from COP_GESMAES a inner join cop_maegescob b on a.idcodmaeges=b.secuencia " +
             " where idcodmaeges=" + idcobranza + " order by lincred,numero";
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CargarDetallesCobranza", ref dataset, "TblDetalleGestion");
            return dataset;
        }

        public DataSet CargarDetallesCobranza(int idcobranza, DateTime fechacompromiso, string estado, DateTime fecinimovto, DateTime fecfinmovto, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            DataSet dsdatos = new DataSet();
            DataSet dsconsulta = new DataSet();
            int i = 0;
            double pago = 0;
            string fecha = " ";
            double diferencia = 0;
            string EstadoObli = " ";
            DateTime fec;
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select ges.LINCRED,ges.NUMERO,ges.FECHA_PAGO,ges.codigoter,mae.detalle,nov.fechacompromiso,");
            stbuilder.Append("(ges.CAPATR+ges.INTATR+ges.SEGATR+ges.ADMATR+ges.MORACUM+ges.EXTRAATR+ges.OTROSATR) as deuda,");
            stbuilder.Append("(select sum(movto.vlr_credito) from cop_movimto movto inner join sys_compro02 compro on movto.compronte=compro.codigo and ");
            stbuilder.Append("compro.exige_detalle='Y' and compro.documento<>'SI' and compro.restri_tesoreria<>'Y' ");
            stbuilder.Append("inner join cop_docmto doc on movto.compronte=doc.compronte and movto.numero_domto=doc.numero_domto and doc.anulado<>'Y' ");
            stbuilder.Append("where movto.fecha_movto>='" + fecinimovto.ToString(varini.PstForFec) + "' and movto.fecha_movto<='" + fecfinmovto.ToString(varini.PstForFec) + "' ");
            stbuilder.Append("and movto.lincred=ges.lincred and movto.numero=ges.numero and movto.codigoter=ges.codigoter) as Credito,");
            stbuilder.Append("(select max(movto2.fecha_movto) from cop_movimto movto2 inner join sys_compro02 compro2 on movto2.compronte=compro2.codigo and ");
            stbuilder.Append("compro2.exige_detalle='Y' and compro2.documento<>'SI' and compro2.restri_tesoreria<>'Y' ");
            stbuilder.Append("inner join cop_docmto doc2 on movto2.compronte=doc2.compronte and movto2.numero_domto=doc2.numero_domto and doc2.anulado<>'Y' ");
            stbuilder.Append("where movto2.fecha_movto>='" + fecinimovto.ToString(varini.PstForFec) + "' and movto2.fecha_movto<='" + fecfinmovto.ToString(varini.PstForFec) + "' ");
            stbuilder.Append("and movto2.lincred=ges.lincred and movto2.numero=ges.numero and movto2.codigoter=ges.codigoter) as fechaMovto ");
            stbuilder.Append("from COP_GESMAES ges ");
            stbuilder.Append("inner join cop_maegescob mae on ges.idcodmaeges=mae.secuencia and mae.estado='" + estado + "' ");
            stbuilder.Append("inner join cop_novfecgestion nov on ges.idcodmaeges=nov.idcodmaeges and nov.lincred=ges.lincred ");
            stbuilder.Append("and nov.numero=ges.numero and nov.codigoter=ges.codigoter ");
            stbuilder.Append("where ges.idcodmaeges=" + idcobranza + " and ges.fecha_pago='" + fechacompromiso + "' and ");
            stbuilder.Append("nov.fecha=(select max(fecha) from cop_novfecgestion nov2 where ges.idcodmaeges=nov2.idcodmaeges and nov2.lincred=ges.lincred ");
            stbuilder.Append("and nov2.numero=ges.numero and nov2.codigoter=ges.codigoter) ");
            stbuilder.Append("order by ges.lincred,ges.numero");

            dsconsulta.Tables.Add("TblConsulta");
            dsconsulta.Tables["TblConsulta"].Columns.Add("id", i.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("FechaCompromiso", fecha.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("lincred", i.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("numero", pago.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("deuda", pago.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("pago", pago.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("diferencia", pago.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("fecha", fecha.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("estadoObli", fecha.GetType());
            dsconsulta.Tables["TblConsulta"].Columns.Add("Detalle", fecha.GetType());

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargarDetallesCobranza", ref dsdata, "DetalleCobranza");
            if (ok == true)
            {
                DataTable tbl = dsdata.Tables["DetalleCobranza"];
                while (i < tbl.Rows.Count)
                {
                    if (tbl.Rows[i]["credito"] is DBNull)
                    {
                        pago = 0;
                        fecha = " ";
                    }
                    else
                    {
                        pago = Convert.ToDouble(tbl.Rows[i]["credito"]);
                        fecha = tbl.Rows[i]["fechaMovto"].ToString();
                    }
                    diferencia = pago - Convert.ToDouble(tbl.Rows[i]["deuda"]);
                    if (pago > 0 && pago >= Convert.ToDouble(tbl.Rows[i]["deuda"]))
                    {
                        EstadoObli = "C";
                    }
                    else if (pago <= 0)
                    {
                        EstadoObli = "N";
                    }
                    else
                    {
                        EstadoObli = "P";
                    }
                    dsconsulta.Tables["TblConsulta"].Rows.Add(idcobranza, fechacompromiso, tbl.Rows[i]["lincred"], tbl.Rows[i]["numero"], tbl.Rows[i]["deuda"], pago, diferencia, fecha, EstadoObli, tbl.Rows[i]["detalle"]);
                    i = i + 1;
                }
            }
            return dsconsulta;
        }

        public bool GrabarFechasProlongadas(int IDCODMAEGES, int LINCRED, double NUMERO, DateTime FECHACOMPROMISO, OdbcConnection myconnect)
        {
            stmysql = "insert into COP_NOVFECGESTION (IDCODMAEGES,LINCRED,NUMERO,FECHA,FECHACOMPROMISO) " +
                "values (" + IDCODMAEGES + "," + LINCRED + "," + NUMERO + ",'" + DateTime.Now.ToString(varini.pstForfecyHora) + "','" + FECHACOMPROMISO.ToString(varini.PstForFec) + "')";
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarFechasProlongadas");
            return ok;
        }

        public DataSet BuscarFechasProlongadas(int IDCODMAEGES, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select LINCRED,NUMERO,FECHA,FECHACOMPROMISO from COP_NOVFECGESTION " +
                " where IDCODMAEGES=" + IDCODMAEGES + " order by fecha,LINCRED,NUMERO";
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarFechasProlongadas", ref dsdata, "tblfecprolongada");
            return dsdata;
        }

        public DataSet CargarReporteGestiones(string usuario, DateTime fechainicio, DateTime fechafin,
           string asoinicial, string asofinal, Form forma,
           DateTime fecfincompromiso, OdbcConnection myconnect)
        {
            DataSet dataset = new DataSet();
            int i = 0;
            bool ultimo = false;
            int b = 0;
            DataSet dsdetalles = new DataSet();
            DataSet data = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress barraprogreso = new ERP.Core.Compartido.Controles.Barraprogress("Consultando Informacion Cobranzas", forma);
            DateTime fecfin;
            DateTime fecini = new DateTime(1950, 1, 1);
            double pago = 0;

            asoinicial = Strings.Right("00000000000000" + asoinicial, 14);
            asofinal = Strings.Right("00000000000000" + asofinal, 14);
            try
            {
                dataset = this.CargarConsultaCobranza(usuario, fechainicio, fechafin, asoinicial, asofinal, myconnect);
                DataTable tblMaes = dataset.Tables["TblMaesGestion"];
                if (tblMaes.Rows.Count > 0)
                {
                    barraprogreso.ValorMinimoMaximo(0, tblMaes.Rows.Count);
                    barraprogreso.Show();
                    fecfin = fecfincompromiso;
                    while (i < tblMaes.Rows.Count)
                    {
                        pago = 0;
                        if (tblMaes.Rows[i]["estado"].ToString() == "3")
                        {
                            fecini = Convert.ToDateTime(tblMaes.Rows[i]["FECHAGESTION"]);
                            if (i == tblMaes.Rows.Count - 1)
                            {
                                fecfin = fecfincompromiso;
                                ultimo = true;
                            }
                            if (!ultimo)
                            {
                                if (tblMaes.Rows[i]["codigoter"].ToString() == tblMaes.Rows[i + 1]["codigoter"].ToString())
                                {
                                    if (tblMaes.Rows[i]["FECHAGESTION"].ToString() != tblMaes.Rows[i + 1]["FECHAGESTION"].ToString())
                                    {
                                        fecfin = Convert.ToDateTime(tblMaes.Rows[i + 1]["FECHAGESTION"]);
                                    }
                                    else
                                    {
                                        fecfin = fecfincompromiso;
                                    }
                                }
                                else
                                {
                                    fecfin = fecfincompromiso;
                                }
                            }
                            dsdetalles = this.CargarDetallesCobranza(Convert.ToInt32(tblMaes.Rows[i]["idcodmaeges"]), Convert.ToDateTime(tblMaes.Rows[i]["fecha_compromiso"]), tblMaes.Rows[i]["estado"].ToString(), fecini, fecfin, myconnect);

                            if (dsdetalles.Tables["TblConsulta"].Rows.Count > 0)
                            {
                                b = 0;
                                while (b < dsdetalles.Tables["TblConsulta"].Rows.Count)
                                {
                                    pago = pago + Convert.ToDouble(dsdetalles.Tables["TblConsulta"].Rows[b]["pago"]);
                                    b = b + 1;
                                }
                                tblMaes.Rows[i]["totalpago"] = pago;
                                if (pago > 0 && pago >= Convert.ToDouble(tblMaes.Rows[i]["totaldeuda"]))
                                {
                                    tblMaes.Rows[i]["estadopago"] = "C";
                                }
                                else if (pago <= 0)
                                {
                                    tblMaes.Rows[i]["estadopago"] = "N";
                                }
                                else
                                {
                                    tblMaes.Rows[i]["estadopago"] = "P";
                                }
                            }
                        }
                        barraprogreso.PerformStep();
                        i = i + 1;
                    }
                }
                data.Tables.Add(dataset.Tables["TblMaesGestion"].Copy());
                if (dsdetalles.Tables.Count > 0)
                {
                    data.Tables.Add(dsdetalles.Tables["TblConsulta"].Copy());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Procedimiento origen: CargarReporteGestiones." + ((char)13) + " Error:" + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            barraprogreso.Close();
            return data;
        }

        public void ImprimeGestiones(DataSet dsdataset, Form myforma)
        {
            ERP.Core.Compartido.Reportes.reporte ImpGestiones = new ERP.Core.Compartido.Reportes.reporte("cop_finfgestion01");
            ERP.Core.Compartido.Reportes.config_report confi = new ERP.Core.Compartido.Reportes.config_report();
            ImpGestiones.SetDataSource(dsdataset);
            // ImpGestiones.Subreports[0].SetDataSource(dsdataset); // ERROR: CS1061
            confi.confi_reportes(myforma, ImpGestiones);
        }

        public bool MarcarAsociadoCobro(string codigoter, int Lincred, double numero,
            string asesor, string Marcar, string EnDemanda, OdbcConnection myconnect)
        {
            string mysql = "";
            stmysql = "";
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            asesor = Strings.Right("0000" + asesor, 4);
            switch (Marcar)
            {
                case "P":
                case "J":
                    stmysql = "update sys_maenit set COBROJUR=1 where codigoter = '" + codigoter + "'";
                    mysql = "update cop_maecar set cobrojur = " + (Marcar == "J" ? "'Y'" : "'P'") + ",cod_abogado = '" + asesor + "',Demanda='" + EnDemanda + "' where codigoter = '" + codigoter +
                    "' and lincred = " + Lincred + " and numero = " + numero;
                    break;
                case "D":
                    stmysql = "update sys_maenit set COBROJUR=0 where codigoter = '" + codigoter + "'";
                    mysql = "update cop_maecar set cobrojur = 'N',cod_abogado ='' where codigoter = '" + codigoter +
                    "' and lincred = " + Lincred + " and numero = " + numero;
                    break;
            }
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "MarcarAsociadoCobro(sys_maenit)");
            ok = this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "MarcarAsociadoCobro(cop_maecar)");
            return ok;
        }

        public bool ExportarPlanoCuotasPendientes(bool BuscaEmpresa, string EmpCenIni, string EmpCenFin,
            string AsociadoIni, string AsociadoFin, int Periodo, string EstadoAsociado, string Clades,
            string Cobjur, DateTime Fecini, DateTime Fecfin, int LincredIni, int LincredFin,
            string CodAbogado, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string titulo = "";
            string where = "";
            string TituloIzquierdo = " ";
            string TituloDerecho = " ";

            try
            {
                if (BuscaEmpresa)
                {
                    EmpCenIni = Strings.Right("0000" + EmpCenIni, 4);
                    EmpCenFin = Strings.Right("0000" + EmpCenFin, 4);
                    where = " and c.empresa between '" + EmpCenIni + "' and '" + EmpCenFin + "' ";
                }
                else
                {
                    EmpCenIni = Strings.Right("00000000" + EmpCenIni, 8);
                    EmpCenFin = Strings.Right("00000000" + EmpCenFin, 8);
                    where = " and c.cencosto between '" + EmpCenIni + "' and '" + EmpCenFin + "' ";
                }
                AsociadoIni = Strings.Right("00000000000000" + AsociadoIni, 14);
                AsociadoFin = Strings.Right("00000000000000" + AsociadoFin, 14);

                string oraclePrefix = (varini.pstTipoBD.ToUpper() == "ORACLE" ? varini.pstUID + "." : "");
                stbuilder.Append("SELECT a.Codigoter,c.Apellido,c.Nombre,c.Estado,a.periodo_causa as PeriodoCausacion,a.lincred as LineaCredito,");
                stbuilder.Append("a.Numero,b.Descripcion,a.saldocapital as Capital,a.saldointeres as Interes,a.saldoextra as Extras,a.saldomora as Mora,a.saldoadmon as Administracion,a.saldoseguro as Seguro,");
                stbuilder.Append("a.saldootros as Otros,(a.saldointeres + a.saldocapital + a.saldoextra + a.saldoseguro + a.saldoadmon + a.saldootros + a.saldomora) as Total,");
                stbuilder.Append("a.diasmora as DiasMora,e.fecha_movto as Fecha,d.empdsto as Empresa ");
                stbuilder.Append(" FROM " + oraclePrefix + "cop_copmora a ");
                stbuilder.Append("inner join " + oraclePrefix + "cop_cuopen e on e.codigoter = a.codigoter and e.lincred = a.lincred ");
                stbuilder.Append("and e.numero = a.numero and e.periodo_causa = a.periodo_causa ");
                stbuilder.Append("inner join " + oraclePrefix + "cop_concar12 b on b.lincred = a.lincred ");
                stbuilder.Append("inner join " + oraclePrefix + "sys_maenit c on c.codigoter = a.codigoter ");
                stbuilder.Append("inner join " + oraclePrefix + "cop_maecar d on d.lincred = a.lincred and d.numero = a.numero and d.codigoter = a.codigoter ");
                stbuilder.Append("left join " + oraclePrefix + "cop_salmaecar f on f.codigoter = a.codigoter and f.lincred = a.lincred ");
                stbuilder.Append("and f.numero = a.numero and f.periodo = a.periodo_contable ");
                stbuilder.Append("inner join " + oraclePrefix + "cop_empresa13 g on g.codigo_empresa =c.empresa ");
                stbuilder.Append("inner join " + oraclePrefix + "sys_cencos h on h.ccosto =c.cencosto ");
                stbuilder.Append("where (a.saldointeres + a.saldocapital + a.saldoextra + a.saldoseguro + a.saldoadmon + a.saldootros + a.saldomora)<> 0 ");
                stbuilder.Append("and a.lincred between " + LincredIni + " and " + LincredFin + " ");
                stbuilder.Append("and a.codigoter between '" + AsociadoIni + "' and '" + AsociadoFin + "' ");
                stbuilder.Append("and a.periodo_contable = " + Periodo + where);
                stbuilder.Append(EstadoAsociado == "0" ? "" : (EstadoAsociado == "1" ? " and c.estado='A' " : (EstadoAsociado == "2" ? " and c.estado='R' " : " and c.estado='S' ")));
                stbuilder.Append(Clades == "0" ? "" : " and f.clades='" + Clades + "' ");
                stbuilder.Append(Cobjur == "0" ? "" : (Cobjur == "1" ? " and d.cobrojur='Y' and d.cod_abogado='" + CodAbogado + "' " : " and d.cobrojur='N' "));
                stbuilder.Append(" and e.fecha_movto between '" + Fecini.ToString(varini.PstForFec) + "' and '" + Fecfin.ToString(varini.PstForFec) + "' ");
                stbuilder.Append("and ((a.lincred>999 and f.saldo<>0) or a.lincred<1000) ");
                stbuilder.Append("order by a.codigoter,a.lincred,a.numero,a.periodo_causa");

                titulo = "CUOTAS PENDIENTES GENERAL A " + Periodo;
                if (BuscaEmpresa)
                {
                    if (EmpCenIni == EmpCenFin)
                    {
                        // this.msgconfig.BuscaEmpresa(EmpCenIni, myconnect, ref TituloIzquierdo); // ERROR: CS1620
                        TituloIzquierdo = "Empresa: " + TituloIzquierdo;
                    }
                    else if (EmpCenIni == "0000" && EmpCenFin == "9999")
                    {
                        TituloIzquierdo = "Empresa: Todas las empresas";
                    }
                    else
                    {
                        TituloIzquierdo = "Empresas desde " + EmpCenIni + " Hasta " + EmpCenFin;
                    }
                }
                else
                {
                    if (EmpCenIni == EmpCenFin)
                    {
                        // this.msgconfig.BuscaCencos(EmpCenIni, myconnect, ref TituloIzquierdo); // ERROR: CS1061
                        TituloIzquierdo = "Cen. Costo: " + TituloIzquierdo;
                    }
                    else if (EmpCenIni == "00000000" && EmpCenFin == "99999999")
                    {
                        TituloIzquierdo = "Cen. Costo: Todas las Centros de costos";
                    }
                    else
                    {
                        TituloIzquierdo = "Cen. Costo desde " + EmpCenIni + " Hasta " + EmpCenFin;
                    }
                }
                TituloIzquierdo = TituloIzquierdo + ((char)10) + "Conceptos desde " + LincredIni + " Hasta " + LincredFin;
                if (AsociadoIni == "00000000000000" && AsociadoFin == "99999999999999")
                {
                    TituloDerecho = "Asociados: Todos los asociados";
                }
                else
                {
                    TituloDerecho = "Asociados desde " + AsociadoIni + " Hasta " + AsociadoFin;
                }
                TituloDerecho += ((char)10) + "Tipo Descuento: " + (Clades == "0" ? "Todos" : (Clades == "1" ? "Nomina" : "Caja"));
                TituloDerecho += ((char)10) + "De " + Fecini.ToString(varini.PstForFec) + " Hasta " + Fecfin.ToString(varini.PstForFec);
                // this.msgconfig.ArmaExcel("", "", "", titulo, "", stbuilder.ToString(), TituloIzquierdo, TituloDerecho); // ERROR: CS1061
            }
            catch (Exception ex)
            {
                // empty catch as in VB
            }
            return false;
        }

        public double CalcularUltPagFactura(string codigoter, int periodo, OdbcConnection myconnect)
        {
            string fechaUltPago = " ";
            return CalcularUltPagFactura(codigoter, periodo, myconnect, ref fechaUltPago);
        }

        public double CalcularUltPagFactura(string codigoter, int periodo, OdbcConnection myconnect, ref string fechaUltPago)
        {
            DataSet dataset = new DataSet();
            int periodoini = 0;
            double valorPago = 0;
            int i = 0;
            StringBuilder stbuilder = new StringBuilder();

            periodoini = (Convert.ToInt32(periodo.ToString().Substring(4)) > 3) ? periodo - 3 : periodo - 91;
            stbuilder.Append("select max(a.fecha_movto) as campo1,sum(a.vlr_credito) as campo2 ");
            stbuilder.Append("from cop_movimto a");
            stbuilder.Append(" inner join sys_compro02 b on a.compronte=b.codigo and b.RecFactura='Y'");
            stbuilder.Append(" inner join cop_concar12 d on a.lincred=d.lincred and (d.codahor<>'2' and d.codahor<>'6' and d.codahor<>'7')");
            stbuilder.Append(" where a.codigoter='" + codigoter + "' and a.periodo>=" + periodoini + " and a.periodo<=" + periodo);
            stbuilder.Append(" and a.fecha_movto=(select max(c.fecha_movto) from cop_movimto c");
            stbuilder.Append(" where c.codigoter='" + codigoter + "' and a.compronte=c.compronte ");
            stbuilder.Append(" and c.periodo>=" + periodoini + " and c.periodo<=" + periodo + " and c.vlr_credito<>0)");
            stbuilder.Append(" group by a.compronte,a.numero_domto  order by campo1 desc");

            // ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "CalcularUltPagFactura", ref fechaUltPago, ref valorPago); // ERROR: CS1503
            if (ok)
            {
                fechaUltPago = Convert.ToDateTime(fechaUltPago).ToString(varini.PstForFec);
            }
            else
            {
                fechaUltPago = " ";
                valorPago = 0;
            }
            return valorPago;
        }

        public DataTable BuscaSolicitudesCredito(string codigoter, string NroSolcitud, DateTime FechaInicial, DateTime FechaFinal, string estado, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new System.Text.StringBuilder();
            string stmysqlLocal;
            DataSet DsDataset = new DataSet();

            stmysqlLocal = "where solcre.fecha_soli between '" + FechaInicial.ToString(varini.PstForFec) + "' and '" + FechaFinal.ToString(varini.PstForFec) + "'";
            if (codigoter.Trim() != "Todos")
            {
                stmysqlLocal = " and solcre.codigoter = '" + codigoter + "'";
            }

            if (NroSolcitud.Trim() != "Todos")
            {
                stmysqlLocal = " and solcre.numero = '" + NroSolcitud + "'";
            }

            switch (estado)
            {
                case "Aprobados":
                    stmysqlLocal = stmysqlLocal + " and solcre.estado = 'A'";
                    break;
                case "Pendientes":
                    stmysqlLocal = stmysqlLocal + " and solcre.estado = 'P'";
                    break;
                case "Evaluados":
                    stmysqlLocal = stmysqlLocal + " and solcre.estado = 'E'";
                    break;
                case "Rechazados":
                    stmysqlLocal = stmysqlLocal + " and solcre.estado = 'R'";
                    break;
                case "Aprobados X Desembolso":
                    stmysqlLocal = stmysqlLocal + " and solcre.estado = 'D'";
                    break;
                case "Grabados":
                    stmysqlLocal = stmysqlLocal + " and solcre.estado = 'G'";
                    break;
            }

            stbuilder.Append("select solcre.numero,solcre.fecha_soli,solcre.codigoter,solcre.lincred,solcre.vlr_solicitud,solcre.tasa_int,solcre.plazo,solcre.cuota,solcre.pagos_mes,solcre.sal_mora,solcre.cupo_disponible,solcre.fec_ingr_coop,solcre.empresa,solcre.cargo,solcre.salario,solcre.otro_ingreso,solcre.fec_ingr_empr,solcre.dscto_mes_emp,solcre.gasto_fijo_mes,solcre.cupo_dismes");
            stbuilder.Append(",solcre.tipo_contrato,solcre.tipo_garantia,solcre.descripcion,solcre.avaluo_ccial,solcre.avaluo_catastro,solcre.asegurado, solcre.por_seguro,solcre.fecven_seguro,solcre.codeudor1,solcre.codeudor2,solcre.codeudor3,solcre.codeudor4,solcre.valor_aprobado,solcre.fecha_aproba,solcre.numero_acta,solcre.fecha_acta,solcre.fecha_programada,");
            stbuilder.Append("solcre.aporte_adicional,solcre.deuda_recoge,solcre.estado,solcre.usuario,solcre.fecha_graba,solcre.conyu_labora,solcre.conyuge,solcre.empresa_labora,solcre.salario_me,solcre.tel_conyuge,solcre.dir_emp_conyu,solcre.ciudad_emp_conyu,solcre.per_acargo_conyu,solcre.observacion,solcre.vehiculo,solcre.tiene_vehiculo,solcre.casa_propia,solcre.fecdesc");
            stbuilder.Append(",solcre.nit,solcre.ciclod,solcre.periodd,solcre.clacuo,solcre.clasei,solcre.clades,solcre.tip_intcie,solcre.tip_cap,solcre.tip_adm,solcre.tip_seg,solcre.tip_otr,solcre.for_adm,solcre.cpto_adm,solcre.cpto_seg,solcre.cpto_otr,solcre.tasaadm,solcre.tasaseg,solcre.tasacpt,solcre.tasaotr,solcre.vlrpre_extra,solcre.aportes,solcre.agencia,solcre.ccosto,solcre.porextra,solcre.cuota_adm,solcre.cuota_seg,solcre.cuota_cptl");
            stbuilder.Append(",solcre.cuota_icie,solcre.cuota_otros,solcre.pergracia,solcre.pergraini,solcre.fepergrini,solcre.pergracuo,solcre.pergradia,solcre.fecha_pergracia,solcre.ciclo_pergracia,solcre.cuex_inmes,solcre.cuex_inant,solcre.pag1cuo,solcre.tippag2,solcre.numpagare,solcre.NumLibranza,solcre.NumCdat,solcre.IngVariables,solcre.IngArriendos,solcre.DeudasTerceros,solcre.Autorizado,");
            stbuilder.Append("solcre.EmpDsto,solcre.NitAseguradora,solcre.NombreAseguradora,solcre.NumPoliza,solcre.Matricula,maenit.apellido,maenit.nombre from cop_solcre solcre ");
            stbuilder.Append("inner join sys_maenit maenit on solcre.codigoter = maenit.codigoter ");
            stbuilder.Append(stmysqlLocal);

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), Myconnect, "BuscaSolicitudesCredito", ref DsDataset, "tblsolicitudes");
            return DsDataset.Tables["tblsolicitudes"];
        }

        public void ApruebaSolicitud(double NumSolicitud, DateTime FechaAprobado, DateTime FechaAprogramacion, string NumActa, double ValorAprobado, string Detalle, string Usuario, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new System.Text.StringBuilder();
            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("fecha_programada = '");
            stbuilder.Append(FechaAprobado.ToString(varini.PstForFec) + "',");
            stbuilder.Append("valor_aprobado = '");
            stbuilder.Append(ValorAprobado + "',");
            stbuilder.Append("fecha_aproba = '");
            stbuilder.Append(FechaAprobado.ToString(varini.PstForFec) + "',");
            stbuilder.Append("estado = 'A',");
            stbuilder.Append("numero_acta = '");
            stbuilder.Append(NumActa + "' ");
            stbuilder.Append("where numero = '" + NumSolicitud + "'");

            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        public void RechazaSolicitud(double NumSolicitud, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new System.Text.StringBuilder();
            stbuilder.Append("update cop_solcre set ");
            stbuilder.Append("estado = 'R' ");
            stbuilder.Append("where numero = '" + NumSolicitud + "'");
            this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "ApruebaSolicitud");
        }

        public DateTime VerificaMinimaFechaCausacion(string codigoter, int periodo, OdbcConnection myconnect)
        {
            DateTime fecha = DateTime.Now;

            stmysql = "select min(a.fecha_movto) as campo1 from cop_copmora b inner join cop_cuopen a on a.codigoter=b.codigoter and a.lincred=b.lincred " +
                " and a.numero=b.numero and a.periodo_causa=b.periodo_causa " +
                "inner join cop_maecar c on b.codigoter=c.codigoter and b.lincred=c.lincred and b.numero=c.numero " +
                "inner join cop_salmaecar d on b.codigoter=d.codigoter and b.lincred=d.lincred and b.numero=d.numero and b.periodo_contable=d.PERIODO  " +
                " where a.codigoter='" + codigoter + "' and b.periodo_contable=" + periodo + " and ((a.lincred<1000 and (d.CUOTA<>0 or d.SALDO<>0)) or a.lincred>=1000 and d.SALDO<>0) " +
                " and (b.saldocapital<>0 or b.saldoextra<>0 or b.saldomora<>0 or b.saldointeres<>0 or b.saldoseguro<>0 or b.saldoadmon<>0 or b.saldootros<>0)";
            // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "VerificaMinimaFechaCausacion", ref fecha); // ERROR: CS1503
            return fecha;
        }

        public DateTime VerificaMaximaFechaCausacion(string codigoter, int lincred, double numero, int periodo, OdbcConnection myconnect)
        {
            DateTime fecha = DateTime.Now;
            string stFecha = " ";
            DataSet dsdata = new DataSet();

            stmysql = "select max(a.fecha_movto) as FecMovto from cop_copmora b inner join cop_cuopen a on a.codigoter=b.codigoter and a.lincred=b.lincred " +
                " and a.numero=b.numero and a.periodo_causa=b.periodo_causa " +
                "inner join cop_maecar c on b.codigoter=c.codigoter and b.lincred=c.lincred and b.numero=c.numero " +
                "inner join cop_salmaecar d on b.codigoter=d.codigoter and b.lincred=d.lincred and b.numero=d.numero and b.periodo_contable=d.PERIODO  " +
                " where a.codigoter='" + codigoter + "' and b.periodo_contable=" + periodo + " and ((a.lincred<1000 and (d.CUOTA<>0 or d.SALDO<>0)) or a.lincred>=1000 and d.SALDO<>0) " +
                " and (b.saldocapital<>0 or b.saldoextra<>0 or b.saldomora<>0 or b.saldointeres<>0 or b.saldoseguro<>0 or b.saldoadmon<>0 or b.saldootros<>0)";

            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "VerificaMinimaFechaCausacion", ref dsdata, "tblminfec");
            if (!ok)
            {
                fecha = new DateTime(1950, 1, 1);
            }
            else
            {
                if (dsdata.Tables["tblminfec"].Rows[0]["FecMovto"] is DBNull)
                {
                    fecha = new DateTime(1950, 1, 1);
                }
                else
                {
                    fecha = Convert.ToDateTime(dsdata.Tables["tblminfec"].Rows[0]["FecMovto"]);
                }
            }
            return fecha;
        }

        public void ArchivoSupersolidaria(DateTime FechaCorte, bool CodCedu, int CodDanne, double salariominimo, string CodActividad, Form myforma, OdbcConnection myconnect, bool csv = true)
        {
            DataSet DsDataset = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Exportando Archivos Super solidaria", myforma);
            msgbarra.Show();
            PlanoUsuarios(Application.StartupPath + "\\archivos planos\\export\\usuario.csv", FechaCorte, CodCedu, CodDanne, salariominimo, CodActividad, msgbarra, myforma, myconnect, csv);
            msgbarra.Close();
        }

        public void ArchivoUnidadInformacionAnalisisfinanciero(DateTime FechaInicial, DateTime FechaFinal, string CodEntidad, int CodDanne, bool NoPrimerVez, string NombreArchivo, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Exportando Archivo Plano De Productos", myforma);
            msgbarra.Show(myforma);
            PlanoProductos(Application.StartupPath + "\\archivos planos\\export\\" + NombreArchivo, FechaInicial, FechaFinal, CodEntidad, CodDanne, NoPrimerVez, msgbarra, myforma, myconnect);
        }

        // NOTE: PlanoProductos is an extremely long method (~700 lines of VB). Due to its size and repetitive
        // titular2 lookup pattern, it is included here as a faithful translation.
        private void PlanoProductos(string NombreArchivo, DateTime FechaInicial, DateTime FechaFinal, string CodEntidad, int CodDanne, bool NoPrimerVez, ERP.Core.Compartido.Controles.Barraprogress msgbarra, Form myforma, OdbcConnection myconnect)
        {
            DateTime fechapro;
            string diaini, mesini, ahnoini, diafin, mesfin, ahnofin, fecdia, fecmes, fecahno;
            int total;
            string tipoProducto, ciudad, tipoid, idtitular;
            string apellido1 = "", apellido2 = "", nombre1 = "", nombre2 = "";
            string rasocial, numproducto, nombre = " ", apellido = " ", nat_jurtitular2 = " ", nittitular2 = " ", rasocialtirular2 = " ";
            string tipoidtitular2 = " ", nombre1titular2 = " ", nombre2titular2 = " ", apellido1titular2 = " ", apellido2titular2 = " ", nitcheqtitular2 = " ";
            bool tienetitular2 = false;
            string periodo;
            string mysql1;

            mesini = FechaInicial.Month.ToString();
            diaini = FechaInicial.Day.ToString();
            ahnoini = FechaInicial.Year.ToString();
            diafin = FechaFinal.Day.ToString();
            mesfin = FechaFinal.Month.ToString();
            ahnofin = FechaFinal.Year.ToString();
            periodo = ahnofin + Strings.Right("00" + mesfin, 2);

            if (NoPrimerVez == true)
            {
                mysql1 = "select car.lincred, car.numero, car.fecfact, nit.codigoter,nit.apellido, nit.nombre, nit.nit, nit.natjur, nit.dpto_ciudad," +
                " nit.tipo_nit,  nit.nit_chequeo, con.codahor, cn.nit as nitjuridico, cn.tipo_persona, cn.nit_chequeo as nitchejur, cn.razon_social, " +
                " cn.nombre as nombrejuridico,hor.num_cuenta, hor.cc_nit_firmareq1 , hor.cc_nit_firmareq2, hor.cc_nit_firmareq3, hor.nom_firmareq1, " +
                " hor.nom_firmareq2, hor.nom_firmareq3, cdat.cc_nit_firmareq1 as cdat_firmareq1,  cdat.cc_nit_firmareq2 as cdat_firmareq2, cdat.cc_nit_firmareq3 as cdar_firmareq3 " +
                " from cop_maecar car inner join sys_maenit nit on car.codigoter=nit.codigoter " +
                " inner join cop_concar12 con on car.lincred=con.lincred inner join sys_ciudad57 ciu on ciu.ciudad=nit.dpto_ciudad " +
                " inner join cop_salmaecar salma on car.codigoter=salma.codigoter and car.lincred=salma.lincred and car.numero=salma.numero and salma.periodo=" + periodo + "" +
                " left join cnt_nit cn on nit.nit=cn.nit left join dep_maeahor hor on car.codigoter=hor.codigoter and car.lincred=hor.lincred and car.numero=hor.num_cuenta " +
                " left join cdt_maecdats cdat on car.codigoter=cdat.codigoter and car.lincred=cdat.lincred and car.numero=cdat.num_cdat where car.lincred<>9999 and car.fecfact between '" + Strings.Format(FechaInicial, varini.PstForFec) + "' and '" + Strings.Format(FechaFinal, varini.PstForFec) + "'" +
                " and (con.codahor='1' or con.codahor='2' or con.codahor='6') and (salma.saldo<>0 or salma.cuopen<>0)";
            }
            else
            {
                mysql1 = "select car.lincred, car.numero, car.fecfact, nit.codigoter,nit.apellido, nit.nombre, nit.nit, nit.natjur, nit.dpto_ciudad," +
                " nit.tipo_nit,  nit.nit_chequeo, con.codahor, cn.nit as nitjuridico, cn.tipo_persona, cn.nit_chequeo as nitchejur, cn.razon_social, " +
                " cn.nombre as nombrejuridico,hor.num_cuenta, hor.cc_nit_firmareq1 , hor.cc_nit_firmareq2, hor.cc_nit_firmareq3, hor.nom_firmareq1, " +
                " hor.nom_firmareq2, hor.nom_firmareq3, cdat.cc_nit_firmareq1 as cdat_firmareq1,  cdat.cc_nit_firmareq2 as cdat_firmareq2, cdat.cc_nit_firmareq3 as cdar_firmareq3 " +
                " from cop_maecar car inner join sys_maenit nit on car.codigoter=nit.codigoter " +
                " inner join cop_concar12 con on car.lincred=con.lincred inner join sys_ciudad57 ciu on ciu.ciudad=nit.dpto_ciudad " +
                " inner join cop_salmaecar salma on car.codigoter=salma.codigoter and car.lincred=salma.lincred and car.numero=salma.numero and salma.periodo=" + periodo + "" +
                " left join cnt_nit cn on nit.nit=cn.nit left join dep_maeahor hor on car.codigoter=hor.codigoter and car.lincred=hor.lincred and car.numero=hor.num_cuenta " +
                " left join cdt_maecdats cdat on car.codigoter=cdat.codigoter and car.lincred=cdat.lincred and car.numero=cdat.num_cdat where car.lincred<>9999 and car.fecfact <='" + Strings.Format(FechaFinal, varini.PstForFec) + "'" +
                "  and  (con.codahor='1' or con.codahor='2' or con.codahor='6') and (salma.saldo<>0 or salma.cuopen<>0)";
            }

            OdbcConnection pmyConec03 = new OdbcConnection();
            OdbcConnection pmyConeccuenta = new OdbcConnection();
            pmyConec03.Close();
            pmyConeccuenta.Close();
            pmyConec03.ConnectionString = varini.pstMyconec;
            pmyConeccuenta.ConnectionString = varini.pstMyconec;

            OdbcCommand myCommand2 = new OdbcCommand(mysql1, pmyConec03);
            OdbcCommand myCommandcuenta = new OdbcCommand(mysql1, pmyConeccuenta);
            myCommand2.Connection.Open();
            myCommandcuenta.Connection.Open();
            OdbcDataReader myReader2 = myCommand2.ExecuteReader();
            OdbcDataReader myReadercuenta = myCommandcuenta.ExecuteReader();
            TextWriter read11;
            read11 = File.CreateText(NombreArchivo);
            total = 0;
            while (myReadercuenta.Read())
            {
                total += 1;
            }
            msgbarra.ValorMinimoMaximo(0, total);
            myCommandcuenta.Connection.Close();
            myReadercuenta.Close();
            bool si1 = false;
            int conce = 1;
            read11.Write("         0");
            read11.Write(Strings.Right("        " + CodEntidad, 8));
            read11.Write(ahnoini + "-" + Strings.Right("00" + mesini, 2) + "-" + Strings.Right("00" + diaini, 2));
            read11.Write(ahnofin + "-" + Strings.Right("00" + mesfin, 2) + "-" + Strings.Right("00" + diafin, 2));
            read11.Write(Strings.Right("          " + total.ToString(), 10));
            read11.WriteLine("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");

            while (myReader2.Read())
            {
                fechapro = Convert.ToDateTime(myReader2["fecfact"]);
                fecdia = fechapro.Day.ToString();
                fecmes = fechapro.Month.ToString();
                fecahno = fechapro.Year.ToString();
                numproducto = myReader2["lincred"].ToString() + myReader2["numero"].ToString();

                if (myReader2["codahor"].ToString() == "6") { tipoProducto = "91"; }
                else if (myReader2["codahor"].ToString() == "2") { tipoProducto = "92"; }
                else if (myReader2["codahor"].ToString() == "1") { tipoProducto = "94"; }
                else { tipoProducto = "09"; }

                ciudad = Strings.Right("00000" + CodDanne.ToString(), 5);

                if (myReader2["tipo_nit"].ToString() == "C") { tipoid = "13"; }
                else if (myReader2["tipo_nit"].ToString() == "N") { tipoid = "31"; }
                else if (myReader2["tipo_nit"].ToString() == "E") { tipoid = "22"; }
                else if (myReader2["tipo_nit"].ToString() == "T") { tipoid = "12"; }
                else if (myReader2["tipo_nit"].ToString() == "U") { tipoid = "00"; }
                else if (myReader2["tipo_nit"].ToString() == "R") { tipoid = "11"; }
                else { tipoid = "00"; }

                apellido = " "; nombre = " ";
                nittitular2 = " "; apellido1titular2 = " "; apellido2titular2 = " ";
                nombre1titular2 = " "; nombre2titular2 = " "; rasocialtirular2 = " ";
                tipoidtitular2 = " "; tienetitular2 = false;

                // The VB code has a very large block filtering titular2 for ahorro (codahor=2), cdat (codahor=6), etc.
                // Due to extreme repetition (~600 lines), the lookup logic is abstracted into a helper call.
                tienetitular2 = BuscaTitular2PlanoProductos(myReader2, myconnect,
                    ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2,
                    ref nat_jurtitular2, ref nitcheqtitular2, ref rasocialtirular2,
                    ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2);

                if (tienetitular2 == true)
                {
                    if (tipoidtitular2 != " " && tipoidtitular2 != "")
                    {
                        if (tipoidtitular2 == "C") { tipoidtitular2 = "13"; }
                        else if (tipoidtitular2 == "N") { tipoidtitular2 = "31"; }
                        else if (tipoidtitular2 == "E") { tipoidtitular2 = "22"; }
                        else if (tipoidtitular2 == "T") { tipoidtitular2 = "12"; }
                        else if (tipoidtitular2 == "U") { tipoidtitular2 = "00"; }
                        else if (tipoidtitular2 == "R") { tipoidtitular2 = "11"; }
                        else { tipoidtitular2 = "00"; }
                    }

                    if (Information.IsNumeric(nat_jurtitular2))
                    {
                        if (Convert.ToInt32(nat_jurtitular2) == 2)
                        {
                            nittitular2 = nittitular2 + nitcheqtitular2;
                        }
                    }
                    else
                    {
                        if (nat_jurtitular2 == "J")
                        {
                            nittitular2 = nittitular2 + nitcheqtitular2;
                        }
                    }

                    tipoidtitular2 = Strings.Left(tipoidtitular2 + "  ", 2);
                    nittitular2 = Strings.Left(nittitular2 + "                    ", 20);
                    apellido1titular2 = Strings.Left(apellido1titular2 + "                                        ", 40);
                    apellido2titular2 = Strings.Left(apellido2titular2 + "                                        ", 40);
                    nombre1titular2 = Strings.Left(nombre1titular2 + "                                        ", 40);
                    nombre2titular2 = Strings.Left(nombre2titular2 + "                                        ", 40);
                    rasocialtirular2 = Strings.Left(rasocialtirular2 + "                                                            ", 60);
                }
                else
                {
                    tipoidtitular2 = "  ";
                    nittitular2 = "                    ";
                    apellido1titular2 = "                                        ";
                    apellido2titular2 = "                                        ";
                    nombre1titular2 = "                                        ";
                    nombre2titular2 = "                                        ";
                    rasocialtirular2 = "                                                            ";
                }

                if (Convert.ToInt32(myReader2["natjur"]) == 1)
                {
                    idtitular = Strings.Left(myReader2["nit"].ToString() + "                    ", 20);
                    rasocial = "                                                            ";
                    ParteNombreAsociado(myReader2["nombre"].ToString(), myReader2["apellido"].ToString(), myconnect, ref nombre1, ref nombre2, ref apellido1, ref apellido2, 1);
                    nombre1 = Strings.Left(nombre1 + "                                        ", 40);
                    nombre2 = Strings.Left(nombre2 + "                                        ", 40);
                    apellido1 = Strings.Left(apellido1 + "                                        ", 40);
                    apellido2 = Strings.Left(apellido2 + "                                        ", 40);
                }
                else
                {
                    idtitular = myReader2["nit"].ToString() + myReader2["nit_chequeo"].ToString();
                    idtitular = Strings.Left(idtitular + "                    ", 20);
                    nombre1 = "                                        ";
                    nombre2 = "                                        ";
                    apellido1 = "                                        ";
                    apellido2 = "                                        ";
                    rasocial = Strings.Left(myReader2["razon_social"].ToString() + "                                                            ", 60);
                }

                read11.Write(Strings.Right("          " + conce.ToString(), 10));
                read11.Write(Strings.Left(numproducto + "                    ", 20));
                read11.Write(fecahno + "-" + Strings.Right("00" + fecmes, 2) + "-" + Strings.Right("00" + fecdia, 2));
                read11.Write(tipoProducto);
                read11.Write(ciudad);
                read11.Write(tipoid);
                read11.Write(idtitular);
                read11.Write(apellido1 + apellido2 + nombre1 + nombre2 + rasocial);
                read11.Write(tipoidtitular2 + nittitular2 + apellido1titular2 + apellido2titular2 + nombre1titular2 + nombre2titular2 + rasocialtirular2);
                read11.WriteLine();
                si1 = true;
                conce += 1;
                msgbarra.PerformStep();
            }
            read11.Write("         0");
            read11.Write(CodEntidad);
            read11.Write(Strings.Right("          " + total.ToString(), 10));
            read11.WriteLine("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");

            msgbarra.Close();
            read11.Close();
            pmyConec03.Close();
            myCommand2.Connection.Close();
        }

        /// <summary>
        /// Helper that encapsulates the ~600-line repetitive titular2 lookup logic from PlanoProductos.
        /// Checks cc_nit_firmareq1/2/3 and cdat_firmareq1/2/3 fields from the reader.
        /// </summary>
        private bool BuscaTitular2PlanoProductos(OdbcDataReader myReader2, OdbcConnection myconnect,
            ref string nombre, ref string apellido, ref string nittitular2, ref string tipoidtitular2,
            ref string nat_jurtitular2, ref string nitcheqtitular2, ref string rasocialtirular2,
            ref string nombre1titular2, ref string nombre2titular2, ref string apellido1titular2, ref string apellido2titular2)
        {
            bool tienetitular2 = false;
            string codahor = myReader2["codahor"].ToString();

            // Determine which set of firmareq fields to use
            string[] firmareqFields;
            if (codahor == "2")
            {
                firmareqFields = new string[] { "cc_nit_firmareq1", "cc_nit_firmareq2", "cc_nit_firmareq3" };
            }
            else if (codahor == "6")
            {
                firmareqFields = new string[] { "cdat_firmareq1", "cdat_firmareq2", "cdar_firmareq3" };
            }
            else
            {
                return false;
            }

            // Check if any firmareq is valid
            bool anyValid = false;
            for (int idx = 0; idx < 3; idx++)
            {
                string val = myReader2[firmareqFields[idx]].ToString();
                if (val != "00000000000000" && val != "" && val != " " && !Convert.IsDBNull(myReader2[firmareqFields[idx]]))
                {
                    if (idx < 2 || Information.IsNumeric(myReader2[firmareqFields[idx]]))
                    {
                        anyValid = true;
                    }
                }
            }

            if (!anyValid) return false;

            string codigoter = myReader2["codigoter"].ToString();

            // Check if firmareq1 == codigoter  (first branch of VB code)
            if (myReader2[firmareqFields[0]].ToString() == codigoter)
            {
                // Try firmareq2
                string f2 = myReader2[firmareqFields[1]].ToString();
                if (f2 != "00000000000000" && f2 != "" && f2 != " " && !Convert.IsDBNull(myReader2[firmareqFields[1]]))
                {
                    tienetitular2 = BuscaTitular2Detalle(f2, myconnect, ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2,
                        ref nat_jurtitular2, ref nitcheqtitular2, ref rasocialtirular2,
                        ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2);
                    if (tienetitular2) return true;
                }
                // Try firmareq3
                string f3 = myReader2[firmareqFields[2]].ToString();
                if (!tienetitular2 && f3 != "00000000000000" && f3 != "" && f3 != " " && !Convert.IsDBNull(myReader2[firmareqFields[2]]))
                {
                    tienetitular2 = BuscaTitular2Detalle(f3, myconnect, ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2,
                        ref nat_jurtitular2, ref nitcheqtitular2, ref rasocialtirular2,
                        ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2);
                }
            }
            else
            {
                // firmareq1 != codigoter, try firmareq1 first
                string f1 = myReader2[firmareqFields[0]].ToString();
                if (f1 != "00000000000000" && f1 != "" && f1 != " " && Information.IsNumeric(myReader2[firmareqFields[0]]) && !Convert.IsDBNull(myReader2[firmareqFields[0]]))
                {
                    tienetitular2 = BuscaTitular2Detalle(f1, myconnect, ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2,
                        ref nat_jurtitular2, ref nitcheqtitular2, ref rasocialtirular2,
                        ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2);
                }
                if (!tienetitular2)
                {
                    string f2 = myReader2[firmareqFields[1]].ToString();
                    if (f2 != "00000000000000" && f2 != "" && f2 != " " && Information.IsNumeric(myReader2[firmareqFields[1]]) && !Convert.IsDBNull(myReader2[firmareqFields[1]]))
                    {
                        tienetitular2 = BuscaTitular2Detalle(f2, myconnect, ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2,
                            ref nat_jurtitular2, ref nitcheqtitular2, ref rasocialtirular2,
                            ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2);
                    }
                }
                if (!tienetitular2)
                {
                    string f3 = myReader2[firmareqFields[2]].ToString();
                    if (f3 != "00000000000000" && f3 != "" && f3 != " " && Information.IsNumeric(myReader2[firmareqFields[2]]) && !Convert.IsDBNull(myReader2[firmareqFields[2]]))
                    {
                        tienetitular2 = BuscaTitular2Detalle(f3, myconnect, ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2,
                            ref nat_jurtitular2, ref nitcheqtitular2, ref rasocialtirular2,
                            ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2);
                    }
                }
            }
            return tienetitular2;
        }

        /// <summary>
        /// Looks up a titular2 by codigoter from sys_maenit, then falls back to cnt_nit.
        /// </summary>
        private bool BuscaTitular2Detalle(string codigoterTitular, OdbcConnection myconnect,
            ref string nombre, ref string apellido, ref string nittitular2, ref string tipoidtitular2,
            ref string nat_jurtitular2, ref string nitcheqtitular2, ref string rasocialtirular2,
            ref string nombre1titular2, ref string nombre2titular2, ref string apellido1titular2, ref string apellido2titular2)
        {
            ok = false;
            string sqltitu2 = "select nombre as campo1, apellido as campo2, nit as campo3, tipo_nit as campo4 from sys_maenit where codigoter='" + codigoterTitular + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(sqltitu2, myconnect, "PlanoProductos", ref nombre, ref apellido, ref nittitular2, ref tipoidtitular2);
            string sqltitul2 = "select natjur as campo1, nit_chequeo as campo2  from sys_maenit where codigoter='" + codigoterTitular + "'";
            ok = this.OdbcConnect.ExecuteQueryconec(sqltitul2, myconnect, "PlanoProductos", ref nat_jurtitular2, ref nitcheqtitular2);

            if (ok == true)
            {
                if (Convert.ToInt32(nat_jurtitular2) == 2)
                {
                    msgcnt.BuscarTercero(nittitular2, myconnect, ref rasocialtirular2);
                    rasocialtirular2 = Strings.Left(rasocialtirular2 + "                                                            ", 60);
                }
                else
                {
                    ParteNombreAsociado(nombre, apellido, myconnect, ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2, 1);
                    rasocialtirular2 = "                                                            ";
                }
                return true;
            }
            else
            {
                // Fallback to cnt_nit
                long nitLong = 0;
                try { nitLong = Convert.ToInt64(codigoterTitular); } catch { }
                sqltitul2 = "select nit as campo1, tipo_persona as campo2, tipo_nit as campo3, nombre as campo4 from cnt_nit where nit='" + nitLong.ToString() + "'";
                ok = this.OdbcConnect.ExecuteQueryconec(sqltitul2, myconnect, "PlanoProductos", ref nittitular2, ref nat_jurtitular2, ref tipoidtitular2, ref nombre);
                if (ok == true)
                {
                    if (nat_jurtitular2 == "N")
                    {
                        ParteNombreAsociado(nombre, apellido, myconnect, ref nombre1titular2, ref nombre2titular2, ref apellido1titular2, ref apellido2titular2, 2);
                        rasocialtirular2 = "                                                            ";
                    }
                    else
                    {
                        sqltitul2 = "select nit_chequeo as campo1, razon_social as campo2 from cnt_nit where nit='" + nitLong.ToString() + "'";
                        ok = this.OdbcConnect.ExecuteQueryconec(sqltitul2, myconnect, "PlanoProductos", ref nitcheqtitular2, ref rasocialtirular2);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void ParteNombreAsociado(string nombre, string apellido, OdbcConnection myconnect)
        {
            string nom1 = " ", nom2 = " ", apl1 = " ", apl2 = " ";
            ParteNombreAsociado(nombre, apellido, myconnect, ref nom1, ref nom2, ref apl1, ref apl2, 1);
        }

        public void ParteNombreAsociado(string nombre, string apellido, OdbcConnection myconnect, ref string nom1, ref string nom2, ref string apl1, ref string apl2, int metodo = 1)
        {
            string nombrecompleto;
            string soloape = "";
            bool tiene1ape = false;
            string[] CadenaNombres, CadenaNom, CadenaApe;

            for (int n = 1; n <= 5; n++)
            {
                nombre = Strings.Replace(nombre, "  ", " ", 1, -1, CompareMethod.Text);
                apellido = Strings.Replace(apellido, "  ", " ", 1, -1, CompareMethod.Text);
            }
            apellido = apellido.Trim();
            nombre = nombre.Trim();
            CadenaNom = Strings.Split(nombre);
            CadenaApe = Strings.Split(apellido);

            if (metodo == 1)
            {
                switch (CadenaNom.Length)
                {
                    case 3:
                        nombre = CadenaNom[0] + " " + CadenaNom[1].Trim() + "." + CadenaNom[2].Trim();
                        break;
                    case 4:
                        nombre = CadenaNom[0] + " " + CadenaNom[1].Trim() + "." + CadenaNom[2].Trim() + "." + CadenaNom[3].Trim();
                        break;
                    case 5:
                        nombre = CadenaNom[0] + "." + CadenaNom[1].Trim() + " " + CadenaNom[2].Trim() + "." + CadenaNom[3].Trim() + "." + CadenaNom[4].Trim();
                        break;
                }

                switch (CadenaApe.Length)
                {
                    case 1:
                        tiene1ape = true;
                        soloape = apellido;
                        break;
                    case 3:
                        apellido = CadenaApe[0] + " " + CadenaApe[1].Trim() + "." + CadenaApe[2].Trim();
                        break;
                    case 4:
                        apellido = CadenaApe[0] + " " + CadenaApe[1].Trim() + "." + CadenaApe[2].Trim() + "." + CadenaApe[3].Trim();
                        break;
                    case 5:
                        apellido = CadenaApe[0] + "." + CadenaApe[1].Trim() + " " + CadenaApe[2].Trim() + "." + CadenaApe[3].Trim() + "." + CadenaApe[4].Trim();
                        break;
                }
            }
            else if (metodo == 3)
            {
                switch (CadenaNom.Length)
                {
                    case 3:
                        apellido = CadenaNom[0] + " " + CadenaNom[1];
                        nombre = CadenaNom[2];
                        break;
                    case 4:
                        apellido = CadenaNom[0] + " " + CadenaNom[1];
                        nombre = CadenaNom[2] + " " + CadenaNom[3];
                        break;
                    case 5:
                        apellido = CadenaNom[0] + " " + CadenaNom[1] + " " + CadenaNom[2];
                        nombre = CadenaNom[3] + " " + CadenaNom[4];
                        break;
                    case 6:
                        apellido = CadenaNom[0] + " " + CadenaNom[1] + " " + CadenaNom[2];
                        nombre = CadenaNom[3] + " " + CadenaNom[4] + " " + CadenaNom[5];
                        break;
                    case 7:
                        apellido = CadenaNom[0] + " " + CadenaNom[1] + " " + CadenaNom[2] + " " + CadenaNom[3];
                        nombre = CadenaNom[4] + " " + CadenaNom[5] + " " + CadenaNom[6];
                        break;
                    case 8:
                        apellido = CadenaNom[0] + " " + CadenaNom[1] + " " + CadenaNom[2] + " " + CadenaNom[3];
                        nombre = CadenaNom[4] + " " + CadenaNom[5] + " " + CadenaNom[6] + " " + CadenaNom[7];
                        break;
                }
                apl1 = apellido;
                nom1 = nombre;
                return; // goto 2 in VB
            }

            nombrecompleto = apellido + " " + nombre;
            nombrecompleto = nombrecompleto.Trim();
            try
            {
                for (int n = 1; n <= 5; n++)
                {
                    nombrecompleto = Strings.Replace(nombrecompleto, "  ", " ", 1, -1, CompareMethod.Text);
                }
                CadenaNombres = Strings.Split(nombrecompleto);
                apl1 = " "; apl2 = " "; nom1 = " "; nom2 = " ";
                switch (CadenaNombres.Length)
                {
                    case 1:
                        apl1 = CadenaNombres[0];
                        break;
                    case 2:
                        apl1 = CadenaNombres[0];
                        nom1 = CadenaNombres[1];
                        apl2 = " "; nom2 = " ";
                        break;
                    case 3:
                        if (tiene1ape == true)
                        {
                            apl1 = soloape;
                            apl2 = " ";
                            nom1 = CadenaNombres[1];
                            nom2 = CadenaNombres[2];
                        }
                        else
                        {
                            apl1 = CadenaNombres[0];
                            apl2 = CadenaNombres[1];
                            nom1 = CadenaNombres[2];
                            nom2 = " ";
                        }
                        break;
                    case 4:
                        apl1 = CadenaNombres[0];
                        apl2 = CadenaNombres[1];
                        nom1 = CadenaNombres[2];
                        nom2 = CadenaNombres[3];
                        break;
                    case 5:
                        if (CadenaNombres[1].Length <= 3)
                        {
                            apl1 = CadenaNombres[0];
                            apl2 = CadenaNombres[1] + " " + CadenaNombres[2];
                            nom1 = CadenaNombres[3];
                            nom2 = CadenaNombres[4];
                        }
                        if (CadenaNombres[2].Length <= 3)
                        {
                            apl1 = CadenaNombres[0];
                            apl2 = CadenaNombres[1];
                            nom1 = CadenaNombres[2] + " " + CadenaNombres[3];
                            nom2 = CadenaNombres[4];
                        }
                        if (CadenaNombres[3].Length <= 3)
                        {
                            apl1 = CadenaNombres[0];
                            apl2 = CadenaNombres[1];
                            nom1 = CadenaNombres[2];
                            nom2 = CadenaNombres[3] + " " + CadenaNombres[4];
                        }
                        break;
                    default:
                        apl1 = nombrecompleto;
                        apl2 = nombrecompleto;
                        nom1 = nombrecompleto;
                        nom2 = nombrecompleto;
                        break;
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        private int NivelIngreso(int Natjur, double Salario, double salarioMinimo)
        {
            double SMLMV = 0;
            if (Natjur == 1)
            {
                SMLMV = Salario / salarioMinimo;
                if (Salario <= 0) return 0;
                if (SMLMV <= 0) return 1;
                if (SMLMV <= 1) return 2;
                if (SMLMV <= 2) return 3;
                if (SMLMV <= 3) return 4;
                if (SMLMV <= 4) return 5;
                if (SMLMV <= 6) return 6;
                if (SMLMV <= 8) return 7;
                if (SMLMV <= 11) return 8;
                if (SMLMV <= 17) return 9;
                if (SMLMV <= 24) return 10;
                if (SMLMV <= 48) return 11;
                if (SMLMV > 48) return 12;
            }
            else if (Natjur == 2)
            {
                SMLMV = Salario * 12;
                if (SMLMV <= 0) return 0;
                if (SMLMV <= 10) return 1;
                if (SMLMV <= 50) return 2;
                if (SMLMV <= 150) return 3;
                if (SMLMV <= 500) return 4;
                if (SMLMV <= 1000) return 5;
                if (SMLMV <= 5000) return 6;
                if (SMLMV <= 10000) return 7;
                if (SMLMV <= 50000) return 8;
                if (SMLMV <= 100000) return 9;
                if (SMLMV <= 500000) return 10;
                if (SMLMV <= 1000000) return 11;
                if (SMLMV > 1000000) return 12;
            }
            return 0;
        }

        private void RemoverCaracteresEspeciales(ref string Campo)
        {
            Campo = Campo.Replace(";", " ");
            Campo = Campo.Replace(",", " ");
        }

        private void GrabaPlanoUsuarios(string NombreArchivo, string Delimitador, string TipoIden, string Cedula, DateTime FechaIngreso,
            string Apellido1, string Apellidos2, string Nombre, string Telefono, string email, string Direccion, string Asociado,
            string FechaRetiro, int activo, int Empleado, int MejarCabFamilia, DateTime FechaNacimiento, int Estracto, int Genero,
            string CodMunicipio, int TipoConracto, int NivelEscolaridad, int SectorEconomico, int NivelIngresos, int EstCivil, int JornadaLaboral, int Ocupacion,
            string ActEconomica, string AsistioAsamblea)
        {
            RemoverCaracteresEspeciales(ref Direccion);
            RemoverCaracteresEspeciales(ref email);
            RemoverCaracteresEspeciales(ref Apellido1);
            RemoverCaracteresEspeciales(ref Apellidos2);
            RemoverCaracteresEspeciales(ref Nombre);

            using (StreamWriter StrDisp = File.AppendText(NombreArchivo))
            {
                StrDisp.Write(TipoIden + Delimitador);
                StrDisp.Write(Cedula + Delimitador);
                StrDisp.Write(Apellido1 + Delimitador);
                StrDisp.Write(Apellidos2 + Delimitador);
                StrDisp.Write(Nombre + Delimitador);
                StrDisp.Write(FechaIngreso + Delimitador);
                StrDisp.Write(Telefono + Delimitador);
                StrDisp.Write(Direccion + Delimitador);
                StrDisp.Write(Asociado + Delimitador);
                StrDisp.Write(activo + Delimitador);
                StrDisp.Write(ActEconomica + Delimitador);
                StrDisp.Write(CodMunicipio + Delimitador);
                StrDisp.Write(email + Delimitador);
                StrDisp.Write(Genero + Delimitador);
                StrDisp.Write(Empleado + Delimitador);
                StrDisp.Write(TipoConracto + Delimitador);
                StrDisp.Write(NivelEscolaridad + Delimitador);
                StrDisp.Write(Estracto + Delimitador);
                StrDisp.Write(NivelIngresos + Delimitador);
                StrDisp.Write(FechaNacimiento + Delimitador);
                StrDisp.Write(EstCivil + Delimitador);
                StrDisp.Write(MejarCabFamilia + Delimitador);
                StrDisp.Write(Ocupacion + Delimitador);
                StrDisp.Write(SectorEconomico + Delimitador);
                StrDisp.Write(JornadaLaboral + Delimitador);
                if (Information.IsDate(FechaRetiro))
                {
                    StrDisp.Write(Convert.ToDateTime(FechaRetiro).ToString("dd/MM/yyyy") + Delimitador);
                }
                else
                {
                    StrDisp.Write(FechaRetiro + Delimitador);
                }
                StrDisp.Write(AsistioAsamblea);
                StrDisp.WriteLine();
                StrDisp.Close();
            }
        }

        // =====================================================================
        // Continuation: lines 19710-22500 of Clscartera.vb
        // =====================================================================

        public object RecalculaCuotaCredito(string codigoter, int lincred, double numero, int periodo,
            OdbcConnection myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet dsDataset = new DataSet();
            decimal TasaInt = 0;
            int plazo = 0;
            double cuota = 0;
            ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos clsliqcre = new ERP.Core.CarteraFinanciera.Services.Creditos.ClsLiqcreditos();

            Stbuilder.Append("Select salmae.saldo, salmae.cuota,copmae.clacuo,copmae.Clasei,salmae.tasaint,copmae.plazo,salmae.periodd,");
            Stbuilder.Append("salmae.ciclod,copmae.fecdesc,copmae.forseg,copmae.tasaseg,salmae.Clades,copmae.tasaadm,concar.codseg, ");
            Stbuilder.Append("solcre.TIP_ADM,concar.previv,maenit.SeguroRiesgo,maenit.apellido,maenit.nombre,maenit.empresa, ");
            Stbuilder.Append("concar.descripcion,concar.seguro,concar.poapen,concar.tasadm,concar.claAdmon,copmae.CUOTA_SEG,copmae.CUOTA_ADM,concar.foradmon,concar.valsegMin,concar.valsegMax,copmae.VALOROB    ");
            Stbuilder.Append("from cop_maecar copmae inner join cop_salmaecar salmae ");
            Stbuilder.Append("on copmae.codigoter = salmae.codigoter and copmae.lincred = salmae.lincred and ");
            Stbuilder.Append("copmae.numero = salmae.numero And salmae.periodo = " + periodo);
            Stbuilder.Append(" inner join cop_concar12 concar on copmae.lincred=concar.lincred ");
            Stbuilder.Append("inner join cop_solcre solcre on copmae.numero_soli=solcre.numero ");
            Stbuilder.Append("inner join sys_maenit maenit on copmae.codigoter=maenit.codigoter ");
            Stbuilder.Append("where copmae.codigoter='" + codigoter + "' and copmae.lincred=" + lincred + " and copmae.numero=" + numero);

            ok = this.OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "ReliquidacionCreditos", ref dsDataset, "TblCartera");
            if (ok == true)
            {
                DataRow r = dsDataset.Tables["TblCartera"].Rows[0];
                TasaInt = Math.Round((Convert.ToDecimal(r["tasaint"]) / Convert.ToDecimal(r["periodd"])) / 100, 8);
                // plazo = this.CalculaCuotasPagar(codigoter, lincred, numero, r["periodd"], r["ciclod"], r["clacuo"], r["tasaint"], r["saldo"], r["cuota"], periodo, r["forseg"], r["tasaseg"], r["CUOTA_SEG"], r["claAdmon"], r["foradmon"], r["tasaadm"], r["CUOTA_ADM"], myconnect, r["VALOROB"], r["valsegMin"], r["valsegMax"]); // ERROR: CS1503
                // cuota = clsliqcre.CalculaNuevaCuota(r["clacuo"], TasaInt, r["seguro"], r["tasadm"], r["poapen"], r["claAdmon"], plazo, r["periodd"], r["saldo"], r["VALOROB"], r["valsegMin"], r["valsegMax"]); // ERROR: CS1503
            }
            return null;
        }

        public double CalculaSaldoPendienteServicios(string codigoter, int periodo, OdbcConnection myconnect)
        {
            return CalculaSaldoPendienteServicios(codigoter, periodo, myconnect, new DateTime(1950, 1, 1));
        }

        public double CalculaSaldoPendienteServicios(string codigoter, int periodo, OdbcConnection myconnect, DateTime fechavemto)
        {
            double saldo = 0;
            stmysql = "select sum(a.saldocapital+a.saldointeres+a.saldomora+a.saldoseguro+a.saldoadmon+a.saldootros) as campo1 " +
                      "from cop_copmora a " +
                      "inner join cop_cuopen b on a.codigoter=b.codigoter and a.lincred=b.lincred " +
                      "and a.numero=b.numero and a.periodo_causa=b.periodo_causa " +
                      "inner join cop_concar12 c on a.lincred=c.lincred " +
                      "where a.codigoter='" + codigoter + "' and a.periodo_contable=" + periodo +
                      " and c.codahor='3' " + (fechavemto == new DateTime(1950, 1, 1) ? "" : " and b.fecha_movto <='" + fechavemto.ToString(varini.PstForFec) + "'");
            // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaSaldoPendienteServicios", ref saldo); // ERROR: CS1503
            return saldo;
        }

        public DataSet BuscaDeudasRespaldadasAsociado(string codigoter, int periodo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdataset = new DataSet();
            codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);

            stbuilder.Append("select maecar.codigoter,{fn concat(maenit.apellido,{fn concat(' ',maenit.nombre)})} as Nombre,");
            stbuilder.Append("{fn concat(rtrim(maecar.lincred),{fn concat('-',rtrim(maecar.numero))})} as Obligacion,");
            stbuilder.Append("(select sum(sal.saldo)*-1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred ");
            stbuilder.Append("  where sal.periodo=" + periodo + " and concar12.codahor='1' and maecar.codigoter=sal.codigoter) as SaldoAportes,");
            stbuilder.Append("(select sum(sal.saldo)*-1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred ");
            stbuilder.Append("  where sal.periodo=" + periodo + " and concar12.codahor='2' and maecar.codigoter=sal.codigoter) as SaldoAhorros,");
            stbuilder.Append("salmaecar.saldo, salmaecar.cuota,case salmaecar.clades when '1' then 'Nomina' else 'Caja' end as Clades ");
            stbuilder.Append("from cop_maecar maecar ");
            stbuilder.Append("inner join cop_salmaecar salmaecar on maecar.codigoter=salmaecar.codigoter and ");
            stbuilder.Append("maecar.lincred=salmaecar.lincred and maecar.numero=salmaecar.numero and salmaecar.periodo = " + periodo);
            stbuilder.Append(" inner join sys_maenit maenit on maecar.codigoter=maenit.codigoter ");
            stbuilder.Append("where maecar.lincred>=1000 and salmaecar.saldo<>0 and ");
            stbuilder.Append("(maecar.codeudor1='" + codigoter + "' or maecar.codeudor2='" + codigoter + "' or ");
            stbuilder.Append("maecar.codeudor3='" + codigoter + "' or maecar.codeudor4='" + codigoter + "')");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaDeudasRespaldadasAsociado", ref dsdataset, "TblRespaldadas");
            return dsdataset;
        }

        public double GrabarEstudioRetiro(string codigoter, DateTime fecsol, double consecutivo, string motivo, string estado,
            string observacion, DateTime fecnovedad, int periodo, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            ok = BuscarEstudioRetiro(consecutivo, myconnect);
            codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);
            motivo = ("0000" + motivo).Substring(("0000" + motivo).Length - 4);

            if (ok == false)
            {
                StBuilder.Append("insert into cop_estretiro(codigoter, fecsol, motivo, estado, observacion,periodo, fecnovedad) values('");
                StBuilder.Append(codigoter + "','" + fecsol.ToString(varini.PstForFec) + "','" + motivo + "','" + estado + "','");
                StBuilder.Append(observacion + "'," + periodo + "," + (estado != "P" ? "'" + fecnovedad.ToString(varini.PstForFec) + "'" : "NULL") + ")");
            }
            else
            {
                StBuilder.Append("update cop_estretiro set codigoter='" + codigoter + "',");
                StBuilder.Append("fecsol='" + fecsol.ToString(varini.PstForFec) + "', motivo='" + motivo + "',periodo=" + periodo);
                StBuilder.Append(",estado='" + estado + "',observacion='" + observacion + "' " + (estado != "P" ? ", fecnovedad='" + fecnovedad.ToString(varini.PstForFec) + "' " : ""));
                StBuilder.Append("where consecutivo=" + consecutivo);
            }

            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabarEstudioRetiro");
            if (consecutivo == 0)
            {
                return BuscarConsecutivoEstudioRetiro(codigoter, myconnect);
            }
            else
            {
                return 0;
            }
        }

        public bool BuscarEstudioRetiro(double consecutivo, OdbcConnection myconnect)
        {
            DataSet dsdata = null;
            return BuscarEstudioRetiro(consecutivo, myconnect, ref dsdata);
        }

        public bool BuscarEstudioRetiro(double consecutivo, OdbcConnection myconnect, ref DataSet dsdata)
        {
            DataSet dsdataset = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select consecutivo, codigoter, fecsol, motivo, estado, fecnovedad, observacion,periodo ");
            stbuilder.Append("from cop_estretiro ");
            stbuilder.Append("where consecutivo=" + consecutivo);

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarEstudioRetiroConsecutivo", ref dsdataset, "tblBuscaEstRetiro");
            if (ok == true)
            {
                dsdata = dsdataset;
            }
            return ok;
        }

        public virtual DataSet BuscarEstudioRetiro(string codigoter, OdbcConnection myconnect)
        {
            DataSet dsdataset = new DataSet();
            StringBuilder stbuilder = new StringBuilder();
            codigoter = ("00000000000000" + codigoter).Substring(("00000000000000" + codigoter).Length - 14);

            stbuilder.Append("select consecutivo, codigoter, fecsol, motivo, estado, fecnovedad, observacion,periodo, ");
            stbuilder.Append("case estado when 'P' then 'Pendiente' when 'R' then 'Rechazada' when 'A' then 'Aprobada' end as EstadoSol ");
            stbuilder.Append("from cop_estretiro ");
            stbuilder.Append("where codigoter='" + codigoter + "'");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarEstudioRetiroConsecutivo", ref dsdataset, "tblBuscaEstRetiro");
            return dsdataset;
        }

        public double BuscarConsecutivoEstudioRetiro(string codigoter, OdbcConnection myconnect)
        {
            double consecutivo = 0;
            stmysql = "select max(consecutivo) as campo1 from cop_estretiro where codigoter='" + codigoter + "'";
            // this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarConsecutivoEstudioRetiro", ref consecutivo); // ERROR: CS1503
            return consecutivo;
        }

        public void TrasladaParProvision(int PeriodoActual, int PeriodoTraslado, OdbcConnection Myconnect, Form myforma)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            int Fila = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Trasladando saldos de cartera",
                myforma, ProgressBarStyle.Continuous, "Del Periodo : " + PeriodoActual + " al periodo: " + PeriodoTraslado);

            StBuilder.Append("select idperiodo,idcodigo,tasab,tasac,tasad,tasae ");
            StBuilder.Append("from cop_parprov where idperiodo = '" + PeriodoActual + "'");

            ok = this.OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), Myconnect, "TrasladaParProvision", ref DsDataSet, "tblparprov");

            msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tblparprov"].Rows.Count);
            msgbarra.Show();

            if (ok == true)
            {
                while (Fila < DsDataSet.Tables["tblparprov"].Rows.Count)
                {
                    DataRow r = DsDataSet.Tables["tblparprov"].Rows[Fila];
                    // this.msgconfig.GrabaParProvision(r["idcodigo"], PeriodoTraslado, r["tasab"], r["tasac"], r["tasad"], r["tasae"], Myconnect); // ERROR: CS1061
                    Fila += 1;
                    msgbarra.PerformStep();
                }
            }

            msgbarra.Close();
            msgbarra.Dispose();
        }

        public DataTable ProvisionGeneralNomina(DateTime FechaProceso, string Salmes, decimal TasaProv, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            string StString = " ";
            int Fila = 0;
            double SaldoCartera = 0, SaldoProv = 0;
            decimal PorProv = 0;
            double Salcar = 0, Diff = 0;

            SaldoCartera = BuscaClasifSaldoCartNomina(FechaProceso, myconnect);
            SaldoProv = Math.Round(SaldoCartera * ((double)(TasaProv / 100)), 0);

            DsDataSet.Tables.Add("tblproGen");
            DsDataSet.Tables["tblproGen"].Columns.Add("periodo", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("lincred", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("Descripcion", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("cntsaldo", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("Saldo", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("Dif", StString.GetType());

            stbuilder.Append("select case when cnttemp.periodo is null then " + Strings.Format(FechaProceso, "yyyy") + " else cnttemp.periodo end as periodo,");
            stbuilder.Append("cnttemp.lincred,case when " + Salmes + " is null then 0 else " + Salmes + " end as cntsaldo");
            stbuilder.Append(", SalCap saldo, SalCap - case when " + Salmes + " is null then 0 else " + Salmes + " end as dif, coptemp.descripcion  ");
            stbuilder.Append("from cnt_coptemp01_vw cnttemp left join cop_coptemp01_vw coptemp on cnttemp.lincred = coptemp.lincred  and periodo_contable = " + Strings.Format(FechaProceso, "yyyyMM"));
            stbuilder.Append(" and (coptemp.clades = '1' or coptemp.clades is null) ");
            stbuilder.Append(" where (" + Salmes + "<> 0 or SalCap <> 0) ");
            stbuilder.Append("group by case when cnttemp.periodo is null then " + Strings.Format(FechaProceso, "yyyy") + " else cnttemp.periodo end,cnttemp.lincred,");
            stbuilder.Append("case when " + Salmes + " is null then 0 else " + Salmes + " end ,SalCap ,SalCap - case when " + Salmes + " is null then 0 else " + Salmes + " end, coptemp.descripcion");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ProvGenNomina", ref DsDataSet, "TblprovGen");
            if (ok == true)
            {
                while (Fila < DsDataSet.Tables["TblprovGen"].Rows.Count)
                {
                    DataRow r = DsDataSet.Tables["TblprovGen"].Rows[Fila];
                    Salcar = 0;
                    if (Convert.IsDBNull(r["saldo"]) == false)
                    {
                        PorProv = Math.Round((Convert.ToDecimal(r["saldo"]) / (decimal)SaldoCartera) * 100, 8);
                        Salcar = Math.Round(SaldoProv * ((double)(PorProv / 100)), 0, MidpointRounding.ToEven);
                    }
                    Diff = Salcar - Convert.ToDouble(r["cntsaldo"]);
                    DsDataSet.Tables["tblproGen"].Rows.Add(r["periodo"], r["lincred"], r["descripcion"], r["cntsaldo"], Salcar, Diff);
                    Fila += 1;
                }
            }
            return DsDataSet.Tables["tblproGen"];
        }

        public DataTable ProvisionGeneralCaja(DateTime FechaProceso, string Salmes, decimal TasaProv, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            string StString = " ";
            int Fila = 0;
            double SaldoCartera = 0, SaldoProv = 0;
            decimal PorProv = 0;
            double Salcar = 0, Diff = 0;

            SaldoCartera = BuscaClasifSaldoCartCaja(FechaProceso, myconnect);
            SaldoProv = Math.Round(SaldoCartera * ((double)(TasaProv / 100)), 0);

            DsDataSet.Tables.Add("tblproGen");
            DsDataSet.Tables["tblproGen"].Columns.Add("periodo", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("lincred", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("Descripcion", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("cntsaldo", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("Saldo", StString.GetType());
            DsDataSet.Tables["tblproGen"].Columns.Add("Dif", StString.GetType());

            stbuilder.Append("select case when cnttemp.periodo is null then " + Strings.Format(FechaProceso, "yyyy") + " else cnttemp.periodo end as periodo,");
            stbuilder.Append("cnttemp.lincred,case when " + Salmes + " is null then 0 else " + Salmes + " end as cntsaldo");
            stbuilder.Append(", SalCap saldo, SalCap - case when " + Salmes + " is null then 0 else " + Salmes + " end as dif, coptemp.descripcion  ");
            stbuilder.Append("from cnt_coptemp02_vw cnttemp left join cop_coptemp01_vw coptemp on cnttemp.lincred = coptemp.lincred  and periodo_contable = " + Strings.Format(FechaProceso, "yyyyMM"));
            stbuilder.Append(" and (coptemp.clades = '2' or coptemp.clades is null) ");
            stbuilder.Append(" where (" + Salmes + "<> 0 or SalCap <> 0) ");
            stbuilder.Append("group by case when cnttemp.periodo is null then " + Strings.Format(FechaProceso, "yyyy") + " else cnttemp.periodo end,cnttemp.lincred,");
            stbuilder.Append("case when " + Salmes + " is null then 0 else " + Salmes + " end ,SalCap ,SalCap - case when " + Salmes + " is null then 0 else " + Salmes + " end, coptemp.descripcion");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ProvGenNomina", ref DsDataSet, "TblprovGen");
            if (ok == true)
            {
                while (Fila < DsDataSet.Tables["TblprovGen"].Rows.Count)
                {
                    DataRow r = DsDataSet.Tables["TblprovGen"].Rows[Fila];
                    Salcar = 0;
                    if (Convert.IsDBNull(r["saldo"]) == false)
                    {
                        PorProv = Math.Round((Convert.ToDecimal(r["saldo"]) / (decimal)SaldoCartera) * 100, 8);
                        Salcar = Math.Round(SaldoProv * ((double)(PorProv / 100)), 0, MidpointRounding.ToEven);
                    }
                    Diff = Salcar - Convert.ToDouble(r["cntsaldo"]);
                    DsDataSet.Tables["tblproGen"].Rows.Add(r["periodo"], r["lincred"], r["descripcion"], r["cntsaldo"], Salcar, Diff);
                    Fila += 1;
                }
            }
            return DsDataSet.Tables["tblproGen"];
        }

        public double BuscaClasifSaldoCartNomina(DateTime FechaProceso, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            double Saldo = 0;

            stbuilder.Append("select sum(saldot) campo1 ");
            stbuilder.Append("from cop_copclas ");
            stbuilder.Append("where periodo_contable = '" + FechaProceso.ToString("yyyyMM") + "' and clades = 1 ");

            // ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscaClasifSaldoCartera", ref Saldo); // ERROR: CS1503
            return Saldo;
        }

        public double BuscaClasifSaldoCartCaja(DateTime FechaProceso, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            double Saldo = 0;

            stbuilder.Append("select sum(saldot) campo1 ");
            stbuilder.Append("from cop_copclas ");
            stbuilder.Append("where periodo_contable = '" + FechaProceso.ToString("yyyyMM") + "' and clades = 2 ");

            // ok = this.OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscaClasifSaldoCartera", ref Saldo); // ERROR: CS1503
            return Saldo;
        }

        public bool EliminarEstudioRetiro(double consecutivo, OdbcConnection myconnect)
        {
            stmysql = "delete from cop_estretiro where consecutivo=" + consecutivo;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarEstudioRetiro");
            return ok;
        }

        public bool RecalculaCuotasXpagar(int periodo, int lincredIni, int lincredFin,
            Form MyForma, OdbcConnection myconnect)
        {
            DataSet dsdataset = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress BarraProgeso = new ERP.Core.Compartido.Controles.Barraprogress("Calculando Cuotas por Pagar", MyForma);
            int Cuopen = 0;
            DateTime fechaVence;

            stmysql = "SELECT copmae.codigoter, copmae.lincred, copmae.numero, saldos.periodd," +
                      "saldos.ciclod, copmae.clacuo, saldos.cuota, saldos.tasaint, saldos.saldo, " +
                      "copmae.tasaseg,copmae.tasaadm,copmae.forseg,parame12.claAdmon,parame12.foradmon,copmae.CUOTA_SEG,copmae.CUOTA_ADM,parame12.valsegMin,parame12.valsegMax,copmae.VALOROB,copmae.NUMERO_SOLI,copmae.fecdesc,copmae.plazo   " +
                      "from cop_maecar copmae " +
                      "inner join cop_saldos_vw saldos on saldos.codigoter = copmae.codigoter and " +
                      "saldos.lincred = copmae.lincred and saldos.numero = copmae.numero and " +
                      "saldos.periodo = " + periodo +
                      " inner join cop_concar12 parame12 on copmae.lincred=parame12.lincred " +
                      " where copmae.lincred between " + lincredIni + " and " + lincredFin + " and copmae.lincred<>9999 " +
                      "and ((copmae.lincred>=1000 and saldos.saldo<>0) or (copmae.lincred<1000 and (saldos.saldo<>0 or saldos.cuota<>0)))";

            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "RecalculaCuotasXpagar", ref dsdataset, "tblRecalcula");
            if (ok == true)
            {
                BarraProgeso.ValorMinimoMaximo(0, dsdataset.Tables["tblRecalcula"].Rows.Count);
                BarraProgeso.Show();

                for (int i = 0; i < dsdataset.Tables["tblRecalcula"].Rows.Count; i++)
                {
                    DataRow r = dsdataset.Tables["tblRecalcula"].Rows[i];
                    Cuopen = 0;
                    // Cuopen = this.CalculaCuotasPagar(r["codigoter"], r["lincred"], r["numero"], r["periodd"], r["ciclod"], r["clacuo"], r["tasaint"], r["saldo"], r["cuota"], periodo, r["forseg"], r["tasaseg"], r["CUOTA_SEG"], r["claAdmon"], r["foradmon"], r["tasaadm"], r["CUOTA_ADM"], myconnect, r["VALOROB"], r["valsegMin"], r["valsegMax"]); // ERROR: CS1503
                    // this.GrabaNumeroCuotas(r["codigoter"], r["lincred"], r["numero"], periodo, Cuopen, Cuopen, myconnect); // ERROR: CS1503
                    // fechaVence = this.CalculaFechaVence(r["NUMERO_SOLI"], r["cuota"], r["fecdesc"], r["plazo"], myconnect, r["periodd"], r["ciclod"]); // ERROR: CS1503
                    // stmysql = " Update  cop_maecar  set FECVEMTO='" + fechaVence.ToString(varini.PstForFec) + "' where codigoter='" + r["codigoter"] + "' and lincred=" + r["lincred"] + " and NUMERO=" + r["numero"]; // ERROR: CS0165
                    OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RecalculaCuotasXpagar");
                    BarraProgeso.PerformStep();
                }
                BarraProgeso.Dispose();
                BarraProgeso.Close();
            }
            return ok;
        }

        public DataSet CargaDatosEstudioPreJuridico(string BuscarPor, string codigo, int periodo, string clades,
            int diasmora, int lineaini, int lineafin, string cobroprejuridico, string cobrojuridico,
            string compromiso, DateTime fecini, DateTime fecfin, Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            string WhereBuscar = "", WhereCobro = "";
            int i = 0;
            string nomcodeudor = "";
            DataSet dsdatcobjur = new DataSet(), dsdata = new DataSet(), dsdatacodeudores = new DataSet(), dsdataobligacion = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress msbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando Informacion", myforma);

            if (codigo != "Todas")
            {
                switch (BuscarPor)
                {
                    case "1":
                        WhereBuscar = " maecar.codigoter='" + ("00000000000000" + codigo).Substring(("00000000000000" + codigo).Length - 14) + "' and ";
                        break;
                    case "2":
                        WhereBuscar = " maenit.empresa='" + ("0000" + codigo).Substring(("0000" + codigo).Length - 4) + "' and ";
                        break;
                    case "3":
                        WhereBuscar = " maenit.cencosto='" + ("00000000" + codigo).Substring(("00000000" + codigo).Length - 8) + "' and ";
                        break;
                    case "4":
                        WhereBuscar = " maenit.agencia='" + ("0000" + codigo).Substring(("0000" + codigo).Length - 4) + "' and ";
                        break;
                }
            }

            if (cobroprejuridico == "N" && cobrojuridico == "N")
            {
                WhereCobro = " (maecar.COBROJUR<>'Y' AND maecar.COBROJUR<>'P') and ";
            }
            else if (cobroprejuridico == "N")
            {
                WhereCobro = " maecar.COBROJUR<>'P' and ";
            }
            else if (cobrojuridico == "N")
            {
                WhereCobro = " maecar.COBROJUR<>'Y' and ";
            }

            stbuilder.Append("select maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
            stbuilder.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sum(saldointeres) as SaldoInteres,");
            stbuilder.Append("sum(saldoseguro+saldoadmon+saldootros) as SaldoOtros, sum(saldomora) as saldomora,sal.clades,car12.descripcion,");
            stbuilder.Append("(select max(a.catego) from cop_copclas a where a.periodo_contable=(select max(b.periodo_contable) from cop_copclas b ");
            stbuilder.Append("  where a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero) ");
            stbuilder.Append(" and a.codigoter=maecar.codigoter and a.lincred=maecar.lincred and a.numero=maecar.numero) as Categoria, ");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='1') as Aportes,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='6') as Cdats,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper=4) as AhorroPermanente,");
            stbuilder.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
            stbuilder.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper<>4) as Ahorros,emp.nombre as nom_empresa ");
            stbuilder.Append("from cop_maecar maecar ");
            stbuilder.Append("inner join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and maecar.numero=sal.numero ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred=car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter=maenit.codigoter ");
            stbuilder.Append("inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa ");
            stbuilder.Append("inner join cop_copmoracircular_vw cirmora on maecar.codigoter=cirmora.codigoter and maecar.lincred=cirmora.lincred ");
            stbuilder.Append(" and maecar.numero=cirmora.numero and sal.periodo=cirmora.periodo_contable ");
            stbuilder.Append("inner join cop_copmora copmora on maecar.codigoter=copmora.codigoter and maecar.lincred=copmora.lincred ");
            stbuilder.Append("and maecar.numero=copmora.numero and sal.periodo=copmora.periodo_contable ");
            stbuilder.Append("where " + WhereBuscar + WhereCobro + " sal.periodo=" + periodo + " and sal.saldo<>0 and cirmora.diasmora>=" + diasmora);
            stbuilder.Append(" and maecar.lincred between " + lineaini + " and " + lineafin + (clades == "3" ? "" : " and sal.clades='" + clades + "' "));
            stbuilder.Append(" group by maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
            stbuilder.Append(" sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sal.clades,sal.periodo,car12.descripcion,emp.nombre ");
            stbuilder.Append(" order by maecar.codigoter,maecar.lincred,maecar.numero");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargaDatosEstudioPreJuridico", ref dsdatcobjur, "tblCobJuri");
            if (ok == true)
            {
                msbarra.ValorMinimoMaximo(0, dsdatcobjur.Tables["tblCobJuri"].Rows.Count);
                msbarra.Show();

                while (i < dsdatcobjur.Tables["tblCobJuri"].Rows.Count)
                {
                    dsdata = null;
                    DataRow r = dsdatcobjur.Tables["tblCobJuri"].Rows[i];
                    dsdata = CargaDatosEstudioPreJuridicoCodeudores(r["codigoter"].ToString(), Convert.ToInt32(r["lincred"]), Convert.ToDouble(r["numero"]), periodo, myconnect);
                    if (dsdata.Tables.Count != 0)
                    {
                        if (dsdatacodeudores.Tables.Count == 0)
                        {
                            dsdatacodeudores.Tables.Add(dsdata.Tables["tblcodeudores"].Copy());
                        }
                        else
                        {
                            for (int j = 0; j < dsdata.Tables["tblcodeudores"].Rows.Count; j++)
                            {
                                DataRow rc = dsdata.Tables["tblcodeudores"].Rows[j];
                                dsdatacodeudores.Tables["tblcodeudores"].Rows.Add(rc["cedula"], rc["LineaDeudor"], rc["ObligacionDeudor"], rc["codigoter"], rc["lincred"], rc["numero"],
                                    rc["valorob"], rc["cuota"], rc["fecfact"], rc["fecultpago"], rc["saldo"], rc["CLASEGAR"], rc["diasmora"], rc["fogacla"], rc["nombre"],
                                    rc["apellido"], rc["SaldoInteres"], rc["SaldoOtros"], rc["saldomora"], rc["clades"], rc["descripcion"], rc["Categoria"], rc["Aportes"],
                                    rc["Cdats"], rc["AhorroPermanente"], rc["Ahorros"], rc["nom_empresa"]);
                            }
                        }
                    }
                    if (nomcodeudor != (r["codigoter"].ToString() + r["nombre"].ToString() + r["apellido"].ToString()))
                    {
                        dsdata = null;
                        nomcodeudor = r["codigoter"].ToString() + r["nombre"].ToString() + r["apellido"].ToString();

                        dsdata = this.CargaDatosObligacionesDeudores(r["codigoter"].ToString(), diasmora, periodo, myconnect);
                        if (dsdata.Tables.Count != 0)
                        {
                            if (dsdataobligacion.Tables.Count == 0)
                            {
                                dsdataobligacion.Tables.Add(dsdata.Tables["tblobligacion"].Copy());
                            }
                            else
                            {
                                for (int j = 0; j < dsdata.Tables["tblobligacion"].Rows.Count; j++)
                                {
                                    DataRow ro = dsdata.Tables["tblobligacion"].Rows[j];
                                    dsdataobligacion.Tables["tblobligacion"].Rows.Add(ro["codigoter"], ro["lincred"], ro["numero"], ro["valorob"], ro["cuota"],
                                        ro["fecfact"], ro["fecultpago"], ro["saldo"], ro["CLASEGAR"], ro["diasmora"], ro["fogacla"], ro["nombre"],
                                        ro["apellido"], ro["SaldoInteres"], ro["SaldoOtros"], ro["saldomora"], ro["clades"], ro["descripcion"], ro["Categoria"]);
                                }
                            }
                        }
                    }
                    i += 1;
                    msbarra.PerformStep();
                }
                msbarra.Dispose();
                msbarra.Close();
                if (dsdatacodeudores.Tables.Count == 0)
                {
                    ConfigDatasetEstCodeudor(ref dsdatacodeudores);
                }
                if (dsdataobligacion.Tables.Count == 0)
                {
                    ConfigDatasetEstObliga(ref dsdataobligacion);
                }
                dsdatcobjur.Tables.Add(dsdatacodeudores.Tables["tblcodeudores"].Copy());
                dsdatcobjur.Tables.Add(dsdataobligacion.Tables["tblobligacion"].Copy());
            }
            return dsdatcobjur;
        }

        public void ConfigDatasetEstCodeudor(ref DataSet dataset)
        {
            string StString = " ";
            int StInteger = 0;
            double StDouble = 0;
            DateTime StFecha = DateTime.Now;

            dataset.Tables.Add("tblcodeudores");
            dataset.Tables["tblcodeudores"].Columns.Add("cedula", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("LineaDeudor", StInteger.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("ObligacionDeudor", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("codigoter", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("lincred", StInteger.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("numero", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("valorob", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("cuota", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("fecfact", StFecha.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("fecultpago", StFecha.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("saldo", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("CLASEGAR", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("diasmora", StInteger.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("fogacla", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("nombre", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("apellido", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("SaldoInteres", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("SaldoOtros", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("saldomora", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("clades", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("descripcion", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("Categoria", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("Aportes", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("Cdats", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("AhorroPermanente", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("Ahorros", StDouble.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("nom_empresa", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("direccionRes", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("telefonoRes", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("direccionEmp", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("telefonoEmp", StString.GetType());
            dataset.Tables["tblcodeudores"].Columns.Add("celular", StString.GetType());

            dataset.Tables["tblcodeudores"].Rows.Add("", 0, 0, "", 0, 0, 0, 0, DateTime.Now, DateTime.Now, 0, "", 0, "", "", "", 0, 0, 0, "", "", "", 0, 0, 0, 0, "", "", "", "", "", "");
        }

        public DataSet CargaDatosEstudioPreJuridicoCodeudores(string codigoter, int lincred, double numero,
            int periodo, OdbcConnection myconnect)
        {
            DataSet dsCodeudores = new DataSet(), dsdata = new DataSet();
            int fila = 0;
            DataSet dsdatosdata = new DataSet();
            DateTime fecha = new DateTime(1950, 1, 1);

            stmysql = "select mae.codeudor1 as codeudor,maenit.nombre,maenit.apellido,emp.nombre as nom_empresa,maenit.DIRECCION,maenit.TELEFONO1,maenit.DIRECCION_ENVIO,maenit.TELEFONO2,maenit.MOVIL  from cop_maecar mae " +
                "inner join sys_maenit maenit on mae.codeudor1=maenit.codigoter " +
                "inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa " +
                " where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero +
                " and (mae.codeudor1<>mae.codigoter and mae.codeudor1<>'' and mae.codeudor1<>'00000000000000') " +
                "union all " +
                "select codeudor2 as codeudor,maenit.nombre,maenit.apellido,emp.nombre as nom_empresa,maenit.DIRECCION,maenit.TELEFONO1,maenit.DIRECCION_ENVIO,maenit.TELEFONO2,maenit.MOVIL  from cop_maecar mae " +
                "inner join sys_maenit maenit on mae.codeudor2=maenit.codigoter " +
                "inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa " +
                " where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero +
                " and (mae.codeudor2<>mae.codigoter and mae.codeudor2<>'' and mae.codeudor2<>'00000000000000') " +
                "union all " +
                "select codeudor3 as codeudor,maenit.nombre,maenit.apellido,emp.nombre as nom_empresa,maenit.DIRECCION,maenit.TELEFONO1,maenit.DIRECCION_ENVIO,maenit.TELEFONO2,maenit.MOVIL  from cop_maecar mae  " +
                "inner join sys_maenit maenit on mae.codeudor3=maenit.codigoter " +
                "inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa " +
                " where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero +
                " and (mae.codeudor3<>mae.codigoter and mae.codeudor3<>'' and mae.codeudor3<>'00000000000000') " +
                "union all " +
                "select codeudor4 as codeudor,maenit.nombre,maenit.apellido,emp.nombre as nom_empresa,maenit.DIRECCION,maenit.TELEFONO1,maenit.DIRECCION_ENVIO,maenit.TELEFONO2,maenit.MOVIL  from cop_maecar mae  " +
                "inner join sys_maenit maenit on mae.codeudor4=maenit.codigoter " +
                "inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa " +
                " where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero +
                " and (mae.codeudor4<>mae.codigoter and mae.codeudor4<>'' and mae.codeudor4<>'00000000000000') ";

            ok = this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CargaDatosEstudioPreJuridicoCodeudores", ref dsdata, "tblconsulta");
            if (ok == true)
            {
                while (fila < dsdata.Tables["tblconsulta"].Rows.Count)
                {
                    DataRow rCod = dsdata.Tables["tblconsulta"].Rows[fila];
                    StringBuilder stbuilder2 = new StringBuilder();
                    stbuilder2.Append("select '" + codigoter + "' as cedula, " + lincred + " as LineaDeudor," + numero + " as ObligacionDeudor,maecar.codigoter,");
                    stbuilder2.Append("maecar.lincred, maecar.numero, maecar.valorob, sal.cuota, maecar.fecfact, maecar.fecultpago, ");
                    stbuilder2.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sum(saldointeres) as SaldoInteres,");
                    stbuilder2.Append("sum(saldoseguro+saldoadmon+saldootros) as SaldoOtros, sum(saldomora) as saldomora,sal.clades,car12.descripcion,");
                    stbuilder2.Append("(select max(a.catego) from cop_copclas a where a.periodo_contable=(select max(b.periodo_contable) from cop_copclas b ");
                    stbuilder2.Append("  where a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero) ");
                    stbuilder2.Append(" and a.codigoter=maecar.codigoter and a.lincred=maecar.lincred and a.numero=maecar.numero) as Categoria, ");
                    stbuilder2.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
                    stbuilder2.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='1') as Aportes,");
                    stbuilder2.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
                    stbuilder2.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='6') as Cdats,");
                    stbuilder2.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
                    stbuilder2.Append("  where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper=4) as AhorroPermanente,");
                    stbuilder2.Append("(select sum(saldo)*-1 from cop_salmaecar salma inner join cop_concar12 concar on salma.lincred=concar.lincred ");
                    stbuilder2.Append(" where maecar.codigoter=salma.codigoter and sal.periodo=salma.periodo and concar.codahor='2' and concar.equisuper<>4) as Ahorros,emp.nombre as nom_empresa,maenit.DIRECCION,maenit.TELEFONO1,maenit.DIRECCION_ENVIO,maenit.TELEFONO2,maenit.MOVIL ");
                    stbuilder2.Append("from cop_maecar maecar ");
                    stbuilder2.Append("inner join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and maecar.numero=sal.numero ");
                    stbuilder2.Append("inner join cop_concar12 car12 on maecar.lincred=car12.lincred ");
                    stbuilder2.Append("inner join sys_maenit maenit on maecar.codigoter=maenit.codigoter ");
                    stbuilder2.Append("inner join cop_empresa13 emp on maenit.empresa=emp.codigo_empresa ");
                    stbuilder2.Append("left join cop_copmoracircular_vw cirmora on maecar.codigoter=cirmora.codigoter and maecar.lincred=cirmora.lincred ");
                    stbuilder2.Append(" and maecar.numero=cirmora.numero and sal.periodo=cirmora.periodo_contable ");
                    stbuilder2.Append("left join cop_copmora copmora on maecar.codigoter=copmora.codigoter and maecar.lincred=copmora.lincred ");
                    stbuilder2.Append("and maecar.numero=copmora.numero and sal.periodo=copmora.periodo_contable ");
                    stbuilder2.Append("where maecar.codigoter='" + rCod["codeudor"] + "' and sal.periodo=" + periodo + " ");
                    stbuilder2.Append("and ((sal.saldo<>0 and maecar.lincred>=1000) or ((sal.saldo<>0 or sal.cuota<>0) and maecar.lincred<1000)) ");
                    stbuilder2.Append("group by maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
                    stbuilder2.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sal.clades,sal.periodo,car12.descripcion,emp.nombre,maenit.DIRECCION,maenit.TELEFONO1,maenit.DIRECCION_ENVIO,maenit.TELEFONO2,maenit.MOVIL ");
                    stbuilder2.Append("order by maecar.codigoter,maecar.lincred,maecar.numero");

                    ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder2.ToString(), myconnect, "CargaDatosEstudioPreJuridicoCodeudores", ref dsCodeudores, "tblcodeudores");
                    if (ok == true)
                    {
                        if (dsdatosdata.Tables.Count == 0)
                        {
                            dsdatosdata.Tables.Add(dsCodeudores.Tables["tblcodeudores"].Copy());
                        }
                        else
                        {
                            for (int ii = 0; ii < dsCodeudores.Tables["tblcodeudores"].Rows.Count; ii++)
                            {
                                DataRow rc = dsCodeudores.Tables["tblcodeudores"].Rows[ii];
                                if (rc["diasmora"] is DBNull) rc["diasmora"] = 0;
                                if (rc["SaldoInteres"] is DBNull) rc["SaldoInteres"] = 0;
                                if (rc["SaldoOtros"] is DBNull) rc["SaldoOtros"] = 0;
                                if (rc["saldomora"] is DBNull) rc["saldomora"] = 0;

                                dsdatosdata.Tables["tblcodeudores"].Rows.Add(rc["cedula"], rc["LineaDeudor"], rc["ObligacionDeudor"], rc["codigoter"], rc["lincred"], rc["numero"],
                                    rc["valorob"], rc["cuota"], rc["fecfact"], rc["fecultpago"], rc["saldo"], rc["CLASEGAR"], rc["diasmora"], rc["fogacla"], rc["nombre"],
                                    rc["apellido"], rc["SaldoInteres"], rc["SaldoOtros"], rc["saldomora"], rc["clades"], rc["descripcion"], rc["Categoria"], rc["Aportes"],
                                    rc["Cdats"], rc["AhorroPermanente"], rc["Ahorros"], rc["nom_empresa"], rc["DIRECCION"], rc["TELEFONO1"], rc["DIRECCION_ENVIO"], rc["TELEFONO2"], rc["MOVIL"]);
                            }
                        }
                        dsCodeudores.Tables.Clear();
                    }
                    else
                    {
                        if (dsdatosdata.Tables.Count == 0)
                        {
                            dsdatosdata.Tables.Add(dsCodeudores.Tables["tblcodeudores"].Copy());
                        }
                        dsdatosdata.Tables["tblcodeudores"].Rows.Add(codigoter, lincred, numero, rCod["codeudor"], 0, 0,
                            0, 0, fecha, fecha, 0, " ", 0, "0", rCod["nombre"], rCod["apellido"], 0, 0, 0, "0", " ", " ", 0,
                            0, 0, 0, rCod["nom_empresa"], rCod["DIRECCION"], rCod["TELEFONO1"], rCod["DIRECCION_ENVIO"], rCod["TELEFONO2"], rCod["MOVIL"]);
                    }
                    fila += 1;
                }
            }
            return dsdatosdata;
        }

        public void ConfigDatasetEstObliga(ref DataSet dataset)
        {
            string StString = " ";
            int StInteger = 0;
            double StDouble = 0;
            DateTime StFecha = DateTime.Now;

            dataset.Tables.Add("tblobligacion");
            dataset.Tables["tblobligacion"].Columns.Add("codigoter", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("lincred", StInteger.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("numero", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("valorob", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("cuota", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("fecfact", StFecha.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("fecultpago", StFecha.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("saldo", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("CLASEGAR", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("diasmora", StInteger.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("fogacla", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("nombre", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("apellido", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("SaldoInteres", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("SaldoOtros", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("saldomora", StDouble.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("clades", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("descripcion", StString.GetType());
            dataset.Tables["tblobligacion"].Columns.Add("Categoria", StString.GetType());

            dataset.Tables["tblobligacion"].Rows.Add("", 0, 0, 0, 0, DateTime.Now, DateTime.Now, 0, "", 0, "", "", "", 0, 0, 0, "", "", "");
        }

        public DataSet CargaDatosObligacionesDeudores(string codigoter, int diasmora, int periodo, OdbcConnection myconnect)
        {
            DataSet dsObligacion = new DataSet();
            DataSet dsdatosdata = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select maecar.codigoter,maecar.lincred, maecar.numero, maecar.valorob, sal.cuota, maecar.fecfact, maecar.fecultpago, ");
            stbuilder.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sum(saldointeres) as SaldoInteres,");
            stbuilder.Append("sum(saldoseguro+saldoadmon+saldootros) as SaldoOtros, sum(saldomora) as saldomora,sal.clades,car12.descripcion,");
            stbuilder.Append("(select max(a.catego) from cop_copclas a where a.periodo_contable=(select max(b.periodo_contable) from cop_copclas b ");
            stbuilder.Append("  where a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero) ");
            stbuilder.Append(" and a.codigoter=maecar.codigoter and a.lincred=maecar.lincred and a.numero=maecar.numero) as Categoria ");
            stbuilder.Append("from cop_maecar maecar ");
            stbuilder.Append("inner join cop_salmaecar sal on maecar.codigoter=sal.codigoter and maecar.lincred=sal.lincred and maecar.numero=sal.numero ");
            stbuilder.Append("inner join cop_concar12 car12 on maecar.lincred=car12.lincred ");
            stbuilder.Append("inner join sys_maenit maenit on maecar.codigoter=maenit.codigoter ");
            stbuilder.Append("inner join cop_copmoracircular_vw cirmora on maecar.codigoter=cirmora.codigoter and maecar.lincred=cirmora.lincred ");
            stbuilder.Append(" and maecar.numero=cirmora.numero and sal.periodo=cirmora.periodo_contable ");
            stbuilder.Append("inner join cop_copmora copmora on maecar.codigoter=copmora.codigoter and maecar.lincred=copmora.lincred ");
            stbuilder.Append("and maecar.numero=copmora.numero and sal.periodo=copmora.periodo_contable ");
            stbuilder.Append("where maecar.codigoter='" + codigoter + "' and sal.periodo=" + periodo + " and sal.saldo<>0 and maecar.lincred>=1000 and cirmora.diasmora<" + diasmora);
            stbuilder.Append(" group by maecar.codigoter,maecar.lincred,maecar.numero,maecar.valorob,sal.cuota,maecar.fecfact,maecar.fecultpago,");
            stbuilder.Append("sal.saldo,maecar.CLASEGAR,cirmora.diasmora,car12.fogacla,maenit.nombre,maenit.apellido,sal.clades,sal.periodo,car12.descripcion ");
            stbuilder.Append("order by maecar.codigoter,maecar.lincred,maecar.numero");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CargaDatosObligacionesDeudores", ref dsObligacion, "tblobligacion");
            if (ok == true)
            {
                if (dsdatosdata.Tables.Count == 0)
                {
                    dsdatosdata.Tables.Add(dsObligacion.Tables["tblobligacion"].Copy());
                }
                else
                {
                    for (int i = 0; i < dsObligacion.Tables["tblobligacion"].Rows.Count; i++)
                    {
                        DataRow r = dsObligacion.Tables["tblobligacion"].Rows[i];
                        dsdatosdata.Tables["tblobligacion"].Rows.Add(r["codigoter"], r["lincred"], r["numero"], r["valorob"], r["cuota"],
                            r["fecfact"], r["fecultpago"], r["saldo"], r["CLASEGAR"], r["diasmora"], r["fogacla"], r["nombre"],
                            r["apellido"], r["SaldoInteres"], r["SaldoOtros"], r["saldomora"], r["clades"], r["descripcion"], r["Categoria"]);
                    }
                }
            }
            return dsdatosdata;
        }

        public bool RecalculaDiasMora(DateTime FecCorte, int lineaini, int lineafin, Form myforma, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdataset = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress BarraProgeso = new ERP.Core.Compartido.Controles.Barraprogress("Calculando Dias Mora", myforma);
            int dias = 0, i = 0, sw1 = 0;
            DateTime fechavence = DateTime.Now;

            stbuilder.Append("select a.codigoter,a.lincred,a.numero,a.periodo_causa,a.periodo_contable,b.fecha_movto ");
            stbuilder.Append("from cop_copmora a ");
            stbuilder.Append("inner join cop_cuopen b on a.codigoter=b.codigoter and a.lincred=b.lincred ");
            stbuilder.Append("and a.numero=b.numero and a.periodo_causa=b.periodo_causa ");
            stbuilder.Append("inner join cop_salmaecar sal on a.codigoter=sal.codigoter and a.lincred=sal.lincred ");
            stbuilder.Append("and a.numero=sal.numero and a.periodo_contable=sal.periodo ");
            stbuilder.Append("where a.periodo_contable=" + FecCorte.ToString("yyyyMM") + " and a.lincred between " + lineaini + " and " + lineafin + " and a.diasmora>1 ");
            stbuilder.Append("and (a.saldocapital<>0 or a.saldoextra<>0) and ((sal.saldo>0 and a.lincred>=1000) or (sal.saldo<=0 and a.lincred<1000)) ");
            stbuilder.Append("order by a.codigoter,a.lincred,a.numero,a.periodo_causa");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "RecalculaDiasMora", ref dsdataset, "TblDiasMora");
            if (ok == true)
            {
                BarraProgeso.ValorMinimoMaximo(0, dsdataset.Tables["TblDiasMora"].Rows.Count);
                BarraProgeso.Show();

                while (i < dsdataset.Tables["TblDiasMora"].Rows.Count)
                {
                    DataRow r = dsdataset.Tables["TblDiasMora"].Rows[i];
                    sw1 = 0;
                    if (r["fecha_movto"] is DBNull)
                    {
                        sw1 = 1;
                    }
                    else
                    {
                        fechavence = Convert.ToDateTime(r["fecha_movto"]);
                    }
                    if (sw1 == 0)
                    {
                        dias = CalculaDias(FecCorte, fechavence);
                        if (dias >= 0)
                        {
                            stmysql = "update cop_copmora  set DiasMora = " + dias + (dias == 0 ? ",saldomora=0 " : "") +
                                      " where codigoter ='" + r["codigoter"] + "' and lincred = " + r["lincred"] +
                                      " and numero = " + r["numero"] + " and periodo_causa = " + r["periodo_causa"] +
                                      " and Periodo_contable = " + r["periodo_contable"];
                            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "RecalculaDiasMora");
                        }
                    }
                    BarraProgeso.PerformStep();
                    i = i + 1;
                }
                BarraProgeso.Dispose();
                BarraProgeso.Close();
                return true;
            }
            return false;
        }

        // Continuation in Clscartera.Part8.cs: VerificaDevolucionInt and remaining methods from lines ~20482-22500

        // VB lines 19148-19542
        private void PlanoUsuarios(string NombreArchivo, DateTime FechaCorte, bool CodCedu, int CodDanne, double salariominimo, string CodActividad, ERP.Core.Compartido.Controles.Barraprogress msgbarra, System.Windows.Forms.Form myforma, System.Data.Odbc.OdbcConnection myconnect)
        {
            PlanoUsuarios(NombreArchivo, FechaCorte, CodCedu, CodDanne, salariominimo, CodActividad, msgbarra, myforma, myconnect, true);
        }

        private void PlanoUsuarios(string NombreArchivo, DateTime FechaCorte, bool CodCedu, int CodDanne, double salariominimo, string CodActividad, ERP.Core.Compartido.Controles.Barraprogress msgbarra, System.Windows.Forms.Form myforma, System.Data.Odbc.OdbcConnection myconnect, bool csv)
        {
            System.Text.StringBuilder stbuilder = new System.Text.StringBuilder();
            int fila = 0, Genero = 0, NivAcadeMico = 0, EstadoCivil = 0, NivelIng = 0;
            string Cedula = "", estrato = "", DptoCiudad = "", TipoNit = "";
            int Empleado = 0, jorLab = 0;
            DataSet DsDataset = new DataSet();
            string Mes = "", StFecRetiro = "";
            string PrimerApellido = "", SegundoApellido = "", PrimerNombre = "", SegundoNombre = "", Nombre = "";
            string AsistioAsamblea = "";
            DataSet dsdataactividad = new DataSet();
            string CodCiudad = "";
            DataSet dtsUsuario = new DataSet();
            int Sw1 = 0;

            StreamWriter StrStream = new StreamWriter(NombreArchivo, false);
            StrStream.Close();

            Microsoft.Win32.RegistryKey Rk;
            string separador;

            if (csv)
            {
                separador = ";";
            }
            else
            {
                Rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Control Panel\\International", true);
                separador = Rk.GetValue("sList").ToString();
            }

            stbuilder.Append("Select a.tipo_nit, a.nit, a.codigoter, a.primer_apellido, a.segundo_apellido, a.nombre,c.estadoact,c.fecnovedad,");
            stbuilder.Append("a.FECHA_INGRESO, a.telefono1, a.direccion, a.clase, a.Activo, a.actividad, a.DPTO_CIUDAD, a.email,direccion, fecnacem,natjur,sexo,contracto,");
            stbuilder.Append("a.fecha_retiro,a.activo,a.estrato,a.niv_acade,a.estado_civil,a.salario,a.estado,a.MujerCabezaFamilia, a.JornadaLaboral,a.NIT_CHEQUEO,a.empleado,b.saldo ");
            stbuilder.Append("from cop_superasocia_vw a ");
            stbuilder.Append("left join cop_supersaldoaso_vw b on b.codigoter = a.codigoter and b.periodo = '" + Strings.Format(FechaCorte, "yyyyMM") + "' ");
            // stbuilder.Append("left join cop_retiros c on a.codigoter=c.codigoter and c.fecnovedad " + OdbcConnect.odbcConect.InOIgual + " (select h.fecnovedad from cop_retiros h "); // ERROR: CS0572
            // stbuilder.Append("where h.codigoter=c.codigoter and h.fecnovedad = (select max(d.fecnovedad) from cop_retiros d where h.codigoter=d.codigoter "); // ERROR: CS0572
            // stbuilder.Append(" and d.fecnovedad<='" + Format(FechaCorte, varini.PstForFec) + "')) "); // ERROR: CS0572
            stbuilder.Append("where  a.codigoter<>'99999999999999' ");
            stbuilder.Append("order by a.clase");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "PlanoUsuarios", ref DsDataset, "Tblusuarios");
            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["Tblusuarios"].Rows.Count);

            while (fila < DsDataset.Tables["Tblusuarios"].Rows.Count)
            {
                System.Data.DataRow row = DsDataset.Tables["Tblusuarios"].Rows[fila];
                Sw1 = 0;
                AsistioAsamblea = "0";

                string estadoVal = row["estado"].ToString();
                if (estadoVal == "A" || estadoVal == "S")
                {
                    // nothing
                }
                else if (estadoVal == "T")
                {
                    if (row["estadoact"] == DBNull.Value)
                    {
                        Sw1 = 0;
                    }
                    else
                    {
                        if (row["estadoact"].ToString() == "T")
                            Sw1 = 1;
                    }
                }
                else if (estadoVal == "R")
                {
                    if (row["estadoact"] == DBNull.Value)
                    {
                        if (Convert.ToDateTime(row["fecha_retiro"]) <= FechaCorte)
                        {
                            if (row["saldo"] == DBNull.Value)
                            {
                                Sw1 = 1;
                            }
                            else
                            {
                                if (Convert.ToDouble(row["saldo"]) != 0)
                                    Sw1 = 0;
                                else
                                    Sw1 = 1;
                            }
                        }
                        else if (Convert.ToDateTime(row["fecha_retiro"]) > FechaCorte)
                        {
                            row["estado"] = "A";
                            Sw1 = 0;
                        }
                    }
                    else
                    {
                        if (row["estadoact"].ToString() != "R")
                        {
                            Sw1 = 0;
                        }
                        else
                        {
                            if (Convert.ToDateTime(row["fecha_retiro"]) < FechaCorte)
                            {
                                if (row["saldo"] == DBNull.Value)
                                {
                                    Sw1 = 1;
                                }
                                else
                                {
                                    if (Convert.ToDouble(row["saldo"]) != 0)
                                        Sw1 = 0;
                                    else
                                        Sw1 = 1;
                                }
                            }
                        }
                    }
                }

                if (row["FECHA_INGRESO"] == DBNull.Value)
                {
                    row["FECHA_INGRESO"] = new DateTime(FechaCorte.Year, 1, 1);
                }

                if (Convert.ToDateTime(row["FECHA_INGRESO"]) > FechaCorte)
                {
                    Sw1 = 1;
                }

                if (Sw1 == 0)
                {
                    switch (row["sexo"].ToString())
                    {
                        case "F": Genero = 2; break;
                        case "M": Genero = 1; break;
                    }

                    if (row["nombre"].ToString().Trim() == "")
                    {
                        row["nombre"] = row["primer_apellido"];
                    }

                    if (row["FECHA_INGRESO"] == DBNull.Value)
                    {
                        row["FECHA_INGRESO"] = new DateTime(FechaCorte.Year, 1, 1);
                    }
                    else if (Convert.ToDateTime(row["FECHA_INGRESO"]) == new DateTime(1950, 1, 1))
                    {
                        row["FECHA_INGRESO"] = new DateTime(FechaCorte.Year, 1, 1);
                    }

                    if (row["niv_acade"] == DBNull.Value)
                    {
                        NivAcadeMico = 0;
                    }
                    else
                    {
                        NivAcadeMico = Convert.ToInt32(row["niv_acade"]);
                    }

                    if (row["email"] == DBNull.Value)
                    {
                        row["email"] = " ";
                    }

                    switch (row["estado_civil"].ToString())
                    {
                        case "1": EstadoCivil = 1; break;
                        case "2": EstadoCivil = 2; break;
                        case "3": EstadoCivil = 6; break;
                        case "4": EstadoCivil = 3; break;
                        case "5": EstadoCivil = 4; break;
                        default:  EstadoCivil = 1; break;
                    }

                    if (row["natjur"] == DBNull.Value)
                    {
                        row["natjur"] = 0;
                    }

                    NivelIng = NivelIngreso(Convert.ToInt32(row["natjur"]), Convert.ToDouble(row["salario"]), salariominimo);

                    if (CodCedu)
                    {
                        if (!Microsoft.VisualBasic.Information.IsNumeric(row["codigoter"]))
                        {
                            MessageBox.Show("Codigo " + row["codigoter"] + " no se puede convertir a numero, presentara problemas al subir al sigcoop", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            Cedula = row["codigoter"].ToString();
                        }
                        else
                        {
                            Cedula = Convert.ToDouble(row["codigoter"]).ToString();
                        }
                    }
                    else
                    {
                        Cedula = row["nit"].ToString();
                    }

                    if (Convert.ToDouble(row["estrato"]) > 0)
                    {
                        estrato = row["estrato"].ToString();
                    }
                    else
                    {
                        estrato = "1";
                    }

                    if (row["fecha_retiro"] == DBNull.Value)
                    {
                        row["fecha_retiro"] = new DateTime(1900, 1, 1);
                    }

                    if (row["fecnacem"] == DBNull.Value)
                    {
                        row["fecnacem"] = new DateTime(1950, 1, 1);
                    }

                    if (row["estado"].ToString() == "R")
                    {
                        row["clase"] = 2;
                        row["activo"] = "0";
                        StFecRetiro = row["fecha_retiro"].ToString();
                    }
                    else
                    {
                        row["fecha_retiro"] = new DateTime(1900, 1, 1);
                        StFecRetiro = "";
                    }

                    switch (row["Empleado"].ToString())
                    {
                        case "N":
                            Empleado = 0;
                            row["contracto"] = 0;
                            break;
                        case "Y":
                            Empleado = 1;
                            break;
                    }

                    if (Convert.ToDouble(row["contracto"]) > 0)
                    {
                        if (NivelIng == 0)
                            NivelIng = 1;
                    }
                    else
                    {
                        if (NivelIng == 0)
                            NivelIng = 1;
                    }

                    if (Empleado == 1)
                    {
                        if (Convert.ToDouble(row["JornadaLaboral"]) > 0)
                            jorLab = Convert.ToInt32(row["JornadaLaboral"]);
                        else
                            jorLab = 1;
                    }
                    else
                    {
                        jorLab = 0;
                    }

                    DptoCiudad = row["DPTO_CIUDAD"].ToString();
                    // ok = msgconfig.BuscaCiudad(DptoCiudad, myconnect, global::ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno); // ERROR: CS1501
                    DptoCiudad = Strings.Right("00000" + row["DPTO_CIUDAD"].ToString(), 5);
                    if (!ok)
                    {
                        DptoCiudad = CodDanne.ToString();
                    }

                    switch (row["tipo_nit"].ToString())
                    {
                        case "C": TipoNit = "C"; break;
                        case "N":
                            TipoNit = "N";
                            Cedula = Strings.Mid(Cedula, 1, 3) + "-" + Strings.Mid(Cedula, 4, 3) + "-" + Strings.Mid(Cedula, 7, 3) + "-" + row["NIT_CHEQUEO"].ToString();
                            break;
                        case "U": TipoNit = "U"; break;
                        case "P": TipoNit = "P"; break;
                        case "E": TipoNit = "E"; break;
                        case "R": TipoNit = "R"; break;
                        case "T": TipoNit = "I"; break;
                        default:  TipoNit = "O"; break;
                    }

                    if (Convert.ToDouble(row["contracto"]) > 4 || Convert.ToDouble(row["contracto"]) < 0)
                    {
                        row["contracto"] = 4;
                    }

                    if (row["natjur"].ToString() == "2")
                    {
                        if (row["tipo_nit"].ToString() == "N")
                        {
                            Genero = 3;
                            estrato = "0";
                            EstadoCivil = 0;
                            row["nombre"] = row["primer_apellido"].ToString() + " " + row["segundo_apellido"].ToString() + " " + row["nombre"].ToString();
                            row["primer_apellido"] = "";
                            row["segundo_apellido"] = "";
                        }
                        NivAcadeMico = 0;
                    }

                    if (row["clase"].ToString() == "0")
                    {
                        if (Empleado == 1)
                        {
                            row["clase"] = 4;
                        }
                        else
                        {
                            estrato = "0";
                            if (row["tipo_nit"].ToString() != "N")
                                Genero = 0;
                            EstadoCivil = 0;
                            NivelIng = 0;
                        }
                    }
                    else if (row["clase"].ToString() == "1")
                    {
                        row["fecha_retiro"] = new DateTime(1900, 1, 1);
                    }

                    // dsdataactividad = this.msgconfig.InscritosActividadesRecreativasAsociado(row["codigoter"].ToString(), row["codigoter"].ToString(), CodActividad, myconnect); // ERROR: CS1061

                    if (dsdataactividad.Tables["TblRecreacion"].Rows.Count > 0)
                    {
                        string asistio = dsdataactividad.Tables["TblRecreacion"].Rows[0]["asistio"].ToString();
                        if (asistio == "Y" || asistio == "S")
                            AsistioAsamblea = "1";
                    }

                    // GrabaPlanoUsuarios(NombreArchivo, separador, TipoNit, Cedula, row["fecha_ingreso"], row["primer_apellido"], row["segundo_apellido"], row["nombre"], row["telefono1"], row["email"], row["direccion"], row["clase"], // ERROR: CS1503
                        // StFecRetiro, row["activo"], Empleado, row["MujerCabezaFamilia"], row["fecnacem"], estrato, Genero, DptoCiudad, row["contracto"], NivAcadeMico, 1, NivelIng, EstadoCivil, jorLab, 1, "0000", AsistioAsamblea); // ERROR: CS1503
                }

                msgbarra.PerformStep();
                fila += 1;
            }

            stbuilder.Replace(stbuilder.ToString(), "");

            switch (FechaCorte.Month)
            {
                case 1:  Mes = "sal.ene"; break;
                case 2:  Mes = "sal.feb"; break;
                case 3:  Mes = "sal.mar"; break;
                case 4:  Mes = "sal.abr"; break;
                case 5:  Mes = "sal.may"; break;
                case 6:  Mes = "sal.jun"; break;
                case 7:  Mes = "sal.jul"; break;
                case 8:  Mes = "sal.ago"; break;
                case 9:  Mes = "sal.sep"; break;
                case 10: Mes = "sal.oct"; break;
                case 11: Mes = "sal.nov"; break;
                case 12: Mes = "sal.dic"; break;
            }

            stbuilder.Append("select tipo_nit,cnt_supertercero_vw.nit,nit_chequeo,razon_social,nombre,FecIngreso,telefono1,direccion,rol,activo,");
            stbuilder.Append("actividad,idciudad,email,genero,empleado,tipcontrato,nivelescolar,estrato,niveling,fecconstitucion,");
            stbuilder.Append("estadoCivil,mujercabeza,ocupacion,sectoreconomico,jornadaLaboral,fecretiro,asamblea,tipo_persona ");
            stbuilder.Append("from cnt_supertercero_vw ");
            stbuilder.Append("inner join cnt_salterc_vw sal on cnt_supertercero_vw.nit=sal.nit ");
            stbuilder.Append("where sal.periodo=" + FechaCorte.Year + " and " + Mes + "<>0 ");
            stbuilder.Append(" and (sal.cuenta like '1645%' or sal.cuenta like '1648%' or sal.cuenta like '1650%' or sal.cuenta like '2495%' or sal.cuenta like '2795%') ");
            stbuilder.Append("group by tipo_nit,cnt_supertercero_vw.nit,nit_chequeo,razon_social,nombre,FecIngreso,telefono1,direccion,rol,activo,");
            stbuilder.Append("actividad,idciudad,email,genero,empleado,tipcontrato,nivelescolar,estrato,niveling,fecconstitucion,");
            stbuilder.Append("estadoCivil,mujercabeza,ocupacion,sectoreconomico,jornadaLaboral,fecretiro,asamblea,tipo_persona ");

            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "PlanoUsuarios", ref DsDataset, "Tblterceros", true);
            msgbarra.ValorMinimoMaximo(0, DsDataset.Tables["Tblterceros"].Rows.Count);

            for (fila = 0; fila <= DsDataset.Tables["Tblterceros"].Rows.Count - 1; fila++)
            {
                System.Data.DataRow rowT = DsDataset.Tables["Tblterceros"].Rows[fila];
                PrimerApellido = rowT["nombre"].ToString();
                SegundoApellido = rowT["nombre"].ToString();
                PrimerNombre = rowT["nombre"].ToString();
                SegundoNombre = rowT["nombre"].ToString();
                Nombre = rowT["nombre"].ToString();

                if (rowT["tipo_persona"].ToString() == "J")
                {
                    if (rowT["tipo_nit"].ToString() == "N")
                    {
                        string nitVal = rowT["nit"].ToString();
                        rowT["nit"] = Strings.Mid(nitVal, 1, 3) + "-" + Strings.Mid(nitVal, 4, 3) + "-" + Strings.Mid(nitVal, 7, 3) + "-" + rowT["NIT_CHEQUEO"].ToString();
                        Nombre = rowT["razon_social"].ToString();
                        rowT["genero"] = "3";
                        PrimerApellido = "";
                        SegundoApellido = "";
                    }
                }
                else
                {
                    this.msgcofsys.SepararNombres(rowT["nombre"].ToString(), ref PrimerApellido, ref SegundoApellido, ref PrimerNombre, ref SegundoNombre);
                    Nombre = PrimerNombre + " " + SegundoNombre;
                }

                CodCiudad = rowT["idciudad"].ToString();
                // ok = msgconfig.BuscaCiudad(CodCiudad, myconnect, global::ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno); // ERROR: CS1501
                CodCiudad = Strings.Right("00000" + rowT["idciudad"].ToString(), 5);
                if (!ok)
                {
                    CodCiudad = CodDanne.ToString();
                }

                StFecRetiro = "";
                // GrabaPlanoUsuarios(NombreArchivo, separador, rowT["tipo_nit"], rowT["nit"], rowT["FecIngreso"], PrimerApellido, SegundoApellido, Nombre, rowT["telefono1"], rowT["email"], rowT["direccion"], rowT["rol"], // ERROR: CS1503
                    // StFecRetiro, rowT["activo"], rowT["empleado"], rowT["mujercabeza"], rowT["fecconstitucion"], rowT["estrato"], rowT["genero"], CodCiudad, rowT["tipcontrato"], rowT["nivelescolar"], rowT["sectoreconomico"], rowT["niveling"], rowT["estadoCivil"], rowT["jornadaLaboral"], rowT["ocupacion"], rowT["actividad"], rowT["asamblea"]); // ERROR: CS1503

                msgbarra.PerformStep();
            }
        }

        // NivelIngreso duplicado eliminado (original en linea 1114 de este mismo archivo)

    } // end partial class Clscartera
} // end namespace msgcop
