using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Accounting.Lending.Reglas.PlantillaDeMatrizDeCartera;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// La plantilla de la matriz de reglas contables de Cartera (feature 013, T0643; contracts/plantillas.md §9; contracts/contabilidad.md §3.7).
/// Una sola hoja <c>Datos</c> con las columnas de contabilidad.md §3.7 (<c>operacion</c> … <c>notas</c>; booleanos <c>S</c>, <c>N</c> o vacío,
/// y vacío y <c>*</c> son lo mismo); la hoja «Instrucciones» sale de esta definición, cuyas reglas se escriben desde
/// <see cref="OperacionesDeCartera"/> y <see cref="RolesDeCuentaDeCartera"/>, no a mano. Molde: <c>PlantillaDeMatrizDeInventario</c>. (nuevo)
/// </summary>
public static class PlantillaDeMatrizDeCartera
{
    public const string Clave = "accounting.lending-rules";

    /// <summary>El evento de auditoría de una importación aplicada (api.md §23.1).</summary>
    public const string EventoImportada = AuditEventTypes.AccountingLendingRulesImported;

    /// <summary>
    /// Lo que llevan en <c>notas</c> las filas sugeridas sin cuenta de la plantilla (C70). Una fila así que vuelve sin cuenta no es un error:
    /// la contadora todavía no la diligenció y se ignora.
    /// </summary>
    public const string PendienteDeValidar = "pendiente de validar";

    public const string Operacion = "operacion";
    public const string Rol = "rol";
    public const string Linea = "linea";
    public const string Modalidad = "modalidad";
    public const string Subsegmento = "subsegmento";
    public const string Categoria = "categoria";
    public const string Concepto = "concepto";
    public const string Empleado = "empleado";
    public const string Gracia = "gracia";
    public const string PorCuentaDelAsociado = "porCuentaDelAsociado";
    public const string Reestructurado = "reestructurado";
    public const string ClaseDeRecaudador = "claseDeRecaudador";
    public const string Motivo = "motivo";

    // Feature 014, US8 (T144; contabilidad.md §4.2, plantillas-y-archivos.md §6): la versión 2 de la plantilla. Un archivo de la versión 1 se sigue
    // leyendo: las columnas nuevas faltan y valen «*».
    public const string Naturaleza = "naturaleza";
    public const string ClaseDeCaptacion = "claseDeCaptacion";
    public const string PlazoDeEmision = "plazoDeEmision";
    public const string ClaseDeAporte = "claseDeAporte";
    public const string MarcaDeCuenta = "marcaDeCuenta";

    /// <summary>La cuenta CUIF sugerida de una fila sugerida: texto informativo que el lector no aplica.</summary>
    public const string CuentaSugerida = "cuentaSugerida";

    /// <summary>La versión de la plantilla (2 desde la feature 014).</summary>
    public const int Version = 2;
    public const string Sucursal = "sucursal";
    public const string CentroDeCosto = "centroDeCosto";
    public const string Cuenta = "cuenta";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";
    public const string Notas = "notas";

