using ERP.Core.CarteraFinanciera.Models;
using ERP.Core.Compartido.Utilidades;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class FrmAgregarReferencia : Form
    {
        private ERP.Core.Compartido.Utilidades.Ayuda MsgAyu = new ERP.Core.Compartido.Utilidades.Ayuda("admin");
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.Compartido.Datos.ClsConect OdbcConnect = new ERP.Core.Compartido.Datos.ClsConect();
        private ParamCop parametros = new ParamCop();
        private bool ok;
        public DataSet datase = new DataSet();
        public Form formulario = new Form();
        public string nomempresa;
        public string idcodigoter;

        public FrmAgregarReferencia(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void LimpiarReferencias()
        {
            TxtRefNombre.Text = "";
            TxtDireccionRef.Text = "";
            TxtTelefono.Text = "";
            TxtCiudadRef.Text = "";
            LblNomCiudadRef.Text = "";
            TxtContactoRef.Text = "";
            CbxTipoProducto.Text = "";
            TxtNroProducto.Text = "0";
            TxtCelular.Text = "";
            TxtParentesco.Clear();
            TxtContactoRef.Enabled = false;
            CbxTipoProducto.Enabled = false;
            TxtNroProducto.Enabled = false;
            TxtCelular.Enabled = true;
            TxtParentesco.Enabled = false;
            LblNomparentesco.Visible = false;
            BtnAyudaparen.Enabled = false;
        }

        private bool ValidarReferencia(int Tipo)
        {
            if (CbxTipoReferencia.Text.Trim() == "")
            {
                MessageBox.Show("Falta el tipo de referencia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                CbxTipoReferencia.Focus();
                return false;
            }
            if (TxtRefNombre.Text.Trim() == "")
            {
                MessageBox.Show("Falta el nombre en la referencia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtRefNombre.Focus();
                return false;
            }
            if (TxtDireccionRef.Text.Trim() == "")
            {
                MessageBox.Show("Falta la direccion en la referencia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtDireccionRef.Focus();
                return false;
            }
            if (TxtTelefono.Text.Trim() == "")
            {
                MessageBox.Show("Falta el telefono en la referencia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtTelefono.Focus();
                return false;
            }
            if (TxtCiudadRef.Text.Trim() == "" || !Information.IsNumeric(TxtCiudadRef.Text))
            {
                MessageBox.Show("Falta la ciudad en la referencia. Recuerde que es el codigo de la ciudad.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                TxtCiudadRef.Text = "";
                TxtCiudadRef.Focus();
                return false;
            }
            switch (Tipo)
            {
                case 3:
                    if (TxtContactoRef.Text.Trim() == "")
                    {
                        MessageBox.Show("Falta el contacto en la referencia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        TxtContactoRef.Focus();
                        return false;
                    }
                    break;
                case 4:
                    if (CbxTipoProducto.Text.Trim() == "")
                    {
                        MessageBox.Show("Falta el tipo de producto en la referencia", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        CbxTipoProducto.Focus();
                        return false;
                    }
                    if (!Information.IsNumeric(TxtNroProducto.Text))
                    {
                        MessageBox.Show("Numero de producto errado.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        TxtNroProducto.Text = "";
                        TxtNroProducto.Focus();
                        return false;
                    }
                    break;
            }
            return true;
        }

        private void CbxTipoReferencia_SelectedIndexChanged(object sender, EventArgs e)
        {
            LimpiarReferencias();
            switch (CbxTipoReferencia.SelectedIndex)
            {
                case 1:
                    TxtParentesco.Enabled = true;
                    LblNomparentesco.Visible = true;
                    BtnAyudaparen.Enabled = true;
                    break;
                case 3:
                    TxtContactoRef.Enabled = true;
                    TxtCelular.Enabled = false;
                    break;
                case 4:
                    CbxTipoProducto.Enabled = true;
                    TxtNroProducto.Enabled = true;
                    TxtCelular.Enabled = false;
                    break;
            }
        }

        private void HelpCiudadRef_Click(object sender, EventArgs e)
        {
            string respCampo = string.Empty;
            TxtCiudadRef.Text = MsgAyu.CargaAyuda("sys_ciudad57", "ciudad", "nombre_ciudad", "dpto", mycon, this, "Nombre de la ciudad", "Departamento",null,null, ref respCampo);
            TxtCiudadRef.Focus();
        }

        private void TxtCiudadRef_Leave(object sender, EventArgs e)
        {
            LblNomCiudadRef.Text = " ";
            if (Information.IsNumeric(TxtCiudadRef.Text))
            {
                string nombre = "";
                ok = parametros.BuscaCiudad(TxtCiudadRef.Text, mycon, ParamCop.Navega.Ninguno, ref nombre);
                LblNomCiudadRef.Text = nombre;
                if (!ok)
                {
                    MessageBox.Show("Codigo de ciudad no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    TxtCiudadRef.Text = "";
                    TxtCiudadRef.Focus();
                }
            }
        }

        private void BtnAgregar_Click(object sender, EventArgs e)
        {
            if (ValidarReferencia(CbxTipoReferencia.SelectedIndex))
            {
                ok = parametros.GrabarReferencias(idcodigoter, Strings.Mid(CbxTipoReferencia.Text, 1, 1), TxtParentesco.Text, TxtRefNombre.Text, TxtDireccionRef.Text, TxtTelefono.Text, Convert.ToInt32(TxtCiudadRef.Text), TxtContactoRef.Text, Strings.Mid(CbxTipoProducto.Text, 1, 1), TxtNroProducto.Text, TxtCelular.Text, mycon);
                if (ok)
                {
                    MessageBox.Show("Operacion finalizada con exito.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LimpiarReferencias();
                }
                else
                {
                    MessageBox.Show("No se pudieron guardar los datos.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void Btn_Salir_Click(object sender, EventArgs e)
        {
            this.Hide();
            parametros.CargarVentanaReferencias(idcodigoter, nomempresa, formulario, datase, mycon, ParamCop.TiposReferencia.Todas);
            this.Close();
        }

        private void TxtParentesco_Leave(object sender, EventArgs e)
        {
            if (TxtParentesco.Text.Trim() == "")
            {
                TxtParentesco.Text = "9999";
            }
            else
            {
                TxtParentesco.Text = Strings.Right("0000" + TxtParentesco.Text.Trim(), 4);
                string codigoParent = TxtParentesco.Text;
                string nombre = "";
                if (!parametros.BuscaParentesco(ref codigoParent, mycon, ref nombre))
                {
                    TxtParentesco.Focus();
                }
                TxtParentesco.Text = codigoParent;
                LblNomparentesco.Text = nombre;
            }
        }

        private void BtnAyudaparen_Click(object sender, EventArgs e)
        {
            string respCampo = string.Empty;
            TxtParentesco.Text = MsgAyu.CargaAyuda("sys_parent51", "codigo", "nombre", "", mycon, this, "Codigo Parentesco", "Nombre", null, null, ref respCampo);
            LblNomparentesco.Text = "";
            TxtParentesco.Focus();
        }
    }
}
