using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services.TarjetaCred
{
    public class Clstarjcredito
    {
        private OdbcCommand mycomqueryconec = new OdbcCommand();
        private ERP.Core.Compartido.Datos.ClsConect msgodbc = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private ERP.Core.CarteraFinanciera.Services.Debitos.ClsMsgDeb msgdeb = new ERP.Core.CarteraFinanciera.Services.Debitos.ClsMsgDeb();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private bool ok;

        public Clstarjcredito()
        {
            msgodbc.MyOdbcConect(varini);
        }

        public virtual bool BuscaParametros(string IdAgencia, string Idbanco, DataSet Dsparcredito, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            try
            {
                Dsparcredito.Tables["tblparcre"].Clear();
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select Codigo_banco,Cupo,TipoAvance,VlrAvance,TasaAvance,Lincred,LincredAvance,Clades,Consecutivo,DiasVence,DiaCorte,");
            stbuilder.Append("Monto1,Plazo1,Periode1,Monto2,Plazo2,Periode2,Monto3,Plazo3,Periode3,Monto4,Plazo4,Periode4,Monto5,Plazo5,Periode5,gravamen,Monto6,Plazo6,Periode6,Monto7,Plazo7,Periode7, ");
            stbuilder.Append("PlazoAvance1,PlazoAvance2,PlazoAvance3,PlazoAvance4,PlazoAvance5,PlazoAvance6,PlazoAvance7,lincredcuoman,vlrcuoman ");
            stbuilder.Append("from cre_parame01 ");
            stbuilder.Append("where Codigo_banco = '" + Idbanco + "' and idagencia = '" + IdAgencia + "'");

            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaParametros", Dsparcredito, "tblparcre");
            if (Dsparcredito.Tables["tblparcre"].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void GrabaConseObligaciones(string IdAgencia, string Idbanco, double consecutivo, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("update cre_parame01 set Consecutivo = '");
            stbuilder.Append(consecutivo + "' ");
            stbuilder.Append("where Codigo_banco = '" + Idbanco + "' and idagencia = '" + IdAgencia + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaConseObligaciones");
        }

        public void GrabaMovimiento(string Idtarjeta, string banco, string Cpte, double ConseCpte, int Transaccion, double Monto,
            DateTime FechaMovto, DateTime FecObligacion, string Tipomovto, string Numterminal, string usuario, double Comision, string IdTransaccion,
            OdbcConnection myconnect, DateTime FecDstoSemanal = default(DateTime), bool DesdeLinea = false, string NomTermLinea = "")
        {
            if (FecDstoSemanal == default(DateTime))
                FecDstoSemanal = new DateTime(1950, 1, 1);

            DataSet dsdatoscredito = new DataSet();
            string lincred = "9999";
            double Numconse = 0;
            int plazo = 0;
            int periode = 0;
            double cuota = 0;
            int Numcuotas = 0;
            decimal TasaInt = 0;
            int clades = 0;
            string IdAgencia = "0";
            string codigoter = "99999999999999";
            double cupo = 0;
            string Codeudor1 = "99999999999999";
            string codeudor2 = "99999999999999";
            int tipocta = 0;
            int trancajero = 0;
            int tranpos = 0;
            double Disponible = 0;
            double avances = 0;
            double DiasMora = 0;
            string estado = "99";
            string diamovto = "30";
            int meses = 0;
            int DiaCorte = 0;
            string Bloqueo = "";
            DateTime fecdes = default(DateTime);
            double Credito = 0;
            string DebCred = "C";
            string CobraManejo = "Y";
            string CobraManejoDs = "Y";
            decimal TasaIntseg = 0;
            decimal TasaIntAdmon = 0;
            string forseg = "";
            int tiposervicio = 0;
            DateTime fecvence = new DateTime(1950, 1, 1);
            string PerioddHojaVida = "1";
            DataSet dsreverso = new DataSet();
            double csCuota = 0;
            double VlrGravamen = 0;
            DataSet dscompania = new DataSet();
            string CptoGravamen = "99";
            bool Yaexiste = false;
            double SaldoObligacion = 0;
            string StMysql = "";
            double StTasaSeguro = 0;
            string cptocap = "99";
            string nit = "99999999999999";
            StringBuilder stBuilder = new StringBuilder();
            DataSet dsdatos = new DataSet();
            string empresa = "";
            double fila = 0;

            if (Monto <= 0)
            {
                return;
            }

            banco = ("0000" + banco).Substring(("0000" + banco).Length - 4);
            Idtarjeta = Idtarjeta.Trim();

            stBuilder.Append("select debmae.Estado,debmae.Codigoter,debmae.DebCre,debmae.CupoCredito,debmae.Codeudor1,");
            stBuilder.Append("debmae.Codeudor2,debmae.fechavence,debmae.CobraManejo,debmae.CobraManejoDS,maenit.AGENCIA,maenit.periodo_desto,");
            stBuilder.Append("cia.Cpto4Mil,crepar.Consecutivo,crepar.Clades,crepar.LincredAvance,crepar.Lincred,crepar.DiaCorte,");
            stBuilder.Append("crepar.Monto1,crepar.plazo1,crepar.Periode1,crepar.PlazoAvance1,crepar.Monto2,crepar.plazo2,crepar.Periode2,crepar.PlazoAvance2,");
            stBuilder.Append("crepar.Monto3,crepar.plazo3,crepar.Periode3,crepar.PlazoAvance3,crepar.Monto4,crepar.plazo4,crepar.Periode4,crepar.PlazoAvance4,");
            stBuilder.Append("crepar.Monto5,crepar.plazo5,crepar.Periode5,crepar.PlazoAvance5,crepar.Monto6,crepar.plazo6,crepar.Periode6,crepar.PlazoAvance6,");
            stBuilder.Append("crepar.Monto7,crepar.plazo7,crepar.Periode7,crepar.PlazoAvance7,crepar.gravamen,cia.CPTO_CAPITAL,maenit.nit,maenit.empresa ");
            stBuilder.Append("from deb_maetarj debmae ");
            stBuilder.Append("inner join sys_maenit maenit on maenit.CODIGOTER = debmae.Codigoter ");
            stBuilder.Append("inner join sys_compania cia on cia.CODIGO ='0001' ");
            stBuilder.Append("inner join cre_parame01 crepar on maenit.AGENCIA=crepar.idagencia and debmae.Banco=crepar.Codigo_banco ");
            stBuilder.Append("where debmae.Banco='" + banco + "' and debmae.Tarjeta='" + Idtarjeta.Trim() + "'");

            ok = this.msgodbc.ExecuteQueryDataset(stBuilder.ToString(), myconnect, "GrabaMovimiento(BuscaDatos)", dsdatos, "tbldata");

            switch (ok)
            {
                case true:
                    // msgdeb.BuscaConvenio(banco, dsdatoscredito, myconnect); // ERROR: CS1620

                    DataRow convenioRow = dsdatoscredito.Tables["tblconvenio"].Rows[0];
                    tipocta = Convert.ToInt32(convenioRow["ahorro"]);
                    trancajero = Convert.ToInt32(convenioRow["TraCajero"]);
                    tranpos = Convert.ToInt32(convenioRow["TraPos"]);
                    Bloqueo = Convert.ToString(convenioRow["bloqueo"]);
                    tiposervicio = Convert.ToInt32(convenioRow["TipoServicio"]);

                    DataRow dataRow = dsdatos.Tables["tbldata"].Rows[0];
                    estado = Convert.ToString(dataRow["Estado"]);
                    codigoter = Convert.ToString(dataRow["Codigoter"]);
                    DebCred = Convert.ToString(dataRow["DebCre"]);
                    cupo = Convert.ToDouble(dataRow["CupoCredito"]);
                    Codeudor1 = Convert.ToString(dataRow["Codeudor1"]);
                    codeudor2 = Convert.ToString(dataRow["Codeudor2"]);
                    fecvence = Convert.ToDateTime(dataRow["fechavence"]);
                    CobraManejo = Convert.ToString(dataRow["CobraManejo"]);
                    CobraManejoDs = Convert.ToString(dataRow["CobraManejoDS"]);
                    IdAgencia = Convert.ToString(dataRow["agencia"]);
                    PerioddHojaVida = Convert.ToString(dataRow["periodo_desto"]);
                    CptoGravamen = Convert.ToString(dataRow["Cpto4Mil"]);
                    cptocap = Convert.ToString(dataRow["CPTO_CAPITAL"]);
                    nit = Convert.ToString(dataRow["nit"]);
                    empresa = Convert.ToString(dataRow["empresa"]);

                    DataRow parcreRow = dsdatos.Tables["tbldata"].Rows[0];

                    switch (DesdeLinea)
                    {
                        case false:
                            Numconse = Convert.ToDouble(parcreRow["Consecutivo"]) + 1;
                            break;
                        case true:
                            string midValue = IdTransaccion.Length >= 5 ? IdTransaccion.Substring(4) : IdTransaccion;
                            if (double.TryParse(midValue, out double numResult))
                            {
                                Numconse = numResult;
                            }
                            else if (double.TryParse(IdTransaccion.Trim(), out double numResult2))
                            {
                                Numconse = numResult2;
                            }
                            else
                            {
                                Numconse = Convert.ToDouble(parcreRow["Consecutivo"]) + 1;
                            }
                            break;
                    }

                    clades = Convert.ToInt32(parcreRow["Clades"]);

                    switch (Transaccion.ToString())
                    {
                        case "2":
                        case "1":
                        case "4":
                            lincred = Convert.ToString(parcreRow["LincredAvance"]);
                            break;
                        case "3":
                        case "9":
                            lincred = Convert.ToString(parcreRow["Lincred"]);
                            break;
                        case "5":
                        case "8":
                            lincred = Convert.ToString(parcreRow["Lincred"]);
                            Credito = Monto - Comision;
                            Monto = 0;
                            Comision = 0;
                            dsreverso = BuscarObligacionReverso(IdTransaccion, myconnect);
                            if (dsreverso.Tables["tblreverso"].Rows.Count > 0)
                            {
                                lincred = Convert.ToString(dsreverso.Tables["tblreverso"].Rows[0]["Lincred"]);
                                Numconse = Convert.ToDouble(dsreverso.Tables["tblreverso"].Rows[0]["numero"]);
                            }
                            break;
                    }

                    DiaCorte = Convert.ToInt32(parcreRow["diacorte"]);

                    // ok = msgdeb.BuscaTerminal(Numterminal, myconnect); // ERROR: CS7036
                    switch (ok)
                    {
                        case true:
                            lincred = Convert.ToString(parcreRow["LincredAvance"]);
                            break;
                    }

                    if (Monto <= Convert.ToDouble(parcreRow["Monto1"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo1"]);
                        periode = Convert.ToInt32(parcreRow["Periode1"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance1"]);
                        }
                    }
                    else if (Monto <= Convert.ToDouble(parcreRow["Monto2"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo2"]);
                        periode = Convert.ToInt32(parcreRow["Periode2"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance2"]);
                        }
                    }
                    else if (Monto <= Convert.ToDouble(parcreRow["Monto3"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo3"]);
                        periode = Convert.ToInt32(parcreRow["Periode3"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance3"]);
                        }
                    }
                    else if (Monto <= Convert.ToDouble(parcreRow["Monto4"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo4"]);
                        periode = Convert.ToInt32(parcreRow["Periode4"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance4"]);
                        }
                    }
                    else if (Monto <= Convert.ToDouble(parcreRow["Monto5"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo5"]);
                        periode = Convert.ToInt32(parcreRow["Periode5"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance5"]);
                        }
                    }
                    else if (Monto <= Convert.ToDouble(parcreRow["Monto6"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo6"]);
                        periode = Convert.ToInt32(parcreRow["Periode6"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance6"]);
                        }
                    }
                    else if (Monto <= Convert.ToDouble(parcreRow["Monto7"]))
                    {
                        plazo = Convert.ToInt32(parcreRow["plazo7"]);
                        periode = Convert.ToInt32(parcreRow["Periode7"]);
                        if (lincred == Convert.ToString(parcreRow["LincredAvance"]))
                        {
                            plazo = Convert.ToInt32(parcreRow["PlazoAvance7"]);
                        }
                    }

                    if (periode == 6)
                    {
                        if (int.TryParse(PerioddHojaVida, out int perioddResult))
                        {
                            periode = perioddResult;
                        }
                        else
                        {
                            periode = 1;
                        }
                    }

                    // ok = msgparcop.BuscaLinea(lincred, dsdatoscredito, myconnect); // ERROR: CS1503, CS1620
                    switch (ok)
                    {
                        case false:
                            MessageBox.Show("Linea de cartera no esta creada " + lincred, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                    }

                    Numcuotas = plazo * periode;

                    DataRow lineasRow = dsdatoscredito.Tables["tbllineas"].Rows[0];
                    TasaInt = Math.Round((Convert.ToDecimal(lineasRow["tasai"]) / periode) / 100, 8);

                    if (periode == 4)
                    {
                        TasaInt = Math.Round(((Convert.ToDecimal(lineasRow["tasai"]) / 30) * 7) / 100, 8);
                        Numcuotas = Convert.ToInt32((plazo * 52) / 12);
                    }

                    switch (Convert.ToString(lineasRow["POAPEN"]))
                    {
                        case "3":
                            TasaIntseg = Math.Round((Convert.ToDecimal(lineasRow["seguro"]) / periode) / 100, 8);
                            break;
                        case "9":
                            csCuota = Math.Round(Monto * (Convert.ToDouble(lineasRow["seguro"]) / 100) / plazo);
                            StTasaSeguro = Convert.ToDouble(lineasRow["seguro"]);
                            lineasRow["seguro"] = 0;
                            break;
                        default:
                            lineasRow["seguro"] = 0;
                            break;
                    }

                    switch (Convert.ToString(lineasRow["claadmon"]))
                    {
                        case "2":
                            TasaIntAdmon = Math.Round((Convert.ToDecimal(lineasRow["tasadm"]) / periode) / 100, 8);
                            break;
                        default:
                            lineasRow["tasadm"] = 0;
                            break;
                    }

                    switch (Convert.ToInt32(lineasRow["clacuo"]))
                    {
                        case 1:
                            cuota = Convert.ToInt32(Financial.Pmt(Convert.ToDouble(TasaInt + TasaIntseg + TasaIntAdmon), Numcuotas, (-Monto), 0));
                            break;
                        case 2:
                            cuota = Convert.ToInt32(((Monto) / plazo) + 0.5);
                            break;
                    }

                    cuota += csCuota;

                    // ok = msgcop.BuscaObligacion(codigoter, lincred, Numconse, myconnect); // ERROR: CS1503

                    switch (ok)
                    {
                        case true:
                            // msgcop.ActualizaNombreTerminalTDLinea(codigoter, lincred, Numconse, NomTermLinea, myconnect); // ERROR: CS1503
                            Yaexiste = true;
                            break;
                        case false:
                            switch (periode)
                            {
                                case 1:
                                    if (Convert.ToDouble(FecObligacion.AddMonths(1).ToString("MM")) == 2)
                                    {
                                        diamovto = "28";
                                    }
                                    else
                                    {
                                        diamovto = "30";
                                    }
                                    meses = 1;
                                    break;
                                case 2:
                                    if (FecObligacion.Day <= 15)
                                    {
                                        if (Convert.ToDouble(FecObligacion.ToString("MM")) == 2)
                                        {
                                            diamovto = "28";
                                        }
                                        else
                                        {
                                            diamovto = "30";
                                        }
                                        meses = 0;
                                    }
                                    else
                                    {
                                        diamovto = "15";
                                        meses = 1;
                                    }
                                    break;
                                case 3:
                                    if (FecObligacion.Day <= 10)
                                    {
                                        diamovto = "20";
                                        meses = 0;
                                    }
                                    else if (FecObligacion.Day <= 20)
                                    {
                                        if (Convert.ToDouble(FecObligacion.ToString("MM")) == 2)
                                        {
                                            diamovto = "28";
                                        }
                                        else
                                        {
                                            diamovto = "30";
                                        }
                                        meses = 0;
                                    }
                                    else
                                    {
                                        diamovto = "10";
                                        meses = 1;
                                    }
                                    break;
                                case 4:
                                    fecdes = FecDstoSemanal;
                                    break;
                            }

                            if (periode != 4)
                            {
                                DateTime fechaAjustada = FecObligacion.AddMonths(meses);
                                fecdes = DateTime.Parse(fechaAjustada.ToString("yyyy") + "/" + fechaAjustada.ToString("MM") + "/" + diamovto);
                            }

                            if (Codeudor1.Trim() == "" || Codeudor1.Trim() == "99999999999999")
                            {
                                Codeudor1 = "";
                            }
                            if (codeudor2.Trim() == "" || codeudor2.Trim() == "99999999999999")
                            {
                                codeudor2 = "";
                            }

                            // msgcop.GrabaNuevoCredito(codigoter, lincred, Numconse, FecObligacion, FecObligacion, FecObligacion, fecdes, " ", plazo, Monto, Monto, 0, cuota, Convert.ToDouble(lineasRow["tasai"]), 5, periode, Convert.ToInt32(lineasRow["clacuo"]), Convert.ToInt32(lineasRow["clasei"]), usuario, FechaMovto, "9999", "99999999", FecObligacion.ToString("yyyyMM"), myconnect, "", clades, Convert.ToDouble(lineasRow["tasadm"]), Convert.ToDouble(lineasRow["seguro"]), 0, 0, 0, 0, 0, 0, 0, 0, Codeudor1, codeudor2, "", "", "", "", "", "", "", "", "", "", "", "", "", empresa, "", Convert.ToString(lineasRow["POAPEN"]), IdTransaccion, "", "", Idtarjeta, NomTermLinea, false); // ERROR: CS1503

                            switch (DesdeLinea)
                            {
                                case false:
                                    this.GrabaConseObligaciones(IdAgencia, banco, Numconse, myconnect);
                                    break;
                            }
                            break;
                    }

                    if (Monto > 0)
                    {
                        Monto -= Comision;
                    }

                    if (Convert.ToDouble(dsdatos.Tables["tbldata"].Rows[0]["gravamen"]) > 0)
                    {
                        VlrGravamen = Math.Round(Monto * (Convert.ToDouble(dsdatos.Tables["tbldata"].Rows[0]["gravamen"]) / 100), 0);
                    }

                    if ((Monto + VlrGravamen) != 0 || Credito != 0)
                    {
                        // msgcop.GrabaCapital(Cpte, ConseCpte, codigoter, lincred, Numconse, Monto + VlrGravamen, Credito, FechaMovto.ToString("yyyyMM"), FechaMovto, 999999, "Movimiento de tarjeta " + FechaMovto.ToString(varini.PstForFec), myconnect, cptocap, "", usuario); // ERROR: CS1503
                    }

                    if (VlrGravamen > 0)
                    {
                        // msgcop.GrabaGmf(Cpte, ConseCpte, codigoter, lincred, Numconse, 0, VlrGravamen, FechaMovto.ToString("yyyyMM"), FechaMovto, 999999, "Movimiento de tarjeta - GMF " + FechaMovto.ToString(varini.PstForFec), myconnect, CptoGravamen, "", usuario); // ERROR: CS1503
                    }

                    VlrGravamen = 0;

                    if (Comision > 0)
                    {
                        if (Convert.ToDouble(dsdatos.Tables["tbldata"].Rows[0]["gravamen"]) > 0)
                        {
                            VlrGravamen = Math.Round(Comision * (Convert.ToDouble(dsdatos.Tables["tbldata"].Rows[0]["gravamen"]) / 100), 0);
                        }

                        // msgcop.GrabaCapital(Cpte, ConseCpte, codigoter, lincred, Numconse, Comision + VlrGravamen, Credito, FechaMovto.ToString("yyyyMM"), FechaMovto, 999999, "Movimiento de tarjeta " + FechaMovto.ToString(varini.PstForFec), myconnect, cptocap, "", usuario); // ERROR: CS1503

                        if (VlrGravamen > 0)
                        {
                            // msgcop.GrabaGmf(Cpte, ConseCpte, codigoter, lincred, Numconse, 0, VlrGravamen, FechaMovto.ToString("yyyyMM"), FechaMovto, 999999, "Movimiento de tarjeta - GMF " + FechaMovto.ToString(varini.PstForFec), myconnect, CptoGravamen, "", usuario); // ERROR: CS1503
                        }
                    }

                    if (Yaexiste)
                    {
                        if (DesdeLinea)
                        {
                            switch (Transaccion.ToString())
                            {
                                case "2":
                                case "1":
                                case "4":
                                case "3":
                                    if (Monto == 0 && Comision != 0)
                                    {
                                        // msgcop.BuscaSaldoObligacion(codigoter, lincred, Numconse, FechaMovto.ToString("yyyyMM"), myconnect, ref SaldoObligacion); // ERROR: CS1503

                                        DataRow parcreRow2 = dsdatos.Tables["tbldata"].Rows[0];
                                        if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto1"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo1"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode1"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance1"]);
                                            }
                                        }
                                        else if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto2"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo2"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode2"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance2"]);
                                            }
                                        }
                                        else if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto3"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo3"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode3"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance3"]);
                                            }
                                        }
                                        else if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto4"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo4"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode4"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance4"]);
                                            }
                                        }
                                        else if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto5"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo5"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode5"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance5"]);
                                            }
                                        }
                                        else if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto6"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo6"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode6"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance6"]);
                                            }
                                        }
                                        else if (SaldoObligacion <= Convert.ToDouble(parcreRow2["Monto7"]))
                                        {
                                            plazo = Convert.ToInt32(parcreRow2["plazo7"]);
                                            periode = Convert.ToInt32(parcreRow2["Periode7"]);
                                            if (lincred == Convert.ToString(parcreRow2["LincredAvance"]))
                                            {
                                                plazo = Convert.ToInt32(parcreRow2["PlazoAvance7"]);
                                            }
                                        }

                                        if (periode == 6)
                                        {
                                            if (int.TryParse(PerioddHojaVida, out int perioddResult2))
                                            {
                                                periode = perioddResult2;
                                            }
                                            else
                                            {
                                                periode = 1;
                                            }
                                        }

                                        Numcuotas = plazo * periode;

                                        TasaInt = Math.Round((Convert.ToDecimal(lineasRow["tasai"]) / periode) / 100, 8);

                                        if (periode == 4)
                                        {
                                            TasaInt = Math.Round(((Convert.ToDecimal(lineasRow["tasai"]) / 30) * 7) / 100, 8);
                                            Numcuotas = Convert.ToInt32((plazo * 52) / 12);
                                        }

                                        switch (Convert.ToString(lineasRow["POAPEN"]))
                                        {
                                            case "3":
                                                TasaIntseg = Math.Round((Convert.ToDecimal(lineasRow["seguro"]) / periode) / 100, 8);
                                                break;
                                            case "9":
                                                csCuota = Math.Round(SaldoObligacion * (StTasaSeguro / 100) / plazo);
                                                lineasRow["seguro"] = 0;
                                                break;
                                            default:
                                                lineasRow["seguro"] = 0;
                                                break;
                                        }

                                        switch (Convert.ToString(lineasRow["claadmon"]))
                                        {
                                            case "2":
                                                TasaIntAdmon = Math.Round((Convert.ToDecimal(lineasRow["tasadm"]) / periode) / 100, 8);
                                                break;
                                            default:
                                                lineasRow["tasadm"] = 0;
                                                break;
                                        }

                                        switch (Convert.ToInt32(lineasRow["clacuo"]))
                                        {
                                            case 1:
                                                cuota = Convert.ToInt32(Financial.Pmt(Convert.ToDouble(TasaInt + TasaIntseg + TasaIntAdmon), Numcuotas, (-SaldoObligacion), 0));
                                                break;
                                            case 2:
                                                cuota = Convert.ToInt32(((SaldoObligacion) / plazo) + 0.5);
                                                break;
                                        }

                                        cuota += csCuota;

                                        StMysql = "update cop_maecar set valorob='" + SaldoObligacion + "',vlrsolicitud='" + SaldoObligacion + "',cuota='" + cuota + "'," +
                                                  "plazo='" + plazo + "',periodd='" + periode + "' where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + Numconse;
                                        this.msgodbc.ExecuteQueryconec(StMysql, myconnect, "GrabaMovimiento(ActualizaParametrosObligacion)");
                                        // msgcop.ActualizarCuaotaSalmaecar(codigoter, lincred, Numconse, FechaMovto.ToString("yyyyMM"), myconnect); // ERROR: CS1503
                                    }
                                    break;
                            }
                        }
                    }

                    DiasMora = this.BuscaDiasMora(codigoter, FechaMovto.ToString("yyyyMM"), Convert.ToString(dsdatos.Tables["tbldata"].Rows[0]["LincredAvance"]), myconnect);
                    DiasMora += this.BuscaDiasMora(codigoter, FechaMovto.ToString("yyyyMM"), Convert.ToString(dsdatos.Tables["tbldata"].Rows[0]["Lincred"]), myconnect);

                    if (DiasMora > 0 && estado != "62")
                    {
                        this.BloqueaTarjeta(Idtarjeta, "99", "6", usuario, myconnect);
                    }
                    else
                    {
                        if (DiasMora == 0 && estado == "99")
                        {
                            this.BloqueaTarjeta(Idtarjeta, " ", "0", usuario, myconnect);
                        }
                    }
                    break;
            }

            dsdatos.Dispose();
            dsreverso.Dispose();
            dsdatoscredito.Dispose();
        }

        public void RevisaTarjetas(string Banco, string Periodo, Form Myforma, string usuario, OdbcConnection myconnect)
        {
            string stmysql;
            DataSet DsDatatarj = new DataSet();
            double fila = 0;
            string codigoter;
            string IdAgencia;
            int sw1 = 0;
            int Diasmora = 0;
            string Idtarjeta;
            string estado = null;
            ERP.Core.Compartido.Controles.Barraprogress Myprogres = new ERP.Core.Compartido.Controles.Barraprogress("Revisando tarjetas", Myforma);

            stmysql = "Select debtarj.estado,debtarj.codigoter,debtarj.tarjeta,debtarj.cuenta,debtarj.lincred,debtarj.disponible," +
                      "debtarj.cupocajero,debtarj.trancajero,debtarj.cupopos,debtarj.tranpos,debtarj.error,debtarj.debcre,debtarj.cupocredito,maenit.apellido,maenit.nombre,maenit.agencia,saldos.saldo" +
                      " from deb_maetarj debtarj inner join sys_maenit maenit on debtarj.codigoter=maenit.codigoter " +
                      "left join cop_saldos_vw saldos on saldos.codigoter = debtarj.codigoter and saldos.lincred= debtarj.lincred and saldos.numero = debtarj.cuenta and saldos.periodo = " + Periodo + "" +
                      " where debtarj.estado = 'A' and debcre = 'C' and banco = '" + Banco + "'";
            this.msgodbc.ExecuteQueryDataset(stmysql, myconnect, "RevisaTarjetas", DsDatatarj, "tbltarjetas");

            Myprogres.DefineMaximo(stmysql, myconnect);
            Myprogres.Show();

            while (fila < DsDatatarj.Tables["tbltarjetas"].Rows.Count)
            {
                DataRow row = DsDatatarj.Tables["tbltarjetas"].Rows[Convert.ToInt32(fila)];
                codigoter = Convert.ToString(row["codigoter"]);
                Idtarjeta = Convert.ToString(row["tarjeta"]);
                if (row["error"] == DBNull.Value)
                {
                    estado = " ";
                }
                else
                {
                    estado = Convert.ToString(row["error"]);
                }

                sw1 = 0;

                // ok = msgparcop.BuscaAsociado(codigoter, DsDatatarj, myconnect); // ERROR: CS1620
                switch (ok)
                {
                    case false:
                        MessageBox.Show("Asociado no existe " + codigoter, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        sw1 = 1;
                        break;
                }

                IdAgencia = Convert.ToString(DsDatatarj.Tables["tblasociados"].Rows[0]["agencia"]);
                ok = BuscaParametros(IdAgencia, Banco, DsDatatarj, myconnect);
                switch (ok)
                {
                    case false:
                        MessageBox.Show("Parametros de agencia no existe, tarjeta credito cedula  " + codigoter, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        sw1 = 1;
                        break;
                }

                if (sw1 == 0)
                {
                    Diasmora = Convert.ToInt32(this.BuscaDiasMora(codigoter, Periodo, Convert.ToString(DsDatatarj.Tables["tblparcre"].Rows[0]["LincredAvance"]), myconnect));
                    Diasmora += Convert.ToInt32(this.BuscaDiasMora(codigoter, Periodo, Convert.ToString(DsDatatarj.Tables["tblparcre"].Rows[0]["Lincred"]), myconnect));

                    if (Diasmora > 0 && estado != "62")
                    {
                        this.BloqueaTarjeta(Idtarjeta, "99", "6", usuario, myconnect);
                    }
                    else
                    {
                        if (Diasmora == 0 && estado == "99")
                        {
                            this.BloqueaTarjeta(Idtarjeta, " ", "0", usuario, myconnect);
                        }
                    }
                }

                Myprogres.PerformStep();
                fila += 1;
            }

            Myprogres.Close();
            Myprogres.Dispose();
        }

        public bool BloqueaTarjeta(string Numtarjeta, string CodBloqueo, string Motivo, string usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("update deb_maetarj set error ='" + CodBloqueo + "',MotivoBloqueo='" + Motivo + "',UsuarioBloqueo='" + usuario + "' where tarjeta = '" + Numtarjeta + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BloqueaTarjeta");
            return true;
        }

        public virtual bool BuscaParametros(string IdAgencia, string Idbanco, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select Codigo_banco,Cupo,TipoAvance,VlrAvance,TasaAvance,Lincred,LincredAvance,Clades,Consecutivo,DiasVence,DiaCorte,");
            stbuilder.Append("Monto1,Plazo1,Periode1,Monto2,Plazo2,Periode2,Monto3,Plazo3,Periode3,Monto4,Plazo4,Periode4,Monto5,Plazo5,Periode5,gravamen,Monto6,Plazo6,Periode6,Monto7,Plazo7,Periode7, ");
            stbuilder.Append("PlazoAvance1,PlazoAvance2,PlazoAvance3,PlazoAvance4,PlazoAvance5,PlazoAvance6,PlazoAvance7,lincredcuoman,vlrcuoman ");
            stbuilder.Append("from cre_parame01 ");
            stbuilder.Append("where IdAgencia = '" + IdAgencia + "' and Codigo_banco = '" + Idbanco + "'");

            ok = this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscaParametros");
            return ok;
        }

        public void GrabaParametros(string IdAgencia, string Idbanco, double Cupo, string LineaCredito, string LineaAvances,
            int clades, double Consecutivo, int DiasVence, int TipoAvance, double ValAvance, decimal TasaAvance, int DiasCorte,
            double monto1, int Periodicidad1, int plazo1, double monto2, int Periodicidad2, int plazo2,
            double monto3, int Periodicidad3, int plazo3, double monto4, int Periodicidad4, int plazo4,
            double monto5, int Periodicidad5, int plazo5, decimal gravamen, double monto6, int Periodicidad6, int plazo6, double monto7,
            int Periodicidad7, int plazo7, int plazoAvance1, int plazoAvance2, int plazoAvance3, int plazoAvance4, int plazoAvance5,
            int plazoAvance6, int plazoAvance7, int lincredcuoman, double vlrcuoman, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = this.BuscaParametros(IdAgencia, Idbanco, myconnect);
            switch (ok)
            {
                case false:
                    stbuilder.Append("insert into cre_parame01(Codigo_banco,idagencia,Cupo,TipoAvance,VlrAvance,TasaAvance,Lincred,LincredAvance,Clades,Consecutivo,DiasVence,");
                    stbuilder.Append("DiaCorte,Monto1,Plazo1,Periode1,Monto2,Plazo2,Periode2,Monto3,Plazo3,Periode3,Monto4,Plazo4,Periode4,Monto5,Plazo5,Periode5,gravamen,Monto6, ");
                    stbuilder.Append("Plazo6,Periode6,Monto7,Plazo7,Periode7,PlazoAvance1,PlazoAvance2,PlazoAvance3,PlazoAvance4,PlazoAvance5,PlazoAvance6,PlazoAvance7,lincredcuoman,vlrcuoman) values('");
                    stbuilder.Append(Idbanco + "','");
                    stbuilder.Append(IdAgencia + "','");
                    stbuilder.Append(Cupo + "','");
                    stbuilder.Append(TipoAvance + "','");
                    stbuilder.Append(ValAvance + "','");
                    stbuilder.Append(TasaAvance + "','");
                    stbuilder.Append(LineaCredito + "','");
                    stbuilder.Append(LineaAvances + "','");
                    stbuilder.Append(clades + "','");
                    stbuilder.Append(Consecutivo + "','");
                    stbuilder.Append(DiasVence + "','");
                    stbuilder.Append(DiasCorte + "','");
                    stbuilder.Append(monto1 + "','");
                    stbuilder.Append(plazo1 + "','");
                    stbuilder.Append(Periodicidad1 + "','");
                    stbuilder.Append(monto2 + "','");
                    stbuilder.Append(plazo2 + "','");
                    stbuilder.Append(Periodicidad2 + "','");
                    stbuilder.Append(monto3 + "','");
                    stbuilder.Append(plazo3 + "','");
                    stbuilder.Append(Periodicidad3 + "','");
                    stbuilder.Append(monto4 + "','");
                    stbuilder.Append(plazo4 + "','");
                    stbuilder.Append(Periodicidad4 + "','");
                    stbuilder.Append(monto5 + "','");
                    stbuilder.Append(plazo5 + "','");
                    stbuilder.Append(Periodicidad5 + "','");
                    stbuilder.Append(gravamen + "','");
                    stbuilder.Append(monto6 + "','");
                    stbuilder.Append(plazo6 + "','");
                    stbuilder.Append(Periodicidad6 + "','");
                    stbuilder.Append(monto7 + "','");
                    stbuilder.Append(plazo7 + "','");
                    stbuilder.Append(Periodicidad7 + "','");
                    stbuilder.Append(plazoAvance1 + "','");
                    stbuilder.Append(plazoAvance2 + "','");
                    stbuilder.Append(plazoAvance3 + "','");
                    stbuilder.Append(plazoAvance4 + "','");
                    stbuilder.Append(plazoAvance5 + "','");
                    stbuilder.Append(plazoAvance6 + "','");
                    stbuilder.Append(plazoAvance7 + "','");
                    stbuilder.Append(lincredcuoman + "','");
                    stbuilder.Append(vlrcuoman + "')");
                    break;
                case true:
                    stbuilder.Append("update cre_parame01 set ");
                    stbuilder.Append("Cupo= '");
                    stbuilder.Append(Cupo + "',");
                    stbuilder.Append("TipoAvance= '");
                    stbuilder.Append(TipoAvance + "',");
                    stbuilder.Append("VlrAvance= '");
                    stbuilder.Append(ValAvance + "',");
                    stbuilder.Append("TasaAvance= '");
                    stbuilder.Append(TasaAvance + "',");
                    stbuilder.Append("Lincred= '");
                    stbuilder.Append(LineaCredito + "',");
                    stbuilder.Append("LincredAvance= '");
                    stbuilder.Append(LineaAvances + "',");
                    stbuilder.Append("Clades= '");
                    stbuilder.Append(clades + "',");
                    stbuilder.Append("Consecutivo= '");
                    stbuilder.Append(Consecutivo + "',");
                    stbuilder.Append("DiasVence= '");
                    stbuilder.Append(DiasVence + "',");
                    stbuilder.Append("DiaCorte= '");
                    stbuilder.Append(DiasCorte + "',");
                    stbuilder.Append("Monto1= '");
                    stbuilder.Append(monto1 + "',");
                    stbuilder.Append("Plazo1= '");
                    stbuilder.Append(plazo1 + "',");
                    stbuilder.Append("Periode1= '");
                    stbuilder.Append(Periodicidad1 + "',");
                    stbuilder.Append("Monto2= '");
                    stbuilder.Append(monto2 + "',");
                    stbuilder.Append("Plazo2= '");
                    stbuilder.Append(plazo2 + "',");
                    stbuilder.Append("Periode2= '");
                    stbuilder.Append(Periodicidad2 + "',");
                    stbuilder.Append("Monto3= '");
                    stbuilder.Append(monto3 + "',");
                    stbuilder.Append("Plazo3= '");
                    stbuilder.Append(plazo3 + "',");
                    stbuilder.Append("Periode3= '");
                    stbuilder.Append(Periodicidad3 + "',");
                    stbuilder.Append("Monto4= '");
                    stbuilder.Append(monto4 + "',");
                    stbuilder.Append("Plazo4= '");
                    stbuilder.Append(plazo4 + "',");
                    stbuilder.Append("Periode4= '");
                    stbuilder.Append(Periodicidad4 + "',");
                    stbuilder.Append("Monto5= '");
                    stbuilder.Append(monto5 + "',");
                    stbuilder.Append("Plazo5= '");
                    stbuilder.Append(plazo5 + "',");
                    stbuilder.Append("Periode5= '");
                    stbuilder.Append(Periodicidad5 + "',");
                    stbuilder.Append("Monto6= '");
                    stbuilder.Append(monto6 + "',");
                    stbuilder.Append("Plazo6= '");
                    stbuilder.Append(plazo6 + "',");
                    stbuilder.Append("Periode6= '");
                    stbuilder.Append(Periodicidad6 + "',");
                    stbuilder.Append("Monto7= '");
                    stbuilder.Append(monto7 + "',");
                    stbuilder.Append("Plazo7= '");
                    stbuilder.Append(plazo7 + "',");
                    stbuilder.Append("Periode7= '");
                    stbuilder.Append(Periodicidad7 + "',");
                    stbuilder.Append("gravamen='");
                    stbuilder.Append(gravamen + "', ");
                    stbuilder.Append("PlazoAvance1= '");
                    stbuilder.Append(plazoAvance1 + "',");
                    stbuilder.Append("PlazoAvance2= '");
                    stbuilder.Append(plazoAvance2 + "',");
                    stbuilder.Append("PlazoAvance3= '");
                    stbuilder.Append(plazoAvance3 + "',");
                    stbuilder.Append("PlazoAvance4= '");
                    stbuilder.Append(plazoAvance4 + "',");
                    stbuilder.Append("PlazoAvance5= '");
                    stbuilder.Append(plazoAvance5 + "',");
                    stbuilder.Append("PlazoAvance6= '");
                    stbuilder.Append(plazoAvance6 + "',");
                    stbuilder.Append("PlazoAvance7= '");
                    stbuilder.Append(plazoAvance7 + "', ");
                    stbuilder.Append("lincredcuoman='");
                    stbuilder.Append(lincredcuoman + "',");
                    stbuilder.Append("vlrcuoman ='");
                    stbuilder.Append(vlrcuoman + "' ");
                    stbuilder.Append("where IdAgencia = '" + IdAgencia + "' and Codigo_banco = '" + Idbanco + "'");
                    break;
            }

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaParametros");
        }

        public void GrabaTarjetaCredito(string IdNumtarjeta, string idbanco, string codigoter, double Cupo, double Disponible,
            double Avances, DateTime fechaAsignacion, string codeudor1, string codeudor2, int DiaCorte, int tipocta,
            int trancajero, int Tranpos, string CobraManejo, OdbcConnection myconnect, DateTime fechavence = default(DateTime),
            bool fecha_cupo = false)
        {
            if (fechavence == default(DateTime))
                fechavence = new DateTime(1950, 1, 1);

            StringBuilder stbuilder = new StringBuilder();

            if (codeudor1.Trim() != "")
            {
                codeudor1 = ("00000000000000" + codeudor1).Substring(("00000000000000" + codeudor1).Length - 14);
            }

            if (codeudor2.Trim() != "")
            {
                codeudor2 = ("00000000000000" + codeudor2).Substring(("00000000000000" + codeudor2).Length - 14);
            }

            stbuilder.Append(" update deb_maetarj set codigoter = '");
            stbuilder.Append(codigoter + "',");
            stbuilder.Append("lincred = 99,");
            stbuilder.Append("cuenta = '");
            stbuilder.Append(codigoter + "',");
            stbuilder.Append("tipocta = '");
            stbuilder.Append(tipocta + "',");
            stbuilder.Append("operacion = 'A:',");
            stbuilder.Append("estado = 'A',");
            stbuilder.Append("disponible = '");
            stbuilder.Append(Disponible + "',");
            stbuilder.Append("cupoCajero = '");
            stbuilder.Append(Avances + "',");
            stbuilder.Append("trancajero = '");
            stbuilder.Append(trancajero + "',");
            stbuilder.Append("CupoPos = '");
            stbuilder.Append(Disponible + "',");
            stbuilder.Append("Tranpos = '");
            stbuilder.Append(Tranpos + "',");
            stbuilder.Append("fecAsignacion = '");
            stbuilder.Append(fechaAsignacion.ToString(varini.PstForFec) + "',");
            stbuilder.Append("debcre = 'C',");
            stbuilder.Append("codeudor1 = '");
            stbuilder.Append(codeudor1 + "',");
            stbuilder.Append("Codeudor2 = '");
            stbuilder.Append(codeudor2 + "',");
            stbuilder.Append("diaCorte = '");
            stbuilder.Append(DiaCorte + "',");
            stbuilder.Append("CupoCredito = '");
            stbuilder.Append(Cupo + "',");
            stbuilder.Append("fechavence = '");
            stbuilder.Append(fechavence.ToString(varini.PstForFec) + "', ");
            stbuilder.Append("CobraManejo='");
            stbuilder.Append(CobraManejo + "' ");
            stbuilder.Append(fecha_cupo ? ", FecAsignaCupo= '" + DateTime.Now.ToString(varini.PstForFec) + "' " : "");
            stbuilder.Append(" where banco = '" + idbanco + "' and Tarjeta = '" + IdNumtarjeta + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaTarjetaCredito");
        }

        public void ConsolidadCreditos(DateTime Fecha, string Lincred, string LineaNueva, string Cpte, double Consecutivo, string Usuario, OdbcConnection myconnect, Form myforma)
        {
            StringBuilder stbuilder = new StringBuilder();
            DateTime fecAprob;
            double Debito;
            double Credito = 0;
            string ConseCredito = Fecha.ToString("yyyyMMdd");
            DataSet Dsdatospar = new DataSet();
            string CodCap = "9999";
            int Cuota = 0;
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Consolidando lineas de tarjeta Credito", myforma);
            int canreg = 0;
            int fila = 0;
            ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcopLocal = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
            ERP.Core.Compartido.Configuracion.ParamSys msgsys = new ERP.Core.Compartido.Configuracion.ParamSys();

            msgsys.BuscarCompania(varini.sptCodEmpr, Dsdatospar, myconnect);

            CodCap = Convert.ToString(Dsdatospar.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"]);

            stbuilder.Append("select copmae.codigoter, copmae.lincred, copmae.numero, copmae.fecsolic,copmae.fecaprob,copmae.fecfact, copmae.fecdesc,copmae.nit,copmae.plazo,copmae.vlrsolicitud,copmae.valorob,salmae.cuota,salmae.tasaint,salmae.ciclod,");
            stbuilder.Append("salmae.periodd, copmae.Clacuo, copmae.Clasei, copmae.fecha_graba, copmae.agencia, copmae.ccosto, salmae.clades, Salmae.saldo, codahor ");
            stbuilder.Append("from cop_maecar copmae inner join cop_salmaecar salmae on copmae.codigoter = salmae.codigoter And copmae.lincred = salmae.lincred And copmae.numero = salmae.numero And salmae.periodo = '" + Fecha.ToString("yyyyMM") + "' ");
            stbuilder.Append("inner join cop_concar12 para12 on copmae.lincred = para12.lincred ");
            stbuilder.Append("where salmae.saldo <> 0 and copmae.lincred = '" + Lincred + "'");
            stbuilder.Append("order by copmae.codigoter,copmae.lincred, copmae.numero");

            msgbarra.DefineMaximo(stbuilder.ToString(), myconnect);
            msgbarra.Show();

            DataSet myread = new DataSet();
            this.msgodbc.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "ConsolidadCreditos", myread, "TblConsolidaCred");
            canreg = myread.Tables["TblConsolidaCred"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myread.Tables["TblConsolidaCred"].Rows[fila];

                // ok = msgcopLocal.BuscaObligacion(Convert.ToString(row["codigoter"]), LineaNueva, Convert.ToDouble(ConseCredito), myconnect, 0, default(DateTime), default(DateTime), default(DateTime), Cuota, 0, 0, 0, 0, 0, 0, 0, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", Fecha.ToString("yyyyMM")); // ERROR: CS1503, CS1620
                switch (ok)
                {
                    case false:
                        if (row["fecaprob"] == DBNull.Value)
                        {
                            fecAprob = Convert.ToDateTime(row["fecsolic"]);
                        }
                        else
                        {
                            fecAprob = Convert.ToDateTime(row["fecaprob"]);
                        }

                        msgcopLocal.GrabaNuevoCredito(Convert.ToString(row["codigoter"]), LineaNueva, Convert.ToDouble(ConseCredito), Convert.ToDateTime(row["fecsolic"]), fecAprob, Convert.ToDateTime(row["fecfact"]), Convert.ToDateTime(row["fecdesc"]), Convert.ToString(row["nit"]), Convert.ToInt32(row["plazo"]), Convert.ToDouble(row["vlrsolicitud"]), Convert.ToDouble(row["valorob"]), 0, Convert.ToDouble(row["cuota"]), Convert.ToDouble(row["tasaint"]),
                            Convert.ToInt32(row["ciclod"]), Convert.ToInt32(row["periodd"]), Convert.ToInt32(row["Clacuo"]), Convert.ToInt32(row["Clasei"]), Usuario, Convert.ToDateTime(row["fecha_graba"]), Convert.ToString(row["agencia"]), Convert.ToString(row["ccosto"]), Fecha.ToString("yyyyMM"), myconnect);
                        break;
                    case true:
                        Cuota += Convert.ToInt32(row["cuota"]);
                        if (row["fecaprob"] == DBNull.Value)
                        {
                            fecAprob = Convert.ToDateTime(row["fecsolic"]);
                        }
                        else
                        {
                            fecAprob = Convert.ToDateTime(row["fecaprob"]);
                        }
                        msgcopLocal.GrabaNuevoCredito(Convert.ToString(row["codigoter"]), LineaNueva, Convert.ToDouble(ConseCredito), Convert.ToDateTime(row["fecsolic"]), fecAprob, Convert.ToDateTime(row["fecfact"]), Convert.ToDateTime(row["fecdesc"]), Convert.ToString(row["nit"]), Convert.ToInt32(row["plazo"]), Convert.ToDouble(row["vlrsolicitud"]), Convert.ToDouble(row["valorob"]), 0, Cuota, Convert.ToDouble(row["tasaint"]),
                            Convert.ToInt32(row["ciclod"]), Convert.ToInt32(row["periodd"]), Convert.ToInt32(row["Clacuo"]), Convert.ToInt32(row["Clasei"]), Usuario, Convert.ToDateTime(row["fecha_graba"]), Convert.ToString(row["agencia"]), Convert.ToString(row["ccosto"]), Fecha.ToString("yyyyMM"), myconnect);
                        break;
                }

                if (Convert.ToDouble(row["saldo"]) < 0)
                {
                    Debito = Convert.ToDouble(row["saldo"]) * -1;
                    Credito = 0;
                }
                else
                {
                    Credito = Convert.ToDouble(row["saldo"]);
                    Debito = 0;
                }

                if (Convert.ToString(row["codahor"]) == "5")
                {
                    // msgcopLocal.GrabaMovimiento(Cpte, Consecutivo, Convert.ToString(row["codigoter"]), Convert.ToString(row["lincred"]), Convert.ToDouble(row["numero"]), Fecha.ToString("yyyyMM"), CodCap, Fecha, Debito, Credito, "Reclasificacion automatica", Usuario, myconnect, "", "", "", "", Convert.ToString(row["codigoter"]), "", "", false); // ERROR: CS1503, CS1620
                    // msgcopLocal.GrabaMovimiento(Cpte, Consecutivo, Convert.ToString(row["codigoter"]), LineaNueva, Convert.ToDouble(ConseCredito), Fecha.ToString("yyyyMM"), CodCap, Fecha, Credito, Debito, "Reclasificacion automatica", Usuario, myconnect, "", "", "", "", Convert.ToString(row["codigoter"]), "", "", false); // ERROR: CS1503, CS1620
                }

                fila += 1;
                msgbarra.PerformStep();
            }
            myread.Dispose();
            msgbarra.Close();
            msgbarra.Dispose();
        }

        public long CupoDisponible(string idagencia, string Banco, string codigoter, int periodo, long Cupo, OdbcConnection myconnect, ref double DispAvance)
        {
            long saldocre = 0;
            long SaldoAvance = 0;
            string mysql;
            long result = 0;

            DataSet dsdatos = new DataSet();
            ok = this.BuscaParametros(idagencia, Banco, dsdatos, myconnect);
            switch (ok)
            {
                case false:
                    MessageBox.Show("Agencia no esta parametrizada, " + idagencia, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
            }

            DataRow parcreRow = dsdatos.Tables["tblparcre"].Rows[0];
            mysql = "select sum(saldo) as Campo1  from cop_saldos_vw  where codigoter = '" + codigoter + "' and lincred in (" + parcreRow["Lincred"] + "," + parcreRow["LincredAvance"] + ") and periodo = " + periodo;
            // this.msgodbc.ExecuteQueryconec(mysql, myconnect, "CupoDisponible", ref saldocre); // ERROR: CS1503

            switch (Convert.ToInt32(parcreRow["TipoAvance"]))
            {
                case 0:
                    DispAvance = (Cupo * Convert.ToDouble(parcreRow["TasaAvance"])) / 100;
                    break;
                case 1:
                    DispAvance = Convert.ToDouble(parcreRow["VlrAvance"]);
                    break;
            }

            result = Cupo - (saldocre + SaldoAvance);
            if (result < 0)
            {
                result = 0;
                SaldoAvance = 0;
            }
            return result;
        }

        public double BuscaDiasMora(string codigoter, string Periodo, string lincred, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            int diasmora = 0;

            stbuilder.Append("select max(copmora.diasmora) as campo1 from cop_copmora copmora ");
            stbuilder.Append("inner join cop_salmaecar sal on copmora.codigoter=sal.codigoter and copmora.lincred=sal.lincred and copmora.numero=sal.numero and copmora.periodo_contable=sal.periodo ");
            stbuilder.Append("where copmora.codigoter= '" + codigoter + "' and sal.saldo>0 ");
            stbuilder.Append("and copmora.periodo_contable = '" + Periodo + "' and copmora.lincred = '" + lincred + "' and (saldoCapital + saldointeres + saldomora + saldoseguro + saldoadmon) > 0 ");

            // ok = this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BloqueosAutomaticos", ref diasmora); // ERROR: CS1503
            return diasmora;
        }

        public string HelpTarjetas(Form myforma, OdbcConnection myconnect)
        {
            string Numtarjeta;
            Numtarjeta = msgsas.CargaAyuda("deb_maetarj", "Banco", "Tarjeta", "", myconnect, myforma, "Tarjetas");
            return Numtarjeta;
        }

        public bool BuscarCuentaTarjeta(string codigoter, string IdNumtarjeta, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("select cuenta from deb_maetarj ");
            stbuilder.Append("where Tarjeta <> '" + IdNumtarjeta + "' and (cuenta ='" + codigoter + "' or CuentaDs='" + codigoter + "') ");

            ok = this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscarCuentaTarjeta");
            return ok;
        }

        public void GrabaTarjetaCreditoDobleServicio(string IdNumtarjeta, string idbanco, string codigoter, double Cupo, double Disponible,
            double Avances, DateTime fechaAsignacion, string codeudor1, string codeudor2, int DiaCorte, int tipocta,
            int trancajero, int Tranpos, string CobraManejo, OdbcConnection myconnect, DateTime fechavence = default(DateTime),
            string estado = "L", bool Fecha_cupo = false)
        {
            if (fechavence == default(DateTime))
                fechavence = new DateTime(1950, 1, 1);

            StringBuilder stbuilder = new StringBuilder();
            string NumCuentaTarj = "";

            if (codeudor1.Trim() != "")
            {
                codeudor1 = ("00000000000000" + codeudor1).Substring(("00000000000000" + codeudor1).Length - 14);
            }

            if (codeudor2.Trim() != "")
            {
                codeudor2 = ("00000000000000" + codeudor2).Substring(("00000000000000" + codeudor2).Length - 14);
            }

            ok = this.BuscarCuentaTarjeta(codigoter, IdNumtarjeta, myconnect);

            switch (ok)
            {
                case false:
                    NumCuentaTarj = codigoter;
                    break;
                case true:
                    NumCuentaTarj = IdNumtarjeta.Trim().Length >= 16 ? IdNumtarjeta.Trim().Substring(2, 14) : IdNumtarjeta.Trim();
                    break;
            }

            stbuilder.Append("update deb_maetarj set codigoter = '");
            stbuilder.Append(codigoter + "',");
            stbuilder.Append("tipocta = '");
            stbuilder.Append(tipocta + "',");
            stbuilder.Append("operacion = 'A:',");
            stbuilder.Append("estado = 'A',");
            stbuilder.Append("codeudor1 = '");
            stbuilder.Append(codeudor1 + "',");
            stbuilder.Append("Codeudor2 = '");
            stbuilder.Append(codeudor2 + "',");
            stbuilder.Append("diaCorte = '");
            stbuilder.Append(DiaCorte + "',");
            stbuilder.Append("CupoCredito = '");
            stbuilder.Append(Cupo + "',");
            stbuilder.Append("fechavence = '");
            stbuilder.Append(fechavence.ToString(varini.PstForFec) + "',");

            switch (estado)
            {
                case "L":
                    stbuilder.Append("lincred = 99,");
                    stbuilder.Append("fecAsignacion = '");
                    stbuilder.Append(fechaAsignacion.ToString(varini.PstForFec) + "',");
                    stbuilder.Append("debcre = 'C',");
                    stbuilder.Append("cuenta = '");
                    stbuilder.Append(NumCuentaTarj + "',");
                    stbuilder.Append("disponible = '");
                    stbuilder.Append(Disponible + "',");
                    stbuilder.Append("cupoCajero = '");
                    stbuilder.Append(Avances + "',");
                    stbuilder.Append("trancajero = '");
                    stbuilder.Append(trancajero + "',");
                    stbuilder.Append("CupoPos = '");
                    stbuilder.Append(Disponible + "',");
                    stbuilder.Append("Tranpos = '");
                    stbuilder.Append(Tranpos + "',");
                    stbuilder.Append("DispCredDs = '");
                    stbuilder.Append(Disponible + "',");
                    stbuilder.Append("CupoCajDs = '");
                    stbuilder.Append(Avances + "',");
                    stbuilder.Append("TransCajDs = '");
                    stbuilder.Append(trancajero + "',");
                    stbuilder.Append("CupoPosDs = '");
                    stbuilder.Append(Disponible + "',");
                    stbuilder.Append("TransPosDs = '");
                    stbuilder.Append(Tranpos + "',");
                    stbuilder.Append("CuentaDs = '");
                    stbuilder.Append(NumCuentaTarj + "',");
                    stbuilder.Append("CobraManejo = '");
                    stbuilder.Append(CobraManejo + "',");
                    stbuilder.Append("CobraManejoDS = '");
                    stbuilder.Append(CobraManejo + "' ");
                    break;
                default:
                    stbuilder.Append("debcre = 'M',");
                    stbuilder.Append("DispCredDs = '");
                    stbuilder.Append(Disponible + "',");
                    stbuilder.Append("CupoCajDs = '");
                    stbuilder.Append(Avances + "',");
                    stbuilder.Append("TransCajDs = '");
                    stbuilder.Append(trancajero + "',");
                    stbuilder.Append("CupoPosDs = '");
                    stbuilder.Append(Disponible + "',");
                    stbuilder.Append("TransPosDs = '");
                    stbuilder.Append(Tranpos + "',");
                    stbuilder.Append("CuentaDs = '");
                    stbuilder.Append(NumCuentaTarj + "',");
                    stbuilder.Append("CobraManejoDS = '");
                    stbuilder.Append(CobraManejo + "' ");
                    break;
            }

            stbuilder.Append(Fecha_cupo ? ", FecAsignaCupo= '" + DateTime.Now.ToString(varini.PstForFec) + "' " : "");
            stbuilder.Append(" where banco = '" + idbanco + "' and Tarjeta = '" + IdNumtarjeta + "'");

            this.msgodbc.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaTarjetaCredito");
        }

        private DataSet BuscarObligacionReverso(string IdTransaccion, OdbcConnection Myconnect)
        {
            bool okLocal = false;
            string StMysql;
            DataSet DsData = new DataSet();

            StMysql = "select lincred,numero from cop_maecar where IdTransaccion ='" + IdTransaccion + "'";
            this.msgodbc.ExecuteQueryDataset(StMysql, Myconnect, "BuscarObligacionReverso", DsData, "tblreverso");

            return DsData;
        }

        public bool CargarCuotaManejo(string IdAgencia, string IdBanco, DateTime FechaMovto, string Cpte, double NumCpte, string Detalle, string Cobro4x100,
            DateTime FecDsctoSemanal, int ClaseCuota, int Plazo, int FormaFecDescto, string usuario, Form Myforma, OdbcConnection Myconnect)
        {
            StringBuilder Stbuilder = new StringBuilder();
            DataSet DsDataSet = new DataSet();
            double fila = 0;
            DataSet dspartarcre = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress Msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Cuota Manejo T. Credito", Myforma);
            int IdLinCred = 0;
            double VlrCuotaManejo = 0;
            double NumCredito = 0;
            string Clades = "1";
            string clacuo = "1";
            string clasei = "1";
            DateTime fecdesc;
            double Cuota = 0;
            double tasaint = 0;
            DateTime fechamovimiento;
            string transaccion = "02";

            ok = this.BuscaParametros(IdAgencia, IdBanco, dspartarcre, Myconnect);
            switch (ok)
            {
                case false:
                    MessageBox.Show("Parametros de tarjeta credito no estan creados, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            IdLinCred = Convert.ToInt32(dspartarcre.Tables["tblparcre"].Rows[0]["lincredcuoman"]);
            VlrCuotaManejo = Convert.ToDouble(dspartarcre.Tables["tblparcre"].Rows[0]["vlrcuoman"]);
            Clades = Convert.ToString(dspartarcre.Tables["tblparcre"].Rows[0]["Clades"]);

            // this.msgparcop.BuscaLinea(IdLinCred, dspartarcre, Myconnect); // ERROR: CS1503, CS1620
            if (dspartarcre.Tables["tbllineas"].Rows.Count <= 0)
            {
                MessageBox.Show("Parametros de la Linea para la cuota de manejo no estan creados, por favor revise", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            clacuo = Convert.ToString(dspartarcre.Tables["tbllineas"].Rows[0]["clacuo"]);
            clasei = Convert.ToString(dspartarcre.Tables["tbllineas"].Rows[0]["clasei"]);
            tasaint = Convert.ToDouble(dspartarcre.Tables["tbllineas"].Rows[0]["tasai"]);

            this.msgparsys.BuscarCompania(varini.sptCodEmpr, dspartarcre, Myconnect);
            transaccion = Convert.ToString(dspartarcre.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"]);

            Stbuilder.Append("Select debtarj.estado,debtarj.codigoter,debtarj.tarjeta,debtarj.cuenta,debtarj.lincred,debtarj.debcre,");
            Stbuilder.Append("maenit.agencia,maenit.nit,maenit.cencosto,maenit.empresa,maenit.periodo_desto ");
            Stbuilder.Append("from deb_maetarj debtarj inner join sys_maenit maenit on debtarj.codigoter=maenit.codigoter ");
            Stbuilder.Append("where debtarj.estado = 'A' and (((debtarj.error <> '62' and debtarj.error <> '99') or debtarj.error is null) or (debtarj.error in ('62','99') and debtarj.fecnovedad>='" + FechaMovto.ToString(varini.PstForFec) + "')) ");
            Stbuilder.Append(" and debtarj.banco = '" + IdBanco + "' and maenit.agencia='" + IdAgencia + "' and CobraManejo='Y' and debtarj.debcre='C' ");

            ok = this.msgodbc.ExecuteQueryDataset(Stbuilder.ToString(), Myconnect, "ArchivoTarjetas", DsDataSet, "tbltarj");

            Msgbarra.ValorMinimoMaximo(0, DsDataSet.Tables["tbltarj"].Rows.Count);
            Msgbarra.Show();

            switch (ok)
            {
                case true:
                    // ok = this.msgcop.BuscaComprobante(Cpte, NumCpte, true, Myconnect); // ERROR: CS1620

                    for (fila = 0; fila < DsDataSet.Tables["tbltarj"].Rows.Count; fila++)
                    {
                        NumCredito = Convert.ToDouble(IdAgencia + Convert.ToInt32(IdBanco).ToString() + FechaMovto.ToString("yyyyMMdd") + (fila + 1).ToString());
                        Cuota = VlrCuotaManejo;
                        fechamovimiento = FechaMovto;

                        DataRow row = DsDataSet.Tables["tbltarj"].Rows[Convert.ToInt32(fila)];
                        // ok = this.msgcop.BuscaObligacion(Convert.ToString(row["codigoter"]), IdLinCred.ToString(), NumCredito, Myconnect); // ERROR: CS1503
                        switch (ok)
                        {
                            case false:
                                string periodo_desto = Convert.ToString(row["periodo_desto"]);
                                if (!int.TryParse(periodo_desto, out int perioddResult) || perioddResult <= 0)
                                {
                                    row["periodo_desto"] = "1";
                                    periodo_desto = "1";
                                }

                                switch (periodo_desto)
                                {
                                    case "1":
                                        switch (FormaFecDescto)
                                        {
                                            case 1:
                                                fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, fechamovimiento.Month == 2 ? 28 : 30);
                                                break;
                                            case 2:
                                                fechamovimiento = FechaMovto.AddMonths(1);
                                                fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, fechamovimiento.Month == 2 ? 28 : 30);
                                                break;
                                            default:
                                                fecdesc = FechaMovto;
                                                break;
                                        }
                                        break;
                                    case "2":
                                        switch (FormaFecDescto)
                                        {
                                            case 1:
                                                if (FechaMovto.Day <= 15)
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, 15);
                                                }
                                                else
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, fechamovimiento.Month == 2 ? 28 : 30);
                                                }
                                                break;
                                            case 2:
                                                if (FechaMovto.Day <= 15)
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, fechamovimiento.Month == 2 ? 28 : 30);
                                                }
                                                else
                                                {
                                                    fechamovimiento = FechaMovto.AddMonths(1);
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, 15);
                                                }
                                                break;
                                            default:
                                                fecdesc = FechaMovto;
                                                break;
                                        }
                                        break;
                                    case "3":
                                        switch (FormaFecDescto)
                                        {
                                            case 1:
                                                if (FechaMovto.Day <= 10)
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, 10);
                                                }
                                                else if (FechaMovto.Day <= 20)
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, 20);
                                                }
                                                else
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, fechamovimiento.Month == 2 ? 28 : 30);
                                                }
                                                break;
                                            case 2:
                                                if (FechaMovto.Day <= 10)
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, 20);
                                                }
                                                else if (FechaMovto.Day <= 20)
                                                {
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, fechamovimiento.Month == 2 ? 28 : 30);
                                                }
                                                else
                                                {
                                                    fechamovimiento = FechaMovto.AddMonths(1);
                                                    fecdesc = new DateTime(fechamovimiento.Year, fechamovimiento.Month, 10);
                                                }
                                                break;
                                            default:
                                                fecdesc = FechaMovto;
                                                break;
                                        }
                                        break;
                                    case "4":
                                        fecdesc = FecDsctoSemanal;
                                        break;
                                    default:
                                        fecdesc = FechaMovto;
                                        break;
                                }

                                if (ClaseCuota == 2)
                                {
                                    Cuota = Math.Round((VlrCuotaManejo / (Plazo * Convert.ToDouble(row["periodo_desto"]))), 0);
                                }

                                // ok = this.msgcop.GrabaNuevoCredito(Convert.ToString(row["codigoter"]), IdLinCred.ToString(), NumCredito, FechaMovto, FechaMovto, FechaMovto, fecdesc, Convert.ToString(row["nit"]), Plazo, VlrCuotaManejo, VlrCuotaManejo, VlrCuotaManejo, VlrCuotaManejo, tasaint, 5, Convert.ToInt32(row["periodo_desto"]), Convert.ToInt32(clacuo), Convert.ToInt32(clasei), usuario, FechaMovto, IdAgencia, Convert.ToString(row["cencosto"]), FechaMovto.ToString("yyyyMM"), Myconnect, "1", Convert.ToInt32(Clades), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", Convert.ToString(row["empresa"]), "", "", "", "", "", Convert.ToString(row["tarjeta"]), Detalle); // ERROR: CS1503
                                break;
                        }

                        // this.msgcop.GrabaMovimiento(Cpte, NumCpte, Convert.ToString(row["codigoter"]), IdLinCred.ToString(), NumCredito, FechaMovto.ToString("yyyyMM"), transaccion, FechaMovto, VlrCuotaManejo, 0, Detalle, usuario, Myconnect); // ERROR: CS1503, CS1620
                    }
                    break;
            }

            Msgbarra.Close();
            Msgbarra.Dispose();

            return true;
        }

        public long CupoDisponibleSinDatos(string tipoavance, decimal tasaavance, double valoravance, double Cupo, double SaldoTarjetaUtilizado, ref double DispAvance)
        {
            long saldocre = 0;
            long SaldoAvance = 0;
            long CupoDisponible = 0;

            switch (tipoavance)
            {
                case "0":
                    DispAvance = (Cupo * Convert.ToDouble(tasaavance)) / 100;
                    break;
                case "1":
                    DispAvance = valoravance;
                    break;
            }

            CupoDisponible = Convert.ToInt64(Cupo - SaldoTarjetaUtilizado);
            if (CupoDisponible < 0)
            {
                CupoDisponible = 0;
                SaldoAvance = 0;
            }
            return CupoDisponible;
        }

        public bool ExecuteQueryDataset(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, ref DataSet DsDataset, string NombreTabla)
        {
            OdbcDataAdapter Myread = new OdbcDataAdapter();
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);
            mycomqueryconec.ExecuteNonQuery();

            Myread.SelectCommand = mycomqueryconec;
            Myread.Fill(DsDataset, NombreTabla);

            if (DsDataset.Tables[NombreTabla].Rows.Count > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool ExecuteQueryconec(string stMysql, OdbcConnection appadoConect, string NombreProcedimiento, string Campo1 = "", string Campo2 = "", string Campo3 = "", string Campo4 = "")
        {
            bool result = false;
            mycomqueryconec.CommandText = stMysql;
            mycomqueryconec.Connection = appadoConect;
            mycomqueryconec.CommandText = Strings.Replace(mycomqueryconec.CommandText, "''", "' '", 1, -1, CompareMethod.Text);

            try
            {
                OdbcDataReader Myreader = mycomqueryconec.ExecuteReader();
                if (Myreader.RecordsAffected > 0)
                {
                    result = true;
                }
                while (Myreader.Read())
                {
                    if (Campo1 != "")
                    {
                        if (Myreader["campo1"] == DBNull.Value)
                        {
                            Campo1 = "0";
                        }
                        else
                        {
                            Campo1 = Myreader["campo1"].ToString().Trim();
                        }
                    }
                    if (Campo2 != "")
                    {
                        if (Myreader["campo2"] == DBNull.Value)
                        {
                            Campo2 = "0";
                        }
                        else
                        {
                            Campo2 = Myreader["campo2"].ToString().Trim();
                        }
                    }
                    if (Campo3 != "")
                    {
                        if (Myreader["campo3"] == DBNull.Value)
                        {
                            Campo3 = "0";
                        }
                        else
                        {
                            Campo3 = Myreader["campo3"].ToString().Trim();
                        }
                    }
                    if (Campo4 != "")
                    {
                        if (Myreader["campo4"] == DBNull.Value)
                        {
                            Campo4 = "0";
                        }
                        else
                        {
                            Campo4 = Myreader["campo4"].ToString().Trim();
                        }
                    }
                    result = true;
                }
                Myreader.Close();
            }
            catch (Exception ex)
            {
                result = false;
                MessageBox.Show(ex.Message + "\n Procedimiento Origen : " + NombreProcedimiento + "\nquery :" + stMysql, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return result;
        }
    }
}
