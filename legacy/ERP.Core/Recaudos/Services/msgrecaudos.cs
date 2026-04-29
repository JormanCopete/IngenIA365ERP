using System;
using System.Data;
using System.Data.Odbc;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;

namespace ERP.Core.Recaudos.Services
{
    public class msgrecaudos
    {
        private string SqlDml;
        private string NombreProcedimiento;
        private ERP.Core.Compartido.Datos.ClsConect OdbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini;

        public enum Manipula
        {
            Crear = 1,
            Actualizar = 2,
            Consultar = 3,
            Eliminar = 4,
            BuscaPrimero = 5,
            BuscaAnterior = 6,
            BuscaSiguiente = 7,
            BuscaUltimo = 8,
            Adelanta = 9,
            Abrir = 10,
            Cerrar = 11
        }

        public bool ManipulaTipoconceptos(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref string convenio, ref string concepto, ref string nombre, ref string tipo, ref DataTable TablaDatos)
        {
            DataTable Tabla = new DataTable();

            NombreProcedimiento = "ManipulaTipoconceptos - " + OpcionManipula.ToString();
            switch (OpcionManipula)
            {
                case Manipula.Crear:
                    SqlDml = " insert into rec_tipocomceptos(convenio,concepto,nombre,tipo)" +
                             " values('" + convenio + "','" + concepto + "','" + nombre + "','" + tipo + "')";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Actualizar:
                    SqlDml = " update rec_tipocomceptos set nombre='" + nombre + "',tipo='" + tipo + "' where convenio='" + convenio + "' and concepto='" + concepto + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Consultar:
                    SqlDml = " select convenio, concepto, nombre, tipo from rec_tipocomceptos where convenio='" + convenio + "'";

                    // if (OdbcConnect.ExecuteConsulta(SqlDml, CadenaConexion, NombreProcedimiento, Tabla) == true) // ERROR: CS1620
                    {
                        if (TablaDatos == null)
                        {
                            DataRow row = Tabla.Rows[0];
                            convenio = row["convenio"].ToString();
                            concepto = row["concepto"].ToString();
                            nombre = row["nombre"].ToString();
                            tipo = row["tipo"].ToString();
                        }
                        else
                        {
                            TablaDatos = Tabla;
                        }
                        return true;
                    }
                    // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                    {
                        return false;
                    }

                case Manipula.Eliminar:
                    SqlDml = " delete from rec_tipocomceptos where convenio='" + convenio + "' and concepto='" + concepto + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
            }
            return false;
        }

        public bool ManipulaTipoconceptos(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref string convenio, ref string concepto, ref string nombre, ref string tipo)
        {
            DataTable TablaDatos = null;
            return ManipulaTipoconceptos(CadenaConexion, ref OpcionManipula, ref convenio, ref concepto, ref nombre, ref tipo, ref TablaDatos);
        }

