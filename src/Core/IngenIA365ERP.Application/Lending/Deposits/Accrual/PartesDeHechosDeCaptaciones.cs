using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>Lo causado (o ajustado) de una cuenta en un día valor, con sus datos de hecho, tal como va a una parte (seis decimales). (nuevo)</summary>
public sealed record LineaDeCausacionDeCuenta(HechoDeCuentaMarcada Hecho, DateOnly Dia, decimal Monto);

/// <summary>
/// De dónde salen las partes de captaciones (mensajes.md §2.2): la fuente (el ciclo diario o la puesta al día de una operación), la corrida (nula en la
/// puesta al día), la operación que pone al día, la tanda y a nombre de quién van si actúa el proceso. (nuevo)
/// </summary>
public sealed record OrigenDePartesDeCuentas(
    AccountingFactPartSource Fuente,
    LendingProcessRun? Corrida,
    MemberAccountOperation? Disparadora,
    int Tanda,
    UserRefV1? UsuarioDeOrigen);

/// <summary>Lo que dejaron las partes: las partes y el mensaje de cada (cuenta, día) para sellar las filas del libro. (nuevo)</summary>
public sealed record PartesDeCuentasEmitidas(IReadOnlyList<AccountingFactPart> Partes, IReadOnlyDictionary<(int Cuenta, DateOnly Dia), Guid> MensajePorFila)
{
    public static PartesDeCuentasEmitidas Vacias { get; } = new([], new Dictionary<(int, DateOnly), Guid>());

    public Guid? Mensaje(int cuenta, DateOnly dia) => MensajePorFila.TryGetValue((cuenta, dia), out var m) ? m : null;
}

