using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Accounting.Inventory.Reglas.PlantillaDeMatrizDeInventario;

namespace IngenIA365ERP.Application.Accounting.Inventory.Reglas;

/// <summary>
/// Plantilla 16 — la matriz de reglas contables de Inventario (feature 012, T509; contracts/plantillas.md §16;
/// contracts/contabilidad.md §2.7). Una sola hoja <c>Datos</c> con las columnas de §16; la hoja «Instrucciones» sale de
/// esta definición, cuyas reglas se escriben desde <see cref="OperacionesDeInventario"/> y <see cref="RolesDeCuenta"/>
/// (qué roles usa cada operación y qué dimensiones admite cada rol), no a mano.
///
/// <para>
/// Vive en Contabilidad, no en <c>Inventory/Imports/CatalogoDePlantillas</c>: Inventario no depende de
/// <c>Application.Accounting</c> (T31). La fila 16 de ese catálogo conserva su definición sin columnas; la ruta
/// <c>/api/accounting/inventory/rules/template.xlsx</c> (T528) usa ésta. (nuevo)
/// </para>
/// </summary>
public static class PlantillaDeMatrizDeInventario
{
    /// <summary>La clave de la plantilla 16 (la misma de <c>CatalogoDePlantillas.MatrizContableClave</c>).</summary>
    public const string Clave = "accounting.inventory-rules";

    /// <summary>El evento de auditoría de una importación aplicada (api.md §26.1). (nuevo)</summary>
    public const string EventoImportada = AuditEventTypes.AccountingInventoryRulesImported;

    public const string Operacion = "operacion";
    public const string Rol = "rol";
    public const string GrupoContable = "grupoContable";
    public const string Bodega = "bodega";
    public const string PuntoDeVenta = "puntoDeVenta";
    public const string MedioDePago = "medioDePago";
    public const string Tarifa = "tarifa";
    public const string TarifaPorcentaje = "tarifaPorcentaje";
    public const string Causa = "causa";
    public const string Sucursal = "sucursal";
    public const string CentroDeCosto = "centroDeCosto";
    public const string Cuenta = "cuenta";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";
    public const string Notas = "notas";