        public bool ManipulaParametros(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref string convenio, ref string nombreconvenio,
            ref string estado, ref double consecutivopagos, ref DataTable TablaDatos, ref string rutaentrada,
            ref string rutasalida, ref string rutasaleserver, ref string rutaentraserver, ref int periodoexporta,
            ref int periodoimporta, ref int horainiexporta, ref int horafinexporta, ref int horainiimporta,
            ref int horafinimporta, ref string correoerrores)
        {
            DataTable Tabla = new DataTable();

            NombreProcedimiento = "ManipulaParametros - " + OpcionManipula.ToString();
            switch (OpcionManipula)
            {
                case Manipula.Crear:
                    SqlDml = " insert into rec_parametros(convenio,nombreconvenio,consecutivopagos,estado,rutaentrada,rutasalida,rutasaleserver,rutaentraserver,periodoexporta,periodoimporta,horainiexporta,horafinexporta,horainiimporta,horafinimporta,correoerrores)" +
                             " values('" + convenio + "','" + nombreconvenio + "'," + consecutivopagos + ",'" + estado + "','" + rutaentrada + "','" + rutasalida +
                             "','" + rutasaleserver + "','" + rutaentraserver + "'," + periodoexporta + "," + periodoimporta + "," + horainiexporta + "," + horafinexporta + "," + horainiimporta + "," + horafinimporta + ",'" + correoerrores + "')";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Actualizar:
                    SqlDml = " update rec_parametros set nombreconvenio='" + nombreconvenio + "',consecutivopagos=" + consecutivopagos +
                             " ,estado='" + estado + "',rutaentrada='" + rutaentrada + "',rutasalida='" + rutasalida + "',rutasaleserver='" + rutasaleserver +
                             "',rutaentraserver='" + rutaentraserver + "',periodoexporta=" + periodoexporta + ",periodoimporta=" + periodoimporta +
                             ",horainiexporta=" + horainiexporta + ",horafinexporta=" + horafinexporta + ",horainiimporta=" + horainiimporta +
                             ",horafinimporta=" + horafinimporta + ",correoerrores='" + correoerrores + "' where convenio='" + convenio + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Consultar:
                    SqlDml = " select convenio, nombreconvenio, consecutivopagos,estado,rutaentrada,rutasalida,rutasaleserver,rutaentraserver,periodoexporta,periodoimporta,horainiexporta,horafinexporta,horainiimporta,horafinimporta,correoerrores from rec_parametros where convenio='" + convenio + "'";

                    // if (OdbcConnect.ExecuteConsulta(SqlDml, CadenaConexion, NombreProcedimiento, Tabla) == true) // ERROR: CS1620
                    {
                        if (TablaDatos == null)
                        {
                            DataRow row = Tabla.Rows[0];
                            convenio = row["convenio"].ToString();
                            nombreconvenio = row["nombreconvenio"].ToString();
                            consecutivopagos = Convert.ToDouble(row["consecutivopagos"]);
                            estado = row["estado"].ToString();
                            rutaentrada = row["rutaentrada"].ToString();
                            rutasalida = row["rutasalida"].ToString();
                            rutasaleserver = row["rutasaleserver"].ToString();
                            rutaentraserver = row["rutaentraserver"].ToString();
                            periodoexporta = Convert.ToInt32(row["periodoexporta"]);
                            periodoimporta = Convert.ToInt32(row["periodoimporta"]);
                            horainiexporta = Convert.ToInt32(row["horainiexporta"]);
                            horafinexporta = Convert.ToInt32(row["horafinexporta"]);
                            horainiimporta = Convert.ToInt32(row["horainiimporta"]);
                            horafinimporta = Convert.ToInt32(row["horafinimporta"]);
                            correoerrores = row["correoerrores"].ToString();
                        }
                        else
                        {
                            TablaDatos = Tabla;
                        }
                        return true;
                    }
                    // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                    {
                        return false;
                    }

                case Manipula.Eliminar:
                    SqlDml = " delete from rec_parametros where convenio='" + convenio + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Adelanta:
                    SqlDml = " update rec_parametros set consecutivopagos=" + consecutivopagos +
                             " where convenio='" + convenio + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
            }

