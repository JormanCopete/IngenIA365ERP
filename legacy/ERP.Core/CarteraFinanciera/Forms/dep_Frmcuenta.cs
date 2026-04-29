using ERP.Core.CarteraFinanciera.Services.Depositos;
using System;
using System.Windows.Forms;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class dep_Frmcuenta : Form
    {
        private System.Data.Odbc.OdbcConnection Mycon = new System.Data.Odbc.OdbcConnection();
        private ClsDepositos msgdep = new ClsDepositos();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgparcop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private bool ok;
        private DateTime FechaMovto;

        public dep_Frmcuenta(System.Data.Odbc.OdbcConnection conexion)
        {
            InitializeComponent();
            this.Mycon = conexion;
        }

        private void txtCuenta_LostFocus(object sender, EventArgs e)
        {
            if (txtCuenta.Text.Trim() != null && txtCuenta.Text.Trim() != "")
            {
                BuscaDisponible();
            }
        }

        private void BuscaDisponible()
        {
            int lincred = 0;
            double SaldoCanje = 0, MinRetiro = 0, SalMinCuenta = 0, DiasCanje = 0, Maxretiro = 0;
            double saldot = 0;

            txtCodigoter.Text = "99999999999999";
            string _codigoter = txtCodigoter.Text;
            string _lincredStr = "0";
            ok = msgdep.BuscarCuentaAhorro(txtCuenta.Text, Mycon, ClsDepositos.Navega.Ninguno, ref _codigoter, ref _lincredStr);
            txtCodigoter.Text = _codigoter;
            int.TryParse(_lincredStr, out lincred);
            if (!ok)
            {
                MessageBox.Show("Numero de cuenta de ahorros no existe", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            msgdep.BuscaSaldoEncanje(txtCuenta.Text, Mycon, ref SaldoCanje);

            // BuscaLineaAhorro: params 8=VALMIT_TRAN, 9=SALMIN_CUENTA, 13=DIAS_CANJE, 16=VALOR_MAX_RET
            string _nombre = "", _nombreResum = "", _cptoInt = "", _cptoRfte = "";
            string _forApli4xmil = "", _cpto4mil = "", _cbte4xmil = "", _comentario = "";
            string _formatoDian = "", _validaRetiro = "N", _manejaformaPAP = "N", _cuentaTesoreria = " ";
            int _perpagoInt = 0, _diasGracia = 0, _forLiq = 0, _formaPag = 0, _diaCanje = 0;
            int _retCheGmf = 0, _cptoInteresInt = 0, _fuente = 0, _diasCanjeOtras = 0, _equisuper = 0;
            double _salminInt = 0, _valmitTran = 0, _salminCuenta = 0, _porpagoInt = 0;
            double _vlrMinRfte = 0, _porRfte = 0, _valorMaxRet = 0, _gravamen = 0;
            double _tope4xmil = 0, _maxefectivo = 0, _consecutivo = 0, _topeRetiro = 0;
            msgdep.BuscaLineaAhorro(ref lincred, Mycon, ClsDepositos.Navega.Ninguno,
                ref _nombre, ref _nombreResum, ref _perpagoInt, ref _salminInt, ref _valmitTran,
                ref _salminCuenta, ref _porpagoInt, ref _vlrMinRfte,
                ref _porRfte, ref _diaCanje, ref _forLiq, ref _diasGracia, ref _valorMaxRet,
                ref _cptoInt, ref _cptoRfte, ref _formaPag, ref _gravamen,
                ref _forApli4xmil, ref _cpto4mil, ref _tope4xmil, ref _cbte4xmil,
                ref _comentario, ref _maxefectivo, ref _consecutivo,
                ref _retCheGmf, ref _cptoInteresInt, ref _formatoDian, ref _fuente, ref _diasCanjeOtras,
                ref _validaRetiro, ref _topeRetiro, ref _manejaformaPAP, ref _cuentaTesoreria, ref _equisuper);
            MinRetiro = _valmitTran;
            SalMinCuenta = _salminCuenta;
            DiasCanje = (double)_diaCanje;
            Maxretiro = _valorMaxRet;

            string nombreTercero = LblNombreTercero.Text;
            string _codigoterP = txtCodigoter.Text;
            System.Data.DataSet _dsPar = new System.Data.DataSet();
            msgparcop.BuscaAsociado(ref _codigoterP, ref _dsPar, Mycon);
            if (_dsPar.Tables["tblasociados"] != null && _dsPar.Tables["tblasociados"].Rows.Count > 0)
                nombreTercero = _dsPar.Tables["tblasociados"].Rows[0]["apellido"].ToString() + " " + _dsPar.Tables["tblasociados"].Rows[0]["nombre"].ToString();
            LblNombreTercero.Text = nombreTercero;

            msgcop.BuscaSaldoObligacion(txtCodigoter.Text, lincred, Convert.ToDouble(txtCuenta.Text), int.Parse(FechaMovto.ToString("yyyyMM")), Mycon, ref saldot);
            saldot = saldot * -1;

            txtSaldoCanje.Text = SaldoCanje.ToString("N2");
            txtSaldoTotal.Text = saldot.ToString("N2");
            txtSaldoDisponible.Text = Math.Round((saldot - SalMinCuenta - SaldoCanje), 2).ToString("N2");
        }

        private void txtCuenta_TextChanged(object sender, EventArgs e)
        {
        }

        private void dep_Frmcuenta_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
        }

        private void dep_Frmcuenta_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();
        }

        private void BtnAceptar_Click(object sender, EventArgs e)
        {
        }
    }
}
