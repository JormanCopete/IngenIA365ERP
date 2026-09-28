using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Lo que se corrige de la contraparte en el caso a, sobre lo que hoy dice el maestro (api.md §24.5 <c>partySnapshotChanges</c>). Nulo = no
/// cambia. Nada de esto es económico: la identificación (tipo, número y DV) se corrige en el maestro y, si cambia, la huella lo manda al caso
/// b. (nuevo)
/// </summary>
public sealed record CambiosDeContraparte(
    string? Name = null,
    string? Address = null,
    string? CityDaneCode = null,
    string? Email = null,
    string? Phone = null,
    IReadOnlyList<string>? Responsibilities = null);

/// <summary>El documento corregido: su versión nueva y su estado (<c>Pending</c>). (nuevo)</summary>
public sealed record CorreccionDelRechazoDto(Guid ElectronicDocumentPublicId, short VersionNumber, ElectronicDocumentStatus Status);

/// <summary>
/// Caso a de un documento rechazado: corregir sin cambio económico (feature 012, I4, T722; FR-066; contracts/dian.md §8.2; api.md §24.5
/// <c>POST /documents/{id}/correct</c>, <c>ElectronicInvoicing.Documents.Correct</c>). Vuelve a armar el canónico con la contraparte tal como
/// está hoy en el maestro (más <see cref="PartySnapshotChanges"/>) y compara la huella económica con <see cref="ReglaDeCorreccionFiscal"/>:
/// <list type="bullet">
/// <item>igual → versión n + 1 <c>CaseA</c> con <c>ChangedFieldsJson</c> (antes y después), versión nueva de la copia fiscal por la fuente si
/// la contraparte cambió (la única excepción de FR-005/SC-013; la auditoría del comando guarda el motivo) y <c>Pending</c> por el mismo canal
/// y con el mismo número, <b>sin</b> mensajes de negocio;</item>
/// <item>distinta → 422 <c>ElectronicInvoicing.Document.EconomicFootprintChanged</c> con <c>data.fields[]</c>: se sigue por el caso b.
/// Quien opera no elige entre a y b: lo decide la huella.</item>
/// </list>
/// No exige el rechazo confirmado (basta la respuesta definitiva). La emisión la hace el procesador (o «Reintentar ahora») después del
/// commit. (nuevo)
/// </summary>
public sealed record CorrectRejectedDocumentCommand(Guid ElectronicDocumentPublicId, string Reason, CambiosDeContraparte? PartySnapshotChanges = null)
    : IRequest<Result<CorreccionDelRechazoDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CorrectRejectedDocumentCommandValidator : ValidadorConMotivo<CorrectRejectedDocumentCommand>
{
    public CorrectRejectedDocumentCommandValidator()
    {
        RuleFor(x => x.ElectronicDocumentPublicId).NotEmpty();
        RuleFor(x => x.PartySnapshotChanges!.Name).MaximumLength(200).When(x => x.PartySnapshotChanges is not null);
        RuleFor(x => x.PartySnapshotChanges!.Address).MaximumLength(200).When(x => x.PartySnapshotChanges is not null);
        RuleFor(x => x.PartySnapshotChanges!.CityDaneCode).Matches("^[0-9]{5}$").When(x => x.PartySnapshotChanges?.CityDaneCode is not null)
            .WithMessage("El municipio va con su código DANE de cinco dígitos.");
        RuleFor(x => x.PartySnapshotChanges!.Email).EmailAddress().MaximumLength(200).When(x => x.PartySnapshotChanges?.Email is not null);
        RuleFor(x => x.PartySnapshotChanges!.Phone).MaximumLength(50).When(x => x.PartySnapshotChanges is not null);
    }
}

