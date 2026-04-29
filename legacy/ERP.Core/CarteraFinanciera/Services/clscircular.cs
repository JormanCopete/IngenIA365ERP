using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Services
{
    public class clscircular
    {
        private ERP.Core.CarteraFinanciera.Models.ParamCop Paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys Paramsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.Compartido.Datos.ClsConect connect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Reportes.config_report ConfigRepor = new ERP.Core.Compartido.Reportes.config_report();
        private ERP.Core.Compartido.Datos.ClsConect OdbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();

        public clscircular()
        {
            OdbcConnect.MyOdbcConect(varini);
        }

        public bool GenerarCircularCobro(string NumAviso, string empresa, string agencia, string cencosto,
            DateTime fecha, string clades, string claseconcepto, string IncluyeCobro, string ImprimeCodeudor,
            string Linea1, string AplicaLeyArrastre, string conceptosExcluir, string IncluyeCompromisos, bool IncluyeCicloActual,
            string SoloIncumplidos, Form forma, OdbcConnection myconnect, string seguridad = "T")
        {
            BorraDatosCircular(NumAviso, int.Parse(fecha.ToString("yyyyMM")), claseconcepto, myconnect);
            CircularPrestamosyAportes(NumAviso, empresa, agencia, cencosto, fecha, clades, claseconcepto, IncluyeCobro, ImprimeCodeudor, Linea1, conceptosExcluir, IncluyeCompromisos, IncluyeCicloActual, AplicaLeyArrastre, SoloIncumplidos, forma, myconnect);

            if (seguridad == "T")
            {
                if (MessageBox.Show("Desea imprimir las circulares?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    // ImprimirCirculares(NumAviso, int.Parse(fecha.ToString("yyyyMM")), claseconcepto, clades, AplicaLeyArrastre, SoloIncumplidos, empresa, agencia, cencosto, Linea1, ImprimeCodeudor, "00000000000000", "99999999999999", forma, myconnect); // ERROR: CS0103
                }
            }
            return true;
        }

        private bool CircularPrestamosyAportes(string NumAviso, string empresa, string agencia, string cencosto,
            DateTime fecha, string clades, string claseconcepto, string IncluyeCobro, string ImprimeCodeudor,
            string Linea1, string conceptosExcluir, string IncluyeCompromisos, bool IncluyeCicloActual,
            string AplicaLeyArrastre, string SoloIncumplidos, Form forma, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            string stmysql = "", whereleyarrastre = "";
            string whereEmpresa = "", whereagencia = "", whereccosto = "";
            int NUmreg = 0;
            string whereclades = "", wherecobro = "", wherecpto = "", wheredias = "", ImpCodeudor = "N";
            DataSet Mycopmora = new DataSet();
            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Generando Circulares de Cobro", forma);
            int fila = 0;
            string newcedula = "";
            bool ok = false;
            string direccion = "";
            string CiudadCod1 = "999999", CiudadCod2 = "999999", CiudadCod3 = "999999", CiudadCod4 = "999999";
            string DireccionCod1 = "", DireccionCod2 = "", DireccionCod3 = "", DireccionCod4 = "";
            int SW = 0, MaxDias = 0;
            string whereDiasxAsociado = "";

            BarraProgreso.Show();

            if (agencia.Trim() != "Todas")
            {
                whereagencia = " and maenit.agencia='" + ("0000" + agencia.Trim()).Substring(Math.Max(0, ("0000" + agencia.Trim()).Length - 4)) + "' ";
            }

            if (empresa.Trim() != "Todas")
            {
                whereEmpresa = " and maenit.empresa='" + ("0000" + empresa.Trim()).Substring(Math.Max(0, ("0000" + empresa.Trim()).Length - 4)) + "' ";
            }

            if (cencosto.Trim() != "Todos")
            {
                whereccosto = " and maenit.cencosto='" + ("00000000" + cencosto.Trim()).Substring(Math.Max(0, ("00000000" + cencosto.Trim()).Length - 8)) + "' ";
            }

            switch (clades.Trim())
            {
                case "N":
                    whereclades = " and salmaecar.clades='1' ";
                    break;
                case "C":
                    whereclades = " and salmaecar.clades='2' ";
                    break;
            }

            switch (IncluyeCobro)
            {
                case "N":
                    wherecobro = " and maecar.cobrojur<>'Y' and maecar.cobrojur<>'P' ";
                    break;
                case "P":
                    wherecobro = " and maecar.cobrojur<>'Y' ";
                    break;
                case "J":
                    wherecobro = " and maecar.cobrojur<>'P' ";
                    break;
            }

            switch (claseconcepto)
            {
                case "1":
                    wherecpto = " and ((copcircular.lincred>999 and salmaecar.saldo<>0) or (copcircular.lincred<1000 and (salmaecar.cuota<>0 or salmaecar.saldo<>0))) ";
                    break;
                case "2":
                    wherecpto = " and (copcircular.lincred>999 and salmaecar.saldo<>0)   ";
                    break;
                case "3":
                    wherecpto = " and (copcircular.lincred<1000 and (salmaecar.cuota<>0 or salmaecar.saldo<>0))  ";
                    break;
                case "4":
                    wherecpto = " and (copcircular.lincred>999 and salmaecar.saldo<>0) and car12.CODAHOR <> '5'   ";
                    break;
                case "5":
                    wherecpto = " and (copcircular.lincred>999 and salmaecar.saldo<>0) and car12.CODAHOR ='5'   ";
                    break;
            }

            if (!IncluyeCicloActual)
            {
                wheredias = " and copmora.diasmora>0 ";
            }

            if (ImprimeCodeudor == "S")
            {
                ImpCodeudor = "Y";
            }

            if (AplicaLeyArrastre == "N")
            {
                whereDiasxAsociado = " and copcircular.diasmora between par.dia_ini and par.dia_fin ";
            }

            switch (IncluyeCompromisos)
            {
                case "N":
                    switch (varini.pstTipoBD.ToUpper())
                    {
                        case "SQL":
                            stmysql = " and {fn concat(copcircular.codigoter,{fn concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))})} not in " +
                                    "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                    "from cop_novfecgestion nov " +
                                    "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                    "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                            if (AplicaLeyArrastre == "Y")
                            {
                                whereleyarrastre = " and {fn concat(maxcircular.codigoter,{fn concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))})} not in " +
                                                   "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                                   "from cop_novfecgestion nov " +
                                                   "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                                    "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                            }
                            break;
                        case "MYSQL":
                        case "ORACLE":
                        case "POSTGRESQL":
                            stmysql = " and concat(copcircular.codigoter,concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))) not in " +
                                      "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                      "from cop_novfecgestion nov " +
                                      "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                      "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                            if (AplicaLeyArrastre == "Y")
                            {
                                whereleyarrastre = " and concat(maxcircular.codigoter,concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))) not in " +
                                                    "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                                    "from cop_novfecgestion nov " +
                                                    "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                                    "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                            }
                            break;
                    }
                    break;
                case "V":
                    if (SoloIncumplidos == "Y")
                    {
                        switch (varini.pstTipoBD.ToUpper())
                        {
                            case "SQL":
                                stmysql = " and {fn concat(copcircular.codigoter,{fn concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))})} in " +
                                        "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                        "from cop_novfecgestion nov " +
                                        "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                        "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                                if (AplicaLeyArrastre == "Y")
                                {
                                    whereleyarrastre = " and {fn concat(maxcircular.codigoter,{fn concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))})} in " +
                                            "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                            "from cop_novfecgestion nov " +
                                            "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                            "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                                }
                                break;
                            case "MYSQL":
                            case "ORACLE":
                            case "POSTGRESQL":
                                stmysql = " and concat(copcircular.codigoter,concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))) in " +
                                          "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                          "from cop_novfecgestion nov " +
                                          "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                          "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                                if (AplicaLeyArrastre == "Y")
                                {
                                    whereleyarrastre = " and concat(maxcircular.codigoter,concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))) in " +
                                              "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                              "from cop_novfecgestion nov " +
                                              "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                              "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                                }
                                break;
                        }
                    }
                    break;
                case "E":
                    if (SoloIncumplidos == "N")
                    {
                        switch (varini.pstTipoBD.ToUpper())
                        {
                            case "SQL":
                                stmysql = " and {fn concat(copcircular.codigoter,{fn concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))})} not in " +
                                        "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                        "from cop_novfecgestion nov " +
                                        "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                        "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                                if (AplicaLeyArrastre == "Y")
                                {
                                    whereleyarrastre = " and {fn concat(maxcircular.codigoter,{fn concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))})} not in " +
                                            "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                            "from cop_novfecgestion nov " +
                                            "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                            "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                                }
                                break;
                            case "MYSQL":
                            case "ORACLE":
                            case "POSTGRESQL":
                                stmysql = " and concat(copcircular.codigoter,concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))) not in " +
                                          "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                          "from cop_novfecgestion nov " +
                                          "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                          "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                                if (AplicaLeyArrastre == "Y")
                                {
                                    whereleyarrastre = " and concat(maxcircular.codigoter,concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))) not in " +
                                              "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                              "from cop_novfecgestion nov " +
                                              "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                              "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                                }
                                break;
                        }
                    }
                    break;
                case "T":
                    if (SoloIncumplidos == "Y")
                    {
                        switch (varini.pstTipoBD.ToUpper())
                        {
                            case "SQL":
                                stmysql = " and {fn concat(copcircular.codigoter,{fn concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))})} in " +
                                        "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                        "from cop_novfecgestion nov " +
                                        "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                        "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                                if (AplicaLeyArrastre == "Y")
                                {
                                    whereleyarrastre = " and {fn concat(maxcircular.codigoter,{fn concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))})} in " +
                                            "(select {fn concat(nov.codigoter,{fn concat(rtrim(nov.lincred),rtrim(nov.numero))})} " +
                                            "from cop_novfecgestion nov " +
                                            "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                            "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                                }
                                break;
                            case "MYSQL":
                            case "ORACLE":
                            case "POSTGRESQL":
                                stmysql = " and concat(copcircular.codigoter,concat(rtrim(copcircular.lincred),rtrim(copcircular.numero))) in " +
                                          "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                          "from cop_novfecgestion nov " +
                                          "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                          "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";

                                if (AplicaLeyArrastre == "Y")
                                {
                                    whereleyarrastre = " and concat(maxcircular.codigoter,concat(rtrim(maxcircular.lincred),rtrim(maxcircular.numero))) in " +
                                              "(select concat(nov.codigoter,concat(rtrim(nov.lincred),rtrim(nov.numero))) " +
                                              "from cop_novfecgestion nov " +
                                              "where '" + fecha.ToString(varini.PstForFec) + "'<=(select max(fechaCompromiso) from cop_novfecgestion nov2 " +
                                              "where nov2.codigoter=nov.codigoter and nov2.lincred=nov.lincred and nov2.numero=nov.numero)) ";
                                }
                                break;
                        }
                    }
                    break;
            }

            StBuilder.Append("select maenit.codigoter,maenit.nit,copcircular.lincred,copcircular.numero,maenit.nombre,maenit.apellido,");
            StBuilder.Append("maenit.direccion,maenit.telefono1,maenit.envio_dir,maenit.direccion_envio,maenit.dpto_ciudad,ciuaso.nombre_ciudad as ciudadasociado,");
            StBuilder.Append("copmora.periodo_causa,copmora.saldocapital,copmora.saldointeres,copmora.saldoextra,");
            StBuilder.Append("copmora.saldomora,copmora.saldoseguro,copmora.saldoadmon,copmora.saldootros,copmora.diasmora,salmaecar.saldo,");
            StBuilder.Append("salmaecar.clades,cuopen.fecha_movto,car12.descripcion,code1.codigoter as codeudor1,");
            StBuilder.Append("code1.nit as nitcodeudor1,code1.nombre as nomcodeudor1,code1.apellido as apecodeudor1,");
            StBuilder.Append("code1.telefono1 as telcodeudor1,code1.direccion as dircodeudor1,code1.direccion_envio as direnviocodeudor1,");
            StBuilder.Append("code1.envio_dir as enviocodeudor1,ciucod1.nombre_ciudad as ciudadcodeudor1,code2.codigoter as codeudor2,");
            StBuilder.Append("code2.nit as nitcodeudor2,code2.nombre as nomcodeudor2,code2.apellido as apecodeudor2,");
            StBuilder.Append("code2.telefono1 as telcodeudor2,code2.direccion as dircodeudor2,code2.direccion_envio as direnviocodeudor2,");
            StBuilder.Append("code2.envio_dir as enviocodeudor2,ciucod2.nombre_ciudad as ciudadcodeudor2,code3.codigoter as codeudor3,");
            StBuilder.Append("code3.nit as nitcodeudor3,code3.nombre as nomcodeudor3,code3.apellido as apecodeudor3,");
            StBuilder.Append("code3.telefono1 as telcodeudor3,code3.direccion as dircodeudor3,code3.direccion_envio as direnviocodeudor3,");
            StBuilder.Append("code3.envio_dir as enviocodeudor3,ciucod3.nombre_ciudad as ciudadcodeudor3,code4.codigoter as codeudor4,");
            StBuilder.Append("code4.nit as nitcodeudor4,code4.nombre as nomcodeudor4,code4.apellido as apecodeudor4,");
            StBuilder.Append("code4.telefono1 as telcodeudor4,code4.direccion as dircodeudor4,code4.direccion_envio as direnviocodeudor4,");
            StBuilder.Append("code4.envio_dir as enviocodeudor4,ciucod4.nombre_ciudad as ciudadcodeudor4,par.dia_ini,par.dia_fin,par.detalle1,par.detalle2,");
            StBuilder.Append("code1.dpto_ciudad as ciudadcod1,code2.dpto_ciudad as ciudadcod2,code3.dpto_ciudad as ciudadcod3,code4.dpto_ciudad as ciudadcod4, ");
            StBuilder.Append("(select Max(maxcircular.diasmora) from cop_copmoracircular_vw maxcircular ");
            StBuilder.Append("inner join cop_maecar maecar on maxcircular.codigoter=maecar.codigoter and maxcircular.lincred=maecar.lincred and maxcircular.numero=maecar.numero ");
            StBuilder.Append("where maxcircular.codigoter=copcircular.codigoter and maxcircular.periodo_contable=" + fecha.ToString("yyyyMM") + whereleyarrastre + whereclades + " ) as maxdiasmora ");
            StBuilder.Append("from  cop_copmoracircular_vw copcircular ");
            StBuilder.Append("inner join  cop_copmora copmora on copcircular.codigoter=copmora.codigoter and copcircular.lincred=copmora.lincred ");
            StBuilder.Append("and copcircular.numero=copmora.numero and copcircular.periodo_contable=copmora.periodo_contable ");
            StBuilder.Append("inner join  sys_maenit maenit on copcircular.codigoter=maenit.codigoter ");
            StBuilder.Append("inner join  sys_ciudad57 ciuaso on maenit.dpto_ciudad=ciuaso.ciudad ");
            StBuilder.Append("inner join  cop_maecar maecar on copcircular.codigoter=maecar.codigoter and copcircular.lincred=maecar.lincred and copcircular.numero=maecar.numero ");
            StBuilder.Append("inner join  cop_salmaecar salmaecar on copcircular.lincred=salmaecar.lincred and copcircular.numero=salmaecar.numero ");
            StBuilder.Append("and copcircular.codigoter=salmaecar.codigoter and copcircular.periodo_contable=salmaecar.periodo ");
            StBuilder.Append("inner join  cop_cuopen cuopen on copcircular.lincred =cuopen.lincred and copcircular.codigoter =cuopen.codigoter ");
            StBuilder.Append("and copcircular.numero =cuopen.numero and copmora.periodo_causa =cuopen.periodo_causa ");
            StBuilder.Append("inner join  cop_concar12 car12 on copcircular.lincred = car12.lincred ");
            StBuilder.Append("left join  sys_maenit code1 on maecar.codeudor1=code1.codigoter ");
            StBuilder.Append("left join  sys_ciudad57 ciucod1 on code1.dpto_ciudad=ciucod1.ciudad ");
            StBuilder.Append("left join  sys_maenit code2 on maecar.codeudor2=code2.codigoter ");
            StBuilder.Append("left join  sys_ciudad57 ciucod2 on code2.dpto_ciudad=ciucod2.ciudad ");
            StBuilder.Append("left join  sys_maenit code3 on maecar.codeudor3=code3.codigoter ");
            StBuilder.Append("left join  sys_ciudad57 ciucod3 on code3.dpto_ciudad=ciucod3.ciudad ");
            StBuilder.Append("left join  sys_maenit code4 on maecar.codeudor4=code4.codigoter ");
            StBuilder.Append("left join  sys_ciudad57 ciucod4 on code4.dpto_ciudad=ciucod4.ciudad ");
            StBuilder.Append("inner join  cop_paracircular par on par.codigo='" + NumAviso + "'  ");
            StBuilder.Append("inner join  cop_empresa13 empresa13 on empresa13.codigo_empresa = maenit.EMPRESA   ");
            StBuilder.Append("where empresa13.bloquearEmp <>'1'  and  copcircular.periodo_contable=" + fecha.ToString("yyyyMM") + whereDiasxAsociado);
            StBuilder.Append(whereagencia + whereEmpresa + whereccosto + whereclades + wherecobro + wherecpto + wheredias + stmysql);
            StBuilder.Append("and maenit.estado not in ('R','T') and copcircular.lincred not in (" + conceptosExcluir + ") and ");
            StBuilder.Append("(copmora.saldocapital<>0 or copmora.saldoextra<>0 or copmora.saldointeres<>0 or copmora.saldomora<>0 or ");
            StBuilder.Append("copmora.saldoadmon<>0 or copmora.saldoseguro<>0 or copmora.saldootros<>0) ");
            StBuilder.Append("order by copcircular.codigoter,copcircular.lincred,copcircular.numero,cuopen.periodo_causa");

            try
            {
                connect.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "CircularPrestamosyAportes", Mycopmora, "Circulares");
                NUmreg = Mycopmora.Tables["Circulares"].Rows.Count;

                BarraProgreso.ValorMinimoMaximo(0, NUmreg);

                while (fila < NUmreg)
                {
                    SW = 0;
                    DataRow row = Mycopmora.Tables["Circulares"].Rows[fila];

                    if (AplicaLeyArrastre == "Y")
                    {
                        if (row["maxdiasmora"] == DBNull.Value)
                        {
                            MaxDias = 0;
                        }
                        else
                        {
                            MaxDias = Convert.ToInt32(row["maxdiasmora"]);
                        }
                        if (MaxDias < Convert.ToInt32(row["dia_ini"]) || MaxDias > Convert.ToInt32(row["dia_fin"]))
                        {
                            SW = 1;
                        }
                    }

                    if (SW == 0)
                    {
                        if (newcedula != row["codigoter"].ToString())
                        {
                            switch (row["envio_dir"].ToString())
                            {
                                case "1":
                                    direccion = row["direccion"].ToString();
                                    break;
                                case "2":
                                    direccion = row["direccion_envio"].ToString().Trim() != "" ? row["direccion_envio"].ToString() : row["direccion"].ToString();
                                    break;
                                default:
                                    direccion = row["direccion"].ToString();
                                    break;
                            }
                            GrabarMaestroCircular(NumAviso, row["codigoter"].ToString(), int.Parse(fecha.ToString("yyyyMM")), claseconcepto, fecha, direccion, row["telefono1"].ToString(), Convert.ToInt32(row["dpto_ciudad"]), row["detalle1"].ToString(), row["detalle2"].ToString(), Convert.ToInt32(row["dia_ini"]), Convert.ToInt32(row["dia_fin"]), ImpCodeudor, AplicaLeyArrastre, myconnect);
                            newcedula = row["codigoter"].ToString();
                        }

                        switch (row["enviocodeudor1"].ToString())
                        {
                            case "1":
                                DireccionCod1 = row["dircodeudor1"].ToString();
                                break;
                            case "2":
                                DireccionCod1 = row["direnviocodeudor1"].ToString().Trim() != "" ? row["direnviocodeudor1"].ToString() : row["dircodeudor1"].ToString();
                                break;
                            default:
                                DireccionCod1 = row["dircodeudor1"].ToString();
                                break;
                        }
                        switch (row["enviocodeudor2"].ToString())
                        {
                            case "1":
                                DireccionCod2 = row["dircodeudor2"].ToString();
                                break;
                            case "2":
                                DireccionCod2 = row["direnviocodeudor2"].ToString().Trim() != "" ? row["direnviocodeudor2"].ToString() : row["dircodeudor2"].ToString();
                                break;
                            default:
                                DireccionCod2 = row["dircodeudor2"].ToString();
                                break;
                        }
                        switch (row["enviocodeudor3"].ToString())
                        {
                            case "1":
                                DireccionCod3 = row["dircodeudor3"].ToString();
                                break;
                            case "2":
                                DireccionCod3 = row["direnviocodeudor3"].ToString().Trim() != "" ? row["direnviocodeudor3"].ToString() : row["dircodeudor3"].ToString();
                                break;
                            default:
                                DireccionCod3 = row["dircodeudor3"].ToString();
                                break;
                        }
                        switch (row["enviocodeudor4"].ToString())
                        {
                            case "1":
                                DireccionCod4 = row["dircodeudor4"].ToString();
                                break;
                            case "2":
                                DireccionCod4 = row["direnviocodeudor4"].ToString().Trim() != "" ? row["direnviocodeudor4"].ToString() : row["dircodeudor4"].ToString();
                                break;
                            default:
                                DireccionCod4 = row["dircodeudor4"].ToString();
                                break;
                        }

                        CiudadCod1 = row["ciudadcod1"] == DBNull.Value ? "999999" : row["ciudadcod1"].ToString();
                        CiudadCod2 = row["ciudadcod2"] == DBNull.Value ? "999999" : row["ciudadcod2"].ToString();
                        CiudadCod3 = row["ciudadcod3"] == DBNull.Value ? "999999" : row["ciudadcod3"].ToString();
                        CiudadCod4 = row["ciudadcod4"] == DBNull.Value ? "999999" : row["ciudadcod4"].ToString();

                        GrabarDetalleCircular(NumAviso, row["codigoter"].ToString(), int.Parse(fecha.ToString("yyyyMM")), claseconcepto, Convert.ToInt32(row["lincred"]), Convert.ToDouble(row["numero"]), Convert.ToInt32(row["periodo_causa"]), Convert.ToDouble(row["saldocapital"]), Convert.ToDouble(row["saldoextra"]),
                            Convert.ToDouble(row["saldointeres"]), Convert.ToDouble(row["saldomora"]), Convert.ToDouble(row["saldoseguro"]), Convert.ToDouble(row["saldoadmon"]), Convert.ToDouble(row["saldootros"]), Convert.ToInt32(row["diasmora"]), Convert.ToDouble(row["saldo"]), Convert.ToDateTime(row["fecha_movto"]),
                            row["codeudor1"].ToString(), row["telcodeudor1"].ToString(), DireccionCod1, CiudadCod1, row["codeudor2"].ToString(), row["telcodeudor2"].ToString(), DireccionCod2, CiudadCod2,
                            row["codeudor3"].ToString(), row["telcodeudor3"].ToString(), DireccionCod3, CiudadCod3, row["codeudor4"].ToString(), row["telcodeudor4"].ToString(), DireccionCod4, CiudadCod4, SoloIncumplidos, myconnect);
                        ok = true;
                    }

                    BarraProgreso.PerformStep();
                    fila += 1;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.ToString(), "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ok = false;
            }

            BarraProgreso.Dispose();
            BarraProgreso.Close();

            return ok;
        }

        public bool BuscarMaestroCircular(string codigoter, int periodo, string numaviso, string clacpto, OdbcConnection myconnect, ref DataSet dsmaecircular)
        {
            DataSet dsdata = new DataSet();
            bool ok;
            StringBuilder stbuilder = new StringBuilder();

            codigoter = ("00000000000000" + codigoter).Substring(Math.Max(0, ("00000000000000" + codigoter).Length - 14));

            stbuilder.Append("select numaviso,codigoter,periodo,clasecpto,fecha,direccion,telefono,codciudad,");
            stbuilder.Append("detalle1,detalle2,dia_ini,dia_fin,enviacodeudor,leyarrastre ");
            stbuilder.Append("from cop_maecircobro ");
            stbuilder.Append("where codigoter='" + codigoter + "' and numaviso='" + numaviso + "' and clasecpto='" + clacpto + "' and periodo=" + periodo);

            ok = connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarMaestroCircular", dsdata, "tblmaecircular");

            if (dsmaecircular != null)
            {
                dsmaecircular.Tables.Add(dsdata.Tables["tblmaecircular"].Copy());
            }
            return ok;
        }

        private bool BuscarMaestroCircular(string codigoter, int periodo, string numaviso, string clacpto, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            codigoter = ("00000000000000" + codigoter).Substring(Math.Max(0, ("00000000000000" + codigoter).Length - 14));

            stbuilder.Append("select numaviso,codigoter,periodo,clasecpto,fecha,direccion,telefono,codciudad,");
            stbuilder.Append("detalle1,detalle2,dia_ini,dia_fin,enviacodeudor,leyarrastre ");
            stbuilder.Append("from cop_maecircobro ");
            stbuilder.Append("where codigoter='" + codigoter + "' and numaviso='" + numaviso + "' and clasecpto='" + clacpto + "' and periodo=" + periodo);

            return connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarMaestroCircular", dsdata, "tblmaecircular");
        }

        private bool GrabarMaestroCircular(string numaviso, string codigoter, int periodo, string clasecpto, DateTime fecha, string direccion,
            string telefono, int codciudad, string detalle1, string detalle2, int diaini, int diafin, string enviacodeudor, string leyarrastre, OdbcConnection myconnect)
        {
            bool ok;
            StringBuilder stbuilder = new StringBuilder();

            ok = BuscarMaestroCircular(codigoter, periodo, numaviso, clasecpto, myconnect);

            if (!ok)
            {
                stbuilder.Append("insert into cop_maecircobro (numaviso,codigoter,periodo,clasecpto,fecha,");
                stbuilder.Append("direccion, telefono, codciudad,detalle1,");
                stbuilder.Append("detalle2,dia_ini,dia_fin,enviacodeudor,leyarrastre) values(");
                stbuilder.Append("'" + numaviso + "','" + codigoter + "'," + periodo + ",'" + clasecpto + "','" + fecha.ToString(varini.PstForFec) + "',");
                stbuilder.Append("'" + direccion + "','" + telefono + "'," + codciudad + ",'" + detalle1 + "',");
                stbuilder.Append("'" + detalle2 + "'," + diaini + "," + diafin + ",'" + enviacodeudor + "','" + leyarrastre + "')");
            }
            else
            {
                stbuilder.Append("update cop_maecircobro set fecha='" + fecha.ToString(varini.PstForFec) + "',");
                stbuilder.Append("direccion='" + direccion + "',telefono='" + telefono + "',codciudad=" + codciudad + ",");
                stbuilder.Append("detalle1='" + detalle1 + "',detalle2='" + detalle2 + "',dia_ini=" + diaini + ",dia_fin=" + diafin + ",enviacodeudor='" + enviacodeudor + "',leyarrastre='" + leyarrastre + "' ");
                stbuilder.Append("where numaviso='" + numaviso + "' and codigoter='" + codigoter + "' and clasecpto='" + clasecpto + "' and periodo=" + periodo);
            }

            ok = connect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarMaestroCircular");
            return ok;
        }

        public bool BuscarDetalleCircular(string codigoter, int periodo, string numaviso, string clasecpto,
            int lincred, double numero, int ciclo, OdbcConnection myconnect, ref DataSet dsdetcircular)
        {
            DataSet dsdata = new DataSet();
            bool ok;
            StringBuilder stbuilder = new StringBuilder();

            codigoter = ("00000000000000" + codigoter).Substring(Math.Max(0, ("00000000000000" + codigoter).Length - 14));

            stbuilder.Append("select numaviso,codigoter,periodo,clasecpto,lincred,numero,ciclo,saldocapital,");
            stbuilder.Append("saldoextra,saldointeres,saldomora,saldoseguro,saldoadmon,saldootros,diasmora,saldo,");
            stbuilder.Append("fechavemto,codeudor1,telefono1,direccion1,ciudad1,codeudor2,telefono2,direccion2,ciudad2,");
            stbuilder.Append("codeudor3,telefono3,direccion3,ciudad3,codeudor4,telefono4,direccion4,ciudad4,soloincumplidos ");
            stbuilder.Append("from cop_detcircobro ");
            stbuilder.Append("where numaviso='" + numaviso + "' and codigoter='" + codigoter + "' and periodo=" + periodo);
            stbuilder.Append(" and clasecpto='" + clasecpto + "' and lincred=" + lincred + " and numero=" + numero + " and ciclo=" + ciclo);

            ok = connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarDetalleCircular", dsdata, "tbldetalle");

            if (dsdetcircular != null)
            {
                dsdetcircular.Tables.Add(dsdata.Tables["tbldetalle"].Copy());
            }
            return ok;
        }

        private bool BuscarDetalleCircular(string codigoter, int periodo, string numaviso, string clasecpto,
            int lincred, double numero, int ciclo, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            StringBuilder stbuilder = new StringBuilder();

            codigoter = ("00000000000000" + codigoter).Substring(Math.Max(0, ("00000000000000" + codigoter).Length - 14));

            stbuilder.Append("select numaviso,codigoter,periodo,clasecpto,lincred,numero,ciclo,saldocapital,");
            stbuilder.Append("saldoextra,saldointeres,saldomora,saldoseguro,saldoadmon,saldootros,diasmora,saldo,");
            stbuilder.Append("fechavemto,codeudor1,telefono1,direccion1,ciudad1,codeudor2,telefono2,direccion2,ciudad2,");
            stbuilder.Append("codeudor3,telefono3,direccion3,ciudad3,codeudor4,telefono4,direccion4,ciudad4,soloincumplidos ");
            stbuilder.Append("from cop_detcircobro ");
            stbuilder.Append("where numaviso='" + numaviso + "' and codigoter='" + codigoter + "' and periodo=" + periodo);
            stbuilder.Append(" and clasecpto='" + clasecpto + "' and lincred=" + lincred + " and numero=" + numero + " and ciclo=" + ciclo);

            return connect.ExecuteQueryDataset(stbuilder.ToString(), myconnect, "BuscarDetalleCircular", dsdata, "tbldetalle");
        }

        private bool GrabarDetalleCircular(string numaviso, string codigoter, int periodo, string clasecpto, int lincred, double numero,
            int ciclo, double saldocapital, double saldoextra, double saldointeres, double saldomora, double saldoseguro,
            double saldoadmon, double saldootros, int diasmora, double saldo, DateTime fechavemto, string codeudor1,
            string telefono1, string direccion1, string ciudad1, string codeudor2, string telefono2, string direccion2, string ciudad2,
            string codeudor3, string telefono3, string direccion3, string ciudad3, string codeudor4, string telefono4, string direccion4,
            string ciudad4, string soloincumplidos, OdbcConnection myconnect)
        {
            bool ok;
            StringBuilder stbuilder = new StringBuilder();

            ok = BuscarDetalleCircular(codigoter, periodo, numaviso, clasecpto, lincred, numero, ciclo, myconnect);

            if (!ok)
            {
                stbuilder.Append("insert into cop_detcircobro (numaviso,codigoter,periodo,clasecpto,lincred,");
                stbuilder.Append("numero,ciclo,saldocapital,saldoextra,");
                stbuilder.Append("saldointeres,saldomora,saldoseguro,saldoadmon,");
                stbuilder.Append("saldootros,diasmora,saldo,fechavemto,");
                stbuilder.Append("codeudor1,telefono1,direccion1,ciudad1,");
                stbuilder.Append("codeudor2,telefono2,direccion2,ciudad2,");
                stbuilder.Append("codeudor3,telefono3,direccion3,ciudad3,");
                stbuilder.Append("codeudor4,telefono4,direccion4,ciudad4,soloincumplidos) values (");
                stbuilder.Append("'" + numaviso + "','" + codigoter + "'," + periodo + ",'" + clasecpto + "'," + lincred + ",");
                stbuilder.Append(numero + "," + ciclo + "," + saldocapital + "," + saldoextra + ",");
                stbuilder.Append(saldointeres + "," + saldomora + "," + saldoseguro + "," + saldoadmon + ",");
                stbuilder.Append(saldootros + "," + diasmora + "," + saldo + ",'" + fechavemto.ToString(varini.PstForFec) + "',");
                stbuilder.Append("'" + codeudor1 + "','" + telefono1 + "','" + direccion1 + "','" + ciudad1 + "',");
                stbuilder.Append("'" + codeudor2 + "','" + telefono2 + "','" + direccion2 + "','" + ciudad2 + "',");
                stbuilder.Append("'" + codeudor3 + "','" + telefono3 + "','" + direccion3 + "','" + ciudad3 + "',");
                stbuilder.Append("'" + codeudor4 + "','" + telefono4 + "','" + direccion4 + "','" + ciudad4 + "','" + soloincumplidos + "')");
            }
            else
            {
                stbuilder.Append("update cop_detcircobro set fechavemto='" + fechavemto.ToString(varini.PstForFec) + "',");
                stbuilder.Append("saldocapital=" + saldocapital + ",saldoextra=" + saldoextra + ",saldointeres=" + saldointeres + ",");
                stbuilder.Append("saldomora=" + saldomora + ",saldoseguro=" + saldoseguro + ",saldoadmon=" + saldoadmon + ",saldootros=" + saldootros + ",");
                stbuilder.Append("diasmora=" + diasmora + ",saldo=" + saldo + ",codeudor1='" + codeudor1 + "',codeudor2='" + codeudor2 + "',codeudor3='" + codeudor3 + "',codeudor4='" + codeudor4 + "',");
                stbuilder.Append("telefono1='" + telefono1 + "',telefono2='" + telefono2 + "',telefono3='" + telefono3 + "',telefono4='" + telefono4 + "',");
                stbuilder.Append("direccion1='" + direccion1 + "',direccion2='" + direccion2 + "',direccion3='" + direccion3 + "',direccion4='" + direccion4 + "',");
                stbuilder.Append("ciudad1='" + ciudad1 + "',ciudad2='" + ciudad2 + "',ciudad3='" + ciudad3 + "',ciudad4='" + ciudad4 + "',soloincumplidos='" + soloincumplidos + "' ");
                stbuilder.Append("where numaviso='" + numaviso + "' and codigoter='" + codigoter + "' and periodo=" + periodo);
                stbuilder.Append(" and clasecpto= '" + clasecpto + "' and lincred=" + lincred + " and numero=" + numero + " and ciclo=" + ciclo);
            }

            ok = connect.ExecuteQueryconec(stbuilder.ToString(), myconnect, "GrabarDetalleCircular");
            return ok;
        }

        public void BorraDatosCircular(string NumAviso, int periodo, string clasecpto, OdbcConnection myconnect)
        {
            string stmysql;
            bool ok;

            stmysql = "delete from cop_detcircobro where numaviso='" + NumAviso + "' and clasecpto='" + clasecpto + "' and periodo=" + periodo;
            ok = connect.ExecuteQueryconec(stmysql, myconnect, "BorraDatosCircular(detalle)");

            if (ok)
            {
                stmysql = "delete from cop_maecircobro where numaviso='" + NumAviso + "' and clasecpto='" + clasecpto + "' and periodo=" + periodo;
                ok = connect.ExecuteQueryconec(stmysql, myconnect, "BorraDatosCircular(Maestro)");
            }
        }

#if CRYSTAL_LEGACY
        public void ImprimirCirculares(string NumAviso, int periodo, string clasecpto,
            string clades, string leyarrastre, string soloincumplidos, string empresa,
            string agencia, string cencosto, string linea1, string impcodeudores,
            string codigoterini, string codigoterfin, Form forma, OdbcConnection myconnect)
        {
            CrystalDecisions.CrystalReports.Engine.ReportDocument repor;
            DataSet dscompania = new DataSet();

            Paramsys.BuscarCompania(varini.sptCodEmpr, dscompania, myconnect);

            switch (clasecpto)
            {
                case "1":
                case "2":
                case "4":
                case "5":
                    if (leyarrastre == "Y")
                    {
                        repor = new ERP.Core.Compartido.Reportes.reporte("cop_repor_circularunica");
                    }
                    else
                    {
                        repor = new ERP.Core.Compartido.Reportes.reporte("cop_repor_circularcobro");
                    }
                    break;
                case "3":
                    repor = new ERP.Core.Compartido.Reportes.reporte("cop_rcircularaportes");
                    break;
                default:
                    repor = new ERP.Core.Compartido.Reportes.reporte("cop_repor_circularcobro");
                    break;
            }

            repor.SetParameterValue("periodo", periodo);
            repor.SetParameterValue("numaviso", NumAviso);
            repor.SetParameterValue("clasecpto", clasecpto);
            repor.SetParameterValue("clades", clades);
            repor.SetParameterValue("leyarrastre", leyarrastre);
            repor.SetParameterValue("soloincumplidos", soloincumplidos);
            repor.SetParameterValue("empresa", empresa);
            repor.SetParameterValue("agencia", agencia);
            repor.SetParameterValue("cencosto", cencosto);
            repor.SetParameterValue("linea1", linea1);
            repor.SetParameterValue("codeudores", impcodeudores);
            repor.SetParameterValue("codigoterini", codigoterini);
            repor.SetParameterValue("codigoterfin", codigoterfin);
            repor.SetParameterValue("NomEmpresa", dscompania.Tables["tblcompania"].Rows[0]["nombre"]);
            repor.SetParameterValue("DirEmpresa", dscompania.Tables["tblcompania"].Rows[0]["Direccion"]);
            repor.SetParameterValue("NitEmpresa", dscompania.Tables["tblcompania"].Rows[0]["NIT"]);
            repor.SetParameterValue("TelEmpresa", dscompania.Tables["tblcompania"].Rows[0]["TELEFONO"]);
            repor.SetParameterValue("CiudadEmpresa", dscompania.Tables["tblcompania"].Rows[0]["Ciudad"]);

            ConfigRepor.confi_reportes(forma, repor);
        }
#endif
    }
}