/// <summary>
/// El armado de las partes de los hechos masivos de captaciones (feature 014, US5, T134; mensajes.md §2.2, §4.0; molde <c>PartesDeHechosDeCartera</c> de la
/// 013): una parte de <c>LND_AccountingFactParts</c> por fuente × día × sucursal contable × tipo × tanda —en la puesta al día de una operación, por sucursal
/// y tipo con una línea por cuenta y día valor— con sus totales por combinación de <c>DepositFactDimensionsV1</c>, la lista por cuenta y su huella, el total
/// de control y el rótulo <c>CCA-{aaaammdd}-{nnnn}</c> (causación), <c>AJC-…</c> (corrección del proceso) o <c>ALD-{operación}-{n}</c> (puesta al día). Lo
/// que va en cada línea es lo causado al centavo en <c>amount</c> con los seis decimales en <c>accruedAmount</c>. El mensaje se emite antes de agregar la
/// parte (la parte guarda su <c>MessagePublicId</c>). No guarda. (nuevo)
/// </summary>
public sealed class PartesDeHechosDeCaptaciones(IApplicationDbContext db, EmisionDeCartera emision)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<PartesDeCuentasEmitidas>> MasivasAsync(OrigenDePartesDeCuentas origen, string tipo, IReadOnlyList<LineaDeCausacionDeCuenta> lineas,
        string? causa = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(origen);
        if (tipo is not (InteresDeCaptacionCausadoV1.Type or AjusteDeCausacionDeCaptacionV1.Type))
            throw new ArgumentException($"«{tipo}» no es un hecho masivo de la causación de captaciones.", nameof(tipo));
        var porDia = origen.Fuente == AccountingFactPartSource.DailyCycleBatch;
        var grupos = lineas.Where(l => l.Monto != 0m)
            .GroupBy(l => (Dia: porDia ? l.Dia : DateOnly.MinValue, l.Hecho.Hecho.Cuenta.BranchId))
            .OrderBy(g => g.Key.Dia).ThenBy(g => g.Key.BranchId).ToList();
        var partes = new List<AccountingFactPart>();
        var mensajePorFila = new Dictionary<(int, DateOnly), Guid>();
        var ordinal = 0;
        foreach (var g in grupos)
        {
            var dia = porDia ? g.Key.Dia : g.Max(l => l.Dia);
            var contable = g.First().Hecho.Hecho.FechaContable;
            var numeroDeParte = Siguiente(origen, ref ordinal);
            var lineasDelHecho = g.GroupBy(l => (l.Hecho.Hecho.Cuenta.Id, Dia: porDia ? DateOnly.MinValue : l.Dia)).OrderBy(x => x.Key.Id).ThenBy(x => x.Key.Dia)
                .Select(x =>
                {
                    var h = x.First().Hecho;
                    var monto = x.Sum(l => l.Monto);
                    return new DepositFactLineV1
                    {
                        Account = h.Hecho.Referencia,
                        HolderPersonPublicId = h.Hecho.TitularPublicId,
                        Dimensions = h.Dimensiones,
                        Amounts = [Valor(monto)],
                        ValueDate = porDia ? null : x.Key.Dia,
                    };
                }).ToList();
            var totales = lineasDelHecho.GroupBy(l => l.Dimensions)
                .Select(t => new DepositFactTotalV1
                {
                    Dimensions = t.Key,
                    Amounts = [Valor(t.Sum(l => l.Amounts.Sum(a => a.AccruedAmount ?? a.Amount)))],
                    AccountCount = t.Select(l => l.Account.AccountPublicId).Distinct().Count(),
                }).ToList();
            var control = lineasDelHecho.Sum(l => l.Amounts.Sum(a => a.AccruedAmount ?? a.Amount));
            var partePublicId = Guid.NewGuid();
            var corrida = origen.Fuente == AccountingFactPartSource.AccountCatchUp ? null : origen.Corrida;
            var puesta = origen.Fuente == AccountingFactPartSource.AccountCatchUp && origen.Disparadora is { } op
                ? new CatchUpOperationV1 { OperationPublicId = op.PublicId, DocumentClass = op.Kind.ToString(), Number = op.Number }
                : null;
            object contenido = tipo == InteresDeCaptacionCausadoV1.Type
                ? new InteresDeCaptacionCausadoV1
                {
                    RunKind = corrida?.Kind.ToString(), RunPublicId = corrida?.PublicId, CatchUpOperation = puesta, FactDate = dia, BatchNumber = origen.Tanda,
                    PartNumber = numeroDeParte, Totals = totales, Lines = lineasDelHecho, LineCount = lineasDelHecho.Count, ControlTotal = control,
                }
                : new AjusteDeCausacionDeCaptacionV1
                {
                    RunKind = corrida?.Kind.ToString(), RunPublicId = corrida?.PublicId, CatchUpOperation = puesta, FactDate = dia, BatchNumber = origen.Tanda,
                    PartNumber = numeroDeParte, Totals = totales, Lines = lineasDelHecho, LineCount = lineasDelHecho.Count, ControlTotal = control,
                    Cause = causa ?? EmisionDeCartera.CausaCorreccionDeTasa,
                };
            var primero = g.First().Hecho.Hecho;
            var emitido = await emision.HechoDeParteAsync(new EmisionDeParte(partePublicId, Rotulo(origen, tipo, dia, numeroDeParte), contable,
                primero.SucursalPublicId, null, null, porDia ? dia : null, origen.UsuarioDeOrigen), tipo, contenido, ct);
            if (emitido.IsFailure) return Result.Failure<PartesDeCuentasEmitidas>(emitido.Error);

            var parte = new AccountingFactPart
            {
                PublicId = partePublicId,
                SourceKind = origen.Fuente,
                ProcessRunId = corrida?.Id,
                BatchNumber = corrida is null ? null : origen.Tanda,
                TriggerAccountOperationId = origen.Fuente == AccountingFactPartSource.AccountCatchUp ? origen.Disparadora?.Id : null,
                FactType = tipo,
                PerLoanMessages = false,
                ValueDate = dia,
                AccountingDate = contable,
                BranchId = g.Key.BranchId,
                OriginEventKey = ClavesDeEvento.ConfirmacionPor(partePublicId),
                MessagePublicId = emitido.Value.PublicId,
                MessageCount = 1,
                LoanCount = 0,
                AccountCount = lineasDelHecho.Select(l => l.Account.AccountPublicId).Distinct().Count(),
                TotalAmount = decimal.Round(control, 2, MidpointRounding.AwayFromZero),
                TotalsByDimensionJson = JsonSerializer.Serialize(totales, Json),
                LoanListSha256 = Huella(lineasDelHecho.Select(l => l.Account).ToList()),
            };
            db.AccountingFactParts.Add(parte);
            partes.Add(parte);
            foreach (var l in g) mensajePorFila[(l.Hecho.Hecho.Cuenta.Id, l.Dia)] = emitido.Value.PublicId;
        }
        return Result.Success(new PartesDeCuentasEmitidas(partes, mensajePorFila));
    }

    /// <summary>Un valor del interés por pagar: al centavo en <c>amount</c> y con sus seis decimales en <c>accruedAmount</c>.</summary>
    public static DepositConceptAmountV1 Valor(decimal monto) => new()
    {
        Concept = nameof(MemberAccountConcept.InterestPayable), Amount = decimal.Round(monto, 2, MidpointRounding.AwayFromZero), AccruedAmount = monto,
    };

    /// <summary>El rótulo de una parte (<c>origin.number</c>, a lo sumo 30 caracteres).</summary>
    public static string Rotulo(OrigenDePartesDeCuentas origen, string tipo, DateOnly dia, int parte)
    {
        ArgumentNullException.ThrowIfNull(origen);
        var prefijo = tipo == AjusteDeCausacionDeCaptacionV1.Type ? "AJC" : "CCA";
        var rotulo = origen.Fuente == AccountingFactPartSource.AccountCatchUp && origen.Disparadora is { } op
            ? string.Create(CultureInfo.InvariantCulture, $"ALD-{op.Number}-{parte}")
            : string.Create(CultureInfo.InvariantCulture, $"{prefijo}-{dia:yyyyMMdd}-{parte:0000}");
        return rotulo.Length <= Lending.Accrual.PartesDeHechosDeCartera.LargoDelRotulo ? rotulo : rotulo[..Lending.Accrual.PartesDeHechosDeCartera.LargoDelRotulo];
    }

    private static int Siguiente(OrigenDePartesDeCuentas origen, ref int ordinal)
    {
        if (origen.Corrida is { } corrida && origen.Fuente != AccountingFactPartSource.AccountCatchUp)
        {
            corrida.PartCount += 1;
            return corrida.PartCount;
        }
        ordinal += 1;
        return ordinal;
    }

    private static string Huella<T>(T lista) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(lista, Json)));
}
