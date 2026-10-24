using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.BankItems;

/// <summary>
/// Una parte de una línea no interactiva: un crédito, una cuenta o un concepto vario de un titular, con su valor. La modalidad sale de la regla del canal
/// (<c>RCD_ChannelModalityRules</c>, FR-198) si no se da; <see cref="Key"/> fija la clave de la porción (la de la línea de giro de una pagaduría,
/// <c>REC:{giro:N}:{línea:N}</c>); sin ella, la de la porción de siempre. (nuevo)
/// </summary>
public sealed record ParteNoInteractiva(
    ApplicationTarget Target,
    Guid? ProductPublicId,
    Guid BeneficiaryPersonPublicId,
    decimal Amount,
    Guid? MiscConceptPublicId = null,
    string? CrossDocument = null,
    ModalidadDePago? LoanModality = null,
    ModalidadDeConsignacion? AccountModality = null,
    IReadOnlyList<Guid>? InstallmentPublicIds = null,
    Guid? DeductionFileLinePublicId = null,
    string? Key = null);

/// <summary>
/// Una línea de un proceso no interactivo (feature 015, T0194; R-17; FR-171): un abono del extracto identificado, una partida identificada después, una línea de giro
/// de una pagaduría. La línea es la unidad: su recibo, sus aplicaciones por los contratos en forma <c>NonInteractive</c> y su hecho, con la clave
/// <see cref="Key"/> (el recibo toma un <c>OperationKey</c> derivado de ella: repetir la línea no duplica nada). (nuevo)
/// </summary>
public sealed record LineaNoInteractiva(
    string Key,
    ReceiptChannel Channel,
    Guid PayerPersonPublicId,
    DateOnly ValueDate,
    decimal Amount,
    int BranchId,
    IReadOnlyList<ParteNoInteractiva> Parts,
    ClaseDeMedioDeRecaudo ContractMeansClass,
    PaymentMeansClass MeansClass = PaymentMeansClass.Transfer,
    Guid? OwnBankAccountPublicId = null,
    string? MeansReference = null,
    Guid? BankStatementLinePublicId = null,
    int? PaymasterRemittanceId = null,
    Guid? UnidentifiedItemPublicId = null,
    bool AllowLateValueDate = true,
    bool PaymasterReference = false,
    int? ContingencyBookId = null,
    int? ContingencyNumber = null,
    Guid? ContingencyImageAttachmentPublicId = null,
    int? CollectionPointId = null,
    Guid? SourceOfFundsAttachmentPublicId = null,
    int? DeliveredByPersonId = null,
    IReadOnlyList<OrigenABloquear>? Origenes = null,
    bool CerrojosTomados = false);

/// <summary>
/// Una fila de origen que la línea bloquea (nivel 6: la partida, la línea del giro de una pagaduría, el cheque) después de los pagadores. Una tanda de varias líneas toma
/// todos sus cerrojos antes de la primera (<see cref="LineaNoInteractiva.CerrojosTomados"/>): el orden fijo no admite volver a los pagadores después de numerar. (nuevo)
/// </summary>
public sealed record OrigenABloquear(string Tabla, long Id);

/// <summary>Una parte que el contrato rechazó después de simular: su índice, el código y el mensaje. (nuevo)</summary>
public sealed record ParteRechazada(int Index, string Code, string Message);

/// <summary>
/// Lo que pasó con la línea: aplicada (con su recibo), ya aplicada (repetida con su clave) o rechazada (sin recibo, con el código del contrato); el excedente y lo
/// rechazado después de simular que quedaron como saldo a favor del pagador; y si se aplicó con una fecha valor posterior a la del movimiento (FR-071, R0). (nuevo)
/// </summary>
public sealed record ResultadoNoInteractivo(
    ProcessingLineOutcome Outcome,
    Receipt? Receipt,
    string? Code,
    string? Message,
    decimal Surplus,
    bool LateValueDate,
    DateOnly? AppliedValueDate,
    IReadOnlyList<ParteRechazada> RejectedParts,
    PayerCreditBalance? CreditBalance);

