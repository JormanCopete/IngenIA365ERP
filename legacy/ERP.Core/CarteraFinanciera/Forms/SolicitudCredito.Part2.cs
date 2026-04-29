// Traduccion de: SolicitudCredito.vb (msgliqcre) -- Parte 2
using System;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace ERP.Core.CarteraFinanciera.Forms
{
    public partial class SolicitudCredito
    {
        // --- TxtSalarioCode1_LostFocus ---
        private void TxtSalarioCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSalarioCode1.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod1.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode1.Text) + Convert.ToDouble(TxtIngVarCode1.Text)) * 0.08;
                    TxtDstoParaFisCod1.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtSalarioCode1.Text = "0";
            }
        }

        // --- TxtSalarioCode2_LostFocus ---
        private void TxtSalarioCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSalarioCode2.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod2.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode2.Text) + Convert.ToDouble(TxtIngVarCode2.Text)) * 0.08;
                    TxtDstoParaFisCod2.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtSalarioCode2.Text = "0";
            }
        }

        // --- TxtSalarioCode3_LostFocus ---
        private void TxtSalarioCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSalarioCode3.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod3.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode3.Text) + Convert.ToDouble(TxtIngVarCode3.Text)) * 0.08;
                    TxtDstoParaFisCod3.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtSalarioCode3.Text = "0";
            }
        }

        // --- TxtSalarioCode4_LostFocus ---
        private void TxtSalarioCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSalarioCode4.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod4.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode4.Text) + Convert.ToDouble(TxtIngVarCode4.Text)) * 0.08;
                    TxtDstoParaFisCod4.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtSalarioCode4.Text = "0";
            }
        }

        // --- TxtOtroIngCode LostFocus handlers ---
        private void TxtOtroIngCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroIngCode1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtOtroIngCode1.Text = "0";
            }
        }

        private void TxtOtroIngCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroIngCode2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtOtroIngCode2.Text = "0";
            }
        }

        private void TxtOtroIngCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroIngCode3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtOtroIngCode3.Text = "0";
            }
        }

        private void TxtOtroIngCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroIngCode4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtOtroIngCode4.Text = "0";
            }
        }

        // --- TxtIngArrCode LostFocus handlers ---
        private void TxtIngArrCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngArrCode1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtIngArrCode1.Text = "0";
            }
        }

        private void TxtIngArrCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngArrCode2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtIngArrCode2.Text = "0";
            }
        }

        private void TxtIngArrCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngArrCode3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtIngArrCode3.Text = "0";
            }
        }

        private void TxtIngArrCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngArrCode4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtIngArrCode4.Text = "0";
            }
        }

        // --- TxtIngVarCode LostFocus handlers ---
        private void TxtIngVarCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngVarCode1.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod1.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode1.Text) + Convert.ToDouble(TxtIngVarCode1.Text)) * 0.08;
                    TxtDstoParaFisCod1.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtIngVarCode1.Text = "0";
            }
        }

        private void TxtIngVarCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngVarCode2.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod2.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode2.Text) + Convert.ToDouble(TxtIngVarCode2.Text)) * 0.08;
                    TxtDstoParaFisCod2.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtIngVarCode2.Text = "0";
            }
        }

        private void TxtIngVarCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngVarCode3.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod3.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode3.Text) + Convert.ToDouble(TxtIngVarCode3.Text)) * 0.08;
                    TxtDstoParaFisCod3.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtIngVarCode3.Text = "0";
            }
        }

        private void TxtIngVarCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtIngVarCode4.Text) == true)
            {
                if (Convert.ToDouble(TxtDstoParaFisCod4.Text) == 0)
                {
                    double sumaParafiscalesVal;
                    sumaParafiscalesVal = (Convert.ToDouble(TxtSalarioCode4.Text) + Convert.ToDouble(TxtIngVarCode4.Text)) * 0.08;
                    TxtDstoParaFisCod4.Text = Convert.ToDouble(sumaParafiscalesVal).ToString("N0");
                }
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtIngVarCode4.Text = "0";
            }
        }

        // --- TxtPensionesCod LostFocus handlers ---
        private void TxtPensionesCod1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPensionesCod1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtPensionesCod1.Text = "0";
            }
        }

        private void TxtPensionesCod2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPensionesCod2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtPensionesCod2.Text = "0";
            }
        }

        private void TxtPensionesCod3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPensionesCod3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtPensionesCod3.Text = "0";
            }
        }

        private void TxtPensionesCod4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPensionesCod4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtPensionesCod4.Text = "0";
            }
        }

        // --- TxtDstoEmpCode LostFocus handlers ---
        private void TxtDstoEmpCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCode1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtDstoEmpCode1.Text = "0";
            }
        }

        private void TxtDstoEmpCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCode2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtDstoEmpCode2.Text = "0";
            }
        }

        private void TxtDstoEmpCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCode3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtDstoEmpCode3.Text = "0";
            }
        }

        private void TxtDstoEmpCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCode4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtDstoEmpCode4.Text = "0";
            }
        }

        // --- TxtDeuTerCode LostFocus handlers ---
        private void TxtDeuTerCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDeuTerCode1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtDeuTerCode1.Text = "0";
            }
        }

        private void TxtDeuTerCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDeuTerCode2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtDeuTerCode2.Text = "0";
            }
        }

        private void TxtDeuTerCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDeuTerCode3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtDeuTerCode3.Text = "0";
            }
        }

        private void TxtDeuTerCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDeuTerCode4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtDeuTerCode4.Text = "0";
            }
        }

        // --- TxtOtroDstocode LostFocus handlers ---
        private void TxtOtroDstocode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroDstocode1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtOtroDstocode1.Text = "0";
            }
        }

        private void TxtOtroDstocode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroDstocode2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtOtroDstocode2.Text = "0";
            }
        }

        private void TxtOtroDstocode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroDstocode3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtOtroDstocode3.Text = "0";
            }
        }

        private void TxtOtroDstocode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtOtroDstocode4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtOtroDstocode4.Text = "0";
            }
        }

        // --- TxtDstoPensionCod LostFocus handlers ---
        private void TxtDstoPensionCod1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoPensionCod1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtDstoPensionCod1.Text = "0";
            }
        }

        private void TxtDstoPensionCod2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoPensionCod2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtDstoPensionCod2.Text = "0";
            }
        }

        private void TxtDstoPensionCod3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoPensionCod3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtDstoPensionCod3.Text = "0";
            }
        }

        private void TxtDstoPensionCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoPensionCod4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtDstoPensionCod4.Text = "0";
            }
        }

        // --- TxtDstoParaFisCod LostFocus handlers ---
        private void TxtDstoParaFisCod1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoParaFisCod1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtDstoParaFisCod1.Text = "0";
            }
        }

        private void TxtDstoParaFisCod2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoParaFisCod2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtDstoParaFisCod2.Text = "0";
            }
        }

        private void TxtDstoParaFisCod3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoParaFisCod3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtDstoParaFisCod3.Text = "0";
            }
        }

        private void TxtDstoParaFisCod4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoParaFisCod4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtDstoParaFisCod4.Text = "0";
            }
        }

        // --- txtGastoperCod LostFocus + CheckedChanged handlers ---
        private void txtGastoperCod1_LostFocus(object sender, EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.chkGastoperCod1.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.txtGastoperCod1.Text))
                    {
                        if (Convert.ToDouble(this.txtGastoperCod1.Text) >= 0 && Convert.ToDouble(this.txtGastoperCod1.Text) <= 100)
                        {
                            // gastospersonales = CDbl(Me.TxtSalario.Text) * (CDbl(Me.TxtGastosPnales.Text) / 100)
                        }
                        else
                        {
                            MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            gastospersonales = 0;
                            this.txtGastoperCod1.Text = "0";
                            this.txtGastoperCod1.Focus();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Los gastos personales deben ser un porcentaje.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        gastospersonales = 0;
                        this.txtGastoperCod1.Text = "0";
                        this.txtGastoperCod1.Focus();
                    }
                    break;
                case false:
                    this.txtGastoperCod1.Text = Convert.ToDouble(this.txtGastoperCod1.Text).ToString("N0");
                    break;
            }
            CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
        }

        private void chkGastoperCod1_CheckedChanged(object sender, EventArgs e)
        {
            txtGastoperCod1_LostFocus(null, null);
        }

        private void txtGastoperCod2_LostFocus(object sender, EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.chkGastoperCod2.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.txtGastoperCod2.Text))
                    {
                        if (Convert.ToDouble(this.txtGastoperCod2.Text) >= 0 && Convert.ToDouble(this.txtGastoperCod2.Text) <= 100)
                        {
                            //gastospersonales = CDbl(Me.TxtSalario.Text) * (CDbl(Me.TxtGastosPnales.Text) / 100)
                        }
                        else
                        {
                            MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            gastospersonales = 0;
                            this.txtGastoperCod2.Text = "0";
                            this.txtGastoperCod2.Focus();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Los gastos personales deben ser un porcentaje.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        gastospersonales = 0;
                        this.txtGastoperCod2.Text = "0";
                        this.txtGastoperCod2.Focus();
                    }
                    break;
                case false:
                    this.txtGastoperCod2.Text = Convert.ToDouble(this.txtGastoperCod2.Text).ToString("N0");
                    break;
            }
            CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
        }

        private void chkGastoperCod2_CheckedChanged(object sender, EventArgs e)
        {
            txtGastoperCod2_LostFocus(null, null);
        }

        private void txtGastoperCod3_LostFocus(object sender, EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.chkGastoperCod3.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.txtGastoperCod3.Text))
                    {
                        if (Convert.ToDouble(this.txtGastoperCod3.Text) >= 0 && Convert.ToDouble(this.txtGastoperCod3.Text) <= 100)
                        {
                            //gastospersonales = CDbl(Me.TxtSalario.Text) * (CDbl(Me.TxtGastosPnales.Text) / 100)
                        }
                        else
                        {
                            MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            gastospersonales = 0;
                            this.txtGastoperCod3.Text = "0";
                            this.txtGastoperCod3.Focus();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Los gastos personales deben ser un porcentaje.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        gastospersonales = 0;
                        this.txtGastoperCod3.Text = "0";
                        this.txtGastoperCod3.Focus();
                    }
                    break;
                case false:
                    this.txtGastoperCod3.Text = Convert.ToDouble(this.txtGastoperCod3.Text).ToString("N0");
                    break;
            }
            CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
        }

        private void chkGastoperCod3_CheckedChanged(object sender, EventArgs e)
        {
            txtGastoperCod3_LostFocus(null, null);
        }

        private void txtGastoperCod4_LostFocus(object sender, EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.chkGastoperCod4.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.txtGastoperCod4.Text))
                    {
                        if (Convert.ToDouble(this.txtGastoperCod4.Text) >= 0 && Convert.ToDouble(this.txtGastoperCod4.Text) <= 100)
                        {
                            //gastospersonales = CDbl(Me.TxtSalario.Text) * (CDbl(Me.TxtGastosPnales.Text) / 100)
                        }
                        else
                        {
                            MessageBox.Show("Los gastos personales no deben ser mayor a 100 ni menores a 0", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            gastospersonales = 0;
                            this.txtGastoperCod4.Text = "0";
                            this.txtGastoperCod4.Focus();
                        }
                    }
                    else
                    {
                        MessageBox.Show("Los gastos personales deben ser un porcentaje.", "SOLIDO", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        gastospersonales = 0;
                        this.txtGastoperCod4.Text = "0";
                        this.txtGastoperCod4.Focus();
                    }
                    break;
                case false:
                    this.txtGastoperCod4.Text = Convert.ToDouble(this.txtGastoperCod4.Text).ToString("N0");
                    break;
            }
            CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
        }

        private void chkGastoperCod4_CheckedChanged(object sender, EventArgs e)
        {
            txtGastoperCod4_LostFocus(null, null);
        }

        // --- TxtDstoEmpCajaCode LostFocus handlers ---
        private void TxtDstoEmpCajaCode1_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCajaCode1.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode1, this.TxtOtroIngCode1, this.TxtIngArrCode1, this.TxtIngVarCode1, this.TxtDstoEmpCode1, this.TxtDeuTerCode1, this.TxtOtroDstocode1, this.LblDispCode1, this.TxtPensionesCod1, this.TxtDstoPensionCod1, this.TxtDstoParaFisCod1, lblTotalIngresocode1, lblTotalEgresocode1, chkGastoperCod1, txtGastoperCod1, TxtDstoEmpCajaCode1);
            }
            else
            {
                this.TxtDstoEmpCajaCode1.Text = "0";
            }
        }

        private void TxtDstoEmpCajaCode2_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCajaCode2.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode2, this.TxtOtroIngCode2, this.TxtIngArrCode2, this.TxtIngVarCode2, this.TxtDstoEmpCode2, this.TxtDeuTerCode2, this.TxtOtroDstocode2, this.LblDispCode2, this.TxtPensionesCod2, this.TxtDstoPensionCod2, this.TxtDstoParaFisCod2, lblTotalIngresocode2, lblTotalEgresocode2, chkGastoperCod2, txtGastoperCod2, TxtDstoEmpCajaCode2);
            }
            else
            {
                this.TxtDstoEmpCajaCode2.Text = "0";
            }
        }

        private void TxtDstoEmpCajaCode3_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCajaCode3.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode3, this.TxtOtroIngCode3, this.TxtIngArrCode3, this.TxtIngVarCode3, this.TxtDstoEmpCode3, this.TxtDeuTerCode3, this.TxtOtroDstocode3, this.LblDispCode3, this.TxtPensionesCod3, this.TxtDstoPensionCod3, this.TxtDstoParaFisCod3, lblTotalIngresocode3, lblTotalEgresocode3, chkGastoperCod3, txtGastoperCod3, TxtDstoEmpCajaCode3);
            }
            else
            {
                this.TxtDstoEmpCajaCode3.Text = "0";
            }
        }

        private void TxtDstoEmpCajaCode4_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtDstoEmpCajaCode4.Text) == true)
            {
                CalculaDisponibleMesCodeudor(this.TxtSalarioCode4, this.TxtOtroIngCode4, this.TxtIngArrCode4, this.TxtIngVarCode4, this.TxtDstoEmpCode4, this.TxtDeuTerCode4, this.TxtOtroDstocode4, this.LblDispCode4, this.TxtPensionesCod4, this.TxtDstoPensionCod4, this.TxtDstoParaFisCod4, lblTotalIngresocode4, lblTotalEgresocode4, chkGastoperCod4, txtGastoperCod4, TxtDstoEmpCajaCode4);
            }
            else
            {
                this.TxtDstoEmpCajaCode4.Text = "0";
            }
        }

        // --- BtnDatosCodeudor_Click ---
        private void BtnDatosCodeudor_Click(object sender, EventArgs e)
        {
            if (this.TxtCodeudor1.Text.Trim() == "" && this.TxtCodeudor2.Text.Trim() == "" && this.TxtCodeudor3.Text.Trim() == "" && this.TxtCodeudor4.Text.Trim() == "")
            {
                this.PnlCodeudores.Visible = false;
            }
            else
            {
                this.PnlCodeudores.Visible = true;
                this.TxtSalarioCode1.Focus();
                TxtSalarioCode1_LostFocus(null, null);
                if (this.TxtCodeudor1.Text.Trim() != "")
                {
                    if (this.TxtCodeu1.Text.Trim() != this.TxtCodeudor1.Text.Trim())
                    {
                        this.TxtOtroIngCode1.Text = "0"; this.TxtIngArrCode1.Text = "0"; this.TxtIngVarCode1.Text = "0"; this.TxtDeuTerCode1.Text = "0"; this.TxtOtroDstocode1.Text = "0"; this.LblDispCode1.Text = "0";
                        this.TxtPensionesCod1.Text = "0"; this.TxtDstoPensionCod1.Text = "0"; this.TxtDstoParaFisCod1.Text = "0";
                        this.chkGastoperCod1.Checked = false;
                        this.txtGastoperCod1.Text = "0";
                    }
                    this.TxtCodeu1.Text = this.TxtCodeudor1.Text;
                    this.LblNomCodeu1.Text = this.TxtNomCodeudor1.Text;
                }
                TxtSalarioCode2_LostFocus(null, null);
                if (this.TxtCodeudor2.Text.Trim() != "")
                {
                    if (this.TxtCodeu2.Text.Trim() != this.TxtCodeudor2.Text.Trim())
                    {
                        this.TxtOtroIngCode2.Text = "0"; this.TxtIngArrCode2.Text = "0"; this.TxtIngVarCode2.Text = "0"; this.TxtDeuTerCode2.Text = "0"; this.TxtOtroDstocode2.Text = "0"; this.LblDispCode2.Text = "0";
                        this.TxtPensionesCod2.Text = "0"; this.TxtDstoPensionCod2.Text = "0"; this.TxtDstoParaFisCod2.Text = "0";
                        this.chkGastoperCod2.Checked = false;
                        this.txtGastoperCod2.Text = "0";
                    }
                    this.TxtCodeu2.Text = this.TxtCodeudor2.Text;
                    this.LblNomCodeu2.Text = this.TxtNomCodeudor2.Text;
                }
                TxtSalarioCode3_LostFocus(null, null);
                if (this.TxtCodeudor3.Text.Trim() != "")
                {
                    if (this.TxtCodeu3.Text.Trim() != this.TxtCodeudor3.Text.Trim())
                    {
                        this.TxtOtroIngCode3.Text = "0"; this.TxtIngArrCode3.Text = "0"; this.TxtIngVarCode3.Text = "0"; this.TxtDeuTerCode3.Text = "0"; this.TxtOtroDstocode3.Text = "0"; this.LblDispCode3.Text = "0";
                        this.TxtPensionesCod3.Text = "0"; this.TxtDstoPensionCod3.Text = "0"; this.TxtDstoParaFisCod3.Text = "0";
                        this.chkGastoperCod3.Checked = false;
                        this.txtGastoperCod3.Text = "0";
                    }
                    this.TxtCodeu3.Text = this.TxtCodeudor3.Text;
                    this.LblNomCodeu3.Text = this.TxtNomCodeudor3.Text;
                }
                TxtSalarioCode4_LostFocus(null, null);
                if (this.TxtCodeudor4.Text.Trim() != "")
                {
                    if (this.TxtCodeu4.Text.Trim() != this.TxtCodeudor4.Text.Trim())
                    {
                        this.TxtOtroIngCode4.Text = "0"; this.TxtIngArrCode4.Text = "0"; this.TxtIngVarCode4.Text = "0"; this.TxtDeuTerCode4.Text = "0"; this.TxtOtroDstocode4.Text = "0"; this.LblDispCode4.Text = "0";
                        this.TxtPensionesCod4.Text = "0"; this.TxtDstoPensionCod4.Text = "0"; this.TxtDstoParaFisCod4.Text = "0";
                        this.chkGastoperCod4.Checked = false;
                        this.txtGastoperCod4.Text = "0";
                    }
                    this.TxtCodeu4.Text = this.TxtCodeudor4.Text;
                    this.LblNomCodeu4.Text = this.TxtNomCodeudor4.Text;
                }
            }
        }

        // --- BtnBienes_Click ---
        private void BtnBienes_Click(object sender, EventArgs e)
        {
            CargarVentanaBienes();
        }

        // --- CargarVentanaBienes ---
        private void CargarVentanaBienes()
        {
            DataSet dsraices = new DataSet();
            DataSet dsvehiculos = new DataSet();
            double Raices = 0;
            double vehiculo = 0;
            int i = 0;
            if (dsbienes.Tables.Count == 0)
            {
                dsraices.Tables.Add("tblnada");
                dsvehiculos.Tables.Add("tblnada");
            }
            else
            {
                dsraices.Tables.Add(dsbienes.Tables["tblBienesRaices"].Copy());
                dsvehiculos.Tables.Add(dsbienes.Tables["tblVehiculo"].Copy());
            }
            dsbienes = this.msgliqcre.CargaBienes(this.empresappl.Text, dsraices, dsvehiculos, this, this.mycon, this.TxtCodigoter.Text);

            if (dsbienes.Tables.Contains("tblBienesRaices") == true)
            {
                if (dsbienes.Tables["tblBienesRaices"].Rows.Count > 0)
                {
                    while (i < dsbienes.Tables["tblBienesRaices"].Rows.Count)
                    {
                        DataRow row = dsbienes.Tables["tblBienesRaices"].Rows[i];
                        Raices += Convert.ToDouble(row["VALOR"]);
                        i += 1;
                    }
                }
            }
            i = 0;
            if (dsbienes.Tables.Contains("tblVehiculo") == true)
            {
                if (dsbienes.Tables["tblVehiculo"].Rows.Count > 0)
                {
                    while (i < dsbienes.Tables["tblVehiculo"].Rows.Count)
                    {
                        DataRow row = dsbienes.Tables["tblVehiculo"].Rows[i];
                        vehiculo += Convert.ToDouble(row["VALOR"]);
                        i += 1;
                    }
                }
            }
            if (Raices > 0)
            {
                this.TxtSolActVivienda.Text = Raices.ToString();
            }
            if (vehiculo > 0)
            {
                this.TxtSolActVehiculo.Text = vehiculo.ToString();
            }

            this.TxtSolActVivienda.Text = Convert.ToDouble(this.TxtSolActVivienda.Text).ToString("N0");
            this.TxtSolActVehiculo.Text = Convert.ToDouble(this.TxtSolActVehiculo.Text).ToString("N0");
            TxtSolActVivienda_LostFocus(null, null);
            TxtSolActVehiculo_LostFocus(null, null);
        }

        // --- CargarVentanaReferencia ---
        private void CargarVentanaReferencia()
        {
            DataSet dsdataRef = new DataSet();
            if (this.DsReferencia.Tables.Count != 0)
            {
                dsdataRef.Tables.Add(DsReferencia.Tables["TblReferencia"].Copy());
            }
            DsReferencia = this.msgparcop.CargarVentanaReferencias(this.TxtCodigoter.Text, this.empresappl.Text, this, dsdataRef, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.TiposReferencia.Todas);
        }

        // --- CargarVentanaParViv ---
        private void CargarVentanaParViv()
        {
            DataSet dsdat = new DataSet();
            if (dsParViv.Tables.Count != 0)
            {
                dsdat.Tables.Add(dsParViv.Tables["tblsolparviv"].Copy());
            }
            else
            {
                dsdat.Tables.Add("tblnada");
            }
            dsParViv = this.msgliqcre.CargarVentanaSolParviv(dsdat, this.empresappl.Text, this, this.mycon);
        }

        // --- BtnReferencia_Click ---
        private void BtnReferencia_Click(object sender, EventArgs e)
        {
            CargarVentanaReferencia();
        }

        // --- BtnDatosVivienda_Click ---
        private void BtnDatosVivienda_Click(object sender, EventArgs e)
        {
            CargarVentanaParViv();
        }

        // --- imprimelibra_Click ---
        private void imprimelibra_Click(object sender, EventArgs e)
        {
            ERP.Core.CarteraFinanciera.Reportes.ImpreDoc ImpLibPagare = new ERP.Core.CarteraFinanciera.Reportes.ImpreDoc(this.usuario.Text);
            //ImpLibPagare.imp_libran_pagare(this.LblNumSolicitud.Text, mycon, "", "", this.txtLincred.Text, this.TxtCodigoter.Text);
        }

        // --- ChkIngreSolanterior_CheckedChanged ---
        private void ChkIngreSolanterior_CheckedChanged(object sender, EventArgs e)
        {
            if (this.ChkIngreSolanterior.Checked == true)
            {
                ActualizaIngresos(1);
            }
            else if (this.ChkIngreSolanterior.Checked == false)
            {
                ActualizaIngresos(2);
            }
        }

        // --- ActualizaIngresos ---
        private void ActualizaIngresos(int opcion)
        {
            string cappagoPorcentajeLocal = "";
            DataRow row = datasolante.Tables["solanterior"].Rows[0];
            switch (opcion)
            {
                case 1:
                    this.TxtOtrosIngresos.Text = row["otro_ingreso"].ToString();
                    this.TxtIngVariables.Text = row["ingvariables"].ToString();
                    this.TxtIngArriendos.Text = row["ingarriendos"].ToString();
                    this.TxtPensiones.Text = row["ingpension"].ToString();
                    this.TxtDstoParafiscales.Text = row["dstoparafiscales"].ToString();
                    this.TxtDstoPension.Text = row["dstopension"].ToString();
                    this.TxtDeudasTerceros.Text = row["deudasterceros"].ToString();
                    this.TxtGastosMes.Text = row["gasto_fijo_mes"].ToString();
                    this.txtIngresoConyuge.Text = row["ingConyuge"].ToString();
                    this.TxtGastosPnales.Text = row["dstoGastosPerso"].ToString();
                    this.CbxRecDeudas.Text = row["cappagoPorcentaje"].ToString();
                    cappagoPorcentajeLocal = row["cappagoRecDeudas"].ToString();
                    switch (cappagoPorcentajeLocal)
                    {
                        case "Y":
                            ChkPorcentaje.Checked = true;
                            break;
                        case "N":
                            ChkPorcentaje.Checked = false;
                            break;
                    }
                    break;
                case 2:
                    this.TxtOtrosIngresos.Text = "0";
                    this.TxtIngVariables.Text = "0";
                    this.TxtIngArriendos.Text = "0";
                    this.TxtPensiones.Text = "0";
                    this.TxtDstoParafiscales.Text = "0";
                    this.TxtDstoPension.Text = "0";
                    this.TxtDeudasTerceros.Text = "0";
                    this.TxtGastosMes.Text = "0";
                    txtIngresoConyuge.Text = "0";
                    TxtGastosPnales.Text = "0";
                    break;
            }
        }

        // --- HelpCargo_Click ---
        private void HelpCargo_Click(object sender, EventArgs e)
        {
            this.TxtIdCargo.Text = this.msgparcop.HelpCargos(this.mycon, this);
            this.TxtIdCargo.Focus();
        }

        // --- TxtIdCargo_LostFocus ---
        private void TxtIdCargo_LostFocus(object sender, EventArgs e)
        {
            this.TxtCargo.Text = " ";
            if (!Information.IsNumeric(this.TxtIdCargo.Text))
            {
                this.TxtIdCargo.Text = "9999";
            }
            //msgparcop.BuscaCargos(this.TxtIdCargo.Text, mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref this.TxtCargo.Text);
        }

        // --- TxtCesantias_LostFocus ---
        private void TxtCesantias_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtCesantias.Text))
            {
                this.TxtCesantias.Text = Convert.ToDouble(this.TxtCesantias.Text).ToString("N2");
            }
            else
            {
                this.TxtCesantias.Text = "0";
            }
        }

        // --- SumaIngresos ---
        private void SumaIngresos()
        {
            double ingresos = 0;
            ingresos = Convert.ToDouble(this.TxtSalario.Text) + Convert.ToDouble(this.TxtOtrosIngresos.Text) + Convert.ToDouble(this.TxtIngArriendos.Text) + Convert.ToDouble(this.TxtIngVariables.Text) + Convert.ToDouble(this.TxtPensiones.Text) + Convert.ToDouble(txtIngresoConyuge.Text);
            this.LblTotIngresos.Text = ingresos.ToString("N0");

            DisponibleparaMes();
        }

        // --- SumaEgresos ---
        private void SumaEgresos()
        {
            double gastospersonal = 0;
            double deudas;
            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    gastospersonal = Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : (Convert.ToDouble(this.TxtSalario.Text) * (Convert.ToDouble(this.TxtGastosPnales.Text) / 100));
                    break;
                case false:
                    gastospersonal = Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : Convert.ToDouble(this.TxtGastosPnales.Text);
                    break;
            }
            deudas = (this.CbxRecDeudas.Text == "No") ? 0 : ((this.CbxRecDeudas.Text == "Si") ? Convert.ToDouble(lblCuotaRecogida.Text) : 0);

            double Egresos = 0;
            Egresos = Convert.ToDouble(this.TxtDesCajaEmpresa.Text) + Convert.ToDouble(this.TxtDesNomEmpresa.Text) + Convert.ToDouble(this.TxtDeudasTerceros.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtGastosMes.Text) + gastospersonal - deudas;
            this.LblTotEgresos.Text = Egresos.ToString("N0");
            DisponibleparaMes();
        }

        // --- sumaParafiscales ---
        private void sumaParafiscales()
        {
            double sumaParafiscalesVal;
            if (salario_compania > Convert.ToDouble(this.TxtSalario.Text))
            {
                sumaParafiscalesVal = (salario_compania + Convert.ToDouble(this.TxtIngVariables.Text)) * 0.08;
            }
            else
            {
                sumaParafiscalesVal = (Convert.ToDouble(this.TxtSalario.Text) + Convert.ToDouble(this.TxtIngVariables.Text)) * 0.08;
            }

            if (Invocado == false)
            {
                this.TxtDstoParafiscales.Text = sumaParafiscalesVal.ToString();
                this.TxtDstoParafiscales.Text = Convert.ToDouble(this.TxtDstoParafiscales.Text).ToString("N0");
            }
        }

        // --- DisponibleparaMes ---
        private void DisponibleparaMes()
        {
            double deudas = 0;
            deudas = (this.CbxRecDeudas.Text == "No") ? 0 : ((this.CbxRecDeudas.Text == "Si") ? this.CuotaRecogidaNomina : 0);

            this.TxtDisponibleMes.Text = (Convert.ToDouble(this.LblTotIngresos.Text) - Convert.ToDouble(this.LblTotEgresos.Text)).ToString("N2");

            double MedioSal = 0;
            double DeducionesNomina = 0;
            double IngresosNomina = 0;

            DeducionesNomina = Convert.ToDouble(this.TxtDesNomEmpresa.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtGastosMes.Text) - deudas;
            IngresosNomina = Convert.ToDouble(this.TxtSalario.Text) + Convert.ToDouble(this.TxtIngVariables.Text) + Convert.ToDouble(this.TxtPensiones.Text);

            this.lblCapacidaNomina.Text = (IngresosNomina - DeducionesNomina).ToString("N0");
            this.LblCapPago.Text = (Convert.ToDouble(this.LblTotIngresos.Text) - Convert.ToDouble(this.LblTotEgresos.Text)).ToString("N0");

            switch (TipoDscto)
            {
                case "0":
                    MedioSal = (Convert.ToDouble(this.TxtSalario.Text) * (PorDscto / 100));
                    break;
                case "1":
                    MedioSal = (Convert.ToDouble(this.TxtSalario.Text) - PorDscto);
                    break;
            }

            if (TipoAsociado == "1")
            {
                this.LblCaja.Text = this.LblCapPago.Text;
                this.LblNomina.Text = "0";
            }
            else
            {
                if (DeducionesNomina >= MedioSal)
                {
                    this.LblCaja.Text = this.LblCapPago.Text;
                    this.LblNomina.Text = "0";
                }
                else
                {
                    this.LblNomina.Text = (Convert.ToDouble(MedioSal) - Convert.ToDouble(DeducionesNomina)).ToString("N0");
                    this.LblCaja.Text = (Convert.ToDouble(this.LblCapPago.Text) - Convert.ToDouble(this.LblNomina.Text)).ToString("N0");
                }
            }

            double cuota_temp = 0;
            switch (ciclo)
            {
                case 5:
                    cuota_temp = Convert.ToDouble(TxtCuota.Text) * periodicidad;
                    break;
                default:
                    cuota_temp = Convert.ToDouble(TxtCuota.Text);
                    break;
            }

            lblCuotaNuevoCredito.Text = cuota_temp.ToString("N0");
            this.lblCapacDescuento.Text = (MedioSal - (DeducionesNomina + cuota_temp)).ToString("N0");

            double TxtSolPasDeudas;
            double TxtSolActAportes;
            double SaldoAohorroSuper = 0;
            double ValorDeudasRecogida;
            ListView Lstdeudasrec = new ListView();
            if (Convert.ToDouble(this.TxtSalario.Text) > 0)
            {
                TxtSolActAportes = msgliqcre.CalculaSaldoAportes(this.TxtCodigoter.Text, Convert.ToDateTime(LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon);
                TxtSolPasDeudas = msgliqcre.CalculaTotalDeuda(this.TxtCodigoter.Text, Convert.ToDateTime(LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, "N");

                SaldoAohorroSuper = msgliqcre.CalculaSaldoAhorrosEquisuper(this.TxtCodigoter.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon);
                if (this.LblNumSolicitud.Text.Trim() != "00000000")
                {
                    //ValorDeudasRecogida = msgliqcre.BuscaDeudasRecogidas(Convert.ToDouble(this.LblNumSolicitud.Text), Lstdeudasrec, this.mycon);
                }
                else
                {
                    ValorDeudasRecogida = this.ValorsaldoRecogida;
                }
                //this.LblDescubierto.Text = (((TxtSolPasDeudas + (Convert.ToDouble(TxtvalorSolicitud.Text) - ValorDeudasRecogida)) - (Convert.ToDouble(TxtSolActAportes) + SaldoAohorroSuper)) / Convert.ToDouble(this.TxtSalario.Text)).ToString("N3");
            }
            else
            {
                this.LblDescubierto.Text = "0";
            }
        }

        // --- HelpCiudad_Click ---
        private void HelpCiudad_Click(object sender, EventArgs e)
        {
            //this.TxtIdCiudad.Text = this.msgparcop.HelpCiudades(this.mycon, this);
            this.TxtIdCiudad.Focus();
        }

        // --- TxtIdCiudad_LostFocus ---
        private void TxtIdCiudad_LostFocus(object sender, EventArgs e)
        {
            this.TxtCiudad.Text = " ";
            if (!Information.IsNumeric(this.TxtIdCiudad.Text))
            {
                this.TxtIdCiudad.Text = "999999";
            }
            //this.msgparcop.BuscaCiudad(this.TxtIdCiudad.Text, this.mycon, ERP.Core.CarteraFinanciera.Models.ParamCop.Navega.Ninguno, ref this.TxtCiudad.Text);
        }

        // --- DisponibleNomina ---
        private void DisponibleNomina()
        {
            double IngresoNomina = 0;
            double EgresosNomina = 0;

            IngresoNomina = Convert.ToDouble(this.TxtSalario.Text) + Convert.ToDouble(this.TxtPensiones.Text) + Convert.ToDouble(this.TxtIngVariables.Text);
            EgresosNomina = Convert.ToDouble(this.TxtDesNomEmpresa.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtGastosMes.Text);
            this.LblDispNomina.Text = (IngresoNomina - EgresosNomina).ToString("N2");

            if (Convert.ToDouble(this.LblTotIngresos.Text) > 0)
            {
                this.LblNivelContingencia.Text = (((Convert.ToDouble(this.LblTotIngresos.Text) - Convert.ToDouble(this.LblTotEgresos.Text)) / Convert.ToDouble(this.LblTotIngresos.Text)) * 100).ToString();
            }
        }

        // --- LblDispNomina_Click (TextChanged) ---
        private void LblDispNomina_Click(object sender, EventArgs e)
        {
            if (LblDispNomina.Text.Trim() != "" && TxtSalario.Text.Trim() != "" && TxtIngVariables.Text.Trim() != "" && TxtPensiones.Text.Trim() != "" && LblDispNomina.Text.Trim() != "0")
            {
                if ((Convert.ToDouble(TxtSalario.Text.Trim()) + Convert.ToDouble(TxtIngVariables.Text.Trim()) + Convert.ToDouble(TxtPensiones.Text.Trim())) != 0)
                {
                    LblPorcNomina.Text = ((Convert.ToDouble(lblCapacidaNomina.Text.Trim()) / (Convert.ToDouble(TxtSalario.Text.Trim()) + Convert.ToDouble(TxtIngVariables.Text.Trim()) + Convert.ToDouble(TxtPensiones.Text.Trim()))) * 100).ToString("N2");
                }
                else
                {
                    LblPorcNomina.Text = "0";
                }
            }
        }

        // --- LblCapPago_Click (TextChanged) ---
        private void LblCapPago_Click(object sender, EventArgs e)
        {
            if (LblCapPago.Text.Trim() != "" && LblTotIngresos.Text.Trim() != "" && LblCapPago.Text.Trim() != "0")
            {
                if (Convert.ToDouble(LblTotIngresos.Text.Trim()) != 0)
                {
                    LblPorcCaja.Text = ((Convert.ToDouble(LblCapPago.Text.Trim()) / Convert.ToDouble(LblTotIngresos.Text.Trim())) * 100).ToString("N2");
                }
                else
                {
                    LblPorcCaja.Text = "0";
                }
            }
        }

        // --- CbxRecDeudas_Validated ---
        private void CbxRecDeudas_Validated(object sender, EventArgs e)
        {
            double gastospersonales = 0;
            switch (this.ChkPorcentaje.Checked)
            {
                case true:
                    if (Information.IsNumeric(this.TxtGastosPnales.Text))
                    {
                        gastospersonales = Convert.ToDouble(this.TxtSalario.Text) * (Convert.ToDouble(this.TxtGastosPnales.Text) / 100);
                    }
                    else
                    {
                        gastospersonales = 0;
                    }
                    break;
                case false:
                    gastospersonales = Information.IsNumeric(this.TxtGastosPnales.Text) == false ? 0 : Convert.ToDouble(this.TxtGastosPnales.Text);
                    break;
            }
            if (this.CbxRecDeudas.Text.Trim() != "")
            {
                if (this.CbxRecDeudas.Text == "No")
                {
                    this.LblTotEgresos.Text = (Convert.ToDouble(this.TxtDeudasTerceros.Text) + Convert.ToDouble(this.TxtDesCajaEmpresa.Text) + Convert.ToDouble(this.TxtDesNomEmpresa.Text) + Convert.ToDouble(this.TxtGastosMes.Text) + gastospersonales + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text)).ToString("N0");
                }
                else if (this.CbxRecDeudas.Text == "Si")
                {
                    this.lblCuotaRecogida.Text = ValorCuotaRecogida.ToString();
                    this.LblTotEgresos.Text = (Convert.ToDouble(this.TxtDeudasTerceros.Text) + Convert.ToDouble(this.TxtDesCajaEmpresa.Text) + Convert.ToDouble(this.TxtDesNomEmpresa.Text) + Convert.ToDouble(this.TxtGastosMes.Text) + gastospersonales + Convert.ToDouble(this.TxtDstoPension.Text) + Convert.ToDouble(this.TxtDstoParafiscales.Text) - Convert.ToDouble(this.lblCuotaRecogida.Text)).ToString("N0");
                }
                SumaIngresos();
            }
        }

        // --- Solvencia Activos LostFocus handlers ---
        private void TxtSolActVivienda_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActVivienda.Text) == true && Convert.ToDouble(this.TxtSolActVivienda.Text) >= 0)
            {
                this.TxtSolActVivienda.Text = Convert.ToDouble(this.TxtSolActVivienda.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActVivienda.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolActVehiculo_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActVehiculo.Text) == true && Convert.ToDouble(this.TxtSolActVehiculo.Text) >= 0)
            {
                this.TxtSolActVehiculo.Text = Convert.ToDouble(this.TxtSolActVehiculo.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActVehiculo.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolActOtros_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActOtros.Text) == true && Convert.ToDouble(this.TxtSolActOtros.Text) >= 0)
            {
                this.TxtSolActOtros.Text = Convert.ToDouble(this.TxtSolActOtros.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActOtros.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtSolActAhorros_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolActAhorros.Text) == true && Convert.ToDouble(this.TxtSolActAhorros.Text) >= 0)
            {
                this.TxtSolActAhorros.Text = Convert.ToDouble(this.TxtSolActAhorros.Text).ToString("N0");
            }
            else
            {
                this.TxtSolActAhorros.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtActCtaBanco_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtActCtaBanco.Text) == true && Convert.ToDouble(this.TxtActCtaBanco.Text) >= 0)
            {
                this.TxtActCtaBanco.Text = Convert.ToDouble(this.TxtActCtaBanco.Text).ToString("N0");
            }
            else
            {
                this.TxtActCtaBanco.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        private void TxtActCxC_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtActCxC.Text) == true && Convert.ToDouble(this.TxtActCxC.Text) >= 0)
            {
                this.TxtActCxC.Text = Convert.ToDouble(this.TxtActCxC.Text).ToString("N0");
            }
            else
            {
                this.TxtActCxC.Text = "0";
            }
            CalcularSolvenciaActivos();
        }

        // --- Solvencia Pasivos LostFocus handlers ---
        private void TxtSolPasOtros_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSolPasOtros.Text) == true && Convert.ToDouble(this.TxtSolPasOtros.Text) >= 0)
            {
                this.TxtSolPasOtros.Text = Convert.ToDouble(this.TxtSolPasOtros.Text).ToString("N0");
            }
            else
            {
                this.TxtSolPasOtros.Text = "0";
            }
            this.CalcularSolvenciaPasivos();
        }

        private void TxtPasObliBanca_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPasObliBanca.Text) == true && Convert.ToDouble(this.TxtPasObliBanca.Text) >= 0)
            {
                this.TxtPasObliBanca.Text = Convert.ToDouble(this.TxtPasObliBanca.Text).ToString("N0");
            }
            else
            {
                this.TxtPasObliBanca.Text = "0";
            }
            this.CalcularSolvenciaPasivos();
        }

        private void TxtPasObliHipot_LostFocus(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtPasObliHipot.Text) == true && Convert.ToDouble(this.TxtPasObliHipot.Text) >= 0)
            {
                this.TxtPasObliHipot.Text = Convert.ToDouble(this.TxtPasObliHipot.Text).ToString("N0");
            }
            else
            {
                this.TxtPasObliHipot.Text = "0";
            }
            this.CalcularSolvenciaPasivos();
        }

        // --- CalcularSolvenciaActivos ---
        private void CalcularSolvenciaActivos()
        {
            this.TxtSolActTotal.Text = (Convert.ToDouble(this.TxtSolActAporte.Text) + Convert.ToDouble(this.TxtSolActOtros.Text) + Convert.ToDouble(this.TxtSolActVehiculo.Text) + Convert.ToDouble(this.TxtSolActVivienda.Text) + Convert.ToDouble(this.TxtActCtaBanco.Text) + Convert.ToDouble(this.TxtActCxC.Text) + Convert.ToDouble(this.TxtSolActAhorros.Text)).ToString();
            this.TxtSolActTotal.Text = Convert.ToDouble(this.TxtSolActTotal.Text).ToString("N0");

            this.TxtSolPatrimonio.Text = (Convert.ToDouble(this.TxtSolActTotal.Text) - Convert.ToDouble(this.TxtSolPasTotal.Text)).ToString();
            this.TxtSolPatrimonio.Text = Convert.ToDouble(this.TxtSolPatrimonio.Text).ToString("N0");

            if (Convert.ToInt32(this.TxtSolActTotal.Text) == 0)
            {
                this.LblNivelEndeudamiento.Text = "0";
            }
            else
            {
                this.LblNivelEndeudamiento.Text = ((Convert.ToDouble(this.TxtSolPasTotal.Text) / Convert.ToDouble(this.TxtSolActTotal.Text)) * 100).ToString();
            }
            this.TxtSolPasTotPyP.Text = (Convert.ToDouble(this.TxtSolPasTotal.Text) + Convert.ToDouble(this.TxtSolPatrimonio.Text)).ToString("N0");
            CalcularSolvenciaPasivos();
        }

        // --- CalcularSolvenciaPasivos ---
        private void CalcularSolvenciaPasivos()
        {
            this.TxtSolPasTotal.Text = (Convert.ToDouble(this.TxtSolPasDeuda.Text) + Convert.ToDouble(this.TxtSolPasOtros.Text) + Convert.ToDouble(this.TxtPasObliBanca.Text) + Convert.ToDouble(this.TxtPasObliHipot.Text)).ToString();
            this.TxtSolPasTotal.Text = Convert.ToDouble(this.TxtSolPasTotal.Text).ToString("N0");

            this.TxtSolPatrimonio.Text = (Convert.ToDouble(this.TxtSolActTotal.Text) - Convert.ToDouble(this.TxtSolPasTotal.Text)).ToString();
            this.TxtSolPatrimonio.Text = Convert.ToDouble(this.TxtSolPatrimonio.Text).ToString("N0");

            this.TxtSolPasTotPyP.Text = (Convert.ToDouble(this.TxtSolPasTotal.Text) + Convert.ToDouble(this.TxtSolPatrimonio.Text)).ToString("N0");
            if (Convert.ToInt32(this.TxtSolActTotal.Text) == 0)
            {
                this.LblNivelEndeudamiento.Text = "0";
            }
            else
            {
                this.LblNivelEndeudamiento.Text = ((Convert.ToDouble(this.TxtSolPasTotal.Text) / Convert.ToDouble(this.TxtSolActTotal.Text)) * 100).ToString();
            }
            formatearSolvenciaPasivos();
        }

        // --- formatearSolvenciaPasivos ---
        private void formatearSolvenciaPasivos()
        {
            this.TxtSolPasDeuda.Text = Convert.ToDouble(this.TxtSolPasDeuda.Text).ToString("N0");
            this.TxtSolPasOtros.Text = Convert.ToDouble(this.TxtSolPasOtros.Text).ToString("N0");
            this.TxtPasObliBanca.Text = Convert.ToDouble(this.TxtPasObliBanca.Text).ToString("N0");
            this.TxtPasObliHipot.Text = Convert.ToDouble(this.TxtPasObliHipot.Text).ToString("N0");
            this.TxtSolActAporte.Text = Convert.ToDouble(this.TxtSolActAporte.Text).ToString("N0");
            this.TxtSolActVivienda.Text = Convert.ToDouble(this.TxtSolActVivienda.Text).ToString("N0");
            this.TxtSolActVehiculo.Text = Convert.ToDouble(this.TxtSolActVehiculo.Text).ToString("N0");
            this.TxtActCtaBanco.Text = Convert.ToDouble(this.TxtActCtaBanco.Text).ToString("N0");
            this.TxtActCxC.Text = Convert.ToDouble(this.TxtActCxC.Text).ToString("N0");
            this.TxtSolActOtros.Text = Convert.ToDouble(this.TxtSolActOtros.Text).ToString("N0");
        }

        // --- CargarDatosSolvenciaActivosPasivos ---
        private void CargarDatosSolvenciaActivosPasivos()
        {
            this.TxtSolActAporte.Text = msgliqcre.CalculaSaldoAportes(this.TxtCodigoter.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon).ToString();
            this.TxtSolPasDeuda.Text = msgliqcre.CalculaTotalDeuda(this.TxtCodigoter.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon, "N").ToString();
            this.TxtSolActAhorros.Text = msgliqcre.CalculaSaldoAhorros(this.TxtCodigoter.Text, Convert.ToDateTime(this.LblFecSolicitud.Text).ToString("yyyyMM"), this.mycon).ToString();

            if (Information.IsNumeric(this.TxtSolActAporte.Text) == true)
            {
                CalcularSolvenciaActivos();
            }
            if (Information.IsNumeric(this.TxtSolActAhorros.Text) == true)
            {
                CalcularSolvenciaActivos();
            }
            if (Information.IsNumeric(this.TxtSolPasDeuda.Text) == true)
            {
                CalcularSolvenciaPasivos();
            }
        }

        // --- CalculaSeguridadSocialToolStripMenuItem_Click ---
        private void CalculaSeguridadSocialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            double sumaParafiscalesVal;

            if (salario_compania > Convert.ToDouble(this.TxtSalario.Text))
            {
                sumaParafiscalesVal = (salario_compania + Convert.ToDouble(this.TxtIngVariables.Text)) * 0.08;
            }
            else
            {
                sumaParafiscalesVal = (Convert.ToDouble(this.TxtSalario.Text) + Convert.ToDouble(this.TxtIngVariables.Text)) * 0.08;
            }

            this.TxtDstoParafiscales.Text = sumaParafiscalesVal.ToString();
            this.TxtDstoParafiscales.Text = Convert.ToDouble(this.TxtDstoParafiscales.Text).ToString("N0");
            SumaEgresos();
        }

        // --- ChkPorcentaje_CheckedChanged ---
        private void ChkPorcentaje_CheckedChanged(object sender, EventArgs e)
        {
            TxtGastosPnales_LostFocus(null, null);
            CalculaDisponibleMes();
        }

        // --- lblPorcnDescuento_TextChanged ---
        private void lblPorcnDescuento_TextChanged(object sender, EventArgs e)
        {
            if (Information.IsNumeric(this.TxtSalario.Text) == true && Information.IsNumeric(lblCapacDescuento.Text) == true)
            {
                if (Convert.ToDouble(this.TxtSalario.Text) > 0)
                {
                    lblPorcnDescuento.Text = (PorDscto - ((Convert.ToDouble(lblCapacDescuento.Text) / Convert.ToDouble(this.TxtSalario.Text)) * 100)).ToString("N2");
                }
                else
                {
                    lblPorcnDescuento.Text = "0";
                }
            }
        }

        // --- Label155_Click ---
        private void Label155_Click(object sender, EventArgs e)
        {
        }
    }
}
