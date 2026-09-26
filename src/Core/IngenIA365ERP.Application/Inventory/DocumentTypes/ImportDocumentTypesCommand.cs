using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Inventory.DocumentTypes.PlantillaDeTiposDeDocumento;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>
/// La plantilla 8 (feature 012, T153; contracts/plantillas.md §8; <c>POST /api/inventory/document-types/import?mode=review|apply</c>):
/// tipos de documento y sus niveles de aprobación en un libro, sobre <see cref="EjecutorDeImportacion"/> y con <b>las
/// mismas reglas que el alta unitaria</b> (<see cref="ReglasDeTipoDeDocumento"/>: clase operable, marcas sólo en su clase,
/// prefijo de hasta 4, sin consecutivo propio en las clases numeradas por resolución DIAN, cambio de consecutivo sin
/// bajar de lo emitido ni cruzar vigencias, inactivar sin documentos abiertos ni dejar sin tipo una clase del sistema).
/// El código es la llave y la clase de un tipo existente no cambia; nada se borra. Cambiar prefijo o número, activar o
/// inactivar y cambiar la política de aprobación piden motivo (<c>requiresReason</c>).
///
/// <para>
/// Hoja <c>NivelesDeAprobacion</c> (permiso <c>Inventory.ApprovalPolicies.Manage</c>): las filas de un tipo reemplazan su
/// política de confirmación con una versión nueva desde <c>vigenteDesde</c> (<see cref="PoliticasDeAprobacion.NuevaVersion"/>,
/// la misma de <c>SaveApprovalPolicyCommand</c>); iguales a la última versión, «sin cambio». Un tipo sin filas conserva su
/// política, así que la plantilla nunca deja un tipo sin aprobación (<c>Approvals.Policy.RequiredForClass</c> no se puede
/// violar desde aquí). <c>Approvals.Policy.ValidFromInClosedPeriod</c> llega con las reglas de Inventario de
/// <see cref="IReglasDePoliticaDeAprobacion"/> (T286), que deben correr también aquí.
/// </para>
/// <para>
/// Lo que responde <c>Import.Cell.NotYetAvailable</c> hasta que exista lo que cita: bodegas por código y canal (US1:
/// hasta entonces <c>*</c> o vacío), y las cinco columnas del modo de paso (las vigencias <c>Contabilidad.*</c> por tipo
/// necesitan las reglas de cadena y de tipo fiscal sin paso de <c>IReglasDeParametros</c>, T286; hasta entonces el tipo
/// hereda el modo general). Los permisos por columna de §0.7 los exige el ejecutor desde ya. (nuevo)
/// </para>
/// </summary>
public sealed record ImportDocumentTypesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportDocumentTypesCommandValidator : AbstractValidator<ImportDocumentTypesCommand>
{
    public ImportDocumentTypesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportDocumentTypesCommand>.LargoMaximo);
    }
}

