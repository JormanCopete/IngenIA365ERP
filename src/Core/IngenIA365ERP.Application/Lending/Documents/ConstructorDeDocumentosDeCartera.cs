using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// Lo que se pide armar: la clase, el titular, el crédito (si la clase lo lleva), la fecha de corte del certificado, el instante R y, en K5, el período
/// del extracto y el año del certificado tributario. (nuevo)
/// </summary>
public sealed record PedidoDeDocumentoDeCartera(IssuedDocumentKind Clase, Person Titular, Loan? Credito, DateOnly? Corte, DateTime Emision, DateOnly Hoy,
    PeriodoDeDocumento? Periodo = null, short? Año = null);

/// <summary>Un crédito que cubre el documento (<c>LND_IssuedDocumentLoans</c>). (nuevo)</summary>
public sealed record LineaDeDocumentoDeCartera(int LoanId, long? LastOperationId, long? LastTransactionId, decimal? BalanceAtCutoff, long? PayoffOperationId);

/// <summary>El firmante vigente de los documentos (<c>Documentos.Firmante.Nombre</c> y <c>.Cargo</c>). (nuevo)</summary>
public sealed record FirmanteDeDocumentos(string Nombre, string Cargo);

/// <summary>
/// Un documento armado y validado, listo para numerar y pintar: sus créditos, el cuerpo de su clase, el corte, la firmeza, las codeudas, las ventas
/// fuera de Cartera, la huella de la liquidación y la versión de reglas (T24), lo que mostrará la verificación y las leyendas propias de la clase. (nuevo)
/// </summary>
public sealed record DocumentoArmado(
    IReadOnlyList<LineaDeDocumentoDeCartera> Lineas,
    CuerpoDeDocumentoDeCartera Cuerpo,
    DateOnly? AsOfDate,
    DateOnly? FinalityUntil,
    bool? CoversCoDebts,
    string? OutsideLendingNote,
    string? SettlementSha256,
    string RulesVersion,
    Dictionary<string, object?> DatosEsenciales,
    IReadOnlyList<string> Leyendas);

