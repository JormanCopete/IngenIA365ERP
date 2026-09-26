using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Rules;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

/// <summary>
/// Los valores que un renglón busca en la matriz (§2.3, paso 2): grupo y bodega del renglón; punto de venta, causa,
/// razón, tratamiento o destino del contenido (<see cref="ReasonCode"/>); medio del pago; tarifa del impuesto; sucursal
/// del renglón o la del sobre; centro de costo del sobre. Sucursal y centro van por <c>PublicId</c>: el resolutor los
/// traduce al <c>Id</c> de Core. (nuevo)
/// </summary>
public sealed record ValoresBuscados(
    string? AccountingGroupCode = null,
    string? WarehouseCode = null,
    string? PointOfSaleCode = null,
    string? PaymentMeansCode = null,
    string? TaxRateCode = null,
    string? ReasonCode = null,
    Guid? BranchPublicId = null,
    Guid? CostCenterPublicId = null);

/// <summary>
/// Una pregunta a la matriz: la operación, el rol, los valores, la <b>fecha de las reglas</b> (<see cref="Fecha"/>, ver
/// <see cref="ResolutorDeReglas.FechaDeLasReglas"/>), el importe del rol —con cero no se pide regla (§13.3)—, las líneas
/// del documento que suman ahí y, en <c>Impuesto</c>/<c>Retencion</c>, la tarifa del mensaje (nula en los impuestos de
/// valor por unidad, que no se comparan). (nuevo)
/// </summary>
public sealed record PeticionDeRegla(
    string Operation,
    string Role,
    ValoresBuscados Valores,
    DateOnly Fecha,
    decimal Importe,
    IReadOnlyList<int> DocumentLines,
    decimal? TarifaDelMensaje = null);

/// <summary>La regla que ganó, con su cuenta ya cargada y la sucursal y el centro traducidos a <c>Id</c>. (nuevo)</summary>
public sealed record ReglaResuelta(
    PeticionDeRegla Peticion,
    InventoryPostingRule Regla,
    ChartOfAccount Cuenta,
    CuentaParaReglas CuentaParaReglas,
    int? BranchId,
    int? CostCenterId);

/// <summary>Un mensaje ya contabilizado que limita una versión nueva (§2.4): su fecha de operación y el documento. (nuevo)</summary>
public sealed record MensajeContabilizado(Guid MessagePublicId, DateOnly OperationDate, string Documento);

/// <summary>
/// La resolución de la matriz (feature 012, T27, T506; contracts/contabilidad.md §2.3 y §2.4). Carga en bloque las reglas
/// que rigen en un rango de fechas con sus cuentas (<see cref="CargarAsync"/>) y responde, sin más consultas, qué regla
/// gana para cada renglón (<see cref="MatrizVigente.Resolver"/>). Además expone las dos comprobaciones de «sin
/// retroactividad sobre lo contabilizado» que usa <c>ReglasDeLaMatriz</c>: el último mensaje contabilizado de una
/// operación y el primero que coincide con una clave nueva. (nuevo)
/// </summary>
public sealed class ResolutorDeReglas(IApplicationDbContext db)
{
    /// <summary>Cuántos recibos se leen por tanda al buscar en el contenido de los mensajes.</summary>
    private const int Tanda = 500;

