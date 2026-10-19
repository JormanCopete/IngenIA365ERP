using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>Un traslado pedido (feature 014, US4, T111; contrato-de-cuentas §3.10): por el contrato o por la pantalla. (nuevo)</summary>
public sealed record TrasladoPedido(
    Recaudador? Recaudador,
    string? Clave,
    string? HuellaDeLaSolicitud,
    Guid SourceAccountPublicId,
    Guid TargetAccountPublicId,
    decimal Monto,
    DateOnly FechaValor,
    IReadOnlyList<Guid> Firmantes,
    ModalidadDeConsignacion ModalidadDestino,
    ReferenciaDeRecibo? Recibo,
    Guid? AprobacionPublicId,
    PrevalidationOutcome Validacion,
    IReadOnlyList<string> Avisos,
    string? Motivo)
{
    /// <summary>US5 (T135): la entrega del saldo de una cancelación (ver <see cref="RetiroPedido.EsCancelacion"/>).</summary>
    public bool EsCancelacion { get; init; }

    /// <summary>US7 (T182): falso en la entrega que hace el proceso al vencer un contractual (la instrucción la dio el titular).</summary>
    public bool ExigeFirmas { get; init; } = true;
}

/// <summary>Lo que revisó un traslado: el retiro del origen, la cuenta destino, el reparto en el destino y el hecho generador del GMF. (nuevo)</summary>
public sealed record TrasladoRevisado(RetiroRevisado Origen, CuentaParaMovimiento Destino, RepartoDeConsignacion Reparto, string HechoGenerador);

