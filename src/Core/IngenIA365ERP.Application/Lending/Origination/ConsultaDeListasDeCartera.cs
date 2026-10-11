using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// La consulta de listas restrictivas de Cartera (feature 013, US2 bloque 6B; T0403; T70, T71, T73; FR-036, FR-049; data-model §2.6), la
/// implementación de <see cref="IConsultaDeListasDeCartera"/>:
/// <list type="bullet">
/// <item>consulta <see cref="IListasRestrictivas"/> por el canal vigente de la cooperativa (<c>COR_ExternalChannelSettings</c>, clase
/// <c>RestrictiveLists</c>; sin configuración, <c>CARGA-PROPIA</c>, que vale en producción) y lo <b>sella</b> en el resultado;</item>
/// <item>agrega a la unidad de trabajo, <b>sin guardar</b>, un <c>ScreeningResult</c> por consulta y contexto (en el re-escaneo, sólo los
/// hallazgos) con las versiones de las listas y las coincidencias;</item>
/// <item>una coincidencia que cuenta (<c>Match</c> o <c>PossibleMatch</c>, no simulada en producción) bloquea: deja el bloqueo automático
/// <c>RestrictiveListMatch</c> con el resultado como disparador y la alerta <c>Cartera.CoincidenciaEnListas</c> al oficial, que se levanta
/// después del commit (si la operación se revierte, no hay nada que alertar);</item>
/// <item>una coincidencia que el oficial ya liberó con las mismas versiones y las mismas coincidencias nace liberada (no vuelve a bloquear lo
/// que se revisó); <c>Unavailable</c> y el simulado en producción no cuentan como consulta.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ConsultaDeListasDeCartera(
    IApplicationDbContext db,
    IProveedoresDeListas proveedores,
    ICredencialesDeCanalExterno credenciales,
    ICurrentTenantService tenant,
    IOptions<CanalesExternosOptions> opciones,
    IAlertas alertas,
    IActorActual actorActual,
    IDateTimeService reloj,
    TareasTrasElCommit? trasElCommit = null) : IConsultaDeListasDeCartera
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ConsultaDeListasRegistrada> ConsultarAsync(PedidoDeConsultaDeListas pedido, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var hoy = reloj.HoyLocal;
        var ahora = reloj.UtcNow;
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == pedido.PersonId, ct);
        var canal = await CanalVigenteAsync(db, hoy, ct);
        var enProduccion = !opciones.Value.AmbienteDePruebas;

        ResultadoExterno<ResultadoDeListas> respuesta;
        var contexto = await ContextoAsync(canal, ct);
        if (contexto is null)
            respuesta = ResultadoExterno<ResultadoDeListas>.Sin(ExternalOutcome.ChannelUnavailable, canal, CodigosDeCanalExterno.EsSimulado(canal),
                "No se pudo resolver la credencial del canal de listas.");
        else
            respuesta = await proveedores.Resolver(canal).ConsultarAsync(new ConsultaDeListas(persona.PublicId, persona.IdType ?? string.Empty,
                persona.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(persona)), contexto, ct);

        // Los valores de la plataforma (Compliance) y los de Cartera son los mismos (data-model §2.6): NoMatch 1, PossibleMatch 2, Match 3, Unavailable 4.
        var resultado = respuesta.Outcome == ExternalOutcome.Ok && respuesta.Valor is { } valor ? (ScreeningOutcome)(int)valor.Resultado : ScreeningOutcome.Unavailable;
        var versiones = respuesta.Valor?.Versiones ?? [];
        var coincidencias = respuesta.Valor?.Coincidencias ?? [];
        var fila = new ScreeningResult
        {
            PersonId = persona.Id,
            Context = pedido.Contexto,
            ApplicationId = pedido.ApplicationId,
            CreditLimitId = pedido.CreditLimitId,
            ChannelCode = respuesta.ChannelCode,
            ListVersions = JsonSerializer.Serialize(versiones.Select(v => new { listCode = v.ListCode, version = v.Version, validFrom = v.ValidFrom, publishedOn = v.PublishedOn }), Json),
            Outcome = resultado,
            Matches = coincidencias.Count == 0
                ? null
                : JsonSerializer.Serialize(coincidencias.Select(c => new
                {
                    listCode = c.ListCode, version = c.Version, entryName = c.NombreEnLista, document = c.DocumentoEnLista, byDocument = c.PorDocumento,
                    score = c.Similitud, program = c.Programa,
                }), Json),
            IsSimulated = respuesta.Simulado || CodigosDeCanalExterno.EsSimulado(respuesta.ChannelCode),
            QueriedAt = ahora,
            QueriedByUserId = (await actorActual.ObtenerAsync(ct)).UserId,
        };

        var cuenta = fila.CuentaComoConsulta(enProduccion);
        var hallazgo = cuenta && fila.Bloquea;
        if (hallazgo && await LiberadaAntesAsync(fila, ct) is { } liberada)
        {
            fila.ReleasedAt = liberada.ReleasedAt;
            fila.ReleasedByUserId = liberada.ReleasedByUserId;
            fila.ReleaseReason = liberada.ReleaseReason;
            hallazgo = false;
        }

        if (!pedido.RegistrarSinHallazgo && !hallazgo) return new ConsultaDeListasRegistrada(fila, false, cuenta);
        db.ScreeningResults.Add(fila);
        if (hallazgo) await BloquearAsync(fila, hoy, ct);
        return new ConsultaDeListasRegistrada(fila, hallazgo, cuenta);
    }

    /// <summary>El canal de listas vigente de la cooperativa (sin configuración, <c>CARGA-PROPIA</c>).</summary>
    public static async Task<string> CanalVigenteAsync(IApplicationDbContext db, DateOnly fecha, CancellationToken ct)
    {
        var configurados = await db.ExternalChannelSettings.AsNoTracking().Where(s => s.Kind == ExternalChannelKind.RestrictiveLists && !s.IsDeleted)
            .ToListAsync(ct);
        return configurados.Where(s => s.VigenteEn(fecha)).OrderByDescending(s => s.ValidFrom).Select(s => s.ChannelCode).FirstOrDefault()
               ?? CodigosDeCanalExterno.CargaPropia;
    }

    private async Task<ContextoDeCanalExterno?> ContextoAsync(string canal, CancellationToken ct)
    {
        var cooperativa = CooperativaDelCanal.Resuelta(tenant) ?? Guid.Empty;
        CredencialDeCanalExterno? credencial = null;
        if (!CodigosDeCanalExterno.NoPideCredencial(canal))
        {
            var resuelta = await credenciales.ResolverAsync(canal, ct);
            if (resuelta.IsFailure) return null;
            credencial = resuelta.Value;
        }
        return new ContextoDeCanalExterno(cooperativa, canal, null, credencial, Guid.NewGuid().ToString("N"), opciones.Value.AmbienteDePruebas);
    }

    /// <summary>Una coincidencia de la persona ya liberada por el oficial con las mismas versiones y las mismas coincidencias, si la hay.</summary>
    private async Task<ScreeningResult?> LiberadaAntesAsync(ScreeningResult fila, CancellationToken ct)
    {
        var liberadas = await db.ScreeningResults.AsNoTracking()
            .Where(r => r.PersonId == fila.PersonId && r.ReleasedAt != null && r.Outcome == fila.Outcome && !r.IsDeleted)
            .OrderByDescending(r => r.ReleasedAt).ToListAsync(ct);
        return liberadas.FirstOrDefault(r => r.ListVersions == fila.ListVersions && r.Matches == fila.Matches);
    }

    private async Task BloquearAsync(ScreeningResult fila, DateOnly hoy, CancellationToken ct)
    {
        db.DebtorBlocks.Add(DebtorBlock.Nuevo(fila.PersonId, DebtorBlockReason.RestrictiveListMatch, fila.PublicId.ToString(), hoy,
            "Coincidencia en listas restrictivas pendiente de revisión del oficial de cumplimiento.", fila.QueriedByUserId));

        var alerta = new AlertaALevantar(TiposDeAlerta.CoincidenciaEnListas, "Coincidencia en listas restrictivas",
            $"Una consulta de listas restrictivas ({Contexto(fila.Context)}) coincidió con una persona. Revise el resultado en Cartera › Listas restrictivas " +
            "y libérelo con motivo si no corresponde; mientras tanto la persona no puede originar ni comprar a crédito.",
            "ScreeningResult", fila.PublicId, DedupKey: $"{TiposDeAlerta.CoincidenciaEnListas}:{fila.PublicId:N}");
        if (trasElCommit is not null) trasElCommit.Agregar(c => alertas.LevantarAsync(alerta, c));
        else await alertas.LevantarAsync(alerta, ct);
    }

    private static string Contexto(ScreeningContext contexto) => contexto switch
    {
        ScreeningContext.Application => "radicación de una solicitud",
        ScreeningContext.Sale => "venta a crédito",
        ScreeningContext.CreditLimit => "asignación de un cupo",
        _ => "re-escaneo de deudores y codeudores",
    };
}
