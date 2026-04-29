using System;

namespace ERP.Core.Compartido.Utilidades
{
    public class Numeros_A_Letras
    {
        /// <summary>
        /// VB Right() equivalent: returns the rightmost n characters, or the whole string if shorter.
        /// </summary>
        private static string Right(string s, int length)
        {
            if (s == null) return "";
            if (length >= s.Length) return s;
            return s.Substring(s.Length - length);
        }

        public string Num_a_Letras(double Numero, bool tasaint = false)
        {
            string res1;
            string P_dec;
            string dec;
            string n = Numero.ToString("N2");
            if (Numero != 0)
            {
                int lon = n.Length - 3;

                string P_ent = "";
                if (lon > 0)
                {
                    P_ent = Convert.ToDouble(n.Substring(0, lon)).ToString();
                }
                else
                {
                    P_ent = "";
                }

                P_dec = n.Substring(lon + 1, 2);
                lon = P_ent.Length;
                if (lon <= 10)
                {
                    res1 = (P_ent == "") ? "" : EnLetras(P_ent).ToUpper();
                    dec = EnLetras(P_dec);
                    if (dec != "")
                    {
                        if (tasaint == true)
                        {
                            res1 = (res1 + "punto " + dec.ToUpper()).ToUpper();
                        }
                        else
                        {
                            if (Right(res1, 10).Trim() == "MILLONES" || Right(res1, 8).Trim() == "MILLON")
                            {
                                res1 = (res1 + "de pesos con " + dec.ToUpper() + "centavos ").ToUpper();
                            }
                            else
                            {
                                res1 = (((res1 != "") ? res1 + "pesos con " : "") + dec.ToUpper() + "centavos ").ToUpper();
                            }
                        }
                    }
                    else
                    {
                        if (tasaint == true)
                        {
                            res1 = res1.ToUpper();
                        }
                        else
                        {
                            if (Right(res1, 10).Trim() == "MILLONES" || Right(res1, 8).Trim() == "MILLON")
                            {
                                res1 = (res1 + "de pesos ").ToUpper();
                            }
                            else
                            {
                                res1 = (res1 + "pesos ").ToUpper();
                            }
                        }
                    }
                    return res1;
                }
            }
            else
            {
                P_dec = n.Substring(1, 2);
                dec = EnLetras(P_dec);
                if (dec != "")
                {
                    if (tasaint == true)
                    {
                        res1 = ("punto " + dec.ToUpper()).ToUpper();
                    }
                    else
                    {
                        res1 = (dec.ToUpper() + "centavos ").ToUpper();
                    }
                }
                else
                {
                    res1 = "Cero Pesos ".ToUpper();
                }
                return res1;
            }
            return "";
        }

