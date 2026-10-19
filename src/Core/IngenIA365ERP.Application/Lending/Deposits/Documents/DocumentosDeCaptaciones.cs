using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Holds;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>Lo que la respuesta al juzgado lleva al documento (feature 014, US13, T196; research E176). (nuevo)</summary>
public sealed record RespuestaParaDocumento(int GarnishmentId, GarnishmentResponseKind Clase, DateOnly Enviada, string Texto, decimal? Retenido,
    decimal? Consignado);

/// <summary>
/// Arma los documentos al titular de captaciones que faltaban (feature 014, US13, T196; contracts/documentos.md §2.1 a §2.5; FR-068, FR-101 a FR-105) para
/// <see cref="ConstructorDeDocumentosDeCartera"/>. Lee, no escribe; todo con R = el instante de emisión (sólo lo registrado hasta R):
/// <list type="bullet">
/// <item><b>extracto</b> de una cuenta y un período (§2.1): saldo inicial por concepto, movimientos por fecha valor con su operación, canal y el reverso
/// enlazado, intereses, retención (o por qué no se retuvo), GMF, cargos, saldo final por concepto, disponible desglosado y, en aportes, las cuotas del
/// período;</item>
/// <item><b>certificado de saldo</b> a la fecha D (§2.2): la reconstrucción a (D, R) por concepto, estado, apertura, titulares y manejo, y los bloqueos y
/// embargos vigentes sólo si se piden;</item>
/// <item><b>certificado tributario</b> del año (§2.3, SC-016) por <see cref="CertificadoTributarioDeCaptaciones"/>: cuadra con los acumulados de retención y
/// el GMF del año, migración incluida;</item>
/// <item><b>certificado de retención</b> de un período (§2.4) por <see cref="CertificadoDeRetencionDeCaptaciones"/>;</item>
/// <item><b>título del CDAT</b> de una versión de condiciones (§2.5), con el número del título anterior;</item>
/// <item><b>respuesta al juzgado</b> de un embargo (research E176).</item>
/// </list>
/// Cada cuenta que toca la cubre (<c>LND_IssuedDocumentAccounts</c>) para que el reverso de una operación la anule (§4). (nuevo)
/// </summary>
public sealed class DocumentosDeCaptaciones(
    IApplicationDbContext db,
    ReglasDeMovimientos movimientos,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales? valores = null,
    IPerfilRegulatorio? perfil = null)
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public Task<Result<DocumentoArmado>> ArmarAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        return p.Clase switch
        {
            IssuedDocumentKind.AccountStatement => ExtractoAsync(p, Cuenta(p), ct),
            IssuedDocumentKind.AccountBalanceCertificate => CertificadoDeSaldoAsync(p, Cuenta(p), ct),
            IssuedDocumentKind.DepositTaxCertificate => CertificadoTributarioAsync(p, ct),
            IssuedDocumentKind.WithholdingCertificate => CertificadoDeRetencionAsync(p, ct),
            IssuedDocumentKind.TermDepositTitle => TituloAsync(p, Cuenta(p), ct),
            IssuedDocumentKind.GarnishmentResponse => RespuestaAsync(p, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(p), p.Clase, "Clase de documento que no es de captaciones."),
        };
    }

    // ---------------------------------------------------------------------------------------------------------- extracto --

    private async Task<Result<DocumentoArmado>> ExtractoAsync(PedidoDeDocumentoDeCartera p, MemberAccount cuenta, CancellationToken ct)
    {
        var periodo = p.Periodo ?? throw new ArgumentException("El extracto lleva período.", nameof(p));
        if (periodo.To < cuenta.OpenedOn)
            return Falla(DocumentErrors.NadaQueCertificar($"la cuenta {cuenta.Number} no existía en el período"));
        var r = p.Emision;
        var libro = await LibroAsync(cuenta.Id, r, ct);
        var filas = libro.Where(f => f.Fila.ValueDate <= periodo.To).Select(f => new FilaDeExtracto(f.Fila.Id, f.Fila.Kind, f.Fila.Concept, f.Fila.Amount,
            f.Fila.ValueDate, f.Fila.OperationId)).ToList();
        var idsDeOperacion = filas.Where(f => f.OperacionId is not null && f.FechaValor >= periodo.From).Select(f => f.OperacionId!.Value).Distinct().ToList();
        var operaciones = await db.MemberAccountOperations.AsNoTracking().Where(o => idsDeOperacion.Contains(o.Id)).ToListAsync(ct);
        var reversadas = operaciones.Where(o => o.ReversesOperationId is not null).Select(o => o.ReversesOperationId!.Value).ToList();
        var numerosReversados = await db.MemberAccountOperations.AsNoTracking().Where(o => reversadas.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Number, ct);
        var porId = operaciones.ToDictionary(o => o.Id);
        var armado = ExtractosDeCuenta.Armar(cuenta.Nature, periodo.From, periodo.To, filas,
            operaciones.ToDictionary(o => o.Id, o => new OperacionDeExtracto(o.PublicId, o.Number, o.Kind.ToString())));
        var movimientosImpresos = armado.Lineas.Select(l =>
        {
            var op = l.OperationNumber is null ? null : operaciones.FirstOrDefault(o => o.PublicId == l.OperationPublicId);
            return new MovimientoDeExtractoImpreso(l.ValueDate, op?.AccountingDate, op is null ? TipoSinOperacion(l.Kind) : TipoDeOperacion(op.Kind),
                op?.Number, op is null ? null : Canal(op), l.Amount, l.Balance,
                op?.ReversesOperationId is { } rev && numerosReversados.TryGetValue(rev, out var n) ? n : null);
        }).ToList();

        var alInicio = MotorDeCaptaciones.Reconstruir(libro.Select(x => x.Movimiento), periodo.From.AddDays(-1), r);
        var alFinal = MotorDeCaptaciones.Reconstruir(libro.Select(x => x.Movimiento), periodo.To, r);

        InteresesDelExtracto? intereses = null;
        RetencionDelExtracto? retencion = null;
        GmfDelExtracto? gmf = null;
        CargosDelExtracto? cargos = null;
        if (cuenta.Nature == LineNature.Deposit)
        {
            var delPeriodo = libro.Where(x => x.Fila.ValueDate >= periodo.From && x.Fila.ValueDate <= periodo.To).Select(x => x.Fila).ToList();
            var causado = delPeriodo.Where(f => f.Concept == MemberAccountConcept.InterestPayable
                                                && f.Kind is MemberAccountTransactionKind.InterestAccrued or MemberAccountTransactionKind.AccrualAdjustment)
                .Sum(f => f.Amount);
            var liquidaciones = await LiquidacionesAsync(cuenta.Id, periodo.From, periodo.To, r, ct);
            var version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, periodo.To, r, ct);
            intereses = new InteresesDelExtracto(Math.Round(causado, 2), liquidaciones.Sum(l => l.GrossInterest), liquidaciones.Sum(l => l.RoundingDifference),
                version?.AnnualRate is { } ea ? $"{Porcentaje(ea)} EA" : version?.RateKind == RateKind.Variable ? "variable" : null,
                version is null ? null : Metodo(version.BalanceMethod));
            var retenido = liquidaciones.Sum(l => l.WithholdingAmount);
            var motivo = retenido == 0m ? liquidaciones.Select(l => l.WithholdingExemptionReason).FirstOrDefault(m => m is not null) : null;
            retencion = new RetencionDelExtracto(liquidaciones.Sum(l => l.WithholdingBase), liquidaciones.Sum(l => l.InflationaryComponentAmount ?? 0m),
                liquidaciones.Select(l => l.WithholdingRate).Where(t => t > 0m).Distinct().Select(Porcentaje).FirstOrDefault(), retenido,
                retenido == 0m ? SinRetencion(motivo, liquidaciones.Count) : null);
            var usos = await (from u in db.GmfMonthlyUsages.AsNoTracking()
                              join o in db.MemberAccountOperations.AsNoTracking() on u.OperationId equals o.Id
                              where u.MemberAccountId == cuenta.Id && o.ValueDate >= periodo.From && o.ValueDate <= periodo.To && o.RegisteredAt <= r
                              select new { u.GmfAmount, u.ExemptAmount, u.Policy }).ToListAsync(ct);
            var politica = await LectorDeCuentas.PoliticaDeGmfAsync(parametros, periodo.To, ct);
            gmf = new GmfDelExtracto(usos.Sum(u => u.GmfAmount), usos.Sum(u => u.ExemptAmount), Politica(politica));
            var deCargos = await (from c in db.AccountCharges.AsNoTracking()
                                  join o in db.MemberAccountOperations.AsNoTracking() on c.AppliedOperationId equals o.Id
                                  where c.MemberAccountId == cuenta.Id && o.ValueDate >= periodo.From && o.ValueDate <= periodo.To && o.RegisteredAt <= r
                                  select new { c.Status, c.Amount }).ToListAsync(ct);
            cargos = new CargosDelExtracto(deCargos.Where(c => c.Status == AccountChargeStatus.Collected).Sum(c => c.Amount),
                deCargos.Where(c => c.Status == AccountChargeStatus.Pending).Sum(c => c.Amount), deCargos.Where(c => c.Status == AccountChargeStatus.Waived).Sum(c => c.Amount));
        }

        var cuotas = new List<CuotaDelExtractoImpresa>();
        if (cuenta.Nature == LineNature.Contribution || cuenta.DepositClass is DepositClass.Permanent or DepositClass.Contractual)
            cuotas = (await LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct, registradoHasta: r))
                .Where(q => q.Cuota.DueDate >= periodo.From && q.Cuota.DueDate <= periodo.To)
                .OrderBy(q => q.Cuota.DueDate)
                .Select(q => new CuotaDelExtractoImpresa(q.Cuota.Periodo, q.Cuota.DueDate, q.Cuota.Amount, q.Pendiente)).ToList();

        var disponible = await DisponibleAsync(cuenta, alFinal, periodo.To, ct);
        var (titulares, oficina, linea) = await DatosDeCuentaAsync(cuenta, periodo.To, ct);
        var leyendas = new List<string> { LeyendaDelExtracto };
        if (cuenta.Origin == MemberAccountOrigin.Migration && cuenta.ActivatedOn is { } activada && periodo.From < activada)
            leyendas.Add($"Saldo migrado al {Fecha(activada.AddDays(-1))}.");
        await TextoPropioAsync(p, leyendas, ct);

        var datos = Datos(periodo.To);
        datos["account"] = cuenta.Number;
        datos["periodFrom"] = periodo.From;
        datos["periodTo"] = periodo.To;
        datos["openingBalance"] = armado.SaldoInicial;
        datos["closingBalance"] = armado.SaldoFinal;
        datos["movements"] = armado.Lineas.Count;
        return Result.Success(new DocumentoArmado([],
            new CuerpoDeExtractoDeCuenta(cuenta.Number, linea, Clase(cuenta), oficina, titulares, periodo.From, periodo.To, armado.SaldoInicial,
                Conceptos(alInicio), movimientosImpresos, intereses, retencion, gmf, cargos, armado.SaldoFinal, Conceptos(alFinal), disponible, cuotas),
            null, null, null, null, alFinal.Huella, VersionesDeReglas.Vigente, datos, leyendas)
        {
            Cuentas = [Cubierta(cuenta.Id, libro, armado.SaldoFinal)],
        });
    }

    // ----------------------------------------------------------------------------------------------- certificado de saldo --

    private async Task<Result<DocumentoArmado>> CertificadoDeSaldoAsync(PedidoDeDocumentoDeCartera p, MemberAccount cuenta, CancellationToken ct)
    {
        var d = p.Corte ?? p.Hoy;
        if (d < cuenta.OpenedOn) return Falla(DocumentErrors.NadaQueCertificar($"la cuenta {cuenta.Number} no existía al {Fecha(d)}"));
        var libro = await LibroAsync(cuenta.Id, p.Emision, ct);
        var saldos = MotorDeCaptaciones.Reconstruir(libro.Select(x => x.Movimiento), d, p.Emision);
        var delTitular = cuenta.Nature == LineNature.Contribution ? saldos.SaldoDeAportes : saldos.SaldoDeCaptacion;
        IReadOnlyList<RestriccionImpresa>? restricciones = null;
        if (p.IncluyeRestricciones)
        {
            var vigentes = (await LectorDeRestricciones.VigentesAsync(db, [cuenta.Id], d, ct)).GetValueOrDefault(cuenta.Id) ?? [];
            restricciones = vigentes.Select(h => new RestriccionImpresa(h.Reason == HoldReason.Garnishment ? "Embargo" : "Bloqueo", Motivo(h.Reason),
                h.Amount ?? delTitular)).ToList();
        }
        var (titulares, _, linea) = await DatosDeCuentaAsync(cuenta, d, ct);
        var leyendas = new List<string>
        {
            string.Format(Co, CuerpoDeCertificadoDeSaldoDeCuenta.LeyendaDelCorte, Fecha(d), p.Emision.ToString("dd/MM/yyyy HH:mm", Co) + " UTC"),
        };
        await TextoPropioAsync(p, leyendas, ct);
        var datos = Datos(d);
        datos["account"] = cuenta.Number;
        datos["date"] = d;
        datos["holderBalance"] = delTitular;
        datos["fingerprint"] = saldos.Huella;
        return Result.Success(new DocumentoArmado([],
            new CuerpoDeCertificadoDeSaldoDeCuenta(cuenta.Number, linea, Clase(cuenta), Estado(cuenta.Status), cuenta.OpenedOn, Manejo(cuenta.HandlingMode),
                titulares, d, Conceptos(saldos), delTitular, restricciones, saldos.Huella),
            d, null, null, null, saldos.Huella, VersionesDeReglas.Vigente, datos, leyendas)
        {
            Cuentas = [Cubierta(cuenta.Id, libro, delTitular)],
        });
    }

    // -------------------------------------------------------------------------------------------- certificado tributario --

    private async Task<Result<DocumentoArmado>> CertificadoTributarioAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        var año = p.Año ?? throw new ArgumentException("El certificado tributario lleva año.", nameof(p));
        var desde = new DateOnly(año, 1, 1);
        var cierre = new DateOnly(año, 12, 31);
        var persona = p.Titular.Id;
        var r = p.Emision;
        var cuentas = await CuentasDeLaPersonaAsync(persona, ct);
        cuentas = cuentas.Where(c => c.OpenedOn <= cierre && (c.ClosedOn is null || c.ClosedOn >= desde)).ToList();
        var incluyeAportes = await SiNoAsync(ParametrosDeCartera.DocumentosCertificadoTributarioIncluyeAportes, p.Hoy, ct);

        var ids = cuentas.Select(c => c.Id).ToList();
        var acumulados = await (from a in db.WithholdingAccumulations.AsNoTracking()
                                join o in db.MemberAccountOperations.AsNoTracking() on a.OperationId equals o.Id
                                where ids.Contains(a.MemberAccountId) && a.TaxYear == año && o.RegisteredAt <= r
                                select new { a.MemberAccountId, a.PersonId, a.GrossInterest, a.InflationaryComponentAmount, a.WithholdingBase, a.WithholdingAmount })
            .ToListAsync(ct);
        var gmfs = await (from u in db.GmfMonthlyUsages.AsNoTracking()
                          join o in db.MemberAccountOperations.AsNoTracking() on u.OperationId equals o.Id
                          where ids.Contains(u.MemberAccountId) && u.Year == año && o.RegisteredAt <= r
                          select new { u.MemberAccountId, u.GmfAmount }).ToListAsync(ct);
        var migrado = await GmfMigradoAsync(cuentas, persona, año, ct);

        var paraCertificar = new List<CuentaParaElCertificadoTributario>();
        var libros = new Dictionary<int, IReadOnlyList<FilaDelLibro>>();
        foreach (var c in cuentas.OrderBy(c => c.Number, StringComparer.Ordinal))
        {
            var referencia = c.ClosedOn is { } cerrada && cerrada < cierre ? cerrada : cierre;
            var proporciones = await ProporcionesAsync(c, referencia, ct);
            var libro = await LibroAsync(c.Id, r, ct);
            libros[c.Id] = libro;
            var saldos = MotorDeCaptaciones.Reconstruir(libro.Select(x => x.Movimiento), cierre, r);
            var esAportes = c.Nature == LineNature.Contribution;
            var revalorizacion = esAportes
                ? libro.Where(x => x.Fila.Concept == MemberAccountConcept.ContributionRevaluation && x.Fila.ValueDate >= desde && x.Fila.ValueDate <= cierre)
                    .Sum(x => x.Fila.Amount)
                : 0m;
            paraCertificar.Add(new CuentaParaElCertificadoTributario(c.Number, Clase(c), esAportes, proporciones,
                esAportes ? saldos.SaldoDeAportes : saldos.SaldoDeCaptacion, gmfs.Where(g => g.MemberAccountId == c.Id).Sum(g => g.GmfAmount),
                acumulados.Where(a => a.MemberAccountId == c.Id)
                    .Select(a => new AcumuladoTributario(a.PersonId, a.GrossInterest, a.InflationaryComponentAmount, a.WithholdingBase, a.WithholdingAmount)).ToList(),
                migrado.GetValueOrDefault(c.Id), revalorizacion));
        }
        var certificado = CertificadoTributarioDeCaptaciones.Armar(año, persona, paraCertificar, incluyeAportes);
        if (certificado.Vacio) return Falla(DocumentErrors.NadaQueCertificar($"la persona no tuvo productos de captación con la cooperativa en {año}"));

        var (agente, nit, _, _) = await AgenteAsync(ct);
        var porNumero = cuentas.ToDictionary(c => c.Number, StringComparer.Ordinal);
        var cubiertas = certificado.Captaciones.Concat(certificado.Aportes).Select(x => porNumero[x.Numero])
            .Select(c => Cubierta(c.Id, libros[c.Id], null)).ToList();
        var leyendas = new List<string> { CuerpoDeCertificadoTributarioDeCaptaciones.LeyendaDelComponente, CuerpoDeCertificadoTributarioDeCaptaciones.LeyendaDeProporcion };
        await TextoPropioAsync(p, leyendas, ct);
        var datos = Datos(cierre);
        datos["year"] = año;
        datos["accounts"] = certificado.Captaciones.Concat(certificado.Aportes).Select(x => new
        {
            number = x.Numero, share = x.Proporcion, balanceAtYearEnd = x.SaldoAl31DeDiciembre, interest = x.Rendimientos, withholding = x.Retencion,
            inflationaryComponent = x.ComponenteInflacionario, gmf = x.Gmf,
        }).ToList();
        datos["totalInterest"] = certificado.TotalRendimientos;
        datos["totalWithholding"] = certificado.TotalRetencion;
        datos["totalGmf"] = certificado.TotalGmf;
        return Result.Success(new DocumentoArmado([],
            new CuerpoDeCertificadoTributarioDeCaptaciones(año, agente, nit, certificado.Captaciones.Select(Impresa).ToList(),
                certificado.Aportes.Select(Impresa).ToList(), certificado.TotalSaldo, certificado.TotalRendimientos, certificado.TotalComponenteInflacionario,
                certificado.TotalRetencion, certificado.TotalGmf, incluyeAportes),
            cierre, null, null, null, null, VersionesDeReglas.Vigente, datos, leyendas)
        {
            Cuentas = cubiertas,
        });
    }

    private static CuentaTributariaImpresa Impresa(CuentaCertificadaTributaria c) => new(c.Numero, c.Clase, c.Proporcion, c.SaldoAl31DeDiciembre, c.Rendimientos,
        c.ComponenteInflacionario, c.Retencion, c.Gmf, c.RevalorizacionDelAño);

    /// <summary>El GMF del año que trajo la migración para la persona, por cuenta (E118: queda en la fila de preparación de la carga de la cuenta).</summary>
    private async Task<Dictionary<int, decimal>> GmfMigradoAsync(IReadOnlyList<MemberAccount> cuentas, int persona, short año, CancellationToken ct)
    {
        var migradas = cuentas.Where(c => c.MigrationLoadId is not null).ToList();
        if (migradas.Count == 0) return [];
        var cargas = migradas.Select(c => c.MigrationLoadId!.Value).Distinct().ToList();
        var filas = await (from f in db.MigrationTaxYearRows.AsNoTracking()
                           join t in db.MigrationLoadParts.AsNoTracking() on f.MigrationLoadPartId equals t.Id
                           where cargas.Contains(t.MigrationLoadId) && !t.IsDeleted && f.PersonId == persona && f.TaxYear == año && f.Concept == MigratedTaxConcept.Gmf
                           select new { t.MigrationLoadId, f.SourceAccountNumber, f.Amount }).ToListAsync(ct);
        var r = new Dictionary<int, decimal>();
        foreach (var c in migradas)
        {
            var numero = c.SourceAccountNumber ?? c.Number;
            var suma = filas.Where(f => f.MigrationLoadId == c.MigrationLoadId && (f.SourceAccountNumber == numero || f.SourceAccountNumber == c.Number))
                .Sum(f => f.Amount);
            if (suma != 0m) r[c.Id] = suma;
        }
        return r;
    }

    // ------------------------------------------------------------------------------------------ certificado de retención --

    private async Task<Result<DocumentoArmado>> CertificadoDeRetencionAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        var periodo = p.Periodo ?? throw new ArgumentException("El certificado de retención lleva período.", nameof(p));
        var persona = p.Titular.Id;
        var r = p.Emision;
        var filas = await (from a in db.WithholdingAccumulations.AsNoTracking()
                           join o in db.MemberAccountOperations.AsNoTracking() on a.OperationId equals o.Id
                           join c in db.MemberAccounts.AsNoTracking() on a.MemberAccountId equals c.Id
                           where a.PersonId == persona && o.ValueDate >= periodo.From && o.ValueDate <= periodo.To && o.RegisteredAt <= r
                           select new
                           {
                               a.MemberAccountId, a.DepositClass, a.InterestSettlementId, a.GrossInterest, a.InflationaryComponentAmount, a.WithholdingBase,
                               a.WithholdingAmount, a.ReversesAccumulationId, c.Number,
                           }).ToListAsync(ct);
        var liquidaciones = filas.Where(f => f.InterestSettlementId is not null).Select(f => f.InterestSettlementId!.Value).Distinct().ToList();
        var tarifas = await db.InterestSettlements.AsNoTracking().Where(l => liquidaciones.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.WithholdingRate, ct);
        var renglones = CertificadoDeRetencionDeCaptaciones.Agrupar(filas.Select(f => new RetencionDelPeriodo(ClaseDe(f.DepositClass),
            f.InterestSettlementId is { } id && tarifas.TryGetValue(id, out var t) && t > 0m ? t : null, f.GrossInterest, f.InflationaryComponentAmount,
            f.WithholdingBase, f.WithholdingAmount)));
        if (renglones.Count == 0)
            return Falla(DocumentErrors.NadaQueCertificar($"la persona no tuvo rendimientos financieros entre el {Fecha(periodo.From)} y el {Fecha(periodo.To)}"));

        var (agente, nit, direccion, ciudad) = await AgenteAsync(ct);
        var leyendas = new List<string>();
        var leyenda = await parametros.LeerAsync(ParametrosDeCartera.DocumentosLeyendaCertificadoRetencion, p.Hoy, ct: ct);
        if (leyenda.IsSuccess && !string.IsNullOrWhiteSpace(leyenda.Value.Texto)) leyendas.Add(leyenda.Value.Texto.Trim());
        await TextoPropioAsync(p, leyendas, ct);
        var cuentasCubiertas = filas.Select(f => f.MemberAccountId).Distinct().Order().Select(id => new CuentaDeDocumentoDeCartera(id, null, null, null)).ToList();
        var datos = Datos(periodo.To);
        datos["periodFrom"] = periodo.From;
        datos["periodTo"] = periodo.To;
        datos["base"] = renglones.Sum(x => x.BaseDeRetencion);
        datos["withholding"] = renglones.Sum(x => x.Retencion);
        return Result.Success(new DocumentoArmado([],
            new CuerpoDeCertificadoDeRetencion(agente, nit, direccion, ciudad, periodo.From, periodo.To,
                renglones.Select(x => new RenglonDeRetencionImpreso($"Rendimientos financieros — {x.Clase}", x.Tarifa is { } t2 ? Porcentaje(t2) : null,
                    x.Rendimientos, x.ComponenteInflacionario, x.BaseDeRetencion, x.Retencion)).ToList(),
                renglones.Sum(x => x.BaseDeRetencion), renglones.Sum(x => x.Retencion)),
            periodo.To, null, null, null, null, VersionesDeReglas.Vigente, datos, leyendas)
        {
            Cuentas = cuentasCubiertas,
        });
    }

    // ------------------------------------------------------------------------------------------------------------ título --

    private async Task<Result<DocumentoArmado>> TituloAsync(PedidoDeDocumentoDeCartera p, MemberAccount cuenta, CancellationToken ct)
    {
        if (cuenta.DepositClass != DepositClass.TermDeposit) return Falla(DocumentErrors.NadaQueCertificar($"la cuenta {cuenta.Number} no es un CDAT"));
        var version = p.VersionDeCondicionesId is { } vid
            ? await db.MemberAccountTermsVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vid && v.MemberAccountId == cuenta.Id, ct)
            : await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuenta.Id).OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync(ct);
        if (version is null) return Falla(DocumentErrors.NadaQueCertificar($"el CDAT {cuenta.Number} no tiene condiciones selladas"));
        var anterior = await (from x in db.IssuedDocumentAccounts.AsNoTracking()
                              join d in db.IssuedLendingDocuments.AsNoTracking() on x.IssuedDocumentId equals d.Id
                              where x.MemberAccountId == cuenta.Id && d.Kind == IssuedDocumentKind.TermDepositTitle && !d.IsDeleted
                                    && !db.IssuedDocumentEvents.Any(e => e.IssuedDocumentId == d.Id && e.Kind == IssuedDocumentEventKind.Voided)
                              orderby d.Id descending
                              select new { d.Id, d.Number }).FirstOrDefaultAsync(ct);
        var primera = await db.MemberAccountTermsVersions.AsNoTracking().Where(v => v.MemberAccountId == cuenta.Id).OrderBy(v => v.VersionNumber)
            .Select(v => v.StartDate ?? v.EffectiveValueDate).FirstAsync(ct);
        var (titulares, _, linea) = await DatosDeCuentaAsync(cuenta, version.StartDate ?? version.EffectiveValueDate, ct);
        var periodicidad = version.InterestPeriodicity ?? RepaymentPeriodicity.SinglePayment;
        decimal? nominal = periodicidad == RepaymentPeriodicity.SinglePayment ? null : EquivalenciaDeTasas.NominalAnual(version.AppliedAnnualRate, periodicidad);
        string? seguro = null;
        if (perfil is not null && await perfil.VigenteAsync(p.Hoy, null, ct) is { DepositInsuranceEnrolled: true })
        {
            var tope = valores is null ? null : await valores.LeerAsync(CodigosDeValorNacional.TopeSeguroDepositos, string.Empty, p.Hoy, null, ct);
            seguro = tope is { IsSuccess: true, Value.Valor: { } t }
                ? $"Depósito amparado por el seguro de depósitos de Fogacoop hasta {Pesos(t)} por ahorrador, según la norma vigente."
                : "Depósito amparado por el seguro de depósitos de Fogacoop según la norma vigente.";
        }
        var reglas = Reglas(version);
        var numeroDelTitulo = MemberAccountTermsVersion.NumeroDeTitulo(cuenta.Number, version.VersionNumber);
        var leyendas = new List<string> { CuerpoDeTituloDeCdat.LeyendaNoNegociable, CuerpoDeTituloDeCdat.LeyendaDeLegitimacion, CuerpoDeTituloDeCdat.LeyendaTributaria };
        await TextoPropioAsync(p, leyendas, ct);
        var datos = Datos(version.StartDate ?? version.EffectiveValueDate);
        datos["account"] = cuenta.Number;
        datos["termsVersion"] = version.VersionNumber;
        datos["principal"] = version.PrincipalAmount;
        datos["maturityDate"] = version.MaturityDate;
        datos["effectiveAnnualRate"] = version.AppliedAnnualRate;
        datos["previousTitle"] = anterior?.Number;
        return Result.Success(new DocumentoArmado([],
            new CuerpoDeTituloDeCdat(numeroDelTitulo, cuenta.Number, linea, version.VersionNumber, anterior?.Number, Causa(version.Cause), titulares,
                Manejo(cuenta.HandlingMode), version.PrincipalAmount, primera, version.StartDate ?? version.EffectiveValueDate, version.TermDays ?? 0,
                version.MaturityDate ?? version.EffectiveValueDate, version.AppliedAnnualRate, nominal, version.TableAnnualRate, version.NegotiatedSpread,
                Periodicidad(periodicidad), (version.InterestTiming ?? InterestTiming.Arrears) == InterestTiming.Arrears ? "vencido" : "anticipado",
                Destino(version.InterestDestination), reglas, seguro),
            version.StartDate ?? version.EffectiveValueDate, null, null, null, version.InputsSha256, version.RulesVersion, datos, leyendas)
        {
            Cuentas = [new CuentaDeDocumentoDeCartera(cuenta.Id, version.OperationId, null, version.PrincipalAmount)
            {
                TermsVersionId = version.Id, PreviousIssuedDocumentId = anterior?.Id,
            }],
            SufijoDeVersion = version.VersionNumber,
            BaseDelNumero = anterior is null ? null : BaseDelTitulo(anterior.Number),
        });
    }

    /// <summary>El número del título sin su sufijo de versión (<c>TIT-000123-2</c> → <c>TIT-000123</c>).</summary>
    public static string BaseDelTitulo(string numero)
    {
        var guion = numero.LastIndexOf('-');
        return guion > 0 && numero[(guion + 1)..].All(char.IsDigit) && numero.IndexOf('-') < guion ? numero[..guion] : numero;
    }

    private static IReadOnlyList<string> Reglas(MemberAccountTermsVersion v)
    {
        var reglas = new List<string>
        {
            "Instrucción al vencimiento: " + (v.MaturityInstruction switch
            {
                MaturityInstruction.Rollover => "prorrogar",
                MaturityInstruction.RenewWithNewTerms => "renovar con condiciones nuevas",
                MaturityInstruction.CancelAndPay => "cancelar y pagar",
                _ => "según el reglamento",
            }) + ".",
            v.AutoRollover == true
                ? $"Prórroga automática{(v.MaxRollovers is { } m ? $" hasta {m.ToString(Co)} veces" : string.Empty)}, con la tasa {ReglaDeTasa(v.RolloverRateRule)}."
                : "Sin prórroga automática.",
        };
        if (v.GraceBusinessDays is { } g)
            reglas.Add($"Días de gracia al vencimiento: {g.ToString(Co)} hábiles{(v.GraceAccrues == true ? ", con causación" : ", sin causación")}.");
        reglas.Add(v.EarlyRedemptionAllowed == true
            ? "Admite redención anticipada" + (v.EarlyRedemptionPenaltyMode is { } pm ? $" con penalidad por {(pm.ToString() == "Reliquidation" ? "reliquidación" : "descuento")}" : string.Empty) + "."
            : "No admite redención anticipada.");
        reglas.Add("Si no se prorroga: " + ((v.AfterMaturityTreatment ?? AfterMaturityTreatment.StopAccruing) == AfterMaturityTreatment.MoveToDemand
            ? "el saldo pasa a la cuenta de ahorro a la vista del titular."
            : "deja de causar intereses y queda a disposición del titular."));
        return reglas;
    }

    private static string ReglaDeTasa(RolloverRateRule? r) => r switch
    {
        RolloverRateRule.SameRate => "misma tasa",
        RolloverRateRule.CurrentTableRate => "de la tabla vigente al prorrogar",
        RolloverRateRule.Agreed => "pactada",
        _ => "de la tabla vigente",
    };

    // ------------------------------------------------------------------------------------------------ respuesta al juzgado --

    private async Task<Result<DocumentoArmado>> RespuestaAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        var r = p.Respuesta ?? throw new ArgumentException("La respuesta al juzgado lleva la respuesta.", nameof(p));
        var g = await db.Garnishments.AsNoTracking().FirstAsync(x => x.Id == r.GarnishmentId, ct);
        var leyendas = new List<string>();
        await TextoPropioAsync(p, leyendas, ct);
        var datos = Datos(r.Enviada);
        datos["order"] = g.OrderNumber;
        datos["process"] = g.ProcessNumber;
        datos["kind"] = r.Clase.ToString();
        datos["retained"] = r.Retenido;
        datos["consigned"] = r.Consignado;
        return Result.Success(new DocumentoArmado([],
            new CuerpoDeRespuestaAlJuzgado(ClaseDeRespuesta(r.Clase), $"{Autoridad(g.Authority)}: {g.CourtName}", g.OrderNumber,
                string.IsNullOrWhiteSpace(g.ProcessNumber) ? null : g.ProcessNumber, g.ClaimantName, g.OrderDate, r.Enviada, r.Texto, r.Retenido, r.Consignado),
            r.Enviada, null, null, null, null, VersionesDeReglas.Vigente, datos, leyendas));
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    /// <summary>Una fila del libro con su forma para la reconstrucción.</summary>
    private sealed record FilaDelLibro(MemberAccountTransaction Fila, MovimientoDeCuenta Movimiento);

    /// <summary>El libro de la cuenta con lo registrado hasta <paramref name="r"/>.</summary>
    private async Task<IReadOnlyList<FilaDelLibro>> LibroAsync(int cuenta, DateTime r, CancellationToken ct)
    {
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == cuenta && !t.IsDeleted && t.RegisteredAt <= r)
            .OrderBy(t => t.Id).ToListAsync(ct);
        return filas.Select(f => new FilaDelLibro(f, new MovimientoDeCuenta(f.Id, f.Kind, f.Concept, f.Amount, f.ValueDate, f.RegisteredAt, f.ReversesTransactionId)))
            .ToList();
    }

    private static CuentaDeDocumentoDeCartera Cubierta(int cuenta, IReadOnlyList<FilaDelLibro> libro, decimal? saldo)
    {
        var ultima = libro.OrderByDescending(f => f.Fila.RegisteredAt).ThenByDescending(f => f.Fila.Id).FirstOrDefault()?.Fila;
        return new CuentaDeDocumentoDeCartera(cuenta, ultima?.OperationId, ultima?.Id is > 0 ? ultima.Id : null, saldo);
    }

    private async Task<List<InterestSettlement>> LiquidacionesAsync(int cuenta, DateOnly desde, DateOnly hasta, DateTime r, CancellationToken ct)
    {
        var lista = await (from l in db.InterestSettlements.AsNoTracking()
                           join o in db.MemberAccountOperations.AsNoTracking() on l.OperationId equals o.Id
                           where l.MemberAccountId == cuenta && l.PeriodTo >= desde && l.PeriodTo <= hasta && o.RegisteredAt <= r
                           select l).ToListAsync(ct);
        var reversos = lista.Where(l => l.ReversedByOperationId is not null).Select(l => l.ReversedByOperationId!.Value).ToList();
        var reversadosAntes = await db.MemberAccountOperations.AsNoTracking().Where(o => reversos.Contains(o.Id) && o.RegisteredAt <= r).Select(o => o.Id).ToListAsync(ct);
        return lista.Where(l => l.ReversedByOperationId is null || !reversadosAntes.Contains(l.ReversedByOperationId.Value)).ToList();
    }

    private async Task<DisponibleImpreso> DisponibleAsync(MemberAccount cuenta, SaldosDeCuenta saldos, DateOnly fecha, CancellationToken ct)
    {
        var delTitular = cuenta.Nature == LineNature.Contribution ? saldos.SaldoDeAportes : saldos.SaldoDeCaptacion;
        var enCanje = (await movimientos.EnCanjeAsync([cuenta.Id], fecha, ct)).GetValueOrDefault(cuenta.Id);
        var minimo = 0m;
        if (cuenta.Nature == LineNature.Deposit && cuenta.Status != MemberAccountStatus.Opening)
            minimo = (await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, cuenta.CreditLineId, fecha, null, ct))?.MinBalance ?? 0m;
        var restricciones = (await LectorDeRestricciones.RestriccionesAsync(db, new Dictionary<int, decimal> { [cuenta.Id] = delTitular }, fecha, ct))
            .GetValueOrDefault(cuenta.Id) ?? RestriccionesDeCuenta.Ninguna;
        var disponible = DisponibleDeCuenta.Calcular(cuenta.Nature, delTitular, restricciones.Bloqueado, restricciones.Embargado, enCanje, minimo);
        return new DisponibleImpreso(delTitular, restricciones.Bloqueado, restricciones.Embargado, enCanje, minimo, disponible.Disponible);
    }

    /// <summary>Los titulares (titular y cotitulares vigentes a <paramref name="fecha"/> con su proporción), la oficina y la línea.</summary>
    private async Task<(IReadOnlyList<TitularImpreso> Titulares, string Oficina, string Linea)> DatosDeCuentaAsync(MemberAccount cuenta, DateOnly fecha,
        CancellationToken ct)
    {
        var proporciones = await ProporcionesAsync(cuenta, fecha, ct);
        var ids = proporciones.Select(x => x.Persona).ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var titulares = proporciones.Select((x, i) => personas.TryGetValue(x.Persona, out var per)
            ? new TitularImpreso(ReferenciasDeGobierno.Nombre(per), $"{per.IdType} {per.TaxId}", i == 0 ? "Titular" : "Cotitular",
                proporciones.Count > 1 ? x.Proporcion : null)
            : new TitularImpreso(string.Empty, string.Empty, i == 0 ? "Titular" : "Cotitular", null)).ToList();
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        return (titulares, oficina, linea);
    }

    private async Task<IReadOnlyList<(int Persona, decimal Proporcion)>> ProporcionesAsync(MemberAccount cuenta, DateOnly fecha, CancellationToken ct)
    {
        var cotitulares = await db.MemberAccountParties.AsNoTracking()
            .Where(x => x.MemberAccountId == cuenta.Id && !x.IsDeleted && x.Role == MemberAccountPartyRole.CoHolder && x.ValidFrom <= fecha
                        && (x.ValidTo == null || x.ValidTo >= fecha))
            .OrderBy(x => x.Id).Select(x => new { x.PersonId, x.Share }).ToListAsync(ct);
        return ProporcionesDeTitularidad.Calcular(cuenta.HolderPersonId, cotitulares.Select(c => (c.PersonId, c.Share)).ToList());
    }

    /// <summary>Las cuentas de captación y aportes en que la persona es titular o fue cotitular.</summary>
    public async Task<List<MemberAccount>> CuentasDeLaPersonaAsync(int persona, CancellationToken ct)
    {
        var comoCotitular = db.MemberAccountParties.AsNoTracking()
            .Where(x => x.PersonId == persona && x.Role == MemberAccountPartyRole.CoHolder && !x.IsDeleted).Select(x => x.MemberAccountId);
        return await db.MemberAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.Status != MemberAccountStatus.Opening && (a.HolderPersonId == persona || comoCotitular.Contains(a.Id)))
            .OrderBy(a => a.Id).ToListAsync(ct);
    }

    /// <summary>
    /// El texto propio de la cooperativa para la clase (<c>Documentos.TextosDeCaptaciones</c>, research E175): se suma como leyenda. Un JSON que no se lee
    /// no impide emitir.
    /// </summary>
    private async Task TextoPropioAsync(PedidoDeDocumentoDeCartera p, List<string> leyendas, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.DocumentosTextosDeCaptaciones, p.Hoy, ct: ct);
        if (leido.IsFailure || string.IsNullOrWhiteSpace(leido.Value.Texto)) return;
        if (TextoDeLaClase(leido.Value.Texto, p.Clase) is { } texto) leyendas.Add(texto);
    }

    /// <summary>El texto de <paramref name="clase"/> en el JSON <c>{ clase: texto }</c>; nulo si no lo trae o no se lee.</summary>
    public static string? TextoDeLaClase(string json, IssuedDocumentKind clase)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (string.Equals(p.Name, clase.ToString(), StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                    return p.Value.GetString()!.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private async Task<bool> SiNoAsync(string clave, DateOnly hoy, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, hoy, ct: ct);
        if (leido.IsFailure) return false;
        if (leido.Value.Valor is bool b) return b;
        return bool.TryParse(leido.Value.Texto?.Trim(), out var v) && v;
    }

    /// <summary>El agente retenedor: la cooperativa con su NIT, su dirección y su ciudad.</summary>
    private async Task<(string Nombre, string? Nit, string? Direccion, string? Ciudad)> AgenteAsync(CancellationToken ct)
    {
        var c = await db.Companies.AsNoTracking().Where(x => !x.IsDeleted).OrderBy(x => x.Id)
            .Select(x => new { x.Name, x.TaxId, x.TaxIdCheckDigit, x.Address, x.City }).FirstOrDefaultAsync(ct);
        if (c is null) return ("Cooperativa", null, null, null);
        var nit = string.IsNullOrWhiteSpace(c.TaxId) ? null : string.IsNullOrWhiteSpace(c.TaxIdCheckDigit) ? c.TaxId : $"{c.TaxId}-{c.TaxIdCheckDigit}";
        return (c.Name, nit, c.Address, c.City);
    }

    private static MemberAccount Cuenta(PedidoDeDocumentoDeCartera p) =>
        p.Cuenta ?? throw new ArgumentException($"La clase {p.Clase} se emite sobre una cuenta.", nameof(p));

    private static List<ConceptoDeCuentaImpreso> Conceptos(SaldosDeCuenta s) =>
        s.Saldos.Where(x => x.Value != 0m).OrderBy(x => (int)x.Key).Select(x => new ConceptoDeCuentaImpreso(Concepto(x.Key), x.Value)).ToList();

    public static string Concepto(MemberAccountConcept c) => c switch
    {
        MemberAccountConcept.Principal => "Capital",
        MemberAccountConcept.InterestPayable => "Interés por pagar",
        MemberAccountConcept.Withholding => "Retención en la fuente",
        MemberAccountConcept.Gmf => "GMF",
        MemberAccountConcept.ChargeReceivable => "Cargos por cobrar",
        MemberAccountConcept.Bonus => "Bonificación",
        _ => DocumentosDeAportes.Clase(c),
    };

    public static string Clase(MemberAccount c) => c.Nature == LineNature.Contribution ? "Aportes" : ClaseDe(c.DepositClass);

    public static string ClaseDe(DepositClass? clase) => clase switch
    {
        DepositClass.Demand => "Ahorro a la vista",
        DepositClass.Contractual => "Ahorro contractual",
        DepositClass.Permanent => "Ahorro permanente",
        DepositClass.TermDeposit => "CDAT",
        _ => "Aportes",
    };

    public static string TipoDeOperacion(MemberAccountOperationKind k) => k switch
    {
        MemberAccountOperationKind.OpeningBalance => "Saldo migrado",
        MemberAccountOperationKind.Deposit => "Consignación",
        MemberAccountOperationKind.Withdrawal => "Retiro",
        MemberAccountOperationKind.Transfer => "Traslado",
        MemberAccountOperationKind.Reversal => "Reverso",
        MemberAccountOperationKind.InterestSettlement => "Liquidación de intereses",
        MemberAccountOperationKind.TermDepositConstitution => "Constitución del CDAT",
        MemberAccountOperationKind.TermDepositInterestPayment => "Pago de intereses del CDAT",
        MemberAccountOperationKind.TermDepositRollover => "Prórroga del CDAT",
        MemberAccountOperationKind.TermDepositRenewal => "Renovación del CDAT",
        MemberAccountOperationKind.TermDepositRedemption => "Redención del CDAT",
        MemberAccountOperationKind.Cancellation => "Cancelación",
        MemberAccountOperationKind.Charge => "Cargo",
        MemberAccountOperationKind.ChargeWaiver => "Condonación de cargo",
        MemberAccountOperationKind.InternalOffset => "Cruce",
        MemberAccountOperationKind.DirectDebit => "Débito automático",
        MemberAccountOperationKind.ContributionRefund => "Devolución",
        MemberAccountOperationKind.AssemblyCredit => "Abono de asamblea",
        MemberAccountOperationKind.Reclassification => "Reclasificación",
        MemberAccountOperationKind.ContractualMaturity => "Vencimiento del contrato",
        MemberAccountOperationKind.ToUnclaimed => "Traslado a no reclamados",
        MemberAccountOperationKind.DeceasedDelivery => "Entrega por fallecimiento",
        MemberAccountOperationKind.GarnishmentConsignment => "Consignación por embargo",
        MemberAccountOperationKind.ContractualSetup => "Apertura del contrato",
        MemberAccountOperationKind.ContractualBreach => "Incumplimiento del contrato",
        MemberAccountOperationKind.ContractualBonus => "Bonificación",
        _ => k.ToString(),
    };

    private static string TipoSinOperacion(string clase) =>
        clase == ExtractosDeCuenta.CausadoDelMes ? "Interés causado del mes" : Enum.TryParse<MemberAccountTransactionKind>(clase, out var k) ? k.ToString() : clase;

    private static string? Canal(MemberAccountOperation o) =>
        o.CollectorCode is { Length: > 0 } c ? $"{c}{(o.PaymentMeansClass is { } m ? $" · {m}" : string.Empty)}" : o.PaymentMeansClass;

    private static string SinRetencion(string? motivo, int liquidaciones) => liquidaciones == 0
        ? "Sin liquidaciones de intereses en el período."
        : motivo switch
        {
            "BelowThreshold" => "No se retuvo: ningún día alcanzó el umbral.",
            "NonTaxpayer" => "No se retuvo: el titular no es contribuyente.",
            "SelfWithholder" => "No se retuvo: el titular es autorretenedor.",
            "ZeroRate" => "No se retuvo: tarifa cero.",
            "NoInterest" => "No se retuvo: sin intereses.",
            _ => "No se retuvo.",
        };

    private static string Politica(GmfExemptionPolicy p) => p switch
    {
        GmfExemptionPolicy.SingleMarkedAccount => "exención en la cuenta marcada",
        GmfExemptionPolicy.PerHolderLimit => "exención por titular",
        _ => "no aplica",
    };

    private static string Metodo(BalanceMethod m) => m switch
    {
        BalanceMethod.DailyBalance => "saldo diario",
        BalanceMethod.AverageBalance => "saldo promedio",
        BalanceMethod.MinimumBalance => "saldo mínimo",
        _ => m.ToString(),
    };

    private static string Estado(MemberAccountStatus s) => s switch
    {
        MemberAccountStatus.Opening => "En apertura",
        MemberAccountStatus.Active => "Activa",
        MemberAccountStatus.Inactive => "Inactiva",
        MemberAccountStatus.Matured => "Vencida",
        MemberAccountStatus.Closing => "En cancelación",
        MemberAccountStatus.Closed => "Cancelada",
        _ => s.ToString(),
    };

    private static string Manejo(HandlingMode m) => m == HandlingMode.Joint ? "conjunto" : "indistinto";

    private static string Motivo(HoldReason r) => r switch
    {
        HoldReason.Internal => "Bloqueo interno",
        HoldReason.LoanCollateral => "Garantía de un crédito",
        HoldReason.SignaturePending => "Firma pendiente",
        HoldReason.Deceased => "Titular fallecido",
        HoldReason.ScreeningMatch => "Coincidencia en listas",
        HoldReason.Garnishment => "Embargo judicial o coactivo",
        _ => "Otro",
    };

    private static string Causa(AccountTermsCause c) => c switch
    {
        AccountTermsCause.Constitution => "Constitución",
        AccountTermsCause.Rollover => "Prórroga",
        AccountTermsCause.Renewal => "Renovación",
        AccountTermsCause.Migration => "Saldo migrado",
        _ => c.ToString(),
    };

    private static string Destino(InterestDestination? d) => d switch
    {
        InterestDestination.ToAccount => "abono a una cuenta de ahorro del titular",
        InterestDestination.PaymentOrder => "giro al beneficiario",
        InterestDestination.Capitalize => "se capitalizan",
        _ => "al vencimiento",
    };

    private static string Periodicidad(RepaymentPeriodicity p) => p switch
    {
        RepaymentPeriodicity.SinglePayment => "al vencimiento",
        RepaymentPeriodicity.Monthly => "mensual",
        RepaymentPeriodicity.Bimonthly => "bimestral",
        RepaymentPeriodicity.Quarterly => "trimestral",
        RepaymentPeriodicity.SemiAnnual => "semestral",
        RepaymentPeriodicity.Annual => "anual",
        _ => p.ToString(),
    };

    private static string ClaseDeRespuesta(GarnishmentResponseKind k) => k switch
    {
        GarnishmentResponseKind.Acknowledgement => "Acuse de recibo del oficio",
        GarnishmentResponseKind.BalanceReport => "Informe de saldos",
        GarnishmentResponseKind.ContributionsNotGarnishable => "Aportes sociales inembargables",
        GarnishmentResponseKind.ConsignmentNotice => "Aviso de consignación",
        GarnishmentResponseKind.ReleaseAcknowledgement => "Acuse del desembargo",
        _ => k.ToString(),
    };

    private static string Autoridad(GarnishmentAuthority a) => a switch
    {
        GarnishmentAuthority.Court => "Juzgado",
        GarnishmentAuthority.TaxAuthority => "Autoridad tributaria",
        _ => "Entidad pública",
    };

    public const string LeyendaDelExtracto =
        "Extracto con la información registrada al momento de su emisión. Valores con signo: + aumenta el saldo del titular, − lo disminuye.";

    private static Dictionary<string, object?> Datos(DateOnly fecha) => new(StringComparer.Ordinal) { ["asOfDate"] = fecha };

    private static string Fecha(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Pesos(decimal v) => "$ " + v.ToString("#,0.00", Co);

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Co) + " %";

    private static Result<DocumentoArmado> Falla(Error e) => Result.Failure<DocumentoArmado>(e);
}
