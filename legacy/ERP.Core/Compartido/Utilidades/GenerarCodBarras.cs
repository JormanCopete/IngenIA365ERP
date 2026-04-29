using System;

namespace ERP.Core.Compartido.Utilidades
{
    public class GenerarCodBarras
    {
        private string lcDataToPrint;
        private string lcOnlyCorrectData;
        private int lnStringLength;
        private int lnweightedTotal;
        private int lnWeightValue;
        private string lcChars;
        private string lcCurrentChar;
        private char lcStartCode;
        private char lcStopCode;
        private char lcFnc1;
        private char lcC128_CheckDigit;
        private int lnCurrentValue;
        private int lnCheckDigitValue;
        private string lcPrintable_string;

        public string generar(string Cadena)
        {
            this.lcDataToPrint = "";
            Cadena = Cadena.Trim();
            Cadena = Cadena.Replace("(", "");
            Cadena = Cadena.Replace(")", "");

            // Check to make sure data is numeric or "FA" and remove all others.
            this.lcOnlyCorrectData = "";
            this.lnStringLength = Cadena.Length;
            for (int I = 1; I <= this.lnStringLength; I += 2)
            {
                // Add all numbers and "FA" to OnlyCorrectData string
                this.lcChars = Cadena.Substring(I - 1, 2);
                if (Microsoft.VisualBasic.Information.IsNumeric(this.lcChars))
                {
                    this.lcOnlyCorrectData = this.lcOnlyCorrectData + Cadena.Substring(I - 1, 2);
                }
                if (this.lcChars == "FA")
                {
                    this.lcOnlyCorrectData = this.lcOnlyCorrectData + Cadena.Substring(I - 1, 2);
                }
            }
            Cadena = this.lcOnlyCorrectData;

            // Assign start, stop and lcFnc1 codes
            this.lcStartCode = (char)205;
            this.lcStopCode = (char)206;
            this.lcFnc1 = (char)202;

            // <<<< Calculate Modulo 103 Check Digit and generate lcDataToPrint >>>>
            // Set lnweightedTotal to the Code 128 value of the start character + lcFnc1
            this.lnweightedTotal = 105 + 102;
            this.lnWeightValue = 2;
            this.lnStringLength = Cadena.Length;
            for (int I = 1; I <= this.lnStringLength; I += 2)
            {
                // Get the value of each number pair
                this.lcCurrentChar = Cadena.Substring(I - 1, 2);
                // get the lcDataToPrint
                if (this.lcCurrentChar == "FA")
                {
                    this.lcDataToPrint = this.lcDataToPrint + (char)202;
                    // multiply by the weighting character
                    this.lnCurrentValue = 102 * this.lnWeightValue;
                }
                else
                {
                    // set the Integer lnCurrentValue to the number of String lcCurrentChar
                    this.lnCurrentValue = (int)Microsoft.VisualBasic.Conversion.Val(this.lcCurrentChar);
                    this.lcDataToPrint = this.lcDataToPrint + (char)(this.lnCurrentValue == 0 ? 194 :
                        (this.lnCurrentValue > 94 ? this.lnCurrentValue + 100 : this.lnCurrentValue + 32));
                    // multiply by the weighting character
                    this.lnCurrentValue = this.lnCurrentValue * this.lnWeightValue;
                }
                // add the values together to get the weighted total
                this.lnweightedTotal = this.lnweightedTotal + this.lnCurrentValue;
                this.lnWeightValue = this.lnWeightValue + 1;
            }

            // divide the lnweightedTotal by 103 and get the remainder, this is the lnCheckDigitValue
            this.lnCheckDigitValue = this.lnweightedTotal % 103;
            // Now that we have the lnCheckDigitValue, find the corresponding ASCII character from the table
            this.lcC128_CheckDigit = (char)(this.lnCheckDigitValue == 0 ? 194 :
                (this.lnCheckDigitValue > 94 ? this.lnCheckDigitValue + 100 : this.lnCheckDigitValue + 32));

            this.lcPrintable_string = this.lcStartCode.ToString() + this.lcFnc1.ToString() +
                this.lcDataToPrint + this.lcC128_CheckDigit.ToString() + this.lcStopCode.ToString() + " ";
            // Return PrintableString
            return this.lcPrintable_string;
        }
    }
}
