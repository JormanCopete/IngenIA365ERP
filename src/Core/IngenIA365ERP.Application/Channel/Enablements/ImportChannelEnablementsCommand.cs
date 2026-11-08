using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Enablements;

/// <summary>
/// Feature 021 (T045; FR-010, FR-011): la plantilla de carga masiva de habilitaciones del canal. Mismo mecanismo que las demás
/// plantillas (revisión previa, todo o nada, errores por fila y columna); identifica a la persona por tipo y número de documento.
/// </summary>
public static class PlantillaDeHabilitaciones
{
    public const string Clave = "channel.enablements";

    public const string TipoDocumento = "tipoDocumento";
    public const string Documento = "documento";
    public const string Accion = "accion";
    public const string Motivo = "motivo";

    public const string Habilitar = "habilitar";
    public const string Inhabilitar = "inhabilitar";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Habilitaciones del canal digital", ModuloDeAuditoria.Channel,
    [
        new HojaDePlantilla("Datos",
        [
            new(TipoDocumento, TipoDeValor.Codigo, Obligatoria: true, Largo: 3, Reglas: "CC, CE, PPT, PEP, TI o PA", Ejemplo: "CC"),
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "la persona debe existir en la cooperativa", Ejemplo: "1010101010"),
            new(Accion, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "habilitar o inhabilitar", Ejemplo: Habilitar),
            new(Motivo, TipoDeValor.Texto, Largo: 300, Reglas: "obligatorio al inhabilitar"),
        ]),
    ]);
}

public sealed record ImportChannelEnablementsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string? Reason = null)
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportChannelEnablementsCommandValidator : AbstractValidator<ImportChannelEnablementsCommand>
{
    public ImportChannelEnablementsCommandValidator() => RuleFor(x => x.File).NotNull();
}

public sealed class ImportChannelEnablementsCommandHandler(IApplicationDbContext db, EjecutorDeImportacion ejecutor, IDateTimeService reloj)
    : IRequestHandler<ImportChannelEnablementsCommand, Result<ImportResultDto>>
{
    public const string AccionInvalida = "Channel.Enablement.ImportActionInvalid";
    public const string SinMotivo = "Channel.Enablement.ImportReasonMissing";

    public Task<Result<ImportResultDto>> Handle(ImportChannelEnablementsCommand request, CancellationToken ct) =>
        ejecutor.EjecutarAsync(PlantillaDeHabilitaciones.Definicion, request, ProcesarAsync, ct);

    private async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken ct)
    {
        var filas = ctx.Datos.Filas;
        var documentos = new Dictionary<FilaDeImportacion, DocumentoValidado>();
        foreach (var fila in filas)
        {
            if (DocumentoDePlantilla.Leer(fila, PlantillaDeHabilitaciones.TipoDocumento, PlantillaDeHabilitaciones.Documento) is { } d) documentos[fila] = d;
        }

        var usuarios = documentos.Values.Select(d => d.Usuario).Distinct().ToList();
        var personas = usuarios.Count == 0
            ? []
            : await db.People.AsNoTracking().Where(p => usuarios.Contains(p.Username)).Select(p => new { p.Id, p.Username }).ToListAsync(ct);
        var porUsuario = personas.GroupBy(p => p.Username).ToDictionary(g => g.Key, g => g.First().Id);
        var ids = porUsuario.Values.ToList();
        var habilitaciones = ids.Count == 0
            ? []
            : await db.ChannelEnablements.Where(h => ids.Contains(h.PersonId)).ToListAsync(ct);
        var porPersona = habilitaciones.ToDictionary(h => h.PersonId);
        var activos = ids.Count == 0
            ? []
            : (await db.Associates.AsNoTracking().Where(a => ids.Contains(a.PersonId) && a.Status == HabilitacionesDelCanal.EstadoActivo)
                .Select(a => a.PersonId).ToListAsync(ct)).ToHashSet();

        var ahora = reloj.UtcNow;
        var configuracion = await ConfiguracionDelCanal.VigenteAsync(db, ahora, ct);
        var noAsociados = configuracion is { AllowNonAssociates: true };

        foreach (var fila in filas)
        {
            if (!documentos.TryGetValue(fila, out var documento)) continue;
            if (!ctx.Datos.LlaveUnica(fila, documento.Usuario, PlantillaDeHabilitaciones.Documento)) continue;
            if (!porUsuario.TryGetValue(documento.Usuario, out var persona))
            {
                fila.Error(PlantillaDeHabilitaciones.Documento, HabilitacionesDelCanal.PersonaInexistente, "La persona no existe en la cooperativa.");
                continue;
            }

            var accion = fila.Texto(PlantillaDeHabilitaciones.Accion)?.Trim().ToLowerInvariant();
            if (accion is not (PlantillaDeHabilitaciones.Habilitar or PlantillaDeHabilitaciones.Inhabilitar))
            {
                fila.Error(PlantillaDeHabilitaciones.Accion, AccionInvalida, "La acción es «habilitar» o «inhabilitar».");
                continue;
            }

            porPersona.TryGetValue(persona, out var h);
            if (accion == PlantillaDeHabilitaciones.Habilitar)
            {
                if (!noAsociados && !activos.Contains(persona))
                {
                    fila.Error(PlantillaDeHabilitaciones.Documento, HabilitacionesDelCanal.NoEsAsociado,
                        "La persona no es asociada activa y la cooperativa no habilita a no asociados en el canal.");
                    continue;
                }
                if (h is { Status: EnablementStatus.Enabled })
                {
                    ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Unchanged);
                    continue;
                }
                var nueva = h is null;
                h ??= new ChannelEnablement { PersonId = persona };
                HabilitacionesDelCanal.Habilitar(h, ahora, EnablementSource.Bulk);
                if (nueva)
                {
                    db.ChannelEnablements.Add(h);
                    porPersona[persona] = h;
                }
                ctx.Registrar(fila, documento.Usuario, nueva ? AccionDeImportacion.Create : AccionDeImportacion.Update);
                continue;
            }

            var motivo = fila.Texto(PlantillaDeHabilitaciones.Motivo, 300);
            if (string.IsNullOrWhiteSpace(motivo))
            {
                fila.Error(PlantillaDeHabilitaciones.Motivo, SinMotivo, "Al inhabilitar, escribe el motivo.");
                continue;
            }
            if (h is null || h.Status == EnablementStatus.Disabled)
            {
                ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Unchanged);
                continue;
            }
            HabilitacionesDelCanal.Inhabilitar(h, ahora, motivo.Trim());
            ctx.Registrar(fila, documento.Usuario, AccionDeImportacion.Update);
        }
    }
}
