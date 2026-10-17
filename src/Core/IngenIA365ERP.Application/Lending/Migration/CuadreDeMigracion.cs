using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Una cuenta del conjunto, sólo para mostrar (Cartera no resuelve cuentas). (nuevo)</summary>
public sealed record CuentaDelCuadreDto(string Code, string Name);

/// <summary>
/// Una fila del cuadre de la carga (api.md §19.2): la familia de roles (el concepto), la modalidad, el sub-segmento y la categoría comunes a sus
/// combinaciones (nulos si mezcla), el conjunto de cuentas que respondió Contabilidad, lo de Cartera y lo contable (en el sentido de Cartera) y la
/// diferencia; <c>Unresolved</c> si ninguna regla de la matriz la resuelve al corte. Sin datos de deudores. (nuevo)
/// </summary>
public sealed record FilaDeCuadreDto(
    string DimensionKey,
    string Family,
    string? Concept,
    string? Modality,
    string? Subsegment,
    string? Category,
    IReadOnlyList<CuentaDelCuadreDto> Accounts,
    decimal Lending,
    decimal Accounting,
    decimal Difference,
    bool Unresolved);

/// <summary>Una diferencia del cuadre o de la conciliación con su aceptación (D17). (nuevo)</summary>
public sealed record MigrationDifferenceDto(
    Guid PublicId,
    MigrationDifferenceKind Kind,
    string DimensionKey,
    string? Concept,
    string? Modality,
    string? Subsegment,
    string? Category,
    string? Accounts,
    string? InvoiceNumber,
    decimal Lending,
    decimal Accounting,
    decimal Difference,
    DateTime? AcceptedAt,
    string? AcceptanceReason);

/// <summary>El cuadre de la carga contra Contabilidad (FR-141; api.md §19.2, <c>GET …/balance-check</c>). (nuevo)</summary>
public sealed record MigrationBalanceCheckDto(
    Guid LoadPublicId,
    DateOnly CutoffDate,
    int CheckVersion,
    bool IsBalanced,
    IReadOnlyList<FilaDeCuadreDto> Rows,
    IReadOnlyList<MigrationDifferenceDto> Differences);

/// <summary>Lo que calculó un cuadre: las filas, la instantánea que se guarda en la carga y su huella. (nuevo)</summary>
public sealed record CuadreCalculado(IReadOnlyList<FilaDeCuadreDto> Filas, string InstantaneaJson, string Huella)
{
    public IEnumerable<FilaDeCuadreDto> ConDiferencia => Filas.Where(f => f.Difference != 0m);
}

