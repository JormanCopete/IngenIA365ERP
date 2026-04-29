// Traducción de: ClsLiqcreditos.vb (msgliqcre) — Parte 2 (líneas VB 2100-4200)
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
        // -----------------------------------------------------------------------
        // VB line 2102
        // -----------------------------------------------------------------------
        private double GrabaConseSolicitud(double NumConse, OdbcConnection myconnect)
        {
            stmysql = "update sys_compania set num_solcred = '" + NumConse + "' where codigo = '" + varini.sptCodEmpr + "'";
            OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaConseSolicitud");
            return 0;
        }

        // VB line 2107
        public void DespliegaSolicitud(DataSet dsdataset, string Usuario, OdbcConnection myconnect,
            Form myforma, ref int Solicitud)
        {
            // SolicitudCredito frmsolicitud = new SolicitudCredito(myconnect); // ERROR: CS0246
            DataSet data = new DataSet();
            bool ook = false;
            bool chkporcentaje = false;

            switch (Solicitud)
            {
                case -1:
                    // frmsolicitud.DsDatosProyeccion = dsdataset; // ERROR: CS0103
                    {
                        DataRow _r0 = dsdataset.Tables["TbldatosCredito"].Rows[0];
                        // frmsolicitud.TxtvalorSolicitud.Text = string.Format("{0:N0}", _r0["valorCredito"]); // ERROR: CS0103
                        // frmsolicitud.TxtvalorSolicitud.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.TxtCuota.Text = string.Format("{0:N0}", _r0["cuota"]); // ERROR: CS0103
                        // frmsolicitud.TxtCuota.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.txtLincred.Text = _r0["lincred"].ToString(); // ERROR: CS0103
                        // frmsolicitud.txtLincred.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.TxtCodigoter.Text = _r0["Cedula"].ToString(); // ERROR: CS0103
                        // frmsolicitud.txtnombreLInea.Text = _r0["NombreLinea"].ToString(); // ERROR: CS0103
                        // frmsolicitud.txtnombreLInea.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.txtTasaInt.Text = _r0["TasaInt"].ToString(); // ERROR: CS0103
                        // frmsolicitud.txtTasaInt.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.TxtPlazo.Text = _r0["plazo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPlazo.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.TxtBaseCupo.Text = _r0["BaseCupo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtBaseCupo.ReadOnly = true; // ERROR: CS0103
                        //frmsolicitud.TxtCupoDisponible.Text = "0";
                        // frmsolicitud.TxtCupoDisponible.ReadOnly = true; // ERROR: CS0103
                        // frmsolicitud.LblFecSolicitud.Text = Convert.ToDateTime(_r0["fecha"]).ToString(varini.PstForFec); // ERROR: CS0103
                        // frmsolicitud.usuario.Text = Usuario; // ERROR: CS0103
                        // frmsolicitud.Dtpfecdsto.Value = Convert.ToDateTime(_r0["FecDesto"]); // ERROR: CS0103
                        // frmsolicitud.Invocado = false; // ERROR: CS0103
                        // frmsolicitud.TxtEmpDescuento.Text = _r0["empresa"].ToString(); // ERROR: CS0103
                        // frmsolicitud.estado = "P"; // ERROR: CS0103
                        // frmsolicitud.EstadoSolicitud = "P"; // ERROR: CS0103
                        // frmsolicitud.solant = true; // ERROR: CS0103
                        // frmsolicitud.ValorsaldoRecogida = Convert.ToDouble(_r0["SaldoDeudaRecogida"]); // ERROR: CS0103
                        // frmsolicitud.ciclo = Convert.ToInt32(_r0["ciclo"]); // ERROR: CS0103
                        // frmsolicitud.periodicidad = Convert.ToInt32(_r0["periodicidad"]); // ERROR: CS0103
                    }
                    try
                    {
                        DataRow _r1 = dsdataset.Tables["TbldatosCapacidadPago"].Rows[0];
                        // frmsolicitud.salarioBasico = Convert.ToDouble(_r1["salario"]); // ERROR: CS0103
                        // frmsolicitud.otroIngreso = Convert.ToDouble(_r1["otro_ingreso"]); // ERROR: CS0103
                        // frmsolicitud.otroIngresoConyuge = Convert.ToDouble(_r1["CONYSALAR"]); // ERROR: CS0103
                        // frmsolicitud.IngresosVariables = Convert.ToDouble(_r1["IngVariables"]); // IngresosVariables // ERROR: CS0103
                        // frmsolicitud.txtIngresoConyuge.Text = _r1["CONYSALAR"].ToString();
                        // frmsolicitud.TxtIngVariables.Text = _r1["IngVariables"].ToString(); // ERROR: CS0103

                        // frmsolicitud.IngArriendos = Convert.ToDouble(_r1["IngArriendos"]); // ERROR: CS0103
                        // frmsolicitud.TxtIngArriendos.Text = _r1["IngArriendos"].ToString(); // ERROR: CS0103

                        // frmsolicitud.IngPension = Convert.ToDouble(_r1["IngPension"]); // ERROR: CS0103
                        // frmsolicitud.TxtPensiones.Text = _r1["IngPension"].ToString(); // ERROR: CS0103

                        // frmsolicitud.DeudasTerceros = Convert.ToDouble(_r1["DeudasTerceros"]); // ERROR: CS0103
                        // frmsolicitud.TxtDeudasTerceros.Text = _r1["DeudasTerceros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDstoParafiscales.Text = _r1["DstoParafiscales"].ToString(); // ERROR: CS0103

                        // frmsolicitud.DstoPension = Convert.ToDouble(_r1["DstoPension"]); // ERROR: CS0103
                        // frmsolicitud.TxtDstoPension.Text = _r1["DstoPension"].ToString(); // ERROR: CS0103

                        // frmsolicitud.GASTO_FIJO_MES = Convert.ToDouble(_r1["Dsctos"]); // ERROR: CS0103
                        // frmsolicitud.TxtGastosMes.Text = _r1["Dsctos"].ToString(); // ERROR: CS0103

                        // frmsolicitud.TxtGastosPnales.Text = _r1["dsGastoper"].ToString(); // ERROR: CS0103
                        // frmsolicitud.varTxtGastosPnales = Convert.ToDouble(_r1["dsGastoper"]); // ERROR: CS0103
                        // frmsolicitud.dstoGastosPerso = Convert.ToDouble(_r1["dsGastoper"]); // ERROR: CS0103

                        // frmsolicitud.TxtDesNomEmpresa.Text = _r1["DeuCoopNomi"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDesCajaEmpresa.Text = _r1["DeuCoopCaja"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCupoDisponible.Text = _r1["CupoTotal"].ToString(); // ERROR: CS0103

                        if (_r1["Porcn"].ToString() == "True")
                        {
                            // frmsolicitud.cappagoPorcentaje = "Y"; // ERROR: CS0103
                        }
                        else
                        {
                            // frmsolicitud.cappagoPorcentaje = "N"; // ERROR: CS0103
                        }

                        // frmsolicitud.ChkPorcentaje.Checked = chkporcentaje; // ERROR: CS0103
                        // frmsolicitud.ValorsaldoRecogida = Convert.ToDouble(_r1["SaldoDeudaRecogida"]); // ERROR: CS0103
                        // frmsolicitud.ValorCuotaRecogida = Convert.ToDouble(_r1["CuotaDeudaRecogidaTotal"]); // ERROR: CS0103
                        // frmsolicitud.CuotaRecogidaCaja = Convert.ToDouble(_r1["CuotaDeudaRecogidaCaja"]); // ERROR: CS0103
                        // frmsolicitud.CuotaRecogidaNomina = Convert.ToDouble(_r1["CuotaDeudaRecogida"]); // ERROR: CS0103
                        // frmsolicitud.lblCuotaRecogida.Text = _r1["CuotaDeudaRecogidaTotal"].ToString(); // ERROR: CS0103
                        switch (_r1["CbxRecDeudas"].ToString())
                        {
                            case "No":
                                // frmsolicitud.CbxRecDeudas.SelectedIndex = 0; // ERROR: CS0103
                                break;
                            case "Si":
                                // frmsolicitud.CbxRecDeudas.SelectedIndex = 1; // ERROR: CS0103
                                break;
                        }
                        // frmsolicitud.TxtSolActVivienda.Text = _r1["TxtSolActVivienda"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActVehiculo.Text = _r1["TxtSolActVehiculo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActOtros.Text = _r1["TxtSolActOtros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtActCtaBanco.Text = _r1["TxtActCtaBanco"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtActCxC.Text = _r1["TxtActCxC"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolPasOtros.Text = _r1["TxtSolPasOtros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPasObliBanca.Text = _r1["TxtPasObliBanca"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPasObliHipot.Text = _r1["TxtPasObliHipot"].ToString(); // ERROR: CS0103
                        if (dsdataset.Tables.Contains("tblBienesRaices") == true)
                        {
                            // frmsolicitud.dsbienes.Tables.Add(dsdataset.Tables["tblBienesRaices"].Copy()); // ERROR: CS0103
                        }
                        if (dsdataset.Tables.Contains("tblVehiculo") == true)
                        {
                            // frmsolicitud.dsbienes.Tables.Add(dsdataset.Tables["tblVehiculo"].Copy()); // ERROR: CS0103
                        }
                    }
                    catch (Exception)
                    {
                    }

                    int K = 0;
                    double totacuoataRecogida = 0;
                    double RecogidaNomina = 0, RecogidaCaja = 0;
                    try
                    {
                        for (K = 0; K <= dsdataset.Tables["tbldeducciones"].Rows.Count - 1; K++)
                        {
                            DataTable _tbl = dsdataset.Tables["tbldeducciones"];
                            if (_tbl.Columns.Contains("CUOTA") == true)
                            {
                                switch (Convert.ToInt32(_tbl.Rows[K]["CLADES"]))
                                {
                                    case 1:
                                        RecogidaNomina += Convert.ToDouble(_tbl.Rows[K]["CUOTA"]);
                                        break;
                                    case 2:
                                        RecogidaCaja += Convert.ToDouble(_tbl.Rows[K]["CUOTA"]);
                                        break;
                                }
                                totacuoataRecogida += Convert.ToDouble(_tbl.Rows[K]["CUOTA"]);
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }

                    // if (frmsolicitud.ValorCuotaRecogida == 0) // ERROR: CS0103
                    {
                        // frmsolicitud.ValorCuotaRecogida = totacuoataRecogida; // ERROR: CS0103
                        // frmsolicitud.CuotaRecogidaCaja = RecogidaCaja; // ERROR: CS0103
                        // frmsolicitud.CuotaRecogidaNomina = RecogidaNomina; // ERROR: CS0103

                        if (totacuoataRecogida == 0)
                        {
                            // frmsolicitud.CbxRecDeudas.SelectedIndex = 0; // ERROR: CS0103
                        }
                        else
                        {
                            // frmsolicitud.CbxRecDeudas.SelectedIndex = 1; // ERROR: CS0103
                        }
                    }
                    break;

                default:
                    // ok = this.BuscarSolicitud(Solicitud, dsdataset, myconnect); // ERROR: CS1620
                    if (ok)
                    {
                        DataRow _rCred = dsdataset.Tables["TbldatosCredito"].Rows[0];
                        // frmsolicitud.LblNumSolicitud.Text = Solicitud.ToString(); // ERROR: CS0103
                        // frmsolicitud.LblFecSolicitud.Text = _rCred["fecha_soli"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCodigoter.Text = _rCred["codigoter"].ToString(); // ERROR: CS0103
                        // frmsolicitud.txtLincred.Text = _rCred["lincred"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtvalorSolicitud.Text = string.Format("{0:N2}", Convert.ToDouble(_rCred["vlr_solicitud"])); // ERROR: CS0103
                        // frmsolicitud.txtTasaInt.Text = _rCred["tasa_int"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPlazo.Text = _rCred["plazo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCuota.Text = _rCred["cuota"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCupoDisponible.Text = _rCred["cupo_disponible"].ToString(); // ERROR: CS0103
                        // frmsolicitud.DtpFecCoop.Value = Convert.ToDateTime(_rCred["fec_ingr_coop"]); // ERROR: CS0103
                        // frmsolicitud.TxtOtrosIngresos.Text = _rCred["otro_ingreso"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtGastosMes.Text = _rCred["gasto_fijo_mes"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDisponibleMes.Text = _rCred["cupo_dismes"].ToString(); // ERROR: CS0103
                        // frmsolicitud.CbxClaseGar.SelectedIndex = Convert.ToInt32(_rCred["tipo_garantia"]); // ERROR: CS0103
                        // frmsolicitud.txtDescripGara.Text = _rCred["descripcion"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtAvaluoComercial.Text = _rCred["avaluo_ccial"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtAvalCatastral.Text = _rCred["avaluo_catastro"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPorSeg.Text = _rCred["por_seguro"].ToString(); // ERROR: CS0103
                        // frmsolicitud.ChkAsegurado.Checked = (_rCred["asegurado"].ToString() == "Y") ? true : false; // ERROR: CS0103
                        // frmsolicitud.DtpFecVenSeguro.Value = Convert.ToDateTime(_rCred["fecven_seguro"]); // ERROR: CS0103
                        // frmsolicitud.TxtCodeudor1.Text = _rCred["codeudor1"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCodeudor2.Text = _rCred["codeudor2"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCodeudor3.Text = _rCred["codeudor3"].ToString(); // ERROR: CS0103
                        // frmsolicitud.ChkTrabConyuge.Checked = (_rCred["conyu_labora"].ToString() == "Y") ? true : false; // ERROR: CS0103
                        // frmsolicitud.TxtNomConyuge.Text = _rCred["conyuge"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtEmpLaboraconyuge.Text = _rCred["empresa_labora"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSalConyuge.Text = _rCred["salario_me"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtTelConyuge.Text = _rCred["tel_conyuge"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDirEmpresaConyuge.Text = _rCred["dir_emp_conyu"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtCiudadConyuge.Text = _rCred["ciudad_emp_conyu"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPercargo.Text = _rCred["per_acargo_conyu"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtVehiculo.Text = _rCred["vehiculo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.ChkCasaPropia.Checked = (_rCred["casa_propia"].ToString() == "Y") ? true : false; // ERROR: CS0103
                        // frmsolicitud.ChkVehiculo.Checked = (_rCred["tiene_vehiculo"].ToString() == "Y") ? true : false; // ERROR: CS0103
                        // frmsolicitud.TxtCodeudor4.Text = _rCred["codeudor4"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDesCajaEmpresa.Text = _rCred["DSCTO_MES_EMP"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDesNomEmpresa.Text = _rCred["dscto_mes_emp_nomina"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtIngVariables.Text = _rCred["IngVariables"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtIngArriendos.Text = _rCred["IngArriendos"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDeudasTerceros.Text = _rCred["DeudasTerceros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtNumCdat.Text = _rCred["NumCdat"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtNit_aseguradora.Text = _rCred["nitaseguradora"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtNombre_aseguradora.Text = _rCred["nombreaseguradora"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtNum_poliza.Text = _rCred["numpoliza"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtMatricula.Text = _rCred["matricula"].ToString(); // ERROR: CS0103
                        // frmsolicitud.Dtpfecdsto.Value = Convert.ToDateTime(_rCred["FECDESC"]); // ERROR: CS0103
                        // frmsolicitud.TxtEmpDescuento.Text = _rCred["EmpDsto"].ToString(); // ERROR: CS0103
                        // frmsolicitud.estado = _rCred["estado"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtEmpDescuento.Text = _rCred["EmpDsto"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtNumPagare.Text = _rCred["NumPagare"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPensiones.Text = _rCred["IngPension"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDstoPension.Text = _rCred["DstoPension"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtDstoParafiscales.Text = _rCred["DstoParafiscales"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtValAprobado.Text = string.Format("{0:N2}", Convert.ToDouble(_rCred["valor_aprobado"])); // ERROR: CS0103
                        // frmsolicitud.txtIngresoConyuge.Text = _rCred["ingConyuge"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtGastosPnales.Text = _rCred["dstoGastosPerso"].ToString(); // ERROR: CS0103
                        // frmsolicitud.varTxtGastosPnales = Convert.ToDouble(_rCred["dstoGastosPerso"]); // ERROR: CS0103
                        // frmsolicitud.CbxRecDeudas.Text = _rCred["cappagoRecDeudas"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSalario.Text = _rCred["SALARIO"].ToString(); // ERROR: CS0103
                        switch (_rCred["cappagoPorcentaje"].ToString())
                        {
                            case "Y":
                                // frmsolicitud.ChkPorcentaje.Checked = true; // ERROR: CS0103
                                break;
                            case "N":
                                // frmsolicitud.ChkPorcentaje.Checked = false; // ERROR: CS0103
                                break;
                        }
                        switch (_rCred["cappagoRecDeudas"].ToString().Trim())
                        {
                            case "No":
                                // frmsolicitud.CbxRecDeudas.SelectedIndex = 0; // ERROR: CS0103
                                break;
                            case "Si":
                                // frmsolicitud.CbxRecDeudas.SelectedIndex = 1; // ERROR: CS0103
                                break;
                        }
                        // frmsolicitud.TxtSolActVivienda.Text = _rCred["ActVivienda"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActVehiculo.Text = _rCred["ActVehiculo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActOtros.Text = _rCred["ActOtros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActAporte.Text = _rCred["ActAportes"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtActCtaBanco.Text = _rCred["ActCajaBanco"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtActCxC.Text = _rCred["ActCxC"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActAhorros.Text = _rCred["ActAhorros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolActTotal.Text = _rCred["TotalAct"].ToString(); // ERROR: CS0103

                        // frmsolicitud.TxtSolPasDeuda.Text = _rCred["PasDeudas"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolPasOtros.Text = _rCred["PasOtros"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPasObliBanca.Text = _rCred["PasObliBanca"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtPasObliHipot.Text = _rCred["PasObliHipoteca"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolPasTotal.Text = _rCred["TotalPas"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolPatrimonio.Text = _rCred["Patrimonio"].ToString(); // ERROR: CS0103
                        // frmsolicitud.TxtSolPasTotPyP.Text = _rCred["TotalPyP"].ToString(); // ERROR: CS0103
                        // frmsolicitud.lblCapacidadRiesgovlr.Text = _rCred["CapitalRiesgo"].ToString(); // ERROR: CS0103
                        // frmsolicitud.lblCapacDescuento.Text = _rCred["capacdsto"].ToString(); // ERROR: CS0103
                        // frmsolicitud.lblPorcnDescuento.Text = _rCred["Porcdsto"].ToString(); // ERROR: CS0103
                        // frmsolicitud.EstadoSolicitud = _rCred["estado"].ToString(); // ERROR: CS0103
                        // frmsolicitud.Porc_Paga_Solic = Convert.ToDouble(_rCred["PorcentajePagaduria"]); // ERROR: CS0103
                        // frmsolicitud.tipodstoPagaduria = _rCred["tipodstoPagaduria"].ToString(); // ERROR: CS0103
                        // frmsolicitud.ciclo = Convert.ToInt32(_rCred["CICLOD"]); // ERROR: CS0103
                        // frmsolicitud.periodicidad = Convert.ToInt32(_rCred["PERIODD"]); // ERROR: CS0103
                        if (_rCred["estado"].ToString() == "A" || _rCred["estado"].ToString() == "G" || _rCred["estado"].ToString() == "D")
                        {
                            // frmsolicitud.GrpInfoAsociado.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoCredito.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.GrpInfoFamiliar.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoLaboral.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoGarantia.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpSolvencia.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.guardar.Enabled = false; // ERROR: CS0103
                        }
                        else if (_rCred["estado"].ToString() == "E")
                        {
                            // frmsolicitud.GrpInfoAsociado.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoCredito.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoFamiliar.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoLaboral.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoGarantia.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.imprimir.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.imprimelibra.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.GrpSolvencia.Enabled = false; // ERROR: CS0103
                        }
                        else if (_rCred["estado"].ToString() == "P" || _rCred["estado"].ToString() == "C")
                        {
                            // frmsolicitud.imprimir.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.guardar.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.imprimelibra.Enabled = true; // ERROR: CS0103
                        }
                        else if (_rCred["estado"].ToString() == "X")
                        {
                            // frmsolicitud.GrpInfoAsociado.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoCredito.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoFamiliar.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoLaboral.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpInfoGarantia.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.GrpSolvencia.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.guardar.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.imprimir.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.imprimelibra.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.cred_recoge.Enabled = false; // ERROR: CS0103
                            // frmsolicitud.cuotas_ext.Enabled = false; // ERROR: CS0103
                        }

                        if (dsdataset.Tables["TbldatosCredito"].Rows[0]["estado"].ToString() != "X")
                        {
                            // frmsolicitud.imprimir.Enabled = true; // ERROR: CS0103
                            // frmsolicitud.imprimelibra.Enabled = true; // ERROR: CS0103
                        }
                        // frmsolicitud.AbrirConexion(); // ERROR: CS0103
                        // frmsolicitud.CargarCodeudor1(); // ERROR: CS0103
                        // frmsolicitud.CargarCodeudor2(); // ERROR: CS0103
                        // frmsolicitud.CargarCodeudor3(); // ERROR: CS0103
                        // frmsolicitud.CargarCodeudor4(); // ERROR: CS0103
                        // this.BuscarExtras(Solicitud, dsdataset, myconnect); // ERROR: CS1620
                        // this.BuscarDeducciones(Solicitud, dsdataset, myconnect); // ERROR: CS1620
                        // frmsolicitud.DsDatosProyeccion = dsdataset; // ERROR: CS0103
                        // frmsolicitud.Invocado = true; //,, // ERROR: CS0103
                        // if (frmsolicitud.TxtCodeudor1.Text.Trim() != "") // ERROR: CS0103
                        {
                            // ook = this.BuscaDatosCodeudores(Solicitud, frmsolicitud.TxtCodeudor1.Text, myconnect, data); // ERROR: CS0103
                            if (ook)
                            {
                                DataRow _rc1 = data.Tables["TblDatCodeudor"].Rows[0];
                                // frmsolicitud.TxtCodeu1.Text = frmsolicitud.TxtCodeudor1.Text; // ERROR: CS0103
                                // frmsolicitud.TxtSalarioCode1.Text = _rc1["salario"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroIngCode1.Text = _rc1["OTROSINGRESOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngArrCode1.Text = _rc1["INGARRIENDO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngVarCode1.Text = _rc1["INGVARIABLE"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCode1.Text = _rc1["DEUDAEMP"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDeuTerCode1.Text = _rc1["DEUDTERCERO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroDstocode1.Text = _rc1["OTRODSTOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.LblDispCode1.Text = _rc1["DISPMES"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtPensionesCod1.Text = _rc1["IngPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoPensionCod1.Text = _rc1["DstoPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoParaFisCod1.Text = _rc1["DstoParafiscales"].ToString(); // ERROR: CS0103
                                switch (_rc1["cappagoPorcentaje"].ToString().Trim())
                                {
                                    case "Y":
                                        // frmsolicitud.chkGastoperCod1.Checked = true; // ERROR: CS0103
                                        break;
                                    case "N":
                                        // frmsolicitud.chkGastoperCod1.Checked = false; // ERROR: CS0103
                                        break;
                                }
                                // frmsolicitud.txtGastoperCod1.Text = _rc1["GastosPers"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCajaCode1.Text = _rc1["DeudaEmpCaja"].ToString(); // ERROR: CS0103
                                data.Tables["TblDatCodeudor"].Clear();
                            }
                        }
                        // if (frmsolicitud.TxtCodeudor2.Text.Trim() != "") // ERROR: CS0103
                        {
                            // ook = this.BuscaDatosCodeudores(Solicitud, frmsolicitud.TxtCodeudor2.Text, myconnect, data); // ERROR: CS0103
                            if (ook)
                            {
                                DataRow _rc2 = data.Tables["TblDatCodeudor"].Rows[0];
                                // frmsolicitud.TxtCodeu2.Text = frmsolicitud.TxtCodeudor2.Text; // ERROR: CS0103
                                // frmsolicitud.TxtSalarioCode2.Text = _rc2["salario"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroIngCode2.Text = _rc2["OTROSINGRESOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngArrCode2.Text = _rc2["INGARRIENDO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngVarCode2.Text = _rc2["INGVARIABLE"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCode2.Text = _rc2["DEUDAEMP"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDeuTerCode2.Text = _rc2["DEUDTERCERO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroDstocode2.Text = _rc2["OTRODSTOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.LblDispCode2.Text = _rc2["DISPMES"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtPensionesCod2.Text = _rc2["IngPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoPensionCod2.Text = _rc2["DstoPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoParaFisCod2.Text = _rc2["DstoParafiscales"].ToString(); // ERROR: CS0103
                                switch (_rc2["cappagoPorcentaje"].ToString().Trim())
                                {
                                    case "Y":
                                        // frmsolicitud.chkGastoperCod2.Checked = true; // ERROR: CS0103
                                        break;
                                    case "N":
                                        // frmsolicitud.chkGastoperCod2.Checked = false; // ERROR: CS0103
                                        break;
                                }
                                // frmsolicitud.txtGastoperCod2.Text = _rc2["GastosPers"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCajaCode2.Text = _rc2["DeudaEmpCaja"].ToString(); // ERROR: CS0103
                                data.Tables["TblDatCodeudor"].Clear();
                            }
                        }
                        // if (frmsolicitud.TxtCodeudor3.Text.Trim() != "") // ERROR: CS0103
                        {
                            // ook = this.BuscaDatosCodeudores(Solicitud, frmsolicitud.TxtCodeudor3.Text, myconnect, data); // ERROR: CS0103
                            if (ook)
                            {
                                DataRow _rc3 = data.Tables["TblDatCodeudor"].Rows[0];
                                // frmsolicitud.TxtCodeu3.Text = frmsolicitud.TxtCodeudor3.Text; // ERROR: CS0103
                                // frmsolicitud.TxtSalarioCode3.Text = _rc3["salario"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroIngCode3.Text = _rc3["OTROSINGRESOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngArrCode3.Text = _rc3["INGARRIENDO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngVarCode3.Text = _rc3["INGVARIABLE"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCode3.Text = _rc3["DEUDAEMP"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDeuTerCode3.Text = _rc3["DEUDTERCERO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroDstocode3.Text = _rc3["OTRODSTOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.LblDispCode3.Text = _rc3["DISPMES"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtPensionesCod3.Text = _rc3["IngPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoPensionCod3.Text = _rc3["DstoPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoParaFisCod3.Text = _rc3["DstoParafiscales"].ToString(); // ERROR: CS0103
                                switch (_rc3["cappagoPorcentaje"].ToString().Trim())
                                {
                                    case "Y":
                                        // frmsolicitud.chkGastoperCod3.Checked = true; // ERROR: CS0103
                                        break;
                                    case "N":
                                        // frmsolicitud.chkGastoperCod3.Checked = false; // ERROR: CS0103
                                        break;
                                }
                                // frmsolicitud.txtGastoperCod3.Text = _rc3["GastosPers"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCajaCode3.Text = _rc3["DeudaEmpCaja"].ToString(); // ERROR: CS0103
                                data.Tables["TblDatCodeudor"].Clear();
                            }
                        }
                        // if (frmsolicitud.TxtCodeudor4.Text.Trim() != "") // ERROR: CS0103
                        {
                            // ook = this.BuscaDatosCodeudores(Solicitud, frmsolicitud.TxtCodeudor4.Text, myconnect, data); // ERROR: CS0103
                            if (ook)
                            {
                                DataRow _rc4 = data.Tables["TblDatCodeudor"].Rows[0];
                                // frmsolicitud.TxtCodeu4.Text = frmsolicitud.TxtCodeudor4.Text; // ERROR: CS0103
                                // frmsolicitud.TxtSalarioCode4.Text = _rc4["salario"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroIngCode4.Text = _rc4["OTROSINGRESOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngArrCode4.Text = _rc4["INGARRIENDO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtIngVarCode4.Text = _rc4["INGVARIABLE"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCode4.Text = _rc4["DEUDAEMP"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDeuTerCode4.Text = _rc4["DEUDTERCERO"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtOtroDstocode4.Text = _rc4["OTRODSTOS"].ToString(); // ERROR: CS0103
                                // frmsolicitud.LblDispCode4.Text = _rc4["DISPMES"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtPensionesCod4.Text = _rc4["IngPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoPensionCod4.Text = _rc4["DstoPension"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoParaFisCod4.Text = _rc4["DstoParafiscales"].ToString(); // ERROR: CS0103
                                switch (_rc4["cappagoPorcentaje"].ToString().Trim())
                                {
                                    case "Y":
                                        // frmsolicitud.chkGastoperCod4.Checked = true; // ERROR: CS0103
                                        break;
                                    case "N":
                                        // frmsolicitud.chkGastoperCod4.Checked = false; // ERROR: CS0103
                                        break;
                                }
                                // frmsolicitud.txtGastoperCod4.Text = _rc4["GastosPers"].ToString(); // ERROR: CS0103
                                // frmsolicitud.TxtDstoEmpCajaCode4.Text = _rc4["DeudaEmpCaja"].ToString(); // ERROR: CS0103
                                data.Tables["TblDatCodeudor"].Clear();
                            }
                        }
                        // frmsolicitud.dsbienes = this.BuscarBienes(Solicitud, TipoBien.Todos, myconnect); // ERROR: CS0103
                        // frmsolicitud.DsReferencia = this.BuscarReferenciaSol(Solicitud, myconnect); // ERROR: CS0103
                        // this.BuscaSolParVivienda(Solicitud, myconnect, frmsolicitud.dsParViv); // ERROR: CS0103
                    }
                    break;
            }

            // OdbcConnect.LlenarVarini(varini); // ERROR: CS1620
            // frmsolicitud.TxtvalorSolicitud.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.TxtCuota.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.txtLincred.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.txtnombreLInea.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.txtTasaInt.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.TxtPlazo.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.TxtBaseCupo.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.TxtCupoDisponible.ReadOnly = true; // ERROR: CS0103
            // frmsolicitud.usuario.Text = Usuario; // ERROR: CS0103
            // frmsolicitud.empresappl.Text = varini.pstEmpresa; // ERROR: CS0103
            // frmsolicitud.Owner = myforma; // ERROR: CS0103
            // frmsolicitud.Show(); // ERROR: CS0103
        }

        // Overload: DespliegaSolicitud without ref (Optional ByRef Solicitud As Integer = -1 default)
        public void DespliegaSolicitud(DataSet dsdataset, string Usuario, OdbcConnection myconnect, Form myforma)
        {
            int solicitud = -1;
            DespliegaSolicitud(dsdataset, Usuario, myconnect, myforma, ref solicitud);
        }

        // VB line 2532
        private void GrabaDeduciones(double NumSolicitud, DataTable Dstbldeduciones, int Periodo, OdbcConnection myconnect)
        {
            int k = 0;
            int NumCuota = 0;
            double Saldo = 0;
            string TotPar = "T";
            double SaldoCapNom = 0, SaldoextNom = 0, SaldoCapCaj = 0, SaldoextCaj = 0;
            int OpRecDeuda = 0;

            string _p33str = "0";
            OdbcConnect.ExecuteQueryconec(
                "select num_solcred from sys_compania where codigo = '" + varini.sptCodEmpr + "'",
                myconnect, "GrabaDeduciones_dummy");
            // VB: Me.msgparsys.BuscarCompania(..., OpRecDeuda) — p33 = ref int
            {
                string _opRecDeudaStr = "0";
                // this.msgparsys.BuscarCompania(varini.sptCodEmpr, myconnect, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _p33str, ref _p33str, // ERROR: CS7036
                    // ref _p33str, ref _p33str, ref _opRecDeudaStr); // ERROR: CS7036
                int.TryParse(_opRecDeudaStr, out OpRecDeuda);
            }

            for (k = 0; k <= Dstbldeduciones.Rows.Count - 1; k++)
            {
                DataRow _row = Dstbldeduciones.Rows[k];
                NumCuota += 1;
                TotPar = "T";
                Saldo = 0;
                SaldoCapCaj = 0; SaldoextCaj = 0; SaldoCapNom = 0; SaldoextNom = 0;
                if (Convert.ToDouble(_row["valor"]) != 0 && _row["numero"].ToString() != "99999999")
                {
                    if (Convert.ToInt32(_row["lincred"]) >= 1000)
                    {
                        // this.msgcop.BuscaSaldoObligacion(_row["cedula"].ToString(), _row["lincred"].ToString(), // ERROR: CS1503
                            // Convert.ToDouble(_row["numero"]), Periodo, myconnect, ref Saldo); // ERROR: CS1503

                        switch (OpRecDeuda)
                        {
                            case 0:
                                if ((Convert.ToDouble(_row["valor"])) < Saldo) // + CDbl(.Item("interes"))
                                {
                                    TotPar = "P";
                                }
                                break;
                            case 1:
                                // this.msgcop.BuscaCuotasClades(_row["cedula"].ToString(), _row["lincred"].ToString(), // ERROR: CS1503
                                    // Convert.ToDouble(_row["numero"]), Periodo, // ERROR: CS1503
                                    // global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Caja, myconnect, // ERROR: CS1503
                                    // ref SaldoCapCaj, ref SaldoextCaj); // ERROR: CS1503
                                if ((Convert.ToDouble(_row["valor"]) + SaldoCapCaj + SaldoextCaj) < Saldo)
                                {
                                    TotPar = "P";
                                }
                                break;
                            case 2:
                                // this.msgcop.BuscaCuotasClades(_row["cedula"].ToString(), _row["lincred"].ToString(), // ERROR: CS1503
                                    // Convert.ToDouble(_row["numero"]), Periodo, // ERROR: CS1503
                                    // global::ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera.Cladesto.Nomina, myconnect, // ERROR: CS1503
                                    // ref SaldoCapNom, ref SaldoextNom); // ERROR: CS1503
                                if ((Convert.ToDouble(_row["valor"]) + SaldoCapNom + SaldoextNom) < Saldo)
                                {
                                    TotPar = "P";
                                }
                                break;
                        }
                    }
                    GrabaSolicitudDeduciones(NumSolicitud, _row["cedula"].ToString(), _row["lincred"].ToString(),
                        Convert.ToDouble(_row["numero"]), Convert.ToDouble(_row["valor"]),
                        Convert.ToDouble(_row["interes"]), TotPar, myconnect);
                }
            }
        }

        // VB line 2573
        private void GrabaSolicitudDeduciones(double NumSolicitud, string codigoter, string lincred, double numcredito,
            double valor, double Interes, string Total, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = BuscaSolicitudDeduciones(NumSolicitud, codigoter, lincred, numcredito, myconnect);
            if (!ok)
            {
                stbuilder.Append("insert into cop_solrecr (numero, codigoter, lincred, nume_cred,valor_pago,inte_adicional,totpar) values ('");
                stbuilder.Append(NumSolicitud + "','");
                stbuilder.Append(codigoter + "','");
                stbuilder.Append(lincred + "','");
                stbuilder.Append(numcredito + "','");
                stbuilder.Append(valor + "','");
                stbuilder.Append(Interes + "','");
                stbuilder.Append(Total + "')");
                OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaDeduciones");
            }
        }

        // VB line 2594
        public bool BuscaSolicitudDeduciones(double NumSolicitud, string codigoter, string lincred, double NumCredito, OdbcConnection myconnect)
        {
            stmysql = "select valor_pago from cop_solrecr where numero = '" + NumSolicitud + "' and codigoter = '" + codigoter + "' and lincred = '" + lincred + "' and nume_cred = '" + NumCredito + "'";
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaSolicitudDeduciones");
            return ok;
        }

        // VB line 2600
        private void EliminarCuotasExtras(double numsolicitud, OdbcConnection myconnect)
        {
            stmysql = "delete from cop_extrasoli where numero = " + numsolicitud;
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "EliminarCuotasExtras");
        }

        // VB line 2605
        public void GrabaCuotasExtras(double NumSolicitud, DataTable Dstblextras, OdbcConnection myconnect)
        {
            int k = 0;
            int NumCuota = 0;
            int contador = 0;
            EliminarCuotasExtras(NumSolicitud, myconnect);
            for (k = 0; k <= Dstblextras.Rows.Count - 1; k++)
            {
                DataRow _row = Dstblextras.Rows[k];
                if (Convert.ToDouble(_row["valor"]) != 0)
                {
                    contador += 1;
                }
            }

            for (k = 0; k <= Dstblextras.Rows.Count - 1; k++)
            {
                DataRow _row = Dstblextras.Rows[k];
                if (Convert.ToDouble(_row["valor"]) != 0)
                {
                    NumCuota += 1;
                    GrabaSolicitudCuotasextras(NumSolicitud, NumCuota, Convert.ToDouble(_row["valor"]),
                        Convert.ToInt32(_row["forpag"]), Convert.ToDateTime(_row["fecha"]),
                        _row["tipoextra"].ToString(), myconnect);
                }
                else if (contador == 0)
                {
                    GrabaSolicitudCuotasextras(NumSolicitud, 1, Convert.ToDouble(_row["valor"]),
                        Convert.ToInt32(_row["forpag"]), Convert.ToDateTime(_row["fecha"]),
                        _row["tipoextra"].ToString(), myconnect);
                }
            }
        }

        // VB line 2630
        private bool GrabaSolicitudCuotasextras(double Numsolicitud, int NumExtra, double Valor, int Clades, DateTime Fecha, string tipoextra, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            ok = this.BuscaSolicitudCuotasextras(Numsolicitud, NumExtra, myconnect);
            if (!ok)
            {
                stbuilder.Append("Insert into cop_extrasoli (numero,numero_cuota,fecha,valor,forma_pago,tipoextra)values ('");
                stbuilder.Append(Numsolicitud + "','");
                stbuilder.Append(NumExtra + "','");
                stbuilder.Append(Fecha.ToString(varini.PstForFec) + "','");
                stbuilder.Append(Valor + "','");
                stbuilder.Append(Clades + "','");
                stbuilder.Append(tipoextra + "')");
                ok = OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaSolicitudCuotasextras");
            }
            return ok;
        }

        // VB line 2650
        private bool BuscaSolicitudCuotasextras(double Numsolicitud, int NumExtra, OdbcConnection myconnect)
        {
            stmysql = "Select fecha,valor,forma_pago,tipoextra from cop_extrasoli where numero = '" + Numsolicitud + "' and numero_cuota = '" + NumExtra + "'";
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscaSolicitudCuotasextras");
            return ok;
        }

        // VB line 2656
        public bool BuscarSolicitud(double NroSolicitud, ref DataSet dsdata, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("select fecha_soli,codigoter,lincred,vlr_solicitud,tasa_int,plazo,cuota,pagos_mes,sal_mora,cupo_disponible ");
            stbuilder.Append(",fec_ingr_coop,empresa,cargo,salario,otro_ingreso,fec_ingr_empr,gasto_fijo_mes,cupo_dismes,tipo_contrato,tipo_garantia,");
            stbuilder.Append("descripcion,avaluo_ccial,avaluo_catastro,asegurado,por_seguro,fecven_seguro,codeudor1,codeudor2,codeudor3,estado,usuario,fecha_graba,");
            stbuilder.Append("conyu_labora,conyuge,empresa_labora,salario_me,tel_conyuge,dir_emp_conyu,");
            stbuilder.Append("ciudad_emp_conyu,per_acargo_conyu,vehiculo,casa_propia,tiene_vehiculo,");
            stbuilder.Append("FECDESC,NIT ,nit as cedula,CICLOD as ciclo,PERIODD as periodicidad,CLACUO ,CLASEI as claint,TASAADM ,TASASEG ,TASACPT ,");
            stbuilder.Append("VLRPRE_EXTRA as VlrVpnExtra,APORTES as SalAportes,AGENCIA ,CCOSTO as CentroCosto,CUOTA_ADM as CuotaAdm ,CUOTA_SEG as CuotaSeg,");
            stbuilder.Append("CUOTA_CPTL as CuotaCapital,CUOTA_ICIE as CuotaIcie,CUOTA_OTROS,TIP_INTCIE as TipoIncie,TIP_CAP as Tipocap,TIP_ADM as Tipoadm,TIP_SEG as Tiposeg,");
            stbuilder.Append("TIP_OTR as TipoOtro,FOR_ADM as foradm,CPTO_ADM as cptoadm,CPTO_SEG as cptoSeg,CPTO_OTR as cptoOtro, ");
            stbuilder.Append("TASAOTR as tasaotro,PERGRACIA,PERGRAINI,CICLO_PERGRACIA,CLADES,CUEX_INMES,CUEX_INANT,PAG1CUO,TIPPAG2,codeudor4,DSCTO_MES_EMP,IngVariables,IngArriendos,DeudasTerceros,numcdat,");
            stbuilder.Append("nitaseguradora,nombreaseguradora,numpoliza,matricula,valor_aprobado,EmpDsto,NumPagare,IngPension,DstoPension,DstoParafiscales,dscto_mes_emp_nomina,dtf,puntos,ingConyuge,dstoGastosPerso,");
            stbuilder.Append("cappagoPorcentaje,cappagoRecDeudas, ActVivienda , ActVehiculo , ActOtros , ActAportes , ActCajaBanco , ActCxC, ActAhorros, TotalAct ");
            stbuilder.Append(",PasDeudas,PasOtros,PasObliBanca,PasObliHipoteca,TotalPas,Patrimonio,TotalPyP,");
            stbuilder.Append("capacNomina,PorcNomina,CapPago,PorcCaja,PorcentajePagaduria,LblNomina,LblCaja,NivelEndeudamiento");
            stbuilder.Append(",NivelContingencia,CapitalRiesgo,capacdsto,Porcdsto,tipodstoPagaduria,CICLOD,PERIODD     from   cop_solcre  where numero=" + NroSolicitud);
            ok = OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarSolicitud", ref dsdata, "TbldatosCredito");
            return ok;
        }

        // VB line 2680 — Overridable overload (no ref DataSet)
        public virtual bool BuscarSolicitud(double NroSolicitud, OdbcConnection myconnect)
        {
            stmysql = "Select estado as campo1 from cop_solcre where numero=" + NroSolicitud;
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarSolicitud");
            return ok;
        }

        // VB line 2687 — GrabaSolicitud (enormous parameter list)
        private void GrabaSolicitud(
            ref double NumSolicitud, DateTime FechaSolicitud, string Codigoter, string Lincred,
            double ValorSolicitud, decimal TasaInt, int plazo, double cuota, double Pagos_mes,
            double salMora, double CupoDisponible, DateTime FechaIngCoop, string NomEmpresa, string Cargo,
            double Salario, double OtrosIngresos, DateTime fechaIngEmpresa, double GastoFijoMes,
            double CupoDispoMes, int TipoContracto, int TipoGarantia,
            string Descripcion, double AvaluoComercial, double AvaluoCatastro, string Asegurado,
            decimal PorSeguro, DateTime FecVenSeguro, string Codeudor1,
            string Codeudor2, string Codeudor3, string Codeudor4, string estado, string usuario,
            DateTime fecha_graba, string ConyugeLabora,
            string NombreConyuge, string EmpresaLabora, double SalrioConyuge, string TelefonoConyuge,
            string DirEmpConyuge, string CiudadEmpConyuge,
            int PersonasCargoConyuge, string Vehiculo, string CasaPropia, string TieneVehiculo,
            DateTime FecDescto, string Nit, int CicloDsto,
            Periodicidad Periodicidad, int Clacuo, int ClaseInt, decimal TasaAdm, decimal TasaSeg,
            decimal TasaCpt, double vlrpre_extra,
            double Aportes, string Agencia, string CentroCosto, double CuotaAdm, double CuotaSeg,
            double CuotaCapital, double CuotaIcie, double CuotaOtros, int TipoIntcie,
            int TipoCap, int TipoAdm, int TipoSeg, int TipoOtr, int ForAdm, int CptoAdm,
            int CptoSeg, int CptoOtr,
            decimal TasaOtr, int PeriodoGracia, int PerGraciaIni, string CicloPerGracia,
            int Clades, string CuexInMes, string CuexIntAnt, string Pag1cuo, string Tippag2,
            double DstoMesEmp,
            double IngVariables, double IngArriendos, double DeudasTerceros, double numcdat,
            string nitaseguradora, string nombreaseguradora, string numpoliza, string matricula,
            string EmpDsto, double NumPagare, double IngPension, double DstoPension,
            double DstoParafiscales, double dscto_mes_emp_nomina, double dtf, double puntos,
            OdbcConnection myconnect,
            double ingConyuge, double dstoGastosPerso, string cappagoPorcentaje,
            string cappagoRecDeudas,
            double ActVivienda, double ActVehiculo, double ActOtros, double ActAportes,
            double ActCajaBanco, double ActCxC, double ActAhorros, double TotalAct,
            double PasDeudas, double PasOtros, double PasObliBanca, double PasObliHipoteca,
            double TotalPas, double Patrimonio, double TotalPyP, double capacNomina,
            double PorcNomina, double CapPago, double PorcCaja, double PorcentajePagaduria,
            double LblNomina, double LblCaja, double Descubierto, double NivelEndeudamiento,
            double NivelContingencia, double CapitalRiesgo, double capacdsto, double Porcdsto,
            string tipodstoPagaduria)
        {
            StringBuilder stbuilder = new StringBuilder();
            double StNumPagare = 0;
            string StForPagare = "99";
            DataSet Dscompania = new DataSet();

            if (NumSolicitud == 0)
            {
                NumSolicitud = BuscaConseSolicitud(myconnect);
            }
            ok = this.BuscarSolicitud(NumSolicitud, myconnect);
            if (!ok)
            {
                this.msgparsys.BuscarCompania(varini.sptCodEmpr, Dscompania, myconnect);
                DataRow _rComp = Dscompania.Tables["tblcompania"].Rows[0];
                switch (_rComp["formapagare"].ToString())
                {
                    case "0":
                        StNumPagare = this.BuscaConsePagare(myconnect);
                        break;
                    case "2":
                        StNumPagare = NumPagare;
                        break;
                }

                stbuilder.Append("insert into cop_solcre (numero,fecha_soli,codigoter,lincred,vlr_solicitud,tasa_int,plazo,cuota,pagos_mes,sal_mora,cupo_disponible ");
                stbuilder.Append(",fec_ingr_coop,empresa,cargo,salario,otro_ingreso,fec_ingr_empr,gasto_fijo_mes,cupo_dismes,tipo_contrato,tipo_garantia,");
                stbuilder.Append("descripcion,avaluo_ccial,avaluo_catastro,asegurado,por_seguro,fecven_seguro,codeudor1,codeudor2,codeudor3,estado,usuario,fecha_graba,");
                stbuilder.Append("conyu_labora,conyuge,empresa_labora,salario_me,tel_conyuge,dir_emp_conyu,");
                stbuilder.Append("ciudad_emp_conyu,per_acargo_conyu,vehiculo,casa_propia,tiene_vehiculo,");
                stbuilder.Append("FECDESC,NIT ,CICLOD ,PERIODD ,CLACUO ,CLASEI ,TASAADM ,TASASEG ,TASACPT ,");
                stbuilder.Append("VLRPRE_EXTRA ,APORTES ,AGENCIA ,CCOSTO ,CUOTA_ADM  ,CUOTA_SEG  ,");
                stbuilder.Append("CUOTA_CPTL  ,CUOTA_ICIE ,CUOTA_OTROS,TIP_INTCIE,TIP_CAP ,TIP_ADM ,TIP_SEG ,");
                stbuilder.Append("TIP_OTR ,FOR_ADM ,CPTO_ADM ,CPTO_SEG ,CPTO_OTR , ");
                stbuilder.Append("TASAOTR,PERGRACIA,PERGRAINI,CICLO_PERGRACIA,CLADES,CUEX_INMES,CUEX_INANT,PAG1CUO,TIPPAG2,codeudor4,DSCTO_MES_EMP,IngVariables,");
                stbuilder.Append("IngArriendos,DeudasTerceros,numcdat,nitaseguradora,nombreaseguradora,numpoliza,EmpDsto,matricula,NumPagare,IngPension,DstoPension,DstoParafiscales,dscto_mes_emp_nomina,");
                stbuilder.Append("tasa_int_sol,plazo_sol,periodd_sol,ciclod_sol,clades_sol,dtf,puntos,cuota_sol,ingConyuge,dstoGastosPerso,cappagoPorcentaje,cappagoRecDeudas, ");
                stbuilder.Append("ActVivienda , ActVehiculo , ActOtros , ActAportes , ActCajaBanco , ActCxC, ActAhorros, TotalAct ");
                stbuilder.Append(",PasDeudas , PasOtros, PasObliBanca, PasObliHipoteca, TotalPas, Patrimonio , TotalPyP,capacNomina,PorcNomina,CapPago,PorcCaja,PorcentajePagaduria,LblNomina,");
                stbuilder.Append("LblCaja,Descubierto,NivelEndeudamiento,NivelContingencia,CapitalRiesgo,capacdsto,Porcdsto,tipodstoPagaduria,dtf_sol,puntos_sol)  values ('");
                stbuilder.Append(NumSolicitud + "','");
                stbuilder.Append(FechaSolicitud.ToString(varini.PstForFec) + "','");
                stbuilder.Append(Codigoter + "','");
                stbuilder.Append(Lincred + "','");
                stbuilder.Append(ValorSolicitud + "','");
                stbuilder.Append(TasaInt + "','");
                stbuilder.Append(plazo + "','");
                stbuilder.Append(cuota + "','");
                stbuilder.Append(Pagos_mes + "','");
                stbuilder.Append(salMora + "','");
                stbuilder.Append(CupoDisponible + "','");
                stbuilder.Append(FechaIngCoop.ToString(varini.PstForFec) + "','");
                stbuilder.Append(NomEmpresa + "','");
                stbuilder.Append(Cargo + "','");
                stbuilder.Append(Salario + "','");
                stbuilder.Append(OtrosIngresos + "','");
                stbuilder.Append(fechaIngEmpresa.ToString(varini.PstForFec) + "','");
                stbuilder.Append(GastoFijoMes + "','");
                stbuilder.Append(CupoDispoMes + "','");
                stbuilder.Append(TipoContracto + "','");
                stbuilder.Append(TipoGarantia + "','");
                stbuilder.Append(Descripcion + "','");
                stbuilder.Append(AvaluoComercial + "','");
                stbuilder.Append(AvaluoCatastro + "','");
                stbuilder.Append(Asegurado + "','");
                stbuilder.Append(PorSeguro + "','");
                stbuilder.Append(FecVenSeguro.ToString(varini.PstForFec) + "','");
                stbuilder.Append(Codeudor1 + "','");
                stbuilder.Append(Codeudor2 + "','");
                stbuilder.Append(Codeudor3 + "','");
                stbuilder.Append(estado + "','");
                stbuilder.Append(usuario + "','");
                stbuilder.Append(fecha_graba.ToString(varini.PstForFec) + "','");
                stbuilder.Append(ConyugeLabora + "','");
                stbuilder.Append(NombreConyuge + "','");
                stbuilder.Append(EmpresaLabora + "','");
                stbuilder.Append(SalrioConyuge + "','");
                stbuilder.Append(TelefonoConyuge + "','");
                stbuilder.Append(DirEmpConyuge + "','");
                stbuilder.Append(CiudadEmpConyuge + "','");
                stbuilder.Append(PersonasCargoConyuge + "','");
                stbuilder.Append(Vehiculo + "','");
                stbuilder.Append(CasaPropia + "','");
                stbuilder.Append(TieneVehiculo + "','");
                stbuilder.Append(FecDescto.ToString(varini.PstForFec) + "','");
                stbuilder.Append(Nit + "','");
                stbuilder.Append(CicloDsto + "','");
                stbuilder.Append(Periodicidad + "','");
                stbuilder.Append(Clacuo + "','");
                stbuilder.Append(ClaseInt + "','");
                stbuilder.Append(TasaAdm + "','");
                stbuilder.Append(TasaSeg + "','");
                stbuilder.Append(TasaCpt + "','");
                stbuilder.Append(vlrpre_extra + "','");
                stbuilder.Append(Aportes + "','");
                stbuilder.Append(Agencia + "','");
                stbuilder.Append(CentroCosto + "','");
                stbuilder.Append(CuotaAdm + "','");
                stbuilder.Append(CuotaSeg + "','");
                stbuilder.Append(CuotaCapital + "','");
                stbuilder.Append(CuotaIcie + "','");
                stbuilder.Append(CuotaOtros + "','");
                stbuilder.Append(TipoIntcie + "','");
                stbuilder.Append(TipoCap + "','");
                stbuilder.Append(TipoAdm + "','");
                stbuilder.Append(TipoSeg + "','");
                stbuilder.Append(TipoOtr + "','");
                stbuilder.Append(ForAdm + "','");
                stbuilder.Append(CptoAdm + "','");
                stbuilder.Append(CptoSeg + "','");
                stbuilder.Append(CptoOtr + "','");
                stbuilder.Append(TasaOtr + "','");
                stbuilder.Append(PeriodoGracia + "','");
                stbuilder.Append(PerGraciaIni + "','");
                stbuilder.Append(CicloPerGracia + "','");
                stbuilder.Append(Clades + "','");
                stbuilder.Append(CuexInMes + "','");
                stbuilder.Append(CuexIntAnt + "','");
                stbuilder.Append(Pag1cuo + "','");
                stbuilder.Append(Tippag2 + "','");
                stbuilder.Append(Codeudor4 + "','");
                stbuilder.Append(DstoMesEmp + "',");
                stbuilder.Append(IngVariables + ",");
                stbuilder.Append(IngArriendos + ",");
                stbuilder.Append(DeudasTerceros + ",");
                stbuilder.Append(numcdat + ",'");
                stbuilder.Append(nitaseguradora + "','");
                stbuilder.Append(nombreaseguradora + "','");
                stbuilder.Append(numpoliza + "','");
                stbuilder.Append(EmpDsto + "','");
                stbuilder.Append(matricula + "','");
                stbuilder.Append(StNumPagare + "',");
                stbuilder.Append(IngPension + ",");
                stbuilder.Append(DstoPension + ",");
                stbuilder.Append(DstoParafiscales + ",");
                stbuilder.Append(dscto_mes_emp_nomina + ",'");
                stbuilder.Append(TasaInt + "','");
                stbuilder.Append(plazo + "','");
                stbuilder.Append(Periodicidad + "','");
                stbuilder.Append(CicloDsto + "','");
                stbuilder.Append(Clades + "','");
                stbuilder.Append(dtf + "','");
                stbuilder.Append(puntos + "','");
                stbuilder.Append(cuota + "', ");
                stbuilder.Append(ingConyuge + ",");
                stbuilder.Append(dstoGastosPerso + ",'");
                stbuilder.Append(cappagoPorcentaje + "','");
                stbuilder.Append(cappagoRecDeudas + "',");
                stbuilder.Append(ActVivienda + ",");
                stbuilder.Append(ActVehiculo + ",");
                stbuilder.Append(ActOtros + ",");
                stbuilder.Append(ActAportes + ",");
                stbuilder.Append(ActCajaBanco + ",");
                stbuilder.Append(ActCxC + ",");
                stbuilder.Append(ActAhorros + ",");
                stbuilder.Append(TotalAct + ",");
                stbuilder.Append(PasDeudas + ",");
                stbuilder.Append(PasOtros + ",");
                stbuilder.Append(PasObliBanca + ",");
                stbuilder.Append(PasObliHipoteca + ",");
                stbuilder.Append(TotalPas + ",");
                stbuilder.Append(Patrimonio + ",");
                stbuilder.Append(TotalPyP + ",");
                stbuilder.Append(capacNomina + ",");
                stbuilder.Append(PorcNomina + ",");
                stbuilder.Append(CapPago + ",");
                stbuilder.Append(PorcCaja + ",");
                stbuilder.Append(PorcentajePagaduria + ",");
                stbuilder.Append(LblNomina + ",");
                stbuilder.Append(LblCaja + ",'");
                stbuilder.Append(Descubierto + "','");
                stbuilder.Append(NivelEndeudamiento + "','");
                stbuilder.Append(NivelContingencia + "','");
                stbuilder.Append(CapitalRiesgo + "','" + capacdsto + "','" + Porcdsto + "','" + tipodstoPagaduria + "','");
                stbuilder.Append(dtf + "','");
                stbuilder.Append(puntos + "')");
            }
            else
            {
                stbuilder.Append("update cop_solcre set fecha_soli='" + FechaSolicitud.ToString(varini.PstForFec) + "',codigoter='" + Codigoter + "',lincred='" + Lincred + "',");
                stbuilder.Append("vlr_solicitud=" + ValorSolicitud + ",tasa_int=" + TasaInt + ",plazo=" + plazo + ",cuota=" + cuota + ",pagos_mes=" + Pagos_mes + ",");
                stbuilder.Append("sal_mora=" + salMora + ",cupo_disponible=" + CupoDisponible + ",fec_ingr_coop='" + FechaIngCoop.ToString(varini.PstForFec) + "',");
                stbuilder.Append("empresa='" + NomEmpresa + "', Cargo='" + Cargo + "', Salario=" + Salario + ", otro_ingreso=" + OtrosIngresos + ",");
                stbuilder.Append("fec_ingr_empr='" + fechaIngEmpresa.ToString(varini.PstForFec) + "', gasto_fijo_mes=" + GastoFijoMes + ", cupo_dismes=" + CupoDispoMes + ",");
                stbuilder.Append("tipo_contrato='" + TipoContracto + "', tipo_garantia='" + TipoGarantia + "',descripcion='" + Descripcion + "',");
                stbuilder.Append("avaluo_ccial=" + AvaluoComercial + ", avaluo_catastro=" + AvaluoCatastro + ", Asegurado='" + Asegurado + "', por_seguro=" + PorSeguro + ",");
                stbuilder.Append("fecven_seguro='" + FecVenSeguro.ToString(varini.PstForFec) + "', Codeudor1='" + Codeudor1 + "', Codeudor2='" + Codeudor2 + "', Codeudor3='" + Codeudor3 + "',");
                stbuilder.Append("estado='" + estado + "', usuario='" + usuario + "', fecha_graba='" + fecha_graba.ToString(varini.PstForFec) + "',");
                stbuilder.Append("conyu_labora='" + ConyugeLabora + "',conyuge='" + NombreConyuge + "',empresa_labora='" + EmpresaLabora + "',salario_me='" + SalrioConyuge + "',");
                stbuilder.Append("tel_conyuge='" + TelefonoConyuge + "', dir_emp_conyu='" + DirEmpConyuge + "',ciudad_emp_conyu='" + CiudadEmpConyuge + "',");
                stbuilder.Append("per_acargo_conyu='" + PersonasCargoConyuge + "', Vehiculo='" + Vehiculo + "', casa_propia='" + CasaPropia + "', tiene_vehiculo='" + TieneVehiculo + "',");
                stbuilder.Append("FECDESC='" + FecDescto.ToString(varini.PstForFec) + "',NIT='" + Nit + "',CICLOD='" + CicloDsto + "',PERIODD='" + Periodicidad + "',CLACUO='" + Clacuo + "',");
                stbuilder.Append("CLASEI='" + ClaseInt + "', TasaAdm='" + TasaAdm + "', TasaSeg='" + TasaSeg + "', TasaCpt='" + TasaCpt + "',");
                stbuilder.Append("VLRPRE_EXTRA='" + vlrpre_extra + "',APORTES='" + Aportes + "',AGENCIA='" + Agencia + "',CCOSTO='" + CentroCosto + "',");
                stbuilder.Append("CUOTA_ADM='" + CuotaAdm + "', CUOTA_SEG='" + CuotaSeg + "',CUOTA_CPTL='" + CuotaCapital + "',CUOTA_ICIE='" + CuotaIcie + "',");
                stbuilder.Append("CUOTA_OTROS='" + CuotaOtros + "', TIP_INTCIE='" + TipoIntcie + "', TIP_CAP='" + TipoCap + "',TIP_ADM='" + TipoAdm + "',TIP_SEG='" + TipoSeg + "',");
                stbuilder.Append("TIP_OTR='" + TipoOtr + "',FOR_ADM='" + ForAdm + "',CPTO_ADM='" + CptoAdm + "',CPTO_SEG='" + CptoSeg + "',CPTO_OTR='" + CptoOtr + "',");
                stbuilder.Append("TASAOTR='" + TasaOtr + "',PERGRACIA='" + PeriodoGracia + "',PERGRAINI='" + PerGraciaIni + "',CICLO_PERGRACIA='" + CicloPerGracia + "',");
                stbuilder.Append("CLADES='" + Clades + "',CUEX_INMES='" + CuexInMes + "',CUEX_INANT='" + CuexIntAnt + "',PAG1CUO='" + Pag1cuo + "',");
                stbuilder.Append("TIPPAG2='" + Tippag2 + "',codeudor4='" + Codeudor4 + "',DSCTO_MES_EMP=" + DstoMesEmp + ",IngVariables=" + IngVariables + ",");
                stbuilder.Append("IngArriendos=" + IngArriendos + ",DeudasTerceros=" + DeudasTerceros + ",numcdat=" + numcdat + ",");
                stbuilder.Append("empdsto = '" + EmpDsto + "',NumPagare='" + NumPagare + "',");
                stbuilder.Append("nitaseguradora='" + nitaseguradora + "',nombreaseguradora='" + nombreaseguradora + "',numpoliza='" + numpoliza + "',matricula='" + matricula + "',");
                stbuilder.Append("IngPension=" + IngPension + ",DstoPension=" + DstoPension + ",DstoParafiscales=" + DstoParafiscales + ",dscto_mes_emp_nomina=" + dscto_mes_emp_nomina + ",");
                stbuilder.Append("tasa_int_sol=" + TasaInt + ",plazo_sol=" + plazo + ",periodd_sol='" + Periodicidad + "',ciclod_sol='" + CicloDsto + "',clades_sol='" + Clades + "',dtf='" + dtf + "',puntos='" + puntos + "',cuota_sol='" + cuota + "', ");
                stbuilder.Append("ingConyuge = " + ingConyuge + ",dstoGastosPerso=" + dstoGastosPerso + ",cappagoPorcentaje='" + cappagoPorcentaje + "',cappagoRecDeudas='" + cappagoRecDeudas + "',");
                stbuilder.Append("ActVivienda =" + ActVivienda + ",ActVehiculo=" + ActVehiculo + ",ActOtros=" + ActOtros + ",ActAportes=" + ActAportes + ",ActCajaBanco=" + ActCajaBanco + ",ActCxC=" + ActCxC + ",ActAhorros=" + ActAhorros + ",");
                stbuilder.Append("TotalAct=" + TotalAct + ",PasDeudas=" + PasDeudas + ",PasOtros=" + PasOtros + ",PasObliBanca=" + PasObliBanca + ",PasObliHipoteca=" + PasObliHipoteca + ",TotalPas=" + TotalPas + ",Patrimonio=" + Patrimonio + ",");
                stbuilder.Append("TotalPyP=" + TotalPyP + ",capacNomina =" + capacNomina + ",PorcNomina =" + PorcNomina + ",CapPago=" + CapPago + ",PorcCaja=" + PorcCaja + ",PorcentajePagaduria=" + PorcentajePagaduria + ",LblNomina=" + LblNomina + ",");
                stbuilder.Append("LblCaja=" + LblCaja + ",Descubierto=" + Descubierto + ",NivelEndeudamiento=" + NivelEndeudamiento + ",NivelContingencia=" + NivelContingencia + ",CapitalRiesgo=" + CapitalRiesgo + ",");
                stbuilder.Append("capacdsto=" + capacdsto + ",Porcdsto=" + Porcdsto + ",tipodstoPagaduria='" + tipodstoPagaduria + "',dtf_sol='" + dtf + "',puntos_sol='" + puntos + "'  where numero = " + NumSolicitud);
            }
            ok = OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabaSolicitud");
        }

        // VB line 2914
        ~ClsLiqcreditos()
        {
        }

        // VB line 2917
        public double CalcularCupoAportes(string codigoter, string periodo, OdbcConnection myconnect,
            ref double BaseAportes, ref double CantVecesPresta)
        {
            double PorAfecta = 0, VecesPresta = 0;
            int CantidadReg = 0, i = 0;
            double saldo = 0;
            double CupoTotal = 0, presta = 0, base_val = 0;
            StringBuilder stbuilder = new StringBuilder();
            double cesantias = 0;

            stbuilder.Append("select car12.cupoap, car12.tasai, salmae.saldo,maenit.cesantias ");
            stbuilder.Append("from cop_concar12 car12 ");
            stbuilder.Append("left join cop_salmaecar salmae on salmae.codigoter='" + codigoter + "' and car12.lincred=salmae.lincred and salmae.periodo=" + periodo + " ");
            stbuilder.Append("inner join sys_maenit maenit on salmae.codigoter=maenit.codigoter ");
            stbuilder.Append("where car12.lincred <1000 and salmae.saldo <> 0 and car12.cupoap > 0 order by car12.lincred");

            DataSet DtDatos = new DataSet();
            OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "CalcularCupoAportes", ref DtDatos, "CupoAportes");
            CantidadReg = DtDatos.Tables["CupoAportes"].Rows.Count;

            while (i < CantidadReg)
            {
                DataRowCollection _rows = DtDatos.Tables["CupoAportes"].Rows;
                //cesantias = _rows[i]["cesantias"];
                VecesPresta = Convert.ToDouble(_rows[i]["cupoap"]);
                PorAfecta = Convert.ToDouble(_rows[i]["tasai"]);

                if (DBNull.Value.Equals(_rows[i]["saldo"]))
                {
                    saldo = 0;
                }
                else
                {
                    double _saldoVal = Convert.ToDouble(_rows[i]["saldo"]);
                    saldo = _saldoVal < 0 ? _saldoVal * -1 : _saldoVal;
                }

                base_val += (saldo * PorAfecta / 100);
                presta += VecesPresta;
                CupoTotal += ((saldo * PorAfecta / 100) * (VecesPresta == 0 ? 1 : VecesPresta));
                i = i + 1;
            }
            CupoTotal += cesantias;
            BaseAportes = base_val + cesantias;
            CantVecesPresta = presta;
            return CupoTotal;
        }

        // Overload: CalcularCupoAportes without optional refs
        public double CalcularCupoAportes(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double ba = 0, cv = 0;
            return CalcularCupoAportes(codigoter, periodo, myconnect, ref ba, ref cv);
        }

        // VB line 2962
        public void CargarDataset(ref DataSet dtdata)
        {
            int a = 0;
            double b = 0;
            dtdata.Tables.Add("TblCupo");
            DataColumnCollection cols = dtdata.Tables["TblCupo"].Columns;
            cols.Add("Lincred", a.GetType());
            cols.Add("CupoTotal", b.GetType());
            cols.Add("Saldo", b.GetType());
            cols.Add("Disponible", b.GetType());
            cols.Add("Respaldo", b.GetType());
            cols.Add("AporteReal", b.GetType());
            cols.Add("DisponibleReal", b.GetType());
            cols.Add("RespaldoReal", b.GetType());
        }

        // VB line 2976
        public void LimpiaDataset(DataSet dtdata)
        {
            dtdata.Tables["TblCupo"].Rows.Clear();
        }

        // VB line 2980
        public DataSet CalcularCupoLineas(string codigoter, string periodo, OdbcConnection myconnect)
        {
            DataSet dtdata = new DataSet();
            int i = 0, CantidadReg = 0;
            double baseaportes = 0;
            double saldo = 0, veces = 0, cupoTotal = 0, Disponible = 0;
            double respaldo = 0, dispReal = 0, AporReal = 0, RespReal = 0;

            CargarDataset(ref dtdata);

            stmysql = "select a.lincred,a.cupoap, a.tasai, sum(b.saldo) as saldo from cop_concar12 a left join cop_salmaecar b on b.codigoter='" + codigoter + "' and a.lincred=b.lincred and b.periodo=" + periodo +
                      " where a.lincred >999 and a.afecta='Y' group by a.lincred,a.cupoap, a.tasai order by a.lincred";

            DataSet DtDatos = new DataSet();
            OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "CalcularCupoLineas", ref DtDatos, "CupoAportesLinea");
            CantidadReg = DtDatos.Tables["CupoAportesLinea"].Rows.Count;

            this.CalcularCupoAportes(codigoter, periodo, myconnect, ref baseaportes, ref RespReal); // CantVecesPresta discarded
            baseaportes = baseaportes; // result stored above
            // Re-call to get only baseaportes cleanly
            {
                double _cv = 0;
                CalcularCupoAportes(codigoter, periodo, myconnect, ref baseaportes, ref _cv);
            }
            AporReal = this.CalculaSaldoAportes(codigoter, periodo, myconnect);

            while (i < CantidadReg)
            {
                DataRowCollection _rows = DtDatos.Tables["CupoAportesLinea"].Rows;
                saldo = DBNull.Value.Equals(_rows[i]["saldo"]) ? 0 : Convert.ToDouble(_rows[i]["saldo"]);
                veces = Convert.ToDouble(_rows[i]["cupoap"]);
                cupoTotal = baseaportes * veces;
                Disponible = cupoTotal - saldo;
                dispReal = AporReal - saldo;
                if (cupoTotal != 0)
                {
                    respaldo = (saldo / cupoTotal) * 100;
                }
                else
                {
                    respaldo = 0;
                }
                if (AporReal != 0)
                {
                    RespReal = (saldo / AporReal) * 100;
                }
                else
                {
                    RespReal = 0;
                }
                dtdata.Tables["TblCupo"].Rows.Add(_rows[i]["lincred"], cupoTotal, saldo, Disponible, respaldo, AporReal, dispReal, RespReal);
                i = i + 1;
            }
            return dtdata;
        }

        // VB line 3024
        public double CalculaTotalDeuda(string codigoter, string periodo, OdbcConnection myconnect, string AfectaCupo = "Y")
        {
            double saldo = 0, Pendientes = 0;
            StringBuilder StBuilder = new StringBuilder();

            StBuilder.Append("select sum(a.saldo) as campo1 ");
            StBuilder.Append("from cop_salmaecar a ");
            StBuilder.Append("inner join cop_concar12 b on a.lincred=b.lincred ");
            StBuilder.Append("where a.codigoter='" + codigoter + "' and a.periodo=" + periodo + (AfectaCupo == "Y" ? " and b.afecta='Y'" : ""));
            StBuilder.Append(" and a.lincred>999 and b.codahor in ('4','5') and a.saldo>0");

            {
                string _s1 = "0";
                string _dummy1 = "0", _dummy2 = "0", _dummy3 = "0";
                OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "CalculaTotalDeuda", ref _s1, ref _dummy1, ref _dummy2, ref _dummy3);
                double.TryParse(_s1, out saldo);
            }

            StBuilder.Replace(StBuilder.ToString(), "");
            StBuilder.Append("select sum(c.saldointeres+c.saldomora+c.saldoseguro+c.saldoadmon+c.saldootros) as campo1 ");
            StBuilder.Append("from cop_salmaecar a ");
            StBuilder.Append("inner join cop_concar12 b on a.lincred=b.lincred ");
            StBuilder.Append("left join cop_copmora c on a.codigoter=c.codigoter and a.lincred=c.lincred and a.numero=c.numero and a.periodo=c.periodo_contable ");
            StBuilder.Append("and (c.saldointeres>0 or c.saldomora>0 or c.saldoseguro>0 or c.saldoadmon>0 or c.saldootros>0)");
            StBuilder.Append("where a.codigoter='" + codigoter + "' and a.periodo=" + periodo + (AfectaCupo == "Y" ? " and b.afecta='Y'" : ""));
            StBuilder.Append(" and a.lincred>999 and b.codahor in ('4','5') and a.saldo>0");

            {
                string _s2 = "0";
                string _dummy1 = "0", _dummy2 = "0", _dummy3 = "0";
                OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "CalculaTotalDeuda", ref _s2, ref _dummy1, ref _dummy2, ref _dummy3);
                double.TryParse(_s2, out Pendientes);
            }

            saldo += Pendientes;
            return saldo;
        }

        // VB line 3054
        public double CalculaSaldoAportes(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double saldo = 0;
            stmysql = "select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor='1' " +
                "where sal.codigoter='" + codigoter + "' and sal.periodo=" + periodo + " group by concar12.codahor";
            {
                string _s = "0";
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaSaldoAportes", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3062
        public double CalculaAporteGarantizado(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double saldo = 0;
            //stmysql = "select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor='1' " + ...
            stmysql = "select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor in ('1','2') and concar12.cupoap > 0  " +
                "where sal.codigoter='" + codigoter + "' and sal.periodo=" + periodo;
            {
                string _s = "0";
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaAporteGarantizado", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3071
        public double CalculaSaldoCesantias(string codigoter, OdbcConnection myconnect)
        {
            double saldo = 0;
            stmysql = "select cesantias as campo1 from sys_maenit where codigoter='" + codigoter + "'";
            {
                string _s = "0";
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaSaldoAportes", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3078
        public double CalculaSaldoAhoCtrtualPermanentes(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double saldo = 0;
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor = '2' ");
            stbuilder.Append("where sal.codigoter='" + codigoter + "' and (concar12.cuenta like '2130%' or cuenta like '2125%') and sal.periodo ='" + periodo + "'");
            {
                string _s = "0";
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "CalculaSaldoAhoCtrtualPermanentes", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3091
        public double CalculaDeducciones(string codigoter, string periodo, OdbcConnection myconnect,
            ref double Nomina, ref double Caja)
        {
            double deducciones = 0;
            StringBuilder stbuilder = new StringBuilder();
            string cast;
            switch (varini.pstTipoBD.ToUpper())
            {
                case "POSTGRES":
                    cast = "cast(case b.CICLOD when '5' then b.periodd else '1' end as integer)";
                    break;
                default:
                    cast = "case b.CICLOD when '5' then b.periodd else '1' end";
                    break;
            }

            stbuilder.Append("select sum(case when (a.lincred>999 and b.saldo>0) or (a.lincred<1000) then (b.cuota * " + cast + " ) else 0 end) as campo1 ");
            stbuilder.Append("from cop_maecar a ");
            stbuilder.Append("left join cop_salmaecar b on a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero and b.periodo=" + periodo);
            stbuilder.Append(" inner join cop_concar12 c on a.lincred=c.lincred and c.compri='Y' ");
            stbuilder.Append("where a.codigoter='" + codigoter + "' and b.clades='1' and c.codnom<>'1' ");

            {
                string _sNom = "0";
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "CalculaDeducciones", ref _sNom, ref _d1, ref _d2, ref _d3);
                double.TryParse(_sNom, out Nomina);
            }

            stbuilder.Replace(stbuilder.ToString(), "");
            stbuilder.Append("select sum(case when (a.lincred>999 and b.saldo>0) or (a.lincred<1000) then (b.cuota * " + cast + ") else 0 end) as campo1 ");
            stbuilder.Append("from cop_maecar a ");
            stbuilder.Append("left join cop_salmaecar b on a.codigoter=b.codigoter and a.lincred=b.lincred and a.numero=b.numero and b.periodo=" + periodo);
            stbuilder.Append(" inner join cop_concar12 c on a.lincred=c.lincred and c.compri='Y' ");
            stbuilder.Append("where a.codigoter='" + codigoter + "' and b.clades='2' and c.codnom<>'1' ");

            {
                string _sCaj = "0";
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "CalculaDeducciones", ref _sCaj, ref _d1, ref _d2, ref _d3);
                double.TryParse(_sCaj, out Caja);
            }

            deducciones = Nomina + Caja;
            return deducciones;
        }

        // Overload without optional refs
        public double CalculaDeducciones(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double n = 0, c = 0;
            return CalculaDeducciones(codigoter, periodo, myconnect, ref n, ref c);
        }

        // VB line 3127
        public ClsLiqcreditos()
        {
            OdbcConnect.MyOdbcConect(varini);
        }

        // VB line 3131
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

        // VB line 3149
        public bool BuscarExtras(int NumSolicitud, ref DataSet dsdata, OdbcConnection myconnect)
        {
            stmysql = "select fecha,valor,case forma_pago when '1' then 'Nomina' when '2' then 'Caja' else ' ' end as DescPag," +
                    "forma_pago as Forpag,tipoextra from cop_extrasoli where numero=" + NumSolicitud;
            ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarExtras", ref dsdata, "tblextras");
            return ok;
        }

        // VB line 3156 — Overridable overload returning DataTable
        public virtual DataTable BuscarExtras(int NumSolicitud, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select numero_cuota,fecha,valor,forma_pago,tipoextra " +
                      "from cop_extrasoli where numero=" + NumSolicitud;
            ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarExtras", ref dsdata, "tblextras");
            return dsdata.Tables["tblextras"];
        }

        // VB line 3164 — Overridable overload with codigoter/lincred/numero/Periodo
        public virtual bool BuscarExtras(string codigoter, int lincred, double numero, string Periodo, ref DataSet DsDataSet, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();

            try
            {
                DsDataSet.Tables.Remove("tblextras");
            }
            catch (Exception)
            {
            }

            StBuilder.Append("select extras.codigoter,extras.lincred,extras.numero,extras.forma_pago,fecha_pago as fecha,salextras.saldo as valor from cop_extras extras inner join cop_salextras salextras ");
            StBuilder.Append("on extras.codigoter = salextras.codigoter and extras.lincred = salextras.lincred ");
            StBuilder.Append("and extras.numero = salextras.numero and extras.num_extra = salextras.num_extra and salextras.periodo = '" + Periodo + "' ");
            StBuilder.Append("where extras.codigoter = '" + codigoter + "' and extras.lincred = '" + lincred + "' and extras.numero = '" + numero + "' and salextras.saldo <> 0");

            OdbcConnect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "BuscarExtras", ref DsDataSet, "tblextras");

            if (DsDataSet.Tables["tblextras"].Rows.Count > 0)
            {
                try
                {
                    DsDataSet.Tables.Add(DsDataSet.Tables["tblextras"].Copy());
                }
                catch (Exception)
                {
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        // VB line 3194
        public bool BuscarDeducciones(int NumSolicitud, ref DataSet dsdata, OdbcConnection myconnect)
        {
            stmysql = "select a.codigoter as cedula,a.lincred,a.nume_cred as numero,b.descripcion,a.valor_pago as valor, a.inte_adicional as interes," +
                    " totpar as total,(a.valor_pago+a.inte_adicional) as TotalDeducir from cop_solrecr a inner join cop_concar12 b on a.lincred=b.lincred where numero = " + NumSolicitud;
            ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarDeducciones", ref dsdata, "tbldeducciones");
            return ok;
        }

        // VB line 3200
        public bool ActualizarGarantiasSolicitud(double solicitud, double numcredito, string usuario, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            string Matricula, numpoliza, nitaseguradora, nombreaseguradora;
            DateTime feccancelacion = DateTime.Now;
            ok = this.BuscarSolicitud(solicitud, ref dsdata, myconnect);
            if (ok)
            {
                DataRow _row = dsdata.Tables["TbldatosCredito"].Rows[0];
                feccancelacion = feccancelacion.AddMonths(Convert.ToInt32(_row["plazo"]));
                if (_row["matricula"] is DBNull)
                {
                    Matricula = " ";
                }
                else
                {
                    Matricula = _row["matricula"].ToString();
                }

                if (_row["numpoliza"] is DBNull)
                {
                    numpoliza = " ";
                }
                else
                {
                    numpoliza = _row["numpoliza"].ToString();
                }

                if (_row["nitaseguradora"] is DBNull)
                {
                    nitaseguradora = " ";
                }
                else
                {
                    nitaseguradora = _row["nitaseguradora"].ToString();
                }

                if (_row["nombreaseguradora"] is DBNull)
                {
                    nombreaseguradora = " ";
                }
                else
                {
                    nombreaseguradora = _row["nombreaseguradora"].ToString();
                }

                // ok = this.msgcop.GrabarGarantia(_row["codigoter"].ToString(), _row["lincred"].ToString(), // ERROR: CS1503
                    // numcredito, Matricula, _row["tipo_garantia"].ToString(), _row["descripcion"].ToString(), // ERROR: CS1503
                    // Convert.ToDouble(_row["avaluo_catastro"]), // ERROR: CS1503
                    // Convert.ToDouble(_row["avaluo_ccial"]), _row["asegurado"].ToString(), numpoliza, // ERROR: CS1503
                    // DateTime.Now, feccancelacion, Convert.ToDateTime(_row["fecven_seguro"]), // ERROR: CS1503
                    // nitaseguradora, nombreaseguradora, "A", usuario, // ERROR: CS1503
                    // Convert.ToDecimal(_row["por_seguro"]), Convert.ToDouble(_row["numcdat"]), myconnect, // ERROR: CS1503
                    // _row["codeudor1"].ToString(), _row["codeudor2"].ToString(), // ERROR: CS1503
                    // _row["codeudor3"].ToString(), _row["codeudor4"].ToString(), // ERROR: CS1503
                    // Convert.ToInt32(_row["plazo"]), Convert.ToDouble(_row["valor_aprobado"])); // ERROR: CS1503
            }
            return ok;
        }

        // VB line 3240
        private void GrabaDatosCodeudores(int solicitud, DataTable datatable, OdbcConnection myconnect)
        {
            int i = 0;
            if (datatable.Rows.Count > 0)
            {
                stmysql = "delete from COP_SOLCODEUDOR where SOLICITUD=" + solicitud;
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "GrabaDatosCodeudores");

                while (i < datatable.Rows.Count)
                {
                    DataRow _row = datatable.Rows[i];
                    this.GrabaCodeudores(solicitud, _row["codeudor"].ToString(),
                        Convert.ToDouble(_row["Salario"]), Convert.ToDouble(_row["OtroIng"]),
                        Convert.ToDouble(_row["IngArriendo"]), Convert.ToDouble(_row["IngVariable"]),
                        Convert.ToDouble(_row["DeudaEmp"]), Convert.ToDouble(_row["DeudaTerc"]),
                        Convert.ToDouble(_row["OtroDsto"]), Convert.ToDouble(_row["DispMes"]),
                        Convert.ToDouble(_row["IngPension"]), Convert.ToDouble(_row["DstoPension"]),
                        Convert.ToDouble(_row["DstoParafiscales"]), myconnect,
                        _row["cappagoPorcentaje"].ToString(), Convert.ToDouble(_row["GastosPers"]),
                        Convert.ToDouble(_row["DeudaEmpCaja"]));
                    ActualizaCodeudor(_row["codeudor"].ToString(), Convert.ToDouble(_row["Salario"]), myconnect);
                    i = i + 1;
                }
            }
        }

        // VB line 3256
        public void ActualizaCodeudor(string codigo, double salario, OdbcConnection myconnect)
        {
            stmysql = "update sys_maenit set Salario=" + salario + " where codigoter='" + codigo + "'";
            OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCodeudor");
        }

        // VB line 3260
        private void GrabaCodeudores(int solicitud, string codeudor, double Salario, double otroing,
            double ingarr, double ingvar, double deudaemp, double deudter, double otrodsto,
            double dispmes, double IngPension, double DstoPension, double DstoParafiscales,
            OdbcConnection myconnect, string cappagoPorcentaje, double GastosPers, double DeudaEmpCaja)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet dsdata = new DataSet();
            // ok = this.BuscaDatosCodeudores(solicitud, codeudor, myconnect, dsdata); // ERROR: CS1620
            if (!ok)
            {
                StBuilder.Append("Insert into COP_SOLCODEUDOR (SOLICITUD,CODEUDOR,Salario,OTROSINGRESOS,INGARRIENDO,INGVARIABLE,");
                StBuilder.Append("DEUDAEMP,DEUDTERCERO,OTRODSTOS,DISPMES,IngPension,DstoPension,DstoParafiscales,cappagoPorcentaje,GastosPers,DeudaEmpCaja) values (");
                StBuilder.Append(solicitud + ",'");
                StBuilder.Append(codeudor + "',");
                StBuilder.Append(Salario + ",");
                StBuilder.Append(otroing + ",");
                StBuilder.Append(ingarr + ",");
                StBuilder.Append(ingvar + ",");
                StBuilder.Append(deudaemp + ",");
                StBuilder.Append(deudter + ",");
                StBuilder.Append(otrodsto + ",");
                StBuilder.Append(dispmes + ",");
                StBuilder.Append(IngPension + ",");
                StBuilder.Append(DstoPension + ",");
                StBuilder.Append(DstoParafiscales + ",'");
                StBuilder.Append(cappagoPorcentaje + "'," + GastosPers + "," + DeudaEmpCaja + ")");
            }
            else
            {
                StBuilder.Append("Update COP_SOLCODEUDOR set Salario=" + Salario + ",OTROSINGRESOS=" + otroing + ",INGARRIENDO=" + ingarr + ",");
                StBuilder.Append("INGVARIABLE=" + ingvar + ",DEUDAEMP=" + deudaemp + ",DEUDTERCERO=" + deudter + ",");
                StBuilder.Append("OTRODSTOS=" + otrodsto + ",DISPMES=" + dispmes + ",IngPension=" + IngPension + ",DstoPension=" + DstoPension + ",DstoParafiscales=" + DstoParafiscales);
                StBuilder.Append(",cappagoPorcentaje ='" + cappagoPorcentaje + "',GastosPers=" + GastosPers + ",DeudaEmpCaja=" + DeudaEmpCaja + "  Where SOLICITUD=" + solicitud + " and CODEUDOR='" + codeudor + "'");
            }
            OdbcConnect.ExecuteQueryconec(StBuilder.ToString(), myconnect, "GrabaCodeudores");
        }

        // VB line 3293
        public void SaldoCodeudores(string codigoter, DateTime Fecha, OdbcConnection Myconnect,
            ref double SaldoAportes, ref double SaldoDeuda, ref double DeudaRespaldada, ref double TotalDeudasRespaldada)
        {
            stmysql = " select sum(a.saldo) as campo1 from cop_salmaecar a inner join cop_concar12 b " +
                   " on b.lincred = a.lincred where b.CODAHOR = '1' and a.periodo= " + Fecha.ToString("yyyyMM") + " and codigoter ='" + codigoter + "'";

            {
                string _s1 = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFormato", ref _s1, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s1, out SaldoAportes);
            }

            stmysql = " select sum(a.saldo) as campo1 from cop_salmaecar a inner join cop_concar12 b " +
                   " on b.lincred = a.lincred where b.CODAHOR = '4' and a.periodo= " + Fecha.ToString("yyyyMM") + " and codigoter ='" + codigoter + "'";

            {
                string _s2 = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFormato", ref _s2, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s2, out SaldoDeuda);
            }

            stmysql = " select sum(a.saldo) as campo1,count(a.saldo) as campo2 from cop_salmaecar a inner join cop_maecar b " +
                      " on b.lincred = a.lincred and b.numero = a.numero and b.codigoter = a.codigoter where  a.periodo= " +
                      Fecha.ToString("yyyyMM") +
                      " and (codeudor1  ='" + codigoter + "' or codeudor2  ='" + codigoter +
                      "' or codeudor3  ='" + codigoter + "' or codeudor4  ='" + codigoter +
                      "') and a.saldo<>0 and a.lincred >= 1000 and a.codigoter <> '" + codigoter + "'";

            {
                string _s3 = "0"; string _s4 = "0"; string _d3 = "0", _d4 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, Myconnect, "ImprimeFormato", ref _s3, ref _s4, ref _d3, ref _d4);
                double.TryParse(_s3, out DeudaRespaldada);
                double.TryParse(_s4, out TotalDeudasRespaldada);
            }
        }

        // VB line 3315
        public DataSet CargaBienes(string empresa, DataSet DsDataRaiz, DataSet DsDataVehiculo,
            Form Myforma, OdbcConnection Myconnet, string codigoter = "0", bool oculatarlinklbl = true)
        {
            // frmBienes FrmBienes = new frmBienes(Myconnet); // ERROR: CS0246
            DataSet dsdatos = new DataSet();

            // FrmBienes.LblNomEmpresa.Text = empresa; // ERROR: CS0103
            // FrmBienes.DsdataBienes = DsDataRaiz; // ERROR: CS0103
            // FrmBienes.DsdataVehiculo = DsDataVehiculo; // ERROR: CS0103
            // FrmBienes.codigo = codigoter; // ERROR: CS0103
            // FrmBienes.ocultarlinklbl = oculatarlinklbl; // ERROR: CS0103
            // FrmBienes.ShowDialog(Myforma); // ERROR: CS0103
            // dsdatos.Tables.Add(FrmBienes.DsdataBienes.Tables["tblBienesRaices"].Copy()); // ERROR: CS0103
            // dsdatos.Tables.Add(FrmBienes.DsdataVehiculo.Tables["tblVehiculo"].Copy()); // ERROR: CS0103
            // FrmBienes.Close(); // ERROR: CS0103
            // FrmBienes.Dispose(); // ERROR: CS0103
            return dsdatos;
        }

        // VB line 3334
        public DataSet CargaBienesHistorial(int tipobi, DataSet DsData, Form forma, OdbcConnection Myconnet,
            string empresa, string codigo = "0", string BuscaTabla = "cop_solcre")
        {
            // frmBienesExisten FrmBienesExisten = new frmBienesExisten(Myconnet); // ERROR: CS0246
            DataSet dsdatos = new DataSet();

            // FrmBienesExisten.LblNomCompania.Text = empresa; // ERROR: CS0103
            // FrmBienesExisten.tipo = tipobi; // ERROR: CS0103
            // FrmBienesExisten.DsBienesIncluidos = DsData; // ERROR: CS0103
            // FrmBienesExisten.codigoter = codigo; // ERROR: CS0103
            // FrmBienesExisten.BuscarTabla = BuscaTabla; // ERROR: CS0103
            // FrmBienesExisten.ShowDialog(forma); // ERROR: CS0103

            // dsdatos = FrmBienesExisten.DsBienesSeleccion; // ERROR: CS0103
            // FrmBienesExisten.Close(); // ERROR: CS0103
            // FrmBienesExisten.Dispose(); // ERROR: CS0103

            return dsdatos;
        }

        // VB line 3352
        private void GrabarBienes(DataSet dsdata, double nrosolicitud, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            int i = 0;
            string _stmysql = "";
            if (dsdata.Tables.Contains("tblBienesRaices") == true)
            {
                if (dsdata.Tables["tblBienesRaices"].Rows.Count > 0)
                {
                    _stmysql = "delete from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='R'";
                    OdbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarBienes(Raices-Borrando)");

                    while (i < dsdata.Tables["tblBienesRaices"].Rows.Count)
                    {
                        DataRow _row = dsdata.Tables["tblBienesRaices"].Rows[i];
                        _stmysql = "insert into cop_solbienes (NumSolicitud,bien,clase,direccion,ciudad,valor) values " +
                                   "(" + nrosolicitud + ",'R','" + _row["CLASE"] + "','" + _row["DIRECCION"] + "'," + _row["CIUDAD"] + "," + _row["VALOR"] + ")";
                        OdbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarBienes(Raices)");
                        i += 1;
                    }
                }
            }
            i = 0;
            if (dsdata.Tables.Contains("tblVehiculo") == true)
            {
                if (dsdata.Tables["tblVehiculo"].Rows.Count > 0)
                {
                    _stmysql = "delete from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='V'";
                    OdbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarBienes(Vehiculo-Borrando)");

                    while (i < dsdata.Tables["tblVehiculo"].Rows.Count)
                    {
                        DataRow _row = dsdata.Tables["tblVehiculo"].Rows[i];
                        _stmysql = "insert into cop_solbienes (NumSolicitud,bien,clase,marca,modelo,valor) values " +
                                   "(" + nrosolicitud + ",'V','" + _row["CLASE"] + "','" + _row["MARCA"] + "','" + _row["MODELO"] + "'," + _row["VALOR"] + ")";
                        OdbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarBienes(Vehiculo)");
                        i += 1;
                    }
                }
            }
        }

        // VB line 3387
        public DataSet BuscarBienes(double nrosolicitud, TipoBien TipoBien, OdbcConnection myconnect)
        {
            DataSet dataset = new DataSet();
            bool _ok;

            switch (TipoBien)
            {
                case ClsLiqcreditos.TipoBien.Raiz:
                    stmysql = "select CLASE,case CLASE when '0' then 'Casa' when '1' then 'Apartamento' when '2' then 'Finca' when '3' then 'Lote' end as NOMCLASE,DIRECCION,a.CIUDAD,VALOR,b.nombre_ciudad as NOMCIUDAD " +
                              "from cop_solbienes a inner join sys_ciudad57 b on a.ciudad=b.ciudad where NumSolicitud=" + nrosolicitud + " and bien='R'";
                    _ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Raices)", ref dataset, "tblBienesRaices");
                    break;

                case ClsLiqcreditos.TipoBien.Vehiculo:
                    stmysql = "select CLASE,case CLASE when '0' then 'Particular' when '1' then 'Publico' end as NOMCLASE,MARCA,MODELO,VALOR " +
                             "from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='V'";
                    _ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Vehiculo)", ref dataset, "tblVehiculo");
                    break;

                case ClsLiqcreditos.TipoBien.Todos:
                    stmysql = "select CLASE,case CLASE when '0' then 'Casa' when '1' then 'Apartamento' when '2' then 'Finca' when '3' then 'Lote' end as NOMCLASE,DIRECCION,a.CIUDAD,VALOR,b.nombre_ciudad as NOMCIUDAD " +
                               "from cop_solbienes a inner join sys_ciudad57 b on a.ciudad=b.ciudad where NumSolicitud=" + nrosolicitud + " and bien='R'";
                    _ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Raices)", ref dataset, "tblBienesRaices");

                    stmysql = "select CLASE,case CLASE when '0' then 'Particular' when '1' then 'Publico' end as NOMCLASE,MARCA,MODELO,VALOR " +
                              "from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='V'";
                    _ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarBienes(Vehiculo)", ref dataset, "tblVehiculo");
                    break;
            }
            return dataset;
        }

        // VB line 3413
        private void GrabarReferenciasSol(DataSet dsdata, double nrosolicitud, OdbcConnection myconnect)
        {
            int i = 0;
            string _stmysql = "";
            if (dsdata.Tables.Contains("TblReferencia") == true)
            {
                if (dsdata.Tables["TblReferencia"].Rows.Count > 0)
                {
                    _stmysql = "delete from cop_solreferencia where NumSolicitud=" + nrosolicitud;
                    OdbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarReferenciasSol(Borrando)");

                    while (i < dsdata.Tables["TblReferencia"].Rows.Count)
                    {
                        DataRow _row = dsdata.Tables["TblReferencia"].Rows[i];
                        _stmysql = "insert into cop_solreferencia (NumSolicitud,TipoReferencia,nombre,direccion,ciudad,telefono) values " +
                                   "(" + nrosolicitud + ",'" + Strings.Mid(_row["Referencia"].ToString(), 1, 1) + "','" + _row["Nombre"] + "','" + _row["DIRECCION"] + "'," + _row["CIUDAD"] + ",'" + _row["Telefono"] + "')";
                        OdbcConnect.ExecuteQueryconec(_stmysql, myconnect, "GrabarBienes(Raices)");
                        i += 1;
                    }
                }
            }
        }

        // VB line 3433
        public DataSet BuscarReferenciaSol(double nrosolicitud, OdbcConnection myconnect)
        {
            DataSet dsdataset = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            stbuilder.Append("Select case TipoReferencia when '1' then '1-Familiares' when '2' then '2-Personales' when '3' then '3-Comerciales' when '4' then '4-Financieras' end as Referencia, ");
            stbuilder.Append("nombre,direccion,telefono,ciudad from cop_solreferencia where NumSolicitud=" + nrosolicitud);

            OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarReferenciaSol", ref dsdataset, "TblReferencia");
            return dsdataset;
        }

        // VB line 3445
        public bool CalcularValorBienes(double nrosolicitud, TipoBien TipoBien, OdbcConnection myconnect,
            ref double VlrBienRaiz, ref double VlrVehiculo)
        {
            DataSet dataset = new DataSet();
            bool _ok = false;

            switch (TipoBien)
            {
                case ClsLiqcreditos.TipoBien.Raiz:
                    stmysql = "select sum(valor) as campo1 from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='R'";
                    {
                        string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                        _ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarBienes(Raices)", ref _s, ref _d1, ref _d2, ref _d3);
                        double.TryParse(_s, out VlrBienRaiz);
                    }
                    break;

                case ClsLiqcreditos.TipoBien.Vehiculo:
                    stmysql = "select sum(valor) as campo1 from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='V'";
                    {
                        string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                        _ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarBienes(Vehiculo)", ref _s, ref _d1, ref _d2, ref _d3);
                        double.TryParse(_s, out VlrVehiculo);
                    }
                    break;

                case ClsLiqcreditos.TipoBien.Todos:
                    stmysql = "select sum(valor) as campo1 from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='R'";
                    {
                        string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                        _ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarBienes(Raices)", ref _s, ref _d1, ref _d2, ref _d3);
                        double.TryParse(_s, out VlrBienRaiz);
                    }
                    stmysql = "select sum(valor) as campo1 from cop_solbienes where NumSolicitud=" + nrosolicitud + " and bien='V'";
                    {
                        string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                        _ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "BuscarBienes(Vehiculo)", ref _s, ref _d1, ref _d2, ref _d3);
                        double.TryParse(_s, out VlrVehiculo);
                    }
                    break;
            }
            return _ok;
        }

        // Overload without optional refs
        public bool CalcularValorBienes(double nrosolicitud, TipoBien TipoBien, OdbcConnection myconnect)
        {
            double r = 0, v = 0;
            return CalcularValorBienes(nrosolicitud, TipoBien, myconnect, ref r, ref v);
        }

        // VB line 3468
        public bool CambiarEstadoSolicitudCredito(double NumSolicitud, string estado, OdbcConnection myconnect, string Usuario)
        {
            stmysql = "update cop_solcre set estado='" + estado + "',UsuNovedad='" + Usuario + "' where numero=" + NumSolicitud;
            ok = OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CambiarEstadoSolicitudCredito");
            return ok;
        }

        // VB line 3474
        public bool ReliquidacionCreditos(string codigoter, int lincred, double numero, int periodo,
            DateTime fechaMovto, double VlrAbono, Form forma, int OpcionReliquidacion,
            double Secuencia, string Compronte, double ConseCpte, OdbcConnection myconnect)
        {
            decimal TasaInt = 0;
            double saldot = 0, cuota = 0;
            int clacuo = 0, claseint = 0;
            DateTime fechaInicial = default(DateTime);
            int plazo = 0, Periodicidad = 0;
            string ciclodsto = "5", Nomasociado = "", forAdmon = "";
            double BaseAportes = 0;
            DateTime FechaDsto = default(DateTime);
            DataSet DtExtra = new DataSet();
            DataTable dtDeduccion = new DataTable();
            DataSet DsProyeccion = new DataSet();
            StringBuilder Stbuilder = new StringBuilder();
            DataSet dsDataset = new DataSet();

            Stbuilder.Append("Select salmae.saldo, copmae.cuota,copmae.clacuo,copmae.Clasei,copmae.tasaint,copmae.plazo,copmae.periodd,");
            Stbuilder.Append("copmae.ciclod,copmae.fecdesc,copmae.forseg,copmae.tasaseg,copmae.Clades,copmae.tasaadm,concar.codseg,copmae.puntos,concar.tipointeres,concar.Dtf, ");
            Stbuilder.Append("solcre.TIP_ADM,concar.previv,maenit.SeguroRiesgo,maenit.apellido,maenit.nombre,maenit.empresa,concar.foradmon,copmae.CUOTA_SEG,copmae.CUOTA_ADM, ");
            Stbuilder.Append("concar.descripcion,concar.seguro,concar.poapen,concar.tasadm,concar.claAdmon,copmae.fecfact,copmae.valorob,copmae.PERGRAINI,copmae.PERGRADIA,concar.valsegMin,concar.valsegMax  ");
            Stbuilder.Append("from cop_maecar copmae inner join cop_salmaecar salmae ");
            Stbuilder.Append("on copmae.codigoter = salmae.codigoter and copmae.lincred = salmae.lincred and ");
            Stbuilder.Append("copmae.numero = salmae.numero and salmae.saldo>0 And salmae.periodo = " + periodo);
            Stbuilder.Append(" inner join cop_concar12 concar on copmae.lincred=concar.lincred ");
            Stbuilder.Append("left join cop_solcre solcre on copmae.numero_soli=solcre.numero ");
            Stbuilder.Append("inner join sys_maenit maenit on copmae.codigoter=maenit.codigoter ");
            Stbuilder.Append("where copmae.codigoter='" + codigoter + "' and copmae.lincred=" + lincred + " and copmae.numero=" + numero);

            ok = OdbcConnect.ExecuteQueryDataset(Stbuilder.ToString(), myconnect, "ReliquidacionCreditos", ref dsDataset, "TblCartera");
            if (ok)
            {
                DataRow _row = dsDataset.Tables["TblCartera"].Rows[0];
                this.CreaTablaDeducciones(dtDeduccion, codigoter);
                DtExtra = BuscarExtrasConSaldo(codigoter, lincred, numero, myconnect);
                if (VlrAbono == 0)
                {
                    if (_row["tipointeres"].ToString() == "1")
                    {
                        _row["tasaint"] = _row["puntos"];
                    }
                    // DsProyeccion = this.GeneraProyeccion(codigoter, lincred, // ERROR: CS1739
                        // Convert.ToDateTime(_row["fecfact"]), // ERROR: CS1739
                        // Convert.ToInt32(_row["plazo"]), // ERROR: CS1739
                        // Convert.ToInt32(_row["periodd"]), // ERROR: CS1739
                        // Convert.ToInt32(_row["clades"]), // ERROR: CS1739
                        // Convert.ToInt32(_row["ciclod"]), // ERROR: CS1739
                        // Convert.ToDecimal(_row["tasaint"]), // ERROR: CS1739
                        // Convert.ToDateTime(_row["fecdesc"]), // ERROR: CS1739
                        // Convert.ToDouble(_row["valorob"]), // ERROR: CS1739
                        // DtExtra.Tables["tblextras"], dtDeduccion, // ERROR: CS1739
                        // Convert.ToInt32(_row["PERGRAINI"]), myconnect, // ERROR: CS1739
                        // clacuo: Convert.ToInt32(_row["clacuo"])); // ERROR: CS1739
                }
                else
                {
                    if (Convert.ToInt32(_row["tipointeres"]) == 1)
                    {
                        // Aqui va la conversion de la dtf.
                        // TasaInteres=TasaInteres+Dtf
                        // _row["tasaint"] = this.ConversionDTFaNMV(Convert.ToDouble(_row["dtf"]), Convert.ToDouble(_row["puntos"])); // ERROR: CS1503
                    }

                    if (Convert.ToInt32(_row["periodd"]) == 4 && Convert.ToInt32(_row["ciclod"]) == 5)
                    {
                        TasaInt = (decimal)Math.Round(((Convert.ToDouble(_row["tasaint"]) / 30) * 7) / 100, 8);
                    }
                    else
                    {
                        TasaInt = (decimal)Math.Round((Convert.ToDouble(_row["tasaint"]) / Convert.ToDouble(_row["periodd"])) / 100, 8);
                    }

                    if (_row["TIP_ADM"] is DBNull)
                    {
                        forAdmon = _row["claAdmon"].ToString();
                    }
                    else
                    {
                        forAdmon = _row["TIP_ADM"].ToString();
                    }

                    if (Convert.ToDouble(_row["SeguroRiesgo"]) > 0)
                    {
                        _row["tasaseg"] = _row["SeguroRiesgo"];
                    }

                    // plazo = this.msgcop.CalculaCuotasPagar(codigoter, lincred, numero, // ERROR: CS1503
                        // Convert.ToInt32(_row["periodd"]), Convert.ToInt32(_row["ciclod"]), // ERROR: CS1503
                        // Convert.ToInt32(_row["clacuo"]), Convert.ToDouble(_row["tasaint"]), // ERROR: CS1503
                        // Convert.ToDouble(_row["saldo"]) + VlrAbono, Convert.ToDouble(_row["cuota"]), // ERROR: CS1503
                        // periodo, Convert.ToInt32(_row["forseg"]), Convert.ToDecimal(_row["tasaseg"]), // ERROR: CS1503
                        // Convert.ToDouble(_row["CUOTA_SEG"]), forAdmon, // ERROR: CS1503
                        // Convert.ToInt32(_row["foradmon"]), Convert.ToDecimal(_row["tasaadm"]), // ERROR: CS1503
                        // Convert.ToDouble(_row["CUOTA_ADM"]), myconnect, // ERROR: CS1503
                        // Convert.ToDouble(_row["valorob"]), // ERROR: CS1503
                        // Convert.ToDouble(_row["valsegMin"]), Convert.ToDouble(_row["valsegMax"])); // ERROR: CS1503

                    switch (OpcionReliquidacion)
                    {
                        case 1:
                            cuota = Convert.ToDouble(_row["cuota"]);
                            break;
                        case 3:
                            double dbValextr = 0;
                            // dbValextr = msgcop.vpn_extras(codigoter, lincred, numero, periodo, // ERROR: CS1503
                                // Convert.ToInt32(_row["periodd"]), Convert.ToInt32(_row["ciclod"]), // ERROR: CS1503
                                // Convert.ToInt32(_row["clacuo"]), // ERROR: CS1503
                                // Convert.ToDouble(TasaInt) / 100, myconnect); // ERROR: CS1503
                            if (dbValextr <= Convert.ToDouble(_row["saldo"]))
                            {
                                _row["saldo"] = Convert.ToDouble(_row["saldo"]) - dbValextr;
                            }
                            cuota = CalculaNuevaCuota(Convert.ToInt32(_row["clacuo"]), TasaInt,
                                Convert.ToDecimal(_row["seguro"]), Convert.ToDecimal(_row["tasadm"]),
                                Convert.ToInt32(_row["forseg"]), Convert.ToInt32(_row["claAdmon"]),
                                plazo, Convert.ToInt32(_row["periodd"]),
                                Convert.ToDouble(_row["saldo"]), Convert.ToDouble(_row["valorob"]),
                                Convert.ToDouble(_row["valsegMin"]), Convert.ToDouble(_row["valsegMax"]));
                            break;
                    }

                    if (Convert.ToInt32(_row["periodd"]) == 4 && Convert.ToInt32(_row["ciclod"]) == 5)
                    {
                        plazo = Convert.ToInt32((plazo * 12) / 52);
                    }
                    else
                    {
                        plazo = (int)Math.Round((double)plazo / Convert.ToDouble(_row["periodd"]), 0);
                    }

                    if (VlrAbono > 0)
                    {
                        FechaDsto = CalculaFechaNuevoDsto(codigoter, lincred, numero, fechaMovto,
                            Convert.ToDateTime(_row["fecdesc"]),
                            Convert.ToInt32(_row["periodd"]), Convert.ToInt32(_row["ciclod"]),
                            ref fechaInicial, myconnect);
                    }
                    else
                    {
                        FechaDsto = Convert.ToDateTime(_row["fecdesc"]);
                        fechaInicial = fechaMovto;
                    }

                    // ciclodsto = this.CalculaCiclo(Convert.ToInt32(_row["ciclod"]), Convert.ToInt32(_row["periodd"]), FechaDsto); // ERROR: CS1503

                    if (Convert.ToInt32(_row["ciclod"]) != 5)
                    {
                        Periodicidad = 1;
                    }
                    else
                    {
                        Periodicidad = Convert.ToInt32(_row["periodd"]);
                    }

                    // 'Me.CreaTablaDeducciones(dtDeduccion, codigoter)
                    // 'DtExtra = BuscarExtrasConSaldo(codigoter, lincred, numero, myconnect)
                    BaseAportes = this.CalculaSaldoAportes(codigoter, periodo.ToString(), myconnect);

                    if (_row["PERGRAINI"] is DBNull || _row["PERGRAINI"].ToString().Trim() == "")
                    {
                        _row["PERGRAINI"] = 0;
                    }

                    // DsProyeccion = this.GeneraPlanPagos(codigoter, Convert.ToDouble(_row["saldo"]), cuota, // ERROR: CS1739
                        // Convert.ToInt32(_row["clacuo"]), Convert.ToInt32(_row["Clasei"]), TasaInt, // ERROR: CS1739
                        // Convert.ToInt32(_row["plazo"]), // ERROR: CS1739
                        // Periodicidad, ciclodsto, fechaInicial, FechaDsto, 0, // ERROR: CS1739
                        // Convert.ToInt32(_row["forseg"]), Convert.ToDecimal(_row["tasaseg"]), // ERROR: CS1739
                        // Convert.ToInt32(_row["codseg"]), // ERROR: CS1739
                        // Convert.ToDouble(_row["tasaint"]), 0, forAdmon, // ERROR: CS1739
                        // Convert.ToDecimal(_row["TASAADM"]), "N", // ERROR: CS1739
                        // _row["previv"].ToString(), DtExtra.Tables["tblextras"], dtDeduccion, // ERROR: CS1739
                        // 0, 0, 0, // ERROR: CS1739
                        // pergradia: Convert.ToInt32(_row["PERGRADIA"]), // ERROR: CS1739
                        // pergraini: Convert.ToInt32(_row["PERGRAINI"]), // ERROR: CS1739
                        // valorob: Convert.ToDouble(_row["valorob"]), // ERROR: CS1739
                        // valsegMin: Convert.ToDouble(_row["valsegMin"]), // ERROR: CS1739
                        // valsegMax: Convert.ToDouble(_row["valsegMax"])); // ERROR: CS1739

                    // CreaTablaDatosCredito(DsProyeccion); // ERROR: CS1620
                    Nomasociado = _row["apellido"].ToString() + " " + _row["nombre"].ToString();

                    DsProyeccion.Tables["TbldatosCredito"].Rows.Add(codigoter, Nomasociado,
                        _row["empresa"].ToString(),
                        fechaInicial.ToString(varini.PstForFec),
                        _row["periodd"], plazo, _row["Clades"], _row["ciclod"], _row["tasaint"],
                        FechaDsto.ToString(varini.PstForFec), lincred, _row["descripcion"],
                        _row["saldo"], BaseAportes, ciclodsto, 0, cuota, 0,
                        BaseAportes, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

                    try
                    {
                        if (dtDeduccion.Rows.Count == 0)
                        {
                            dtDeduccion.Rows.Add(codigoter, 0, 0, 0, 0, 0, 0);
                        }
                    }
                    catch (Exception)
                    {
                    }

                    DsProyeccion.Tables.Add(dtDeduccion.Copy());
                    DsProyeccion.Tables.Add(DtExtra.Tables["tblextras"].Copy());
                    if (OpcionReliquidacion == 3)
                    {
                        ActualizaCuotaMovto(Secuencia, Compronte, ConseCpte, Convert.ToDouble(_row["cuota"]), myconnect);
                        this.msgcop.GrabarNuevaCuota(codigoter, lincred, numero, cuota, myconnect);
                        msgcop.ModificaCuotasalmecar(codigoter, lincred, numero, cuota, periodo, myconnect);
                    }
                }

                switch (MessageBox.Show("Desea Imprimir el plan de pagos?", "SOLIDO", MessageBoxButtons.YesNo))
                {
                    case DialogResult.Yes:
                        this.ImprimePlanpagos(DsProyeccion, forma);
                        break;
                }
            }
            return ok;
        }

        // VB line 3622
        private void ActualizaCuotaMovto(double secuencia, string Comprobante, double NumCpte, double cuota, OdbcConnection myconnect)
        {
            stmysql = "update cop_movimto set ReliqCuota=" + cuota + " where secuencia=" + secuencia + " and COMPRONTE = '" + Comprobante + "' and NUMERO_DOMTO = " + NumCpte;
            OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "ActualizaCuotaMovto");
        }

        // VB line 3627
        public double CalculaNuevaCuota(int Clacuo, decimal TasaInt, decimal TasaSeg, decimal TasaAdmon,
            int Forseg, int forAdmon, int NumCuotas, int period, double VlrPrestamo,
            double VLRCREDITO = 0, double VALSEGMIN = 0, double VALSEGMAX = 999999999)
        {
            double Cuota = 0;
            double cuotaSeguro = 0;
            decimal IntSeg = 0, IntAdm = 0;
            int Plazo;

            Plazo = Convert.ToInt32(NumCuotas / period);
            switch (Forseg)
            {
                case 3:
                    if (VLRCREDITO >= VALSEGMIN && VLRCREDITO <= VALSEGMAX)
                    {
                        IntSeg = (decimal)Math.Round((Convert.ToDouble(TasaSeg) / 100) / period, 8);
                    }
                    else
                    {
                        IntSeg = 0;
                    }
                    break;
                case 7:
                    Cuota += VlrPrestamo * (Convert.ToDouble(TasaSeg / 100) / period);
                    break;
                case 9:
                    cuotaSeguro = Math.Round(VlrPrestamo * (Convert.ToDouble(TasaSeg) / 100) / Plazo, 0);
                    break;
            }

            switch (forAdmon)
            {
                case 2:
                    IntAdm = (decimal)Math.Round((Convert.ToDouble(TasaAdmon) / 100) / period, 8);
                    break;
            }

            switch (Clacuo)
            {
                case 1:
                    // VB: CInt((FormatNumber(Financial.Pmt(TasaInt + IntSeg + IntAdm, NumCuotas, (-VlrPrestamo), 0), )))
                    // Note: FormatNumber with empty precision arg — translate as default (2 decimals), then CInt
                    Cuota = Convert.ToInt32(Convert.ToDouble(string.Format("{0:N2}",
                        Microsoft.VisualBasic.Financial.Pmt(
                            Convert.ToDouble(TasaInt + IntSeg + IntAdm),
                            NumCuotas,
                            -VlrPrestamo,
                            0))));
                    break;
                case 2:
                    Cuota = Convert.ToInt32(((VlrPrestamo) / NumCuotas) + 0.5);
                    break;
            }
            Cuota = Cuota + cuotaSeguro;
            return Cuota;
        }

        // VB line 3662
        public DateTime CalculaFechaNuevoDsto(string codigoter, int lincred, double numcredito,
            DateTime FechaActual, DateTime FechaDsto, int periodicidad, int ciclo,
            ref DateTime fechainicial, OdbcConnection myconnect)
        {
            int DiaDsto, DiaActual;
            DateTime fecha = DateTime.Now;
            int DiaQuin = 0;
            string FecUltCau = " ";

            if (ciclo != 5)
            {
                periodicidad = 1;
            }

            stmysql = "select max(fecha_movto) as campo1 from cop_copmora copmora inner join cop_cuopen coppen "
                + "on copmora.codigoter = coppen.codigoter and copmora.lincred = coppen.lincred and coppen.numero = copmora.numero and copmora.periodo_causa = coppen.periodo_causa"
                + " where copmora.codigoter = '" + codigoter + "' and copmora.lincred =  " + lincred + " and copmora.numero = " + numcredito
                + " group by copmora.codigoter, copmora.lincred, copmora.numero";

            {
                string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaFechaNuevoDsto", ref FecUltCau, ref _d1, ref _d2, ref _d3);
            }

            if (FecUltCau.Trim() == "0")
            {
                FecUltCau = FechaDsto.ToString(varini.PstForFec);
            }
            else if (FecUltCau.Trim() == "")
            {
                FecUltCau = FechaDsto.ToString(varini.PstForFec);
            }

            DiaDsto = Convert.ToInt32(FechaDsto.ToString("dd"));
            DiaActual = Convert.ToInt32(FechaActual.ToString("dd"));

            switch (periodicidad)
            {
                case 1:
                {
                    DateTime _fecUltCauDt = default(DateTime);
                    DateTime.TryParse(FecUltCau, out _fecUltCauDt);
                    if (FechaActual.ToString("dd-MM-yyyy") != _fecUltCauDt.ToString("dd-MM-yyyy") &&
                        string.Compare(FechaActual.ToString("dd-MM-yyyy"), _fecUltCauDt.ToString("dd-MM-yyyy")) < 0)
                    {
                        fecha = FechaActual.AddMonths(1);
                    }
                    fechainicial = FechaActual;
                    if (DiaDsto == 30 || DiaDsto == 31)
                    {
                        DiaDsto = DateTime.DaysInMonth(Convert.ToInt32(fecha.ToString("yyyy")), Convert.ToInt32(fecha.ToString("MM")));
                    }
                    if (Convert.ToInt32(fecha.ToString("MM")) == 2 && DiaDsto == 29)
                    {
                        DiaDsto = 28;
                    }
                    fecha = new DateTime(Convert.ToInt32(fecha.ToString("yyyy")), Convert.ToInt32(fecha.ToString("MM")), DiaDsto);
                    break;
                }
                case 2:
                {
                    fechainicial = FechaActual;
                    DateTime _fecUltCauDt2 = default(DateTime);
                    DateTime.TryParse(FecUltCau, out _fecUltCauDt2);
                    if (string.Compare(FechaActual.ToString("dd-MM-yyyy"), _fecUltCauDt2.ToString("dd-MM-yyyy")) > 0)
                    {
                        if (DiaActual <= 15)
                        {
                            fecha = new DateTime(Convert.ToInt32(FechaActual.ToString("yyyy")), Convert.ToInt32(FechaActual.ToString("MM")), 15);
                            DiaQuin = 16;
                        }
                        else if (DiaActual > 15)
                        {
                            fecha = new DateTime(Convert.ToInt32(FechaActual.ToString("yyyy")), Convert.ToInt32(FechaActual.ToString("MM")),
                                Convert.ToInt32(FechaActual.ToString("MM")) == 2 ? 28 : 30);
                            DiaQuin = 1;
                        }
                    }
                    else
                    {
                        if (DiaActual <= 15)
                        {
                            fecha = new DateTime(Convert.ToInt32(FechaActual.ToString("yyyy")), Convert.ToInt32(FechaActual.ToString("MM")),
                                Convert.ToInt32(FechaActual.ToString("MM")) == 2 ? 28 : 30);
                            DiaQuin = 16;
                        }
                        else if (DiaActual > 15)
                        {
                            fecha = FechaActual.AddMonths(1);
                            fecha = new DateTime(Convert.ToInt32(fecha.ToString("yyyy")), Convert.ToInt32(fecha.ToString("MM")), 15);
                            DiaQuin = 1;
                        }
                    }
                    break;
                }
                case 3:
                {
                    DateTime _fecUltCau3 = default(DateTime);
                    DateTime.TryParse(FecUltCau, out _fecUltCau3);
                    if (_fecUltCau3 != FechaDsto)
                    {
                        fechainicial = _fecUltCau3.AddDays(1);
                        fecha = _fecUltCau3.AddDays(10);
                    }
                    else
                    {
                        fechainicial = _fecUltCau3;
                        fecha = _fecUltCau3.AddDays(10);
                    }
                    break;
                }
                case 4:
                {
                    DateTime _fecUltCau4 = default(DateTime);
                    DateTime.TryParse(FecUltCau, out _fecUltCau4);
                    if (_fecUltCau4 != FechaDsto)
                    {
                        fechainicial = _fecUltCau4.AddDays(1);
                        fecha = _fecUltCau4.AddDays(7);
                    }
                    else
                    {
                        fechainicial = _fecUltCau4;
                        fecha = _fecUltCau4.AddDays(7);
                    }
                    break;
                }
            }
            return fecha;
        }

        // VB line 3759
        public DataSet BuscarExtrasConSaldo(string codigoter, int lincred, double numero, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select fecha_pago as fecha,valor,case forma_pago when '1' then 'Nomina' when '2' then 'Caja' else ' ' end as DescPag," +
                    "forma_pago as Forpag from cop_extras where codigoter='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero;
            ok = OdbcConnect.ExecuteQueryDataset(stmysql, myconnect, "BuscarExtras", ref dsdata, "tblextras");
            if (!ok)
            {
                dsdata.Tables["tblextras"].Rows.Add(DateTime.Now, 0, 0, 0);
            }
            return dsdata;
        }

        // VB line 3771
        public void CreaTablaDatosCredito(ref DataSet dsdata)
        {
            string Ststring = " ";
            int stint = 0;
            decimal StDecimal = 0;
            double StDouble = 0;
            try
            {
                dsdata.Tables.Add("TbldatosCredito");
                DataColumnCollection cols = dsdata.Tables["TbldatosCredito"].Columns;
                cols.Add("Cedula", Ststring.GetType());
                cols.Add("Nomasociado", Ststring.GetType());
                cols.Add("Empresa", Ststring.GetType());
                cols.Add("fecha", Ststring.GetType());
                cols.Add("periodicidad", stint.GetType());
                cols.Add("plazo", stint.GetType());
                cols.Add("clades", stint.GetType());
                cols.Add("ciclo", stint.GetType());
                cols.Add("TasaInt", StDecimal.GetType());
                cols.Add("FecDesto", Ststring.GetType());
                cols.Add("lincred", Ststring.GetType());
                cols.Add("NombreLinea", Ststring.GetType());
                cols.Add("valorCredito", StDouble.GetType());
                cols.Add("BaseCupo", StDouble.GetType());
                cols.Add("CiloDsto", StDouble.GetType());
                cols.Add("ValMenos", StDouble.GetType());
                cols.Add("Cuota", StDouble.GetType());
                cols.Add("VlrVpnExtra", StDouble.GetType());
                cols.Add("SalAportes", StDouble.GetType());
                cols.Add("Agencia", Ststring.GetType());
                cols.Add("CentroCosto", Ststring.GetType());
                cols.Add("TipoIncie", stint.GetType());
                cols.Add("Tipocap", stint.GetType());
                cols.Add("Tipoadm", stint.GetType());
                cols.Add("Tiposeg", stint.GetType());
                cols.Add("TipoOtro", stint.GetType());
                cols.Add("foradm", stint.GetType());
                cols.Add("clacuo", stint.GetType());
                cols.Add("claint", stint.GetType());
                cols.Add("tasaadm", StDecimal.GetType());
                cols.Add("tasaSeg", StDecimal.GetType());
                cols.Add("tasacpt", StDecimal.GetType());
                cols.Add("CuotaAdm", StDecimal.GetType());
                cols.Add("CuotaSeg", StDecimal.GetType());
                cols.Add("CuotaCapital", StDecimal.GetType());
                cols.Add("CuotaIcie", StDecimal.GetType());
                cols.Add("cptoadm", stint.GetType());
                cols.Add("cptoSeg", stint.GetType());
                cols.Add("cptoOtro", stint.GetType());
                cols.Add("tasaotro", StDecimal.GetType());
            }
            catch (Exception)
            {
            }
        }

        // VB line 3822
        public double CalculaSaldoAhorros(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double saldo = 0;
            stmysql = "select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor='2' " +
                "where sal.codigoter='" + codigoter + "' and sal.periodo=" + periodo;
            {
                string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaSaldoAhorros", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3830
        public double CalculaSaldoAhorrosEquisuper(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double saldo = 0;
            stmysql = "select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor='2' and concar12.equisuper=4 " +
                "where sal.codigoter='" + codigoter + "' and sal.periodo=" + periodo;
            {
                string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaSaldoAhorrosEquisuper", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3837
        public double CalculaSaldoAhorroaVoluntarios(string codigoter, string periodo, OdbcConnection myconnect)
        {
            double saldo = 0;
            stmysql = "select sum(sal.saldo)*-1 as campo1 from cop_salmaecar sal inner join cop_concar12 concar12 on sal.lincred=concar12.lincred and concar12.codahor='2' and concar12.equisuper<>4 " +
                "where sal.codigoter='" + codigoter + "' and sal.periodo=" + periodo;
            {
                string _s = "0"; string _d1 = "0", _d2 = "0", _d3 = "0";
                OdbcConnect.ExecuteQueryconec(stmysql, myconnect, "CalculaSaldoAhorrosEquisuper", ref _s, ref _d1, ref _d2, ref _d3);
                double.TryParse(_s, out saldo);
            }
            return saldo;
        }

        // VB line 3844
        public DataSet CargarVentanaSolParviv(DataSet Dsdata, string empresa, Form forma, OdbcConnection myconnect)
        {
            // Frmparviv FrmParViv = new Frmparviv(myconnect); // ERROR: CS0246
            // FrmParViv.dsdata = Dsdata; // ERROR: CS0103
            // FrmParViv.LblNomEmpresa.Text = empresa; // ERROR: CS0103
            // FrmParViv.ShowDialog(forma); // ERROR: CS0103
            // return FrmParViv.dsdata; // ERROR: CS0103
            DataSet dataSet = new DataSet();
            return dataSet;
        }

        // VB line 3855
        public void GrabaSolParVivienda(int NroSolicitud, DataSet dsdata, OdbcConnection Myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            if (dsdata.Tables.Contains("tblsolparviv"))
            {
                if (dsdata.Tables["tblsolparviv"].Rows.Count == 0)
                {
                    this.EliminaSolParVivienda(NroSolicitud, Myconnect);
                }
                else
                {
                    ok = BuscaSolParVivienda(NroSolicitud, Myconnect);
                    DataRow _row = dsdata.Tables["tblsolparviv"].Rows[0];
                    if (!ok)
                    {
                        stbuilder.Append("insert into cop_solparviv (NroSolicitud,ClaViv,TipViv,IntSocial,Subsidio,EntRedes,ValRedes,Desembolso,moneda) ");
                        stbuilder.Append("values (");
                        stbuilder.Append(NroSolicitud + ",'");
                        stbuilder.Append(_row["ClaViv"] + "','");
                        stbuilder.Append(_row["TipViv"] + "','");
                        stbuilder.Append(_row["IntSocial"] + "','");
                        stbuilder.Append(_row["Subsidio"] + "','");
                        stbuilder.Append(_row["EntRedes"] + "','");
                        stbuilder.Append(_row["ValRedes"] + "','");
                        stbuilder.Append(_row["Desembolso"] + "','");
                        stbuilder.Append(_row["moneda"] + "')");
                    }
                    else
                    {
                        stbuilder.Append("update cop_solparviv  set ");
                        stbuilder.Append("ClaViv = '");
                        stbuilder.Append(_row["ClaViv"] + "',");
                        stbuilder.Append("TipViv = '");
                        stbuilder.Append(_row["TipViv"] + "',");
                        stbuilder.Append("IntSocial = '");
                        stbuilder.Append(_row["IntSocial"] + "',");
                        stbuilder.Append("Subsidio = '");
                        stbuilder.Append(_row["Subsidio"] + "',");
                        stbuilder.Append("EntRedes = '");
                        stbuilder.Append(_row["EntRedes"] + "',");
                        stbuilder.Append("ValRedes = '");
                        stbuilder.Append(_row["ValRedes"] + "',");
                        stbuilder.Append("Desembolso = '");
                        stbuilder.Append(_row["Desembolso"] + "',");
                        stbuilder.Append("moneda = '");
                        stbuilder.Append(_row["moneda"] + "'");
                        stbuilder.Append(" where NroSolicitud = " + NroSolicitud);
                    }
                    OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), Myconnect, "GrabaSolParVivienda");
                }
            }
        }

        // VB line 3903
        public bool BuscaSolParVivienda(int NroSolicitud, OdbcConnection myconnect, DataSet DsDatSet = null)
        {
            StringBuilder stbuilder = new StringBuilder();
            DataSet DsDataset = new DataSet();
            try
            {
                DsDatSet.Tables.Remove("tblsolparviv");
            }
            catch (Exception)
            {
            }

            stbuilder.Append("select ClaViv,TipViv,IntSocial,Subsidio,EntRedes,ValRedes,Desembolso,moneda ");
            stbuilder.Append("from cop_solparviv "); // ,NroSolicitud
            stbuilder.Append("where NroSolicitud = " + NroSolicitud);

            ok = OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaSolParVivienda", ref DsDataset, "tblsolparviv");
            try
            {
                DsDatSet.Tables.Add(DsDataset.Tables["tblsolparviv"].Copy());
            }
            catch (Exception)
            {
            }
            return ok;
        }

        // VB line 3924
        public void EliminaSolParVivienda(int NroSolicitud, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            stbuilder.Append("delete from  cop_solparviv ");
            stbuilder.Append("where NroSolicitud = " + NroSolicitud);
            OdbcConnect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "EliminaSolParVivienda");
        }

        // VB line 3933
        public bool ActualizarDatosCredVivienda(double solicitud, double numcredito, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            DataSet dsparviv = new DataSet();
            DataSet dslinea = new DataSet();
            ok = this.BuscarSolicitud(solicitud, ref dsdata, myconnect);
            if (ok)
            {
                DataRow _rowCred = dsdata.Tables["TbldatosCredito"].Rows[0];
                // this.msgparcop.BuscaLinea(_rowCred["lincred"].ToString(), dslinea, myconnect); // ERROR: CS1503, CS1620

                if (dslinea.Tables["tbllineas"].Rows[0]["FOGACLA"].ToString() == "3")
                {
                    ok = this.BuscaSolParVivienda((int)solicitud, myconnect, dsparviv);
                    if (ok)
                    {
                        DataRow _rowPar = dsparviv.Tables["tblsolparviv"].Rows[0];
                        // this.msgparcop.GrabaParVivienda( // ERROR: CS1061
                            // dsdata.Tables["TbldatosCredito"].Rows[0]["codigoter"].ToString(), // ERROR: CS1061
                            // dsdata.Tables["TbldatosCredito"].Rows[0]["lincred"].ToString(), // ERROR: CS1061
                            // numcredito, _rowPar["claviv"].ToString(), _rowPar["tipviv"].ToString(), // ERROR: CS1061
                            // Convert.ToDouble(_rowPar["Intsocial"]), Convert.ToDouble(_rowPar["subsidio"]), // ERROR: CS1061
                            // _rowPar["entredes"].ToString(), Convert.ToDouble(_rowPar["valredes"]), // ERROR: CS1061
                            // _rowPar["desembolso"].ToString(), _rowPar["moneda"].ToString(), myconnect); // ERROR: CS1061
                    }
                }
            }
            return ok;
        }

        // VB line 3955
        /// <summary>
        /// Calcula dias entre dos fechas usando base 360 (comercial)
        /// </summary>
        public int CalculaDias(DateTime fechaInicial, DateTime FechaFinal)
        {
            long DiasVencido;

            // VB uses "Año" (with ñ) — translated to AnoIni/AnoFin
            int AnoIni, MesIni, DiaIni;
            int AnoFin, MesFin, DiaFin;

            AnoIni = Convert.ToInt32(FechaFinal.ToString("yyyy"));
            MesIni = Convert.ToInt32(FechaFinal.ToString("MM"));
            DiaIni = Convert.ToInt32(FechaFinal.ToString("dd"));

            AnoFin = Convert.ToInt32(fechaInicial.ToString("yyyy"));
            MesFin = Convert.ToInt32(fechaInicial.ToString("MM"));
            DiaFin = Convert.ToInt32(fechaInicial.ToString("dd"));

            if (DiaFin > 30)
            {
                DiaFin = 30;
            }
            if (DiaIni > 30)
            {
                DiaIni = 30;
            }

            if (MesFin == 2)
            {
                if (DiaFin >= 28)
                {
                    DiaFin = 30;
                }
            }

            if (MesIni == 2)
            {
                if (DiaIni >= 28)
                {
                    DiaIni = 30;
                }
            }

            DiasVencido = (((AnoFin - AnoIni) * 360) + ((MesFin - MesIni) * 30) + (DiaFin - DiaIni));
            return (int)DiasVencido;
        }

        // VB line 3995 — XML doc comment preserved
        /// <summary>
        /// funcion para buscar la solicitud de los creditos
        /// </summary>
        /// <param name="codigoter">el codigo de asociado</param>
        /// <param name="lincred">la linea</param>
        /// <param name="numero">el numero de el concepto</param>
        /// <param name="myconnect">la objeto de conexion</param>
        /// <param name="periodo">si va consulta datos de cop_maecar periodo va "N"
        /// , si va consultar datos al cop_salmaecar se envia el periodo
        /// </param>
        public DataSet BuscaDatosSolicitudCredito(string codigoter, int lincred, double numero, OdbcConnection myconnect, string periodo = "N")
        {
            DataSet dsdata = new DataSet();
            DataSet dsdataset = new DataSet();
            StringBuilder stbuilder = new StringBuilder();
            DataSet dsdeduccion = new DataSet();
            DataSet dsextras = new DataSet();

            if (periodo == "N")
            {
                stbuilder.Append("select mae.codigoter,mae.lincred,mae.numero,mae.fecsolic,mae.fecdesc,mae.fecfact,mae.periodd,");
                stbuilder.Append("mae.plazo,mae.valorob,mae.cuota,mae.tasaint,mae.ciclod,mae.clacuo,mae.clasei,mae.clades,");
                stbuilder.Append("mae.tasaadm,mae.tasaseg,mae.cuota_adm,mae.cuota_seg,mae.cuota_cptl,mae.cuota_icie,mae.cuota_otros,");
                stbuilder.Append("mae.numero_soli,sol.tip_intcie,sol.tip_cap,sol.tip_adm,sol.tip_seg,sol.tip_otr,sol.for_adm,sol.cpto_adm,sol.cpto_seg,sol.cpto_otr,mae.pergraini,sol.cuota as cuotasol,mae.dtf,mae.puntos ");
                stbuilder.Append("from cop_maecar mae ");
                stbuilder.Append("left join cop_solcre sol on mae.numero_soli = sol.numero ");
                stbuilder.Append("where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero);
            }
            else
            {
                // ok = msgcop.BuscaSaldoObligacion(codigoter, lincred, numero, periodo, myconnect); // ERROR: CS1501
                if (ok)
                {
                    stbuilder.Append("select mae.codigoter,mae.lincred,mae.numero,mae.fecsolic,mae.fecdesc,mae.fecfact,salmae.periodd,");
                    stbuilder.Append("mae.plazo,mae.valorob,salmae.cuota,salmae.tasaint,salmae.ciclod,mae.clacuo,mae.clasei,salmae.clades,");
                    stbuilder.Append("mae.tasaadm,mae.tasaseg,mae.cuota_adm,mae.cuota_seg,mae.cuota_cptl,mae.cuota_icie,mae.cuota_otros,");
                    stbuilder.Append("mae.numero_soli,sol.tip_intcie,sol.tip_cap,sol.tip_adm,sol.tip_seg,sol.tip_otr,sol.for_adm,sol.cpto_adm,sol.cpto_seg,sol.cpto_otr,mae.pergraini,sol.cuota as cuotasol,mae.dtf,mae.puntos ");
                    stbuilder.Append("from cop_maecar mae ");
                    stbuilder.Append("inner join COP_SALMAECAR  salmae on  mae.codigoter = salmae.codigoter and mae.lincred=salmae.lincred and mae.numero=salmae.numero and salmae.periodo=" + periodo);
                    stbuilder.Append(" left join cop_solcre sol on mae.numero_soli = sol.numero ");
                    stbuilder.Append("where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero);
                }
                else
                {
                    stbuilder.Append("select mae.codigoter,mae.lincred,mae.numero,mae.fecsolic,mae.fecdesc,mae.fecfact,mae.periodd,");
                    stbuilder.Append("mae.plazo,mae.valorob,mae.cuota,mae.tasaint,mae.ciclod,mae.clacuo,mae.clasei,mae.clades,");
                    stbuilder.Append("mae.tasaadm,mae.tasaseg,mae.cuota_adm,mae.cuota_seg,mae.cuota_cptl,mae.cuota_icie,mae.cuota_otros,");
                    stbuilder.Append("mae.numero_soli,sol.tip_intcie,sol.tip_cap,sol.tip_adm,sol.tip_seg,sol.tip_otr,sol.for_adm,sol.cpto_adm,sol.cpto_seg,sol.cpto_otr,mae.pergraini,sol.cuota as cuotasol,mae.dtf,mae.puntos ");
                    stbuilder.Append("from cop_maecar mae ");
                    stbuilder.Append("left join cop_solcre sol on mae.numero_soli = sol.numero ");
                    stbuilder.Append("where mae.codigoter='" + codigoter + "' and mae.lincred=" + lincred + " and mae.numero=" + numero);
                }
            }

            ok = OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaDatosSolicitudCredito", ref dsdata, "tblsolicitud");
            if (ok)
            {
                stbuilder.Replace(stbuilder.ToString(), "");
                DataRow _row = dsdata.Tables["tblsolicitud"].Rows[0];

                dsdeduccion = this.BuscarDeducciones(Convert.ToInt32(_row["numero_soli"]), myconnect);
                ok = this.BuscarExtras(Convert.ToInt32(_row["numero_soli"]), ref dsextras, myconnect);

                if (!ok)
                {
                    try
                    {
                        dsextras.Tables.Remove("tblextras");
                    }
                    catch (Exception)
                    {
                    }

                    stbuilder.Append("select fecha_pago as FECHA,VALOR,case forma_pago when '1' then 'Nomina' when '2' then 'Caja' else ' ' end as DescPag,forma_pago AS FORPAG,tipoextra ");
                    stbuilder.Append("FROM cop_extras ");
                    stbuilder.Append("WHERE CODIGOTER='" + codigoter + "' and lincred=" + lincred + " and numero=" + numero);

                    OdbcConnect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscaDatosSolicitudCredito", ref dsextras, "tblextras");
                }

                dsdata.Tables.Add(dsdeduccion.Tables["tbldeducciones"].Copy());
                dsdata.Tables.Add(dsextras.Tables["tblextras"].Copy());
            }
            return dsdata;
        }

        // NOTE: GeneraRevisionProyeccion full implementation is in Part3.cs
        // NOTE: BuscarDeducciones(int, OdbcConnection) overload is in Part3.cs

    } // partial class ClsLiqcreditos
} // namespace ERP.Core.CarteraFinanciera.Services.Creditos
