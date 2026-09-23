using System.Text.Json.Serialization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Attachments.Local;

// Feature 011, research R16. Con AttachmentStorage:Provider = Local, las autorizaciones que firma el
// almacén apuntan a /api/attachments/local-blob/{token} en lugar de a S3. Estas dos piezas son lo que
// hay detrás de esas rutas: las rutas sólo reenvían (Principio III) y quien decide es IAlmacenLocal,
// que rechaza lo mismo que rechaza la política de S3. Las rutas no existen en Production ni con S3.

/// <summary>
/// Recibe una subida como la recibiría S3: los campos del formulario y el archivo. El archivo y el
/// token no van a la auditoría —el primero es contenido de la cooperativa, el segundo una autorización.
/// </summary>
public sealed record RecibirSubidaLocalCommand(
    [property: JsonIgnore] string Token,
    IReadOnlyDictionary<string, string> Campos,
    [property: JsonIgnore] Stream Archivo) : IRequest<Result>;

public sealed class RecibirSubidaLocalCommandValidator : AbstractValidator<RecibirSubidaLocalCommand>
{
    public RecibirSubidaLocalCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Campos).NotNull();
        RuleFor(x => x.Archivo).NotNull();
    }
}

public sealed class RecibirSubidaLocalCommandHandler(IAlmacenLocal almacen) : IRequestHandler<RecibirSubidaLocalCommand, Result>
{
    public Task<Result> Handle(RecibirSubidaLocalCommand request, CancellationToken ct) =>
        almacen.RecibirAsync(request.Token, request.Campos, request.Archivo, ct);
}

/// <summary>Entrega un archivo con el nombre y el tipo que firmó el enlace de descarga.</summary>
public sealed record LeerBlobLocalQuery(string Token) : IRequest<Result<ArchivoLocal>>;

public sealed class LeerBlobLocalQueryValidator : AbstractValidator<LeerBlobLocalQuery>
{
    public LeerBlobLocalQueryValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(4000);
}

public sealed class LeerBlobLocalQueryHandler(IAlmacenLocal almacen) : IRequestHandler<LeerBlobLocalQuery, Result<ArchivoLocal>>
{
    public Task<Result<ArchivoLocal>> Handle(LeerBlobLocalQuery request, CancellationToken ct) =>
        almacen.LeerAsync(request.Token, ct);
}
