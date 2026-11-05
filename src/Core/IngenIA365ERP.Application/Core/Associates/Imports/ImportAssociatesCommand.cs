using FluentValidation;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.AssociateGroups;
using IngenIA365ERP.Application.Core.Associates.Contracts;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Core.Associates.Imports.PlantillaDeAsociados;

namespace IngenIA365ERP.Application.Core.Associates.Imports;

/// <summary>
/// <c>POST /api/core/associates/import?mode=review|apply</c> (<c>Core.Associates.Create</c>, con clave; feature 016, US3): la plantilla
/// <see cref="PlantillaDeAsociados"/> sobre el ejecutor común. (nuevo)
/// </summary>
public sealed record ImportAssociatesCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null,
    SiYaExiste Existentes = SiYaExiste.Actualizar)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportAssociatesCommandValidator : AbstractValidator<ImportAssociatesCommand>
{
    public ImportAssociatesCommandValidator() => RuleFor(x => x.File).NotNull();
}

/// <summary>
/// Las reglas del alta unitaria, fila por fila, con personas, afiliaciones y catálogos cargados en bloque:
/// <list type="bullet">
/// <item>la persona se busca viva por el usuario derivado; sin ella, <c>Import.Cell.NotFound</c> («esta plantilla no crea personas»);</item>
/// <item>sin afiliación se crea por <see cref="AssociateRegistrar.Construir"/>, que escribe la fila y la bandera <c>IsAssociate</c>; un
/// retirado nace retirado con su fecha y su motivo, sin trámite en Cartera (D8);</item>
/// <item>con afiliación se actualiza sólo lo que trae valor y queda el cambio campo a campo (exige <c>Core.Associates.Update</c>). Un vigente
/// que el archivo marca retirado es <c>Associate.WithdrawalOwnedByLending</c>: lo retira Cartera. El archivo tampoco reactiva a un retirado.
/// Retiro y reingreso sólo se guardan al crear: si difieren queda un aviso.</item>
/// <item>con <see cref="SiYaExiste.Conservar"/> sólo agrega: la afiliación viva queda sin cambio, sin validar su fila y sin exigir
/// <c>Core.Associates.Update</c>.</item>
/// </list>
/// El estado y las fechas tienen que decir lo mismo: «retirado» se decide por <c>PersonaDeOriginacion.Retirado</c>, que también mira las
/// fechas, y un vigente con un retiro sin reingreso quedaría contado como retirado en silencio.
/// </summary>
public sealed class ImportAssociatesCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, AssociateRegistrar asociados,
    IDateTimeService reloj, ICurrentUserService usuario)
    : IRequestHandler<ImportAssociatesCommand, Result<ImportResultDto>>
{
    public const string PermisoDeActualizar = "Core.Associates.Update";
    public const string CodigoInvalido = "Associate.Invalid";
    public const string CodigoEstadoYFechas = "Associate.StatusDatesMismatch";
    public const string CodigoRetiroAntesDeAfiliacion = "Associate.WithdrawalBeforeJoin";
    public const string CodigoSoloAlCrear = "Associate.OnlyOnCreate";
    public const string CodigoAfiliacionEliminada = "Associate.Deleted";

    private static readonly IReadOnlyDictionary<string, string> ColumnaDe = new Dictionary<string, string>
    {
        [nameof(AssociateInput.JoinDate)] = P.FechaAfiliacion,
        [nameof(AssociateInput.ContributionRate)] = P.TasaAporte,
        [nameof(AssociateInput.ContributionPledged)] = P.AporteComprometido,
    };

    private sealed record GrupoCitado(int Id, bool Activo);

    /// <summary>Los catálogos que cita el archivo, cargados una vez.</summary>
    private sealed record Catalogos(CatalogoCitado<int> Oficinas, CatalogoCitado<GrupoCitado> Grupos, CatalogoCitado<int> Comites, CatalogoCitado<int> Motivos);

    /// <summary>Lo que trae una fila, ya convertido. Nulo = la fila no lo trae.</summary>
    private sealed record DatosDeFila(
        DateOnly? FechaAfiliacion, int? OficinaId, string? Estado, GrupoCitado? Grupo, string? Clase, DateOnly? FechaRetiro, int? MotivoId,
        DateOnly? FechaReingreso, string? Periodicidad, string? Forma, decimal? Tasa, decimal? Comprometido, int? ComiteId, string? Categoria);

    public Task<Result<ImportResultDto>> Handle(ImportAssociatesCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(P.Definicion, request, (ctx, c) => ProcesarAsync(ctx, request.Existentes == SiYaExiste.Conservar, c), ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, bool conservarExistentes, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;

        var documentos = new Dictionary<FilaDeImportacion, DocumentoValidado>();
        foreach (var fila in filas)
            if (DocumentoDePlantilla.Leer(fila, P.TipoDocumento, P.Documento) is { } validado)
                documentos[fila] = validado;

        var usuarios = documentos.Values.Select(d => d.Usuario).Distinct().ToList();
        var personas = usuarios.Count == 0
            ? []
            : await db.People.Where(p => usuarios.Contains(p.Username) && !p.IsDeleted).ToListAsync(ct);
        var porUsuario = personas.GroupBy(p => p.Username).ToDictionary(g => g.Key, g => g.First());
        var ids = personas.Select(p => p.Id).ToList();
        // Eliminadas incluidas: el índice por persona no es filtrado, así que una afiliación de baja también ocupa el lugar.
        var afiliaciones = ids.Count == 0
            ? []
            : await db.Associates.IgnoreQueryFilters().Where(a => ids.Contains(a.PersonId)).ToListAsync(ct);
        var porPersona = afiliaciones.GroupBy(a => a.PersonId).ToDictionary(g => g.Key, g => g.OrderBy(a => a.IsDeleted).First());

        var catalogos = await LeerCatalogosAsync(ct);
        var validador = new AssociateInputValidator();
        var puedeActualizar = ctx.TienePermiso(PermisoDeActualizar);

        foreach (var fila in filas)
        {
            // La fila que no se va a usar no se valida: sólo se comprueba que no se repita en el archivo.
            if (conservarExistentes && documentos.TryGetValue(fila, out var citado)
                && porUsuario.TryGetValue(citado.Usuario, out var citada)
                && porPersona.TryGetValue(citada.Id, out var viva) && !viva.IsDeleted)
            {
                if (ctx.Datos.LlaveUnica(fila, citado.Usuario, P.Documento))
                    ctx.Registrar(fila, citado.Usuario, AccionDeImportacion.Unchanged);
                continue;
            }

            var datos = Leer(fila, catalogos);
            if (!documentos.TryGetValue(fila, out var documento)) continue;
            if (!ctx.Datos.LlaveUnica(fila, documento.Usuario, P.Documento)) continue;

            if (!porUsuario.TryGetValue(documento.Usuario, out var persona))
            {
                fila.Error(P.Documento, ImportErrors.CellNotFound,
                    $"No hay una persona viva con documento «{documento.Numero}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                continue;
            }
            if (fila.TieneErrores || datos is null) continue;

            var entrada = Entrada(datos);
            if (!Valida(fila, validador, entrada)) continue;

            porPersona.TryGetValue(persona.Id, out var afiliacion);
            if (afiliacion is null)
                Crear(ctx, fila, documento, persona, datos, entrada);
            else if (afiliacion.IsDeleted)
                fila.Error(P.Documento, CodigoAfiliacionEliminada, $"La afiliación de {documento.Usuario} fue eliminada y esta plantilla no la restaura.");
            else if (!puedeActualizar)
                fila.Error(P.Documento, ImportErrors.CellPermissionRequired,
                    $"El asociado {documento.Usuario} ya existe y actualizarlo exige el permiso {PermisoDeActualizar}.");
            else
                Actualizar(ctx, fila, documento, datos, afiliacion);
        }
    }

    private async Task<Catalogos> LeerCatalogosAsync(CancellationToken ct) => new(
        CatalogoCitado<int>.Desde(
            await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).Select(b => new { b.Id, b.LegacyCode, b.Name, b.ShortName }).ToListAsync(ct),
            b => b.Id, b => b.LegacyCode, b => b.Name, b => b.ShortName),
        CatalogoCitado<GrupoCitado>.Desde(
            await db.AssociateGroups.AsNoTracking().Where(g => !g.IsDeleted).Select(g => new { g.Id, g.Code, g.Name, g.IsActive }).ToListAsync(ct),
            g => new GrupoCitado(g.Id, g.IsActive), g => g.Code, g => g.Name),
        CatalogoCitado<int>.Desde(
            await db.Committees.AsNoTracking().Where(c => !c.IsDeleted).Select(c => new { c.Id, c.LegacyCode, c.Name, c.ShortName }).ToListAsync(ct),
            c => c.Id, c => c.LegacyCode, c => c.Name, c => c.ShortName),
        CatalogoCitado<int>.Desde(
            await db.WithdrawalReasons.AsNoTracking().Where(w => !w.IsDeleted).Select(w => new { w.Id, w.LegacyCode, w.Name, w.ShortName }).ToListAsync(ct),
            w => w.Id, w => w.LegacyCode, w => w.Name, w => w.ShortName));

    // ------------------------------------------------------------------ lectura --

    private DatosDeFila? Leer(FilaDeImportacion fila, Catalogos c)
    {
        var estado = Cerrado(fila, P.Estado, P.Estados);
        var afiliacion = fila.Fecha(P.FechaAfiliacion);
        var retiro = fila.Fecha(P.FechaRetiro);
        var reingreso = fila.Fecha(P.FechaReingreso);
        var motivo = Id(fila, P.MotivoRetiro, c.Motivos, "un motivo de retiro", "en Maestros › Motivos de retiro");
        var hoy = reloj.HoyLocal;

        if (retiro > hoy) fila.Error(P.FechaRetiro, CodigoEstadoYFechas, "La fecha de retiro no puede ser futura.");
        if (reingreso > hoy) fila.Error(P.FechaReingreso, CodigoEstadoYFechas, "La fecha de reingreso no puede ser futura.");
        var reingresoCubreElRetiro = reingreso is { } r && retiro is { } w && r >= w;
        if (AssociateRegistrar.EsRetirado(estado))
        {
            fila.Requerido(P.FechaRetiro);
            fila.Requerido(P.MotivoRetiro);
            if (reingresoCubreElRetiro)
                fila.Error(P.FechaReingreso, CodigoEstadoYFechas, "Un retirado no puede tener un reingreso igual o posterior a su retiro: revise el estado o las fechas.");
        }
        else if (retiro is not null && !reingresoCubreElRetiro)
        {
            fila.Error(P.FechaRetiro, CodigoEstadoYFechas, "Un vigente con fecha de retiro lleva una fecha de reingreso igual o posterior: revise el estado o las fechas.");
        }
        if (retiro < afiliacion)
            fila.Aviso(P.FechaRetiro, CodigoRetiroAntesDeAfiliacion, $"La fecha de retiro {retiro:yyyy-MM-dd} es anterior a la de afiliación {afiliacion:yyyy-MM-dd}.");

        var datos = new DatosDeFila(
            afiliacion, Id(fila, P.Oficina, c.Oficinas, "una agencia", "en Maestros › Agencias"), estado,
            fila.Referencia(P.Grupo, c.Grupos, "un grupo de asociados", "en Maestros › Grupos del asociado"),
            Cerrado(fila, P.Clase, P.Clases), retiro, motivo, reingreso,
            Cerrado(fila, P.PeriodicidadDescuento, P.Periodicidades), Cerrado(fila, P.FormaDescuento, P.Formas),
            fila.Cantidad(P.TasaAporte), fila.Monto(P.AporteComprometido),
            Id(fila, P.Comite, c.Comites, "un comité", "en Maestros › Comités"), fila.Texto(P.Categoria));
        return fila.TieneErrores ? null : datos;
    }

    private static int? Id(FilaDeImportacion fila, string columna, CatalogoCitado<int> catalogo, string que, string dondeSeCrea)
    {
        var id = fila.Referencia(columna, catalogo, que, dondeSeCrea);
        return id == 0 ? null : id;
    }

    /// <summary>Un valor de una lista cerrada; el mensaje dice lo que admite, sin repetir sus abreviaturas.</summary>
    private static string? Cerrado(FilaDeImportacion fila, string columna, IReadOnlyDictionary<string, string> valores)
    {
        var crudo = fila.Texto(columna);
        if (crudo is null) return null;
        if (valores.TryGetValue(TablaLeida.Normalizar(crudo), out var codigo)) return codigo;
        fila.Error(columna, ImportErrors.CellFormat,
            $"«{crudo}» no es válido en «{columna}». Admite: {string.Join(", ", valores.Keys.Where(k => k.Length > 1))}.");
        return null;
    }

    private static AssociateInput Entrada(DatosDeFila d) => new()
    {
        JoinDate = d.FechaAfiliacion,
        ContributionRate = d.Tasa ?? 0m,
        ContributionPledged = d.Comprometido ?? 0m,
        CategoryRating = d.Categoria,
        AssociateClass = d.Clase,
        DeductionPeriod = d.Periodicidad,
        DeductionType = d.Forma,
    };

    private static bool Valida(FilaDeImportacion fila, AssociateInputValidator validador, AssociateInput entrada)
    {
        var resultado = validador.Validate(entrada);
        foreach (var falla in resultado.Errors)
            fila.Error(ColumnaDe.GetValueOrDefault(falla.PropertyName, P.Documento), CodigoInvalido, falla.ErrorMessage);
        return resultado.IsValid;
    }

    // ------------------------------------------------------------------ crear --

    private void Crear(ContextoDeImportacion ctx, FilaDeImportacion fila, DocumentoValidado documento, Person persona, DatosDeFila d, AssociateInput entrada)
    {
        if (d.Grupo is { Activo: false })
        {
            fila.Error(P.Grupo, AssociateGroupErrors.Inactive().Code, AssociateGroupErrors.Inactive().Message);
            return;
        }
        var referencias = new ReferenciasDeAfiliacion(GrupoId: d.Grupo?.Id, SucursalId: d.OficinaId, ComiteId: d.ComiteId);
        asociados.Construir(persona, entrada, referencias, new HistoriaDeAfiliacion(d.Estado!, d.FechaRetiro, d.MotivoId, d.FechaReingreso));
        ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Create);
    }

    // ------------------------------------------------------------- actualizar --

    private void Actualizar(ContextoDeImportacion ctx, FilaDeImportacion fila, DocumentoValidado documento, DatosDeFila d, Associate a)
    {
        if (AssociateRegistrar.RetiroQueEsDeCartera(a.Status, d.Estado) is { } retiroDeCartera)
        {
            fila.Error(P.Estado, retiroDeCartera.Code, retiroDeCartera.Message);
            return;
        }
        if (AssociateRegistrar.EsRetirado(a.Status) && !AssociateRegistrar.EsRetirado(d.Estado))
        {
            fila.Error(P.Estado, AssociateRegistrar.RetiroEsDeCarteraCode, "El asociado está retirado y esta plantilla no lo reactiva.");
            return;
        }
        if (d.Grupo is { Activo: false } grupo && grupo.Id != a.AssociateGroupId)
        {
            fila.Error(P.Grupo, AssociateGroupErrors.Inactive().Code, AssociateGroupErrors.Inactive().Message);
            return;
        }

        var cambios = new List<CampoCambiadoDto>();

        void Texto(string columna, string? nuevo, Func<string?> leer, Action<string?> escribir)
        {
            if (nuevo is null || string.Equals(leer(), nuevo, StringComparison.Ordinal)) return;
            cambios.Add(new CampoCambiadoDto(columna, leer(), nuevo));
            escribir(nuevo);
        }

        void Decimal(string columna, decimal? nuevo, Func<decimal> leer, Action<decimal> escribir)
        {
            if (nuevo is not { } valor || leer() == valor) return;
            cambios.Add(new CampoCambiadoDto(columna, leer().ToString(System.Globalization.CultureInfo.InvariantCulture),
                valor.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            escribir(valor);
        }

        void Referencia(string columna, int? nuevo, Func<int?> leer, Action<int?> escribir)
        {
            if (nuevo is null || leer() == nuevo) return;
            cambios.Add(new CampoCambiadoDto(columna, leer()?.ToString(System.Globalization.CultureInfo.InvariantCulture), fila.Crudo(columna)));
            escribir(nuevo);
        }

        if (d.FechaAfiliacion is { } afiliacion && a.JoinDate != afiliacion)
        {
            cambios.Add(new CampoCambiadoDto(P.FechaAfiliacion, a.JoinDate?.ToString("yyyy-MM-dd"), afiliacion.ToString("yyyy-MM-dd")));
            a.JoinDate = afiliacion;
        }
        Referencia(P.Oficina, d.OficinaId, () => a.BranchId, v => a.BranchId = v);
        Texto(P.Estado, d.Estado, () => a.Status, v => a.Status = v);
        Referencia(P.Grupo, d.Grupo?.Id, () => a.AssociateGroupId, v => a.AssociateGroupId = v);
        Texto(P.Clase, d.Clase, () => a.AssociateClass, v => a.AssociateClass = v);
        Texto(P.PeriodicidadDescuento, d.Periodicidad, () => a.DeductionPeriod, v => a.DeductionPeriod = v);
        Texto(P.FormaDescuento, d.Forma, () => a.DeductionType, v => a.DeductionType = v);
        Decimal(P.TasaAporte, d.Tasa, () => a.ContributionRate, v => a.ContributionRate = v);
        Decimal(P.AporteComprometido, d.Comprometido, () => a.ContributionPledged, v => a.ContributionPledged = v);
        Referencia(P.Comite, d.ComiteId, () => a.CommitteeId, v => a.CommitteeId = v);
        Texto(P.Categoria, d.Categoria, () => a.CategoryRating, v => a.CategoryRating = v);

        if (d.FechaRetiro is { } retiro && retiro != a.WithdrawalDate) SoloAlCrear(fila, P.FechaRetiro);
        if (d.MotivoId is { } motivo && motivo != a.WithdrawalReasonId) SoloAlCrear(fila, P.MotivoRetiro);
        if (d.FechaReingreso is { } reingreso && reingreso != a.RejoinDate) SoloAlCrear(fila, P.FechaReingreso);

        if (cambios.Count == 0)
        {
            ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Unchanged);
            return;
        }
        a.UpdatedAt = reloj.UtcNow;
        a.UpdatedBy = usuario.UserName;
        ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Update, cambios);
    }

    private static void SoloAlCrear(FilaDeImportacion fila, string columna) =>
        fila.Aviso(columna, CodigoSoloAlCrear, $"«{columna}» sólo se guarda al crear el asociado: el valor del archivo no se aplicó.");
}