        private string EnLetras(string Numero)
        {
            int b;
            int paso;
            string mas;
            bool una = false;
            string expresion = "";
            string entero = "";
            string deci = "";
            string flag;
            mas = "N";
            flag = "N";
            bool Dedocea15 = false;
            for (paso = 1; paso <= Numero.Length; paso++)
            {
                if (Numero.Substring(paso - 1, 1) == ".")
                {
                    flag = "S";
                }
                else
                {
                    if (flag == "N")
                    {
                        entero = entero + Numero.Substring(paso - 1, 1);
                    }
                    else
                    {
                        deci = deci + Numero.Substring(paso - 1, 1);
                    }
                }
            }
            Numero = (Numero == "") ? "0" : Numero;
            if (deci.Length == 1)
            {
                // deci = deci; // & "0"
            }
            bool fin = false;
            string VlrMilTmp;
            flag = "N";
            double NumeroVal = Convert.ToDouble(Numero);
            long intNumero = (long)Math.Floor(NumeroVal);
            if (intNumero >= -9999999999L && intNumero <= 9999999999L)
            {
                if (entero.Length == 10)
                {
                    mas = "S";
                    switch (entero.Substring(0, 1))
                    {
                        case "1":
                            if (flag == "N")
                            {
                                expresion = expresion + "un mil ";
                            }
                            break;
                        case "2":
                            if (flag == "N")
                            {
                                expresion = expresion + "dos mil ";
                            }
                            break;
                        case "3":
                            if (flag == "N")
                            {
                                expresion = expresion + "tres mil ";
                            }
                            break;
                        case "4":
                            if (flag == "N")
                            {
                                expresion = expresion + "cuatro mil ";
                            }
                            break;
                        case "5":
                            if (flag == "N")
                            {
                                expresion = expresion + "cinco mil ";
                            }
                            break;
                        case "6":
                            if (flag == "N")
                            {
                                expresion = expresion + "seis mil ";
                            }
                            break;
                        case "7":
                            if (flag == "N")
                            {
                                expresion = expresion + "siete mil ";
                            }
                            break;
                        case "8":
                            if (flag == "N")
                            {
                                expresion = expresion + "ocho mil ";
                            }
                            break;
                        case "9":
                            if (flag == "N")
                            {
                                expresion = expresion + "nueve mil ";
                            }
                            break;
                    }
                    entero = Right(entero, entero.Length - 1);
                }

                for (paso = entero.Length; paso >= 1; paso--)
                {
                    b = entero.Length - (paso - 1);
                    switch (paso)
                    {
                        case 3:
                        case 6:
                        case 9:
                            switch (entero.Substring(b - 1, 1))
                            {
                                case "1":
                                    if (entero.Substring(b, 1) == "0" && entero.Substring(b + 1, 1) == "0")
                                    {
                                        expresion = expresion + "cien ";
                                    }
                                    else
                                    {
                                        expresion = expresion + "ciento ";
                                        flag = "N";
                                    }
                                    break;
                                case "2":
                                    expresion = expresion + "doscientos ";
                                    flag = "N";
                                    break;
                                case "3":
                                    expresion = expresion + "trescientos ";
                                    flag = "N";
                                    break;
                                case "4":
                                    expresion = expresion + "cuatrocientos ";
                                    flag = "N";
                                    break;
                                case "5":
                                    expresion = expresion + "quinientos ";
                                    flag = "N";
                                    break;
                                case "6":
                                    expresion = expresion + "seiscientos ";
                                    flag = "N";
                                    break;
                                case "7":
                                    expresion = expresion + "setecientos ";
                                    flag = "N";
                                    break;
                                case "8":
                                    expresion = expresion + "ochocientos ";
                                    flag = "N";
                                    break;
                                case "9":
                                    expresion = expresion + "novecientos ";
                                    flag = "N";
                                    break;
                            }
                            break;

                        case 2:
                        case 5:
                        case 8:
                            switch (entero.Substring(b - 1, 1))
                            {
                                case "1":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        flag = "N";
                                        expresion = expresion + "diez ";
                                    }
                                    if (entero.Substring(b, 1) == "1")
                                    {
                                        flag = "S";
                                        expresion = expresion + "once ";
                                        if (paso == 2)
                                        {
                                            fin = true;
                                            goto label1;
                                        }
                                    }
                                    if (entero.Substring(b, 1) == "2")
                                    {
                                        flag = "S";
                                        expresion = expresion + "doce ";
                                        if (paso == 2)
                                        {
                                            fin = true;
                                            goto label1;
                                        }
                                        if (paso == 5)
                                        {
                                            Dedocea15 = true;
                                        }
                                    }
                                    if (entero.Substring(b, 1) == "3")
                                    {
                                        flag = "S";
                                        expresion = expresion + "trece ";
                                        if (paso == 2)
                                        {
                                            fin = true;
                                            goto label1;
                                        }
                                        if (paso == 5)
                                        {
                                            Dedocea15 = true;
                                        }
                                    }
                                    if (entero.Substring(b, 1) == "4")
                                    {
                                        flag = "S";
                                        expresion = expresion + "catorce ";
                                        if (paso == 2)
                                        {
                                            fin = true;
                                            goto label1;
                                        }
                                        if (paso == 5)
                                        {
                                            Dedocea15 = true;
                                        }
                                    }
                                    if (entero.Substring(b, 1) == "5")
                                    {
                                        flag = "S";
                                        expresion = expresion + "quince ";
                                        if (paso == 2)
                                        {
                                            fin = true;
                                            goto label1;
                                        }
                                        if (paso == 5)
                                        {
                                            Dedocea15 = true;
                                        }
                                    }
                                    if (string.Compare(entero.Substring(b, 1), "5") > 0)
                                    {
                                        flag = "N";
                                        expresion = expresion + "dieci";
                                    }
                                    break;
                                case "2":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "veinte ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "veinti";
                                        flag = "N";
                                    }
                                    break;
                                case "3":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "treinta ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "treinta y ";
                                        flag = "N";
                                    }
                                    break;
                                case "4":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "cuarenta ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "cuarenta y ";
                                        flag = "N";
                                    }
                                    break;
                                case "5":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "cincuenta ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "cincuenta y ";
                                        flag = "N";
                                    }
                                    break;
                                case "6":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "sesenta ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "sesenta y ";
                                        flag = "N";
                                    }
                                    break;
                                case "7":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "setenta ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "setenta y ";
                                        flag = "N";
                                    }
                                    break;
                                case "8":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "ochenta ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "ochenta y ";
                                        flag = "N";
                                    }
                                    break;
                                case "9":
                                    if (entero.Substring(b, 1) == "0")
                                    {
                                        expresion = expresion + "noventa ";
                                        flag = "S";
                                    }
                                    else
                                    {
                                        expresion = expresion + "noventa y ";
                                        flag = "N";
                                    }
                                    break;
                            }
                            break;

                        case 1:
                        case 4:
                        case 7:
                            VlrMilTmp = Right(entero, 6);
                            switch (entero.Substring(b - 1, 1))
                            {
                                case "1":
                                    if (flag == "N")
                                    {
                                        if (paso == 1)
                                        {
                                            if (entero.Length == 1)
                                            {
                                                expresion = expresion + "un ";
                                            }
                                            else
                                            {
                                                if (entero.Substring(b - 1, 1) == "1" && entero.Substring(b - 2, 1) == "0")
                                                {
                                                    expresion = expresion + "un ";
                                                }
                                                else if (entero.Substring(b - 1, 1) == "1" && entero.Substring(b - 2, 1) != "0")
                                                {
                                                    expresion = expresion + "uno ";
                                                }
                                            }
                                        }
                                        else
                                        {
                                            expresion = expresion + "un ";
                                        }
                                    }
                                    else
                                    {
                                        if (paso == 1)
                                        {
                                            if (entero.Substring(entero.Length - 1, 1) == "1")
                                            {
                                                if (entero.Length == 1)
                                                {
                                                    expresion = expresion + "un ";
                                                }
                                                else
                                                {
                                                    expresion = expresion + "uno ";
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case "2":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "dos ";
                                    }
                                    else
                                    {
                                        if (Dedocea15 == false)
                                        {
                                            if (b == entero.Length - 1)
                                            {
                                                expresion = expresion + "dos ";
                                            }
                                            else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                            {
                                                expresion = expresion + "dos ";
                                            }
                                            else if (paso == 1)
                                            {
                                                if (entero.Substring(entero.Length - 1, 1) == "2")
                                                {
                                                    expresion = expresion + "dos ";
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Dedocea15 = false;
                                        }
                                    }
                                    break;
                                case "3":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "tres ";
                                    }
                                    else
                                    {
                                        if (Dedocea15 == false)
                                        {
                                            if (b == entero.Length)
                                            {
                                                expresion = expresion + "tres ";
                                            }
                                            else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                            {
                                                expresion = expresion + "tres ";
                                            }
                                        }
                                        else
                                        {
                                            Dedocea15 = false;
                                        }
                                    }
                                    break;
                                case "4":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "cuatro ";
                                    }
                                    else
                                    {
                                        if (Dedocea15 == false)
                                        {
                                            if (b == entero.Length)
                                            {
                                                expresion = expresion + "cuatro ";
                                            }
                                            else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                            {
                                                expresion = expresion + "cuatro ";
                                            }
                                        }
                                        else
                                        {
                                            Dedocea15 = false;
                                        }
                                    }
                                    break;
                                case "5":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "cinco ";
                                    }
                                    else
                                    {
                                        if (Dedocea15 == false)
                                        {
                                            if (b == entero.Length)
                                            {
                                                expresion = expresion + "cinco ";
                                            }
                                            else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                            {
                                                expresion = expresion + "cinco ";
                                            }
                                        }
                                        else
                                        {
                                            Dedocea15 = false;
                                        }
                                    }
                                    break;
                                case "6":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "seis ";
                                    }
                                    else
                                    {
                                        if (b == entero.Length)
                                        {
                                            expresion = expresion + "seis ";
                                        }
                                        else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                        {
                                            expresion = expresion + "seis ";
                                        }
                                    }
                                    break;
                                case "7":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "siete ";
                                    }
                                    else
                                    {
                                        if (paso == 1)
                                        {
                                            expresion = expresion + "siete ";
                                        }
                                        else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                        {
                                            expresion = expresion + "siete ";
                                        }
                                    }
                                    break;
                                case "8":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "ocho ";
                                    }
                                    else
                                    {
                                        if (b == entero.Length)
                                        {
                                            expresion = expresion + "ocho ";
                                        }
                                        else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                        {
                                            expresion = expresion + "ocho ";
                                        }
                                    }
                                    break;
                                case "9":
                                    if (flag == "N")
                                    {
                                        expresion = expresion + "nueve ";
                                    }
                                    else
                                    {
                                        if (b == entero.Length)
                                        {
                                            expresion = expresion + "nueve ";
                                        }
                                        else if (paso == 4 && VlrMilTmp.Length >= 3 && (VlrMilTmp.Substring(0, 1) != "0" || VlrMilTmp.Substring(1, 1) != "0" || VlrMilTmp.Substring(2, 1) != "0"))
                                        {
                                            expresion = expresion + "nueve ";
                                        }
                                    }
                                    break;
                            }
                            break;
                    }
                label1:
                    if (paso == 4)
                    {
                        string fra = Right(entero, 6);
                        if (fra.Length >= 3 && (fra.Substring(0, 1) != "0" || fra.Substring(1, 1) != "0" || fra.Substring(2, 1) != "0"))
                        {
                            expresion = expresion + "mil ";
                        }
                    }
                    if (paso == 7)
                    {
                        if (mas == "S")
                        {
                            if (una == false)
                            {
                                expresion = expresion + "millones ";
                                una = true;
                            }
                        }
                        else
                        {
                            string fra = Right(entero, 9);
                            if (entero.Substring(0, 1) == "1" && entero.Length == 7)
                            {
                                expresion = expresion + "millon ";
                            }
                            else
                            {
                                expresion = expresion + "millones ";
                            }
                        }
                    }

                    if (fin == true)
                    {
                        break;
                    }
                }

                string EnLetrasResult;
                if (deci != "")
                {
                    if (entero.Substring(0, 1) == "-")
                    {
                        EnLetrasResult = "menos " + expresion + "con " + deci;
                    }
                    else
                    {
                        EnLetrasResult = expresion + "con " + deci;
                    }
                }
                else
                {
                    if (entero.Substring(0, 1) == "-")
                    {
                        if (expresion.Length >= 8 && expresion.Substring(0, 8).Trim() == "millones")
                        {
                            expresion = expresion.Substring(8);
                        }
                        EnLetrasResult = "menos " + expresion;
                    }
                    else
                    {
                        EnLetrasResult = expresion;
                    }
                }
                return EnLetrasResult;
            }
            else
            {
                return "";
            }
        }
    }
}
