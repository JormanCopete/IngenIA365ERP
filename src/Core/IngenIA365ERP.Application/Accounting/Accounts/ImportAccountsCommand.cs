using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Accounting.Setup;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Accounts;

// Carga masiva de auxiliares (feature 009 E2, 2026-09-22; pedido del dueño al implantar COOFLOPAL:
// una cooperativa arranca con cientos de auxiliares y crearlas de a una no es viable).
//
// Misma mecánica que la apertura (US13): plantilla con los encabezados en la fila 1, cada fila
// validada con las MISMAS reglas que `CreateAccountCommand`/`UpdateAccountCommand` —no hay una
// segunda puerta con reglas propias— y **con un solo error no se guarda nada**. Una fila cuyo
// código ya existe **actualiza** la cuenta (nombre y reglas) con el mismo candado que la pantalla:
// con movimientos sólo cambia el nombre; una cuenta del catálogo no se edita. Así el archivo es
// idempotente: se corrige y se vuelve a subir sin duplicar nada.

/// <summary>Lo que dejó la importación: cuántas se crearon, cuántas se actualizaron y cuántas ya estaban como el archivo pide.</summary>
public sealed record ImportacionDeCuentasDto(int Created, int Updated, int Unchanged, IReadOnlyList<ErrorDeFila> Errors);

/// <summary>Las columnas de la plantilla, en su orden; los encabezados se comparan sin tildes ni mayúsculas.</summary>
public static class PlantillaDeCuentas
{
    public const string Cuenta = "cuenta";
    public const string Nombre = "nombre";
    public const string AplicaA = "aplicaA";
    public const string ExigeTercero = "exigeTercero";
    public const string ExigeDocumento = "exigeDocumento";
    public const string ExigeCentro = "exigeCentro";
    public const string ExigeSucursal = "exigeSucursal";
    public const string Banco = "banco";
    public const string NumeroDeCuenta = "numeroCuenta";
    public const string ClaseDeImpuesto = "claseImpuesto";
    public const string ConceptoTributario = "conceptoTributario";
    public const string ExigeBase = "exigeBase";
    public const string Tarifa = "tarifa";
    public const string TarifaDesde = "tarifaDesde";

    public static readonly IReadOnlyList<string> Columnas =
    [
        Cuenta, Nombre, AplicaA, ExigeTercero, ExigeDocumento, ExigeCentro, ExigeSucursal,
        Banco, NumeroDeCuenta, ClaseDeImpuesto, ConceptoTributario, ExigeBase, Tarifa, TarifaDesde,
    ];

    public static readonly IReadOnlyList<string> Obligatorias = [Cuenta, Nombre];

    /// <summary>«sí» en cualquiera de sus formas; vacío es «no».</summary>
    public static bool? Booleano(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        if (t.Length == 0) return false;
        return TablaLeida.Normalizar(t) switch
        {
            "si" or "s" or "x" or "1" or "true" or "verdadero" or "sy" => true,
            "no" or "n" or "0" or "false" or "falso" => false,
            _ => null,
        };
    }
}

// -------------------------------------------------------------------------------- plantilla --

public sealed record GetAccountTemplateQuery : IRequest<Result<TablaExportable>>;

public sealed class GetAccountTemplateQueryValidator : AbstractValidator<GetAccountTemplateQuery>;