    /// <summary>Las columnas en el orden de §3.7 (también el de la descarga).</summary>
    public static IReadOnlyList<string> Columnas { get; } =
    [
        Operacion, Rol, Linea, Modalidad, Subsegmento, Categoria, Concepto, Empleado, Gracia, PorCuentaDelAsociado, Reestructurado,
        ClaseDeRecaudador, Motivo, Naturaleza, ClaseDeCaptacion, PlazoDeEmision, ClaseDeAporte, MarcaDeCuenta, Sucursal, CentroDeCosto, Cuenta,
        CuentaSugerida, VigenteDesde, VigenteHasta, Notas,
    ];

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Matriz de reglas contables de Cartera", ModuloDeAuditoria.Accounting,
    [
        new HojaDePlantilla("Datos",
        [
            new(Operacion, TipoDeValor.Texto, Largo: 40,
                Reglas: "un hecho de Cartera o * (vacío = *). Sólo los roles de resultado admiten fijar el hecho; en los demás va *. Hechos: "
                    + string.Join(", ", OperacionesDeCartera.DeNegocio.Select(o => o.Codigo)),
                Ejemplo: LendingPostingRule.Cualquiera),
            new(Rol, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "uno de: " + string.Join(", ", R.Todos.Select(r => r.Codigo)), Ejemplo: R.Capital),
            new(Linea, TipoDeValor.Texto, Largo: 10, Reglas: Dimension(DimensionDeCartera.CreditLine) + "; código de la línea de crédito", Ejemplo: "*"),
            new(Modalidad, TipoDeValor.Texto, Largo: 30, Reglas: Dimension(DimensionDeCartera.Modality) + "; del catálogo de modalidades vigente", Ejemplo: "CONSUMO"),
            new(Subsegmento, TipoDeValor.Texto, Largo: 20, Reglas: Dimension(DimensionDeCartera.Subsegment) + "; tiene que ser de la modalidad", Ejemplo: "ConLibranza"),
            new(Categoria, TipoDeValor.Texto, Largo: 2, Reglas: Dimension(DimensionDeCartera.RiskCategory) + "; A a E (E1 y E2 van a E)", Ejemplo: "A"),
            new(Concepto, TipoDeValor.Texto, Largo: 30,
                Reglas: Dimension(DimensionDeCartera.Concept) + "; uno de: " + string.Join(", ", R.Conceptos), Ejemplo: "*"),
            new(Empleado, TipoDeValor.SiNoIndiferente, Reglas: Dimension(DimensionDeCartera.IsEmployee) + "; S, N o vacío (= cualquiera)", Ejemplo: null),
            new(Gracia, TipoDeValor.SiNoIndiferente, Reglas: Dimension(DimensionDeCartera.InGracePeriod) + "; S, N o vacío", Ejemplo: null),
            new(PorCuentaDelAsociado, TipoDeValor.SiNoIndiferente, Reglas: Dimension(DimensionDeCartera.OnBehalfOfAssociate) + "; S, N o vacío", Ejemplo: null),
            new(Reestructurado, TipoDeValor.SiNoIndiferente, Reglas: Dimension(DimensionDeCartera.Restructured) + "; S, N o vacío", Ejemplo: null),
            new(ClaseDeRecaudador, TipoDeValor.Texto, Largo: 20,
                Reglas: Dimension(DimensionDeCartera.CollectorClass) + "; una de: " + string.Join(", ", R.ClasesDeRecaudador), Ejemplo: null),
            new(Motivo, TipoDeValor.Texto, Largo: 40,
                Reglas: string.Join("; ", R.Todos.Where(r => r.Motivos is not null).Select(r => $"{r.Codigo}: {string.Join(", ", r.Motivos!)}")),
                Ejemplo: null),
            new(Naturaleza, TipoDeValor.Texto, Largo: 15,
                Reglas: Dimension(DimensionDeCartera.LineNature) + "; Deposit (captaciones) o Contribution (aportes); los roles de captaciones no admiten Placement",
                Ejemplo: null),
            new(ClaseDeCaptacion, TipoDeValor.Texto, Largo: 15,
                Reglas: Dimension(DimensionDeCartera.DepositClass) + "; uno de: " + string.Join(", ", R.ClasesDeCaptacion), Ejemplo: "Demand"),
            new(PlazoDeEmision, TipoDeValor.Texto, Largo: 20,
                Reglas: Dimension(DimensionDeCartera.IssueTermBand) + "; obligatorio en el capital de CDAT y contractual; uno de: " + string.Join(", ", R.PlazosDeEmision),
                Ejemplo: null),
            new(ClaseDeAporte, TipoDeValor.Texto, Largo: 15,
                Reglas: Dimension(DimensionDeCartera.ContributionClass) + "; uno de: " + string.Join(", ", R.ClasesDeAporte), Ejemplo: null),
            new(MarcaDeCuenta, TipoDeValor.Texto, Largo: 15,
                Reglas: Dimension(DimensionDeCartera.AccountFlag) + "; uno de: " + string.Join(", ", R.MarcasDeCuenta) + " (la inactividad es otro rol)", Ejemplo: "Standard"),
            new(Sucursal, TipoDeValor.Sucursal, Reglas: Dimension(DimensionDeCartera.Branch) + "; código o nombre; vacío o * = cualquiera", Ejemplo: "*"),
            new(CentroDeCosto, TipoDeValor.CentroDeCosto,
                Reglas: "opcional en todos: se asigna a la línea si la cuenta maneja centro y no se compara; código o nombre", Ejemplo: "*"),
            new(Cuenta, TipoDeValor.CuentaContable, Obligatoria: true,
                Reglas: "auxiliar de movimiento, activa y habilitada para Cartera; una fila sugerida que vuelve sin cuenta se ignora",
                Ejemplo: "el código completo de la auxiliar"),
            new(CuentaSugerida, TipoDeValor.Texto, Largo: 60,
                Reglas: "informativa: la cuenta CUIF que se sugiere para la fila (sugerencia, pendiente de la contadora); no se aplica al importar", Ejemplo: null),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true,
                Reglas: "la misma llave con una fecha posterior es una versión nueva y cierra la anterior la víspera; no puede empezar en o antes de lo ya contabilizado",
                Ejemplo: "AAAA-MM-01"),
            new(VigenteHasta, TipoDeValor.Fecha, Reglas: "vacío = abierta", Ejemplo: null),
            new(Notas, TipoDeValor.Texto, Largo: 300, Reglas: "quién decidió y por qué", Ejemplo: "Aprobado por la contadora"),
        ]),
    ]);

    /// <summary>Qué roles exigen o admiten la dimensión (para la hoja «Instrucciones»).</summary>
    private static string Dimension(DimensionDeCartera dimension)
    {
        var exigen = R.Todos.Where(r => r.Exigidas.Contains(dimension)).Select(r => r.Codigo).ToList();
        var admiten = R.Todos.Where(r => r.Opcionales.Contains(dimension)).Select(r => r.Codigo).ToList();
        var partes = new List<string>();
        if (exigen.Count > 0) partes.Add("obligatoria en " + string.Join(", ", exigen));
        if (admiten.Count > 0) partes.Add("opcional en " + string.Join(", ", admiten));
        if (dimension is DimensionDeCartera.Modality or DimensionDeCartera.Concept) partes.Add("obligatoria también en GastoDeterioro y RecuperacionDeterioro con motivo Individual");
        return partes.Count == 0 ? "no la usa ningún rol" : string.Join("; ", partes);
    }
}

