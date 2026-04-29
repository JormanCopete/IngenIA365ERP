using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmlavado : Form
    {
        public System.Data.Odbc.OdbcConnection myconexion;
        public string Usuario;
        public double IdTransaccion;
        public string compronte;
        public double numero_domto;
        public bool impOrigenFond;
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys paramsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Reportes.config_report confirep = new ERP.Core.Compartido.Reportes.config_report();
        private bool ok;
        private double IdLavado = 0;

        public frmlavado()
        {
            InitializeComponent();
        }

        private void frmlavado_Load(object sender, EventArgs e)
        {
            CargarDatosAsociado();
            this.TxtObservaciones.Text = " ";
        }

        private void CargarDatosAsociado()
        {
            DataSet dsdata = new DataSet();
            //ok = paramcop.BuscaAsociado(this.TxtIdAsociado.Text, dsdata, myconexion);
            if (ok)
            {
                DataRow row = dsdata.Tables["tblasociados"].Rows[0];
                this.TxtNomAsociado.Text = row["nombre"].ToString();
                this.TxtApeAsociado.Text = row["apellido"].ToString();
                this.TxtDirAsociado.Text = row["direccion"].ToString();
                this.TxtTelAsociado.Text = row["telefono1"].ToString();
                this.TxtSalarioAsociado.Text = Strings.FormatNumber(Convert.ToDouble(row["salario"]), 0);
                this.TxtOtrosIngAsociado.Text = Strings.FormatNumber(Convert.ToDouble(row["otro_ingreso"]), 0);
                this.CbxTipoIdAsociado.Text = this.CbxTipoIdAsociado.Items[this.CbxTipoIdAsociado.FindString(row["tipo_nit"].ToString())].ToString();
            }
        }

        private void ChkRepiteInfo_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.ChkRepiteInfo.Checked)
            {
                this.TxtIdCliente.Text = "";
                this.TxtDirCliente.Text = "";
                this.TxtNomCliente.Text = "";
                this.TxtApeCliente.Text = "";
                this.TxtTelCliente.Text = "";
                this.CbxTipoIdCliente.SelectedIndex = 0;
            }
            else
            {
                this.TxtIdCliente.Text = this.TxtIdAsociado.Text;
                this.TxtDirCliente.Text = this.TxtDirAsociado.Text;
                this.TxtNomCliente.Text = this.TxtNomAsociado.Text;
                this.TxtApeCliente.Text = this.TxtApeAsociado.Text;
                this.TxtTelCliente.Text = this.TxtTelAsociado.Text;
                this.CbxTipoIdCliente.SelectedIndex = this.CbxTipoIdAsociado.SelectedIndex;
            }
        }

        private void BtnActualizar_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Esta seguro de actualizar la informacion del titular?", "SOLIDO", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                //ok = this.paramcop.ActualizaDatosAsociado(this.TxtIdAsociado.Text, this.TxtNomAsociado.Text, this.TxtApeAsociado.Text, this.TxtDirAsociado.Text, this.TxtTelAsociado.Text, this.CbxTipoIdAsociado.Text.Substring(0, 1), this.TxtSalarioAsociado.Text, this.TxtOtrosIngAsociado.Text, myconexion);
                if (ok)
                {
                    MessageBox.Show("Registro se actualizo correctamente", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void TxtSalarioAsociado_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtSalarioAsociado.Text))
            {
                this.TxtSalarioAsociado.Text = "0";
            }
            else
            {
                this.TxtSalarioAsociado.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtSalarioAsociado.Text), 0);
            }
        }

        private void TxtOtrosIngAsociado_LostFocus(object sender, EventArgs e)
        {
            if (!Information.IsNumeric(this.TxtOtrosIngAsociado.Text))
            {
                this.TxtOtrosIngAsociado.Text = "0";
            }
            else
            {
                this.TxtOtrosIngAsociado.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtOtrosIngAsociado.Text), 0);
            }
        }

        private void BtnImprimir_Click(object sender, EventArgs e)
        {
            this.BtnImprimir.Enabled = false;
            GrabarDeclaracion();
            this.BtnImprimir.Enabled = true;
        }

        private void GrabarDeclaracion()
        {
            if (ValidaCampos())
            {
                if (IdLavado == 0)
                {
                    //IdLavado = this.paramcop.BuscaConseLavado(this.myconexion);
                }

                //ok = this.msgcop.GrabarDeclaracionLavadoActivos(IdLavado, DateTime.Now, this.TxtIdAsociado.Text, this.TxtActEcoAsociado.Text, this.compronte, this.numero_domto, this.IdTransaccion, this.CbxTipoOperacion.Text.Substring(0, 1), this.CbxDetalleOperacion.Text.Substring(0, 1),
                //    this.TxtProducto.Text, this.LblValorTransaccion.Text, this.TxtIdCliente.Text, this.CbxTipoIdCliente.Text.Substring(0, 1), this.TxtNomCliente.Text, this.TxtApeCliente.Text, this.TxtDirCliente.Text, this.TxtTelCliente.Text, this.TxtObservaciones.Text, this.myconexion);
                if (ok)
                {
                    ImprimirFormato(IdLavado);
                }
            }
        }

        private void ImprimirFormato(double IdLavado)
        {
            DataSet dsdataset = new DataSet();
            ERP.Core.Compartido.Reportes.reporte rep = new ERP.Core.Compartido.Reportes.reporte("cop_rformalavado");
            if (ValidaCampos())
            {
                dsdataset = CreaDataset(IdLavado);
                rep.SetDataSource(dsdataset);
                try
                {
                    rep.SetParameterValue("impOrigenFond", impOrigenFond ? "Y" : "N");
                }
                catch (Exception)
                {
                }
                confirep.confi_reportes(this, rep);
            }
        }

        private bool ValidaCampos()
        {
            if (this.CbxDetalleOperacion.Text.Trim() == "")
            {
                MessageBox.Show("Debe escoger el detalle de la operacion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxDetalleOperacion.Focus();
                return false;
            }
            if (this.CbxTipoIdCliente.Text.Trim() == "")
            {
                MessageBox.Show("Debe escoger el tipo de identificacion de la persona que realiza la transaccion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxTipoIdCliente.Focus();
                return false;
            }
            if (this.TxtIdCliente.Text.Trim() == "")
            {
                MessageBox.Show("Debe ingresar el numero de identificacion de la persona que realiza la transaccion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtIdCliente.Focus();
                return false;
            }
            if (this.TxtNomCliente.Text.Trim() == "")
            {
                MessageBox.Show("Debe ingresar el nombre de la persona que realiza la transaccion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtNomCliente.Focus();
                return false;
            }
            if (this.TxtApeCliente.Text.Trim() == "")
            {
                MessageBox.Show("Debe ingresar el nombre de la persona que realiza la transaccion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtApeCliente.Focus();
                return false;
            }
            return true;
        }

        private DataSet CreaDataset(double IdLavado)
        {
            DataSet dsdata = new DataSet();
            string StString = "";
            double StDouble = 0;
            double VlrIngresos = 0;
            string NombreUsuario = " ";

            dsdata.Tables.Add("tbllavado");
            dsdata.Tables["tbllavado"].Columns.Add("cedula", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("TipoOperacion", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("Producto", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("Valor", StDouble.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("DetalleOperacion", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("TipoIdCliente", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("IdCliente", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("NomCliente", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("DirCliente", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("TelCliente", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("TipoIdAsociado", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("IdAsociado", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("NomAsociado", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("DirAsociado", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("TelAsociado", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("ActividadEconomica", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("Ingresos", StDouble.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("Consecutivo", StDouble.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("NomUsuario", StString.GetType());
            dsdata.Tables["tbllavado"].Columns.Add("Observacion", StString.GetType());

            //this.paramsys.BuscaUsuario(this.Usuario, this.myconexion, "", ref NombreUsuario);

            VlrIngresos = Convert.ToDouble(this.TxtSalarioAsociado.Text) + Convert.ToDouble(this.TxtOtrosIngAsociado.Text);
            dsdata.Tables["tbllavado"].Rows.Add(
                this.TxtIdAsociado.Text, this.CbxTipoOperacion.Text.Substring(0, 1), this.TxtProducto.Text, this.LblValorTransaccion.Text, this.CbxDetalleOperacion.Text.Substring(0, 1),
                this.CbxTipoIdCliente.Text.Substring(0, 1), this.TxtIdCliente.Text, this.TxtNomCliente.Text + " " + this.TxtApeCliente.Text, this.TxtDirCliente.Text, this.TxtTelCliente.Text, this.CbxTipoIdAsociado.Text.Substring(0, 1),
                this.TxtIdAsociado.Text, this.TxtNomAsociado.Text + " " + this.TxtApeAsociado.Text, this.TxtDirAsociado.Text, this.TxtTelAsociado.Text, this.TxtActEcoAsociado.Text,
                VlrIngresos, IdLavado, NombreUsuario, this.TxtObservaciones.Text);

            return dsdata;
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            if (IdLavado == 0)
            {
                MessageBox.Show("El formato de lavado de activo no se ha generado. Debe diligenciar el formato para poder salir de esta ventana", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            this.Close();
        }
    }
}
