using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Common.Parameters;

// Feature 015, US8 (T0262; FR-221): lo común del cambio de la política contable de Recaudo y de Tesorería, en la plataforma para que ninguno de los dos módulos
// nombre al otro (RecaudoYTesoreriaSeHablanPorSusPuertos).

/// <summary>La política contable vigente de un módulo: <c>Bloquear</c> o <c>Advertir</c>, desde cuándo. (nuevo)</summary>
public sealed record PoliticaContableDto(string Module, string Policy, DateOnly ValidFrom);

/// <summary>Lo común del cambio de política de Recaudo y de Tesorería. (nuevo)</summary>
public static class CambioDePoliticaContable
{
    public static async Task<Result<PoliticaContableDto>> CambiarAsync(ISender sender, IAlertas alertas, IDateTimeService reloj, string modulo, string nombre,
        string tipoDeAlerta, string moduloDeOrigen, string politica, string motivo, DateOnly? desde, CancellationToken ct)
    {
        var fecha = desde ?? reloj.HoyLocal;
        var alta = await sender.Send(new AddParameterVersionCommand(modulo, ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla, ParameterScopeKind.None, null, null,
            politica, fecha, motivo, null) { OperationKey = Guid.NewGuid() }, ct);
        if (alta.IsFailure) return Result.Failure<PoliticaContableDto>(alta.Error);
        if (politica == "Advertir")
            await alertas.LevantarAsync(new AlertaALevantar(tipoDeAlerta, $"Política contable de {nombre} en «advertir»",
                $"Desde el {fecha:dd/MM/yyyy} {nombre} confirma operaciones aunque la matriz contable no tenga su regla. Motivo: {motivo}",
                DedupKey: $"{tipoDeAlerta}:{fecha:yyyy-MM-dd}", ModuloDeOrigen: moduloDeOrigen), ct);
        return Result.Success(new PoliticaContableDto(modulo, politica, fecha));
    }
}
