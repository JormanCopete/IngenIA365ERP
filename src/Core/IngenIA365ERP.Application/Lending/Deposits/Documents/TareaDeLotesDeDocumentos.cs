using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements.Tareas;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// Un lote programado del parámetro <c>Documentos.LotesProgramados</c> (feature 014, respuesta 86 del dueño, E250): la clase de documento y el día en que se
/// emite (de cada mes, o de un mes del año si trae «mes»).
/// </summary>
public sealed record LoteProgramadoDeDocumentos(string Clase, int Dia, int? Mes);

/// <summary>
/// Emite los lotes de documentos al titular que la cooperativa programó (<c>Documentos.LotesProgramados</c>, JSON): por cada uno cuyo día es hoy, el mismo
/// <see cref="IssueLendingDocumentBatchCommand"/> que se lanza a mano —su alcance, su copia del extracto, su auditoría—, con la entrega por descarga (el envío
/// por correo masivo se pide a mano). Un documento ya emitido no se repite. Devuelve cuántos lotes corrió. Sin Cartera activada o sin parámetro no hace nada.
/// Sin ruta: lo corre la tarea <c>captaciones.lotes-de-documentos</c>. (nuevo)
/// </summary>
public sealed record RunScheduledDocumentBatchesCommand : IRequest<Result<int>>;

public sealed class RunScheduledDocumentBatchesCommandValidator : AbstractValidator<RunScheduledDocumentBatchesCommand>;

public sealed class RunScheduledDocumentBatchesCommandHandler(IApplicationDbContext db, ISender sender, ReglasDeParametrosDeCartera parametros, IDateTimeService reloj)
    : IRequestHandler<RunScheduledDocumentBatchesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(RunScheduledDocumentBatchesCommand request, CancellationToken ct)
    {
        if (!await TareasDeCartera.CarteraActivadaAsync(db, ct)) return Result.Success(0);
        var hoy = reloj.HoyLocal;
        var leido = await parametros.LeerAsync(ParametrosDeCartera.DocumentosLotesProgramados, hoy, ct: ct);
        if (leido.IsFailure) return Result.Success(0);
        var lotes = Leer(leido.Value.Texto);
        var corridos = 0;
        foreach (var lote in lotes.Where(l => Toca(l, hoy)))
        {
            var clase = Enum.Parse<IssuedDocumentKind>(lote.Clase, ignoreCase: true);
            var r = await sender.Send(new IssueLendingDocumentBatchCommand(clase, null, null, null, DocumentDeliveryMode.Download) { OperationKey = Guid.NewGuid() }, ct);
            if (r.IsFailure) return Result.Failure<int>(r.Error);
            corridos++;
        }
        return Result.Success(corridos);
    }

    /// <summary>¿Le toca al lote correr en <paramref name="hoy"/>? Sin mes, el día de cada mes (el 29 al 31 se corre el último día de un mes más corto).</summary>
    public static bool Toca(LoteProgramadoDeDocumentos lote, DateOnly hoy)
    {
        if (lote.Mes is { } mes && mes != hoy.Month) return false;
        var ultimo = DateTime.DaysInMonth(hoy.Year, hoy.Month);
        return Math.Min(lote.Dia, ultimo) == hoy.Day;
    }

    /// <summary>Los lotes válidos del JSON (clase conocida y día de 1 a 31); lo que no se entiende se ignora.</summary>
    public static IReadOnlyList<LoteProgramadoDeDocumentos> Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            var lista = new List<LoteProgramadoDeDocumentos>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var clase = e.TryGetProperty("clase", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (clase is null || !Enum.TryParse<IssuedDocumentKind>(clase, ignoreCase: true, out var k) || !IssueLendingDocumentBatchCommand.Admite(k)) continue;
                if (!e.TryGetProperty("dia", out var d) || !d.TryGetInt32(out var dia) || dia is < 1 or > 31) continue;
                int? mes = e.TryGetProperty("mes", out var m) && m.TryGetInt32(out var mm) && mm is >= 1 and <= 12 ? mm : null;
                lista.Add(new LoteProgramadoDeDocumentos(k.ToString(), dia, mes));
            }
            return lista;
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>
/// La tarea programada <c>captaciones.lotes-de-documentos</c> (feature 014, respuesta 86 del dueño): una vez al día desde las 6:00, en cada cooperativa, por
/// <see cref="IEjecutorEnCooperativa"/> como cualquier tarea de fondo. Escribe sólo por <see cref="RunScheduledDocumentBatchesCommand"/>. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeLotesDeDocumentos : ITareaProgramada
{
    public const string NombreDeLaTarea = "captaciones.lotes-de-documentos";

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => TareasDeCartera.UnaVezAlDia(ahoraLocal, ultimaCorrida);

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var r = await servicios.GetRequiredService<ISender>().Send(new RunScheduledDocumentBatchesCommand(), ct);
        if (r.IsFailure) throw new InvalidOperationException($"Los lotes programados de documentos fallaron: {r.Error.Code} {r.Error.Message}");
    }
}