/// <summary>
/// Arma cada clase de documento de K1 (feature 013, US13, T0783; adaptadores-y-archivos.md §8.5, §8.6; FR-131, FR-068, T24, D23). Lee, no escribe:
/// <list type="bullet">
/// <item><b>plan de pagos</b>: el crédito vivo y la versión de su plan vigente en R, con sus condiciones selladas y las tasas efectiva y nominal;</item>
/// <item><b>paz y salvo de una obligación</b>: saldo cero <b>hoy con la información de ahora</b>; si no, <c>Lending.Document.ClearanceDenied</c> con la
/// obligación. La firmeza es el registro del pago que canceló más los días hábiles de <c>Pagos.FirmezaDiasPorClaseDeMedio</c> de su clase de medio
/// (<see cref="FirmezaDelPago"/>, con <c>ICalendarioHabil</c>; el cruce interno, cero);</item>
/// <item><b>paz y salvo general</b>: todas las obligaciones de la persona como deudora —y como codeudora sólo con
/// <c>Documentos.PazYSalvoIncluyeCodeudas</c>— en cero; una venta suya con el mensaje en reproceso (recibo <c>Rejected</c> todavía reprocesable)
/// lo niega con la factura (US13-4); una venta aceptada sin migrar en la activación (<c>NotMigratedWithBalance</c>, <c>OutsideLendingD29</c> de la
/// conciliación de la carga activada) no lo niega y queda en <c>OutsideLendingNote</c>;</item>
/// <item><b>certificado de saldo</b>: la liquidación a la fecha D con R = el instante de emisión por <see cref="LecturaDelCredito"/>, con su huella y
/// su versión de reglas; una D futura se proyecta sin pagos y dice hasta cuándo vale;</item>
/// <item><b>extracto</b> (K5, T0971): la reconstrucción a la víspera del período y a su último día (con el libro teórico desde el primero), las dos
/// con R = la emisión, por <see cref="ExtractoDeCredito"/>; los reversos conocidos se informan con lo que reversaron;</item>
/// <item><b>certificado tributario</b> (K5, T0971): todos los créditos de la persona como deudora reconstruidos al 31 de diciembre con R = la emisión,
/// por <see cref="CertificadoTributario"/>; los de segmento SARC <c>VIVIENDA</c> según el catálogo de modalidades vigente a ese día, aparte. Sin
/// créditos en el año, <c>Lending.Document.NothingToCertify</c>.</item>
/// </list>
/// Cada crédito que toca pasa por el alcance de oficina y línea (<see cref="LecturaDelCredito"/>): fuera, el 404 genérico. (nuevo)
/// </summary>
public sealed class ConstructorDeDocumentosDeCartera(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeParametrosDeCartera parametros,
    FirmezaDelPago firmeza,
    IDateTimeService reloj,
    INormativaVigente? normativa = null)
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Los estados en que un crédito no es una obligación que certificar (nunca se desembolsó o se anuló antes).</summary>
    private static readonly LoanStatus[] SinObligacion = [LoanStatus.PendingDisbursement, LoanStatus.Voided];

    /// <summary>El firmante vigente; sin nombre o sin cargo, <c>Lending.Document.SignerMissing</c>.</summary>
    public async Task<Result<FirmanteDeDocumentos>> FirmanteAsync(DateOnly hoy, CancellationToken ct)
    {
        var nombre = await parametros.LeerAsync(ParametrosDeCartera.DocumentosFirmanteNombre, hoy, ct: ct);
        var cargo = await parametros.LeerAsync(ParametrosDeCartera.DocumentosFirmanteCargo, hoy, ct: ct);
        var n = nombre.IsSuccess ? nombre.Value.Texto?.Trim() : null;
        var c = cargo.IsSuccess ? cargo.Value.Texto?.Trim() : null;
        if (string.IsNullOrWhiteSpace(n) || string.IsNullOrWhiteSpace(c)) return Result.Failure<FirmanteDeDocumentos>(DocumentErrors.SinFirmante());
        return Result.Success(new FirmanteDeDocumentos(n, c));
    }

    /// <summary>Arma el documento de <paramref name="p"/> (ver la clase). La clase ya es de K1 y el crédito, si viene, ya es del titular.</summary>
    public Task<Result<DocumentoArmado>> ArmarAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        return p.Clase switch
        {
            IssuedDocumentKind.PaymentPlan => PlanDePagosAsync(p, Requerido(p), ct),
            IssuedDocumentKind.LoanClearance => PazYSalvoAsync(p, Requerido(p), ct),
            IssuedDocumentKind.GeneralClearance => PazYSalvoGeneralAsync(p, ct),
            IssuedDocumentKind.BalanceCertificate => CertificadoDeSaldoAsync(p, Requerido(p), ct),
            IssuedDocumentKind.Statement => ExtractoAsync(p, Requerido(p), ct),
            IssuedDocumentKind.TaxCertificate => CertificadoTributarioAsync(p, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(p), p.Clase, "Clase de documento desconocida."),
        };
    }

    // ------------------------------------------------------------------------------------------------------- plan de pagos --

    private async Task<Result<DocumentoArmado>> PlanDePagosAsync(PedidoDeDocumentoDeCartera p, Loan credito, CancellationToken ct)
    {
        if (credito.Status != LoanStatus.Active)
            return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no está vigente y su plan ya no se certifica"));
        var version = await db.RepaymentScheduleVersions.AsNoTracking()
            .Where(v => v.LoanId == credito.Id && v.RegisteredAt <= p.Emision)
            .OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
        if (version is null) return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no tiene plan de pagos"));
        var condiciones = await db.LoanTermsVersions.AsNoTracking().FirstAsync(t => t.Id == version.TermsVersionId, ct);
        var cuotas = await db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == version.Id).OrderBy(i => i.InstallmentNumber)
            .Select(i => new CuotaDelPlanImpresa(i.InstallmentNumber, i.DueDate, i.Principal, i.Interest + i.GraceInterest, i.Insurance, i.OtherCharges,
                i.Total, i.ClosingPrincipal))
            .ToListAsync(ct);
        var linea = await NombreDeLineaAsync(credito.CreditLineId, ct);
        var efectiva = condiciones.AnnualRate;
        var nominal = efectiva is { } ea ? EquivalenciaDeTasas.NominalAnual(ea, condiciones.Periodicity) : (decimal?)null;
        var (ultimaOperacion, ultimaTransaccion) = await UltimasAsync(credito.Id, p.Emision, ct);

        var datos = Datos(p.Hoy);
        datos["loan"] = credito.Number;
        datos["scheduleVersion"] = version.VersionNumber;
        datos["effectiveFrom"] = version.EffectiveValueDate;
        datos["installments"] = cuotas.Count;
        datos["effectiveAnnualRate"] = efectiva;
        return Result.Success(new DocumentoArmado(
            [new LineaDeDocumentoDeCartera(credito.Id, ultimaOperacion, ultimaTransaccion, null, null)],
            new CuerpoDePlanDePagos(credito.Number, linea, version.VersionNumber, version.EffectiveValueDate, version.PrincipalBase, efectiva, nominal,
                condiciones.Periodicity.ToString(), cuotas.Count, cuotas),
            p.Hoy, null, null, null, null, version.RulesVersion, datos, []));
    }

    // ----------------------------------------------------------------------------------------------------- paz y salvo --

    private async Task<Result<DocumentoArmado>> PazYSalvoAsync(PedidoDeDocumentoDeCartera p, Loan credito, CancellationToken ct)
    {
        if (SinObligacion.Contains(credito.Status))
            return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no tiene desembolso"));
        var certificado = await CertificarAsync(credito, p, ct);
        if (certificado.IsFailure) return Falla(certificado.Error);
        var c = certificado.Value;
        if (c.Saldo > 0m) return Falla(DocumentErrors.PazYSalvoNegado([new ObligacionConSaldoDto(credito.PublicId, credito.Number, c.Saldo)], []));

        var leyendas = new List<string>();
        if (c.Firmeza is { } hasta && p.Hoy < hasta) leyendas.Add(string.Format(Co, ModelosDeDocumentoDeCartera.LeyendaDeFirmeza, Fecha(hasta)));
        var datos = Datos(p.Hoy);
        datos["loans"] = new[] { new { number = credito.Number, balance = 0m } };
        datos["finalityUntil"] = c.Firmeza;
        return Result.Success(new DocumentoArmado([c.Linea],
            new CuerpoDePazYSalvo(false, p.Hoy, [new CreditoCertificado(credito.Number, c.NombreDeLinea, "Deudor", 0m)], c.Firmeza, null, []),
            p.Hoy, c.Firmeza, null, null, c.Estado.Huella, c.Estado.RulesVersion, datos, leyendas));
    }

    private async Task<Result<DocumentoArmado>> PazYSalvoGeneralAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        var titular = p.Titular;
        var codeudas = await IncluyeCodeudasAsync(p.Hoy, ct);
        var comoDeudor = await db.Loans.AsNoTracking()
            .Where(l => l.DebtorPersonId == titular.Id && !l.IsDeleted && !SinObligacion.Contains(l.Status))
            .OrderBy(l => l.Id).ToListAsync(ct);
        var comoCodeudor = new List<Loan>();
        if (codeudas)
        {
            var partes = await db.LoanParties.AsNoTracking()
                .Where(x => x.PersonId == titular.Id && x.Role == CreditPartyRole.CoDebtor && !x.IsDeleted).ToListAsync(ct);
            var ids = partes.Where(x => x.VigenteA(p.Hoy)).Select(x => x.LoanId).Distinct().ToList();
            comoCodeudor = await db.Loans.AsNoTracking()
                .Where(l => ids.Contains(l.Id) && l.DebtorPersonId != titular.Id && !l.IsDeleted && !SinObligacion.Contains(l.Status))
                .OrderBy(l => l.Id).ToListAsync(ct);
        }

        var certificados = new List<(Loan Credito, string Papel, Certificado Datos)>();
        foreach (var (credito, papel) in comoDeudor.Select(l => (l, "Deudor")).Concat(comoCodeudor.Select(l => (l, "Codeudor"))))
        {
            var c = await CertificarAsync(credito, p, ct);
            if (c.IsFailure) return Falla(c.Error);
            certificados.Add((credito, papel, c.Value));
        }
        var conSaldo = certificados.Where(c => c.Datos.Saldo > 0m)
            .Select(c => new ObligacionConSaldoDto(c.Credito.PublicId, c.Credito.Number, c.Datos.Saldo)).ToList();

        var tipoDeVenta = VentaACreditoRegistradaV1.Type;
        var enReproceso = await db.LendingInboundMessages.AsNoTracking()
            .Where(m => m.PersonId == titular.Id && m.MessageType == tipoDeVenta && m.Status == InboundMessageStatus.Rejected && m.NotRetryableAt == null)
            .OrderBy(m => m.Id)
            .Select(m => new VentaEnReprocesoDto(m.OriginNumber, m.MessagePublicId))
            .ToListAsync(ct);
        if (conSaldo.Count > 0 || enReproceso.Count > 0) return Falla(DocumentErrors.PazYSalvoNegado(conSaldo, enReproceso));

        var fuera = await VentasFueraDeCarteraAsync(titular.Id, ct);
        var firmezas = certificados.Where(c => c.Datos.Firmeza is not null).Select(c => c.Datos.Firmeza!.Value).ToList();
        DateOnly? firmeza = firmezas.Count == 0 ? null : firmezas.Max();

        var leyendas = new List<string> { CuerpoDePazYSalvo.LeyendaDeAlcance, codeudas ? ModelosDeDocumentoDeCartera.LeyendaConCodeudas : ModelosDeDocumentoDeCartera.LeyendaSinCodeudas };
        if (firmeza is { } hasta && p.Hoy < hasta) leyendas.Add(string.Format(Co, ModelosDeDocumentoDeCartera.LeyendaDeFirmeza, Fecha(hasta)));
        string? nota = null;
        if (fuera.Count > 0)
        {
            var facturas = string.Join(", ", fuera.Select(v => v.Factura));
            nota = Recortar(string.Format(Co, ModelosDeDocumentoDeCartera.LeyendaDeVentasFueraDeCartera, $"de las facturas {facturas}"),
                Domain.Entities.Lending.Documents.IssuedLendingDocument.LargoDeLaNota);
            leyendas.Add(nota);
        }

        var datos = Datos(p.Hoy);
        datos["coversCoDebts"] = codeudas;
        datos["loans"] = certificados.Select(c => new { number = c.Credito.Number, role = c.Papel, balance = c.Datos.Saldo }).ToList();
        datos["finalityUntil"] = firmeza;
        datos["outsideLending"] = fuera.Select(v => v.Factura).ToList();
        var version = certificados.Select(c => c.Datos.Estado.RulesVersion).FirstOrDefault() ?? VersionesDeReglas.Vigente;
        return Result.Success(new DocumentoArmado(certificados.Select(c => c.Datos.Linea).ToList(),
            new CuerpoDePazYSalvo(true, p.Hoy, certificados.Select(c => new CreditoCertificado(c.Credito.Number, c.Datos.NombreDeLinea, c.Papel, c.Datos.Saldo)).ToList(),
                firmeza, codeudas, fuera),
            p.Hoy, firmeza, codeudas, nota, null, version, datos, leyendas));
    }

    // -------------------------------------------------------------------------------------------- certificado de saldo --

    private async Task<Result<DocumentoArmado>> CertificadoDeSaldoAsync(PedidoDeDocumentoDeCartera p, Loan credito, CancellationToken ct)
    {
        if (SinObligacion.Contains(credito.Status))
            return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no tiene desembolso"));
        var d = p.Corte ?? p.Hoy;
        var desde = await db.LoanOperations.AsNoTracking()
            .Where(o => o.LoanId == credito.Id && (o.Kind == LoanOperationKind.Disbursement || o.Kind == LoanOperationKind.OpeningBalance))
            .MinAsync(o => (DateOnly?)o.ValueDate, ct) ?? credito.OpenedOn;
        if (d < desde) return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no tenía saldo al {Fecha(d)}"));

        var leido = await LecturaDelCredito.ReconstruirAsync(db, cargador, alcance, auditoria, reloj, credito.PublicId, d, p.Emision, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var estado = leido.Value.Estado;
        var liquidacion = GetLoanSettlementQueryHandler.Dto(credito, estado, d, p.Emision);
        var proyectada = d > p.Hoy;
        DateOnly? validaHasta = proyectada ? estado.ValidaHasta is { } v && v < d ? v : d : null;
        var (ultimaOperacion, ultimaTransaccion) = await UltimasAsync(credito.Id, p.Emision, ct);
        var conceptos = liquidacion.ByConcept.Where(c => c.Value != 0m).Select(c => new ConceptoCertificado(c.Key, c.Value)).ToList();

        var leyendas = new List<string>();
        if (validaHasta is { } hasta) leyendas.Add(string.Format(Co, ModelosDeDocumentoDeCartera.LeyendaDeProyeccion, Fecha(hasta)));
        var datos = Datos(p.Hoy);
        datos["loan"] = credito.Number;
        datos["date"] = d;
        datos["toPayOff"] = liquidacion.ToPayOff;
        datos["validUntil"] = validaHasta;
        return Result.Success(new DocumentoArmado(
            [new LineaDeDocumentoDeCartera(credito.Id, ultimaOperacion, ultimaTransaccion, liquidacion.ToPayOff, null)],
            new CuerpoDeCertificadoDeSaldo(credito.Number, await NombreDeLineaAsync(credito.CreditLineId, ct), d, conceptos, liquidacion.ToPayOff,
                liquidacion.DaysPastDue, proyectada, validaHasta, liquidacion.Fingerprint, estado.RulesVersion),
            d, null, null, null, estado.Huella, estado.RulesVersion, datos, leyendas));
    }

    // ------------------------------------------------------------------------------------------------------- extracto (K5) --

    private async Task<Result<DocumentoArmado>> ExtractoAsync(PedidoDeDocumentoDeCartera p, Loan credito, CancellationToken ct)
    {
        var periodo = p.Periodo ?? throw new ArgumentException("El extracto lleva período.", nameof(p));
        if (SinObligacion.Contains(credito.Status))
            return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no tiene desembolso"));
        var cargado = await cargador.CargarAsync(credito.Id, p.Emision, periodo.To, ct);
        if (cargado is null) return Falla(LecturaDelCredito.Inexistente());
        if (!cargado.Listo) return Falla(cargado.Error!);
        var origen = Origen(cargado);
        if (origen is null || periodo.To < origen)
            return Falla(DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no existía en el período"));

        ExtractoArmado extracto;
        EstadoDelCredito alFinal;
        try
        {
            var vispera = periodo.From.AddDays(-1);
            var alInicio = vispera >= origen ? MotorDeCartera.Reconstruir(cargado.Entrada!, vispera) : null;
            alFinal = MotorDeCartera.Reconstruir(cargado.Entrada!, periodo.To, periodo.From);
            if (SinUsura(cargado, alFinal, periodo.To, p.Hoy) is { } faltante) return Falla(faltante);
            var porId = cargado.Operaciones.ToDictionary(o => o.Id);
            var operaciones = cargado.Operaciones.ToDictionary(o => CargadorDeReconstruccion.Id(o.Id),
                o => new OperacionDelExtracto(CargadorDeReconstruccion.Id(o.Id), o.Number, o.Kind), StringComparer.Ordinal);
            var reversos = cargado.Operaciones
                .Where(o => o.ReversesOperationId is { } r && porId.ContainsKey(r))
                .Select(o => (Reverso: o, Original: porId[o.ReversesOperationId!.Value]))
                .Select(x => new ReversoDelExtracto(x.Original.ValueDate, x.Reverso.Number, x.Original.Number, x.Original.Kind, x.Original.Amount))
                .ToList();
            extracto = ExtractoDeCredito.Armar(alInicio, alFinal, periodo.From, periodo.To, operaciones, reversos);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ex.Codigo == ExtractoDeCredito.CodigoNadaQueExtractar
                ? DocumentErrors.NadaQueCertificar($"el crédito {credito.Number} no tuvo movimientos ni saldo en el período")
                : ErroresDeGobierno.De(ex.Problema));
        }

        var (ultimaOperacion, ultimaTransaccion) = await UltimasAsync(credito.Id, p.Emision, ct);
        var lineas = extracto.Lineas.Select(l =>
        {
            var intereses = Intereses.Sum(c => l.De(c));
            var capital = l.De(LoanConcept.Principal);
            return new LineaDeExtractoImpresa(l.FechaValor, l.Descripcion, capital, intereses, l.Neto - capital - intereses, l.Neto, l.ValorInformativo);
        }).ToList();
        var datos = Datos(p.Hoy);
        datos["loan"] = credito.Number;
        datos["periodFrom"] = periodo.From;
        datos["periodTo"] = periodo.To;
        datos["openingBalance"] = extracto.TotalInicial;
        datos["closingBalance"] = extracto.TotalFinal;
        datos["movements"] = extracto.Lineas.Count;
        return Result.Success(new DocumentoArmado(
            [new LineaDeDocumentoDeCartera(credito.Id, ultimaOperacion, ultimaTransaccion, extracto.TotalFinal, null)],
            new CuerpoDeExtracto(credito.Number, await NombreDeLineaAsync(credito.CreditLineId, ct), periodo.From, periodo.To, Conceptos(extracto.SaldoInicial),
                lineas, Conceptos(extracto.SaldoFinal), extracto.TotalInicial, extracto.TotalFinal),
            null, null, null, null, alFinal.Huella, alFinal.RulesVersion, datos, [ModelosDeDocumentoDeCartera.LeyendaDelExtracto]));
    }

    // ------------------------------------------------------------------------------------------ certificado tributario (K5) --

    private async Task<Result<DocumentoArmado>> CertificadoTributarioAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        var año = p.Año ?? throw new ArgumentException("El certificado tributario lleva año.", nameof(p));
        var cierre = new DateOnly(año, 12, 31);
        var creditos = await db.Loans.AsNoTracking()
            .Where(l => l.DebtorPersonId == p.Titular.Id && !l.IsDeleted && !SinObligacion.Contains(l.Status) && l.OpenedOn <= cierre)
            .OrderBy(l => l.Id).ToListAsync(ct);
        foreach (var credito in creditos)
        {
            var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, LecturaDelCredito.Inexistente(), auditoria, "Loan",
                credito.PublicId, ct);
            if (dentro.IsFailure) return Falla(dentro.Error);
        }
        var cargados = creditos.Count == 0
            ? new Dictionary<int, CreditoCargado>()
            : await cargador.CargarAsync(creditos.Select(c => c.Id).ToList(), p.Emision, cierre, ct);
        var catalogo = normativa is null ? null : await normativa.CatalogoDeModalidadesAsync(cierre, p.Emision, ct);

        var paraCertificar = new List<CreditoParaCertificar>();
        var porNumero = new Dictionary<string, (Loan Credito, EstadoDelCredito Estado)>(StringComparer.Ordinal);
        foreach (var credito in creditos)
        {
            if (!cargados.TryGetValue(credito.Id, out var cargado)) continue;
            if (!cargado.Listo) return Falla(cargado.Error!);
            if (Origen(cargado) is not { } origen || origen > cierre) continue;
            EstadoDelCredito estado;
            try
            {
                estado = MotorDeCartera.Reconstruir(cargado.Entrada!, cierre);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            if (SinUsura(cargado, estado, cierre, p.Hoy) is { } faltante) return Falla(faltante);
            var vivienda = EsVivienda(catalogo, cargado.Vigentes.ModalityCode, cargado.Vigentes.SubsegmentCode);
            paraCertificar.Add(new CreditoParaCertificar(credito.Number, await NombreDeLineaAsync(credito.CreditLineId, ct), vivienda, estado));
            porNumero[credito.Number] = (credito, estado);
        }

        var certificado = CertificadoTributario.Armar(año, paraCertificar);
        if (certificado.Vacio)
            return Falla(DocumentErrors.NadaQueCertificar($"la persona no tuvo créditos con la cooperativa en {año}"));

        var lineas = new List<LineaDeDocumentoDeCartera>();
        foreach (var c in certificado.Todos)
        {
            var credito = porNumero[c.Numero].Credito;
            var (ultimaOperacion, ultimaTransaccion) = await UltimasAsync(credito.Id, p.Emision, ct);
            lineas.Add(new LineaDeDocumentoDeCartera(credito.Id, ultimaOperacion, ultimaTransaccion, c.SaldoTotal, null));
        }

        var datos = Datos(p.Hoy);
        datos["year"] = año;
        datos["loans"] = certificado.Todos.Select(c => new
        {
            number = c.Numero, housing = c.EsVivienda, interestPaid = c.InteresesPagados, balanceAtYearEnd = c.SaldoTotal,
        }).ToList();
        datos["totalInterestPaid"] = certificado.TotalInteresesPagados;
        datos["totalHousingInterestPaid"] = certificado.TotalInteresesDeVivienda;
        var version = porNumero.Values.Select(x => x.Estado.RulesVersion).FirstOrDefault() ?? VersionesDeReglas.Vigente;
        return Result.Success(new DocumentoArmado(lineas,
            new CuerpoDeCertificadoTributario(año, certificado.Creditos.Select(Impreso).ToList(), certificado.Vivienda.Select(Impreso).ToList(),
                certificado.TotalInteresesPagados, certificado.TotalInteresesDeVivienda, certificado.TotalSaldo),
            cierre, null, null, null, null, version, datos,
            [CuerpoDePazYSalvo.LeyendaDeAlcance, ModelosDeDocumentoDeCartera.LeyendaDeInteresesPagados]));
    }

    private static CreditoCertificadoTributario Impreso(InteresesPagadosPorCredito c) => new(c.Numero, c.Linea, c.InteresCorrientePagado,
        c.InteresDeMoraPagado, c.InteresesPagados, c.SaldoDeCapital, c.SaldoDeIntereses, c.SaldoDeOtros, c.SaldoTotal);

    /// <summary>La fecha valor del primer desembolso o del saldo migrado conocidos en R; nula si el crédito no tiene ninguno.</summary>
    private static DateOnly? Origen(CreditoCargado cargado) =>
        cargado.Operaciones.Where(o => o.Kind is LoanOperationKind.Disbursement or LoanOperationKind.OpeningBalance)
            .Select(o => (DateOnly?)o.ValueDate).Min();

    /// <summary>
    /// ¿El segmento SARC de la modalidad es <c>VIVIENDA</c> en el catálogo vigente al 31 de diciembre? Sin catálogo legible, la modalidad
    /// <c>VIVIENDA</c> misma (hasta K6b ninguna línea la usa).
    /// </summary>
    private static bool EsVivienda(Result<DocumentoNormativoVigente<CatalogoDeModalidades>>? catalogo, string modalidad, string? subsegmento)
    {
        const string vivienda = "VIVIENDA";
        if (catalogo is { IsSuccess: true } leido && leido.Value.Documento.Buscar(modalidad, subsegmento) is { } combinacion)
            return string.Equals(combinacion.SarcSegment, vivienda, StringComparison.OrdinalIgnoreCase);
        return string.Equals(modalidad, vivienda, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Como la liquidación (<see cref="LecturaDelCredito"/>): un día hasta D ≤ hoy sin usura certificada no se certifica.</summary>
    private static Error? SinUsura(CreditoCargado cargado, EstadoDelCredito estado, DateOnly d, DateOnly hoy) =>
        d <= hoy && cargado.Entrada!.Condiciones.UsuraRequerida && estado.PrimerDiaSinUsura is { } sinUsura && sinUsura <= d
            ? ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura, cargado.Series?.ClaveDeUsura ?? cargado.Pactadas.ModalityCode, sinUsura)
            : null;

    private static readonly LoanConcept[] Intereses =
        [LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    /// <summary>Los conceptos con saldo distinto de cero, en el orden del enum, como los imprime el documento.</summary>
    private static List<ConceptoCertificado> Conceptos(IReadOnlyDictionary<LoanConcept, decimal> saldos) =>
        saldos.Where(x => x.Value != 0m).OrderBy(x => (int)x.Key).Select(x => new ConceptoCertificado(x.Key.ToString(), x.Value)).ToList();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    /// <summary>Lo que se sabe de un crédito certificado: el estado de hoy con la información de ahora, su saldo, el pago que lo canceló y la firmeza.</summary>
    private sealed record Certificado(EstadoDelCredito Estado, decimal Saldo, LineaDeDocumentoDeCartera Linea, DateOnly? Firmeza, string NombreDeLinea);

    private async Task<Result<Certificado>> CertificarAsync(Loan credito, PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        // «Saldo cero a hoy con la información de ahora»: R nulo.
        var leido = await LecturaDelCredito.ReconstruirAsync(db, cargador, alcance, auditoria, reloj, credito.PublicId, p.Hoy, null, ct);
        if (leido.IsFailure) return Result.Failure<Certificado>(leido.Error);
        var estado = leido.Value.Estado;
        var saldo = Math.Max(0m, estado.ValorParaCancelar);
        var (ultimaOperacion, ultimaTransaccion) = await UltimasAsync(credito.Id, p.Emision, ct);

        long? pagoQueCancelo = null;
        DateOnly? hasta = null;
        if (saldo == 0m)
        {
            var pago = await db.LoanOperations.AsNoTracking()
                .Where(o => o.LoanId == credito.Id && o.Kind == LoanOperationKind.Payment && o.ReversedByOperationId == null)
                .OrderByDescending(o => o.Sequence).FirstOrDefaultAsync(ct);
            if (pago is not null)
            {
                pagoQueCancelo = pago.Id;
                hasta = await FirmezaAsync(pago, ct);
            }
        }
        return Result.Success(new Certificado(estado, saldo,
            new LineaDeDocumentoDeCartera(credito.Id, ultimaOperacion, ultimaTransaccion, saldo, pagoQueCancelo), hasta,
            await NombreDeLineaAsync(credito.CreditLineId, ct)));
    }

    /// <summary>
    /// D23: el registro del pago que canceló (fecha de Colombia) más los días hábiles de su clase de medio en <c>Pagos.FirmezaDiasPorClaseDeMedio</c>
    /// (el cruce interno, cero). Una clase que el contrato no conoce cuenta con el defecto (<see cref="FirmezaDelPago"/>).
    /// </summary>
    private async Task<DateOnly> FirmezaAsync(Domain.Entities.Lending.Operations.LoanOperation pago, CancellationToken ct)
    {
        var desfase = reloj.AhoraLocal.Offset;
        var registro = DateOnly.FromDateTime(DateTime.SpecifyKind(pago.RegisteredAt, DateTimeKind.Unspecified) + desfase);
        var clase = Enum.TryParse<ClaseDeMedioDeRecaudo>(pago.PaymentMeansClass, out var c) ? c : ClaseDeMedioDeRecaudo.Other;
        return await firmeza.CalcularAsync(clase, registro, ct);
    }

    private async Task<bool> IncluyeCodeudasAsync(DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.DocumentosPazYSalvoIncluyeCodeudas, hoy, ct: ct);
        if (leido.IsFailure) return false;
        if (leido.Value.Valor is bool b) return b;
        var texto = leido.Value.Texto?.Trim();
        return bool.TryParse(texto, out var valor) ? valor : texto is "1" or "Sí" or "Si" or "S";
    }

    /// <summary>Las ventas de la persona aceptadas sin migrar en la carga con que se activó Cartera (FR-143): no niegan el paz y salvo general.</summary>
    private async Task<IReadOnlyList<VentaFueraDeCartera>> VentasFueraDeCarteraAsync(int personId, CancellationToken ct)
    {
        var activacion = await db.LendingActivations.AsNoTracking().OrderByDescending(a => a.Id)
            .Select(a => new { a.MigrationLoadId, a.CheckVersion }).FirstOrDefaultAsync(ct);
        if (activacion is null) return [];
        var aceptadas = new[] { SaleReconciliationOutcome.NotMigratedWithBalance, SaleReconciliationOutcome.OutsideLendingD29 };
        return await db.MigrationSaleReconciliations.AsNoTracking()
            .Where(s => s.MigrationLoadId == activacion.MigrationLoadId && s.CheckVersion == activacion.CheckVersion && s.PersonId == personId
                        && aceptadas.Contains(s.Outcome))
            .OrderBy(s => s.SaleDate).ThenBy(s => s.InvoiceNumber)
            .Select(s => new VentaFueraDeCartera(s.InvoiceNumber, s.SaleDate, s.Amount))
            .ToListAsync(ct);
    }

    /// <summary>La última operación y la última transacción del crédito conocidas en R.</summary>
    private async Task<(long? Operacion, long? Transaccion)> UltimasAsync(int loanId, DateTime r, CancellationToken ct)
    {
        var operacion = await db.LoanOperations.AsNoTracking().Where(o => o.LoanId == loanId && o.RegisteredAt <= r).MaxAsync(o => (long?)o.Id, ct);
        var transaccion = await db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == loanId && t.RegisteredAt <= r).MaxAsync(t => (long?)t.Id, ct);
        return (operacion, transaccion);
    }

    private async Task<string> NombreDeLineaAsync(int creditLineId, CancellationToken ct) =>
        await db.CreditLines.AsNoTracking().Where(l => l.Id == creditLineId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

    private static Dictionary<string, object?> Datos(DateOnly hoy) => new(StringComparer.Ordinal) { ["asOfDate"] = hoy };

    private static Loan Requerido(PedidoDeDocumentoDeCartera p) =>
        p.Credito ?? throw new ArgumentException($"La clase {p.Clase} se emite sobre un crédito.", nameof(p));

    private static string Fecha(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];

    private static Result<DocumentoArmado> Falla(Error e) => Result.Failure<DocumentoArmado>(e);
}