public sealed class GetAccountTemplateQueryHandler(IApplicationDbContext db) : IRequestHandler<GetAccountTemplateQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(GetAccountTemplateQuery request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Result.Failure<TablaExportable>(AccountingErrors.NotInitialized);

        var columnas = PlantillaDeCuentas.Columnas
            .Select(c => new ColumnaExportable(c, c == PlantillaDeCuentas.Tarifa ? TipoDeColumna.Decimal : c == PlantillaDeCuentas.TarifaDesde ? TipoDeColumna.Fecha : TipoDeColumna.Texto))
            .ToList();
        return Result.Success(new TablaExportable(
            "Plantilla de cuentas auxiliares",
            $"Una fila por cuenta. La empresa mueve en el nivel {setup.MovementLevel}: las auxiliares llevan {LongitudDeAuxiliar.Texto(setup.MovementLevel)}.",
            columnas,
            [],
            null,
            [
                "cuenta: el código completo. Cuelga de la cuenta del plan cuyo código sea su prefijo (por ejemplo 51100501 cuelga de 511005); si esa cuenta no existe todavía, agréguela en una fila anterior del mismo archivo.",
                $"Auxiliares de movimiento: {LongitudDeAuxiliar.Texto(setup.MovementLevel)}. Donde el catálogo no trae subcuentas (3505 EXCEDENTES, por ejemplo) primero se crea la propia de 6 dígitos y debajo la auxiliar.",
                "nombre: hasta 200 caracteres.",
                "aplicaA: los módulos que pueden mover la cuenta, separados por espacios o comas: CNT (contabilidad), NOM (nómina), CAR (cartera), INV (inventario), TES (tesorería), CDT (CDT y ahorros), ACT (activos). Vacío = ninguno, y entonces no la mueve nadie.",
                "exigeTercero, exigeDocumento, exigeCentro, exigeSucursal, exigeBase: «sí» o vacío. Son las reglas que cada línea del comprobante tendrá que cumplir; quedan fijas cuando la cuenta reciba su primer movimiento.",
                "banco: el nombre o el código del banco, sólo en cuentas bancarias (se concilian con extracto). numeroCuenta acompaña a banco.",
                "claseImpuesto: Withholding (retención), Vat (IVA), Ica, Gmf o IncomeTax. conceptoTributario es libre (p. ej. 2365 honorarios). tarifa va en porcentaje (4 = 4 %) con su tarifaDesde (yyyy-MM-dd).",
                "Una fila cuyo código ya existe actualiza esa cuenta. Si la cuenta ya tiene movimientos sólo puede cambiar el nombre: cualquier cambio de reglas es un error y no se guarda nada.",
            ]));
    }
}

// --------------------------------------------------------------------------------- importar --

public sealed record ImportAccountsCommand(string FileName, byte[] Content) : IRequest<Result<ImportacionDeCuentasDto>>;

public sealed class ImportAccountsCommandValidator : AbstractValidator<ImportAccountsCommand>
{
    public ImportAccountsCommandValidator()
    {
        RuleFor(x => x.FileName).NotEmpty().WithMessage("Indique el archivo.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El archivo está vacío.");
    }
}

