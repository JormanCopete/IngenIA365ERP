using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>
/// El fondeo pedido de un CDAT (feature 014, US6, T170): por un recaudador por el contrato de cuentas (con su consignación, su huella y la validación previa
/// sellada) o desde otra cuenta del titular (con las firmas y la aprobación de la atribución de retiro si hace falta). (nuevo)
/// </summary>
public sealed record FondeoDeCdat(
    TermDepositFundingKind Kind,
    DateOnly FechaValor,
    PrevalidationOutcome Validacion,
    string? Motivo)
{
    public Recaudador? Recaudador { get; init; }

    public ConsignacionAAplicar? Consignacion { get; init; }

    public string? Huella { get; init; }

    /// <summary>La cuenta origen, ya leída dentro del alcance de quien opera.</summary>
    public MemberAccount? Origen { get; init; }

    public IReadOnlyList<Guid> Firmantes { get; init; } = [];

    public Guid? AprobacionPublicId { get; init; }
}

/// <summary>Lo que dejó el fondeo: la operación de constitución, la versión 1 sellada y el comprobante (el del contrato de cuentas). (nuevo)</summary>
public sealed record CdatFondeado(MemberAccountOperation Operacion, MemberAccountTermsVersion Version, ComprobanteDeCuentaDto Comprobante);

