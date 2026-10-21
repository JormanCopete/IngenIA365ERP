using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Activation;

/// <summary>Lo que nació de una carga al activar: cuentas, transacciones de saldo inicial, partes, cuotas, marcas del GMF y acumulados tributarios. (nuevo)</summary>
public sealed record CuentasNacidas(int Cuentas, int TransaccionesIniciales, int Partes, int Cuotas, int Exentas, int Acumulados, int SoportePendiente)
{
    public object ParaJson() => new
    {
        accounts = Cuentas, openingTransactions = TransaccionesIniciales, parties = Partes, installments = Cuotas, gmfExemptions = Exentas,
        taxYearAccumulations = Acumulados, supportPending = SoportePendiente,
    };
}

/// <summary>
/// El nacimiento de las cuentas de una carga de aportes o captaciones al activar (feature 014, US9, T154, T155; data-model §7.1; FR-114; US9-4), dentro de
/// la transacción de la activación y por tandas de cuentas:
/// <list type="bullet">
/// <item>cada cuenta nace <c>Origin = Migration</c> con el número que conserva (o, en una de aportes sin número, el consecutivo de su línea, que antes se
/// lleva por encima de los números migrados de cada línea, data-model §0.4), su apertura real, activa desde la activación (o inactiva desde el corte), su
/// titularidad (titular, cotitulares, firmantes, representante con el soporte pendiente —la cuenta queda con la actualización de datos pendiente— y
/// beneficiarios) y su control con la marca de causación <b>en el corte</b>, la última liquidación y el último movimiento del titular;</item>
/// <item>una operación <c>OpeningBalance</c> con <b>una transacción por concepto</b> fechada al corte (valor y contable), <b>sin hechos contables</b>:
/// Contabilidad ya tiene esos saldos (FR-114). Lo que estaba en canje va en una segunda operación de saldo inicial con su canje hasta los días hábiles del
/// cheque, para que no cuente como disponible;</item>
/// <item>las cuotas atrasadas nacen exigibles (generadas por la operación de saldo inicial) y lo pagado de cada una es la parte del saldo inicial de su
/// concepto que la nombra; el no descontado por Nómina o la pagaduría queda con su motivo;</item>
/// <item>la marca de exención del GMF y lo exento ya usado en el mes del corte, y el acumulado tributario del año (rendimientos, componente
/// inflacionario, retención), con <c>Origin = Migration</c>. El GMF del año queda en la carga para el certificado (research E118);</item>
/// <item>US6 (pendiente de la fase 10, research E148): cada CDAT nace activo con su apertura en la constitución del título, su versión 1 de condiciones
/// (<c>Cause = Migration</c>) sellada en su operación de saldo inicial con el plan que regenera <see cref="PlanDeCdat"/> desde su inicio vigente (las
/// condiciones de vencimiento las copia de la versión de la línea al corte), el destino de los intereses (la cuenta a la vista de la carga o viva, o el giro a
/// la cuenta bancaria registrada del titular) y el saldo inicial de capital e interés por pagar con su versión y el período en curso. Los CDAT nacen al final
/// (la cuenta destino ya existe).</item>
/// </list>
/// Todo lo del libro lo escribe <see cref="RegistroDeCuentas"/>; los números, <see cref="NumeradorDeCartera"/>. No emite hechos. (nuevo)
/// </summary>
public sealed class NacimientoDeCuentasMigradas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    ReglasDeMovimientos movimientos,
    ReglasDeParametrosDeCartera parametros,
    IActorActual actorActual,
    IDateTimeService reloj,
    IngenIA365ERP.Application.Common.Calendar.ICalendarioHabil? calendario = null,
    Contractual.LectorDeContractual? contractual = null)
{
    public const string PrefijoSaldoInicial = "SIN-";

    /// <summary>Cuántas cuentas nacen por guardado (cada tanda son cuatro guardados).</summary>
    public const int Tanda = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CuentasNacidas> NacerAsync(MigrationLoad carga, FilasDeLaCarga filas, DateOnly activacion, string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(filas);
        var corte = carga.CutoffDate;
        var actor = await actorActual.ObtenerAsync(ct);
        var cuentas = filas.Cuentas;
        if (cuentas.Count == 0) return new CuentasNacidas(0, 0, 0, 0, 0, 0, 0);

        var lineas = await LineasDeLaCarga.LeerAsync(db, carga.Nature, corte, cuentas.Select(c => c.LineaCodigo).Distinct().ToList(), ct);
        var oficinas = cuentas.Select(c => c.OficinaPublicId).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var ids = cuentas.Select(c => c.TitularId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var mayoria = await LectorDeCuentas.MayoriaDeEdadAsync(parametros, corte, ct);
        var politica = await LectorDeCuentas.PoliticaDeGmfAsync(parametros, corte, ct);
        var canjeHasta = cuentas.Any(c => c.Naturaleza == LineNature.Deposit)
            ? await movimientos.CanjeHastaAsync(ClaseDeMedioDeRecaudo.Check, activacion, ct)
            : null;

        var ahorroPorNumero = filas.CuentasDeAhorro.ToDictionary(r => r.Fila.SourceAccountNumber, r => r.Fila, StringComparer.Ordinal);
        var aportesPorTitular = filas.CuentasDeAportes.ToDictionary(r => r.Fila.PersonId, r => r.Fila);
        var partesPor = filas.PartesDeCuenta.GroupBy(p => p.Fila.SourceAccountNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Fila).ToList(), StringComparer.Ordinal);
        var cuotasPor = filas.Cuotas.GroupBy(q => q.Fila.SourceAccountNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Fila).OrderBy(q => q.DueDate).ToList(), StringComparer.Ordinal);
        var exencionPor = filas.Exenciones.ToDictionary(e => e.Fila.SourceAccountNumber, e => e.Fila, StringComparer.Ordinal);
        var tributarioPor = filas.Tributario.GroupBy(t => t.Fila.SourceAccountNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Fila).ToList(), StringComparer.Ordinal);
        // US6: los CDAT, el calendario hábil de sus planes, sus cuentas destino vivas y la cuenta bancaria registrada de los que reciben por giro.
        var cdatPor = filas.Cdat.ToDictionary(c => c.Fila.SourceAccountNumber, c => c.Fila, StringComparer.Ordinal);
        var calendarioDeCdat = filas.Cdat.Count == 0 ? CdatMigrado.SinFestivos : calendario is null ? CdatMigrado.SinFestivos
            : await calendario.ObtenerAsync(filas.Cdat.Min(c => c.Fila.StartDate), filas.Cdat.Max(c => c.Fila.MaturityDate).AddDays((int)RepaymentPeriodicity.Monthly),
                IngenIA365ERP.Application.Common.Calendar.DiasDeDescanso.SabadoYDomingo, null, ct);
        var destinosVivos = filas.Cdat.Select(c => c.Fila.DestinationAccountNumber).OfType<string>().Distinct().ToList();
        var cuentasVivas = destinosVivos.Count == 0 ? new Dictionary<string, (int Id, Guid PublicId)>(StringComparer.Ordinal)
            : (await db.MemberAccounts.AsNoTracking().Where(a => destinosVivos.Contains(a.Number) && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
                .Select(a => new { a.Number, a.Id, a.PublicId }).ToListAsync(ct)).ToDictionary(a => a.Number, a => (a.Id, a.PublicId), StringComparer.Ordinal);
        var porGiro = filas.Cdat.Where(c => c.Fila.InterestDestination == InterestDestination.PaymentOrder).Select(c => c.Fila.PersonId).Distinct().ToList();
        var bancarias = porGiro.Count == 0 ? [] : (await db.Associates.AsNoTracking().Where(a => porGiro.Contains(a.PersonId) && !a.IsDeleted)
                .Select(a => new { a.Id, a.PersonId, a.DepositBankId, a.DepositBankAccountNumber, a.DepositBankAccountType }).ToListAsync(ct))
            .GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.MaxBy(a => a.Id)!);
        var codigosDeBanco = filas.Cdat.Select(c => c.Fila.DestinationBankTransferCode).OfType<string>().Distinct().ToList();
        var bancosDelGiro = codigosDeBanco.Count == 0 ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : (await db.Banks.AsNoTracking().Where(b => b.TransferCode != null && codigosDeBanco.Contains(b.TransferCode) && !b.IsDeleted)
                .Select(b => new { b.TransferCode, b.Id }).ToListAsync(ct)).ToDictionary(b => b.TransferCode!, b => b.Id, StringComparer.OrdinalIgnoreCase);
        var nacidasPorNumero = new Dictionary<string, (int Id, Guid PublicId)>(StringComparer.Ordinal);
        // US11 (T189; research E166): los embargos de la carga nacen antes que las cuentas (un embargo por persona y oficio); los bloqueos van con su cuenta.
        var bloqueosPor = filas.Bloqueos.GroupBy(b => b.Fila.SourceAccountNumber, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Fila).ToList(), StringComparer.Ordinal);
        var embargos = await EmbargosMigradosAsync(filas, cuentas, actor.UserId, ct);
        var creditosDeGarantia = await CreditosDeGarantiaAsync(filas, ct);

        // §0.4: el consecutivo de cada línea arranca por encima de los números migrados (también los de las cuentas de ahorro).
        foreach (var grupo in cuentas.Where(c => c.Numero is not null).GroupBy(c => c.LineaCodigo, StringComparer.OrdinalIgnoreCase))
            await numerador.LlevarCuentasPorEncimaDeAsync(lineas[grupo.Key].Code, grupo.Select(c => c.Numero!).ToList(), lineas[grupo.Key].Prefijo, ct);

        var canje = cuentas.Count(c => c.Numero is { } n && ahorroPorNumero.TryGetValue(n, out var r) && r.ClearingAmount > 0m);
        var numerosDeOperacion = await numerador.NumerosDeOperacionDeCuentaAsync(MemberAccountOperationKind.OpeningBalance, PrefijoSaldoInicial,
            cuentas.Count + canje, ct);
        var siguienteNumero = 0;

        int transacciones = 0, partesNacidas = 0, cuotasNacidas = 0, exentas = 0, acumulados = 0, soportePendiente = 0;
        foreach (var tanda in cuentas.Chunk(Tanda))
        {
            var ahora = reloj.UtcNow;
            // 1) Cuentas, controles y titularidad.
            var nacidas = new List<(CuentaDeLaCarga Origen, MemberAccount Cuenta, MemberAccountControl Control)>(tanda.Length);
            foreach (var c in tanda)
            {
                var linea = lineas[c.LineaCodigo];
                var cdat = c.Parte == Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes.TermDeposits ? cdatPor[c.Numero!] : null;
                var ahorro = cdat is null && c.Numero is { } n && c.Naturaleza == LineNature.Deposit ? ahorroPorNumero[n] : null;
                var aportes = c.Naturaleza == LineNature.Contribution ? aportesPorTitular[c.TitularId] : null;
                var apertura = cdat?.ConstitutedOn ?? ahorro?.OpenedOn ?? aportes!.OpenedOn;
                var cuenta = MemberAccount.Abrir(linea.Id, c.Naturaleza, c.Clase, c.TitularId, sucursales[c.OficinaPublicId],
                    ahorro?.HandlingMode ?? HandlingMode.Indistinct, apertura, null, ahora, MemberAccountOrigin.Migration, carga.Id, c.Numero);
                cuenta.PublicId = Guid.NewGuid();
                if (c.Numero is not null) NumeradorDeCartera.ConservarNumeroDeCuenta(cuenta, c.Numero);
                else await numerador.NumerarCuentaAsync(cuenta, linea.Code, linea.Prefijo, ct);
                cuenta.Activar(activacion, ahora);
                if (c.Inactiva) cuenta.Inactivar(corte, ahora);
                // US6: el embargo de un CDAT migrado es la marca hasta que la entrega de embargos registre la orden (research E148).
                if (cdat is { IsGarnished: true }) cuenta.IsGarnished = true;
                db.MemberAccounts.Add(cuenta);

                var propias = c.Numero is { } num && partesPor.TryGetValue(num, out var lista) ? lista : [];
                var conRepresentante = AgregarPartes(cuenta, c, apertura, corte, propias, personas, mayoria);
                partesNacidas += propias.Count + 1;
                if (conRepresentante)
                {
                    cuenta.RequiresDataUpdate = true;
                    cuenta.DataUpdateRequiredSince = activacion;
                    soportePendiente++;
                }

                var control = registro.IniciarControl(cuenta, corte, ahora);
                if (ahorro?.LastSettledThrough is { } liquidada) RegistroDeCuentas.MarcarLiquidacion(control, liquidada);
                if (ahorro?.LastHolderActivityDate is { } actividad) RegistroDeCuentas.MarcarActividadDelTitular(control, actividad);
                nacidas.Add((c, cuenta, control));
            }
            await db.SaveChangesAsync(ct);
            foreach (var (_, cuenta, _) in nacidas) nacidasPorNumero[cuenta.Number] = (cuenta.Id, cuenta.PublicId);

            // 2) La operación de saldo inicial (y la del canje).
            var operaciones = new Dictionary<int, (MemberAccountOperation Saldo, MemberAccountOperation? Canje)>(nacidas.Count);
            foreach (var (c, cuenta, control) in nacidas)
            {
                var ahorro = c.Numero is { } n && c.Naturaleza == LineNature.Deposit && ahorroPorNumero.TryGetValue(n, out var r) ? r : null;
                var enCanje = ahorro?.ClearingAmount ?? 0m;
                var huella = Huella(c);
                var saldo = Operacion(cuenta, control, ahora, numerosDeOperacion[siguienteNumero++], corte,
                    c.Saldos.Where(kv => kv.Key != MemberAccountConcept.ChargeReceivable).Sum(kv => kv.Value) - enCanje,
                    actor, carga, motivo, huella, c, null);
                var deCanje = enCanje > 0m
                    ? Operacion(cuenta, control, ahora, numerosDeOperacion[siguienteNumero++], corte, enCanje, actor, carga, motivo, huella, c, canjeHasta ?? activacion)
                    : null;
                operaciones[cuenta.Id] = (saldo, deCanje);
            }
            await db.SaveChangesAsync(ct);

            // 2b) US6: la versión 1 de condiciones de cada CDAT (causa Migration) con su plan, en su operación de saldo inicial.
            var condicionesDe = new Dictionary<int, (MemberAccountTermsVersion Version, PlanDeCdatCalculado Plan)>();
            foreach (var (c, cuenta, control) in nacidas.Where(x => x.Origen.Parte == Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes.TermDeposits))
            {
                var f = cdatPor[c.Numero!];
                var linea = lineas[c.LineaCodigo].DeCaptacion!;
                var plan = PlanDeCdat.Generar(CdatMigrado.Condiciones(f, linea), calendarioDeCdat);
                int? destinoCuenta = null, destinoPersona = null, destinoBanco = null;
                Guid? destinoPublico = null;
                byte? tipo = null;
                string? numeroBancario = null;
                if (f.InterestDestination == InterestDestination.ToAccount && f.DestinationAccountNumber is { } numeroDestino)
                {
                    var destino = nacidasPorNumero.TryGetValue(numeroDestino, out var nacida) ? nacida : cuentasVivas[numeroDestino];
                    (destinoCuenta, destinoPublico) = (destino.Id, destino.PublicId);
                }
                else
                {
                    destinoPersona = c.TitularId;
                    if (f.DestinationBankTransferCode is { } codigoDelBanco && f.DestinationBankAccountNumber is { } cuentaDelGiro && f.DestinationBankAccountType is { } tipoDelGiro
                        && bancosDelGiro.TryGetValue(codigoDelBanco, out var bancoDelGiro))
                        (destinoBanco, tipo, numeroBancario) = (bancoDelGiro, tipoDelGiro, cuentaDelGiro);
                    else if (bancarias.TryGetValue(c.TitularId, out var b) && b.DepositBankId is { } banco && !string.IsNullOrWhiteSpace(b.DepositBankAccountNumber)
                        && byte.TryParse(b.DepositBankAccountType, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t))
                        (destinoBanco, tipo, numeroBancario) = (banco, t, b.DepositBankAccountNumber);
                }
                var destinoDto = new DestinoDeCdat(f.InterestDestination == InterestDestination.ToAccount ? TermDepositDestinationKind.Account : TermDepositDestinationKind.PaymentOrder,
                    destinoPublico, null, null, tipo, numeroBancario);
                var saldo = operaciones[cuenta.Id].Saldo;
                var tabla = f.TableAnnualRate ?? f.AppliedAnnualRate;
                var version = LectorDeCdat.NuevaVersion(cuenta, 1, saldo, AccountTermsCause.Migration, linea, plan, tabla, null, destinoDto, destinoCuenta, destinoPersona,
                    destinoBanco, f.MaturityInstruction, f.RolloverCount, null, ahora, saldo.InputsSha256, f.NegotiatedSpread);
                registro.SellarCondiciones(control, version, LectorDeCdat.FilasDelPlan(version, plan), 0);
                condicionesDe[cuenta.Id] = (version, plan);
            }
            if (condicionesDe.Count > 0) await db.SaveChangesAsync(ct);

            // 2c) US7 (T182): la versión 1 de condiciones de cada contractual (causa Migration) en su operación de saldo inicial —el plan desde el inicio que
            // da su vencimiento, la tasa base y la bonificada pactadas o las de la línea, y las cuotas pagadas al corte (sus aportes para la bonificación)—, la
            // marca de la última cuota del plan vencida al corte (el paso diario genera las siguientes) y, si ya estaba incumplido, la versión de castigo.
            var contratos = 0;
            foreach (var (c, cuenta, control) in nacidas.Where(x => x.Origen.Parte == Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes.MemberAccounts
                                                                     && x.Origen.Clase == DepositClass.Contractual))
            {
                var f = ahorroPorNumero[c.Numero!];
                var linea = lineas[c.LineaCodigo].DeCaptacion!;
                var plan = await ContractualMigrado.PlanAsync(f, linea, contractual, corte, ct);
                var saldo = operaciones[cuenta.Id].Saldo;
                var version = Contractual.LectorDeContractual.NuevaVersion(cuenta, 1, saldo, AccountTermsCause.Migration, linea, plan, plan.Condiciones.TasaBase, null,
                    null, null, ahora, saldo.InputsSha256, f.ContractualPaidInstallments);
                registro.SellarCondiciones(control, version, [], 0);
                if (plan.Cuotas.LastOrDefault(q => q.Vencimiento <= corte) is { } ultima)
                    RegistroDeCuentas.MarcarCuotaGenerada(control, Contractual.GeneradorDeCuotasDeContractual.Clave(plan, ultima));
                if (f.ContractualStatus == ContractualStatus.Breached)
                {
                    cuenta.Incumplir();
                    var numeroDeCastigo = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.ContractualBreach,
                        Contractual.IncumplimientoDeContractual.PrefijoIncumplimiento, ct);
                    var castigo = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
                    {
                        MemberAccount = cuenta, MemberAccountId = cuenta.Id, Kind = MemberAccountOperationKind.ContractualBreach, Number = numeroDeCastigo,
                        Sequence = sello.Sequence, ValueDate = corte, AccountingDate = corte, RegisteredAt = sello.RegisteredAt, Amount = 0m, ActorKind = actor.Kind,
                        ActorUserId = actor.UserId, OriginUserCentralId = actor.CentralUserId, MigrationLoadId = carga.Id,
                        Reason = "Contrato incumplido en el sistema anterior: causa a la tasa de castigo desde el corte.",
                        Payload = JsonSerializer.Serialize(new { load = carga.PublicId, migrated = true, breached = true }, Json), InputsSha256 = saldo.InputsSha256,
                        RulesVersion = VersionesDeReglas.Vigente,
                    });
                    await db.SaveChangesAsync(ct);
                    registro.SellarCondiciones(control, Contractual.LectorDeContractual.VersionDeCastigo(cuenta, version, version, castigo, ahora, saldo.InputsSha256), [],
                        version.VersionNumber);
                }
                contratos++;
            }
            if (contratos > 0) await db.SaveChangesAsync(ct);

            // 3) Las cuotas atrasadas (generadas por la operación de saldo inicial).
            var cuotasDe = new Dictionary<int, List<(AccountInstallment Cuota, MigrationAccountInstallmentRow Fila)>>();
            foreach (var (c, cuenta, _) in nacidas)
            {
                if (c.Numero is not { } n || !cuotasPor.TryGetValue(n, out var filasDeCuotas)) continue;
                var saldo = operaciones[cuenta.Id].Saldo;
                var lista = filasDeCuotas.Select(q => (new AccountInstallment
                {
                    MemberAccount = cuenta, MemberAccountId = cuenta.Id, PeriodYear = q.PeriodYear, PeriodMonth = q.PeriodMonth, SubPeriodNumber = q.SubPeriodNumber,
                    DueDate = q.DueDate, Amount = q.Amount, GeneratedByOperationId = saldo.Id, RulesVersion = VersionesDeReglas.Vigente,
                    InputsSha256 = new HuellaDeCartera().Texto("load", carga.PublicId.ToString("N")).Texto("account", n).Entero("year", q.PeriodYear)
                        .Entero("month", q.PeriodMonth).Entero("sub", q.SubPeriodNumber).Decimal("amount", q.Amount, 2).Decimal("paid", q.PaidAmount, 2).Calcular(),
                }, q)).ToList();
                registro.RegistrarCuotas(lista.Select(x => x.Item1));
                cuotasDe[cuenta.Id] = lista;
                cuotasNacidas += lista.Count;
            }
            await db.SaveChangesAsync(ct);

            // 4) Las transacciones de saldo inicial por concepto, lo no descontado, la marca del GMF y el acumulado tributario.
            foreach (var (c, cuenta, _) in nacidas)
            {
                var (saldo, deCanje) = operaciones[cuenta.Id];
                var filasDelLibro = new List<MemberAccountTransaction>();
                var cuotas = cuotasDe.GetValueOrDefault(cuenta.Id) ?? [];
                foreach (var (concepto, monto) in c.Saldos.OrderBy(kv => (int)kv.Key))
                {
                    var restante = monto;
                    if (concepto == c.ConceptoDeCuotas)
                        foreach (var (cuota, fila) in cuotas.Where(x => x.Fila.PaidAmount > 0m))
                        {
                            filasDelLibro.Add(Fila(cuenta, saldo, concepto, fila.PaidAmount, corte, cuota.Id));
                            restante -= fila.PaidAmount;
                        }
                    if (concepto == MemberAccountConcept.Principal && deCanje is not null)
                    {
                        filasDelLibro.Add(Fila(cuenta, deCanje, concepto, deCanje.Amount, corte, null));
                        restante -= deCanje.Amount;
                    }
                    if (restante != 0m) filasDelLibro.Add(Fila(cuenta, saldo, concepto, restante, corte, null));
                }
                // US6: el saldo inicial del CDAT nombra su versión; el interés por pagar, además, el período en curso al corte.
                if (condicionesDe.TryGetValue(cuenta.Id, out var deCdat))
                {
                    var enCurso = deCdat.Plan.PeriodoDe(corte)?.Numero ?? deCdat.Plan.Periodos.LastOrDefault(p => p.Desde < corte)?.Numero;
                    filasDelLibro = filasDelLibro.Select(x => new MemberAccountTransaction
                    {
                        MemberAccount = x.MemberAccount, MemberAccountId = x.MemberAccountId, Operation = x.Operation, OperationId = x.OperationId, Kind = x.Kind,
                        Concept = x.Concept, Amount = x.Amount, ValueDate = x.ValueDate, AccountingDate = x.AccountingDate, RegisteredAt = x.RegisteredAt,
                        TermsVersionId = deCdat.Version.Id,
                        SchedulePeriodNumber = x.Concept == MemberAccountConcept.InterestPayable ? enCurso : null,
                    }).ToList();
                }
                registro.RegistrarTransacciones(filasDelLibro);
                transacciones += filasDelLibro.Count;

                foreach (var (cuota, fila) in cuotas.Where(x => x.Fila.NonDeductionReason is not null))
                    registro.RegistrarNoDescontado(new AccountInstallmentNonDeduction
                    {
                        InstallmentId = cuota.Id, Origin = NonDeductionOrigin.Activation, Reason = fila.NonDeductionReason!.Value, RequestedAmount = fila.Amount,
                        DeductedAmount = fila.PaidAmount, CollectorCode = "MIG", ReferenceKey = string.Create(CultureInfo.InvariantCulture, $"CARGA-{carga.Number}"),
                        OccurredOn = fila.DueDate, Detail = "Migrada: no descontada al corte.",
                    });

                if (c.Numero is not { } numero) continue;
                if (bloqueosPor.TryGetValue(numero, out var bloqueos))
                    foreach (var b in bloqueos)
                    {
                        if (b.Kind == "Garnishment")
                        {
                            if (b.RetainedAmount is not > 0m) continue;
                            db.AccountHolds.Add(Domain.Entities.Lending.Deposits.Restrictions.AccountHold.Poner(cuenta.Id, HoldReason.Garnishment, b.RetainedAmount,
                                b.ValidFrom, null, $"Migrado con la carga {carga.Number}", null, null, garnishmentId: embargos[(c.TitularId, b.Reference.ToUpperInvariant())].Id));
                            cuenta.IsGarnished = true;
                        }
                        else
                            db.AccountHolds.Add(Domain.Entities.Lending.Deposits.Restrictions.AccountHold.Poner(cuenta.Id, b.Reason!.Value, b.IsTotal ? null : b.Amount,
                                b.ValidFrom, null, $"{b.Reference} (migrado con la carga {carga.Number})", null, null,
                                loanId: b.LoanNumber is { } ln ? creditosDeGarantia.GetValueOrDefault(ln) : null));
                    }
                if (exencionPor.TryGetValue(numero, out var exencion))
                {
                    db.GmfExemptAccounts.Add(GmfExemptAccount.Marcar(cuenta, exencion.MarkedSince, "Marca migrada con la carga de captaciones.", null));
                    registro.RegistrarUsoDeGmfMigrado(exencion.PersonId, saldo, cuenta, corte, exencion.MonthExemptUsed, politica);
                    exentas++;
                }
                if (tributarioPor.TryGetValue(numero, out var tributario))
                    foreach (var porPersona in tributario.GroupBy(t => (t.PersonId, t.TaxYear)))
                    {
                        decimal Valor(MigratedTaxConcept k) => porPersona.Where(t => t.Concept == k).Sum(t => t.Amount);
                        registro.RegistrarAcumuladoMigrado(porPersona.Key.PersonId, saldo, cuenta, porPersona.Key.TaxYear, Valor(MigratedTaxConcept.Interest),
                            Valor(MigratedTaxConcept.InflationaryComponent),
                            porPersona.Where(t => t.Concept == MigratedTaxConcept.Withholding).Sum(t => t.WithholdingBase ?? 0m), Valor(MigratedTaxConcept.Withholding));
                        acumulados++;
                    }
            }
            await db.SaveChangesAsync(ct);
        }
        return new CuentasNacidas(cuentas.Count, transacciones, partesNacidas, cuotasNacidas, exentas, acumulados, soportePendiente);
    }

    /// <summary>
    /// US11 (T189; research E166): los embargos de la carga, uno por persona y oficio, activos desde su fecha y sin aprobación (el sistema anterior ya los
    /// aplicaba): sin soporte adjunto (se sube después), sin respuesta registrada y con el valor de la medida que trae la fila. Guarda.
    /// </summary>
    private async Task<Dictionary<(int Persona, string Oficio), Domain.Entities.Lending.Deposits.Restrictions.Garnishment>> EmbargosMigradosAsync(FilasDeLaCarga filas,
        IReadOnlyList<CuentaDeLaCarga> cuentas, int? usuario, CancellationToken ct)
    {
        var titularDe = cuentas.Where(c => c.Numero is not null).ToDictionary(c => c.Numero!, c => c.TitularId, StringComparer.Ordinal);
        var r = new Dictionary<(int, string), Domain.Entities.Lending.Deposits.Restrictions.Garnishment>();
        var juzgados = filas.Bloqueos.Where(b => b.Fila.CourtPersonId != null).Select(b => b.Fila.CourtPersonId!.Value).Distinct().ToList();
        var nombres = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => juzgados.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var ahora = reloj.UtcNow;
        foreach (var b in filas.Bloqueos.Select(x => x.Fila).Where(f => f.Kind == "Garnishment" && titularDe.ContainsKey(f.SourceAccountNumber)))
        {
            var clave = (titularDe[b.SourceAccountNumber], b.Reference.ToUpperInvariant());
            if (r.ContainsKey(clave)) continue;
            r[clave] = new Domain.Entities.Lending.Deposits.Restrictions.Garnishment
            {
                PersonId = clave.Item1, Authority = b.Authority ?? GarnishmentAuthority.Court, CourtPersonId = b.CourtPersonId!.Value,
                CourtName = ReferenciasDeGobierno.Nombre(nombres[b.CourtPersonId!.Value]), ProcessNumber = b.ProcessNumber ?? string.Empty,
                OrderNumber = b.Reference, OrderDate = b.OrderDate ?? b.ValidFrom, ReceivedOn = b.ValidFrom, ClaimantName = b.ClaimantName ?? string.Empty,
                Amount = b.MeasureAmount, IsChildSupport = b.IsChildSupport, AccountsScope = GarnishmentAccountsScope.AllSavings,
                OrderAttachmentPublicId = Guid.Empty, Status = GarnishmentStatus.Active, RegisteredByUserId = usuario, RegisteredAt = ahora,
                Notes = "Migrado con la carga de captaciones: el oficio se adjunta después.",
            };
        }
        if (r.Count == 0) return r;
        db.Garnishments.AddRange(r.Values);
        await db.SaveChangesAsync(ct);
        return r;
    }

    /// <summary>US11 (T189): los créditos vivos que garantizan las cuentas de la carga (bloqueos <c>LoanCollateral</c>), por número.</summary>
    private async Task<Dictionary<string, int?>> CreditosDeGarantiaAsync(FilasDeLaCarga filas, CancellationToken ct)
    {
        var numeros = filas.Bloqueos.Where(b => b.Fila.LoanNumber is not null).Select(b => b.Fila.LoanNumber!).Distinct().ToList();
        if (numeros.Count == 0) return new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        return (await db.Loans.AsNoTracking().Where(l => numeros.Contains(l.Number) && !l.IsDeleted).Select(l => new { l.Number, l.Id }).ToListAsync(ct))
            .ToDictionary(l => l.Number, l => (int?)l.Id, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La titularidad de la cuenta: el titular (con la proporción que le dejan los cotitulares, como al abrir), las demás partes con su vigencia y los
    /// beneficiarios en el conjunto 1. Devuelve si hay un representante legal (su soporte se sube después).
    /// </summary>
    private bool AgregarPartes(MemberAccount cuenta, CuentaDeLaCarga c, DateOnly apertura, DateOnly corte, IReadOnlyList<MigrationAccountPartyRow> propias,
        IReadOnlyDictionary<int, Domain.Entities.Core.Person> personas, int mayoria)
    {
        var titular = personas[c.TitularId];
        var juridica = LectorDeCuentas.EsJuridica(titular);
        var menor = !juridica && ReglasDeTitularidad.EsMenor(titular.DateOfBirth, corte, mayoria);
        var contexto = new ContextoDeTitularidad(c.Naturaleza, c.TitularId, AdmiteCotitulares: true, AdmiteMenores: true, AdmitePersonasJuridicas: true,
            TitularMenor: c.Naturaleza == LineNature.Deposit && menor, TitularJuridico: c.Naturaleza == LineNature.Deposit && juridica, SoporteDiferido: true);
        var solicitadas = propias.Where(p => p.Role != MemberAccountPartyRole.Beneficiary)
            .Select(p => new ParteSolicitada(p.PersonId, p.Role, p.Role == MemberAccountPartyRole.CoHolder ? p.Share : null, p.SignatureLimit, p.RepresentationBasis))
            .ToList();
        var manejo = cuenta.HandlingMode;
        var (resueltas, problema) = ReglasDeTitularidad.Resolver(contexto, manejo, solicitadas);
        if (problema is not null)
            throw new InvalidOperationException($"La titularidad de la cuenta {cuenta.Number} no se resolvió al activar ({problema.Codigo}): la validación entre partes debía detenerla.");
        var porPersona = propias.Where(p => p.Role != MemberAccountPartyRole.Beneficiary).ToDictionary(p => p.PersonId);
        foreach (var r in resueltas)
        {
            var origen = porPersona.GetValueOrDefault(r.PersonaId);
            var fila = MemberAccountParty.Nueva(cuenta, r.PersonaId, r.Papel, origen?.ValidFrom ?? apertura, r.Proporcion, r.LimiteDeFirma, r.Fundamento,
                soporteDiferido: r.Papel == MemberAccountPartyRole.LegalRepresentative);
            fila.ValidTo = origen?.ValidTo;
            db.MemberAccountParties.Add(fila);
        }
        foreach (var b in propias.Where(p => p.Role == MemberAccountPartyRole.Beneficiary))
        {
            var fila = MemberAccountParty.Nueva(cuenta, b.PersonId, MemberAccountPartyRole.Beneficiary, b.ValidFrom, b.Share, conjunto: 1);
            fila.ValidTo = b.ValidTo;
            db.MemberAccountParties.Add(fila);
        }
        return resueltas.Any(r => r.Papel == MemberAccountPartyRole.LegalRepresentative);
    }

    private MemberAccountOperation Operacion(MemberAccount cuenta, MemberAccountControl control, DateTime ahora, string numero, DateOnly corte, decimal monto,
        IngenIA365ERP.Application.Common.Execution.Actor actor, MigrationLoad carga, string motivo, string huella, CuentaDeLaCarga c, DateOnly? canjeHasta) =>
        registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccount = cuenta,
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.OpeningBalance,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = corte,
            AccountingDate = corte,
            RegisteredAt = sello.RegisteredAt,
            Amount = Math.Max(0m, monto),
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            ClearingUntil = canjeHasta,
            MigrationLoadId = carga.Id,
            Reason = motivo.Length <= 300 ? motivo : motivo[..300],
            Payload = JsonSerializer.Serialize(new
            {
                load = carga.PublicId, part = c.Parte, batch = c.Tanda, row = c.Fila, balances = c.Saldos.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
                clearing = canjeHasta is not null,
            }, Json),
            InputsSha256 = huella,
            RulesVersion = VersionesDeReglas.Vigente,
        });

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation operacion, MemberAccountConcept concepto, decimal monto, DateOnly corte,
        long? cuota) => new()
    {
        MemberAccount = cuenta,
        MemberAccountId = cuenta.Id,
        Operation = operacion,
        OperationId = operacion.Id,
        Kind = MemberAccountTransactionKind.OpeningBalance,
        Concept = concepto,
        InstallmentId = cuota,
        Amount = monto,
        ValueDate = corte,
        AccountingDate = corte,
        RegisteredAt = operacion.RegisteredAt,
    };

    private static string Huella(CuentaDeLaCarga c) => new HuellaDeCartera()
        .Texto("part", c.Parte).Entero("batch", c.Tanda).Entero("row", c.Fila).Texto("number", c.Numero).Entero("holder", c.TitularId).Texto("line", c.LineaCodigo)
        .Lista("balances", c.Saldos.OrderBy(kv => (int)kv.Key), (h, kv) => h.Texto("concept", kv.Key.ToString()).Decimal("amount", kv.Value, 6))
        .Calcular();
}
