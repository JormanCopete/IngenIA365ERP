using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Charges;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La <b>implementación real</b> del contrato de cuentas (feature 014, US3, T083; research R09, E33; contrato-de-cuentas §3; FR-086–FR-093). Reemplaza a
/// <see cref="ContratoDeCuentasSinActivar"/> en <c>DependencyInjection</c> y, como la de obligaciones, responde «no habilitado» <b>por naturaleza</b>
/// mientras la naturaleza de la cuenta no tenga su fecha de activación en <c>LND_Setup</c> (<see cref="ActivacionesDeCaptacion"/>), con los mismos
/// códigos y mensajes que la provisional. Corre en la unidad de trabajo del recaudador, se llama directo y nunca lanza por un rechazo de negocio. Las
/// escrituras siguen los pasos de §3.4: el intento por un contexto aparte; el validador; la transacción en curso; la activación de la naturaleza de cada
/// cuenta; el recaudador con <c>ProveedorDeCuentas</c> adoptado; la validación previa contable fuera del cerrojo (con <c>Contabilidad.FaltaDeRegla =
/// Bloquear</c> bloquea el ítem, A8 de la 013); el cerrojo del lote (setup → personas → cuentas); cada ítem por <see cref="ConsignacionEnCuentas"/> o
/// <see cref="ReversionDeCuentas"/>, y el hecho de auditoría de cada ítem en <c>COR_AuditOutbox</c>.
///
/// <para>Lo que hace en K7 (US3): <b>aportes</b> enteros —exigibles, consignar a cuotas o como extraordinario, no descontados, simular la
/// consignación, consultar y reversar— y la cuota del <b>ahorro permanente</b> (exigibles y su consignación por cuotas).</para>
/// <para>K8 (US4, T111): la consignación libre en el ahorro a la vista (con canje, cargos pendientes, depósito inicial y atribución de efectivo), el
/// <b>retiro</b> (firmas y manejo, disponible, topes, atribución con aprobador presente, GMF) y el <b>traslado</b> entre cuentas, la simulación de las tres y
/// el disponible desglosado de la consulta (el mismo que valida el retiro, SC-007). Los aportes no se retiran por el contrato
/// (<c>Lending.Contributions.NotWithdrawable</c>); la cancelación con entrega (<c>CloseAccount</c>) llega con la liquidación de intereses (US5).</para>
/// (nuevo)
/// </summary>
public sealed class ContratoDeCuentasDeCartera(
    IApplicationDbContext db,
    ITransaccionDelRecaudador transaccion,
    IRegistroDeIntegracion registroDeIntegracion,
    ValidacionPreviaContableDeCartera validacionPrevia,
    ICerrojoDeCartera cerrojo,
    ConsignacionEnCuentas consignacion,
    ReversionDeCuentas reversion,
    RegistroDeCuentas registro,
    ReglasDeParametrosDeCartera parametros,
    AuditoriaDeEscriturasDeCartera auditoria,
    IValidator<SolicitudDeConsignacion> validadorDeConsignacion,
    IValidator<SolicitudDeReversoDeCuenta> validadorDeReverso,
    IValidator<SolicitudDeExigibles> validadorDeExigibles,
    IValidator<SolicitudDeSimulacionDeCuenta> validadorDeSimulacion,
    IValidator<SolicitudDeConsultaDeCuentas> validadorDeConsulta,
    IDateTimeService reloj,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    TrasladoDeCuenta traslados,
    CargosDeCuentas cargos,
    IValidator<SolicitudDeRetiro> validadorDeRetiro,
    IValidator<SolicitudDeTraslado> validadorDeTraslado) : IContratoDeCuentas
{
    /// <summary>El tipo de entidad de los eventos de auditoría del contrato.</summary>
    public const string Entidad = "AccountContract";

    public const string MensajeConsignada = "Consignación aplicada.";
    public const string MensajeRetirada = "Retiro autorizado y aplicado: el recaudador entrega el dinero.";
    public const string MensajeYaRetirada = "El retiro ya estaba aplicado con esta clave: se devuelve el comprobante original.";
    public const string MensajeTrasladada = "Traslado aplicado.";
    public const string MensajeYaConsignada = "La consignación ya estaba aplicada con esta clave: se devuelve el comprobante original.";
    public const string MensajeReversada = "Operación reversada.";
    public const string MensajeNoDescontado = "No descontado registrado: la cuota sigue exigible.";

    /// <summary>Los conceptos de Nómina por naturaleza y clase (E-02): aportes, y ahorro permanente o contractual.</summary>
    public const string ConceptoDeAportes = "DESC_APORTES";

    public const string ConceptoDeAhorros = "DESC_AHORROS";

    // ------------------------------------------------------------------------------------------------------------ consultar --

    public async Task<RespuestaDelContratoDto<CuentasDePersonaDto>> ConsultarAsync(SolicitudDeConsultaDeCuentas solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var forma = await validadorDeConsulta.ValidateAsync(solicitud, ct);
        if (!forma.IsValid) return Rechazo<CuentasDePersonaDto>(Invalida(forma));
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var (captaciones, aportes) = (Estado(fechas, LineNature.Deposit), Estado(fechas, LineNature.Contribution));
        if (captaciones == EstadoDeCartera.NotActivated && aportes == EstadoDeCartera.NotActivated)
            return new(ResultadoDelContrato.Answered, ContratoDeCuentasSinActivar.CodigoCaptacionesNoActivadas,
                ContratoDeCuentasSinActivar.MensajeCaptacionesNoActivadas,
                new CuentasDePersonaDto(captaciones, aportes, solicitud.PersonPublicId, solicitud.Date, []));
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta) return Rechazo<CuentasDePersonaDto>(noCuenta);
        var persona = await db.People.AsNoTracking().Where(p => p.PublicId == solicitud.PersonPublicId && !p.IsDeleted).Select(p => (int?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (persona is null) return Rechazo<CuentasDePersonaDto>(new Error(ErroresDePagos.CodigoNoEncontrado, "La persona no existe en el maestro."));

        var naturalezas = fechas.DeCaptacionEn(DateOnly.MaxValue);
        var roles = solicitud.IncludeAsSignatory
            ? new[] { MemberAccountPartyRole.Holder, MemberAccountPartyRole.CoHolder, MemberAccountPartyRole.Signatory, MemberAccountPartyRole.LegalRepresentative }
            : [MemberAccountPartyRole.Holder, MemberAccountPartyRole.CoHolder];
        var partes = await (from p in db.MemberAccountParties.AsNoTracking()
                            join a in db.MemberAccounts.AsNoTracking() on p.MemberAccountId equals a.Id
                            where p.PersonId == persona && roles.Contains(p.Role) && p.ValidTo == null && !p.IsDeleted && !a.IsDeleted
                                  && a.Status != MemberAccountStatus.Closed && naturalezas.Contains(a.Nature)
                            select new { Cuenta = a, p.Role }).ToListAsync(ct);
        var propias = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == persona && !a.IsDeleted && a.Status != MemberAccountStatus.Closed && naturalezas.Contains(a.Nature))
            .ToListAsync(ct);
        var cuentas = propias.Select(a => (Cuenta: a, Rol: MemberAccountPartyRole.Holder))
            .Concat(partes.Where(p => propias.All(a => a.Id != p.Cuenta.Id)).Select(p => (p.Cuenta, Rol: p.Role)))
            .GroupBy(x => x.Cuenta.Id).Select(g => g.First()).OrderBy(x => x.Cuenta.Number, StringComparer.Ordinal).ToList();
        var ids = cuentas.Select(c => c.Cuenta.Id).ToList();
        var saldos = await LectorDeCuentas.SaldosAsync(db, ids, ct);
        var lineas = await LineasAsync(cuentas.Select(c => c.Cuenta.CreditLineId), ct);
        var firmantes = await FirmantesAsync(ids, ct);
        var exentas = await db.GmfExemptAccounts.AsNoTracking().Where(g => ids.Contains(g.MemberAccountId) && g.ValidTo == null && !g.IsDeleted)
            .Select(g => g.MemberAccountId).ToListAsync(ct);
        // US4 (T111, SC-007): el disponible desglosado es el mismo que valida el retiro (DisponibleDeCuenta, por ReglasDeMovimientos).
        var disponibles = await movimientos.DisponiblesAsync(cuentas.Select(c => c.Cuenta).ToList(), solicitud.Date, ct);
        var retirables = new Dictionary<int, bool>();
        foreach (var c in cuentas.Where(x => x.Cuenta.Nature == LineNature.Deposit))
            retirables[c.Cuenta.Id] = (await Deposits.Lines.LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, c.Cuenta.CreditLineId, solicitud.Date, null, ct))
                ?.AllowsPartialWithdrawals ?? false;

        var consultadas = cuentas.Select(c =>
        {
            var linea = lineas.GetValueOrDefault(c.Cuenta.CreditLineId);
            var bloqueos = new List<string>();
            if (c.Cuenta.TechnicalNegativeSince is not null) bloqueos.Add("Sobregiro técnico");
            if (c.Cuenta.Status == MemberAccountStatus.Opening) bloqueos.Add("Apertura pendiente");
            var disponible = disponibles.GetValueOrDefault(c.Cuenta.Id) ?? Domain.Lending.Deposits.DisponibleCalculado.Cero;
            if (disponible.EnCanje > 0m) bloqueos.Add("Valores en canje");
            // US11 (T189): lo bloqueado y lo embargado se dicen (el valor va en el disponible desglosado).
            if (c.Cuenta.IsGarnished || disponible.Embargado > 0m) bloqueos.Add("Embargo");
            if (disponible.Retenido > 0m) bloqueos.Add("Bloqueo");
            var retira = c.Cuenta.Nature == LineNature.Deposit && retirables.GetValueOrDefault(c.Cuenta.Id) && c.Cuenta.TechnicalNegativeSince is null
                         && (c.Cuenta.DepositClass == DepositClass.TermDeposit
                             ? c.Cuenta.Status == MemberAccountStatus.Matured
                             : c.Cuenta.Status is MemberAccountStatus.Active or MemberAccountStatus.Inactive);
            return new CuentaConsultadaDto(c.Cuenta.PublicId, c.Cuenta.Number, (NaturalezaDeCuenta)(int)c.Cuenta.Nature,
                c.Cuenta.DepositClass is { } k ? (ClaseDeCaptacion)(int)k : null, linea.Code ?? string.Empty, linea.Name ?? string.Empty,
                (RolEnLaCuenta)(int)c.Rol, (EstadoDeCuenta)(int)c.Cuenta.Status, c.Cuenta.HandlingMode.ToString(), firmantes.GetValueOrDefault(c.Cuenta.Id) ?? [],
                ReglasDeMovimientos.Dto(disponible),
                c.Cuenta.Status is MemberAccountStatus.Active or MemberAccountStatus.Inactive,
                retira, exentas.Contains(c.Cuenta.Id), bloqueos);
        }).ToList();
        var respuesta = new CuentasDePersonaDto(captaciones, aportes, solicitud.PersonPublicId, solicitud.Date, consultadas);
        await auditoria.LecturaAsync(AuditEventTypes.LendingAccountContractQueried, "Person", solicitud.PersonPublicId, solicitud.Collector,
            new { date = solicitud.Date, recordedAsOf = reloj.UtcNow, count = consultadas.Count }, ct);
        return new(ResultadoDelContrato.Answered, AuditEventTypes.LendingAccountContractQueried, "Cuentas consultadas.", respuesta);
    }

    // ------------------------------------------------------------------------------------------------------------ exigibles --

    public async Task<RespuestaDelContratoDto<ExigiblesDeCuentasDto>> ListarExigiblesAsync(SolicitudDeExigibles solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var forma = await validadorDeExigibles.ValidateAsync(solicitud, ct);
        if (!forma.IsValid) return Rechazo<ExigiblesDeCuentasDto>(Invalida(forma));
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var (captaciones, aportes) = (Estado(fechas, LineNature.Deposit), Estado(fechas, LineNature.Contribution));
        if (captaciones == EstadoDeCartera.NotActivated && aportes == EstadoDeCartera.NotActivated)
            return new(ResultadoDelContrato.Answered, ContratoDeCuentasSinActivar.CodigoCaptacionesNoActivadas,
                ContratoDeCuentasSinActivar.MensajeCaptacionesNoActivadas, new ExigiblesDeCuentasDto(captaciones, aportes, [], []));
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta) return Rechazo<ExigiblesDeCuentasDto>(noCuenta);

        var exigibles = await ExigiblesAsync(solicitud.PersonPublicIds, solicitud.Date, solicitud.PeriodEnd, fechas.DeCaptacionEn(DateOnly.MaxValue), ct);
        // US9 (T155): las personas de una carga aplicada sin activar (contrato-de-cuentas §3.7): su cuota existe pero todavía no se puede pagar aquí.
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => solicitud.PersonPublicIds.Contains(p.PublicId))
            .Select(p => new { p.Id, p.PublicId }).ToListAsync(ct);
        var migradas = await Deposits.Migration.CargaVigenteDeCaptaciones.PersonasAsync(db, personas.Select(p => p.Id).ToList(), ct);
        var respuesta = new ExigiblesDeCuentasDto(captaciones, aportes, exigibles, personas.Where(p => migradas.Contains(p.Id)).Select(p => p.PublicId).ToList());
        await auditoria.LecturaAsync(AuditEventTypes.LendingAccountContractListed, "Person", solicitud.PersonPublicIds.Count == 1 ? solicitud.PersonPublicIds[0] : null,
            solicitud.Collector, new { date = solicitud.Date, people = solicitud.PersonPublicIds.Count, count = exigibles.Count,
                sha256 = HuellaDeOperacion.Calcular("Listed", respuesta) }, ct);
        return new(ResultadoDelContrato.Answered, AuditEventTypes.LendingAccountContractListed, "Cuotas exigibles listadas.", respuesta);
    }

    /// <summary>
    /// Las cuotas exigibles de unas personas (FR-087, §3.6): las de sus cuentas de aportes y de ahorro permanente o contractual vivas de las naturalezas
    /// activadas, con lo pendiente y, con <paramref name="hasta"/>, sólo las que vencen hasta ese día (las atrasadas siempre). En bloque, con la
    /// prelación dentro de la persona: vencimiento y luego la línea. También la usan Nómina y los informes por el contrato.
    /// </summary>
    public async Task<IReadOnlyList<ExigibleDeCuentaDto>> ExigiblesAsync(IReadOnlyList<Guid> personas, DateOnly fecha, DateOnly? hasta,
        IReadOnlyList<LineNature> naturalezas, CancellationToken ct)
    {
        var publicos = personas.Distinct().ToList();
        var porPersona = await db.People.AsNoTracking().Where(p => publicos.Contains(p.PublicId)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var ids = porPersona.Keys.ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.HolderPersonId) && !a.IsDeleted && naturalezas.Contains(a.Nature)
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive)
                        && (a.Nature == LineNature.Contribution || a.DepositClass == DepositClass.Permanent || a.DepositClass == DepositClass.Contractual))
            .ToListAsync(ct);
        if (cuentas.Count == 0) return [];
        var lineas = await LineasAsync(cuentas.Select(c => c.CreditLineId), ct);
        var extraordinarios = new Dictionary<int, bool>();
        foreach (var lineaId in cuentas.Where(c => c.Nature == LineNature.Contribution).Select(c => c.CreditLineId).Distinct())
            extraordinarios[lineaId] = (await Deposits.Lines.LectorDeLineasDeCaptacion.VigenteDeAportesAsync(db, lineaId, fecha, null, ct))?.AllowsExtraordinary
                                        ?? false;
        var tope = hasta ?? DateOnly.MaxValue;
        var cuotas = (await LectorDeCuotas.DeCuentasAsync(db, cuentas.Select(c => c.Id).ToList(), ct))
            .Where(q => q.Pendiente > 0m && q.Cuota.DueDate <= tope).ToList();
        var porCuenta = cuentas.ToDictionary(c => c.Id);
        return cuotas
            .Select(q => (Cuota: q, Cuenta: porCuenta[q.Cuota.MemberAccountId]))
            .GroupBy(x => x.Cuenta.HolderPersonId)
            .SelectMany(g => g.OrderBy(x => x.Cuota.Cuota.DueDate).ThenBy(x => lineas.GetValueOrDefault(x.Cuenta.CreditLineId).Code, StringComparer.Ordinal)
                .ThenBy(x => x.Cuenta.Id)
                .Select((x, i) =>
                {
                    var aportes = x.Cuenta.Nature == LineNature.Contribution;
                    var extra = aportes && extraordinarios.GetValueOrDefault(x.Cuenta.CreditLineId);
                    return new ExigibleDeCuentaDto(porPersona[x.Cuenta.HolderPersonId], x.Cuenta.PublicId, x.Cuenta.Number, (NaturalezaDeCuenta)(int)x.Cuenta.Nature,
                        x.Cuenta.DepositClass is { } k ? (ClaseDeCaptacion)(int)k : null, lineas.GetValueOrDefault(x.Cuenta.CreditLineId).Code ?? string.Empty,
                        x.Cuota.Cuota.PublicId, x.Cuota.Cuota.Periodo, x.Cuota.Cuota.DueDate, x.Cuota.Cuota.Amount, x.Cuota.Pendiente, extra,
                        extra ? ConsignacionEnCuentas.ComoExtraordinario : string.Empty, aportes ? ConceptoDeAportes : ConceptoDeAhorros, i + 1);
                }))
            .ToList();
    }

    // ------------------------------------------------------------------------------------------------------------- simular --

    public async Task<RespuestaDelContratoDto<SimulacionDeCuentaDto>> SimularAsync(SolicitudDeSimulacionDeCuenta solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var forma = await validadorDeSimulacion.ValidateAsync(solicitud, ct);
        if (!forma.IsValid) return Rechazo<SimulacionDeCuentaDto>(Invalida(forma));
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == solicitud.AccountPublicId && !a.IsDeleted, ct);
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var naturaleza = cuenta?.Nature ?? LineNature.Deposit;
        if (!fechas.Activada(naturaleza))
        {
            var (codigo, mensaje) = ContratoDeCuentasSinActivar.NoActivada(naturaleza);
            return new(ResultadoDelContrato.Answered, codigo, mensaje, NoPermitida(mensaje));
        }
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta) return Rechazo<SimulacionDeCuentaDto>(noCuenta);
        if (cuenta is null) return Rechazo<SimulacionDeCuentaDto>(new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta no existe."));

        var simulacion = solicitud.Operation switch
        {
            "Withdrawal" => await SimularRetiroAsync(cuenta, solicitud, ct),
            "Transfer" => await SimularTrasladoAsync(cuenta, solicitud, ct),
            _ => await SimularConsignacionAsync(cuenta, solicitud, ct),
        };
        var (dto, rechazo) = simulacion;
        await auditoria.LecturaAsync(AuditEventTypes.LendingAccountContractSimulated, nameof(MemberAccount), cuenta.PublicId, solicitud.Collector,
            new { operation = solicitud.Operation, amount = solicitud.Amount, valueDate = solicitud.ValueDate, allowed = dto.Allowed,
                  sha256 = dto.SimulationId }, ct);
        return new(ResultadoDelContrato.Answered, rechazo?.Code ?? AuditEventTypes.LendingAccountContractSimulated, rechazo?.Message ?? "Operación simulada.", dto);
    }

    /// <summary>La simulación de una consignación (§3.7): el reparto, el canje, el origen de fondos, la atribución del efectivo y los cargos.</summary>
    private async Task<(SimulacionDeCuentaDto, Error?)> SimularConsignacionAsync(MemberAccount cuenta, SolicitudDeSimulacionDeCuenta s, CancellationToken ct)
    {
        var leida = await movimientos.LeerAsync(cuenta, s.ValueDate, ct);
        var antes = ReglasDeMovimientos.Dto(leida.Disponible);
        Error? rechazo = await consignacion.OperableAsync(cuenta, ct) ?? ConsignacionEnCuentas.ClaseDeMedio(s.Collector.Class, s.MeansClass)
            ?? await movimientos.SoloAsociadosAsync(leida, s.ValueDate, ct)
            ?? await movimientos.TopesAsync(leida, s.Amount, s.MeansClass, esRetiro: false, s.ValueDate, ct);
        IReadOnlyList<DistribucionEnCuotaDto> cubiertas = [];
        var modalidad = s.Modality ?? (cuenta.Nature == LineNature.Deposit && cuenta.DepositClass == DepositClass.Demand
            ? ModalidadDeConsignacion.Free
            : ModalidadDeConsignacion.Installments);
        if (rechazo is null)
        {
            var reparto = await consignacion.RepartirAsync(cuenta, modalidad, s.Amount, null, s.ValueDate, ct);
            if (reparto.IsFailure) rechazo = reparto.Error;
            else cubiertas = reparto.Value.PorCuota;
        }
        var canje = cuenta.Nature == LineNature.Deposit && await movimientos.CanjeHastaAsync(s.MeansClass, s.ValueDate, ct) is not null ? s.Amount : 0m;
        var despues = DisponibleDeCuenta.Calcular(cuenta.Nature, leida.Disponible.Saldo + s.Amount, leida.Disponible.Retenido, leida.Disponible.Embargado,
            leida.Disponible.EnCanje + canje, leida.Disponible.Minimo);
        var atribucion = s.MeansClass == ClaseDeMedioDeRecaudo.Cash
            ? await movimientos.AtribucionRequeridaAsync(ReglasDeMovimientos.PermisoDeConsignacion, s.Amount, s.ValueDate, ct)
            : null;
        var propuestos = cuenta.Nature == LineNature.Deposit && rechazo is null
            ? await cargos.PorEventoAsync(leida, ChargeTrigger.DepositOverFree, s.Amount, s.ValueDate, despues.Disponible, ct)
            : [];
        var dto = new SimulacionDeCuentaDto(HuellaDeOperacion.Calcular("Lending.AccountContract.Simulate", s), rechazo is null, antes,
            rechazo is null ? ReglasDeMovimientos.Dto(despues) : antes, null, Cargos(propuestos), [], atribucion,
            s.MeansClass == ClaseDeMedioDeRecaudo.Cash && await LectorDeCuentas.UmbralDeOrigenDeFondosAsync(parametros, s.ValueDate, ct) is { } umbral
            && s.Amount >= umbral,
            cubiertas, rechazo is null ? Avisos(atribucion) : [rechazo.Message]);
        return (dto, rechazo);
    }

    /// <summary>La simulación de un retiro (§3.7, §3.9): firmas, disponible con el GMF, topes, atribución y cargos, sin pedir aprobaciones.</summary>
    private async Task<(SimulacionDeCuentaDto, Error?)> SimularRetiroAsync(MemberAccount cuenta, SolicitudDeSimulacionDeCuenta s, CancellationToken ct)
    {
        var pedido = new RetiroPedido(s.Collector, null, null, cuenta.PublicId, s.Amount, s.ValueDate, s.MeansClass, s.SignerPersonPublicIds, null, null, null,
            PrevalidationOutcome.NotApplicable, [], null);
        var revisado = await retiros.RevisarAsync(cuenta, pedido, pedirAprobacion: false, ct);
        return await SimulacionDeSalidaAsync(cuenta, s, revisado.IsSuccess ? revisado.Value : null, revisado.IsFailure ? revisado.Error : null,
            ClaseDeMedio(s), ct);
    }

    /// <summary>La simulación de un traslado (§3.7, §3.10): el retiro en el origen y la consignación en el destino, con el GMF de su hecho generador.</summary>
    private async Task<(SimulacionDeCuentaDto, Error?)> SimularTrasladoAsync(MemberAccount cuenta, SolicitudDeSimulacionDeCuenta s, CancellationToken ct)
    {
        var destino = s.TargetAccountPublicId is { } d
            ? await db.MemberAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == d && !a.IsDeleted, ct)
            : null;
        if (destino is null)
            return await SimulacionDeSalidaAsync(cuenta, s, null, new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta destino no existe."), null, ct);
        var pedido = new TrasladoPedido(s.Collector, null, null, cuenta.PublicId, destino.PublicId, s.Amount, s.ValueDate, s.SignerPersonPublicIds,
            s.Modality ?? ModalidadDeConsignacion.Free, null, null, PrevalidationOutcome.NotApplicable, [], null);
        var revisado = await traslados.RevisarAsync(cuenta, destino, pedido, pedirAprobacion: false, ct);
        var (dto, rechazo) = await SimulacionDeSalidaAsync(cuenta, s, revisado.IsSuccess ? revisado.Value.Origen : null,
            revisado.IsFailure ? revisado.Error : null, null, ct);
        return (revisado.IsSuccess ? dto with { InstallmentsCovered = revisado.Value.Reparto.PorCuota } : dto, rechazo);
    }

    private async Task<(SimulacionDeCuentaDto, Error?)> SimulacionDeSalidaAsync(MemberAccount cuenta, SolicitudDeSimulacionDeCuenta s, RetiroRevisado? revisado,
        Error? rechazo, Error? medio, CancellationToken ct)
    {
        rechazo ??= medio;
        var leida = revisado?.Cuenta ?? await movimientos.LeerAsync(cuenta, s.ValueDate, ct);
        var (firmas, personas) = revisado is { Firmas: { } f } ? (f, revisado.Personas) : await movimientos.FirmasAsync(leida, s.SignerPersonPublicIds, s.Amount, s.ValueDate, ct);
        var atribucion = await movimientos.AtribucionRequeridaAsync(ReglasDeMovimientos.PermisoDeRetiro, s.Amount, s.ValueDate, ct);
        var dto = new SimulacionDeCuentaDto(HuellaDeOperacion.Calcular("Lending.AccountContract.Simulate", s), rechazo is null,
            ReglasDeMovimientos.Dto(leida.Disponible), revisado is null || rechazo is not null ? ReglasDeMovimientos.Dto(leida.Disponible) : ReglasDeMovimientos.Dto(revisado.Despues),
            ReglasDeMovimientos.Dto(revisado?.Gmf), Cargos(revisado?.Cargos ?? []), firmas.Faltantes.Select(x => personas.GetValueOrDefault(x)).ToList(), atribucion,
            false, [], rechazo is null ? Avisos(atribucion) : [rechazo.Message]);
        return (dto, rechazo);
    }

    /// <summary>El efectivo o el cheque los entrega el recaudador (§3.9): otra clase de medio no se retira por el contrato.</summary>
    private static Error? ClaseDeMedio(SolicitudDeSimulacionDeCuenta s) =>
        s.MeansClass is ClaseDeMedioDeRecaudo.Cash or ClaseDeMedioDeRecaudo.Check ? null : ErroresDeAportes.ClaseDeMedio(s.MeansClass, s.Collector.Class);

    private static IReadOnlyList<CargoCalculadoDto> Cargos(IReadOnlyList<Deposits.Charges.CargoPropuesto> propuestos) =>
        propuestos.Where(p => p.Calculo.SeCausa)
            .Select(p => new CargoCalculadoDto(p.Definicion.Code, p.Calculo.Valor, p.Definicion.IsVatTaxed,
                p.Calculo.Resultado == ResultadoDeCargo.Cobrado ? nameof(AccountChargeStatus.Collected) : nameof(AccountChargeStatus.Pending)))
            .ToList();

    private static IReadOnlyList<string> Avisos(AtribucionRequeridaDto? atribucion) => atribucion is null
        ? []
        : [$"La operación supera la atribución de quien la registra ({atribucion.MaxAmount:N2}): necesitará un aprobador presente con segundo factor."];

    // ------------------------------------------------------------------------------------------------------------ consignar --

    public async Task<RespuestaDeConsignacionDto> ConsignarAsync(SolicitudDeConsignacion solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var consignaciones = solicitud.Deposits ?? [];
        var noDescontados = solicitud.NotDeducted ?? [];
        var claves = consignaciones.Select(c => c.IdempotencyKey).Concat(noDescontados.Select(n => n.IdempotencyKey)).Distinct().ToList();
        await auditoria.IntentoAsync(AuditEventTypes.LendingAccountContractAttempted, Entidad, claves.FirstOrDefault() ?? string.Empty,
            new { method = nameof(ConsignarAsync), collector = solicitud.Collector?.Code, keys = claves, items = consignaciones.Count + noDescontados.Count,
                  total = consignaciones.Sum(c => c.Amount), sha256 = HuellaDeOperacion.Calcular(ConsignacionEnCuentas.Metodo, solicitud) }, ct);

        var forma = await validadorDeConsignacion.ValidateAsync(solicitud, ct);
        Error? lote = !forma.IsValid ? Invalida(forma) : !transaccion.HayEnCurso() ? ErroresDePagos.TransaccionRequerida() : null;
        if (lote is null && await RecaudadorAsync(solicitud.Collector!, ct) is { } noCuenta) lote = noCuenta;
        if (lote is not null)
        {
            foreach (var clave in claves) await AuditarAsync(AuditEventTypes.LendingAccountContractRejected, clave, solicitud.Collector, new { code = lote.Code, message = lote.Message }, ct);
            if (lote.Code != ErroresDePagos.CodigoTransaccionRequerida) await db.SaveChangesAsync(ct);
            return new RespuestaDeConsignacionDto(consignaciones.Select(_ => Rechazo<ComprobanteDeCuentaDto>(lote)).ToList(),
                noDescontados.Select(_ => Rechazo<NoDescontadoDeCuentaRegistradoDto>(lote)).ToList());
        }

        // La naturaleza de cada cuenta del lote y su activación (§3.13): un ítem de una naturaleza sin activar se rechaza con su código.
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var publicos = consignaciones.Select(c => c.AccountPublicId).Concat(noDescontados.Select(n => n.AccountPublicId)).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => publicos.Contains(a.PublicId) && !a.IsDeleted).ToDictionaryAsync(a => a.PublicId, ct);
        var previas = await ValidacionPreviaAsync(solicitud.Collector, cuentas.Values.Where(a => fechas.Activada(a.Nature)).ToList(), ct);

        // El cerrojo de todo el lote en una pasada (§3.4 paso 7): setup compartido, los titulares y las cuentas por Id.
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync(cuentas.Values.Select(a => a.HolderPersonId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
        await cerrojo.BloquearCuentasAsync(cuentas.Values.Select(a => a.Id).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);

        var respuestas = new List<RespuestaDelContratoDto<ComprobanteDeCuentaDto>>(consignaciones.Count);
        foreach (var item in consignaciones)
        {
            var cuenta = cuentas.GetValueOrDefault(item.AccountPublicId);
            Result<ConsignacionAplicada> r;
            if (cuenta is not null && !fechas.Activada(cuenta.Nature))
                r = Result.Failure<ConsignacionAplicada>(NoActivada(cuenta.Nature));
            else if (cuenta is not null && previas.GetValueOrDefault(cuenta.Id) is { Bloqueo: { } bloqueo })
                r = Result.Failure<ConsignacionAplicada>(bloqueo);
            else
            {
                var previa = cuenta is null ? null : previas.GetValueOrDefault(cuenta.Id);
                r = await consignacion.AplicarAsync(new ConsignacionEnCurso(solicitud.Collector, item, previa?.Outcome ?? PrevalidationOutcome.NotApplicable,
                    previa?.Avisos ?? []), ct);
            }
            if (r.IsSuccess)
            {
                var c = r.Value.Comprobante;
                await AuditarAsync(AuditEventTypes.LendingAccountContractDeposited, item.IdempotencyKey, solicitud.Collector,
                    new { operation = c.Number, account = c.AccountNumber, valueDate = c.ValueDate, amount = c.Amount, transactions = c.Transactions,
                          installments = c.InstallmentsCovered, alreadyApplied = r.Value.YaAplicada }, ct);
                await db.SaveChangesAsync(ct);
                respuestas.Add(new(r.Value.YaAplicada ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied,
                    AuditEventTypes.LendingAccountContractDeposited, r.Value.YaAplicada ? MensajeYaConsignada : MensajeConsignada, c));
            }
            else
            {
                await AuditarAsync(AuditEventTypes.LendingAccountContractRejected, item.IdempotencyKey, solicitud.Collector,
                    new { code = r.Error.Code, message = r.Error.Message, account = item.AccountPublicId, amount = item.Amount }, ct);
                await db.SaveChangesAsync(ct);
                respuestas.Add(Rechazo<ComprobanteDeCuentaDto>(r.Error));
            }
        }

        var registrados = new List<RespuestaDelContratoDto<NoDescontadoDeCuentaRegistradoDto>>(noDescontados.Count);
        foreach (var n in noDescontados)
        {
            var cuenta = cuentas.GetValueOrDefault(n.AccountPublicId);
            var r = cuenta is null
                ? Rechazo<NoDescontadoDeCuentaRegistradoDto>(new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta no existe."))
                : !fechas.Activada(cuenta.Nature)
                    ? Rechazo<NoDescontadoDeCuentaRegistradoDto>(NoActivada(cuenta.Nature))
                    : await NoDescontadoAsync(solicitud.Collector, cuenta, n, ct);
            await AuditarAsync(r.Result == ResultadoDelContrato.Rejected ? AuditEventTypes.LendingAccountContractRejected : AuditEventTypes.LendingAccountContractDeposited,
                n.IdempotencyKey, solicitud.Collector, new { notDeducted = n.NotDeductedAmount, expected = n.ExpectedAmount, reason = n.Reason.ToString(),
                    installment = n.InstallmentPublicId, sourceItemPublicId = n.SourceItemPublicId, code = r.Code }, ct);
            registrados.Add(r);
        }
        await db.SaveChangesAsync(ct);
        return new RespuestaDeConsignacionDto(respuestas, registrados);
    }

    private async Task<RespuestaDelContratoDto<NoDescontadoDeCuentaRegistradoDto>> NoDescontadoAsync(Recaudador recaudador, MemberAccount cuenta,
        NoDescontadoDeCuenta n, CancellationToken ct)
    {
        var cuota = await db.AccountInstallments.AsNoTracking().FirstOrDefaultAsync(q => q.PublicId == n.InstallmentPublicId && !q.IsDeleted, ct);
        if (cuota is null || cuota.MemberAccountId != cuenta.Id) return Rechazo<NoDescontadoDeCuentaRegistradoDto>(ErroresDeAportes.CuotaAjena(n.InstallmentPublicId));
        var dto = new NoDescontadoDeCuentaRegistradoDto(cuota.PublicId, n.NotDeductedAmount, n.Reason.ToString());
        var ya = await db.AccountInstallmentNonDeductions.AsNoTracking()
            .AnyAsync(x => x.InstallmentId == cuota.Id && x.CollectorCode == recaudador.Code && x.ReferenceKey == n.IdempotencyKey, ct);
        if (ya) return new(ResultadoDelContrato.AlreadyApplied, AuditEventTypes.LendingAccountContractDeposited, MensajeNoDescontado, dto);
        registro.RegistrarNoDescontado(new AccountInstallmentNonDeduction
        {
            InstallmentId = cuota.Id,
            Origin = recaudador.Class == ClaseDeRecaudador.Payroll ? NonDeductionOrigin.Contract : NonDeductionOrigin.PaymasterResult,
            Reason = (NonDeductionReason)(int)n.Reason,
            RequestedAmount = n.ExpectedAmount,
            DeductedAmount = n.ExpectedAmount - n.NotDeductedAmount,
            CollectorCode = recaudador.Code,
            ReferenceKey = n.IdempotencyKey,
            OccurredOn = reloj.HoyLocal,
            Detail = string.IsNullOrWhiteSpace(n.Detail) ? null : n.Detail.Trim(),
        });
        return new(ResultadoDelContrato.Applied, AuditEventTypes.LendingAccountContractDeposited, MensajeNoDescontado, dto);
    }

    // ------------------------------------------------------------------------------------------------- retirar y trasladar --

    public async Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> RetirarAsync(SolicitudDeRetiro solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var huella = HuellaDeOperacion.Calcular(RetiroDeCuenta.Metodo, solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingAccountContractAttempted, Entidad, solicitud.IdempotencyKey ?? string.Empty,
            new { method = nameof(RetirarAsync), collector = solicitud.Collector?.Code, account = solicitud.AccountPublicId, amount = solicitud.Amount, sha256 = huella },
            ct);
        var forma = await validadorDeRetiro.ValidateAsync(solicitud, ct);
        Error? lote = !forma.IsValid ? Invalida(forma) : !transaccion.HayEnCurso() ? ErroresDePagos.TransaccionRequerida() : null;
        if (lote is not null) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(lote, solicitud.IdempotencyKey, solicitud.Collector, ct);
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == solicitud.AccountPublicId && !a.IsDeleted, ct);
        var naturaleza = cuenta?.Nature ?? LineNature.Deposit;
        if (!(await ActivacionesDeCaptacion.LeerAsync(db, ct)).Activada(naturaleza))
            return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(NoActivada(naturaleza), solicitud.IdempotencyKey, solicitud.Collector, ct);
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta)
            return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(noCuenta, solicitud.IdempotencyKey, solicitud.Collector, ct);
        if (cuenta is null)
            return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(new Error(ErroresDePagos.CodigoNoEncontrado, "La cuenta no existe."), solicitud.IdempotencyKey,
                solicitud.Collector, ct);
        // Un retiro en efectivo lo autoriza la Cartera y lo entrega el recaudador (FR-088): efectivo o cheque; otra clase de medio no.
        Error? previo = solicitud.MeansClass is ClaseDeMedioDeRecaudo.Cash or ClaseDeMedioDeRecaudo.Check
            ? ConsignacionEnCuentas.ClaseDeMedio(solicitud.Collector.Class, solicitud.MeansClass)
            : ErroresDeAportes.ClaseDeMedio(solicitud.MeansClass, solicitud.Collector.Class);
        if (cuenta.Nature == LineNature.Contribution) previo ??= ErroresDeAportes.NoRetirable();
        // La cancelación en efectivo por el contrato sigue sin entrega hasta que exista Recaudo (E99): se cancela desde la Cartera (E234).
        if (solicitud.CloseAccount) previo ??= ErroresDeAportes.CancelacionPorElContratoNoDisponible();
        if (previo is not null) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(previo, solicitud.IdempotencyKey, solicitud.Collector, ct);

        var previa = (await ValidacionPreviaAsync(solicitud.Collector, [cuenta], ct, RetiroDeCuentaAplicadoV1.Type)).GetValueOrDefault(cuenta.Id);
        if (previa is { Bloqueo: { } bloqueo }) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(bloqueo, solicitud.IdempotencyKey, solicitud.Collector, ct);

        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
        await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);

        var r = await retiros.AplicarAsync(new RetiroPedido(solicitud.Collector, solicitud.IdempotencyKey, huella, solicitud.AccountPublicId, solicitud.Amount,
            solicitud.ValueDate, solicitud.MeansClass, solicitud.SignerPersonPublicIds, solicitud.ReceiverPersonPublicId, solicitud.Receipt, solicitud.ApprovalPublicId,
            previa?.Outcome ?? PrevalidationOutcome.NotApplicable, previa?.Avisos ?? [], null), ct);
        if (r.IsFailure) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(r.Error, solicitud.IdempotencyKey, solicitud.Collector, ct);
        var c = r.Value.Comprobante;
        await AuditarAsync(AuditEventTypes.LendingAccountContractWithdrawn, solicitud.IdempotencyKey, solicitud.Collector,
            new { operation = c.Number, account = c.AccountNumber, valueDate = c.ValueDate, amount = c.Amount, gmf = c.Gmf?.Amount, transactions = c.Transactions,
                  approval = solicitud.ApprovalPublicId, alreadyApplied = r.Value.YaAplicado }, ct);
        await db.SaveChangesAsync(ct);
        return new(r.Value.YaAplicado ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied, AuditEventTypes.LendingAccountContractWithdrawn,
            r.Value.YaAplicado ? MensajeYaRetirada : MensajeRetirada, c);
    }

    public async Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> TrasladarAsync(SolicitudDeTraslado solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var huella = HuellaDeOperacion.Calcular(TrasladoDeCuenta.Metodo, solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingAccountContractAttempted, Entidad, solicitud.IdempotencyKey ?? string.Empty,
            new { method = nameof(TrasladarAsync), collector = solicitud.Collector?.Code, source = solicitud.SourceAccountPublicId,
                  target = solicitud.TargetAccountPublicId, amount = solicitud.Amount, sha256 = huella }, ct);
        var forma = await validadorDeTraslado.ValidateAsync(solicitud, ct);
        Error? lote = !forma.IsValid ? Invalida(forma) : !transaccion.HayEnCurso() ? ErroresDePagos.TransaccionRequerida() : null;
        if (lote is not null) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(lote, solicitud.IdempotencyKey, solicitud.Collector, ct);
        var origen = await db.MemberAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == solicitud.SourceAccountPublicId && !a.IsDeleted, ct);
        var destino = await db.MemberAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == solicitud.TargetAccountPublicId && !a.IsDeleted, ct);
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        foreach (var n in new[] { origen?.Nature ?? LineNature.Deposit, destino?.Nature ?? LineNature.Deposit }.Distinct())
            if (!fechas.Activada(n)) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(NoActivada(n), solicitud.IdempotencyKey, solicitud.Collector, ct);
        if (await RecaudadorAsync(solicitud.Collector, ct) is { } noCuenta)
            return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(noCuenta, solicitud.IdempotencyKey, solicitud.Collector, ct);
        if (origen is null || destino is null)
            return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(new Error(ErroresDePagos.CodigoNoEncontrado, "Una de las cuentas no existe."),
                solicitud.IdempotencyKey, solicitud.Collector, ct);
        if (origen.Nature == LineNature.Contribution)
            return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(ErroresDeAportes.NoRetirable(), solicitud.IdempotencyKey, solicitud.Collector, ct);

        var previa = (await ValidacionPreviaAsync(solicitud.Collector, [origen], ct, TrasladoEntreCuentasAplicadoV1.Type)).GetValueOrDefault(origen.Id);
        if (previa is { Bloqueo: { } bloqueo }) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(bloqueo, solicitud.IdempotencyKey, solicitud.Collector, ct);

        // R08: setup → personas → cuentas, cada nivel por Id, en una pasada.
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync(new[] { origen.HolderPersonId, destino.HolderPersonId }.Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
        await cerrojo.BloquearCuentasAsync(new[] { origen.Id, destino.Id }.Order().ToList(), ModoDeEspera.Esperar, ct);

        var r = await traslados.AplicarAsync(new TrasladoPedido(solicitud.Collector, solicitud.IdempotencyKey, huella, solicitud.SourceAccountPublicId,
            solicitud.TargetAccountPublicId, solicitud.Amount, solicitud.ValueDate, solicitud.SignerPersonPublicIds, solicitud.TargetModality, solicitud.Receipt,
            solicitud.ApprovalPublicId, previa?.Outcome ?? PrevalidationOutcome.NotApplicable, previa?.Avisos ?? [], null), ct);
        if (r.IsFailure) return await RechazoAuditadoAsync<ComprobanteDeCuentaDto>(r.Error, solicitud.IdempotencyKey, solicitud.Collector, ct);
        var c = r.Value.Comprobante;
        await AuditarAsync(AuditEventTypes.LendingAccountContractTransferred, solicitud.IdempotencyKey, solicitud.Collector,
            new { operation = c.Number, account = c.AccountNumber, target = c.TargetAccountPublicId, amount = c.Amount, gmf = c.Gmf?.Amount,
                  transactions = c.Transactions, alreadyApplied = r.Value.YaAplicado }, ct);
        await db.SaveChangesAsync(ct);
        return new(r.Value.YaAplicado ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied, AuditEventTypes.LendingAccountContractTransferred,
            r.Value.YaAplicado ? "El traslado ya estaba aplicado con esta clave: se devuelve el comprobante original." : MensajeTrasladada, c);
    }

    // ------------------------------------------------------------------------------------------------------------- reversar --

    public async Task<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>> ReversarAsync(SolicitudDeReversoDeCuenta solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await auditoria.IntentoAsync(AuditEventTypes.LendingAccountContractAttempted, Entidad, solicitud.IdempotencyKey ?? string.Empty,
            new { method = nameof(ReversarAsync), collector = solicitud.Collector?.Code, sha256 = HuellaDeOperacion.Calcular("Lending.AccountContract.Reverse", solicitud) },
            ct);
        var forma = await validadorDeReverso.ValidateAsync(solicitud, ct);
        Error? lote = !forma.IsValid ? Invalida(forma) : !transaccion.HayEnCurso() ? ErroresDePagos.TransaccionRequerida() : null;
        if (lote is null && await RecaudadorAsync(solicitud.Collector!, ct) is { } noCuenta) lote = noCuenta;
        if (lote is not null) return await RechazoAuditadoAsync<ComprobanteDeReversoDeCuentaDto>(lote, solicitud, ct);

        var original = await reversion.OperacionAsync(solicitud.Collector.Code, solicitud.IdempotencyKey, ct);
        if (original is null) return await RechazoAuditadoAsync<ComprobanteDeReversoDeCuentaDto>(ErroresDeAportes.OperacionInexistente(), solicitud, ct);
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == original.MemberAccountId, ct);
        if (!(await ActivacionesDeCaptacion.LeerAsync(db, ct)).Activada(cuenta.Nature))
            return await RechazoAuditadoAsync<ComprobanteDeReversoDeCuentaDto>(NoActivada(cuenta.Nature), solicitud, ct);

        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
        var ids = new[] { original.MemberAccountId, original.CounterpartMemberAccountId ?? original.MemberAccountId }.Distinct().Order().ToList();
        await cerrojo.BloquearCuentasAsync(ids, ModoDeEspera.Esperar, ct);

        var r = await reversion.ReversarAsync(original, solicitud.Reason, EmisionDeCartera.ReversoIniciadoPorElRecaudador, ct);
        if (r.IsFailure) return await RechazoAuditadoAsync<ComprobanteDeReversoDeCuentaDto>(r.Error, solicitud, ct);
        var c = r.Value.Comprobante;
        await AuditarAsync(AuditEventTypes.LendingAccountContractReversed, solicitud.IdempotencyKey, solicitud.Collector,
            new { operation = c.Number, reversed = c.ReversedNumber, amount = c.ReversedAmount, transactions = c.Transactions, overdraft = c.LeftTechnicalOverdraft,
                  reason = solicitud.Reason }, ct);
        await db.SaveChangesAsync(ct);
        return new(r.Value.YaReversada ? ResultadoDelContrato.AlreadyApplied : ResultadoDelContrato.Applied, AuditEventTypes.LendingAccountContractReversed,
            MensajeReversada, c);
    }

    // ------------------------------------------------------------------------------------------------------------- comunes --

    private static EstadoDeCartera Estado(FechasDeActivacion fechas, LineNature naturaleza) =>
        fechas.Activada(naturaleza) ? EstadoDeCartera.Active : EstadoDeCartera.NotActivated;

    private static Error NoActivada(LineNature naturaleza)
    {
        var (codigo, mensaje) = ContratoDeCuentasSinActivar.NoActivada(naturaleza);
        return new ErrorConDatos(codigo, mensaje, new { nature = naturaleza.ToString() });
    }

    private static SimulacionDeCuentaDto NoPermitida(string mensaje)
    {
        var cero = new DisponibleDto(0m, 0m, 0m, 0m, 0m, 0m);
        return new SimulacionDeCuentaDto(string.Empty, false, cero, cero, null, [], [], null, false, [], [mensaje]);
    }

    private async Task<LineNature> NaturalezaAsync(Guid cuenta, CancellationToken ct) =>
        await db.MemberAccounts.AsNoTracking().Where(a => a.PublicId == cuenta).Select(a => (LineNature?)a.Nature).FirstOrDefaultAsync(ct)
        ?? LineNature.Deposit;

    private async Task<Dictionary<int, (string? Code, string? Name)>> LineasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lista.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => ((string?)l.Code, (string?)l.Name), ct);
    }

    private async Task<Dictionary<int, IReadOnlyList<FirmanteDto>>> FirmantesAsync(IReadOnlyCollection<int> cuentas, CancellationToken ct)
    {
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var filas = await (from p in db.MemberAccountParties.AsNoTracking()
                           join persona in db.People.AsNoTracking().IgnoreQueryFilters() on p.PersonId equals persona.Id
                           where ids.Contains(p.MemberAccountId) && p.ValidTo == null && !p.IsDeleted && p.Role != MemberAccountPartyRole.Beneficiary
                           select new { p.MemberAccountId, Persona = persona, p.Role, p.SignatureLimit }).ToListAsync(ct);
        return filas.GroupBy(f => f.MemberAccountId).ToDictionary(g => g.Key,
            g => (IReadOnlyList<FirmanteDto>)g.Select(f => new FirmanteDto(f.Persona.PublicId, ReferenciasDeGobierno.Nombre(f.Persona), (RolEnLaCuenta)(int)f.Role,
                f.SignatureLimit)).ToList());
    }

    /// <summary>
    /// ¿Cuenta el recaudador para el contrato de cuentas? (§3.3): fila activa a hoy con rol <c>ObligationsCollector</c>, su clase y la adopción de
    /// <c>ProveedorDeCuentas</c> en una versión que este despliegue soporta; la de <c>ProveedorDeObligaciones</c> no habilita cuentas.
    /// </summary>
    private async Task<Error?> RecaudadorAsync(Recaudador r, CancellationToken ct)
    {
        var vigente = await registroDeIntegracion.RecaudadorAsync(r.Code, reloj.HoyLocal, ct);
        if (vigente is null || !string.Equals(vigente.Class.ToString(), r.Class.ToString(), StringComparison.Ordinal))
            return ErroresDePagos.RecaudadorNoRegistrado(r.Code);
        var adopcion = vigente.Adopciones.FirstOrDefault(a => a.Contrato == ContratosDeIntegracion.ProveedorDeCuentas);
        if (adopcion is null) return ErroresDePagos.RecaudadorNoRegistrado(r.Code);
        return ContratosDeIntegracion.VersionesDelContratoDeCuentas.Contains(adopcion.Version)
            ? null
            : new ErrorConDatos(ErroresDePagos.CodigoVersionNoSoportada, $"Cartera no acepta la versión {adopcion.Version} de {ContratosDeIntegracion.ProveedorDeCuentas}.",
                new { contract = ContratosDeIntegracion.ProveedorDeCuentas, version = adopcion.Version, supported = ContratosDeIntegracion.VersionesDelContratoDeCuentas });
    }

    /// <summary>
    /// La validación previa contable del lote (§3.4 paso 6, A8 de la 013): un hecho representativo por combinación de línea, naturaleza y sucursal de las
    /// cuentas, fuera del cerrojo. Con <c>Contabilidad.FaltaDeRegla = Bloquear</c> la combinación que no se contabiliza bloquea sus consignaciones; si no,
    /// el sello va en la operación y el aviso en el comprobante.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, DecisionDeValidacionPrevia>> ValidacionPreviaAsync(Recaudador recaudador, IReadOnlyList<MemberAccount> cuentas,
        CancellationToken ct, string? tipoDeSalida = null)
    {
        if (cuentas.Count == 0) return new Dictionary<int, DecisionDeValidacionPrevia>();
        var lineas = await LineasAsync(cuentas.Select(c => c.CreditLineId), ct);
        var oficinas = cuentas.Select(c => c.BranchId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        // US8 (T145): el titular y el número de una cuenta de cada combinación (la cuenta contable que exige tercero o cruce los pide).
        var personas = cuentas.Select(c => c.HolderPersonId).Distinct().ToList();
        var titulares = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var decisiones = new Dictionary<int, DecisionDeValidacionPrevia>();
        foreach (var grupo in cuentas.GroupBy(c => (c.CreditLineId, c.Nature, c.DepositClass, c.BranchId, c.IsGarnished)))
        {
            var k = grupo.Key;
            var hechos = Representativo(recaudador, lineas.GetValueOrDefault(k.CreditLineId).Code ?? string.Empty, k.Nature, k.DepositClass,
                sucursales.GetValueOrDefault(k.BranchId), reloj.HoyLocal, tipoDeSalida, k.IsGarnished,
                titulares.GetValueOrDefault(grupo.First().HolderPersonId), grupo.First().Number);
            var decision = await validacionPrevia.DecidirAsync(hechos, OrigenDeValidacionPrevia.Contrato, $"el lote de {recaudador.Class}", ct);
            foreach (var cuenta in grupo) decisiones[cuenta.Id] = decision;
        }
        return decisiones;
    }

    /// <summary>
    /// Los hechos de una combinación, sin importes reales: la validación previa sólo mira hecho, sucursal, dimensiones y conceptos. US8 (T145; research
    /// E108): con los representativos de <see cref="HechosRepresentativosDeCaptaciones"/> —su propio origen y la fecha de hoy, el traslado con su destino y
    /// el retiro con su GMF—; hasta el 2026-10-18 iban sin fecha ni origen (todos una unidad del año 1, donde ninguna regla rige) y el traslado sin destino.
    /// </summary>
    public static IReadOnlyList<MensajeContableDto> Representativo(Recaudador recaudador, string linea, LineNature naturaleza, DepositClass? clase, Guid sucursal,
        DateOnly fecha, string? tipoDeSalida = null, bool embargada = false, Guid titular = default, string? numero = null)
    {
        ArgumentNullException.ThrowIfNull(recaudador);
        var d = HechosRepresentativosDeCaptaciones.Cuenta(HechosRepresentativosDeCaptaciones.Dimensiones(linea, naturaleza, clase, sucursal, embargada), titular,
            numero);
        var cobro = recaudador.Class.ToString();
        if (tipoDeSalida == RetiroDeCuentaAplicadoV1.Type)
            return HechosRepresentativosDeCaptaciones.Unidad(fecha, d, HechosRepresentativosDeCaptaciones.Retiro(d, cobro), HechosRepresentativosDeCaptaciones.Gmf(d));
        if (tipoDeSalida == TrasladoEntreCuentasAplicadoV1.Type)
            return HechosRepresentativosDeCaptaciones.Unidad(fecha, d, HechosRepresentativosDeCaptaciones.Traslado(d, d), HechosRepresentativosDeCaptaciones.Gmf(d));
        return HechosRepresentativosDeCaptaciones.Unidad(fecha, d, HechosRepresentativosDeCaptaciones.Consignacion(d, cobro));
    }

    private async Task AuditarAsync(string evento, string clave, Recaudador? recaudador, object detalle, CancellationToken ct) =>
        await auditoria.HechoAsync(evento, Entidad, clave ?? string.Empty,
            new { collector = recaudador?.Code, collectorClass = recaudador?.Class.ToString(), detail = detalle }, ct);

    private Task<RespuestaDelContratoDto<T>> RechazoAuditadoAsync<T>(Error e, SolicitudDeReversoDeCuenta s, CancellationToken ct) =>
        RechazoAuditadoAsync<T>(e, s.IdempotencyKey, s.Collector, ct);

    /// <summary>
    /// Un rechazo auditado en <c>COR_AuditOutbox</c> y guardado (salvo sin transacción): también la solicitud de aprobación que dejó una atribución superada.
    /// </summary>
    private async Task<RespuestaDelContratoDto<T>> RechazoAuditadoAsync<T>(Error e, string? clave, Recaudador? recaudador, CancellationToken ct)
    {
        await AuditarAsync(AuditEventTypes.LendingAccountContractRejected, clave ?? string.Empty, recaudador, new { code = e.Code, message = e.Message }, ct);
        if (e.Code != ErroresDePagos.CodigoTransaccionRequerida) await db.SaveChangesAsync(ct);
        return Rechazo<T>(e);
    }

    private static RespuestaDelContratoDto<T> Rechazo<T>(Error e) =>
        new(ResultadoDelContrato.Rejected, e.Code, e.Message, default, e is ErrorConDatos d ? ContratoDeObligacionesDeCartera.Datos(d) : null);

    private static Error Invalida(FluentValidation.Results.ValidationResult r) =>
        ErroresDePagos.Validacion(r.Errors.Select(e => (JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName), e.ErrorCode, e.ErrorMessage)).ToList());
}
