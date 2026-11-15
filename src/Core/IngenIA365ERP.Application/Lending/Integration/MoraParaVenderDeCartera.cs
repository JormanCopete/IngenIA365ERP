using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Lo que la mora del deudor exige para venderle a crédito y si la evidencia que trae la venta alcanza. (nuevo)</summary>
/// <param name="Dias">La mayor altura de mora de sus créditos vivos a la fecha.</param>
/// <param name="Tolerancia"><c>Cupo.MoraMaximaParaVender</c> vigente (de la línea o el general).</param>
/// <param name="Politica"><c>Cupo.PoliticaDeMoraParaVender</c> vigente (de la línea o el general).</param>
/// <param name="Accion">Una de <see cref="AccionesPorMora"/>.</param>
/// <param name="Permite">¿Procede la venta con la evidencia dada?</param>
/// <param name="Motivo">Por qué no procede, en español, para quien vende; nulo si procede.</param>
public sealed record MoraParaVenderEvaluada(int Dias, int Tolerancia, string Politica, string Accion, bool Permite, string? Motivo);

/// <summary>
/// La mora para vender de Cartera (decisión del dueño del 2026-11-12): <b>un solo sitio</b> que lee los días de mora del deudor, la tolerancia y la
/// política vigentes (por línea, con caída al general) y el umbral del bloqueo automático, y aplica <see cref="ReglaDeMoraParaVender"/> con la evidencia
/// de la venta (los días que confirmó quien cobra, la aprobación del supervisor presente). Lo usan la consulta (<see cref="ConsultasDeCartera"/>) y la
/// reserva (<see cref="ReservasDeCartera"/>), que antes repetían cada una su comparación. Sólo lee. (nuevo)
/// </summary>
public sealed class MoraParaVenderDeCartera(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros)
{
    /// <summary>El <c>Id</c> de la línea de colocación viva con ese código, o nulo (sin código, o un código que no es de una línea de crédito).</summary>
    public async Task<int?> LineaAsync(string? codigo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var normalizado = codigo.Trim().ToUpperInvariant();
        return await db.CreditLines.AsNoTracking().Where(l => l.Code == normalizado && !l.IsDeleted && l.Nature == LineNature.Placement)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>La mayor altura de mora de sus créditos vivos, de la posición diaria vigente a la fecha (la última fecha, la última versión).</summary>
    public async Task<int> DiasDeMoraAsync(int personId, DateOnly fecha, CancellationToken ct)
    {
        var creditos = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == personId && !l.IsDeleted && l.Status == LoanStatus.Active)
            .Select(l => l.Id).ToListAsync(ct);
        if (creditos.Count == 0) return 0;
        var posiciones = await db.LoanDailyPositions.AsNoTracking().Where(p => creditos.Contains(p.LoanId) && p.PositionDate <= fecha)
            .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.DaysPastDue }).ToListAsync(ct);
        return posiciones.GroupBy(p => p.LoanId)
            .Select(g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First().DaysPastDue)
            .DefaultIfEmpty(0).Max();
    }

    /// <summary>
    /// Evalúa la mora de la venta. <paramref name="topeDuro"/> (bloqueo vigente, listas, autorización de datos) bloquea sin leer nada más. La huella de
    /// la aprobación fija persona, clase del medio, valor, línea, fecha y días (<see cref="ReglaDeMoraParaVender.Huella"/>).
    /// </summary>
    public async Task<MoraParaVenderEvaluada> EvaluarAsync(bool topeDuro, int personId, Guid personPublicId, PaymentMeansClass clase, decimal monto,
        DateOnly fecha, string? lineaCodigo, int? diasConfirmados, Guid? aprobacion, CancellationToken ct)
    {
        if (topeDuro) return new MoraParaVenderEvaluada(0, 0, PoliticasDeMoraParaVender.Bloquear, AccionesPorMora.Blocked, false, null);

        var dias = await DiasDeMoraAsync(personId, fecha, ct);
        var linea = await LineaAsync(lineaCodigo, ct);
        var tolerancia = await EnteroAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, fecha, linea, 0, ct);
        var politica = await TextoAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, fecha, linea, PoliticasDeMoraParaVender.Bloquear, ct);
        int? umbral = await EnteroAsync(ParametrosDeCartera.BloqueosDiasDeMoraParaBloquear, fecha, null, 0, ct) is var u && u > 0 ? u : null;

        var accion = ReglaDeMoraParaVender.Accion(false, dias, tolerancia, politica, umbral);
        var aprobada = accion is AccionesPorMora.Approve or AccionesPorMora.Confirm && aprobacion is not null
            && await ReglaDeMoraParaVender.AprobacionVigenteAsync(db,
                aprobacion, ReglaDeMoraParaVender.Huella(personPublicId, clase, monto, lineaCodigo, fecha, dias), ct);
        var permite = ReglaDeMoraParaVender.Permite(accion, dias, diasConfirmados, aprobada);
        return new MoraParaVenderEvaluada(dias, tolerancia, politica, accion, permite, permite ? null : Motivo(accion, dias, tolerancia, umbral));
    }

    /// <summary>Por qué la mora no deja vender, para quien vende.</summary>
    public static string Motivo(string accion, int dias, int tolerancia, int? umbral) => accion switch
    {
        AccionesPorMora.Confirm => $"Tiene {dias} días de mora: quien cobra debe confirmar en el panel de pago que la vio para venderle a crédito.",
        AccionesPorMora.Approve => $"Tiene {dias} días de mora: venderle a crédito exige la aprobación de un supervisor presente.",
        _ when umbral is { } u && dias >= u => $"Tiene {dias} días de mora: desde {u} días Cartera bloquea al deudor.",
        _ => $"Tiene {dias} días de mora: más de los {tolerancia} que admite la venta a crédito.",
    };

    private async Task<int> EnteroAsync(string clave, DateOnly fecha, int? linea, int porDefecto, CancellationToken ct)
    {
        var leido = linea is { } id
            ? await parametros.LeerAsync(clave, fecha, null, ParameterScopeKind.CreditLine, id, ct)
            : await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess && leido.Value.Valor is int d && d >= 0 ? d : porDefecto;
    }

    private async Task<string> TextoAsync(string clave, DateOnly fecha, int? linea, string porDefecto, CancellationToken ct)
    {
        var leido = linea is { } id
            ? await parametros.LeerAsync(clave, fecha, null, ParameterScopeKind.CreditLine, id, ct)
            : await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto.Trim() : porDefecto;
    }
}