public sealed class ImportDocumentTypesCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj)
    : IRequestHandler<ImportDocumentTypesCommand, Result<ImportResultDto>>
{
    /// <summary>El largo de <c>COR_ApprovalPolicies.Reason</c>.</summary>
    private const int LargoDelMotivoDePolitica = 300;

    public Task<Result<ImportResultDto>> Handle(ImportDocumentTypesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        // Todo en bloque, con seguimiento: la plantilla modifica en el contexto y el ejecutor guarda o deshace.
        var tipos = await db.InventoryDocumentTypes.Include(t => t.Warehouses).Include(t => t.Sequences).ToListAsync(ct);
        var porCodigo = tipos.ToDictionary(t => t.Code, StringComparer.OrdinalIgnoreCase);

        // Lo ya emitido por tipo y prefijo (también anulados: el número es único por tipo y prefijo).
        var emitidos = (await db.InventoryDocuments.IgnoreQueryFilters()
                .Where(d => d.Number != null)
                .GroupBy(d => new { d.DocumentTypeId, d.Prefix })
                .Select(g => new { g.Key.DocumentTypeId, g.Key.Prefix, Ultimo = g.Max(d => d.Number) })
                .ToListAsync(ct))
            .ToDictionary(e => (e.DocumentTypeId, e.Prefix ?? string.Empty), e => e.Ultimo);
        var abiertos = (await db.InventoryDocuments
                .Where(d => d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval)
                .GroupBy(d => new { d.DocumentTypeId, d.Status })
                .Select(g => new { g.Key.DocumentTypeId, g.Key.Status, Cantidad = g.Count() })
                .ToListAsync(ct))
            .ToList();

        var inactivaciones = new List<(FilaDeImportacion Fila, InventoryDocumentType Tipo)>();
        Tipos(ctx, tipos, porCodigo, emitidos, inactivaciones, hoy);

        // Con todas las filas aplicadas: inactivar no deja sin tipo activo una clase que el sistema genera solo.
        foreach (var (fila, tipo) in inactivaciones)
        {
            var borradores = abiertos.Where(a => a.DocumentTypeId == tipo.Id && tipo.Id != 0 && a.Status == DocumentStatus.Draft).Sum(a => a.Cantidad);
            var enAprobacion = abiertos.Where(a => a.DocumentTypeId == tipo.Id && tipo.Id != 0 && a.Status == DocumentStatus.PendingApproval).Sum(a => a.Cantidad);
            var quedaOtro = tipos.Any(t => !ReferenceEquals(t, tipo) && !t.IsDeleted && t.Class == tipo.Class && t.IsActive);
            var inactivacion = ReglasDeTipoDeDocumento.Inactivacion(tipo.Class, borradores, enAprobacion, quedaOtro);
            if (inactivacion.IsFailure) Error(fila, P.Activo, inactivacion.Error);
        }

        await NivelesAsync(ctx, porCodigo, ct);
    }

    // ------------------------------------------------------------------------------------------ TiposDeDocumento --

    private void Tipos(
        ContextoDeImportacion ctx,
        List<InventoryDocumentType> tipos,
        Dictionary<string, InventoryDocumentType> porCodigo,
        IReadOnlyDictionary<(int, string), long?> emitidos,
        List<(FilaDeImportacion, InventoryDocumentType)> inactivaciones,
        DateOnly hoy)
    {
        var hoja = ctx.Hoja(P.HojaTipos);
        foreach (var fila in hoja.Filas)
        {
            var codigo = fila.Codigo(P.Codigo);
            var nombre = fila.Texto(P.Nombre);
            var clase = fila.Enumeracion(P.Clase, P.EtiquetasDeClase);
            var prefijoCrudo = fila.Texto(P.Prefijo);
            var siguiente = fila.Entero(P.SiguienteNumero);
            var vigenciaDelPrefijo = fila.Fecha(P.VigenciaDelPrefijo) ?? hoy;
            var exigeTercero = fila.SiNo(P.ExigeTercero);
            var exigeCentro = fila.SiNo(P.ExigeCentroDeCosto);
            var exigeMotivo = fila.SiNo(P.ExigeMotivo);
            var exigeReferencia = fila.SiNo(P.ExigeReferenciaExterna);
            var bodegas = fila.Lista(P.Bodegas);
            var canal = fila.Codigo(P.Canal);
            var retiroGravado = fila.SiNo(P.RetiroGravado);
            var ivaNoDescontable = fila.SiNo(P.IvaNoDescontable);
            var fechaFutura = fila.SiNo(P.PermiteFechaFutura);
            var activo = fila.SiNo(P.Activo, porDefecto: true);

            if (prefijoCrudo is not null && !System.Text.RegularExpressions.Regex.IsMatch(prefijoCrudo, ReglasDeTipoDeDocumento.PatronDePrefijo))
                fila.Error(P.Prefijo, ImportErrors.CellFormat, $"«{prefijoCrudo}» no es válido en «{P.Prefijo}». {ReglasDeTipoDeDocumento.MensajeDePrefijo}");
            if (siguiente is < 1)
                fila.Error(P.SiguienteNumero, ImportErrors.CellFormat, "El siguiente número es 1 o mayor.");
            if (bodegas is not null && !FilaDeImportacion.EsTodos(bodegas))
                fila.Error(P.Bodegas, ImportErrors.CellNotYetAvailable,
                    "Las bodegas se eligen por código cuando existan las bodegas de inventario: por ahora escriba * (todas las operativas) o deje la celda vacía.");
            if (canal is not null)
                fila.Error(P.Canal, ImportErrors.CellNotYetAvailable,
                    "El canal se elige por código cuando existan los canales de venta: por ahora deje la celda vacía.");
            foreach (var columna in P.ColumnasDelModoDePaso.Where(c => !fila.EstaVacia(c)))
                fila.Error(columna, ImportErrors.CellNotYetAvailable,
                    $"«{columna}» todavía no se carga por plantilla: déjela vacía y el tipo hereda el modo de paso general.");

            if (!hoja.LlaveUnica(fila, codigo, P.Codigo) || fila.TieneErrores || codigo is null || nombre is null || clase is null) continue;

            // Las reglas del alta unitaria, en el mismo orden.
            var disponible = ReglasDeTipoDeDocumento.ClaseDisponible(clase.Value);
            if (disponible.IsFailure) { fila.Error(P.Clase, ImportErrors.CellNotYetAvailable, disponible.Error.Message); continue; }
            var marcas = ReglasDeTipoDeDocumento.Marcas(clase.Value, new MarcasDelTipo(retiroGravado, ivaNoDescontable, fechaFutura));
            if (marcas.IsFailure) { Error(fila, ColumnaDeMarca(retiroGravado, ivaNoDescontable, clase.Value), marcas.Error); continue; }

            var porResolucion = ClasesDeDocumento.De(clase.Value).NumberedBy == NumberedBy.DianResolution;
            if (porResolucion && siguiente is not null) { Error(fila, P.SiguienteNumero, InventoryErrors.NumberedByResolution(clase.Value)); continue; }
            var prefijo = ReglasDeTipoDeDocumento.Prefijo(prefijoCrudo);

            if (!porCodigo.TryGetValue(codigo, out var tipo))
            {
                tipo = new InventoryDocumentType
                {
                    Code = codigo,
                    Name = nombre,
                    Class = clase.Value,
                    FiscalPrefix = porResolucion && prefijo.Length > 0 ? prefijo : null,
                    RequiresCounterparty = exigeTercero,
                    RequiresCostCenter = exigeCentro,
                    RequiresReason = exigeMotivo,
                    RequiresExternalReference = exigeReferencia,
                    IsTaxableWithdrawal = retiroGravado,
                    VatNonDeductible = ivaNoDescontable,
                    AllowsFutureDate = fechaFutura,
                    AllWarehouses = true,
                    IsActive = activo,
                };
                if (!porResolucion)
                    tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = prefijo, NextValue = siguiente ?? 1, ValidFrom = vigenciaDelPrefijo });
                db.InventoryDocumentTypes.Add(tipo);
                tipos.Add(tipo);
                porCodigo[codigo] = tipo;
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, [new(P.Nombre, null, nombre), new(P.Clase, null, clase.Value.ToString())]);
                continue;
            }

            if (tipo.Class != clase.Value)
            {
                fila.Error(P.Clase, ImportErrors.CellFormat,
                    $"La clase de un tipo existente no cambia: «{tipo.Code}» es {tipo.Class}. Para otra clase, cree otro código.");
                continue;
            }

            var campos = new List<CampoCambiadoDto>();
            Diferencia(campos, P.Nombre, tipo.Name, nombre);
            Diferencia(campos, P.ExigeTercero, SiNo(tipo.RequiresCounterparty), SiNo(exigeTercero));
            Diferencia(campos, P.ExigeCentroDeCosto, SiNo(tipo.RequiresCostCenter), SiNo(exigeCentro));
            Diferencia(campos, P.ExigeMotivo, SiNo(tipo.RequiresReason), SiNo(exigeMotivo));
            Diferencia(campos, P.ExigeReferenciaExterna, SiNo(tipo.RequiresExternalReference), SiNo(exigeReferencia));
            Diferencia(campos, P.RetiroGravado, SiNo(tipo.IsTaxableWithdrawal), SiNo(retiroGravado));
            Diferencia(campos, P.IvaNoDescontable, SiNo(tipo.VatNonDeductible), SiNo(ivaNoDescontable));
            Diferencia(campos, P.PermiteFechaFutura, SiNo(tipo.AllowsFutureDate), SiNo(fechaFutura));

            // El consecutivo: el prefijo de la fila (vacío es un prefijo) y, si viene, el siguiente número.
            if (porResolucion)
            {
                var fiscal = prefijo.Length > 0 ? prefijo : null;
                Diferencia(campos, P.Prefijo, tipo.FiscalPrefix, fiscal);
                tipo.FiscalPrefix = fiscal;
            }
            else
            {
                var vigente = tipo.Sequences.FirstOrDefault(s => !s.IsDeleted && s.ValidTo is null);
                var cambiaPrefijo = vigente is null || vigente.Prefix != prefijo;
                var cambiaNumero = !cambiaPrefijo && siguiente is not null && vigente!.NextValue != siguiente.Value;
                if (cambiaPrefijo || cambiaNumero)
                {
                    var usado = tipo.Sequences.FirstOrDefault(s => !s.IsDeleted && s.Prefix == prefijo);
                    long nuevoSiguiente = siguiente ?? usado?.NextValue ?? 1;
                    var desde = cambiaPrefijo ? vigenciaDelPrefijo : vigente!.ValidFrom;
                    var ultimo = tipo.Id == 0 ? null : emitidos.GetValueOrDefault((tipo.Id, prefijo));
                    var cambio = ReglasDeTipoDeDocumento.CambiarConsecutivo(tipo, prefijo, nuevoSiguiente, desde, ultimo);
                    if (cambio.IsFailure) { Error(fila, cambiaPrefijo ? P.Prefijo : P.SiguienteNumero, cambio.Error); continue; }
                    if (cambiaPrefijo) Diferencia(campos, P.Prefijo, vigente?.Prefix, prefijo);
                    Diferencia(campos, P.SiguienteNumero, Numero(vigente?.NextValue), Numero(nuevoSiguiente));
                    ctx.PedirMotivo();
                }
            }

            if (FilaDeImportacion.EsTodos(bodegas) && !tipo.AllWarehouses)
            {
                Diferencia(campos, P.Bodegas, "(las permitidas)", "*");
                tipo.AllWarehouses = true;
                foreach (var vieja in tipo.Warehouses.Where(w => !w.IsDeleted))
                {
                    vieja.IsDeleted = true;
                    vieja.DeletedAt = reloj.UtcNow;
                }
            }

            if (tipo.IsActive != activo)
            {
                Diferencia(campos, P.Activo, SiNo(tipo.IsActive), SiNo(activo));
                if (!activo) inactivaciones.Add((fila, tipo));
                ctx.PedirMotivo();
            }

            tipo.Name = nombre;
            tipo.RequiresCounterparty = exigeTercero;
            tipo.RequiresCostCenter = exigeCentro;
            tipo.RequiresReason = exigeMotivo;
            tipo.RequiresExternalReference = exigeReferencia;
            tipo.IsTaxableWithdrawal = retiroGravado;
            tipo.VatNonDeductible = ivaNoDescontable;
            tipo.AllowsFutureDate = fechaFutura;
            tipo.IsActive = activo;
            ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }
    }

    /// <summary>La columna de la primera marca que la clase no admite, en el orden de <see cref="ReglasDeTipoDeDocumento.Marcas"/>.</summary>
    private static string ColumnaDeMarca(bool retiroGravado, bool ivaNoDescontable, DocumentClass clase) =>
        retiroGravado && clase != DocumentClass.InternalConsumption ? P.RetiroGravado
        : ivaNoDescontable && ClasesDeDocumento.De(clase).Group != DocumentClassGroup.Purchases ? P.IvaNoDescontable
        : P.PermiteFechaFutura;

    // --------------------------------------------------------------------------------------- NivelesDeAprobacion --

    private async Task NivelesAsync(ContextoDeImportacion ctx, Dictionary<string, InventoryDocumentType> porCodigo, CancellationToken ct)
    {
        var hoja = ctx.Hoja(P.HojaNiveles);
        if (hoja.Filas.Count == 0) return;

        var permisos = (await db.Permissions.AsNoTracking().Select(p => p.Resource + "." + p.Action).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var tiposCitados = CatalogoCitado<InventoryDocumentType>.Desde(porCodigo.Values, t => t, t => t.Code);

        var leidas = new List<(FilaDeImportacion Fila, InventoryDocumentType Tipo, NivelDeAprobacion Nivel, DateOnly Desde)>();
        foreach (var fila in hoja.Filas)
        {
            var tipo = fila.Referencia(P.TipoDeDocumento, tiposCitados, "un tipo de documento", "en la hoja TiposDeDocumento o en Inventario › Tipos de documento");
            var nivel = fila.Entero(P.Nivel);
            var umbral = fila.Monto(P.Umbral);
            var permiso = fila.Texto(P.Permiso);
            var desde = fila.Fecha(P.VigenteDesde);
            if (permiso is not null && !permisos.Contains(permiso))
                Error(fila, P.Permiso, ErroresDeAprobaciones.PermisoDesconocido(permiso));

            var llave = tipo is null || nivel is null ? null : $"{tipo.Code}|{nivel.Value.ToString(CultureInfo.InvariantCulture)}";
            if (!hoja.LlaveUnica(fila, llave, P.Nivel) || fila.TieneErrores || tipo is null || nivel is null || umbral is null || permiso is null || desde is null) continue;
            leidas.Add((fila, tipo, new NivelDeAprobacion(nivel.Value, umbral.Value, permiso), desde.Value));
        }

        var claves = porCodigo.Values
            .Select(t => ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, t.PublicId))
            .ToList();
        var versiones = await db.ApprovalPolicies.Include(p => p.Levels).Where(p => claves.Contains(p.PolicyKey)).ToListAsync(ct);
        var motivo = ctx.Motivo ?? string.Empty;
        if (motivo.Length > LargoDelMotivoDePolitica) motivo = motivo[..LargoDelMotivoDePolitica];

        foreach (var grupo in leidas.GroupBy(l => l.Tipo))
        {
            var filas = grupo.OrderBy(l => l.Nivel.Order).ToList();
            var tipo = grupo.Key;
            var desde = filas[0].Desde;
            var distintas = filas.Where(l => l.Desde != desde).ToList();
            foreach (var otra in distintas)
                otra.Fila.Error(P.VigenteDesde, ImportErrors.CellFormat,
                    $"Todos los niveles de {tipo.Code} llevan la misma vigenteDesde (la del nivel {filas[0].Nivel.Order}: {desde:yyyy-MM-dd}).");
            if (distintas.Count > 0) continue;

            var niveles = filas.Select(l => l.Nivel).ToList();
            if (EvaluadorDePolitica.ValidarNiveles(niveles) is { } invalido)
            {
                Error(filas[0].Fila, P.Nivel, ErroresDeAprobaciones.NivelesInvalidos(invalido));
                continue;
            }

            var clave = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, tipo.PublicId);
            var deLaClave = versiones.Where(v => v.PolicyKey == clave).ToList();
            var ultima = deLaClave.OrderByDescending(v => v.ValidFrom).FirstOrDefault();
            if (ultima is not null && MismosNiveles(ultima, niveles))
            {
                foreach (var l in filas) ctx.Registrar(l.Fila, Llave(tipo, l.Nivel), AccionDeImportacion.Unchanged);
                continue;
            }

            var nueva = PoliticasDeAprobacion.NuevaVersion(deLaClave, ApprovalSubjects.DocumentConfirmation, tipo.PublicId, desde, motivo, niveles);
            if (nueva.IsFailure)
            {
                Error(filas[0].Fila, P.VigenteDesde, nueva.Error);
                continue;
            }
            db.ApprovalPolicies.Add(nueva.Value);
            versiones.Add(nueva.Value);
            ctx.PedirMotivo();
            foreach (var l in filas)
            {
                var antes = ultima?.Levels.FirstOrDefault(n => !n.IsDeleted && n.Order == l.Nivel.Order);
                var campos = new List<CampoCambiadoDto>();
                Diferencia(campos, P.Umbral, Numero(antes?.Threshold), Numero(l.Nivel.Threshold));
                Diferencia(campos, P.Permiso, antes?.PermissionCode, l.Nivel.PermissionCode);
                Diferencia(campos, P.VigenteDesde, ultima?.ValidFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                ctx.Registrar(l.Fila, Llave(tipo, l.Nivel), antes is null ? AccionDeImportacion.Create : AccionDeImportacion.Update, campos);
            }
        }
    }

    private static bool MismosNiveles(ApprovalPolicy politica, IReadOnlyList<NivelDeAprobacion> niveles)
    {
        var actuales = politica.Levels.Where(l => !l.IsDeleted).OrderBy(l => l.Order).ToList();
        return actuales.Count == niveles.Count
            && actuales.Zip(niveles.OrderBy(n => n.Order)).All(p =>
                p.First.Order == p.Second.Order && p.First.Threshold == p.Second.Threshold
                && string.Equals(p.First.PermissionCode, p.Second.PermissionCode, StringComparison.Ordinal));
    }

    private static string Llave(InventoryDocumentType tipo, NivelDeAprobacion nivel) =>
        $"{tipo.Code}|{nivel.Order.ToString(CultureInfo.InvariantCulture)}";

    // --------------------------------------------------------------------------------------------- auxiliares --

    private static void Diferencia(List<CampoCambiadoDto> campos, string columna, string? antes, string? despues)
    {
        var a = string.IsNullOrEmpty(antes) ? null : antes;
        var d = string.IsNullOrEmpty(despues) ? null : despues;
        if (!string.Equals(a, d, StringComparison.Ordinal)) campos.Add(new(columna, a, d));
    }

    private static string? Numero(decimal? valor) => valor?.ToString("0.##", CultureInfo.InvariantCulture);

    private static string? Numero(long? valor) => valor?.ToString(CultureInfo.InvariantCulture);

    private static string SiNo(bool valor) => valor ? "sí" : "no";

    private static void Error(FilaDeImportacion fila, string columna, Error error) => fila.Error(columna, error.Code, error.Message);
}

