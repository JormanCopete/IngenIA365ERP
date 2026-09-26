using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// Lo que Inventario le aporta a la plataforma de parámetros y de aprobaciones (feature 012; US1 T226, US3 T286; FR-012, FR-075;
/// contracts/api.md §7, §15.1; data-model §4.1; T21, T33). Reemplaza en el contenedor a <see cref="ResolutorDeAmbitoVacio"/>,
/// <see cref="ReglasDeParametrosVacias"/> y <see cref="ReglasDePoliticaDeAprobacionVacias"/>. (nuevo)
/// <list type="bullet">
/// <item><see cref="IResolutorDeAmbitoDeParametro"/>: la bodega (<see cref="ParameterScopeKind.Warehouse"/>, US1) dentro del alcance
/// de quien pide y el tipo de documento (<see cref="ParameterScopeKind.DocumentType"/>, US3); inexistente o fuera, el 404 de su
/// catálogo. Punto y caja los suma US5.</item>
/// <item><see cref="IReglasDeParametros"/>: (1) ninguna vigencia empieza dentro de un período de inventario cerrado
/// (<c>Parameters.ValidFromInClosedPeriod</c> con <c>lastClosedDate</c>); (2) <c>Costeo.Metodo</c> y <c>Costeo.Ambito</c> sólo
/// desde el primer día de un período abierto sin kardex en o después de esa fecha (<c>Parameters.RequiresPeriodStart</c> con
/// <c>earliestAllowed</c>; <c>Peps</c> ya lo rechaza la definición hasta I5 con <c>Parameters.ValueNotAllowed</c>); (3)
/// <c>Contabilidad.ModoDePaso</c> por cadena —un tipo encadenado no admite modo propio (<c>Inventory.PostingMode.ChainMismatch</c>)
/// y con <c>chain</c> se escribe una vigencia por tipo activo de la cadena— y dejar sin paso un tipo fiscal exige
/// <c>Inventory.DocumentTypes.DisableFiscalPosting</c> y <c>confirmFiscalWithoutPosting</c>
/// (<c>Inventory.PostingMode.FiscalRequiresConfirmation</c> con <c>fiscalDocumentTypes</c>).</item>
/// <item><see cref="IReglasDePoliticaDeAprobacion"/>: el tipo existe y se describe; la versión no empieza en un período cerrado
/// (<c>Approvals.Policy.ValidFromInClosedPeriod</c>); y los tipos que siempre se aprueban no admiten una política vacía
/// (<c>Approvals.Policy.RequiredForClass</c>): el de saldo inicial y los de ajuste de conteo, que se reconocen porque su política
/// vigente tiene un nivel con <see cref="PermisoDeConteo"/> (lo siembra US11 así, api.md §12).</item>
/// </list>
/// </summary>
public sealed class ReglasDePlataformaDeInventario(
    IApplicationDbContext db,
    IAlcanceDeInventario alcanceDeLaPeticion,
    ILectorDeParametros? parametros = null,
    IPermissionChecker? permisos = null)
    : IResolutorDeAmbitoDeParametro, IReglasDeParametros, IReglasDePoliticaDeAprobacion
{
    /// <summary>Dejar sin paso a contabilidad un tipo fiscal (api.md §7).</summary>
    public const string PermisoDeFiscalSinPaso = "Inventory.DocumentTypes.DisableFiscalPosting";

    /// <summary>El permiso del nivel sembrado en los tipos de ajuste de conteo (api.md §12): los marca como «siempre se aprueban».</summary>
    public const string PermisoDeConteo = "Inventory.Counts.Approve";

    // ------------------------------------------------------------------------------------------- ámbitos --

    public async Task<Result<AmbitoDeParametro>> ResolverAsync(ParameterScopeKind ambito, Guid publicId, CancellationToken ct)
    {
        switch (ambito)
        {
            case ParameterScopeKind.Warehouse:
            {
                var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
                var bodega = await db.Warehouses.AsNoTracking().Where(w => w.PublicId == publicId)
                    .Select(w => new { w.Id, w.PublicId, w.Code, w.Name }).FirstOrDefaultAsync(ct);
                return bodega is null || !alcance.IncluyeBodega(bodega.Id)
                    ? Result.Failure<AmbitoDeParametro>(ErroresDeAlcance.BodegaInexistente())
                    : Result.Success(new AmbitoDeParametro(ParameterScopeKind.Warehouse, bodega.Id, bodega.PublicId, bodega.Code, bodega.Name));
            }
            case ParameterScopeKind.DocumentType:
            {
                var tipo = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.PublicId == publicId)
                    .Select(t => new { t.Id, t.PublicId, t.Code, t.Name }).FirstOrDefaultAsync(ct);
                return tipo is null
                    ? Result.Failure<AmbitoDeParametro>(InventoryErrors.DocumentTypeNotFound())
                    : Result.Success(new AmbitoDeParametro(ParameterScopeKind.DocumentType, tipo.Id, tipo.PublicId, tipo.Code, tipo.Name));
            }
            default:
                return Result.Failure<AmbitoDeParametro>(Error.NotFound);
        }
    }

    public async Task<IReadOnlyList<AmbitoDeParametro>> DescribirAsync(ParameterScopeKind ambito, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        switch (ambito)
        {
            case ParameterScopeKind.Warehouse:
            {
                var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
                var bodegas = await db.Warehouses.AsNoTracking().Where(w => ids.Contains(w.Id))
                    .Select(w => new { w.Id, w.PublicId, w.Code, w.Name }).ToListAsync(ct);
                return bodegas.Where(b => alcance.IncluyeBodega(b.Id))
                    .Select(b => new AmbitoDeParametro(ParameterScopeKind.Warehouse, b.Id, b.PublicId, b.Code, b.Name))
                    .ToList();
            }
            case ParameterScopeKind.DocumentType:
                return await db.InventoryDocumentTypes.AsNoTracking().Where(t => ids.Contains(t.Id))
                    .Select(t => new AmbitoDeParametro(ParameterScopeKind.DocumentType, t.Id, t.PublicId, t.Code, t.Name))
                    .ToListAsync(ct);
            default:
                return [];
        }
    }

    // ---------------------------------------------------------------------------------------- parámetros --

    public async Task<Result<DecisionDeReglasDeParametro>> EvaluarAsync(AltaDeParametro alta, CancellationToken ct)
    {
        var corte = await CorteAsync(ct);
        if (corte?.LastClosedDate is { } cerrado && alta.ValidFrom <= cerrado)
            return Result.Failure<DecisionDeReglasDeParametro>(ErroresDeParametros.EnPeriodoCerrado(cerrado));

        if (alta.Definicion.Modulo != ParametrosDeInventario.Modulo) return Result.Success(DecisionDeReglasDeParametro.Adelante);

        switch (alta.Definicion.Clave)
        {
            case ParametrosDeInventario.CosteoMetodo:
            case ParametrosDeInventario.CosteoAmbito:
                var inicio = await InicioDePeriodoAsync(alta.Definicion.Clave, alta.ValidFrom, corte?.LastClosedDate, ct);
                return inicio.IsFailure ? Result.Failure<DecisionDeReglasDeParametro>(inicio.Error) : Result.Success(DecisionDeReglasDeParametro.Adelante);
            case ParametrosDeInventario.ContabilidadModoDePaso:
                return await ModoDePasoAsync(alta, ct);
            default:
                return Result.Success(DecisionDeReglasDeParametro.Adelante);
        }
    }

    /// <summary>
    /// (2) El método y el ámbito de costo cambian sólo desde el primer día de un período abierto sin kardex en o después de esa
    /// fecha: si no, la primera fecha posible —el primer día del mes siguiente al último cerrado, al último movimiento o a la
    /// fecha pedida, la mayor—.
    /// </summary>
    private async Task<Result> InicioDePeriodoAsync(string clave, DateOnly desde, DateOnly? ultimoCierre, CancellationToken ct)
    {
        var ultimoMovimiento = await db.KardexEntries.AsNoTracking().MaxAsync(k => (DateOnly?)k.OperationDate, ct);
        var esInicio = desde.Day == 1
            && (ultimoCierre is null || desde > ultimoCierre)
            && (ultimoMovimiento is null || desde > ultimoMovimiento);
        if (esInicio) return Result.Success();

        var primera = PrimeroDelMes(desde.Day == 1 ? desde : desde.AddMonths(1));
        if (ultimoCierre is { } c && c.AddDays(1) > primera) primera = c.AddDays(1);
        if (ultimoMovimiento is { } m && PrimeroDelMes(m.AddMonths(1)) > primera) primera = PrimeroDelMes(m.AddMonths(1));
        return Result.Failure(ErroresDeParametros.RequiereInicioDePeriodo(clave, primera));

        static DateOnly PrimeroDelMes(DateOnly fecha) => new(fecha.Year, fecha.Month, 1);
    }

    /// <summary>(3) El modo de paso por cadena y el tipo fiscal sin paso (FR-075).</summary>
    private async Task<Result<DecisionDeReglasDeParametro>> ModoDePasoAsync(AltaDeParametro alta, CancellationToken ct)
    {
        var activos = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.Code).ToListAsync(ct);
        IReadOnlyList<InventoryDocumentType> alcanzados;
        IReadOnlyList<AmbitoDeParametro>? ambitos = null;

        if (alta.Chain is { } textoDeCadena)
        {
            if (alta.ScopeKind != ParameterScopeKind.DocumentType)
                return Invalido("La cadena se indica con el ámbito DocumentType.");
            if (!Enum.TryParse<PostingChain>(textoDeCadena, ignoreCase: true, out var cadena) || cadena == PostingChain.None || !Enum.IsDefined(cadena))
                return Invalido($"«{textoDeCadena}» no es una cadena: Purchases, Sales o Transfers.");
            alcanzados = activos.Where(t => ClasesDeDocumento.De(t.Class).Chain == cadena).ToList();
            if (alcanzados.Count == 0) return Invalido($"La cadena {cadena} no tiene tipos de documento activos.");
            ambitos = alcanzados.Select(Ambito).ToList();
        }
        else if (alta.ScopeKind == ParameterScopeKind.DocumentType && alta.Ambito is { } ambito)
        {
            var tipo = activos.FirstOrDefault(t => t.Id == ambito.Id)
                ?? await db.InventoryDocumentTypes.AsNoTracking().FirstAsync(t => t.Id == ambito.Id, ct);
            var cadena = ClasesDeDocumento.De(tipo.Class).Chain;
            if (cadena != PostingChain.None)
            {
                var deLaCadena = activos.Where(t => ClasesDeDocumento.De(t.Class).Chain == cadena).Select(t => Nombrado(t)).ToList();
                return Result.Failure<DecisionDeReglasDeParametro>(InventoryErrors.PostingModeChainMismatch(cadena, deLaCadena));
            }
            alcanzados = [tipo];
        }
        else
        {
            // El valor general: lo heredan los tipos sin vigencia propia a esa fecha.
            var propios = parametros is null
                ? []
                : (await parametros.VigenciasAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, ct))
                    .Where(v => v.ScopeKind == ParameterScopeKind.DocumentType && v.VigenteEn(alta.ValidFrom))
                    .Select(v => v.ScopeId).ToHashSet();
            alcanzados = activos.Where(t => !propios.Contains(t.Id)).ToList();
        }

        if (alta.Valor == "NoPasa")
        {
            var fiscales = alcanzados.Where(t => ClasesDeDocumento.De(t.Class).IsFiscal).Select(t => Nombrado(t)).ToList();
            if (fiscales.Count > 0)
            {
                if (permisos is null || !await permisos.HasPermissionAsync(PermisoDeFiscalSinPaso, ct))
                    return Result.Failure<DecisionDeReglasDeParametro>(ErroresDeParametros.PermisoRequerido(PermisoDeFiscalSinPaso));
                if (!alta.ConfirmFiscalWithoutPosting)
                    return Result.Failure<DecisionDeReglasDeParametro>(InventoryErrors.PostingModeFiscalRequiresConfirmation(fiscales));
            }
        }

        return Result.Success(new DecisionDeReglasDeParametro(ambitos, ambitos));

        static Result<DecisionDeReglasDeParametro> Invalido(string mensaje) =>
            Result.Failure<DecisionDeReglasDeParametro>(new Error(Error.Validation.Code, mensaje));
    }

    private static AmbitoDeParametro Ambito(InventoryDocumentType t) => new(ParameterScopeKind.DocumentType, t.Id, t.PublicId, t.Code, t.Name);

    private static InventoryErrors.TipoNombrado Nombrado(InventoryDocumentType t) => new(t.PublicId, t.Code, t.Name, t.Class.ToString());

    // ------------------------------------------------------------------------------ políticas de aprobación --

    public async Task<Result> EvaluarAsync(AltaDePoliticaDeAprobacion alta, CancellationToken ct)
    {
        InventoryDocumentType? tipo = null;
        if (alta.DocumentTypePublicId is { } publicId)
        {
            tipo = await db.InventoryDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == publicId, ct);
            if (tipo is null) return Result.Failure(InventoryErrors.DocumentTypeNotFound());
        }

        var corte = await CorteAsync(ct);
        if (corte?.LastClosedDate is { } cerrado && alta.ValidFrom <= cerrado)
            return Result.Failure(ErroresDeAprobaciones.PoliticaEnPeriodoCerrado(cerrado));

        if (tipo is not null && alta.CantidadDeNiveles == 0 && alta.Subject == ApprovalSubjects.DocumentConfirmation
            && await SiempreSeApruebaAsync(tipo, alta.ValidFrom, ct))
            return Result.Failure(ErroresDeAprobaciones.PoliticaRequerida(tipo.Class.ToString()));

        return Result.Success();
    }

    public async Task<IReadOnlyDictionary<Guid, TipoDeDocumentoDeAprobacionDto>> DescribirTiposAsync(IReadOnlyCollection<Guid> tipos, CancellationToken ct) =>
        tipos.Count == 0
            ? new Dictionary<Guid, TipoDeDocumentoDeAprobacionDto>()
            : (await db.InventoryDocumentTypes.AsNoTracking().IgnoreQueryFilters().Where(t => tipos.Contains(t.PublicId)).ToListAsync(ct))
                .ToDictionary(t => t.PublicId, t => new TipoDeDocumentoDeAprobacionDto(t.PublicId, t.Code, t.Name, t.Class.ToString()));

    /// <summary>
    /// El saldo inicial siempre se aprueba; un tipo de ajuste de conteo, cuando su política vigente (o la última) tiene un nivel
    /// con <see cref="PermisoDeConteo"/>.
    /// </summary>
    private async Task<bool> SiempreSeApruebaAsync(InventoryDocumentType tipo, DateOnly desde, CancellationToken ct)
    {
        if (tipo.Class == DocumentClass.OpeningBalance) return true;
        if (tipo.Class is not (DocumentClass.PositiveAdjustment or DocumentClass.NegativeAdjustment)) return false;

        var clave = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, tipo.PublicId);
        var versiones = await db.ApprovalPolicies.AsNoTracking().Include(p => p.Levels).Where(p => p.PolicyKey == clave).ToListAsync(ct);
        var vigente = versiones.Where(v => v.ValidFrom < desde).OrderByDescending(v => v.ValidFrom).FirstOrDefault()
            ?? versiones.OrderByDescending(v => v.ValidFrom).FirstOrDefault();
        return vigente is not null && vigente.Levels.Any(l => !l.IsDeleted && l.PermissionCode == PermisoDeConteo);
    }

    private async Task<Domain.Entities.Inventory.Periods.InventorySetup?> CorteAsync(CancellationToken ct) =>
        await db.InventorySetups.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
}
