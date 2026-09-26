using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// El motor común de toda importación por plantilla (feature 012, T49, T156; contracts/plantillas.md §0.3–§0.5). Cada
/// comando <c>Import…Command</c> le pasa su <see cref="DefinicionDePlantilla"/> y una función que procesa las filas con
/// las <b>mismas reglas que el alta unitaria</b>; el ejecutor hace lo que es igual para todas:
///
/// <list type="number">
/// <item>sin <c>mode</c>, 400 <c>Import.ModeRequired</c>; archivo vacío, <c>Archivo.Vacio</c>;</item>
/// <item>lee cada hoja por su nombre (la de una sección, la primera o el <c>.csv</c>): hoja obligatoria ausente,
/// <c>Archivo.HojaFaltante</c>; columna obligatoria ausente, <c>Archivo.ColumnaFaltante</c>; columna desconocida, aviso
/// <c>Import.Column.Unknown</c>; más de <see cref="MaximoDeFilasPorHoja"/> filas, <c>Archivo.Ilegible</c>;</item>
/// <item>una hoja o columna que exige un permiso que la persona no tiene y trae datos,
/// <c>Import.Cell.PermissionRequired</c> (§0.7);</item>
/// <item>corre la plantilla, que carga los catálogos citados en bloque y convierte cada celda con
/// <see cref="FilaDeImportacion"/>;</item>
/// <item><c>review</c> deshace todo lo que la plantilla dejó en el contexto y responde 200 aunque haya errores;
/// <c>apply</c> corre en <see cref="TransaccionExplicita"/> y guarda con un solo <c>SaveChanges</c> o nada (422
/// <c>Import.Invalid</c> con el mismo cuerpo en <c>data</c>); sin motivo cuando la revisión lo pide, un error
/// <c>Import.Cell.Required</c> sin fila en la columna <c>reason</c>;</item>
/// <item>si la plantilla lo pide, corre <c>despuesDeGuardar</c> tras el primer guardado (lo que necesita los Id nuevos) y
/// guarda otra vez en la misma transacción;</item>
/// <item>registra el evento de la importación (plantilla, modo, archivo y su SHA-256, conteos) en el módulo de la
/// plantilla; en <c>apply</c>, dentro de la misma transacción.</item>
/// </list>
///
/// Los topes de la respuesta: <see cref="MaximoDeErrores"/> errores y <see cref="MaximoDeCambios"/> cambios; el resto
/// va en la revisión en Excel (<see cref="ImportResultDto.Rows"/>). (nuevo)
/// </summary>
public sealed class EjecutorDeImportacion(
    IApplicationDbContext db,
    ITabularFileReader lector,
    ICurrentUserPermissions permisos,
    IServiceProvider servicios)
{
    public const int MaximoDeErrores = 1000;
    public const int MaximoDeCambios = 5000;
    public const int MaximoDeFilasPorHoja = 60000;
    public const int MaximoDeBytes = 16 * 1024 * 1024;

    /// <summary>El evento de una revisión (nuevo).</summary>
    public const string EventoRevisada = "Import.Reviewed";

    /// <summary>El evento de una aplicación (nuevo).</summary>
    public const string EventoAplicada = "Import.Applied";

    /// <summary>
    /// Corre la importación de <paramref name="comando"/> sobre <paramref name="plantilla"/>. <paramref name="procesar"/>
    /// recibe el contexto con las hojas leídas: carga los catálogos citados, recorre las filas, deja errores y
    /// resultados, y agrega o modifica entidades en el contexto de datos sin guardar.
    /// </summary>
    public async Task<Result<ImportResultDto>> EjecutarAsync(
        DefinicionDePlantilla plantilla,
        IComandoDeImportacion comando,
        Func<ContextoDeImportacion, CancellationToken, Task> procesar,
        CancellationToken ct,
        Func<ContextoDeImportacion, CancellationToken, Task>? despuesDeGuardar = null)
    {
        ArgumentNullException.ThrowIfNull(plantilla);
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(procesar);

        if (comando.Mode is not { } modo) return Result.Failure<ImportResultDto>(ImportErrors.ModeRequired);
        var archivo = comando.File;
        if (archivo?.Contenido is not { Length: > 0 }) return Result.Failure<ImportResultDto>(ArchivosTabulares.Vacio);
        if (archivo.Contenido.Length > MaximoDeBytes)
            return Result.Failure<ImportResultDto>(ArchivosTabulares.Ilegible($"el archivo pesa más de {MaximoDeBytes / (1024 * 1024)} MB."));

        var concedidos = permisos.EsMaestroGlobal
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var contexto = new ContextoDeImportacion(plantilla, modo, comando.Reason, concedidos, permisos.EsMaestroGlobal);

        var lectura = await LeerHojasAsync(plantilla, archivo, contexto, ct);
        if (lectura.IsFailure) return Result.Failure<ImportResultDto>(lectura.Error);

        ComprobarPermisos(contexto);

        return modo == ModoDeImportacion.Review
            ? await RevisarAsync(contexto, archivo, procesar, ct)
            : await AplicarAsync(contexto, archivo, procesar, despuesDeGuardar, ct);
    }

    // --------------------------------------------------------------- lectura --

    private async Task<Result> LeerHojasAsync(DefinicionDePlantilla plantilla, ArchivoDeImportacion archivo, ContextoDeImportacion contexto, CancellationToken ct)
    {
        IReadOnlyList<string> presentes = [];
        if (!plantilla.EsDeUnaHoja)
        {
            var hojas = await lector.ListarHojasAsync(archivo.Contenido, archivo.NombreArchivo, ct);
            if (hojas.IsFailure) return Result.Failure(hojas.Error);
            presentes = hojas.Value;
        }

        foreach (var definicion in plantilla.Hojas)
        {
            TablaLeida tabla;
            if (plantilla.EsDeUnaHoja)
            {
                var leida = await lector.LeerHojaAsync(archivo.Contenido, archivo.NombreArchivo, null, 1, ct);
                if (leida.IsFailure) return Result.Failure(leida.Error);
                tabla = leida.Value;
            }
            else if (!presentes.Any(p => TablaLeida.Normalizar(p) == TablaLeida.Normalizar(definicion.Nombre)))
            {
                if (definicion.Obligatoria) return Result.Failure(ImportErrors.HojaFaltante(definicion.Nombre));
                tabla = new TablaLeida([], [], "xlsx");
            }
            else
            {
                var leida = await lector.LeerHojaAsync(archivo.Contenido, archivo.NombreArchivo, definicion.Nombre, 1, ct);
                if (leida.IsFailure) return Result.Failure(leida.Error);
                tabla = leida.Value;
            }

            var vacia = tabla.Encabezados.Count == 0 && tabla.Filas.Count == 0;
            if (!vacia || definicion.Obligatoria)
            {
                foreach (var columna in definicion.Columnas.Where(c => c.Obligatoria))
                    if (tabla.IndiceDe(columna.Nombre) < 0)
                        return Result.Failure(plantilla.EsDeUnaHoja
                            ? ArchivosTabulares.SinEncabezado(columna.Nombre)
                            : ArchivosTabulares.SinEncabezado(definicion.Nombre, columna.Nombre));
            }

            if (tabla.Filas.Count(f => !f.EstaVacia) > MaximoDeFilasPorHoja)
                return Result.Failure(ArchivosTabulares.Ilegible(
                    $"la hoja «{definicion.Nombre}» tiene más de {MaximoDeFilasPorHoja:N0} filas; pártala en varios archivos."));

            var hoja = new HojaDeImportacion(contexto, definicion, tabla, plantilla.EsDeUnaHoja);
            contexto.AgregarHoja(hoja);

            foreach (var (encabezado, indice) in tabla.Encabezados.Select((e, i) => (e, i)))
            {
                if (string.IsNullOrWhiteSpace(encabezado) || definicion.Columna(encabezado) is not null) continue;
                // «resultado» y «errores» son las columnas que agrega la revisión en Excel: volver a subir ese libro es normal.
                var normalizado = TablaLeida.Normalizar(encabezado);
                if (normalizado is "resultado" or "errores") continue;
                contexto.Aviso(new ErrorDeFila(1, encabezado, ImportErrors.ColumnUnknown,
                    $"La columna «{encabezado}» no es de la plantilla y se ignora. ¿Está bien escrita?", hoja.NombreEnErrores));
            }
        }
        return Result.Success();
    }

    /// <summary>§0.7: una hoja o una columna con permiso propio, con datos, exige ese permiso.</summary>
    private static void ComprobarPermisos(ContextoDeImportacion contexto)
    {
        foreach (var hoja in contexto.Hojas)
        {
            if (hoja.Definicion.Permiso is { } permisoDeHoja && hoja.Filas.Count > 0 && !contexto.TienePermiso(permisoDeHoja))
            {
                contexto.ErrorSinFila(hoja.NombreEnErrores ?? hoja.Nombre, string.Empty, ImportErrors.CellPermissionRequired,
                    $"Para cargar la hoja «{hoja.Nombre}» hace falta el permiso {permisoDeHoja}.");
            }

            foreach (var columna in hoja.Definicion.Columnas.Where(c => c.Permiso is not null))
            {
                if (contexto.TienePermiso(columna.Permiso!)) continue;
                foreach (var fila in hoja.Filas.Where(f => !f.EstaVacia(columna.Nombre)))
                    fila.Error(columna.Nombre, ImportErrors.CellPermissionRequired,
                        $"Llenar «{columna.Nombre}» exige el permiso {columna.Permiso}.");
            }
        }
    }

    // ------------------------------------------------------ revisar y aplicar --

    private async Task<Result<ImportResultDto>> RevisarAsync(
        ContextoDeImportacion contexto, ArchivoDeImportacion archivo,
        Func<ContextoDeImportacion, CancellationToken, Task> procesar, CancellationToken ct)
    {
        var antes = Pendientes();
        try
        {
            await procesar(contexto, ct);
        }
        finally
        {
            // La revisión no guarda nada (FR-030): se deshace lo que la plantilla dejó en el contexto.
            Deshacer(antes);
        }

        var resultado = contexto.Resultado(archivo, aplicado: false);
        await RegistrarAsync(contexto, resultado, EventoRevisada, ct);
        return Result.Success(resultado);
    }

    private async Task<Result<ImportResultDto>> AplicarAsync(
        ContextoDeImportacion contexto, ArchivoDeImportacion archivo,
        Func<ContextoDeImportacion, CancellationToken, Task> procesar,
        Func<ContextoDeImportacion, CancellationToken, Task>? despuesDeGuardar, CancellationToken ct)
    {
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var antes = Pendientes();
            try
            {
                await procesar(contexto, ct);
            }
            catch
            {
                Deshacer(antes);
                throw;
            }

            if (contexto.RequiereMotivo && contexto.Motivo is null)
                contexto.ErrorSinFila(null, ImportErrors.ColumnaDelMotivo, ImportErrors.CellRequired,
                    "El archivo crea o cierra una vigencia o cambia algo que exige decir por qué: escriba el motivo.");

            if (contexto.HayErrores)
            {
                Deshacer(antes);
                return Result.Failure<ImportResultDto>(ImportErrors.Invalid(contexto.Resultado(archivo, aplicado: false)));
            }

            var resultado = contexto.Resultado(archivo, aplicado: true);
            await db.SaveChangesAsync(ct);

            // Lo que necesita los Id recién asignados (la ruta de una categoría nueva, la vigencia de una bodega nueva):
            // un segundo guardado dentro de la misma transacción; un error aquí revierte todo.
            if (despuesDeGuardar is not null)
            {
                await despuesDeGuardar(contexto, ct);
                if (contexto.HayErrores)
                    return Result.Failure<ImportResultDto>(ImportErrors.Invalid(contexto.Resultado(archivo, aplicado: false)));
                await db.SaveChangesAsync(ct);
            }
            await RegistrarAsync(contexto, resultado, EventoAplicada, ct);
            return Result.Success(resultado);
        }, ct);
    }

    /// <summary>
    /// El evento de la importación (§0.5): plantilla, modo, nombre y SHA-256 del archivo, conteos. El antes y después de
    /// cada entidad lo escribe el interceptor al guardar; aquí va el resumen. En un módulo encadenado, una fila de
    /// <c>COR_AuditOutbox</c> (dentro de la transacción de <c>apply</c>); en los demás, Mongo.
    /// </summary>
    private async Task RegistrarAsync(ContextoDeImportacion contexto, ImportResultDto resultado, string accion, CancellationToken ct)
    {
        var conteos = resultado.Sheets.ToDictionary(h => h.Sheet, h => new { h.Rows, h.Created, h.Updated, h.Unchanged });
        var metadata = new Dictionary<string, string>
        {
            ["Template"] = resultado.Template,
            ["Mode"] = resultado.Mode.ToString(),
            ["FileName"] = resultado.FileName.Length > 200 ? resultado.FileName[..200] : resultado.FileName,
            ["FileSha256"] = resultado.FileSha256,
            ["Valid"] = resultado.Valid ? "true" : "false",
            ["TotalErrors"] = resultado.TotalErrors.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (contexto.Motivo is not null) metadata["Reason"] = contexto.Motivo;

        await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
        {
            Action = accion,
            EntityType = resultado.Template,
            EntityId = resultado.FileSha256,
            Module = contexto.Plantilla.Modulo,
            NewValues = new { sheets = conteos, warnings = resultado.Warnings.Count, errors = resultado.TotalErrors },
            Metadata = metadata,
        }, ct);
    }

    // ---------------------------------------------------- deshacer el contexto --

    /// <summary>Lo que ya estaba pendiente en el contexto antes de la plantilla (normalmente nada).</summary>
    private HashSet<object> Pendientes() =>
        db is DbContext ctx
            ? ctx.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).Select(e => e.Entity).ToHashSet(ReferenceEqualityComparer.Instance)
            : [];

    /// <summary>
    /// Deshace lo que la plantilla agregó o cambió, sin tocar lo que ya estaba pendiente ni lo ya guardado por quien
    /// envuelve el comando (la fila de <c>COR_OperationKeys</c> de <c>IdempotencyBehavior</c>, que no es un
    /// <c>ChangeTracker.Clear</c>).
    /// </summary>
    private void Deshacer(HashSet<object> antes)
    {
        if (db is not DbContext ctx) return;
        foreach (var entrada in ctx.ChangeTracker.Entries().ToList())
        {
            if (antes.Contains(entrada.Entity)) continue;
            switch (entrada.State)
            {
                case EntityState.Added:
                    entrada.State = EntityState.Detached;
                    break;
                case EntityState.Modified:
                case EntityState.Deleted:
                    Restaurar(entrada);
                    break;
            }
        }
    }

    private static void Restaurar(EntityEntry entrada)
    {
        entrada.CurrentValues.SetValues(entrada.OriginalValues);
        entrada.State = EntityState.Unchanged;
    }
}
