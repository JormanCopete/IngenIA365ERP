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

        public bool GrabarEstudioCredito(int NumSolicitud, string Codigoter, int Lincred, string RecSolicitud,
            string RecPagare, string RecLibraza, string RecOtros, string SoliCompleta, string ActBdatos,
            string CumpEstObligac, double CapacidadPago, string CertifIngresos, double ScoreCifin, double NivelEndeudam,
            string CalidadActivos, double NivelContingencia, string Estado, string Evaluacion, string VerifiRef,
            string usuario, DateTime FecEstudio, string ClaEne1, string ClaEne2,
            string ClaEne3, string ClaEne4, string ClaEne5, string ClaFeb1, string ClaFeb2,
            string ClaFeb3, string ClaFeb4, string ClaFeb5, string ClaMar1, string ClaMar2,
            string ClaMar3, string ClaMar4, string ClaMar5, string ClaAbr1, string ClaAbr2,
            string ClaAbr3, string ClaAbr4, string ClaAbr5, string ClaMay1, string ClaMay2,
            string ClaMay3, string ClaMay4, string ClaMay5, string ClaJun1, string ClaJun2,
            string ClaJun3, string ClaJun4, string ClaJun5, string ClaJul1, string ClaJul2,
            string ClaJul3, string ClaJul4, string ClaJul5, string ClaAgo1, string ClaAgo2,
            string ClaAgo3, string ClaAgo4, string ClaAgo5, string ClaSep1, string ClaSep2,
            string ClaSep3, string ClaSep4, string ClaSep5, string ClaOct1, string ClaOct2,
            string ClaOct3, string ClaOct4, string ClaOct5, string ClaNov1, string ClaNov2,
            string ClaNov3, string ClaNov4, string ClaNov5, string ClaDic1, string ClaDic2,
            string ClaDic3, string ClaDic4, string ClaDic5, double pagonomina, double pagocaja,
            double ActVivienda, double ActVehiculo, double ActAportes, double ActOtros,
            double TotalAct, double PasDeudas, double PasOtros, double TotalPas,
            double Patrimonio, double TotalPyP, double gastospersonales, double recogedeudas,
            double numcuenta, double SalPromedio, double ActCajaBanco, double ActCxC,
            double PasObliBanca, double PasObliHipo, string porcentaje, double ActAhorros,
            double scoredatacredito, double cupo, double descubierto, double saldodeudaexterna,
            double cuotadeudaexterna, string califidatacredito, double SalMorDatacredito, OdbcConnection myconnect, double extraSeguro,
            double CapitalRiesgo, double PorcentajePagaduria, string tipodstoPagaduria)
        {
            string Nomusu = " ", mysql = "", estadoAct = "E";
            Codigoter = Strings.Right("00000000000000" + Codigoter, 14);
            // this.msgcofsys.BuscaUsuario(usuario, myconnect, "", Nomusu); // ERROR: CS1501
            // ok = this.BuscarEstudioCredito(NumSolicitud, Codigoter, myconnect); // ERROR: CS1061
            string extra;
            extra = Strings.Replace(extraSeguro.ToString(), ",", ".");

            switch (ok)
            {
                case false:
                    stmysql = "Insert into cop_estsol (NroSolicitud, Codigoter, Lincred, RecSolicitud, RecPagare, RecLibraza, RecOtros, " +
                                "SoliCompleta, ActBdatos, CumpEstObligac, CapacidadPago, CertifIngresos, ScoreCifin, " +
                                "NivelEndeudam, CalidadActivos, NivelContingencia, Estado, Evaluacion, VerifiRef, " +
                                "usuario, Nomusu, Fechasys, ClaEne1, ClaEne2, ClaEne3, ClaEne4, ClaEne5, ClaFeb1, " +
                                "ClaFeb2, ClaFeb3, ClaFeb4, ClaFeb5, ClaMar1, ClaMar2, ClaMar3, ClaMar4, ClaMar5, " +
                                "ClaAbr1, ClaAbr2, ClaAbr3, ClaAbr4, ClaAbr5, ClaMay1, ClaMay2, ClaMay3, ClaMay4, " +
                                "ClaMay5, ClaJun1, ClaJun2, ClaJun3, ClaJun4, ClaJun5, ClaJul1, ClaJul2, ClaJul3, " +
                                "ClaJul4, ClaJul5, ClaAgo1, ClaAgo2, ClaAgo3, ClaAgo4, ClaAgo5, ClaSep1, ClaSep2, " +
                                "ClaSep3, ClaSep4, ClaSep5, ClaOct1, ClaOct2, ClaOct3, ClaOct4, ClaOct5, ClaNov1, " +
                                "ClaNov2, ClaNov3, ClaNov4, ClaNov5, ClaDic1, ClaDic2, ClaDic3, ClaDic4, ClaDic5,FecEstudio, " +
                                "pagonomina, pagocaja, ActVivienda, ActVehiculo, ActAportes, ActOtros, TotalAct, PasDeudas, PasOtros, " +
                                "TotalPas, Patrimonio, TotalPyP,GastosPers,recogedeudas,numcuenta,SalPromedio,ActCajaBanco,ActCxC," +
                                "PasObliBanca,PasObliHipoteca,porcentaje,ActAhorros,scoredatacredito,cupo,descubierto,saldodeudaexterna,cuotadeudaexterna, " +
                                "califidatacredito,SalMorDatacredito, extraSeguro,CapitalRiesgo,PorcentajePagaduria,tipodstoPagaduria) values (" + NumSolicitud + ",'" + Codigoter + "'," +
                              Lincred + ",'" + RecSolicitud + "','" + RecPagare + "','" + RecLibraza + "','" + RecOtros + "','" + SoliCompleta + "','" +
                              ActBdatos + "','" + CumpEstObligac + "'," + CapacidadPago + ",'" + CertifIngresos + "'," + ScoreCifin + "," + NivelEndeudam +
                              ",'" + CalidadActivos + "'," + NivelContingencia + ",'" + Estado + "','" + Evaluacion + "','" + VerifiRef + "','" + usuario + "','" +
                              Nomusu + "','" + Strings.Format(DateTime.Now, varini.pstForfecyHora) + "','" + ClaEne1 + "','" + ClaEne2 + "','" +
                              ClaEne3 + "','" + ClaEne4 + "','" + ClaEne5 + "','" + ClaFeb1 + "','" + ClaFeb2 + "','" + ClaFeb3 + "','" + ClaFeb4 + "','" +
                              ClaFeb5 + "','" + ClaMar1 + "','" + ClaMar2 + "','" + ClaMar3 + "','" + ClaMar4 + "','" + ClaMar5 + "','" + ClaAbr1 + "','" + ClaAbr2 + "','" +
                              ClaAbr3 + "','" + ClaAbr4 + "','" + ClaAbr5 + "','" + ClaMay1 + "','" + ClaMay2 + "','" + ClaMay3 + "','" + ClaMay4 + "','" + ClaMay5 + "','" +
                              ClaJun1 + "','" + ClaJun2 + "','" + ClaJun3 + "','" + ClaJun4 + "','" + ClaJun5 + "','" + ClaJul1 + "','" + ClaJul2 + "','" + ClaJul3 + "','" +
                              ClaJul4 + "','" + ClaJul5 + "','" + ClaAgo1 + "','" + ClaAgo2 + "','" + ClaAgo3 + "','" + ClaAgo4 + "','" + ClaAgo5 + "','" + ClaSep1 + "','" +
                              ClaSep2 + "','" + ClaSep3 + "','" + ClaSep4 + "','" + ClaSep5 + "','" + ClaOct1 + "','" + ClaOct2 + "','" + ClaOct3 + "','" + ClaOct4 + "','" +
                              ClaOct5 + "','" + ClaNov1 + "','" + ClaNov2 + "','" + ClaNov3 + "','" + ClaNov4 + "','" + ClaNov5 + "','" + ClaDic1 + "','" + ClaDic2 + "','" +
                              ClaDic3 + "','" + ClaDic4 + "','" + ClaDic5 + "','" + Strings.Format(FecEstudio, varini.PstForFec) + "'," + pagonomina + "," + pagocaja + "," + ActVivienda + "," +
                              ActVehiculo + "," + ActAportes + "," + ActOtros + "," + TotalAct + "," + PasDeudas + "," + PasOtros + "," + TotalPas + "," + Patrimonio + "," + TotalPyP + "," +
                              gastospersonales + "," + recogedeudas + "," + numcuenta + "," + SalPromedio + "," + ActCajaBanco + "," + ActCxC + "," + PasObliBanca + "," + PasObliHipo +
                              ",'" + porcentaje + "'," + ActAhorros + "," + scoredatacredito + "," + cupo + "," + descubierto + "," + saldodeudaexterna + "," + cuotadeudaexterna + ",'" + califidatacredito + "'," +
                              SalMorDatacredito + "," + extra + "," + CapitalRiesgo + "," + PorcentajePagaduria + ",'" + tipodstoPagaduria + "')";
                    break;

                case true:
                    stmysql = "Update cop_estsol set RecSolicitud='" + RecSolicitud + "', RecPagare='" + RecPagare + "', RecLibraza='" + RecLibraza + "', RecOtros='" + RecOtros +
                               "',SoliCompleta='" + SoliCompleta + "', ActBdatos='" + ActBdatos + "', CumpEstObligac='" + CumpEstObligac + "', CapacidadPago=" + CapacidadPago + ", CertifIngresos='" + CertifIngresos + "', ScoreCifin=" + ScoreCifin +
                               ",NivelEndeudam=" + NivelEndeudam + ", CalidadActivos='" + CalidadActivos + "', NivelContingencia=" + NivelContingencia + ", Estado='" + Estado + "', Evaluacion='" + Evaluacion + "', VerifiRef='" + VerifiRef + "'," +
                               "usuario='" + usuario + "', Nomusu='" + Nomusu + "', Fechasys='" + Strings.Format(DateTime.Now, varini.pstForfecyHora) + "', ClaEne1='" + ClaEne1 + "', ClaEne2='" + ClaEne2 + "', ClaEne3='" + ClaEne3 + "'," +
                               "ClaEne4='" + ClaEne4 + "', ClaEne5='" + ClaEne5 + "', ClaFeb1='" + ClaFeb1 + "',ClaFeb2='" + ClaFeb2 + "', ClaFeb3='" + ClaFeb3 + "', ClaFeb4='" + ClaFeb4 + "', ClaFeb5='" + ClaFeb5 + "', ClaMar1='" + ClaMar1 + "', ClaMar2='" + ClaMar2 + "'," +
                               "ClaMar3='" + ClaMar3 + "', ClaMar4='" + ClaMar4 + "', ClaMar5='" + ClaMar5 + "',ClaAbr1='" + ClaAbr1 + "', ClaAbr2='" + ClaAbr2 + "', ClaAbr3='" + ClaAbr3 + "', ClaAbr4='" + ClaAbr4 + "', ClaAbr5='" + ClaAbr5 + "', ClaMay1='" + ClaMay1 + "'," +
                               "ClaMay2='" + ClaMay2 + "', ClaMay3='" + ClaMay3 + "', ClaMay4='" + ClaMay4 + "',ClaMay5='" + ClaMay5 + "', ClaJun1='" + ClaJun1 + "', ClaJun2='" + ClaJun2 + "', ClaJun3='" + ClaJun3 + "', ClaJun4='" + ClaJun4 + "', ClaJun5='" + ClaJun5 + "'," +
                               "ClaJul1='" + ClaJul1 + "', ClaJul2='" + ClaJul2 + "', ClaJul3='" + ClaJul3 + "',ClaJul4='" + ClaJul4 + "', ClaJul5='" + ClaJul5 + "', ClaAgo1='" + ClaAgo1 + "', ClaAgo2='" + ClaAgo2 + "', ClaAgo3='" + ClaAgo3 + "', ClaAgo4='" + ClaAgo4 + "'," +
                               "ClaAgo5='" + ClaAgo5 + "', ClaSep1='" + ClaSep1 + "', ClaSep2='" + ClaSep2 + "',ClaSep3='" + ClaSep3 + "', ClaSep4='" + ClaSep4 + "', ClaSep5='" + ClaSep5 + "', ClaOct1='" + ClaOct1 + "', ClaOct2='" + ClaOct2 + "', ClaOct3='" + ClaOct3 + "'," +
                               "ClaOct4='" + ClaOct4 + "', ClaOct5='" + ClaOct5 + "', ClaNov1='" + ClaNov1 + "',ClaNov2='" + ClaNov2 + "', ClaNov3='" + ClaNov3 + "', ClaNov4='" + ClaNov4 + "', ClaNov5='" + ClaNov5 + "', ClaDic1='" + ClaDic1 + "', ClaDic2='" + ClaDic2 + "'," +
                               "ClaDic3='" + ClaDic3 + "', ClaDic4='" + ClaDic4 + "', ClaDic5='" + ClaDic5 + "',FecEstudio='" + Strings.Format(FecEstudio, varini.PstForFec) + "',pagonomina=" + pagonomina + ",pagocaja=" + pagocaja + ",ActVivienda=" + ActVivienda + "," +
                               "ActVehiculo=" + ActVehiculo + ",ActAportes=" + ActAportes + ",ActOtros=" + ActOtros + ",TotalAct=" + TotalAct + ",PasDeudas=" + PasDeudas + ",PasOtros=" + PasOtros + ",TotalPas=" + TotalPas + ",Patrimonio=" + Patrimonio + ",TotalPyP=" + TotalPyP +
                               ",GastosPers=" + gastospersonales + ",recogedeudas=" + recogedeudas + ",numcuenta=" + numcuenta + ",salpromedio=" + SalPromedio +
                               ",ActCajaBanco=" + ActCajaBanco + ",ActCxC=" + ActCxC + ",PasObliBanca=" + PasObliBanca + ",PasObliHipoteca=" + PasObliHipo + ",porcentaje='" + porcentaje + "',ActAhorros=" + ActAhorros + ",scoredatacredito=" + scoredatacredito + ",cupo=" + cupo +
                               ",descubierto=" + descubierto + ",saldodeudaexterna=" + saldodeudaexterna + ",cuotadeudaexterna=" + cuotadeudaexterna + ",califidatacredito='" + califidatacredito + "',SalMorDatacredito=" + SalMorDatacredito + ", extraSeguro=" + extra +
                               ",CapitalRiesgo=" + CapitalRiesgo + ",PorcentajePagaduria= " + PorcentajePagaduria + ",tipodstoPagaduria='" + tipodstoPagaduria + "'  where NroSolicitud=" + NumSolicitud + " and codigoter='" + Codigoter + "'";
                    break;
            }

            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabarEstudioCredito");
            if (ok == true)
            {
                // ok = this.BuscaSolicitudCredito(NumSolicitud, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", estadoAct); // ERROR: CS7036
                if (estadoAct == "P" || estadoAct == "C")
                {
                    mysql = "update cop_solcre set estado='" + Estado + "' where numero=" + NumSolicitud;
                    this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "GrabarEstudioCredito - Actualizar estado solicitud");
                }
                mysql = "update sys_maenit set cuotadeudaexterna=" + cuotadeudaexterna + ",Acierta=" + scoredatacredito + ",saldodeudaexterna=" + saldodeudaexterna + ",ScoreCifin=" + ScoreCifin + ",califidatacredito='" + califidatacredito + "',SalMorDatacredito=" + SalMorDatacredito + "  where codigoter='" + Codigoter + "'";
                this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "GrabarEstudioCredito - Actualizar estado solicitud");
            }

            return ok;
        }


        public DataSet CargarGrillaSolicitudCred(string codigoter, string solicitud, string estado, DateTime FecIni, DateTime FecFin, string signo, OdbcConnection myconnect)
        {
            string stwhere = "";
            switch (solicitud)
            {
                case "Todas":
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "DB2":
                            stmysql = "select sol.Estado,sol.NUMERO, sol.CODIGOTER,(mae.APELLIDO || ' ' || mae.NOMBRE) as Nombre,sol.VLR_SOLICITUD as ValorSolicitado, (sol.LINCRED || '-' || con.DESCRIPCION) as LineaCredito, sol.FECHA_SOLI as FechaSolicitud,  sol.TASA_INT as TasaInt, sol.PLAZO," +
                                      " fecdesc as FechaDscto,case sol.CLADES when '1' then 'Nomina' else 'Caja' end as clades from cop_solcre sol left join cop_concar12 con on sol.lincred = con.lincred " +
                                      "left join sys_maenit mae on sol.codigoter = mae.codigoter where " +
                                      (estado != "T" ? "sol.ESTADO = '" + estado + "' and" : "sol.ESTADO <> 'X' and") + " sol.FECHA_SOLI >='" + Strings.Format(FecIni, varini.PstForFec) + "' and sol.FECHA_SOLI <= '" + Strings.Format(FecFin, varini.PstForFec) + "' " +
                                      "and  sol.codigoter " + signo + "'" + codigoter + "'";
                            break;
                        default:
                            stmysql = "select sol.Estado,sol.NUMERO, sol.CODIGOTER,{fn concat(mae.APELLIDO,{fn concat(' ', mae.NOMBRE)})} as Nombre,sol.VLR_SOLICITUD as ValorSolicitado, {fn concat(rtrim(sol.LINCRED),{fn concat('-',con.DESCRIPCION)})} as LineaCredito, sol.FECHA_SOLI as FechaSolicitud,  sol.TASA_INT as TasaInt, sol.PLAZO," +
                                      " fecdesc as FechaDscto,case sol.CLADES when '1' then 'Nomina' else 'Caja' end as clades from cop_solcre sol left join cop_concar12 con on sol.lincred = con.lincred " +
                                      "left join sys_maenit mae on sol.codigoter = mae.codigoter where " +
                                      (estado != "T" ? "sol.ESTADO = '" + estado + "' and" : "sol.ESTADO <> 'X' and") + " sol.FECHA_SOLI >='" + Strings.Format(FecIni, varini.PstForFec) + "' and sol.FECHA_SOLI <= '" + Strings.Format(FecFin, varini.PstForFec) + "' " +
                                      "and  sol.codigoter " + signo + "'" + codigoter + "'";
                            break;
                    }
                    break;

                default:
                    switch (codigoter)
                    {
                        case "":
                        case "Todos":
                            stwhere = "";
                            break;
                        default:
                            stwhere = " and sol.CODIGOTER='" + Strings.Right("00000000000000" + codigoter, 14) + "'";
                            break;
                    }
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "DB2":
                            stmysql = "select sol.Estado,sol.NUMERO, sol.CODIGOTER,(mae.APELLIDO || ' ' || mae.NOMBRE)  as Nombre,sol.VLR_SOLICITUD as ValorSolicitado, (sol.LINCRED || '-' || con.DESCRIPCION) as LineaCredito, sol.FECHA_SOLI as FechaSolicitud,  sol.TASA_INT as TasaInt, sol.PLAZO," +
                                     " fecdesc as FechaDscto,case sol.CLADES when '1' then 'Nomina' else 'Caja' end as clades from cop_solcre sol left join cop_concar12 con on sol.lincred = con.lincred " +
                                     "left join sys_maenit mae on sol.codigoter = mae.codigoter where sol.NUMERO = " + solicitud + stwhere;
                            break;
                        default:
                            stmysql = "select sol.Estado,sol.NUMERO, sol.CODIGOTER,{fn concat(mae.APELLIDO,{fn concat(' ', mae.NOMBRE)})} as Nombre,sol.VLR_SOLICITUD as ValorSolicitado, {fn concat(rtrim(sol.LINCRED),{fn concat('-',con.DESCRIPCION)})} as LineaCredito, sol.FECHA_SOLI as FechaSolicitud,  sol.TASA_INT as TasaInt, sol.PLAZO," +
                                     " fecdesc as FechaDscto,case sol.CLADES when '1' then 'Nomina' else 'Caja' end as clades from cop_solcre sol left join cop_concar12 con on sol.lincred = con.lincred " +
                                     "left join sys_maenit mae on sol.codigoter = mae.codigoter where sol.NUMERO = " + solicitud + stwhere;
                            break;
                    }
                    break;
            }

            DataSet dtdatos = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CargarGrillaSolicitudCred", dtdatos, "TblSolCredito");

            return dtdatos;
        }

        public bool BuscaSolicitudesEnMaecar(int NumSolicitud, OdbcConnection myconnect)
        {
            stmysql = "select codigoter as campo1 from cop_maecar where numero_soli=" + NumSolicitud;
            ok = this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaSolicitudesEnMaecar");
            return ok;
        }

        public void CargaVentanaCredritosCoodeudor(string Codigoter, string Periodo, double Abono, string Usuario, Form Myforma, OdbcConnection mycon, ref string CodeDedudor, ref int lincred, ref double Numero)
        {
            // Frmcodeuda frmCoodeuda = new Frmcodeuda(mycon); // ERROR: CS0246
            // frmCoodeuda.TxtCodigoter.Text = Codigoter; // ERROR: CS0103
            // frmCoodeuda.Lblperiodo.Text = Periodo; // ERROR: CS0103
            // frmCoodeuda.Txtusuario.Text = Usuario; // ERROR: CS0103
            // frmCoodeuda.LblAbono.Text = Strings.Format(Abono, "###,###,###.00"); // ERROR: CS0103
            // frmCoodeuda.ShowDialog(Myforma); // ERROR: CS0103
            // CodeDedudor = frmCoodeuda.Codigoter; // ERROR: CS0103
            // lincred = frmCoodeuda.Lincred; // ERROR: CS0103
            // Numero = frmCoodeuda.Numero; // ERROR: CS0103
        }

        public void CargaVentanaCredritosCoodeudor(string Codigoter, string Periodo, double Abono, string Usuario, Form Myforma, OdbcConnection mycon)
        {
            string CodeDedudor = null;
            int lincred = 0;
            double Numero = 0;
            CargaVentanaCredritosCoodeudor(Codigoter, Periodo, Abono, Usuario, Myforma, mycon, ref CodeDedudor, ref lincred, ref Numero);
        }

        public bool PlanoReferencias(string NomArchivo, Form forma, OdbcConnection myconnect)
        {
            string line;
            decimal TotReg;
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Cargando Plano de Referencias", forma);
            string TipReferencia, codigoter, nombre, direccion, telefono;
            int ciudad;
            string contacto, tipproducto, numprod, celular, parentesco;

            ok = this.VaLidarPlanoReferencia(NomArchivo, forma, myconnect);
            if (ok == true)
            {
                try
                {
                    strStreamReader = new StreamReader(NomArchivo);
                    line = strStreamReader.ReadLine();
                    strStreamReader.Close();
                    // TotReg = Math.Round((double)(FileSystem.FileLen(NomArchivo) / (line.Length + 2))); // ERROR: CS0266
                    // BarraProgreso.ValorMinimoMaximo(0, (int)TotReg); // ERROR: CS0165
                    BarraProgreso.Show();

                    FileSystem.FileOpen(1, NomArchivo, OpenMode.Input);
                    while (FileSystem.EOF(1) == false)
                    {
                        TipReferencia = ""; FileSystem.Input(1, ref TipReferencia);
                        codigoter = ""; FileSystem.Input(1, ref codigoter);
                        nombre = ""; FileSystem.Input(1, ref nombre);
                        direccion = ""; FileSystem.Input(1, ref direccion);
                        telefono = ""; FileSystem.Input(1, ref telefono);
                        ciudad = 0; FileSystem.Input(1, ref ciudad);
                        contacto = ""; FileSystem.Input(1, ref contacto);
                        tipproducto = ""; FileSystem.Input(1, ref tipproducto);
                        numprod = ""; FileSystem.Input(1, ref numprod);
                        celular = ""; FileSystem.Input(1, ref celular);
                        parentesco = ""; FileSystem.Input(1, ref parentesco);

                        // this.msgconfig.GrabarReferencias(codigoter, TipReferencia, nombre, direccion, telefono, ciudad, contacto, tipproducto, numprod, celular, parentesco, myconnect); // ERROR: CS1503
                        BarraProgreso.PerformStep();
                    }
                    BarraProgreso.Close();
                    BarraProgreso.Dispose();
                    FileSystem.FileClose(1);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                    BarraProgreso.Close();
                    BarraProgreso.Dispose();
                    FileSystem.FileClose(1);
                    ok = false;
                }
            }
            return ok;
        }

        public bool VaLidarPlanoReferencia(string NomArchivo, Form Myforma, OdbcConnection myconnect, ref string mensaje)
        {
            string line;
            decimal totreg;
            ArrayList arreglo = new ArrayList();
            bool okk;
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Validando Plano de Referencia", Myforma);
            string TipReferencia, codigoter, nombre, direccion, telefono, ciudad;
            string contacto, tipproducto, numprod, celular, parentesco;
            bool NoExisteFicha = false;
            string nomruta = AppDomain.CurrentDomain.BaseDirectory + "\\Referencias.txt";
            try
            {
                okk = true;
                strStreamWriter = new StreamWriter(nomruta, false);
                strStreamReader = new StreamReader(NomArchivo);
                line = strStreamReader.ReadLine();

                // totreg = Math.Round((double)(FileSystem.FileLen(NomArchivo) / (line.Length + 2))); // ERROR: CS0266
                // BarraProgreso.ValorMinimoMaximo(0, (int)totreg); // ERROR: CS0165
                BarraProgreso.Show();

                arreglo.Add("Codigoter");
                arreglo.Add("Tipo Referencia");
                strStreamWriter.WriteLine(Strings.Join((object[])arreglo.ToArray(typeof(object)), "    "));
                while (line.Trim().Length > 20)
                {
                    mensaje = "";
                    arreglo.Clear();
                    arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));

                    TipReferencia = arreglo[0].ToString();
                    codigoter = arreglo[1].ToString();
                    nombre = arreglo[2].ToString();
                    direccion = arreglo[3].ToString();
                    telefono = arreglo[4].ToString();
                    ciudad = arreglo[5].ToString();
                    contacto = arreglo[6].ToString();
                    tipproducto = arreglo[7].ToString();
                    numprod = arreglo[8].ToString();
                    celular = arreglo[9].ToString();
                    parentesco = arreglo[10].ToString();
                    arreglo.Add(codigoter);
                    arreglo.Add(TipReferencia);
                    if (Information.IsNumeric(TipReferencia) == false)
                    {
                        mensaje = "Debe Ingresar un tipo de referencia. ";
                    }
                    else if (Convert.ToInt32(TipReferencia) > 4 || Convert.ToInt32(TipReferencia) < 1)
                    {
                        mensaje = "Tipo de referencia incorrecto. ";
                    }
                    // ok = this.msgconfig.BuscaAsociado(codigoter, myconnect); // ERROR: CS1501
                    if (ok == false)
                    {
                        mensaje += "Asociado no existe. ";
                    }
                    if (nombre.Trim() == "")
                    {
                        mensaje += "Nombre errado. ";
                    }

                    if (Information.IsNumeric(parentesco) == false)
                    {
                        mensaje += "Error en codigo de parentesco";
                    }
                    else
                    {
                        // if (msgconfig.BuscaParentesco(parentesco, myconnect) == false) // ERROR: CS7036
                        {
                            mensaje += "Parentesco no existe";
                        }
                    }

                    if (Information.IsNumeric(ciudad) == false)
                    {
                        mensaje += "Error en codigo de ciudad. ";
                    }
                    else
                    {
                        // ok = this.msgconfig.BuscaCiudad(ciudad, myconnect); // ERROR: CS1501
                        if (ok == false)
                        {
                            mensaje += "Codigo de ciudad no existe. ";
                        }
                    }
                    if (TipReferencia == "4")
                    {
                        if (Information.IsNumeric(tipproducto) == false)
                        {
                            mensaje += "Error en el tipo de producto ";
                        }
                        else if (Convert.ToInt32(tipproducto) < 1 || Convert.ToInt32(tipproducto) > 5)
                        {
                            mensaje += "Tipo de producto incorrecto ";
                        }
                        if (Information.IsNumeric(numprod) == false)
                        {
                            mensaje += "Numero de producto incorrecto";
                        }
                    }
                    if (mensaje.Trim() != "")
                    {
                        NoExisteFicha = true;
                        arreglo.Add(mensaje);
                        strStreamWriter.WriteLine(Strings.Join((object[])arreglo.ToArray(typeof(object)), "         "));
                    }

                    line = strStreamReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    BarraProgreso.PerformStep();
                }
                strStreamReader.Close();
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                if (NoExisteFicha == true)
                {
                    MessageBox.Show("El archivo plano tiene los siguientes errores.", "SOLIDO", MessageBoxButtons.OK);
                    System.Diagnostics.Process.Start(nomruta);
                    okk = false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamReader.Close();
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
            }
            return okk;
        }

        public bool VaLidarPlanoReferencia(string NomArchivo, Form Myforma, OdbcConnection myconnect)
        {
            string mensaje = "";
            return VaLidarPlanoReferencia(NomArchivo, Myforma, myconnect, ref mensaje);
        }

        public bool NominaCrearDescuentos(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
           int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection Conect, Form Pertenece,
           int Clades)
        {
            MsgBoxResult res;

            string stwhere;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;

            if (Agencia == "Todos")
            {
                WhereAgencia = "";
            }
            else
            {
                WhereAgencia = "  and Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' ";
            }
            if (Empresa == "Todos")
            {
                WhereEmpresa = "";
            }
            else
            {
                WhereEmpresa = " and Empresa = '" + Strings.Right("0000" + Empresa, 4) + "' ";
            }
            if (Cencosto == "Todos")
            {
                WhereCencosto = "";
            }
            else
            {
                WhereCencosto = " and cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' ";
            }

            stwhere = " Periodo = '" + CicloAAAAPP + "' " + WhereAgencia + WhereEmpresa + WhereCencosto;

            if (Periodicidad != "")
            {
                stmysql = "select codigoter as campo1 from cop_nomdes where  " + stwhere + " and periodicidad ='" + Periodicidad +
                             "' and adicional ='" + Adicional + "'";

                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NominaCrearDescuentos");

                if (ok == true)
                {
                    if (MessageBox.Show("Este proceso de descuento ya ha sido realizado... " + "\r\n" + "Desea borrarlo y continuar?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        stmysql = "delete from cop_nomdes where  " + stwhere + "  and periodicidad ='" + Periodicidad +
                                     "' and adicional ='" + Adicional + "' and codeuda = '99999999999999' ";
                        ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Conect, "NominaCrearDescuentos");
                    }
                    else
                    {
                        return false;
                    }
                }

                res = (MsgBoxResult)MessageBox.Show("El proceso se Iniciara. " + "\r\n" + "Los datos son correctos ?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (res == MsgBoxResult.Yes)
                {
                    switch (Clades)
                    {
                        case 0:
                            ok = NominaTodosDescuentos(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                               Adicional, Conect, Pertenece);
                            break;
                        case 1:
                            ok = NominaDescNoExtras(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                                                       Adicional, Conect, Pertenece);
                            break;
                        case 2:
                            ok = NominaDescSoloExtras(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                                              Adicional, Conect, Pertenece);
                            break;
                        case 3:
                            ok = NominaTodosDescuentos(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                                                   Adicional, Conect, Pertenece, TipoDescuento.Vacaciones);
                            break;
                        case 4:
                            ok = NominaTodosDescuentos(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                           Adicional, Conect, Pertenece, TipoDescuento.Sin_Vacaciones);
                            break;
                        case 5:
                            ok = NominaTodosDescuentos(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                           Adicional, Conect, Pertenece, TipoDescuento.Todos, true);
                            break;
                        case 6:
                            ok = NominaDescNoExtrasNoVacaciones(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                                                                     Adicional, Conect, Pertenece);
                            break;
                        case 7:
                            ok = NominaDescNoVacacionesNoPrimas(PeriodoNomina, Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad,
                                                                                     Adicional, Conect, Pertenece);
                            break;
                    }
                }
            }

            return ok;
        }

        public bool NominaTodosDescuentos(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
          int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection ConectSub, Form Pertenece,
          TipoDescuento Tipodescuento = TipoDescuento.Todos, bool UnConcepto = false)
        {
            string MysqlCodeu, MysqlCodeu1;
            ArrayList Codeudores = new ArrayList();
            bool Atrasados = false;
            string cobracodeudor = "";
            ERP.Core.Compartido.Configuracion.ParamSys paramSys = new ERP.Core.Compartido.Configuracion.ParamSys();

            string whereConcepto = "";
            string whereConceptoVacaciones = "";
            bool validaVacacionesAtrasados = false;
            string filtroBDWhere = "";
            string filtroBDSelect = " ";
            if (UnConcepto == true)
            {
                string comcep = Interaction.InputBox("A que concepto desea aplicar los descuentos...", "SOLIDO", "");
                if (comcep.Length > 6 || comcep == "")
                {
                    MessageBox.Show("El Concepto esta errado." + "\r\n" + "Inicie el proceso de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                else
                {
                    whereConcepto = " inner join cop_nomconce f on f.lincred = a.lincred and f.copto_nomina = '" + comcep + "' ";
                    whereConceptoVacaciones = " inner join cop_nomconce nomce on nomce.lincred = cupen.lincred and nomce.copto_nomina = '" + comcep + "' ";
                }
            }

            ERP.Core.CarteraFinanciera.Models.ParamCop parametros = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            ERP.Core.Compartido.Controles.Barraprogress progreso = new ERP.Core.Compartido.Controles.Barraprogress("Generando todos los descuentos.", Pertenece);

            string Mysql, mysql1;
            MsgBoxResult res;
            double tot_apor;
            string stwhere, stwhere1;
            string pdadesdec = "";
            string WhereHastaCiclo;
            int Clades_local; string Detalle = ""; DateTime FecIni = DateTime.MinValue; DateTime FecFin = DateTime.MinValue; int CicloMes = 0;
            string WhereAgencia;
            string WhereEmpresa;
            string WhereCencosto;
            string WhereVacaciones;
            string WhereCuopen;
            string WhereSaldo = "";
            int CantiDescu = 0;
            int i;
            string AgenciaHv, CencostoHv;

            if (Tipodescuento == Clscartera.TipoDescuento.Vacaciones)
            {
                WhereVacaciones = " and a.motivo_novedad='2' ";
            }
            else if (Tipodescuento == Clscartera.TipoDescuento.Sin_Vacaciones)
            {
                WhereVacaciones = " and a.motivo_novedad<>'2' ";
            }
            else
            {
                WhereVacaciones = " ";
            }

            if (Agencia == "Todos") { WhereAgencia = ""; AgenciaHv = "9999"; }
            else { WhereAgencia = "  and a.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' "; AgenciaHv = "a.agencia"; }
            if (Empresa == "Todos") { WhereEmpresa = ""; }
            else { WhereEmpresa = " and a.EmpDsto = '" + Strings.Right("0000" + Empresa, 4) + "' "; }
            if (Cencosto == "Todos") { WhereCencosto = ""; CencostoHv = "99999999"; }
            else { WhereCencosto = " and a.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' "; CencostoHv = "a.cencosto"; }

            stwhere = " a.Periodo_contable = '" + PeriodoNomina + "' ";
            stwhere1 = " " + WhereAgencia + WhereEmpresa + WhereCencosto;

            // parametros.BuscarPlanillaNomina(Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad, // ERROR: CS1061
             // Adicional, ConectSub, Clades_local, Detalle, FecIni, FecFin, CicloMes); // ERROR: CS1061

            res = (MsgBoxResult)MessageBox.Show("Desea incluir las cuotas Atrasadas?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (res == MsgBoxResult.Yes)
            {
                pdadesdec = Interaction.InputBox("Hasta que ciclo incluira las cuotas Atrasadas?", "SOLIDO", "");
                if (pdadesdec == "")
                {
                    MessageBox.Show("Debe especificar hasta que ciclo se incluiran las cuotas." + "\r\n" + "Intentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return ok;
                }
                if (Information.IsNumeric(pdadesdec) == false || pdadesdec.Length < 6)
                {
                    MessageBox.Show("Debe especificar un periodo valido." + "\r\n" + "Intentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return ok;
                }
                WhereHastaCiclo = " and a.periodo_causa <= '" + pdadesdec + "' ";
                WhereCuopen = "";
                Atrasados = true;
                if (Tipodescuento == Clscartera.TipoDescuento.Vacaciones)
                {
                    validaVacacionesAtrasados = true;
                    WhereVacaciones = " ";
                }
                else
                {
                    validaVacacionesAtrasados = false;
                }
            }
            else
            {
                WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                Atrasados = false;
                WhereCuopen = " and (d.saldo <> 0 or d.cuota <> 0)";
            }

            // paramSys.BuscarCompania(varini.sptCodEmpr, ConectSub, "", "", "", "", "", "", "", "", "", "", "", "", "", // ERROR: CS7036
                                 // "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", cobracodeudor); // ERROR: CS7036

            Mysql = "select a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4 from cop_cuopen_vw a " +
            " inner join sys_maenit c on c.codigoter = a.codigoter " +
            " left join cop_salmaecar d on d.Periodo = '" + PeriodoNomina + "' and d.codigoter = a.codigoter " +
            " and d.lincred = a.lincred and d.numero = a.numero " + whereConcepto + " where  " +
            stwhere + WhereHastaCiclo + " and c.estado <> 'R' and a.percidad ='" + Periodicidad +
            "'  and a.clades = '1' " + WhereVacaciones + "  " + stwhere1 +
            " and ((a.lincred >= 1000 and d.saldo <> 0 ) or (a.lincred < 1000 " + WhereCuopen + ")) " +
            " AND  (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
            " group by a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4  order by a.codigoter asc";

            try
            {
                DataSet myReader = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(Mysql, ConectSub, "Nomina Todos Descuentos", myReader, "DATOSPrincipal");
                CantiDescu = myReader.Tables["DATOSPrincipal"].Rows.Count;

                myReader.Tables.Add("DATOSSubConsulta");

                progreso.ValorMinimoMaximo(0, CantiDescu);
                progreso.Show();
                ArrayList Aplicados = new ArrayList();

                for (i = 0; i <= CantiDescu - 1; i++)
                {
                    DataRow row = myReader.Tables["DATOSPrincipal"].Rows[i];

                    Application.DoEvents();
                    progreso.PerformStep();

                    if (Aplicados.Contains(row["codigoter"].ToString() + row["lincred"].ToString() +
                    row["numero"].ToString()) == false)
                    {
                        if (validaVacacionesAtrasados == false)
                        {
                            Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                      "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";

                            mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                             CicloAAAAPP + "' as periodo,a.percidad,'" +
                             Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                             Detalle + "' as detalle,'" +
                             CicloMes + "' as ciclo, " + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                              " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                              " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                              " sum(a.causa_inte) " +
                              " as causa_inte,sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                              " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                              " from cop_cuopen_vw a  " +
                              whereConcepto + " where a.codigoter ='" +
                             row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                             "' and a.numero='" + row["numero"] +
                             "' and a.clades = '1' " + WhereVacaciones +
                             " and (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA " +
                             " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
                             "group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero";
                        }
                        else
                        {
                            switch (varini.pstTipoBD.ToUpper())
                            {
                                case "MYSQL":
                                case "ORACLE":
                                case "POSTGRESQL":
                                    filtroBDWhere = "  (concat(a.codigoter,concat(a.lincred,a.numero))) =  ";
                                    filtroBDSelect = "  (concat(cupen.codigoter,concat(cupen.lincred,cupen.numero)))   ";
                                    break;
                                case "SQL":
                                    filtroBDWhere = "  ( {fn  concat(a.codigoter, {fn concat(rtrim(a.lincred),rtrim(a.numero))})}) =  ";
                                    filtroBDSelect = "  ( {fn concat(cupen.codigoter,{fn concat(rtrim(cupen.lincred),rtrim(cupen.numero))})})";
                                    break;
                                case "DB2":
                                    filtroBDWhere = "   (a.codigoter || a.lincred || a.numero) =  ";
                                    filtroBDSelect = "  (cupen.codigoter || cupen.lincred  || cupen.numero)   ";
                                    break;
                            }

                            Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                  "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";

                            mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                             CicloAAAAPP + "' as periodo,a.percidad,'" +
                             Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                             Detalle + "' as detalle,'" +
                             CicloMes + "' as ciclo, " + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                              " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                              " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                              " sum(a.causa_inte) " +
                              " as causa_inte,sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                              " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                              " from cop_cuopen_vw a  " +
                              whereConcepto + " where a.codigoter ='" +
                             row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                             "' and a.numero='" + row["numero"] +
                             "' and a.clades = '1' " +
                             " and (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA " +
                             " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0  and  " + filtroBDWhere +
                             " ( select " + filtroBDSelect + "  from cop_cuopen_vw  cupen " + whereConceptoVacaciones + "  where cupen.codigoter='" + row["codigoter"].ToString() + "'" +
                             "  and cupen.Periodo_contable =" + PeriodoNomina + "  and  cupen.percidad ='" + Periodicidad + "' " +
                             "  and cupen.lincred ='" + row["lincred"] + "' and cupen.numero='" + row["numero"] + "' and cupen.clades = '1'  and   cupen.motivo_novedad='2')" +
                             " group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero ";
                        }
                        this.OdbcConnect.ExecuteQueryconec(Mysql + mysql1, ConectSub, "NominaDescTodos");
                    }

                    ok = true;
                    Aplicados.Add(row["codigoter"].ToString() + row["lincred"].ToString() +
                             row["numero"].ToString());

                    if (cobracodeudor == "Y")
                    {
                        if (Atrasados == true)
                        {
                            for (int j = 1; j <= 4; j++)
                            {
                                MysqlCodeu = "";
                                MysqlCodeu1 = "";
                                string CodCodeudor;
                                string EmpresaCode = " ";
                                CodCodeudor = Strings.Right("00000000000000" + row["codeudor" + j].ToString().Trim(), 14);
                                if (CodCodeudor != "00000000000000" &&
                                   CodCodeudor != row["codigoter"].ToString())
                                {
                                    Codeudores.Add(CodCodeudor);
                                    // BuscaAsociado(CodCodeudor, ConectSub, "", "", "", "", // ERROR: CS7036
                                    // "", "", "", "", "", "", "", "", "", "", EmpresaCode); // ERROR: CS7036

                                    MysqlCodeu = "delete  from cop_nomdes where codigoter ='" +
                                    CodCodeudor + "' and periodo = '" + CicloAAAAPP + "' and periodicidad ='" +
                                    Periodicidad + "' and lincred ='" + row["lincred"] +
                                    "' and numero='" + row["numero"] + "'";

                                    this.OdbcConnect.ExecuteQueryconec(MysqlCodeu, ConectSub, "NominaDescTodosEliminacodeuda");

                                    if (validaVacacionesAtrasados == false)
                                    {
                                        MysqlCodeu = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                              "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras,Codeuda) ";

                                        MysqlCodeu1 = "select '" + EmpresaCode + "',a.agencia,a.cencosto,'" +
                                        CicloAAAAPP + "' as periodo,'" + Periodicidad + "' as percidad,'" +
                                        Adicional + "' as adicional,'" +
                                        CodCodeudor + "' as codigoter,a.lincred,a.numero,'" +
                                        Detalle + "' as detalle,'" +
                                        CicloMes + "' as ciclo," + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                                        " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                        " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                        " sum(a.causa_inte) " +
                                        " as causa_inte,sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu, " +
                                        " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro ,a.codigoter as Codeuda " +
                                        " from cop_cuopen_vw a " + whereConcepto +
                                        " where  a.codigoter ='" +
                                        row["codigoter"].ToString() + "' and " + stwhere + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                        "' and a.numero='" + row["numero"] + "' and  (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA " +
                                        " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0  and a.periodo_causa < " + pdadesdec +
                                        " group by a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero";
                                    }
                                    else
                                    {
                                        switch (varini.pstTipoBD.ToUpper())
                                        {
                                            case "MYSQL":
                                            case "ORACLE":
                                            case "POSTGRESQL":
                                                filtroBDWhere = "  (concat(a.codigoter,concat(a.lincred,a.numero))) =  ";
                                                filtroBDSelect = "  (concat(cupen.codigoter,concat(cupen.lincred,cupen.numero)))   ";
                                                break;
                                            case "SQL":
                                                filtroBDWhere = "  ( {fn  concat(a.codigoter, {fn concat(rtrim(a.lincred),rtrim(a.numero))})}) =  ";
                                                filtroBDSelect = "  ( {fn concat(cupen.codigoter,{fn concat(rtrim(cupen.lincred),rtrim(cupen.numero))})})";
                                                break;
                                            case "DB2":
                                                filtroBDWhere = "   (a.codigoter || a.lincred || a.numero) =  ";
                                                filtroBDSelect = "  (cupen.codigoter || cupen.lincred  || cupen.numero)   ";
                                                break;
                                        }

                                        MysqlCodeu = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                                                                     "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras,Codeuda) ";

                                        MysqlCodeu1 = "select '" + EmpresaCode + "',a.agencia,a.cencosto,'" +
                                        CicloAAAAPP + "' as periodo,'" + Periodicidad + "' as percidad,'" +
                                        Adicional + "' as adicional,'" +
                                        CodCodeudor + "' as codigoter,a.lincred,a.numero,'" +
                                        Detalle + "' as detalle,'" +
                                        CicloMes + "' as ciclo," + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                                        " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                        " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                        " sum(a.causa_inte) " +
                                        " as causa_inte,sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu, " +
                                        " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro ,a.codigoter as Codeuda " +
                                        " from cop_cuopen_vw a " + whereConcepto +
                                        " where  a.codigoter ='" +
                                        row["codigoter"].ToString() + "' and " + stwhere + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                        "' and a.numero='" + row["numero"] + "' and  (CAUSA_CAPI+ CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA " +
                                        " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0  and a.periodo_causa < " + pdadesdec +
                                        "  and " + filtroBDWhere + "  ( select " + filtroBDSelect + " from cop_cuopen_vw  cupen " + whereConceptoVacaciones + " where cupen.codigoter='" + row["codigoter"].ToString() + "' " +
                                        "  and cupen.Periodo_contable =" + PeriodoNomina + " and cupen.percidad ='" + Periodicidad + "' " +
                                        "  and cupen.lincred ='" + row["lincred"] + "' and cupen.numero='" + row["numero"] + "' and cupen.clades = '1'  and   cupen.motivo_novedad='2')" +
                                        " group by a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero";
                                    }

                                    this.OdbcConnect.ExecuteQueryconec(MysqlCodeu + MysqlCodeu1, ConectSub, "NominaDescTodosGrabacodeuda");
                                }
                            }
                        }
                    }
                }

                myReader.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
            }
            if (progreso != null)
            {
                progreso.Close();
            }

            if (ok == false)
            {
                MessageBox.Show("No se encontraron datos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                // if (i != CantiDescu) // ERROR: CS0165
                {
                    MessageBox.Show("El Proceso Termino Pero no aplico todos los Descuentos .", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                {
                    MessageBox.Show("El Proceso Termino Correctamente.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            return ok;
        }

        // NOTE: NominaDescNoExtrasNoVacaciones, NominaDescNoVacacionesNoPrimas, NominaDescNoExtras, NominaDescSoloExtras
        // follow nearly identical patterns to NominaTodosDescuentos above.
        // They are translated faithfully from the VB source below.

        public bool NominaDescNoExtrasNoVacaciones(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
             int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection ConectSub, Form Pertenece)
        {
            string MysqlCodeu, MysqlCodeu1;
            ArrayList Codeudores = new ArrayList();
            bool Atrasados = false;
            string cobracodeudor = "";
            ERP.Core.Compartido.Configuracion.ParamSys paramSys = new ERP.Core.Compartido.Configuracion.ParamSys();
            ERP.Core.CarteraFinanciera.Models.ParamCop parametros = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            ERP.Core.Compartido.Controles.Barraprogress progreso = new ERP.Core.Compartido.Controles.Barraprogress("Generando descuentos sin Extras ni vacaciones.", Pertenece);

            string Mysql, mysql1;
            MsgBoxResult res;
            double tot_apor;
            string stwhere, stwhere1;
            string pdadesdec = "";
            string WhereHastaCiclo;
            int Clades_local = 0; string Detalle = ""; DateTime FecIni = DateTime.MinValue; DateTime FecFin = DateTime.MinValue; int CicloMes = 0;
            string WhereAgencia, WhereEmpresa, WhereCencosto;
            string WhereSaldo = "";
            int CantiDescu = 0;
            string AgenciaHv, CencostoHv, WhereVacaciones;
            int i = 0;

            if (Agencia == "Todos") { WhereAgencia = ""; AgenciaHv = "9999"; }
            else { WhereAgencia = "  and a.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' "; AgenciaHv = "a.agencia"; }
            if (Empresa == "Todos") { WhereEmpresa = ""; }
            else { WhereEmpresa = " and a.EmpDsto = '" + Strings.Right("0000" + Empresa, 4) + "' "; }
            if (Cencosto == "Todos") { WhereCencosto = ""; CencostoHv = "99999999"; }
            else { WhereCencosto = " and a.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' "; CencostoHv = "a.cencosto"; }

            stwhere = " a.Periodo_contable = '" + PeriodoNomina + "' ";
            stwhere1 = " " + WhereAgencia + WhereEmpresa + WhereCencosto;
            WhereVacaciones = " and a.motivo_novedad<>'2' ";

            // parametros.BuscarPlanillaNomina(Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad, // ERROR: CS1061
             // Adicional, ConectSub, Clades_local, Detalle, FecIni, FecFin, CicloMes); // ERROR: CS1061

            res = (MsgBoxResult)MessageBox.Show("Desea incluir las cuotas Atrasadas?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (res == MsgBoxResult.Yes)
            {
                pdadesdec = Interaction.InputBox("Hasta que ciclo incluira las cuotas Atrasadas?", "SOLIDO", "");
                if (pdadesdec == "") { MessageBox.Show("Debe especificar hasta que ciclo se incluiran las cuotas.\r\nIntentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return ok; }
                if (Information.IsNumeric(pdadesdec) == false || pdadesdec.Length < 6) { MessageBox.Show("Debe especificar un periodo valido.\r\nIntentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return ok; }
                WhereHastaCiclo = " and a.periodo_causa <= '" + pdadesdec + "' ";
                Atrasados = true;
            }
            else
            {
                WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                Atrasados = false;
            }

            // paramSys.BuscarCompania(varini.sptCodEmpr, ConectSub, "", "", "", "", "", "", "", "", "", "", "", "", "", // ERROR: CS7036
                                 // "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", cobracodeudor); // ERROR: CS7036

            Mysql = "select a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4 from cop_cuopen_vw a " +
            " inner join sys_maenit c on c.codigoter = a.codigoter " +
            " left join cop_salmaecar d on d.Periodo = '" + PeriodoNomina + "' and d.codigoter = a.codigoter " +
            " and d.lincred = a.lincred and d.numero = a.numero  where  " +
            stwhere + WhereHastaCiclo + " and c.estado <> 'R' and a.percidad ='" + Periodicidad +
            "'  and d.clades = '1' " + WhereVacaciones + "  " + stwhere1 +
            " and ((a.lincred >= 1000 and d.saldo <> 0) or (a.lincred < 1000 and (d.saldo <> 0 or d.cuota <> 0))) " +
            " AND  (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
            " group by a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4  order by a.codigoter asc";

            try
            {
                DataSet myReader = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(Mysql, ConectSub, "Nomina Desc No Extras", myReader, "DATOSPrincipal");
                CantiDescu = myReader.Tables["DATOSPrincipal"].Rows.Count;
                myReader.Tables.Add("DATOSSubConsulta");
                progreso.ValorMinimoMaximo(0, CantiDescu);
                progreso.Show();
                ArrayList Aplicados = new ArrayList();

                for (i = 0; i <= CantiDescu - 1; i++)
                {
                    DataRow row = myReader.Tables["DATOSPrincipal"].Rows[i];
                    Application.DoEvents();
                    progreso.PerformStep();

                    if (Aplicados.Contains(row["codigoter"].ToString() + row["lincred"].ToString() + row["numero"].ToString()) == false)
                    {
                        Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                  "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";

                        mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                         CicloAAAAPP + "' as periodo,a.percidad,'" +
                          Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                         Detalle + "' as detalle,'" +
                         CicloMes + "' as ciclo, " + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                          " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                          " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                          " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end as causa_inte, " +
                         " rtrim('0') as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                         " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                         " from cop_cuopen_vw a " +
                         "left join cop_maecar b on a.codigoter = b.codigoter  and a.lincred = b.lincred " +
                         " and a.numero = b.numero " +
                         " inner join cop_concar12 c on c.lincred = a.lincred " +
                         " where a.codigoter ='" +
                         row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                         "' and a.numero='" + row["numero"] +
                         "' and a.clades = '1' " +
                         " and (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA " +
                         " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
                         "group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";

                        this.OdbcConnect.ExecuteQueryconec(Mysql + mysql1, ConectSub, "NominaDescTodos");
                    }

                    ok = true;
                    Aplicados.Add(row["codigoter"].ToString() + row["lincred"].ToString() + row["numero"].ToString());

                    if (cobracodeudor == "Y")
                    {
                        if (Atrasados == true)
                        {
                            for (int j = 1; j <= 4; j++)
                            {
                                string CodCodeudor;
                                MysqlCodeu = "";
                                MysqlCodeu1 = "";
                                CodCodeudor = Strings.Right("00000000000000" + row["codeudor" + j].ToString().Trim(), 14);
                                if (CodCodeudor != "00000000000000" && CodCodeudor != row["codigoter"].ToString())
                                {
                                    Codeudores.Add(CodCodeudor);
                                    MysqlCodeu = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                          "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras,Codeuda) ";
                                    MysqlCodeu1 = "select a.EmpDsto,a.agencia,a.cencosto,'" +
                                    CicloAAAAPP + "' as periodo,'" + Periodicidad + "' as percidad,'" +
                                    Adicional + "' as adicional,'" +
                                    CodCodeudor + "' as codigoter,a.lincred,a.numero,'" +
                                    Detalle + "' as detalle,'" +
                                    CicloMes + "' as ciclo," + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                                    " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                    " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                    " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end  as causa_inte, rtrim('0') as causa_extr," +
                                    " sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu)as causa_segu," +
                                    "sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro,a.codigoter as Codeuda " +
                                    " from cop_cuopen_vw a " +
                                    "  inner join cop_concar12 c on c.lincred = a.lincred " +
                                    " where a.clades = '1' and " +
                                    " a.codigoter ='" +
                                    row["codigoter"].ToString() + "' and " + stwhere + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                    "' and a.numero='" + row["numero"] + "' and and (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA " +
                                    " + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 and a.periodo_causa < " + pdadesdec +
                                    " group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                    this.OdbcConnect.ExecuteQueryconec(MysqlCodeu + MysqlCodeu1, ConectSub, "NominaDescTodosGrabacodeuda");
                                }
                            }
                        }
                    }
                }
                myReader.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
            }
            if (progreso != null) { progreso.Close(); }
            if (ok == false) { MessageBox.Show("No se encontraron datos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            else { if (i != CantiDescu) { MessageBox.Show("El Proceso Termino Pero no aplico todos los Descuentos .", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation); } else { MessageBox.Show("El Proceso Termino Correctamente.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); } }
            return ok;
        }

        public bool NominaDescNoVacacionesNoPrimas(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
             int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection ConectSub, Form Pertenece)
        {
            string MysqlCodeu, MysqlCodeu1;
            ArrayList Codeudores = new ArrayList();
            bool Atrasados = false;
            string cobracodeudor = "";
            ERP.Core.Compartido.Configuracion.ParamSys paramSys = new ERP.Core.Compartido.Configuracion.ParamSys();
            ERP.Core.CarteraFinanciera.Models.ParamCop parametros = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            ERP.Core.Compartido.Controles.Barraprogress progreso = new ERP.Core.Compartido.Controles.Barraprogress("Generando descuentos sin Extras ni vacaciones.", Pertenece);

            string Mysql, mysql1;
            MsgBoxResult res;
            string stwhere, stwhere1;
            string pdadesdec = "";
            string WhereHastaCiclo;
            int Clades_local = 0; string Detalle = ""; DateTime FecIni = DateTime.MinValue; DateTime FecFin = DateTime.MinValue; int CicloMes = 0;
            string WhereAgencia, WhereEmpresa, WhereCencosto;
            int CantiDescu = 0;
            string AgenciaHv, CencostoHv, WhereVacacionesPrimas;
            int i = 0;

            if (Agencia == "Todos") { WhereAgencia = ""; AgenciaHv = "9999"; }
            else { WhereAgencia = "  and a.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' "; AgenciaHv = "a.agencia"; }
            if (Empresa == "Todos") { WhereEmpresa = ""; }
            else { WhereEmpresa = " and a.EmpDsto = '" + Strings.Right("0000" + Empresa, 4) + "' "; }
            if (Cencosto == "Todos") { WhereCencosto = ""; CencostoHv = "99999999"; }
            else { WhereCencosto = " and a.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' "; CencostoHv = "a.cencosto"; }

            stwhere = " a.Periodo_contable = '" + PeriodoNomina + "' ";
            stwhere1 = " " + WhereAgencia + WhereEmpresa + WhereCencosto;
            WhereVacacionesPrimas = " and a.clase_novedad = ' ' and a.motivo_novedad=' ' ";

            // parametros.BuscarPlanillaNomina(Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad, // ERROR: CS1061
             // Adicional, ConectSub, Clades_local, Detalle, FecIni, FecFin, CicloMes); // ERROR: CS1061

            res = (MsgBoxResult)MessageBox.Show("Desea incluir las cuotas Atrasadas?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == MsgBoxResult.Yes)
            {
                pdadesdec = Interaction.InputBox("Hasta que ciclo incluira las cuotas Atrasadas?", "SOLIDO", "");
                if (pdadesdec == "") { MessageBox.Show("Debe especificar hasta que ciclo se incluiran las cuotas.\r\nIntentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return ok; }
                if (Information.IsNumeric(pdadesdec) == false || pdadesdec.Length < 6) { MessageBox.Show("Debe especificar un periodo valido.\r\nIntentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return ok; }
                WhereHastaCiclo = " and a.periodo_causa <= '" + pdadesdec + "' ";
                Atrasados = true;
            }
            else
            {
                WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                Atrasados = false;
            }

            // paramSys.BuscarCompania(varini.sptCodEmpr, ConectSub, "", "", "", "", "", "", "", "", "", "", "", "", "", // ERROR: CS7036
                                 // "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", cobracodeudor); // ERROR: CS7036

            Mysql = "select a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4,a.tipoextra from cop_cuopen_vw a " +
            " inner join sys_maenit c on c.codigoter = a.codigoter " +
            " left join cop_salmaecar d on d.Periodo = '" + PeriodoNomina + "' and d.codigoter = a.codigoter " +
            " and d.lincred = a.lincred and d.numero = a.numero  where  " +
            stwhere + WhereHastaCiclo + " and c.estado <> 'R' and a.percidad ='" + Periodicidad +
            "'  and a.clades = '1' " + WhereVacacionesPrimas + "  " + stwhere1 +
            " and ((a.lincred >= 1000 and d.saldo <> 0) or (a.lincred < 1000 and (d.saldo <> 0 or d.cuota <> 0))) " +
            " AND  (CAUSA_CAPI + CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
            " group by a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4  order by a.codigoter asc";

            try
            {
                DataSet myReader = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(Mysql, ConectSub, "Nomina Desc No Extras", myReader, "DATOSPrincipal");
                CantiDescu = myReader.Tables["DATOSPrincipal"].Rows.Count;
                myReader.Tables.Add("DATOSSubConsulta");
                progreso.ValorMinimoMaximo(0, CantiDescu);
                progreso.Show();
                ArrayList Aplicados = new ArrayList();

                for (i = 0; i <= CantiDescu - 1; i++)
                {
                    DataRow row = myReader.Tables["DATOSPrincipal"].Rows[i];
                    Application.DoEvents();
                    progreso.PerformStep();

                    if (Aplicados.Contains(row["codigoter"].ToString() + row["lincred"].ToString() + row["numero"].ToString()) == false)
                    {
                        bool tipoextraIsNull = row["tipoextra"] is DBNull;
                        if (tipoextraIsNull)
                        {
                            Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                            "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";
                            mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                             CicloAAAAPP + "' as periodo,a.percidad,'" +
                              Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                             Detalle + "' as detalle,'" +
                             CicloMes + "' as ciclo, " + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                              " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                              " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                              " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end as causa_inte, " +
                             " sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                             " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                             " from cop_cuopen_vw a " +
                             "left join cop_maecar b on a.codigoter = b.codigoter  and a.lincred = b.lincred " +
                             " and a.numero = b.numero " +
                             " inner join cop_concar12 c on c.lincred = a.lincred " +
                             " where a.codigoter ='" +
                             row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                             "' and a.numero='" + row["numero"] +
                             "' and a.clades = '1' " +
                             " and (CAUSA_CAPI + CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA " +
                             " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
                             "group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                            this.OdbcConnect.ExecuteQueryconec(Mysql + mysql1, ConectSub, "NominaDescTodos");
                        }
                        else
                        {
                            if (row["tipoextra"].ToString() != "P")
                            {
                                Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                          "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";
                                mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                                 CicloAAAAPP + "' as periodo,a.percidad,'" +
                                  Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                                 Detalle + "' as detalle,'" +
                                 CicloMes + "' as ciclo, " + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                                  " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                  " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                  " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end as causa_inte, " +
                                 " sum(a.causa_extr) as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                                 " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                                 " from cop_cuopen_vw a " +
                                 "left join cop_maecar b on a.codigoter = b.codigoter  and a.lincred = b.lincred " +
                                 " and a.numero = b.numero " +
                                 " inner join cop_concar12 c on c.lincred = a.lincred " +
                                 " where a.codigoter ='" +
                                 row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                 "' and a.numero='" + row["numero"] +
                                 "' and a.clades = '1' " +
                                 " and (CAUSA_CAPI + CAUSA_EXTR + CAUSA_INTE+ INTMOR_CAUSA " +
                                 " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
                                 "group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                this.OdbcConnect.ExecuteQueryconec(Mysql + mysql1, ConectSub, "NominaDescTodos");
                            }
                        }
                    }

                    ok = true;
                    Aplicados.Add(row["codigoter"].ToString() + row["lincred"].ToString() + row["numero"].ToString());

                    if (cobracodeudor == "Y")
                    {
                        if (Atrasados == true)
                        {
                            for (int j = 1; j <= 4; j++)
                            {
                                string CodCodeudor;
                                MysqlCodeu = "";
                                MysqlCodeu1 = "";
                                CodCodeudor = Strings.Right("00000000000000" + row["codeudor" + j].ToString().Trim(), 14);
                                if (CodCodeudor != "00000000000000" && CodCodeudor != row["codigoter"].ToString())
                                {
                                    Codeudores.Add(CodCodeudor);
                                    MysqlCodeu = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                          "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras,Codeuda) ";
                                    MysqlCodeu1 = "select a.EmpDsto,a.agencia,a.cencosto,'" +
                                    CicloAAAAPP + "' as periodo,'" + Periodicidad + "' as percidad,'" +
                                    Adicional + "' as adicional,'" +
                                    CodCodeudor + "' as codigoter,a.lincred,a.numero,'" +
                                    Detalle + "' as detalle,'" +
                                    CicloMes + "' as ciclo," + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                                    " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                    " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                    " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end  as causa_inte, rtrim('0') as causa_extr," +
                                    " sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu)as causa_segu," +
                                    "sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro,a.codigoter as Codeuda " +
                                    " from cop_cuopen_vw a " +
                                    "  inner join cop_concar12 c on c.lincred = a.lincred " +
                                    " where a.clades = '1' and " +
                                    " a.codigoter ='" +
                                    row["codigoter"].ToString() + "' and " + stwhere + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                    "' and a.numero='" + row["numero"] + "' and and (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA " +
                                    " + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 and a.periodo_causa < " + pdadesdec +
                                    " group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                    this.OdbcConnect.ExecuteQueryconec(MysqlCodeu + MysqlCodeu1, ConectSub, "NominaDescTodosGrabacodeuda");
                                }
                            }
                        }
                    }
                }
                myReader.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
            }
            if (progreso != null) { progreso.Close(); }
            if (ok == false) { MessageBox.Show("No se encontraron datos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            else { if (i != CantiDescu) { MessageBox.Show("El Proceso Termino Pero no aplico todos los Descuentos .", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation); } else { MessageBox.Show("El Proceso Termino Correctamente.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); } }
            return ok;
        }

        public bool NominaDescNoExtras(int PeriodoNomina, string Agencia, string Empresa, string Cencosto,
             int CicloAAAAPP, string Periodicidad, string Adicional, OdbcConnection ConectSub, Form Pertenece)
        {
            string MysqlCodeu, MysqlCodeu1;
            ArrayList Codeudores = new ArrayList();
            bool Atrasados = false;
            string cobracodeudor = "";
            ERP.Core.Compartido.Configuracion.ParamSys paramSys = new ERP.Core.Compartido.Configuracion.ParamSys();
            ERP.Core.CarteraFinanciera.Models.ParamCop parametros = new ERP.Core.CarteraFinanciera.Models.ParamCop();
            ERP.Core.Compartido.Controles.Barraprogress progreso = new ERP.Core.Compartido.Controles.Barraprogress("Generando descuentos sin Extras.", Pertenece);

            string Mysql, mysql1;
            MsgBoxResult res;
            string stwhere, stwhere1;
            string pdadesdec = "";
            string WhereHastaCiclo;
            int Clades_local = 0; string Detalle = ""; DateTime FecIni = DateTime.MinValue; DateTime FecFin = DateTime.MinValue; int CicloMes = 0;
            string WhereAgencia, WhereEmpresa, WhereCencosto;
            int CantiDescu = 0;
            string AgenciaHv, CencostoHv;
            int i = 0;

            if (Agencia == "Todos") { WhereAgencia = ""; AgenciaHv = "9999"; }
            else { WhereAgencia = "  and a.Agencia = '" + Strings.Right("0000" + Agencia, 4) + "' "; AgenciaHv = "a.agencia"; }
            if (Empresa == "Todos") { WhereEmpresa = ""; }
            else { WhereEmpresa = " and a.EmpDsto = '" + Strings.Right("0000" + Empresa, 4) + "' "; }
            if (Cencosto == "Todos") { WhereCencosto = ""; CencostoHv = "99999999"; }
            else { WhereCencosto = " and a.cencosto = '" + Strings.Right("00000000" + Cencosto, 8) + "' "; CencostoHv = "a.cencosto"; }

            stwhere = " a.Periodo_contable = '" + PeriodoNomina + "' ";
            stwhere1 = " " + WhereAgencia + WhereEmpresa + WhereCencosto;

            // parametros.BuscarPlanillaNomina(Agencia, Empresa, Cencosto, CicloAAAAPP, Periodicidad, // ERROR: CS1061
             // Adicional, ConectSub, Clades_local, Detalle, FecIni, FecFin, CicloMes); // ERROR: CS1061

            res = (MsgBoxResult)MessageBox.Show("Desea incluir las cuotas Atrasadas?", "SOLIDO", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == MsgBoxResult.Yes)
            {
                pdadesdec = Interaction.InputBox("Hasta que ciclo incluira las cuotas Atrasadas?", "SOLIDO", "");
                if (pdadesdec == "") { MessageBox.Show("Debe especificar hasta que ciclo se incluiran las cuotas.\r\nIntentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return ok; }
                if (Information.IsNumeric(pdadesdec) == false || pdadesdec.Length < 6) { MessageBox.Show("Debe especificar un periodo valido.\r\nIntentelo de nuevo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); return ok; }
                WhereHastaCiclo = " and a.periodo_causa <= '" + pdadesdec + "' ";
                Atrasados = true;
            }
            else
            {
                WhereHastaCiclo = " and a.periodo_causa = '" + CicloAAAAPP + "' ";
                Atrasados = false;
            }

            // paramSys.BuscarCompania(varini.sptCodEmpr, ConectSub, "", "", "", "", "", "", "", "", "", "", "", "", "", // ERROR: CS7036
                                 // "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", cobracodeudor); // ERROR: CS7036

            Mysql = "select a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4 from cop_cuopen_vw a " +
            " inner join sys_maenit c on c.codigoter = a.codigoter " +
            " left join cop_salmaecar d on d.Periodo = '" + PeriodoNomina + "' and d.codigoter = a.codigoter " +
            " and d.lincred = a.lincred and d.numero = a.numero  where  " +
            stwhere + WhereHastaCiclo + " and c.estado <> 'R' and a.percidad ='" + Periodicidad +
            "'  and d.clades = '1' " + stwhere1 +
            " and ((a.lincred >= 1000 and d.saldo <> 0) or (a.lincred < 1000 and (d.saldo <> 0 or d.cuota <> 0))) " +
            " AND  (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
            " group by a.codigoter, a.lincred, a.numero,a.PERCIDAD,a.codeudor1," +
            " a.codeudor2,a.codeudor3,a.codeudor4  order by a.codigoter asc";

            try
            {
                DataSet myReader = new DataSet();
                this.OdbcConnect.ExecuteQueryDataset(Mysql, ConectSub, "Nomina Desc No Extras", myReader, "DATOSPrincipal");
                CantiDescu = myReader.Tables["DATOSPrincipal"].Rows.Count;
                myReader.Tables.Add("DATOSSubConsulta");
                progreso.ValorMinimoMaximo(0, CantiDescu);
                progreso.Show();
                ArrayList Aplicados = new ArrayList();

                for (i = 0; i <= CantiDescu - 1; i++)
                {
                    DataRow row = myReader.Tables["DATOSPrincipal"].Rows[i];
                    Application.DoEvents();
                    progreso.PerformStep();

                    if (Aplicados.Contains(row["codigoter"].ToString() + row["lincred"].ToString() + row["numero"].ToString()) == false)
                    {
                        Mysql = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                  "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras) ";
                        mysql1 = " select a.EmpDsto," + AgenciaHv + "," + CencostoHv + ",'" +
                         CicloAAAAPP + "' as periodo,a.percidad,'" +
                          Adicional + "' as adicional, a.codigoter,a.lincred,a.numero,'" +
                         Detalle + "' as detalle,'" +
                         CicloMes + "' as ciclo, " + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                          " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                          " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                          " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end as causa_inte, " +
                         " rtrim('0') as causa_extr,sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu) as causa_segu," +
                         " sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro" +
                         " from cop_cuopen_vw a " +
                         "left join cop_maecar b on a.codigoter = b.codigoter  and a.lincred = b.lincred " +
                         " and a.numero = b.numero " +
                         " inner join cop_concar12 c on c.lincred = a.lincred " +
                         " where a.codigoter ='" +
                         row["codigoter"].ToString() + "' and " + stwhere + WhereHastaCiclo + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                         "' and a.numero='" + row["numero"] +
                         "' and a.clades = '1' " +
                         " and (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA " +
                         " +CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 " +
                         "group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                        this.OdbcConnect.ExecuteQueryconec(Mysql + mysql1, ConectSub, "NominaDescTodos");
                    }

                    ok = true;
                    Aplicados.Add(row["codigoter"].ToString() + row["lincred"].ToString() + row["numero"].ToString());

                    if (cobracodeudor == "Y")
                    {
                        if (Atrasados == true)
                        {
                            for (int j = 1; j <= 4; j++)
                            {
                                string CodCodeudor;
                                MysqlCodeu = "";
                                MysqlCodeu1 = "";
                                CodCodeudor = Strings.Right("00000000000000" + row["codeudor" + j].ToString().Trim(), 14);
                                if (CodCodeudor != "00000000000000" && CodCodeudor != row["codigoter"].ToString())
                                {
                                    Codeudores.Add(CodCodeudor);
                                    MysqlCodeu = "insert into cop_nomdes (empresa,agencia,cencosto,periodo,periodicidad,adicional,codigoter,lincred,numero,detalle," +
                                                                          "ciclo,vlr_aportes,vlr_prestamos,vlr_interes,vlr_extras,vlr_mora,vlr_seguro,vlr_admon,vlr_otras,Codeuda) ";
                                    MysqlCodeu1 = "select a.EmpDsto,a.agencia,a.cencosto,'" +
                                    CicloAAAAPP + "' as periodo,'" + Periodicidad + "' as percidad,'" +
                                    Adicional + "' as adicional,'" +
                                    CodCodeudor + "' as codigoter,a.lincred,a.numero,'" +
                                    Detalle + "' as detalle,'" +
                                    CicloMes + "' as ciclo," + (string.Compare(row["lincred"].ToString(), "1000") >= 0 ?
                                    " rtrim('0') as vlr_aportes, sum(a.causa_capi) as causa_capi," :
                                    " sum(a.causa_capi) as vlr_aportes, rtrim('0') as causa_capi,") +
                                    " case c.PREVIV when 'N' then sum(a.causa_inte) else 0 end  as causa_inte, rtrim('0') as causa_extr," +
                                    " sum(a.intmor_causa) as vlr_mora,sum(a.causa_segu)as causa_segu," +
                                    "sum(a.causa_admi)as causa_admi,sum(a.causa_otro) as causa_otro,a.codigoter as Codeuda " +
                                    " from cop_cuopen_vw a " +
                                    "  inner join cop_concar12 c on c.lincred = a.lincred " +
                                    " where a.clades = '1' and " +
                                    " a.codigoter ='" +
                                    row["codigoter"].ToString() + "' and " + stwhere + " and  a.percidad ='" + Periodicidad + "' and a.lincred ='" + row["lincred"] +
                                    "' and a.numero='" + row["numero"] + "' and and (CAUSA_CAPI + CAUSA_INTE+ INTMOR_CAUSA " +
                                    " + CAUSA_SEGU+ CAUSA_ADMI+ CAUSA_OTRO ) <> 0 and a.periodo_causa < " + pdadesdec +
                                    " group by a.EmpDsto,a.agencia,a.cencosto, a.codigoter, a.percidad, a.lincred,a.numero,c.PREVIV";
                                    this.OdbcConnect.ExecuteQueryconec(MysqlCodeu + MysqlCodeu1, ConectSub, "NominaDescTodosGrabacodeuda");
                                }
                            }
                        }
                    }
                }
                myReader.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error conexion BD:" + varini.pstBdatos + " Descripcion:" + ex.Message);
            }
            if (progreso != null) { progreso.Close(); }
            if (ok == false) { MessageBox.Show("No se encontraron datos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            else { if (i != CantiDescu) { MessageBox.Show("El Proceso Termino Pero no aplico todos los Descuentos .", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation); } else { MessageBox.Show("El Proceso Termino Correctamente.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information); } }
            return ok;
        }

        // Methods from VB lines 14499-17527 are in Clscartera.Part6b.cs

    } // end partial class Clscartera
} // end namespace msgcop
