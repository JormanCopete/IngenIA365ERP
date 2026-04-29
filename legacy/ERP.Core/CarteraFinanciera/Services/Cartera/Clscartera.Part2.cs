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

        // ActuaEstaAntici duplicado eliminado (original en Clscartera.cs:2099)

        public bool AplicaCptoAfavor(DateTime FechaMovto, string Periodo, string Usuario, ref string Cpte, ref double ConseCpte, Form Myforma, OdbcConnection Myconnet, string empresa = "Todos")
        {
            int primer = 1;
            string CuentaCpte = "999999999999";
            double Saldo = 0;
            double Credito = 0, Debito = 0;
            string Cptofavor = "9999";
            int sw1 = 0, Tipolinea = 0;
            string CptoMovCap = "99";
            double Dif = 0;
            ERP.Core.Compartido.Controles.Barraprogress Progress = new ERP.Core.Compartido.Controles.Barraprogress("Aplicando Saldo Afavor", Myforma);
            int canreg = 0, fila = 0;
            string FiltraEmpresa = "";
            DataSet dscompania = new DataSet();
            DataSet myRead = new DataSet();
            string ParamValidaCiclo = "Y", StestadoPeriodo = "A";
            string estado = "";

            if (empresa != "Todos")
            {
                FiltraEmpresa = " and car.empdsto='" + empresa + "'";
            }

            this.msgcofsys.BuscarCompania(varini.sptCodEmpr, dscompania, Myconnet);

            Cptofavor = dscompania.Tables["tblcompania"].Rows[0]["sobra"].ToString();
            Cpte = dscompania.Tables["tblcompania"].Rows[0]["CpteFavor"].ToString();
            ParamValidaCiclo = dscompania.Tables["tblcompania"].Rows[0]["paramcausacion"].ToString();

            stmysql = "select sal.codigoter, sal.saldo from cop_salmaecar sal inner join cop_maecar car on sal.codigoter=car.CODIGOTER and " +
                "sal.lincred=car.lincred and sal.numero=car.numero and sal.periodo = '" + Periodo + "' and car.lincred =" + Cptofavor + FiltraEmpresa;

            string _p1 = "", _p2 = "", _p3 = "", _p4 = "";
            int _tipolinea = 0;
            //ok = this.BuscaLinea(Convert.ToInt32(Cptofavor), Myconnet, ref _p1, ref _p2, ref _p3, ref _p4, ref Tipolinea);
            switch (Tipolinea)
            {
                case 3:
                    CptoMovCap = dscompania.Tables["tblcompania"].Rows[0]["cpto_servi"].ToString();
                    break;
                case 4:
                    CptoMovCap = dscompania.Tables["tblcompania"].Rows[0]["CPTO_CAPITAL"].ToString();
                    break;
                default:
                    MessageBox.Show("Revise el tipo de linea del concepto a favor incorrecto", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            switch (ParamValidaCiclo)
            {
                case "N":
                    if (Convert.ToDouble(FechaMovto.ToString("yyyyMM")) < Convert.ToDouble(DateTime.Now.ToString("yyyyMM")))
                    {
                        string _stp = "A";
                        //this.buscaPeriodo("copc", Myconnet, ref _p1, ref _p2, DateTime.Now, ref _stp, ref _p3, DateTime.Now.ToString("yyyy"));
                        StestadoPeriodo = _stp;
                        if (StestadoPeriodo != "C")
                        {
                            FechaMovto = DateTime.Now;
                        }
                    }
                    break;
                default:
                    string _est = "";
                    //msgcofsys.buscaPeriodo("copc", Myconnet, ref _p1, ref _p2, FechaMovto, ref _est);
                    estado = _est;
                    if (estado == "C")
                    {
                        string _stp2 = "A";
                        // this.buscaPeriodo("copc", Myconnet, ref _p1, ref _p2, DateTime.Now, ref _stp2, ref _p3, DateTime.Now.ToString("yyyy")); // ERROR: CS1503
                        StestadoPeriodo = _stp2;
                        if (StestadoPeriodo != "C")
                        {
                            FechaMovto = DateTime.Now;
                        }
                    }
                    break;
            }

            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnet, "AplicaCptoAfavor", ref myRead, "TblCptoFavor");
            canreg = myRead.Tables["TblCptoFavor"].Rows.Count;

            Progress.ValorMinimoMaximo(0, canreg);
            Progress.Show();

            while (fila < canreg)
            {
                Application.DoEvents();
                DataRow row = myRead.Tables["TblCptoFavor"].Rows[fila];
                sw1 = 0;

                this.BuscaSaldoObligacion(row["codigoter"].ToString(), Convert.ToInt32(Cptofavor), 0, FechaMovto.ToString("yyyyMM"), Myconnet, ref Saldo);

                if (Saldo >= 0)
                {
                    sw1 = 1;
                }

                Credito = Convert.ToDouble(row["saldo"]) * -1;

                if (Credito > (Saldo * -1))
                {
                    Credito = (Saldo * -1);
                }

                if (sw1 == 0)
                {
                    if (primer == 1)
                    {
                        //this.BuscaComprobante(ref Cpte, ref ConseCpte, true, Myconnet, ref _p1, ref _p2, ref _p3, ref _p4);
                        // CuentaCpte retrieved from overload
                    }
                    primer = 0;

                    this.GrabaMovimiento(Cpte, ConseCpte, row["codigoter"].ToString(), 9999, Convert.ToDouble("99999999"), Convert.ToInt32(FechaMovto.ToString("yyyyMM")), "99", FechaMovto, 0, ref Credito, "Aplicacion de saldo a favor -- Proceso automatico " + DateTime.Now.ToString("ddMMyyhhmmss") + " !!", Usuario, Myconnet, 999999, " ", " ", "", row["codigoter"].ToString());
                    Debito = (Convert.ToDouble(row["saldo"]) * -1) - Credito;
                    this.GrabaMovimiento(Cpte, ConseCpte, row["codigoter"].ToString(), Convert.ToInt32(Cptofavor), 0, Convert.ToInt32(FechaMovto.ToString("yyyyMM")), CptoMovCap, FechaMovto, Debito, ref Credito, "Aplicacion de saldo a favor -- Proceso automatico " + DateTime.Now.ToString("ddMMyyhhmmss") + " !!", Usuario, Myconnet, 999999, " ", " ", "", row["codigoter"].ToString());
                }
                Progress.PerformStep();
                ok = true;
                fila += 1;
            }

            if (ConseCpte > 0)
            {
                ok = false;
                double _dif = 0;
                string _c1 = " ", _c2 = " ", _c3 = "N", _c4 = "N";
                double _d1 = 0, _d2 = 0;
                this.BuscaComprobante(ref Cpte, ref ConseCpte, false, Myconnet, ref _c1, ref _c2, ref _d1, ref _d2, ref _c3, ref _c4, ref _dif);
                Dif = _dif;
                if (Dif == 0)
                {
                    ok = TrasladaContabilidad(Cpte, ConseCpte, Myconnet, varini.pstUsuario);
                }
            }
            Progress.Close();
            Progress.Dispose();
            myRead.Dispose();
            return ok;
        }

        public bool GrabaMovimiento(string Comprobante, double ConseCompro, string codigoter, int lincred, double NumCredito, int Periodo, string TipoTransacion, DateTime fechamovto, double debito, ref double credito, string Detalle, string Usuario, OdbcConnection Myconnect,
            int CICLO = 999999, string Cuenta = " ", string Nit = " ",
            string Factura = "", string IdBenef = "99999999999999", Cladesto Clades = Cladesto.Todo, int NumExtra = 0, bool RecCuotas = true,
            string TipoDocaux = "", string NumDocAux = "", string Detallefact = "", DateTime? FecVenFactNullable = null, string Banco = "9999", string Numcheque = " ",
            string PagoCodeuda = "99999999999999", int CicloNomina = 999999, string Cencosto = "99999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, string EmpDsto = "9999",
            bool DesdeApliNomina = false, double VlrBase = 0, string EsMovtoDe = "CC", bool DesdeGrabCredito = false, double IdSolCredito = 0, string TipoNomina = "", DateTime? feccausacdtasNullable = null,
            double idsolaux = 0, string AplicaExtrasNom = "Y", double secuenciadepende = 0, bool ValidaSaldoCreditos = true, PrioridadesAtomar PrioridadSoloCreditos = PrioridadesAtomar.Todos, string TrasladoCpto = " ", int LineaAhorro = 9999)
        {
            DateTime FecVenFact = FecVenFactNullable ?? new DateTime(1950, 1, 1);
            DateTime feccausacdtas = feccausacdtasNullable ?? new DateTime(1900, 1, 1);

            string Tipo_movto = "0";
            bool ok = false;
            int itemPrioridad = 0;
            string AjuCau = "N";
            bool DesdeApliNomCart = false;
            int TipoLinea = 0;
            string estado = "C";
            DialogResult MSGOK;
            string Cerrado = "N", anulado = "N";
            bool ValidaObligacion = true;
            string Marca_reliquidacion = "N";

            varini.pstUsuario = Usuario;
            // ok = this.BuscaAsociado(codigoter, Myconnect); // ERROR: CS1620

            if (ok == false)
            {
                MessageBox.Show("Asociado no existe, " + codigoter);
                return false;
            }

            switch (Clades)
            {
                case Cladesto.Caja:
                case Cladesto.Todo:
                   // this.buscaPeriodo("copc", Myconnect, fechamovto, ref estado, fechamovto.ToString("yyyy"));
                    break;
                case Cladesto.Nomina:
                    if (DesdeGrabCredito == false)
                    {
                        //this.buscaPeriodo("copn", Myconnect, fechamovto, ref estado, fechamovto.ToString("yyyy"));
                        DesdeApliNomCart = true;
                    }
                    else
                    {
                        //this.buscaPeriodo("copc", Myconnect, fechamovto, ref estado, fechamovto.ToString("yyyy"));
                    }
                    break;
            }

            switch (estado)
            {
                case "P":
                    MSGOK = MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    if (MSGOK == DialogResult.No)
                    {
                        return false;
                    }
                    break;
                case "C":
                    MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            //ok = this.BuscaComprobante(ref Comprobante, ref ConseCompro, false, Myconnect, Cerrado: ref Cerrado, ANULADO: ref anulado);

            if (Cerrado == "Y")
            {
                MessageBox.Show("Documento cerrado no permite ingresar movimiento", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (anulado == "Y")
            {
                MessageBox.Show("Documento anulado no permite ingresar movimiento", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (TipoNomina.Trim() == "")
            {
                //msgcofsys.BuscarCompania(varini.sptCodEmpr, Myconnect, TipoNomina: ref TipoNomina);
            }

            if (codigoter == "99999999999999" && lincred.ToString() == "9999")
            {
                GrabaContrapartida(Comprobante, ConseCompro, codigoter, lincred.ToString(), Cuenta, Nit, TipoTransacion, fechamovto, debito, credito, "9999", Myconnect, Factura, Usuario, TipoDocaux, NumDocAux, Detallefact, FecVenFact, Cencosto, VlrBase, idsolaux, secuenciadepende);
                return true;
            }

            switch (TipoTransacion)
            {
                case "99":
                    PagoAutomatico(Comprobante, ConseCompro, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, Myconnect, 0, "99999999999999", (int)Clades, Usuario, PagoCodeuda, CicloNomina, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomCart, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos, PrioridadSoloCreditos);
                    return true;
                default:
                    //ok = ValidaTipoMOvimiento(TipoTransacion, lincred, Myconnect);
                    if (ok == false)
                    {
                        return false;
                    }

                    //this.BuscaTipoMovto(TipoTransacion, Myconnect, ref Tipo_movto, AjuCau: ref AjuCau);
                    this.BuscaLinea(lincred, Myconnect, TipoLinea: ref TipoLinea);

                    if (AjuCau == "Y")
                    {
                        //GrabaAjusteCausacion(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Tipo_movto, TipoTransacion, CICLO, Periodo, fechamovto, Detalle, Myconnect, NumExtra, varini.pstUsuario);
                        return true;
                    }

                    switch (Tipo_movto)
                    {
                        case "1":
                        case "10":
                            ok = this.ValidaPrioridad(codigoter, lincred, NumCredito, Periodo, "Capital", Myconnect, ref itemPrioridad);
                            break;
                        case "2":
                        case "14":
                            ok = this.ValidaPrioridad(codigoter, lincred, NumCredito, Periodo, "Interes", Myconnect, ref itemPrioridad);
                            break;
                        case "3":
                            ok = this.ValidaPrioridad(codigoter, lincred, NumCredito, Periodo, "Mora", Myconnect, ref itemPrioridad);
                            break;
                        case "5":
                            ok = this.ValidaPrioridad(codigoter, lincred, NumCredito, Periodo, "Seguro", Myconnect, ref itemPrioridad);
                            break;
                        case "6":
                            ok = this.ValidaPrioridad(codigoter, lincred, NumCredito, Periodo, "Admon", Myconnect, ref itemPrioridad);
                            break;
                    }
                    break;
            }

            ok = this.BuscaObligacion(codigoter, lincred, NumCredito, Myconnect);
            if (ok == false)
            {
                GrabaNuevoCredito(codigoter, lincred.ToString(), NumCredito, fechamovto, fechamovto, fechamovto, fechamovto, " ", 0, debito, 0, 0, 0, 0, 5, 1, 1, 1, Usuario, fechamovto, "9999", "99999999", Periodo.ToString(), Myconnect);
            }

            switch (TipoLinea)
            {
                case 1:
                    if (RecCuotas == true)
                    {
                        this.GrabaAportes(Comprobante, ConseCompro, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, Myconnect, 0, 0, Usuario, PagoCodeuda, 999999, UsuarioSobreGiro, VlrSobreGiro, "9999", TipoNomina, secuenciadepende, TrasladoCpto);
                        if (DesdeApliNomina == true)
                        {
                            return true;
                        }
                    }
                    if (credito > 0 || debito > 0)
                    {
                        this.GrabaCapitalAportes(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                    }
                    break;
                case 2:
                    switch (Tipo_movto)
                    {
                        case "7":
                        case "13":
                            // rutina para ahorros
                            if (RecCuotas == true)
                            {
                                this.GrabaAhorros(Comprobante, ConseCompro, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, Myconnect, 0, 0, Usuario, IdBenef, PagoCodeuda, 999999, UsuarioSobreGiro, VlrSobreGiro, "9999", TipoNomina, secuenciadepende, TrasladoCpto);
                                if (DesdeApliNomina == true)
                                {
                                    return true;
                                }
                            }
                            if (credito > 0 || debito > 0)
                            {
                                this.GrabaCapitalAhorros(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, Usuario, IdBenef, Banco, Numcheque, Factura, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, EsMovtoDe, secuenciadepende, TrasladoCpto, feccausacdtas, LineaAhorro);
                            }
                            break;
                        case "8":
                            //GrabaRetencion(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, "", Factura, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, VlrBase, Usuario, TrasladoCpto);
                            break;
                        case "9":
                            GrabaGmf(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, IdBenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, TrasladoCpto);
                            break;
                    }
                    break;
                case 3:
                    if (RecCuotas == true)
                    {
                        this.GrabaServicios(Comprobante, ConseCompro, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, Myconnect, TipoTransacion, 0, Usuario, PagoCodeuda, 999999, UsuarioSobreGiro, VlrSobreGiro, "9999", "9999", TipoNomina, secuenciadepende, TrasladoCpto);
                        if (DesdeApliNomina == true)
                        {
                            return true;
                        }
                    }
                    if (credito > 0 || debito > 0)
                    {
                        switch (Tipo_movto)
                        {
                            case "1":
                                if (lincred >= 1000 && credito > 0)
                                {
                                    Marca_reliquidacion = "Y";
                                }
                                break;
                            default:
                                Marca_reliquidacion = "N";
                                break;
                        }
                        this.GrabaCapitalServicios(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, Marca_reliquidacion);
                    }
                    break;
                case 6:
                case 7:
                    switch (Tipo_movto)
                    {
                        case "8":
                            //GrabaRetencion(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, "", "", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, VlrBase, Usuario, TrasladoCpto);
                            break;
                        case "11":
                            GrabaCapitalCdats(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, Usuario, TrasladoCpto);
                            break;
                        case "12":
                            GrabaInteresesCdats(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, TipoTransacion, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, Usuario, feccausacdtas, TrasladoCpto);
                            break;
                    }
                    break;
                default:
                    if (Tipo_movto == "10")
                    {
                        if (RecCuotas == true)
                        {
                            this.GrabaCreditos(Comprobante, ConseCompro, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, Myconnect, Convert.ToInt32(Tipo_movto), NumExtra, "99999999999999", (int)Clades, Usuario, PagoCodeuda, 999999, UsuarioSobreGiro, VlrSobreGiro, EmpDsto, false, TipoNomina, DesdeApliNomCart, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos, TrasladoCpto);
                            if (DesdeApliNomina == true)
                            {
                                return true;
                            }
                        }
                        if ((credito > 0 || debito > 0) && Clades == Cladesto.Todo)
                        {
                            GrabaExtra(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 0, Detalle, Myconnect, NumExtra, TipoTransacion, "99999999999999", Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                        }
                    }
                    else
                    {
                        if (RecCuotas == true)
                        {
                            this.GrabaCreditos(Comprobante, ConseCompro, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, Myconnect, itemPrioridad, 0, "99999999999999", 0, Usuario, PagoCodeuda, 999999, UsuarioSobreGiro, VlrSobreGiro, EmpDsto, false, TipoNomina, DesdeApliNomCart, "Y", secuenciadepende, ValidaSaldoCreditos, TrasladoCpto);
                            if (DesdeApliNomina == true)
                            {
                                return true;
                            }
                        }

                        switch (Tipo_movto)
                        {
                            case "1":
                                if (credito > 0 || debito > 0)
                                {
                                    if (credito > 0)
                                    {
                                        Marca_reliquidacion = "Y";
                                    }
                                    this.GrabaCapital(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, TipoTransacion, IdBenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, IdSolCredito, secuenciadepende, TrasladoCpto, Marca_reliquidacion);
                                }
                                break;
                            case "2":
                                if (credito > 0 || debito > 0)
                                {
                                    this.GrabaInteres(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, TipoTransacion, "", Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                }
                                break;
                            case "3":
                                if (credito > 0 || debito > 0)
                                {
                                    this.GrabaMora(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, "", "", Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                }
                                break;
                            case "5":
                                if (credito > 0 || debito > 0)
                                {
                                    this.GrabaSeguro(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, "", "", Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                }
                                break;
                            case "6":
                                if (credito > 0 || debito > 0)
                                {
                                    this.GrabaAdmon(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, "", "", Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                }
                                break;
                            case "8":
                                // retfuente
                                break;
                            case "9":
                                GrabaGmf(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, TipoTransacion, IdBenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, TrasladoCpto);
                                break;
                            case "14":
                                if (credito > 0 || debito > 0)
                                {
                                    this.GrabaInteresAnticipado(Comprobante, ConseCompro, codigoter, lincred, NumCredito, debito, credito, Periodo, fechamovto, 999999, Detalle, Myconnect, TipoTransacion, "", Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                }
                                break;
                        }
                    }
                    break;
            }

            return true;
        }

        public bool GrabaAbonoTarjetaCredito(string Compronte, double ConseCpte, string codigoter, DateTime fechamovto, ref double Valorpagar, string Detalle, bool Aplnomina, string Idbenef, string PagoCodeuda, string usuario, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet Dsdatospagos = new DataSet();
            double Numreg = 0;

            stbuilder.Append("SELECT copmora.codigoter, copmora.lincred, copmora.numero, copmora.SaldoCapital , copmora.SaldoExtra ,");
            stbuilder.Append("copmora.SaldoInteres , copmora.SaldoMora,copmora.SaldoSeguro, copmora.SaldoAdmon, copmora.periodo_causa,");
            stbuilder.Append("copmora.nume_extra,para12.previv  from cop_copmora copmora inner join cop_concar12 para12 ");
            stbuilder.Append("on copmora.lincred = para12.lincred ");
            stbuilder.Append("inner join cop_salmaecar sal on sal.codigoter=copmora.codigoter and sal.lincred=copmora.lincred and sal.numero=copmora.numero and sal.periodo=copmora.periodo_contable ");
            stbuilder.Append("where copmora.codigoter = '" + codigoter + "' and para12.codahor = 5 ");
            stbuilder.Append("and copmora.Periodo_contable = " + fechamovto.ToString("yyyyMM") + " and copmora.lincred >= 1000 and sal.saldo>0 ");
            stbuilder.Append("and (copmora.SaldoCapital + copmora.SaldoExtra + copmora.SaldoInteres + copmora.SaldoMora + copmora.SaldoSeguro + copmora.SaldoAdmon) <> 0 ");
            stbuilder.Append("order BY copmora.periodo_contable,COPMORA.PERIODO_CAUSA, copmora.lincred, copmora.numero,copmora.codigoter ");

            ok = this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "AbonotarjetaCredito", ref Dsdatospagos, "tblcuotas");

            GrabaPagosCreditos(Dsdatospagos, Compronte, ConseCpte.ToString(), fechamovto, Valorpagar, Detalle, false, codigoter, usuario, PagoCodeuda, myconnect);
            return ok;
        }

        private void GrabaPagosCreditos(DataSet dsdataset, string comprobante, string ConseComprobante, DateTime FechaMovto,
            double Credito, string Detalle, bool AplNomina, string Idbenef, string Usuario, string PagoCodeuda, OdbcConnection myconnect)
        {
            double ValorPagar = 0;
            int Item = 0;
            double Numreg = 0;
            string Periodo;
            Array Prioridades;
            double SaldoInteres = 0;
            int Nroextra = 0;
            Periodo = FechaMovto.ToString("yyyyMM");
            Prioridades = BuscaPrioridad(myconnect);
            Prioridades.SetValue("Extras", 10);

            for (int numreg = 0; numreg <= dsdataset.Tables["tblcuotas"].Rows.Count - 1; numreg++)
            {
                DataRow row = dsdataset.Tables["tblcuotas"].Rows[numreg];
                for (Item = 1; Item <= Prioridades.GetUpperBound(0); Item++)
                {
                    ValorPagar = 0;
                    switch (Prioridades.GetValue(Item).ToString())
                    {
                        case "Capital":
                            if (Credito > Convert.ToDouble(row["SaldoExtra"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoExtra"]);
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if (Credito > Convert.ToDouble(row["SaldoCapital"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoCapital"]);
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if (ValorPagar < 0)
                            {
                                if (Credito > (Convert.ToDouble(row["SaldoCapital"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoCapital"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = Credito;
                                }

                                //GrabaCapital(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);

                                Credito = Credito - ValorPagar;
                                ValorPagar = 0;
                            }

                            if (ValorPagar != 0)
                            {
                             //   GrabaCapital(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);
                            }
                            break;

                        case "Extras":
                            if (row["previv"].ToString() == "Y")
                            {
                                SaldoInteres = Convert.ToDouble(row["SaldoInteres"]);
                            }
                            else
                            {
                                SaldoInteres = 0;
                            }

                            if (Credito > Convert.ToDouble(row["SaldoExtra"]) + SaldoInteres)
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoExtra"]) + SaldoInteres;
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if ((Convert.ToInt32(row["nume_extra"]) != Nroextra) && Nroextra > 0)
                            {
                                ValorPagar = 0;
                            }

                            if (ValorPagar < 0)
                            {
                                if (Credito > (Convert.ToDouble(row["SaldoExtra"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoExtra"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = Credito;
                                }

                                //GrabaExtra(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, Convert.ToInt32(row["nume_extra"]), "", Idbenef, Usuario, PagoCodeuda);

                                Credito = Credito - ValorPagar;
                                ValorPagar = 0;
                            }

                            if (AplNomina == true)
                            {
                                if (Convert.ToDouble(row["SaldoInteres"]) > 0 && row["previv"].ToString() == "Y")
                                {
                                    if (ValorPagar > 0)
                                    {
                                        //this.GrabaInteres(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, Convert.ToDouble(row["SaldoInteres"]), Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);
                                        ValorPagar -= Convert.ToDouble(row["SaldoInteres"]);
                                        Credito = Credito - Convert.ToDouble(row["SaldoInteres"]);
                                    }
                                }
                            }

                            if (ValorPagar != 0)
                            {
                                //GrabaExtra(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, Convert.ToInt32(row["nume_extra"]), "", Idbenef, Usuario, PagoCodeuda);
                            }
                            break;

                        case "Interes":
                            if (Credito > Convert.ToDouble(row["SaldoInteres"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoInteres"]);
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if (ValorPagar < 0)
                            {
                                if (Credito > (Convert.ToDouble(row["SaldoInteres"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoInteres"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = Credito;
                                }

                                //GrabaInteres(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);

                                Credito = Credito - ValorPagar;
                                ValorPagar = 0;
                            }

                            if (ValorPagar != 0)
                            {
                                //GrabaInteres(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);
                            }
                            break;

                        case "Mora":
                            if (Credito > Convert.ToDouble(row["SaldoMora"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoMora"]);
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if (ValorPagar < 0)
                            {
                                if (Credito > (Convert.ToDouble(row["SaldoMora"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoMora"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = Credito;
                                }

                                //GrabaMora(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);

                                Credito = Credito - ValorPagar;
                                ValorPagar = 0;
                            }

                            if (ValorPagar != 0)
                            {
                                //GrabaMora(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);
                            }
                            break;

                        case "Admon":
                            if (Credito > Convert.ToDouble(row["SaldoAdmon"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoAdmon"]);
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if (ValorPagar < 0)
                            {
                                if (Credito > (Convert.ToDouble(row["SaldoAdmon"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoAdmon"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = Credito;
                                }

                                //GrabaAdmon(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);

                                Credito = Credito - ValorPagar;
                                ValorPagar = 0;
                            }

                            if (ValorPagar != 0)
                            {
                               // GrabaAdmon(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);
                            }
                            break;

                        case "Seguro":
                            if (Credito > Convert.ToDouble(row["SaldoSeguro"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoSeguro"]);
                            }
                            else
                            {
                                ValorPagar = Credito;
                            }

                            if (ValorPagar < 0)
                            {
                                if (Credito > (Convert.ToDouble(row["SaldoSeguro"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoSeguro"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = Credito;
                                }

                                //GrabaSeguro(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);

                                Credito = Credito - ValorPagar;
                                ValorPagar = 0;
                            }

                            if (ValorPagar != 0)
                            {
                                //GrabaSeguro(comprobante, Convert.ToDouble(ConseComprobante), row["codigoter"].ToString(), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), Detalle, myconnect, "", Idbenef, Usuario, PagoCodeuda);
                            }
                            break;
                    }
                    Credito = Credito - ValorPagar;
                    if (Credito <= 0)
                    {
                        break;
                    }
                }
            }
        }

        private void PagoLineaCreditos(string Compronte, double ConseCpte, string Codigoter, string Lincred, string Periodo, ref double Valorpagar, DateTime Fechamovto, string usuario, string detalle, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            int canreg = 0, fila = 0;
            string TipoNomina = "0";

            stbuilder.Append("select copmae.codigoter, copmae.lincred, copmae.numero from cop_maecar copmae inner join cop_salmaecar salmae on copmae.codigoter= salmae.codigoter and ");
            stbuilder.Append("copmae.lincred = salmae.lincred And copmae.numero = salmae.numero And salmae.periodo = '" + Periodo + "'");
            stbuilder.Append("where copmae.codigoter = '" + Codigoter + "' AND COPMAE.LINCRED = '" + Lincred + "'");

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "PagoLineaCreditos", ref myRead, "TblPagoLinCred");
            canreg = myRead.Tables["TblPagoLinCred"].Rows.Count;

            //msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, TipoNomina: ref TipoNomina);

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblPagoLinCred"].Rows[fila];
                this.PagoAutomatico(Compronte, ConseCpte, Codigoter, Convert.ToInt32(Lincred), Convert.ToDouble(row["numero"]), Convert.ToInt32(Periodo), Fechamovto, 0, ref Valorpagar, detalle, myconnect, 0, Codigoter, 0, null, "99999999999999", 999999, "9999", false, TipoNomina);
                fila += 1;
            }
            myRead.Dispose();
        }

        private void GrabaContrapartida(string Comprobante, double ConseCompro, string codigoter, string lincred, string Cuenta, string NIT, string codmovto, DateTime fechaMovto, double debito, double credito, string agencia, OdbcConnection myconnect,
            string factura = "", string Usuario = null, string TipoDocAux = null, string NumDocAux = null, string DeatalleFact = null, DateTime? FecVencefactNullable = null, string Cencosto = "99999999", double VlrBase = 0,
            double idsolaux = 0, double secuenciadepende = 0)
        {
            DateTime FecVencefact = FecVencefactNullable ?? new DateTime(1950, 1, 1);
            string stmysql;
            string cencos = "99999999";
            string detalle = " ";
            if (Cencosto == "99999999")
            {
                stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + lincred;
                this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref cencos);
            }
            else
            {
                cencos = Cencosto;
            }

            //this.BsucarCompania(varini.sptCodEmpr, myconnect, codmovto: ref codmovto);
            string _det = " ";
            this.BuscaComprobante(ref Comprobante, ref ConseCompro, false, myconnect, Detalle: ref _det);
            detalle = _det;

            this.InsertMovto(Comprobante, ConseCompro, codigoter, Convert.ToInt32(lincred), 0, Cuenta, fechaMovto, debito, credito, cencos, 0, codmovto, "999999", Usuario, agencia, Cuenta, 0, " ", "", TipoDocAux, NumDocAux, 0, 0, cencos, " ", VlrBase, NIT, 0, detalle, myconnect, factura, "99999999999999", DeatalleFact, FecVencefact, " ", "99999999999999", " ", 0, "CC", 0, new DateTime(1900, 1, 1), idsolaux, secuenciadepende);
        }

        private void InsertMovto(string compronte, double ConseCompro, string codigoter,
            int lincred, double NumCredito, string cuenta,
            DateTime fecmovto, double debito, double credito, string cencos,
            double tasai, string cod_movto, string ciclos, string user, string stAgencia,
            string cuenta2, double vlr_dsctos, string bus, string codigo_banco,
            string domto_cruce, string num_doc_cruce, double vlr_che_local, double vlr_che_otras,
            string centroco, string anticipo, double base_reten, string nit, int NRO_EXTRA, string Detalle, OdbcConnection appconnect,
            string factura = "", string IdBenef = "99999999999999", string DetalleMovto = null, DateTime? FecVenceFactNullable = null,
            string NumCheque = " ", string CodCodeudor = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0,
            string EsMovtoDe = "CC", double IdSolCredito = 0, DateTime? feccausacdtasNullable = null, double idsolaux = 0, double secuenciadepende = 0, string Marca_reliquidacion = "N", int LineaAhorro = 9999)
        {
            DateTime FecVenceFact = FecVenceFactNullable ?? new DateTime(1950, 1, 1);
            DateTime feccausacdtas = feccausacdtasNullable ?? new DateTime(1900, 1, 1);

            string mysql;
            string Nomusu = "admin";
            string AplCencos = "N", Cencosto = "99999999", CencCpte = "99999999";
            DataSet dsasocia = new DataSet();

            if (CodCodeudor != "99999999999999" && CodCodeudor != null)
            {
                IdBenef = CodCodeudor;
            }

            if (IdBenef == "99999999999999")
            {
                IdBenef = codigoter;
            }

            if (num_doc_cruce == null)
            {
                num_doc_cruce = "0";
            }

            if (num_doc_cruce.Trim() == "")
            {
                num_doc_cruce = "0";
            }

            string _aplCencos = AplCencos, _cencosto = Cencosto;
            //msgcnt.BuscarCuenta(cuenta, appconnect, ref _aplCencos, ref _cencosto);
            AplCencos = _aplCencos;
            Cencosto = _cencosto;

            if (AplCencos == "Y")
            {
                if (cencos == "99999999")
                {
                    if (Cencosto != "99999999")
                    {
                        cencos = Cencosto;
                    }
                    else
                    {
                        double _cc = ConseCompro;
                        string _cpte = compronte;
                        string _cenCpte = CencCpte;
                        //msgcnt.BuscaComprobante(_cpte, _cc, false, appconnect, CentroCosto: ref _cenCpte);
                        CencCpte = _cenCpte;
                        if (CencCpte != "99999999")
                        {
                            cencos = CencCpte;
                        }
                    }
                }

                if (codigoter != "99999999999999")
                {
                    ok = this.msgconfig.BuscaAsociado(ref codigoter, ref dsasocia, appconnect);
                    if (ok == true)
                    {
                        if (dsasocia.Tables["tblasociados"].Rows[0]["cenutilidad"].ToString() != "99999999")
                        {
                            cencos = dsasocia.Tables["tblasociados"].Rows[0]["cenutilidad"].ToString();
                        }
                    }
                }
            }
            else
            {
                cencos = "99999999";
            }

            GrabaDocumento(compronte, ConseCompro, debito, credito, fecmovto, Detalle, appconnect, IdBenef);
            //msgcofsys.BuscaUsuario(user, appconnect, global::ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref Nomusu);

            if (DetalleMovto == null)
            {
                DetalleMovto = Detalle;
            }

            mysql = "insert into cop_movimto (COMPRONTE ,NUMERO_DOMTO ,CODIGOTER ,LINCRED ,"
                + "NUMERO, CUENTA,FECHA_MOVTO ,VLR_DEBITO,VLR_CREDITO ,CENCOS ,"
                + "TASA_INT, COD_MOVTO, CICLOS, "
                + "FECHA_SYSTEMA,USUARIO,AGENCIA,CUENTA2,VLR_DSCTOS,BUS,CODIGO_BANCO,DOMTO_CRUCE,"
                + "NUM_DOC_CRUCE,VLR_CHE_LOCAL,VLR_CHE_OTRAS,CENTROCO,ANTICIPO,BASE_RETEN,NRO_EXTRA,NIT, "
                + " factura,NomUsu,detalle,FecVence,NumCheque,periodo,CodCodeudor,UsuarioSobreGiro,VlrSobreGiro,EsMovtoDe,idsolcre,feccausacdtas,idsolaux,secuenciadepende,Marca_reliquidacion,LineaAhorro) values ('"
                + compronte + "','" + ConseCompro + "','"
                + codigoter + "','" + lincred + "','"
                + NumCredito + "','" + cuenta + "','"
                + fecmovto.ToString(varini.PstForFec) + "','"
                + debito + "','" + credito + "','" + cencos + "','" + tasai + "','"
                + cod_movto + "','" + ciclos + "','" + DateTime.Now.ToString(varini.pstForfecyHora) + "','"
                + user + "','" + stAgencia + "','" + cuenta2 + "','" + vlr_dsctos + "','" + bus + "','"
                + codigo_banco + "','" + domto_cruce + "','" + num_doc_cruce + "','" + vlr_che_local + "','"
                + vlr_che_otras + "','" + centroco + "','" + anticipo + "','" + base_reten + "','" + NRO_EXTRA + "','"
                + nit + "','" + factura + "','" + Nomusu + "','" + DetalleMovto + "','" + FecVenceFact.ToString(varini.PstForFec)
                + "','" + NumCheque + "','" + fecmovto.ToString("yyyyMM") + "' , '" + CodCodeudor + "','" + UsuarioSobreGiro
                + "'," + VlrSobreGiro + ",'" + EsMovtoDe + "','" + IdSolCredito + "','" + feccausacdtas.ToString(varini.PstForFec) + "','" + idsolaux + "'," + secuenciadepende + ",'" + Marca_reliquidacion + "'," + LineaAhorro + ")";

            this.OdbcConnect.ExecuteQueryconec(mysql, appconnect, "InsertMovto");
        }

        public void GrabaDocumento(string compronte, double ConseCompro, double Debito, double credito, DateTime FechaMovto, string detalle, OdbcConnection Myconect, string IdBenef = "99999999999999", bool ActualizaIdBenef = false)
        {
            bool ok;
            string stmysql;
            string DOMTO = "NC";
            stmysql = "select documento as campo1 from sys_compro02 where codigo =  '" + compronte + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "GrabaDocumento", ref DOMTO);

            ok = BuscaComprobante(ref compronte, ref ConseCompro, false, Myconect);
            if (ok == true)
            {
                if (ActualizaIdBenef == false)
                {
                    stmysql = "update cop_docmto set debito = debito + " + Debito + ", credito = credito + " + credito
                             + ",DETALLE = '" + detalle + "' where COMPRONTE ='" + compronte + "' and  numero_domto = '" + ConseCompro + "'";
                }
                else
                {
                    stmysql = "update cop_docmto set IdBenef='" + IdBenef + "' where COMPRONTE ='" + compronte + "' and  numero_domto = '" + ConseCompro + "'";
                }
                this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "GrabaDocumento");
            }
            else
            {
                stmysql = "insert into cop_docmto ( COMPRONTE, NUMERO_DOMTO ,DOMTO ,DETALLE,DEBITO ,CREDITO , DOCTIPO,NODOC ,FECHA,CERRADO, IdBenef ) values ('"
                          + Strings.Right("0000" + compronte.Trim(), 4) + "','" + ConseCompro + "','"
                          + DOMTO.Trim() + "','" + detalle + "','" + Debito + "','" + credito + "','" + DOMTO + "','" + ConseCompro.ToString().Trim() + "','"
                          + FechaMovto.ToString(varini.PstForFec) + "','N','" + IdBenef + "')";
                this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "GrabaDocumento");
            }
        }

        public void GrabaDatosDocumento(string compronte, double ConseCompro, OdbcConnection Myconect,
            string banco, string NumCheque, string BenefCheque)
        {
            ok = BuscaComprobante(ref compronte, ref ConseCompro, false, Myconect);
            if (ok == true)
            {
                stmysql = "update cop_docmto set banco = '" + banco + "', CHEQUE = '" + NumCheque
                        + "', IdBenefCheque = '" + BenefCheque + "' where COMPRONTE ='" + compronte + "' and  numero_domto = '" + ConseCompro + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "GrabaDatosDocumento");
            }
        }

        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string ControlConse, ref string TipoDoc,
            ref double debitos, ref double creditos, ref string CERRADO, ref string ANULADO, ref double Diferencia, ref string Detalle,
            ref string Doctipo, ref string NODOC, ref DateTime FechaMovto, ref string ActContab, ref string Nombre,
            ref string cuenta, ref string IdBenef, ref string AFECTA_3XMIL, ref string validadora,
            ref int NumRegistros, ref string CentroCosto, ref string RESTRI_TESORERIA, ref string modulo, ref string devolvermora, ref string lavadoactivos)
        {
            string stmysql;
            bool ok;
            int Num_consecu = 0;
            Comprobante = Strings.Right("0000" + Comprobante, 4);
            if (Consecutivo == 0)
            {
                stmysql = "select control_consec as campo1, num_consecu as campo2, DOCUMENTO as campo3, ACTUALIZA_CONTA as campo4 from sys_compro02 where codigo =  '" + Comprobante + "'";
                string _numConsecu = Num_consecu.ToString();
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref ControlConse, ref _numConsecu, ref TipoDoc, ref ActContab);
                int.TryParse(_numConsecu, out Num_consecu);

                stmysql = "select NOMBRE as campo1,CUENTA_CONTABLE as campo2, cencosto as campo3, RESTRI_TESORERIA as campo4 from sys_compro02 where codigo =  '" + Comprobante + "'";
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref Nombre, ref cuenta, ref CentroCosto, ref RESTRI_TESORERIA);

                stmysql = "select AFECTA_3XMIL as campo1, modulo as campo2, devolvermora as campo3,lava_activos as campo4 from sys_compro02 where codigo =  '" + Comprobante + "'";
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref AFECTA_3XMIL, ref modulo, ref devolvermora, ref lavadoactivos);

                Num_consecu = Num_consecu + 1;
                if (ActualizaConse == true)
                {
                    stmysql = "Update sys_compro02 set num_consecu = " + Num_consecu + " where codigo = '" + Comprobante + "'";
                    this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante");
                }
                Consecutivo = Num_consecu;
                return ok;
            }
            else
            {
                stmysql = "select control_consec as campo1, num_consecu as campo2, DOCUMENTO as campo3, ACTUALIZA_CONTA as campo4 from sys_compro02 where codigo =  '" + Comprobante + "'";
                string _numConsecu2 = Num_consecu.ToString();
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref ControlConse, ref _numConsecu2, ref TipoDoc, ref ActContab);
                int.TryParse(_numConsecu2, out Num_consecu);

                stmysql = "select AFECTA_3XMIL as campo1 ,validadora as campo2, devolvermora as campo3,lava_activos as campo4 from sys_compro02 where codigo =  '" + Comprobante + "'";
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref AFECTA_3XMIL, ref validadora, ref devolvermora, ref lavadoactivos);

                stmysql = "select NOMBRE as campo1, CUENTA_CONTABLE as campo2, cencosto as campo3 from sys_compro02 where codigo =  '" + Comprobante + "'";
                //ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref Nombre, ref cuenta, ref CentroCosto);

                // Codigo nuevo 25/9/8
                stmysql = "select RESTRI_TESORERIA as campo1, modulo as campo2 from sys_compro02 where codigo = '" + Comprobante + "'";
                this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref RESTRI_TESORERIA, ref modulo);

                string _deb = debitos.ToString(), _cre = creditos.ToString();
                stmysql = "select compronte, debito as campo1, credito as campo2,CERRADO as campo3,ANULADO as campo4  from cop_docmto where compronte = '" + Comprobante + "' and NUMERO_DOMTO = " + Consecutivo;
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref _deb, ref _cre, ref CERRADO, ref ANULADO);
                double.TryParse(_deb, out debitos);
                double.TryParse(_cre, out creditos);

                string _fecStr = FechaMovto.ToString();
                stmysql = "select detalle as campo1, doctipo as campo2, nodoc as campo3,fecha as campo4 from cop_docmto where compronte = '" + Comprobante + "' and NUMERO_DOMTO = " + Consecutivo;
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref Detalle, ref Doctipo, ref NODOC, ref _fecStr);
                DateTime.TryParse(_fecStr, out FechaMovto);

                stmysql = "select Idbenef as campo1 from cop_docmto where compronte = '" + Comprobante + "' and NUMERO_DOMTO = " + Consecutivo;
                ok = this.OdbcConnect.ExecuteQueryconec(stmysql, Myconect, "BuscaComprobante", ref IdBenef);

                Diferencia = creditos - debitos;
                return ok;
            }
        }

        // Overload with fewer params (used when not all ref params needed)
        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect)
        {
            string ControlConse = " ", TipoDoc = " ", Detalle = " ", Doctipo = "NC", NODOC = " ", ActContab = "1", Nombre = " ";
            string cuenta = "999999999999", IdBenef = "99999999999999", AFECTA_3XMIL = "N", validadora = "0";
            string CentroCosto = "99999999", RESTRI_TESORERIA = "N", modulo = "", devolvermora = "N", lavadoactivos = "N";
            string CERRADO = "N", ANULADO = "N";
            double debitos = 0, creditos = 0, Diferencia = 0;
            int NumRegistros = 0;
            DateTime FechaMovto = new DateTime(1950, 1, 1);
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref ControlConse, ref TipoDoc, ref debitos, ref creditos, ref CERRADO, ref ANULADO, ref Diferencia, ref Detalle,
                ref Doctipo, ref NODOC, ref FechaMovto, ref ActContab, ref Nombre,
                ref cuenta, ref IdBenef, ref AFECTA_3XMIL, ref validadora,
                ref NumRegistros, ref CentroCosto, ref RESTRI_TESORERIA, ref modulo, ref devolvermora, ref lavadoactivos);
        }

        // Overload with Cerrado/Anulado ref params
        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string CERRADO, ref string ANULADO)
        {
            string ControlConse = " ", TipoDoc = " ", Detalle = " ", Doctipo = "NC", NODOC = " ", ActContab = "1", Nombre = " ";
            string cuenta = "999999999999", IdBenef = "99999999999999", AFECTA_3XMIL = "N", validadora = "0";
            string CentroCosto = "99999999", RESTRI_TESORERIA = "N", modulo = "", devolvermora = "N", lavadoactivos = "N";
            double debitos = 0, creditos = 0, Diferencia = 0;
            int NumRegistros = 0;
            DateTime FechaMovto = new DateTime(1950, 1, 1);
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref ControlConse, ref TipoDoc, ref debitos, ref creditos, ref CERRADO, ref ANULADO, ref Diferencia, ref Detalle,
                ref Doctipo, ref NODOC, ref FechaMovto, ref ActContab, ref Nombre,
                ref cuenta, ref IdBenef, ref AFECTA_3XMIL, ref validadora,
                ref NumRegistros, ref CentroCosto, ref RESTRI_TESORERIA, ref modulo, ref devolvermora, ref lavadoactivos);
        }

        // Overload with Diferencia ref param
        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string _p1, ref string _p2, ref double _d1, ref double _d2, ref string _p3, ref string _p4, ref double Diferencia)
        {
            string Detalle = " ", Doctipo = "NC", NODOC = " ", ActContab = "1", Nombre = " ";
            string cuenta = "999999999999", IdBenef = "99999999999999", AFECTA_3XMIL = "N", validadora = "0";
            string CentroCosto = "99999999", RESTRI_TESORERIA = "N", modulo = "", devolvermora = "N", lavadoactivos = "N";
            int NumRegistros = 0;
            DateTime FechaMovto = new DateTime(1950, 1, 1);
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref _p1, ref _p2, ref _d1, ref _d2, ref _p3, ref _p4, ref Diferencia, ref Detalle,
                ref Doctipo, ref NODOC, ref FechaMovto, ref ActContab, ref Nombre,
                ref cuenta, ref IdBenef, ref AFECTA_3XMIL, ref validadora,
                ref NumRegistros, ref CentroCosto, ref RESTRI_TESORERIA, ref modulo, ref devolvermora, ref lavadoactivos);
        }

        // Overload with Detalle ref param
        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string Detalle)
        {
            string ControlConse = " ", TipoDoc = " ", CERRADO = "N", ANULADO = "N", Doctipo = "NC", NODOC = " ", ActContab = "1", Nombre = " ";
            string cuenta = "999999999999", IdBenef = "99999999999999", AFECTA_3XMIL = "N", validadora = "0";
            string CentroCosto = "99999999", RESTRI_TESORERIA = "N", modulo = "", devolvermora = "N", lavadoactivos = "N";
            double debitos = 0, creditos = 0, Diferencia = 0;
            int NumRegistros = 0;
            DateTime FechaMovto = new DateTime(1950, 1, 1);
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref ControlConse, ref TipoDoc, ref debitos, ref creditos, ref CERRADO, ref ANULADO, ref Diferencia, ref Detalle,
                ref Doctipo, ref NODOC, ref FechaMovto, ref ActContab, ref Nombre,
                ref cuenta, ref IdBenef, ref AFECTA_3XMIL, ref validadora,
                ref NumRegistros, ref CentroCosto, ref RESTRI_TESORERIA, ref modulo, ref devolvermora, ref lavadoactivos);
        }

        // Overload with CuentaCpte (cuenta at position 18)
        public bool BuscaComprobante(ref string Comprobante, ref double Consecutivo, bool ActualizaConse, OdbcConnection Myconect,
            ref string _p1, ref string _p2, ref string _p3, ref string _p4, ref string _p5, ref string _p6, ref string _p7, ref string _p8,
            ref string _p9, ref string _p10, ref DateTime _fec, ref string _p11, ref string _p12,
            ref string CuentaCpte)
        {
            string AFECTA_3XMIL = "N", validadora = "0";
            string CentroCosto = "99999999", RESTRI_TESORERIA = "N", modulo = "", devolvermora = "N", lavadoactivos = "N";
            double debitos = 0, creditos = 0, Diferencia = 0;
            int NumRegistros = 0;
            return BuscaComprobante(ref Comprobante, ref Consecutivo, ActualizaConse, Myconect,
                ref _p1, ref _p2, ref debitos, ref creditos, ref _p5, ref _p6, ref Diferencia, ref _p8,
                ref _p9, ref _p10, ref _fec, ref _p11, ref _p12,
                ref CuentaCpte, ref _p3, ref AFECTA_3XMIL, ref validadora,
                ref NumRegistros, ref CentroCosto, ref RESTRI_TESORERIA, ref modulo, ref devolvermora, ref lavadoactivos);
        }

        public void PagoAutomatico(string Comprobante, double ConseComprobante, string codigoter, int lincred, double NumCredito, int Periodo,
            DateTime fechamovto, double debito, ref double credito, string Detalle, OdbcConnection appconnect, int ItemPrioridad = 0,
            string Idbenef = "99999999999999", int Clades = 0, string Usuario = null,
            string PagoCodeuda = "99999999999999", int CicloNomina = 999999, string EmpDsto = "9999", bool DesdeGrabCredito = false,
            string TipoNomina = "", bool DesdeApliNomina = false, string AplicaExtrasNom = "Y", double secuenciadepende = 0,
            bool ValidaSaldoCreditos = true, PrioridadesAtomar PrioridadSoloCreditos = PrioridadesAtomar.Todos)
        {
            Array Prioridades;
            int item;
            DataSet DsPerApl = new DataSet();
            StringBuilder stbuilder = new StringBuilder();
            int fila = 0;
            string CicloACobrar;
            string FormaAplicarCaja = "1";

            Prioridades = BuscaPrioridad(appconnect, PrioridadSoloCreditos);
            if (CicloNomina == 999999)
            {
                stbuilder.Append("select distinct(copmora.periodo_causa) as Ciclo from cop_copmora copmora ");
                stbuilder.Append("where copmora.periodo_contable = " + Periodo);
                stbuilder.Append(" and copmora.codigoter = '" + codigoter + "' ");
                stbuilder.Append(" and (copmora.SaldoCapital <> 0 or copmora.SaldoExtra <> 0 or copmora.SaldoInteres <> 0 or copmora.SaldoMora <> 0 or copmora.SaldoSeguro <> 0 or copmora.SaldoAdmon <> 0 or copmora.Saldootros <> 0) ");
                stbuilder.Append(" order by copmora.periodo_causa");
                this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), appconnect, "PagoAutomatico", ref DsPerApl, "Tblciclos");
            }
            else
            {
                if (TipoNomina.Trim() == "")
                {
                    //msgcofsys.BuscarCompania(varini.sptCodEmpr, appconnect, TipoNomina: ref TipoNomina);
                }

                if (TipoNomina != "3")
                {
                    CicloACobrar = " and copmora.periodo_causa <= " + CicloNomina + " ";
                }
                else
                {
                    CicloACobrar = " and copmora.periodo_causa = " + CicloNomina + " ";
                }
                stbuilder.Append("select distinct(copmora.periodo_causa) as Ciclo from cop_copmora copmora ");
                stbuilder.Append("where copmora.periodo_contable = " + Periodo + CicloACobrar);
                stbuilder.Append(" and copmora.codigoter = '" + codigoter + "' ");
                stbuilder.Append(" and (copmora.SaldoCapital <> 0 or copmora.SaldoExtra <> 0 or copmora.SaldoInteres <> 0 or copmora.SaldoMora <> 0 or copmora.SaldoSeguro <> 0 or copmora.SaldoAdmon <> 0 or copmora.Saldootros <> 0) ");
                stbuilder.Append(" order by copmora.periodo_causa");
                this.OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), appconnect, "PagoAutomatico", ref DsPerApl, "Tblciclos");
            }

            fila = 0;

            if (DesdeApliNomina == true)
            {
                if (TipoNomina == "2")
                {
                    for (item = 1; item <= Prioridades.GetUpperBound(0); item++)
                    {
                        fila = 0;
                        while (fila < DsPerApl.Tables["Tblciclos"].Rows.Count)
                        {
                            DataRow row = DsPerApl.Tables["Tblciclos"].Rows[fila];
                            CicloNomina = Convert.ToInt32(row["ciclo"]);
                            ItemPrioridad = item;
                            switch (Prioridades.GetValue(item).ToString())
                            {
                                case "Capital":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Interes":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Servicio":
                                    if (credito > 0)
                                    {
                                        ItemPrioridad = 0;
                                        GrabaServicios(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad.ToString(), Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito.ToString(), TipoNomina, secuenciadepende);
                                    }
                                    break;
                                case "Mora":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Admon":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Seguro":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Aportes":
                                    if (credito > 0)
                                    {
                                        ItemPrioridad = 0;
                                        GrabaAportes(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    }
                                    break;
                                case "Ahorros":
                                    if (credito > 0)
                                    {
                                        ItemPrioridad = 0;
                                        GrabaAhorros(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, "99999999999999", PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    }
                                    break;
                            }
                            fila += 1;
                        }
                    }
                }
                else
                {
                    while (fila < DsPerApl.Tables["Tblciclos"].Rows.Count)
                    {
                        DataRow row = DsPerApl.Tables["Tblciclos"].Rows[fila];
                        CicloNomina = Convert.ToInt32(row["ciclo"]);
                        for (item = 1; item <= Prioridades.GetUpperBound(0); item++)
                        {
                            switch (Prioridades.GetValue(item).ToString())
                            {
                                case "Capital":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Interes":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Servicio":
                                    if (credito > 0) GrabaServicios(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad.ToString(), Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito.ToString(), TipoNomina, secuenciadepende);
                                    break;
                                case "Mora":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Admon":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Seguro":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Aportes":
                                    if (credito > 0) GrabaAportes(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    break;
                                case "Ahorros":
                                    if (credito > 0) GrabaAhorros(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, "99999999999999", PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    break;
                            }
                        }
                        fila += 1;
                    }
                }
            }
            else
            {
                //msgcofsys.BuscarCompania(varini.sptCodEmpr, appconnect, FormaAplicarCaja: ref FormaAplicarCaja);
                if (FormaAplicarCaja == "2")
                {
                    Prioridades.SetValue("Extras", 10);
                    for (item = 1; item <= Prioridades.GetUpperBound(0); item++)
                    {
                        fila = 0;
                        while (fila < DsPerApl.Tables["Tblciclos"].Rows.Count)
                        {
                            DataRow row = DsPerApl.Tables["Tblciclos"].Rows[fila];
                            CicloNomina = Convert.ToInt32(row["ciclo"]);
                            ItemPrioridad = item;
                            switch (Prioridades.GetValue(item).ToString())
                            {
                                case "Capital":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Interes":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Servicio":
                                    if (credito > 0)
                                    {
                                        ItemPrioridad = 0;
                                        GrabaServicios(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad.ToString(), Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito.ToString(), TipoNomina, secuenciadepende);
                                    }
                                    break;
                                case "Mora":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Admon":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Seguro":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                                case "Aportes":
                                    if (credito > 0)
                                    {
                                        ItemPrioridad = 0;
                                        GrabaAportes(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    }
                                    break;
                                case "Ahorros":
                                    if (credito > 0)
                                    {
                                        ItemPrioridad = 0;
                                        GrabaAhorros(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, "99999999999999", PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    }
                                    break;
                                case "Extras":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende, ValidaSaldoCreditos);
                                    break;
                            }
                            fila += 1;
                        }
                    }
                }
                else
                {
                    while (fila < DsPerApl.Tables["Tblciclos"].Rows.Count)
                    {
                        DataRow row = DsPerApl.Tables["Tblciclos"].Rows[fila];
                        CicloNomina = Convert.ToInt32(row["ciclo"]);
                        for (item = 1; item <= Prioridades.GetUpperBound(0); item++)
                        {
                            switch (Prioridades.GetValue(item).ToString())
                            {
                                case "Capital":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Interes":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Servicio":
                                    if (credito > 0) GrabaServicios(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad.ToString(), Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito.ToString(), TipoNomina, secuenciadepende);
                                    break;
                                case "Mora":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Admon":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Seguro":
                                    if (credito > 0) GrabaCreditos(Comprobante, ConseComprobante, codigoter, lincred, NumCredito, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, 0, "99999999999999", Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, DesdeGrabCredito, TipoNomina, DesdeApliNomina, AplicaExtrasNom, secuenciadepende);
                                    break;
                                case "Aportes":
                                    if (credito > 0) GrabaAportes(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    break;
                                case "Ahorros":
                                    if (credito > 0) GrabaAhorros(Comprobante, ConseComprobante, lincred, NumCredito, codigoter, Periodo, fechamovto, debito, ref credito, Detalle, appconnect, ItemPrioridad, Clades, Usuario, "99999999999999", PagoCodeuda, CicloNomina, " ", 0, EmpDsto, TipoNomina, secuenciadepende);
                                    break;
                            }
                        }
                        fila += 1;
                    }
                }
            }
        }

        public bool GrabaNuevoCredito(string codigoter, string lincred, double numecre, DateTime fecsolic, DateTime fecaprob,
           DateTime fecfact, DateTime fecdesc, string nit, int plazo, double vlrsolicitud,
           double valorob, double saldot, double cuota, double tasaint, int ciclod, int periodd,
           int clacuo, int clasei, string usuario, DateTime fecha, string agencia, string ccosto, string periodo, OdbcConnection myconnect,
           string clasegar = " ", string clades = "", double tasaadm = 0, double tasaseg = 0,
           string cladoc = "", int nodoc = 0, double cargosad = 0, double vlpre_extra = 0, int pergraini = 0, string fepergraini = "",
           string pergracuo = "", int pergradia = 0, string codeudor1 = "", string codeudor2 = "", string codeudor3 = "", string codeudor4 = "",
           double cuota_adm = 0, double cuota_seg = 0, double cuota_cptl = 0, double cuota_icie = 0, double cuota_otros = 0,
           double numero_soli = 0, string cuex_inmes = "", string cuex_inant = "", string pag1cuo = "", string tippag2 = "", string fecvence = "NULL",
           string EmpDsto = "9999", string CobroJur = "", string forseg = "10", string idtransaccion = "", string TOTINTANT = "N", string NumPagare = "",
           string NumTarjeta = "", string NomTermLinea = "", bool BuscaObligacion = true, DateTime? FECCIERRENullable = null)
        {
            DateTime FECCIERRE = FECCIERRENullable ?? new DateTime(1950, 1, 1);
            string mysql, stFepegr1, stFepegr2;
            bool ok;
            ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();

            if (Information.IsDate(fepergraini))
            {
                stFepegr1 = "FEPERGRINI,";
                stFepegr2 = Strings.Format(Convert.ToDateTime(fepergraini), varini.PstForFec) + "','";
            }
            else
            {
                stFepegr1 = "";
                stFepegr2 = "";
            }

            if (Information.IsDate(fecvence) == false)
            {
                fecvence = "NULL";
            }
            else
            {
                fecvence = "'" + Convert.ToDateTime(fecvence).ToString(varini.PstForFec) + "'";
            }

            // codigo para convertir las fechas nulas en fechas validas
            // VB IIf side-effects preserved
            if (Information.IsDate(fecvence) == false) { fecvence = new DateTime(1950, 1, 1).ToString(varini.PstForFec); }
            if (Information.IsDate(fecsolic) == false) { fecsolic = Convert.ToDateTime(new DateTime(1950, 1, 1).ToString(varini.PstForFec)); }
            if (Information.IsDate(fecaprob) == false) { fecaprob = Convert.ToDateTime(new DateTime(1950, 1, 1).ToString(varini.PstForFec)); }
            if (Information.IsDate(fecfact) == false) { fecfact = Convert.ToDateTime(new DateTime(1950, 1, 1).ToString(varini.PstForFec)); }
            if (Information.IsDate(fecdesc) == false) { fecdesc = Convert.ToDateTime(new DateTime(1950, 1, 1).ToString(varini.PstForFec)); }
            if (Information.IsDate(fecha) == false) { fecha = Convert.ToDateTime(new DateTime(1950, 1, 1).ToString(varini.PstForFec)); }

            //if (paramcop.BuscaEmpresa(EmpDsto, myconnect) == false || EmpDsto == "9999")
            //{
            //    paramcop.BuscaAsociado(codigoter, myconnect, EmpDsto: ref EmpDsto);
            //}

            if (BuscaObligacion)
            {
                ok = this.BuscaObligacion(codigoter, Convert.ToInt32(lincred), numecre, myconnect);
            }
            else
            {
                ok = false;
            }

            if (ok == false)
            {
                mysql = "INSERT into COP_MAECAR ("
                    + "CODIGOTER ,LINCRED ,NUMERO ,FECSOLIC, FECAPROB ,FECFACT ,FECDESC , "
                    + "NIT ,PLAZO, VLRSOLICITUD ,VALOROB ,SALDOT ,CUOTA ,TASAINT, "
                    + "CICLOD ,PERIODD ,CLACUO ,CLASEI ,CLASEGAR, CLADES ,TASAADM,TASASEG ,"
                    + "CLADOC ,NODOC ,CARGOSAD,VLRPRE_EXTRA,PERGRAINI," + stFepegr1 + "PERGRACUO,PERGRADIA,"
                    + "USUARIO ,FECHA_GRABA ,CODEUDOR1 ,CODEUDOR2 ,"
                    + "CODEUDOR3 ,CODEUDOR4 ,AGENCIA,CCOSTO ,"
                    + "CUOTA_ADM ,CUOTA_SEG ,CUOTA_CPTL ,CUOTA_ICIE ,CUOTA_OTROS ,NUMERO_SOLI,"
                    + "CUEX_INMES,CUEX_INANT,PAG1CUO,TIPPAG2,FECVEMTO,EmpDsto,cobrojur,forseg,idtransaccion,TOTINTANT,filler1,Tarjeta,filler2,FECCIERRE) VALUES ('"
                    + codigoter + "','" + lincred + "','" + numecre + "','" + Strings.Format(fecsolic, varini.PstForFec) + "','" + Strings.Format(fecaprob, varini.PstForFec) + "','"
                    + Strings.Format(fecfact, varini.PstForFec) + "','" + Strings.Format(fecdesc, varini.PstForFec) + "','" + nit + "','" + plazo + "','" + vlrsolicitud + "','"
                    + valorob + "','" + saldot + "','" + cuota + "','" + tasaint + "','" + ciclod + "','" + periodd + "','"
                    + clacuo + "','" + clasei + "','" + clasegar + "','" + clades + "','" + tasaadm + "','" + tasaseg + "','"
                    + cladoc + "','" + nodoc + "','" + cargosad + "','" + vlpre_extra + "','" + pergraini + "','" + stFepegr2
                    + pergracuo + "','" + pergradia + "','" + usuario + "','" + Strings.Format(fecha, varini.PstForFec) + "','"
                    + codeudor1 + "','" + codeudor2 + "','" + codeudor3 + "','" + codeudor4 + "','" + agencia + "','" + ccosto + "','"
                    + cuota_adm + "','" + cuota_seg + "','" + cuota_cptl + "','" + cuota_icie + "','" + cuota_otros + "','"
                    + numero_soli + "','" + cuex_inmes + "','" + cuex_inant + "','" + pag1cuo + "','" + tippag2 + "'," + fecvence + ",'" + EmpDsto + "','" + CobroJur + "','" + forseg + "','" + idtransaccion + "','" + TOTINTANT + "','" + NumPagare + "','" + NumTarjeta + "','" + Strings.Mid(NomTermLinea, 1, 20) + "','" + Strings.Format(FECCIERRE, varini.PstForFec) + "')";
            }
            else
            {
                mysql = "UPDATE COP_MAECAR SET FECSOLIC= '" + fecsolic.ToString(varini.PstForFec) + "',FECAPROB= '" + fecaprob.ToString(varini.PstForFec) + "',"
                        + "FECFACT= '" + fecfact.ToString(varini.PstForFec) + "',FECDESC ='" + fecdesc.ToString(varini.PstForFec) + "',NIT= '" + nit + "',PLAZO = '" + plazo + "',VLRSOLICITUD = '" + vlrsolicitud + "',"
                        + "VALOROB= '" + valorob + "',SALDOT= '" + saldot + "',CUOTA= '" + cuota + "',TASAINT= '" + tasaint + "',CICLOD= '" + ciclod + "',PERIODD= '" + periodd + "',CLACUO= '" + clacuo + "',"
                        + "CLASEI= '" + clasei + "',CLASEGAR= '" + clasegar + "',CLADES= '" + clades + "',TASAADM= '" + tasaadm + "',TASASEG = '" + tasaseg + "',CLADOC= '" + cladoc + "',NODOC= '" + nodoc + "',"
                        + "CARGOSAD= '" + cargosad + "',VLRPRE_EXTRA= '" + vlpre_extra + "',PERGRAINI= '" + pergraini + "',PERGRADIA= '" + pergradia + "',"
                        + " USUARIO= '" + usuario + "',FECHA_GRABA= '" + fecha.ToString(varini.PstForFec) + "',CODEUDOR1= '" + codeudor1 + "',CODEUDOR2= '" + codeudor2 + "',CODEUDOR3= '" + codeudor3 + "',"
                        + "AGENCIA= '" + agencia + "',CCOSTO= '" + ccosto + "',CUOTA_ADM= '" + cuota_adm + "',CUOTA_SEG= '" + cuota_seg + "',CUOTA_CPTL= '" + cuota_cptl + "',CUOTA_ICIE= '" + cuota_icie + "',CUOTA_OTROS= '" + cuota_otros + "',"
                        + "NUMERO_SOLI= '" + numero_soli + "',CUEX_INMES= '" + cuex_inmes + "',CUEX_INANT= '" + cuex_inant + "',PAG1CUO= '" + pag1cuo + "',TIPPAG2= '" + tippag2 + "',FECVEMTO =" + fecvence + ",forseg='" + forseg
                        + "',idtransaccion='" + idtransaccion + "',filler2='" + Strings.Mid(NomTermLinea, 1, 20) + "' where codigoter = '" + codigoter + "' and lincred = " + lincred + " and NUMERO = " + numecre;
            }

            ok = this.OdbcConnect.ExecuteQueryconec(mysql, myconnect, "GrabaObligaciones");
            return ok;
        }

        public bool GrabaNovedad(TipoSelecion TipoSelecionVal, string Codigoter, string Periodo, string Observaciones,
            string Usuario, string capital, string interes, string extras, string cuotas_fijas, string TipoNov,
            string cuotas, MotivoNovedad MotivoNov, string periodd, DateTime FecCaduca, OdbcConnection Myconnect)
        {
            switch (TipoSelecionVal)
            {
                case Clscartera.TipoSelecion.Asociado:
                    GrabaNovedadAsociado(Codigoter, Periodo, Observaciones, Usuario, capital, interes, extras, cuotas_fijas, TipoNov, cuotas, MotivoNov, periodd, FecCaduca, Myconnect);
                    break;
            }
            return false;
        }

        private void GrabaNovedadAsociado(string Codigoter, string Periodo, string Observaciones,
            string Usuario, string capital, string interes, string extras, string cuotas_fijas, string tipo_novedad,
            string cuotas, MotivoNovedad MotivoNov, string periodd, DateTime FecCaduca, OdbcConnection Myconnect)
        {
            int canreg = 0, fila = 0;

            stmysql = "select a.lincred, a.numero from cop_maecar a left join cop_concar12 b on a.lincred = b.lincred left join "
                + "sys_maenit c on a.codigoter = c.codigoter inner join cop_salmaecar d on d.codigoter = a.codigoter and d.lincred = a.lincred "
                + " and d.numero = a.numero and d.periodo = '" + Periodo + "' where a.codigoter = '" + Codigoter + "' and d.saldo <> '0' and b.compri ='Y' ";

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, Myconnect, "GrabaNovedadAsociado", ref myRead, "TblGrabNovAsociado");
            canreg = myRead.Tables["TblGrabNovAsociado"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblGrabNovAsociado"].Rows[fila];
                IngresaNovedad(Codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["Numero"]), Periodo, Observaciones, Usuario, capital, interes, extras, cuotas_fijas, tipo_novedad, cuotas, MotivoNov, periodd, FecCaduca, Myconnect);
                fila += 1;
            }
            myRead.Dispose();
        }

        private void IngresaNovedad(string Codigoter, int lincred, double numero, string Periodo, string Observaciones,
            string Usuario, string capital, string interes, string extras, string cuotas_fijas, string tipo_novedad,
            string cuotas, MotivoNovedad MotivoNov, string periodd, DateTime FecCaduca, OdbcConnection Myconnect)
        {
            stmysql = "insert into cop_caunov (codigoter, lincred ,numero ,periodo_causa ,fecha ,observa ,"
                + "usuario,fecha_sys ,estado,capital,interes,extras,cuotas_fijas,tipo_novedad,cuotas,motivo,periodd,FecCaduca) values ('"
                + Codigoter + "','" + lincred + "','" + numero + "','" + Periodo + "','" + DateTime.Now.ToString(varini.PstForFec) + "','" + Observaciones + "','"
                + Usuario.Trim() + "','" + DateTime.Now.ToString(varini.PstForFec) + "','O','" + capital + "','" + interes + "','" + extras + "','" + cuotas_fijas + "','" + tipo_novedad + "','"
                + cuotas + "','" + (int)MotivoNov + "','" + periodd + "','" + FecCaduca + "')";
        }

        private double GrabaCreditos(string Comprobante, double ConseComprobante, string codigoter, int lincred, double NumCredito, int Periodo,
            DateTime fechamovto, double debito, ref double credito, string Detalle, OdbcConnection appconnect, int ItemPrioridad = 0,
            int Nroextra = 0, string Idbenef = "99999999999999", int Clades = 0, string Usuario = null,
            string PagoCodeuda = "99999999999999", int PeriodoCausa = 999999, string UsuarioSobreGiro = " ", double VlrSobreGiro = 0,
            string empdsto = "9999", bool DesdeGrabCredito = false, string TipoNomina = "", bool DesdeApliNomina = false, string AplicaExtrasNom = "Y",
            double secuenciadepende = 0, bool ValidaSaldoCreditos = true, string TrasladoCpto = " ")
        {
            Array Prioridades;
            string stmysql;
            int item, Itemfinal, itemInicial;
            double ValorPagar, Saldo = 0;
            string Stclades;
            int sw1;
            double SaldoInteres = 0;
            int canreg = 0, fila = 0, ExtClaDes = 0;
            double ValCapCaus = 0, ValExtCaus = 0;
            string CicloACobrar;
            string StInnerCuopen = "", StWhereClades = "";
            bool AplicarMovtosExtras = true;

            if (AplicaExtrasNom == "N")
            {
                AplicarMovtosExtras = false;
            }

            if (PeriodoCausa != 999999)
            {
                if (TipoNomina != "3")
                {
                    CicloACobrar = " and copmora.periodo_causa <= " + PeriodoCausa + " ";
                }
                else
                {
                    CicloACobrar = " and copmora.periodo_causa = " + PeriodoCausa + " ";
                }
            }
            else
            {
                CicloACobrar = "";
            }

            Prioridades = BuscaPrioridad(appconnect);
            Prioridades.SetValue("Extras", 10);

            switch (Clades)
            {
                case 1:
                    Stclades = " and copmae.clades = '1' " + (empdsto.Trim() == "Todos" ? "" : " and copmae.empdsto = '" + empdsto + "'");
                    break;
                case 2:
                    Stclades = " and copmae.clades = '2' ";
                    break;
                default:
                    Stclades = null;
                    break;
            }

            if (DesdeGrabCredito == true)
            {
                switch (Clades)
                {
                    case 1:
                    case 2:
                        StInnerCuopen = " inner join cop_cuopen cuopen on copmora.codigoter=cuopen.codigoter and copmora.lincred=cuopen.lincred and copmora.numero=cuopen.numero and copmora.periodo_causa=cuopen.periodo_causa ";
                        StWhereClades = " and cuopen.CLADES='" + Clades + "' ";
                        break;
                }
            }

            if (lincred == 9999 && NumCredito == 99999999)
            {
                stmysql = "SELECT copmora.codigoter, copmora.lincred,copmora.numero, SaldoCapital , SaldoExtra ,SaldoInteres , SaldoMora,SaldoSeguro, SaldoAdmon, periodo_causa,nume_extra,para12.previv,para12.PRIORI "
                    + " from cop_copmora copmora inner join cop_maecar copmae on copmora.codigoter = copmae.codigoter  and copmora.lincred =  copmae.lincred and copmora.numero = copmae.numero "
                    + " inner join cop_concar12 para12 on copmora.lincred = para12.lincred "
                    + " where copmora.codigoter = '" + codigoter + "' and Periodo_contable = " + Periodo + Stclades + CicloACobrar
                    + " and copmora.lincred >= 1000 and (SaldoCapital <> 0 or  SaldoExtra <> 0 or  SaldoInteres <> 0 or  SaldoMora <> 0 or SaldoSeguro<>0 or SaldoAdmon <> 0) "
                    + " order BY copmora.periodo_contable,COPMORA.PERIODO_CAUSA, copmora.lincred, copmora.numero,copmora.codigoter ";
            }
            else
            {
                stmysql = "SELECT copmora.codigoter, copmora.lincred, copmora.numero, copmora.SaldoCapital , copmora.SaldoExtra ,copmora.SaldoInteres , copmora.SaldoMora,copmora.SaldoSeguro, copmora.SaldoAdmon, copmora.periodo_causa, copmora.nume_extra,para12.previv,para12.PRIORI "
                    + " from cop_copmora copmora inner join cop_concar12 para12 on copmora.lincred = para12.lincred " + StInnerCuopen + " where copmora.codigoter = '" + codigoter
                    + "' and copmora.lincred = " + lincred + " and copmora.numero = " + NumCredito + " and copmora.Periodo_contable = " + Periodo + CicloACobrar + StWhereClades
                    + " and copmora.lincred >= 1000 and (copmora.SaldoCapital <> 0 or copmora.SaldoExtra <> 0 or copmora.SaldoInteres <> 0 or copmora.SaldoMora <> 0 or copmora.SaldoSeguro <> 0 or copmora.SaldoAdmon <> 0) "
                    + " order BY copmora.periodo_contable,COPMORA.PERIODO_CAUSA, copmora.lincred, copmora.numero,copmora.codigoter ";
            }

            if (ItemPrioridad > 0)
            {
                itemInicial = ItemPrioridad;
                Itemfinal = ItemPrioridad;
            }
            else
            {
                itemInicial = ItemPrioridad;
                Itemfinal = Prioridades.GetUpperBound(0);
                Prioridades.SetValue("", 10);
            }

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, appconnect, "GrabaCreditos", ref myRead, "TblGrabaCreditos");
            canreg = myRead.Tables["TblGrabaCreditos"].Rows.Count;

            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblGrabaCreditos"].Rows[fila];
                sw1 = 0; ValCapCaus = 0; ValExtCaus = 0;

                if (ValidaSaldoCreditos == true)
                {
                    this.BuscaSaldoObligacion(codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Periodo.ToString(), appconnect, ref Saldo);
                    if (Saldo <= 0)
                    {
                        sw1 = 1;
                    }
                }

                if (DesdeApliNomina == true)
                {
                    if (row["PRIORI"] != DBNull.Value)
                    {
                        if (row["PRIORI"].ToString().Trim() == "99")
                        {
                            //this.BuscaSaldosCuotasPendientes(codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Periodo, appconnect, ref ValCapCaus, ref ValExtCaus);
                            if (ValCapCaus == 0 && ValExtCaus == 0)
                            {
                                sw1 = 1;
                            }
                        }
                    }
                }

                if (sw1 == 0)
                {
                    for (item = itemInicial; item <= Itemfinal; item++)
                    {
                        ValorPagar = 0;
                        switch (Prioridades.GetValue(item).ToString())
                        {
                            case "Capital":
                                if (AplicarMovtosExtras == true)
                                {
                                    if (credito > Convert.ToDouble(row["SaldoExtra"]))
                                    {
                                        ValorPagar = Convert.ToDouble(row["SaldoExtra"]);
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    if (ValorPagar != 0 && Itemfinal != itemInicial)
                                    {
                                        //ok = BuscaExtras(codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["nume_extra"]), appconnect, ref ExtClaDes);
                                        if (ExtClaDes != Clades && Clades > 0)
                                        {
                                            ok = false;
                                        }
                                        if (ok == true)
                                        {
                                            if (ValorPagar > Saldo)
                                            {
                                                ValorPagar = Saldo;
                                            }
                                            GrabaExtra(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, Convert.ToInt32(row["nume_extra"]), "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                            credito = credito - ValorPagar;
                                            Saldo = Saldo - ValorPagar;
                                        }
                                    }
                                }

                                if (credito > Convert.ToDouble(row["SaldoCapital"]))
                                {
                                    ValorPagar = Convert.ToDouble(row["SaldoCapital"]);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }

                                if (ValorPagar < 0)
                                {
                                    if (credito > (Convert.ToDouble(row["SaldoCapital"]) * -1))
                                    {
                                        ValorPagar = (Convert.ToDouble(row["SaldoCapital"]) * -1);
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    GrabaCapital(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, 0, TrasladoCpto);

                                    credito = credito - ValorPagar;
                                    ValorPagar = 0;
                                }

                                if (ValorPagar != 0)
                                {
                                    if (ValorPagar > Saldo)
                                    {
                                        ValorPagar = Saldo;
                                    }
                                    GrabaCapital(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, 0, TrasladoCpto);
                                    Saldo = Saldo - ValorPagar;
                                }
                                break;

                            case "Extras":
                                if (AplicarMovtosExtras == true)
                                {
                                    if (row["previv"].ToString() == "Y")
                                    {
                                        SaldoInteres = Convert.ToDouble(row["SaldoInteres"]);
                                    }
                                    else
                                    {
                                        SaldoInteres = 0;
                                    }

                                    if (credito > Convert.ToDouble(row["SaldoExtra"]) + SaldoInteres)
                                    {
                                        ValorPagar = Convert.ToDouble(row["SaldoExtra"]) + SaldoInteres;
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    if ((Convert.ToInt32(row["nume_extra"]) != Nroextra) && Nroextra > 0)
                                    {
                                        ValorPagar = 0;
                                    }

                                    //BuscaExtras(codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["nume_extra"]), appconnect, ref ExtClaDes);
                                    if (ExtClaDes != Clades && Clades > 0)
                                    {
                                        ValorPagar = 0;
                                    }

                                    if (ValorPagar < 0)
                                    {
                                        if (credito > (Convert.ToDouble(row["SaldoExtra"]) * -1))
                                        {
                                            ValorPagar = (Convert.ToDouble(row["SaldoExtra"]) * -1);
                                        }
                                        else
                                        {
                                            ValorPagar = credito;
                                        }

                                        GrabaExtra(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, Convert.ToInt32(row["nume_extra"]), "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);

                                        credito = credito - ValorPagar;
                                        ValorPagar = 0;
                                    }

                                    if (Clades == 1)
                                    {
                                        if (Convert.ToDouble(row["SaldoInteres"]) > 0 && row["previv"].ToString() == "Y")
                                        {
                                            if (ValorPagar > 0)
                                            {
                                                this.GrabaInteres(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, Convert.ToDouble(row["SaldoInteres"]), Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                                ValorPagar -= Convert.ToDouble(row["SaldoInteres"]);
                                                credito = credito - Convert.ToDouble(row["SaldoInteres"]);
                                            }
                                        }
                                    }

                                    if (ValorPagar != 0)
                                    {
                                        if (ValorPagar > Saldo)
                                        {
                                            ValorPagar = Saldo;
                                        }
                                        GrabaExtra(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, Convert.ToInt32(row["nume_extra"]), "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                        Saldo = Saldo - ValorPagar;
                                    }
                                }
                                break;

                            case "Interes":
                                if (credito > Convert.ToDouble(row["SaldoInteres"]))
                                {
                                    ValorPagar = Convert.ToDouble(row["SaldoInteres"]);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }

                                if (ValorPagar < 0)
                                {
                                    if (credito > (Convert.ToDouble(row["SaldoInteres"]) * -1))
                                    {
                                        ValorPagar = (Convert.ToDouble(row["SaldoInteres"]) * -1);
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    GrabaInteres(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);

                                    credito = credito - ValorPagar;
                                    ValorPagar = 0;
                                }

                                if (ValorPagar != 0)
                                {
                                    GrabaInteres(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                }
                                break;

                            case "Mora":
                                if (credito > Convert.ToDouble(row["SaldoMora"]))
                                {
                                    ValorPagar = Convert.ToDouble(row["SaldoMora"]);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }

                                if (ValorPagar < 0)
                                {
                                    if (credito > (Convert.ToDouble(row["SaldoMora"]) * -1))
                                    {
                                        ValorPagar = (Convert.ToDouble(row["SaldoMora"]) * -1);
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    GrabaMora(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);

                                    credito = credito - ValorPagar;
                                    ValorPagar = 0;
                                }

                                if (ValorPagar != 0)
                                {
                                    GrabaMora(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                }
                                break;

                            case "Admon":
                                if (credito > Convert.ToDouble(row["SaldoAdmon"]))
                                {
                                    ValorPagar = Convert.ToDouble(row["SaldoAdmon"]);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }

                                if (ValorPagar < 0)
                                {
                                    if (credito > (Convert.ToDouble(row["SaldoAdmon"]) * -1))
                                    {
                                        ValorPagar = (Convert.ToDouble(row["SaldoAdmon"]) * -1);
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    GrabaAdmon(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);

                                    credito = credito - ValorPagar;
                                    ValorPagar = 0;
                                }

                                if (ValorPagar != 0)
                                {
                                    GrabaAdmon(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                }
                                break;

                            case "Seguro":
                                if (credito > Convert.ToDouble(row["SaldoSeguro"]))
                                {
                                    ValorPagar = Convert.ToDouble(row["SaldoSeguro"]);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }

                                if (ValorPagar < 0)
                                {
                                    if (credito > (Convert.ToDouble(row["SaldoSeguro"]) * -1))
                                    {
                                        ValorPagar = (Convert.ToDouble(row["SaldoSeguro"]) * -1);
                                    }
                                    else
                                    {
                                        ValorPagar = credito;
                                    }

                                    GrabaSeguro(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);

                                    credito = credito - ValorPagar;
                                    ValorPagar = 0;
                                }

                                if (ValorPagar != 0)
                                {
                                    GrabaSeguro(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), 0, ValorPagar, Periodo, fechamovto, Convert.ToInt32(row["periodo_causa"]), Detalle, appconnect, "", Idbenef, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                }
                                break;
                        }

                        credito = credito - ValorPagar;
                    }
                }
                fila += 1;
            }

            myRead.Dispose();
            return credito;
        }

        private double GrabaAportes(string Comprobante, double ConseComprobante, int Lincred, double ConseCredito,
            string codigoter, int periodo, DateTime FechaMovto, double debito, ref double credito, string detalle,
            OdbcConnection myconnect, int codMovto = 0, int clades = 0, string Usuario = null,
            string PagoCodeuda = "99999999999999", int CicloNomina = 999999, string UsuarioSobreGiro = " ", double VlrSobreGiro = 0,
            string empdsto = "9999", string TipoNomina = "", double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            double ValorPagar = 0, total = 0;
            string Stclades;
            string stmysql;
            int reg = 0, tfila = 0;
            string CicloACobrar;

            if (CicloNomina != 999999)
            {
                if (TipoNomina.Trim() == "")
                {
                    //msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, TipoNomina: ref TipoNomina);
                }

                if (TipoNomina != "3")
                {
                    CicloACobrar = " and copmora.periodo_causa <= " + CicloNomina + " ";
                }
                else
                {
                    CicloACobrar = " and copmora.periodo_causa = " + CicloNomina + " ";
                }
            }
            else
            {
                CicloACobrar = "";
            }

            switch (clades)
            {
                case 1:
                    Stclades = " and copmae.clades = '1'" + (empdsto.Trim() == "Todos" ? "" : " and copmae.empdsto = '" + empdsto + "'");
                    break;
                case 2:
                    Stclades = " and copmae.clades = '2' ";
                    break;
                default:
                    Stclades = null;
                    break;
            }

            if (Lincred == 9999)
            {
                stmysql = "SELECT copmora.lincred, copmora.numero, SaldoCapital, periodo_causa"
                    + " from cop_copmora copmora inner join cop_maecar copmae on copmora.codigoter = copmae.codigoter  and copmora.lincred =  copmae.lincred and copmora.numero = copmae.numero "
                    + " inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                    + " where copmora.codigoter = '" + codigoter + "'" + Stclades + " and Periodo_contable = " + periodo + CicloACobrar + " and  CODAHOR = 1 and "
                    + " (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon) <> 0 "
                    + " order by copmora.codigoter, copmora.lincred, copmora.numero, periodo_causa ";
            }
            else
            {
                stmysql = "SELECT copmora.lincred, numero, SaldoCapital, periodo_causa"
                    + " from cop_copmora copmora inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                    + " where codigoter = '" + codigoter + "' and Periodo_contable = " + periodo + " and  CODAHOR = 1 and "
                    + " copmora.lincred = " + Lincred + " and copmora.numero = " + ConseCredito + CicloACobrar + " and "
                    + " (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon) <> 0 "
                    + " order by codigoter, copmora.lincred, numero, periodo_causa ";
            }

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "GrabaAportes", ref myRead, "TblGrabaAportes");
            reg = myRead.Tables["TblGrabaAportes"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myRead.Tables["TblGrabaAportes"].Rows[tfila];
                if (credito > Convert.ToDouble(row["SaldoCapital"]))
                {
                    ValorPagar = Convert.ToDouble(row["SaldoCapital"]);
                }
                else
                {
                    ValorPagar = credito;
                }

                if (ValorPagar < 0)
                {
                    if (credito > (Convert.ToDouble(row["SaldoCapital"]) * -1))
                    {
                        ValorPagar = (Convert.ToDouble(row["SaldoCapital"]) * -1);
                    }
                    else
                    {
                        ValorPagar = credito;
                    }

                    GrabaCapitalAportes(Comprobante, ConseComprobante, codigoter, Convert.ToDouble(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto.ToString(), Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                    credito = credito - ValorPagar;
                    ValorPagar = 0;
                }

                if (ValorPagar > 0)
                {
                    GrabaCapitalAportes(Comprobante, ConseComprobante, codigoter, Convert.ToDouble(row["lincred"]), Convert.ToDouble(row["numero"]), debito, ValorPagar, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto.ToString(), Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                }

                credito = credito - ValorPagar;
                tfila += 1;
            }

            myRead.Dispose();
            return total;
        }

        private double GrabaAhorros(string Comprobante, double ConseComprobante, int Lincred, double ConseCredito,
            string codigoter, int periodo, DateTime FechaMovto, double debito, ref double credito, string detalle,
            OdbcConnection myconnect, int codMovto = 0, int clades = 0,
            string Usuario = null, string Idbenef = "99999999999999",
            string PagoCodeuda = "99999999999999", int CicloNomina = 999999,
            string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, string empdsto = "9999", string TipoNomina = "", double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            int reg = 0, tfila = 0;
            double ValorPagar = 0, total = 0;
            string Stclades;
            string stmysql;
            string CicloACobrar;

            if (CicloNomina != 999999)
            {
                if (TipoNomina.Trim() == "")
                {
                    //msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, TipoNomina: ref TipoNomina);
                }

                if (TipoNomina != "3")
                {
                    CicloACobrar = " and copmora.periodo_causa <= " + CicloNomina + " ";
                }
                else
                {
                    CicloACobrar = " and copmora.periodo_causa = " + CicloNomina + " ";
                }
            }
            else
            {
                CicloACobrar = "";
            }

            switch (clades)
            {
                case 1:
                    Stclades = " and copmae.clades = '1' " + (empdsto.Trim() == "Todos" ? "" : " and copmae.empdsto = '" + empdsto + "'");
                    break;
                case 2:
                    Stclades = " and copmae.clades = '2' ";
                    break;
                default:
                    Stclades = null;
                    break;
            }

            if (Lincred == 9999)
            {
                stmysql = "SELECT copmora.lincred, copmora.numero, SaldoCapital, periodo_causa"
                    + " from cop_copmora copmora inner join cop_maecar copmae on copmora.codigoter = copmae.codigoter  and copmora.lincred =  copmae.lincred and copmora.numero = copmae.numero "
                    + " inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                    + " where copmora.codigoter = '" + codigoter + "'" + Stclades + "and Periodo_contable = " + periodo + CicloACobrar + " and  CODAHOR = 2 and "
                    + " (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon) <> 0 "
                    + " order by copmora.codigoter, copmora.lincred, copmora.numero, periodo_causa ";
            }
            else
            {
                stmysql = "SELECT copmora.lincred, numero, SaldoCapital, periodo_causa"
                    + " from cop_copmora copmora inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                    + " where codigoter = '" + codigoter + "' and Periodo_contable = " + periodo + " and  CODAHOR = 2 and "
                    + " copmora.lincred = " + Lincred + " and copmora.numero = " + ConseCredito + CicloACobrar + " and "
                    + " (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon) <> 0 "
                    + " order by codigoter, copmora.lincred, numero, periodo_causa ";
            }

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "GrabaAhorros", ref myRead, "TblGrabaAhorros");
            reg = myRead.Tables["TblGrabaAhorros"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myRead.Tables["TblGrabaAhorros"].Rows[tfila];
                if (credito > Convert.ToDouble(row["SaldoCapital"]))
                {
                    ValorPagar = Convert.ToDouble(row["SaldoCapital"]);
                }
                else
                {
                    ValorPagar = credito;
                }

                if (ValorPagar < 0)
                {
                    if (credito > (Convert.ToDouble(row["SaldoCapital"]) * -1))
                    {
                        ValorPagar = (Convert.ToDouble(row["SaldoCapital"]) * -1);
                    }
                    else
                    {
                        ValorPagar = credito;
                    }

                    GrabaCapitalAhorros(Comprobante, ConseComprobante, codigoter, Convert.ToDouble(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto.ToString(), Usuario, Idbenef, "9999", " ", " ", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "CC", 0, TrasladoCpto);
                    credito = credito - ValorPagar;
                    ValorPagar = 0;
                }

                if (ValorPagar > 0)
                {
                    GrabaCapitalAhorros(Comprobante, ConseComprobante, codigoter, Convert.ToDouble(row["lincred"]), Convert.ToDouble(row["numero"]), debito, ValorPagar, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto.ToString(), Usuario, Idbenef, "9999", " ", " ", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "CC", 0, TrasladoCpto);
                }

                credito = credito - ValorPagar;
                tfila += 1;
            }

            myRead.Dispose();
            return total;
        }

        public void GrabaCapitalAportes(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, string CodMovto = "0", string Usuario = null,
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            string stmysql;
            string CptoApor = "00";
            int TipoMov = 0;
            string Nomres = " ", Cuenta = "999999999999";
            string cencos = "99999999", nit = " ";
            bool ok = false;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_APORTES as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_APORTES = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                string _tipoMov = TipoMov.ToString();
                //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalAportes", ref CptoApor, ref _tipoMov, ref Nomres);
                int.TryParse(_tipoMov, out TipoMov);
            }
            else
            {
                CptoApor = CodMovto;
            }

            string _tm = TipoMov.ToString();
            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2 from cop_codmov codmov where cod_movto = '" + CptoApor + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _tm, ref Nomres);
            int.TryParse(_tm, out TipoMov);

            stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + LINCRED;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref cencos, ref Cuenta);

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            if (ok == false)
            {
                MessageBox.Show("Cuenta contable no  existe en el maestro de cuentas" + "\n" + " Linea : " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref nit);

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            this.InsertMovto(Comprobante, ConseComprobante, codigoter, (int)LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoApor, Ciclo.ToString(), Usuario, "9999", Cuenta, 0, TrasladoCpto, " ", " ", "0", 0, 0, cencos, " ", 0, nit, 0, Detalle, myconnect, "", "99999999999999", null, null, " ", PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, "CC", 0, null, 0, secuenciadepende);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                //GrabaCopmora(codigoter, LINCRED, ConseCredito, debito, credito, "C", Ciclo, periodo, myconnect);
            }

            if (TipoMov == 4)
            {
               // GrabaSaldos(codigoter, LINCRED, ConseCredito, periodo.ToString(), debito, credito, myconnect);
            }
        }

        private void GrabaCapitalAhorros(string Comprobante, double ConseComprobante, string codigoter, double LINCRED, double ConseCredito,
            double debito, double credito, int periodo, DateTime Fechamovto, int Ciclo, string Detalle,
            OdbcConnection myconnect, string CodMovto = "0", string Usuario = null, string Idbenef = "99999999999999",
            string banco = "9999", string NumCheque = " ", string DesPrendible = " ",
            string PagoCodeuda = "99999999999999", string UsuarioSobreGiro = " ", double VlrSobreGiro = 0, string EsMovtoDe = "CC", double secuenciadepende = 0, string TrasladoCpto = " ", DateTime? feccausa_ahorNullable = null, int LineaAhorro = 9999)
        {
            DateTime feccausa_ahor = feccausa_ahorNullable ?? new DateTime(1900, 1, 1);
            string stmysql;
            string CptoAho = "00";
            int TipoMov = 0;
            string Nomres = " ", Cuenta = "999999999999";
            string cencos = "99999999", nit = " ";
            bool ok = false;

            if (CodMovto == "0")
            {
                stmysql = "select CPTO_AHORROS as campo1, tipo_movto as campo2, codmov.nomres as campo3, cuenta as campo4 from sys_compania compania inner join cop_codmov codmov on compania.CPTO_AHORROS = codmov.cod_movto where codigo = '" + varini.sptCodEmpr + "'";
                string _tm = TipoMov.ToString();
                //this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalAportes", ref CptoAho, ref _tm, ref Nomres);
                int.TryParse(_tm, out TipoMov);
            }
            else
            {
                CptoAho = CodMovto;
            }

            string _tm2 = TipoMov.ToString();
            stmysql = "select tipo_movto as campo1, codmov.nomres as campo2 from cop_codmov codmov where cod_movto = '" + CptoAho + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapital", ref _tm2, ref Nomres);
            int.TryParse(_tm2, out TipoMov);

            stmysql = "select CENTROCO as campo1, CUENTA as campo2 from cop_concar12 where lincred = " + LINCRED;
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalAhorros", ref cencos, ref Cuenta);

            //ok = msgcnt.BuscarCuenta(Cuenta, myconnect);
            if (ok == false)
            {
                MessageBox.Show("Cuenta contable no  existe en el maestro de cuentas" + "\n" + " Linea : " + LINCRED, "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            stmysql = "select nit as campo1 from sys_maenit where codigoter = '" + codigoter + "'";
            this.OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaCapitalAhorros", ref nit);

            if (Ciclo == 0)
            {
                Ciclo = 999999;
            }

            this.InsertMovto(Comprobante, ConseComprobante, codigoter, (int)LINCRED, ConseCredito, Cuenta, Fechamovto, debito, credito, cencos, 0, CptoAho, Ciclo.ToString(), Usuario, "9999", Cuenta, 0, TrasladoCpto, banco, " ", "0", 0, 0, cencos, "  ", 0, nit, 0, Detalle, myconnect, DesPrendible, Idbenef, null, null, NumCheque, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, EsMovtoDe, 0, feccausa_ahor, 0, secuenciadepende, "N", LineaAhorro);

            if (Ciclo > 0 && Ciclo != 999999)
            {
                //GrabaCopmora(codigoter, LINCRED, ConseCredito, debito, credito, "C", Ciclo, periodo, myconnect);
            }

            //GrabaSaldos(codigoter, LINCRED, ConseCredito, periodo.ToString(), debito, credito, myconnect);
        }

        private double GrabaServicios(string Comprobante, double ConseComprobante, string codigoter, int lincred,
            double ConseCredito, int periodo, DateTime FechaMovto, double debito, ref double credito,
            string detalle, OdbcConnection myconnect, string codMovto = "0", int Clades = 0, string Usuario = null,
            string PagoCodeuda = "99999999999999", int CicloNomina = 999999, string UsuarioSobreGiro = " ", double VlrSobreGiro = 0,
            string empdsto = "9999", string DesdeGrabCredito = "9999", string TipoNomina = "", double secuenciadepende = 0, string TrasladoCpto = " ")
        {
            int reg = 0, tfila = 0;
            double saldo = 0;
            int sw1 = 0;
            double ValorPagar = 0, total = 0;
            string Stclades;
            string stmysql;
            string Tipo_movto = "00";
            string CicloACobrar;
            string StInnerCuopen = "", StWhereClades = "";

            if (codMovto != "0")
            {
                //this.BuscaTipoMovto(codMovto, myconnect, ref Tipo_movto);
            }

            if (CicloNomina != 999999)
            {
                if (TipoNomina.Trim() == "")
                {
                   // msgcofsys.BuscarCompania(varini.sptCodEmpr, myconnect, TipoNomina: ref TipoNomina);
                }

                if (TipoNomina != "3")
                {
                    CicloACobrar = " and copmora.periodo_causa <= " + CicloNomina + " ";
                }
                else
                {
                    CicloACobrar = " and copmora.periodo_causa = " + CicloNomina + " ";
                }
            }
            else
            {
                CicloACobrar = "";
            }

            switch (Clades)
            {
                case 1:
                    Stclades = " and copmae.clades = '1'" + (empdsto.Trim() == "Todos" ? "" : " and copmae.empdsto = '" + empdsto + "'");
                    break;
                case 2:
                    Stclades = " and copmae.clades = '2' ";
                    break;
                default:
                    Stclades = null;
                    break;
            }

            if (DesdeGrabCredito == "True" || DesdeGrabCredito == true.ToString())
            {
                switch (Clades)
                {
                    case 1:
                    case 2:
                        StInnerCuopen = " inner join cop_cuopen cuopen on copmora.codigoter=cuopen.codigoter and copmora.lincred=cuopen.lincred and copmora.numero=cuopen.numero and copmora.periodo_causa=cuopen.periodo_causa ";
                        StWhereClades = " and cuopen.CLADES='" + Clades + "' ";
                        break;
                }
            }

            if (lincred == 9999)
            {
                stmysql = "SELECT copmora.lincred, copmora.numero, SaldoCapital,SaldoMora, periodo_causa"
                    + " from cop_copmora copmora inner join cop_maecar copmae on copmora.codigoter = copmae.codigoter  and copmora.lincred =  copmae.lincred and copmora.numero = copmae.numero"
                    + " inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred"
                    + " where copmora.codigoter = '" + codigoter + "'" + Stclades + " and Periodo_contable = " + periodo + CicloACobrar + " and  CODAHOR = '3' and "
                    + " (SaldoCapital + SaldoExtra + SaldoInteres + SaldoMora + SaldoSeguro + SaldoAdmon) <> 0 "
                    + " order by copmora.codigoter, copmora.lincred, copmora.numero, periodo_causa ";
            }
            else
            {
                stmysql = "SELECT copmora.lincred, copmora.numero, copmora.SaldoCapital,copmora.SaldoMora, copmora.periodo_causa"
                    + " from cop_copmora copmora inner join cop_concar12 parame12 on copmora.lincred = parame12.lincred " + StInnerCuopen
                    + " where copmora.codigoter = '" + codigoter + "' and copmora.Periodo_contable = " + periodo + " and  CODAHOR = '3' and "
                    + " (copmora.SaldoCapital + copmora.SaldoExtra + copmora.SaldoInteres + copmora.SaldoMora + copmora.SaldoSeguro + copmora.SaldoAdmon) <> 0 and "
                    + " copmora.lincred = " + lincred + " and copmora.numero = " + ConseCredito + CicloACobrar + StWhereClades
                    + " order by copmora.codigoter, copmora.lincred, copmora.numero, copmora.periodo_causa ";
            }

            DataSet myRead = new DataSet();
            this.OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "GrabaServicios", ref myRead, "TblGrabaServicio");
            reg = myRead.Tables["TblGrabaServicio"].Rows.Count;

            while (tfila < reg)
            {
                DataRow row = myRead.Tables["TblGrabaServicio"].Rows[tfila];
                sw1 = 0;
                saldo = 0;
                if (Convert.ToInt32(row["lincred"]) >= 1000)
                {
                    this.BuscaSaldoObligacion(codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), periodo.ToString(), myconnect, ref saldo);
                    if (saldo <= 0)
                    {
                        sw1 = 1;
                    }
                }

                if (sw1 == 0)
                {
                    switch (Tipo_movto)
                    {
                        case "3":
                            if (credito > Convert.ToDouble(row["SaldoMora"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoMora"]);
                            }
                            else
                            {
                                ValorPagar = credito;
                            }
                            break;
                        default:
                            if (credito > Convert.ToDouble(row["SaldoCapital"]))
                            {
                                ValorPagar = Convert.ToDouble(row["SaldoCapital"]);
                            }
                            else
                            {
                                ValorPagar = credito;
                            }
                            break;
                    }

                    if (ValorPagar < 0)
                    {
                        switch (Tipo_movto)
                        {
                            case "3":
                                if (credito > (Convert.ToDouble(row["SaldoCapital"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoCapital"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }

                                GrabaCapitalServicios(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                break;
                            default:
                                if (credito > (Convert.ToDouble(row["SaldoMora"]) * -1))
                                {
                                    ValorPagar = (Convert.ToDouble(row["SaldoMora"]) * -1);
                                }
                                else
                                {
                                    ValorPagar = credito;
                                }
                                GrabaMora(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), ValorPagar, 0, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto, codigoter, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                break;
                        }

                        credito = credito - ValorPagar;
                        ValorPagar = 0;
                    }

                    if (ValorPagar > 0)
                    {
                        switch (Tipo_movto)
                        {
                            case "3":
                                GrabaMora(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), debito, ValorPagar, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto, codigoter, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, 0, TrasladoCpto);
                                break;
                            default:
                                GrabaCapitalServicios(Comprobante, ConseComprobante, codigoter, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), debito, ValorPagar, periodo, FechaMovto, Convert.ToInt32(row["periodo_causa"]), detalle, myconnect, codMovto, Usuario, PagoCodeuda, UsuarioSobreGiro, VlrSobreGiro, secuenciadepende, TrasladoCpto);
                                break;
                        }
                    }

                    credito = credito - ValorPagar;
                }
                tfila += 1;
            }

            myRead.Dispose();
            return total;
        }

    } // end partial class Clscartera
} // end namespace msgcop
