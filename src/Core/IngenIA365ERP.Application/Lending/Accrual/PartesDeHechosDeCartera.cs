using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Accrual;

/// <summary>Lo que el sobre y las dimensiones de los hechos de un crédito necesitan, leído en bloque para una tanda. (nuevo)</summary>
public sealed record DatosDeHecho(
    Loan Credito,
    LoanTermsVersion Condiciones,
    string CodigoDeLinea,
    Guid DeudorPublicId,
    bool DeudorEsEmpleado,
    Guid SucursalPublicId,
    Guid? AseguradoraPublicId)
{
    public LoanRefV1 Referencia => new() { LoanPublicId = Credito.PublicId, LoanNumber = Credito.Number };

    /// <summary>
    /// K2 (T0843; FR-116, C44): el sub-segmento que rige después de un traslado de libranza o del paso a la forma alterna, si cambió frente al de
    /// las condiciones; nulo = el de las condiciones.
    /// </summary>
    public string? Subsegmento { get; init; }

    public HechoDeCredito Hecho(DateOnly fechaContable) =>
        new(Credito, Condiciones, CodigoDeLinea, DeudorPublicId, DeudorEsEmpleado, SucursalPublicId, fechaContable) { Subsegmento = Subsegmento };
}

/// <summary>
/// De dónde salen las partes (data-model §22.3): la fuente, la corrida (nula en la puesta al día), la operación que pone al día (sólo en
/// <c>LoanCatchUp</c>), la tanda de cada día causado (el ciclo usa una por fecha, porque <c>UK_LND_AccountingFactParts_Batch</c> no lleva la
/// fecha) y a nombre de quién van los mensajes si actúa el proceso. (nuevo)
/// </summary>
public sealed record OrigenDePartes(
    AccountingFactPartSource Fuente,
    LendingProcessRun? Corrida,
    LoanOperation? Disparadora,
    IReadOnlyDictionary<DateOnly, int> TandaPorDia,
    UserRefV1? UsuarioDeOrigen)
{
    /// <summary>La fecha contable de lo que no se fecha en su día (el ajuste de la corrección: la del período abierto a hoy, T36).</summary>
    public DateOnly? FechaDeRegistro { get; init; }

    public int Tanda(DateOnly dia) => TandaPorDia.TryGetValue(dia, out var t) ? t : 1;
}

/// <summary>Las filas de causación (o de ajuste) de un crédito en un día valor, con sus datos de hecho.</summary>
public sealed record LineaDeCausacion(DatosDeHecho Datos, DateOnly Dia, IReadOnlyList<MovimientoDeLibro> Filas);

/// <summary>Un hecho por crédito que emite el proceso: su tipo, el crédito, el día y su contenido ya armado.</summary>
public sealed record HechoPorCredito(string Tipo, DatosDeHecho Datos, DateOnly Dia, object Contenido);

/// <summary>Lo que dejaron las partes: las partes, los mensajes y el mensaje de cada (crédito, día, concepto) para las filas del libro.</summary>
public sealed record PartesEmitidas(
    IReadOnlyList<AccountingFactPart> Partes,
    IReadOnlyList<IntegrationMessage> Mensajes,
    IReadOnlyDictionary<(int LoanId, DateOnly Dia, LoanConcept Concepto), Guid> MensajePorFila)
{
    public static PartesEmitidas Vacias { get; } = new([], [], new Dictionary<(int, DateOnly, LoanConcept), Guid>());

    public Guid? Mensaje(int loanId, DateOnly dia, LoanConcept concepto) =>
        MensajePorFila.TryGetValue((loanId, dia, concepto), out var m) ? m : null;
}

