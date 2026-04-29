using System;
using System.Collections;
using System.Data;
using System.Data.Odbc;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Printing;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Services
{
    public partial class msginv
    {
        // CargarVentanaCantidadesBodega duplicado eliminado (original en Part3)


















        private object ImprimeDevolucion(int IdTipoMovto, double Secuencia, OdbcConnection Myconnect, Form myforma, string descripcion, int Cladoc)
        {
            string nit = " ";
            int Regimen = 0;
            string Direccion = " ", Telefono = " ", Nombre = " ";

            // CrystalDecisions.CrystalReports.Engine.ReportDocument factura = new CrystalDecisions.CrystalReports.Engine.ReportDocument(); // ERROR: CS0246

            string DocEmite = "";
            ERP.Core.Compartido.Forms.imprimir Impresion = new ERP.Core.Compartido.Forms.imprimir();
            switch (Cladoc)
            {
                case 9:
                    // factura = new ERP.Core.Compartido.Reportes.reporte("inv_rordencompra"); // ERROR: CS0103
                    break;
                default:
                    // factura = new ERP.Core.Compartido.Reportes.reporte("inv_rtransaccion"); // ERROR: CS0103
                    break;
            }
            ERP.Core.Compartido.Reportes.config_report ConfRep = new ERP.Core.Compartido.Reportes.config_report();

            // BuscarCompania helper: extract p15=nit, p16=Direccion, p26=Nombre, p27=Telefono
            {
                string _p1 = " ", _p2 = " ", _p3 = " ", _p4 = " ", _p5 = " ", _p6 = " ", _p7 = " ", _p8 = " ", _p9 = " ", _p10 = " ";
                string _p11 = " ", _p12 = " ", _p13 = " ", _p14 = " ", _p15 = " ", _p16 = " ", _p17 = " ", _p18 = " ", _p19 = " ", _p20 = " ";
                string _p21 = " ", _p22 = " ", _p23 = " ", _p24 = " ", _p25 = " ", _p26 = " ", _p27 = " ", _p28 = " ", _p29 = " ", _p30 = " ";
                string _p31 = " ", _p32 = " ", _p33 = " ", _p34 = " ";
                // msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect, // ERROR: CS7036
                    // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                    // ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, // ERROR: CS7036
                    // ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, ref _p29, ref _p30, // ERROR: CS7036
                    // ref _p31, ref _p32, ref _p33, ref _p34); // ERROR: CS7036
                nit = _p15;
                Direccion = _p16;
                Nombre = _p26;
                Telefono = _p27;
            }

            switch (Cladoc)
            {
                case 1: DocEmite = "DEVOLUCION"; break;
                case 4: DocEmite = "COMPRAS"; break;
                case 5: DocEmite = "ORDEN DE SALIDA"; break;
                case 6: DocEmite = "REMISION"; break;
                case 9: DocEmite = "ORDEN DE COMPRA"; break;
            }

            // factura.SetParameterValue("IdTipoMovto", IdTipoMovto); // ERROR: CS0103
            // factura.SetParameterValue("Secuencia", Secuencia); // ERROR: CS0103
            // factura.SetParameterValue("nit", nit); // ERROR: CS0103
            // factura.SetParameterValue("direccion", Direccion); // ERROR: CS0103
            // factura.SetParameterValue("telefono", Telefono); // ERROR: CS0103
            // factura.SetParameterValue("titulo", descripcion); // ERROR: CS0103
            // factura.SetParameterValue("DocEmite", DocEmite); // ERROR: CS0103

            // ConfRep.confi_reportes(myforma, factura, null, true); // ERROR: CS0103
            return null;
        }

        public bool ValidarPlanoInventarioFisico(string NombreArchivo, Form Myforma, OdbcConnection myconnect)
        {
            string Periodo = "", bodega, idProducto, existenciaProducto, costo;
            string Ubicacion;
            double contador = 0;

            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Verificando Archivo Plano", Myforma);
            string mensaje = "";
            decimal TotReg;
            string line;
            bool okk;
            ArrayList arreglo = new ArrayList();
            bool NoExisteFicha = false;
            string codigo = " ";

            string nomruta = Path.GetTempFileName();
            nomruta = Path.GetTempFileName().Replace("tmp", "txt");

            StreamWriter strStreamWriter;
            StreamReader strStreamReader;

            okk = true;
            strStreamWriter = new StreamWriter(nomruta, false);
            strStreamReader = new StreamReader(NombreArchivo);

            line = strStreamReader.ReadLine();

            try
            {
                TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (Strings.Len(line) + 2)));
                arreglo.Clear();

                BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
                BarraProgreso.Show();

                arreglo.Add("Estos son los siguientes errores presentados en el archivo");
                arreglo.Add("  ");
                arreglo.Add("  ");
                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));

                while (line.Trim().Length > 6)
                {
                    mensaje = "";
                    arreglo.Clear();
                    arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));

                    Periodo = arreglo[0].ToString();
                    Ubicacion = arreglo[1].ToString();
                    bodega = arreglo[2].ToString();
                    idProducto = arreglo[3].ToString();
                    existenciaProducto = arreglo[4].ToString();
                    costo = arreglo[5].ToString();

                    arreglo.Clear();
                    contador = contador + 1;

                    VerificarDatosInventarioFisico(contador, Periodo, Ubicacion, bodega, idProducto, existenciaProducto, costo, myconnect, ref mensaje);

                    if (mensaje.Trim() != "")
                    {
                        NoExisteFicha = true;
                        arreglo.Add(mensaje);
                        strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "         "));
                    }

                    line = strStreamReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    BarraProgreso.PerformStep();
                }

                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();
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
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
                contador = 0;
            }
            contador = 0;
            return okk;
        }

        public void VerificarDatosInventarioFisico(double linea, string Periodo, string Ubicacion, string Bodega, string idProducto, string existencia, string costo, OdbcConnection myconnect, ref string mensaje)
        {
            if (Information.IsNumeric(Periodo) == false || Periodo.Length != 6)
            {
                mensaje = "  Error en la Linea-" + linea + "--> Periodo debe ser YYYYmm.-->" + Periodo;
            }

            if (Information.IsNumeric(Ubicacion) == true)
            {
                // ok = msginvconf.BuscaUbicacion(Convert.ToInt32(Ubicacion), myconnect); // ERROR: CS7036
                switch (ok)
                {
                    case false:
                        mensaje += "   Error en la Linea-" + linea + "-->la Ubicacion No Existe-->" + Ubicacion;
                        break;
                }
            }
            else
            {
                mensaje += "   Error en la Linea-" + linea + "-->la Ubicacion  debe ser Numerico.-->" + Ubicacion;
            }

            if (Information.IsNumeric(Bodega) == true)
            {
                // ok = msginvconf.BuscaBodega(Bodega, Ubicacion, myconnect); // ERROR: CS1501
                switch (ok)
                {
                    case false:
                        mensaje += "  Error en la Linea-" + linea + "-->la Bodega  " + Bodega + " No Existe en la Ubicacion-->" + Ubicacion;
                        break;
                }
            }
            else
            {
                mensaje += "   Error en la Linea-" + linea + "-->la Bodega  debe ser Numerico.-->" + Bodega;
            }

            if (Information.IsNumeric(idProducto) == true)
            {
                // ok = this.BuscaInventario(idProducto, Periodo, Ubicacion, Bodega, myconnect); // ERROR: CS1503
                switch (ok)
                {
                    case false:
                        mensaje += "   Error en la Linea-" + linea + "-->El Producto " + idProducto + " no Existe en la  Bodega " + Bodega + "  en la Ubicacion-->" + Ubicacion + " en el Periodo   " + Periodo;
                        break;
                }
            }
            else
            {
                mensaje += "  Error en la Linea-" + linea + "-->la Codigo del Producto   debe ser Numerico.-->" + idProducto;
            }

            if (Information.IsNumeric(existencia) == false)
            {
                mensaje += "  Error en la Linea-" + linea + "--> La Existencia  Fisica debe ser Numerica ";
            }

            if (Information.IsNumeric(costo) == false)
            {
                mensaje += " Error en la Linea-" + linea + "-->  El Costo del Producto debe ser Numerica  ";
            }
        }

        public bool ActualizarInventarioFisicoPlano(string NombreArchivo, Form Myforma, OdbcConnection myconnect)
        {
            string Periodo = "", bodega, idProducto, existenciaProducto, costo;
            string Ubicacion;
            double CantInicial = 0, CantCompra = 0, CantVendida = 0;
            double Cantfinal = 0, Costoteorico = 0, UltCosto = 0, costoinicial = 0;

            ERP.Core.Compartido.Controles.Barraprogress BarraProgreso = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Inventario Fisico por Plano", Myforma);
            string mensaje = "";
            decimal TotReg;
            string line;
            bool okk;
            ArrayList arreglo = new ArrayList();
            bool NoExisteFicha = false;
            string codigo = " ";

            string nomruta = Path.GetTempFileName();
            nomruta = Path.GetTempFileName().Replace("tmp", "txt");
            string mysql;
            okk = true;

            StreamWriter strStreamWriter = new StreamWriter(nomruta, false);
            StreamReader strStreamReader = new StreamReader(NombreArchivo);
            line = strStreamReader.ReadLine();

            try
            {
                TotReg = Math.Round((decimal)(FileSystem.FileLen(NombreArchivo) / (Strings.Len(line) + 2)));
                arreglo.Clear();

                BarraProgreso.ValorMinimoMaximo(0, (int)TotReg);
                BarraProgreso.Show();

                arreglo.Add("Estos son los siguientes errores presentados en el archivo");
                arreglo.Add("  ");
                arreglo.Add("  ");
                strStreamWriter.WriteLine(Strings.Join((string[])arreglo.ToArray(typeof(string)), "    "));
                while (line.Trim().Length > 6)
                {
                    mensaje = "";
                    arreglo.Clear();
                    arreglo.AddRange(Strings.Split(line, ",", -1, CompareMethod.Text));
                    Periodo = arreglo[0].ToString();
                    Ubicacion = arreglo[1].ToString();
                    bodega = arreglo[2].ToString();
                    idProducto = arreglo[3].ToString();
                    existenciaProducto = arreglo[4].ToString();
                    costo = arreglo[5].ToString();

                    arreglo.Clear();

                    // ok = this.BuscaInventario(idProducto, Periodo, Ubicacion, bodega, myconnect, ref CantInicial, ref CantCompra, ref CantVendida, ref Cantfinal, ref Costoteorico, ref UltCosto, ref costoinicial); // ERROR: CS1503
                    switch (ok)
                    {
                        case true:
                            if (Convert.ToDouble(costo) == 0)
                            {
                                costo = Costoteorico.ToString();
                            }
                            // GrabaInvFisico(idProducto, Periodo, Ubicacion, bodega, existenciaProducto, Cantfinal, costo, myconnect); // ERROR: CS1503
                            break;
                    }

                    line = strStreamReader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    BarraProgreso.PerformStep();
                }

                strStreamWriter.Close();
                strStreamWriter.Dispose();
                strStreamReader.Close();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                strStreamWriter.Close();
                strStreamWriter.Dispose();
                BarraProgreso.Close();
                BarraProgreso.Dispose();
                okk = false;
            }
            return okk;
        }

        public bool validadLimeteVentas(OdbcConnection myconnect, int periodo, string nit, string Producto, int cantidad, int idtipoMov)
        {
            string CtrlExist = "  ";
            string stmysqlGrupos = "  ";
            string grupo = " ";
            int cantiRestingProducto = 0;
            string vereficarestringprod = " ";
            int cantiRestingGrupo = 0;
            string vereficarestringGrup = " ";
            // msginvconf.BuscaProductos(Producto, myconnect, ref grupo, ref vereficarestringprod, ref cantiRestingProducto); // ERROR: CS7036
            // msginvconf.BuscaGrupo(grupo, myconnect, ref vereficarestringGrup, ref cantiRestingGrupo); // ERROR: CS7036

            // this.msginvconf.BuscaTipomovto(idtipoMov, myconnect, ref CtrlExist); // ERROR: CS7036

            bool ok1 = false;
            string limiteGrupo1 = "   ";
            string descripgrupo1 = "   ";

            switch (CtrlExist)
            {
                case "1":
                    if ((vereficarestringGrup == "Y" && cantiRestingGrupo > 0) || (vereficarestringprod == "Y" && cantiRestingProducto > 0))
                    {
                        stmysql = "  select   invpro.idproducto,invpro.Descripcion as campo1,"
                                + "  invpro.cantRestringVentas as  campo2, gruposterc.cantRestringVentas as campo3,gruposterc.Descripcion as campo4 "
                                + "  from  inv_productos invpro "
                                + "  inner join  inv_grupos gruposterc "
                                + "  on invpro.IdGruProducto  =  gruposterc.IdGruProducto"
                                + "  where   invpro.idproducto   = " + Producto
                                + "  AND invpro.rstingLimeteVentas = 'Y' "
                                + "  and invpro.cantRestringVentas > 0   "
                                + "  and invpro.cantRestringVentas <    " + cantidad;
                        string descripcion1 = "   ", limite1 = "  ", execido1 = "  ";
                        ok1 = Exec4(stmysql, myconnect, "validadLimeteVentas", ref descripcion1, ref limite1, ref limiteGrupo1, ref descripgrupo1);
                        switch (ok1)
                        {
                            case true:
                                MessageBox.Show(" Productos  " + Producto + " - " + descripcion1 + " " + "\r\n"
                                    + " Pertence al Grupo " + descripgrupo1 + "\r\n"
                                    + " Limite Maximo por Grupo " + limiteGrupo1 + " Unidades " + "\r\n"
                                    + " Limite Maximo por Producto  " + limite1 + "\r\n"
                                    + " Disponible  por Producto  " + (Convert.ToInt32(limite1) - cantidad) + "\r\n"
                                    + " Disponible  por Grupo  " + (Convert.ToInt32(limiteGrupo1) - cantidad),
                                    "Solido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                return true;
                            case false:
                                stmysqlGrupos = " select  gruposterc.IdGruProducto,gruposterc.Descripcion as campo1, sum(invmov.Cantidad) as campo2,gruposterc.cantRestringVentas as campo3"
                                    + "  from inv_movtos invmov "
                                    + "inner join inv_docs  invdoc "
                                    + "on invmov.idtipomovto = invdoc.idtipomovto "
                                    + "and invmov.secuencia = invdoc.secuencia  "
                                    + "inner join inv_tipomovtos tipoMovto  "
                                    + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                                    + "inner join inv_productos invpro    "
                                    + " on invmov.idproducto = invpro.idproducto "
                                    + " inner join  inv_grupos gruposterc "
                                    + " on invpro.IdGruProducto  =  gruposterc.IdGruProducto "
                                    + " inner join cnt_nit tercero "
                                    + " on invdoc.IdCliente = tercero.NIT "
                                    + "  where invmov.periodo = " + periodo + "  "
                                    + " and  tercero.NIT =  '" + nit + "'  "
                                    + " and tipoMovto.CtrlExistencia = 1 "
                                    + " and gruposterc.IdGruProducto = '" + grupo + "'  AND gruposterc.rstingLimeteVentas = 'Y' "
                                    + " and gruposterc.cantRestringVentas > 0 "
                                    + " group by gruposterc.IdGruProducto,gruposterc.Descripcion,gruposterc.cantRestringVentas  "
                                    + " HAVING(sum(invmov.Cantidad) +  " + cantidad + ") > gruposterc.cantRestringVentas ";

                                string descripcion = "   ", limite = "  ", execido = "  ";
                                ok = Exec3(stmysqlGrupos, myconnect, "validadLimeteVentas", ref descripcion, ref execido, ref limite);

                                switch (ok)
                                {
                                    case true:
                                        MessageBox.Show(" ha completado el Limte de Productos que puede  llevar  " + "\r\n" + "  por este Grupo " + grupo + " - " + descripcion + " " + "\r\n"
                                            + " Y el Limite Maximo es de " + limite + " Unidades  " + "\r\n"
                                            + " Unidades Compradas por este Grupo " + execido + " Unidades" + "\r\n"
                                            + " Disponible por este Grupo " + (Convert.ToInt32(limite) - (Convert.ToInt32(execido))),
                                            "Solido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                        return true;

                                    case false:
                                        string descripcionProd = "   ", limiteProd = "  ", execidoProd = "  ";
                                        stmysql = " select  invpro.idproducto,invpro.Descripcion as campo1,sum(invmov.Cantidad) as campo2, invpro.cantRestringVentas as  campo3 "
                                            + " from inv_movtos invmov "
                                            + "inner join inv_docs  invdoc "
                                            + "on invmov.idtipomovto = invdoc.idtipomovto "
                                            + "and invmov.secuencia = invdoc.secuencia  "
                                            + "inner join inv_tipomovtos tipoMovto  "
                                            + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                                            + "inner join inv_productos invpro    "
                                            + " on invmov.idproducto = invpro.idproducto "
                                            + " inner join cnt_nit tercero "
                                            + " on invdoc.IdCliente = tercero.NIT "
                                            + " where invmov.periodo = " + periodo + "  "
                                            + " and  tercero.NIT =  '" + nit + "'  "
                                            + " and  tipoMovto.CtrlExistencia = 1 "
                                            + " and invpro.idproducto  = " + Producto + "  and  invpro.rstingLimeteVentas = 'Y' "
                                            + " and invpro.cantRestringVentas > 0 "
                                            + "group by invpro.idproducto,invpro.Descripcion ,invpro.cantRestringVentas "
                                            + "HAVING(sum(invmov.Cantidad) +  " + cantidad + ") > invpro.cantRestringVentas ";

                                        ok = Exec3(stmysql, myconnect, "validadLimeteVentas", ref descripcionProd, ref execidoProd, ref limiteProd);
                                        switch (ok)
                                        {
                                            case true:
                                                stmysqlGrupos = " select  gruposterc.IdGruProducto,gruposterc.Descripcion as campo1, sum(invmov.Cantidad) as campo2,gruposterc.cantRestringVentas as campo3"
                                                    + "  from inv_movtos invmov "
                                                    + "inner join inv_docs  invdoc "
                                                    + "on invmov.idtipomovto = invdoc.idtipomovto "
                                                    + "and invmov.secuencia = invdoc.secuencia  "
                                                    + "inner join inv_tipomovtos tipoMovto  "
                                                    + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                                                    + "inner join inv_productos invpro    "
                                                    + " on invmov.idproducto = invpro.idproducto "
                                                    + " inner join  inv_grupos gruposterc "
                                                    + " on invpro.IdGruProducto  =  gruposterc.IdGruProducto "
                                                    + " inner join cnt_nit tercero "
                                                    + " on invdoc.IdCliente = tercero.NIT "
                                                    + "  where invmov.periodo = " + periodo + "  "
                                                    + " and  tercero.NIT =  '" + nit + "'  "
                                                    + " and tipoMovto.CtrlExistencia = 1 "
                                                    + " and gruposterc.IdGruProducto = '" + grupo + "'  AND gruposterc.rstingLimeteVentas = 'Y' "
                                                    + " and gruposterc.cantRestringVentas > 0 "
                                                    + " group by gruposterc.IdGruProducto,gruposterc.Descripcion,gruposterc.cantRestringVentas  ";

                                                string descripciongrupo = "  ", execidogrupo = "  ", limiteGrupo = "   ";
                                                ok = Exec3(stmysqlGrupos, myconnect, "validadLimeteVentas", ref descripciongrupo, ref execidogrupo, ref limiteGrupo);
                                                if (ok == true)
                                                {
                                                    MessageBox.Show(" Productos  " + Producto + " - " + descripcionProd + " " + "\r\n"
                                                        + " Limete Maximo por este Producto " + limiteProd + " Unidades  " + "\r\n"
                                                        + " Unidades compradas " + execidoProd + " Unidades" + "\r\n"
                                                        + " Pertenece al Grupo  " + grupo + " - " + descripciongrupo + "\r\n"
                                                        + " Limete Maximo por este Grupo  es " + limiteGrupo + " Unidades " + "\r\n"
                                                        + " Unidades Compradas por este Grupo es " + execidogrupo + " Unidades " + "\r\n"
                                                        + " Disponible por Producto  " + (Convert.ToInt32(limiteProd) - (Convert.ToInt32(execidoProd))) + " Unidades " + "\r\n"
                                                        + " Disponible por Grupo " + (Convert.ToInt32(limiteGrupo) - (Convert.ToInt32(execidogrupo))) + " Unidades ",
                                                        "Solido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                                }
                                                else
                                                {
                                                    string descripcionProd1 = "   ", limiteProd1 = "  ", execidoProd1 = "  ";
                                                    stmysql = " select  invpro.idproducto,invpro.Descripcion as campo1,sum(invmov.Cantidad) as campo2, invpro.cantRestringVentas as  campo3 "
                                                        + " from inv_movtos invmov "
                                                        + "inner join inv_docs  invdoc "
                                                        + "on invmov.idtipomovto = invdoc.idtipomovto "
                                                        + "and invmov.secuencia = invdoc.secuencia  "
                                                        + "inner join inv_tipomovtos tipoMovto  "
                                                        + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                                                        + "inner join inv_productos invpro    "
                                                        + " on invmov.idproducto = invpro.idproducto "
                                                        + " inner join cnt_nit tercero "
                                                        + " on invdoc.IdCliente = tercero.NIT "
                                                        + " where invmov.periodo = " + periodo + "  "
                                                        + " and  tercero.NIT =  '" + nit + "'  "
                                                        + " and  tipoMovto.CtrlExistencia = 1 "
                                                        + " and invpro.idproducto  = " + Producto + "  and  invpro.rstingLimeteVentas = 'Y' "
                                                        + " and invpro.cantRestringVentas > 0 "
                                                        + "group by invpro.idproducto,invpro.Descripcion ,invpro.cantRestringVentas "
                                                        + "HAVING(sum(invmov.Cantidad) +  " + cantidad + ") > invpro.cantRestringVentas ";

                                                    ok = Exec3(stmysql, myconnect, "validadLimeteVentas", ref descripcionProd1, ref execidoProd1, ref limiteProd1);

                                                    MessageBox.Show("ha completado el Limte de Productos  " + Producto + " - " + descripcionProd + " " + "\r\n"
                                                        + " Limete Maximo por este Producto " + limiteProd + " Unidades  " + "\r\n"
                                                        + " Unidades compradas " + execidoProd + " Unidades",
                                                        "Solido", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                                                }
                                                return true;
                                            case false:
                                                return false;
                                        }
                                        break;
                                }
                                break;
                        }
                    }
                    return false;

                default:
                    return false;
            }
        }

        public void CargarVentanaLimiteVentas(string codigoter, string Nombre, string periodo, Form myForma, OdbcConnection myconnect)
        {
            // frmLimiteVentas frmLimiteVentas = new frmLimiteVentas(myconnect); // ERROR: CS0118
            // frmLimiteVentas.lblInfoCodigoter.Text = codigoter; // ERROR: CS1061
            // frmLimiteVentas.lblInfoNombre.Text = Nombre; // ERROR: CS1061
            // frmLimiteVentas.Lblperiodo.Text = periodo; // ERROR: CS1061
            // frmLimiteVentas.chkAsociado.Checked = true; // ERROR: CS1061
            // frmLimiteVentas.rdbGrupos.Checked = true; // ERROR: CS1061
            // frmLimiteVentas.cargaDatosInciales(); // ERROR: CS1061
            // frmLimiteVentas.Show(myForma); // ERROR: CS1061
        }

        public DataSet buscaLimiteVentas(OdbcConnection myconnect, int periodo, string formaBuscar, string codigoter)
        {
            switch (formaBuscar)
            {
                case "GruposAsociado":
                    stmysql = " select  gruposterc.IdGruProducto as  codigo_Grupo,gruposterc.Descripcion,gruposterc.cantRestringVentas as Cantidad_Restringida,sum(invmov.Cantidad) as Cantidad_Comprada,( gruposterc.cantRestringVentas -sum(invmov.Cantidad)) as Disponible"
                        + " from inv_movtos invmov "
                        + "inner join inv_docs  invdoc "
                        + "on invmov.idtipomovto = invdoc.idtipomovto "
                        + "and invmov.secuencia = invdoc.secuencia  "
                        + "inner join inv_tipomovtos tipoMovto  "
                        + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                        + "inner join inv_productos invpro    "
                        + " on invmov.idproducto = invpro.idproducto "
                        + " inner join  inv_grupos gruposterc "
                        + "on invpro.IdGruProducto  =  gruposterc.IdGruProducto "
                        + " inner join cnt_nit tercero "
                        + " on invdoc.IdCliente = tercero.NIT "
                        + "  where invmov.periodo = " + periodo + "  "
                        + " and  tercero.NIT =  '" + codigoter + "'  "
                        + " and  tipoMovto.CtrlExistencia = 1 "
                        + " AND gruposterc.rstingLimeteVentas = 'Y'  and gruposterc.cantRestringVentas > 0 "
                        + " group by gruposterc.IdGruProducto,gruposterc.Descripcion,gruposterc.cantRestringVentas  ";
                    break;
                case "Grupos":
                    stmysql = " select gruposterc.IdGruProducto codigo_Grupo ,gruposterc.Descripcion, "
                        + " gruposterc.cantRestringVentas as Cantidad_Restringida "
                        + "from inv_grupos gruposterc "
                        + " where gruposterc.rstingLimeteVentas = 'Y' "
                        + "  and gruposterc.cantRestringVentas > 0 ";
                    break;
                case "ProductosAsociado":
                    stmysql = " select  invpro.idproducto codigo_Producto, invpro.Descripcion ,invpro.cantRestringVentas as Cantidad_Restringida,sum(invmov.Cantidad) as  cantidad_Comprada,(invpro.cantRestringVentas - sum(invmov.Cantidad) ) as disponible   "
                        + "from inv_movtos invmov "
                        + "inner join inv_docs  invdoc "
                        + "on invmov.idtipomovto = invdoc.idtipomovto "
                        + "and invmov.secuencia = invdoc.secuencia  "
                        + "inner join inv_tipomovtos tipoMovto  "
                        + "on invmov.idtipomovto = tipoMovto.IdTipoMovto  "
                        + "inner join inv_productos invpro    "
                        + " on invmov.idproducto = invpro.idproducto "
                        + " inner join cnt_nit tercero "
                        + " on invdoc.IdCliente = tercero.NIT "
                        + "  where invmov.periodo = " + periodo + "  "
                        + " and  tercero.NIT =  '" + codigoter + "'  "
                        + " and  tipoMovto.CtrlExistencia = 1 "
                        + " and  invpro.rstingLimeteVentas = 'Y'   and invpro.cantRestringVentas > 0 "
                        + " group by invpro.idproducto,invpro.Descripcion,invpro.cantRestringVentas ";
                    break;
                case "Productos":
                    stmysql = " select invpro.idproducto codigo_Producto ,invpro.Descripcion, "
                        + " invpro.cantRestringVentas as Cantidad_Restringida,invpro.IdGruProducto as grupo "
                        + "  from inv_productos invpro "
                        + "  where invpro.rstingLimeteVentas = 'Y' "
                        + "  and invpro.cantRestringVentas > 0 ";
                    break;
            }
            DataSet Dsdataset = new DataSet();
            ok = this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "BuscaInventarioConsolidado", ref Dsdataset, "tblimite");
            return Dsdataset;
        }

        public bool BuscarSaldoCredito(OdbcConnection myconnect, string codigoter, string periodo, string idMovimiento, ref string saldocredito)
        {
            bool ValidarOk = false;
            int CtrlExist = 0;
            string ctrlFactura = " ";
            int lincredsocio = 0, lincredemp = 0, lincreTercero = 0, increTerceroPrecAsociado = 0, lincreClientesPatronales = 0;
            string inLIncred = "";
            codigoter = Strings.Right("00000000000000" + codigoter, 14);
            // this.msginvconf.BuscaTipomovto(idMovimiento, myconnect, ref CtrlExist, ref ctrlFactura); // ERROR: CS7036
            switch (CtrlExist)
            {
                case 1:
                    // ok = msginvconf.BuscaDatosFacturacion(ctrlFactura, myconnect, ref lincredsocio, ref lincredemp, ref lincreClientesPatronales, ref increTerceroPrecAsociado); // ERROR: CS7036
                    switch (ok)
                    {
                        case true:
                            inLIncred = lincredsocio + "," + lincredemp + "," + lincreClientesPatronales + "," + increTerceroPrecAsociado;
                            stmysql = "select c.CODIGOTER, sum(a.saldo) as campo1 "
                                + " from cop_salmaecar a "
                                + " inner join cop_maecar c "
                                + " on a.CODIGOTER = c.CODIGOTER "
                                + " and a.LINCRED = c.LINCRED "
                                + " and a.NUMERO = c.NUMERO "
                                + " inner join cop_concar12 b on a.lincred=b.lincred "
                                + " where  a.codigoter='" + codigoter + "'  and "
                                + " a.periodo='" + periodo + "' and a.LINCRED in(" + inLIncred + ")"
                                + " and a.lincred<>9999  and a.saldo > 0 "
                                + " group by c.codigoter  ";
                            ValidarOk = Exec1(stmysql, myconnect, "BuscarSaldoCredito", ref saldocredito);
                            break;
                    }
                    break;
            }
            return ValidarOk;
        }

        public bool CargarCantidadCostoProd(string IdProducto, string Ubicacion, string Bodega, int Periodo, OdbcConnection myconnect, ref double CantidadDisponible, ref double CostoProducot)
        {
            string StUbicacion = "", StBodega = "";
            double CantDisp = 0, costo = 0;

            switch (Ubicacion.Trim())
            {
                case "":
                    MessageBox.Show("Debe seleccionar una ubicacion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            switch (Bodega.Trim())
            {
                case "":
                    MessageBox.Show("Debe seleccionar una bodega", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            StUbicacion = Ubicacion.Trim();
            StUbicacion = Strings.Mid(StUbicacion, 1, StUbicacion.IndexOf(" - "));

            StBodega = Bodega.Trim();
            StBodega = Strings.Mid(StBodega, 1, StBodega.IndexOf(" - "));

            // ok = this.BuscaInventario(IdProducto, Periodo, StUbicacion, StBodega, myconnect, ref CantDisp, ref costo); // ERROR: CS1501

            switch (ok)
            {
                case true:
                    CantidadDisponible = CantDisp;
                    CostoProducot = costo;
                    break;
            }

            return ok;
        }

        public bool ActualizaContabilidadTrasladoBod(int IdTipomovto, double Secuencia, DateTime fecha, string Usuario, OdbcConnection myconect, Form Myforma,
            bool Anulacion)
        {
            ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Actualizando Contabilidad", Myforma);
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            DataSet dsdatosDocs = new DataSet();
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";
            string CpteTran = "9999", CpteCost = "9999";
            string ConseCpte;
            int canreg = 0, fila = 0;
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999", CuentaRetFte = "999999999999", CtaIca = "999999999999";
            int CtrlExistencia = 0;
            string Cptecartera = "9999", CuentaCreditos = "99999999999999", cencos = "99999999";

            stmysql = "select IdGruProducto,invdoc.factura,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invpro.ClaIva,invmovto.clamovto,sum(invmovto.cantidad) as cantidad, "
                + "sum(invmovto.vlrUnidad) as VlrUnidad,sum(invmovto.subtotal) as  Subtotal,sum(invmovto.neto) as Neto,invmovto.IdPunto,invmovto.IdTurno "
                + "from inv_movtos invmovto inner join inv_docs invdoc on invmovto.idtipomovto = invdoc.idtipomovto "
                + "and invmovto.secuencia = invdoc.secuencia inner join inv_productos invpro on invmovto.idproducto = invpro.idproducto "
                + "where invmovto.idtipoMovto = " + IdTipomovto + " And invmovto.secuencia = " + Secuencia
                + " group by IdGruProducto,invdoc.factura,invdoc.idcliente,invdoc.detalle,invmovto.idubicacion,invmovto.idbodega,invpro.ClaIva,invmovto.clamovto,invmovto.IdPunto,invmovto.IdTurno";

            msgbarra.DefineMaximo(stmysql, myconect);
            msgbarra.Show();

            Application.DoEvents();

            DataSet myRead = new DataSet();
            this.MyOdbcConet.ExecuteQueryDataset(stmysql, myconect, "ActualizaContabilidad", ref myRead, "TblActContab");
            canreg = myRead.Tables["TblActContab"].Rows.Count;

            ConseCpte = "0";
            while (fila < canreg)
            {
                DataRow row = myRead.Tables["TblActContab"].Rows[fila];
                Application.DoEvents();

                // msginvconfig.BuscaGrupo(row["IdGruProducto"].ToString(), myconect, ref cencos); // ERROR: CS7036
                // msginvconfig.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], IdTipomovto, myconect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto, ref CuentaRetFte, ref CtaIca); // ERROR: CS7036
                // msginvconfig.BuscaTipomovto(IdTipomovto, myconect, ref CpteTran, ref CtrlExistencia); // ERROR: CS7036
                // msgcnt.BuscaComprobante(CpteTran, 0, false, myconect); // ERROR: CS1620

                if (CpteTran == "9999")
                {
                    MessageBox.Show("Comprobante de la linea " + row["IdTipomovto"] + " no esta parametrizado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                switch (Anulacion)
                {
                    case true:
                        ConseCpte = row["IdPunto"].ToString() + row["IdTurno"].ToString() + IdTipomovto + Secuencia;
                        break;
                    case false:
                        if (Information.IsNumeric(row["factura"]))
                        {
                            if (Convert.ToDouble(row["factura"]) != 0)
                            {
                                ConseCpte = row["factura"].ToString();
                            }
                            else
                            {
                                if (ConseCpte == "0")
                                {
                                    ConseCpte = row["IdPunto"].ToString() + row["IdTurno"].ToString() + IdTipomovto + Secuencia;
                                }
                            }
                        }
                        else
                        {
                            if (ConseCpte == "0")
                            {
                                ConseCpte = row["IdPunto"].ToString() + row["IdTurno"].ToString() + IdTipomovto + Secuencia;
                            }
                        }
                        break;
                }

                row["detalle"] = Strings.Mid(row["detalle"].ToString(), 1, 80);

                switch (CtrlExistencia)
                {
                    case 3:
                        if (Convert.ToDouble(row["neto"]) > 0)
                        {
                            switch (row["clamovto"].ToString())
                            {
                                case "C":
                                    if (Convert.ToDouble(row["ClaIva"]) > 0)
                                    {
                                        msgcnt.GrabaMovimiento(CpteTran, Convert.ToDouble(ConseCpte), InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"].ToString(), fecha, row["detalle"].ToString(), " ", Convert.ToDouble(row["Subtotal"]), 0, Convert.ToDouble(row["Subtotal"]), Usuario, myconect, 0, " ", "99999999999999", cencos, " ", 0, "POST");
                                    }
                                    else
                                    {
                                        msgcnt.GrabaMovimiento(CpteTran, Convert.ToDouble(ConseCpte), InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"].ToString(), fecha, row["detalle"].ToString(), " ", Convert.ToDouble(row["Subtotal"]), 0, Convert.ToDouble(row["Subtotal"]), Usuario, myconect, 0, " ", "99999999999999", cencos, " ", 0, "POST");
                                    }
                                    break;
                                case "V":
                                    if (Convert.ToDouble(row["ClaIva"]) > 0)
                                    {
                                        msgcnt.GrabaMovimiento(CpteTran, Convert.ToDouble(ConseCpte), InvGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"].ToString(), fecha, row["detalle"].ToString(), " ", 0, Convert.ToDouble(row["Subtotal"]), Convert.ToDouble(row["Subtotal"]), Usuario, myconect, 0, " ", "99999999999999", cencos, " ", 0, "POST");
                                    }
                                    else
                                    {
                                        msgcnt.GrabaMovimiento(CpteTran, Convert.ToDouble(ConseCpte), InvNoGravado, "9999", Strings.Format(fecha, "yyyyMM"), row["idcliente"].ToString(), fecha, row["detalle"].ToString(), " ", 0, Convert.ToDouble(row["Subtotal"]), Convert.ToDouble(row["Subtotal"]), Usuario, myconect, 0, " ", "99999999999999", cencos, " ", 0, "POST");
                                    }
                                    break;
                            }
                        }
                        break;
                }

                fila += 1;
                msgbarra.PerformStep();
            }

            // ok = msgcnt.CierreDocumento(CpteTran, ConseCpte, myconect); // ERROR: CS1503
            msgbarra.Close();
            msgbarra.Dispose();
            myRead.Dispose();

            return ok;
        }

        private object ImprimeTraslado(int IdTipoMovto, double Secuencia, OdbcConnection Myconnect, Form myforma, string descripcion, int Cladoc)
        {
            string nit = " ", Direccion = " ", Telefono = " ", Nombre = " ";
            int Regimen = 0;
            string DocEmite = "";
            ERP.Core.Compartido.Forms.imprimir Impresion = new ERP.Core.Compartido.Forms.imprimir();
            ERP.Core.Compartido.Reportes.reporte factura = new ERP.Core.Compartido.Reportes.reporte("inv_rtraslado");
            ERP.Core.Compartido.Reportes.config_report ConfRep = new ERP.Core.Compartido.Reportes.config_report();

            // BuscarCompania helper
            {
                string _p1 = " ", _p2 = " ", _p3 = " ", _p4 = " ", _p5 = " ", _p6 = " ", _p7 = " ", _p8 = " ", _p9 = " ", _p10 = " ";
                string _p11 = " ", _p12 = " ", _p13 = " ", _p14 = " ", _p15 = " ", _p16 = " ", _p17 = " ", _p18 = " ", _p19 = " ", _p20 = " ";
                string _p21 = " ", _p22 = " ", _p23 = " ", _p24 = " ", _p25 = " ", _p26 = " ", _p27 = " ", _p28 = " ", _p29 = " ", _p30 = " ";
                string _p31 = " ", _p32 = " ", _p33 = " ", _p34 = " ";
                // msgsys.BuscarCompania(varini.sptCodEmpr, Myconnect, // ERROR: CS7036
                    // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, ref _p9, ref _p10, // ERROR: CS7036
                    // ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, // ERROR: CS7036
                    // ref _p21, ref _p22, ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, ref _p29, ref _p30, // ERROR: CS7036
                    // ref _p31, ref _p32, ref _p33, ref _p34); // ERROR: CS7036
                nit = _p15;
                Direccion = _p16;
                Nombre = _p26;
                Telefono = _p27;
            }

            DocEmite = "TRASLADO BODEGAS";

            factura.SetParameterValue("IdTipoMovto", IdTipoMovto);
            factura.SetParameterValue("Secuencia", Secuencia);
            factura.SetParameterValue("nit", nit);
            factura.SetParameterValue("direccion", Direccion);
            factura.SetParameterValue("telefono", Telefono);
            factura.SetParameterValue("titulo", descripcion);
            factura.SetParameterValue("DocEmite", DocEmite);

            // ConfRep.confi_reportes(myforma, factura, null, true); // ERROR: CS1503
            return null;
        }

        public object GrabaMovimientoDevolucion(double IdTipoMovto, double secuencia, double IdProducto, string TipoVenta, string Cencosto, double IdBodega, OdbcConnection myconect, DateTime FecMovto, double Cantidad, double VlrUnidad, string NumFactura,
            decimal TasaIva, decimal TasaDscto, string IdCliente, double VlrIva,
            double VlrDsto, double VlrTotal, double Subtotal, string Idusuario, int Idpunto, int IdTurno, double VlrArqueo, string detalle, DateTime FecVence, decimal TasaRetFte, decimal VlrRetFte,
            decimal TasaIca, decimal VlrIca, string MovtoPos, bool DesdeConv, int idvendedor, double CostoDevolucion, double IdTipoMovDevolver, double SecuenciaDevolver)
        {
            string estado = "C";
            MsgBoxResult MSGOK;
            int CtrlExistencia = 0;
            string ClaMovto = "N";
            string OtrosImp = "N";
            DataTable DsDatosImp = new DataTable();
            double VlrCosto = 0;
            double Valadm = 0, IvaAdm = 0, IvaTiq = 0, OtroImp = 0, AeroPort = 0, ImpComb = 0;

            if (IdCliente == "0" || IdCliente == null)
            {
                IdCliente = "99999999999999";
            }

            // this.msginvconf.buscaPeriodo("post", myconect, ref FecMovto, ref estado, Strings.Format(FecMovto, "yyyy")); // ERROR: CS7036
            switch (estado)
            {
                case "P":
                    MSGOK = (MsgBoxResult)MessageBox.Show("Periodo de trabajo en modo de prevencion, Desea Continuar ?", "SOLIDO", MessageBoxButtons.YesNo);
                    switch (MSGOK)
                    {
                        case MsgBoxResult.No:
                            return false;
                    }
                    break;
                case "C":
                    MSGOK = (MsgBoxResult)MessageBox.Show("Periodo de trabajo esta cerrado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
            }

            // msginvconf.BuscaProductos(IdProducto, myconect, ref OtrosImp); // ERROR: CS7036

            switch (OtrosImp)
            {
                case "Y":
                    DsDatosImp = CargaOtrosImpuestos();
                    if (DsDatosImp.Rows.Count > 0)
                    {
                        DataRow impRow = DsDatosImp.Rows[0];
                        Valadm = Convert.ToDouble(impRow["Valadm"]);
                        IvaAdm = Convert.ToDouble(impRow["IvaAdm"]);
                        IvaTiq = Convert.ToDouble(impRow["IvaTiq"]);
                        OtroImp = Convert.ToDouble(impRow["OtroImp"]);
                        AeroPort = Convert.ToDouble(impRow["AeroPort"]);
                        ImpComb = Convert.ToDouble(impRow["ImpComb"]);
                    }
                    break;
            }

            ClaMovto = GrabaExistenciaDevolucion(IdProducto, Cencosto, IdBodega, ref Cantidad, VlrUnidad, IdTipoMovto, TipoVenta, FecMovto, myconect, ref VlrCosto, "", ref Subtotal, CostoDevolucion);

            GrabaTransaccion(IdTipoMovto, secuencia, myconect, IdCliente, FecMovto, VlrTotal, Subtotal, VlrDsto, VlrIva, "", Idusuario, 0, 0, 0, 0, 0, Idpunto, IdTurno, VlrArqueo, detalle, FecVence, VlrRetFte, VlrIca, true, NumFactura, idvendedor, (int)IdTipoMovDevolver, SecuenciaDevolver);
            GrabaMovtoTransaccion(IdTipoMovto, secuencia, Cencosto, IdBodega, IdProducto, TipoVenta, ClaMovto, myconect, FecMovto, Cantidad, NumFactura, TasaIva, TasaDscto, IdCliente, VlrIva, Subtotal, VlrDsto, VlrTotal, VlrUnidad, Idusuario, Idpunto, IdTurno,
                Valadm, IvaAdm, IvaTiq, OtroImp, AeroPort, ImpComb, CostoDevolucion, TasaRetFte, VlrRetFte, TasaIca, VlrIca, MovtoPos);

            return null;
        }

        public string GrabaExistenciaDevolucion(double IdProducto, string CenCosto, double IdBodega, ref double Cantidad, double ValorUnidad, double IdTipoMovto, string TipoVenta, DateTime Fecmovto, OdbcConnection myconnect,
            ref double VlrCosto, string TipoTran, ref double VlrSubtotal, double CostoDevolucion)
        {
            int CtrlExistencia = 0;
            double NewCosto = 0, CostoAnterior = 0;
            double CantInicial = 0, CantCompra = 0, cantvendida = 0, Cantfinal = 0, costo = 0, UltCosto = 0;
            double NumCant = 0;
            string ClaseTran = " ";
            double CostoInicial = 0;
            string Costea = "N";

            // this.msginvconf.BuscaTipomovto(IdTipoMovto, myconnect, ref CtrlExistencia, ref Costea); // ERROR: CS7036
            // this.msginvconf.BuscaProductos(IdProducto, myconnect, ref NumCant); // ERROR: CS7036

            switch (TipoVenta)
            {
                case "P":
                    ValorUnidad = (ValorUnidad * Cantidad) / (Cantidad * NumCant);
                    Cantidad *= NumCant;
                    break;
            }

            this.BuscaInventario(IdProducto, Strings.Format(Fecmovto, "yyyyMM"), CenCosto, IdBodega, myconnect, ref CantInicial, ref CantCompra, ref cantvendida, ref Cantfinal, ref costo, ref UltCosto, ref CostoInicial);

            if (CostoDevolucion == 0)
            {
                if (Cantidad != 0)
                {
                    CostoDevolucion = VlrSubtotal / Cantidad;
                }
            }

            switch (CtrlExistencia)
            {
                case 0:
                    switch (Costea)
                    {
                        case "Y":
                            NewCosto = CalculaCosotoProducto(IdProducto, Cantidad, CostoDevolucion, myconnect, costo, Cantfinal);
                            break;
                        case "N":
                            NewCosto = costo;
                            costo = UltCosto;
                            break;
                    }
                    this.GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, Cantidad, 0, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "C";
                    costo = NewCosto;
                    break;
                case 1:
                    NewCosto = CalculaCosotoProducto(IdProducto, Cantidad * -1, CostoDevolucion, myconnect, costo, Cantfinal);
                    GrabaInventario(IdProducto, CenCosto, IdBodega, CantInicial, 0, Cantidad, CostoInicial, NewCosto, costo, Strings.Format(Fecmovto, "yyyyMM"), myconnect);
                    ClaseTran = "V";
                    costo = NewCosto;
                    break;
            }
            VlrCosto = costo;
            return ClaseTran;
        }

        public double BuscarCantidadDevuelta(int idtipomovto, double Secuencia, double Idproducto, double IdUbicacion, double IdBodega, OdbcConnection myconnect)
        {
            StringBuilder stbuilder = new StringBuilder();
            double Idprod = 0, Cantidad = 0;

            stbuilder.Append("select movto.idproducto as campo1,sum(movto.cantidad) as campo2 ");
            stbuilder.Append("from inv_movtos movto ");
            stbuilder.Append("inner join inv_docs docs on movto.idtipomovto=docs.idtipomovto and movto.secuencia=docs.secuencia ");
            stbuilder.Append("where docs.idtipomovdev='" + idtipomovto + "' and docs.secuenciadev='" + Secuencia + "' and movto.Idproducto='" + Idproducto + "' ");
            stbuilder.Append("and movto.idubicacion='" + IdUbicacion + "' and movto.idbodega='" + IdBodega + "' ");
            stbuilder.Append("group by movto.idproducto");

            string sIdprod = Idprod.ToString(), sCantidad = Cantidad.ToString();
            ok = this.MyOdbcConet.ExecuteQueryconec(stbuilder.ToString(), myconnect, "BuscarCantidadDevuelta", ref sIdprod, ref sCantidad);
            if (ok) double.TryParse(sCantidad, out Cantidad);
            return Cantidad;
        }

        public void CargarVentanaPrecioProductos(string codigo, string Periodo, Form myForma, OdbcConnection myconnect)
        {
            // inv_frmPreProducto inv_frmPreProducto = new inv_frmPreProducto(myconnect); // ERROR: CS0118

            // inv_frmPreProducto.txtIdproducto.Text = codigo; // ERROR: CS1061
            // inv_frmPreProducto.Lblperiodo.Text = Periodo; // ERROR: CS1061
            // inv_frmPreProducto.txtIdproducto_LostFocus(null, null); // ERROR: CS1061
            // inv_frmPreProducto.Show(myForma); // ERROR: CS1061
        }

        public void GeneraInformeCuadreInv(double Ubicacion, double IdBodega, int IdPeriodo, Form MyForma, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdata = new DataSet(), DsdataCta = new DataSet(), DsImpresion = new DataSet();
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando informe cuadre Inventario - Contabilidad", MyForma);

            double fila = 0;
            string StUbicacion = "", StBodega = "", StCuenta = "";
            int Sw1 = 0;
            double fila2 = 0;
            double SaldoCuenta = 0;
            string cencos = "99999999", StManCencosto = "N";
            bool Inserta = false;
            string Cuenta = "";
            string StNomCuenta = "", StNomGrupo = "";

            DsImpresion.Tables.Add("tblimpresion");

            DsImpresion.Tables["tblimpresion"].Columns.Add("idubicacion", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("idbodega", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("idgrupo", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("idproducto", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("cuenta", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("cantidad", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("costo", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("Costototal", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("SaldoCuenta", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("Nomcuenta", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("NomGrupo", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("NomProducto", StBodega.GetType());

            if (Ubicacion != 0)
            {
                StUbicacion = " and a.idubicacion = " + Ubicacion;
            }

            if (IdBodega != 0)
            {
                StBodega = " and a.idbodega = " + IdBodega;
            }

            StBuilder.Append("select a.Idproducto,b.IdGruProducto,a.CantFinal,a.costo,(a.CantFinal*a.costo) as Costototal, ");
            StBuilder.Append("a.idubicacion,a.idbodega,b.TasaIva,b.descripcion ");
            StBuilder.Append("from inv_ctrlinv a ");
            StBuilder.Append("inner join inv_productos b on a.Idproducto=b.IdProducto ");
            StBuilder.Append("where a.Idperiodo=" + IdPeriodo + " and a.CantFinal<>0 and a.costo<>0 " + StUbicacion + StBodega);
            StBuilder.Append(" order by a.idproducto");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeCuadreInv", ref Dsdata, "tblcostos", true);

            switch (ok)
            {
                case true:
                    msgbarra.ValorMinimoMaximo(0, Dsdata.Tables["tblcostos"].Rows.Count);
                    msgbarra.Show();

                    for (fila = 0; fila <= Dsdata.Tables["tblcostos"].Rows.Count - 1; fila++)
                    {
                        DataRow row = Dsdata.Tables["tblcostos"].Rows[(int)fila];
                        StBuilder.Replace(StBuilder.ToString(), "");
                        Sw1 = 0; StNomCuenta = " "; StNomGrupo = " ";

                        if (Convert.ToDouble(row["TasaIva"]) > 0)
                        {
                            StCuenta = "a.invgravado";
                        }
                        else
                        {
                            StCuenta = "a.invnogravado";
                        }

                        StBuilder.Append("select  distinct(" + StCuenta + ") as cuenta ");
                        StBuilder.Append("from inv_cuentas a ");
                        StBuilder.Append("inner join inv_tipomovtos b on a.IdTipoMovto=b.IdTipoMovto ");
                        StBuilder.Append("where a.idubicacion = " + row["idubicacion"] + " and a.idbodega =" + row["idbodega"] + " ");
                        StBuilder.Append("and a.IdGruProducto='" + row["IdGruProducto"] + "'  and a.IdTipoMovto not in (select TipMovAjuInv from inv_facturas) ");
                        StBuilder.Append(" order by " + StCuenta);

                        // this.msginvconf.BuscaGrupo(row["IdGruProducto"].ToString(), myconnect, ref StNomGrupo, ref cencos); // ERROR: CS7036

                        ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeCuadreInv", ref DsdataCta, "tblcuentas", true);
                        switch (ok)
                        {
                            case false:
                                Cuenta = "999999999999";
                                // SaldoCuenta = msgcnt.BuscarSaldoCuenta(Cuenta, IdPeriodo, "9999", "99999999", myconnect); // ERROR: CS1503

                                DsImpresion.Tables["tblimpresion"].Rows.Add(row["idubicacion"], row["idbodega"], row["IdGruProducto"], row["IdProducto"], Cuenta, row["cantfinal"], row["costo"], row["costototal"], SaldoCuenta, StNomCuenta, StNomGrupo, row["descripcion"]);
                                break;
                            case true:
                                for (fila2 = 0; fila2 <= DsdataCta.Tables["tblcuentas"].Rows.Count - 1; fila2++)
                                {
                                    SaldoCuenta = 0; cencos = "99999999"; Inserta = false;
                                    StNomCuenta = " ";

                                    Cuenta = DsdataCta.Tables["tblcuentas"].Rows[(int)fila2]["cuenta"].ToString();

                                    // msgcnt.BuscarCuenta(Cuenta, myconnect, ref StManCencosto, ref StNomCuenta); // ERROR: CS7036
                                    switch (StManCencosto)
                                    {
                                        case "N":
                                            cencos = "99999999";
                                            break;
                                    }

                                    if (Cuenta != "999999999999")
                                    {
                                        // SaldoCuenta = msgcnt.BuscarSaldoCuenta(Cuenta, IdPeriodo, "9999", cencos, myconnect); // ERROR: CS1503
                                        Sw1 = 1;
                                        Inserta = true;
                                    }
                                    else
                                    {
                                        if (Sw1 == 0)
                                        {
                                            // SaldoCuenta = msgcnt.BuscarSaldoCuenta(Cuenta, IdPeriodo, "9999", cencos, myconnect); // ERROR: CS1503
                                            Inserta = true;
                                        }
                                    }

                                    if (Inserta)
                                    {
                                        DsImpresion.Tables["tblimpresion"].Rows.Add(row["idubicacion"], row["idbodega"], row["IdGruProducto"], row["IdProducto"], Cuenta, row["cantfinal"], row["costo"], row["costototal"], SaldoCuenta, StNomCuenta, StNomGrupo, row["descripcion"]);
                                    }
                                }
                                DsdataCta.Tables.Clear();
                                break;
                        }
                        msgbarra.PerformStep();
                    }

                    msgbarra.Close();
                    msgbarra.Dispose();

                    if (DsImpresion.Tables["tblimpresion"].Rows.Count != 0)
                    {
                        ImprimeInformeCuadreInv(DsImpresion, Ubicacion, IdBodega, IdPeriodo, MyForma, myconnect);
                    }
                    break;
            }
        }

        public void ImprimeInformeCuadreInv(DataSet dsreporte, double IdUbicacion, double IdBodega, int Periodo, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte msgrep = new ERP.Core.Compartido.Reportes.reporte("inv_fcuadreinv01");
            ERP.Core.Compartido.Reportes.config_report config = new ERP.Core.Compartido.Reportes.config_report();
            DataSet dscompania = new DataSet();
            string StNomBodega = " ", StNomUbicacion = " ";

            // this.msgsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, myconnect); // ERROR: CS1620

            if (IdUbicacion != 0)
            {
                // this.msginvconf.BuscaUbicacion(IdUbicacion, myconnect, ref StNomUbicacion); // ERROR: CS7036
            }
            else
            {
                StNomUbicacion = "Todas las Ubicaciones";
            }

            if (IdBodega != 0)
            {
                // this.msginvconf.BuscaBodega(IdBodega, IdUbicacion, myconnect, ref StNomBodega); // ERROR: CS1501
            }
            else
            {
                StNomBodega = "Todas las Bodegas";
            }

            msgrep.SetDataSource(dsreporte);
            msgrep.SetParameterValue("empresa", dscompania.Tables["tblcompania"].Rows[0]["nombre"]);
            msgrep.SetParameterValue("nit", dscompania.Tables["tblcompania"].Rows[0]["NIT"]);
            msgrep.SetParameterValue("direccion", dscompania.Tables["tblcompania"].Rows[0]["Direccion"]);
            msgrep.SetParameterValue("telefono", dscompania.Tables["tblcompania"].Rows[0]["TELEFONO"]);
            msgrep.SetParameterValue("IdUbicacion", IdUbicacion);
            msgrep.SetParameterValue("IdBodega", IdBodega);
            msgrep.SetParameterValue("NomUbicacion", StNomUbicacion);
            msgrep.SetParameterValue("NomBodega", StNomBodega);
            msgrep.SetParameterValue("periodo", Periodo);

            config.confi_reportes(myforma, msgrep);
        }

        public void GeneraInformeCuadreOtrasCtas(double Ubicacion, double IdBodega, DateTime fecIni, DateTime FecFin, int Rubro, Form MyForma, OdbcConnection myconnect)
        {
            StringBuilder StBuilder = new StringBuilder();
            DataSet Dsdata = new DataSet(), DsdataCta = new DataSet(), DsImpresion = new DataSet();
            ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
            ERP.Core.Compartido.Controles.Barraprogress msgbarra = new ERP.Core.Compartido.Controles.Barraprogress("Generando informe cuadre Inventario - Contabilidad", MyForma);

            double fila = 0, fila2 = 0;
            string StUbicacion = "", StBodega = "", StCuenta = "";
            int Sw1 = 0;
            double SaldoCuenta = 0;
            string cencos = "99999999", StManCencosto = "N";
            bool Inserta = false;
            string Cuenta = "";
            string StNomCuenta = "", StNomGrupo = "", StFiltro = "", StCuentaIva = "";
            double StValor = 0, VlrDebito = 0, VlrCredito = 0;
            string CostoNoGravado = "999999999999", CostoGravado = "999999999999", InvNoGravado = "999999999999", InvGravado = "999999999999", CuentaRetFte = "999999999999", CtaIca = "999999999999";
            string CuentaIva = "999999999999", Cuentadsto = "999999999999", VentasGrabadas = "999999999999", ventasNoGrabadas = "999999999999", Neto = "999999999999";

            DsImpresion.Tables.Add("tblimpresion");

            DsImpresion.Tables["tblimpresion"].Columns.Add("idubicacion", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("idbodega", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("idgrupo", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("idproducto", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("tasa", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("clasemovto", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("Valor", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("cuenta", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("VlrDebito", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("VlrCredito", fila.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("Nomcuenta", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("NomGrupo", StBodega.GetType());
            DsImpresion.Tables["tblimpresion"].Columns.Add("NomProducto", StBodega.GetType());

            if (Ubicacion != 0)
            {
                StUbicacion = " and invmovto.idubicacion = " + Ubicacion;
            }

            if (IdBodega != 0)
            {
                StBodega = " and invmovto.idbodega = " + IdBodega;
            }

            switch (Rubro)
            {
                case 3:
                    StFiltro = " and invmovto.ClaMovto='V' ";
                    break;
            }

            StBuilder.Append("select invmovto.idtipomovto,invpro.IdGruProducto,invmovto.idubicacion,invmovto.idbodega,invmovto.tasaiva,invmovto.ClaMovto,invmovto.IdProducto,invpro.descripcion,");
            StBuilder.Append("sum(invmovto.vlrIva) as Iva ,sum(invmovto.Vlrdsto) as dsto,sum(invmovto.subtotal) as  Subtotal,");
            StBuilder.Append("sum(invmovto.neto) as Neto, sum(invmovto.VlrRetfte) as VlrRetfte, sum(invmovto.Vlrica) as VlrIca,");
            StBuilder.Append("SUM(invmovto.costo  * case invmovto.TipoVenta when 'P' then (prec.cantidad * invmovto.Cantidad) else invmovto.Cantidad end) as CostoTotal ");
            StBuilder.Append("from inv_movtos invmovto ");
            StBuilder.Append("inner join inv_docs invdoc on invmovto.idtipomovto = invdoc.idtipomovto and invmovto.secuencia = invdoc.secuencia ");
            StBuilder.Append("inner join inv_productos invpro on invmovto.idproducto = invpro.idproducto ");
            StBuilder.Append("left join inv_precios prec on invpro.IdProducto=prec.IdProducto and invpro.IdTipoPrecio=prec.IdTipoPrecio and TipoCliente=2 ");
            StBuilder.Append("inner join inv_tipomovtos tipmov on invmovto.idtipomovto=tipmov.idtipomovto ");
            StBuilder.Append("where invmovto.FecMovto between '" + Strings.Format(fecIni, varini.PstForFec) + "' and '" + Strings.Format(FecFin, varini.PstForFec) + "' and ((invmovto.pos='Y' and invdoc.Estado<>'CA') or invmovto.pos<>'Y') and tipmov.actcontab='3' ");
            StBuilder.Append(StFiltro + StUbicacion + StBodega);
            StBuilder.Append(" group by invmovto.idtipomovto,invpro.IdGruProducto,invmovto.idubicacion,invmovto.idbodega,invmovto.TasaIva,invmovto.ClaMovto,invmovto.IdProducto,invpro.descripcion");

            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeCuadreOtrasCtas", ref Dsdata, "tblmovtos", true);

            switch (ok)
            {
                case true:
                    msgbarra.ValorMinimoMaximo(0, Dsdata.Tables["tblmovtos"].Rows.Count);
                    msgbarra.Show();

                    for (fila = 0; fila <= Dsdata.Tables["tblmovtos"].Rows.Count - 1; fila++)
                    {
                        DataRow row = Dsdata.Tables["tblmovtos"].Rows[(int)fila];
                        // this.msginvconf.BuscaCuentaContable(row["idubicacion"], row["idbodega"], row["IdGruProducto"], row["idtipomovto"], myconnect, ref CuentaIva, ref Cuentadsto, ref VentasGrabadas, ref ventasNoGrabadas, ref CostoNoGravado, ref CostoGravado, ref InvNoGravado, ref InvGravado, ref Neto, ref CuentaRetFte, ref CtaIca); // ERROR: CS7036
                        StCuentaIva = ""; Sw1 = 0; StValor = 0;
                        switch (Rubro)
                        {
                            case 9:
                                if (Convert.ToDouble(row["CostoTotal"]) != 0)
                                {
                                    if (Convert.ToDouble(row["tasaiva"]) > 0)
                                        Cuenta = InvGravado;
                                    else
                                        Cuenta = InvNoGravado;
                                    StValor = Convert.ToDouble(row["CostoTotal"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 2:
                                if (Convert.ToDouble(row["Iva"]) != 0)
                                {
                                    // ok = this.msginvconf.BuscaCuentasIva(row["idubicacion"], row["idbodega"], row["IdGruProducto"], row["idtipomovto"], row["tasaiva"], 1, myconnect, ref StCuentaIva); // ERROR: CS1503
                                    if (ok)
                                    {
                                        if (StCuentaIva.Trim() != "" && StCuentaIva.Trim() != "999999999999")
                                        {
                                            CuentaIva = StCuentaIva.Trim();
                                        }
                                    }
                                    Cuenta = CuentaIva;
                                    StValor = Convert.ToDouble(row["Iva"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 3:
                                if (Convert.ToDouble(row["neto"]) != 0)
                                {
                                    if (Convert.ToDouble(row["tasaiva"]) > 0)
                                    {
                                        Cuenta = VentasGrabadas;
                                        StValor = Convert.ToDouble(row["subtotal"]);
                                    }
                                    else
                                    {
                                        Cuenta = ventasNoGrabadas;
                                        if (Convert.ToDouble(row["Dsto"]) != 0)
                                        {
                                            StValor = Convert.ToDouble(row["subtotal"]);
                                        }
                                        else
                                        {
                                            StValor = Convert.ToDouble(row["neto"]);
                                        }
                                    }
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 4:
                                if (Convert.ToDouble(row["CostoTotal"]) != 0)
                                {
                                    if (Convert.ToDouble(row["tasaiva"]) > 0)
                                        Cuenta = CostoGravado;
                                    else
                                        Cuenta = CostoNoGravado;
                                    StValor = Convert.ToDouble(row["CostoTotal"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 5:
                                if (Convert.ToDouble(row["neto"]) != 0)
                                {
                                    Cuenta = Neto;
                                    StValor = Convert.ToDouble(row["neto"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 6:
                                if (Convert.ToDouble(row["Vlrica"]) != 0)
                                {
                                    Cuenta = CtaIca;
                                    StValor = Convert.ToDouble(row["Vlrica"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 7:
                                if (Convert.ToDouble(row["VlrRetfte"]) != 0)
                                {
                                    Cuenta = CuentaRetFte;
                                    StValor = Convert.ToDouble(row["VlrRetfte"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                            case 8:
                                if (Convert.ToDouble(row["Dsto"]) != 0)
                                {
                                    Cuenta = Cuentadsto;
                                    StValor = Convert.ToDouble(row["Dsto"]);
                                }
                                else
                                {
                                    Sw1 = 1;
                                }
                                break;
                        }

                        if (Sw1 == 0)
                        {
                            // this.msginvconf.BuscaGrupo(row["IdGruProducto"].ToString(), myconnect, ref StNomGrupo, ref cencos); // ERROR: CS7036
                            // msgcnt.BuscarCuenta(Cuenta, myconnect, ref StManCencosto, ref StNomCuenta); // ERROR: CS7036
                            switch (StManCencosto)
                            {
                                case "N":
                                    cencos = "99999999";
                                    break;
                            }

                            DsImpresion.Tables["tblimpresion"].Rows.Add(row["idubicacion"], row["idbodega"], row["IdGruProducto"], row["IdProducto"], row["tasaiva"], row["ClaMovto"], StValor, Cuenta, 0, 0, StNomCuenta, StNomGrupo, row["descripcion"]);
                        }
                        msgbarra.PerformStep();
                    }

                    StFiltro = "";

                    for (fila = 0; fila <= DsImpresion.Tables["tblimpresion"].Rows.Count - 1; fila++)
                    {
                        DataRow row = DsImpresion.Tables["tblimpresion"].Rows[(int)fila];
                        if (!StFiltro.Contains(row["cuenta"].ToString()))
                        {
                            StFiltro = StFiltro + ";" + row["cuenta"];
                            VlrDebito = 0; VlrCredito = 0;
                            StBuilder.Replace(StBuilder.ToString(), "");

                            StBuilder.Append("select SUM(vlr_debito) as debito,SUM(vlr_credito) as credito ");
                            StBuilder.Append("from cnt_movimto ");
                            StBuilder.Append("where cuenta ='" + row["cuenta"] + "' and fecha between '" + Strings.Format(fecIni, varini.PstForFec) + "' and '" + Strings.Format(FecFin, varini.PstForFec) + "'");

                            ok = this.MyOdbcConet.ExecuteQueryDataset(StBuilder.ToString(), myconnect, "GeneraInformeCuadreOtrasCtas(MovtoCnt)", ref DsdataCta, "tblmovto", true);
                            switch (ok)
                            {
                                case true:
                                    VlrDebito = Convert.ToDouble(DsdataCta.Tables["tblmovto"].Rows[0]["debito"]);
                                    VlrCredito = Convert.ToDouble(DsdataCta.Tables["tblmovto"].Rows[0]["credito"]);
                                    break;
                            }

                            if (fila < (DsImpresion.Tables["tblimpresion"].Rows.Count - 1))
                            {
                                for (fila2 = fila; fila2 <= DsImpresion.Tables["tblimpresion"].Rows.Count - 1; fila2++)
                                {
                                    if (row["cuenta"].ToString() == DsImpresion.Tables["tblimpresion"].Rows[(int)fila2]["cuenta"].ToString())
                                    {
                                        DsImpresion.Tables["tblimpresion"].Rows[(int)fila2]["VlrDebito"] = VlrDebito;
                                        DsImpresion.Tables["tblimpresion"].Rows[(int)fila2]["VlrCredito"] = VlrCredito;
                                    }
                                }
                            }
                            else
                            {
                                row["VlrDebito"] = VlrDebito;
                                row["VlrCredito"] = VlrCredito;
                            }
                            DsdataCta.Tables.Clear();
                        }
                    }

                    msgbarra.Close();
                    msgbarra.Dispose();

                    if (DsImpresion.Tables["tblimpresion"].Rows.Count != 0)
                    {
                        ImprimeInformeCuadreOtrasCtas(DsImpresion, Ubicacion, IdBodega, fecIni, FecFin, Rubro, MyForma, myconnect);
                    }
                    break;
            }
        }

        public void ImprimeInformeCuadreOtrasCtas(DataSet dsreporte, double IdUbicacion, double IdBodega, DateTime FecIni, DateTime FecFin, int Rubro, Form myforma, OdbcConnection myconnect)
        {
            ERP.Core.Compartido.Reportes.reporte msgrep = new ERP.Core.Compartido.Reportes.reporte("inv_fcuadreinv01a");
            ERP.Core.Compartido.Reportes.config_report config = new ERP.Core.Compartido.Reportes.config_report();
            DataSet dscompania = new DataSet();
            string StNomBodega = " ", StNomUbicacion = " ";

            // this.msgsys.BuscarCompania(varini.sptCodEmpr, ref dscompania, myconnect); // ERROR: CS1620

            if (IdUbicacion != 0)
            {
                // this.msginvconf.BuscaUbicacion(IdUbicacion, myconnect, ref StNomUbicacion); // ERROR: CS7036
            }
            else
            {
                StNomUbicacion = "Todas las Ubicaciones";
            }

            if (IdBodega != 0)
            {
                // this.msginvconf.BuscaBodega(IdBodega, IdUbicacion, myconnect, ref StNomBodega); // ERROR: CS1501
            }
            else
            {
                StNomBodega = "Todas las Bodegas";
            }

            msgrep.SetDataSource(dsreporte);
            msgrep.SetParameterValue("empresa", dscompania.Tables["tblcompania"].Rows[0]["nombre"]);
            msgrep.SetParameterValue("nit", dscompania.Tables["tblcompania"].Rows[0]["NIT"]);
            msgrep.SetParameterValue("direccion", dscompania.Tables["tblcompania"].Rows[0]["Direccion"]);
            msgrep.SetParameterValue("telefono", dscompania.Tables["tblcompania"].Rows[0]["TELEFONO"]);
            msgrep.SetParameterValue("IdUbicacion", IdUbicacion);
            msgrep.SetParameterValue("IdBodega", IdBodega);
            msgrep.SetParameterValue("NomUbicacion", StNomUbicacion);
            msgrep.SetParameterValue("NomBodega", StNomBodega);
            msgrep.SetParameterValue("fecinicio", FecIni);
            msgrep.SetParameterValue("fecfinal", FecFin);
            msgrep.SetParameterValue("rubro", Rubro);

            config.confi_reportes(myforma, msgrep);
        }

        // --- Exec helpers (same pattern as msginvconfig.CSharp) ---
        private bool Exec0(string sql, OdbcConnection conn, string proc)
        {
            string a = " ", b = " ", c = " ", d = " ";
            return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref a, ref b, ref c, ref d);
        }

        private bool Exec1(string sql, OdbcConnection conn, string proc, ref string c1)
        {
            string b = " ", c = " ", d = " ";
            return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref b, ref c, ref d);
        }

        private bool Exec2(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2)
        {
            string c = " ", d = " ";
            return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c, ref d);
        }

        private bool Exec3(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2, ref string c3)
        {
            string d = " ";
            return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref d);
        }

        private bool Exec4(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2, ref string c3, ref string c4)
        {
            return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref c4);
        }
    }
}
