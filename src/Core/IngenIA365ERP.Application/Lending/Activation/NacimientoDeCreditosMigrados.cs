using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Schedules;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>Lo que nació en la activación, sumado tanda por tanda (data-model §30.1, <c>CountsJson</c>). (nuevo)</summary>
public sealed class ConteoDeActivacion
{
    public int Creditos { get; set; }

    public int Castigados { get; set; }

    public int SaldosIniciales { get; set; }

    public int Codeudores { get; set; }

    public int Garantias { get; set; }

    public int Pagares { get; set; }

    public int VinculosDeFormaDePago { get; set; }

    public int Preferencias { get; set; }

    public int Cupos { get; set; }

    public int Bloqueos { get; set; }

    public int ReclamacionesAlSeguro { get; set; }

    public int Insolvencias { get; set; }

    public int Autorizaciones { get; set; }

    public int AvisosPrevios { get; set; }

    public int Reclamos { get; set; }

    public int Vinculados { get; set; }

    public int ObligacionesEnCero { get; set; }

    public decimal Capital { get; set; }

    /// <summary>Lo registrado a mano antes de activar que prevaleció sobre la carga (no nació de ella), por concepto.</summary>
    public Dictionary<string, int> Prevalecio { get; } = new(StringComparer.Ordinal);

    public object ParaJson() => new
    {
        loans = Creditos, writtenOff = Castigados, openingTransactions = SaldosIniciales, coDebtors = Codeudores, collaterals = Garantias,
        promissoryNotes = Pagares, payrollLinks = VinculosDeFormaDePago, prepaymentPreferences = Preferencias, creditLimits = Cupos, blocks = Bloqueos,
        insuranceClaims = ReclamacionesAlSeguro, insolvencyProceedings = Insolvencias, authorizations = Autorizaciones, priorNotices = AvisosPrevios,
        claims = Reclamos, relatedParties = Vinculados, zeroObligations = ObligacionesEnCero, principalTotal = Capital, manualPrevails = Prevalecio,
    };
}

