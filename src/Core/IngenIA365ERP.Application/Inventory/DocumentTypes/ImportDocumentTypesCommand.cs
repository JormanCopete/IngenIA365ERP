using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
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
/// violar desde aquí). Cada versión pasa además por las reglas de Inventario de <see cref="IReglasDePoliticaDeAprobacion"/>
/// (US3, T286): <c>Approvals.Policy.ValidFromInClosedPeriod</c>.
/// </para>
/// <para>
/// Las cinco columnas del modo de paso (US3, T286; plantillas.md §8, FR-075) escriben vigencias de <c>Contabilidad.ModoDePaso</c>,
/// <c>.Granularidad</c>, <c>.DisparadorDeLote</c> y <c>.HoraDeLote</c> con ámbito tipo de documento desde <c>vigenciaDelModo</c> (vacía =
/// hoy), sin cruces (<c>Parameters.Overlaps</c>) ni en un período cerrado (<c>Parameters.ValidFromInClosedPeriod</c>); lo igual a la
/// vigencia propia del tipo es «sin cambio». Sobre lo que quedaría (archivo + lo existente): todos los tipos activos de una
/// cadena tienen el mismo <c>modoDePaso</c> a la misma fecha (<c>Inventory.PostingMode.ChainMismatch</c> nombrando la cadena y sus
/// tipos); dejar <c>NoPasa</c> un tipo fiscal exige <c>Inventory.DocumentTypes.DisableFiscalPosting</c> y, en la aplicación, repetir
/// sus códigos en <c>confirmFiscalWithoutPosting</c> —la revisión los devuelve en <c>extra.fiscalTypesWithoutPosting</c>— (si no,
/// <c>Inventory.PostingMode.FiscalRequiresConfirmation</c>). En el saldo inicial y en las clases sin mensajes contables el modo se
/// ignora con el aviso <c>Import.Cell.Ignored</c>. Las vigencias se escriben después del primer guardado (los tipos nuevos ya
/// tienen Id). Bodegas por código y canal siguen respondiendo <c>Import.Cell.NotYetAvailable</c> (hasta entonces <c>*</c> o
/// vacío). Los permisos por columna de §0.7 los exige el ejecutor. (nuevo)
/// </para>
/// </summary>
public sealed record ImportDocumentTypesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>Los códigos de los tipos fiscales que la aplicación deja sin paso, separados por coma (plantillas.md §8).</summary>
    public string? ConfirmFiscalWithoutPosting { get; init; }
}

public sealed class ImportDocumentTypesCommandValidator : AbstractValidator<ImportDocumentTypesCommand>
{
    public ImportDocumentTypesCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportDocumentTypesCommand>.LargoMaximo);
    }
}

