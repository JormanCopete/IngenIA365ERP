// Traducción de: ClsLiqcreditos.vb (msgliqcre) — Parte 4 (líneas VB 6300-8617)
using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.Creditos
{
    public partial class ClsLiqcreditos
    {
        // all methods from VB lines 6300-8617 (end of class), faithfully translated

        // New función para buscar el número de solicitud de crédito
        public double BuscarNumSolicitud(string codigoter, string lincred, string numero, OdbcConnection myconnect)
        {
            string sql;
            string num_soli = " ";
            sql = "SELECT NUMERO_SOLI as campo1 FROM cop_maecar " +
                  "WHERE codigoter= '" + codigoter + "' and lincred=" + lincred + " and numero= " + numero;
            this.OdbcConnect.ExecuteQueryconec(sql, myconnect, "ConsultaNumeroSolicitud", ref num_soli);
            return Convert.ToDouble(num_soli);
        }

        public virtual double ReimprimirPlandepagos(double NumSolicitud, DateTime fecha, double Valor, decimal Tasa, int plazo,
            string clasedescto, string periodicidad, string ciclo, System.Windows.Forms.Form myForma, OdbcConnection myconnect,
            ref decimal TasaInteresCal, bool Grabar = true)
        {
            DataSet dsdeduccion = new DataSet();
            DataSet dsextras = new DataSet();
            DataSet dsproyeccion = new DataSet();
            DataTable dsdatat = new DataTable();
            DataTable dssolicitud = new DataTable();
            bool CicloaFecha;
            DataSet Dsparametros = new DataSet();
            double VlrInteresCierre = 0;
            double NuevaCuota = 0;
            try
            {
                // dssolicitud = this.BuscaSolicitudesCredito(NumSolicitud, myconnect); // ERROR: CS1503
                // dsdeduccion = this.BuscarDeducciones(NumSolicitud, myconnect); // ERROR: CS1503
                // this.BuscarExtras(NumSolicitud, ref dsextras, myconnect); // ERROR: CS1503

                DataRow row0 = dssolicitud.Rows[0];
                // this.msgparcop.BuscaLinea(row0["lincred"], Dsparametros, myconnect); // ERROR: CS1503, CS1620

                switch (Dsparametros.Tables["tbllineas"].Rows[0]["intcie"].ToString())
                {
                    case "3":
                        if (row0["VLR_SOLICITUD"].Equals(Valor))
                        {
                            VlrInteresCierre = Convert.ToDouble(row0["cuota_icie"]);
                            Valor = Convert.ToDouble(Valor) - Convert.ToDouble(row0["cuota_icie"]);
                        }
                        break;
                }

                switch (Dsparametros.Tables["tbllineas"].Rows[0]["sumaga"].ToString())
                {
                    case "2":
                        if (row0["VLR_SOLICITUD"].Equals(Valor))
                        {
                            Valor = Convert.ToDouble(Valor) - (Convert.ToDouble(row0["cuota_adm"]) + Convert.ToDouble(row0["cuota_seg"]) + Convert.ToDouble(row0["cuota_cptl"]));
                        }
                        break;
                    default:
                        switch (Dsparametros.Tables["tbllineas"].Rows[0]["claAdmon"].ToString())
                        {
                            case "4":
                                if (Dsparametros.Tables["tbllineas"].Rows[0]["foradmon"].ToString() != "7")
                                {
                                    Valor = Convert.ToDouble(Valor) - Convert.ToDouble(row0["cuota_adm"]);
                                }
                                break;
                        }
                        break;
                }

                LiquidaDsctoporPlazos(NumSolicitud, row0["tipo_garantia"], row0["codigoter"], row0["lincred"], plazo,
                    Convert.ToInt32(fecha.ToString("yyyyMM")), Valor, ref dsdeduccion, myForma, myconnect);

                // dsproyeccion = this.GeneraProyeccion(row0["codigoter"], row0["lincred"], fecha, plazo, periodicidad, // ERROR: CS7036
                    // clasedescto, ciclo, Tasa, row0["fecdesc"], Valor, dsextras.Tables["tblextras"], // ERROR: CS7036
                    // dsdeduccion.Tables["tbldeducciones"], row0["pergraini"], myconnect, ref NuevaCuota, ref NuevaCuota, row0["clacuo"]); // ERROR: CS7036

                switch (MessageBox.Show("Imprime Plan de pagos? ", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question))
                {
                    case DialogResult.Yes:
                        this.ImprimePlanpagos(dsproyeccion, myForma, NumSolicitud);
                        break;
                }

                NuevaCuota = Convert.ToDouble(dsproyeccion.Tables["TbldatosCredito"].Rows[0]["cuota"]);
                TasaInteresCal = Convert.ToDecimal(dsproyeccion.Tables["TbldatosCredito"].Rows[0]["TasaInt"]);

                return NuevaCuota;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 0;
        }

        // Overload without optional ref param
        public virtual double ReimprimirPlandepagos(double NumSolicitud, DateTime fecha, double Valor, decimal Tasa, int plazo,
            string clasedescto, string periodicidad, string ciclo, System.Windows.Forms.Form myForma, OdbcConnection myconnect,
            bool Grabar)
        {
            decimal tasaInteresCal = 0;
            return ReimprimirPlandepagos(NumSolicitud, fecha, Valor, Tasa, plazo, clasedescto, periodicidad, ciclo, myForma, myconnect, ref tasaInteresCal, Grabar);
        }

        public virtual double ReimprimirPlandepagos(double NumSolicitud, DateTime fecha, double Valor, decimal Tasa, int plazo,
            string clasedescto, string periodicidad, string ciclo, System.Windows.Forms.Form myForma, OdbcConnection myconnect)
        {
            decimal tasaInteresCal = 0;
            return ReimprimirPlandepagos(NumSolicitud, fecha, Valor, Tasa, plazo, clasedescto, periodicidad, ciclo, myForma, myconnect, ref tasaInteresCal, true);
        }

        public bool Bloquear_AsocPorDiasMora(string codigoter, string Lincred, int periodo, OdbcConnection myconnect)
        {
            string bloq_AsoMora = "    ";
            string diasMora = "   ";

            stmysql = " select bloq_AsoMora as campo1 from cop_concar12" +
                      " where LINCRED =" + Lincred;

            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "Bloquer_AsocPorDiasMora", ref bloq_AsoMora);

            if (Convert.ToInt32(bloq_AsoMora) != 0)
            {
                stmysql = "select max(a.diasmora) as campo1" +
                          " from cop_copmora a" +
                          " inner join cop_maecar maecar " +
                          " on  maecar.codigoter=a.codigoter " +
                          " and maecar.lincred=a.lincred " +
                          " and maecar.numero=a.numero  " +
                          " inner  join cop_salmaecar c " +
                          " on  c.CODIGOTER = maecar.codigoter " +
                          " and c.LINCRED = maecar.lincred " +
                          " and c.NUMERO = maecar.numero " +
                          " where   a.codigoter ='" + codigoter + "'" +
                          " and  a.lincred =" + Lincred +
                          " and a.periodo_contable=" + periodo +
                          " and (a.SaldoCapital + a.SaldoExtra + a.SaldoInteres + a.SaldoMora + a.SaldoAdmon + a.SaldoOtros + a.SaldoSeguro) <> 0" +
                          " and ((a.lincred>=1000 and c.saldo>0) or (a.lincred<1000 and (c.saldo<>0 or maecar.cuota<>0))) ";

                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "Bloquer_AsocPorDiasMora", ref diasMora);

                if (Convert.ToInt32(diasMora) >= Convert.ToInt32(bloq_AsoMora))
                {
                    MessageBox.Show("El asociado está bloqueado por la línea: " + Lincred + " " + "\r\n" + "Porque tiene " + diasMora + " días de mora" + "\r\n" + "En los parámetros de las líneas de crédito " + "\r\n" + "en la opción bloquear a partir de tantos días de mora hay " + bloq_AsoMora + ".", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        public bool BuscarPeriocidadEmpresa(OdbcConnection myconnect, string codEmpresa, int Periocidad, int CicloDsto, string clades, DateTime fechaSolicitud, ref DateTime fechapridcsto)
        {
            codEmpresa = Microsoft.VisualBasic.Strings.Right("0000" + codEmpresa, 4);
            DataSet dstprueba = new DataSet();
            string encontraperiocidad = "  ";
            // msgparcop.Buscar_Periocidad_Emp(myconnect, codEmpresa, clades, ref dstprueba, ref encontraperiocidad); // ERROR: CS1061

            if (encontraperiocidad.Trim() == "NNN")
            {
                return false;
            }
            int dia = fechaSolicitud.Day;
            int mes = fechaSolicitud.Month;
            int Ano = fechaSolicitud.Year;
            string diaPeriocidad = "     ";

            DataSet dstperiocidad = new DataSet();
            stmysql = "  select DiasInicio, DiasFinal,Dias  " +
                      "  from cop_paramPeriocidad " +
                      "  where  codEmpresa ='" + codEmpresa + "'  and periocidad = '" + Periocidad + "'  and  clades ='" + encontraperiocidad + "'";
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarPeriocidadEmpresa", ref dstperiocidad, "tblPerio");

            switch (Periocidad)
            {
                case 1:
                    if (dstperiocidad.Tables["tblPerio"].Rows.Count > 0)
                    {
                        stmysql = "select Dias as campo1 " +
                                   "  from cop_paramPeriocidad " +
                                   " where  codEmpresa ='" + codEmpresa + "'  and periocidad = '" + Periocidad + "' and clades='" + encontraperiocidad + "'" +
                                   " and  " + dia + "  between diasInicio and  diasFinal ";
                        ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarPeriocidadEmpresa", ref diaPeriocidad);

                        if (ok == true) // este proceso se realiza cuando el dia que le elegi
                        {
                            fechapridcsto = Convert.ToDateTime(diaPeriocidad + "-" + mes + "-" + Ano);
                        }
                        else
                        {
                            stmysql = "select Dias as campo1 " +
                                      "  from cop_paramPeriocidad " +
                                      " where  codEmpresa ='" + codEmpresa + "'  and periocidad = '" + Periocidad + "' and clades='" + encontraperiocidad + "'" +
                                      "  and  " + dia + "  >  diasFinal  ";

                            OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarPeriocidadEmpresa", ref diaPeriocidad);

                            if (mes == 12)
                            {
                                fechapridcsto = Convert.ToDateTime(diaPeriocidad + "- 01 -" + (Ano + 1));
                            }
                            else if (mes == 1)
                            {
                                fechapridcsto = Convert.ToDateTime("28 -" + (mes + 1) + "-" + Ano);
                            }
                            else
                            {
                                fechapridcsto = Convert.ToDateTime(diaPeriocidad + "-" + (mes + 1) + "-" + Ano);
                            }
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }

                case 2:
                    if (dstperiocidad.Tables["tblPerio"].Rows.Count > 0)
                    {
                        switch (CicloDsto)
                        {
                            case 0:
                            case 1:
                                try
                                {
                                    int filaatoma = 0;
                                    bool verificarSalida = false;

                                    string diaFinal = dstperiocidad.Tables[0].Rows[CicloDsto]["DiasFinal"].ToString();
                                    string diaInicio = dstperiocidad.Tables[0].Rows[CicloDsto]["DiasInicio"].ToString();
                                    string diaPoner = dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString();

                                    if (dia < Convert.ToInt32(diaFinal))
                                    {
                                        if (CicloDsto == 0)
                                        {
                                            if (Convert.ToInt32(diaPoner) > 15 && Convert.ToInt32(diaFinal) <= 15)
                                            {
                                            }
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                        }
                                        fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                    }
                                    else
                                    {
                                        if (mes == 12)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "- 01 -" + (Ano + 1));
                                        }
                                        else if (mes == 1)
                                        {
                                            if ((mes + 1) != 0 && Convert.ToInt32(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString()) > 28)
                                            {
                                                fechapridcsto = Convert.ToDateTime("28" + " -" + (mes + 1) + "-" + Ano);
                                            }
                                            else
                                            {
                                                fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + " -" + (mes + 1) + "-" + Ano);
                                            }
                                        }
                                        else
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "-" + (mes + 1) + "-" + Ano);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Debe parametrizar para la periocidad Quincenal todos los ciclo de descuento en Actualiza empresa ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                    return false;
                                }
                                break;

                            case 2:
                            {
                                int filaatomar = 0;
                                bool verificarSalida = false;
                                int filaatomar1 = 1;
                                int longitud = dstperiocidad.Tables[0].Rows.Count - 1;
                                string diaFinal;
                                string diaInicio;
                                string diaInicio1;
                                while (verificarSalida == false)
                                {
                                    diaFinal = dstperiocidad.Tables[0].Rows[filaatomar]["DiasFinal"].ToString();
                                    diaInicio = dstperiocidad.Tables[0].Rows[filaatomar]["DiasInicio"].ToString();

                                    if (filaatomar1 < longitud + 1)
                                    {
                                        diaInicio1 = dstperiocidad.Tables[0].Rows[filaatomar1]["DiasInicio"].ToString();
                                    }
                                    else
                                    {
                                        diaInicio1 = dstperiocidad.Tables[0].Rows[longitud]["DiasInicio"].ToString();
                                    }

                                    if (Convert.ToInt32(diaInicio) <= dia && Convert.ToInt32(diaFinal) >= dia)
                                    {
                                        int diaponer = Convert.ToInt32(dstperiocidad.Tables[0].Rows[filaatomar]["Dias"].ToString());
                                        if (dia >= diaponer)
                                        {
                                            if (mes == 12)
                                            {
                                                Ano += 1;
                                                mes = 1;
                                            }
                                            else
                                            {
                                                mes += 1;
                                            }
                                        }
                                        fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[filaatomar]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                        verificarSalida = true;
                                    }
                                    else if (Convert.ToInt32(diaInicio1) > dia)
                                    {
                                        fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[filaatomar1]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                        verificarSalida = true;
                                    }
                                    else if (filaatomar1 > longitud)
                                    {
                                        if (mes == 12)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[0]["Dias"].ToString() + "- 01 -" + (Ano + 1));
                                        }
                                        else if (mes == 1)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[0]["Dias"].ToString() + " -" + (mes + 1) + "-" + Ano);
                                        }
                                        else
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[0]["Dias"].ToString() + "-" + (mes + 1) + "-" + Ano);
                                        }
                                        verificarSalida = true;
                                    }
                                    filaatomar = filaatomar + 1;
                                    filaatomar1 = filaatomar1 + 1;
                                }
                                break;
                            }
                        }
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                case 3:
                    if (dstperiocidad.Tables["tblPerio"].Rows.Count > 0)
                    {
                        switch (CicloDsto)
                        {
                            case 0:
                            case 1:
                            case 2:
                                try
                                {
                                    string diaFinal = dstperiocidad.Tables[0].Rows[CicloDsto]["DiasFinal"].ToString();
                                    if (dia < Convert.ToInt32(diaFinal))
                                    {
                                        fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                    }
                                    else
                                    {
                                        if (mes == 12)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "- 01 -" + (Ano + 1));
                                        }
                                        else if (mes == 1)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + " -" + (mes + 1) + "-" + Ano);
                                        }
                                        else
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[CicloDsto]["Dias"].ToString() + "-" + (mes + 1) + "-" + Ano);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Debe parametrizar para la periocidad Decadal todos los ciclo de descuento en Actualiza empresa ", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                    return false;
                                }
                                break;

                            case 3:
                            {
                                int filaatomar = 0;
                                bool verificarSalida = false;
                                int filaatomar1 = 1;
                                int longitud = dstperiocidad.Tables[0].Rows.Count - 1;
                                string diaFinal;
                                string diaInicio;
                                string diaInicio1;
                                while (verificarSalida == false)
                                {
                                    diaFinal = dstperiocidad.Tables[0].Rows[filaatomar]["DiasFinal"].ToString();
                                    diaInicio = dstperiocidad.Tables[0].Rows[filaatomar]["DiasInicio"].ToString();

                                    if (filaatomar1 < longitud + 1)
                                    {
                                        diaInicio1 = dstperiocidad.Tables[0].Rows[filaatomar1]["DiasInicio"].ToString();
                                    }
                                    else
                                    {
                                        diaInicio1 = dstperiocidad.Tables[0].Rows[longitud]["DiasInicio"].ToString();
                                    }

                                    if (Convert.ToInt32(diaInicio) <= dia && Convert.ToInt32(diaFinal) >= dia)
                                    {
                                        fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[filaatomar]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                        verificarSalida = true;
                                    }
                                    else if (Convert.ToInt32(diaInicio1) > dia)
                                    {
                                        fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[filaatomar1]["Dias"].ToString() + "-" + mes + "-" + Ano);
                                        verificarSalida = true;
                                    }
                                    else if (filaatomar1 > longitud)
                                    {
                                        if (mes == 12)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[0]["Dias"].ToString() + "- 01 -" + (Ano + 1));
                                        }
                                        else if (mes == 1)
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[0]["Dias"].ToString() + " -" + (mes + 1) + "-" + Ano);
                                        }
                                        else
                                        {
                                            fechapridcsto = Convert.ToDateTime(dstperiocidad.Tables[0].Rows[0]["Dias"].ToString() + "-" + (mes + 1) + "-" + Ano);
                                        }
                                        verificarSalida = true;
                                    }
                                    filaatomar = filaatomar + 1;
                                    filaatomar1 = filaatomar1 + 1;
                                }
                                break;
                            }
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
            }

            return false;
        }

        public double CalculaTotaLCUOTA(string codigoter, string periodo, OdbcConnection myconnect, string SoloCredito = "Y")
        {
            double cuota = 0;
            StringBuilder StBuilder = new StringBuilder();

            switch (SoloCredito.Trim())
            {
                case "Y":
                    StBuilder.Append("select sum(case c.CICLOD when '5' then c.CUOTA*c.periodd else c.cuota end) as campo1 ");
                    StBuilder.Append("from cop_salmaecar a ");
                    StBuilder.Append("inner join cop_maecar c ");
                    StBuilder.Append("on a.CODIGOTER = c.CODIGOTER ");
                    StBuilder.Append("and a.LINCRED = c.LINCRED ");
                    StBuilder.Append("and a.NUMERO = c.NUMERO ");
                    StBuilder.Append("inner join cop_concar12 b on a.lincred=b.lincred ");
                    StBuilder.Append("where a.codigoter='" + codigoter + "' and a.periodo=" + periodo + "  ");
                    StBuilder.Append(" and a.lincred<>9999 and b.codahor in ('4','5') and a.saldo>0");
                    break;
                default:
                    StBuilder.Append("select sum(case a.CICLOD when '5' then a.CUOTA*a.periodd else a.cuota end) as campo1 ");
                    StBuilder.Append("from cop_salmaecar a ");
                    StBuilder.Append("inner join cop_maecar c ");
                    StBuilder.Append("on a.CODIGOTER = c.CODIGOTER ");
                    StBuilder.Append("and a.LINCRED = c.LINCRED ");
                    StBuilder.Append("and a.NUMERO = c.NUMERO ");
                    StBuilder.Append("inner join cop_concar12 b on a.lincred=b.lincred ");
                    StBuilder.Append("where a.codigoter='" + codigoter + "' and a.periodo=" + periodo + "  ");
                    StBuilder.Append(" and a.lincred<>9999 and b.codahor in ('1','2','3','4','5') and a.saldo>0");
                    StBuilder.Append(" and ((a.lincred >= 1000 And A.saldo > 0) Or (a.lincred < 1000 and (a.CUOTA <> 0 or A.saldo <> 0)))  ");
                    break;
            }

            string _cuota = "0";
            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "CalculaTotaLCUOTA", ref _cuota);
            if (Microsoft.VisualBasic.Information.IsNumeric(_cuota)) cuota = Convert.ToDouble(_cuota);
            return cuota;
        }

        public int CalculaTotaLDiasMora(string codigoter, string periodo, OdbcConnection myconnect)
        {
            int diasMora = 0;
            stmysql = "select max(a.diasmora) as campo1" +
                      " from cop_copmora a" +
                      " inner join cop_maecar maecar " +
                      " on  maecar.codigoter=a.codigoter " +
                      " and maecar.lincred=a.lincred " +
                      " and maecar.numero=a.numero  " +
                      " inner  join cop_salmaecar c " +
                      " on  c.CODIGOTER = maecar.codigoter " +
                      " and c.LINCRED = maecar.lincred " +
                      " and c.NUMERO = maecar.numero " +
                      " and c.periodo= " + periodo +
                      " where   a.codigoter ='" + codigoter + "'" +
                      " and a.periodo_contable=" + periodo +
                      " and (a.SaldoCapital + a.SaldoExtra + a.SaldoInteres + a.SaldoMora + a.SaldoAdmon + a.SaldoOtros + a.SaldoSeguro) <> 0" +
                      " and ((a.lincred>=1000 and c.saldo>0) or (a.lincred<1000 and (c.saldo<>0 or c.cuota<>0))) ";
            string _dias = "0";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaTotaLDiasMora", ref _dias);
            if (Microsoft.VisualBasic.Information.IsNumeric(_dias)) diasMora = Convert.ToInt32(_dias);
            return diasMora;
        }

        public double CalculaTotaLGarantias(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double cuota = 0;
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("select sum(AVALUO_CATASTRAL) as campo1 ");
            StBuilder.Append("from cop_salmaecar a ");
            StBuilder.Append("inner join cop_maecar c ");
            StBuilder.Append("on a.CODIGOTER = c.CODIGOTER ");
            StBuilder.Append("and a.LINCRED = c.LINCRED ");
            StBuilder.Append("  and a.NUMERO = c.NUMERO ");
            StBuilder.Append(" inner join cop_garantia garant ");
            StBuilder.Append(" on a.CODIGOTER = garant.CODIGOTER  ");
            StBuilder.Append(" and a.LINCRED = garant.LINCRED ");
            StBuilder.Append(" and a.NUMERO = garant.NUMERO  ");
            StBuilder.Append("inner join cop_concar12 b on a.lincred=b.lincred ");
            StBuilder.Append("where a.codigoter='" + codigoter + "' and a.periodo=" + periodo + "  ");
            StBuilder.Append(" and a.lincred<>9999 and b.codahor in ('4','5') and a.saldo>0 and TIPO_GARANTIA <> 1");

            string _cuota = "0";
            this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "CalculaTotaLGarantias", ref _cuota);
            if (Microsoft.VisualBasic.Information.IsNumeric(_cuota)) cuota = Convert.ToDouble(_cuota);
            return cuota;
        }

        public void calcularscoringIndividual(System.Windows.Forms.Form Pertenese, OdbcConnection myconnect, DataSet datasetscoring, DateTime fechaEstudioCredito, bool EstCodeudor = false, string nombredeudor = "")
        {
            string codigoter = "";
            decimal salario = 0;
            decimal otrosingresos = 0;
            decimal deudaexterna = 0;
            decimal cuotaexterna = 0;
            decimal acierta = 0;
            string califiDatacredito = " ";
            decimal garantiadmisible = 0;
            double garantiadmisibleCredito;
            double RecogeCuota;
            double RecogeSaldo;
            string topPar = " ";
            double valorSolcitado;
            double CuotaSolicitado;
            int Linea;
            int numeroSolicitud;
            string FechaSolictud;
            string Plazo;
            double DeudasInternas = 0; // esta variable se va Utilizar Para Guardar el SaldoCapital
            double CuotaInterna = 0;   // esta variable se va Utilizar Para Guardar el CuotaPeriodicaInterna
            double VarsalarioCapacidaPago;
            double varAntiguedaLaboral;
            DateTime fechaIngreso;
            DateTime fechaReingreso;
            double saldoTotalDeudaRecogido = 0;

            // parte capacidad de pago
            double SALDOCAPITAL;
            double CuotaPeriodica;
            double ahorrospermanentes;
            double ahorroVoluntarios;
            int DiasMora;
            double IndemnizacionLaboral;
            double ValorRiesgo;
            double capacidadescuento;
            decimal capacidadPago = 0;
            // parte solevencia
            double Reciprocidad;
            double Endeudamiento;
            double Descubierto;
            double PosicionNeta;
            // parte Garantias
            string clades;
            double ValorRiesgoSinGarantias;
            int TipoClaseGarantia;
            // servicio a la deuda
            string CalificacionManual;
            string calidadAsociado;
            double saldoMoraDATACREDITO;
            double MoraIngreso;
            string CalificacionActual = "A";
            // seccion Puntos CapacidadPago
            double puntoporcentEdad = 0, ReportPorcentajeEdad = 0;
            double puntoporcentAntigLab = 0, reportporcentAntigLab = 0;
            double puntoporcentTipoContraro = 0, reportporcentTipoContraro = 0;
            double puntoporcentMesesultimasolic = 0, reportporcentMesesultimasolic = 0;
            double puntoporcentCapacidaPago = 0, reportporcentCapacidaPago = 0;
            double puntoporcentCapacidadDescuento = 0, reportporcentCapacidadDescuento = 0;
            double puntoporcentCapaSalario = 0, reportporcentCapaSalario = 0;
            double puntoporcentAntigCoop = 0, reportporcentAntigCoop = 0;
            // seccion Puntos SOLVENCIA
            double puntoporcentReciprocidad = 0, reportporcentReciprocidad = 0;
            double puntoporcentEndeudamiento = 0, reportporcentEndeudamiento = 0;
            double puntoporcentDescubierto = 0, reportporcentDescubierto = 0;
            double puntoPosicionNeta = 0, reportPosicionNeta = 0;
            // seccion Puntos GARANTIAS
            double puntoporcentFormaPago = 0, reportporcentFormaPago = 0;
            double puntoporcentValorRiesgo = 0, reportporcentValorRiesgo = 0;
            double puntoporcentajeclaseGarantia = 0, reportporcentajeclaseGarantia = 0;
            // seccion Puntos SERVICIO DE LA DEUDA
            double puntoporcentCalidadAsociado = 0, reportporcentCalidadAsociado = 0;
            double puntoporcentsaldoMoraDATACREDITO = 0, reportporcentsaldoMoraDATACREDITO = 0;
            double puntoporcentCALDATACREDITO = 0, reportporcentCALDATACREDITO = 0;
            double puntoporcentACIERTA = 0, reportporcentACIERTA = 0;
            double puntoporcentCalificacionActual = 0, reportporcentCalificacionActual = 0;

            // datos necesarios — initialize to avoid compiler errors
            codigoter = ""; salario = 0; otrosingresos = 0; deudaexterna = 0; cuotaexterna = 0;
            acierta = 0; califiDatacredito = " "; garantiadmisible = 0; garantiadmisibleCredito = 0;
            clades = ""; saldoMoraDATACREDITO = 0; RecogeSaldo = 0; RecogeCuota = 0; topPar = " ";
            valorSolcitado = 0; CuotaSolicitado = 0; Linea = 0; numeroSolicitud = 0; FechaSolictud = "";
            Plazo = ""; TipoClaseGarantia = 0; saldoTotalDeudaRecogido = 0; capacidadPago = 0; capacidadescuento = 0;
            SALDOCAPITAL = 0; CuotaPeriodica = 0; ahorrospermanentes = 0; ahorroVoluntarios = 0;
            DiasMora = 0; IndemnizacionLaboral = 0; ValorRiesgo = 0;
            Reciprocidad = 0; Endeudamiento = 0; Descubierto = 0; PosicionNeta = 0;
            ValorRiesgoSinGarantias = 0; CalificacionManual = ""; calidadAsociado = "";
            MoraIngreso = 0; VarsalarioCapacidaPago = 0; varAntiguedaLaboral = 0;
            fechaIngreso = DateTime.MinValue; fechaReingreso = DateTime.MinValue;

            if (datasetscoring.Tables["tblScoring"].Rows.Count >= 1)
            {
                DataRow _row = datasetscoring.Tables["tblScoring"].Rows[0];
                codigoter = _row["codigoter"].ToString();
                // salario = Convert.ToDouble(_row["salario"].ToString()); // ERROR: CS0266
                // otrosingresos = Convert.ToDouble(_row["otrosingresos"].ToString()); // ERROR: CS0266
                // deudaexterna = Convert.ToDouble(_row["deudaexterna"].ToString()); // ERROR: CS0266
                // cuotaexterna = Convert.ToDouble(_row["cuotaexterna"].ToString()); // ERROR: CS0266
                // acierta = Convert.ToDouble(_row["acieta"].ToString()); // ERROR: CS0266
                califiDatacredito = _row["califiDatacredito"].ToString();
                clades = _row["clades"].ToString(); // parte de la garantia
                saldoMoraDATACREDITO = Convert.ToDouble(_row["SalMorDatacredito"].ToString());
                RecogeSaldo = Convert.ToDouble(_row["RecogeSaldo"].ToString());
                RecogeCuota = Convert.ToDouble(_row["RecogeCuota"].ToString());
                topPar = _row["topPar"].ToString();
                valorSolcitado = Convert.ToDouble(_row["valorSolcitado"].ToString());
                CuotaSolicitado = Convert.ToDouble(_row["CuotaSolicitado"].ToString());
                Linea = Convert.ToInt32(_row["Linea"].ToString());
                numeroSolicitud = Convert.ToInt32(_row["numeroSolicitud"].ToString());
                FechaSolictud = _row["FechaSolictud"].ToString();
                Plazo = _row["Plazo"].ToString();
                TipoClaseGarantia = Convert.ToInt32(_row["TipoClaseGarantia"].ToString());
                saldoTotalDeudaRecogido = Convert.ToDouble(_row["RecogeSaldoTotal"].ToString());
                capacidadPago = Convert.ToDecimal(_row["LblPorcCaja"].ToString());
                capacidadescuento = Convert.ToDouble(_row["lblPorcnDescuento"].ToString());
            }

            string salario_minimo_compania = "   ";
            stmysql = "select  salario_minimo as campo1 from sys_compania where CODIGO = '0001'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "calcularscoringIndividual", ref salario_minimo_compania);
            if (salario_minimo_compania.Trim() == "")
            {
                salario_minimo_compania = "0";
            }

            SALDOCAPITAL = CalculaTotalDeuda(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect, "N");
            DeudasInternas = SALDOCAPITAL;

            // datos preguntar
            CuotaPeriodica = CalculaTotaLCUOTA(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect);
            CuotaInterna = CuotaPeriodica;

            double saldoenaportes;
            ahorrospermanentes = CalculaSaldoAhorrosEquisuper(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect);

            saldoenaportes = this.CalculaSaldoAportes(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect);
            ahorrospermanentes = ahorrospermanentes + saldoenaportes;

            ahorroVoluntarios = CalculaSaldoAhorroaVoluntarios(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect);

            DiasMora = CalculaTotaLDiasMora(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect);
            garantiadmisible = (decimal)CalculaTotaLGarantias(codigoter, fechaEstudioCredito.ToString("yyyyMM"), myconnect);

            SALDOCAPITAL = (SALDOCAPITAL + valorSolcitado);
            CuotaPeriodica = (CuotaPeriodica + CuotaSolicitado);

            if (topPar == "T") // proceso se hace si el proceso es recoger deuda Total
            {
                SALDOCAPITAL = SALDOCAPITAL - RecogeSaldo; // se le suma a las Deudas
                CuotaPeriodica = CuotaPeriodica - RecogeCuota; // SUECE CUANDO EL RECOGEDUEDA ES TOTAL
            }

            if (Microsoft.VisualBasic.Information.IsNumeric(DiasMora) == false)
            {
                DiasMora = 0;
            }

            // SECCION CAPACIDAD DE PAGO
            DataSet datasetasociado = new DataSet();
            // msgparcop.BuscaAsociado(codigoter, ref datasetasociado, myconnect); // ERROR: CS1620
            long edad;
            long tiempolaboral;
            long mesesultimasolicitud;
            int tipocontrato;
            DateTime fechanacimiento;
            DateTime fechaingresoemp;
            string nombreasociado;

            // initialize
            edad = 0; tiempolaboral = 0; mesesultimasolicitud = 0; tipocontrato = 0;
            fechanacimiento = DateTime.MinValue; fechaingresoemp = DateTime.MinValue;
            nombreasociado = ""; CalificacionManual = "";

            if (datasetasociado.Tables["tblasociados"].Rows.Count >= 1)
            {
                DataRow _arow = datasetasociado.Tables["tblasociados"].Rows[0];
                fechanacimiento = Convert.ToDateTime(_arow["FECNACEM"].ToString());
                fechaingresoemp = Convert.ToDateTime(_arow["FEING_EMPRESA"].ToString());
                tipocontrato = Convert.ToInt32(_arow["CONTRACTO"].ToString());
                CalificacionManual = _arow["CALMAN"].ToString();
                nombreasociado = _arow["apellido"].ToString() + " " + _arow["nombre"].ToString();
                fechaIngreso = Convert.ToDateTime(_arow["fecha_ingreso"].ToString());
                fechaReingreso = Convert.ToDateTime(_arow["fecha_reingreso"].ToString());
            }

            edad = (long)DateAndTime.DateDiff(DateInterval.Year, fechanacimiento, fechaEstudioCredito);
            tiempolaboral = (long)DateAndTime.DateDiff(DateInterval.Year, fechaingresoemp, fechaEstudioCredito);

            string fechamaximasolicitud = "  ";
            stmysql = "select max(FECHA_SOLI) as campo1 from cop_solcre where codigoter = '" + codigoter + "' and  ESTADO <> 'X' " +
                      " and FECHA_SOLI < (select FECHA_SOLI from cop_solcre   where numero =  " + numeroSolicitud + " " +
                                          " and codigoter = '" + codigoter + "' and ESTADO <> 'X' ) ";

            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "calcularscoring", ref fechamaximasolicitud);
            if (Information.IsDate(fechamaximasolicitud) == true)
            {
                mesesultimasolicitud = (long)DateAndTime.DateDiff(DateInterval.Month, Convert.ToDateTime(fechamaximasolicitud), fechaEstudioCredito);
            }
            else
            {
                mesesultimasolicitud = 0;
            }

            switch (tipocontrato)
            {
                case 1:
                    IndemnizacionLaboral = (30 + ((tiempolaboral - 1) * 20)) * ((double)salario / 30);
                    break;
                default:
                    IndemnizacionLaboral = 0;
                    break;
            }

            ValorRiesgo = (ahorrospermanentes + ahorroVoluntarios + ((double)garantiadmisible * 0.7) + IndemnizacionLaboral - SALDOCAPITAL);
            if (ValorRiesgo > 0)
            {
                ValorRiesgo = 0;
            }

            // *** parte solevencia ***
            if (ahorrospermanentes > 0)
            {
                Reciprocidad = SALDOCAPITAL / (ahorrospermanentes + ahorroVoluntarios);
            }
            else
            {
                Reciprocidad = 0;
            }
            if (salario > 0)
            {
                Endeudamiento = (SALDOCAPITAL + (double)deudaexterna) / (double)salario;
            }
            else
            {
                Endeudamiento = (SALDOCAPITAL + (double)deudaexterna) / (Convert.ToDouble(salario_minimo_compania) * 3);
            }

            if (salario > 0)
            {
                Descubierto = (ahorrospermanentes + ahorroVoluntarios - SALDOCAPITAL) / (double)salario;
            }
            else
            {
                Descubierto = (ahorrospermanentes + ahorroVoluntarios - SALDOCAPITAL) / (Convert.ToDouble(salario_minimo_compania) * 3);
            }

            if (salario > 0)
            {
                PosicionNeta = (ahorrospermanentes + ahorroVoluntarios + ((double)garantiadmisible * 0.7) - SALDOCAPITAL) / (double)salario;
            }
            else
            {
                PosicionNeta = (ahorrospermanentes + ahorroVoluntarios + ((double)garantiadmisible * 0.7) - SALDOCAPITAL) / (Convert.ToDouble(salario_minimo_compania) * 3);
            }

            // *** Garantia ***
            // Valor en Riesgo (sin garantias con indemnizac)
            if (salario > 0)
            {
                ValorRiesgoSinGarantias = ((ahorrospermanentes + ahorroVoluntarios + IndemnizacionLaboral) - SALDOCAPITAL) / (double)salario;
            }
            else
            {
                ValorRiesgoSinGarantias = ((ahorrospermanentes + ahorroVoluntarios + IndemnizacionLaboral) - SALDOCAPITAL) / (Convert.ToDouble(salario_minimo_compania) * 3);
            }

            // preguntas asocias Calidad Asociado
            DataSet DtDatos = new DataSet();
            DataRowCollection Datarow;
            int PErIni = Convert.ToInt32(fechaEstudioCredito.ToString("yyyy"));
            // DtDatos = msgcop.CargarClasifCart(codigoter, PErIni, PErIni, myconnect, Convert.ToInt32(fechaEstudioCredito.ToString("yyyyMM"))); // ERROR: CS1061
            calidadAsociado = "A";
            if (DtDatos.Tables[0].Rows.Count > 0)
            {
                calidadAsociado = DtDatos.Tables[0].Rows[0]["Categoria"].ToString();
            }
            if (salario > 0)
            {
                MoraIngreso = saldoMoraDATACREDITO / (double)salario;
            }
            else
            {
                MoraIngreso = saldoMoraDATACREDITO / (Convert.ToDouble(salario_minimo_compania) * 3);
            }

            // Porcentaje de los Puntos de Scoring
            double PUNTAJE = 0; // variable que guarda los puntos

            // *** Capacidad de Pago ***
            double porcentEdad, porcentAntigLab, porcentTipoContraro;
            double porcentMesesultimasolic, porcentCapacidadDescuento;
            double porcentCapacidaPago, porcentCapaTotal;
            double porcentCapaSalario, porcentAntigCoop;
            double mesesAntigLaboral;

            porcentEdad = hayarpuntoscoring(myconnect, "1", "1", edad.ToString(), ref puntoporcentEdad, ref ReportPorcentajeEdad);
            PUNTAJE = PUNTAJE + porcentEdad;

            porcentAntigLab = hayarpuntoscoring(myconnect, "1", "2", tiempolaboral.ToString(), ref puntoporcentAntigLab, ref reportporcentAntigLab);
            PUNTAJE = PUNTAJE + porcentAntigLab;

            porcentTipoContraro = hayarpuntoscoring(myconnect, "1", "3", tipocontrato.ToString(), ref puntoporcentTipoContraro, ref reportporcentTipoContraro);
            PUNTAJE = PUNTAJE + porcentTipoContraro;

            porcentMesesultimasolic = hayarpuntoscoring(myconnect, "1", "4", mesesultimasolicitud.ToString(), ref puntoporcentMesesultimasolic, ref reportporcentMesesultimasolic);
            PUNTAJE = PUNTAJE + porcentMesesultimasolic;

            porcentCapacidaPago = hayarpuntoscoring(myconnect, "1", "5", capacidadPago.ToString(), ref puntoporcentCapacidaPago, ref reportporcentCapacidaPago);
            PUNTAJE = PUNTAJE + porcentCapacidaPago;

            porcentCapacidadDescuento = hayarpuntoscoring(myconnect, "1", "6", capacidadescuento.ToString(), ref puntoporcentCapacidadDescuento, ref reportporcentCapacidadDescuento);
            PUNTAJE = PUNTAJE + porcentCapacidadDescuento;

            porcentCapaSalario = hayarpuntoscoring(myconnect, "1", "7", salario.ToString(), ref puntoporcentCapaSalario, ref reportporcentCapaSalario);
            PUNTAJE = PUNTAJE + porcentCapaSalario;

            if (fechaReingreso > fechaIngreso)
            {
                mesesAntigLaboral = (double)DateAndTime.DateDiff(DateInterval.Month, fechaReingreso, fechaEstudioCredito);
            }
            else
            {
                mesesAntigLaboral = (double)DateAndTime.DateDiff(DateInterval.Month, fechaIngreso, fechaEstudioCredito);
            }

            porcentAntigCoop = hayarpuntoscoring(myconnect, "1", "8", mesesAntigLaboral.ToString(), ref puntoporcentAntigCoop, ref reportporcentAntigCoop);
            PUNTAJE = PUNTAJE + porcentAntigCoop;

            porcentCapaTotal = porcentEdad + porcentAntigLab + porcentTipoContraro + porcentMesesultimasolic + porcentCapacidadDescuento + porcentCapacidaPago + porcentCapaSalario + porcentAntigCoop;

            // *** SOLVENCIA ***
            double porcentReciprocidad, porcentEndeudamiento, porcentDescubierto;
            double porcentPosicionNeta, puntoTotalSolvencia;

            porcentReciprocidad = hayarpuntoscoring(myconnect, "2", "1", Reciprocidad.ToString(), ref puntoporcentReciprocidad, ref reportporcentReciprocidad);
            PUNTAJE = PUNTAJE + porcentReciprocidad;

            porcentEndeudamiento = hayarpuntoscoring(myconnect, "2", "2", Endeudamiento.ToString(), ref puntoporcentEndeudamiento, ref reportporcentEndeudamiento);
            PUNTAJE = PUNTAJE + porcentEndeudamiento;

            porcentDescubierto = hayarpuntoscoring(myconnect, "2", "3", Descubierto.ToString(), ref puntoporcentDescubierto, ref reportporcentDescubierto);
            PUNTAJE = PUNTAJE + porcentDescubierto;

            porcentPosicionNeta = hayarpuntoscoring(myconnect, "2", "4", PosicionNeta.ToString(), ref puntoPosicionNeta, ref reportPosicionNeta);
            PUNTAJE = PUNTAJE + porcentPosicionNeta;

            puntoTotalSolvencia = porcentReciprocidad + porcentEndeudamiento + porcentDescubierto + porcentPosicionNeta;

            // *** GARANTIAS ***
            double porcentFormaPago, porcentValorRiesgo, porcentajeGarantia, porcentajeclaseGarantia;
            porcentFormaPago = hayarpuntoscoring(myconnect, "3", "1", clades, ref puntoporcentFormaPago, ref reportporcentFormaPago);
            PUNTAJE = PUNTAJE + porcentFormaPago;

            porcentValorRiesgo = hayarpuntoscoring(myconnect, "3", "2", ValorRiesgoSinGarantias.ToString(), ref puntoporcentValorRiesgo, ref reportporcentValorRiesgo);
            PUNTAJE = PUNTAJE + porcentValorRiesgo;

            // SECCION PARA PORCENTAJE DE CLASEDEGARANTIA QUE HIZO EL CREDITO
            porcentajeclaseGarantia = hayarpuntoscoring(myconnect, "3", "3", TipoClaseGarantia.ToString(), ref puntoporcentajeclaseGarantia, ref reportporcentajeclaseGarantia);
            PUNTAJE = PUNTAJE + porcentajeclaseGarantia;

            porcentajeGarantia = porcentFormaPago + porcentValorRiesgo + porcentajeclaseGarantia;

            // *** SERVICIO DE LA DEUDA ***
            double porcentCalidadAsociado, porcentsaldoMoraDATACREDITO;
            double porcentCALDATACREDITO;
            double porcentACIERTA, porcentCalificacionActual;
            double porcentSeviDeuda;

            porcentsaldoMoraDATACREDITO = hayarpuntoscoring(myconnect, "4", "1", MoraIngreso.ToString(), ref puntoporcentsaldoMoraDATACREDITO, ref reportporcentsaldoMoraDATACREDITO);
            if (MoraIngreso != 0) // para tener en cuenta cuando se va parametrizar
            {
                porcentsaldoMoraDATACREDITO = hayarpuntoscoring(myconnect, "4", "1", saldoMoraDATACREDITO.ToString(), ref puntoporcentsaldoMoraDATACREDITO, ref reportporcentsaldoMoraDATACREDITO);
            }
            PUNTAJE = PUNTAJE + porcentsaldoMoraDATACREDITO;

            porcentCALDATACREDITO = hayarpuntoscoring(myconnect, "4", "2", califiDatacredito.Trim(), ref puntoporcentCALDATACREDITO, ref reportporcentCALDATACREDITO);
            PUNTAJE = PUNTAJE + porcentCALDATACREDITO;

            porcentCalidadAsociado = hayarpuntoscoring(myconnect, "4", "3", calidadAsociado.Trim(), ref puntoporcentCalidadAsociado, ref reportporcentCalidadAsociado);
            PUNTAJE = PUNTAJE + porcentCalidadAsociado;

            porcentACIERTA = hayarpuntoscoring(myconnect, "4", "4", acierta.ToString(), ref puntoporcentACIERTA, ref reportporcentACIERTA);
            PUNTAJE = PUNTAJE + porcentACIERTA;

            porcentCalificacionActual = hayarpuntoscoring(myconnect, "4", "5", CalificacionManual.Trim(), ref puntoporcentCalificacionActual, ref reportporcentCalificacionActual);
            PUNTAJE = PUNTAJE + porcentCalificacionActual;

            porcentSeviDeuda = porcentCalidadAsociado + porcentsaldoMoraDATACREDITO + porcentCALDATACREDITO + porcentACIERTA + porcentCalificacionActual;

            string RIESGO;
            RIESGO = hayarriesgoscoring(myconnect, "5", "1", PUNTAJE.ToString());

            // **** MOSTRAR SCORING ****
            string Ststring = " ";
            DataSet datoMostrarScoring = new DataSet();
            datoMostrarScoring.Tables.Add("tblScoring");
            DataColumnCollection cols = datoMostrarScoring.Tables["tblScoring"].Columns;
            cols.Add("codigoter", Ststring.GetType());
            cols.Add("nombreaso", Ststring.GetType());
            cols.Add("periodo", Ststring.GetType());
            cols.Add("salario", Ststring.GetType());
            cols.Add("otrosingresos", Ststring.GetType());
            cols.Add("SumaSaldoCapita", Ststring.GetType());
            cols.Add("DiasDeMora", Ststring.GetType());
            cols.Add("CuotaPeriodica", Ststring.GetType());
            cols.Add("garantiadmisible", Ststring.GetType());
            cols.Add("AportYahorrosperma", Ststring.GetType());
            cols.Add("AhorrosVoluntario", Ststring.GetType());
            cols.Add("DeudasExterna", Ststring.GetType());
            cols.Add("CuotaExterna", Ststring.GetType());
            cols.Add("PosIndemnizacionLABORAL", Ststring.GetType());
            cols.Add("ValorRiesgo", Ststring.GetType());
            cols.Add("Edad", Ststring.GetType());
            cols.Add("AntigLab", Ststring.GetType());
            cols.Add("Tipocontrato", Ststring.GetType());
            cols.Add("Mesesultsoli", Ststring.GetType());
            cols.Add("CapacidadPago", Ststring.GetType());
            cols.Add("CapacidadDescuento", Ststring.GetType());
            cols.Add("Reciprocidad", Ststring.GetType());
            cols.Add("Endeudamiento", Ststring.GetType());
            cols.Add("Descubierto", Ststring.GetType());
            cols.Add("PosicionNetagarnt", Ststring.GetType());
            cols.Add("FormaPago", Ststring.GetType());
            cols.Add("ValorenRiesgoSingarant", Ststring.GetType());
            cols.Add("CalidadAsociado", Ststring.GetType());
            cols.Add("saldoMoraDATACREDITO", Ststring.GetType());
            cols.Add("MoraIngreso", Ststring.GetType());
            cols.Add("CALDATACREDITO", Ststring.GetType());
            cols.Add("ACIERTA", Ststring.GetType());
            cols.Add("CalificacionActual", Ststring.GetType());
            cols.Add("Puntaje", Ststring.GetType());
            cols.Add("scoring", Ststring.GetType());
            cols.Add("PuntCapacidadPago", Ststring.GetType());
            cols.Add("PuntSolvencia", Ststring.GetType());
            cols.Add("PuntGarantia", Ststring.GetType());
            cols.Add("PuntServiDeuda", Ststring.GetType());
            cols.Add("RecogeDeudas", Ststring.GetType());
            cols.Add("valorSolcitado", Ststring.GetType());
            cols.Add("CuotaSolicitado", Ststring.GetType());
            cols.Add("Linea", Ststring.GetType());
            cols.Add("numeroSolicitud", Ststring.GetType());
            cols.Add("FechaSolictud", Ststring.GetType());
            cols.Add("Plazo", Ststring.GetType());
            cols.Add("DeudasInternas", Ststring.GetType());
            cols.Add("CuotaInterna", Ststring.GetType());
            cols.Add("RecogeSaldo", Ststring.GetType());
            cols.Add("fechaIngreso", Ststring.GetType());
            cols.Add("fechaReingreso", Ststring.GetType());
            cols.Add("mesesAntigLaboral", Ststring.GetType());
            cols.Add("porcentAntigCoop", Ststring.GetType());
            cols.Add("porcentCapaSalario", Ststring.GetType());
            cols.Add("porcentajeclaseGarantia", Ststring.GetType());
            cols.Add("TipoClaseGarantia", Ststring.GetType());
            // seccion Puntos CapacidadPago
            cols.Add("puntoporcentEdad", Ststring.GetType());
            cols.Add("ReportPorcentajeEdad", Ststring.GetType());
            cols.Add("puntoporcentAntigLab", Ststring.GetType());
            cols.Add("ReportporcentAntigLab", Ststring.GetType());
            cols.Add("puntoporcentTipoContraro", Ststring.GetType());
            cols.Add("ReportporcentTipoContraro", Ststring.GetType());
            cols.Add("puntoporcentMesesultimasolic", Ststring.GetType());
            cols.Add("ReportporcentMesesultimasolic", Ststring.GetType());
            cols.Add("puntoporcentCapacidaPago", Ststring.GetType());
            cols.Add("ReportporcentCapacidaPago", Ststring.GetType());
            cols.Add("puntoporcentCapacidadDescuento", Ststring.GetType());
            cols.Add("ReportporcentCapacidadDescuento", Ststring.GetType());
            cols.Add("puntoporcentCapaSalario", Ststring.GetType());
            cols.Add("ReportporcentCapaSalario", Ststring.GetType());
            cols.Add("puntoporcentAntigCoop", Ststring.GetType());
            cols.Add("ReportporcentAntigCoop", Ststring.GetType());
            // seccion Puntos SOLVENCIA
            cols.Add("puntoporcentReciprocidad", Ststring.GetType());
            cols.Add("reportporcentReciprocidad", Ststring.GetType());
            cols.Add("puntoporcentEndeudamiento", Ststring.GetType());
            cols.Add("reportporcentEndeudamiento", Ststring.GetType());
            cols.Add("puntoporcentDescubierto", Ststring.GetType());
            cols.Add("reportporcentDescubierto", Ststring.GetType());
            cols.Add("puntoPosicionNeta", Ststring.GetType());
            cols.Add("reportPosicionNeta", Ststring.GetType());
            // seccion Puntos GARANTIAS
            cols.Add("puntoporcentFormaPago", Ststring.GetType());
            cols.Add("reportporcentFormaPago", Ststring.GetType());
            cols.Add("puntoporcentValorRiesgo", Ststring.GetType());
            cols.Add("reportporcentValorRiesgo", Ststring.GetType());
            cols.Add("puntoporcentajeclaseGarantia", Ststring.GetType());
            cols.Add("reportporcentajeclaseGarantia", Ststring.GetType());
            // seccion Puntos SERVICIO DE LA DEUDA
            cols.Add("puntoporcentCalidadAsociado", Ststring.GetType());
            cols.Add("reportporcentCalidadAsociado", Ststring.GetType());
            cols.Add("puntoporcentsaldoMoraDATACREDITO", Ststring.GetType());
            cols.Add("reportporcentsaldoMoraDATACREDITO", Ststring.GetType());
            cols.Add("puntoporcentCALDATACREDITO", Ststring.GetType());
            cols.Add("reportporcentCALDATACREDITO", Ststring.GetType());
            cols.Add("puntoporcentACIERTA", Ststring.GetType());
            cols.Add("reportporcentACIERTA", Ststring.GetType());
            cols.Add("puntoporcentCalificacionActual", Ststring.GetType());
            cols.Add("reportporcentCalificacionActual", Ststring.GetType());

            datoMostrarScoring.Tables["tblScoring"].Rows.Add(
                codigoter, nombreasociado, fechaEstudioCredito.ToString("yyyyMM"),
                string.Format("{0:N2}", Convert.ToDouble(salario)),
                string.Format("{0:N2}", Convert.ToDouble(otrosingresos)),
                string.Format("{0:N2}", SALDOCAPITAL), DiasMora,
                string.Format("{0:N2}", CuotaPeriodica),
                string.Format("{0:N2}", (double)garantiadmisible),
                string.Format("{0:N2}", ahorrospermanentes),
                string.Format("{0:N2}", ahorroVoluntarios),
                string.Format("{0:N2}", (double)deudaexterna),
                string.Format("{0:N2}", (double)cuotaexterna),
                string.Format("{0:N2}", IndemnizacionLaboral),
                string.Format("{0:N2}", ValorRiesgo),
                edad, tiempolaboral, tipocontrato, mesesultimasolicitud,
                capacidadPago, capacidadescuento,
                string.Format("{0:N2}", Reciprocidad),
                string.Format("{0:N2}", Endeudamiento),
                string.Format("{0:N2}", Descubierto),
                string.Format("{0:N2}", PosicionNeta),
                clades,
                string.Format("{0:N2}", ValorRiesgoSinGarantias),
                calidadAsociado,
                string.Format("{0:N2}", saldoMoraDATACREDITO),
                MoraIngreso, califiDatacredito,
                acierta, CalificacionManual,
                PUNTAJE, RIESGO,
                porcentCapaTotal, puntoTotalSolvencia, porcentajeGarantia,
                porcentSeviDeuda,
                string.Format("{0:N2}", RecogeCuota),
                string.Format("{0:N2}", valorSolcitado),
                string.Format("{0:N2}", CuotaSolicitado),
                Linea, numeroSolicitud, FechaSolictud, Plazo,
                string.Format("{0:N2}", DeudasInternas),
                string.Format("{0:N2}", CuotaInterna),
                string.Format("{0:N2}", saldoTotalDeudaRecogido),
                fechaIngreso, fechaReingreso,
                mesesAntigLaboral, porcentAntigCoop, porcentCapaSalario,
                string.Format("{0:N2}", porcentajeclaseGarantia), TipoClaseGarantia,
                puntoporcentEdad, ReportPorcentajeEdad,
                puntoporcentAntigLab, reportporcentAntigLab,
                puntoporcentTipoContraro, reportporcentTipoContraro,
                puntoporcentMesesultimasolic, reportporcentMesesultimasolic,
                puntoporcentCapacidaPago, reportporcentCapacidaPago,
                puntoporcentCapacidadDescuento, reportporcentCapacidadDescuento,
                puntoporcentCapaSalario, reportporcentCapaSalario,
                puntoporcentAntigCoop, reportporcentAntigCoop,
                puntoporcentReciprocidad, reportporcentReciprocidad,
                puntoporcentEndeudamiento, reportporcentEndeudamiento,
                puntoporcentDescubierto, reportporcentDescubierto,
                puntoPosicionNeta, reportPosicionNeta,
                puntoporcentFormaPago, reportporcentFormaPago,
                puntoporcentValorRiesgo, reportporcentValorRiesgo,
                puntoporcentajeclaseGarantia, reportporcentajeclaseGarantia,
                puntoporcentCalidadAsociado, reportporcentCalidadAsociado,
                puntoporcentsaldoMoraDATACREDITO, reportporcentsaldoMoraDATACREDITO,
                puntoporcentCALDATACREDITO, reportporcentCALDATACREDITO,
                puntoporcentACIERTA, reportporcentACIERTA,
                puntoporcentCalificacionActual, reportporcentCalificacionActual);

            // datoMostrarScoring.WriteXmlSchema("D:\\esquemscoring.xml");
            ImprimirScoring(Pertenese, datoMostrarScoring, myconnect, EstCodeudor, nombredeudor);
        }

        public string hayarriesgoscoring(OdbcConnection myconnect, string codcriterioEvaluacion, string codsubitem, string valor)
        {
            DataSet DsData = new DataSet();
            double porcentaje;
            double valorrango;
            string igualdad = "";
            stmysql = " select a.rangoInicial,a.rangofinal,a.Valor,b.porcentaje ,a.igualdad,  " +
                      "(case a.igualdad when  '1' then '>  <=' when '2' then '>=  <' when '3' then '=  =' when '4' then '>  <'  when '5' then '=  <' " +
                      "  when '6' then '> =' when '7' then '<  <' when '8' then '> >' when '9' then '<= <=' when '10' then '>= >='  when '11' then '>= <='  end ) as igualdaco " +
                      " from  cop_rangoscoring  a  " +
                      " inner join cop_paramscoring b  " +
                      " on a.codcriterioEvaluacion = b.codcriterioEvaluacion   " +
                      " and a.codsubitem  = b.codsubitem   " +
                      " where a.codcriterioEvaluacion = '" + codcriterioEvaluacion + "'" +
                      " and a.codsubitem = '" + codsubitem + "' order by a.Valor desc";

            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "ProcesoRecogerCreditos", ref DsData, "tblscoring");
            if (DsData.Tables["tblscoring"].Rows.Count > 0)
            {
                for (int i = 0; i <= (DsData.Tables["tblscoring"].Rows.Count - 1); i++)
                {
                    DataRow _r = DsData.Tables["tblscoring"].Rows[i];
                    porcentaje = Convert.ToDouble(_r["porcentaje"].ToString());
                    valorrango = Convert.ToDouble(_r["Valor"].ToString());
                    igualdad = _r["igualdad"].ToString();

                    switch (igualdad.Trim())
                    {
                        case "1":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "2":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "3":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) == Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) == Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "4":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "5":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) == Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "6":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) == Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "7":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) < Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "8":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) > Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "9":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "10":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                        case "11":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    switch (Convert.ToInt32(_r["Valor"]))
                                    {
                                        case 1: return "Alto";
                                        case 2: return "Medio-Alto";
                                        case 3: return "Medio";
                                        case 4: return "Medio-Bajo";
                                        case 5: return "Bajo";
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            return "";
        }

        // hayarpuntoscoring — VB Optional ByRef params: punto As Integer = 1, porcentaje As Double = 1
        // Overload with ref params (primary)
        public double hayarpuntoscoring(OdbcConnection myconnect, string codcriterioEvaluacion, string codsubitem, string valor, ref double punto, ref double porcentaje)
        {
            DataSet DsData = new DataSet();
            int valorrango = 0;
            string igualdad = "";
            stmysql = " select a.rangoInicial,a.rangofinal,a.Valor,b.porcentaje,a.igualdad,  " +
                      "(case a.igualdad when  '1' then '>  <=' when '2' then '>=  <' when '3' then '=  =' when '4' then '>  <'  when '5' then '=  <' " +
                      "  when '6' then '> =' when '7' then '<  <' when '8' then '> >' when '9' then '<= <=' when '10' then '>= >='  when '11' then '>= <='  end ) as igualdaco " +
                      " from  cop_rangoscoring  a  " +
                      " inner join cop_paramscoring b  " +
                      " on a.codcriterioEvaluacion = b.codcriterioEvaluacion   " +
                      " and a.codsubitem  = b.codsubitem   " +
                      " where a.codcriterioEvaluacion = '" + codcriterioEvaluacion + "'" +
                      " and a.codsubitem = '" + codsubitem + "' order by a.Valor desc";

            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "ProcesoRecogerCreditos", ref DsData, "tblscoring");
            if (DsData.Tables["tblscoring"].Rows.Count > 0)
            {
                for (int i = 0; i <= (DsData.Tables["tblscoring"].Rows.Count - 1); i++)
                {
                    DataRow _r = DsData.Tables["tblscoring"].Rows[i];
                    // InputBox("d", "d", valor & ">" & .Item("rangoInicial").ToString & " and " & valor & "<=" & .Item("rangofinal").ToString)
                    porcentaje = Convert.ToDouble(_r["porcentaje"].ToString());
                    valorrango = (int)Convert.ToDouble(_r["Valor"].ToString());
                    punto = Convert.ToDouble(_r["Valor"].ToString());
                    igualdad = _r["igualdad"].ToString();

                    switch (igualdad.Trim())
                    {
                        case "1":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) > 0 && string.Compare(valor, _r["rangofinal"].ToString()) <= 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "2":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) >= 0 && string.Compare(valor, _r["rangofinal"].ToString()) < 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "3":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) == Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) == Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (valor == _r["rangoInicial"].ToString() && valor == _r["rangofinal"].ToString())
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "4":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) > 0 && string.Compare(valor, _r["rangofinal"].ToString()) < 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "5":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) == Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (valor == _r["rangoInicial"].ToString() && string.Compare(valor, _r["rangofinal"].ToString()) < 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "6":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) == Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) > 0 && valor == _r["rangofinal"].ToString())
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "7":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) < Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) < Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) < 0 && string.Compare(valor, _r["rangofinal"].ToString()) < 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "8":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) > Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) > Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) > 0 && string.Compare(valor, _r["rangofinal"].ToString()) > 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "9":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) <= 0 && string.Compare(valor, _r["rangofinal"].ToString()) <= 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "10":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) >= 0 && string.Compare(valor, _r["rangofinal"].ToString()) >= 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                        case "11":
                            if (Microsoft.VisualBasic.Information.IsNumeric(_r["rangoInicial"].ToString()) == true && Microsoft.VisualBasic.Information.IsNumeric(_r["rangofinal"].ToString()) && Microsoft.VisualBasic.Information.IsNumeric(valor))
                            {
                                if (Convert.ToDouble(valor) >= Convert.ToDouble(_r["rangoInicial"].ToString()) && Convert.ToDouble(valor) <= Convert.ToDouble(_r["rangofinal"].ToString()))
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            else
                            {
                                if (string.Compare(valor, _r["rangoInicial"].ToString()) >= 0 && string.Compare(valor, _r["rangofinal"].ToString()) <= 0)
                                {
                                    return (Convert.ToDouble(_r["Valor"]) * Convert.ToDouble(_r["porcentaje"])) / 100;
                                }
                            }
                            break;
                    }
                }
            }
            return (valorrango * porcentaje) / 100;
        }

        // Overload without optional refs (for callers that don't need point/porcentaje back)
        public double hayarpuntoscoring(OdbcConnection myconnect, string codcriterioEvaluacion, string codsubitem, string valor, ref double punto)
        {
            double porcentaje = 1;
            return hayarpuntoscoring(myconnect, codcriterioEvaluacion, codsubitem, valor, ref punto, ref porcentaje);
        }

        public double hayarpuntoscoring(OdbcConnection myconnect, string codcriterioEvaluacion, string codsubitem, string valor)
        {
            double punto = 1;
            double porcentaje = 1;
            return hayarpuntoscoring(myconnect, codcriterioEvaluacion, codsubitem, valor, ref punto, ref porcentaje);
        }

        public void ImprimirScoring(System.Windows.Forms.Form Pertenese, DataSet dataset, OdbcConnection myconnect, bool EstCodeudor, string nombredeudor)
        {
            ERP.Core.Compartido.Reportes.reporte Informe = new ERP.Core.Compartido.Reportes.reporte("cop_fscoring01");
            ERP.Core.Compartido.Reportes.config_report configreport = new ERP.Core.Compartido.Reportes.config_report();
            string nitcomp = " ", diremp = " ", nomemp = " ", telemp = " ";
            string imprimeCodeudor = "";
            string _p15 = " ", _p16 = " ", _p26 = " ", _p27 = " ";
            // msgparsys.BuscarCompania(varini.sptCodEmpr, myconnect, ref _p15, ref _p16, ref nomemp, ref telemp); // ERROR: CS7036
            nitcomp = _p15; diremp = _p16; nomemp = _p26; telemp = _p27;
            // faithful call: VB passes positional params 1,2,,(,,,,,,,,,,,,p15,p16,,,,,,,,,p26,p27)
            // Use the BuscarCompaniaHelper pattern from other parts if available, or inline
            BuscarCompaniaHelperScoring(varini.sptCodEmpr, myconnect, out nitcomp, out diremp, out nomemp, out telemp);

            switch (EstCodeudor)
            {
                case true:
                    imprimeCodeudor = "Y";
                    break;
                case false:
                    imprimeCodeudor = "N";
                    nombredeudor = " ";
                    break;
            }
            Informe.SetDataSource(dataset);
            Informe.SetParameterValue("empresa", nomemp);
            Informe.SetParameterValue("nit", nitcomp);
            Informe.SetParameterValue("direccion", diremp);
            Informe.SetParameterValue("telefono", telemp);
            Informe.SetParameterValue("codeudor", imprimeCodeudor);
            Informe.SetParameterValue("deudor", nombredeudor);
            configreport.confi_reportes(Pertenese, Informe);
        }

        private void BuscarCompaniaHelperScoring(object codEmpr, OdbcConnection myconnect, out string nitcomp, out string diremp, out string nomemp, out string telemp)
        {
            string _p1 = " ", _p2 = " ", _p3 = " ", _p4 = " ", _p5 = " ", _p6 = " ", _p7 = " ";
            string _p8 = " ", _p9 = " ", _p10 = " ", _p11 = " ", _p12 = " ", _p13 = " ", _p14 = " ";
            string _p15 = " ", _p16 = " ", _p17 = " ", _p18 = " ", _p19 = " ", _p20 = " ", _p21 = " ";
            string _p22 = " ", _p23 = " ", _p24 = " ", _p25 = " ", _p26 = " ", _p27 = " ";
            // msgparsys.BuscarCompania(codEmpr, myconnect, // ERROR: CS7036
                // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, // ERROR: CS7036
                // ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, // ERROR: CS7036
                // ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, // ERROR: CS7036
                // ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27); // ERROR: CS7036
            nitcomp = _p15;
            diremp = _p16;
            nomemp = _p26;
            telemp = _p27;
        }

        public DataSet calcularscoringGrupal(System.Windows.Forms.Form Pertenese, OdbcConnection myconnect, string where, string periodoscoring, DateTime fechascoring)
        {
            string codigoter = "";
            double salario = 0;
            string NoTieneSalario;
            double otrosingresos = 0;
            double deudaexterna = 0;
            double cuotaexterna = 0;
            double acierta = 0;
            string califiDatacredito = " ";
            double garantiadmisible = 0;
            double garantiadmisibleCredito;
            double RecogeCuota;
            DateTime fechaIngreso = DateTime.MinValue;
            DateTime fechaReingreso = DateTime.MinValue;
            double CONYSALAR = 0;
            double IngVariables = 0;
            double DstoParafiscales;
            string tipodsto = "0";
            double PorDescuento = 0;

            // parte capacidad de pago
            double SALDOCAPITAL;
            double CuotaPeriodica;
            double ahorrospermanentes;
            double ahorroVoluntarios;
            double DiasMora;
            double IndemnizacionLaboral;
            double ValorRiesgo;
            double capacidadescuento;
            double capacidadPago = 0;
            double capacidadPago_tmp = 0;
            double capacidadescuento_tmp = 0;
            double ingresosNomina = 0;
            double deduccionesNomina = 0;
            double MedioSal = 0;
            double DstoCaja = 0;
            double DstoNomina = 0;

            // parte solevencia
            double Reciprocidad;
            double Endeudamiento;
            double Descubierto;
            double PosicionNeta;
            // parte Garantias
            string clades;
            double ValorRiesgoSinGarantias;
            // servicio a la deuda
            string CalificacionManual;
            string calidadAsociado;
            double saldoMoraDATACREDITO;
            double MoraIngreso;
            string CalificacionActual = "A";

            // initialize
            garantiadmisibleCredito = 0; RecogeCuota = 0; SALDOCAPITAL = 0; CuotaPeriodica = 0;
            ahorrospermanentes = 0; ahorroVoluntarios = 0; DiasMora = 0; IndemnizacionLaboral = 0;
            ValorRiesgo = 0; capacidadescuento = 0; Reciprocidad = 0; Endeudamiento = 0;
            Descubierto = 0; PosicionNeta = 0; ValorRiesgoSinGarantias = 0; saldoMoraDATACREDITO = 0;
            MoraIngreso = 0; clades = ""; CalificacionManual = ""; calidadAsociado = "";
            NoTieneSalario = ""; DstoParafiscales = 0;

            StringBuilder stbuilder = new StringBuilder();
            string Ststring = " ";
            DataSet datoMostrarScoring = new DataSet();
            datoMostrarScoring.Tables.Add("tblScoring");
            DataColumnCollection cols = datoMostrarScoring.Tables["tblScoring"].Columns;
            cols.Add("codigoter", Ststring.GetType());
            cols.Add("nombreaso", Ststring.GetType());
            cols.Add("periodo", Ststring.GetType());
            cols.Add("NoTieneSalario", Ststring.GetType());
            cols.Add("salario", Ststring.GetType());
            cols.Add("otrosingresos", Ststring.GetType());
            cols.Add("IngVariables", Ststring.GetType());
            cols.Add("DeudasTerceros", Ststring.GetType());
            cols.Add("DescuentoNomina", Ststring.GetType());
            cols.Add("DescuentoCaja", Ststring.GetType());
            cols.Add("DstoParafiscales", Ststring.GetType());
            cols.Add("TipoDescuento", Ststring.GetType());
            cols.Add("PorcentajeDescuento", Ststring.GetType());
            cols.Add("SumaSaldoCapita", Ststring.GetType());
            cols.Add("DiasDeMora", Ststring.GetType());
            cols.Add("CuotaPeriodica", Ststring.GetType());
            cols.Add("garantiadmisible", Ststring.GetType());
            cols.Add("AportYahorrosperma", Ststring.GetType());
            cols.Add("AhorrosVoluntario", Ststring.GetType());
            cols.Add("DeudasExterna", Ststring.GetType());
            cols.Add("CuotaExterna", Ststring.GetType());
            cols.Add("PosIndemnizacionLABORAL", Ststring.GetType());
            cols.Add("ValorRiesgo", Ststring.GetType());
            cols.Add("fechaIngreso", Ststring.GetType());
            cols.Add("fechaReingreso", Ststring.GetType());
            cols.Add("Edad", Ststring.GetType());
            cols.Add("AntigLab", Ststring.GetType());
            cols.Add("Tipocontrato", Ststring.GetType());
            cols.Add("Mesesultsoli", Ststring.GetType());
            cols.Add("CapacidadPago", Ststring.GetType());
            cols.Add("CapacidadDescuento", Ststring.GetType());
            cols.Add("mesesAntigLaboral", Ststring.GetType());
            cols.Add("Reciprocidad", Ststring.GetType());
            cols.Add("Endeudamiento", Ststring.GetType());
            cols.Add("Descubierto", Ststring.GetType());
            cols.Add("PosicionNetagarnt", Ststring.GetType());
            cols.Add("FormaPago", Ststring.GetType());
            cols.Add("ValorenRiesgoSingarant", Ststring.GetType());
            cols.Add("CalidadAsociado", Ststring.GetType());
            cols.Add("saldoMoraDATACREDITO", Ststring.GetType());
            cols.Add("MoraIngreso", Ststring.GetType());
            cols.Add("CALDATACREDITO", Ststring.GetType());
            cols.Add("ACIERTA", Ststring.GetType());
            cols.Add("CalificacionActual", Ststring.GetType());
            cols.Add("puntoEdad", Ststring.GetType());
            cols.Add("PuntoAntigLab", Ststring.GetType());
            cols.Add("PuntoTipoContraro", Ststring.GetType());
            cols.Add("PuntoMesesultsoli", Ststring.GetType());
            cols.Add("PuntoCapacidPago", Ststring.GetType());
            cols.Add("PuntoCapaciDesc", Ststring.GetType());
            cols.Add("puntoCapaSalario", Ststring.GetType());
            cols.Add("puntoAntigCoop", Ststring.GetType());
            cols.Add("PuntoReciprocidad", Ststring.GetType());
            cols.Add("PuntoEndeudamiento", Ststring.GetType());
            cols.Add("PuntoDescubierto", Ststring.GetType());
            cols.Add("PuntoPosicionNeta", Ststring.GetType());
            cols.Add("PuntoFormaPago", Ststring.GetType());
            cols.Add("PuntoValorenRiesgo", Ststring.GetType());
            cols.Add("PuntoCalidadAsociado", Ststring.GetType());
            cols.Add("PuntoValormorasalario", Ststring.GetType());
            cols.Add("PuntoCALDATACREDITO", Ststring.GetType());
            cols.Add("PuntoACIERTA", Ststring.GetType());
            cols.Add("PuntoCalificacionActual", Ststring.GetType());
            cols.Add("Puntaje", Ststring.GetType());
            cols.Add("scoring", Ststring.GetType());

            string salario_minimo_compania = "   ";
            stmysql = "select  salario_minimo as campo1 from sys_compania where CODIGO = '0001'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "calcularscoringGrupal", ref salario_minimo_compania);
            if (salario_minimo_compania.Trim() == "")
            {
                salario_minimo_compania = "0";
            }

            stbuilder.Append("select DISTINCT maenit.codigoter as codigoter,maenit.salario as salario,(maenit.OTRO_INGRESO + maenit.CONYSALAR) as otrosingresos, ");
            stbuilder.Append("maenit.saldodeudaexterna as deudaexterna,maenit.cuotadeudaexterna as cuotaexterna,  ");
            stbuilder.Append(" maenit.Acierta as acieta,maenit.califidatacredito as califiDatacredito,  ");
            stbuilder.Append("maenit.CLASE_DESTO as Clades,maenit.SalMorDatacredito as SalMorDatacredito,maenit.CONYSALAR,maenit.IngVariables,emp.tipodsto,emp.PorDescuento  ");
            stbuilder.Append("FROM  sys_maenit maenit   ");
            stbuilder.Append("inner join cop_empresa13 emp   ");
            stbuilder.Append("on maenit.EMPRESA = emp.codigo_empresa   ");
            stbuilder.Append("inner join sys_cencos cencos  ");
            stbuilder.Append("on maenit.CENCOSTO = cencos.CCOSTO  ");
            stbuilder.Append("left join  cop_retiros retiros ");
            stbuilder.Append(" on maenit.codigoter = retiros.codigoter  ");
            stbuilder.Append(" and retiros.periodo<='" + periodoscoring + "' ");
            stbuilder.Append(" and retiros.EstadoAct in (select g.estadoact  FROM cop_retiros g where g.codigoter= maenit.codigoter ");
            stbuilder.Append(" and  g.periodo<='" + periodoscoring + "'  and  g.FecNovedad=(select max(f.fecnovedad) from cop_retiros f where f.codigoter=maenit.codigoter ");
            stbuilder.Append(" and  f.periodo<='" + periodoscoring + "')) " + where);

            // InputBox("d", "d", stbuilder.ToString)
            DataSet datasetscoring = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "calcularscoringGrupal", ref datasetscoring, "datasetscoring");
            ERP.Core.Compartido.Controles.Barraprogress pro = new ERP.Core.Compartido.Controles.Barraprogress("Generando Scoring", Pertenese);
            pro.ValorMinimoMaximo(0, 5);
            pro.Show();
            pro.ValorMinimoMaximo(0, datasetscoring.Tables["datasetscoring"].Rows.Count);

            // datos Necesarios
            for (int i = 0; i <= (datasetscoring.Tables["datasetscoring"].Rows.Count - 1); i++)
            {
                DataRow _drow = datasetscoring.Tables["datasetscoring"].Rows[i];
                salario = 0; otrosingresos = 0; deudaexterna = 0; cuotaexterna = 0; acierta = 0; garantiadmisible = 0;
                garantiadmisibleCredito = 0; RecogeCuota = 0; SALDOCAPITAL = 0; CuotaPeriodica = 0; ahorrospermanentes = 0;
                ahorroVoluntarios = 0; DiasMora = 0; IndemnizacionLaboral = 0; ValorRiesgo = 0; capacidadescuento = 0;
                capacidadPago = 0; Reciprocidad = 0; Endeudamiento = 0; Descubierto = 0; PosicionNeta = 0;
                ValorRiesgoSinGarantias = 0; saldoMoraDATACREDITO = 0; MoraIngreso = 0;

                codigoter = _drow["codigoter"].ToString();
                salario = Convert.ToDouble(_drow["salario"].ToString());
                CONYSALAR = Convert.ToDouble(_drow["CONYSALAR"]);
                IngVariables = Convert.ToDouble(_drow["IngVariables"]);
                tipodsto = _drow["tipodsto"].ToString();
                PorDescuento = Convert.ToDouble(_drow["PorDescuento"]);

                otrosingresos = Convert.ToDouble(_drow["otrosingresos"].ToString());
                deudaexterna = Convert.ToDouble(_drow["deudaexterna"].ToString());
                cuotaexterna = Convert.ToDouble(_drow["cuotaexterna"].ToString());
                acierta = Convert.ToDouble(_drow["acieta"].ToString());
                califiDatacredito = _drow["califiDatacredito"].ToString();
                clades = _drow["clades"].ToString(); // parte de la garantia
                saldoMoraDATACREDITO = Convert.ToDouble(_drow["SalMorDatacredito"].ToString());

                SALDOCAPITAL = CalculaTotalDeuda(codigoter, periodoscoring, myconnect, "N");
                // datos preguntar
                CuotaPeriodica = CalculaTotaLCUOTA(codigoter, periodoscoring, myconnect);

                double saldoenaportes;
                ahorrospermanentes = CalculaSaldoAhorrosEquisuper(codigoter, periodoscoring, myconnect);

                saldoenaportes = this.CalculaSaldoAportes(codigoter, periodoscoring, myconnect);
                ahorrospermanentes = ahorrospermanentes + saldoenaportes;

                ahorroVoluntarios = CalculaSaldoAhorroaVoluntarios(codigoter, periodoscoring, myconnect);
                DiasMora = CalculaTotaLDiasMora(codigoter, periodoscoring, myconnect);
                garantiadmisible = CalculaTotaLGarantias(codigoter, periodoscoring, myconnect);

                CuotaPeriodica = CuotaPeriodica - RecogeCuota; // SUECE CUANDO EL RECOGEDUEDA ES TOTAL

                if (Microsoft.VisualBasic.Information.IsNumeric(DiasMora) == false)
                {
                    DiasMora = 0;
                }

                // SECCION CAPACIDAD DE PAGO
                DataSet datasetasociado = new DataSet();
                // msgparcop.BuscaAsociado(codigoter, ref datasetasociado, myconnect); // ERROR: CS1620
                double edad;
                double tiempolaboral;
                double mesesultimasolicitud;
                int tipocontrato;
                DateTime fechanacimiento;
                DateTime fechaingresoemp;
                string nombreasociado;

                edad = 0; tiempolaboral = 0; mesesultimasolicitud = 0; tipocontrato = 0;
                fechanacimiento = DateTime.MinValue; fechaingresoemp = DateTime.MinValue;
                nombreasociado = ""; CalificacionManual = "";

                if (datasetasociado.Tables["tblasociados"].Rows.Count >= 1)
                {
                    DataRow _arow = datasetasociado.Tables["tblasociados"].Rows[0];
                    fechanacimiento = Convert.ToDateTime(_arow["FECNACEM"].ToString());
                    fechaingresoemp = Convert.ToDateTime(_arow["FEING_EMPRESA"].ToString());
                    tipocontrato = Convert.ToInt32(_arow["CONTRACTO"].ToString());
                    CalificacionManual = _arow["CALMAN"].ToString();
                    nombreasociado = _arow["apellido"].ToString() + " " + _arow["nombre"].ToString();
                    fechaIngreso = Convert.ToDateTime(_arow["fecha_ingreso"].ToString());
                    fechaReingreso = Convert.ToDateTime(_arow["fecha_reingreso"].ToString());
                }

                edad = (double)DateAndTime.DateDiff(DateInterval.Year, fechanacimiento, fechascoring);
                tiempolaboral = (double)DateAndTime.DateDiff(DateInterval.Year, fechaingresoemp, fechascoring);

                string fechamaximasolicitud = "  ";
                stmysql = "select max(FECHA_SOLI) as campo1 from cop_solcre where codigoter = '" + codigoter + "' and  ESTADO <> 'X' ";
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "calcularscoring", ref fechamaximasolicitud);
                if (Information.IsDate(fechamaximasolicitud) == true)
                {
                    mesesultimasolicitud = (double)DateAndTime.DateDiff(DateInterval.Month, Convert.ToDateTime(fechamaximasolicitud), fechascoring);
                }
                else
                {
                    mesesultimasolicitud = 0;
                }

                switch (tipocontrato)
                {
                    case 1:
                        IndemnizacionLaboral = (30 + ((tiempolaboral - 1) * 20)) * (salario / 30);
                        break;
                    default:
                        IndemnizacionLaboral = 0;
                        break;
                }

                ValorRiesgo = (ahorrospermanentes + ahorroVoluntarios + (garantiadmisible * 0.7) + IndemnizacionLaboral - SALDOCAPITAL);
                if (ValorRiesgo > 0)
                {
                    ValorRiesgo = 0;
                }

                if (salario > 0)
                {
                    NoTieneSalario = "N";
                }
                else
                {
                    NoTieneSalario = "Y";
                }

                // se generara Proceso para calcular la capacidad de pago
                CalculaDeducciones(codigoter, periodoscoring, myconnect, ref DstoNomina, ref DstoCaja);

                if (salario > 0)
                {
                    DstoParafiscales = (salario + IngVariables) * 0.08;
                    capacidadPago_tmp = (salario + IngVariables + otrosingresos) - (deudaexterna + DstoNomina + DstoCaja + DstoParafiscales);
                    capacidadPago = (capacidadPago_tmp / (salario + IngVariables + otrosingresos)) * 100;
                }
                else
                {
                    DstoParafiscales = ((Convert.ToDouble(salario_minimo_compania) * 3) + IngVariables) * 0.08;
                    capacidadPago_tmp = ((Convert.ToDouble(salario_minimo_compania) * 3) + IngVariables + otrosingresos) - (deudaexterna + DstoNomina + DstoCaja + DstoParafiscales);
                    capacidadPago = (capacidadPago_tmp / (((Convert.ToDouble(salario_minimo_compania) * 3) + IngVariables + otrosingresos))) * 100;
                }

                if (salario > 0)
                {
                    DstoParafiscales = (salario + IngVariables) * 0.08;
                    ingresosNomina = (salario + IngVariables);
                    deduccionesNomina = (DstoNomina + DstoParafiscales);

                    switch (tipodsto)
                    {
                        case "0":
                            MedioSal = (salario * (PorDescuento / 100));
                            break;
                        case "1":
                            MedioSal = (salario - PorDescuento);
                            break;
                    }

                    capacidadescuento_tmp = MedioSal - deduccionesNomina;
                    capacidadescuento = PorDescuento - (capacidadescuento_tmp / salario) * 100;
                }
                else
                {
                    DstoParafiscales = ((Convert.ToDouble(salario_minimo_compania) * 3) + IngVariables) * 0.08;
                    ingresosNomina = ((Convert.ToDouble(salario_minimo_compania) * 3) + IngVariables);
                    deduccionesNomina = (DstoNomina + DstoParafiscales);

                    switch (tipodsto)
                    {
                        case "0":
                            MedioSal = ((Convert.ToDouble(salario_minimo_compania) * 3) * (PorDescuento / 100));
                            break;
                        case "1":
                            MedioSal = ((Convert.ToDouble(salario_minimo_compania) * 3) - PorDescuento);
                            break;
                    }

                    capacidadescuento_tmp = MedioSal - deduccionesNomina;
                    capacidadescuento = PorDescuento - (capacidadescuento_tmp / (Convert.ToDouble(salario_minimo_compania) * 3)) * 100;
                }

                // *** parte solevencia ***
                if (ahorrospermanentes > 0)
                {
                    Reciprocidad = SALDOCAPITAL / (ahorrospermanentes + ahorroVoluntarios);
                }
                else
                {
                    Reciprocidad = 0;
                }
                if (salario > 0)
                {
                    Endeudamiento = (SALDOCAPITAL + deudaexterna) / salario;
                }
                else
                {
                    Endeudamiento = (SALDOCAPITAL + deudaexterna) / (Convert.ToDouble(salario_minimo_compania) * 3);
                }

                if (salario > 0)
                {
                    Descubierto = (ahorrospermanentes + ahorroVoluntarios - SALDOCAPITAL) / salario;
                }
                else
                {
                    Descubierto = (ahorrospermanentes + ahorroVoluntarios - SALDOCAPITAL) / (Convert.ToDouble(salario_minimo_compania) * 3);
                }

                if (salario > 0)
                {
                    PosicionNeta = (ahorrospermanentes + ahorroVoluntarios + (garantiadmisible * 0.7) - SALDOCAPITAL) / salario;
                }
                else
                {
                    PosicionNeta = (ahorrospermanentes + ahorroVoluntarios + (garantiadmisible * 0.7) - SALDOCAPITAL) / (Convert.ToDouble(salario_minimo_compania) * 3);
                }

                // *** Garantia ***
                // Valor en Riesgo (sin garantias con indemnizac)
                if (salario > 0)
                {
                    ValorRiesgoSinGarantias = ((ahorrospermanentes + ahorroVoluntarios + IndemnizacionLaboral) - SALDOCAPITAL) / salario;
                }
                else
                {
                    ValorRiesgoSinGarantias = ((ahorrospermanentes + ahorroVoluntarios + IndemnizacionLaboral) - SALDOCAPITAL) / (Convert.ToDouble(salario_minimo_compania) * 3);
                }

                // preguntas asocias Calidad Asociado
                DataSet DtDatos = new DataSet();
                DataRowCollection Datarow;
                int PErIni = Convert.ToInt32(fechascoring.ToString("yyyy"));
                // DtDatos = msgcop.CargarClasifCart(codigoter, PErIni, PErIni, myconnect, Convert.ToInt32(fechascoring.ToString("yyyyMM"))); // ERROR: CS1061
                calidadAsociado = "A";
                if (DtDatos.Tables[0].Rows.Count > 0)
                {
                    calidadAsociado = DtDatos.Tables[0].Rows[0]["Categoria"].ToString();
                }
                if (salario > 0)
                {
                    MoraIngreso = saldoMoraDATACREDITO / salario;
                }
                else
                {
                    MoraIngreso = saldoMoraDATACREDITO / (Convert.ToDouble(salario_minimo_compania) * 3);
                }

                // Porcentaje de los Puntos de Scoring
                double PUNTAJE = 0; // variable que guarda los puntos

                // *** Capacidad de Pago ***
                double porcentEdad, porcentAntigLab, porcentTipoContraro;
                double porcentMesesultimasolic, porcentCapacidadDescuento;
                double porcentCapacidaPago, porcentCapaTotal;
                double porcentCapaSalario, porcentAntigCoop;
                double mesesAntigLaboral;

                double puntoEdad = 0;
                porcentEdad = hayarpuntoscoring(myconnect, "1", "1", edad.ToString(), ref puntoEdad);
                PUNTAJE = PUNTAJE + porcentEdad;

                double puntoAntigLab = 0;
                porcentAntigLab = hayarpuntoscoring(myconnect, "1", "2", tiempolaboral.ToString(), ref puntoAntigLab);
                PUNTAJE = PUNTAJE + porcentAntigLab;

                double puntoTipoContrato = 0;
                porcentTipoContraro = hayarpuntoscoring(myconnect, "1", "3", tipocontrato.ToString(), ref puntoTipoContrato);
                PUNTAJE = PUNTAJE + porcentTipoContraro;

                double puntoMesesultimasolic = 0;
                porcentMesesultimasolic = hayarpuntoscoring(myconnect, "1", "4", mesesultimasolicitud.ToString(), ref puntoMesesultimasolic);
                PUNTAJE = PUNTAJE + porcentMesesultimasolic;

                double puntocapacidaPago = 0;
                porcentCapacidaPago = hayarpuntoscoring(myconnect, "1", "5", capacidadPago.ToString(), ref puntocapacidaPago);
                PUNTAJE = PUNTAJE + porcentCapacidaPago;

                double puntocapadesc = 0;
                porcentCapacidadDescuento = hayarpuntoscoring(myconnect, "1", "6", capacidadescuento.ToString(), ref puntocapadesc);
                PUNTAJE = PUNTAJE + porcentCapacidadDescuento;

                double puntoCapaSalario = 0;
                porcentCapaSalario = hayarpuntoscoring(myconnect, "1", "7", salario.ToString(), ref puntoCapaSalario);
                PUNTAJE = PUNTAJE + porcentCapaSalario;

                if (fechaReingreso > fechaIngreso)
                {
                    mesesAntigLaboral = (double)DateAndTime.DateDiff(DateInterval.Month, fechaReingreso, fechascoring);
                }
                else
                {
                    mesesAntigLaboral = (double)DateAndTime.DateDiff(DateInterval.Month, fechaIngreso, fechascoring);
                }

                double puntoAntigCoop = 0;
                porcentAntigCoop = hayarpuntoscoring(myconnect, "1", "8", mesesAntigLaboral.ToString(), ref puntoAntigCoop);
                PUNTAJE = PUNTAJE + porcentAntigCoop;

                porcentCapaTotal = porcentEdad + porcentAntigLab + porcentTipoContraro + porcentMesesultimasolic + porcentCapacidadDescuento + porcentCapacidaPago + porcentCapaSalario + porcentAntigCoop;

                // *** SOLVENCIA ***
                double porcentReciprocidad, porcentEndeudamiento, porcentDescubierto;
                double porcentPosicionNeta, puntoTotalSolvencia;
                double puntoReciprocidad = 0;
                porcentReciprocidad = hayarpuntoscoring(myconnect, "2", "1", Reciprocidad.ToString(), ref puntoReciprocidad);
                PUNTAJE = PUNTAJE + porcentReciprocidad;

                double puntoEndeudamiento = 0;
                porcentEndeudamiento = hayarpuntoscoring(myconnect, "2", "2", Endeudamiento.ToString(), ref puntoEndeudamiento);
                PUNTAJE = PUNTAJE + porcentEndeudamiento;

                double puntoDescubierto = 0;
                porcentDescubierto = hayarpuntoscoring(myconnect, "2", "3", Descubierto.ToString(), ref puntoDescubierto);
                PUNTAJE = PUNTAJE + porcentDescubierto;

                double puntoPosicionNeta = 0;
                porcentPosicionNeta = hayarpuntoscoring(myconnect, "2", "4", PosicionNeta.ToString(), ref puntoPosicionNeta);
                PUNTAJE = PUNTAJE + porcentPosicionNeta;

                puntoTotalSolvencia = porcentReciprocidad + porcentEndeudamiento + porcentDescubierto + porcentPosicionNeta;

                // *** GARANTIAS ***
                double porcentFormaPago, porcentValorRiesgo, porcentajeGarantia;

                double puntoformaPago = 0;
                porcentFormaPago = hayarpuntoscoring(myconnect, "3", "1", clades, ref puntoformaPago);
                PUNTAJE = PUNTAJE + porcentFormaPago;

                double puntoValorRiesgo = 0;
                porcentValorRiesgo = hayarpuntoscoring(myconnect, "3", "2", ValorRiesgoSinGarantias.ToString(), ref puntoValorRiesgo);
                PUNTAJE = PUNTAJE + porcentValorRiesgo;
                porcentajeGarantia = porcentFormaPago + porcentValorRiesgo;

                // *** SERVICIO DE LA DEUDA ***
                double porcentCalidadAsociado, porcentsaldoMoraDATACREDITO;
                double porcentCALDATACREDITO;
                double porcentACIERTA, porcentCalificacionActual;
                double porcentSeviDeuda;

                double puntoCalidadAsociado = 0;
                porcentCalidadAsociado = hayarpuntoscoring(myconnect, "4", "3", calidadAsociado, ref puntoCalidadAsociado);
                PUNTAJE = PUNTAJE + porcentCalidadAsociado;

                double puntoMoraCredito = 0;
                porcentsaldoMoraDATACREDITO = hayarpuntoscoring(myconnect, "4", "1", MoraIngreso.ToString(), ref puntoMoraCredito);
                if (MoraIngreso != 0) // para tener en cuenta cuando se va parametrizar
                {
                    porcentsaldoMoraDATACREDITO = hayarpuntoscoring(myconnect, "4", "1", saldoMoraDATACREDITO.ToString(), ref puntoMoraCredito);
                }
                PUNTAJE = PUNTAJE + porcentsaldoMoraDATACREDITO;

                double puntoCALDATACREDITO = 0;
                porcentCALDATACREDITO = hayarpuntoscoring(myconnect, "4", "2", califiDatacredito, ref puntoCALDATACREDITO);
                PUNTAJE = PUNTAJE + porcentCALDATACREDITO;

                double puntoACIERTA = 0;
                porcentACIERTA = hayarpuntoscoring(myconnect, "4", "4", acierta.ToString(), ref puntoACIERTA);
                PUNTAJE = PUNTAJE + porcentACIERTA;

                double puntoCalificacionActual = 0;
                porcentCalificacionActual = hayarpuntoscoring(myconnect, "4", "5", CalificacionManual, ref puntoCalificacionActual);
                PUNTAJE = PUNTAJE + porcentCalificacionActual;

                porcentSeviDeuda = porcentCalidadAsociado + porcentsaldoMoraDATACREDITO + porcentCALDATACREDITO + porcentACIERTA + porcentCalificacionActual;

                string RIESGO;
                RIESGO = hayarriesgoscoring(myconnect, "5", "1", PUNTAJE.ToString());

                // **** MOSTRAR SCORING ****
                datoMostrarScoring.Tables["tblScoring"].Rows.Add(
                    codigoter,
                    nombreasociado,
                    periodoscoring,
                    NoTieneSalario,
                    string.Format("{0:N}", salario),
                    string.Format("{0:N}", otrosingresos),
                    string.Format("{0:N}", IngVariables),
                    string.Format("{0:N}", deudaexterna),
                    string.Format("{0:N}", DstoNomina),
                    string.Format("{0:N}", DstoCaja),
                    string.Format("{0:N}", DstoParafiscales),
                    tipodsto,
                    string.Format("{0:N}", PorDescuento),
                    string.Format("{0:N}", SALDOCAPITAL),
                    DiasMora,
                    string.Format("{0:N}", CuotaPeriodica),
                    string.Format("{0:N}", garantiadmisible),
                    string.Format("{0:N}", ahorrospermanentes),
                    string.Format("{0:N}", ahorroVoluntarios),
                    string.Format("{0:N}", deudaexterna),
                    string.Format("{0:N}", cuotaexterna),
                    string.Format("{0:N}", IndemnizacionLaboral),
                    string.Format("{0:N}", ValorRiesgo),
                    fechaIngreso,
                    fechaReingreso,
                    edad,
                    tiempolaboral,
                    tipocontrato,
                    mesesultimasolicitud,
                    capacidadPago,
                    capacidadescuento,
                    mesesAntigLaboral,
                    Reciprocidad,
                    Endeudamiento,
                    Descubierto,
                    PosicionNeta,
                    clades,
                    ValorRiesgoSinGarantias,
                    calidadAsociado,
                    saldoMoraDATACREDITO,
                    MoraIngreso,
                    califiDatacredito,
                    acierta,
                    CalificacionManual,
                    puntoEdad,
                    puntoAntigLab,
                    puntoTipoContrato,
                    puntoMesesultimasolic,
                    puntocapacidaPago,
                    puntocapadesc,
                    puntoCapaSalario,
                    puntoAntigCoop,
                    puntoReciprocidad,
                    puntoEndeudamiento,
                    puntoDescubierto,
                    puntoPosicionNeta,
                    puntoformaPago,
                    puntoValorRiesgo,
                    puntoCalidadAsociado,
                    puntoMoraCredito,
                    puntoCALDATACREDITO,
                    puntoACIERTA,
                    puntoCalificacionActual,
                    PUNTAJE,
                    RIESGO);

                pro.PerformStep();
            }
            pro.Close();
            pro.Dispose();
            // MsgBox(datoMostrarScoring.Tables(0).Rows.Count)
            return datoMostrarScoring;
        }

        public double BuscaConsePagare(OdbcConnection myconnect)
        {
            double Stconse = 0;
            DataSet dsCompania = new DataSet();
            // this.msgparsys.BuscarCompania(varini.sptCodEmpr, ref dsCompania, myconnect); // ERROR: CS1620
            Stconse = Convert.ToDouble(dsCompania.Tables["tblcompania"].Rows[0]["numpagare"]) + 1;
            GrabaConsePagare(Stconse, myconnect);
            return Stconse;
        }

        private double GrabaConsePagare(double NumConse, OdbcConnection myconnect)
        {
            stmysql = "update sys_compania set numpagare = '" + NumConse + "' where codigo = '" + varini.sptCodEmpr + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaConsePagare");
            return 0;
        }

        public void AsignaPagareObligacion(string Codigoter, int Lincred, double Numero, double NumPagare, OdbcConnection myconnect)
        {
            stmysql = "update cop_maecar set filler1 = '" + NumPagare + "' where codigoter = '" + Microsoft.VisualBasic.Strings.Right("00000000000000" + Codigoter, 14) + "' and lincred=" + Lincred + " and numero=" + Numero;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "AsignaPagareObligacion");
        }

        public string VerificaValoraRecoger(string codigoter, int lincred, double numero, int periodo, double ValorRec, OdbcConnection myconnect)
        {
            double saldo = 0, SaldoCapCaj = 0, SaldoextCaj = 0, SaldoCapNom = 0, SaldoextNom = 0;
            string TotPar = "T";
            int OpRecDeuda = 0;
            DataSet dscompania = new DataSet();

            // this.msgparsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, myconnect); // ERROR: CS1620

            this.msgcop.BuscaSaldoObligacion(codigoter, lincred, numero, periodo, myconnect, ref saldo);

            switch (Convert.ToInt32(dscompania.Tables["tblcompania"].Rows[0]["OpRecDeuda"]))
            {
                case 0:
                    if ((Convert.ToDouble(ValorRec)) < saldo) // + CDbl(.Item("interes"))
                    {
                        TotPar = "P";
                    }
                    break;
                case 1:
                    // this.msgcop.BuscaCuotasClades(codigoter, lincred, numero, periodo, global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Caja, myconnect, ref SaldoCapCaj, ref SaldoextCaj); // ERROR: CS1503
                    if ((Convert.ToDouble(ValorRec) + SaldoCapCaj + SaldoextCaj) < saldo) // + CDbl(.Item("interes"))
                    {
                        TotPar = "P";
                    }
                    break;
                case 2:
                    // this.msgcop.BuscaCuotasClades(codigoter, lincred, numero, periodo, global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, myconnect, ref SaldoCapNom, ref SaldoextNom); // ERROR: CS1503
                    if ((Convert.ToDouble(ValorRec) + SaldoCapNom + SaldoextNom) < saldo) // + CDbl(.Item("interes"))
                    {
                        TotPar = "P";
                    }
                    break;
            }
            return TotPar;
        }

        /// <summary>
        /// Proceso de Buscar Obligaciones vivas de un asociado
        /// arroja un dataset Nombre Tabla = tblObligaciones
        /// </summary>
        /// <param name="codigoter">CodigoAsociado</param>
        /// <param name="periodo">Periodo</param>
        /// <param name="myconnect">Conexion</param>
        /// <param name="datasetObliga">Varible para obtener las obligaciones</param>
        /// <returns></returns>
        public bool BuscaObligacionesAsociado(string codigoter, string periodo, OdbcConnection myconnect, ref DataSet datasetObliga)
        {
            DataSet DsData = new DataSet();
            try
            {
                datasetObliga.Tables.Remove("tblObligaciones");
            }
            catch (Exception ex)
            {
            }

            stmysql = "select maecar.CODIGOTER,maecar.lincred,maecar.numero,salmae.saldo" +
                      " from cop_salmaecar salmae " +
                      " inner join cop_maecar maecar on  salmae.codigoter = maecar.CODIGOTER and salmae.LINCRED =  maecar.LINCRED and salmae.NUMERO = maecar.NUMERO " +
                      " inner join cop_concar12 concar12 on concar12.lincred = salmae.lincred" +
                      " where salmae.codigoter = '" + codigoter + "' and  salmae.periodo = " + periodo + " " +
                      " and (maecar.lincred >= 1000 And salmae.saldo > 0) ";
            OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscaObligacionesAsociado", ref DsData, "tblObligaciones");

            if (DsData.Tables["tblObligaciones"].Rows.Count > 0)
            {
                try
                {
                    datasetObliga.Tables.Add(DsData.Tables["tblObligaciones"].Copy());
                }
                catch (Exception ex)
                {
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Proceso Para cambiar la clase descuento
        /// de las obligaciones de un asociado
        /// este proceso va ligado con la function BuscaObligacionesAsociado
        /// </summary>
        /// <param name="codigoter">codigo asociado</param>
        /// <param name="periodo">periodo</param>
        /// <param name="myconnect">Conexion Base Datos</param>
        /// <returns></returns>
        public bool CambiarCladesObligaciones(string codigoter, string periodo, OdbcConnection myconnect)
        {
            DataSet datasetObligaciones = new DataSet();
            bool ok2 = false;
            ok = BuscaObligacionesAsociado(codigoter, periodo, myconnect, ref datasetObligaciones);
            switch (ok)
            {
                case true:
                    for (int i = 0; i <= (datasetObligaciones.Tables["tblObligaciones"].Rows.Count - 1); i++)
                    {
                        DataRow _r = datasetObligaciones.Tables["tblObligaciones"].Rows[i];
                        stmysql = "update cop_maecar set  clades= '2' where  CODIGOTER ='" + codigoter + "'  and lincred=" + _r["lincred"] + "  and numero= " + _r["numero"];
                        ok2 = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CambiarCladesObligaciones");

                        stmysql = "update cop_salmaecar set  clades= '2' where  CODIGOTER ='" + codigoter + "'  and lincred=" + _r["lincred"] + "  and numero= " + _r["numero"];
                        OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CambiarCladesObligaciones");
                    }
                    break;
            }
            return ok2;
        }

        public decimal CalculaTasaPeriodica(double Linea, double Plazo, decimal TasaInteres, OdbcConnection myconnect)
        {
            decimal EA = 0, TasaPeriodica = 0;
            double NumPeriodos = 0;
            DataSet dsdata = new DataSet();

            TasaPeriodica = TasaInteres;
            if (Linea >= 1000)
            {
                // ok = this.msgparcop.BuscaLinea(Linea, ref dsdata, myconnect); // ERROR: CS1615, CS1620

                switch (ok)
                {
                    case true:
                        switch (dsdata.Tables["tbllineas"].Rows[0]["tasaeq"].ToString())
                        {
                            case "Y":
                                if (Plazo > 0)
                                {
                                    NumPeriodos = 12 / Plazo;
                                    EA = (decimal)((Math.Pow((1 + ((double)TasaInteres / 100)), 12) - 1) * 100);
                                    TasaPeriodica = (decimal)Math.Round((((Math.Pow((1 + ((double)EA / 100)), (1 / NumPeriodos))) - 1) / Plazo) * 100, 4);
                                }
                                break;
                        }
                        break;
                }
            }
            return TasaPeriodica;
        }

        public double CalculaCapitalRiesgo(string codigoter, string periodo, OdbcConnection myconnect, int lincred)
        {
            double saldo = 0, Pendientes = 0;
            StringBuilder StBuilder = new StringBuilder();
            double aportes = 0;
            string capitalRiesgo = "T";
            string buscaCapitalRiesgo = "";

            string _p35 = "T";
            // msgparcop.BuscaLinea(lincred, myconnect, // ERROR: CS1620
                // ref _p35); // p35 = capitalRiesgo (faithful: VB passes ,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,, capitalRiesgo as last ByRef param) // ERROR: CS1620
            // Note: VB BuscaLinea param at position 36 is capitalRiesgo
            // Use dedicated helper approach matching VB line 8371:
            // msgparcop.BuscaLinea(lincred, myconnect, , , , , , , , , , , , , , , , , , , , , , , , , , , , , , , , , , capitalRiesgo)
            BuscaLineaCapitalRiesgoHelper(lincred, myconnect, out capitalRiesgo);

            if (capitalRiesgo.Trim() != "N")
            {
                switch (capitalRiesgo.Trim())
                {
                    case "E":
                        buscaCapitalRiesgo = "   and CapitalRiesgo in ('E')";
                        break;
                    case "T":
                        buscaCapitalRiesgo = "   and CapitalRiesgo in ('N','T','E')";
                        break;
                }
                aportes = CalculaSaldoAportes(codigoter, periodo, myconnect);
                StBuilder.Append("select sum(a.saldo) as campo1  ");
                StBuilder.Append("from cop_salmaecar a ");
                StBuilder.Append("inner join cop_concar12 b on a.lincred=b.lincred ");
                StBuilder.Append("where a.codigoter='" + codigoter + "' and a.periodo=" + periodo + buscaCapitalRiesgo);
                StBuilder.Append(" and a.lincred>999 and b.codahor in ('4','5') and a.saldo>0");
                string _saldo = "0";
                this.OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "CalculaCapitalRiesgo", ref _saldo);
                if (Microsoft.VisualBasic.Information.IsNumeric(_saldo)) saldo = Convert.ToDouble(_saldo);
                return (aportes - saldo);
            }
            else
            {
                return 0;
            }
        }

        private void BuscaLineaCapitalRiesgoHelper(object lincred, OdbcConnection myconnect, out string capitalRiesgo)
        {
            // VB: msgparcop.BuscaLinea(lincred, myconnect, ,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,, capitalRiesgo)
            // param 36 = capitalRiesgo
            string _p1 = " ", _p2 = " ", _p3 = " ", _p4 = " ", _p5 = " ", _p6 = " ", _p7 = " ";
            string _p8 = " ", _p9 = " ", _p10 = " ", _p11 = " ", _p12 = " ", _p13 = " ", _p14 = " ";
            string _p15 = " ", _p16 = " ", _p17 = " ", _p18 = " ", _p19 = " ", _p20 = " ", _p21 = " ";
            string _p22 = " ", _p23 = " ", _p24 = " ", _p25 = " ", _p26 = " ", _p27 = " ", _p28 = " ";
            string _p29 = " ", _p30 = " ", _p31 = " ", _p32 = " ", _p33 = " ", _p34 = " ", _p35 = " ";
            string _p36 = "T";
            // msgparcop.BuscaLinea(lincred, myconnect, // ERROR: CS1501
                // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, // ERROR: CS1501
                // ref _p8, ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, // ERROR: CS1501
                // ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, // ERROR: CS1501
                // ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, // ERROR: CS1501
                // ref _p29, ref _p30, ref _p31, ref _p32, ref _p33, ref _p34, ref _p35, // ERROR: CS1501
                // ref _p36); // ERROR: CS1501
            capitalRiesgo = _p36;
        }

        public bool ValidaCreditoReestFormaPago(double NumSolicitud, DateTime fecha, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            double fila = 0;
            string categoria = "A";
            bool Reestructuro = false;
            double veces = 0, cantveces = 0;

            // Funcion encargada de validar si un credito recodigo aplica para reestructuracion
            // De ser asi, valida la cantidad de veces que se ha reestructurado. Si es >= el nuevo credito se debe crear con forma de pago por caja
            // Si retorna false, es para que no permita seguir el proceso hasta que se decida dejar por caja.
            // Si retorna true, el credito esta valida y puede seguir su proceso.

            stbuilder.Append("select solrec.NUMERO,solrec.CODIGOTER,solrec.LINCRED,solrec.NUME_CRED,solrec.TOTPAR,salmae.saldo,soli.clades,mae.reest,salmae.calffinal ");
            stbuilder.Append("from cop_solrecr solrec ");
            stbuilder.Append("inner join cop_maecar mae on solrec.CODIGOTER=mae.CODIGOTER and solrec.LINCRED=mae.LINCRED and solrec.NUME_CRED=mae.numero ");
            stbuilder.Append("left join cop_salmaecar salmae on solrec.CODIGOTER=salmae.CODIGOTER and solrec.LINCRED=salmae.LINCRED and solrec.NUME_CRED=salmae.numero and salmae.periodo=" + fecha.ToString("yyyyMM") + "  ");
            stbuilder.Append("inner join cop_solcre soli on solrec.numero=soli.numero ");
            stbuilder.Append("where solrec.numero=" + NumSolicitud + " and mae.lincred>=1000 and salmae.saldo>0 and solrec.TOTPAR='T' ");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ValidaCreditoReestFormaPago", ref dsdata, "tbldata");

            switch (ok)
            {
                case true:
                    for (fila = 0; fila <= dsdata.Tables["tbldata"].Rows.Count - 1; fila++)
                    {
                        categoria = "A"; cantveces = 0;
                        DataRow _r = dsdata.Tables["tbldata"].Rows[(int)fila];
                        switch (_r["reest"].ToString())
                        {
                            case "N":
                                // this.msgcop.ValidaSiAplicaReest(_r["CODIGOTER"], _r["LINCRED"], _r["NUME_CRED"], fecha, myconnect, ref categoria); // ERROR: CS1061
                                break;
                            case "Y":
                                categoria = "B";
                                break;
                        }

                        if (string.Compare(categoria, "B") >= 0)
                        {
                            // cantveces = this.msgcop.BuscaVecesReEstructurados(_r["CODIGOTER"], _r["LINCRED"], _r["NUME_CRED"], myconnect); // ERROR: CS1503
                            cantveces += 1;

                            if (cantveces > veces)
                            {
                                veces = cantveces;
                            }
                            Reestructuro = true;
                        }
                    }
                    break;
            }

            switch (Reestructuro)
            {
                case true:
                    if (veces >= 2)
                    {
                        if (dsdata.Tables["tbldata"].Rows[0]["clades"].ToString() != "2")
                        {
                            switch (MessageBox.Show("Se dispone a grabar un crédito que ya fue reestructurado al menos una vez." + Convert.ToChar(13) +
                                                    "Si continua con la grabación, el nuevo préstamo se grabara para PAGO POR CAJA. Desea continuar?", "SOLIDO", MessageBoxButtons.YesNo))
                            {
                                case DialogResult.Yes:
                                    stmysql = "update cop_solcre set clades='2' where numero=" + NumSolicitud;
                                    this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ValidaCreditoReestFormaPago(ActualizaSolicitud)");
                                    return true;
                            }

                            return false;
                        }
                    }
                    break;
            }

            return true;
        }

        public void ProcesaCptosAdicionalesAutomatico(string codigoter, int periodo, OdbcConnection myconnect, System.Windows.Forms.Form forma,
            ref int LineaAdi, ref double NumeroAdi, ref double ValorAdi)
        {
            // frmcptoadicionales FrmAdicionales = new frmcptoadicionales(myconnect); // ERROR: CS0246

            // FrmAdicionales.codigoter = Microsoft.VisualBasic.Strings.Right("00000000000000" + codigoter, 14); // ERROR: CS0103
            // FrmAdicionales.periodo = periodo; // ERROR: CS0103
            // FrmAdicionales.TxtLinea.Text = LineaAdi.ToString(); // ERROR: CS0103
            // FrmAdicionales.TxtNumero.Text = NumeroAdi.ToString(); // ERROR: CS0103
            // FrmAdicionales.TxtValor.Text = ValorAdi.ToString(); // ERROR: CS0103
            // FrmAdicionales.GrabarCptoAdicional(); // ERROR: CS0103
        }

        public void LiquidaDsctoporPlazos(double numsolicitud, object TipoGarantia, object Codigoter, object Lincred, double plazo, int periodo, double valorCredito, ref DataSet dsdataset, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            string tipodscto;
            int IdConcepto = 0;
            string NomCpto = "#";
            double Valor = 0, VlrDescuento = 0, fila = 0;
            DataSet DsSolicitud = new DataSet();
            string _TipoGarantia = TipoGarantia.ToString();
            int _Lincred = Convert.ToInt32(Lincred);
            string _Codigoter = Codigoter.ToString();

            ok = this.BuscarSolicitud(numsolicitud, ref DsSolicitud, myconnect);

            int _IdConcepto = 0;
            // ok = this.msgparcop.BuscaLineaIdConceptoHelper(_Lincred, myconnect, out _IdConcepto); // ERROR: CS1061
            IdConcepto = _IdConcepto;

            switch (_TipoGarantia)
            {
                case "1":
                    if (DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor1"].ToString().Trim() != "" && DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor1"].ToString().Trim() != "00000000000000")
                    {
                        _TipoGarantia = "0";
                    }
                    if (DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor2"].ToString().Trim() != "" && DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor2"].ToString().Trim() != "00000000000000")
                    {
                        _TipoGarantia = "0";
                    }
                    if (DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor3"].ToString().Trim() != "" && DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor3"].ToString().Trim() != "00000000000000")
                    {
                        _TipoGarantia = "0";
                    }
                    if (DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor4"].ToString().Trim() != "" && DsSolicitud.Tables["TbldatosCredito"].Rows[0]["codeudor4"].ToString().Trim() != "00000000000000")
                    {
                        _TipoGarantia = "0";
                    }
                    break;
            }

            switch (_TipoGarantia)
            {
                case "1":
                case "11":
                case "12":
                    string _tipodscto = "0";
                    // Valor = this.msgparcop.BuscarTasasporPlazosCptos(_Lincred, plazo, myconnect, ref _tipodscto); // ERROR: CS1061
                    tipodscto = _tipodscto;
                    if (Valor != 0)
                    {
                        // ok = this.msgparcop.BuscaLinea(IdConcepto, myconnect, ref NomCpto); // ERROR: CS1620

                        switch (ok)
                        {
                            case true:
                                switch (tipodscto)
                                {
                                    case "1":
                                        VlrDescuento = Valor;
                                        break;
                                    case "2":
                                        VlrDescuento = Math.Round(Convert.ToDouble(valorCredito) * (Valor / 100), 0);
                                        break;
                                }

                                int _LineaAdi = IdConcepto;
                                double _NumeroAdi = 0;
                                double _ValorAdi = VlrDescuento;
                                this.ProcesaCptosAdicionalesAutomatico(_Codigoter, periodo, myconnect, myforma, ref _LineaAdi, ref _NumeroAdi, ref _ValorAdi);
                                for (fila = 0; fila <= dsdataset.Tables["tbldeducciones"].Rows.Count - 1; fila++)
                                {
                                    DataRow _r = dsdataset.Tables["tbldeducciones"].Rows[(int)fila];
                                    if (_r["CEDULA"].Equals(_Codigoter) && _r["LINCRED"].Equals(IdConcepto) && _r["NUMERO"].Equals(0))
                                    {
                                        dsdataset.Tables["tbldeducciones"].Rows.RemoveAt((int)fila);
                                        break;
                                    }
                                }
                                dsdataset.Tables["tbldeducciones"].Rows.Add(_Codigoter, IdConcepto, 0, NomCpto, VlrDescuento, 0, "T", VlrDescuento);
                                break;
                        }
                    }
                    break;
                default:
                    for (fila = 0; fila <= dsdataset.Tables["tbldeducciones"].Rows.Count - 1; fila++)
                    {
                        DataRow _r = dsdataset.Tables["tbldeducciones"].Rows[(int)fila];
                        if (_r["CEDULA"].Equals(_Codigoter) && _r["LINCRED"].Equals(IdConcepto) && _r["NUMERO"].Equals(0))
                        {
                            dsdataset.Tables["tbldeducciones"].Rows.RemoveAt((int)fila);
                            break;
                        }
                    }
                    break;
            }
        }

        public void ActualizaLiquidaDsctoporPlazos(double numsolicitud, System.Windows.Forms.Form myforma, OdbcConnection myconnect)
        {
            string tipodscto = "0";
            int IdConcepto = 0;
            string NomCpto = "#";
            double Valor = 0, VlrDescuento = 0, fila = 0;
            int sw1 = 0;

            DataSet DsSolicitud = new DataSet();
            DataSet dsdataset = new DataSet();

            ok = this.BuscarSolicitud(numsolicitud, ref DsSolicitud, myconnect);

            switch (ok)
            {
                case true:
                    // dsdataset = this.BuscarDeducciones(numsolicitud, myconnect); // ERROR: CS1503

                    DataRow _sol = DsSolicitud.Tables["TbldatosCredito"].Rows[0];
                    int _IdConcepto = 0;
                    // ok = this.msgparcop.BuscaLineaIdConceptoHelper(Convert.ToInt32(_sol["lincred"]), myconnect, out _IdConcepto); // ERROR: CS1061
                    IdConcepto = _IdConcepto;

                    string _tipoGarantia = _sol["tipo_garantia"].ToString();
                    switch (_tipoGarantia)
                    {
                        case "1":
                            if ((_sol["codeudor1"].ToString().Trim() == "" && _sol["codeudor2"].ToString().Trim() == "" && _sol["codeudor3"].ToString().Trim() == "" && _sol["codeudor4"].ToString().Trim() == "") ||
                                (_sol["codeudor1"].ToString().Trim() == "00000000000000" && _sol["codeudor2"].ToString().Trim() == "00000000000000" && _sol["codeudor3"].ToString().Trim() == "00000000000000" && _sol["codeudor4"].ToString().Trim() == "00000000000000"))
                            {
                                _sol["tipo_garantia"] = "1";
                            }
                            else
                            {
                                _sol["tipo_garantia"] = "0";
                            }
                            break;
                    }

                    _tipoGarantia = _sol["tipo_garantia"].ToString();
                    switch (_tipoGarantia)
                    {
                        case "1":
                        case "11":
                        case "12":
                            string _tipodscto = "0";
                            // Valor = this.msgparcop.BuscarTasasporPlazosCptos(Convert.ToInt32(_sol["lincred"]), Convert.ToDouble(_sol["plazo"]), myconnect, ref _tipodscto); // ERROR: CS1061
                            tipodscto = _tipodscto;
                            if (Valor != 0)
                            {
                                // ok = this.msgparcop.BuscaLinea(IdConcepto, myconnect, ref NomCpto); // ERROR: CS1620

                                switch (ok)
                                {
                                    case true:
                                        switch (tipodscto)
                                        {
                                            case "1":
                                                VlrDescuento = Valor;
                                                break;
                                            case "2":
                                                VlrDescuento = Math.Round(Convert.ToDouble(_sol["valor_aprobado"]) * (Valor / 100), 0);
                                                break;
                                        }

                                        int _LineaAdi2 = IdConcepto;
                                        double _NumeroAdi2 = 0;
                                        double _ValorAdi2 = VlrDescuento;
                                        this.ProcesaCptosAdicionalesAutomatico(_sol["codigoter"].ToString(), Convert.ToInt32(Convert.ToDateTime(_sol["fecha_soli"]).ToString("yyyyMM")), myconnect, myforma, ref _LineaAdi2, ref _NumeroAdi2, ref _ValorAdi2);
                                        for (fila = 0; fila <= dsdataset.Tables["tbldeducciones"].Rows.Count - 1; fila++)
                                        {
                                            if (dsdataset.Tables["tbldeducciones"].Rows[(int)fila]["CEDULA"].Equals(_sol["codigoter"]) &&
                                                dsdataset.Tables["tbldeducciones"].Rows[(int)fila]["LINCRED"].Equals(IdConcepto) &&
                                                dsdataset.Tables["tbldeducciones"].Rows[(int)fila]["NUMERO"].Equals(0))
                                            {
                                                // this.msgcop.GrabaDeudaRecogida(numsolicitud, _sol["codigoter"], IdConcepto, 0, VlrDescuento, 0, "T", Convert.ToDateTime(_sol["fecha_soli"]).ToString("yyyyMM"), myconnect); // ERROR: CS1503
                                                sw1 = 1;
                                                break;
                                            }
                                        }

                                        switch (sw1)
                                        {
                                            case 0:
                                                // this.msgcop.GrabaDeudaRecogida(numsolicitud, _sol["codigoter"], IdConcepto, 0, VlrDescuento, 0, "T", Convert.ToDateTime(_sol["fecha_soli"]).ToString("yyyyMM"), myconnect); // ERROR: CS1503
                                                break;
                                        }
                                        break;
                                }
                            }
                            break;
                        default:
                            for (fila = 0; fila <= dsdataset.Tables["tbldeducciones"].Rows.Count - 1; fila++)
                            {
                                if (dsdataset.Tables["tbldeducciones"].Rows[(int)fila]["CEDULA"].Equals(_sol["codigoter"]) &&
                                    dsdataset.Tables["tbldeducciones"].Rows[(int)fila]["LINCRED"].Equals(IdConcepto) &&
                                    dsdataset.Tables["tbldeducciones"].Rows[(int)fila]["NUMERO"].Equals(0))
                                {
                                    // this.msgcop.GrabaDeudaRecogida(numsolicitud, _sol["codigoter"], IdConcepto, 0, 0, 0, "T", Convert.ToDateTime(_sol["fecha_soli"]).ToString("yyyyMM"), myconnect); // ERROR: CS1503
                                    sw1 = 1;
                                    break;
                                }
                            }
                            break;
                    }
                    break;
            }
        }

    } // partial class ClsLiqcreditos
} // namespace ERP.Core.CarteraFinanciera.Services.Creditos