/// <summary>
/// Lo que hoy tiene la cooperativa para la plantilla 8 con datos (T153; contracts/plantillas.md §0.6,
/// <c>GET /api/inventory/document-types/template.xlsx?withData=true</c>): cada tipo con su consecutivo vigente y los
/// niveles de su política de confirmación vigente, con los mismos valores que la importación lee, así el mismo libro se
/// revisa «sin cambio». Las bodegas restringidas y el canal quedan vacíos (vacío = sin cambio al importar). (nuevo)
/// </summary>
public sealed record GetDocumentTypesTemplateDataQuery : IRequest<Result<DatosDePlantilla>>;

public sealed class GetDocumentTypesTemplateDataQueryValidator : AbstractValidator<GetDocumentTypesTemplateDataQuery>
{
    public GetDocumentTypesTemplateDataQueryValidator() => RuleFor(x => x).NotNull();
}

public sealed class GetDocumentTypesTemplateDataQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetDocumentTypesTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetDocumentTypesTemplateDataQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var tipos = await db.InventoryDocumentTypes.AsNoTracking().Include(t => t.Sequences).OrderBy(t => t.Code).ToListAsync(ct);
        var porPublicId = tipos.ToDictionary(t => t.PublicId);
        var politicas = await db.ApprovalPolicies.AsNoTracking().Include(p => p.Levels)
            .Where(p => p.Module == ApprovalPolicy.ModuloInventario && p.Subject == ApprovalSubjects.DocumentConfirmation && p.DocumentTypePublicId != null)
            .ToListAsync(ct);

        var filasDeTipos = tipos.Select(t =>
        {
            var vigente = t.Sequences.Where(s => !s.IsDeleted && s.ValidTo is null).OrderByDescending(s => s.ValidFrom).FirstOrDefault();
            var porResolucion = ClasesDeDocumento.De(t.Class).NumberedBy == NumberedBy.DianResolution;
            return (IReadOnlyList<object?>)
            [
                t.Code, t.Name, t.Class.ToString(),
                porResolucion ? t.FiscalPrefix : vigente?.Prefix,
                porResolucion ? null : vigente?.NextValue,
                porResolucion ? null : vigente?.ValidFrom,
                t.RequiresCounterparty, t.RequiresCostCenter, t.RequiresReason, t.RequiresExternalReference,
                t.AllWarehouses ? "*" : null, null,
                t.IsTaxableWithdrawal, t.VatNonDeductible, t.AllowsFutureDate,
                null, null, null, null, null,
                t.IsActive,
            ];
        }).ToList();

        var filasDeNiveles = politicas
            .Where(p => p.VigenteEn(hoy) && porPublicId.ContainsKey(p.DocumentTypePublicId!.Value))
            .OrderBy(p => porPublicId[p.DocumentTypePublicId!.Value].Code, StringComparer.Ordinal)
            .SelectMany(p => p.Levels.Where(l => !l.IsDeleted).OrderBy(l => l.Order).Select(l => (IReadOnlyList<object?>)
                [porPublicId[p.DocumentTypePublicId!.Value].Code, (int)l.Order, l.Threshold, l.PermissionCode, p.ValidFrom]))
            .ToList();

        var filas = new Dictionary<string, IReadOnlyList<IReadOnlyList<object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [P.HojaTipos] = filasDeTipos,
            [P.HojaNiveles] = filasDeNiveles,
        };
        return Result.Success(new DatosDePlantilla(filas));
    }
}