/// <summary>
/// El cuadre de la carga contra Contabilidad (feature 013, US8, T0741; FR-141, T58, R27; contabilidad.md §14.3): arma las combinaciones de la carga
/// —por cada crédito de las tandas vivas su línea, modalidad, sub-segmento, categoría <b>contable al corte</b> (de <c>risk-data</c>), empleado,
/// reestructurado y la sucursal contable de su oficina, y por concepto el capital, los intereses, el suspenso, los pagos por cuenta del asociado, los
/// cargos, los anticipos, los saldos a favor y el deterioro individual; los castigados en orden; y el deterioro general por sucursal— con
/// <see cref="SaldosContablesDeCartera.Clave"/> y <see cref="SaldosContablesDeCartera.RolDelConcepto"/> (el mismo cálculo de la conciliación) y
/// las compara <b>por conjunto de cuentas</b> con <see cref="IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync"/> al corte. Cartera no resuelve
/// cuentas: muestra el conjunto que devolvió Contabilidad (<c>AccountSetDisplay</c>). Vivienda queda fuera hasta K6b (no se carga). Sin
/// contabilidad iniciada, <c>Lending.Accounting.NotStarted</c>. <see cref="Registrar"/> escribe las diferencias <c>BalanceCheck</c> de una
/// versión sin guardar. (nuevo)
/// </summary>
public sealed class CuadreDeMigracion(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// La lectura por carga, <b>sin escribir</b> ni preguntar a Contabilidad (US8, T0757): la instantánea guardada de la versión vigente con sus
    /// diferencias y su aceptación. La usan <c>GET …/balance-check</c> y la vista <c>migration-reconciliation</c> del centro de informes (C109).
    /// </summary>
    public static async Task<MigrationBalanceCheckDto> LeerAsync(IApplicationDbContext db, MigrationLoad carga, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(carga);
        var filas = string.IsNullOrWhiteSpace(carga.BalanceCheckJson) ? [] : JsonSerializer.Deserialize<List<FilaDeCuadreDto>>(carga.BalanceCheckJson, Json) ?? [];
        var diferencias = await ConsultasDeDiferencias.DeLaVersionAsync(db, carga, MigrationDifferenceKind.BalanceCheck, ct);
        return new MigrationBalanceCheckDto(carga.PublicId, carga.CutoffDate, carga.CurrentCheckVersion, carga.IsBalanced, filas, diferencias);
    }

    public async Task<Result<CuadreCalculado>> CalcularAsync(MigrationLoad carga, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        var combinaciones = await CombinacionesAsync(carga, ct);
        if (combinaciones.Count == 0) return Result.Success(Vacio());

        var estado = await contabilidad.EstadoDelPeriodoAsync(carga.CutoffDate, ct);
        if (estado.IsFailure) return Result.Failure<CuadreCalculado>(estado.Error);
        if (!estado.Value.AccountingInitialized) return Result.Failure<CuadreCalculado>(ErroresDeMigracion.SinContabilidad());

        var claves = combinaciones.Keys.ToList();
        var mapeados = await contabilidad.SaldosDeCuentasMapeadasAsync(carga.CutoffDate, claves.Select(c => c.ComoCombinacion()).ToList(), ct);
        if (mapeados.IsFailure)
            return mapeados.Error.Code == "Accounting.NotInitialized"
                ? Result.Failure<CuadreCalculado>(ErroresDeMigracion.SinContabilidad())
                : Result.Failure<CuadreCalculado>(mapeados.Error);

        var filas = new List<FilaDeCuadreDto>();
        foreach (var set in mapeados.Value.Sets)
        {
            var deEste = set.Combinations.Select(i => claves[i]).ToList();
            if (deEste.Count == 0) continue;
            var rol = deEste[0].Rol;
            var cuentas = set.Accounts.Select(a => new CuentaDelCuadreDto(a.AccountCode, a.AccountName)).OrderBy(a => a.Code, StringComparer.Ordinal).ToList();
            var contable = set.Accounts.Sum(a => a.BalanceByBranch.Sum(b => b.Balance)) * SaldosContablesDeCartera.Signo(rol);
            var deCartera = deEste.Sum(c => combinaciones[c]);
            filas.Add(new FilaDeCuadreDto(Llave(set.RoleFamily, cuentas.Select(c => c.Code)), set.RoleFamily, Comun(deEste, c => c.Concepto) ?? rol,
                Comun(deEste, c => c.Modalidad), Comun(deEste, c => c.Subsegmento), Comun(deEste, c => c.Categoria), cuentas, deCartera, contable,
                deCartera - contable, Unresolved: false));
        }
        foreach (var i in mapeados.Value.UnresolvedCombinations)
        {
            var c = claves[i];
            filas.Add(new FilaDeCuadreDto(LlaveSinRegla(c), c.Rol, c.Concepto ?? c.Rol,
                c.Modalidad, c.Subsegmento, c.Categoria, [], combinaciones[c], 0m, combinaciones[c], Unresolved: true));
        }

        var ordenadas = filas.OrderBy(f => f.DimensionKey, StringComparer.Ordinal).ToList();
        var json = JsonSerializer.Serialize(ordenadas, Json);
        return Result.Success(new CuadreCalculado(ordenadas, json, Huella(json)));
    }

    /// <summary>Las diferencias <c>BalanceCheck</c> de una versión (sin guardar): una por fila con diferencia.</summary>
    public IReadOnlyList<MigrationLoadDifference> Registrar(MigrationLoad carga, int version, CuadreCalculado cuadre)
    {
        var nuevas = cuadre.ConDiferencia.Select(f => new MigrationLoadDifference
        {
            MigrationLoadId = carga.Id,
            CheckVersion = version,
            Kind = MigrationDifferenceKind.BalanceCheck,
            DimensionKey = f.DimensionKey.Length <= 300 ? f.DimensionKey : f.DimensionKey[..236] + "#" + Huella(f.DimensionKey),
            ConceptFamily = Recortar(f.Concept ?? f.Family, 40),
            ModalityCode = Recortar(f.Modality, 40),
            SubsegmentCode = Recortar(f.Subsegment, 40),
            RiskCategory = Enum.TryParse<RatingCategory>(f.Category, out var cat) ? cat : null,
            AccountSetKey = f.Accounts.Count == 0 ? null : Recortar(string.Join(",", f.Accounts.Select(a => a.Code)), 200),
            AccountSetDisplay = f.Accounts.Count == 0 ? null : Recortar(string.Join("; ", f.Accounts.Select(a => $"{a.Code} {a.Name}")), 1000),
            LendingAmount = f.Lending,
            AccountingAmount = f.Accounting,
            Difference = f.Difference,
        }).ToList();
        db.MigrationLoadDifferences.AddRange(nuevas);
        return nuevas;
    }

    public static CuadreCalculado Vacio() => new([], "[]", ApplyMigrationLoadCommandHandler.HuellaVacia);

    public static string Huella(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    /// <summary>
    /// La llave de una combinación que ninguna regla resuelve: <b>todas</b> sus dimensiones. Hasta el bloque 13C omitía empleado, gracia,
    /// reestructurado y clase de recaudador, y dos créditos de la misma línea y oficina, uno de un empleado y otro no, daban la misma llave y
    /// aplicar la carga caía en <c>UK_LND_MigrationLoadDifferences_Load_Version_Dim</c> (500).
    /// </summary>
    public static string LlaveSinRegla(ClaveDeSaldoDeCartera c)
    {
        ArgumentNullException.ThrowIfNull(c);
        static string B(bool? v) => v is null ? string.Empty : v.Value ? "1" : "0";
        var llave = $"SinRegla|{c.Rol}|{c.Linea}|{c.Modalidad}|{c.Subsegmento}|{c.Categoria}|{c.Concepto}|{B(c.Empleado)}|{B(c.Gracia)}|{B(c.Reestructurado)}|"
                    + $"{c.ClaseDeRecaudador}|{c.Motivo}|{c.Sucursal:N}";
        return llave.Length <= 300 ? llave : llave[..236] + "#" + Huella(llave);
    }

    private static string Llave(string familia, IEnumerable<string> cuentas)
    {
        var llave = $"{familia}|{string.Join(",", cuentas)}";
        return llave.Length <= 300 ? llave : llave[..236] + "#" + Huella(llave);
    }

    private static string? Recortar(string? texto, int largo) => texto is null || texto.Length <= largo ? texto : texto[..largo];

    private static string? Comun(IReadOnlyList<ClaveDeSaldoDeCartera> claves, Func<ClaveDeSaldoDeCartera, string?> campo)
    {
        var valores = claves.Select(campo).Distinct(StringComparer.Ordinal).ToList();
        return valores.Count == 1 ? valores[0] : null;
    }

    // ------------------------------------------------------------------------------------------------- combinaciones --

    /// <summary>Las combinaciones de la carga (tandas vivas) con su saldo de Cartera al corte.</summary>
    private async Task<Dictionary<ClaveDeSaldoDeCartera, decimal>> CombinacionesAsync(MigrationLoad carga, CancellationToken ct)
    {
        var resultado = new Dictionary<ClaveDeSaldoDeCartera, decimal>();
        var partes = db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id);
        var creditos = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var castigados = await db.MigrationChargedOffRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        if (creditos.Count == 0 && castigados.Count == 0) return resultado;

        var saldos = (await db.MigrationBalanceRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var riesgo = (await db.MigrationRiskDataRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct))
            .GroupBy(r => r.SourceLoanNumber, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var reestructurados = (await db.MigrationRestructuringRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId))
            .Select(r => r.SourceLoanNumber).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

        var codigos = creditos.Select(c => c.CreditLineCode).Concat(castigados.Select(c => c.CreditLineCode)).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().Where(l => codigos.Contains(l.Code)).Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), carga.CutoffDate, ct: ct);
        var modalidadDe = lineas.ToDictionary(l => l.Code, l => versiones.TryGetValue(l.Id, out var v) ? (v.Version.ModalityCode, v.Version.SubsegmentCode) : (null, null),
            StringComparer.OrdinalIgnoreCase);
        var personas = creditos.Select(c => c.PersonId).Concat(castigados.Select(c => c.PersonId)).Distinct().ToList();
        var empleados = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.IsEmployee, ct);
        var oficinas = creditos.Select(c => c.OfficePublicId).Concat(castigados.Select(c => c.OfficePublicId)).Distinct().ToList();
        var sucursales = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.PublicId))
            .ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);

        void Sumar(ClaveDeSaldoDeCartera clave, decimal monto)
        {
            if (monto == 0m) return;
            resultado[clave] = resultado.GetValueOrDefault(clave) + monto;
        }

        foreach (var c in creditos)
        {
            var (modalidad, subsegmento) = modalidadDe.GetValueOrDefault(c.CreditLineCode);
            // D18: fuera del cuadre hasta K6b; desde K6b (T1055, FR-141) la vivienda entra por concepto, modalidad, sub-segmento, categoría y cuenta.
            if (string.Equals(modalidad, "VIVIENDA", StringComparison.OrdinalIgnoreCase) && !Housing.LectorDeVivienda.Disponible()) continue;
            var r = riesgo.GetValueOrDefault(c.SourceLoanNumber);
            var categoria = r is null ? RatingCategory.A : HomologacionDeCalificacion.Contable(r.Rating, r.Subcategory);
            var d = new DimensionesDeCreditoDeCartera(c.CreditLineCode, modalidad, subsegmento, categoria.ToString(), empleados.GetValueOrDefault(c.PersonId),
                reestructurados.Contains(c.SourceLoanNumber), c.OfficePublicId, sucursales.GetValueOrDefault(c.OfficePublicId));
            foreach (var s in saldos[c.SourceLoanNumber])
                Sumar(SaldosContablesDeCartera.Clave(d, SaldosContablesDeCartera.RolDelConcepto(s.Concept, enControl: false), s.Concept), s.Amount);
            if (r is null) continue;
            Sumar(SaldosContablesDeCartera.Clave(d, "DeterioroIndividual", LoanConcept.Principal), r.PrincipalImpairment);
            Sumar(SaldosContablesDeCartera.Clave(d, "DeterioroIndividual", LoanConcept.CurrentInterest), r.InterestImpairment);
            Sumar(SaldosContablesDeCartera.Clave(d, "DeterioroIndividual", LoanConcept.OtherCharge), r.OtherImpairment);
            Sumar(new ClaveDeSaldoDeCartera("DeterioroGeneral", null, null, null, null, null, null, null, null, null, "General", c.OfficePublicId),
                r.GeneralImpairment);
        }

        foreach (var c in castigados)
        {
            var (modalidad, subsegmento) = modalidadDe.GetValueOrDefault(c.CreditLineCode);
            var d = new DimensionesDeCreditoDeCartera(c.CreditLineCode, modalidad, subsegmento, nameof(RatingCategory.E), empleados.GetValueOrDefault(c.PersonId),
                false, c.OfficePublicId, sucursales.GetValueOrDefault(c.OfficePublicId));
            Sumar(SaldosContablesDeCartera.Clave(d, "CastigadaEnOrden", LoanConcept.Principal), c.PrincipalBalance);
            Sumar(SaldosContablesDeCartera.Clave(d, "CastigadaEnOrden", LoanConcept.CurrentInterest), c.InterestBalance);
            Sumar(SaldosContablesDeCartera.Clave(d, "CastigadaEnOrden", LoanConcept.OtherCharge), c.OtherBalance);
        }
        return resultado;
    }
}

