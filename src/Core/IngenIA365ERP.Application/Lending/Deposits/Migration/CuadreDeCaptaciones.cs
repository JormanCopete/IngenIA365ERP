using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Deposits.Migration;

/// <summary>
/// El cuadre de la carga de aportes o captaciones contra Contabilidad (feature 014, US9, T154; FR-115; plantillas-y-archivos.md §4.9; contabilidad.md §8.1):
/// arma, por cada cuenta de las tandas vivas, las combinaciones de sus saldos al corte con la <b>misma</b> clave de la conciliación de captaciones
/// (<see cref="SaldosContablesDeCaptaciones.Clave"/>: rol del concepto, línea, naturaleza, clase, clase de aporte, marca —exenta del GMF si la carga la marca,
/// estándar si no— y la sucursal contable de su oficina) y las compara <b>por conjunto de cuentas</b> con
/// <see cref="IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync"/> al corte (<see cref="CuadreDeMigracion.CompararAsync"/>, el mismo cálculo de los
/// créditos). La Cartera no resuelve cuentas: muestra el conjunto que devolvió Contabilidad. El saldo inicial que nacerá al activar es exactamente lo que se
/// cuadra aquí, así que después de activar la conciliación de captaciones da lo mismo. Sin contabilidad iniciada, <c>Lending.Accounting.NotStarted</c>.
/// (nuevo)
/// </summary>
public sealed class CuadreDeCaptaciones(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
{
    /// <summary>Las combinaciones de la carga con su saldo de Cartera al corte.</summary>
    public static IReadOnlyDictionary<ClaveDeSaldoDeCartera, decimal> Combinaciones(FilasDeLaCarga filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var exentas = filas.Exenciones.Select(e => e.Fila.SourceAccountNumber).ToHashSet(StringComparer.Ordinal);
        var resultado = new Dictionary<ClaveDeSaldoDeCartera, decimal>();
        foreach (var c in filas.Cuentas)
        {
            var marca = c.Numero is { } n && exentas.Contains(n) ? AccountFlag.GmfExempt : AccountFlag.Standard;
            foreach (var (concepto, monto) in c.Saldos)
            {
                if (monto == 0m) continue;
                var clave = SaldosContablesDeCaptaciones.Clave(concepto, c.Inactiva, c.LineaCodigo, c.Naturaleza, c.Clase, marca, c.OficinaPublicId);
                if (clave is null) continue;
                resultado[clave] = resultado.GetValueOrDefault(clave) + monto;
            }
        }
        return resultado;
    }

    public async Task<Result<CuadreCalculado>> CalcularAsync(FilasDeLaCarga filas, CancellationToken ct) =>
        await CuadreDeMigracion.CompararAsync(contabilidad, filas.Carga.CutoffDate, Combinaciones(filas), ct);

    /// <summary>Las diferencias <c>BalanceCheck</c> de una versión (sin guardar), como en la 013.</summary>
    public IReadOnlyList<MigrationLoadDifference> Registrar(MigrationLoad carga, int version, CuadreCalculado cuadre) =>
        new CuadreDeMigracion(db, contabilidad).Registrar(carga, version, cuadre);

    /// <summary>
    /// El recálculo al activar (FR-115, como la 013): con los saldos contables de este momento vuelve a calcular la huella. Si es la aceptada, no cambia nada;
    /// si cambió, nace la versión siguiente con sus diferencias y la carga la toma como vigente (la aceptación anterior deja de valer). No guarda.
    /// </summary>
    public async Task<Result<RecalculoDeActivacion>> RecalcularAsync(MigrationLoad carga, CancellationToken ct)
    {
        var filas = await FilasDeLaCarga.LeerAsync(db, carga, ct);
        var calculado = await CalcularAsync(filas, ct);
        if (calculado.IsFailure) return Result.Failure<RecalculoDeActivacion>(calculado.Error);
        if (string.Equals(calculado.Value.Huella, carga.BalanceCheckSha256, StringComparison.Ordinal))
        {
            var pendientes = (await ConciliacionDeVentasDeMigracion.VigentesAsync(db, carga, carga.CurrentCheckVersion, ct)).Count(d => !d.Aceptada);
            return Result.Success(new RecalculoDeActivacion(false, carga.CurrentCheckVersion, pendientes));
        }
        var nueva = carga.CurrentCheckVersion + 1;
        var diferencias = Registrar(carga, nueva, calculado.Value);
        carga.SellarCuadre(nueva, calculado.Value.InstantaneaJson, calculado.Value.Huella, diferencias.Count == 0);
        return Result.Success(new RecalculoDeActivacion(true, nueva, diferencias.Count));
    }
}

/// <summary>El cuadre de la carga de aportes o captaciones (api.md §16): la instantánea de la versión vigente, sus diferencias y las cifras de referencia. (nuevo)</summary>
public sealed record DepositMigrationBalanceCheckDto(
    Guid LoadPublicId,
    LineNature Nature,
    DateOnly CutoffDate,
    int CheckVersion,
    bool IsBalanced,
    IReadOnlyList<FilaDeCuadreDto> Rows,
    IReadOnlyList<MigrationDifferenceDto> Differences,
    IReadOnlyList<CotejoDeCifraDto> ReferenceFigures);

/// <summary>
/// El cuadre de la carga (feature 014, US9, T154; api.md §16, <c>GET /api/lending/deposits/migration/loads/{id}/balance-check</c>,
/// <c>Lending.Reconciliation.View</c>): la instantánea de la versión vigente con sus diferencias y su aceptación, y como segunda columna de cotejo las cifras
/// de referencia (FR-118). No trae titulares ni documentos. Sin contabilidad iniciada, <c>Lending.Accounting.NotStarted</c>. La carga es de toda la
/// cooperativa (sin alcance de oficina ni de línea). (nuevo)
/// </summary>
public sealed record GetDepositMigrationBalanceCheckQuery(Guid MigrationLoadPublicId) : IRequest<Result<DepositMigrationBalanceCheckDto>>;

public sealed class GetDepositMigrationBalanceCheckQueryValidator : AbstractValidator<GetDepositMigrationBalanceCheckQuery>
{
    public GetDepositMigrationBalanceCheckQueryValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

public sealed class GetDepositMigrationBalanceCheckQueryHandler(IApplicationDbContext db, IContabilidadParaCartera contabilidad)
    : IRequestHandler<GetDepositMigrationBalanceCheckQuery, Result<DepositMigrationBalanceCheckDto>>
{
    public async Task<Result<DepositMigrationBalanceCheckDto>> Handle(GetDepositMigrationBalanceCheckQuery request, CancellationToken ct)
    {
        var carga = await ConsultasDeCargasDeCaptaciones.BuscarAsync(db, request.MigrationLoadPublicId, seguimiento: false, ct);
        if (carga is null) return Result.Failure<DepositMigrationBalanceCheckDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
        var estado = await contabilidad.EstadoDelPeriodoAsync(carga.CutoffDate, ct);
        if (estado.IsFailure) return Result.Failure<DepositMigrationBalanceCheckDto>(estado.Error);
        if (!estado.Value.AccountingInitialized) return Result.Failure<DepositMigrationBalanceCheckDto>(ErroresDeMigracion.SinContabilidad());

        var leido = await CuadreDeMigracion.LeerAsync(db, carga, ct);
        var filas = await FilasDeLaCarga.LeerAsync(db, carga, ct);
        var cotejo = CifrasDeReferencia.Cotejar(filas.Totales(), CifrasDeReferencia.Leer(carga.ReferenceFiguresJson));
        return Result.Success(new DepositMigrationBalanceCheckDto(carga.PublicId, carga.Nature, carga.CutoffDate, leido.CheckVersion, leido.IsBalanced,
            leido.Rows, leido.Differences, cotejo));
    }
}