    /// <summary>
    /// Las reglas no borradas cuya vigencia toca <c>[desde, hasta]</c>, con sus cuentas (y las tarifas de éstas) y los
    /// mapas <c>PublicId → Id</c> de sucursales y centros de costo. Sin seguimiento: nada de esto se escribe.
    /// </summary>
    public async Task<MatrizVigente> CargarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde) (desde, hasta) = (hasta, desde);
        var reglas = await db.InventoryPostingRules.AsNoTracking()
            .Where(r => !r.IsDeleted && r.ValidFrom <= hasta && (r.ValidTo == null || r.ValidTo >= desde))
            .ToListAsync(ct);
        var idsDeCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Include(a => a.TaxRates)
            .Where(a => idsDeCuenta.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var sucursales = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).ToDictionaryAsync(b => b.PublicId, b => b.Id, ct);
        var centros = await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted).ToDictionaryAsync(c => c.PublicId, c => c.Id, ct);
        return new MatrizVigente(desde, hasta, reglas, cuentas, sucursales, centros);
    }

    /// <summary>
    /// §2.3, paso 1: la fecha de las reglas de un mensaje es su <c>origin.operationDate</c>; en un
    /// <c>AjusteDeCostoReconocido</c>, su <c>effectiveDate</c>. En un <c>DocumentoAnulado</c> cada contenido usa la de su
    /// original (<see cref="FechaDeLasReglasDelAnulado"/>).
    /// </summary>
    public static DateOnly FechaDeLasReglas(IntegrationEnvelopeV1 sobre, object? contenido) => contenido switch
    {
        AjusteDeCostoReconocidoV1 ajuste => ajuste.EffectiveDate,
        _ => sobre.Origin.OperationDate,
    };

    /// <summary>La fecha de las reglas de un contenido anulado: la del original (T29, G2).</summary>
    public static DateOnly FechaDeLasReglasDelAnulado(VoidedContentV1 contenido) => contenido.OperationDate;

    // -------------------------------------------------------------- retroactividad (§2.4) --

    /// <summary>
    /// El mensaje contabilizado (con comprobante) de la operación con la fecha de operación más reciente, o nulo si no
    /// hay. Las anulaciones no cuentan: sus reglas son las de la fecha de su original, que ya está contabilizado.
    /// </summary>
    public async Task<MensajeContabilizado?> UltimoContabilizadoAsync(string operacion, CancellationToken ct)
    {
        await foreach (var mensaje in ContabilizadosAsync(operacion, null, null, descendente: true, ct))
            return mensaje.Contabilizado;
        return null;
    }

    /// <summary>
    /// El primer mensaje contabilizado de la operación, desde <see cref="InventoryPostingRule.ValidFrom"/> (y hasta su
    /// <c>ValidTo</c>, si lo tiene), que tiene un renglón con los valores que fija <paramref name="regla"/>; nulo si
    /// ninguno. Así una clave nueva puede empezar antes de lo contabilizado sin cambiar el resultado de nada ya
    /// contabilizado (FR-078). La comparación es sobre los valores del contenido y del sobre y es <b>conservadora</b>:
    /// cuenta como coincidencia que cada valor fijado aparezca en el mensaje, aunque sea en renglones distintos, para no
    /// dejar pasar nunca una regla que habría cambiado un comprobante.
    /// </summary>
    public async Task<MensajeContabilizado?> ContabilizadoQueCoincideAsync(InventoryPostingRule regla, CancellationToken ct)
    {
        var sucursal = regla.BranchId is { } b
            ? await db.Branches.AsNoTracking().Where(x => x.Id == b).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var centro = regla.CostCenterId is { } c
            ? await db.CostCenters.AsNoTracking().Where(x => x.Id == c).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;

        await foreach (var mensaje in ContabilizadosAsync(regla.Operation, regla.ValidFrom, regla.ValidTo, descendente: false, ct))
        {
            if (Coincide(regla, sucursal, centro, mensaje)) return mensaje.Contabilizado;
        }
        return null;
    }

    private sealed record Leido(MensajeContabilizado Contabilizado, JsonElement? Contenido, Guid BranchPublicId, Guid? CostCenterPublicId);

    /// <summary>
    /// Los recibos con comprobante de la operación en el rango, con el contenido de su mensaje. Si el tipo de mensaje trae
    /// varias operaciones (ajustes, devoluciones), se filtra por la <c>operation</c> del contenido.
    /// </summary>
    private async IAsyncEnumerable<Leido> ContabilizadosAsync(
        string operacion, DateOnly? desde, DateOnly? hasta, bool descendente,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var definicion = OperacionesDeInventario.Buscar(operacion);
        if (definicion is null) yield break;
        var tipo = definicion.Mensaje;
        var compartido = OperacionesDeInventario.DelMensaje(tipo).Count > 1;

        var consulta =
            from p in db.InventoryPostings.AsNoTracking()
            join m in db.IntegrationMessages.AsNoTracking() on p.MessagePublicId equals m.PublicId
            where p.AccountingDocumentId != null && !p.IsDeleted && p.MessageType == tipo
            select new
            {
                p.Id, p.MessagePublicId, p.OperationDate, p.SourceDocumentTypeCode, p.SourceDocumentNumber, p.SourceDocumentClass,
                m.PayloadJson, m.BranchPublicId, m.CostCenterPublicId,
            };
        if (desde is { } d) consulta = consulta.Where(x => x.OperationDate >= d);
        if (hasta is { } h) consulta = consulta.Where(x => x.OperationDate <= h);
        consulta = descendente
            ? consulta.OrderByDescending(x => x.OperationDate).ThenByDescending(x => x.Id)
            : consulta.OrderBy(x => x.OperationDate).ThenBy(x => x.Id);

        for (var salto = 0; ; salto += Tanda)
        {
            var tanda = await consulta.Skip(salto).Take(Tanda).ToListAsync(ct);
            foreach (var x in tanda)
            {
                var contenido = Leer(x.PayloadJson);
                if (compartido && !string.Equals(Texto(contenido, "operation"), operacion, StringComparison.Ordinal)) continue;
                var documento = string.Join(' ', new[] { x.SourceDocumentTypeCode ?? x.SourceDocumentClass, x.SourceDocumentNumber }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                yield return new Leido(new MensajeContabilizado(x.MessagePublicId, x.OperationDate, documento), contenido, x.BranchPublicId, x.CostCenterPublicId);
            }
            if (tanda.Count < Tanda) yield break;
        }
    }

    private static bool Coincide(InventoryPostingRule regla, Guid? sucursal, Guid? centro, Leido mensaje)
    {
        var valores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (mensaje.Contenido is { } contenido) Recoger(contenido, valores);

        bool Esta(string? codigo) => codigo is null || valores.Contains(codigo);

        if (!Esta(regla.AccountingGroupCode) || !Esta(regla.WarehouseCode) || !Esta(regla.PointOfSaleCode)
            || !Esta(regla.PaymentMeansCode) || !Esta(regla.TaxRateCode) || !Esta(regla.ReasonCode))
            return false;
        if (sucursal is { } s && mensaje.BranchPublicId != s && !valores.Contains(s.ToString())) return false;
        if (centro is { } c && mensaje.CostCenterPublicId != c) return false;
        return true;
    }

    private static void Recoger(JsonElement elemento, HashSet<string> valores)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propiedad in elemento.EnumerateObject()) Recoger(propiedad.Value, valores);
                break;
            case JsonValueKind.Array:
                foreach (var item in elemento.EnumerateArray()) Recoger(item, valores);
                break;
            case JsonValueKind.String:
                if (elemento.GetString() is { Length: > 0 } texto) valores.Add(texto.Trim());
                break;
        }
    }

    private static JsonElement? Leer(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Texto(JsonElement? elemento, string propiedad) =>
        elemento is { ValueKind: JsonValueKind.Object } e && e.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String
            ? valor.GetString()
            : null;
}

