using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;

namespace IngenIA365ERP.Application.Common.Parameters;

/// <summary>
/// La plantilla de parámetros de un módulo (feature 015, segunda ronda del 2026-10-27, D-l: los parámetros de Recaudo y de Tesorería se cargan por plantilla, sin
/// pantalla). Una fila por vigencia nueva —clave del catálogo del módulo, valor, vigente desde, motivo y, si la clave la exige, la fuente legal—, con las mismas reglas
/// que el alta de una vigencia (<see cref="AddParameterVersionCommandHandler"/>: clave del catálogo, permiso propio, valor admitido, fuente legal, reglas del módulo y sin
/// cruces) y escrita por su mismo escritor (<see cref="AddParameterVersionCommandHandler.AgregarVigenciasAsync"/>), en el ámbito general. Todo o nada. (nuevo)
/// </summary>
public static class PlantillaDeParametrosDeModulo
{
    public const string Clave = "clave";
    public const string Valor = "valor";
    public const string VigenteDesde = "vigenteDesde";
    public const string Motivo = "motivo";
    public const string FuenteLegal = "fuenteLegal";

    /// <summary>Las columnas de la plantilla.</summary>
    public static ColumnaDePlantilla[] Columnas(string ejemploDeClave, string ejemploDeValor) =>
    [
        new(Clave, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Reglas: "una clave del catálogo de parámetros del módulo", Ejemplo: ejemploDeClave),
        new(Valor, TipoDeValor.Texto, Obligatoria: true, Largo: 500, Reglas: "el valor en el formato de la clave (número con punto decimal, Sí/No, una de sus opciones)",
            Ejemplo: ejemploDeValor),
        new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "cierra la vigencia anterior la víspera; no puede haber otra que empiece ese día o después"),
        new(Motivo, TipoDeValor.Texto, Obligatoria: true, Largo: 500),
        new(FuenteLegal, TipoDeValor.Texto, Largo: 300, Reglas: "obligatoria en las claves que la exigen (la norma que fija el valor)"),
    ];

    public static async Task AplicarAsync(ContextoDeImportacion ctx, IApplicationDbContext db, IReglasDeParametros? reglas, string modulo, CancellationToken ct)
    {
        foreach (var fila in ctx.Datos.Filas)
        {
            var clave = fila.Texto(Clave, 120)?.Trim();
            var texto = fila.Texto(Valor, 500);
            var desde = fila.Fecha(VigenteDesde);
            var motivo = fila.Texto(Motivo, 500);
            var fuente = fila.Texto(FuenteLegal, 300);
            if (clave is null || texto is null || desde is null || motivo is null) continue;
            var definicion = CatalogoDeParametros.Buscar(modulo, clave);
            if (definicion is null)
            {
                fila.Error(Clave, ImportErrors.CellNotFound, $"«{clave}» no es un parámetro de {modulo}.");
                continue;
            }
            ctx.Datos.LlaveUnica(fila, $"{clave}|{desde:yyyy-MM-dd}", Clave);
            if (definicion.PermisoAdicional is { } permiso && !ctx.TienePermiso(permiso))
                fila.Error(Clave, ImportErrors.CellPermissionRequired, $"El parámetro «{clave}» exige además el permiso {permiso}.");
            var valor = definicion.Interpretar(texto);
            if (!valor.Admitido) fila.Error(Valor, ImportErrors.CellFormat, ErroresDeParametros.ValorNoAdmitido(definicion, texto).Message);
            if (definicion.ExigeFuenteLegal && string.IsNullOrWhiteSpace(fuente))
                fila.Error(FuenteLegal, ImportErrors.CellRequired, $"El parámetro «{clave}» exige la fuente legal.");
            if (fila.TieneErrores) continue;
            if (reglas is not null)
            {
                var decision = await reglas.EvaluarAsync(new AltaDeParametro(definicion, ParameterScopeKind.None, null, null, valor.Texto!, desde.Value, false), ct);
                if (decision.IsFailure)
                {
                    fila.Error(Valor, decision.Error.Code, decision.Error.Message);
                    continue;
                }
            }
            var agregada = await AddParameterVersionCommandHandler.AgregarVigenciasAsync(db, definicion, ParameterScopeKind.None, [0], valor.Texto!, desde.Value,
                motivo, fuente, ct);
            if (agregada.IsFailure)
            {
                fila.Error(VigenteDesde, agregada.Error.Code, agregada.Error.Message);
                continue;
            }
            ctx.Registrar(fila, $"{clave}@{desde:yyyy-MM-dd}", AccionDeImportacion.Create, [new CampoCambiadoDto(Valor, null, valor.Texto)]);
        }
    }
}