            return false;
        }

        public bool ManipulaParametros(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref string convenio)
        {
            string nombreconvenio = "";
            string estado = "A";
            double consecutivopagos = 0;
            DataTable TablaDatos = null;
            string rutaentrada = "";
            string rutasalida = "";
            string rutasaleserver = "";
            string rutaentraserver = "";
            int periodoexporta = 0;
            int periodoimporta = 0;
            int horainiexporta = 0;
            int horafinexporta = 0;
            int horainiimporta = 0;
            int horafinimporta = 0;
            string correoerrores = "";
            return ManipulaParametros(CadenaConexion, ref OpcionManipula, ref convenio, ref nombreconvenio,
                ref estado, ref consecutivopagos, ref TablaDatos, ref rutaentrada,
                ref rutasalida, ref rutasaleserver, ref rutaentraserver, ref periodoexporta,
                ref periodoimporta, ref horainiexporta, ref horafinexporta, ref horainiimporta,
                ref horafinimporta, ref correoerrores);
        }

        public bool ManipulaConceptos(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref string referencia, ref string cedula, ref string nombre,
            ref string programa, ref string periodo, ref double valor, ref DateTime fechavence, ref string codbarra,
            ref string refeelectronica, ref string concepto, ref string convenio, ref string estado, ref DataTable TablaDatos)
        {
            DataTable Tabla = new DataTable();

            NombreProcedimiento = "ManipulaConceptos - " + OpcionManipula.ToString();
            switch (OpcionManipula)
            {
                case Manipula.Crear:
                    SqlDml = " insert into rec_comceptos(referencia,cedula,nombre,programa,periodo,valor,fechavence,codbarra,refeelectronica,concepto,convenio,estado) " +
                                      " values('" + referencia + "','" + cedula + "','" + nombre + "','" + programa + "','" + periodo + "'," + valor + ",'" + fechavence.ToString(varini.PstForFec) + "','" + codbarra + "','" + refeelectronica + "','" + concepto + "','" + convenio + "')";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Actualizar:
                    SqlDml = " update rec_comceptos set cedula='" + cedula + "',nombre='" + nombre + "',programa='" + programa + "',periodo='" + periodo + "'," +
                             "valor=" + valor + ",fechavence='" + fechavence.ToString(varini.PstForFec) + "',codbarra='" + codbarra + "'," +
                             "refeelectronica='" + refeelectronica + "',concepto='" + concepto + "',convenio='" + convenio + "',estado='" + estado + "' where referencia='" + referencia + "' and concepto='" + concepto + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Consultar:
                    SqlDml = " select referencia,cedula,nombre,programa,periodo,valor,fechavence,codbarra,refeelectronica,concepto,convenio,estado from rec_comceptos where referencia='" + referencia + "' and concepto='" + concepto + "' and convenio='" + convenio + "'";

                    // if (OdbcConnect.ExecuteConsulta(SqlDml, CadenaConexion, NombreProcedimiento, Tabla) == true) // ERROR: CS1620
                    {
                        if (TablaDatos == null)
                        {
                            DataRow row = Tabla.Rows[0];
                            referencia = row["referencia"].ToString();
                            cedula = row["cedula"].ToString();
                            nombre = row["nombre"].ToString();
                            programa = row["programa"].ToString();
                            periodo = row["periodo"].ToString();
                            valor = Convert.ToDouble(row["valor"]);
                            fechavence = Convert.ToDateTime(row["fechavence"]);
                            codbarra = row["codbarra"].ToString();
                            refeelectronica = row["refeelectronica"].ToString();
                            concepto = row["concepto"].ToString();
                            convenio = row["convenio"].ToString();
                            estado = row["estado"].ToString();
                        }
                        else
                        {
                            TablaDatos = Tabla;
                        }
                        return true;
                    }
                    // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                    {
                        return false;
                    }

                case Manipula.Eliminar:
                    SqlDml = " delete from rec_comceptos where referencia='" + referencia + "' and concepto='" + concepto + "' and convenio='" + convenio + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Cerrar:
                    SqlDml = " update rec_comceptos set estado='C' where referencia='" + referencia + "' and concepto='" + concepto + "' and convenio='" + convenio + "'";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
            }

            return false;
        }

        public bool ManipulaConceptos(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref string referencia, ref string concepto, ref string convenio)
        {
            string cedula = "";
            string nombre = "";
            string programa = "";
            string periodo = "";
            double valor = 0;
            DateTime fechavence = new DateTime(1950, 1, 1);
            string codbarra = "";
            string refeelectronica = "";
            string estado = "A";
            DataTable TablaDatos = null;
            return ManipulaConceptos(CadenaConexion, ref OpcionManipula, ref referencia, ref cedula, ref nombre,
                ref programa, ref periodo, ref valor, ref fechavence, ref codbarra,
                ref refeelectronica, ref concepto, ref convenio, ref estado, ref TablaDatos);
        }

        public bool ManipulaPagos(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref double numero, ref string referencia, ref string concepto,
            ref string detalle, ref DateTime fecha, ref DateTime fachasys, ref string usuario, ref string nomusu,
            ref double valor, ref string hora, ref string convenio, ref string estado, ref DataTable TablaDatos, string filtro,
            ref string cerrado, ref string usuarioautoriza, ref double efectivo, ref double tdebito)
        {
            DataTable Tabla = new DataTable();

            NombreProcedimiento = "ManipulaPagos - " + OpcionManipula.ToString();
            switch (OpcionManipula)
            {
                case Manipula.Crear:
                    SqlDml = " insert into rec_pagocomceptos(numero, referencia, concepto, detalle, fecha, fachasys, usuario, nomusu, valor,hora,estado,convenio,usuarioautoriza,efectivo,tdebito) " +
                             " values(" + numero + ",'" + referencia + "','" + concepto + "','" + detalle + "','" + fecha.ToString(varini.PstForFec) +
                             "','" + fachasys.ToString(varini.pstForfecyHora) + "','" + usuario + "','" + nomusu + "'," + valor + ",'" + hora + "','" + estado + "','" + convenio + "','" + usuarioautoriza + "'," + efectivo + "," + tdebito + ")";

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Actualizar:
                    SqlDml = " update rec_pagocomceptos set referencia='" + referencia + "', concepto='" + concepto + "', detalle='" + detalle + "'," +
                             " fecha='" + fecha.ToString(varini.PstForFec) + "', fachasys='" + fachasys.ToString(varini.pstForfecyHora) + "', usuario='" + usuario + "'," +
                             " nomusu='" + nomusu + "', valor=" + valor + ",hora='" + hora + "',convenio='" + convenio + "',estado='" + estado +
                             "',usuarioautoriza='" + usuarioautoriza + "',efectivo=" + efectivo + ", tdebito=" + tdebito + " where numero=" + numero;

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Consultar:
                    SqlDml = " select g.numero, g.referencia, g.concepto, g.detalle, g.fecha, g.fachasys, g.usuario, g.nomusu, g.usuarioautoriza, g.valor, g.hora, g.estado," +
                             "g.convenio, g.cerrado,g.efectivo,g.tdebito, p.nombreconvenio from rec_pagocomceptos g inner join rec_parametros p on g.convenio=p.convenio " + filtro;

                    // if (OdbcConnect.ExecuteConsulta(SqlDml, CadenaConexion, NombreProcedimiento, Tabla) == true) // ERROR: CS1620
                    {
                        if (TablaDatos == null)
                        {
                            DataRow row = Tabla.Rows[0];
                            numero = Convert.ToDouble(row["numero"]);
                            referencia = row["referencia"].ToString();
                            concepto = row["concepto"].ToString();
                            detalle = row["detalle"].ToString();
                            fecha = Convert.ToDateTime(row["fecha"]);
                            fachasys = Convert.ToDateTime(row["fachasys"]);
                            usuario = row["usuario"].ToString();
                            nomusu = row["nomusu"].ToString();
                            valor = Convert.ToDouble(row["valor"]);
                            hora = row["hora"].ToString();
                            convenio = row["convenio"].ToString();
                            estado = row["estado"].ToString();
                            cerrado = row["cerrado"].ToString();
                            usuarioautoriza = row["usuarioautoriza"].ToString();
                            efectivo = Convert.ToDouble(row["efectivo"]);
                            tdebito = Convert.ToDouble(row["tdebito"]);
                        }
                        else
                        {
                            TablaDatos = Tabla;
                        }
                        return true;
                    }
                    // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                    {
                        return false;
                    }

                case Manipula.Eliminar:
                    SqlDml = " delete from rec_pagocomceptos where numero=" + numero;

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                case Manipula.Cerrar:
                    SqlDml = " update rec_pagocomceptos set cerrado='" + cerrado + "' where numero=" + numero;

                    return OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
            }

            return false;
        }

        public bool ManipulaPagos(OdbcConnection CadenaConexion, ref Manipula OpcionManipula, ref double numero)
        {
            string referencia = "";
            string concepto = "";
            string detalle = "";
            DateTime fecha = new DateTime(1950, 1, 1);
            DateTime fachasys = new DateTime(1950, 1, 1);
            string usuario = "";
            string nomusu = "";
            double valor = 0;
            string hora = "";
            string convenio = "";
            string estado = "A";
            DataTable TablaDatos = null;
            string filtro = "";
            string cerrado = "N";
            string usuarioautoriza = "";
            double efectivo = 0;
            double tdebito = 0;
            return ManipulaPagos(CadenaConexion, ref OpcionManipula, ref numero, ref referencia, ref concepto,
                ref detalle, ref fecha, ref fachasys, ref usuario, ref nomusu,
                ref valor, ref hora, ref convenio, ref estado, ref TablaDatos, filtro,
                ref cerrado, ref usuarioautoriza, ref efectivo, ref tdebito);
        }

        public bool CargarArchivo(string Ruta, string convenio, string concepto, OdbcConnection CadenaConexion)
        {
            string referencia, cedula, nombre, programa, periodo, valor, fechavence, codbarra;
            DateTime FechaVencimiento;

            NombreProcedimiento = "CargarArchivo";
            try
            {
                using (StreamReader reader = new StreamReader(Ruta))
                {
                    SqlDml = "update rec_parametros set estado='C' where convenio='" + convenio + "'";
                    OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                    SqlDml = "delete from rec_comceptos where convenio='" + convenio + "' and concepto='" + concepto + "' and estado='A'";
                    OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);

                    string LineaLeer;
                    while ((LineaLeer = reader.ReadLine()) != null)
                    {
                        if (LineaLeer.Trim() != "")
                        {
                            referencia = LineaLeer.Substring(5, 16).Trim();
                            cedula = "0";
                            nombre = "0";
                            programa = "0";
                            periodo = "0";
                            codbarra = referencia;
                            valor = LineaLeer.Substring(21, 12).Trim();
                            fechavence = LineaLeer.Substring(33, 8).Trim();
                            FechaVencimiento = DateTime.Parse(fechavence.Substring(0, 4) + "-" + fechavence.Substring(4, 2) + "-" + fechavence.Substring(6, 2));

                            SqlDml = "select convenio,referencia,concepto from rec_comceptos where referencia='" + referencia + "' and convenio='" + convenio + "' and concepto='" + concepto + "'";
                            if (OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento) == false)
                            {
                                SqlDml = " insert into rec_comceptos(referencia,cedula,nombre,programa,periodo,valor,fechavence,codbarra,refeelectronica,concepto,convenio,estado,fachasys) " +
                                                        " values('" + referencia + "','" + cedula + "','" + nombre + "','" + programa + "','" + periodo + "'," + Convert.ToDouble(valor) + ",'" + FechaVencimiento.ToString(varini.PstForFec) + "','" + codbarra + "','" + referencia + "','" + concepto + "','" + convenio + "','A','" + DateTime.Now.ToString(varini.pstForfecyHora) + "')";

                                OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
                            }
                        }
                        Debug.WriteLine(reader.BaseStream.Position);
                    }
                }
                SqlDml = "update rec_parametros set estado='A' where convenio='" + convenio + "'";
                OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
            }
            catch (Exception)
            {
                SqlDml = "update rec_parametros set estado='A' where convenio='" + convenio + "'";
                OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
                return false;
            }

            return true;
        }

        public bool ExportaArchivo(string Ruta, string nombre, string convenio, string concepto, OdbcConnection CadenaConexion)
        {
            DataTable TablaDatos = new DataTable();
            TextWriter Escribe = null;
            SqlDml = "update rec_parametros set estado='C' where convenio='" + convenio + "'";
            OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
            string Sql = "select referencia, valor, fecha from rec_pagocomceptos where estado='C' and convenio='" + convenio + "' and concepto='" + concepto +
                        "' and fecha between '" + DateTime.Now.Date.ToString(varini.PstForFec) + "' and '" + DateTime.Now.Date.ToString(varini.PstForFec) + "'";

            NombreProcedimiento = "ExportaArchivo";
            // if (OdbcConnect.ExecuteConsulta(Sql, CadenaConexion, NombreProcedimiento, TablaDatos) == true) // ERROR: CS1620
            {
                try
                {
                    SqlDml = "update rec_parametros set estado='A' where convenio='" + convenio + "'";
                    OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
                    Escribe = File.CreateText(Ruta + "\\" + nombre + DateTime.Now.ToString("yyyyMMddHHmm"));
                    for (int contador = 0; contador < TablaDatos.Rows.Count; contador++)
                    {
                        DataRow row = TablaDatos.Rows[contador];
                        Escribe.Write(row["referencia"].ToString() + Convert.ToInt64(row["valor"]).ToString().PadLeft(12, '0') + Convert.ToDateTime(row["fecha"]).ToString("yyyyMMdd"));
                        Escribe.WriteLine();
                    }
                    Escribe.Close();
                    Escribe.Dispose();
                }
                catch (Exception)
                {
                    SqlDml = "update rec_parametros set estado='A' where convenio='" + convenio + "'";
                    OdbcConnect.ExecuteQueryconec(SqlDml, CadenaConexion, NombreProcedimiento);
                    if (Escribe != null)
                    {
                        Escribe.Close();
                        Escribe.Dispose();
                    }
                    return false;
                }
                return true;
            }
            // else // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
            {
                return false;
            }
        }

        public void ImprimePago(int numero, string convenio, OdbcConnection myconnect)
        {
            int NumCopiasImp = 0;
            ERP.Core.Compartido.Configuracion.ParamSys MsgParSys = new ERP.Core.Compartido.Configuracion.ParamSys();
            string Direccion = " ", Nomres = " ", Telefono = " ", CompaNit = " ", ciudad = " ";
            // MsgParSys.BuscarCompania(varini.sptCodEmpr, myconnect, "", "", "", "", "", "", "", "", "", "", "", "", ref CompaNit, ref Direccion, "", "", "", "", "", "", "", "", "", ref Nomres, ref Telefono, "", "", "", "", "", "", "", ref ciudad); // ERROR: CS7036

            ERP.Core.Compartido.Reportes.reporte R = new ERP.Core.Compartido.Reportes.reporte("rec_pago");
            R.SetParameterValue("numero", numero);
            R.SetParameterValue("empresa", Nomres);
            R.SetParameterValue("nit", CompaNit);
            R.SetParameterValue("direccion", Direccion);
            R.SetParameterValue("ciudad", ciudad);
            R.SetParameterValue("telefono", Telefono);
            R.SetParameterValue("convenio", convenio);

            PrinterSettings instance = new PrinterSettings();
            string impresoraPredt = instance.PrinterName;
            R.PrintOptions.PrinterName = impresoraPredt;
            if (!int.TryParse(NumCopiasImp.ToString(), out NumCopiasImp))
            {
                NumCopiasImp = 0;
            }
            R.PrintToPrinter((NumCopiasImp + 1), false, 0, 0);
        }

        public msgrecaudos()
        {
            OdbcConnect.MyOdbcConect(varini);
        }
    }
}