/// <summary>
/// La matriz cargada para un rango de fechas (nuevo). <see cref="Resolver"/> aplica §2.3: candidatas de la operación y el
/// rol, vigentes en la fecha, cuyas dimensiones fijadas son iguales a las buscadas (las exigidas siempre están fijadas; las
/// opcionales pueden ser <c>*</c>); gana la de mayor peso; sin candidata, <c>Accounting.InventoryRule.Missing</c>; en los
/// roles de impuesto, <c>Accounting.InventoryRule.TaxRateMismatch</c> si la tarifa de la regla o la vigente de la cuenta
/// no es la del mensaje.
/// </summary>
public sealed class MatrizVigente
{
    private readonly ILookup<(string Operacion, string Rol), InventoryPostingRule> _porOperacionYRol;
    private readonly IReadOnlyDictionary<int, ChartOfAccount> _cuentas;
    private readonly IReadOnlyDictionary<Guid, int> _sucursales;
    private readonly IReadOnlyDictionary<Guid, int> _centros;

    internal MatrizVigente(
        DateOnly desde, DateOnly hasta, IReadOnlyList<InventoryPostingRule> reglas,
        IReadOnlyDictionary<int, ChartOfAccount> cuentas, IReadOnlyDictionary<Guid, int> sucursales, IReadOnlyDictionary<Guid, int> centros)
    {
        Desde = desde;
        Hasta = hasta;
        Reglas = reglas;
        _porOperacionYRol = reglas.ToLookup(r => (r.Operation, r.Role));
        _cuentas = cuentas;
        _sucursales = sucursales;
        _centros = centros;
    }

    public DateOnly Desde { get; }

    public DateOnly Hasta { get; }

    /// <summary>Las reglas cargadas (para la completitud y las consultas).</summary>
    public IReadOnlyList<InventoryPostingRule> Reglas { get; }

    /// <summary>La cuenta de una regla cargada.</summary>
    public ChartOfAccount? CuentaDe(InventoryPostingRule regla) => _cuentas.GetValueOrDefault(regla.AccountId);