/// <summary>
/// La constitución de un CDAT al fondearse (feature 014, US6, T170; research R11, E135; FR-067, FR-069, FR-073): el CDAT en apertura con su propuesta, sin
/// solicitud de tasa en espera (<c>Lending.TermDeposit.PendingNegotiation</c>) ni coincidencias en listas sin liberar, recibe <b>exactamente</b> su monto
/// (<c>Lending.TermDeposit.FundingAmountMismatch</c>) por un recaudador o desde otra cuenta de ahorro (con las reglas del retiro de esa cuenta: firmas,
/// disponible, topes y atribución, y su GMF por <c>Gmf.HechosGeneradores</c>: el traslado entre ahorro y CDAT está gravado por defecto, D32). En una
/// transacción y bajo el cerrojo de las dos cuentas: la operación <c>TermDepositConstitution</c> (<c>CDC-</c>), la versión 1 de condiciones con el plan desde la
/// fecha del fondeo (<see cref="PlanDeCdat"/>) por <see cref="RegistroDeCuentas.SellarCondiciones"/>, el capital del CDAT, la salida y el GMF de la cuenta
/// origen, el hecho <c>CdatConstituido</c> con su contraparte (+ <c>GmfRetenido</c>) y la cuenta activa sin propuesta. Guarda. (nuevo)
/// </summary>
public sealed class ConstitucionDeCdat(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    LectorDeCdat lector,
    ICerrojoDeCartera cerrojo,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>El prefijo del comprobante de la constitución de un CDAT.</summary>
    public const string PrefijoConstitucion = "CDC-";

    public async Task<Result<CdatFondeado>> FondearAsync(MemberAccount cdat, FondeoDeCdat f, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cdat);
        ArgumentNullException.ThrowIfNull(f);
        if (cdat.DepositClass != DepositClass.TermDeposit) return Falla(new Error(ErroresDeCdat.CodigoNoEsCdat, $"La cuenta {cdat.Number} no es un CDAT."));
        if (cdat.Status != MemberAccountStatus.Opening) return Falla(ErroresDeCdat.YaFondeado(cdat.Number));
        var propuesta = PropuestaDeCdat.Leer(cdat.PendingTermsJson);
        if (propuesta is null) return Falla(ErroresDeCdat.FondeoInvalido($"El CDAT {cdat.Number} no tiene condiciones pendientes de sellar."));
        if (await db.NegotiatedRateRequests.AsNoTracking()
                .AnyAsync(r => r.MemberAccountId == cdat.Id && r.Status == NegotiatedRateRequestStatus.PendingDecision && !r.IsDeleted, ct))
            return Falla(ErroresDeCdat.NegociacionPendiente(cdat.Number));
        var personas = await db.MemberAccountParties.AsNoTracking()
            .Where(p => p.MemberAccountId == cdat.Id && p.ValidTo == null && !p.IsDeleted && (p.Role == MemberAccountPartyRole.Holder || p.Role == MemberAccountPartyRole.CoHolder))
            .Select(p => p.PersonId).ToListAsync(ct);
        if (!personas.Contains(cdat.HolderPersonId)) personas.Add(cdat.HolderPersonId);
        if (await LectorDeCuentas.CoincidenciasSinLiberarAsync(db, personas, ct) is { Count: > 0 } coincidencias)
            return Falla(ErroresDeCuentas.ListasBloquean(coincidencias));
        var monto = f.Kind == TermDepositFundingKind.Collector ? f.Consignacion?.Amount ?? 0m : propuesta.Monto;
        if (monto != propuesta.Monto) return Falla(ErroresDeCdat.MontoDistinto(propuesta.Monto, monto));
        if (f.FechaValor < cdat.OpenedOn) return Falla(ErroresDeCdat.FechaDeInicio(cdat.OpenedOn));

        var version = await db.DepositLineVersions.AsNoTracking().FirstAsync(v => v.Id == propuesta.VersionDeLineaId, ct);
        var condiciones = LectorDeCdat.Condiciones(propuesta, version, f.FechaValor);
        var calendario = await lector.CalendarioAsync(condiciones.Inicio, PlanDeCdat.Vencimiento(condiciones.Inicio, condiciones.PlazoDias), ct);
        var plan = PlanDeCdat.Generar(condiciones, calendario);

        // El origen: una cuenta de ahorro viva distinta del CDAT, revisada con las reglas del retiro (firmas, disponible, topes, atribución, GMF).
        RetiroRevisado? retiro = null;
        var origen = f.Origen;
        if (f.Kind == TermDepositFundingKind.TransferFromAccount)
        {
            if (origen is null || origen.Id == cdat.Id || origen.Nature != LineNature.Deposit || origen.DepositClass is not (DepositClass.Demand or DepositClass.Permanent))
                return Falla(ErroresDeCdat.FondeoInvalido("El CDAT se fondea desde una cuenta de ahorro viva del asociado."));
            var hecho = HechosGeneradoresDeGmf.DeTraslado(origen.DepositClass, DepositClass.TermDeposit, origen.HolderPersonId == cdat.HolderPersonId);
            var revisado = await retiros.RevisarAsync(origen, new RetiroPedido(null, null, null, origen.PublicId, monto, f.FechaValor,
                ClaseDeMedioDeRecaudo.Transfer, f.Firmantes, null, null, f.AprobacionPublicId, f.Validacion, [], f.Motivo)
            {
                Clase = MemberAccountOperationKind.Transfer, HechoGenerador = hecho, Prefijo = PrefijoConstitucion,
            }, pedirAprobacion: true, ct);
            if (revisado.IsFailure) return Falla(revisado.Error);
            retiro = revisado.Value;
        }
        else if (f.Recaudador is null || f.Consignacion is null)
        {
            return Falla(ErroresDeCdat.FondeoInvalido("El fondeo por recaudador trae su consignación."));
        }

        var ids = new List<int> { cdat.Id };
        if (origen is not null) ids.Add(origen.Id);
        await cerrojo.BloquearCuentasAsync(ids.Order().ToList(), ModoDeEspera.Esperar, ct);

        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cdat.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.TermDepositConstitution, PrefijoConstitucion, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var contable = await emision.HechoDeCuentaAsync(cdat, f.FechaValor, ct);
        if (contable.IsFailure) return Falla(contable.Error);
        var huella = new HuellaDeCartera().Identificador("account", cdat.PublicId).Decimal("amount", monto, 2).Fecha("start", f.FechaValor)
            .Entero("termDays", propuesta.PlazoDias).Decimal("rate", propuesta.TasaAplicada, 6).Texto("periodicity", propuesta.Periodicidad.ToString())
            .Texto("timing", propuesta.Modalidad.ToString()).Decimal("interest", plan.InteresTotal, 2).Calcular();
        var c = f.Consignacion;
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cdat.Id,
            CounterpartMemberAccountId = origen?.Id,
            Kind = MemberAccountOperationKind.TermDepositConstitution,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = f.FechaValor,
            AccountingDate = contable.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = monto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = f.Recaudador?.Class.ToString(),
            CollectorCode = f.Recaudador?.Code,
            IdempotencyKey = c?.IdempotencyKey,
            RequestSha256 = f.Huella,
            ContractVersion = f.Recaudador is null ? null : IContratoDeCuentas.VersionDelContrato,
            PaymentMeansClass = (c?.MeansClass ?? ClaseDeMedioDeRecaudo.Transfer).ToString(),
            ReceiptNumber = c?.Receipt.Number,
            ReceiptDate = c?.Receipt.Date,
            ReceiptBranchPublicId = c?.Receipt.OfficePublicId,
            ReceiptMeansReference = c?.Receipt.MeansReference,
            CashOriginDeclarationAttachmentPublicId = c?.SourceOfFundsAttachmentPublicId,
            AuthorizedByUserId = retiro?.Atribucion.AprobadoPor,
            SignedByPersonIdsJson = retiro?.Firmas?.Presentadas is { Count: > 0 } firmas ? JsonSerializer.Serialize(firmas.Order()) : null,
            Reason = string.IsNullOrWhiteSpace(f.Motivo) ? null : LectorDeCdat.Recortar(f.Motivo, 500),
            Payload = JsonSerializer.Serialize(new
            {
                funding = f.Kind.ToString(), source = origen?.PublicId, amount = monto, start = f.FechaValor, termDays = propuesta.PlazoDias,
                rate = propuesta.TasaAplicada, interest = plan.InteresTotal,
            }, Json),
            InputsSha256 = huella,
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = f.Validacion.ToString(),
        });
        await db.SaveChangesAsync(ct);

        // La versión 1 sellada con su plan (sólo inserción, por el registro).
        var destino = await DestinoAsync(propuesta.Destino, ct);
        var terminos = LectorDeCdat.NuevaVersion(cdat, 1, operacion, AccountTermsCause.Constitution, version, plan, propuesta.TasaDeTabla, propuesta.Negociacion,
            propuesta.Destino, destino.Cuenta, destino.Persona, destino.Banco, propuesta.Instruccion, 0, null, ahora, huella);
        registro.SellarCondiciones(control, terminos, LectorDeCdat.FilasDelPlan(terminos, plan), 0);
        await db.SaveChangesAsync(ct);

        var hechoCdat = await emision.HechoDeCuentaAsync(cdat, f.FechaValor, ct);
        if (hechoCdat.IsFailure) return Falla(hechoCdat.Error);
        Domain.Entities.Lending.Deposits.Projections.MemberAccountControl? controlOrigen = null;
        OtraCuentaDeCdat? otra = null;
        (GmfAEmitir, HechoDeCuenta)? gmfAEmitir = null;
        var gmf = retiro?.Gmf;
        if (origen is not null)
        {
            controlOrigen = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == origen.Id, ct);
            var hechoOrigen = await emision.HechoDeCuentaAsync(origen, f.FechaValor, ct);
            if (hechoOrigen.IsFailure) return Falla(hechoOrigen.Error);
            otra = new OtraCuentaDeCdat(hechoOrigen.Value, [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -monto }]);
            if (gmf is { Valor: > 0m })
                gmfAEmitir = (new GmfAEmitir(gmf, retiro!.Cuenta.ExentaDeGmf ? origen.PublicId : null, origen.DepositClass?.ToString()), hechoOrigen.Value);
        }
        var negociada = propuesta.Negociacion is { } n
            ? new NegotiatedRateRefV1
            {
                RulePublicId = n.ReglaPublicId,
                ApproverUserPublicId = n.AprobadaPor is { } u
                    ? await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == u).Select(x => x.PublicId).FirstOrDefaultAsync(ct)
                    : Guid.Empty,
            }
            : null;
        var unidad = await emision.CdatConstituidoAsync(new HechoDeCdatAEmitir(hechoCdat.Value, operacion,
            [new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = monto }], EmisionDeCartera.Terminos(terminos, cdat.Number),
            f.Validacion)
        {
            OtraCuenta = otra,
            Recaudador = f.Recaudador is { } r && c is not null ? EmisionDeCartera.Recaudador(r, c.IdempotencyKey, c.MeansClass, c.DepositorPublicId, c.Receipt) : null,
            Gmf = gmfAEmitir,
            Negociada = negociada,
            Tercero = c?.DepositorPublicId,
        }, ct);
        if (unidad.IsFailure) return Falla(unidad.Error);
        var mensaje = unidad.Value.De(CdatConstituidoV1.Type);
        var mensajeGmf = unidad.Value.De(GmfRetenidoV1.Type);
        var fecha = hechoCdat.Value.FechaContable;
        var filas = new List<MemberAccountTransaction>
        {
            Fila(cdat.Id, operacion, f.Kind == TermDepositFundingKind.Collector ? MemberAccountTransactionKind.Deposit : MemberAccountTransactionKind.TransferIn,
                MemberAccountConcept.Principal, monto, f.FechaValor, fecha, mensaje, terminos.Id),
        };
        if (origen is not null)
        {
            filas.Add(Fila(origen.Id, operacion, MemberAccountTransactionKind.TransferOut, MemberAccountConcept.Principal, -monto, f.FechaValor, fecha, mensaje, null));
            if (GravamenMovimientosFinancieros.SeCobra(gmf))
            {
                filas.Add(Fila(origen.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Principal, -gmf!.Valor, f.FechaValor, fecha, mensajeGmf, null));
                filas.Add(Fila(origen.Id, operacion, MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf, gmf.Valor, f.FechaValor, fecha, mensajeGmf, null));
            }
        }
        registro.RegistrarTransacciones(filas);
        if (origen is not null && gmf is not null)
            registro.RegistrarUsoDeGmf(origen.HolderPersonId, operacion, origen.Id, f.FechaValor, gmf.HechoGenerador, gmf.Base, gmf.ExentoAplicado, gmf.BaseGravada,
                gmf.Valor, gmf.Politica, gmf.Tarifa);
        if (controlOrigen is not null) RegistroDeCuentas.MarcarActividadDelTitular(controlOrigen, f.FechaValor);
        RegistroDeCuentas.MarcarActividadDelTitular(control, f.FechaValor);
        var seguida = db.MemberAccounts.Local.FirstOrDefault(a => a.Id == cdat.Id) ?? cdat;
        seguida.Activar(f.FechaValor, ahora);
        seguida.PendingTermsJson = null;
        if (!ReferenceEquals(seguida, cdat))
        {
            cdat.Status = seguida.Status;
            cdat.PendingTermsJson = null;
        }
        await db.SaveChangesAsync(ct);

        var comprobante = new ComprobanteDeCuentaDto(operacion.PublicId, operacion.Number, cdat.PublicId, cdat.Number, c?.IdempotencyKey ?? string.Empty,
            nameof(MemberAccountOperationKind.TermDepositConstitution), operacion.ValueDate, operacion.AccountingDate, operacion.RegisteredAt, monto,
            filas.Where(x => x.MemberAccountId == cdat.Id).Select(x => new LineaDeCuentaDto((ConceptoDeCuenta)(int)x.Concept, x.Kind.ToString(), x.Amount)).ToList(),
            ReglasDeMovimientos.Dto(gmf), [], [], new DisponibleDto(monto, 0m, 0m, 0m, 0m, 0m), null, null, false, huella, operacion.RulesVersion, []);
        return Result.Success(new CdatFondeado(operacion, terminos, comprobante));
    }

    /// <summary>El destino de la propuesta con sus Ids internos (la cuenta, la persona y el banco del giro).</summary>
    private async Task<(int? Cuenta, int? Persona, int? Banco)> DestinoAsync(DestinoDeCdat d, CancellationToken ct)
    {
        int? cuenta = d.AccountPublicId is { } a
            ? await db.MemberAccounts.AsNoTracking().Where(x => x.PublicId == a).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        int? persona = d.PayeePersonPublicId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        int? banco = d.BankPublicId is { } b
            ? await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
            : null;
        return (cuenta, persona, banco);
    }

    private static MemberAccountTransaction Fila(int cuenta, MemberAccountOperation op, MemberAccountTransactionKind clase, MemberAccountConcept concepto, decimal monto,
        DateOnly valor, DateOnly contable, Guid? mensaje, int? terminos) => new()
    {
        MemberAccountId = cuenta, Operation = op, OperationId = op.Id, Kind = clase, Concept = concepto, Amount = monto, ValueDate = valor, AccountingDate = contable,
        RegisteredAt = op.RegisteredAt, MessagePublicId = mensaje, TermsVersionId = terminos,
    };

    private static Result<CdatFondeado> Falla(Error e) => Result.Failure<CdatFondeado>(e);
}