public sealed class ImportAccountsCommandHandler(
    IApplicationDbContext db,
    ITabularFileReader lector,
    IDateTimeService clock,
    ICurrentUserService user,
    AccountingAuditEmitter audit)
    : IRequestHandler<ImportAccountsCommand, Result<ImportacionDeCuentasDto>>
{
    private sealed record Fila(
        int Numero, string Codigo, string Nombre, IReadOnlyList<string> Modulos,
        bool Tercero, bool Cruce, bool Centro, bool Sucursal,
        CuentaBancariaInput? Banco, CuentaDeImpuestoInput? Impuesto);

    public async Task<Result<ImportacionDeCuentasDto>> Handle(ImportAccountsCommand request, CancellationToken ct)
    {
        var setup = await db.AccountingSetups.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted, ct);
        if (setup is null) return Fallo(AccountingErrors.NotInitialized);

        var lectura = await lector.LeerAsync(request.Content, request.FileName, 1, ct);
        if (lectura.IsFailure) return Fallo(lectura.Error);
        var tabla = lectura.Value;
        var indices = PlantillaDeCuentas.Columnas.ToDictionary(c => c, tabla.IndiceDe, StringComparer.Ordinal);
        foreach (var obligatoria in PlantillaDeCuentas.Obligatorias)
            if (indices[obligatoria] < 0) return Fallo(ArchivosTabulares.SinEncabezado(obligatoria));

        var filasCrudas = tabla.Filas.Where(f => !f.EstaVacia).ToList();
        if (filasCrudas.Count == 0) return Fallo(ArchivosTabulares.Vacio);

        string? Celda(FilaLeida f, string columna) => indices[columna] < 0 ? null : f[indices[columna]]?.Trim();

        var bancos = await db.Banks.AsNoTracking().Where(b => !b.IsDeleted).Select(b => new { b.Id, b.PublicId, b.Name, b.LegacyCode }).ToListAsync(ct);
        var errores = new List<ErrorDeFila>();
        var filas = new List<Fila>(filasCrudas.Count);
        var vistos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cruda in filasCrudas)
        {
            var n = cruda.Numero;
            var codigo = Celda(cruda, PlantillaDeCuentas.Cuenta) ?? string.Empty;
            var nombre = Celda(cruda, PlantillaDeCuentas.Nombre) ?? string.Empty;
            var antes = errores.Count;

            if (codigo.Length == 0 || !codigo.All(char.IsDigit))
                errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.Cuenta, "Accounting.Account.CodeInvalid", codigo.Length == 0 ? "La fila no trae código de cuenta." : $"El código «{codigo}» no es numérico."));
            else if (codigo.Length > LongitudDeAuxiliar.Maximo)
                errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.Cuenta, "Accounting.Account.CodeInvalid", $"El código {codigo} tiene {codigo.Length} dígitos; el máximo es {LongitudDeAuxiliar.Maximo}."));
            else if (vistos.TryGetValue(codigo, out var otra))
                errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.Cuenta, "Accounting.Account.CodeDuplicate", $"El código {codigo} ya viene en la fila {otra}."));
            else vistos[codigo] = n;

            if (nombre.Length == 0 || nombre.Length > 200)
                errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.Nombre, "Accounting.Account.NameInvalid", "El nombre es obligatorio y de hasta 200 caracteres."));

            var modulos = new List<string>();
            foreach (var m in (Celda(cruda, PlantillaDeCuentas.AplicaA) ?? string.Empty).Split([',', ';', ' ', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var codigoModulo = m.ToUpperInvariant();
                if (ModuloContable.EsValido(codigoModulo)) modulos.Add(codigoModulo);
                else errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.AplicaA, "Accounting.Account.ModuleUnknown", $"El módulo «{m}» no existe. Use {string.Join(", ", ModuloContable.Codigos)}."));
            }

            var banderas = new Dictionary<string, bool>();
            foreach (var columna in new[] { PlantillaDeCuentas.ExigeTercero, PlantillaDeCuentas.ExigeDocumento, PlantillaDeCuentas.ExigeCentro, PlantillaDeCuentas.ExigeSucursal, PlantillaDeCuentas.ExigeBase })
            {
                var valor = PlantillaDeCuentas.Booleano(Celda(cruda, columna));
                if (valor is null) errores.Add(new ErrorDeFila(n, columna, "Accounting.Account.FlagInvalid", $"«{Celda(cruda, columna)}» no es sí ni no."));
                banderas[columna] = valor ?? false;
            }

            CuentaBancariaInput? banco = null;
            var nombreBanco = Celda(cruda, PlantillaDeCuentas.Banco);
            if (!string.IsNullOrEmpty(nombreBanco))
            {
                var b = bancos.FirstOrDefault(x => string.Equals(x.Name, nombreBanco, StringComparison.OrdinalIgnoreCase) || string.Equals(x.LegacyCode, nombreBanco, StringComparison.OrdinalIgnoreCase));
                if (b is null) errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.Banco, "Accounting.Account.BankNotFound", $"No hay un banco con nombre o código «{nombreBanco}»."));
                else banco = new CuentaBancariaInput(b.PublicId, Celda(cruda, PlantillaDeCuentas.NumeroDeCuenta));
            }

            CuentaDeImpuestoInput? impuesto = null;
            var clase = Celda(cruda, PlantillaDeCuentas.ClaseDeImpuesto);
            if (!string.IsNullOrEmpty(clase))
            {
                var kind = TaxKindTexto.Parse(clase);
                if (kind is null or TaxKind.None)
                    errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.ClaseDeImpuesto, "Accounting.Account.TaxKindUnknown", $"La clase de impuesto «{clase}» no existe. Use Withholding, Vat, Ica, Gmf o IncomeTax."));
                else
                {
                    var tarifas = new List<TarifaInput>();
                    var textoTarifa = Celda(cruda, PlantillaDeCuentas.Tarifa);
                    var textoDesde = Celda(cruda, PlantillaDeCuentas.TarifaDesde);
                    if (!string.IsNullOrEmpty(textoTarifa))
                    {
                        var limpio = textoTarifa.Replace("%", string.Empty).Trim();
                        if (!limpio.Contains('.')) limpio = limpio.Replace(',', '.');
                        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var porcentaje) || porcentaje < 0m || porcentaje > 100m)
                            errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.Tarifa, "Accounting.Account.RateInvalid", $"«{textoTarifa}» no es un porcentaje entre 0 y 100."));
                        else if (!DateOnly.TryParse(textoDesde, CultureInfo.InvariantCulture, DateTimeStyles.None, out var desde))
                            errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.TarifaDesde, "Accounting.Account.RateInvalid", "Una tarifa lleva su fecha de vigencia (yyyy-MM-dd)."));
                        else tarifas.Add(new TarifaInput(desde, decimal.Round(porcentaje / 100m, 6)));
                    }
                    impuesto = new CuentaDeImpuestoInput(kind.Value.ToString(), Celda(cruda, PlantillaDeCuentas.ConceptoTributario), banderas[PlantillaDeCuentas.ExigeBase], tarifas);
                }
            }
            else if (banderas[PlantillaDeCuentas.ExigeBase])
                errores.Add(new ErrorDeFila(n, PlantillaDeCuentas.ExigeBase, "Accounting.Account.TaxKindUnknown", "«exigeBase» es de las cuentas de impuesto: indique también la clase."));

            if (errores.Count > antes) continue;
            filas.Add(new Fila(n, codigo, nombre, modulos,
                banderas[PlantillaDeCuentas.ExigeTercero], banderas[PlantillaDeCuentas.ExigeDocumento],
                banderas[PlantillaDeCuentas.ExigeCentro], banderas[PlantillaDeCuentas.ExigeSucursal], banco, impuesto));
        }

        // De menor a mayor código: una cuenta propia (350505) se crea antes que su auxiliar (35050501),
        // venga en la fila que venga, y así el padre existe cuando se resuelve la hija.
        filas = filas.OrderBy(f => f.Codigo.Length).ThenBy(f => f.Codigo, StringComparer.Ordinal).ToList();

        var plan = await db.ChartOfAccounts.Include(a => a.TaxRates).Where(a => !a.IsDeleted).ToListAsync(ct);
        var porCodigo = plan.ToDictionary(a => a.Code, StringComparer.OrdinalIgnoreCase);
        var conHijosDelCatalogo = plan.Where(a => a.Origin == AccountOrigin.Catalog && a.ParentId is not null).Select(a => a.ParentId!.Value).ToHashSet();
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "system";
        int creadas = 0, actualizadas = 0, iguales = 0;

        foreach (var fila in filas)
        {
            if (porCodigo.TryGetValue(fila.Codigo, out var existente))
            {
                var resultado = await ActualizarAsync(existente, fila, errores, ahora, quien, ct);
                if (resultado == Cambio.Actualizada) actualizadas++;
                else if (resultado == Cambio.Igual) iguales++;
                continue;
            }

            // Padre: la cuenta viva cuyo código es el prefijo más largo del nuevo (así vale tanto
            // 51100501 → 511005 como 35050501 → 350505 creada en este mismo archivo).
            var padre = plan.Where(a => fila.Codigo.Length > a.Code.Length && fila.Codigo.StartsWith(a.Code, StringComparison.Ordinal))
                .OrderByDescending(a => a.Code.Length).FirstOrDefault();
            if (padre is null)
            {
                errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, "Accounting.Account.ParentNotFound",
                    $"No hay en el plan ninguna cuenta que sea prefijo de {fila.Codigo}: {fila.Codigo} no cuelga de ninguna parte."));
                continue;
            }

            var nivel = (byte)(padre.Level + 1);
            if (nivel > setup.MovementLevel)
            {
                errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, "Accounting.Account.LevelNotAllowed",
                    $"{fila.Codigo} sería de nivel {nivel} y la empresa mueve en el nivel {setup.MovementLevel}."));
                continue;
            }
            if (nivel < 5 && conHijosDelCatalogo.Contains(padre.Id))
            {
                errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, "Accounting.Account.CodeInvalid",
                    $"Bajo {padre.Code} el catálogo ya define sus cuentas: cuelgue la auxiliar de una de ellas."));
                continue;
            }
            if (LongitudDeAuxiliar.Reparo(fila.Codigo, nivel) is { } reparo)
            {
                errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, "Accounting.Account.CodeInvalid", reparo));
                continue;
            }

            var esDeMovimiento = nivel == setup.MovementLevel;
            var cuenta = new ChartOfAccount
            {
                Code = fila.Codigo,
                Name = fila.Nombre,
                Level = nivel,
                Nature = padre.Nature,
                Parent = padre,
                NiifItemCode = padre.NiifItemCode,
                Origin = AccountOrigin.Company,
                IsMovement = esDeMovimiento,
                IsActive = true,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            if (esDeMovimiento)
            {
                var reglas = await CreateAccountCommandHandler.AplicarReglasAsync(db, cuenta, fila.Modulos, fila.Tercero, fila.Cruce, fila.Centro, fila.Sucursal, fila.Banco, fila.Impuesto, ahora, quien, ct);
                if (reglas.IsFailure) { errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, reglas.Error.Code, reglas.Error.Message)); continue; }
            }
            else if (fila.Modulos.Count > 0 || fila.Tercero || fila.Cruce || fila.Centro || fila.Sucursal || fila.Banco is not null || fila.Impuesto is not null)
            {
                errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.AplicaA,
                    "Accounting.Account.NotMovement", $"{fila.Codigo} es de nivel {nivel} y no recibe movimiento: las reglas se ponen en sus auxiliares, no aquí."));
                continue;
            }
            db.ChartOfAccounts.Add(cuenta);
            plan.Add(cuenta);
            porCodigo[cuenta.Code] = cuenta;
            creadas++;
        }

        if (errores.Count > 0)
            return Fallo(AccountingErrors.AccountsInvalid(errores.OrderBy(e => e.Row).ThenBy(e => e.Column, StringComparer.Ordinal).ToList()));

        await db.SaveChangesAsync(ct);
        await audit.EmitAsync("Accounting.Accounts.Imported", nameof(ChartOfAccount), null, null,
            new { file = request.FileName, created = creadas, updated = actualizadas, unchanged = iguales }, ct);
        return Result.Success(new ImportacionDeCuentasDto(creadas, actualizadas, iguales, []));
    }

    private enum Cambio { Igual, Actualizada, Rechazada }

    /// <summary>Una cuenta que ya existe: el mismo candado que la pantalla (FR-012). No toca nada si la fila pide lo que ya tiene.</summary>
    private async Task<Cambio> ActualizarAsync(ChartOfAccount cuenta, Fila fila, List<ErrorDeFila> errores, DateTime ahora, string quien, CancellationToken ct)
    {
        if (cuenta.Origin == AccountOrigin.Catalog)
        {
            errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, AccountingErrors.AccountFromCatalog.Code, $"{cuenta.Code} viene del catálogo: {AccountingErrors.AccountFromCatalog.Message}"));
            return Cambio.Rechazada;
        }

        var cambiaNombre = !string.Equals(cuenta.Name, fila.Nombre, StringComparison.Ordinal);
        var cambianReglas = cuenta.IsMovement && (
            cuenta.EnabledModules != ModuloContable.Desde(fila.Modulos) ||
            cuenta.RequiresThirdParty != fila.Tercero || cuenta.RequiresCrossDocument != fila.Cruce ||
            cuenta.RequiresCostCenter != fila.Centro || cuenta.RequiresBranch != fila.Sucursal ||
            (fila.Banco is null) != (cuenta.BankId is null) ||
            (fila.Impuesto is null) != (cuenta.TaxKind == TaxKind.None) ||
            (fila.Impuesto is not null && (TaxKindTexto.Parse(fila.Impuesto.Kind) != cuenta.TaxKind || fila.Impuesto.RequiresTaxBase != cuenta.RequiresTaxBase)));

        if (cambianReglas && cuenta.FirstMovementAt is { } desde)
        {
            errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.AplicaA, AccountingErrors.AccountLocked(desde).Code,
                $"{cuenta.Code}: {AccountingErrors.AccountLocked(desde).Message}"));
            return Cambio.Rechazada;
        }
        if (!cuenta.IsMovement && (fila.Modulos.Count > 0 || fila.Tercero || fila.Cruce || fila.Centro || fila.Sucursal || fila.Banco is not null || fila.Impuesto is not null))
        {
            errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.AplicaA, "Accounting.Account.NotMovement",
                $"{cuenta.Code} no es de movimiento: las reglas se ponen en sus auxiliares, no aquí."));
            return Cambio.Rechazada;
        }

        if (cuenta.IsMovement)
        {
            var reglas = await CreateAccountCommandHandler.AplicarReglasAsync(db, cuenta, fila.Modulos, fila.Tercero, fila.Cruce, fila.Centro, fila.Sucursal, fila.Banco, fila.Impuesto, ahora, quien, ct);
            if (reglas.IsFailure) { errores.Add(new ErrorDeFila(fila.Numero, PlantillaDeCuentas.Cuenta, reglas.Error.Code, reglas.Error.Message)); return Cambio.Rechazada; }
        }
        cuenta.Name = fila.Nombre;
        if (!cambiaNombre && !cambianReglas) return Cambio.Igual;
        cuenta.UpdatedAt = ahora;
        cuenta.UpdatedBy = quien;
        return Cambio.Actualizada;
    }

    private static Result<ImportacionDeCuentasDto> Fallo(Error error) => Result.Failure<ImportacionDeCuentasDto>(error);
}
