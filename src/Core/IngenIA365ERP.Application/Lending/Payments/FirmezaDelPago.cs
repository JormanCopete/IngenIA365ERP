using System.Text.Json;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// La <b>firmeza</b> de un pago (feature 013, US5, T0540; FR-082, D23; interfaces §3.14): <c>FinalityDate</c> = fecha valor + los días hábiles
/// de su clase de medio en <c>Pagos.FirmezaDiasPorClaseDeMedio</c> (JSON <c>{ clase: días }</c> con <c>*</c> como defecto; por defecto 10 hábiles y
/// <c>InternalOffset</c> 0: en firme al aplicarse), contados con el calendario nacional (<see cref="ICalendarioHabil"/>, sábado y domingo de
/// descanso). Se escribe en la operación y, en una cancelación, en <c>FinalityUntil</c> del comprobante: hasta entonces el paz y salvo dice
/// «sujeta a firmeza». (nuevo)
/// </summary>
public sealed class FirmezaDelPago(ReglasDeParametrosDeCartera parametros, ICalendarioHabil calendario)
{
    /// <summary>Los días por defecto de una clase sin entrada ni comodín (D23).</summary>
    public const int DiasPorDefecto = 10;

    /// <summary>La fecha de firmeza de un pago de clase <paramref name="clase"/> con fecha valor <paramref name="fechaValor"/>.</summary>
    public async Task<DateOnly> CalcularAsync(ClaseDeMedioDeRecaudo clase, DateOnly fechaValor, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.PagosFirmezaDiasPorClaseDeMedio, fechaValor, ct: ct);
        var dias = Dias(leido.IsSuccess ? leido.Value.Texto : null, clase);
        if (dias <= 0) return fechaValor;
        var calendarioDelRango = await calendario.ObtenerAsync(fechaValor, fechaValor.AddDays(dias * 3 + 31), DiasDeDescanso.SabadoYDomingo, ct: ct);
        return calendarioDelRango.SumarHabiles(fechaValor, dias);
    }

    /// <summary>Los días hábiles de la clase en el JSON del parámetro (la clase por su nombre; si no, <c>*</c>; si no, el defecto).</summary>
    public static int Dias(string? json, ClaseDeMedioDeRecaudo clase)
    {
        var defecto = clase == ClaseDeMedioDeRecaudo.InternalOffset ? 0 : DiasPorDefecto;
        if (string.IsNullOrWhiteSpace(json)) return defecto;
        try
        {
            var mapa = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (mapa is null) return defecto;
            if (mapa.TryGetValue(clase.ToString(), out var propia)) return Math.Max(0, propia);
            if (clase == ClaseDeMedioDeRecaudo.InternalOffset) return 0;
            return mapa.TryGetValue("*", out var todas) ? Math.Max(0, todas) : defecto;
        }
        catch (JsonException)
        {
            return defecto;
        }
    }
}