public sealed class ImportDocumentTypesCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    IDateTimeService reloj,
    ILectorDeParametros? parametros = null,
    IReglasDePoliticaDeAprobacion? reglasDePolitica = null)
    : IRequestHandler<ImportDocumentTypesCommand, Result<ImportResultDto>>
{
    /// <summary>El largo de <c>COR_ApprovalPolicies.Reason</c>.</summary>
    private const int LargoDelMotivoDePolitica = 300;

    /// <summary>La clave de <c>extra</c> con los códigos de los tipos fiscales que quedarían sin paso.</summary>
    public const string ExtraFiscalesSinPaso = "fiscalTypesWithoutPosting";

    /// <summary>El aviso de una celda que la clase ignora (nuevo, T286).</summary>
    public const string AvisoIgnorada = "Import.Cell.Ignored";

    /// <summary>Las claves que escriben las columnas del modo de paso, con su columna.</summary>
    private static readonly (string Columna, string Clave)[] ClavesDelModo =
    [
        (P.ModoDePaso, ParametrosDeInventario.ContabilidadModoDePaso),
        (P.Granularidad, ParametrosDeInventario.ContabilidadGranularidad),
        (P.DisparadorDeLote, ParametrosDeInventario.ContabilidadDisparadorDeLote),
        (P.HoraDeLote, ParametrosDeInventario.ContabilidadHoraDeLote),
    ];

    /// <summary>Un valor del modo de paso pedido para un tipo (el Id se lee al escribir: los tipos nuevos lo reciben al guardar).</summary>
    private sealed record ModoPedido(FilaDeImportacion Fila, InventoryDocumentType Tipo, string Columna, string Clave, string Valor, DateOnly Desde);

    private readonly List<ModoPedido> _modos = [];

    /// <summary>Los tipos que crea este archivo (todavía no están en la base, aunque el contexto ya les haya dado Id).</summary>
    private readonly HashSet<InventoryDocumentType> _nuevos = new(ReferenceEqualityComparer.Instance);
    private string? _confirmados;

    public Task<Result<ImportResultDto>> Handle(ImportDocumentTypesCommand request, CancellationToken ct)
    {
        _confirmados = request.ConfirmFiscalWithoutPosting;
        return ejecutor.EjecutarAsync(P.Definicion, request, ProcesarAsync, ct, EscribirModosAsync);
    }

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        _modos.Clear();
        _nuevos.Clear();
        // Las vigencias propias de cada tipo de las cuatro claves del modo de paso (para «sin cambio» y la regla de cadena).
        var propias = parametros is null
            ? []
            : (await parametros.VigenciasAsync(ParametrosDeInventario.Modulo, null, ct))
                .Where(v => v.ScopeKind == ParameterScopeKind.DocumentType && ClavesDelModo.Any(c => c.Clave == v.Key))
                .ToList();
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
        Tipos(ctx, tipos, porCodigo, emitidos, inactivaciones, hoy, propias);

        // Con todas las filas aplicadas: inactivar no deja sin tipo activo una clase que el sistema genera solo.
        foreach (var (fila, tipo) in inactivaciones)
        {
            var borradores = abiertos.Where(a => a.DocumentTypeId == tipo.Id && tipo.Id != 0 && a.Status == DocumentStatus.Draft).Sum(a => a.Cantidad);
            var enAprobacion = abiertos.Where(a => a.DocumentTypeId == tipo.Id && tipo.Id != 0 && a.Status == DocumentStatus.PendingApproval).Sum(a => a.Cantidad);
            var quedaOtro = tipos.Any(t => !ReferenceEquals(t, tipo) && !t.IsDeleted && t.Class == tipo.Class && t.IsActive);
            var inactivacion = ReglasDeTipoDeDocumento.Inactivacion(tipo.Class, borradores, enAprobacion, quedaOtro);
            if (inactivacion.IsFailure) Error(fila, P.Activo, inactivacion.Error);
        }

        await ModosDePasoAsync(ctx, tipos, propias, ct);
        await NivelesAsync(ctx, porCodigo, ct);
    }

    // ------------------------------------------------------------------------------------------ TiposDeDocumento --

    private void Tipos(
        ContextoDeImportacion ctx,
        List<InventoryDocumentType> tipos,
        Dictionary<string, InventoryDocumentType> porCodigo,
        IReadOnlyDictionary<(int, string), long?> emitidos,
        List<(FilaDeImportacion, InventoryDocumentType)> inactivaciones,
        DateOnly hoy,
        IReadOnlyList<VigenciaDeParametro> propias)
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
            var vigenciaDelModo = fila.Fecha(P.VigenciaDelModo) ?? hoy;
            var valoresDelModo = new List<(string Columna, string Clave, string Valor)>();
            foreach (var (columna, clave) in ClavesDelModo)
            {
                var crudo = fila.Crudo(columna);
                if (crudo is null) continue;
                var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, clave)!;
                var valor = definicion.Interpretar(crudo, CatalogoDeParametros.EntregaVigente);
                if (!valor.Admitido)
                    fila.Error(columna, ImportErrors.CellFormat,
                        $"«{crudo}» no es admitido en «{columna}». Admite: {string.Join(", ", definicion.Admitidos(CatalogoDeParametros.EntregaVigente))}.");
                else
                    valoresDelModo.Add((columna, clave, valor.Texto!));
            }

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
                _nuevos.Add(tipo);
                var alta = new List<CampoCambiadoDto> { new(P.Nombre, null, nombre), new(P.Clase, null, clase.Value.ToString()) };
                alta.AddRange(ModoDelTipo(ctx, fila, tipo, valoresDelModo, vigenciaDelModo, propias));
                ctx.Registrar(fila, codigo, AccionDeImportacion.Create, alta);
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
            campos.AddRange(ModoDelTipo(ctx, fila, tipo, valoresDelModo, vigenciaDelModo, propias));
            ctx.Registrar(fila, codigo, campos.Count == 0 ? AccionDeImportacion.Unchanged : AccionDeImportacion.Update, campos);
        }
    }

    // ------------------------------------------------------------------------------------------- modo de paso --

    /// <summary>
    /// Lo que una fila pide del modo de paso para su tipo: los valores distintos de la vigencia propia a esa fecha se anotan para
    /// escribir y vuelven como campos cambiados (piden motivo); en el saldo inicial y en las clases sin mensajes contables, el
    /// aviso <see cref="AvisoIgnorada"/>.
    /// </summary>
    private IEnumerable<CampoCambiadoDto> ModoDelTipo(ContextoDeImportacion ctx, FilaDeImportacion fila, InventoryDocumentType tipo,
        IReadOnlyList<(string Columna, string Clave, string Valor)> valores, DateOnly desde, IReadOnlyList<VigenciaDeParametro> propias)
    {
        if (valores.Count == 0) yield break;
        var clase = ClasesDeDocumento.De(tipo.Class);
        if (tipo.Class == DocumentClass.OpeningBalance || !ConfirmacionDeDocumento.EmiteNegocioAContabilidad(clase))
        {
            foreach (var v in valores)
                fila.Aviso(v.Columna, AvisoIgnorada, $"La clase {tipo.Class} no pasa a contabilidad por comprobante: «{v.Columna}» se ignora.");
            yield break;
        }

        foreach (var (columna, clave, valor) in valores)
        {
            var vigente = _nuevos.Contains(tipo)
                ? null
                : propias.Where(v => v.ScopeId == tipo.Id && v.Key == clave && v.VigenteEn(desde)).OrderByDescending(v => v.ValidFrom).FirstOrDefault();
            if (vigente is not null && string.Equals(vigente.Value, valor, StringComparison.Ordinal)) continue;
            _modos.Add(new ModoPedido(fila, tipo, columna, clave, valor, desde));
            ctx.PedirMotivo();
            yield return new CampoCambiadoDto(columna, vigente?.Value, valor);
        }
    }

    /// <summary>
    /// Las reglas del archivo completo sobre el modo de paso (plantillas.md §8): período cerrado y cruces, cadenas y tipos
    /// fiscales sin paso. No escribe: las vigencias van después del primer guardado (<see cref="EscribirModosAsync"/>).
    /// </summary>
    private async Task ModosDePasoAsync(ContextoDeImportacion ctx, List<InventoryDocumentType> tipos, IReadOnlyList<VigenciaDeParametro> propias, CancellationToken ct)
    {
        var ultimoCierre = await db.InventorySetups.AsNoTracking().OrderBy(x => x.Id).Select(x => x.LastClosedDate).FirstOrDefaultAsync(ct);
        foreach (var m in _modos.Where(m => ultimoCierre is { } c && m.Desde <= c).ToList())
        {
            Error(m.Fila, P.VigenciaDelModo, ErroresDeParametros.EnPeriodoCerrado(ultimoCierre!.Value));
            _modos.Remove(m);
        }
        foreach (var m in _modos.Where(m => !_nuevos.Contains(m.Tipo)).ToList())
        {
            var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, m.Clave)!;
            if (await AddParameterVersionCommandHandler.CruceAsync(db, definicion, ParameterScopeKind.DocumentType, m.Tipo.Id, m.Desde, ct) is { } cruce)
            {
                Error(m.Fila, P.VigenciaDelModo, ErroresDeParametros.SeCruza(cruce));
                _modos.Remove(m);
            }
        }

        // Cadenas: sobre lo que quedaría, todos los tipos activos de una cadena con el mismo modo a la misma fecha.
        var modosDePaso = _modos.Where(m => m.Clave == ParametrosDeInventario.ContabilidadModoDePaso).ToList();
        foreach (var porCadena in modosDePaso.GroupBy(m => ClasesDeDocumento.De(m.Tipo.Class).Chain).Where(g => g.Key != PostingChain.None))
        {
            var cadena = porCadena.Key;
            var activos = tipos.Where(t => !t.IsDeleted && t.IsActive && ClasesDeDocumento.De(t.Class).Chain == cadena).OrderBy(t => t.Code).ToList();
            var pedidos = porCadena.ToList();
            var coherente = pedidos.Select(m => (m.Valor, m.Desde)).Distinct().Count() == 1;
            if (coherente)
            {
                var (valor, desde) = (pedidos[0].Valor, pedidos[0].Desde);
                coherente = activos.All(t => pedidos.Any(m => ReferenceEquals(m.Tipo, t))
                    || propias.Any(v => v.ScopeId == t.Id && v.Key == ParametrosDeInventario.ContabilidadModoDePaso && v.VigenteEn(desde) && v.Value == valor));
            }
            if (coherente) continue;
            var nombrados = activos.Select(t => new InventoryErrors.TipoNombrado(t.PublicId, t.Code, t.Name, t.Class.ToString())).ToList();
            foreach (var m in pedidos)
            {
                Error(m.Fila, P.ModoDePaso, InventoryErrors.PostingModeChainMismatch(cadena, nombrados));
                _modos.Remove(m);
            }
        }

        // Tipos fiscales sin paso: permiso y confirmación explícita.
        var fiscales = _modos.Where(m => m.Clave == ParametrosDeInventario.ContabilidadModoDePaso && m.Valor == "NoPasa" && ClasesDeDocumento.De(m.Tipo.Class).IsFiscal).ToList();
        if (fiscales.Count == 0) return;
        if (!ctx.TienePermiso(P.PermisoDeFiscalSinPaso))
        {
            foreach (var m in fiscales)
            {
                m.Fila.Error(P.ModoDePaso, ImportErrors.CellPermissionRequired,
                    $"Dejar sin paso a contabilidad el tipo fiscal {m.Tipo.Code} exige el permiso {P.PermisoDeFiscalSinPaso}.");
                _modos.Remove(m);
            }
            return;
        }
        var codigos = fiscales.Select(m => m.Tipo.Code).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        ctx.Extra[ExtraFiscalesSinPaso] = codigos;
        if (ctx.Modo != ModoDeImportacion.Apply) return;
        var confirmados = (_confirmados ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (codigos.All(confirmados.Contains)) return;
        var nombradosFiscales = fiscales.Select(m => m.Tipo).Distinct()
            .Select(t => new InventoryErrors.TipoNombrado(t.PublicId, t.Code, t.Name, t.Class.ToString())).ToList();
        foreach (var m in fiscales) Error(m.Fila, P.ModoDePaso, InventoryErrors.PostingModeFiscalRequiresConfirmation(nombradosFiscales));
    }

    /// <summary>Tras el primer guardado (los tipos nuevos ya tienen Id): una vigencia por tipo y clave, con el motivo de la importación.</summary>
    private async Task EscribirModosAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        foreach (var m in _modos)
        {
            var definicion = CatalogoDeParametros.Buscar(ParametrosDeInventario.Modulo, m.Clave)!;
            var r = await AddParameterVersionCommandHandler.AgregarVigenciasAsync(db, definicion, ParameterScopeKind.DocumentType, [m.Tipo.Id],
                m.Valor, m.Desde, ctx.Motivo ?? string.Empty, null, ct);
            if (r.IsFailure) Error(m.Fila, P.VigenciaDelModo, r.Error);
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

            if (reglasDePolitica is not null)
            {
                // Las reglas de Inventario (T286): un tipo nuevo todavía no está en la base, así que sólo se le mira el período.
                var evaluada = await reglasDePolitica.EvaluarAsync(
                    new AltaDePoliticaDeAprobacion(ApprovalSubjects.DocumentConfirmation, _nuevos.Contains(tipo) ? null : tipo.PublicId, desde, niveles.Count), ct);
                if (evaluada.IsFailure)
                {
                    Error(filas[0].Fila, P.VigenteDesde, evaluada.Error);
                    continue;
                }
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

public sealed class GetDocumentTypesTemplateDataQueryHandler(IApplicationDbContext db, IDateTimeService reloj, ILectorDeParametros? parametros = null)
    : IRequestHandler<GetDocumentTypesTemplateDataQuery, Result<DatosDePlantilla>>
{
    public async Task<Result<DatosDePlantilla>> Handle(GetDocumentTypesTemplateDataQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        // El modo de paso propio de cada tipo vigente hoy (US3, T286): el mismo libro se revisa «sin cambio».
        var propias = parametros is null
            ? []
            : (await parametros.VigenciasAsync(ParametrosDeInventario.Modulo, null, ct))
                .Where(v => v.ScopeKind == ParameterScopeKind.DocumentType && v.VigenteEn(hoy))
                .ToList();
        string? Propio(int tipo, string clave) => propias.FirstOrDefault(v => v.ScopeId == tipo && v.Key == clave)?.Value;
        DateOnly? DesdeDelModo(int tipo) => propias.Where(v => v.ScopeId == tipo
                && (v.Key == ParametrosDeInventario.ContabilidadModoDePaso || v.Key == ParametrosDeInventario.ContabilidadGranularidad
                    || v.Key == ParametrosDeInventario.ContabilidadDisparadorDeLote || v.Key == ParametrosDeInventario.ContabilidadHoraDeLote))
            .Select(v => (DateOnly?)v.ValidFrom).Max();
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
                Propio(t.Id, ParametrosDeInventario.ContabilidadModoDePaso), Propio(t.Id, ParametrosDeInventario.ContabilidadGranularidad),
                Propio(t.Id, ParametrosDeInventario.ContabilidadDisparadorDeLote), Propio(t.Id, ParametrosDeInventario.ContabilidadHoraDeLote),
                DesdeDelModo(t.Id),
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