/// <summary>
/// El traslado entre dos cuentas (feature 014, US4, T111; contrato-de-cuentas §3.10; FR-052, FR-053): una sola operación <c>Transfer</c> con las dos cuentas
/// bajo el cerrojo en orden de <c>Id</c>. Valida el retiro en el origen (pasos 1–5 de §3.9) y la consignación en el destino (§3.8: operable, sólo asociados,
/// el reparto en cuotas si el destino es aportes o permanente, el límite de participación). El GMF sigue <c>Gmf.HechosGeneradores</c>: exento entre cuentas
/// de ahorro del mismo titular, gravado entre ahorro y CDAT o hacia otro titular (D32). Emite <c>TrasladoEntreCuentasAplicado</c> con su
/// <c>GmfRetenido</c>. Guarda en la transacción de quien llama. (nuevo)
/// </summary>
public sealed class TrasladoDeCuenta(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ReglasDeMovimientos movimientos,
    RetiroDeCuenta retiros,
    ConsignacionEnCuentas consignacion,
    IActorActual actorActual,
    IDateTimeService reloj,
    Accrual.AjusteDeCausacionDeCuenta? ajusteDeCausacion = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string PrefijoTraslado = "TRC-";

    /// <summary>El nombre de la huella de un traslado del contrato.</summary>
    public const string Metodo = "Lending.AccountContract.Transfer";

    /// <summary>Revisa el traslado sin escribir (también para simular).</summary>
    public async Task<Result<TrasladoRevisado>> RevisarAsync(MemberAccount origen, MemberAccount destino, TrasladoPedido p, bool pedirAprobacion, CancellationToken ct)
    {
        if (origen.Id == destino.Id) return Result.Failure<TrasladoRevisado>(ErroresDeMovimientos.MismaCuenta());
        if (destino.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive))
            return Result.Failure<TrasladoRevisado>(ErroresDeMovimientos.DestinoNoOperable(destino.Number, destino.Status));
        if (destino.DepositClass is DepositClass.TermDeposit)
            return Result.Failure<TrasladoRevisado>(ErroresDeAportes.EntregaNoDisponible("El traslado a un CDAT", nameof(EntregaDeCartera.K9)));
        var leidaDestino = await movimientos.LeerAsync(destino, p.FechaValor, ct);
        if (leidaDestino.Titular.IsDeceased) return Result.Failure<TrasladoRevisado>(ErroresDeMovimientos.DestinoNoOperable(destino.Number, destino.Status));
        if (await movimientos.SoloAsociadosAsync(leidaDestino, p.FechaValor, ct) is { } soloAsociados) return Result.Failure<TrasladoRevisado>(soloAsociados);

        // El GMF del traslado sigue su propio hecho generador (D32), con las demás reglas del retiro en el origen.
        var hecho = HechosGeneradoresDeGmf.DeTraslado(origen.DepositClass, destino.DepositClass, origen.HolderPersonId == destino.HolderPersonId);
        var retiro = await retiros.RevisarAsync(origen, Retiro(p) with { HechoGenerador = hecho }, pedirAprobacion, ct);
        if (retiro.IsFailure) return Result.Failure<TrasladoRevisado>(retiro.Error);

        // US7 (T182): el contractual recibe por cuotas, como el permanente y los aportes.
        var modalidad = destino.Nature == LineNature.Contribution || destino.DepositClass is DepositClass.Permanent or DepositClass.Contractual
            ? p.ModalidadDestino
            : ModalidadDeConsignacion.Free;
        var reparto = await consignacion.RepartirAsync(destino, modalidad, p.Monto, null, p.FechaValor, ct);
        if (reparto.IsFailure) return Result.Failure<TrasladoRevisado>(reparto.Error);
        return Result.Success(new TrasladoRevisado(retiro.Value, leidaDestino, reparto.Value, hecho));
    }

    /// <summary>El retiro del origen que el traslado revisa con las reglas del retiro (firmas, disponible, topes, atribución).</summary>
    private static RetiroPedido Retiro(TrasladoPedido p) => new(p.Recaudador, p.Clave, p.HuellaDeLaSolicitud, p.SourceAccountPublicId, p.Monto, p.FechaValor,
        ClaseDeMedioDeRecaudo.Transfer, p.Firmantes, null, p.Recibo, p.AprobacionPublicId, p.Validacion, p.Avisos, p.Motivo)
    {
        Clase = MemberAccountOperationKind.Transfer,
        EsCancelacion = p.EsCancelacion,
        ExigeAtribucion = !p.EsCancelacion,
        ExigeFirmas = p.ExigeFirmas,
    };

    /// <summary>Aplica el traslado (ver la clase). Las dos cuentas ya están bloqueadas.</summary>
    public async Task<Result<(ComprobanteDeCuentaDto Comprobante, MemberAccountOperation Operacion, bool YaAplicado)>> AplicarAsync(TrasladoPedido p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.Recaudador is { } recaudador && p.Clave is { } clave)
        {
            var previa = await db.MemberAccountOperations.AsNoTracking().FirstOrDefaultAsync(o => o.CollectorCode == recaudador.Code && o.IdempotencyKey == clave, ct);
            if (previa is not null)
                return string.Equals(previa.RequestSha256, p.HuellaDeLaSolicitud, StringComparison.Ordinal)
                    ? Result.Success((await ComprobantesDeCuenta.DeAsync(db, movimientos, previa, reloj.HoyLocal, ct), previa, true))
                    : Result.Failure<(ComprobanteDeCuentaDto, MemberAccountOperation, bool)>(ErroresDePagos.ClaveReusada(clave));
        }
        var origen = await db.MemberAccounts.FirstOrDefaultAsync(a => a.PublicId == p.SourceAccountPublicId && !a.IsDeleted, ct);
        var destino = await db.MemberAccounts.FirstOrDefaultAsync(a => a.PublicId == p.TargetAccountPublicId && !a.IsDeleted, ct);
        if (origen is null || destino is null)
            return Result.Failure<(ComprobanteDeCuentaDto, MemberAccountOperation, bool)>(new Error(ErroresDePagos.CodigoNoEncontrado, "Una de las cuentas no existe."));
        var revisado = await RevisarAsync(origen, destino, p, pedirAprobacion: true, ct);
        if (revisado.IsFailure) return Result.Failure<(ComprobanteDeCuentaDto, MemberAccountOperation, bool)>(revisado.Error);
        var rv = revisado.Value;
        var gmf = rv.Origen.Gmf;

        var hechoOrigen = await emision.HechoDeCuentaAsync(origen, p.FechaValor, ct);
        if (hechoOrigen.IsFailure) return Result.Failure<(ComprobanteDeCuentaDto, MemberAccountOperation, bool)>(hechoOrigen.Error);
        var hechoDestino = await emision.HechoDeCuentaAsync(destino, p.FechaValor, ct);
        if (hechoDestino.IsFailure) return Result.Failure<(ComprobanteDeCuentaDto, MemberAccountOperation, bool)>(hechoDestino.Error);
        var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == origen.Id, ct);
        var controlDestino = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == destino.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Transfer, PrefijoTraslado, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var firmantes = rv.Origen.Firmas?.Presentadas ?? [];
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = origen.Id,
            CounterpartMemberAccountId = destino.Id,
            Kind = MemberAccountOperationKind.Transfer,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = p.FechaValor,
            AccountingDate = hechoOrigen.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = p.Monto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = p.Recaudador?.Class.ToString(),
            CollectorCode = p.Recaudador?.Code,
            IdempotencyKey = p.Clave,
            RequestSha256 = p.HuellaDeLaSolicitud,
            ContractVersion = p.Recaudador is null ? null : IContratoDeCuentas.VersionDelContrato,
            PaymentMeansClass = nameof(ClaseDeMedioDeRecaudo.Transfer),
            ReceiptNumber = p.Recibo?.Number,
            ReceiptDate = p.Recibo?.Date,
            AuthorizedByUserId = rv.Origen.Atribucion.AprobadoPor,
            SignedByPersonIdsJson = firmantes.Count == 0 ? null : JsonSerializer.Serialize(firmantes.Order()),
            Reason = string.IsNullOrWhiteSpace(p.Motivo) ? null : p.Motivo.Trim().Length <= 500 ? p.Motivo.Trim() : p.Motivo.Trim()[..500],
            Payload = JsonSerializer.Serialize(new
            {
                source = origen.PublicId, target = destino.PublicId, amount = p.Monto, valueDate = p.FechaValor, signers = p.Firmantes,
                targetModality = p.ModalidadDestino.ToString(), gmfEvent = rv.HechoGenerador, gmf = gmf?.Valor,
            }, Json),
            InputsSha256 = new HuellaDeCartera().Identificador("source", origen.PublicId).Identificador("target", destino.PublicId).Decimal("amount", p.Monto, 2)
                .Fecha("valueDate", p.FechaValor).Decimal("gmf", gmf?.Valor, 2).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = p.Validacion.ToString(),
        });
        await db.SaveChangesAsync(ct);

        var valoresOrigen = new List<DepositConceptAmountV1>
        {
            new() { Concept = nameof(MemberAccountConcept.Principal), Amount = -p.Monto, ReasonCode = rv.HechoGenerador },
        };
        var valoresDestino = rv.Reparto.Porciones.GroupBy(x => x.Concepto).Select(g => new DepositConceptAmountV1
        {
            Concept = g.Key.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(g.Key)?.ToString(), Amount = g.Sum(x => x.Monto),
        }).OrderBy(v => v.Concept, StringComparer.Ordinal).ToList();
        var exenta = rv.Origen.Cuenta.ExentaDeGmf ? origen.PublicId : (Guid?)null;
        var unidad = await emision.TrasladoConGmfAsync(new TrasladoAEmitir(hechoOrigen.Value, hechoDestino.Value, operacion, valoresOrigen, valoresDestino),
            // US8 (T143; contabilidad.md §2 y §9): el GMF va a la cuenta por pagar de la clase del producto que genera el hecho —la cuenta que se debita—, no
            // la del destino (hasta el 2026-10-18 tomaba la del destino y, con aportes, «Contribution», que no es una clase de captación).
            gmf is null ? null : new GmfAEmitir(gmf, exenta, origen.DepositClass?.ToString()), p.Validacion, ct);
        if (unidad.IsFailure) return Result.Failure<(ComprobanteDeCuentaDto, MemberAccountOperation, bool)>(unidad.Error);
        var mensaje = unidad.Value.De(TrasladoEntreCuentasAplicadoV1.Type);

        var filas = new List<MemberAccountTransaction>
        {
            new()
            {
                MemberAccountId = origen.Id, Operation = operacion, OperationId = operacion.Id, Kind = MemberAccountTransactionKind.TransferOut,
                Concept = MemberAccountConcept.Principal, Amount = -p.Monto, ValueDate = p.FechaValor, AccountingDate = hechoOrigen.Value.FechaContable,
                RegisteredAt = operacion.RegisteredAt, MessagePublicId = mensaje,
            },
        };
        filas.AddRange(rv.Reparto.Porciones.Select(x => new MemberAccountTransaction
        {
            MemberAccountId = destino.Id, Operation = operacion, OperationId = operacion.Id, Kind = MemberAccountTransactionKind.TransferIn, Concept = x.Concepto,
            InstallmentId = x.Cuota?.Cuota.Id, Amount = x.Monto, ValueDate = p.FechaValor, AccountingDate = hechoOrigen.Value.FechaContable,
            RegisteredAt = operacion.RegisteredAt, MessagePublicId = mensaje,
        }));
        if (GravamenMovimientosFinancieros.SeCobra(gmf))
        {
            // El GMF lo calculó el motor; aquí sólo se escribe en pareja.
            var mensajeGmf = unidad.Value.De(GmfRetenidoV1.Type);
            filas.Add(new MemberAccountTransaction
            {
                MemberAccountId = origen.Id, Operation = operacion, OperationId = operacion.Id, Kind = MemberAccountTransactionKind.GmfCharged,
                Concept = MemberAccountConcept.Principal, Amount = -gmf.Valor, ValueDate = p.FechaValor, AccountingDate = hechoOrigen.Value.FechaContable,
                RegisteredAt = operacion.RegisteredAt, MessagePublicId = mensajeGmf,
            });
            filas.Add(new MemberAccountTransaction
            {
                MemberAccountId = origen.Id, Operation = operacion, OperationId = operacion.Id, Kind = MemberAccountTransactionKind.GmfCharged,
                Concept = MemberAccountConcept.Gmf, Amount = gmf.Valor, ValueDate = p.FechaValor, AccountingDate = hechoOrigen.Value.FechaContable,
                RegisteredAt = operacion.RegisteredAt, MessagePublicId = mensajeGmf,
            });
        }
        registro.RegistrarTransacciones(filas);
        if (gmf is not null)
            registro.RegistrarUsoDeGmf(origen.HolderPersonId, operacion, origen.Id, p.FechaValor, gmf.HechoGenerador, gmf.Base, gmf.ExentoAplicado, gmf.BaseGravada,
                gmf.Valor, gmf.Politica, gmf.Tarifa);
        RegistroDeCuentas.MarcarActividadDelTitular(control, p.FechaValor);
        RegistroDeCuentas.MarcarActividadDelTitular(controlDestino, p.FechaValor);
        if (origen.Status == MemberAccountStatus.Inactive) origen.Reactivar(ahora);
        if (destino.Status == MemberAccountStatus.Inactive) destino.Reactivar(ahora);
        await db.SaveChangesAsync(ct);
        // US5 (T133; FR-062): el traslado fechado atrás ajusta la causación de las dos cuentas, enlazado a esta operación.
        if (ajusteDeCausacion is not null)
            foreach (var afectada in new[] { origen, destino })
                if (await ajusteDeCausacion.AjustarAsync(afectada, operacion, EmisionDeCartera.ClaseTrasladoDeCuenta, ct) is { IsFailure: true } ajusteFallido)
                    return Result.Failure<(ComprobanteDeCuentaDto Comprobante, MemberAccountOperation Operacion, bool YaAplicado)>(ajusteFallido.Error);

        var final = await movimientos.LeerAsync(origen, p.FechaValor, ct);
        var comprobante = new ComprobanteDeCuentaDto(operacion.PublicId, operacion.Number, origen.PublicId, origen.Number, p.Clave ?? string.Empty,
            nameof(MemberAccountOperationKind.Transfer), operacion.ValueDate, operacion.AccountingDate, operacion.RegisteredAt, p.Monto,
            filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(), ReglasDeMovimientos.Dto(gmf), [],
            rv.Reparto.PorCuota, ReglasDeMovimientos.Dto(final.Disponible), null, destino.PublicId, false, operacion.InputsSha256, operacion.RulesVersion, p.Avisos);
        return Result.Success((comprobante, operacion, false));
    }
}
