using ERP.Core.CarteraFinanciera.Services.Cartera;
using System;
using System.Data;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class cop_fconcuope01 : Form
    {
        private ERP.Core.CarteraFinanciera.Models.ParamCop parcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Utilidades.Ayuda Ayu = new ERP.Core.Compartido.Utilidades.Ayuda("");
        private Clscartera carte = new Clscartera();
        public System.Data.Odbc.OdbcConnection MyConnect = new System.Data.Odbc.OdbcConnection();

        public cop_fconcuope01(System.Data.Odbc.OdbcConnection Conect)
        {
            InitializeComponent();
            this.MyConnect = Conect;
        }

        public bool INICI;

        private void codigo_LostFocus(object sender, EventArgs e)
        {
            if (codigo.Text == "")
            {
                codigo.Focus();
            }
            else
            {
                limpiar1();
                string _cod1 = codigo.Text;
                System.Data.DataSet _ds1 = new System.Data.DataSet();
                parcop.BuscaAsociado(ref _cod1, ref _ds1, MyConnect);
                if (_ds1.Tables["tblasociados"] != null && _ds1.Tables["tblasociados"].Rows.Count > 0)
                    this.nombreApellido.Text = _ds1.Tables["tblasociados"].Rows[0]["apellido"].ToString() + " " + _ds1.Tables["tblasociados"].Rows[0]["nombre"].ToString();
            }
        }

        private void lincred_LostFocus(object sender, EventArgs e)
        {
            if (lincred.Text == "")
            {
                lincred.Focus();
            }
        }

        private void numero_LostFocus(object sender, EventArgs e)
        {
            movi();
        }

        private void ayuda_codi_Click(object sender, EventArgs e)
        {
            codigo.Text = parcop.HelpAsociados(this.MyConnect, this);
            codigo.Focus();
        }

        private void movi()
        {
            bool encontro = false;
            if (numero.Text == "")
            {
                numero.Focus();
            }
            else
            {
                if (lincred.Text == "0" && numero.Text == "0")
                {
                    this.nom_obliga.Text = "Todas...";
                }
                else
                {
                    int _lincred = Convert.ToInt32(lincred.Text);
                    string _p9 = this.nom_obliga.Text;
                    parcop.BuscaLinea(ref _lincred, this.MyConnect, ref _p9);
                    this.nom_obliga.Text = _p9;
                }

                DataTable dtable;
                dtable = carte.CargaGrillaCuopen(codigo.Text, this.lincred.Text,
                              this.numero.Text, Convert.ToInt32(periodo_cartera.Text), this.MyConnect,
                              Convert.ToInt32(Txt_perIni.Text), Convert.ToInt32(Txt_perFin.Text));
                DatGriCuopen.DataSource = dtable;
                if (dtable.Rows.Count > 0)
                {
                    encontro = true;
                }

                if (encontro == true)
                {
                    double salotro = 0, salcapi = 0, salmora = 0, salinte = 0, saltotal = 0, salextra = 0;
                    for (int i = 0; i < DatGriCuopen.Rows.Count; i++)
                    {
                        DataGridViewRow row = DatGriCuopen.Rows[i];
                        salcapi += Convert.ToDouble(row.Cells[4].Value);
                        salinte += Convert.ToDouble(row.Cells[5].Value);
                        salextra += Convert.ToDouble(row.Cells[6].Value);
                        salotro += Convert.ToDouble(row.Cells[7].Value);
                        salmora += Convert.ToDouble(row.Cells[8].Value);
                        saltotal += Convert.ToDouble(row.Cells[2].Value);
                    }

                    try
                    {
                        DgvTotales.Rows.Clear();
                        this.DgvTotales.Columns.Clear();
                        for (int i = 0; i < this.DatGriCuopen.Columns.Count; i++)
                        {
                            this.DgvTotales.Columns.Add(this.DatGriCuopen.Columns[i].Name, this.DatGriCuopen.Columns[i].HeaderText);
                            this.DgvTotales.Columns[i].Width = this.DatGriCuopen.Columns[i].Width;
                        }
                        this.DgvTotales.ColumnHeadersVisible = false;

                        this.DgvTotales.Rows.Add(" ", "TOTALES",
                            Strings.FormatNumber(saltotal, 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False),
                            " ",
                            Strings.FormatNumber(salcapi, 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False),
                            Strings.FormatNumber(salinte, 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False),
                            Strings.FormatNumber(salextra, 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False),
                            Strings.FormatNumber(salotro, 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False),
                            Strings.FormatNumber(salmora, 2, Microsoft.VisualBasic.TriState.UseDefault, Microsoft.VisualBasic.TriState.False),
                            " ");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString(), "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("No se encontraron cuotas pendientes para este asociado por este No de cta.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lincred.ResetText();
                    numero.ResetText();
                    lincred.Focus();
                    return;
                }
            }
        }

        private void limpiar1()
        {
            nombreApellido.Text = "";
        }

        private void cop_fconcext01_Load(object sender, EventArgs e)
        {
            carte.OdbcConnect.LlenarVarini(ref carte.varini);
            this.empresappl.Text = carte.varini.pstEmpresa;
            if (INICI == true)
            {
                limpiar1();
                string _cod2 = codigo.Text;
                System.Data.DataSet _ds2 = new System.Data.DataSet();
                parcop.BuscaAsociado(ref _cod2, ref _ds2, MyConnect);
                if (_ds2.Tables["tblasociados"] != null && _ds2.Tables["tblasociados"].Rows.Count > 0)
                    this.nombreApellido.Text = _ds2.Tables["tblasociados"].Rows[0]["apellido"].ToString() + " " + _ds2.Tables["tblasociados"].Rows[0]["nombre"].ToString();
                movi();
            }
            else
            {
                DateTime _fechaIni = DateTime.Parse("1/1/1950");
                DateTime _fechaFin = DateTime.Parse("1/1/1950");
                string _estado = "C";
                string _periodoCartera = this.periodo_cartera.Text;
                carte.buscaPeriodo("copc", this.MyConnect, ref _fechaIni, ref _fechaFin, DateTime.Parse("1/1/1950"), ref _estado, ref _periodoCartera);
                this.periodo_cartera.Text = _periodoCartera;
            }
        }

        private void codigo_TextChanged(object sender, EventArgs e)
        {
        }

        private void Salir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void numero_TextChanged(object sender, EventArgs e)
        {
        }

        private void impre_Click(object sender, EventArgs e)
        {
            impre.Enabled = false;
            try
            {
                ERP.Core.Compartido.Reportes.config_report confi = new ERP.Core.Compartido.Reportes.config_report();
                ERP.Core.Compartido.Reportes.reporte r = new ERP.Core.Compartido.Reportes.reporte("cop_imprecuopen");
                r.SetParameterValue("empresa", carte.varini.pstEmpresa);
                r.SetParameterValue("codigoter", codigo.Text);
                r.SetParameterValue("lincred", lincred.Text);
                r.SetParameterValue("numero", numero.Text);
                r.SetParameterValue("periodo", periodo_cartera.Text);
                r.SetParameterValue("perIni", Txt_perIni.Text.Trim());
                r.SetParameterValue("perFin", Txt_perFin.Text.Trim());
                confi.confi_reportes(this, r, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            impre.Enabled = true;
        }

        private void DgvTotales_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DatGriCuopen_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void DatGriCuopen_ColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            this.DgvTotales.Columns[e.Column.Index].Width = e.Column.Width;
            this.DgvTotales.HorizontalScrollingOffset = this.DatGriCuopen.HorizontalScrollingOffset;
        }

        private void DatGriCuopen_Scroll(object sender, ScrollEventArgs e)
        {
            if (e.ScrollOrientation == ScrollOrientation.HorizontalScroll)
            {
                this.DgvTotales.HorizontalScrollingOffset = e.NewValue;
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            if (validarPer())
            {
                movi();
            }
        }

        private bool validarPer()
        {
            if (this.Txt_perIni.Text.Trim() == "")
            {
                MessageBox.Show("Falta periodo Inicial", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Txt_perIni.Focus();
                return false;
            }
            if (this.Txt_perIni.Text.Trim() != "0")
            {
                if (this.Txt_perIni.Text.Trim().Length != 6)
                {
                    MessageBox.Show("Periodo Inicial incorrecto", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Txt_perIni.Focus();
                    return false;
                }
            }
            if (this.Txt_perFin.Text.Trim() == "")
            {
                MessageBox.Show("Falta periodo final", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Txt_perFin.Focus();
                return false;
            }
            if (this.Txt_perFin.Text.Trim() != "0")
            {
                if (this.Txt_perFin.Text.Trim().Length != 6)
                {
                    MessageBox.Show("Periodo final incorrecto", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Txt_perFin.Focus();
                    return false;
                }
            }
            return true;
        }
    }
}
