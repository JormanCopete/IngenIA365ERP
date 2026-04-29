using ERP.Core.CarteraFinanciera.Services.Creditos;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class frmBienes : Form
    {
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private OdbcConnection mycon;
        private ClsLiqcreditos clslicred = new ClsLiqcreditos();
        public DataSet DsdataBienes = new DataSet();
        public DataSet DsdataVehiculo = new DataSet();
        public string codigo;
        private bool ok;
        public bool ocultarlinklbl;

        public frmBienes(OdbcConnection conexion)
        {
            InitializeComponent();
            this.mycon = conexion;
        }

        private void frmBienes_Load(object sender, EventArgs e)
        {
            string ststring = "";
            int stinteger = 0;
            double stdouble = 0;

            this.DgwBienesRaices.AutoGenerateColumns = false;
            if (DsdataBienes.Tables.Count == 0 || DsdataBienes.Tables[0].TableName != "tblBienesRaices")
            {
                DsdataBienes.Tables.Add("tblBienesRaices");
                DataColumnCollection cols = DsdataBienes.Tables["tblBienesRaices"].Columns;
                cols.Add("CLASE", ststring.GetType());
                cols.Add("NOMCLASE", ststring.GetType());
                cols.Add("DIRECCION", ststring.GetType());
                cols.Add("CIUDAD", stinteger.GetType());
                cols.Add("VALOR", stdouble.GetType());
                cols.Add("NOMCIUDAD", ststring.GetType());
            }
            else
            {
                this.DgwBienesRaices.DataSource = DsdataBienes.Tables["tblBienesRaices"];
            }

            this.DgwVehiculos.AutoGenerateColumns = false;
            if (DsdataVehiculo.Tables.Count == 0 || DsdataVehiculo.Tables[0].TableName != "tblVehiculo")
            {
                DsdataVehiculo.Tables.Add("tblVehiculo");
                DataColumnCollection cols2 = DsdataVehiculo.Tables["tblVehiculo"].Columns;
                cols2.Add("CLASE", ststring.GetType());
                cols2.Add("NOMCLASE", ststring.GetType());
                cols2.Add("MARCA", ststring.GetType());
                cols2.Add("MODELO", ststring.GetType());
                cols2.Add("VALOR", stdouble.GetType());
            }
            else
            {
                this.DgwVehiculos.DataSource = DsdataVehiculo.Tables["tblVehiculo"];
            }

            this.linklblbienesHojadeVida.Visible = ocultarlinklbl;
            this.linklblBuscaVehojaVida.Visible = ocultarlinklbl;
            LlbExitenBienes2.Visible = ocultarlinklbl;
            LlbExitenBienes.Visible = ocultarlinklbl;
        }

        private void BtnSalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void ayuda_codi_Click(object sender, EventArgs e)
        {
            this.TxtCodCiudad.Text = ""; // this.paramcop.HelpCiudades(this.mycon, this);
            this.TxtCodCiudad.Focus();
        }

        private void TxtCodCiudad_LostFocus(object sender, EventArgs e)
        {
            this.LblNomCiudad.Text = " ";
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtCodCiudad.Text))
            {
                string nomCiudad = " ";
                //this.paramcop.BuscaCiudad(this.TxtCodCiudad.Text, this.mycon, ref nomCiudad);
                this.LblNomCiudad.Text = nomCiudad;
            }
        }

        private bool ValidarRaices()
        {
            if (this.CbxClaseRaiz.Text.Trim() == "")
            {
                MessageBox.Show("Debe escoger un tipo de bien raiz", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxClaseRaiz.Focus();
                return false;
            }
            if (this.TxtDireccion.Text.Trim() == "")
            {
                MessageBox.Show("Debe digitar una direccion", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtDireccion.Focus();
                return false;
            }
            if (!Microsoft.VisualBasic.Information.IsNumeric(this.TxtCodCiudad.Text))
            {
                MessageBox.Show("Error en codigo de ciudad. Debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtCodCiudad.Text = "";
                this.TxtCodCiudad.Focus();
                return false;
            }
            //ok = this.paramcop.BuscaCiudad(this.TxtCodCiudad.Text, this.mycon);
            if (!ok)
            {
                MessageBox.Show("Codigo de ciudad no existe.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtCodCiudad.Text = "";
                this.TxtCodCiudad.Focus();
                return false;
            }
            if (!Microsoft.VisualBasic.Information.IsNumeric(this.TxtValorComercial.Text))
            {
                MessageBox.Show("El valor comercial debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtValorComercial.Text = "0";
                this.TxtValorComercial.Focus();
                return false;
            }
            return true;
        }

        private bool ValidarVehiculo()
        {
            if (this.CbxClaseVehiculo.Text.Trim() == "")
            {
                MessageBox.Show("Debe escoger un tipo de vehiculo", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.CbxClaseVehiculo.Focus();
                return false;
            }
            if (this.TxtMarca.Text.Trim() == "")
            {
                MessageBox.Show("Debe digitar una marca", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtMarca.Focus();
                return false;
            }
            if (this.TxtModelo.Text.Trim() == "")
            {
                MessageBox.Show("Debe digitar un modelo", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtModelo.Text = "";
                this.TxtModelo.Focus();
                return false;
            }
            if (!Microsoft.VisualBasic.Information.IsNumeric(this.TxtValorVehiculo.Text))
            {
                MessageBox.Show("El valor comercial debe ser numerico", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtValorVehiculo.Text = "0";
                this.TxtValorVehiculo.Focus();
                return false;
            }
            return true;
        }

        private void InicializaBienRaiz()
        {
            this.CbxClaseRaiz.SelectedIndex = -1;
            this.TxtDireccion.Text = "";
            this.TxtCodCiudad.Text = "";
            this.TxtValorComercial.Text = "0";
        }

        private void InicializaVehiculos()
        {
            this.CbxClaseVehiculo.SelectedIndex = -1;
            this.TxtMarca.Text = "";
            this.TxtModelo.Text = "";
            this.TxtValorVehiculo.Text = "0";
        }

        private void BtnGrabarBien_Click(object sender, EventArgs e)
        {
            if (ValidarRaices())
            {
                DsdataBienes.Tables["tblBienesRaices"].Rows.Add(
                    this.CbxClaseRaiz.SelectedIndex,
                    this.CbxClaseRaiz.Text,
                    this.TxtDireccion.Text,
                    this.TxtCodCiudad.Text,
                    this.TxtValorComercial.Text,
                    this.LblNomCiudad.Text);
                this.DgwBienesRaices.DataSource = this.DsdataBienes.Tables["tblBienesRaices"];
                this.DgwBienesRaices.Refresh();
                InicializaBienRaiz();
            }
        }

        private void BtnGrabarVeh_Click(object sender, EventArgs e)
        {
            if (ValidarVehiculo())
            {
                DsdataVehiculo.Tables["tblVehiculo"].Rows.Add(
                    this.CbxClaseVehiculo.SelectedIndex,
                    this.CbxClaseVehiculo.Text,
                    this.TxtMarca.Text,
                    this.TxtModelo.Text,
                    this.TxtValorVehiculo.Text);
                this.DgwVehiculos.DataSource = this.DsdataVehiculo.Tables["tblVehiculo"];
                this.DgwVehiculos.Refresh();
                InicializaVehiculos();
            }
        }

        private void BtnEliminarRaiz_Click(object sender, EventArgs e)
        {
            if (this.DgwBienesRaices.SelectedCells.Count <= 0)
            {
                MessageBox.Show("Debe selecionar un registro", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            this.DsdataBienes.Tables["tblBienesRaices"].Rows.RemoveAt(this.DgwBienesRaices.CurrentRow.Index);
            this.DgwBienesRaices.DataSource = this.DsdataBienes.Tables["tblBienesRaices"];
        }

        private void BtnEliminarVehiculo_Click(object sender, EventArgs e)
        {
            if (this.DgwVehiculos.SelectedCells.Count <= 0)
            {
                MessageBox.Show("Debe selecionar un registro", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            this.DsdataVehiculo.Tables["tblVehiculo"].Rows.RemoveAt(this.DgwVehiculos.CurrentRow.Index);
            this.DgwVehiculos.DataSource = this.DsdataVehiculo.Tables["tblVehiculo"];
        }

        private void TxtValorComercial_LostFocus(object sender, EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtValorComercial.Text))
            {
                this.TxtValorComercial.Text = Convert.ToDouble(this.TxtValorComercial.Text).ToString("N2");
            }
            else
            {
                this.TxtValorComercial.Text = "0";
            }
        }

        private void TxtValorVehiculo_LostFocus(object sender, EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(this.TxtValorVehiculo.Text))
            {
                this.TxtValorVehiculo.Text = Convert.ToDouble(this.TxtValorVehiculo.Text).ToString("N2");
            }
            else
            {
                this.TxtValorVehiculo.Text = "0";
            }
        }

        private void LlbExitenBienes_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DataSet dsdatos = new DataSet();
            DataSet data = new DataSet();

            data = DsdataBienes;
            dsdatos = clslicred.CargaBienesHistorial(1, data, this, mycon, this.LblNomEmpresa.Text, codigo);
            if (dsdatos.Tables.Count != 0)
            {
                if (dsdatos.Tables["TblBienSeleccion"].Rows.Count != 0)
                {
                    for (int cont = 0; cont <= dsdatos.Tables["TblBienSeleccion"].Rows.Count - 1; cont++)
                    {
                        DataRow row = dsdatos.Tables["TblBienSeleccion"].Rows[cont];
                        DsdataBienes.Tables["tblBienesRaices"].Rows.Add(
                            row["Clase"].ToString(), row["NOMCLASE"].ToString(),
                            row["direccion"].ToString(), row["ciudad"].ToString(),
                            row["valor"].ToString(), row["nomciudad"].ToString());
                    }
                }
                this.DgwBienesRaices.DataSource = DsdataBienes.Tables["tblBienesRaices"];
            }
            this.DgwBienesRaices.Refresh();
        }

        private void LlbExitenBienes2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DataSet dsdatos = new DataSet();
            DataSet data = new DataSet();

            data = DsdataVehiculo;
            dsdatos = clslicred.CargaBienesHistorial(2, data, this, mycon, this.LblNomEmpresa.Text, codigo);
            if (dsdatos.Tables.Count != 0)
            {
                if (dsdatos.Tables["TblBienSeleccion"].Rows.Count != 0)
                {
                    for (int cont = 0; cont <= dsdatos.Tables["TblBienSeleccion"].Rows.Count - 1; cont++)
                    {
                        DataRow row = dsdatos.Tables["TblBienSeleccion"].Rows[cont];
                        DsdataVehiculo.Tables["tblVehiculo"].Rows.Add(
                            row["Clase"].ToString(), row["NOMCLASE"].ToString(),
                            row["marca"].ToString(), row["modelo"].ToString(),
                            row["valor"].ToString());
                    }
                }
                this.DgwVehiculos.DataSource = DsdataVehiculo.Tables["tblVehiculo"];
            }
            this.DgwVehiculos.Refresh();
        }

        private void linklblbienesHojadeVida_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DataSet dsdatos = new DataSet();
            DataSet data = new DataSet();

            data = DsdataBienes;
            dsdatos = clslicred.CargaBienesHistorial(1, data, this, mycon, this.LblNomEmpresa.Text, codigo, "sys_maenit");
            if (dsdatos.Tables.Count != 0)
            {
                if (dsdatos.Tables["TblBienSeleccion"].Rows.Count != 0)
                {
                    for (int cont = 0; cont <= dsdatos.Tables["TblBienSeleccion"].Rows.Count - 1; cont++)
                    {
                        DataRow row = dsdatos.Tables["TblBienSeleccion"].Rows[cont];
                        DsdataBienes.Tables["tblBienesRaices"].Rows.Add(
                            row["Clase"].ToString(), row["NOMCLASE"].ToString(),
                            row["direccion"].ToString(), row["ciudad"].ToString(),
                            row["valor"].ToString(), row["nomciudad"].ToString());
                    }
                }
                this.DgwBienesRaices.DataSource = DsdataBienes.Tables["tblBienesRaices"];
            }
            this.DgwBienesRaices.Refresh();
        }

        private void linklblBuscaVehojaVida_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            DataSet dsdatos = new DataSet();
            DataSet data = new DataSet();

            data = DsdataVehiculo;
            dsdatos = clslicred.CargaBienesHistorial(2, data, this, mycon, this.LblNomEmpresa.Text, codigo, "sys_maenit");
            if (dsdatos.Tables.Count != 0)
            {
                if (dsdatos.Tables["TblBienSeleccion"].Rows.Count != 0)
                {
                    for (int cont = 0; cont <= dsdatos.Tables["TblBienSeleccion"].Rows.Count - 1; cont++)
                    {
                        DataRow row = dsdatos.Tables["TblBienSeleccion"].Rows[cont];
                        DsdataVehiculo.Tables["tblVehiculo"].Rows.Add(
                            row["Clase"].ToString(), row["NOMCLASE"].ToString(),
                            row["marca"].ToString(), row["modelo"].ToString(),
                            row["valor"].ToString());
                    }
                }
                this.DgwVehiculos.DataSource = DsdataVehiculo.Tables["tblVehiculo"];
            }
            this.DgwVehiculos.Refresh();
        }
    }
}