    /// <summary>
    /// La regla que gana para <paramref name="peticion"/>, o <c>Success(null)</c> si el importe es cero (un rol sin
    /// importe no pide regla). La fecha tiene que estar dentro del rango cargado.
    /// </summary>
    public Result<ReglaResuelta?> Resolver(PeticionDeRegla peticion)
    {
        ArgumentNullException.ThrowIfNull(peticion);
        if (peticion.Importe == 0m) return Result.Success<ReglaResuelta?>(null);
        if (peticion.Fecha < Desde || peticion.Fecha > Hasta)
            throw new ArgumentOutOfRangeException(nameof(peticion), peticion.Fecha, $"La matriz se cargó del {Desde:yyyy-MM-dd} al {Hasta:yyyy-MM-dd}.");

        var v = peticion.Valores;
        int? sucursal = v.BranchPublicId is { } bp && _sucursales.TryGetValue(bp, out var b) ? b : null;
        int? centro = v.CostCenterPublicId is { } cp && _centros.TryGetValue(cp, out var c) ? c : null;
        var grupo = Codigo(v.AccountingGroupCode);
        var bodega = Codigo(v.WarehouseCode);
        var punto = Codigo(v.PointOfSaleCode);
        var medio = Codigo(v.PaymentMeansCode);
        var tarifa = Codigo(v.TaxRateCode);
        var motivo = string.IsNullOrWhiteSpace(v.ReasonCode) ? null : v.ReasonCode.Trim();

        var ganadora = _porOperacionYRol[(peticion.Operation, peticion.Role)]
            .Where(r => r.VigenteEn(peticion.Fecha)
                && Igual(r.AccountingGroupCode, grupo) && Igual(r.WarehouseCode, bodega) && Igual(r.PointOfSaleCode, punto)
                && Igual(r.PaymentMeansCode, medio) && Igual(r.TaxRateCode, tarifa)
                && (r.ReasonCode is null || string.Equals(r.ReasonCode, motivo, StringComparison.OrdinalIgnoreCase))
                && (r.BranchId is null || r.BranchId == sucursal)
                && (r.CostCenterId is null || r.CostCenterId == centro))
            .OrderByDescending(r => r.SpecificityWeight)
            .ThenByDescending(r => r.ValidFrom)
            .ThenBy(r => r.Id)
            .FirstOrDefault();

        if (ganadora is null)
            return Result.Failure<ReglaResuelta?>(AccountingErrors.InventoryRuleMissing(peticion.Operation, peticion.Role,
                new
                {
                    accountingGroupCode = grupo, warehouseCode = bodega, pointOfSaleCode = punto, paymentMeansCode = medio,
                    taxRateCode = tarifa, reasonCode = motivo, branch = v.BranchPublicId, costCenter = v.CostCenterPublicId,
                },
                peticion.Fecha, peticion.DocumentLines));

        var cuenta = _cuentas.GetValueOrDefault(ganadora.AccountId)
            ?? throw new InvalidOperationException($"La cuenta {ganadora.AccountId} de la regla {ganadora.PublicId} no se cargó.");

        // C8: los impuestos de valor por unidad no traen tarifa y no se comparan.
        if (RolesDeCuenta.Buscar(peticion.Role) is { EsDeImpuesto: true } && peticion.TarifaDelMensaje is { } delMensaje)
        {
            if (ganadora.TaxRate is { } deLaRegla && deLaRegla != delMensaje)
                return Result.Failure<ReglaResuelta?>(AccountingErrors.InventoryRuleTaxRateMismatch(tarifa ?? string.Empty, cuenta.Code, deLaRegla, delMensaje));
            if (CuentaParaReglas.TarifaVigenteDe(cuenta, peticion.Fecha) is { } deLaCuenta && deLaCuenta != delMensaje)
                return Result.Failure<ReglaResuelta?>(AccountingErrors.InventoryRuleTaxRateMismatch(tarifa ?? string.Empty, cuenta.Code, deLaCuenta, delMensaje));
        }

        return Result.Success<ReglaResuelta?>(new ReglaResuelta(peticion, ganadora, cuenta, CuentaParaReglas.De(cuenta, peticion.Fecha), sucursal, centro));
    }

    private static bool Igual(string? deLaRegla, string? buscado) => deLaRegla is null || string.Equals(deLaRegla, buscado, StringComparison.Ordinal);

    private static string? Codigo(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();
}
