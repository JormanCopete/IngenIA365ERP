using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Cumplimiento;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Compliance;

/// <summary>
/// La consulta a listas de Recaudo (feature 015, US14, T0240; FR-098, FR-189, FR-203; contexto de listas <c>Collection</c>, por nombre: Recaudo no conoce los tipos de Cartera, FR-008), sobre la consulta común de listas
/// (el canal vigente o <c>CARGA-PROPIA</c>), con el mismo molde que <c>ListasDeTesoreria</c>. Decisión del dueño (pendiente de confirmar en P7): una coincidencia
/// <b>exacta</b> (<c>Match</c>) es vinculante y bloquea siempre, con la alerta inmediata <c>Recaudo.ListaVinculante</c> al oficial; una <b>posible</b>
/// (<c>PossibleMatch</c>) no es vinculante y alerta <c>Recaudo.ListaNoVinculante</c> (lo que hace la operación lo dice <c>Sarlaft.ListasNoVinculantes.Efecto</c>). En
/// producción no cuenta un resultado simulado. Cada coincidencia deja su fila en <c>RCD_ComplianceAlertsLog</c>. (nuevo)
/// </summary>
public sealed class ListasDeRecaudo(
    IApplicationDbContext db,
    IProveedoresDeListas proveedores,
    ICredencialesDeCanalExterno credenciales,
    ICurrentTenantService tenant,
    IAmbienteDeDespliegue ambiente,
    IAlertas alertas,
    IDateTimeService reloj,
    AlertasDiferidas? diferidas = null) : Common.IListasDeRecaudo
{
    /// <summary>El contexto de la consulta (el <c>ScreeningContext.Collection</c> de Cartera, por nombre).</summary>
    public const string Contexto = "Collection";

    public async Task<ScreeningOutcomeR> EvaluarAsync(IReadOnlyCollection<int> personIds, Guid operacionPublicId, CancellationToken ct)
    {
        if (personIds.Count == 0) return ScreeningOutcomeR.Clear;
        var hoy = reloj.HoyLocal;
        var canal = await CanalVigenteAsync(hoy, ct);
        var contexto = await ContextoAsync(canal, ct);
        if (contexto is null) return ScreeningOutcomeR.Clear;
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IdType, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName }).ToListAsync(ct);
        var resultado = ScreeningOutcomeR.Clear;
        foreach (var p in personas.OrderBy(p => p.Id))
        {
            var nombre = PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName);
            var r = await proveedores.Resolver(canal).ConsultarAsync(new ConsultaDeListas(p.PublicId, p.IdType ?? string.Empty, p.TaxId ?? string.Empty, nombre), contexto, ct);
            if (r.Outcome != ExternalOutcome.Ok || r.Valor is not { } valor) continue;
            if (ambiente.EsProduccion && (r.Simulado || CodigosDeCanalExterno.EsSimulado(r.ChannelCode))) continue;
            if (valor.Resultado == ScreeningOutcome.Match)
            {
                resultado = ScreeningOutcomeR.BindingMatch;
                await AlertarAsync(p.Id, p.PublicId, nombre, operacionPublicId, vinculante: true, ct);
            }
            else if (valor.Resultado == ScreeningOutcome.PossibleMatch)
            {
                if (resultado == ScreeningOutcomeR.Clear) resultado = ScreeningOutcomeR.NonBindingMatch;
                await AlertarAsync(p.Id, p.PublicId, nombre, operacionPublicId, vinculante: false, ct);
            }
        }
        return resultado;
    }

    private async Task AlertarAsync(int persona, Guid personaPublicId, string nombre, Guid operacion, bool vinculante, CancellationToken ct)
    {
        var tipo = vinculante ? TiposDeAlerta.RecaudoListaVinculante : TiposDeAlerta.RecaudoListaNoVinculante;
        var aviso = new AlertaALevantar(tipo,
            vinculante ? "Coincidencia en lista vinculante en la caja" : "Coincidencia en lista no vinculante en la caja",
            string.Create(CultureInfo.InvariantCulture,
                $"{nombre} coincide en una lista {(vinculante ? "vinculante: no se recibe su dinero" : "no vinculante: revise la operación")} (operación {operacion:N})."),
            "Person", personaPublicId, DedupKey: $"{tipo}:{operacion:N}:{personaPublicId:N}", ModuloDeOrigen: "Receipts",
            RecipientPermissions: ["Receipts.Compliance.View"]);
        // La alerta de la vinculante es inmediata y sobrevive al fallo de la operación que la encontró (AlertasDiferidas).
        Guid? alerta = null;
        if (diferidas is { Activa: true }) diferidas.Encolar(aviso);
        else
        {
            var levantada = await alertas.LevantarAsync(aviso, ct);
            alerta = levantada.IsSuccess ? levantada.Value.AlertPublicId : null;
        }
        db.ReceiptsComplianceAlertLogs.Add(new ComplianceAlertLog
        {
            PersonId = persona, Signal = vinculante ? SenalDeEfectivo.BindingListMatch : SenalDeEfectivo.NonBindingListMatch, WindowFrom = reloj.HoyLocal,
            WindowTo = reloj.HoyLocal, Count = 1, Total = 0m, AlertPublicId = alerta, DocumentPublicId = operacion, EvaluatedAt = reloj.UtcNow,
        });
    }

    private async Task<string> CanalVigenteAsync(DateOnly fecha, CancellationToken ct)
    {
        var configurados = await db.ExternalChannelSettings.AsNoTracking().Where(s => s.Kind == ExternalChannelKind.RestrictiveLists && !s.IsDeleted).ToListAsync(ct);
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
        return new ContextoDeCanalExterno(cooperativa, canal, null, credencial, Guid.NewGuid().ToString("N"), !ambiente.EsProduccion);
    }
}