/// <summary>
/// <c>POST /api/accounting/lending/rules/import?mode=review|apply</c> (feature 013, T0643; plantillas.md §0.5 y §9): la mecánica común de
/// <see cref="EjecutorDeImportacion"/> —revisión que no guarda, aplicación todo o nada con 422 <c>Import.Invalid</c> y
/// <c>data.errors[] { row, column, code }</c>— y <b>las mismas reglas que el alta una a una</b>, porque cada fila pasa por
/// <see cref="ReglasDeLaMatrizDeCartera.ValidarAsync"/>. La llave es <c>DimensionKey</c> + <c>vigenteDesde</c>: la misma llave es «sin
/// cambio» (o actualiza notas y <c>vigenteHasta</c>); la misma clave con una fecha posterior es una versión nueva. Una fila sugerida
/// («pendiente de validar») sin cuenta se ignora. Queda auditado como <c>Accounting.LendingRules.Imported</c>. (nuevo)
/// </summary>
public sealed record ImportLendingPostingRulesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportLendingPostingRulesCommandValidator : AbstractValidator<ImportLendingPostingRulesCommand>
{
    public ImportLendingPostingRulesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportLendingPostingRulesCommand>.LargoMaximo);
    }
}

public sealed class ImportLendingPostingRulesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    ReglasDeLaMatrizDeCartera reglas,
    AccountingAuditEmitter auditoria)
    : IRequestHandler<ImportLendingPostingRulesCommand, Result<ImportResultDto>>
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public async Task<Result<ImportResultDto>> Handle(ImportLendingPostingRulesCommand request, CancellationToken ct)
    {
        var r = await ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);
        if (r.IsSuccess && r.Value.Applied)
        {
            var hoja = r.Value.Sheets.FirstOrDefault();
            await auditoria.EmitAsync(P.EventoImportada, "LendingPostingRule", null, null,
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
        var porClave = (await db.LendingPostingRules.Where(r => !r.IsDeleted).ToListAsync(ct))
            .GroupBy(r => r.DimensionKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        foreach (var fila in ctx.Datos.Filas.OrderBy(f => f.Crudo(P.VigenteDesde), StringComparer.Ordinal).ThenBy(f => f.Numero))
            await FilaAsync(ctx, fila, catalogos, cuentas, sucursales, centros, porClave, ct);
    }

    private async Task FilaAsync(
        ContextoDeImportacion ctx, FilaDeImportacion fila, CatalogosDeLaMatrizDeCartera catalogos,
        CatalogoCitado<int> cuentas, CatalogoCitado<int> sucursales, CatalogoCitado<int> centros,
        Dictionary<string, List<LendingPostingRule>> porClave, CancellationToken ct)
    {
        // Una fila sugerida que la contadora todavía no diligenció (C70): sin cuenta y con la marca, no es un error.
        if (fila.EstaVacia(P.Cuenta) && string.Equals(fila.Crudo(P.Notas), P.PendienteDeValidar, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Registrar(fila, $"sugerida:{fila.Numero}", AccionDeImportacion.Unchanged);
            return;
        }

        var operacion = Comodin(fila.Texto(P.Operacion, 40)) ?? LendingPostingRule.Cualquiera;
        var rol = fila.Texto(P.Rol, 40);
        var linea = Comodin(fila.Texto(P.Linea, 10));
        var modalidad = Comodin(fila.Texto(P.Modalidad, 30));
        var subsegmento = Comodin(fila.Texto(P.Subsegmento, 20));
        var categoria = Comodin(fila.Texto(P.Categoria, 2));
        var concepto = Comodin(fila.Texto(P.Concepto, 30));
        var empleado = Booleano(fila, P.Empleado);
        var gracia = Booleano(fila, P.Gracia);
        var porCuenta = Booleano(fila, P.PorCuentaDelAsociado);
        var reestructurado = Booleano(fila, P.Reestructurado);
        var clase = Comodin(fila.Texto(P.ClaseDeRecaudador, 20));
        var motivo = Comodin(fila.Texto(P.Motivo, 40));
        // Feature 014 (T144): las cinco dimensiones de captaciones (faltan en un archivo de la versión 1: valen «*»).
        var naturaleza = Comodin(fila.Texto(P.Naturaleza, 15));
        var claseDeCaptacion = Comodin(fila.Texto(P.ClaseDeCaptacion, 15));
        var plazo = Comodin(fila.Texto(P.PlazoDeEmision, 20));
        var claseDeAporte = Comodin(fila.Texto(P.ClaseDeAporte, 15));
        var marca = Comodin(fila.Texto(P.MarcaDeCuenta, 15));
        int? sucursal = Comodin(fila.Crudo(P.Sucursal)) is null ? null : fila.Referencia(P.Sucursal, sucursales, "una sucursal", "en Maestros → Agencias");
        int? centro = Comodin(fila.Crudo(P.CentroDeCosto)) is null ? null : fila.Referencia(P.CentroDeCosto, centros, "un centro de costo", "en Maestros → Centros de costo");
        int? cuenta = fila.Referencia(P.Cuenta, cuentas, "una cuenta", "en Contabilidad → Plan de cuentas");
        var desde = fila.Fecha(P.VigenteDesde);
        var hasta = fila.Fecha(P.VigenteHasta);
        var notas = fila.Texto(P.Notas, 300);
        if (fila.TieneErrores || rol is null || cuenta is not { } idCuenta || desde is not { } validFrom) return;

        var propuesta = new ReglaPropuestaDeCartera(operacion, rol, idCuenta, validFrom, hasta, linea, modalidad, subsegmento, categoria, concepto,
            empleado, gracia, porCuenta, reestructurado, clase, motivo, sucursal, centro, naturaleza, claseDeCaptacion, plazo, claseDeAporte, marca);

        var forma = ReglasDeLaMatrizDeCartera.Forma(propuesta.Operation, propuesta.Role, propuesta.ReasonCode, propuesta.Presentes, propuesta.DepositClass);
        if (forma.Count > 0)
        {
            foreach (var error in forma) fila.Error(ColumnaDe(error), error.Code, error.Message);
            return;
        }

        var clave = propuesta.Clave;
        if (!ctx.Datos.LlaveUnica(fila, clave + "@" + validFrom.ToString("yyyy-MM-dd", Invariante), P.VigenteDesde)) return;
        var versiones = porClave.TryGetValue(clave, out var lista) ? lista : [];

        var misma = versiones.FirstOrDefault(v => v.ValidFrom == validFrom);
        if (misma is not null)
        {
            await ExistenteAsync(ctx, fila, misma, propuesta, notas, ct);
            return;
        }

        var validacion = await reglas.ValidarAsync(propuesta, catalogos, versiones, esVersion: versiones.Count > 0, ct);
        foreach (var aviso in validacion.Avisos) fila.Aviso(P.Cuenta, aviso.Code, aviso.Message);
        if (!validacion.EsValida)
        {
            foreach (var error in validacion.Errores) fila.Error(ColumnaDe(error), error.Code, error.Message);
            return;
        }

        foreach (var anterior in validacion.ACerrar) anterior.CerrarVigencia(validFrom.AddDays(-1));
        var regla = propuesta.ComoRegla(ReglasDeMatrizDeCarteraComunes.Notas(notas, ctx.Motivo));
        db.LendingPostingRules.Add(regla);
        if (lista is null) porClave[clave] = [regla];
        else lista.Add(regla);
        ctx.Registrar(fila, clave, AccionDeImportacion.Create,
        [
            new(P.Cuenta, null, fila.Crudo(P.Cuenta)),
            new(P.VigenteDesde, null, validFrom.ToString("yyyy-MM-dd", Invariante)),
            .. validacion.ACerrar.Select(a => new CampoCambiadoDto(P.VigenteHasta, null, validFrom.AddDays(-1).ToString("yyyy-MM-dd", Invariante))),
        ]);
    }

    /// <summary>
    /// La misma llave (clave y fecha): la cuenta no se cambia en su sitio —lo contabilizado dejaría de explicarse—, así que otra cuenta es
    /// <c>Overlaps</c> (se agrega una versión con otra fecha). Cambian las notas y <c>vigenteHasta</c> (con la regla de la desactivación).
    /// </summary>
    private async Task ExistenteAsync(
        ContextoDeImportacion ctx, FilaDeImportacion fila, LendingPostingRule regla, ReglaPropuestaDeCartera propuesta, string? notas, CancellationToken ct)
    {
        if (regla.AccountId != propuesta.AccountId)
        {
            var error = AccountingErrors.LendingRuleOverlaps(new { rulePublicId = regla.PublicId, validFrom = regla.ValidFrom, validTo = regla.ValidTo, dimensionKey = regla.DimensionKey });
            fila.Error(P.Cuenta, error.Code, error.Message + " Para cambiar la cuenta, agregue una fila con una fecha posterior.");
            return;
        }

        var campos = new List<CampoCambiadoDto>();
        if (propuesta.ValidTo != regla.ValidTo)
        {
            if (propuesta.ValidTo is not { } hasta)
            {
                fila.Error(P.VigenteHasta, "Accounting.LendingRule.ValidToInvalid", "Una vigencia cerrada no se reabre desde la plantilla: cree una versión nueva.");
                return;
            }
            if (hasta < regla.ValidFrom.AddDays(-1))
            {
                var invalida = AccountingErrors.LendingRuleValidToInvalid(regla.ValidFrom, hasta);
                fila.Error(P.VigenteHasta, invalida.Code, invalida.Message);
                return;
            }
            if (await reglas.RetroactividadDelCierreAsync(regla, hasta, ct) is { } retro)
            {
                fila.Error(P.VigenteHasta, retro.Code, retro.Message);
                return;
            }
            campos.Add(new(P.VigenteHasta, regla.ValidTo?.ToString("yyyy-MM-dd", Invariante), hasta.ToString("yyyy-MM-dd", Invariante)));
            regla.CerrarVigencia(hasta);
        }
        if (notas is not null && !string.Equals(notas, regla.Notes, StringComparison.Ordinal))
        {
            campos.Add(new(P.Notas, regla.Notes, notas));
            regla.Notes = notas;
        }
        ctx.Registrar(fila, regla.DimensionKey, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
    }

    /// <summary>Sí, no o cualquiera; <c>*</c> es lo mismo que vacío (plantillas.md §9).</summary>
    private static bool? Booleano(FilaDeImportacion fila, string columna) =>
        Comodin(fila.Crudo(columna)) is null ? null : fila.SiNoIndiferente(columna);

    private static string? Comodin(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == LendingPostingRule.Cualquiera ? null : valor.Trim();

    /// <summary>La columna de la plantilla a la que pertenece un error de <see cref="ReglasDeLaMatrizDeCartera"/>.</summary>
    private static string ColumnaDe(Error error)
    {
        var dimension = error is ErrorConDatos { Data: { } datos }
            ? datos.GetType().GetProperty("dimension")?.GetValue(datos) as string
            : null;
        return dimension switch
        {
            "creditLineCode" => P.Linea,
            "modalityCode" => P.Modalidad,
            "subsegmentCode" => P.Subsegmento,
            "riskCategory" => P.Categoria,
            "conceptCode" => P.Concepto,
            "isEmployee" => P.Empleado,
            "inGracePeriod" => P.Gracia,
            "onBehalfOfAssociate" => P.PorCuentaDelAsociado,
            "restructured" => P.Reestructurado,
            "collectorClass" => P.ClaseDeRecaudador,
            "reasonCode" => P.Motivo,
            "branch" => P.Sucursal,
            "costCenter" => P.CentroDeCosto,
            "operation" => P.Operacion,
            "lineNature" => P.Naturaleza,
            "depositClass" => P.ClaseDeCaptacion,
            "issueTermBand" => P.PlazoDeEmision,
            "contributionClass" => P.ClaseDeAporte,
            "accountFlag" => P.MarcaDeCuenta,
            _ => error.Code switch
            {
                "Accounting.LendingRule.OperationUnknown" => P.Operacion,
                "Accounting.LendingRule.RoleNotInOperation" => P.Rol,
                "Accounting.LendingRule.ValidToInvalid" => P.VigenteHasta,
                "Accounting.LendingRule.LineNatureMismatch" => P.Naturaleza,
                "Accounting.LendingRule.Overlaps" or "Accounting.LendingRule.RetroactiveOverPosted" => P.VigenteDesde,
                _ when error.Code.StartsWith("Accounting.Account.", StringComparison.Ordinal) || error.Code == "Accounting.NotInitialized" => P.Cuenta,
                _ => P.Operacion,
            },
        };
    }
}
