using ERP.Core.Compartido.Utilidades;
using ERP.Core.Compartido.Controles;
using System;
using System.Data;
using System.Data.Odbc;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.Compartido.Forms
{
    public partial class FrmFormPago : Form
    {
        private OdbcConnection mycon = new OdbcConnection();
        private ERP.Core.CarteraFinanciera.Models.ParamCop msgconfigCop = new ERP.Core.CarteraFinanciera.Models.ParamCop();
        private ERP.Core.Compartido.Configuracion.ParamSys msgparsys = new ERP.Core.Compartido.Configuracion.ParamSys();
        private ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera msgcop = new ERP.Core.CarteraFinanciera.Services.Cartera.Clscartera();
        private ERP.Core.Compartido.Datos.ClsConect MyOdbcConet = new ERP.Core.Compartido.Datos.ClsConect();
        public ERP.Core.Compartido.Datos.ClsConect.odbcConect varini = new ERP.Core.Compartido.Datos.ClsConect.odbcConect();
        private ERP.Core.Compartido.Utilidades.Ayuda msgsas;
        private int cuentatiempo;
        private double topelavado = 0;
        public string modulo;
        public string cedula;
        public string usuario;
        private int index = -1;
        private OdbcDataReader dsdata;
        private DataSet dataset = new DataSet();
        private string consult;

        public FrmFormPago(OdbcConnection conexion)
        {
            InitializeComponent();
            MyOdbcConet.MyOdbcConect(ref varini);
            msgsas = new Ayuda(varini.pstUsuario);
            this.mycon = conexion;
        }

        private void FrmFormPago_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                    break;
            }
        }

        private void FrmFormPago_Load(object sender, EventArgs e)
        {
            this.CenterToScreen();

            Initializacampos();

            string _efectivo = this.TxtEfectivo.Text;
            string _cheque = this.TxtCheque.Text;
            string _banco = "0";
            string _numCheque = "0";
            string _numCuenta = "0";
            string _debito = this.TxtDebito.Text;
            string _nroTarjDebito = this.txtNroTarjDebito.Text;
            string _credito = this.txtCredito.Text;
            string _nroTarjCredito = this.txtNroTarjCredito.Text;
            string _otros = "0";
            string _nro_otros = "0";
            string _vlrtitulo = "0";
            string _nrotitulo = "0";
            string _motivoPago = "0";

            VarIni.buscaFormapago(this.lblComprobante.Text.Trim(), Convert.ToDouble(this.LblConseComprobate.Text.Trim()),
                mycon, ref _efectivo, ref _cheque, ref _banco, ref _numCheque, ref _numCuenta,
                ref _debito, ref _nroTarjDebito, ref _credito, ref _nroTarjCredito,
                ref _otros, ref _nro_otros, ref _vlrtitulo, ref _nrotitulo, ref _motivoPago, ref dataset);

            this.TxtEfectivo.Text = _efectivo;
            this.TxtCheque.Text = _cheque;
            this.TxtDebito.Text = _debito;
            this.txtNroTarjDebito.Text = _nroTarjDebito;
            this.txtCredito.Text = _credito;
            this.txtNroTarjCredito.Text = _nroTarjCredito;

            // BuscarCompania with topelavado at param 43
            string _p1 = "", _p2 = "", _p3 = "", _p4 = "", _p5 = "", _p6 = "", _p7 = "", _p8 = "";
            string _p9 = "", _p10 = "", _p11 = "", _p12 = "", _p13 = "", _p14 = "", _p15 = "";
            string _p16 = "", _p17 = "", _p18 = "", _p19 = "", _p20 = "", _p21 = "", _p22 = "";
            string _p23 = "", _p24 = "", _p25 = "", _p26 = "", _p27 = "", _p28 = "", _p29 = "";
            string _p30 = "", _p31 = "", _p32 = "", _p33 = "", _p34 = "", _p35 = "", _p36 = "";
            string _p37 = "", _p38 = "", _p39 = "", _p40 = "", _p41 = "", _p42 = "", _p43 = "", _p44 = "", _p45 = "", _p46 = "";
            double _topelavado = 0;

            // this.msgparsys.BuscarCompania(varini.sptCodEmpr, this.mycon, // ERROR: CS7036
                // ref _p1, ref _p2, ref _p3, ref _p4, ref _p5, ref _p6, ref _p7, ref _p8, // ERROR: CS7036
                // ref _p9, ref _p10, ref _p11, ref _p12, ref _p13, ref _p14, ref _p15, // ERROR: CS7036
                // ref _p16, ref _p17, ref _p18, ref _p19, ref _p20, ref _p21, ref _p22, // ERROR: CS7036
                // ref _p23, ref _p24, ref _p25, ref _p26, ref _p27, ref _p28, ref _p29, // ERROR: CS7036
                // ref _p30, ref _p31, ref _p32, ref _p33, ref _p34, ref _p35, ref _p36, // ERROR: CS7036
                // ref _p37, ref _p38, ref _p39, ref _p40, ref _p41, ref _topelavado, ref _p43, ref _p44,ref _p45,ref _p46); // ERROR: CS7036
            topelavado = _topelavado;

            if (Convert.ToDouble(this.LblvalorApagar.Text) <= 0)
            {
                switch (this.modulo)
                {
                    case "cop":
                    case "dep":
                        // this.LblvalorApagar.Text = this.msgcop.ValorReciboCaja(this.lblComprobante.Text, Convert.ToDouble(this.LblConseComprobate.Text), this.mycon).ToString(); // ERROR: CS1061
                        break;
                }
                this.LblvalorApagar.Text = Strings.FormatNumber(Convert.ToDouble(this.LblvalorApagar.Text), 2);
            }

            if (TxtEfectivo.Text == "" || TxtEfectivo.Text.Trim() == "0")
            {
                TxtEfectivo.Text = this.LblvalorApagar.Text;
            }

            if (this.LblTotalPago.Text == "0")
            {
                Initializacampos();
            }

            this.TxtEfectivo.Focus();
            this.Timer1.Start();
        }

        private void Initializacampos()
        {
            this.TxtCheque.Text = "0";
            this.txtCredito.Text = "0";
            this.TxtDebito.Text = "0";
            this.txtNroTarjCredito.Text = null;
            this.txtNroTarjDebito.Text = null;
            this.LblTotalPago.Text = "0";
            this.LblPendiente.Text = "0";
            LimpiarCheque();
            this.DgvCheque.Rows.Clear();
        }

        public bool CargaFormapago(string comprobante, double ConseComprobante, double ValPagar,
            string Codigoter, string Modulo, Form Parent, string usuario)
        {
            FrmFormPago FormaPag = new FrmFormPago(mycon);
            FormaPag.lblComprobante.Text = comprobante;
            FormaPag.LblConseComprobate.Text = ConseComprobante.ToString();
            FormaPag.LblvalorApagar.Text = ValPagar.ToString("N");
            FormaPag.modulo = Modulo;
            FormaPag.cedula = Codigoter;
            FormaPag.usuario = usuario;
            if (FormaPag.ShowDialog(Parent) == DialogResult.OK)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool CargaFormapago(string comprobante, double ConseComprobante, double ValPagar,
            string Codigoter, string Modulo, Form Parent)
        {
            return CargaFormapago(comprobante, ConseComprobante, ValPagar, Codigoter, Modulo, Parent, "");
        }

        private void TotalPago()
        {
            double VlrTotPag = 0;
            double VlrPenPag = 0;
            VlrTotPag = Convert.ToDouble(this.TxtEfectivo.Text) + Convert.ToDouble(this.TxtDebito.Text)
                + Convert.ToDouble(this.txtCredito.Text) + Convert.ToDouble(TxtCheque.Text)
                + Convert.ToDouble(this.TxtTitulo.Text);
            VlrPenPag = Convert.ToDouble(this.LblvalorApagar.Text) - VlrTotPag;
            this.LblTotalPago.Text = VlrTotPag.ToString("N");
            this.LblPendiente.Text = VlrPenPag.ToString("N");
        }

        private bool ValidarCampos()
        {
            double total = 0;

            if (!Information.IsNumeric(this.txtCredito.Text))
            {
                this.txtCredito.Text = "0";
            }

            if (!Information.IsNumeric(this.TxtDebito.Text))
            {
                this.TxtDebito.Text = "0";
            }

            if (!Information.IsNumeric(this.LblTotalPago.Text))
            {
                this.LblTotalPago.Text = "0";
            }

            if (!Information.IsNumeric(this.LblPendiente.Text))
            {
                this.LblPendiente.Text = "0";
            }

            if (!Information.IsNumeric(this.TxtCheque.Text))
            {
                this.TxtCheque.Text = "0";
            }
            if (!Information.IsNumeric(this.TxtEfectivo.Text))
            {
                this.TxtEfectivo.Text = "0";
            }
            if (!Information.IsNumeric(this.TxtTitulo.Text))
            {
                this.TxtTitulo.Text = "0";
            }
            if (Convert.ToInt32(this.TxtTitulo.Text) != 0)
            {
                if (this.TxtNoTitulo.Text.Trim() == "")
                {
                    MessageBox.Show("Debe digitar un numero de titulo.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtNoTitulo.Focus();
                    return false;
                }
                if (this.CbxMotivo.Text.Trim() == "")
                {
                    MessageBox.Show("Debe escoger un motivo de pago.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.CbxMotivo.Focus();
                    return false;
                }
            }
            else
            {
                this.CbxMotivo.SelectedIndex = 0;
            }
            if (Convert.ToDouble(this.TxtDebito.Text) != 0)
            {
                if (this.txtNroTarjDebito.Text.Trim() == "")
                {
                    MessageBox.Show("Debe digitar el numero de tarjeta debito.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.txtNroTarjDebito.Focus();
                    return false;
                }
            }
            if (Convert.ToDouble(this.txtCredito.Text) != 0)
            {
                if (this.txtNroTarjCredito.Text.Trim() == "")
                {
                    MessageBox.Show("Debe digitar el numero de tarjeta credito.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.txtNroTarjCredito.Focus();
                    return false;
                }
            }
            // Codigo nuevo jorman 23/9/8
            total = Convert.ToDouble(this.TxtEfectivo.Text) + Convert.ToDouble(this.TxtDebito.Text)
                + Convert.ToDouble(this.TxtCheque.Text) + Convert.ToDouble(this.txtCredito.Text)
                + Convert.ToDouble(this.TxtTitulo.Text);
            if (total < Convert.ToDouble(this.LblvalorApagar.Text))
            {
                MessageBox.Show("El valor de su pago es inferior al valor a pagar", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtEfectivo.Focus();
                return false;
            }
            // final codigo nuevo
            return true;
        }

        private void aceptar_Click(object sender, EventArgs e)
        {
            bool ok = ValidarCampos();
            if (ok)
            {
                TotalPago();

                MsgBoxResult okk = MsgBoxResult.Yes;
                if (okk == MsgBoxResult.No)
                {
                    this.TxtEfectivo.Focus();
                    return;
                }
                GrabaPago();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        private void SacarDiferencia()
        {
            double total = 0;
            double diferencia = 0;

            total = Convert.ToDouble(this.TxtEfectivo.Text) + Convert.ToDouble(this.TxtDebito.Text)
                + Convert.ToDouble(this.TxtCheque.Text) + Convert.ToDouble(this.txtCredito.Text)
                + Convert.ToDouble(this.TxtTitulo.Text);
            diferencia = total - Convert.ToDouble(this.LblvalorApagar.Text);
            if (diferencia != 0)
            {
                if (Convert.ToDouble(this.TxtEfectivo.Text) != 0)
                {
                    if (Convert.ToDouble(this.TxtEfectivo.Text) >= diferencia)
                    {
                        this.TxtEfectivo.Text = (Convert.ToDouble(this.TxtEfectivo.Text) - diferencia).ToString();
                        diferencia = 0;
                    }
                    else
                    {
                        diferencia = diferencia - Convert.ToDouble(this.TxtEfectivo.Text);
                        this.TxtEfectivo.Text = "0";
                    }
                }
                if (diferencia != 0)
                {
                    if (Convert.ToDouble(this.TxtCheque.Text) != 0)
                    {
                        if (Convert.ToDouble(this.TxtCheque.Text) >= diferencia)
                        {
                            this.TxtCheque.Text = (Convert.ToDouble(this.TxtCheque.Text) - diferencia).ToString();
                            diferencia = 0;
                        }
                        else
                        {
                            diferencia = diferencia - Convert.ToDouble(this.TxtCheque.Text);
                            this.TxtCheque.Text = "0";
                        }
                    }
                }
                if (diferencia != 0)
                {
                    if (Convert.ToDouble(this.TxtDebito.Text) != 0)
                    {
                        if (Convert.ToDouble(this.TxtDebito.Text) >= diferencia)
                        {
                            this.TxtDebito.Text = (Convert.ToDouble(this.TxtDebito.Text) - diferencia).ToString();
                            diferencia = 0;
                        }
                        else
                        {
                            diferencia = diferencia - Convert.ToDouble(this.TxtDebito.Text);
                            this.TxtDebito.Text = "0";
                        }
                    }
                }
                if (diferencia != 0)
                {
                    if (Convert.ToDouble(this.txtCredito.Text) != 0)
                    {
                        if (Convert.ToDouble(this.txtCredito.Text) >= diferencia)
                        {
                            this.txtCredito.Text = (Convert.ToDouble(this.txtCredito.Text) - diferencia).ToString();
                            diferencia = 0;
                        }
                        else
                        {
                            diferencia = diferencia - Convert.ToDouble(this.txtCredito.Text);
                            this.txtCredito.Text = "0";
                        }
                    }
                }
                if (diferencia != 0)
                {
                    if (Convert.ToDouble(this.TxtTitulo.Text) != 0)
                    {
                        if (Convert.ToDouble(this.TxtTitulo.Text) >= diferencia)
                        {
                            this.TxtTitulo.Text = (Convert.ToDouble(this.TxtTitulo.Text) - diferencia).ToString();
                            diferencia = 0;
                        }
                        else
                        {
                            diferencia = diferencia - Convert.ToDouble(this.TxtTitulo.Text);
                            this.TxtTitulo.Text = "0";
                        }
                    }
                }
            }
        }

        private void GrabaPago()
        {
            SacarDiferencia();
            VarIni.GrabaFormapago(
                this.lblComprobante.Text.Trim(),
                Convert.ToDouble(this.LblConseComprobate.Text.Trim()),
                Convert.ToDouble(this.TxtEfectivo.Text.Trim()),
                Convert.ToDouble(this.TxtCheque.Text.Trim()),
                "0", "0", "0",
                Convert.ToDouble(this.TxtDebito.Text.Trim()),
                this.txtNroTarjDebito.Text.Trim(),
                Convert.ToDouble(this.txtCredito.Text.Trim()),
                this.txtNroTarjCredito.Text.Trim(),
                0, " ", 
                Convert.ToDouble(this.TxtTitulo.Text),
                this.TxtNoTitulo.Text,
                this.CbxMotivo.SelectedIndex.ToString(),
                this.mycon
            );

            if (dataset.Tables["forpago_Cheq"].Rows.Count > 0)
            {
                VarIni.GrabaFormapagoCheque(
                    this.lblComprobante.Text.Trim(),
                    Convert.ToDouble(this.LblConseComprobate.Text.Trim()),
                    this.dataset,
                    this.mycon,
                    usuario
                );
            }

            if (this.modulo == "cop")
            {
                if (Convert.ToDouble(this.TxtEfectivo.Text) != 0)
                {
                    if (Convert.ToDouble(this.TxtEfectivo.Text) >= topelavado)
                    {
                        this.msgcop.ValidarLavadoActivos(this.cedula, this.lblComprobante.Text,
                            Convert.ToDouble(this.LblConseComprobate.Text), "1", Convert.ToDouble(this.TxtEfectivo.Text), this,
                            this.usuario, this.modulo, this.mycon);
                    }
                }
            }
        }

        private void TxtEfectivo_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtEfectivo.Text))
            {
                this.TotalPago();
                this.TxtEfectivo.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtEfectivo.Text), 2);
            }
            else
            {
                this.TxtEfectivo.Text = "0";
            }
        }

        private void TxtEfectivo_TextChanged(object sender, EventArgs e)
        {
        }

        private void TxtDebito_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDebito.Text))
            {
                this.TotalPago();
                this.TxtDebito.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtDebito.Text), 2);
            }
            else
            {
                this.TxtDebito.Text = "0";
            }
        }

        private void TxtDebito_TextChanged(object sender, EventArgs e)
        {
        }

        private void txtCredito_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.txtCredito.Text))
            {
                this.TotalPago();
                this.txtCredito.Text = Strings.FormatNumber(Convert.ToDouble(this.txtCredito.Text), 2);
            }
            else
            {
                this.txtCredito.Text = "0";
            }
        }

        private void txtCredito_TextChanged(object sender, EventArgs e)
        {
        }

        private void cmbsalir_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void TxtTitulo_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtTitulo.Text))
            {
                this.TotalPago();
                this.TxtTitulo.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtTitulo.Text), 2);
            }
        }

        private void TxtCheque_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtCheque.Text))
            {
                this.TotalPago();
                this.TxtCheque.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtCheque.Text), 2);
            }
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (LblCerrando.Tag != null && LblCerrando.Tag.ToString() == "1")
            {
                cuentatiempo += 1;
                if (LblCerrando.Visible == false)
                {
                    LblCerrando.Visible = true;
                }
                else
                {
                    LblCerrando.Visible = false;
                }
                if (cuentatiempo == 8)
                {
                    this.Timer1.Interval = 500;
                }
                else if (cuentatiempo == 12)
                {
                    LblCerrando.Visible = true;
                    this.Timer1.Stop();
                }
            }
            else
            {
                if (LblCerrando.Visible == false) LblCerrando.Visible = true;
            }
        }

        // Link Agregar Cheque muestra el panel de cheques
        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.PnlDatosCheque.Visible = true;
            LimpiarCheque();
            if (dataset.Tables["forpago_Cheq"].Rows.Count > 0)
            {
                this.DgvCheque.AutoGenerateColumns = false;
                this.DgvCheque.DataSource = dataset.Tables["forpago_Cheq"];
            }
        }

        // Ayuda cheque panel
        private void BtnAyudaBanco_Click(object sender, EventArgs e)
        {
            this.TxtBancoChe.Text = msgsas.CargaAyuda("sys_banco03", "CODIGO_BANCO", "NOMBRE", "", this.mycon, this, "", "  ");
            this.TxtBancoChe.Focus();
        }

        // Dar formato al valor del cheque
        private void TxtValCheque_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtValCheque.Text))
            {
                this.TxtValCheque.Text = Strings.FormatNumber(Convert.ToDouble(this.TxtValCheque.Text), 2);
            }
        }

        // Agregar datos del cheque al DataGridView
        private void BtnAgrDgv_Click(object sender, EventArgs e)
        {
            if (ValidarCheque())
            {
                if (index >= 0)
                {
                    this.DgvCheque.Rows.RemoveAt(index);
                    index = -1;
                }
                this.dataset.Tables["forpago_Cheq"].Rows.Add(this.TxtValCheque.Text, this.TxtChequeNum.Text.Trim(), this.TxtCuentache.Text, this.TxtBancoChe.Text);
                LimpiarCheque();
                this.DgvCheque.AutoGenerateColumns = false;
                this.DgvCheque.DataSource = this.dataset.Tables["forpago_Cheq"];
                this.DgvCheque.Refresh();
            }
        }

        // Validar campos del cheque
        private bool ValidarCheque()
        {
            bool Ok = msgconfigCop.BuscaBanco(this.TxtBancoChe.Text, this.mycon);
            if (!Ok)
            {
                MessageBox.Show("Banco no existe, Por favor intente de nuevo", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.TxtBancoChe.Focus();
                return Ok;
            }
            if (Convert.ToDouble(this.TxtValCheque.Text) != 0)
            {
                if (Convert.ToDouble(this.TxtChequeNum.Text) == 0)
                {
                    MessageBox.Show("Debe digitar un numero de cheque diferente a cero.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtChequeNum.Text = "0";
                    this.TxtChequeNum.Focus();
                    return false;
                }
                if (this.TxtCuentache.Text.Trim() == "")
                {
                    MessageBox.Show("Debe digitar un numero de cuenta.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtCuentache.Focus();
                    return false;
                }
                if (this.TxtBancoChe.Text.Trim() == "" || this.TxtBancoChe.Text.Trim() == "9999")
                {
                    MessageBox.Show("Debe digitar un codigo de banco.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.TxtBancoChe.Focus();
                    return false;
                }
            }
            return true;
        }

        // Inicializar campos del panel de cheques
        private void LimpiarCheque()
        {
            this.TxtValCheque.Text = "0";
            this.TxtChequeNum.Text = null;
            this.TxtCuentache.Text = null;
            this.TxtBancoChe.Text = "9999";
        }

        // Eliminar Fila del dataGridView
        private void BtnRestDgv_Click(object sender, EventArgs e)
        {
            if (this.DgvCheque.RowCount > 0)
            {
                this.DgvCheque.Rows.RemoveAt(this.DgvCheque.CurrentRow.Index);
            }
        }

        // Salir del panel de cheques
        private void BtnSalir_Click(object sender, EventArgs e)
        {
            double suma = 0;
            int i = 0;
            this.PnlDatosCheque.Visible = false;
            if (this.DgvCheque.RowCount > 0)
            {
                while (i < DgvCheque.RowCount)
                {
                    suma = suma + Convert.ToDouble(DgvCheque[0, i].Value);
                    i = i + 1;
                }
                this.TxtCheque.Text = Strings.FormatNumber(suma, 2);
                this.TotalPago();
            }
            else
            {
                this.TxtCheque.Text = "0";
                this.TotalPago();
            }

            this.PnlDatosCheque.Visible = false;
            this.DgvCheque.DataSource = null;
        }

        // Cargar datos del DataGridView a los campos del panel para ser editados
        private void DgwReferencias_DoubleClick(object sender, EventArgs e)
        {
            if (this.DgvCheque.RowCount > 0)
            {
                index = this.DgvCheque.CurrentRow.Index;
                this.TxtValCheque.Text = this.DgvCheque[0, index].Value.ToString();
                this.TxtChequeNum.Text = this.DgvCheque[1, index].Value.ToString();
                this.TxtCuentache.Text = this.DgvCheque[2, index].Value.ToString();
                this.TxtBancoChe.Text = this.DgvCheque[3, index].Value.ToString();
            }
        }

        private void TxtValCheque_MouseUp(object sender, MouseEventArgs e)
        {
            this.ToolTip.SetToolTip(TxtValCheque, "Valor pendiente: " + LblPendiente.Text);
        }
    }
}