    /// <summary>Las columnas en el orden de §16 (también el de la descarga con datos).</summary>
    public static IReadOnlyList<string> Columnas { get; } =
    [
        Operacion, Rol, GrupoContable, Bodega, PuntoDeVenta, MedioDePago, Tarifa, TarifaPorcentaje, Causa, Sucursal, CentroDeCosto,
        Cuenta, VigenteDesde, VigenteHasta, Notas,
    ];

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Matriz de reglas contables de Inventario", ModuloDeAuditoria.Accounting,
    [
        new HojaDePlantilla("Datos",
        [
            new(Operacion, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "una de: " + string.Join(", ", OperacionesDeInventario.Todas.Select(o => o.Codigo)), Ejemplo: OperacionesDeInventario.Compra),
            new(Rol, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "un rol de la operación: " + string.Join("; ", OperacionesDeInventario.Todas.Select(o => $"{o.Codigo} → {string.Join(", ", o.Roles)}")),
                Ejemplo: RolesDeCuenta.Inventario),
            new(GrupoContable, TipoDeValor.Codigo, Largo: 10, Reglas: Dimension(DimensionDeRegla.AccountingGroupCode) + "; vacío o * = cualquiera", Ejemplo: "ABARROTES"),
            new(Bodega, TipoDeValor.Codigo, Largo: 10, Reglas: Dimension(DimensionDeRegla.WarehouseCode) + "; vacío o * = cualquiera", Ejemplo: "*"),
            new(PuntoDeVenta, TipoDeValor.Codigo, Largo: 10, Reglas: Dimension(DimensionDeRegla.PointOfSaleCode) + "; vacío o * = cualquiera", Ejemplo: "*"),
            new(MedioDePago, TipoDeValor.Codigo, Largo: 10, Reglas: Dimension(DimensionDeRegla.PaymentMeansCode), Ejemplo: null),
            new(Tarifa, TipoDeValor.Codigo, Largo: 20, Reglas: Dimension(DimensionDeRegla.TaxRateCode) + " (código de la tarifa en Core)", Ejemplo: null),
            new(TarifaPorcentaje, TipoDeValor.Porcentaje, Reglas: "con tarifa: en puntos (19 = 19 %), la del catálogo en vigenteDesde; se guarda como fracción; vacío en los impuestos por unidad", Ejemplo: null),
            new(Causa, TipoDeValor.Texto, Largo: 40,
                Reglas: "Sobrante: Surplus; Faltante: ShortageToCashier; GastoDeArqueo: ShortageToExpense; CajaDestino: Safe o Deposit; "
                    + "Contrapartida de AjusteNegativo y Baja: la causa de ajuste de Inventario; de AjusteDeCosto: "
                    + string.Join(", ", RolesDeCuenta.RazonesDeAjusteDeCosto),
                Ejemplo: null),
            new(Sucursal, TipoDeValor.Sucursal, Reglas: Dimension(DimensionDeRegla.Branch) + "; código o nombre; vacío o * = cualquiera", Ejemplo: "*"),
            new(CentroDeCosto, TipoDeValor.CentroDeCosto, Reglas: Dimension(DimensionDeRegla.CostCenter) + "; código o nombre; vacío o * = cualquiera", Ejemplo: "*"),
            new(Cuenta, TipoDeValor.CuentaContable, Obligatoria: true,
                Reglas: "auxiliar de movimiento, activa y habilitada para Inventario; en Impuesto y Retencion, de una clase de impuesto compatible con la tarifa",
                Ejemplo: "1435XXXX (el código completo de la auxiliar)"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true,
                Reglas: "la misma llave con una fecha posterior es una versión nueva y cierra la anterior la víspera; no puede empezar en o antes de lo ya contabilizado",
                Ejemplo: "AAAA-MM-01"),
            new(VigenteHasta, TipoDeValor.Fecha, Reglas: "vacío = abierta", Ejemplo: null),
            new(Notas, TipoDeValor.Texto, Largo: 400, Reglas: "quién decidió y por qué", Ejemplo: "Aprobado por la contadora"),
        ]),
    ]);

    /// <summary>Qué roles exigen o admiten la dimensión (para la hoja «Instrucciones»).</summary>
    private static string Dimension(DimensionDeRegla dimension)
    {
        var exigen = RolesDeCuenta.Todos.Where(r => r.Exigidas.Contains(dimension)).Select(r => r.Codigo).ToList();
        var admiten = RolesDeCuenta.Todos.Where(r => r.Opcionales.Contains(dimension)).Select(r => r.Codigo).ToList();
        var partes = new List<string>();
        if (exigen.Count > 0) partes.Add("obligatoria en " + string.Join(", ", exigen));
        if (admiten.Count > 0) partes.Add("opcional en " + string.Join(", ", admiten));
        return partes.Count == 0 ? "no la usa ningún rol" : string.Join("; ", partes);
    }
}

/// <summary>
/// <c>POST /api/accounting/inventory/rules/import?mode=review|apply</c> (T509; plantillas.md §0.5 y §16): la mecánica común
/// de <see cref="EjecutorDeImportacion"/> —revisión que no guarda, aplicación todo o nada con 422 <c>Import.Invalid</c>— y
/// <b>las mismas reglas que el alta una a una</b>, porque cada fila pasa por <see cref="ReglasDeLaMatriz.ValidarAsync"/>.
/// La llave es <c>DimensionKey</c> + <c>vigenteDesde</c>: la misma llave es «sin cambio» (o actualiza notas y
/// <c>vigenteHasta</c>); la misma clave con una fecha posterior es una versión nueva. Queda auditado como
/// <c>Accounting.InventoryRules.Imported</c>. (nuevo)
/// </summary>
public sealed record ImportInventoryPostingRulesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportInventoryPostingRulesCommandValidator : AbstractValidator<ImportInventoryPostingRulesCommand>
{
    public ImportInventoryPostingRulesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportInventoryPostingRulesCommand>.LargoMaximo);
    }
}

public sealed class ImportInventoryPostingRulesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    ReglasDeLaMatriz reglas,
    AccountingAuditEmitter auditoria)
    : IRequestHandler<ImportInventoryPostingRulesCommand, Result<ImportResultDto>>
{
    public async Task<Result<ImportResultDto>> Handle(ImportInventoryPostingRulesCommand request, CancellationToken ct)
    {
        var r = await ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);
        if (r.IsSuccess && r.Value.Applied)
        {
            var hoja = r.Value.Sheets.FirstOrDefault();
            await auditoria.EmitAsync(P.EventoImportada, "InventoryPostingRule", null, null,
                new { fileName = r.Value.FileName, fileSha256 = r.Value.FileSha256, created = hoja?.Created ?? 0, updated = hoja?.Updated ?? 0, unchanged = hoja?.Unchanged ?? 0 },
                ct);
        }
        return r;
    }

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var catalogos = await reglas.CargarAsync(ct);
        var cuentas = CatalogoCitado<int>.Desde(
            await db.ChartOfAccounts.AsNoTracking().Where(a => !a.IsDeleted).Select(a => new { a.Id, a.Code }).ToListAsync(ct),
            a => a.Id, a => a.Code);
        var sucursales = CatalogoCitado<int>.Desde(
            await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).Select(b => new { b.Id, b.LegacyCode, b.Name }).ToListAsync(ct),
            b => b.Id, b => b.LegacyCode, b => b.Name);
        var centros = CatalogoCitado<int>.Desde(
            await db.CostCenters.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.LegacyCode, c.Name }).ToListAsync(ct),
            c => c.Id, c => c.LegacyCode, c => c.Name);

        // Las reglas vivas, seguidas: una versión nueva cierra la anterior sobre la misma entidad.
        var porClave = (await db.InventoryPostingRules.Where(r => !r.IsDeleted).ToListAsync(ct))
            .GroupBy(r => r.DimensionKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var fila in ctx.Datos.Filas.OrderBy(f => f.Crudo(P.VigenteDesde), StringComparer.Ordinal).ThenBy(f => f.Numero))
            await FilaAsync(ctx, fila, catalogos, cuentas, sucursales, centros, porClave, ct);
    }

    private async Task FilaAsync(
        ContextoDeImportacion ctx, FilaDeImportacion fila, CatalogosDeLaMatriz catalogos,
        CatalogoCitado<int> cuentas, CatalogoCitado<int> sucursales, CatalogoCitado<int> centros,
        Dictionary<string, List<InventoryPostingRule>> porClave, CancellationToken ct)
    {
        var operacion = fila.Texto(P.Operacion, 40);
        var rol = fila.Texto(P.Rol, 40);
        var grupo = Comodin(fila.Texto(P.GrupoContable, 10));
        var bodega = Comodin(fila.Texto(P.Bodega, 10));
        var punto = Comodin(fila.Texto(P.PuntoDeVenta, 10));
        var medio = Comodin(fila.Texto(P.MedioDePago, 10));
        var tarifa = Comodin(fila.Texto(P.Tarifa, 20));
        var porcentaje = fila.Porcentaje(P.TarifaPorcentaje);
        var causa = Comodin(fila.Texto(P.Causa, 40));
        int? sucursal = Comodin(fila.Crudo(P.Sucursal)) is null ? null : fila.Referencia(P.Sucursal, sucursales, "una sucursal", "en Maestros → Agencias");
        int? centro = Comodin(fila.Crudo(P.CentroDeCosto)) is null ? null : fila.Referencia(P.CentroDeCosto, centros, "un centro de costo", "en Maestros → Centros de costo");
        int? cuenta = fila.Referencia(P.Cuenta, cuentas, "una cuenta", "en Contabilidad → Plan de cuentas");
        var desde = fila.Fecha(P.VigenteDesde);
        var hasta = fila.Fecha(P.VigenteHasta);
        var notas = fila.Texto(P.Notas, 400);
        if (fila.TieneErrores || operacion is null || rol is null || cuenta is not { } idCuenta || desde is not { } validFrom) return;

        // La tarifa viaja en puntos y se guarda como fracción (9,6), como la del catálogo (T19).
        var propuesta = new ReglaPropuesta(operacion, rol, idCuenta, validFrom, hasta, grupo, bodega, punto, medio, tarifa,
            porcentaje is { } p ? decimal.Round(p, 6) : null, causa, sucursal, centro);

        var forma = ReglasDeLaMatriz.Forma(propuesta.Operation, propuesta.Role, propuesta.Presentes);
        if (forma.Count > 0)
        {
            foreach (var error in forma) fila.Error(ColumnaDe(error), error.Code, error.Message);
            return;
        }

        var clave = propuesta.Clave;
        if (!ctx.Datos.LlaveUnica(fila, clave + "@" + validFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), P.VigenteDesde)) return;
        var versiones = porClave.TryGetValue(clave, out var lista) ? lista : [];

        var misma = versiones.FirstOrDefault(v => v.ValidFrom == validFrom);
        if (misma is not null)
        {
            await ExistenteAsync(ctx, fila, misma, propuesta, notas, catalogos, ct);
            return;
        }

        var validacion = await reglas.ValidarAsync(propuesta, catalogos, versiones, esVersion: versiones.Count > 0, ct);
        foreach (var aviso in validacion.Avisos) fila.Aviso(P.TarifaPorcentaje, aviso.Code, aviso.Message);
        if (!validacion.EsValida)
        {
            foreach (var error in validacion.Errores) fila.Error(ColumnaDe(error), error.Code, error.Message);
            return;
        }

        foreach (var anterior in validacion.ACerrar) anterior.CerrarVigencia(validFrom.AddDays(-1));
        var regla = propuesta.ComoRegla(ReglasDeMatrizComunes.Notas(notas, ctx.Motivo));
        db.InventoryPostingRules.Add(regla);
        if (lista is null) porClave[clave] = [regla];
        else lista.Add(regla);
        ctx.Registrar(fila, clave, AccionDeImportacion.Create,
        [
            new(P.Cuenta, null, fila.Crudo(P.Cuenta)),
            new(P.VigenteDesde, null, validFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            .. validacion.ACerrar.Select(a => new CampoCambiadoDto(P.VigenteHasta, null, validFrom.AddDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))),
        ]);
    }

    /// <summary>
    /// La misma llave (clave y fecha): la cuenta no se cambia en su sitio —lo contabilizado dejaría de reproducirse—, así
    /// que otra cuenta es <c>Overlaps</c> (se agrega una versión con otra fecha). Cambian las notas y <c>vigenteHasta</c>
    /// (con la regla de la desactivación).
    /// </summary>
    private async Task ExistenteAsync(
        ContextoDeImportacion ctx, FilaDeImportacion fila, InventoryPostingRule regla, ReglaPropuesta propuesta, string? notas,
        CatalogosDeLaMatriz catalogos, CancellationToken ct)
    {
        if (regla.AccountId != propuesta.AccountId)
        {
            var error = AccountingErrors.InventoryRuleOverlaps(new { rulePublicId = regla.PublicId, validFrom = regla.ValidFrom, validTo = regla.ValidTo, dimensionKey = regla.DimensionKey });
            fila.Error(P.Cuenta, error.Code, error.Message + " Para cambiar la cuenta, agregue una fila con una fecha posterior.");
            return;
        }

        var campos = new List<CampoCambiadoDto>();
        if (propuesta.ValidTo != regla.ValidTo)
        {
            if (propuesta.ValidTo is not { } hasta)
            {
                fila.Error(P.VigenteHasta, AccountingErrors.InventoryRuleValidToInvalid(regla.ValidFrom, regla.ValidTo!.Value).Code,
                    "Una vigencia cerrada no se reabre desde la plantilla: cree una versión nueva.");
                return;
            }
            if (hasta < regla.ValidFrom.AddDays(-1))
            {
                var invalida = AccountingErrors.InventoryRuleValidToInvalid(regla.ValidFrom, hasta);
                fila.Error(P.VigenteHasta, invalida.Code, invalida.Message);
                return;
            }
            if (await reglas.RetroactividadDelCierreAsync(regla, hasta, catalogos, ct) is { } retro)
            {
                fila.Error(P.VigenteHasta, retro.Code, retro.Message);
                return;
            }
            campos.Add(new(P.VigenteHasta, regla.ValidTo?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                hasta.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
            regla.CerrarVigencia(hasta);
        }
        if (notas is not null && !string.Equals(notas, regla.Notes, StringComparison.Ordinal))
        {
            campos.Add(new(P.Notas, regla.Notes, notas));
            regla.Notes = notas;
        }
        ctx.Registrar(fila, regla.DimensionKey, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
    }

    private static string? Comodin(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == InventoryPostingRule.Cualquiera ? null : valor.Trim();

    /// <summary>La columna de la plantilla a la que pertenece un error de <see cref="ReglasDeLaMatriz"/>.</summary>
    private static string ColumnaDe(Error error)
    {
        var dimension = error is ErrorConDatos { Data: { } datos }
            ? datos.GetType().GetProperty("dimension")?.GetValue(datos) as string
            : null;
        return dimension switch
        {
            "accountingGroupCode" => P.GrupoContable,
            "warehouseCode" => P.Bodega,
            "pointOfSaleCode" => P.PuntoDeVenta,
            "paymentMeansCode" => P.MedioDePago,
            "taxRateCode" => P.Tarifa,
            "taxRate" => P.TarifaPorcentaje,
            "reasonCode" => P.Causa,
            "branch" => P.Sucursal,
            "costCenter" => P.CentroDeCosto,
            _ => error.Code switch
            {
                "Accounting.InventoryRule.OperationUnknown" => P.Operacion,
                "Accounting.InventoryRule.RoleNotInOperation" => P.Rol,
                "Accounting.InventoryRule.TaxRateMismatch" => P.TarifaPorcentaje,
                "Accounting.InventoryRule.ValidToInvalid" => P.VigenteHasta,
                "Accounting.InventoryRule.Overlaps" or "Accounting.InventoryRule.RetroactiveOverPosted" => P.VigenteDesde,
                _ when error.Code.StartsWith("Accounting.Account.", StringComparison.Ordinal) || error.Code == "Accounting.NotInitialized" => P.Cuenta,
                _ => P.Operacion,
            },
        };
    }
}

/// <summary>
/// La plantilla 16 llena (<c>GET …/rules/template.xlsx?withData=true</c>, plantillas.md §0.6): las reglas vigentes hoy y
/// las que empiezan después, una fila por regla en el orden de <see cref="PlantillaDeMatrizDeInventario.Columnas"/>, con la
/// tarifa en puntos. Así se exporta, se corrige y se vuelve a importar con el mismo libro. (nuevo)
/// </summary>
public sealed record GetInventoryRulesTemplateQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetInventoryRulesTemplateQueryValidator : AbstractValidator<GetInventoryRulesTemplateQuery>;

public sealed class GetInventoryRulesTemplateQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetInventoryRulesTemplateQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetInventoryRulesTemplateQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var reglas = await db.InventoryPostingRules.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.ValidTo == null || r.ValidTo >= hoy))
            .OrderBy(r => r.Operation).ThenBy(r => r.Role).ThenBy(r => r.DimensionKey).ThenBy(r => r.ValidFrom)
            .ToListAsync(ct);
        var idsCuenta = reglas.Select(r => r.AccountId).Distinct().ToList();
        var cuentas = await db.ChartOfAccounts.AsNoTracking().Where(a => idsCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Code, ct);
        var sucursales = await db.Branches.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.LegacyCode ?? b.Name, ct);
        var centros = await db.CostCenters.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.LegacyCode ?? c.Name, ct);

        var filas = reglas.Select(r => (IReadOnlyList<object?>)
        [
            r.Operation, r.Role, r.AccountingGroupCode, r.WarehouseCode, r.PointOfSaleCode, r.PaymentMeansCode, r.TaxRateCode,
            r.TaxRate is { } t ? t * 100m : null, r.ReasonCode,
            r.BranchId is { } b ? sucursales.GetValueOrDefault(b) : null,
            r.CostCenterId is { } c ? centros.GetValueOrDefault(c) : null,
            cuentas.GetValueOrDefault(r.AccountId), r.ValidFrom, r.ValidTo, r.Notes,
        ]).ToList();
        return Result.Success(new DatosDePlantilla(new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Datos"] = filas,
        }));
    }
}
