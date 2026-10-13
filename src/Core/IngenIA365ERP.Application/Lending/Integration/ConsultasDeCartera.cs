using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La implementación real de <see cref="IConsultasDeCartera"/> (feature 013, US6, T0582; FR-058, FR-059, FR-060, C31; interfaces.md §5.1). Reemplaza a
/// <see cref="ConsultasDeCarteraNoHabilitada"/> en <c>DependencyInjection</c>; antes de activar Cartera responde <see cref="CarteraNoHabilitada"/>.
///
/// <para><see cref="EstadoCrediticioAsync"/>, una <b>consulta</b> (no cambia el estado del negocio):</para>
/// <list type="bullet">
/// <item>el estado del deudor: bloqueos vigentes (<see cref="IBloqueosDelDeudor"/>), listas restrictivas sin escribir
/// (<see cref="IVerificacionDeListas"/>, C31), la autorización vigente <c>CreditProcessing</c> (<see cref="IAutorizacionDeDatos"/>) —cualquiera da
/// <c>Blocked</c> con el motivo en español y sin detalle de listas—; la mora de la posición diaria vigente de sus créditos (<c>InArrears</c> con
/// <c>DaysPastDue</c>); <c>NoRelationship</c> si no es asociado ni tiene cupo;</item>
/// <item><c>AllowsSale</c>: <c>Active</c>, o <c>InArrears</c> con días hasta <c>Cupo.MoraMaximaParaVender</c>;</item>
/// <item>el cupo que aplica y su disponible (<see cref="DisponibleDeCupo.ParaVentaAsync"/>: cupo − saldos vivos − reservas − aprobadas sin desembolsar,
/// como deudor y codeudor);</item>
/// <item>las líneas activas de origen comercial que admiten el tipo de comprador, con plazo y cuotas máximas y las periodicidades de Comercial con
/// equivalencia (<c>LND_CommercialTermEquivalences</c>) que la línea admite;</item>
/// <item>la lectura <c>Lending.CreditStatus.Queried</c> con la huella de entradas y respuesta; su <c>EventId</c> es el <c>EvidenceId</c>
/// (FR-059).</item>
/// </list>
/// <see cref="EstadoDeValidacionAsync"/> lee el recibo <c>LND_InboundMessages</c> con la tabla de §5.1. Respeta el <see cref="CancellationToken"/> de quien
/// llama (<c>Cartera.ConsultaSegundos</c>). (nuevo)
/// </summary>
public sealed class ConsultasDeCartera(
    IApplicationDbContext db,
    IBloqueosDelDeudor bloqueos,
    IVerificacionDeListas listas,
    IAutorizacionDeDatos autorizaciones,
    DisponibleDeCupo cupos,
    VersionVigenteDeLinea versiones,
    EquivalenciasDeComercio equivalencias,
    ReglasDeParametrosDeCartera parametros,
    LendingAuditEmitter auditoria) : IConsultasDeCartera
{
    /// <summary>El método de la huella de la consulta.</summary>
    public const string MetodoConsulta = "Lending.CreditStatus.Queried";

    public async Task<EstadoCrediticioDto> EstadoCrediticioAsync(ConsultaCrediticia consulta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        if (!await ArmadoDeSolicitud.CarteraActivadaAsync(db, ct)) return CarteraNoHabilitada.Instancia;

        var fecha = consulta.OperationDate;
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == consulta.PersonPublicId && !p.IsDeleted)
            .Select(p => new { p.Id, p.PublicId, p.IsAssociate }).FirstOrDefaultAsync(ct);
        if (persona is null)
            return await ConEvidenciaAsync(consulta, new EstadoCrediticioDto(EstadosCrediticios.NoRelationship, null, [], null) { AllowsSale = false }, ct);

        var cupo = await cupos.ParaVentaAsync(persona.Id, null, fecha, ct);
        var lineas = await LineasAsync(consulta.MeansClass, fecha, ct);
        var motivo = await MotivoDeBloqueoAsync(persona.Id, persona.PublicId, ct);
        var dias = motivo is null ? await DiasDeMoraAsync(persona.Id, fecha, ct) : 0;

        string estado;
        bool permite;
        if (motivo is not null)
        {
            estado = EstadosCrediticios.Blocked;
            permite = false;
        }
        else if (!persona.IsAssociate && cupo is null)
        {
            estado = EstadosCrediticios.NoRelationship;
            permite = false;
        }
        else if (dias > 0)
        {
            estado = EstadosCrediticios.InArrears;
            permite = dias <= await MoraMaximaParaVenderAsync(fecha, ct);
        }
        else
        {
            estado = EstadosCrediticios.Active;
            permite = true;
        }

        var respuesta = new EstadoCrediticioDto(estado, cupo?.Disponible, lineas, null)
        {
            TotalQuota = cupo?.Limite,
            DaysPastDue = estado == EstadosCrediticios.InArrears ? dias : null,
            BlockReason = motivo,
            AllowsSale = permite,
        };
        return await ConEvidenciaAsync(consulta, respuesta, ct);
    }

    public async Task<EstadoDeValidacionDto> EstadoDeValidacionAsync(Guid messagePublicId, CancellationToken ct)
    {
        var recibo = await db.LendingInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == messagePublicId, ct);
        if (recibo is null) return new EstadoDeValidacionDto(EstadosDeValidacion.Pending, null, null);
        var evaluacion = recibo.LastEvaluationId is { } id
            ? await db.LendingInboundMessageEvaluations.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct)
            : null;
        return EstadoDe(recibo, evaluacion);
    }

    /// <summary>La tabla de interfaces.md §5.1 sobre el recibo y su última evaluación (también la usan la bandeja y la pantalla del crédito).</summary>
    public static EstadoDeValidacionDto EstadoDe(LendingInboundMessage recibo, LendingInboundMessageEvaluation? evaluacion)
    {
        ArgumentNullException.ThrowIfNull(recibo);
        var cuando = (DateTime?)recibo.LastProcessedAt;
        return recibo.Status switch
        {
            InboundMessageStatus.Processed => new(EstadosDeValidacion.Validated, cuando, null),
            InboundMessageStatus.ConfirmedByException => new(EstadosDeValidacion.Validated, recibo.ConfirmedByExceptionAt ?? cuando,
                $"confirmada por excepción: {recibo.ExceptionReason}"),
            InboundMessageStatus.ValidationFailed => new(EstadosDeValidacion.Failed, cuando, evaluacion?.FailureReason),
            InboundMessageStatus.NotIncorporated => new(EstadosDeValidacion.NotIncorporated, cuando,
                evaluacion?.FailureReason ?? ErroresDeVentasACredito.MotivoAceptadaSinMigrar),
            InboundMessageStatus.Rejected when recibo.NotRetryableAt is not null => new(EstadosDeValidacion.NotIncorporated, recibo.NotRetryableAt,
                $"No se creó la obligación: {evaluacion?.FailureReason ?? recibo.NotRetryableReason}"),
            InboundMessageStatus.Rejected => new(EstadosDeValidacion.Pending, cuando, $"{evaluacion?.FailureReason} Queda para reproceso."),
            _ => new(EstadosDeValidacion.Pending, cuando, null),
        };
    }

    /// <summary>El motivo de bloqueo en español (sin detalle de listas), o nulo si la persona puede comprar a crédito.</summary>
    private async Task<string?> MotivoDeBloqueoAsync(int personId, Guid personPublicId, CancellationToken ct)
    {
        var vigentes = await bloqueos.VigentesAsync([personId], ct);
        if (vigentes.Count > 0) return MotivoDe(vigentes.OrderBy(b => (int)b.Reason).First().Reason);
        if (await listas.CoincideAsync(personId, ct)) return MotivoDe(DebtorBlockReason.RestrictiveListMatch);
        var autorizacion = await autorizaciones.VigenteAsync(personPublicId, DataAuthorizationPurpose.CreditProcessing, null, ct);
        return autorizacion is { Autoriza: true } ? null : "Falta la autorización vigente de tratamiento de datos para crédito: regístrela antes de vender a crédito.";
    }

    /// <summary>El texto de un bloqueo para quien vende (FR-060): sin detalle de listas restrictivas.</summary>
    public static string MotivoDe(DebtorBlockReason motivo) => motivo switch
    {
        DebtorBlockReason.Manual => "Tiene un bloqueo de crédito registrado por la cooperativa.",
        DebtorBlockReason.Arrears => "Tiene créditos en mora.",
        DebtorBlockReason.ValidationFailed => "Tiene un crédito de una venta con validación fallida, pendiente de confirmar en Cartera.",
        DebtorBlockReason.WrittenOff => "Tiene cartera castigada.",
        DebtorBlockReason.RestrictiveListMatch => "Tiene una restricción de cumplimiento: consulte con el oficial de cumplimiento.",
        DebtorBlockReason.AssociateWithdrawal => "Está en proceso de retiro como asociado.",
        DebtorBlockReason.Deceased => "Figura como fallecido.",
        DebtorBlockReason.Insolvency => "Está en un proceso de insolvencia.",
        DebtorBlockReason.Garnishment => "Tiene un embargo registrado.",
        DebtorBlockReason.LimitRevalidation => "Su cupo está en revalidación.",
        _ => "Tiene un bloqueo de crédito.",
    };

    /// <summary>La mayor altura de mora de sus créditos vivos, de la posición diaria vigente a la fecha (la última fecha, la última versión).</summary>
    private async Task<int> DiasDeMoraAsync(int personId, DateOnly fecha, CancellationToken ct)
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

    private async Task<int> MoraMaximaParaVenderAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, fecha, ct: ct);
        return leido.IsSuccess && leido.Value.Valor is int d && d >= 0 ? d : 0;
    }

    /// <summary>
    /// Las líneas activas de origen comercial a la fecha que admiten el tipo de comprador (un cliente, las que admiten no asociados), con plazo y cuotas
    /// máximas y las periodicidades de Comercial que tienen equivalencia y la línea admite.
    /// </summary>
    private async Task<IReadOnlyList<LineaDeCreditoDto>> LineasAsync(PaymentMeansClass clase, DateOnly fecha, CancellationToken ct)
    {
        var activas = await db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && l.Status == CreditLineStatus.Active && l.UsuryBlockedSince == null)
            .OrderBy(l => l.Code).ToListAsync(ct);
        if (activas.Count == 0) return [];
        var vigentes = await versiones.LeerVariasAsync(activas.Select(l => l.Id).ToList(), fecha, null, ct);
        var deComercio = (await equivalencias.VigentesAsync(fecha, null, ct)).Where(e => e.Kind == CommercialTermKind.Periodicity && e.Periodicity is not null).ToList();
        var salida = new List<LineaDeCreditoDto>();
        foreach (var linea in activas)
        {
            if (!vigentes.TryGetValue(linea.Id, out var v)) continue;
            var version = v.Version;
            if (!version.AllowsCommercialOrigin) continue;
            if (clase == PaymentMeansClass.CustomerCredit && !version.AllowsNonAssociates) continue;
            var admitidas = version.LeerPeriodicidades();
            salida.Add(new LineaDeCreditoDto(linea.Code, linea.Name, version.MaxTermMonths, version.MaxInstallments)
            {
                Periodicities = deComercio.Where(e => admitidas.Contains(e.Periodicity!.Value)).Select(e => e.CommercialCode).ToList(),
            });
        }
        return salida;
    }

    /// <summary>Deja la lectura auditada con la huella de entradas y respuesta; su <c>EventId</c> es la evidencia (FR-059).</summary>
    private async Task<EstadoCrediticioDto> ConEvidenciaAsync(ConsultaCrediticia consulta, EstadoCrediticioDto respuesta, CancellationToken ct)
    {
        var huella = HuellaDeOperacion.Calcular(MetodoConsulta, new
        {
            request = consulta,
            response = new
            {
                respuesta.Status, respuesta.AvailableQuota, respuesta.TotalQuota, respuesta.DaysPastDue, respuesta.AllowsSale, respuesta.BlockReason,
                lines = respuesta.Lines.Select(l => new { l.Code, l.MaxTermMonths, l.MaxInstallments, l.Periodicities }),
            },
        });
        var evento = await auditoria.EmitirConsultaDeCupoAsync(consulta.PersonPublicId, new
        {
            meansClass = consulta.MeansClass.ToString(),
            amount = consulta.Amount,
            operationDate = consulta.OperationDate,
            salesChannel = consulta.SalesChannelCode,
            status = respuesta.Status,
            availableQuota = respuesta.AvailableQuota,
            allowsSale = respuesta.AllowsSale,
            sha256 = huella,
        }, ct);
        return respuesta with { EvidenceId = evento?.ToString() };
    }
}