/// <summary>
/// El armado de <c>LND_AccountingFactParts</c> y de sus contenidos (feature 013, US4, T0495; T14, C02–C04; mensajes.md §10.0, §10.4–§10.6):
/// <list type="bullet">
/// <item><b>masivos</b>: una parte por fuente × día × sucursal contable × tipo × tanda (en la puesta al día, por sucursal y tipo con la
/// operación como tanda y una línea por crédito y día valor); totales por combinación de <c>LoanFactDimensionsV1</c> y concepto, la lista
/// por crédito con su huella (<c>LoanListSha256</c>) y su total de control, y el ordinal de la parte en la corrida;</item>
/// <item><b>por crédito</b>: la parte de la tanda es el origen de un mensaje por crédito (<c>PerLoanMessages</c>, sin mensaje propio).</item>
/// </list>
/// El mensaje lo emite <see cref="EmisionDeCartera"/> antes de agregar la parte, porque la parte guarda su <c>MessagePublicId</c> (es un hecho).
/// No guarda. (nuevo)
/// </summary>
public sealed class PartesDeHechosDeCartera(IApplicationDbContext db, EmisionDeCartera emision, FechaContable fechaContable)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Los conceptos de cada hecho masivo de la causación.</summary>
    public static IReadOnlyDictionary<string, LoanConcept[]> ConceptosDe { get; } = new Dictionary<string, LoanConcept[]>
    {
        [InteresCausadoV1.Type] = [LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense],
        [MoraCausadaV1.Type] = [LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense],
        [SeguroCausadoV1.Type] = [LoanConcept.Insurance],
    };

    /// <summary>El tipo masivo de la causación de un concepto.</summary>
    public static string TipoDe(LoanConcept concepto) =>
        ConceptosDe.First(kv => kv.Value.Contains(concepto)).Key;

    /// <summary>Los datos de hecho de los créditos cargados, en bloque.</summary>
    public async Task<IReadOnlyDictionary<int, DatosDeHecho>> DatosAsync(IEnumerable<CreditoCargado> cargados, CancellationToken ct)
    {
        var lista = cargados.ToList();
        var lineas = lista.Select(c => c.Credito.CreditLineId).Distinct().ToList();
        var personas = lista.Select(c => c.Credito.DebtorPersonId).Distinct().ToList();
        var oficinas = lista.Select(c => c.Credito.BranchId).Distinct().ToList();
        var versiones = lista.Select(c => c.Pactadas.CreditLineVersionId).Distinct().ToList();
        var codigos = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        var deudores = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IsEmployee }).ToDictionaryAsync(p => p.Id, ct);
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var seguros = await (from s in db.CreditLineInsurances.AsNoTracking()
                             join p in db.People.AsNoTracking().IgnoreQueryFilters() on s.InsurerPersonId equals p.Id
                             where versiones.Contains(s.CreditLineVersionId)
                             select new { s.CreditLineVersionId, s.Code, p.PublicId }).ToListAsync(ct);

        // K2 (T0843): el vínculo de libranza vigente de los créditos cuya forma de pago cambió después de nacer (traslado o forma alterna).
        var creditos = lista.Select(c => c.Credito.Id).ToList();
        var cambiados = (await db.LoanPayrollLinks.AsNoTracking()
                .Where(v => creditos.Contains(v.LoanId) && !v.IsDeleted && v.ValidTo == null
                            && (v.Origin == LoanPayrollLinkOrigin.Transfer || v.Origin == LoanPayrollLinkOrigin.ChannelChange))
                .ToListAsync(ct))
            .GroupBy(v => v.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.ValidFrom).First());

        var datos = new Dictionary<int, DatosDeHecho>();
        foreach (var c in lista)
        {
            var deudor = deudores.GetValueOrDefault(c.Credito.DebtorPersonId);
            var suyos = seguros.Where(s => s.CreditLineVersionId == c.Pactadas.CreditLineVersionId).ToList();
            var subsegmento = EmisionDeCartera.SubsegmentoVigente(c.Vigentes.SubsegmentCode, cambiados.GetValueOrDefault(c.Credito.Id));
            datos[c.Credito.Id] = new DatosDeHecho(c.Credito, c.Vigentes, codigos.GetValueOrDefault(c.Credito.CreditLineId) ?? string.Empty,
                deudor?.PublicId ?? Guid.Empty, deudor?.IsEmployee ?? false, sucursales.GetValueOrDefault(c.Credito.BranchId),
                suyos.Count == 1 ? suyos[0].PublicId : null)
            {
                Subsegmento = subsegmento == c.Vigentes.SubsegmentCode ? null : subsegmento,
            };
        }
        return datos;
    }

    /// <summary>
    /// Las partes masivas de unas filas de causación (o del ajuste de la corrección): agrupa, arma el contenido, emite y agrega las partes.
    /// <paramref name="tipoUnico"/> fuerza un solo tipo (el ajuste de la corrección lleva todos los conceptos en <c>AjusteDeCausacionPorFechaValor</c>).
    /// </summary>
    public async Task<Result<PartesEmitidas>> MasivasAsync(OrigenDePartes origen, IReadOnlyList<LineaDeCausacion> lineas, string? tipoUnico = null,
        CorrectedNationalValueV1? valorCorregido = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(origen);
        var porDia = origen.Fuente == AccountingFactPartSource.DailyCycleBatch;
        var filas = lineas.SelectMany(l => l.Filas.Where(f => f.Monto != 0m).Select(f => (Linea: l, Fila: f, Tipo: tipoUnico ?? TipoDe(f.Concepto))));
        var grupos = filas
            .GroupBy(x => (Dia: porDia ? x.Linea.Dia : DateOnly.MinValue, x.Linea.Datos.Credito.BranchId, x.Tipo))
            .OrderBy(g => g.Key.Dia).ThenBy(g => g.Key.BranchId).ThenBy(g => g.Key.Tipo, StringComparer.Ordinal)
            .ToList();
        var partes = new List<AccountingFactPart>();
        var mensajes = new List<IntegrationMessage>();
        var mensajePorFila = new Dictionary<(int, DateOnly, LoanConcept), Guid>();
        var ordinal = 0;
        foreach (var g in grupos)
        {
            var dia = porDia ? g.Key.Dia : g.Max(x => x.Linea.Dia);
            var fechaDelSobre = origen.Fuente == AccountingFactPartSource.NationalValueCorrectionBatch ? origen.Corrida?.AffectedTo ?? dia : dia;
            // US9 (T0711): la causación del día se fecha en su día aunque Cartera ya haya cerrado el mes (no ocurre: el cierre exige la causación al
            // día); lo demás salta los meses cerrados por Cartera.
            var contable = await fechaContable.CalcularAsync(origen.Fuente == AccountingFactPartSource.NationalValueCorrectionBatch
                ? origen.FechaDeRegistro ?? dia : dia, conCierreDeCartera: origen.Fuente != AccountingFactPartSource.DailyCycleBatch, ct);
            if (contable.IsFailure) return Result.Failure<PartesEmitidas>(contable.Error);
            var tanda = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? 1 : origen.Tanda(dia);
            var numeroDeParte = SiguienteParte(origen, ref ordinal);
            var datosDelPrimero = g.First().Linea.Datos;
            // US9 (T0702): la categoría contable vigente de cada crédito de la parte (antes, la del primero para todos).
            var categorias = await new CategoriaContableDeCartera(db).VigentesAsync(g.Select(x => x.Linea.Datos.Credito.Id).Distinct().ToList(), contable.Value, ct);

            // Una línea por crédito (o por crédito y día valor fuera del ciclo diario).
            var lineasDelHecho = g.GroupBy(x => (x.Linea.Datos.Credito.Id, Dia: porDia ? DateOnly.MinValue : x.Linea.Dia))
                .OrderBy(l => l.Key.Id).ThenBy(l => l.Key.Dia)
                .Select(l =>
                {
                    var d = l.First().Linea.Datos;
                    var dims = EmisionDeCartera.Dimensiones(d.Hecho(contable.Value), categorias[d.Credito.Id]);
                    var seguro = g.Key.Tipo == SeguroCausadoV1.Type;
                    return new LoanFactLineV1
                    {
                        Loan = d.Referencia,
                        DebtorPersonPublicId = d.DeudorPublicId,
                        CounterpartyPersonPublicId = seguro ? d.AseguradoraPublicId : null,
                        Dimensions = dims,
                        Amounts = l.GroupBy(x => (x.Fila.Concepto, x.Fila.Cuota)).OrderBy(c => (int)c.Key.Concepto).ThenBy(c => c.Key.Cuota ?? 0)
                            .Select(c => new ConceptAmountV1
                            {
                                Concept = c.Key.Concepto.ToString(),
                                InstallmentNumber = c.Key.Cuota,
                                Amount = c.Sum(x => x.Fila.Monto),
                                OnBehalfOfAssociate = seguro,
                            }).ToList(),
                        ValueDate = porDia ? null : l.Key.Dia,
                    };
                }).ToList();
            var totales = lineasDelHecho.GroupBy(l => l.Dimensions)
                .Select(t => new LoanFactTotalV1
                {
                    Dimensions = t.Key,
                    Amounts = t.SelectMany(l => l.Amounts).GroupBy(a => a.Concept, StringComparer.Ordinal).OrderBy(a => a.Key, StringComparer.Ordinal)
                        .Select(a => new ConceptAmountV1 { Concept = a.Key, Amount = a.Sum(x => x.Amount), OnBehalfOfAssociate = a.Any(x => x.OnBehalfOfAssociate) })
                        .ToList(),
                    LoanCount = t.Select(l => l.Loan.LoanPublicId).Distinct().Count(),
                }).ToList();
            var control = lineasDelHecho.Sum(l => l.Amounts.Sum(a => a.Amount));
            var partePublicId = Guid.NewGuid();
            var contenido = Contenido(g.Key.Tipo, origen, dia, tanda, numeroDeParte, totales, lineasDelHecho, control, valorCorregido);
            var emitido = await emision.HechoDeParteAsync(new EmisionDeParte(partePublicId, Rotulo(origen, dia, numeroDeParte),
                contable.Value, datosDelPrimero.SucursalPublicId, null, null,
                origen.Fuente == AccountingFactPartSource.NationalValueCorrectionBatch ? null : dia, origen.UsuarioDeOrigen), g.Key.Tipo, contenido, ct);
            if (emitido.IsFailure) return Result.Failure<PartesEmitidas>(emitido.Error);

            var parte = new AccountingFactPart
            {
                PublicId = partePublicId,
                SourceKind = origen.Fuente,
                ProcessRunId = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? null : origen.Corrida?.Id,
                BatchNumber = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? null : tanda,
                TriggerOperationId = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? origen.Disparadora?.Id : null,
                FactType = g.Key.Tipo,
                PerLoanMessages = false,
                ValueDate = fechaDelSobre,
                AccountingDate = contable.Value,
                BranchId = g.Key.BranchId,
                OriginEventKey = ClavesDeEvento.ConfirmacionPor(partePublicId),
                MessagePublicId = emitido.Value.PublicId,
                MessageCount = 1,
                LoanCount = lineasDelHecho.Select(l => l.Loan.LoanPublicId).Distinct().Count(),
                TotalAmount = control,
                TotalsByDimensionJson = JsonSerializer.Serialize(totales, Json),
                LoanListSha256 = Huella(lineasDelHecho),
            };
            db.AccountingFactParts.Add(parte);
            partes.Add(parte);
            mensajes.Add(emitido.Value);
            foreach (var x in g) mensajePorFila[(x.Linea.Datos.Credito.Id, x.Linea.Dia, x.Fila.Concepto)] = emitido.Value.PublicId;
        }
        return Result.Success(new PartesEmitidas(partes, mensajes, mensajePorFila));
    }

    /// <summary>
    /// Los hechos por crédito que emite el proceso (C02): una parte por día × sucursal × tipo con <c>PerLoanMessages</c>, origen de un mensaje
    /// por crédito con clave <c>Confirmation:{crédito:N}</c> y tercero el deudor.
    /// </summary>
    public async Task<Result<PartesEmitidas>> PorCreditoAsync(OrigenDePartes origen, IReadOnlyList<HechoPorCredito> hechos, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(origen);
        var partes = new List<AccountingFactPart>();
        var mensajes = new List<IntegrationMessage>();
        var ordinal = 0;
        foreach (var g in hechos.GroupBy(h => (h.Dia, h.Datos.Credito.BranchId, h.Tipo)).OrderBy(g => g.Key.Dia).ThenBy(g => g.Key.BranchId)
                     .ThenBy(g => g.Key.Tipo, StringComparer.Ordinal))
        {
            var contable = await fechaContable.CalcularAsync(g.Key.Dia, ct);
            if (contable.IsFailure) return Result.Failure<PartesEmitidas>(contable.Error);
            var tanda = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? 1 : origen.Tanda(g.Key.Dia);
            var numeroDeParte = SiguienteParte(origen, ref ordinal);
            var partePublicId = Guid.NewGuid();
            var rotulo = Rotulo(origen, g.Key.Dia, numeroDeParte);
            var suyos = new List<IntegrationMessage>();
            foreach (var h in g.OrderBy(h => h.Datos.Credito.Id))
            {
                var emitido = await emision.HechoDeParteAsync(new EmisionDeParte(partePublicId, rotulo, contable.Value, h.Datos.SucursalPublicId,
                    h.Datos.DeudorPublicId, h.Datos.Credito.PublicId, g.Key.Dia, origen.UsuarioDeOrigen), h.Tipo, h.Contenido, ct);
                if (emitido.IsFailure) return Result.Failure<PartesEmitidas>(emitido.Error);
                suyos.Add(emitido.Value);
            }
            var parte = new AccountingFactPart
            {
                PublicId = partePublicId,
                SourceKind = origen.Fuente,
                ProcessRunId = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? null : origen.Corrida?.Id,
                BatchNumber = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? null : tanda,
                TriggerOperationId = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? origen.Disparadora?.Id : null,
                FactType = g.Key.Tipo,
                PerLoanMessages = true,
                ValueDate = g.Key.Dia,
                AccountingDate = contable.Value,
                BranchId = g.Key.BranchId,
                OriginEventKey = ClavesDeEvento.ConfirmacionPor(partePublicId),
                MessagePublicId = null,
                MessageCount = suyos.Count,
                LoanCount = g.Select(h => h.Datos.Credito.Id).Distinct().Count(),
                TotalAmount = 0m,
                TotalsByDimensionJson = "[]",
                LoanListSha256 = Huella(g.Select(h => h.Datos.Referencia).ToList()),
            };
            db.AccountingFactParts.Add(parte);
            partes.Add(parte);
            mensajes.AddRange(suyos);
        }
        return Result.Success(new PartesEmitidas(partes, mensajes, new Dictionary<(int, DateOnly, LoanConcept), Guid>()));
    }

    private static int SiguienteParte(OrigenDePartes origen, ref int ordinal)
    {
        if (origen.Corrida is { } corrida && origen.Fuente != AccountingFactPartSource.LoanCatchUp)
        {
            corrida.PartCount += 1;
            return corrida.PartCount;
        }
        ordinal += 1;
        return ordinal;
    }

    /// <summary>El largo de <c>origin.number</c> (<c>COR_IntegrationMessages.OriginNumber</c>, mensajes.md §3: string(30)).</summary>
    public const int LargoDelRotulo = 30;

    /// <summary>
    /// El rótulo visible de una parte, su <c>origin.number</c> (mensajes.md §3): <c>CAU-20261214-0007</c> en el ciclo diario, <c>VNC-…</c> en la
    /// corrección por valor nacional, <c>CIE-…</c> y <c>REA-…</c> en el cierre y su reapertura, y <c>ALD-{operación}-{n}</c> en la puesta al día
    /// de una operación. Hasta el bloque 8C llevaba el nombre del tipo (<c>AjusteDeCausacionPorFechaValor-20261214-1</c>), que no cabe en los 30
    /// caracteres de la columna: InMemory no lo nota y PostgreSQL y SQL Server rechazaban el guardado de la tanda.
    /// </summary>
    public static string Rotulo(OrigenDePartes origen, DateOnly dia, int parte)
    {
        ArgumentNullException.ThrowIfNull(origen);
        var rotulo = origen.Fuente switch
        {
            AccountingFactPartSource.LoanCatchUp when origen.Disparadora is { } op => string.Create(CultureInfo.InvariantCulture, $"ALD-{op.Number}-{parte}"),
            AccountingFactPartSource.NationalValueCorrectionBatch => string.Create(CultureInfo.InvariantCulture, $"VNC-{dia:yyyyMMdd}-{parte:0000}"),
            AccountingFactPartSource.MonthlyClose => string.Create(CultureInfo.InvariantCulture, $"CIE-{dia:yyyyMMdd}-{parte:0000}"),
            AccountingFactPartSource.CloseReopening => string.Create(CultureInfo.InvariantCulture, $"REA-{dia:yyyyMMdd}-{parte:0000}"),
            _ => string.Create(CultureInfo.InvariantCulture, $"CAU-{dia:yyyyMMdd}-{parte:0000}"),
        };
        return rotulo.Length <= LargoDelRotulo ? rotulo : rotulo[..LargoDelRotulo];
    }

    private static string Huella<T>(T lista)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(lista, Json);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static object Contenido(string tipo, OrigenDePartes origen, DateOnly dia, int tanda, int parte, IReadOnlyList<LoanFactTotalV1> totales,
        IReadOnlyList<LoanFactLineV1> lineas, decimal control, CorrectedNationalValueV1? valorCorregido)
    {
        var corrida = origen.Fuente == AccountingFactPartSource.LoanCatchUp ? null : origen.Corrida;
        var deCorrida = corrida?.Kind.ToString();
        var puesta = origen.Fuente == AccountingFactPartSource.LoanCatchUp && origen.Disparadora is { } op
            ? new CatchUpOperationV1 { OperationPublicId = op.PublicId, DocumentClass = op.Kind.ToString(), Number = op.Number }
            : null;
        return tipo switch
        {
            InteresCausadoV1.Type => new InteresCausadoV1
            {
                RunKind = deCorrida, RunPublicId = corrida?.PublicId, FactDate = dia, BatchNumber = tanda, PartNumber = parte, CatchUpOperation = puesta,
                Totals = totales, Lines = lineas, LineCount = lineas.Count, ControlTotal = control,
            },
            MoraCausadaV1.Type => new MoraCausadaV1
            {
                RunKind = deCorrida, RunPublicId = corrida?.PublicId, FactDate = dia, BatchNumber = tanda, PartNumber = parte, CatchUpOperation = puesta,
                Totals = totales, Lines = lineas, LineCount = lineas.Count, ControlTotal = control,
            },
            SeguroCausadoV1.Type => new SeguroCausadoV1
            {
                RunKind = deCorrida, RunPublicId = corrida?.PublicId, FactDate = dia, BatchNumber = tanda, PartNumber = parte, CatchUpOperation = puesta,
                Totals = totales, Lines = lineas, LineCount = lineas.Count, ControlTotal = control,
            },
            AjusteDeCausacionPorFechaValorV1.Type => new AjusteDeCausacionPorFechaValorV1
            {
                RunKind = deCorrida, RunPublicId = corrida?.PublicId, FactDate = dia, BatchNumber = tanda, PartNumber = parte, CatchUpOperation = puesta,
                Cause = valorCorregido is null ? CausaPorOperacion : CausaPorCorreccion, CorrectedNationalValue = valorCorregido,
                Totals = totales, Lines = lineas, LineCount = lineas.Count, ControlTotal = control,
            },
            _ => throw new ArgumentException($"«{tipo}» no es un hecho masivo de la causación.", nameof(tipo)),
        };
    }

    /// <summary>La causa del ajuste provocado por una operación fechada atrás (§10.5).</summary>
    public const string CausaPorOperacion = "ValueDatedOperation";

    /// <summary>La causa del ajuste de la corrida <c>NationalValueCorrection</c> (§10.5).</summary>
    public const string CausaPorCorreccion = "NationalValueCorrection";
}
