using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class AyudaCuen : Form
    {
        public string Codigoter;
        private ClsDepositos dep = new ClsDepositos();
        private System.Data.Odbc.OdbcConnection conect;
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.CarteraFinanciera.Models.ParamCop paramCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();

        public AyudaCuen(System.Data.Odbc.OdbcConnection conect)
        {
            InitializeComponent();
            this.conect = conect;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            Var.ResCuenta = 0;
            Var.ResLinea = 0;
            Var.ResValorAcuenta = 0;
            this.Close();
        }

        private void Retornar()
        {
            if (DgvGrilla.Rows.Count > 0)
            {
                if (DgvGrilla.SelectedRows.Count > 0)
                {
                    Var.ResCuenta = Convert.ToDouble(DgvGrilla.SelectedRows[0].Cells[0].Value);
                    Var.ResLinea = Convert.ToInt32(DgvGrilla.SelectedRows[0].Cells[1].Value);
                    if (!Microsoft.VisualBasic.Information.IsNumeric(TxtValor.Text))
                    {
                        TxtValor.Text = "0";
                    }
                    Var.ResValorAcuenta = Convert.ToDouble(TxtValor.Text);
                }
            }
            else
            {
                Var.ResValorAcuenta = 0;
                Var.ResCuenta = 0;
                Var.ResLinea = 0;
            }
            this.Close();
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            Retornar();
        }

        private void AyudaCuen_Load(object sender, EventArgs e)
        {
            if (Codigoter != "")
            {
                dep.CargaCuentasAhorro(Codigoter, conect, ref DgvGrilla);
            }
        }

        private void DgvGrilla_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (DgvGrilla.SelectedRows.Count > 0)
            {
                Retornar();
            }
        }

        private void DgvGrilla_DoubleClick(object sender, EventArgs e)
        {
        }

        private void DgvGrilla_SelectionChanged(object sender, EventArgs e)
        {
            int index;
            if (DgvGrilla.Rows.Count > 0)
            {
                if (DgvGrilla.SelectedRows.Count > 0)
                {
                    index = 0;
                    Var.ResCuenta = Convert.ToDouble(DgvGrilla.SelectedRows[index].Cells[0].Value);
                    Var.ResLinea = Convert.ToInt32(DgvGrilla.SelectedRows[index].Cells[1].Value);
                    if (!Microsoft.VisualBasic.Information.IsNumeric(TxtValor.Text))
                    {
                        TxtValor.Text = "0";
                    }
                    Var.ResValorAcuenta = Convert.ToDouble(TxtValor.Text);
                }
            }
        }

        private void DgvGrilla_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
        }

        private void TxtValor_LostFocus(object sender, EventArgs e)
        {
            if (Microsoft.VisualBasic.Information.IsNumeric(TxtValor.Text))
            {
                TxtValor.Text = Convert.ToDouble(TxtValor.Text).ToString("N0");
            }
            else
            {
                TxtValor.Text = "0";
            }
        }

        private void TxtCodigoter_LostFocus(object sender, EventArgs e)
        {
            string _cod = TxtCodigoter.Text;
            if (msgcop.BuscaAsociado(ref _cod, conect))
            {
                dep.CargaCuentasAhorro(_cod, conect, ref DgvGrilla);
            }
        }

        private void ayuda_codi_Click(object sender, EventArgs e)
        {
            TxtCodigoter.Text = paramCop.HelpAsociados(conect, this);
            TxtCodigoter.Focus();
        }
    }
}