/// <summary>
/// La aplicación no interactiva de Recaudo (feature 015, T0194; R-17, R-32; FR-068, FR-071, FR-078, FR-171, FR-174, FR-198; DT §2.4): la usan la identificación de
/// abonos, las partidas, el giro de una pagaduría y la contingencia. Por línea:
/// <list type="number">
/// <item>repetida con su clave (el <c>OperationKey</c> del recibo sale de la clave): <c>AlreadyApplied</c> con el recibo original;</item>
/// <item>listas (<see cref="IListasDeRecaudo"/>): una coincidencia vinculante rechaza la línea (<c>Receipts.Compliance.BindingListMatch</c>);</item>
/// <item>cerrojos: configuración y pagadores (el origen —la partida, la línea del giro— lo bloquea quien llama antes);</item>
/// <item>la modalidad del canal y, en créditos, la simulación: lo que excede la modalidad queda como saldo a favor del pagador (FR-198); una simulación rechazada
/// rechaza la línea sin escribir ni numerar;</item>
/// <item>numeración (antes de los contratos, como el recibo de caja; si el contrato rechaza todo, el número vuelve: <see cref="NumeradorDeRecaudo.Devolver"/>);</item>
/// <item>contratos <c>NonInteractive</c>, obligaciones antes que cuentas, con la fecha valor de la línea; si el contrato no la admite (fecha fuera de su rango o antes
/// del cierre), se repite con la más antigua que admite, el recibo guarda la fecha del movimiento (<c>OriginalValueDate</c>) y se alerta
/// <c>Recaudo.FechaValorTardia</c> al módulo (FR-071 en R0);</item>
/// <item>las filas del recibo y el hecho del canal.</item>
/// </list>
/// No abre transacción: corre en la de quien llama (la de la tanda, <c>IdempotencyBehavior</c>); un rechazo del contrato es una respuesta por ítem y no la revierte,
/// así las demás líneas de la tanda siguen (FR-171). Ningún proceso la llama: siempre la confirma una persona (FR-174). (nuevo)
/// </summary>
public sealed class AplicacionNoInteractiva(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IListasDeRecaudo listas,
    IContabilidadParaRecaudo contabilidad,
    EmisionDeRecaudo emision,
    IAlertas alertas)
{
    /// <summary>Los códigos con que los contratos rechazan una fecha valor que no admiten (Cartera y Captaciones).</summary>
    public static readonly IReadOnlySet<string> CodigosDeFechaValor = new HashSet<string>(StringComparer.Ordinal)
    {
        "Lending.Payment.ValueDateOutOfRange", "Lending.Payment.ValueDateBeforeClosing", "Lending.Savings.ValueDateOutOfRange",
    };

    /// <summary>El <c>OperationKey</c> del recibo de una línea: un Guid estable derivado de su clave (repetir la clave encuentra el mismo recibo).</summary>
    public static Guid ClaveDelRecibo(string clave)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes("REC.NoInteractiva|" + clave));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    /// <summary>La clase de documento del recibo por canal.</summary>
    public static ReceiptDocumentKind ClaseDelCanal(ReceiptChannel canal) => canal switch
    {
        ReceiptChannel.Bank => ReceiptDocumentKind.BankReceipt,
        ReceiptChannel.Paymaster => ReceiptDocumentKind.PaymasterReceipt,
        ReceiptChannel.Contingency => ReceiptDocumentKind.ContingencyReceipt,
        ReceiptChannel.Agreement => ReceiptDocumentKind.AgreementReceipt,
        ReceiptChannel.Online => ReceiptDocumentKind.OnlineReceipt,
        _ => ReceiptDocumentKind.CashReceipt,
    };

    public async Task<ResultadoNoInteractivo> AplicarAsync(LineaNoInteractiva linea, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(linea);
        if (linea.Parts.Count == 0 || linea.Parts.Any(p => p.Amount <= 0m))
            throw new ArgumentException("La línea no interactiva lleva al menos una parte con valor positivo.", nameof(linea));
        var clave = ClaveDelRecibo(linea.Key);
        if (await db.IssuedReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.OperationKey == clave, ct) is { } previo)
            return new ResultadoNoInteractivo(ProcessingLineOutcome.AlreadyApplied, previo, null, null, 0m, previo.OriginalValueDate is not null, previo.ValueDate, [], null);

        var actor = await actorActual.ObtenerAsync(ct);
        var hoy = reloj.HoyLocal;
        var titulares = linea.Parts.Select(p => p.BeneficiaryPersonPublicId).Append(linea.PayerPersonPublicId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => titulares.Contains(p.PublicId)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        if (!personas.TryGetValue(linea.PayerPersonPublicId, out var pagador)) return Rechazo(ErroresDeRecaudo.CodigoNoEncontrado, "El pagador no existe en el maestro de personas.");
        if (linea.Parts.Any(p => !personas.ContainsKey(p.BeneficiaryPersonPublicId))) return Rechazo(ErroresDeRecaudo.CodigoNoEncontrado, "Un titular no existe en el maestro de personas.");

        // Listas: una coincidencia vinculante no recibe el dinero (FR-203).
        var ids = personas.Values.Append(linea.DeliveredByPersonId ?? 0).Where(x => x != 0).Distinct().OrderBy(x => x).ToList();
        if (await listas.EvaluarAsync(ids, clave, ct) == ScreeningOutcomeR.BindingMatch)
            return Rechazo(ErroresDeRecaudo.ListaVinculante().Code, ErroresDeRecaudo.ListaVinculante().Message);

        // Validación previa contable: en procesos, la línea queda para reproceso con el aviso (FR-221).
        var evaluacion = await contabilidad.EvaluarAsync([new CombinacionDeHecho(ClavesDeRecaudo.Codigo, EmisionDeRecaudo.TipoDelHechoDelCanal(linea.Channel),
            new Dictionary<string, string>(), linea.ValueDate)], ct);
        if (!evaluacion.Postable && evaluacion.Policy == "Bloquear")
            return Rechazo("Receipts.Prevalidation.NotPostable", "La matriz contable de Recaudo no tiene regla para este recibo: complétela y reprocese la línea.");

        if (!linea.CerrojosTomados)
        {
            await cerrojo.TomarSetupAsync(ct: ct);
            await cerrojo.BloquearPagadoresAsync(ids, ct);
            foreach (var grupo in (linea.Origenes ?? []).GroupBy(o => o.Tabla))
                await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, grupo.Key, grupo.Select(o => o.Id).Distinct().Order().ToList(), ct: ct);
        }

        // Modalidad del canal y simulación de los créditos (el excedente de la modalidad queda a favor del pagador).
        var partes = new List<(ParteNoInteractiva Parte, decimal Valor, ModalidadDePago? Credito, ModalidadDeConsignacion? Cuenta)>();
        decimal excedente = 0m;
        foreach (var p in linea.Parts)
        {
            switch (p.Target)
            {
                case ApplicationTarget.Loan:
                {
                    var modalidad = p.LoanModality ?? await ModalidadDeCreditoAsync(linea.Channel, linea.ValueDate, ct);
                    var sim = await obligaciones.SimularAsync(new SolicitudDeSimulacion(ClavesDeRecaudo.Recaudador, p.ProductPublicId!.Value, modalidad, p.Amount,
                        linea.ValueDate), ct);
                    if (sim.Result == ResultadoDelContrato.Rejected || sim.Value is null)
                    {
                        if (!CodigosDeFechaValor.Contains(sim.Code)) return Rechazo(sim.Code, sim.Message);
                        partes.Add((p, p.Amount, modalidad, null));
                        break;
                    }
                    var sobra = Math.Max(0m, sim.Value.Surplus);
                    if (sobra >= p.Amount) return Rechazo("Receipts.NonInteractive.NothingToApply", "La modalidad del canal no aplica nada a la obligación: queda todo como excedente.");
                    excedente += sobra;
                    partes.Add((p, p.Amount - sobra, modalidad, null));
                    break;
                }
                case ApplicationTarget.Account:
                {
                    var modalidad = p.AccountModality ?? await ModalidadDeCuentaAsync(linea.Channel, p.BeneficiaryPersonPublicId, p.ProductPublicId!.Value, linea.ValueDate, ct);
                    var sim = await cuentas.SimularAsync(new SolicitudDeSimulacionDeCuenta(ClavesDeRecaudo.Recaudador, "Deposit", p.ProductPublicId!.Value, p.Amount,
                        linea.ValueDate, linea.ContractMeansClass, [], null, modalidad), ct);
                    if ((sim.Result == ResultadoDelContrato.Rejected || sim.Value is null || !sim.Value.Allowed) && !CodigosDeFechaValor.Contains(sim.Code))
                        return Rechazo(string.IsNullOrEmpty(sim.Code) ? "Receipts.NonInteractive.AccountRejected" : sim.Code, sim.Message);
                    partes.Add((p, p.Amount, null, modalidad));
                    break;
                }
                default:
                    partes.Add((p, p.Amount, null, null));
                    break;
            }
        }

        // Numeración, antes de los contratos (el contrato guarda el número del recibo).
        var (tipo, prefijo) = await TipoAsync(ClaseDelCanal(linea.Channel), linea.CollectionPointId, ct);
        var numero = await numerador.AsignarAsync(tipo.Code, prefijo, ct);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == linea.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var recibo = new Receipt
        {
            Number = numero, DocumentTypeId = tipo.Id, Channel = linea.Channel, BranchId = linea.BranchId, CollectionPointId = linea.CollectionPointId,
            OwnBankAccountPublicId = linea.OwnBankAccountPublicId, PayerPersonId = pagador, DeliveredByPersonId = linea.DeliveredByPersonId, ValueDate = linea.ValueDate,
            ProposedAccountingDate = linea.ValueDate, RegisteredAt = reloj.UtcNow, TotalMeans = linea.Amount, OperationKey = clave,
            BankStatementLinePublicId = linea.BankStatementLinePublicId, PaymasterRemittanceId = linea.PaymasterRemittanceId,
            IsContingency = linea.Channel == ReceiptChannel.Contingency, ContingencyBookId = linea.ContingencyBookId, ContingencyNumber = linea.ContingencyNumber,
            ContingencyImageAttachmentPublicId = linea.ContingencyImageAttachmentPublicId, CashierUserId = actor.UserId,
            PayerIsThirdParty = linea.Parts.Any(p => personas[p.BeneficiaryPersonPublicId] != pagador),
            VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
        };
        var medioId = await MedioAsync(linea.MeansClass, ct);
        if (medioId is null)
        {
            numerador.Devolver(tipo.Code, prefijo, numero);
            return Rechazo("Receipts.BankItem.NoTransferMeans", ErroresDeRecaudo.SinMedioDeTransferencia().Message);
        }
        var medio = new ReceiptMeans { Ordinal = 1, PaymentMeansId = medioId.Value, MeansClass = linea.MeansClass, Amount = linea.Amount, Reference = linea.MeansReference };
        var conceptos = await ConceptosAsync(linea.Parts, ct);
        var aplicaciones = new List<(ReceiptApplication Aplicacion, ReceiptApplicationPortion Porcion, int Indice, decimal Valor, ModalidadDePago? Credito, ModalidadDeConsignacion? Cuenta)>();
        for (var i = 0; i < partes.Count; i++)
        {
            var (p, valor, credito, cuenta) = partes[i];
            var a = new ReceiptApplication
            {
                Ordinal = i + 1, Target = p.Target, ProductPublicId = p.ProductPublicId, BeneficiaryPersonId = personas[p.BeneficiaryPersonPublicId],
                LoanModality = (int?)credito, AccountModality = (int?)cuenta, MiscConceptId = p.MiscConceptPublicId is { } mc ? conceptos.GetValueOrDefault(mc) : null,
                CrossDocumentReference = p.CrossDocument, InstallmentPublicIdsJson = p.InstallmentPublicIds is { Count: > 0 } c ? OperacionesDeCajaJson(c) : null, Amount = valor,
            };
            var porcion = new ReceiptApplicationPortion
            {
                MeansClass = linea.MeansClass, Amount = valor,
                IdempotencyKey = p.Key ?? ClavesDeRecaudo.Porcion(recibo.PublicId, a.PublicId, linea.MeansClass),
            };
            aplicaciones.Add((a, porcion, i, valor, credito, cuenta));
        }

        // Contratos: obligaciones antes que cuentas, con la fecha valor de la línea (o la más antigua que admitan).
        ReferenciaDeRecibo Referencia(DateOnly fecha) => new(numero, fecha, oficina, linea.MeansReference, ClaseDelCanal(linea.Channel).ToString());
        var rechazadas = new List<ParteRechazada>();
        var aplicadas = new HashSet<int>();
        var fechaAplicada = linea.ValueDate;
        var tardia = false;
        var deCreditos = aplicaciones.Where(x => x.Aplicacion.Target == ApplicationTarget.Loan).ToList();
        if (deCreditos.Count > 0)
        {
            var (respuesta, fecha, esTardia) = await AplicarCreditosAsync(deCreditos, linea, pagador, Referencia, fechaAplicada, ct);
            fechaAplicada = fecha;
            tardia |= esTardia;
            for (var k = 0; k < deCreditos.Count; k++)
            {
                var r = k < respuesta.Payments.Count ? respuesta.Payments[k] : null;
                if (r is null || r.Result == ResultadoDelContrato.Rejected || r.Value is null)
                {
                    rechazadas.Add(new ParteRechazada(deCreditos[k].Indice, r?.Code ?? "Receipts.Receipt.ContractUnavailable", r?.Message ?? "El módulo no respondió."));
                    continue;
                }
                var x = deCreditos[k];
                x.Porcion.ModuleOperationPublicId = r.Value.OperationPublicId;
                x.Porcion.ModuleOperationNumber = r.Value.Number;
                x.Aplicacion.ProductNumber ??= r.Value.ObligationNumber;
                x.Aplicacion.ModuleVoucherPublicId ??= r.Value.OperationPublicId;
                x.Aplicacion.ModuleVoucherNumber ??= r.Value.Number;
                x.Aplicacion.FinalityUntil = r.Value.FinalityUntil;
                aplicadas.Add(x.Indice);
            }
        }
        var deCuentas = aplicaciones.Where(x => x.Aplicacion.Target == ApplicationTarget.Account).ToList();
        if (deCuentas.Count > 0)
        {
            var (respuesta, fecha, esTardia) = await ConsignarAsync(deCuentas, linea, pagador, Referencia, fechaAplicada, ct);
            if (aplicadas.Count == 0) fechaAplicada = fecha;
            tardia |= esTardia;
            for (var k = 0; k < deCuentas.Count; k++)
            {
                var r = k < respuesta.Deposits.Count ? respuesta.Deposits[k] : null;
                if (r is null || r.Result == ResultadoDelContrato.Rejected || r.Value is null)
                {
                    rechazadas.Add(new ParteRechazada(deCuentas[k].Indice, r?.Code ?? "Receipts.Receipt.ContractUnavailable", r?.Message ?? "El módulo no respondió."));
                    continue;
                }
                var x = deCuentas[k];
                x.Porcion.ModuleOperationPublicId = r.Value.OperationPublicId;
                x.Porcion.ModuleOperationNumber = r.Value.Number;
                x.Aplicacion.ProductNumber ??= r.Value.AccountNumber;
                x.Aplicacion.ModuleVoucherPublicId ??= r.Value.OperationPublicId;
                x.Aplicacion.ModuleVoucherNumber ??= r.Value.Number;
                aplicadas.Add(x.Indice);
            }
        }
        foreach (var x in aplicaciones.Where(x => x.Aplicacion.Target == ApplicationTarget.MiscConcept)) aplicadas.Add(x.Indice);
        if (aplicadas.Count == 0)
        {
            numerador.Devolver(tipo.Code, prefijo, numero);
            var primera = rechazadas.FirstOrDefault();
            return new ResultadoNoInteractivo(ProcessingLineOutcome.Rejected, null, primera?.Code, primera?.Message, 0m, tardia, null, rechazadas, null);
        }

        // Las filas del recibo: sólo lo aplicado; el excedente y lo rechazado después de simular quedan a favor del pagador (FR-171, FR-198).
        recibo.ValueDate = fechaAplicada;
        recibo.ProposedAccountingDate = fechaAplicada;
        recibo.OriginalValueDate = tardia ? linea.ValueDate : null;
        var vivas = aplicaciones.Where(x => aplicadas.Contains(x.Indice)).ToList();
        recibo.TotalApplied = vivas.Sum(x => x.Valor);
        var aFavor = linea.Amount - recibo.TotalApplied;
        recibo.InputsHash = HuellaDeRecibo.Calcular(new
        {
            linea.Key, linea.Channel, linea.PayerPersonPublicId, linea.ValueDate, linea.Amount, linea.BankStatementLinePublicId, linea.PaymasterRemittanceId,
            Parts = linea.Parts.Select(p => new { p.Target, p.ProductPublicId, p.BeneficiaryPersonPublicId, p.Amount, p.MiscConceptPublicId, p.DeductionFileLinePublicId }),
        });
        db.IssuedReceipts.Add(recibo);
        await db.SaveChangesAsync(ct);
        medio.ReceiptId = recibo.Id;
        db.ReceiptMeans.Add(medio);
        var ordinal = 0;
        foreach (var x in vivas)
        {
            x.Aplicacion.ReceiptId = recibo.Id;
            x.Aplicacion.Ordinal = ++ordinal;
            db.ReceiptApplications.Add(x.Aplicacion);
        }
        await db.SaveChangesAsync(ct);
        foreach (var x in vivas)
        {
            x.Porcion.ReceiptApplicationId = x.Aplicacion.Id;
            x.Porcion.ReceiptMeansId = medio.Id;
            db.ReceiptApplicationPortions.Add(x.Porcion);
        }
        PayerCreditBalance? saldo = null;
        if (aFavor > 0m)
        {
            saldo = SaldosAFavor.Reconocer(db, pagador, rechazadas.Count > 0 ? PayerCreditBalanceOrigin.RejectedLine : PayerCreditBalanceOrigin.NonInteractiveSurplus,
                recibo.PublicId, recibo.Number, linea.BranchId, aFavor, hoy, actor.UserId, reloj.UtcNow, linea.OwnBankAccountPublicId);
            if (linea.Channel == ReceiptChannel.Paymaster) saldo.Kind = PayerCreditBalanceKind.Paymaster;
        }
        await db.SaveChangesAsync(ct);
        if (saldo is not null)
        {
            SaldosAFavor.Reconocido(db, saldo, actor.UserId, reloj.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        var naturalezas = await NaturalezasAsync(vivas.Select(x => x.Aplicacion).ToList(), hoy, ct);
        await emision.ReciboNoInteractivoAsync(recibo, vivas.Select(x => x.Aplicacion).ToList(), medio, vivas.Select(x => x.Porcion).ToList(), naturalezas,
            linea.UnidentifiedItemPublicId, aFavor, saldo is not null && linea.Channel == ReceiptChannel.Paymaster, tipo.Code, ct);
        if (tardia) await AvisarFechaTardiaAsync(recibo, linea.ValueDate, ct);
        return new ResultadoNoInteractivo(ProcessingLineOutcome.Applied, recibo, null, null, excedente, tardia, fechaAplicada, rechazadas, saldo);
    }

    private static ResultadoNoInteractivo Rechazo(string codigo, string mensaje) =>
        new(ProcessingLineOutcome.Rejected, null, codigo, mensaje, 0m, false, null, [], null);

    private async Task<(RespuestaDeAplicacionDto Respuesta, DateOnly Fecha, bool Tardia)> AplicarCreditosAsync(
        IReadOnlyList<(ReceiptApplication Aplicacion, ReceiptApplicationPortion Porcion, int Indice, decimal Valor, ModalidadDePago? Credito, ModalidadDeConsignacion? Cuenta)> partes,
        LineaNoInteractiva linea, int pagador, Func<DateOnly, ReferenciaDeRecibo> referencia, DateOnly fecha, CancellationToken ct)
    {
        var pagadorPublicId = linea.PayerPersonPublicId;
        _ = pagador;
        async Task<RespuestaDeAplicacionDto> AplicarConAsync(DateOnly f) => await obligaciones.AplicarAsync(new SolicitudDeAplicacion(ClavesDeRecaudo.Recaudador,
            FormaDeAplicacion.NonInteractive, partes.Select(x => new PagoAAplicar(x.Porcion.IdempotencyKey, x.Aplicacion.ProductPublicId!.Value, x.Credito!.Value, x.Valor, f,
                linea.ContractMeansClass, pagadorPublicId, referencia(f), DeductionFileLinePublicId: linea.Parts[x.Indice].DeductionFileLinePublicId)).ToList(), []), ct);
        var respuesta = await AplicarConAsync(fecha);
        if (!linea.AllowLateValueDate || !TodasPorFecha(respuesta.Payments.Select(p => (p.Result, p.Code)))) return (respuesta, fecha, false);
        var minima = FechaMinima(respuesta.Payments.Select(p => p.Data));
        return (await AplicarConAsync(minima), minima, true);
    }

    private async Task<(RespuestaDeConsignacionDto Respuesta, DateOnly Fecha, bool Tardia)> ConsignarAsync(
        IReadOnlyList<(ReceiptApplication Aplicacion, ReceiptApplicationPortion Porcion, int Indice, decimal Valor, ModalidadDePago? Credito, ModalidadDeConsignacion? Cuenta)> partes,
        LineaNoInteractiva linea, int pagador, Func<DateOnly, ReferenciaDeRecibo> referencia, DateOnly fecha, CancellationToken ct)
    {
        _ = pagador;
        async Task<RespuestaDeConsignacionDto> ConsignarConAsync(DateOnly f) => await cuentas.ConsignarAsync(new SolicitudDeConsignacion(ClavesDeRecaudo.Recaudador,
            FormaDeAplicacion.NonInteractive, partes.Select(x => new ConsignacionAAplicar(x.Porcion.IdempotencyKey, x.Aplicacion.ProductPublicId!.Value,
                x.Cuenta ?? ModalidadDeConsignacion.Free, x.Valor, f, linea.ContractMeansClass, linea.PayerPersonPublicId, referencia(f), linea.Parts[x.Indice].InstallmentPublicIds,
                linea.SourceOfFundsAttachmentPublicId, linea.Parts[x.Indice].DeductionFileLinePublicId)).ToList(), []), ct);
        var respuesta = await ConsignarConAsync(fecha);
        if (!linea.AllowLateValueDate || !TodasPorFecha(respuesta.Deposits.Select(p => (p.Result, p.Code)))) return (respuesta, fecha, false);
        var minima = FechaMinima(respuesta.Deposits.Select(p => p.Data));
        return (await ConsignarConAsync(minima), minima, true);
    }

    private static bool TodasPorFecha(IEnumerable<(ResultadoDelContrato Result, string Code)> respuestas)
    {
        var lista = respuestas.ToList();
        return lista.Count > 0 && lista.All(r => r.Result == ResultadoDelContrato.Rejected && CodigosDeFechaValor.Contains(r.Code));
    }

    /// <summary>La fecha más antigua que admite el contrato: la que trae su respuesta (<c>minDate</c>), o hoy.</summary>
    private DateOnly FechaMinima(IEnumerable<IReadOnlyDictionary<string, object?>?> datos)
    {
        var fechas = new List<DateOnly>();
        foreach (var d in datos)
        {
            if (d?.GetValueOrDefault("minDate") is { } m)
            {
                if (m is DateOnly f) fechas.Add(f);
                else if (DateOnly.TryParse(m.ToString(), CultureInfo.InvariantCulture, out var g)) fechas.Add(g);
            }
        }
        return fechas.Count > 0 ? fechas.Max() : reloj.HoyLocal;
    }

    private async Task<(ReceiptDocumentType Tipo, string Prefijo)> TipoAsync(ReceiptDocumentKind clase, int? punto, CancellationToken ct)
    {
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, clase, punto, ct);
        return (tipo, string.IsNullOrEmpty(prefijo) && clase != ReceiptDocumentKind.CashReceipt ? tipo.Code : prefijo);
    }

    /// <summary>El medio del catálogo común de la clase (transferencia o consignación): el primero activo por orden.</summary>
    private async Task<int?> MedioAsync(PaymentMeansClass clase, CancellationToken ct) =>
        await db.PaymentMeans.AsNoTracking().Where(m => m.Class == clase && m.IsActive).OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct)
        ?? await db.PaymentMeans.AsNoTracking().Where(m => (m.Class == PaymentMeansClass.Transfer || m.Class == PaymentMeansClass.BankDeposit) && m.IsActive)
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => (int?)m.Id).FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyDictionary<Guid, int>> ConceptosAsync(IReadOnlyList<ParteNoInteractiva> partes, CancellationToken ct)
    {
        var pedidos = partes.Where(p => p.MiscConceptPublicId is not null).Select(p => p.MiscConceptPublicId!.Value).Distinct().ToList();
        if (pedidos.Count == 0) return new Dictionary<Guid, int>();
        return await db.MiscConcepts.AsNoTracking().Where(c => pedidos.Contains(c.PublicId)).ToDictionaryAsync(c => c.PublicId, c => c.Id, ct);
    }

    /// <summary>La modalidad de créditos del canal (FR-198): la regla vigente de menor prioridad; sin regla, «cuota del ciclo».</summary>
    public async Task<ModalidadDePago> ModalidadDeCreditoAsync(ReceiptChannel canal, DateOnly fecha, CancellationToken ct)
    {
        var reglas = await db.ChannelModalityRules.AsNoTracking().Where(r => r.Channel == canal && r.Target == ApplicationTarget.Loan).OrderBy(r => r.Priority).ToListAsync(ct);
        var regla = reglas.FirstOrDefault(r => r.VigenteEn(fecha) && r.LoanModality is not null);
        return regla?.LoanModality is { } m ? (ModalidadDePago)m : ModalidadDePago.CycleInstallment;
    }

    /// <summary>La modalidad de cuentas del canal (FR-198, D38): «cuotas» si la persona tiene cuotas exigibles de la cuenta, si no «libre».</summary>
    public async Task<ModalidadDeConsignacion> ModalidadDeCuentaAsync(ReceiptChannel canal, Guid persona, Guid cuenta, DateOnly fecha, CancellationToken ct)
    {
        var reglas = (await db.ChannelModalityRules.AsNoTracking().Where(r => r.Channel == canal && r.Target == ApplicationTarget.Account).OrderBy(r => r.Priority)
            .ToListAsync(ct)).Where(r => r.VigenteEn(fecha) && r.AccountModality is not null).ToList();
        if (reglas.Count == 0) return ModalidadDeConsignacion.Free;
        bool? hayExigibles = null;
        foreach (var r in reglas)
        {
            if (!r.OnlyWhenInstallmentsDue) return (ModalidadDeConsignacion)r.AccountModality!.Value;
            hayExigibles ??= await ExigiblesAsync(persona, cuenta, fecha, ct);
            if (hayExigibles == true) return (ModalidadDeConsignacion)r.AccountModality!.Value;
        }
        return ModalidadDeConsignacion.Free;
    }

    private async Task<bool> ExigiblesAsync(Guid persona, Guid cuenta, DateOnly fecha, CancellationToken ct)
    {
        var r = await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, [persona], fecha), ct);
        return r.Value?.Installments.Any(i => i.AccountPublicId == cuenta && i.Pending > 0m) == true;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NaturalezasAsync(IReadOnlyList<ReceiptApplication> aplicaciones, DateOnly hoy, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, string>();
        var titulares = aplicaciones.Where(a => a.Target == ApplicationTarget.Account).Select(a => a.BeneficiaryPersonId).Distinct().ToList();
        if (titulares.Count == 0) return resultado;
        var publicos = await db.People.AsNoTracking().Where(p => titulares.Contains(p.Id)).Select(p => p.PublicId).ToListAsync(ct);
        foreach (var titular in publicos)
        {
            var r = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, titular, hoy), ct);
            foreach (var c in r.Value?.Accounts ?? []) resultado[c.AccountPublicId] = c.Nature.ToString();
        }
        return resultado;
    }

    /// <summary>FR-071 en R0: la identificación tardía se aplicó con la fecha más antigua que admite el contrato; se avisa al módulo para que corrija la mora.</summary>
    private async Task AvisarFechaTardiaAsync(Receipt recibo, DateOnly movimiento, CancellationToken ct) =>
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoFechaValorTardia, "Recaudo aplicado con fecha valor posterior al movimiento",
            string.Create(CultureInfo.InvariantCulture,
                $"El recibo {recibo.Number} se aplicó con fecha {recibo.ValueDate:dd/MM/yyyy} y el dinero llegó el {movimiento:dd/MM/yyyy}: revise la mora causada en esos días."),
            "Receipt", recibo.PublicId, DedupKey: $"Recaudo.FechaValorTardia:{recibo.PublicId:N}", ModuloDeOrigen: "Receipts",
            ScopeBranchPublicId: await db.Branches.AsNoTracking().Where(b => b.Id == recibo.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)), ct);

    private static string OperacionesDeCajaJson(IReadOnlyList<Guid> cuotas) => System.Text.Json.JsonSerializer.Serialize(cuotas);
}