/// <summary>
/// El nacimiento de los créditos de la carga en la activación (feature 013, US8, T0750; FR-140, T83, T84; data-model §29.3 «Al activar nace», §30.1),
/// <b>por tandas</b> que la activación guarda y suelta del contexto una a una (C60). Por cada crédito de <c>loans</c>:
/// <list type="bullet">
/// <item><c>LND_Loans</c> con origen <c>Migration</c>, <b>el número de origen</b> (no numera créditos migrados: el numerador los salta después, FR-057),
/// la oficina, la línea, el monto original, la carga y, si nació de una venta migrada, la identidad de la venta (factura, pago, documento y mensaje
/// de su fila de conciliación) con <c>SaleObligationStatus = Migrated</c>;</item>
/// <item>por <see cref="RegistroDeCartera"/>: su control con la marca en el <b>corte</b>; la operación <c>OpeningBalance</c> fechada al corte; sus
/// transacciones por concepto —el capital sin cuota; los intereses corrientes y de mora repartidos por cuota de la más antigua a la más nueva,
/// primero lo causado y después el suspenso (la cuota trae el total y la parte de saldos, la división); seguro, cobranza y otros por cuota; anticipos
/// y saldos a favor sin cuota— <b>sin mensajes contables</b> (FR-140: Contabilidad ya tenía esos saldos y el cuadre lo comprobó); las condiciones
/// selladas (versión 1, causa <c>Migration</c>) con lo pactado y la versión de la línea vigente al corte, con el conteo de reestructuraciones; y el plan
/// migrado (<c>IsMigratedPlan</c>) desde <c>installments</c> conservando el capital de cada cuota: el interés lo causa el motor desde el corte (D5);</item>
/// <item>después de guardar (con sus Id): codeudores y avalistas (<c>LND_LoanParties</c>), garantías (una fila por crédito: la misma referencia puede
/// respaldar varios), pagarés con canal e id externo sellados, el vínculo de forma de pago y pagaduría (<c>LND_LoanPayrollLinks</c>, origen
/// <c>Migration</c>, C44) y la preferencia de abono.</item>
/// </list>
/// Los de <c>charged-off</c> nacen <c>WrittenOff</c> con su saldo inicial (capital, intereses y otros, una sola cuota vencida al castigo) para que sus
/// recuperaciones funcionen desde K1; sin condiciones pactadas en la carga, toman las de la versión de la línea. Nada de esto emite hechos. (nuevo)
/// </summary>
public sealed class NacimientoDeCreditosMigrados(
    IApplicationDbContext db,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    IDateTimeService reloj,
    ICalendarioHabil? calendario = null)
{
    /// <summary>Créditos por tanda (C60).</summary>
    public const int TamañoDeTanda = 1000;

    /// <summary>El prefijo del comprobante de un saldo inicial.</summary>
    public const string PrefijoDeSaldoInicial = "SI-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Los números de la carga (vivos y castigados, en el orden de sus tandas y filas), partidos en tandas de <see cref="TamañoDeTanda"/>.</summary>
    public async Task<IReadOnlyList<IReadOnlyList<string>>> TandasAsync(MigrationLoad carga, CancellationToken ct)
    {
        var partes = await PartesVivasAsync(carga.Id, ct);
        var orden = partes.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
        var vivos = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId))
            .Select(r => new { r.MigrationLoadPartId, r.RowNumber, r.SourceLoanNumber }).ToListAsync(ct);
        var castigados = await db.MigrationChargedOffRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId))
            .Select(r => new { r.MigrationLoadPartId, r.RowNumber, r.SourceLoanNumber }).ToListAsync(ct);
        var numeros = vivos.OrderBy(r => orden[r.MigrationLoadPartId]).ThenBy(r => r.RowNumber).Select(r => r.SourceLoanNumber)
            .Concat(castigados.OrderBy(r => orden[r.MigrationLoadPartId]).ThenBy(r => r.RowNumber).Select(r => r.SourceLoanNumber))
            .ToList();
        return numeros.Chunk(TamañoDeTanda).Select(t => (IReadOnlyList<string>)t).ToList();
    }

    /// <summary>
    /// Hace nacer los créditos <paramref name="numeros"/> de la carga y los guarda en dos pasos dentro de la transacción de quien llama (lo que pide
    /// los Id del crédito y de su operación va en el segundo). No suelta el contexto: eso lo hace la activación entre tandas.
    /// </summary>
    public async Task NacerTandaAsync(MigrationLoad carga, IReadOnlyCollection<string> numeros, Actor actor, ConteoDeActivacion conteo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(conteo);
        if (numeros.Count == 0) return;
        var corte = carga.CutoffDate;
        var partes = await PartesVivasAsync(carga.Id, ct);
        var lista = numeros.ToList();

        var vivos = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber))
            .ToListAsync(ct);
        var castigados = await db.MigrationChargedOffRows.AsNoTracking()
            .Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber)).ToListAsync(ct);
        var cuotas = (await db.MigrationInstallmentRows.AsNoTracking()
                .Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var saldos = (await db.MigrationBalanceRows.AsNoTracking()
                .Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var reestructuras = (await db.MigrationRestructuringRows.AsNoTracking()
                .Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber)).ToListAsync(ct))
            .GroupBy(r => r.SourceLoanNumber, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var codeudores = (await db.MigrationCoDebtorRows.AsNoTracking()
                .Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var garantias = (await db.MigrationCollateralRows.AsNoTracking()
                .Where(r => partes.Contains(r.MigrationLoadPartId) && lista.Contains(r.SourceLoanNumber)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);

        // Catálogos en bloque: líneas (con su versión vigente al corte), oficinas, personas y la identidad de las ventas migradas.
        var codigosCastigados = castigados.Select(c => c.CreditLineCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lineasPorCodigo = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => codigosCastigados.Contains(l.Code))
            .ToDictionaryAsync(l => l.Code, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);
        var idsDeLinea = vivos.Select(v => v.CreditLineId ?? 0).Concat(lineasPorCodigo.Values).Where(i => i > 0).Distinct().ToList();
        var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(idsDeLinea, corte, ct: ct);
        var publicosDeOficina = vivos.Select(v => v.OfficePublicId).Concat(castigados.Select(c => c.OfficePublicId)).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => publicosDeOficina.Contains(b.PublicId))
            .ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var idsDePersona = vivos.Select(v => v.PersonId).Concat(castigados.Select(c => c.PersonId)).Distinct().ToList();
        var asociados = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => idsDePersona.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.IsAssociate, ct);
        var idsDeFila = vivos.Select(v => v.Id).ToList();
        var ventas = (await db.MigrationSaleReconciliations.AsNoTracking()
                .Where(r => r.MigrationLoadId == carga.Id && r.CheckVersion == carga.CurrentCheckVersion && r.MigrationLoanRowId != null
                            && idsDeFila.Contains(r.MigrationLoanRowId!.Value) && !r.IsDeleted)
                .ToListAsync(ct))
            .GroupBy(r => r.MigrationLoanRowId!.Value).ToDictionary(g => g.Key, g => g.First());

        var operaciones = await numerador.NumerosDeOperacionAsync(LoanOperationKind.OpeningBalance, PrefijoDeSaldoInicial, vivos.Count + castigados.Count, ct);
        var vencimientos = cuotas.SelectMany(g => g).Select(c => c.DueDate).Concat(castigados.Select(c => c.WrittenOffOn)).DefaultIfEmpty(corte).Max();
        var habiles = calendario is null ? null : await calendario.ObtenerAsync(corte, vencimientos.AddDays(30), DiasDeDescanso.SoloDomingo, ct: ct);
        var ahora = reloj.UtcNow;
        var nacidos = new List<(Loan Credito, LoanOperation Operacion, MigrationLoanRow? Fila)>(lista.Count);
        var i = 0;

        foreach (var fila in vivos)
        {
            var version = versiones.GetValueOrDefault(fila.CreditLineId ?? 0)
                ?? throw new InvalidOperationException($"La línea {fila.CreditLineCode} del crédito {fila.SourceLoanNumber} no tiene versión vigente al corte.");
            var venta = ventas.GetValueOrDefault(fila.Id);
            var credito = new Loan
            {
                Number = fila.SourceLoanNumber,
                DebtorPersonId = fila.PersonId,
                DebtorKind = asociados.GetValueOrDefault(fila.PersonId) ? DebtorKind.Associate : DebtorKind.Customer,
                BranchId = Oficina(oficinas, fila.OfficePublicId, fila.SourceLoanNumber),
                CreditLineId = version.Version.CreditLineId,
                Origin = LoanOrigin.Migration,
                Status = LoanStatus.Active,
                ApprovedAmount = fila.OriginalAmount,
                OpenedOn = corte,
                SourceInvoiceNumber = fila.InvoiceNumber is { } f ? Recortar(f, 30) : null,
                SourceSalesDocumentPublicId = venta?.SaleDocumentPublicId,
                SourcePaymentPublicId = venta?.CreditPaymentPublicId,
                SourceMessagePublicId = venta?.MessagePublicId,
                SaleObligationStatus = venta is null ? null : SaleObligationStatus.Migrated,
                MigrationLoadId = carga.Id,
            };
            var susCuotas = cuotas[fila.SourceLoanNumber].OrderBy(c => c.InstallmentNumber).ToList();
            var susSaldos = saldos[fila.SourceLoanNumber].GroupBy(s => s.Concept).ToDictionary(g => g.Key, g => g.Sum(s => s.Amount));
            var condiciones = Condiciones(credito, fila.TermsJson, version, reestructuras.GetValueOrDefault(fila.SourceLoanNumber), corte, susCuotas);
            var transacciones = Transacciones(susSaldos, susCuotas);
            var operacion = Nacer(credito, operaciones[i++], condiciones, susCuotas.Select(c => new CuotaMigrada(c.InstallmentNumber, c.DueDate, c.Principal)).ToList(),
                transacciones, carga, actor, ahora, habiles, fila.RowNumber, fila.TermsJson);
            nacidos.Add((credito, operacion, fila));
            conteo.SaldosIniciales += transacciones.Count;
            conteo.Creditos++;
            conteo.Capital += susSaldos.GetValueOrDefault(LoanConcept.Principal);
        }

        foreach (var fila in castigados)
        {
            var lineaId = lineasPorCodigo.GetValueOrDefault(fila.CreditLineCode);
            var version = versiones.GetValueOrDefault(lineaId)
                ?? throw new InvalidOperationException($"La línea {fila.CreditLineCode} del crédito castigado {fila.SourceLoanNumber} no tiene versión vigente al corte.");
            var credito = new Loan
            {
                Number = fila.SourceLoanNumber,
                DebtorPersonId = fila.PersonId,
                DebtorKind = asociados.GetValueOrDefault(fila.PersonId) ? DebtorKind.Associate : DebtorKind.Customer,
                BranchId = Oficina(oficinas, fila.OfficePublicId, fila.SourceLoanNumber),
                CreditLineId = lineaId,
                Origin = LoanOrigin.Migration,
                Status = LoanStatus.WrittenOff,
                ApprovedAmount = fila.PrincipalBalance,
                OpenedOn = corte,
                WrittenOffOn = fila.WrittenOffOn,
                MigrationLoadId = carga.Id,
            };
            // Una sola cuota, vencida al castigo: el saldo castigado entero es exigible.
            var cuota = new MigrationInstallmentRow
            {
                SourceLoanNumber = fila.SourceLoanNumber, InstallmentNumber = 1, DueDate = fila.WrittenOffOn, Principal = fila.PrincipalBalance,
                CurrentInterest = fila.InterestBalance, OtherCharges = fila.OtherBalance,
            };
            var susSaldos = new Dictionary<LoanConcept, decimal>
            {
                [LoanConcept.Principal] = fila.PrincipalBalance,
                [LoanConcept.CurrentInterest] = fila.InterestBalance,
                [LoanConcept.OtherCharge] = fila.OtherBalance,
            };
            var condiciones = Condiciones(credito, "{}", version, null, corte, [cuota]);
            var transacciones = Transacciones(susSaldos, [cuota]);
            var operacion = Nacer(credito, operaciones[i++], condiciones, [new CuotaMigrada(1, fila.WrittenOffOn, fila.PrincipalBalance)],
                transacciones, carga, actor, ahora, habiles, fila.RowNumber, "{}");
            nacidos.Add((credito, operacion, null));
            conteo.SaldosIniciales += transacciones.Count;
            conteo.Castigados++;
        }
        await db.SaveChangesAsync(ct);

        // Lo que pide los Id del crédito y de su operación de saldo inicial.
        foreach (var (credito, operacion, fila) in nacidos)
        {
            foreach (var c in codeudores[credito.Number])
            {
                db.LoanParties.Add(LoanParty.DeLaSolicitud(credito.Id, c.CoDebtorPersonId, c.Role, c.Since ?? fila?.DisbursementDate ?? corte, null));
                conteo.Codeudores++;
            }
            foreach (var g in garantias[credito.Number]) Garantia(credito, g, conteo);
            if (fila is null) continue;

            var conPagaduria = fila.RepaymentChannel is RepaymentChannel.OwnPayroll or RepaymentChannel.PaymasterDeduction;
            db.LoanPayrollLinks.Add(new LoanPayrollLink
            {
                LoanId = credito.Id,
                RepaymentChannel = fila.RepaymentChannel,
                PaymasterPersonId = !conPagaduria ? null : fila.RepaymentChannel == RepaymentChannel.OwnPayroll ? credito.DebtorPersonId : fila.PaymasterPersonId,
                IsOwnPayroll = fila.RepaymentChannel == RepaymentChannel.OwnPayroll,
                EmployeeIdAtPaymaster = fila.EmployeeIdAtPaymaster,
                LibranzaDate = fila.LibranzaDate,
                Origin = LoanPayrollLinkOrigin.Migration,
                OperationId = operacion.Id,
                ValidFrom = fila.DisbursementDate,
            });
            conteo.VinculosDeFormaDePago++;
            if (fila.PrepaymentChoice is { } eleccion)
            {
                db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(credito.DebtorPersonId, credito.Id, null, eleccion, 1, operacion.RegisteredAt,
                    PreferenceOrigin.Migration, actor.UserId, operationId: operacion.Id));
                conteo.Preferencias++;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Las transacciones de saldo inicial de un crédito (FR-140): el capital sin cuota; el interés corriente y el de mora de cada cuota partidos entre
    /// lo causado y el suspenso —la carga trae el total por cuota y el total de cada parte, así que lo causado se asigna de la cuota más antigua a la
    /// más nueva y el resto de cada cuota es suspenso—; seguro por cuota; cobranza y otros cargos por cuota (primero la cobranza); anticipos y saldos
    /// a favor sin cuota. Lo que la carga declara en saldos y las cuotas no reparten (no debería pasar: el cuadre lo valida) queda sin cuota.
    /// </summary>
    public static IReadOnlyList<(LoanConcept Concepto, int? Cuota, decimal Monto)> Transacciones(
        IReadOnlyDictionary<LoanConcept, decimal> saldos, IReadOnlyList<MigrationInstallmentRow> cuotas)
    {
        var filas = new List<(LoanConcept, int?, decimal)>();
        void Agregar(LoanConcept c, int? cuota, decimal monto)
        {
            if (monto != 0m) filas.Add((c, cuota, monto));
        }

        Agregar(LoanConcept.Principal, null, saldos.GetValueOrDefault(LoanConcept.Principal));
        Repartir(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense, c => c.CurrentInterest);
        Repartir(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense, c => c.DefaultInterest);
        Repartir(LoanConcept.Insurance, null, c => c.Insurance);
        Repartir(LoanConcept.CollectionFee, LoanConcept.OtherCharge, c => c.OtherCharges);
        Agregar(LoanConcept.Advance, null, saldos.GetValueOrDefault(LoanConcept.Advance));
        Agregar(LoanConcept.CreditBalance, null, saldos.GetValueOrDefault(LoanConcept.CreditBalance));
        return filas;

        void Repartir(LoanConcept primero, LoanConcept? resto, Func<MigrationInstallmentRow, decimal> deLaCuota)
        {
            var paraElPrimero = saldos.GetValueOrDefault(primero);
            var paraElResto = resto is { } r ? saldos.GetValueOrDefault(r) : 0m;
            foreach (var cuota in cuotas.OrderBy(c => c.InstallmentNumber))
            {
                var total = deLaCuota(cuota);
                var delPrimero = Math.Min(total, Math.Max(0m, paraElPrimero));
                paraElPrimero -= delPrimero;
                Agregar(primero, cuota.InstallmentNumber, delPrimero);
                if (resto is { } otro)
                {
                    var delResto = total - delPrimero;
                    paraElResto -= delResto;
                    Agregar(otro, cuota.InstallmentNumber, delResto);
                }
            }
            Agregar(primero, null, paraElPrimero);
            if (resto is { } sobrante) Agregar(sobrante, null, paraElResto);
        }
    }

    // --------------------------------------------------------------------------------------------------------- armado --

    private LoanOperation Nacer(Loan credito, string numeroDeOperacion, LoanTermsVersion condiciones, IReadOnlyList<CuotaMigrada> cuotasDelPlan,
        IReadOnlyList<(LoanConcept Concepto, int? Cuota, decimal Monto)> transacciones, MigrationLoad carga, Actor actor, DateTime ahora,
        Domain.Common.Calendar.CalendarioHabil? habiles, int filaDeLaCarga, string terminos)
    {
        var corte = carga.CutoffDate;
        var capital = transacciones.Where(t => t.Concepto == LoanConcept.Principal).Sum(t => t.Monto);
        db.Loans.Add(credito);
        var control = registro.IniciarControl(credito, corte, ahora);
        var entradas = JsonSerializer.Serialize(new
        {
            load = carga.PublicId, loadNumber = carga.Number, cutoff = corte, loan = credito.Number, row = filaDeLaCarga,
            balances = transacciones.Select(t => new { concept = t.Concepto.ToString(), installment = t.Cuota, amount = t.Monto }),
        }, Json);
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new LoanOperation
        {
            Loan = credito,
            Kind = LoanOperationKind.OpeningBalance,
            Number = numeroDeOperacion,
            Sequence = sello.Sequence,
            ValueDate = corte,
            AccountingDate = corte,
            RegisteredAt = sello.RegisteredAt,
            Amount = capital,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            MigrationLoadId = carga.Id,
            Reason = string.Create(CultureInfo.InvariantCulture, $"Saldo inicial de la carga de migración {carga.Number} al corte {corte:yyyy-MM-dd}"),
            Payload = entradas,
            InputsSha256 = TramoDeDesembolso.Sha256(entradas),
            RulesVersion = condiciones.RulesVersion,
        });
        registro.RegistrarTransacciones(transacciones.Select(t => new LoanTransaction
        {
            Loan = credito,
            Operation = operacion,
            Kind = LoanTransactionKind.OpeningBalance,
            Concept = t.Concepto,
            InstallmentNumber = t.Cuota,
            Amount = t.Monto,
            ValueDate = corte,
            AccountingDate = corte,
            RegisteredAt = operacion.RegisteredAt,
        }).ToList());

        var sellada = Sellar(condiciones, operacion);
        registro.RegistrarVersionDeCondiciones(sellada);

        // El plan migrado (D5): el capital de cada cuota como vino; el interés de las cuotas que vencen después del corte, el que causará el motor.
        var plan = GeneradorDePlan.DesdeMigracion(new EntradaDePlanMigrado(corte, capital, cuotasDelPlan, sellada.Periodicity, sellada.AmortizationSystem,
            sellada.AnnualRate ?? 0m, sellada.DayCount, sellada.RateEquivalence, Engine.CargadorDeReconstruccion.Redondeo(sellada)));
        var version = new RepaymentScheduleVersion
        {
            Loan = credito,
            VersionNumber = 1,
            TermsVersion = sellada,
            Operation = operacion,
            EffectiveValueDate = corte,
            RegisteredAt = operacion.RegisteredAt,
            PrincipalBase = capital,
            FirstRegeneratedInstallment = plan.Cuotas.Count == 0 ? 1 : plan.Cuotas[0].Numero,
            InstallmentCount = plan.Cuotas.Count,
            IsMigratedPlan = true,
            RulesVersion = plan.RulesVersion,
            InputsSha256 = TramoDeDesembolso.Sha256(terminos + entradas),
        };
        registro.RegistrarPlan(version, plan.Cuotas.Select(c => new RepaymentInstallment
        {
            ScheduleVersion = version,
            InstallmentNumber = c.Numero,
            PeriodStart = c.Desde,
            DueDate = c.Vencimiento,
            ExigibleDate = habiles?.SiguienteHabil(c.Vencimiento) ?? c.Vencimiento,
            OpeningPrincipal = c.SaldoInicial,
            Principal = c.Capital,
            Interest = c.Interes,
            GraceInterest = c.InteresDeGracia,
            Insurance = c.Seguro,
            OtherCharges = 0m,
            Total = c.Total,
            ClosingPrincipal = c.SaldoFinal,
            IsExtraordinary = false,
            IsGraceInstallment = c.SoloInteres,
        }).ToList());
        return operacion;
    }

    /// <summary>
    /// Las condiciones pactadas del crédito (sin crédito ni operación todavía): las columnas de la plantilla (<c>TermsJson</c>) y, donde no vienen,
    /// las de la versión de la línea vigente al corte. Una tasa variable se reajusta en el primer ciclo (<c>NextResetDate</c> = corte + 1).
    /// </summary>
    private static LoanTermsVersion Condiciones(Loan credito, string terminos, VersionDeLineaLeida linea, MigrationRestructuringRow? reestructura,
        DateOnly corte, IReadOnlyList<MigrationInstallmentRow> cuotas)
    {
        var v = linea.Version;
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(terminos) ? "{}" : terminos);
        var t = doc.RootElement;
        var claseDeTasa = Enumeracion(t, "rateKind", v.RateKind);
        var periodicidad = Enumeracion(t, "periodicity", v.DefaultPeriodicity);
        var plazo = Entero(t, "term") ?? Math.Max(1, cuotas.Count);
        var reajuste = Entero(t, "resetMonths") is { } meses && Enum.IsDefined(typeof(RepaymentPeriodicity), meses * (int)RepaymentPeriodicity.Monthly)
            ? (RepaymentPeriodicity?)(RepaymentPeriodicity)(meses * (int)RepaymentPeriodicity.Monthly)
            : v.ResetPeriodicity;
        var seguros = linea.Seguros.Select(s => new
        {
            code = s.Code, @base = s.Base.ToString(), rate = s.Rate, periodicity = s.ChargePeriodicity.ToString(), computes = s.Computa(),
        });
        return new LoanTermsVersion
        {
            Loan = credito,
            VersionNumber = 1,
            EffectiveValueDate = corte,
            Cause = TermsVersionCause.Migration,
            // K6b (T1053, T1055): un crédito de vivienda migrado sella las reglas de vivienda; los demás, la vigente.
            RulesVersion = VersionesDeReglas.ParaCreditoNuevo(string.Equals(v.ModalityCode, ReglasDeLineaDeCredito.ModalidadDeVivienda, StringComparison.OrdinalIgnoreCase)),
            CreditLineVersionId = v.Id,
            ModalityCode = v.ModalityCode,
            SubsegmentCode = v.SubsegmentCode,
            ForEmployees = v.ForEmployees,
            InterestTaxTreatment = v.InterestTaxTreatment,
            RateKind = claseDeTasa,
            AnnualRate = Decimal(t, "annualRate") ?? v.AnnualRate,
            IndexCode = Texto(t, "index") ?? v.IndexCode,
            IndexKey = Texto(t, "indexKey") ?? v.IndexKey,
            Margin = Decimal(t, "margin") ?? v.Margin,
            RateFloor = Decimal(t, "floorRate") ?? v.RateFloor,
            RateCeiling = Decimal(t, "ceilingRate") ?? v.RateCeiling,
            ResetPeriodicity = claseDeTasa == RateKind.Variable ? reajuste : null,
            NextResetDate = claseDeTasa == RateKind.Variable ? corte.AddDays(1) : null,
            DefaultRateKind = Enumeracion(t, "defaultRateKind", v.DefaultRateKind),
            DefaultRate = Decimal(t, "defaultRate") ?? v.DefaultRate,
            DefaultRateFactor = Decimal(t, "defaultFactor") ?? v.DefaultRateFactor,
            DefaultOnInterestAllowed = false,
            DayCount = Enumeracion(t, "dayCount", v.DayCount),
            RateEquivalence = Enumeracion(t, "equivalence", v.RateEquivalence),
            AmortizationSystem = Enumeracion(t, "amortizationSystem", v.DefaultAmortizationSystem),
            Periodicity = periodicidad,
            Installments = plazo,
            TermMonths = Math.Max(1, (int)Math.Ceiling(plazo * (decimal)(int)periodicidad / (int)RepaymentPeriodicity.Monthly)),
            FirstDueDate = cuotas.Count == 0 ? corte : cuotas.Min(c => c.DueDate),
            PaymentDay = v.PaymentDay,
            RoundingUnit = Decimal(t, "roundingUnit") ?? v.RoundingUnit,
            RoundingMode = Enumeracion(t, "roundingMode", v.RoundingMode),
            RoundingMoment = Enumeracion(t, "roundingMoment", v.RoundingMoment),
            DenominationUnit = v.DenominationUnit,
            AllocationOrder = Enumeracion(t, "allocationOrder", v.AllocationOrder),
            ConceptPriority = v.ConceptPriority,
            SuspenseWithItsConcept = v.SuspenseWithItsConcept,
            AllowsSpecificInstallmentOutOfOrder = v.AllowsSpecificInstallmentOutOfOrder,
            NoteRecalculation = v.NoteRecalculation,
            Insurances = JsonSerializer.Serialize(seguros, Json),
            RestructuringCount = reestructura?.RestructuringCount ?? 0,
            RestructuredOn = reestructura?.LastRestructuredOn,
            InputsSha256 = TramoDeDesembolso.Sha256(terminos),
        };
    }

    /// <summary>Las condiciones con su operación (la versión 1 la produce el saldo inicial); <c>LoanTermsVersion</c> es de sólo inserción.</summary>
    private static LoanTermsVersion Sellar(LoanTermsVersion c, LoanOperation operacion) => new()
    {
        Loan = c.Loan, VersionNumber = c.VersionNumber, Operation = operacion, EffectiveValueDate = c.EffectiveValueDate, RegisteredAt = operacion.RegisteredAt,
        Cause = c.Cause, RulesVersion = c.RulesVersion, CreditLineVersionId = c.CreditLineVersionId, ModalityCode = c.ModalityCode, SubsegmentCode = c.SubsegmentCode,
        ForEmployees = c.ForEmployees, InterestTaxTreatment = c.InterestTaxTreatment, RateKind = c.RateKind, AnnualRate = c.AnnualRate, IndexCode = c.IndexCode,
        IndexKey = c.IndexKey, Margin = c.Margin, RateFloor = c.RateFloor, RateCeiling = c.RateCeiling, ResetPeriodicity = c.ResetPeriodicity,
        NextResetDate = c.NextResetDate, DefaultRateKind = c.DefaultRateKind, DefaultRate = c.DefaultRate, DefaultRateFactor = c.DefaultRateFactor,
        DefaultOnInterestAllowed = c.DefaultOnInterestAllowed, DayCount = c.DayCount, RateEquivalence = c.RateEquivalence, AmortizationSystem = c.AmortizationSystem,
        Periodicity = c.Periodicity, Installments = c.Installments, TermMonths = c.TermMonths, FirstDueDate = c.FirstDueDate, PaymentDay = c.PaymentDay,
        RoundingUnit = c.RoundingUnit, RoundingMode = c.RoundingMode, RoundingMoment = c.RoundingMoment, AllocationOrder = c.AllocationOrder,
        ConceptPriority = c.ConceptPriority, SuspenseWithItsConcept = c.SuspenseWithItsConcept, AllowsSpecificInstallmentOutOfOrder = c.AllowsSpecificInstallmentOutOfOrder,
        NoteRecalculation = c.NoteRecalculation, Insurances = c.Insurances, RestructuringCount = c.RestructuringCount, RestructuredOn = c.RestructuredOn,
        InputsSha256 = c.InputsSha256, DenominationUnit = c.DenominationUnit,
    };

    /// <summary>Una garantía de la carga: un pagaré nace como <c>LND_PromissoryNotes</c> con su canal e id externo; las demás, como garantía del crédito.</summary>
    private void Garantia(Loan credito, MigrationCollateralRow g, ConteoDeActivacion conteo)
    {
        if (g.CollateralKind == CollateralKind.PromissoryNote)
        {
            var clase = g.PromissoryNoteKind ?? PromissoryNoteKind.Physical;
            db.PromissoryNotes.Add(new PromissoryNote
            {
                Kind = clase,
                LoanId = credito.Id,
                Number = g.Reference,
                IssuedOn = g.ConstitutedOn,
                Status = clase == PromissoryNoteKind.Dematerialized ? PromissoryNoteStatus.InCustody : PromissoryNoteStatus.Signed,
                CustodianChannelCode = g.CustodianChannelCode,
                ExternalNoteId = g.ExternalNoteId,
            });
            conteo.Pagares++;
            return;
        }
        db.Collaterals.Add(new Collateral
        {
            Kind = g.CollateralKind,
            OwnerPersonId = g.GuaranteeFundPersonId ?? credito.DebtorPersonId,
            LoanId = credito.Id,
            Description = g.Description ?? g.Reference,
            Value = g.Value,
            AdmissibleValue = g.Value,
            ValuationDate = g.AppraisalDate,
            PolicyNumber = g.PolicyNumber,
            PolicyInsurerPersonId = g.InsurerPersonId,
            PolicyExpiresOn = g.PolicyExpiresOn,
            RegistrationReference = g.Reference,
            FundCoverage = g.CoveragePercentage,
            FundFee = g.FeePercentage,
            Status = g.Status,
            ConstitutedOn = g.ConstitutedOn,
            ValidUntil = g.ValidUntil,
        });
        conteo.Garantias++;
    }

    private async Task<List<int>> PartesVivasAsync(int carga, CancellationToken ct) =>
        await db.MigrationLoadParts.AsNoTracking().Where(p => p.MigrationLoadId == carga && p.Status == MigrationPartStatus.Imported && !p.IsDeleted)
            .OrderBy(p => p.PartKey).ThenBy(p => p.BatchNumber).Select(p => p.Id).ToListAsync(ct);

    private static int Oficina(IReadOnlyDictionary<Guid, int> oficinas, Guid publicId, string numero) =>
        oficinas.TryGetValue(publicId, out var id) ? id : throw new InvalidOperationException($"La oficina del crédito {numero} no existe.");

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];

    private static string? Texto(JsonElement t, string nombre) =>
        t.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Decimal(JsonElement t, string nombre) =>
        t.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static int? Entero(JsonElement t, string nombre) =>
        t.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static TEnum Enumeracion<TEnum>(JsonElement t, string nombre, TEnum porDefecto) where TEnum : struct, Enum =>
        Texto(t, nombre) is { } texto && Enum.TryParse<TEnum>(texto, ignoreCase: true, out var valor) ? valor : porDefecto;
}
