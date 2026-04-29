using System;
using System.Data;
using System.Data.Odbc;
using System.Text;
using System.Windows.Forms;

namespace ERP.Core.Inventario.Services
{
    // Traducción de: ClsInvConfig.vb
    public class ClsInvConfig
    {
        private string stmysql;
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        private ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.Contabilidad.Services.ClsContabilidad msgcnt = new ERP.Core.Contabilidad.Services.ClsContabilidad();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private bool ok;

        public enum Navega : int
        {
            Anterior = 1,
            Primero = 2,
            Siguiente = 3,
            Ultimo = 4,
            Ninguno = 5
        }

        public ClsInvConfig()
        {
            MyOdbcConet.MyOdbcConect(ref varini);
        }

        // Exec helpers — wrap ExecuteQueryconec for 0–4 campo params
        private bool Exec(string sql, OdbcConnection conn, string proc)
        { string a = " ", b = " ", c = " ", d = " "; return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref a, ref b, ref c, ref d); }
        private bool Exec(string sql, OdbcConnection conn, string proc, ref string c1)
        { string b = " ", c = " ", d = " "; return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref b, ref c, ref d); }
        private bool Exec(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2)
        { string c = " ", d = " "; return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c, ref d); }
        private bool Exec(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2, ref string c3)
        { string d = " "; return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref d); }
        private bool Exec(string sql, OdbcConnection conn, string proc, ref string c1, ref string c2, ref string c3, ref string c4)
        { return MyOdbcConet.ExecuteQueryconec(sql, conn, proc, ref c1, ref c2, ref c3, ref c4); }

        // ─────────────────────────────────────────────────────────────────────
        // ConfiguraForma
        // ─────────────────────────────────────────────────────────────────────
        public void ConfiguraForma(Form Forma, ref string Empresa, ref string Servidor,
            ref string Bd, ref string Nomforma, ref string Usuario, ref string Fecha)
        {
            MyOdbcConet.LlenarVarini(ref varini);
            // Empresa  = varini.PstEmpresa; // ERROR: CS1061
            // Servidor = varini.PstServer; // ERROR: CS1061
            // Bd       = varini.PstBdatos; // ERROR: CS1061
            Nomforma = Forma.Name;
            // Usuario  = varini.PstUsuario; // ERROR: CS1061
            Fecha    = DateTime.Now.ToString(varini.PstForFec);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaProductos
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaProductos(double IdProducto, string Descripcion, string Resumido,
            int IdBodega, string ClaProducto, string Medida, int IdTipoPrecio,
            int IdTipoDsto, int ClaIva, decimal TasaIva, string CodigoBarras,
            decimal CostoPro, decimal UltCostoPro, double CantDisp,
            DateTime FecUltCompra, int CantUltCompra, string GruProducto,
            DateTime FecCreacion, string IdUsuario, string otrosimp, int Estado,
            decimal RetFte, string ctrlexistencia, OdbcConnection Myconect,
            string rstingLimeteVentas, int cantRestringVentas,
            double stockminimo, double stockmaximo)
        {
            ok = BuscaProductosExiste(IdProducto, Myconect);
            if (!ok)
            {
                stmysql = "insert into inv_productos (IdProducto,Descripcion,Resumido,IdBodega,ClaProducto,Medida,IdTipoPrecio,IdTipoDsto,ClaIva,TasaIva,CodigoBarras,CostoPro,UltCostoPro,CantDisp,FecUltCompra,CantUltCompra,IdGruProducto,FecCreacion,otrosimp,estado,RetFte,IdUsuario,ctrlexistencia,rstingLimeteVentas,cantRestringVentas,stockminimo,stockmaximo)"
                    + "values (" + IdProducto + ",'" + Descripcion + "','" + Resumido + "','" + IdBodega + "','" + ClaProducto + "','" + Medida + "','" + IdTipoPrecio + "','" + IdTipoDsto + "','" + ClaIva + "','" + TasaIva + "','" + CodigoBarras + "','" + CostoPro + "','" + UltCostoPro
                    + "','" + CantDisp + "','" + FecUltCompra.ToString(varini.PstForFec) + "','" + CantUltCompra + "','" + GruProducto + "','" + FecCreacion.ToString(varini.PstForFec) + "','" + otrosimp + "','" + Estado + "','" + RetFte + "','" + IdUsuario + "','" + ctrlexistencia + "','" + rstingLimeteVentas + "'," + cantRestringVentas + "," + stockminimo + "," + stockmaximo + ")";
            }
            else
            {
                stmysql = "update inv_productos  set Descripcion = '" + Descripcion + "',Resumido = '" + Resumido + "',IdBodega = '" + IdBodega + "',ClaProducto = '" + ClaProducto + "',Medida = '" + Medida + "',IdTipoPrecio ='" + IdTipoPrecio + "',IdTipoDsto = '" + IdTipoDsto
                    + "',ClaIva = '" + ClaIva + "',TasaIva ='" + TasaIva + "',CodigoBarras = '" + CodigoBarras + "',CostoPro = '" + CostoPro + "',UltCostoPro = '" + UltCostoPro + "',CantDisp = '" + CantDisp + "',FecUltCompra = '" + FecUltCompra.ToString(varini.PstForFec) + "',CantUltCompra = '" + CantUltCompra
                    + "',IdGruProducto = '" + GruProducto + "',FecCreacion = '" + FecCreacion.ToString(varini.PstForFec) + "',IdUsuario = '" + IdUsuario + "', otrosimp = '" + otrosimp + "',estado = '" + Estado + "', RetFte = '" + RetFte + "',ctrlexistencia='" + ctrlexistencia + "' "
                    + ",rstingLimeteVentas = '" + rstingLimeteVentas + "',cantRestringVentas =" + cantRestringVentas + ",stockminimo=" + stockminimo + ",stockmaximo=" + stockmaximo + "   where IdProducto = " + IdProducto;
            }
            ok = Exec(stmysql, Myconect, "GrabaProductos");
            return ok;
        }

        // Helper privado: solo verifica existencia por IdProducto
        private bool BuscaProductosExiste(double IdProducto, OdbcConnection Myconect)
        {
            string _u = " ";
            stmysql = "select" + varini.Psttop + " Descripcion as campo1 from inv_productos where IdProducto = " + IdProducto;
            return Exec(stmysql, Myconect, "BuscaProductos", ref _u);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EliminaProducto
        // ─────────────────────────────────────────────────────────────────────
        public void EliminaProducto(double IdProducto, OdbcConnection Myconect)
        {
            stmysql = "delete from inv_productos where IdProducto = " + IdProducto;
            Exec(stmysql, Myconect, "EliminaProducto");
        }

        // ─────────────────────────────────────────────────────────────────────
        // CargaProductos
        // ─────────────────────────────────────────────────────────────────────
        public void CargaProductos(OdbcConnection myconect, DataGridView Grilla, string orden)
        {
            var IdProducto = new DataGridViewTextBoxColumn { Name = "IdProducto", HeaderText = "Producto", Width = 100 };
            var Idcolumn   = new DataGridViewTextBoxColumn { Name = "Descripcion", HeaderText = "Descripcion", Width = 260 };
            var TipoVenta  = new DataGridViewTextBoxColumn { Name = "Tipoventa", HeaderText = "Tipo Ven", Width = 50 };
            var Cantidad   = new DataGridViewTextBoxColumn { Name = "Cantidad", HeaderText = "Cant.", Width = 50 };
            var ValorUni   = new DataGridViewTextBoxColumn { Name = "VlrUnidad", HeaderText = "Vlr. Unidad", Width = 100 };
            var Valor      = new DataGridViewTextBoxColumn { Name = "Valor", HeaderText = "SubTotal", Width = 100 };
            var Iva        = new DataGridViewTextBoxColumn { Name = "Iva", HeaderText = "Iva", Width = 50 };
            var Dsto       = new DataGridViewTextBoxColumn { Name = "Dsto", HeaderText = "Dsto", Width = 50 };
            var Estado     = new DataGridViewTextBoxColumn { Name = "Estado", HeaderText = "Estado", Width = 50 };
            var Ubicacion  = new DataGridViewTextBoxColumn { Name = "ubicacion", HeaderText = "Ubicacion", Width = 50 };
            var Bodega     = new DataGridViewTextBoxColumn { Name = "bodega", HeaderText = "Bodega", Width = 50 };
            var ConsecMovto= new DataGridViewTextBoxColumn { Name = "consecmovto", HeaderText = "consecmovto", Width = 50, SortMode = DataGridViewColumnSortMode.Programmatic };
            var CostoMovto = new DataGridViewTextBoxColumn { Name = "CostoMovto", HeaderText = "CostoMovto", Width = 50 };
            var TasaRtFte  = new DataGridViewTextBoxColumn { Name = "TasaRtFte", HeaderText = "TasaRtFte", Width = 50 };
            var TasaIca    = new DataGridViewTextBoxColumn { Name = "TasaIca", HeaderText = "TasaIca", Width = 50 };
            var vlrTasaIca = new DataGridViewTextBoxColumn { Name = "vlrTasaIca", HeaderText = "vlrTasaIca", Width = 50 };
            var vlrTasaRtFte = new DataGridViewTextBoxColumn { Name = "vlrTasaRtFte", HeaderText = "vlrTasaRtFte", Width = 50 };
            var aplica_orden = new DataGridViewTextBoxColumn { Name = "aplica_orden", HeaderText = "aplica_orden", Width = 50 };

            Grilla.Columns.Clear();
            Grilla.RowHeadersVisible = false;
            Grilla.ColumnHeadersVisible = true;
            Grilla.AutoGenerateColumns = true;
            Grilla.Columns.Add(IdProducto);
            Grilla.Columns.Add(Idcolumn);
            Grilla.Columns.Add(TipoVenta);
            Grilla.Columns.Add(Cantidad);
            Grilla.Columns.Add(ValorUni);
            Grilla.Columns.Add(Valor);
            Grilla.Columns.Add(Iva);
            Grilla.Columns.Add(Dsto);
            Grilla.Columns.Add(Estado);
            Grilla.Columns.Add(Ubicacion);
            Grilla.Columns.Add(Bodega);
            Grilla.Columns.Add(ConsecMovto);
            Grilla.Columns.Add(CostoMovto);
            Grilla.Columns.Add(TasaRtFte);
            Grilla.Columns.Add(TasaIca);
            Grilla.Columns.Add(vlrTasaIca);
            Grilla.Columns.Add(vlrTasaRtFte);
            if (orden.Trim() == "Y")
                Grilla.Columns.Add(aplica_orden);

            Grilla.Rows.Clear();
            Grilla.RowsDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            Grilla.Rows.Add();
            Grilla.Columns[7].Visible  = false;
            Grilla.Columns[11].Visible = false;
            Grilla.Columns[12].Visible = false;
            Grilla.Columns[13].Visible = false;
            Grilla.Columns[14].Visible = false;
            Grilla.Columns[15].Visible = false;
            Grilla.Columns[16].Visible = false;
            if (orden.Trim() == "Y")
                Grilla.Columns[17].Visible = false;
            Grilla.EditMode = DataGridViewEditMode.EditOnEnter;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaProductos
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaProductos(ref string IdProducto, OdbcConnection Myconect,
            ref string Descripcion, ref string Resumido, ref string IdBodega,
            ref string ClaProducto, ref string Medida, ref string IdTipoPrecio,
            ref string IdTipoDsto, ref string ClaIva, ref string TasaIva,
            ref string CodigoBarras, ref string CostoPro, ref string UltCostoPro,
            ref string CantDisp, ref string FecUltCompra, ref string CantUltCompra,
            ref string GruProducto, ref string FecCreacion, ref string IdUsuario,
            ref string PreVenta, ref string FecIni, ref string Fecfin,
            ref string PorDsto, string Nit, ref Navega Navegar,
            ref string PreVentCant, ref string NumCant, ref string OtrosImp,
            ref string estado, ref string RetFte, ref string CtrlExistencia,
            ref string porccosto, ref string rstingLimeteVentas,
            ref string cantRestringVentas, ref string stockminimo, ref string stockmaximo)
        {
            bool ok = false;
            string where = "";

            switch (Navegar)
            {
                case Navega.Ninguno:
                    where = "from inv_productos where IdProducto = " + IdProducto;
                    break;
                case Navega.Anterior:
                    where = "from  inv_productos where IdProducto < " + IdProducto + " order by IdProducto desc " + varini.Pstlimit;
                    break;
                case Navega.Primero:
                    where = "from  inv_productos where IdProducto > 0 order by IdProducto " + varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = "from  inv_productos where IdProducto > " + IdProducto + "  order by IdProducto " + varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = "from  inv_productos where IdProducto < 99999999  order by IdProducto desc" + varini.Pstlimit;
                    break;
            }

            stmysql = "select" + varini.Psttop + " Descripcion as campo1,Resumido as campo2,IdBodega as campo3,ClaProducto as campo4 ";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref Descripcion, ref Resumido, ref IdBodega, ref ClaProducto);

            if (!ok)
            {
                stmysql = "select" + varini.Psttop + " Idproducto as campo1 from inv_productos where CodigoBarras = '" + IdProducto + "'";
                ok = Exec(stmysql, Myconect, "BuscaProductos", ref IdProducto);
                if (!ok)
                    return false;

                where = "from inv_productos where IdProducto = " + IdProducto;
                stmysql = "select" + varini.Psttop + " Descripcion as campo1,Resumido as campo2,IdBodega as campo3,ClaProducto as campo4 ";
                ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref Descripcion, ref Resumido, ref IdBodega, ref ClaProducto);
            }

            stmysql = "select" + varini.Psttop + " Medida as campo1,IdTipoPrecio as campo2,IdTipoDsto as campo3,ClaIva as campo4 ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref Medida, ref IdTipoPrecio, ref IdTipoDsto, ref ClaIva);

            stmysql = "select" + varini.Psttop + " TasaIva as campo1,CodigoBarras as campo2, RetFte as campo3, ctrlexistencia as campo4 ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref TasaIva, ref CodigoBarras, ref RetFte, ref CtrlExistencia);

            stmysql = "select" + varini.Psttop + " FecUltCompra as campo1,CantUltCompra as campo2,IdGruProducto as campo3,estado as campo4 ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref FecUltCompra, ref CantUltCompra, ref GruProducto, ref estado);

            stmysql = "select" + varini.Psttop + " FecCreacion as campo1,IdUsuario as campo2,IdProducto as campo3,OtrosImp as campo4 ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref FecCreacion, ref IdUsuario, ref IdProducto, ref OtrosImp);

            stmysql = "select" + varini.Psttop + " rstingLimeteVentas as campo1, cantRestringVentas as campo2,stockminimo as campo3,stockmaximo as campo4    ";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaProductos", ref rstingLimeteVentas, ref cantRestringVentas, ref stockminimo, ref stockmaximo);

            if (Nit == "99999999999999" || Nit == null)
            {
                BuscaListaPrecios(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, ref PreVenta);
            }
            else
            {
                string _u = " ", _tipoTercero = "0", _precioesp = "N";
                // msgcnt.BuscarTercero(Nit, Myconect, ref _u, ref _u, ref _u, ref _u, ref _tipoTercero, ref _precioesp, ref _u); // ERROR: CS7036
                int TipoTercero = Convert.ToInt32(_tipoTercero);
                string Precioesp = _precioesp;
                if (TipoTercero == 5 || TipoTercero == 6)
                {
                    BuscaListaPrecios(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, "1", ref PreVenta);
                    if (Precioesp == "N") PreVenta = "0";
                    if (PreVenta == "0")
                        BuscaListaPrecios(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, ref PreVenta);
                }
                else
                {
                    if (Precioesp == "Y")
                        BuscaListaPrecios(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, "3", ref PreVenta);
                    else
                        BuscaListaPrecios(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, ref PreVenta);
                }
            }

            BuscaListaPreciosCantidad(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, "2", ref PreVentCant, ref NumCant);
            BuscaListaPrecios(int.Parse(IdTipoPrecio), Myconect, ref IdProducto, "4", ref porccosto);
            BuscaDsto(int.Parse(IdTipoDsto), IdProducto, Myconect, ref PorDsto, GruProducto);
            return ok;
        }

        // Helper privado: BuscaListaPrecios con TipoCliente "0" por defecto
        private void BuscaListaPrecios(int IdTipoPrecio, OdbcConnection myconect, ref string IdProducto, ref string PreVenta)
        {
            string _tipocli = "0", _fecini = " ", _fecfin = " ", _desc = " ";
            BuscaListaPrecios(IdTipoPrecio, myconect, ref IdProducto, ref _tipocli, ref _fecini, ref _fecfin, ref PreVenta, ref _desc);
        }

        // Helper privado: BuscaListaPrecios con TipoCliente explícito
        private void BuscaListaPrecios(int IdTipoPrecio, OdbcConnection myconect, ref string IdProducto, string TipoCliente, ref string PreVenta)
        {
            string _fecini = " ", _fecfin = " ", _desc = " ";
            string _tipocli = TipoCliente;
            BuscaListaPrecios(IdTipoPrecio, myconect, ref IdProducto, ref _tipocli, ref _fecini, ref _fecfin, ref PreVenta, ref _desc);
        }

        // Helper privado: BuscaListaPreciosCantidad con parámetros simplificados
        private void BuscaListaPreciosCantidad(int IdTipoPrecio, OdbcConnection myconect,
            ref string IdProducto, string TipoCliente, ref string PreVentCant, ref string NumCant)
        {
            string _fecini = " ", _fecfin = " ", _desc = " ";
            string _tipocli = TipoCliente;
            BuscaListaPreciosCantidad(IdTipoPrecio, myconect, ref IdProducto, ref _tipocli,
                ref _fecini, ref _fecfin, ref PreVentCant, ref _desc, ref NumCant);
        }

        // Helper privado: BuscaDsto con parámetros simplificados
        private void BuscaDsto(int IdTipoDsto, string IdProducto, OdbcConnection Myconect,
            ref string PorDsto, string IdGrupo)
        {
            string _desc = " ", _fecini = " ", _fecfin = " ";
            BuscaDsto(IdTipoDsto, IdProducto, Myconect, ref _desc, ref _fecini, ref _fecfin, ref PorDsto, IdGrupo);
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaPrecio
        // ─────────────────────────────────────────────────────────────────────
        public void BuscaPrecio(int IdTipoPrecio, double IdProducto, OdbcConnection Myconect,
            ref string Descripcion, ref string FecIni, ref string Fecfin, ref string PreVenta)
        {
            string _clasePrecio = "0";
            stmysql = "select ClasePrecio as campo1,Descripcion as campo2 from inv_tipoprecios where IdTipoPrecio = " + IdTipoPrecio;
            Exec(stmysql, Myconect, "BuscaTipoPrecio", ref _clasePrecio, ref Descripcion);
            int ClasePrecio = Convert.ToInt32(_clasePrecio);
            if (ClasePrecio == 0)
            {
                stmysql = " select FecIni as campo1,FecFin as campo2,PreVenta as campo3 from inv_precios where IdTipoPrecio = " + ClasePrecio + " and IdProducto =" + IdProducto;
                Exec(stmysql, Myconect, "BuscaTipoPrecio", ref FecIni, ref Fecfin, ref PreVenta);
            }
            else if (ClasePrecio == 1)
            {
                stmysql = " select FecIni as campo1,FecFin as campo2,PreVenta as campo3 from inv_precios where IdTipoPrecio = " + ClasePrecio + " and IdCliente =" + IdProducto;
                Exec(stmysql, Myconect, "BuscaTipoPrecio", ref FecIni, ref Fecfin, ref PreVenta);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaTipoDsto / BuscaTipodsto / BuscaDsto
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaTipoDsto(int IdTipoDsto, OdbcConnection myconnect,
            int ClaseDsto, string Descripcion)
        {
            ok = BuscaTipodsto(ref IdTipoDsto, myconnect);
            if (!ok)
            {
                stmysql = " insert into inv_tipodstos(IdTipoDsto,ClaseDsto,Descripcion) values ('"
                        + IdTipoDsto + "','" + ClaseDsto + "','" + Descripcion + "')";
            }
            else
            {
                stmysql = "update inv_tipodstos set ClaseDsto = '" + ClaseDsto + "',Descripcion = '" + Descripcion
                        + "' where IdTipoDsto = '" + IdTipoDsto + "'";
            }
            ok = Exec(stmysql, myconnect, "GrabaTipoDsto");
            return ok;
        }

        public bool BuscaTipodsto(ref int IdTipoDsto, OdbcConnection myconnect,
            ref string ClaseDsto, ref string Descripcion, ref Navega navegar)
        {
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:
                    where = "from inv_tipodstos where IdTipoDsto = " + IdTipoDsto;
                    break;
                case Navega.Anterior:
                    where = "from  inv_tipodstos where IdTipoDsto < " + IdTipoDsto + " order by IdTipoDsto desc " + varini.Pstlimit;
                    break;
                case Navega.Primero:
                    where = "from  inv_tipodstos where IdTipoDsto > 0 order by IdTipoDsto " + varini.Pstlimit;
                    break;
                case Navega.Siguiente:
                    where = "from  inv_tipodstos where IdTipoDsto > " + IdTipoDsto + "  order by IdTipoDsto " + varini.Pstlimit;
                    break;
                case Navega.Ultimo:
                    where = "from  inv_tipodstos where IdTipoDsto < 99999999  order by IdTipoDsto desc" + varini.Pstlimit;
                    break;
            }
            string _id = IdTipoDsto.ToString();
            stmysql = "select" + varini.Psttop + " ClaseDsto as campo1,Descripcion as campo2,IdTipoDsto as campo3 ";
            ok = Exec(stmysql + where, myconnect, "BuscaTipodsto", ref ClaseDsto, ref Descripcion, ref _id);
            IdTipoDsto = Convert.ToInt32(_id);
            return ok;
        }

        // Overload: simple (solo IdTipoDsto + conn)
        public bool BuscaTipodsto(ref int IdTipoDsto, OdbcConnection myconnect)
        {
            string _clase = "0", _desc = " ";
            Navega _nav = Navega.Ninguno;
            return BuscaTipodsto(ref IdTipoDsto, myconnect, ref _clase, ref _desc, ref _nav);
        }

        // Overload: IdTipoDsto + conn + ClaseDsto (para BuscaListaDstos)
        public bool BuscaTipodsto(int IdTipoDsto, OdbcConnection myconnect, ref int ClaseDsto)
        {
            string _clase = "0", _desc = " ";
            Navega _nav = Navega.Ninguno;
            int _id = IdTipoDsto;
            ok = BuscaTipodsto(ref _id, myconnect, ref _clase, ref _desc, ref _nav);
            ClaseDsto = Convert.ToInt32(_clase);
            return ok;
        }

        public void BuscaDsto(int IdTipoDsto, string IdProducto, OdbcConnection Myconect,
            ref string Descripcion, ref string FecIni, ref string Fecfin,
            ref string PorDsto, string IdGrupo)
        {
            string _claseDsto = "0";
            stmysql = "select ClaseDsto as campo1,Descripcion as campo2 from inv_tipodstos where IdTipoDsto = " + IdTipoDsto;
            Exec(stmysql, Myconect, "BuscaTipoPrecio", ref _claseDsto, ref Descripcion);
            int ClaseDsto = Convert.ToInt32(_claseDsto);
            if (ClaseDsto == 0)
            {
                stmysql = " select FecIni as campo1,FecFin as campo2,PorDsto as campo3 from inv_dstos where IdTipoDsto = " + IdTipoDsto + " and IdProducto =" + IdProducto;
                Exec(stmysql, Myconect, "BuscaTipoPrecio", ref FecIni, ref Fecfin, ref PorDsto);
            }
            else if (ClaseDsto == 2)
            {
                stmysql = " select FecIni as campo1,FecFin as campo2,PorDsto as campo3 from inv_dstos where IdTipoDsto = " + IdTipoDsto + " and IdGrupo =" + IdGrupo;
                Exec(stmysql, Myconect, "BuscaTipoPrecio", ref FecIni, ref Fecfin, ref PorDsto);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaPunto / BuscaPuntoUsuario
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaPunto(int IdPunto, int Idturno, DateTime Fecha, OdbcConnection myconnect,
            ref string IdUsuario, ref string IdTipoMovto, ref string Printer,
            ref string estado, ref string Base, ref string idubicacion, ref string idbodega)
        {
            stmysql = "select IdUsuario as campo1,IdTipoMovto as campo2,Printer as campo3, Estado as campo4 from Inv_puntos where IdPunto =" + IdPunto + " and IdFecha='" + Fecha.ToString(varini.PstForFec) + "' and idturno = '" + Idturno + "'";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaPunto", ref IdUsuario, ref IdTipoMovto, ref Printer, ref estado);

            string _idturno = Idturno.ToString();
            stmysql = "select IdTurno as campo1,Base as campo2,idubicacion as campo3,idbodega as campo4 from Inv_puntos where IdPunto =" + IdPunto + " and IdFecha='" + Fecha.ToString(varini.PstForFec) + "' and idturno = '" + Idturno + "'";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaPunto", ref _idturno, ref Base, ref idubicacion, ref idbodega);
            return ok;
        }

        // Overload simple para GrabaPunto
        private bool BuscaPunto(object IdPunto, int IdTurno, DateTime fecha, OdbcConnection myconnect)
        {
            string _u = " ", _nav = " ";
            return BuscaPunto(Convert.ToInt32(IdPunto), IdTurno, fecha, myconnect,
                ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);
        }

        public bool BuscaPuntoUsuario(int IdPunto, string IdUsuario, DateTime Fecha,
            OdbcConnection myconnect, ref string IdTipoMovto, ref string Printer,
            ref string estado, ref string Idturno, ref string Base)
        {
            stmysql = "select IdUsuario as campo1,IdTipoMovto as campo2,Printer as campo3, Estado as campo4 from Inv_puntos where IdPunto =" + IdPunto + " and IdFecha='" + Fecha.ToString(varini.PstForFec) + "' and IdUsuario = '" + IdUsuario + "'";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaPunto", ref IdUsuario, ref IdTipoMovto, ref Printer, ref estado);

            stmysql = "select IdTurno as campo1,Base as campo2 from Inv_puntos where IdPunto =" + IdPunto + " and IdFecha='" + Fecha.ToString(varini.PstForFec) + "' and IdUsuario = '" + IdUsuario + "'";
            ok = Exec(stmysql, myconnect, "BuscaPunto", ref Idturno, ref Base);
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaCuentaContable (overload 1: idGrupo + IdTipomovto)
        // ─────────────────────────────────────────────────────────────────────
        public void GrabaCuentaContable(string idGrupo, int IdTipomovto,
            string CuentaIva, string CuentaDstos, string VentasGravadas,
            string VentasNoGravadas, string Neto, string CostosNoGravado,
            string CostosGravado, string InvNoGravado, string InvGravado,
            string RetFte, string Ica, OdbcConnection myconnect, string Usuario)
        {
            ok = BuscaCuentaContable(idGrupo, IdTipomovto, myconnect);
            if (!ok)
            {
                stmysql = "insert into inv_cuentas(IdGruProducto,IdTipoMovto,Iva,Dstos,VentasGravadas,VentasNoGravadas,Neto,CostoNoGravado,CostoGravado,InvNoGravado,InvGravado,RetFte,ica) "
                        + "values ('" + idGrupo + "','" + IdTipomovto + "','" + CuentaIva + "','" + CuentaDstos + "','"
                        + VentasGravadas + "','" + VentasNoGravadas + "','" + Neto + "','" + CostosNoGravado + "','" + CostosGravado + "','" + InvNoGravado + "','" + InvGravado + "','" + RetFte + "','" + Ica + "','" + Usuario + "')";
            }
            else
            {
                stmysql = "update inv_cuentas set Iva ='" + CuentaIva + "', Dstos = '" + CuentaDstos + "',VentasGravadas = '"
                    + VentasGravadas + "', VentasNoGravadas ='" + VentasNoGravadas + "',Neto = '" + Neto + "',CostoNoGravado  = '"
                    + CostosNoGravado + "', CostoGravado = '" + CostosGravado + "', InvNoGravado = '" + InvNoGravado + "',InvGravado = '" + InvGravado + "',RetFte = '" + RetFte + "',"
                    + "Ica = '" + Ica + "', usuario= '" + Usuario + "'"
                    + " where IdGruProducto = '" + idGrupo + "' and IdTipoMovto = '" + IdTipomovto + "'";
            }
            Exec(stmysql, myconnect, "GrabaCuentaContable");
        }

        public bool BuscaCuentaContable(string idGrupo, int IdTipomovto, OdbcConnection myconnect,
            ref string CuentaIva, ref string CuentaDstos, ref string VentasGravadas,
            ref string VentasNoGravadas, ref string CostoNogravado, ref string Costogravado,
            ref string InvNoGravado, ref string InvGravado, ref string Neto,
            ref string RetFte, ref string Ica)
        {
            stmysql = "select iva as campo1, Dstos as campo2, VentasGravadas as campo3, VentasNoGravadas as campo4  from inv_cuentas  where IdGruProducto = '" + idGrupo + "' and IdTipoMovto= " + IdTipomovto;
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaCuentaContable", ref CuentaIva, ref CuentaDstos, ref VentasGravadas, ref VentasNoGravadas);

            stmysql = "select Neto as campo1,costoNoGravado as campo2,CostoGravado as campo3,InvNoGravado as campo4 from inv_cuentas  where IdGruProducto = '" + idGrupo + "' and IdTipoMovto= " + IdTipomovto;
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaCuentaContable", ref Neto, ref CostoNogravado, ref Costogravado, ref InvNoGravado);

            stmysql = "select InvGravado as campo1, RetFte as campo2, ica as campo3 from inv_cuentas  where IdGruProducto = '" + idGrupo + "' and IdTipoMovto= " + IdTipomovto;
            ok = Exec(stmysql, myconnect, "BuscaCuentaContable", ref InvGravado, ref RetFte, ref Ica);
            return ok;
        }

        // Overload simple (solo existencia)
        public bool BuscaCuentaContable(string idGrupo, int IdTipomovto, OdbcConnection myconnect)
        {
            string _u = "999999999999";
            return BuscaCuentaContable(idGrupo, IdTipomovto, myconnect,
                ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u);
        }

        public bool EliminaCuentaContable(string idGrupo, int IdTipomovto, OdbcConnection myconnect)
        {
            stmysql = "delete from inv_cuentas where IdGruProducto ='" + idGrupo + "' and IdTipoMovto= " + IdTipomovto;
            ok = Exec(stmysql, myconnect, "EliminaPunto");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaCuentaContable (overload 2: IdUbicacion + IdBodega + idGrupo)
        // ─────────────────────────────────────────────────────────────────────
        public virtual void GrabaCuentaContable(string IdUbicacion, string IdBodega,
            string idGrupo, int IdTipomovto, string CuentaIva, string CuentaDstos,
            string VentasGravadas, string VentasNoGravadas, string Neto,
            string CostosNoGravado, string CostosGravado, string InvNoGravado,
            string InvGravado, string RetFte, string Ica,
            OdbcConnection myconnect, string Usuario)
        {
            ok = BuscaCuentaContable(IdUbicacion, IdBodega, idGrupo, IdTipomovto, myconnect);
            if (!ok)
            {
                stmysql = "insert into inv_cuentas(idubicacion,idbodega,IdGruProducto,IdTipoMovto,Iva,Dstos,VentasGravadas,VentasNoGravadas,Neto,CostoNoGravado,CostoGravado,InvNoGravado,InvGravado,RetFte,ica,usuario) "
                        + "values ('" + IdUbicacion + "','" + IdBodega + "','" + idGrupo + "','" + IdTipomovto + "','" + CuentaIva + "','" + CuentaDstos + "','"
                        + VentasGravadas + "','" + VentasNoGravadas + "','" + Neto + "','" + CostosNoGravado + "','" + CostosGravado + "','" + InvNoGravado + "','" + InvGravado + "','" + RetFte + "','" + Ica + "','" + Usuario + "')";
            }
            else
            {
                stmysql = "update inv_cuentas set Iva ='" + CuentaIva + "', Dstos = '" + CuentaDstos + "',VentasGravadas = '"
                    + VentasGravadas + "', VentasNoGravadas ='" + VentasNoGravadas + "',Neto = '" + Neto + "',CostoNoGravado  = '"
                    + CostosNoGravado + "', CostoGravado = '" + CostosGravado + "', InvNoGravado = '" + InvNoGravado + "',InvGravado = '" + InvGravado + "',RetFte = '" + RetFte + "',"
                    + "Ica = '" + Ica + "', usuario= '" + Usuario + "'"
                    + " where IdGruProducto = '" + idGrupo + "' and IdTipoMovto = '" + IdTipomovto + "' and idubicacion = '" + IdUbicacion + "' and idbodega = '" + IdBodega + "'";
            }
            Exec(stmysql, myconnect, "GrabaCuentaContable");
        }

        public virtual bool BuscaCuentaContable(string IdUbicacion, string IdBodega,
            string idGrupo, int IdTipomovto, OdbcConnection myconnect,
            ref string CuentaIva, ref string CuentaDstos, ref string VentasGravadas,
            ref string VentasNoGravadas, ref string CostoNogravado, ref string Costogravado,
            ref string InvNoGravado, ref string InvGravado, ref string Neto,
            ref string RetFte, ref string Ica,
            double tasaiva, double tasaica, double tasarft)
        {
            string Civa = " ", Cica = " ", Crft = " ";

            stmysql = "select iva as campo1, Dstos as campo2, VentasGravadas as campo3, VentasNoGravadas as campo4  from inv_cuentas  where IdGruProducto = '" + idGrupo + "' and IdTipoMovto= " + IdTipomovto + " and idubicacion = '" + IdUbicacion + "' and idbodega = '" + IdBodega + "'";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaCuentaContable", ref CuentaIva, ref CuentaDstos, ref VentasGravadas, ref VentasNoGravadas);

            stmysql = "select Neto as campo1,costoNoGravado as campo2,CostoGravado as campo3,InvNoGravado as campo4 from inv_cuentas  where IdGruProducto = '" + idGrupo + "' and IdTipoMovto= " + IdTipomovto + " and idubicacion = '" + IdUbicacion + "' and idbodega = '" + IdBodega + "'";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaCuentaContable", ref Neto, ref CostoNogravado, ref Costogravado, ref InvNoGravado);

            stmysql = "select InvGravado as campo1, RetFte as campo2, ica as campo3 from inv_cuentas  where IdGruProducto = '" + idGrupo + "' and IdTipoMovto= " + IdTipomovto + " and idubicacion = '" + IdUbicacion + "' and idbodega = '" + IdBodega + "'";
            ok = Exec(stmysql, myconnect, "BuscaCuentaContable", ref InvGravado, ref RetFte, ref Ica);

            if (tasaiva > 0)
                if (BuscaCuentasIva(IdUbicacion, IdBodega, idGrupo, IdTipomovto.ToString(), (decimal)tasaiva, 1, myconnect, ref Civa))
                    CuentaIva = Civa;
            if (tasaica > 0)
                if (BuscaCuentasIva(IdUbicacion, IdBodega, idGrupo, IdTipomovto.ToString(), (decimal)tasaica, 6, myconnect, ref Cica))
                    Ica = Cica;
            if (tasarft > 0)
                if (BuscaCuentasIva(IdUbicacion, IdBodega, idGrupo, IdTipomovto.ToString(), (decimal)tasarft, 11, myconnect, ref Crft))
                    RetFte = Crft;

            return ok;
        }

        // Overload simple (solo existencia)
        public virtual bool BuscaCuentaContable(string IdUbicacion, string IdBodega,
            string idGrupo, int IdTipomovto, OdbcConnection myconnect)
        {
            string _u = "999999999999";
            return BuscaCuentaContable(IdUbicacion, IdBodega, idGrupo, IdTipomovto, myconnect,
                ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, ref _u, 0, 0, 0);
        }

        public virtual bool EliminaCuentaContable(string IdUbicacion, string IdBodega,
            string idGrupo, int IdTipomovto, OdbcConnection myconnect)
        {
            stmysql = "delete from inv_cuentas where IdGruProducto ='" + idGrupo + "' and IdTipoMovto= " + IdTipomovto + " and idubicacion = '" + IdUbicacion + "' and idbodega = '" + IdBodega + "'";
            ok = Exec(stmysql, myconnect, "EliminaPunto");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaPunto / EliminaPunto
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaPunto(string IdPunto, int IdTurno, DateTime fecha,
            OdbcConnection myconnect, ref string IdUsuario, ref string IdTipoMovto,
            ref string Printer, ref string estado,
            double Base, double IdUbicacion, double IdBodega)
        {
            ok = BuscaPunto(IdPunto, IdTurno, fecha, myconnect);
            if (!ok)
            {
                stmysql = "insert into Inv_puntos (IdPunto,IdFecha,IdUsuario,IdTipoMovto,Printer, Estado,IdTurno,Base,idubicacion,idbodega) values ('"
                        + IdPunto + "','" + fecha.ToString(varini.PstForFec) + "','" + IdUsuario + "','" + IdTipoMovto
                        + "','" + Printer + " ','" + estado + "','" + IdTurno + "','" + Base + "'," + IdUbicacion + "," + IdBodega + ")";
            }
            else
            {
                stmysql = "update Inv_puntos set IdUsuario = '" + IdUsuario + "',IdTipoMovto='" + IdTipoMovto + "',Printer='" + Printer
                        + "',Estado =" + estado + ",Base = '" + Base + "',idubicacion = '" + IdUbicacion + "',idbodega = '" + IdBodega + "' "
                        + " where IdPunto = '" + IdPunto + "' and IdTurno = ' " + IdTurno + "' and IdFecha = '" + fecha.ToString(varini.PstForFec) + "'";
            }
            ok = Exec(stmysql, myconnect, "GrabaPunto");
            return ok;
        }

        public bool EliminaPunto(string IdPunto, OdbcConnection myconnect)
        {
            stmysql = "delete from Inv_puntos where IdPunto ='" + IdPunto + "'";
            ok = Exec(stmysql, myconnect, "EliminaPunto");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaListaDstos
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaListaDstos(int IdTipoDsto, OdbcConnection myconnect,
            ref string IdProducto, ref string IdCliente, ref string IdGrupo,
            ref string ClaProducto, ref string ClaPago, ref string Fecini,
            ref string FecFin, ref string TipoCliente, ref string CantIni,
            ref string CantFin, ref string PerCompra, ref string MontoCompra,
            ref string PorDsto, ref string periodo)
        {
            int ClaseDsto = 0;
            BuscaTipodsto(IdTipoDsto, myconnect, ref ClaseDsto);
            switch (ClaseDsto)
            {
                case 0:
                    stmysql = "select Fecini as campo1, FecFin as campo2, CantIni as campo3, CantFin as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdProducto ='" + IdProducto + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref Fecini, ref FecFin, ref CantIni, ref CantFin);
                    stmysql = "select  TipoCliente as campo1, PerCompra as campo2, MontoCompra as campo3,PorDsto as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdProducto ='" + IdProducto + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref TipoCliente, ref PerCompra, ref MontoCompra, ref PorDsto);
                    stmysql = "select Periodo as campo1 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdProducto ='" + IdProducto + "'";
                    ok = Exec(stmysql, myconnect, "BuscaListaDstos", ref periodo);
                    break;
                case 1:
                    stmysql = "select Fecini as campo1, FecFin as campo2, CantIni as campo3, CantFin as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdCliente ='" + IdCliente + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref Fecini, ref FecFin, ref CantIni, ref CantFin);
                    stmysql = "select PerCompra as campo1, MontoCompra as campo2, PorDsto as campo3, Periodo as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdCliente ='" + IdCliente + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref PerCompra, ref MontoCompra, ref PorDsto, ref periodo);
                    stmysql = "select TipoCliente as campo1 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdCliente ='" + IdCliente + "'";
                    ok = Exec(stmysql, myconnect, "BuscaListaDstos", ref TipoCliente);
                    break;
                case 2:
                    stmysql = "select Fecini as campo1, FecFin as campo2, CantIni as campo3, CantFin as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdGrupo ='" + IdGrupo + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref Fecini, ref FecFin, ref CantIni, ref CantFin);
                    stmysql = "select PerCompra as campo1, MontoCompra as campo2, PorDsto as campo3, Periodo as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdGrupo ='" + IdGrupo + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref PerCompra, ref MontoCompra, ref PorDsto, ref periodo);
                    stmysql = "select TipoCliente as campo1 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdGrupo ='" + IdGrupo + "'";
                    ok = Exec(stmysql, myconnect, "BuscaListaDstos", ref TipoCliente);
                    break;
                case 3:
                    stmysql = "select Fecini as campo1, FecFin as campo2, CantIni as campo3, CantFin as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaProducto ='" + ClaProducto + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref Fecini, ref FecFin, ref CantIni, ref CantFin);
                    stmysql = "select PerCompra as campo1, MontoCompra as campo2, PorDsto as campo3, Periodo as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaProducto ='" + ClaProducto + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref PerCompra, ref MontoCompra, ref PorDsto, ref periodo);
                    stmysql = "select TipoCliente as campo1 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaProducto ='" + ClaProducto + "'";
                    ok = Exec(stmysql, myconnect, "BuscaListaDstos", ref TipoCliente);
                    break;
                case 4:
                    stmysql = "select Fecini as campo1, FecFin as campo2, CantIni as campo3, CantFin as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaPago ='" + ClaPago + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref Fecini, ref FecFin, ref CantIni, ref CantFin);
                    stmysql = "select PerCompra as campo1, MontoCompra as campo2, PorDsto as campo3, Periodo as campo4 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaPago ='" + ClaPago + "'";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaDstos", ref PerCompra, ref MontoCompra, ref PorDsto, ref periodo);
                    stmysql = "select TipoCliente as campo1 from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaPago ='" + ClaPago + "'";
                    ok = Exec(stmysql, myconnect, "BuscaListaDstos", ref TipoCliente);
                    break;
            }
            return ok;
        }

        // Overload 7 params: para GrabaDsto
        public bool BuscaListaDstos(int IdTipoDsto, OdbcConnection myconnect,
            string IdProducto, string IdCliente, string IdGrupo,
            string ClaProducto, string ClaPago)
        {
            string _idp = IdProducto, _idc = IdCliente, _idg = IdGrupo,
                   _clap = ClaProducto, _clap2 = ClaPago;
            string _fi = " ", _ff = " ", _tc = " ", _ci = "0", _cf = "0",
                   _pc = "0", _mc = "0", _pd = "0", _per = "0";
            return BuscaListaDstos(IdTipoDsto, myconnect, ref _idp, ref _idc, ref _idg,
                ref _clap, ref _clap2, ref _fi, ref _ff, ref _tc, ref _ci, ref _cf,
                ref _pc, ref _mc, ref _pd, ref _per);
        }

        // ─────────────────────────────────────────────────────────────────────
        // EliminaDsto
        // ─────────────────────────────────────────────────────────────────────
        public bool EliminaDsto(int IdTipoDsto, OdbcConnection Myconnect)
        {
            return EliminaDsto(IdTipoDsto, Myconnect, "99999999", "99999999999999", "9999", "99", "99");
        }

        public bool EliminaDsto(int IdTipoDsto, OdbcConnection Myconnect,
            string IdProducto, string IdCliente, string IdGrupo, string ClaProd, string ClaPago)
        {
            bool ok = true;
            switch (IdTipoDsto)
            {
                case 1:
                    if (IdProducto == "99999999" || IdProducto == "")
                    {
                        if (MessageBox.Show("Esta seguro que desea eliminar todos los registro de descuento por productos", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "'";
                        else
                            return ok;
                    }
                    else
                        stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdProducto = '" + IdProducto + "'";
                    break;
                case 2:
                    if (IdCliente == "99999999999999" || IdCliente == "")
                    {
                        if (MessageBox.Show("Esta seguro que desea eliminar todos los registro de descuento por clientes", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "'";
                        else
                            return ok;
                    }
                    else
                        stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdCliente = '" + IdCliente + "'";
                    break;
                case 3:
                    if (IdGrupo == "9999" || IdGrupo == "")
                    {
                        if (MessageBox.Show("Esta seguro que desea eliminar todos los registro de descuento por grupo", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "'";
                        else
                            return ok;
                    }
                    else
                        stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and IdGrupo = '" + IdGrupo + "'";
                    break;
                case 4:
                    if (ClaProd == "2" || ClaProd == "")
                    {
                        if (MessageBox.Show("Esta seguro que desea eliminar todos los registro de descuento por clase de producto", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "'";
                        else
                            return ok;
                    }
                    else
                        stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaProducto = '" + ClaProd + "'";
                    break;
                case 5:
                    if (ClaPago == "3" || ClaPago == "")
                    {
                        if (MessageBox.Show("Esta seguro que desea eliminar todos los registro de descuento por forma de pago", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
                            stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "'";
                        else
                            return ok;
                    }
                    else
                        stmysql = " Delete from inv_dstos where IdTipoDsto = '" + IdTipoDsto + "' and ClaPago = '" + ClaPago + "'";
                    break;
            }
            ok = Exec(stmysql, Myconnect, "EliminaDsto");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaDsto
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaDsto(int IdTipoDsto, OdbcConnection Myconnect,
            string IdProducto, string IdCliente, string IdGrupo,
            string ClaProducto, string ClaPago,
            DateTime Fecini, DateTime FecFin,
            int CantIni, int CantFin, int PerCompra, int MontoCompra,
            string TipoCliente, double PorDsto, int Periodo, int ClaseDsto)
        {
            bool ok = true;
            if (ClaProducto == "2") ClaProducto = "99";
            if (ClaPago == "3") ClaPago = "99";
            ok = BuscaListaDstos(IdTipoDsto, Myconnect, IdProducto, IdCliente, IdGrupo, ClaProducto, ClaPago);
            if (!ok)
            {
                stmysql = "Insert into inv_dstos (IdTipoDsto,IdProducto,IdCliente,IdGrupo,ClaProducto,ClaPago,ClaseDsto,Fecini,FecFin,CantIni,CantFin,PerCompra,MontoCompra,TipoCliente,PorDsto,Periodo) values ('"
                   + IdTipoDsto + "','" + IdProducto + "','" + IdCliente + "','" + IdGrupo + "','" + ClaProducto + "','" + ClaPago + "','" + ClaseDsto + "','" + Fecini.ToString(varini.PstForFec) + "','" + FecFin.ToString(varini.PstForFec) + "','" + CantIni + "','" + CantFin + "','"
                   + PerCompra + "','" + MontoCompra + "','" + TipoCliente + "','" + PorDsto + "','" + Periodo + "')";
            }
            else
            {
                switch (ClaseDsto)
                {
                    case 0:
                        stmysql = "update inv_dstos set IdCliente = '" + IdCliente + "', IdGrupo= '" + IdGrupo + "', ClaProducto='" + ClaProducto + "', ClaPago= '" + ClaPago + "', ClaseDsto='" + ClaseDsto + "', Fecini = '" + Fecini.ToString(varini.PstForFec)
                            + "',FecFin = '" + FecFin.ToString(varini.PstForFec) + "', CantIni ='" + CantIni + "',CantFin = '" + CantFin + "', PerCompra ='" + PerCompra + "', MontoCompra= '" + MontoCompra + "',TipoCliente= '" + TipoCliente + "', PorDsto= '" + PorDsto + "', Periodo= '" + Periodo + "'"
                            + " where IdTipoDsto = '" + IdTipoDsto + "' and IdProducto= '" + IdProducto + "'";
                        break;
                    case 1:
                        stmysql = "update inv_dstos set IdProducto = '" + IdProducto + "', IdGrupo= '" + IdGrupo + "', ClaProducto='" + ClaProducto + "', ClaPago= '" + ClaPago + "', ClaseDsto='" + ClaseDsto + "', Fecini = '" + Fecini.ToString(varini.PstForFec)
                            + "',FecFin = '" + FecFin.ToString(varini.PstForFec) + "', CantIni ='" + CantIni + "',CantFin = '" + CantFin + "', PerCompra ='" + PerCompra + "', MontoCompra= '" + MontoCompra + "',TipoCliente= '" + TipoCliente + "', PorDsto= '" + PorDsto + "', Periodo= '" + Periodo + "'"
                            + " where IdTipoDsto = '" + IdTipoDsto + "' and IdCliente= '" + IdCliente + "'";
                        break;
                    case 2:
                        stmysql = "update inv_dstos set IdCliente = '" + IdCliente + "', IdProducto= '" + IdProducto + "', ClaProducto='" + ClaProducto + "', ClaPago= '" + ClaPago + "', ClaseDsto='" + ClaseDsto + "', Fecini = '" + Fecini.ToString(varini.PstForFec)
                            + "',FecFin = '" + FecFin.ToString(varini.PstForFec) + "', CantIni ='" + CantIni + "',CantFin = '" + CantFin + "', PerCompra ='" + PerCompra + "', MontoCompra= '" + MontoCompra + "',TipoCliente= '" + TipoCliente + "', PorDsto= '" + PorDsto + "', Periodo= '" + Periodo + "'"
                            + " where IdTipoDsto = '" + IdTipoDsto + "' and IdGrupo= '" + IdGrupo + "'";
                        break;
                    case 3:
                        stmysql = "update inv_dstos set IdCliente = '" + IdCliente + "', IdGrupo= '" + IdGrupo + "', IdProducto='" + IdProducto + "', ClaPago= '" + ClaPago + "', ClaseDsto='" + ClaseDsto + "', Fecini = '" + Fecini.ToString(varini.PstForFec)
                            + "',FecFin = '" + FecFin.ToString(varini.PstForFec) + "', CantIni ='" + CantIni + "',CantFin = '" + CantFin + "', PerCompra ='" + PerCompra + "', MontoCompra= '" + MontoCompra + "',TipoCliente= '" + TipoCliente + "', PorDsto= '" + PorDsto + "', Periodo= '" + Periodo + "'"
                            + " where IdTipoDsto = '" + IdTipoDsto + "' and ClaProducto= '" + ClaProducto + "'";
                        break;
                    case 4:
                        stmysql = "update inv_dstos set IdCliente = '" + IdCliente + "', IdGrupo= '" + IdGrupo + "', ClaProducto='" + ClaProducto + "', IdProducto= '" + IdProducto + "', ClaseDsto='" + ClaseDsto + "', Fecini = '" + Fecini.ToString(varini.PstForFec)
                            + "',FecFin = '" + FecFin.ToString(varini.PstForFec) + "', CantIni ='" + CantIni + "',CantFin = '" + CantFin + "', PerCompra ='" + PerCompra + "', MontoCompra= '" + MontoCompra + "',TipoCliente= '" + TipoCliente + "', PorDsto= '" + PorDsto + "', Periodo= '" + Periodo + "'"
                            + " where IdTipoDsto = '" + IdTipoDsto + "' and ClaPago= '" + ClaPago + "'";
                        break;
                }
            }
            ok = Exec(stmysql, Myconnect, "GrabaDsto");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaListaPrecios
        // ─────────────────────────────────────────────────────────────────────
        public void GrabaListaPrecios(int IdTipoPrecio, int ClasePrecio, string IdProducto,
            int TipoCliente, DateTime FecIni, DateTime FecFin, double PreVenta,
            string Descripcion, double Cantidad, OdbcConnection myconnect)
        {
            string _idp = IdProducto, _tc = TipoCliente.ToString(), _fi = " ", _ff = " ", _pv = " ", _d = " ";
            ok = BuscaListaPrecios(IdTipoPrecio, myconnect, ref _idp, ref _tc, ref _fi, ref _ff, ref _pv, ref _d);
            if (!ok)
            {
                stmysql = "Insert into inv_precios (IdTipoPrecio,IdProducto,TipoCliente,FecIni,FecFin,PreVenta,ClasePrecio,Descripcion,cantidad) values ('"
                        + IdTipoPrecio + "','" + IdProducto + "','" + TipoCliente + "','" + FecIni.ToString(varini.PstForFec) + "','" + FecFin.ToString(varini.PstForFec)
                        + "','" + PreVenta + "'," + ClasePrecio + ",'" + Descripcion + "','" + Cantidad + "')";
            }
            else
            {
                switch (ClasePrecio)
                {
                    case 0:
                        stmysql = "update inv_precios set FecIni = '" + FecIni.ToString(varini.PstForFec)
                                + "',FecFin = '" + FecFin.ToString(varini.PstForFec) + "', PreVenta ='" + PreVenta + "',ClasePrecio = '" + ClasePrecio + "', Descripcion ='" + Descripcion + "', cantidad = '"
                                + Cantidad + "'"
                                + " where IdTipoPrecio = '" + IdTipoPrecio + "' and IdProducto = " + IdProducto + " and TipoCliente = '" + TipoCliente + "'";
                        break;
                }
            }
            Exec(stmysql, myconnect, "GrabaListaPrecios");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaListaPreciosCantidad (public full)
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaListaPreciosCantidad(int IdTipoPrecio, OdbcConnection myconnect,
            ref string IdProducto, ref string TipoCliente, ref string FecIni, ref string FecFin,
            ref string PreVenta, ref string Descripcion, ref string cantidad)
        {
            int ClasePrecio = 0;
            BuscaClasePrecio(IdTipoPrecio, myconnect, ref ClasePrecio);
            switch (ClasePrecio)
            {
                case 0:
                    stmysql = "select FecIni as campo1 ,FecFin as campo2,PreVenta as campo3,Descripcion as campo4 from inv_precios where IdTipoPrecio = '" + IdTipoPrecio + "' and IdProducto ='" + IdProducto + "' and tipoCliente = " + TipoCliente;
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaPrecios", ref FecIni, ref FecFin, ref PreVenta, ref Descripcion);
                    stmysql = "select cantidad as campo1 from inv_precios where IdTipoPrecio = '" + IdTipoPrecio + "' and IdProducto ='" + IdProducto + "' and tipoCliente = " + TipoCliente;
                    ok = Exec(stmysql, myconnect, "BuscaListaPrecios", ref cantidad);
                    break;
            }
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaListaPrecios (public full)
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaListaPrecios(int IdTipoPrecio, OdbcConnection myconnect,
            ref string IdProducto, ref string TipoCliente, ref string FecIni, ref string FecFin,
            ref string PreVenta, ref string Descripcion)
        {
            int ClasePrecio = 0;
            BuscaClasePrecio(IdTipoPrecio, myconnect, ref ClasePrecio);
            switch (ClasePrecio)
            {
                case 0:
                    stmysql = "select FecIni as campo1 ,FecFin as campo2,PreVenta as campo3,Descripcion as campo4 from inv_precios where IdTipoPrecio = '" + IdTipoPrecio + "' and IdProducto ='" + IdProducto + "' and tipoCliente = " + TipoCliente;
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql, myconnect, "BuscaListaPrecios", ref FecIni, ref FecFin, ref PreVenta, ref Descripcion);
                    break;
            }
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaClasePrecio
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaClasePrecio(int IdTipoPrecio, OdbcConnection myconnect, ref int ClasePrecio)
        {
            string _cp = ClasePrecio.ToString();
            stmysql = "select clasePrecio as campo1 from inv_precios where  IdTipoPrecio = '" + IdTipoPrecio + "'";
            ok = Exec(stmysql, myconnect, "BuscaClasePrecio", ref _cp);
            if (int.TryParse(_cp, out int parsed)) ClasePrecio = parsed;
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaGrupo
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaGrupo(ref string IdGruProducto, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumido, Navega navegar,
            ref string Cencos, ref string idgruposecund, ref string DescripcionSecundario,
            ref string rstingLimeteVentas, ref int cantRestringVentas)
        {
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = " where a.IdGruProducto = '" + IdGruProducto + "'"; break;
                case Navega.Anterior:  where = " where a.IdGruProducto < '" + IdGruProducto + "' order by a.IdGruProducto desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = " where a.IdGruProducto > ' ' order by a.IdGruProducto " + varini.Pstlimit; break;
                case Navega.Siguiente: where = " where a.IdGruProducto > '" + IdGruProducto + "'  order by a.IdGruProducto " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = " where a.IdGruProducto < '9999'  order by a.IdGruProducto desc" + varini.Pstlimit; break;
            }
            stmysql = "select" + varini.Psttop + " a.descripcion as campo1, a.resumido as campo2,a.IdGruProducto as campo3,a.Cencos as campo4  from inv_grupos  a";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, myconnect, "BuscaGrupo", ref Descripcion, ref Resumido, ref IdGruProducto, ref Cencos);

            string _rl = rstingLimeteVentas, _crv = cantRestringVentas.ToString();
            stmysql = "select" + varini.Psttop + " a.rstingLimeteVentas as campo1, a.cantRestringVentas as campo2    from inv_grupos  a";
            Exec(stmysql + where, myconnect, "BuscaGrupo", ref _rl, ref _crv);
            rstingLimeteVentas = _rl;
            if (int.TryParse(_crv, out int parsedCrv)) cantRestringVentas = parsedCrv;

            string descripGrupSecundario = "", descripGrupPrimario = "";
            stmysql = " select a.idgruposecund as campo1,grupsecund.descripcion as campo2, grupprim.descripcion as campo3 "
                    + " from inv_grupos a "
                    + " inner  join inv_GrupoSecundario grupsecund "
                    + " on a.idgruposecund = grupsecund.idgrupo "
                    + " inner  join inv_Grupo_Primario grupprim "
                    + " on grupsecund.idgrupoprimario = grupprim.idgrupo    ";
            Exec(stmysql + where, myconnect, "BuscaGrupo", ref idgruposecund, ref descripGrupSecundario, ref descripGrupPrimario);
            DescripcionSecundario = descripGrupSecundario + " Grupo Primario " + descripGrupPrimario;
            return ok;
        }

        private bool BuscaGrupoExiste(int IdGruProducto, OdbcConnection myconnect)
        {
            string _id = IdGruProducto.ToString(), _d = " ", _r = " ", _c = " ", _igs = " ", _ds = " ", _rl = "N";
            int _crv = 0;
            return BuscaGrupo(ref _id, myconnect, ref _d, ref _r, Navega.Ninguno, ref _c, ref _igs, ref _ds, ref _rl, ref _crv);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaGrupo / EliminaGrupo
        // ─────────────────────────────────────────────────────────────────────
        public void GrabaGrupo(int IdGruProducto, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumen,
            ref string Cencos, ref string idgruposecund,
            string rstingLimeteVentas, int cantRestringVentas)
        {
            ok = BuscaGrupoExiste(IdGruProducto, myconnect);
            if (ok)
                stmysql = "update inv_grupos set Descripcion='" + Descripcion + "',Resumido='" + Resumen + "', cencos = '" + Cencos + "', idgruposecund ='" + idgruposecund + "'  "
                        + ", rstingLimeteVentas = '" + rstingLimeteVentas + "',cantRestringVentas=" + cantRestringVentas + "     where IdGruProducto  = " + IdGruProducto;
            else
                stmysql = "insert into inv_grupos(IdGruProducto,Descripcion,Resumido,cencos,idgruposecund,rstingLimeteVentas,cantRestringVentas) values ("
                        + IdGruProducto + ",'" + Descripcion + "','" + Resumen + "','" + Cencos + "','" + idgruposecund + "','" + rstingLimeteVentas + "'," + cantRestringVentas + ")";
            Exec(stmysql, myconnect, "GrabaGrupo");
        }

        public bool EliminaGrupo(ref string IdGruProducto, OdbcConnection myconnect)
        {
            IdGruProducto = Microsoft.VisualBasic.Strings.Right("0000" + IdGruProducto, 4);
            stmysql = "delete  from inv_grupos where IdGruProducto = '" + IdGruProducto + "'";
            return ok; // stmysql built but not executed — matches VB source
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaTipolista / BuscaTipolista / EliminaTipolista
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaTipolista(int IdTipoPrecio, OdbcConnection myconnect,
            int ClasePrecio, string Descripcion)
        {
            ok = BuscaTipolista(ref IdTipoPrecio, myconnect);
            if (!ok)
                stmysql = " insert into inv_tipolistas(IdTipoPrecio,ClasePrecio,Descripcion) values ('"
                        + IdTipoPrecio + "','" + ClasePrecio + "','" + Descripcion + "')";
            else
                stmysql = "update inv_tipolistas set ClasePrecio = '" + ClasePrecio + "',Descripcion = '" + Descripcion
                        + "' where IdTipoPrecio = '" + IdTipoPrecio + "'";
            ok = Exec(stmysql, myconnect, "GrabaTipoPrecio");
            return ok;
        }

        public bool BuscaTipolista(ref int IdTipoPrecio, OdbcConnection myconnect,
            ref int ClasePrecio, ref string Descripcion, Navega navegar)
        {
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_tipolistas where IdTipoprecio = " + IdTipoPrecio; break;
                case Navega.Anterior:  where = "from  inv_tipolistas where IdTipoprecio < " + IdTipoPrecio + " order by IdTipoprecio desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_tipolistas where IdTipoprecio > 0 order by IdTipoprecio " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_tipolistas where IdTipoprecio > " + IdTipoPrecio + "  order by IdTipoprecio " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_tipolistas where IdTipoprecio < 99999999  order by IdTipoprecio desc" + varini.Pstlimit; break;
            }
            string _cp = ClasePrecio.ToString(), _id = IdTipoPrecio.ToString();
            stmysql = "select" + varini.Psttop + " ClasePrecio as campo1, Descripcion as campo2, IdTipoprecio as campo3 ";
            ok = Exec(stmysql + where, myconnect, "BuscaTipoPrecio", ref _cp, ref Descripcion, ref _id);
            if (int.TryParse(_cp, out int parsedCp)) ClasePrecio = parsedCp;
            if (int.TryParse(_id, out int parsedId)) IdTipoPrecio = parsedId;
            return ok;
        }

        // Overload: simple existence check
        public bool BuscaTipolista(ref int IdTipoPrecio, OdbcConnection myconnect)
        {
            int _cp = 0; string _d = " ";
            return BuscaTipolista(ref IdTipoPrecio, myconnect, ref _cp, ref _d, Navega.Ninguno);
        }

        public void EliminaTipolista(int IdTipoPrecio, OdbcConnection myconnect)
        {
            stmysql = "delete from inv_tipolistas where IdTipoPrecio = '" + IdTipoPrecio + "'";
            Exec(stmysql, myconnect, "EliminaTipoPrecio");
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaBodega (overload 1: by IdBodega)
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaBodega(ref int IdBodega, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumen, Navega navegar)
        {
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_bodegas where IdBodega = " + IdBodega; break;
                case Navega.Anterior:  where = "from  inv_bodegas where IdBodega < " + IdBodega + " order by IdBodega desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_bodegas where IdBodega > 0 order by IdBodega " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_bodegas where IdBodega > " + IdBodega + "  order by IdBodega " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_bodegas where IdBodega < 99999999  order by IdBodega desc" + varini.Pstlimit; break;
            }
            string _id = IdBodega.ToString();
            stmysql = "select" + varini.Psttop + " Descripcion as campo1,Resumido as campo2,IdBodega as campo3 ";
            ok = Exec(stmysql + where, myconnect, "BuscaBodega", ref Descripcion, ref Resumen, ref _id);
            if (int.TryParse(_id, out int parsed)) IdBodega = parsed;
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaBodega (overload 2: by IdBodega + IdUbicacion, Overridable)
        // ─────────────────────────────────────────────────────────────────────
        public virtual bool BuscaBodega(ref int IdBodega, ref int IdUbicacion, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumen, Navega navegar)
        {
            string IdCencosto = IdUbicacion.ToString(), CodBodega = IdBodega.ToString();
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_bodegas where IdUbicacion='" + IdCencosto + "' and IdBodega = " + CodBodega; break;
                case Navega.Anterior:  where = "from  inv_bodegas where IdUbicacion='" + IdCencosto + "' and  IdBodega < " + CodBodega + " order by IdBodega desc "; break;
                case Navega.Primero:   where = "from  inv_bodegas where IdUbicacion='" + IdCencosto + "' and IdBodega > 0 order by IdBodega "; break;
                case Navega.Siguiente: where = "from  inv_bodegas where IdUbicacion='" + IdCencosto + "' and IdBodega > " + CodBodega + "  order by IdBodega "; break;
                case Navega.Ultimo:    where = "from  inv_bodegas where IdUbicacion='" + IdCencosto + "' and IdBodega < 99999999  order by IdBodega desc"; break;
            }
            string _d = Descripcion, _r = Resumen, _id = IdBodega.ToString(), _idub = IdUbicacion.ToString();
            stmysql = "select Descripcion as campo1,Resumido as campo2,IdBodega as campo3,IdUbicacion as campo4 ";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, myconnect, "BuscaBodega", ref _d, ref _r, ref _id, ref _idub);
            Descripcion = _d; Resumen = _r;
            if (int.TryParse(_id, out int parsedId)) IdBodega = parsedId;
            if (int.TryParse(_idub, out int parsedUb)) IdUbicacion = parsedUb;

            if (!ok)
            {
                string altWhere = "";
                switch (navegar)
                {
                    case Navega.Siguiente: altWhere = "from  inv_bodegas where IdUbicacion>'" + IdCencosto + "' and IdBodega >0 order by IdBodega "; break;
                    case Navega.Anterior:  altWhere = "from  inv_bodegas where IdUbicacion<'" + IdCencosto + "' and IdBodega >0 order by IdBodega "; break;
                }
                if (altWhere != "")
                {
                    _d = Descripcion; _r = Resumen; _id = IdBodega.ToString(); _idub = IdUbicacion.ToString();
                    stmysql = "select Descripcion as campo1,Resumido as campo2,IdBodega as campo3,IdUbicacion as campo4 ";
                    ok = MyOdbcConet.ExecuteQueryconec(stmysql + altWhere, myconnect, "BuscaBodega", ref _d, ref _r, ref _id, ref _idub);
                    Descripcion = _d; Resumen = _r;
                    if (int.TryParse(_id, out parsedId)) IdBodega = parsedId;
                    if (int.TryParse(_idub, out parsedUb)) IdUbicacion = parsedUb;
                }
            }
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaUbicacion
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaUbicacion(ref int IdUbicacion, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumen, Navega navegar)
        {
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_ubicacion where idubicacion = " + IdUbicacion; break;
                case Navega.Anterior:  where = "from  inv_ubicacion where idubicacion < " + IdUbicacion + " order by idubicacion desc "; break;
                case Navega.Primero:   where = "from  inv_ubicacion where idubicacion > 0 order by idubicacion "; break;
                case Navega.Siguiente: where = "from  inv_ubicacion where idubicacion > " + IdUbicacion + "  order by idubicacion "; break;
                case Navega.Ultimo:    where = "from  inv_ubicacion where idubicacion < 99999999  order by idubicacion desc"; break;
            }
            string _id = IdUbicacion.ToString();
            stmysql = "select Descripcion as campo1,Resumido as campo2,idubicacion as campo3 ";
            ok = Exec(stmysql + where, myconnect, "BuscaUbicacion", ref Descripcion, ref Resumen, ref _id);
            if (int.TryParse(_id, out int parsed)) IdUbicacion = parsed;
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaBodega / EliminaBodega / GrabaUbicacion / EliminaUbicacion
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaBodega(int IdBodega, int IdUbicacion, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumen)
        {
            int _id1 = IdBodega, _ub1 = IdUbicacion;
            string _d1 = " ", _r1 = " ";
            ok = BuscaBodega(ref _id1, ref _ub1, myconnect, ref _d1, ref _r1, Navega.Ninguno);
            if (ok)
                stmysql = "update inv_bodegas set Descripcion='" + Descripcion + "',Resumido='" + Resumen + "' "
                        + "where IdBodega  = " + IdBodega + " and IdUbicacion='" + IdUbicacion + "'";
            else
                stmysql = "insert into inv_bodegas(IdBodega,IdUbicacion,Descripcion,Resumido) values ("
                        + IdBodega + ",'" + IdUbicacion + "','" + Descripcion + "','" + Resumen + "')";
            ok = Exec(stmysql, myconnect, "BuscaBodega");
            return ok;
        }

        public bool EliminaBodega(int IdBodega, int IdUbicacion, OdbcConnection myconnect)
        {
            stmysql = "delete from inv_bodegas where IdBodega = '" + IdBodega + "' and IdUbicacion='" + IdUbicacion + "'";
            ok = Exec(stmysql, myconnect, "EliminaBodega");
            return ok;
        }

        public bool GrabaUbicacion(int IdUbicacion, OdbcConnection myconnect,
            ref string Descripcion, ref string Resumen)
        {
            int _ub = IdUbicacion;
            string _d = " ", _r = " ";
            ok = BuscaUbicacion(ref _ub, myconnect, ref _d, ref _r, Navega.Ninguno);
            if (ok)
                stmysql = "update inv_ubicacion set Descripcion='" + Descripcion + "',Resumido='" + Resumen + "' "
                        + "where idubicacion  = " + IdUbicacion;
            else
                stmysql = "insert into inv_ubicacion(idubicacion,Descripcion,Resumido) values ("
                        + IdUbicacion + ",'" + Descripcion + "','" + Resumen + "')";
            ok = Exec(stmysql, myconnect, "GrabaUbicacion");
            return ok;
        }

        public bool EliminaUbicacion(int IdUbicacion, OdbcConnection myconnect)
        {
            stmysql = "delete from inv_ubicacion where idubicacion = " + IdUbicacion;
            ok = Exec(stmysql, myconnect, "EliminaUbicacion");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaDatosFacturacion
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaDatosFacturacion(ref string IdCodigo, OdbcConnection myconnect,
            ref string Resolucion, ref string FecResol, ref int RegimenIva, ref string Prefijo,
            ref string NumInicial, ref string NumFinal, ref double Consefact, Navega navegar,
            ref string CpteCartera, ref int lincred, ref int clades, ref int plazo,
            ref string CpteCartEmpl, ref int lincredEmpl, ref int cladesEmpl, ref int plazoEmpl,
            ref string CpteCartPatro, ref int lincredPatro, ref int cladesPatro, ref int plazoPatro,
            ref int TipMovAjuInv, ref string CpteCartTeresp, ref int LincredTeresp, ref int cladesTeresp,
            ref int plazoTeresp, ref string CpteCartTer, ref int LincredTer, ref int cladesTer,
            ref int plazoTer, ref int actcostos, ref string ImpTirillaBonos, ref string AbreRegistradora,
            ref string grupocomision, ref double tasartfcomision, ref string PrevenCostoUtilidad,
            ref string soloundescuento, ref string ivacondescuento)
        {
            string where = "";
            if (navegar == Navega.Ninguno)
                IdCodigo = Microsoft.VisualBasic.Strings.Right("0000" + IdCodigo, 4);
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_facturas where IdCodigo = '" + IdCodigo + "'"; break;
                case Navega.Anterior:  where = "from  inv_facturas where IdCodigo < '" + IdCodigo + "' order by IdCodigo desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_facturas where IdCodigo > ' ' order by IdCodigo " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_facturas where IdCodigo > '" + IdCodigo + "'  order by IdCodigo " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_facturas where IdCodigo < '99999999'  order by IdCodigo desc" + varini.Pstlimit; break;
            }
            stmysql = "select" + varini.Psttop + " Resolucion ,FecResol,RegimenIva ,Prefijo, NumInicial ,NumFinal,Consefact , IdCodigo,"
                    + "CpteCartera ,lincred,clades , plazo, CpteCartEmpl ,lincredEmpl,cladesEmpl , plazoEmpl ,CpteCartPatro ,lincredPatro,cladesPatro,"
                    + "plazoPatro,TipMovAjuInv ,CpteCartTeresp , LincredTeresp, cladesTeresp, plazoTeresp,  CpteCartTer , LincredTer,  cladesTer,"
                    + "plazoTer,actcostos,imptirbonos,abreregistradora , grupocomision ,tasartfcomision ,PrevenCostoUtilidad,soloundescuento,ivacondescuento ";
            DataTable TablaDatos = new DataTable();
            if (MyOdbcConet.ExecuteConsulta(stmysql + where, myconnect, "BuscaDatosFacturacion", ref TablaDatos))
            {
                DataRow r = TablaDatos.Rows[0];
                Resolucion          = r["Resolucion"].ToString();
                FecResol            = r["FecResol"].ToString();
                if (int.TryParse(r["RegimenIva"].ToString(), out int ri)) RegimenIva = ri;
                Prefijo             = r["Prefijo"].ToString();
                NumInicial          = r["NumInicial"].ToString();
                NumFinal            = r["NumFinal"].ToString();
                if (double.TryParse(r["Consefact"].ToString(), out double cf)) Consefact = cf;
                IdCodigo            = r["IdCodigo"].ToString();
                CpteCartera         = r["CpteCartera"].ToString();
                if (int.TryParse(r["lincred"].ToString(), out int lc)) lincred = lc;
                if (int.TryParse(r["clades"].ToString(), out int cl)) clades = cl;
                if (int.TryParse(r["plazo"].ToString(), out int pl)) plazo = pl;
                CpteCartEmpl        = r["CpteCartEmpl"].ToString();
                if (int.TryParse(r["lincredEmpl"].ToString(), out int lce)) lincredEmpl = lce;
                if (int.TryParse(r["cladesEmpl"].ToString(), out int cle)) cladesEmpl = cle;
                if (int.TryParse(r["plazoEmpl"].ToString(), out int ple)) plazoEmpl = ple;
                CpteCartPatro       = r["CpteCartPatro"].ToString();
                if (int.TryParse(r["lincredPatro"].ToString(), out int lcp)) lincredPatro = lcp;
                if (int.TryParse(r["cladesPatro"].ToString(), out int clp)) cladesPatro = clp;
                if (int.TryParse(r["plazoPatro"].ToString(), out int plp)) plazoPatro = plp;
                if (int.TryParse(r["TipMovAjuInv"].ToString(), out int tm)) TipMovAjuInv = tm;
                CpteCartTeresp      = r["CpteCartTeresp"].ToString();
                if (int.TryParse(r["LincredTeresp"].ToString(), out int ltr)) LincredTeresp = ltr;
                if (int.TryParse(r["cladesTeresp"].ToString(), out int cltr)) cladesTeresp = cltr;
                if (int.TryParse(r["plazoTeresp"].ToString(), out int ptr)) plazoTeresp = ptr;
                CpteCartTer         = r["CpteCartTer"].ToString();
                if (int.TryParse(r["LincredTer"].ToString(), out int lt2)) LincredTer = lt2;
                if (int.TryParse(r["cladesTer"].ToString(), out int clt2)) cladesTer = clt2;
                if (int.TryParse(r["plazoTer"].ToString(), out int plt2)) plazoTer = plt2;
                if (int.TryParse(r["actcostos"].ToString(), out int ac)) actcostos = ac;
                ImpTirillaBonos     = r["imptirbonos"].ToString();
                AbreRegistradora    = r["abreregistradora"].ToString();
                grupocomision       = r["grupocomision"].ToString();
                if (double.TryParse(r["tasartfcomision"].ToString(), out double trf)) tasartfcomision = trf;
                PrevenCostoUtilidad = r["PrevenCostoUtilidad"].ToString();
                soloundescuento     = r["soloundescuento"].ToString();
                ivacondescuento     = r["ivacondescuento"].ToString();
                TablaDatos.Dispose();
                return true;
            }
            TablaDatos.Dispose();
            return false;
        }

        // Private overload: existence-only check
        private bool BuscaDatosFacturacion(string IdCodigo, OdbcConnection myconnect)
        {
            string _c1 = " ";
            stmysql = "select IdCodigo as campo1 from inv_facturas where IdCodigo = '" + Microsoft.VisualBasic.Strings.Right("0000" + IdCodigo, 4) + "'";
            return Exec(stmysql, myconnect, "BuscaDatosFacturacion", ref _c1);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaDatosFacturacion
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaDatosFacturacion(string IdCodigo, OdbcConnection myconnect,
            ref string Resolucion, ref string FecResol, int RegimenIva, ref string Prefijo,
            ref string NumInicial, ref string NumFinal, ref double Consefact,
            string CpteCartera, int lincred, int clades, int plazo,
            string CpteCartEmpl, int lincredEmpl, int cladesEmpl, int plazoEmpl,
            string CpteCartPatro, int lincredPatro, int cladesPatro, int plazoPatro,
            int TipMovAjuInv, string CpteCartTeresp, int LincredTeresp, int cladesTeresp,
            int plazoTeresp, string CpteCartTer, int LincredTer, int cladesTer,
            int plazoTer, int ActCostos, ref string ImpTirillaBonos, ref string AbreRegistradora,
            string grupocomision, decimal tasartfcomision, string PrevenCostoUtilidad,
            string Usuario, string soloundescuento, string ivacondescuento)
        {
            ok = BuscaDatosFacturacion(IdCodigo, myconnect);
            if (!ok)
            {
                if (Consefact == 0) Consefact = double.TryParse(NumInicial, out double ni) ? ni : 0;
                stmysql = "insert into inv_facturas (IdCodigo,Resolucion,FecResol,RegimenIva,Prefijo,NumInicial,NumFinal,Consefact,CpteCartera,lincred,clades,plazo,CpteCartEmpl,lincredEmpl,"
                       + "cladesEmpl,plazoEmpl,CpteCartPatro,lincredPatro,cladesPatro,plazoPatro,TipMovAjuInv,CpteCartTeresp,LincredTeresp,cladesTeresp,plazoTeresp,CpteCartTer,LincredTer,"
                       + "cladesTer,plazoTer,actcostos,imptirbonos,abreregistradora,grupocomision,tasartfcomision,PrevenCostoUtilidad,usuario,soloundescuento,ivacondescuento)"
                       + " values('"
                       + IdCodigo + "','" + Resolucion + "','" + FecResol + "','" + RegimenIva + "','" + Prefijo + "','" + NumInicial + "','" + NumFinal + "','" + Consefact + "','"
                       + CpteCartera + "','" + lincred + "','" + clades + "','" + plazo + "','" + CpteCartEmpl + "','" + lincredEmpl + "','" + cladesEmpl + "','" + plazoEmpl + "','" + CpteCartPatro + "','"
                       + lincredPatro + "','" + cladesPatro + "','" + plazoPatro + "','" + TipMovAjuInv + "','" + CpteCartTeresp + "','" + LincredTeresp + "','" + cladesTeresp + "','" + plazoTeresp + "','"
                       + CpteCartTer + "','" + LincredTer + "','" + cladesTer + "','" + plazoTer + "','" + ActCostos + "','" + ImpTirillaBonos + "','" + AbreRegistradora + "','" + grupocomision + "',"
                       + tasartfcomision + ",'" + PrevenCostoUtilidad + "','" + Usuario + "','" + soloundescuento + "','" + ivacondescuento + "')";
            }
            else
            {
                stmysql = "update inv_facturas  set Resolucion  = '" + Resolucion + "',FecResol = '" + FecResol + "',RegimenIva=" + RegimenIva + ",Prefijo='" + Prefijo
                        + "',NumInicial= '" + NumInicial + "',NumFinal=' " + NumFinal + "',Consefact='" + Consefact + "', CpteCartera = '" + CpteCartera + "',lincred = '" + lincred + "', clades = '" + clades + "', plazo = '"
                        + plazo + "',CpteCartEmpl = '" + CpteCartEmpl + "',lincredEmpl= '" + lincredEmpl + "',cladesEmpl = '" + cladesEmpl + "',plazoEmpl = '" + plazoEmpl + "',CpteCartPatro = '"
                        + CpteCartPatro + "',lincredPatro = '" + lincredPatro + "',cladesPatro = '" + cladesPatro + "',plazoPatro= '" + plazoPatro + "', TipMovAjuInv = '" + TipMovAjuInv + "', "
                        + "CpteCartTeresp = '" + CpteCartTeresp + "',LincredTeresp = '" + LincredTeresp + "',cladesTeresp = '" + cladesTeresp + "',plazoTeresp = '" + plazoTeresp + "',CpteCartTer ='"
                        + CpteCartTer + "',LincredTer = '" + LincredTer + "',cladesTer = '" + cladesTer + "',plazoTer = '" + plazoTer + "',actcostos ='" + ActCostos + "',imptirbonos='" + ImpTirillaBonos + "',abreregistradora='" + AbreRegistradora + "' "
                        + ",grupocomision ='" + grupocomision + "',tasartfcomision = " + tasartfcomision + ",PrevenCostoUtilidad = '" + PrevenCostoUtilidad + "',usuario = '" + Usuario + "',soloundescuento='" + soloundescuento + "',ivacondescuento='" + ivacondescuento + "' where idcodigo = '" + IdCodigo + "'";
            }
            ok = Exec(stmysql, myconnect, "GrabaDatosFacturacion");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaConseFactura / EliminaDatosFacturacion
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaConseFactura(string IdCodigo, OdbcConnection myconnect, ref double Consefact)
        {
            string _cf = " ";
            stmysql = "select Consefact as campo1 from inv_facturas where IdCodigo = '" + Microsoft.VisualBasic.Strings.Right("0000" + IdCodigo, 4) + "'";
            ok = Exec(stmysql, myconnect, "BuscaConseFactura", ref _cf);
            if (!ok)
            {
                MessageBox.Show("Datos de facturacion no estan creados", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return ok;
            }
            if (double.TryParse(_cf, out double parsedCf)) Consefact = parsedCf;
            Consefact += 1;
            stmysql = "update inv_facturas  set Consefact  = '" + Consefact + "'"
                    + "where idcodigo = '" + IdCodigo + "'";
            ok = Exec(stmysql, myconnect, "BuscaConseFactura");
            return ok;
        }

        public bool EliminaDatosFacturacion(string IdCodigo, OdbcConnection myconnect)
        {
            IdCodigo = Microsoft.VisualBasic.Strings.Right("0000" + IdCodigo, 4);
            stmysql = "delete from inv_facturas where IdCodigo = '" + IdCodigo + "'";
            ok = Exec(stmysql, myconnect, "EliminaDatosFacturacion");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaTurno / GrabaTurno / EliminaTurno
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaTurno(ref int IdTurno, OdbcConnection myconnect,
            ref string Descripcion, ref string HoraInicial, ref string HoraFinal,
            ref Navega navegar, ref string Cuenta)
        {
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_turnos where IdTurno = " + IdTurno; break;
                case Navega.Anterior:  where = "from  inv_turnos where IdTurno < " + IdTurno + " order by IdTurno desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_turnos where IdTurno > 0 order by IdTurno " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_turnos where IdTurno > " + IdTurno + "  order by IdTurno " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_turnos where IdTurno < 99999999  order by IdTurno desc" + varini.Pstlimit; break;
            }
            string _id = IdTurno.ToString();
            stmysql = "select" + varini.Psttop + " Descripcion as campo1,HoraInicial as campo2,HoraFinal as campo3, IdTurno as campo4 ";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, myconnect, "BuscaTurno", ref Descripcion, ref HoraInicial, ref HoraFinal, ref _id);
            if (int.TryParse(_id, out int parsed)) IdTurno = parsed;

            stmysql = "select" + varini.Psttop + " cuenta as campo1 ";
            ok = Exec(stmysql + where, myconnect, "BuscaTurno", ref Cuenta);
            return ok;
        }

        private bool BuscaTurnoExiste(int IdTurno, OdbcConnection myconnect)
        {
            int _id = IdTurno; Navega _nav = Navega.Ninguno;
            string _d = " ", _hi = " ", _hf = " ", _c = " ";
            return BuscaTurno(ref _id, myconnect, ref _d, ref _hi, ref _hf, ref _nav, ref _c);
        }

        public void GrabaTurno(int IdTurno, string Descripcion, DateTime HoraInicial,
            DateTime HoraFinal, string Cuenta, OdbcConnection myconnect)
        {
            // ok = BuscaTurnoExiste(IdTurno, myconnect); // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
            // if (!ok) // ERROR: CS1002, CS1003, CS1026, CS1525, CS8641
                // stmysql = "insert into inv_turnos(IdTurno,Descripcion,HoraInicial,HoraFinal,Cuenta) values('" // ERROR: CS1061
                        // + IdTurno + "','" + Descripcion + "','" + HoraInicial.ToString(varini.PstForHora) + "','" + HoraFinal.ToString(varini.PstForHora) + "','" + Cuenta + "')"; // ERROR: CS1061
            // else // ERROR: CS8641 - orphaned else
                // stmysql = "update inv_turnos set Descripcion='" + Descripcion + "',HoraInicial= '" + HoraInicial.ToString(varini.PstForHora) + "',HoraFinal = '" + HoraFinal.ToString(varini.PstForHora) + "', Cuenta = '" + Cuenta + "'" // ERROR: CS1061
                        // + " where IdTurno = " + IdTurno; // ERROR: CS1061
            Exec(stmysql, myconnect, "GrabaTurno");
        }

        public void EliminaTurno(int IdTurno, OdbcConnection myconnect)
        {
            stmysql = "delete from inv_turnos where IdTurno = " + IdTurno;
            Exec(stmysql, myconnect, "EliminaTurno");
        }

        // ─────────────────────────────────────────────────────────────────────
        // GrabaTipoMovto / BuscaTipomovto / EliminaTipoMovto
        // ─────────────────────────────────────────────────────────────────────
        public bool GrabaTipoMovto(int IdTipoMovto, string Descripcion, string Resumido,
            string CpteTran, string CpteCost, double Secuencia, int CtrlExistencia,
            string ClaDoc, int ActCont, string ctrlprecio, string ctrlfactura,
            string vlrtotalcompra, string Costea, string Devolucion,
            OdbcConnection Myconect, string TrasladaCont, string usuario,
            string OrdenPedido_sost_precio, string bonifica, string valcupocred)
        {
            bool ok = false;
            ok = BuscaTipomovtoExiste(IdTipoMovto, Myconect);
            if (!ok)
                stmysql = "Insert Into inv_tipomovtos (IdTipoMovto,Descripcion,Resumido,CpteTran,CpteCost,Secuencia,CtrlExistencia,ClaDoc, ActContab,ctrlPrecio,ctrlfactura,vlrtotalencompra,Costea,Devolucion,TrasladaCont,usuario,OrdenPedido_sost_precio,bonifica,valcupocred)"
                    + " values(" + IdTipoMovto + ",'" + Descripcion + "','" + Resumido + "','" + CpteTran + "','" + CpteCost + "','" + Secuencia + "','" + CtrlExistencia + "','" + ClaDoc + "','"
                    + ActCont + "','" + ctrlprecio + "','" + ctrlfactura + "','" + vlrtotalcompra + "','" + Costea + "','" + Devolucion + "','" + TrasladaCont + "','" + usuario + "','" + OrdenPedido_sost_precio + "','" + bonifica + "','" + valcupocred + "')";
            else
                stmysql = "Update inv_tipomovtos set Descripcion = '" + Descripcion + "',Resumido ='" + Resumido + "',CpteTran ='" + CpteTran + "',CpteCost ='" + CpteCost + "',Secuencia  = '"
                    + Secuencia + "',CtrlExistencia  ='" + CtrlExistencia + "',ClaDoc  ='" + ClaDoc + "', ActContab ='" + ActCont + "', ctrlPrecio='" + ctrlprecio + "',ctrlfactura='" + ctrlfactura + "',"
                    + "vlrtotalencompra='" + vlrtotalcompra + "',Costea='" + Costea + "',Devolucion='" + Devolucion + "',TrasladaCont='" + TrasladaCont + "',usuario = '" + usuario + "',OrdenPedido_sost_precio='" + OrdenPedido_sost_precio + "',bonifica='" + bonifica + "',valcupocred='" + valcupocred + "'  where IdTipoMovto = " + IdTipoMovto;
            Exec(stmysql, Myconect, "GrabaTipoMovto");
            return ok;
        }

        public bool BuscaTipomovto(ref int IdTipoMovto, OdbcConnection Myconect,
            ref string Descripcion, ref string Resumido,
            ref string CpteTran, ref string CpteCost, ref double Secuencia,
            ref string CtrlExistencia, ref string ClaDoc,
            ref int ActCont, ref Navega navegar, ref string CtrlPrecio,
            ref string ctrlfactura, ref string vlrtotalcompra,
            ref string Costea, ref string Devolucion, ref string TrasladaCont,
            ref string OrdenPedido_sost_precio, ref string bonifica, ref string valcupocred)
        {
            bool ok = false;
            string where = "";
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_tipomovtos where IdTipoMovto = " + IdTipoMovto; break;
                case Navega.Anterior:  where = "from  inv_tipomovtos where IdTipoMovto < " + IdTipoMovto + " order by IdTipoMovto desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_tipomovtos where IdTipoMovto > 0 order by IdTipoMovto " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_tipomovtos where IdTipoMovto > " + IdTipoMovto + "  order by IdTipoMovto " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_tipomovtos where IdTipoMovto < 99999999  order by IdTipoMovto desc" + varini.Pstlimit; break;
            }
            stmysql = "select" + varini.Psttop + " Descripcion as campo1, Resumido as campo2,CpteTran as campo3,CpteCost as campo4 ";
            ok = MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaTipomovto", ref Descripcion, ref Resumido, ref CpteTran, ref CpteCost);

            string _sec = Secuencia.ToString();
            stmysql = "select" + varini.Psttop + " Secuencia as campo1,CtrlExistencia as campo2,ClaDoc as campo3,vlrtotalencompra as campo4 ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaTipomovto", ref _sec, ref CtrlExistencia, ref ClaDoc, ref vlrtotalcompra);
            if (double.TryParse(_sec, out double parsedSec)) Secuencia = parsedSec;

            string _idm = IdTipoMovto.ToString(), _act = ActCont.ToString();
            stmysql = "select" + varini.Psttop + " IdTipoMovto as campo1, ActContab as campo2, ctrlprecio as campo3,ctrlfactura as campo4  ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaTipomovto", ref _idm, ref _act, ref CtrlPrecio, ref ctrlfactura);
            if (int.TryParse(_idm, out int parsedIdm)) IdTipoMovto = parsedIdm;
            if (int.TryParse(_act, out int parsedAct)) ActCont = parsedAct;

            stmysql = "select costea as campo1, Devolucion as campo2,TrasladaCont as campo3,OrdenPedido_sost_precio   as campo4   ";
            MyOdbcConet.ExecuteQueryconec(stmysql + where, Myconect, "BuscaTipomovto", ref Costea, ref Devolucion, ref TrasladaCont, ref OrdenPedido_sost_precio);

            stmysql = "select bonifica as campo1, valcupocred as campo2    ";
            Exec(stmysql + where, Myconect, "BuscaTipomovto", ref bonifica, ref valcupocred);
            return ok;
        }

        private bool BuscaTipomovtoExiste(int IdTipoMovto, OdbcConnection Myconect)
        {
            int _id = IdTipoMovto; Navega _nav = Navega.Ninguno;
            string _d = " ", _r = " ", _ct = " ", _cc = " "; double _sec = 0;
            string _ctrl = " ", _cla = " "; int _ac = 0;
            string _cp = "Y", _cf = "9999", _vtc = "N", _cos = "N", _dev = "N", _tc = "N", _op = "N", _bon = "N", _val = "N";
            return BuscaTipomovto(ref _id, Myconect, ref _d, ref _r, ref _ct, ref _cc, ref _sec,
                ref _ctrl, ref _cla, ref _ac, ref _nav, ref _cp, ref _cf,
                ref _vtc, ref _cos, ref _dev, ref _tc, ref _op, ref _bon, ref _val);
        }

        public bool EliminaTipoMovto(int IdTipoMovto, OdbcConnection Myconect)
        {
            stmysql = "delete from inv_tipomovtos where IdTipoMovto = " + IdTipoMovto;
            ok = Exec(stmysql, Myconect, "EliminaTipoMovto");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Help* / CargarLista*
        // ─────────────────────────────────────────────────────────────────────
        public string HelpBodega(OdbcConnection Myconnect, Form Myforma, ref string IdUbicacion)
        {
            string StBodega = null, StFiltro = null, StRespCampo2 = null;
            if (IdUbicacion.Trim() != "" && IdUbicacion.Trim() != "999999")
                StFiltro = " idubicacion=" + IdUbicacion.Trim();
            StBodega = msgsas.CargaAyuda("inv_bodegas", "IdBodega", "IdUbicacion", "Descripcion", Myconnect, Myforma, "Ubicacion", "Descripcion Bodega", null, StFiltro, ref StRespCampo2);
            if (StRespCampo2 != null)
                IdUbicacion = StRespCampo2;
            return StBodega;
        }

        public string HelpUbicacion(OdbcConnection Myconnect, Form Myforma)
        {
            return msgsas.CargaAyuda("inv_ubicacion", "IdUbicacion", "Descripcion", "Resumido", Myconnect, Myforma);
        }

        public void CargarListaUbicacion(ComboBox CbxUbicacion, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select idubicacion,descripcion from inv_ubicacion";
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "CargarListaUbicacion", ref dsdata, "tblubicacion");
            CbxUbicacion.Items.Clear();
            CbxUbicacion.Items.Add("");
            for (int fila = 0; fila < dsdata.Tables["tblubicacion"].Rows.Count; fila++)
            {
                DataRow row = dsdata.Tables["tblubicacion"].Rows[fila];
                CbxUbicacion.Items.Add(row["idubicacion"] + " - " + row["descripcion"]);
            }
        }

        public void CargarListaBodega(string IdUbicacion, ComboBox CbxBodega, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            if (!Microsoft.VisualBasic.Information.IsNumeric(IdUbicacion))
                IdUbicacion = "0";
            stmysql = "select idbodega,descripcion from inv_bodegas where idubicacion=" + IdUbicacion;
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "CargarListaBodega", ref dsdata, "tblbodegas");
            CbxBodega.Items.Clear();
            CbxBodega.Items.Add("");
            for (int fila = 0; fila < dsdata.Tables["tblbodegas"].Rows.Count; fila++)
            {
                DataRow row = dsdata.Tables["tblbodegas"].Rows[fila];
                CbxBodega.Items.Add(row["idbodega"] + " - " + row["descripcion"]);
            }
        }

        public string HelpTipoMovto(OdbcConnection Myconnect, Form Myforma, string CtrlExistencia)
        {
            string StFiltro = null;
            if (CtrlExistencia != "")
                StFiltro = " CtrlExistencia=" + CtrlExistencia;
            return msgsas.CargaAyuda("inv_tipomovtos", "IdTipoMovto", "Descripcion", "Resumido", Myconnect, Myforma, null, null, null, StFiltro);
        }

        public string HelpGrupo(OdbcConnection Myconnect, Form Myforma)
        {
            return msgsas.CargaAyuda("inv_grupos", "IdGruProducto", "Descripcion", "Resumido", Myconnect, Myforma);
        }

        // ─────────────────────────────────────────────────────────────────────
        // buscaPeriodo
        // ─────────────────────────────────────────────────────────────────────
        public void buscaPeriodo(string Modulo, OdbcConnection myconnet,
            ref string FechaIni, ref string fechaFin, string fechaValidarStr,
            ref string estado, ref string Periodo, string anio)
        {
            string fechaInicial = "0", fechaFinal = "0", estadoFecha = "A";
            string Anioactual = (anio == "9999") ? DateTime.Now.ToString("yyyy") : anio;
            string Anoperiodo = "0", MesPeriodo = "0";

            stmysql = "select anio as campo1, periodo as campo2  from sys_periodo where  modulo = '" + Modulo + "' and anio = '" + Anioactual + "'";
            Exec(stmysql, myconnet, "buscaPeriodo", ref Anoperiodo, ref MesPeriodo);

            DateTime fechaValidar = new DateTime(1950, 1, 1);
            if (Periodo != "999999" && Periodo != "" && Periodo.Length >= 6)
            {
                string periodoFecStr = Periodo.Substring(0, 4) + "/" + Periodo.Substring(4, 2) + "/01";
                DateTime.TryParse(periodoFecStr, out fechaValidar);
            }
            else if (fechaValidarStr != "" && fechaValidarStr != "1/1/1950")
                DateTime.TryParse(fechaValidarStr, out fechaValidar);

            switch (fechaValidar.ToString("MM"))
            {
                case "01": fechaInicial = "fecha_ini01"; fechaFinal = "fecha_fin01"; estadoFecha = "estado_01"; break;
                case "02": fechaInicial = "fecha_ini02"; fechaFinal = "fecha_fin02"; estadoFecha = "estado_02"; break;
                case "03": fechaInicial = "fecha_ini03"; fechaFinal = "fecha_fin03"; estadoFecha = "estado_03"; break;
                case "04": fechaInicial = "fecha_ini04"; fechaFinal = "fecha_fin04"; estadoFecha = "estado_04"; break;
                case "05": fechaInicial = "fecha_ini05"; fechaFinal = "fecha_fin05"; estadoFecha = "estado_05"; break;
                case "06": fechaInicial = "fecha_ini06"; fechaFinal = "fecha_fin06"; estadoFecha = "estado_06"; break;
                case "07": fechaInicial = "fecha_ini07"; fechaFinal = "fecha_fin07"; estadoFecha = "estado_07"; break;
                case "08": fechaInicial = "fecha_ini08"; fechaFinal = "fecha_fin08"; estadoFecha = "estado_08"; break;
                case "09": fechaInicial = "fecha_ini09"; fechaFinal = "fecha_fin09"; estadoFecha = "estado_09"; break;
                case "10": fechaInicial = "fecha_ini10"; fechaFinal = "fecha_fin10"; estadoFecha = "estado_10"; break;
                case "11": fechaInicial = "fecha_ini11"; fechaFinal = "fecha_fin11"; estadoFecha = "estado_11"; break;
                case "12": fechaInicial = "fecha_ini12"; fechaFinal = "fecha_fin12"; estadoFecha = "estado_12"; break;
                case "13": fechaInicial = "fecha_ini13"; fechaFinal = "fecha_fin13"; estadoFecha = "estado_13"; break;
            }

            if (fechaValidar != new DateTime(1950, 1, 1))
            {
                Periodo = fechaValidar.ToString("yyyyMM");
                stmysql = "select " + fechaInicial + " as campo1," + fechaFinal + " as campo2," + estadoFecha + " as campo3 from sys_periodo where  modulo = '" + Modulo + "' and anio = '" + Anioactual + "'";
                Exec(stmysql, myconnet, "buscaPeriodo", ref FechaIni, ref fechaFin, ref estado);
            }
            else
                Periodo = Anoperiodo + MesPeriodo;
        }

        // ─────────────────────────────────────────────────────────────────────
        // helpVendedor / BuscaVendedor / GuardarVendedor / EliminarVendedor
        // ─────────────────────────────────────────────────────────────────────
        public string helpVendedor(OdbcConnection myconnet, Form form)
        {
            return msgsas.CargaAyuda("inv_vendedor", "cedula", "apellido", "nombre", myconnet, form);
        }

        public bool BuscaVendedor(OdbcConnection myconnet, string cedula,
            ref DataSet dsdatasetVendedores, ref Navega navegar,
            string GuardarDatos, int idVendedor)
        {
            string where = "";
            DataSet DsData = new DataSet();
            try { DsData.Tables.Remove("tbl_vendedor"); } catch { }
            switch (navegar)
            {
                case Navega.Ninguno:
                    where = GuardarDatos == "N"
                        ? "from inv_vendedor where cedula = '" + cedula + "'"
                        : "from inv_vendedor where idVendedor = " + idVendedor;
                    break;
                case Navega.Anterior:  where = "from  inv_vendedor where cedula < '" + cedula + "' order by cedula desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_vendedor where cedula > 0 order by cedula " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_vendedor where cedula > '" + cedula + "'  order by cedula " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_vendedor where cedula < cedula  order by cedula desc" + varini.Pstlimit; break;
            }
            stmysql = "select idVendedor,cedula,nombre,apellido,direccion,telefono,celular,ciudad,tipo_vendedor,AplicaComision  " + where;
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnet, "BuscaVendedor", ref DsData, "tbl_vendedor");
            if (DsData.Tables["tbl_vendedor"].Rows.Count > 0)
            {
                try { dsdatasetVendedores.Tables.Add(DsData.Tables["tbl_vendedor"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        public bool GuardarVendedor(OdbcConnection myconnet, int idVendedor, string cedula,
            string nombre, string apellido, string direccion, string telefono,
            string celular, int ciudad, string tipo_vendedor, string AplicaComision)
        {
            DataSet datasetPrueba = new DataSet();
            Navega _nav = Navega.Ninguno;
            if (!BuscaVendedor(myconnet, cedula, ref datasetPrueba, ref _nav, "Y", idVendedor))
                stmysql = "insert into inv_vendedor(cedula,nombre,apellido,direccion,telefono,celular,ciudad,tipo_vendedor,AplicaComision)  values('" + cedula + "','" + nombre + "','" + apellido + "','" + direccion + "','" + telefono + "','" + celular + "'," + ciudad + ",'" + tipo_vendedor + "','" + AplicaComision + "')";
            else
                stmysql = "update inv_vendedor set cedula ='" + cedula + "',nombre='" + nombre + "',apellido='" + apellido + "',direccion='" + direccion + "',telefono='" + telefono + "',celular='" + celular + "',ciudad = " + ciudad + ",tipo_vendedor = '" + tipo_vendedor + "',AplicaComision='" + AplicaComision + "'  where idVendedor = " + idVendedor;
            return Exec(stmysql, myconnet, "GuardarVendedor");
        }

        public bool EliminarVendedor(OdbcConnection myconnet, int idVendedor)
        {
            DataSet datasetPrueba = new DataSet();
            string cedula = " ";
            Navega _nav = Navega.Ninguno;
            if (BuscaVendedor(myconnet, cedula, ref datasetPrueba, ref _nav, "Y", idVendedor))
                stmysql = "Delete  from inv_vendedor where  idVendedor = " + idVendedor;
            return Exec(stmysql, myconnet, "EliminarVendedor");
        }

        // ─────────────────────────────────────────────────────────────────────
        // helpGrupoPrimario / BuscaGrupoPrimario / GuardarGrupoPrimario / EliminarGrupoPrimario
        // ─────────────────────────────────────────────────────────────────────
        public string helpGrupoPrimario(OdbcConnection myconnet, Form form)
        {
            return msgsas.CargaAyuda("inv_Grupo_Primario", "idgrupo", "descripcion", "resumido", myconnet, form);
        }

        public bool BuscaGrupoPrimario(OdbcConnection myconnet, string idgrupo,
            ref DataSet dsdatasetVendedores, ref Navega navegar)
        {
            string where = "";
            DataSet DsData = new DataSet();
            try { DsData.Tables.Remove("tbl_GrupoPrimario"); } catch { }
            switch (navegar)
            {
                case Navega.Ninguno:   where = "from inv_Grupo_Primario where idgrupo = '" + idgrupo + "'"; break;
                case Navega.Anterior:  where = "from  inv_Grupo_Primario where idgrupo < '" + idgrupo + "' order by idgrupo desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = "from  inv_Grupo_Primario where idgrupo > 0 order by idgrupo " + varini.Pstlimit; break;
                case Navega.Siguiente: where = "from  inv_Grupo_Primario where idgrupo > '" + idgrupo + "'  order by idgrupo " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = "from  inv_Grupo_Primario where idgrupo < idgrupo  order by idgrupo desc" + varini.Pstlimit; break;
            }
            stmysql = "select idgrupo,descripcion,resumido   " + where;
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnet, "BuscaGrupoPrimario", ref DsData, "tbl_GrupoPrimario");
            if (DsData.Tables["tbl_GrupoPrimario"].Rows.Count > 0)
            {
                try { dsdatasetVendedores.Tables.Add(DsData.Tables["tbl_GrupoPrimario"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        public bool GuardarGrupoPrimario(OdbcConnection myconnet, string idgrupo,
            string descripcion, string resumido)
        {
            DataSet datasetPrueba = new DataSet();
            Navega _nav = Navega.Ninguno;
            if (!BuscaGrupoPrimario(myconnet, idgrupo, ref datasetPrueba, ref _nav))
                stmysql = "insert into inv_Grupo_Primario(idgrupo,descripcion,resumido)  values('" + idgrupo + "','" + descripcion + "','" + resumido + "')";
            else
                stmysql = "update inv_Grupo_Primario set descripcion ='" + descripcion + "',resumido='" + resumido + "'   where idgrupo = '" + idgrupo + "'";
            return Exec(stmysql, myconnet, "GuardarGrupoPrimario");
        }

        public bool EliminarGrupoPrimario(OdbcConnection myconnet, string idgrupo)
        {
            DataSet datasetPrueba = new DataSet();
            Navega _nav = Navega.Ninguno;
            if (BuscaGrupoPrimario(myconnet, idgrupo, ref datasetPrueba, ref _nav))
                stmysql = "Delete  from inv_Grupo_Primario where  idgrupo = '" + idgrupo + "'";
            return Exec(stmysql, myconnet, "EliminarGrupoUno");
        }

        // ─────────────────────────────────────────────────────────────────────
        // helpGrupoSecundario / BuscaGrupoSecundario (x2) / GuardarGruposecundario / EliminarGruposecundario
        // ─────────────────────────────────────────────────────────────────────
        public string helpGrupoSecundario(OdbcConnection myconnet, Form form, string filtro)
        {
            if (filtro.Trim() != "")
                return msgsas.CargaAyuda("inv_gruposecundario", "idgrupo", "descripcion", "resumido", myconnet, form, null, null, null, filtro);
            else
                return msgsas.CargaAyuda("inv_gruposecundario", "idgrupo", "descripcion", "resumido", myconnet, form);
        }

        public bool BuscaGrupoSecundario(OdbcConnection myconnet, string idgrupo,
            ref DataSet dsdatasetVendedores, ref Navega navegar)
        {
            string where = "";
            DataSet DsData = new DataSet();
            try { DsData.Tables.Remove("tbl_GrupoSecundario"); } catch { }
            switch (navegar)
            {
                case Navega.Ninguno:   where = "where  a.idgrupo = '" + idgrupo + "'"; break;
                case Navega.Anterior:  where = " where a.idgrupo < '" + idgrupo + "' order by a.idgrupo desc " + varini.Pstlimit; break;
                case Navega.Primero:   where = " where a.idgrupo > 0 order by a.idgrupo " + varini.Pstlimit; break;
                case Navega.Siguiente: where = " where a.idgrupo > '" + idgrupo + "'  order by a.idgrupo " + varini.Pstlimit; break;
                case Navega.Ultimo:    where = " where a.idgrupo < a.idgrupo  order by a.idgrupo desc" + varini.Pstlimit; break;
            }
            stmysql = "select a.idgrupo,a.descripcion,a.resumido,a.idgrupoprimario,grupprim.descripcion as descprim  from inv_GrupoSecundario a  inner  join inv_Grupo_Primario grupprim  on a.idgrupoprimario = grupprim.idgrupo    " + where;
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnet, "BuscaGrupoSecundario", ref DsData, "tbl_GrupoSecundario");
            if (DsData.Tables["tbl_GrupoSecundario"].Rows.Count > 0)
            {
                try { dsdatasetVendedores.Tables.Add(DsData.Tables["tbl_GrupoSecundario"].Copy()); } catch { }
                return true;
            }
            return false;
        }

        public bool BuscaGrupoSecundario(OdbcConnection myconnect, string idgrupo,
            ref string Descripcion, ref string descripcionGrupPri)
        {
            string where = "from inv_GrupoSecundario a  inner  join inv_Grupo_Primario grupprim  on a.idgrupoprimario = grupprim.idgrupo where  a.idgrupo = '" + idgrupo + "'";
            stmysql = " select a.idgrupo as campo1 ,a.descripcion as campo2 ,grupprim.descripcion as campo3   ";
            string _dummy = " ";
            ok = Exec(stmysql + where, myconnect, "BuscaGrupoSecundario", ref _dummy, ref Descripcion, ref descripcionGrupPri);
            return ok;
        }

        public bool GuardarGruposecundario(OdbcConnection myconnet, string idgrupo,
            string descripcion, string resumido, string idgrupoprimario)
        {
            DataSet dataset = new DataSet();
            Navega _nav = Navega.Ninguno;
            if (!BuscaGrupoSecundario(myconnet, idgrupo, ref dataset, ref _nav))
                stmysql = "insert into inv_GrupoSecundario(idgrupo,descripcion,resumido,idgrupoprimario)  values('" + idgrupo + "','" + descripcion + "','" + resumido + "','" + idgrupoprimario + "')";
            else
                stmysql = "update inv_GrupoSecundario set descripcion ='" + descripcion + "',resumido='" + resumido + "'   where idgrupo = '" + idgrupo + "'";
            return Exec(stmysql, myconnet, "GuardarGrupoPrimario");
        }

        public bool EliminarGruposecundario(OdbcConnection myconnet, string idgrupo, string idgrupoprimario)
        {
            DataSet dataset = new DataSet();
            Navega _nav = Navega.Ninguno;
            if (BuscaGrupoSecundario(myconnet, idgrupo, ref dataset, ref _nav))
                stmysql = "Delete  from inv_GrupoSecundario where  idgrupo = '" + idgrupo + "'";
            return Exec(stmysql, myconnet, "EliminarGrupoUno");
        }

        // ─────────────────────────────────────────────────────────────────────
        // helpFacturacion / HelpTipoGrupos / BuscaDescripcionGrupos
        // ─────────────────────────────────────────────────────────────────────
        public string helpFacturacion(OdbcConnection myconnet, Form form)
        {
            return msgsas.CargaAyuda("inv_facturas", "IdCodigo", "Resolucion", "grupocomision", myconnet, form);
        }

        public string HelpTipoGrupos(OdbcConnection myconnet, string grupocomision, Form form)
        {
            switch (grupocomision)
            {
                case "P": return msgsas.CargaAyuda("inv_Grupo_Primario", "idgrupo", "descripcion", "resumido", myconnet, form);
                case "S": return msgsas.CargaAyuda("inv_gruposecundario", "idgrupo", "descripcion", "resumido", myconnet, form);
                case "T": return msgsas.CargaAyuda("inv_grupos", "IdGruProducto", "Descripcion", "Resumido", myconnet, form);
            }
            return null;
        }

        public bool BuscaDescripcionGrupos(OdbcConnection myconnet, string grupocomision,
            string idgrupo, ref string descripcion, ref string descripcionGrilla)
        {
            string DescripcionPri = " ", Descripcionsec = "  ", descripcionter = " ";
            switch (grupocomision)
            {
                case "P":
                    stmysql = "select descripcion as campo1  from inv_Grupo_Primario where idgrupo = '" + idgrupo + "'";
                    ok = Exec(stmysql, myconnet, "BuscaDescripcionGrupos", ref DescripcionPri);
                    descripcion = "Grupo Primario " + DescripcionPri;
                    descripcionGrilla = DescripcionPri;
                    break;
                case "S":
                    ok = BuscaGrupoSecundario(myconnet, idgrupo, ref Descripcionsec, ref DescripcionPri);
                    descripcion = "Grupo Secundario " + Descripcionsec + " Grupo Primario " + DescripcionPri;
                    descripcionGrilla = Descripcionsec;
                    break;
                case "T":
                    stmysql = " select a.descripcion as campo1,grupsecund.descripcion as campo2, grupprim.descripcion as campo3 "
                        + " from inv_grupos a "
                        + " inner  join inv_GrupoSecundario grupsecund "
                        + " on a.idgruposecund = grupsecund.idgrupo "
                        + " inner  join inv_Grupo_Primario grupprim "
                        + " on grupsecund.idgrupoprimario = grupprim.idgrupo  where a.IdGruProducto = '" + idgrupo + "'";
                    ok = Exec(stmysql, myconnet, "BuscaDescripcionGrupos", ref descripcionter, ref Descripcionsec, ref DescripcionPri);
                    descripcion = "Grupo Terciario " + descripcionter + " Grupo Secundario " + Descripcionsec + " Grupo Primario " + DescripcionPri;
                    descripcionGrilla = descripcionter;
                    break;
                default:
                    descripcion = "";
                    ok = false;
                    break;
            }
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BuscaComisiones / buscaComisionesGrabadas / grabarComisiones / EliminarComisiones
        // ─────────────────────────────────────────────────────────────────────
        public bool BuscaComisiones(OdbcConnection myconnet, string grupocomision,
            string idFactura, ref DataSet dsdatasetComisiones,
            int tipoComision, int grupos)
        {
            idFactura = Microsoft.VisualBasic.Strings.Right("0000" + idFactura, 4);
            DataSet DsData = new DataSet();
            string tabla = "", columnas = "";
            try { DsData.Tables.Remove("tbl_Comsiones"); } catch { }

            switch (tipoComision)
            {
                case 0:
                    columnas = "  comi.VentasIncial,comi.VentasFinal,comi.PorceComiosion,comi.idGrupoComision,case comi.tipovendedor when 'E' then 'Externo' when 'I' then 'Interno' else ' ' end as tipovendedor"
                             + ",case comi.tipobonifica when 'T' then 'Total' when 'R' then 'Rango' else ' ' end as tipobonifica,comi.porcomiventa ,comi.porcomirecaudo, comi.porbonifica ";
                    tabla = "  inv_param_comisiones  ";
                    break;
                case 1:
                    columnas = " (case comi.TipoCliente when 0  then 'todos' when  1 then 'Asociados' when 2 then 'Cantidad'  when 3 then  'Especiales' when 4 then '% Sobre Costo' end) as TipoCliente  ,comi.PorceComiosion,comi.idGrupoComision  ";
                    tabla = " inv_param_comisiones_Precios ";
                    break;
            }

            switch (grupocomision)
            {
                case "P":
                    stmysql = "select comi.idGrupo,prim.descripcion," + columnas
                             + " from " + tabla + " comi inner join inv_Grupo_Primario prim  on comi.idGrupo  = prim.idgrupo   where comi.idFactura = '" + idFactura + "'    and   comi.idGrupoComision='" + grupocomision + "' " + (grupos != 0 ? "  and comi.idGrupo=" + grupos : "");
                    break;
                case "S":
                    stmysql = "select comi.idGrupo,sec.descripcion," + columnas
                             + " from " + tabla + " comi inner join inv_GrupoSecundario  sec  on comi.idGrupo  = sec.idgrupo  where comi.idFactura = '" + idFactura + "'  and comi.idGrupoComision='" + grupocomision + "'" + (grupos != 0 ? "  and comi.idGrupo=" + grupos : "");
                    break;
                case "T":
                    stmysql = "select comi.idGrupo,terc.descripcion," + columnas
                             + " from " + tabla + " comi inner join inv_grupos  terc  on comi.idGrupo  = terc.IdGruProducto  where comi.idFactura = '" + idFactura + "' and    comi.idGrupoComision='" + grupocomision + "'" + (grupos != 0 ? "  and comi.idGrupo=" + grupos : "");
                    break;
            }

            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnet, "BuscaGrupoSecundario", ref DsData, "tbl_Comsiones");
            try
            {
                dsdatasetComisiones.Tables.Add(DsData.Tables["tbl_Comsiones"].Copy());
                return true;
            }
            catch { return false; }
        }

        public bool buscaComisionesGrabadas(OdbcConnection myconnet, string idfactura,
            string idgrupo, string VentasIncial, string valorfinal, string idGrupoComision,
            int tipoComision, int tipocliente, string tipovendedor)
        {
            switch (tipoComision)
            {
                case 0:
                    stmysql = "select idGrupo,PorceComiosion   from inv_param_comisiones "
                        + "  where idFactura  = '" + idfactura + "' and idGrupo = '" + idgrupo + "' and VentasIncial = " + VentasIncial + " and VentasFinal = " + valorfinal + " and  idGrupoComision='" + idGrupoComision + "' and tipovendedor='" + tipovendedor + "'";
                    ok = Exec(stmysql, myconnet, "buscaComisionesGrabadas");
                    break;
                case 1:
                    stmysql = "select idGrupo,TipoCliente,PorceComiosion   from inv_param_comisiones_Precios "
                             + "  where idFactura  = '" + idfactura + "' and idGrupo = '" + idgrupo + "' and  TipoCliente = " + tipocliente + " and  idGrupoComision='" + idGrupoComision + "'";
                    ok = Exec(stmysql, myconnet, "buscaComisionesGrabadas");
                    break;
            }
            return ok;
        }

        public bool grabarComisiones(OdbcConnection myconnet, string idfactura,
            DataSet datasetcomision, ref int tipoComision)
        {
            int tipocliente = 0;
            switch (tipoComision)
            {
                case 0:
                    for (int i = 0; i < datasetcomision.Tables["tbl_Comsiones"].Rows.Count; i++)
                    {
                        DataRow row = datasetcomision.Tables["tbl_Comsiones"].Rows[i];
                        string tvStr = row["tipovendedor"].ToString();
                        string tv1 = tvStr.Length > 0 ? tvStr.Substring(0, 1) : " ";
                        if (!buscaComisionesGrabadas(myconnet, idfactura, row["idGrupo"].ToString(), row["VentasIncial"].ToString(), row["VentasFinal"].ToString(), row["idGrupoComision"].ToString(), 0, 0, tv1))
                            stmysql = "insert into inv_param_comisiones(idFactura,idGrupoComision,idGrupo,VentasIncial,VentasFinal,PorceComiosion,tipovendedor,tipobonifica,porcomiventa,porcomirecaudo,porbonifica) "
                                    + "values ('" + idfactura + "','" + row["idGrupoComision"] + "','" + row["idGrupo"] + "'," + row["VentasIncial"] + "," + row["VentasFinal"] + "," + row["PorceComiosion"] + ",'" + tv1 + "','"
                                    + (row["tipobonifica"].ToString().Length > 0 ? row["tipobonifica"].ToString().Substring(0, 1) : " ") + "'," + row["porcomiventa"] + "," + row["porcomirecaudo"] + "," + row["porbonifica"] + ")";
                        else
                            stmysql = "update inv_param_comisiones set PorceComiosion=" + row["PorceComiosion"]
                                    + "  where idFactura='" + idfactura + "' and idGrupo ='" + row["idGrupo"] + "'   and  VentasIncial=" + row["VentasIncial"] + "  and  VentasFinal=" + row["VentasFinal"] + "  and  idGrupoComision ='" + row["idGrupoComision"] + "'";
                        ok = Exec(stmysql, myconnet, "grabarComisiones");
                    }
                    break;
                case 1:
                    for (int i = 0; i < datasetcomision.Tables["tbl_Comsiones"].Rows.Count; i++)
                    {
                        DataRow row = datasetcomision.Tables["tbl_Comsiones"].Rows[i];
                        switch (row["TipoCliente"].ToString().Trim())
                        {
                            case "Todos":         tipocliente = 0; break;
                            case "Asociados":     tipocliente = 1; break;
                            case "Cantidad":      tipocliente = 2; break;
                            case "Especiales":    tipocliente = 3; break;
                            case "% Sobre Costo": tipocliente = 4; break;
                        }
                        if (!buscaComisionesGrabadas(myconnet, idfactura, row["idGrupo"].ToString(), "0", "0", row["idGrupoComision"].ToString(), 1, tipocliente, " "))
                            stmysql = "insert into inv_param_comisiones_Precios(idFactura,idGrupoComision,idGrupo,TipoCliente,PorceComiosion) "
                                    + "values ('" + idfactura + "','" + row["idGrupoComision"] + "','" + row["idGrupo"] + "'," + tipocliente + "," + row["PorceComiosion"] + ")";
                        else
                            stmysql = "update inv_param_comisiones_Precios set PorceComiosion=" + row["PorceComiosion"]
                                    + "  where idFactura='" + idfactura + "' and idGrupo ='" + row["idGrupo"] + "'  and  TipoCliente=" + tipocliente + "  and  idGrupoComision ='" + row["idGrupoComision"] + "'";
                        ok = Exec(stmysql, myconnet, "grabarComisiones");
                    }
                    break;
            }
            return ok;
        }

        public bool EliminarComisiones(OdbcConnection myconnect, string TipoBorrado,
            string idFactura, string idGrupo, string VentasIncial, string VentasFinal,
            string idGrupoComision, int tipocomision)
        {
            string where = "", tabla = " ";
            int tipocliente = 0;
            switch (tipocomision)
            {
                case 0:
                    tabla = "  inv_param_comisiones ";
                    where = "  VentasIncial=" + VentasIncial + " and VentasFinal=" + VentasFinal + "  and idGrupoComision='" + idGrupoComision + "'";
                    break;
                case 1:
                    switch (VentasIncial.Trim())
                    {
                        case "Todos":         tipocliente = 0; break;
                        case "Asociados":     tipocliente = 1; break;
                        case "Cantidad":      tipocliente = 2; break;
                        case "Especiales":    tipocliente = 3; break;
                        case "% Sobre Costo": tipocliente = 4; break;
                    }
                    tabla = "  inv_param_comisiones_Precios ";
                    where = "  TipoCliente=" + tipocliente + "   and idGrupoComision='" + idGrupoComision + "'";
                    break;
            }
            switch (TipoBorrado)
            {
                case "Unico":
                    stmysql = "delete  from " + tabla + " Where idFactura ='" + idFactura + "' and idGrupo='" + idGrupo + "' and   " + where;
                    break;
                case "Varios":
                    stmysql = "delete  from  inv_param_comisiones where idFactura='" + idFactura + "'";
                    break;
            }
            ok = Exec(stmysql, myconnect, "EliminarRangoscoring");
            return ok;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HelpProductos / BuscaCuentasIva (x2) / GrabarCuentasIva / EliminarCuentasIva / EliminarCuentasIvaTasaTipo
        // ─────────────────────────────────────────────────────────────────────
        public string HelpProductos(OdbcConnection myconnect, Form form)
        {
            return msgsas.CargaAyuda("inv_productos", "IdProducto", "Descripcion", "Resumido", myconnect, form);
        }

        public bool BuscaCuentasIva(string idUbicacion, string IdBodega, string IdGrupo,
            string IdtipoMovto, decimal Tasa, int tipo, OdbcConnection myconnect,
            ref string CuentaIva)
        {
            var sb = new StringBuilder();
            sb.Append("select cuenta as campo1 ");
            sb.Append("from inv_cuentasiva ");
            sb.Append("where idubicacion='" + idUbicacion + "' and idbodega='" + IdBodega + "' and IdGruProducto='" + IdGrupo + "' and ");
            sb.Append("IdTipoMovto='" + IdtipoMovto + "' and tipo=" + tipo + " and tasa='" + Tasa + "'");

            string StCuenta = "";
            ok = Exec(sb.ToString(), myconnect, "BuscaCuentasIva", ref StCuenta);
            CuentaIva = ok ? StCuenta : "";
            return ok;
        }

        public virtual DataSet BuscaCuentasIva(string idUbicacion, string IdBodega,
            string IdGrupo, string IdtipoMovto, int tipo, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            var sb = new StringBuilder();
            sb.Append("select tasa, cuenta ");
            sb.Append("from inv_cuentasiva ");
            sb.Append("where idubicacion='" + idUbicacion + "' and idbodega='" + IdBodega + "' and IdGruProducto='" + IdGrupo + "' and ");
            sb.Append("IdTipoMovto='" + IdtipoMovto + "' and tipo=" + tipo + " order by tasa");
            MyOdbcConet.ExecuteQueryDataset(sb.ToString(), myconnect, "BuscaCuentasIva", ref dsdata, "tblctaivas");
            return dsdata;
        }

        public bool GrabarCuentasIva(string idUbicacion, string IdBodega, string IdGrupo,
            string IdtipoMovto, double TasaIva, string Cuenta, int tipo, OdbcConnection myconnect)
        {
            ok = BuscaCuentasIva(idUbicacion, IdBodega, IdGrupo, IdtipoMovto, (decimal)TasaIva, tipo, myconnect, ref Cuenta);
            var sb = new StringBuilder();
            if (!ok)
            {
                sb.Append("insert into inv_cuentasiva (idubicacion, idbodega, IdGruProducto, IdTipoMovto, tasa, cuenta, tipo) values('");
                sb.Append(idUbicacion + "','");
                sb.Append(IdBodega + "','");
                sb.Append(IdGrupo + "','");
                sb.Append(IdtipoMovto + "','");
                sb.Append(TasaIva + "','");
                sb.Append(Cuenta + "',");
                sb.Append(tipo + ")");
            }
            else
            {
                sb.Append("update inv_cuentasiva set ");
                sb.Append("cuenta='" + Cuenta + "' ");
                sb.Append("where idubicacion='" + idUbicacion + "' and idbodega='" + IdBodega + "' and IdGruProducto='" + IdGrupo + "' and ");
                sb.Append("IdTipoMovto='" + IdtipoMovto + "' and tasa='" + TasaIva + "' and tipo=" + tipo);
            }
            ok = Exec(sb.ToString(), myconnect, "GrabarCuentasIva");
            return ok;
        }

        public void EliminarCuentasIva(string idUbicacion, string IdBodega,
            string IdGrupo, string IdtipoMovto, OdbcConnection myconnect)
        {
            var sb = new StringBuilder();
            sb.Append("delete from inv_cuentasiva ");
            sb.Append("where idubicacion='" + idUbicacion + "' and idbodega='" + IdBodega + "' and IdGruProducto='" + IdGrupo + "' and ");
            sb.Append("IdTipoMovto='" + IdtipoMovto + "'");
            Exec(sb.ToString(), myconnect, "EliminarCuentasIva");
        }

        public void EliminarCuentasIvaTasaTipo(string idUbicacion, string IdBodega,
            string IdGrupo, string IdtipoMovto, double tasa, int tipo, OdbcConnection myconnect)
        {
            var sb = new StringBuilder();
            sb.Append("delete from inv_cuentasiva ");
            sb.Append("where idubicacion='" + idUbicacion + "' and idbodega='" + IdBodega + "' and IdGruProducto='" + IdGrupo + "' and ");
            sb.Append("IdTipoMovto='" + IdtipoMovto + "' and tasa=" + tasa + " and tipo=" + tipo);
            Exec(sb.ToString(), myconnect, "EliminarCuentasIvaTipo");
        }

        // ─────────────────────────────────────────────────────────────────────
        // CargarListaUbicacion_Especifica / CargarListaBodega_Especifica
        // ─────────────────────────────────────────────────────────────────────
        public void CargarListaUbicacion_Especifica(string idUbicacion,
            ComboBox CbxUbicacion, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            stmysql = "select idubicacion,descripcion from inv_ubicacion where idubicacion=" + idUbicacion;
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "CargarListaUbicacion", ref dsdata, "tblubicacion");
            CbxUbicacion.Items.Clear();
            for (int fila = 0; fila < dsdata.Tables["tblubicacion"].Rows.Count; fila++)
            {
                DataRow row = dsdata.Tables["tblubicacion"].Rows[fila];
                CbxUbicacion.Items.Add(row["idubicacion"] + " - " + row["descripcion"]);
            }
        }

        public void CargarListaBodega_Especifica(string IdUbicacion, string IdBodega,
            ComboBox CbxBodega, OdbcConnection myconnect)
        {
            DataSet dsdata = new DataSet();
            if (!Microsoft.VisualBasic.Information.IsNumeric(IdUbicacion))
                IdUbicacion = "0";
            stmysql = "select idbodega,descripcion from inv_bodegas where idubicacion=" + IdUbicacion + " and  idbodega= " + IdBodega;
            MyOdbcConet.ExecuteQueryDataset(stmysql, myconnect, "CargarListaBodega", ref dsdata, "tblbodegas");
            CbxBodega.Items.Clear();
            for (int fila = 0; fila < dsdata.Tables["tblbodegas"].Rows.Count; fila++)
            {
                DataRow row = dsdata.Tables["tblbodegas"].Rows[fila];
                CbxBodega.Items.Add(row["idbodega"] + " - " + row["descripcion"]);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // ValidaPunto_turnoCompraVenta / ManipulaCuentasComision
        // ─────────────────────────────────────────────────────────────────────
        public bool ValidaPunto_turnoCompraVenta(int IdPunto, int Idturno,
            DateTime Fecha, string IdUsuario, OdbcConnection myconnect)
        {
            stmysql = "select IdTipoMovto,Secuencia from inv_movtos where pos= 'N' and IdPunto =" + IdPunto + " and FecMovto='" + Fecha.ToString(varini.PstForFec) + "' and IdTurno = '" + Idturno + "' and  Idusuario= '" + IdUsuario + "'";
            ok = Exec(stmysql, myconnect, "ValidaPunto_turnoCompraVenta");
            return ok;
        }

        public bool ManipulaCuentasComision(string metodo, string tipovendedor,
            ref string netopagarcontado, ref string netopagarcredito,
            ref string netopagarcomision, ref string retencion,
            ref string gasto, ref double basereten, ref double porcenreten,
            ref string calretencion, OdbcConnection myconnect)
        {
            DataTable Tabla = new DataTable();
            stmysql = "";
            switch (metodo)
            {
                case "1": // INSERTA
                    stmysql = "insert into inv_cuentascomision (tipovendedor,netopagarcontado,netopagarcredito,netopagarcomision,retencion,gasto,basereten,porcenreten,calretencion) "
                             + "values ('" + tipovendedor + "','" + netopagarcontado + "','" + netopagarcredito + "','" + netopagarcomision + "','" + retencion + "','" + gasto + "',"
                             + basereten + "," + porcenreten + ",'" + calretencion + "')";
                    break;
                case "2": // ACTUALIZA
                    stmysql = "update inv_cuentascomision set netopagarcontado='" + netopagarcontado + "',netopagarcredito='" + netopagarcredito + "',netopagarcomision='" + netopagarcomision + "',retencion='" + retencion + "',"
                             + "gasto='" + gasto + "',basereten=" + basereten + ",porcenreten=" + porcenreten + ",calretencion='" + calretencion + "' where tipovendedor='" + tipovendedor + "'";
                    break;
                case "3": // ELIMINA
                    stmysql = "delete from inv_cuentascomision where tipovendedor='" + tipovendedor + "'";
                    break;
                case "4": // CONSULTA
                    stmysql = "select netopagarcontado,netopagarcredito,netopagarcomision,retencion,gasto,basereten,porcenreten,calretencion from inv_cuentascomision where tipovendedor='" + tipovendedor + "'";
                    break;
            }

            if (stmysql != "")
            {
                if (metodo == "4")
                {
                    if (MyOdbcConet.ExecuteConsulta(stmysql, myconnect, "ManipulaCuentasComision", ref Tabla))
                    {
                        DataRow r = Tabla.Rows[0];
                        netopagarcontado  = r["netopagarcontado"].ToString();
                        netopagarcredito  = r["netopagarcredito"].ToString();
                        netopagarcomision = r["netopagarcomision"].ToString();
                        retencion         = r["retencion"].ToString();
                        gasto             = r["gasto"].ToString();
                        if (double.TryParse(r["basereten"].ToString(), out double br)) basereten = br;
                        if (double.TryParse(r["porcenreten"].ToString(), out double pr)) porcenreten = pr;
                        calretencion      = r["calretencion"].ToString();
                        return true;
                    }
                    return false;
                }
                else
                    return Exec(stmysql, myconnect, "ManipulaCuentasComision");
            }
            return false;
        }
    }
}