public sealed class CorrectRejectedDocumentCommandHandler(
    IApplicationDbContext db,
    CasosDeRechazo casos,
    ConstructorDelCanonico constructor,
    IDateTimeService reloj) : IRequestHandler<CorrectRejectedDocumentCommand, Result<CorreccionDelRechazoDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<CorreccionDelRechazoDto>> Handle(CorrectRejectedDocumentCommand request, CancellationToken ct)
    {
        var preparado = await casos.PrepararAsync(request.ElectronicDocumentPublicId, EventoDelDocumentoElectronico.CorregirCasoA, ct);
        if (preparado.IsFailure) return Falla(preparado.Error);
        var (documento, fuente) = preparado.Value;
        var version = CasosDeRechazo.Vigente(documento)!;

        // El canónico vigente (de la copia fiscal vigente) y el corregido (con la contraparte del maestro y los cambios del cuerpo).
        var entrada = await fuente.LeerAsync(version.SourceDocumentPublicId, ct);
        if (entrada.IsFailure) return Falla(entrada.Error);
        var delMaestro = await fuente.ContraparteDelMaestroAsync(version.SourceDocumentPublicId, ct);
        if (delMaestro.IsFailure) return Falla(delMaestro.Error);

        var fotoVigente = entrada.Value.Contrapartes.MaxBy(f => f.Version);
        var fotoNueva = delMaestro.Value is { } m ? Aplicar(m, request.PartySnapshotChanges) : null;
        var cambiaLaCopia = fotoNueva is not null && (fotoVigente is null || fotoNueva with { Version = fotoVigente.Version } != fotoVigente);
        var corregida = cambiaLaCopia ? entrada.Value with { Contrapartes = [.. entrada.Value.Contrapartes, fotoNueva!] } : entrada.Value;

        var numeracion = ReconstruccionDelCanonico.NumeracionDe(documento.EmissionSetting!, documento.Resolution, documento.Prefix, documento.Consecutive,
            documento.ContingencyType == ContingencyType.Issuer03, documento.CorrectsDocument);
        var antes = await constructor.ConstruirAsync(entrada.Value, numeracion, ct);
        if (antes.IsFailure) return Falla(antes.Error);
        var despues = await constructor.ConstruirAsync(corregida, numeracion, ct);
        if (despues.IsFailure) return Falla(despues.Error);

        var datosAntes = ConstructorDelCanonico.DatosFiscales(antes.Value.Documento);
        var datosDespues = ConstructorDelCanonico.DatosFiscales(despues.Value.Documento);
        var decision = ReglaDeCorreccionFiscal.Decidir(datosAntes, datosDespues);
        if (decision is ReglaDeCorreccionFiscal.CasoB b) return Falla(ErroresDeDocumentosElectronicos.EconomicFootprintChanged(b.Fields));
        var cambiados = ((ReglaDeCorreccionFiscal.CasoA)decision).CamposCambiados;

        if (cambiaLaCopia)
        {
            var registrada = await fuente.RegistrarVersionDeContraparteAsync(version.SourceDocumentPublicId, fotoNueva!, request.Reason, ct);
            if (registrada.IsFailure) return Falla(registrada.Error);
        }

        var ahora = reloj.UtcNow;
        var transicion = documento.AplicarEvento(EventoDelDocumentoElectronico.CorregirCasoA, null, ahora);
        if (!transicion.Procede) return Falla(ErroresDeDocumentosElectronicos.DeLaTransicion(transicion));

        var parte = despues.Value.Documento.Counterparty;
        documento.CounterpartyTaxId = Recortar(parte.TaxId, 20);
        documento.CounterpartyName = Recortar(parte.Name, 200);

        var detalle = cambiados.Select(c => new { field = c, before = Valor(datosAntes.Contraparte, c), after = Valor(datosDespues.Contraparte, c) }).ToList();
        var nueva = await casos.AgregarVersionAsync(documento, version.SourceDocumentPublicId, DocumentVersionReason.CaseA, despues.Value, request.Reason,
            JsonSerializer.Serialize(new { counterpartySnapshotVersion = cambiaLaCopia ? fotoNueva!.Version : (int?)null, fields = detalle }, Json),
            ahora, ct);

        return Result.Success(new CorreccionDelRechazoDto(documento.PublicId, nueva.VersionNumber, documento.Status));
    }

    /// <summary>Los cambios del cuerpo sobre la contraparte del maestro.</summary>
    private static FotoFiscalDeEntrada Aplicar(FotoFiscalDeEntrada foto, CambiosDeContraparte? c) => c is null
        ? foto
        : foto with
        {
            LegalName = Limpio(c.Name) ?? foto.LegalName,
            Address = Limpio(c.Address) ?? foto.Address,
            MunicipalityDaneCode = Limpio(c.CityDaneCode) ?? foto.MunicipalityDaneCode,
            Email = Limpio(c.Email) ?? foto.Email,
            Phone = Limpio(c.Phone) ?? foto.Phone,
            Responsibilities = c.Responsibilities is { Count: > 0 } r
                ? string.Join(';', r.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal))
                : foto.Responsibilities,
        };

    /// <summary>El valor de un campo no económico de la contraparte, con el nombre de <see cref="ReglaDeCorreccionFiscal"/>.</summary>
    private static string? Valor(ContraparteFiscal c, string campo) => campo switch
    {
        "counterparty.name" => c.Nombre,
        "counterparty.address" => c.Direccion,
        "counterparty.cityDaneCode" => c.CiudadDane,
        "counterparty.email" => c.Correo,
        "counterparty.phone" => c.Telefono,
        "counterparty.responsibilities" => string.Join(';', c.Responsabilidades.Order(StringComparer.Ordinal)),
        _ => null,
    };

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string? Recortar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<CorreccionDelRechazoDto> Falla(Error error) => Result.Failure<CorreccionDelRechazoDto>(error);
}