/// <summary>
/// El cuadre de la carga (feature 013, US8, T0741; api.md §19.2, <c>GET /api/lending/migration/loads/{id}/balance-check</c>,
/// <c>Lending.Reconciliation.View</c>, C85): la instantánea de la versión vigente con sus diferencias y su aceptación. No trae documentos ni
/// nombres. Sin contabilidad iniciada, <c>Lending.Accounting.NotStarted</c>. (nuevo)
/// </summary>
public sealed record GetMigrationBalanceCheckQuery(Guid MigrationLoadPublicId) : IRequest<Result<MigrationBalanceCheckDto>>;

public sealed class GetMigrationBalanceCheckQueryValidator : AbstractValidator<GetMigrationBalanceCheckQuery>
{
    public GetMigrationBalanceCheckQueryValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class GetMigrationBalanceCheckQueryHandler(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
    : IRequestHandler<GetMigrationBalanceCheckQuery, Result<MigrationBalanceCheckDto>>
{
    public async Task<Result<MigrationBalanceCheckDto>> Handle(GetMigrationBalanceCheckQuery request, CancellationToken ct)
    {
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<MigrationBalanceCheckDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        var estado = await contabilidad.EstadoDelPeriodoAsync(carga.CutoffDate, ct);
        if (estado.IsFailure) return Result.Failure<MigrationBalanceCheckDto>(estado.Error);
        if (!estado.Value.AccountingInitialized) return Result.Failure<MigrationBalanceCheckDto>(ErroresDeMigracion.SinContabilidad());

        return Result.Success(await CuadreDeMigracion.LeerAsync(db, carga, ct));
    }
}

/// <summary>Las diferencias de la versión vigente de una carga, como DTO. (nuevo)</summary>
internal static class ConsultasDeDiferencias
{
    public static async Task<IReadOnlyList<MigrationDifferenceDto>> DeLaVersionAsync(IApplicationDbContext db, MigrationLoad carga,
        MigrationDifferenceKind? clase, CancellationToken ct) =>
        await db.MigrationLoadDifferences.AsNoTracking()
            .Where(d => d.MigrationLoadId == carga.Id && d.CheckVersion == carga.CurrentCheckVersion && !d.IsDeleted && (clase == null || d.Kind == clase))
            .OrderBy(d => d.Kind).ThenBy(d => d.DimensionKey)
            .Select(d => new MigrationDifferenceDto(d.PublicId, d.Kind, d.DimensionKey, d.ConceptFamily, d.ModalityCode, d.SubsegmentCode,
                d.RiskCategory == null ? null : d.RiskCategory.ToString(), d.AccountSetDisplay, d.InvoiceNumber, d.LendingAmount, d.AccountingAmount,
                d.Difference, d.AcceptedAt, d.AcceptanceReason))
            .ToListAsync(ct);
}
