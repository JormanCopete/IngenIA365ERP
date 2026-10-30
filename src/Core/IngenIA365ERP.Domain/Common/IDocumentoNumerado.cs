namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Un documento de Recaudo o de la Tesorería nueva que recibe su número al final de la operación, cuando ya se tomaron los cerrojos (feature 015, R-13;
/// corrida del 2026-10-26). Lo numera sólo su numerador (<c>NumeradorDeRecaudo.NumerarAsync</c>, <c>NumeradorDeTesoreria.NumerarAsync</c>): quien llama no
/// escribe <c>Number</c> (<c>SoloElNumeradorNumera</c>). Un documento que nace con el número en su inicializador no la necesita. (nuevo)
/// </summary>
public interface IDocumentoNumerado
{
    string Number { get; set; }
}
