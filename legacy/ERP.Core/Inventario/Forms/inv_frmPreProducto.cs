using System;
using ERP.Core.Inventario.Services;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Inventario.Forms
{
    public partial class inv_frmPreProducto : Form
    {
        private ERP.Core.Compartido.Configuracion.ParamSys MsgSys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private msginv msginv = new msginv();
        private ERP.Core.Compartido.Utilidades.Ayuda MsgSasAyuda = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private ERP.Core.Inventario.Services.ClsInvConfig msginvconfig = new ERP.Core.Inventario.Services.ClsInvConfig();
        private bool Ok;
        public string codigo = "";
        private int lista = 0;
        private OdbcConnection myconnect = new OdbcConnection();

        public inv_frmPreProducto(OdbcConnection conexion)
            : base()
        {
            InitializeComponent();
            this.myconnect = conexion;
        }

        private void inv_frmPreProducto_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.Close();
                    break;
                default:
                    // this.MsgSys.ejecutarFocusFormulario(sender, e, this); // ERROR: CS1061
                    break;
            }
        }

        public void AbrirConexion()
        {
            if (this.myconnect.State != ConnectionState.Open)
            {
                this.myconnect.Open();
            }
        }

        private void frmLimiteVentas_Load(object sender, EventArgs e)
        {
            this.CenterToParent();
            // MsgSys.ConfiguraForma(this, this.empresappl.Text); // ERROR: CS1501
            AbrirConexion();
            this.KeyPreview = true;
        }

        private void opcion_ClickEvent(object sender, EventArgs e)
        {
            this.Close();
        }

        private void HelpProducto_Click(object sender, EventArgs e)
        {
            this.txtIdproducto.Text = MsgSasAyuda.CargaAyuda("inv_productos", "IdProducto", "Descripcion", "Resumido", myconnect, this);
            this.txtIdproducto.Focus();
        }

        public void txtIdproducto_LostFocus(object sender, EventArgs e)
        {
            string NomProducto = " ";
            int ClaIva = 0;
            double TasaIva = 0;
            double PorDsto = 0;
            double RetFte = 0;

            if (this.txtIdproducto.Text.Trim() != "")
            {
                string _NomProducto = NomProducto;
                string _lista = lista.ToString();
                string _ClaIva = ClaIva.ToString();
                string _TasaIva = TasaIva.ToString();
                string _PorDsto = PorDsto.ToString();
                string _RetFte = RetFte.ToString();

                // Ok = msginvconfig.BuscaProductos(this.txtIdproducto.Text, myconnect, ref _NomProducto, "", "", "", "", ref _lista, "", ref _ClaIva, ref _TasaIva, "", "", "", "", "", "", "", "", "", "", "", "", ref _PorDsto, "", "", "", "", "", "", ref _RetFte); // ERROR: CS7036

                NomProducto = _NomProducto;
                int.TryParse(_lista, out lista);
                int.TryParse(_ClaIva, out ClaIva);
                double.TryParse(_TasaIva, out TasaIva);
                double.TryParse(_PorDsto, out PorDsto);
                double.TryParse(_RetFte, out RetFte);

                switch (Ok)
                {
                    case false:
                        MessageBox.Show("Producto no esta Creado", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        this.txtIdproducto.Text = null;
                        this.txtIdproducto.Focus();
                        return;
                    case true:
                        InitializaCamposGenerales();
                        lblInfoNombre.Text = NomProducto;
                        string precio = "0";
                        string cant = "";
                        string porc = "";

                        TipoCliente(0, ref precio);
                        LblTodos.Text = CalculaPrecio(Convert.ToDouble(precio), 1, ClaIva, TasaIva, PorDsto, RetFte, 0).ToString();
                        LblTodos.Text = Strings.FormatNumber(Convert.ToDouble(LblTodos.Text), 0);

                        precio = "0";
                        TipoCliente(1, ref precio);
                        LblAsociados.Text = CalculaPrecio(Convert.ToDouble(precio), 1, ClaIva, TasaIva, PorDsto, RetFte, 0).ToString();
                        LblAsociados.Text = Strings.FormatNumber(Convert.ToDouble(LblAsociados.Text), 0);

                        precio = "0";
                        cant = "";
                        TipoCliente(2, ref precio, ref cant);
                        LblCant.Text = cant;
                        LblCantidad.Text = CalculaPrecio(Convert.ToDouble(precio), 1, ClaIva, TasaIva, PorDsto, RetFte, 0).ToString();
                        LblCantidad.Text = Strings.FormatNumber(Convert.ToDouble(LblCantidad.Text), 0);

                        precio = "0";
                        TipoCliente(3, ref precio);
                        LblEspeciales.Text = CalculaPrecio(Convert.ToDouble(precio), 1, ClaIva, TasaIva, PorDsto, RetFte, 0).ToString();
                        LblEspeciales.Text = Strings.FormatNumber(Convert.ToDouble(LblEspeciales.Text), 0);

                        precio = "0";
                        porc = "";
                        TipoCliente(4, ref porc);
                        // this.msginv.BuscaInventario(this.txtIdproducto.Text, this.Lblperiodo.Text, myconnect, "", "", "", "", ref precio); // ERROR: CS1501
                        LblPorc.Text = porc;
                        LblSobreCosto.Text = Strings.FormatNumber((Convert.ToDouble(precio) / (1 - (Convert.ToDouble(porc) / 100))), 0);
                        LblSobreCosto.Text = CalculaPrecio(Convert.ToDouble(LblSobreCosto.Text), 1, ClaIva, TasaIva, PorDsto, RetFte, 0).ToString();
                        LblSobreCosto.Text = Strings.FormatNumber(Convert.ToDouble(LblSobreCosto.Text), 0);
                        break;
                }
            }
        }

        private void InitializaCamposGenerales()
        {
            this.LblTodos.Text = "";
            this.LblAsociados.Text = "";
            this.LblCantidad.Text = "";
            this.LblEspeciales.Text = "";
            this.LblSobreCosto.Text = "";
        }

        private void TipoCliente(int tipoCli, ref string precio)
        {
            string cantidad = "";
            TipoCliente(tipoCli, ref precio, ref cantidad);
        }

        private void TipoCliente(int tipoCli, ref string precio, ref string cantidad)
        {
            switch (tipoCli)
            {
                case 2:
                    // Ok = msginvconfig.BuscaListaPreciosCantidad(lista, myconnect, this.txtIdproducto.Text.Trim(), tipoCli, "", "", ref precio, "", ref cantidad); // ERROR: CS1620
                    break;
                default:
                    // Ok = msginvconfig.BuscaListaPrecios(lista, myconnect, this.txtIdproducto.Text.Trim(), tipoCli, "", "", ref precio, ""); // ERROR: CS1620
                    break;
            }
        }

        private double CalculaPrecio(double PreVenta, double Cantidad, int ClaIva, double TasaIva, double PorDsto, double RetFte, double TasaIca)
        {
            double Subtotal = 0, VlrIva = 0, VlrDsto = 0, VlrRetfte = 0, VlrIca = 0;

            VlrDsto = Math.Round((PreVenta * Cantidad) * (PorDsto / 100), 0);

            switch (ClaIva)
            {
                case 0:
                    Subtotal = (PreVenta * Cantidad);
                    break;
                case 1:
                    VlrIva = Math.Round(((PreVenta * Cantidad) - VlrDsto) * (TasaIva / 100), 0);
                    Subtotal = (PreVenta * Cantidad);
                    break;
                case 2:
                    VlrIva = Math.Round((((PreVenta * Cantidad) - VlrDsto) * TasaIva) / (100 + TasaIva), 0);
                    // Subtotal = (PreVenta * Quantity) - VlrIva; // ERROR: CS0103
                    break;
            }

            if (RetFte > 0)
            {
                VlrRetfte = Math.Round(Subtotal * (RetFte / 100), 0);
            }

            if (TasaIca > 0)
            {
                VlrIca = Math.Round(Subtotal * (TasaIca / 1000), 0);
            }

            return (Subtotal + VlrIva - (VlrDsto + VlrRetfte + VlrIca));
        }
    }
}
