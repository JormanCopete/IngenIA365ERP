using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// <see cref="IValoresNacionales"/> sobre <c>COR_NationalValues</c> (feature 013, T40). Cada lectura trae en una consulta todas
/// las versiones de la serie (código y clave) con <c>ValidFrom</c> hasta la fecha pedida y resuelve con
/// <see cref="VigenciaBitemporal"/>; el estado de revisión sale de la última <c>COR_NationalValueReviews</c>.
///
/// <para>Las alertas (<c>Cartera.ValorNacionalFaltante</c>, <c>Cartera.IndiceDesactualizado</c>) se levantan con
/// <see cref="IAlertas"/>, que guarda: se llama donde no hay otros cambios pendientes o donde el faltante aborta lo que se hacía.
/// Sin <see cref="IAlertas"/> (pruebas, semillas) sólo se anotan en el log. (nuevo)</para>
///
/// <para>Feature 013, US1-3 (T0344): dentro de una operación idempotente la alerta se <b>encola</b> en <see cref="AlertasDiferidas"/> y la
/// levanta <c>IdempotencyBehavior</c> al terminar, fuera de la transacción: el faltante suele hacer fallar el comando, y la alerta guardada
/// adentro se revertía con él.</para>
/// </summary>
public sealed class LectorDeValoresNacionales(
    IApplicationDbContext db,
    IAlertas? alertas = null,
    ILogger<LectorDeValoresNacionales>? logger = null,
    AlertasDiferidas? diferidas = null) : IValoresNacionales
{
    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    public async Task<Result<ValorNacionalVigente>> LeerAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        clave ??= string.Empty;
        var versiones = await VersionesAsync(codigo, clave, fecha, ct);
        var vigente = VigenciaBitemporal.Vigente(versiones, fecha, informacionHasta ?? DateTime.MaxValue);
        if (vigente is null) return await FaltanteAsync(codigo, clave, fecha, ct);
        return Result.Success(await AVigenteAsync(vigente, desactualizado: false, ct));
    }

    public async Task<Result<ValorNacionalVigente>> LeerUltimoPublicadoAsync(string codigo, string clave, DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        clave ??= string.Empty;
        var versiones = await VersionesAsync(codigo, clave, fecha, ct);
        var vigente = VigenciaBitemporal.Vigente(versiones, fecha, informacionHasta ?? DateTime.MaxValue);
        if (vigente is null) return await FaltanteAsync(codigo, clave, fecha, ct);

        var maximo = CodigosDeValorNacional.Buscar(codigo)?.AntiguedadMaximaDias;
        var antiguedad = fecha.DayNumber - vigente.ValidFrom.DayNumber;
        var desactualizado = maximo is { } dias && antiguedad > dias;
        if (desactualizado)
        {
            await LevantarAsync(new AlertaALevantar(
                AlertasDeValoresNacionales.IndiceDesactualizado,
                $"{codigo} {clave} desactualizado",
                $"El último {codigo} {clave} publicado es del {vigente.ValidFrom:yyyy-MM-dd} ({antiguedad} días antes del {fecha:yyyy-MM-dd}); se admiten {maximo} días. "
                + "Pida al administrador de la plataforma registrar el valor publicado.",
                EntityType: nameof(NationalValue), EntityPublicId: vigente.PublicId,
                DedupKey: AlertasDeValoresNacionales.ClaveDeIndiceDesactualizado(codigo, clave)), ct);
        }
        return Result.Success(await AVigenteAsync(vigente, desactualizado, ct));
    }

    public async Task<SerieDeValorNacional> LeerSerieAsync(string codigo, string clave, DateOnly desde, DateOnly hasta, DateTime? informacionHasta = null, CancellationToken ct = default)
    {
        clave ??= string.Empty;
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var versiones = await VersionesAsync(codigo, clave, hasta, ct);
        var r = informacionHasta ?? DateTime.MaxValue;
        var revisiones = await RevisionesDeCopias.UltimasAsync(db, versiones.Select(v => v.PublicId).ToList(), ct);

        var dias = new Dictionary<DateOnly, ValorNacionalVigente?>();
        var memo = new Dictionary<Guid, ValorNacionalVigente>();
        for (var d = desde; d <= hasta; d = d.AddDays(1))
        {
            var v = VigenciaBitemporal.Vigente(versiones, d, r);
            if (v is null) { dias[d] = null; continue; }
            if (!memo.TryGetValue(v.PublicId, out var leido))
                memo[v.PublicId] = leido = Mapear(v, revisiones.GetValueOrDefault(v.PublicId), desactualizado: false);
            dias[d] = leido;
        }
        return new SerieDeValorNacional(codigo, clave, dias);
    }

    private Task<List<NationalValue>> VersionesAsync(string codigo, string clave, DateOnly hasta, CancellationToken ct) =>
        db.NationalValues.AsNoTracking()
            .Where(v => v.Code == codigo && v.Key == clave && v.ValidFrom <= hasta && !v.IsDeleted)
            .ToListAsync(ct);

    private async Task<ValorNacionalVigente> AVigenteAsync(NationalValue v, bool desactualizado, CancellationToken ct)
    {
        var revisiones = await RevisionesDeCopias.UltimasAsync(db, [v.PublicId], ct);
        return Mapear(v, revisiones.GetValueOrDefault(v.PublicId), desactualizado);
    }

    private static ValorNacionalVigente Mapear(NationalValue v, UltimaRevision? revision, bool desactualizado) =>
        new(v.PublicId, v.SourcePublicId, v.Code, v.Key, v.Value, v.Unit, v.Label, v.ValidFrom, v.ValidTo, v.ReferenceDate,
            v.Source, v.SourceSequence, v.KnownAt, RevisionesDeCopias.Estado(revision), desactualizado);

    private async Task<Result<ValorNacionalVigente>> FaltanteAsync(string codigo, string clave, DateOnly fecha, CancellationToken ct)
    {
        var error = ErroresDeValoresNacionales.Faltante(codigo, clave, fecha);
        await LevantarAsync(new AlertaALevantar(
            AlertasDeValoresNacionales.ValorNacionalFaltante,
            $"Falta {codigo} {clave}".TrimEnd(),
            error.Message,
            DedupKey: AlertasDeValoresNacionales.ClaveDeFaltante(codigo, clave, fecha)), ct);
        return Result.Failure<ValorNacionalVigente>(error);
    }

    private async Task LevantarAsync(AlertaALevantar alerta, CancellationToken ct)
    {
        if (alertas is not null && diferidas is { Activa: true })
        {
            diferidas.Encolar(alerta);
            return;
        }
        if (alertas is null)
        {
            _logger.LogWarning("[ValoresNacionales.Alerta] {Tipo}: {Cuerpo}", alerta.TypeCode, alerta.Body);
            return;
        }
        try
        {
            var r = await alertas.LevantarAsync(alerta, ct);
            if (r.IsFailure)
                _logger.LogWarning("[ValoresNacionales.AlertaNoLevantada] {Tipo}: {Codigo}", alerta.TypeCode, r.Error.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[ValoresNacionales.AlertaNoLevantada] {Tipo}", alerta.TypeCode);
        }
    }
}
