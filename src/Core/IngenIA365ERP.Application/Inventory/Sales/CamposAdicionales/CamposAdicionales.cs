using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales.CamposAdicionales;

// Los campos adicionales de venta (2026-11-15, pedido del dueño: en COOFLOPAL la placa del vehículo): la cooperativa los define con su nombre,
// tipo y tamaño; se digitan en el borrador de la venta o la cotización; el documento sella su copia; los obligatorios se exigen al confirmar y
// los que se imprimen van en las notas del documento electrónico (la DIAN las recibe y la representación gráfica las imprime). (nuevo)

public sealed record DocumentFieldDto(Guid PublicId, string Code, string Name, DocumentFieldType DataType, int MaxLength, bool IsRequired, bool ShowInSales,
    bool ShowInQuotes, bool PrintOnInvoice, int SortOrder, bool IsActive);

public sealed record DocumentFieldValueDto(Guid DefinitionPublicId, string Code, string Name, string Value);

public sealed record DocumentFieldValueInput(string Code, string? Value);

/// <summary>Las reglas en un solo sitio: a qué documentos aplica un campo, su valor admitido, el faltante al confirmar y las notas. (nuevo)</summary>
public static class CamposAdicionalesDeVenta
{
    private static readonly HashSet<DocumentClass> Ventas =
    [
        DocumentClass.SalesInvoice, DocumentClass.SalesInvoiceFromShipments, DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt,
    ];

    /// <summary>¿El campo se pide en un documento de esta clase?</summary>
    public static bool Aplica(DocumentFieldDefinition campo, DocumentClass clase) =>
        (campo.ShowInSales && Ventas.Contains(clase)) || (campo.ShowInQuotes && clase == DocumentClass.SalesQuote);

    /// <summary>¿La clase admite campos adicionales?</summary>
    public static bool AdmiteCampos(DocumentClass clase) => Ventas.Contains(clase) || clase == DocumentClass.SalesQuote;

    /// <summary>El valor limpio que admite el campo, o el error que lo dice.</summary>
    public static Result<string> Interpretar(DocumentFieldDefinition campo, string valor)
    {
        var texto = valor.Trim();
        if (texto.Length > campo.MaxLength) return Result.Failure<string>(ErroresDeCamposAdicionales.TooLong(campo.Name, campo.MaxLength));
        return campo.DataType switch
        {
            DocumentFieldType.Number when !decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out _) =>
                Result.Failure<string>(ErroresDeCamposAdicionales.NotANumber(campo.Name)),
            DocumentFieldType.Date when !DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) =>
                Result.Failure<string>(ErroresDeCamposAdicionales.NotADate(campo.Name)),
            DocumentFieldType.Text => Result.Success(texto.ToUpperInvariant()),
            _ => Result.Success(texto),
        };
    }

    /// <summary>El primer campo obligatorio activo que le falta al documento al confirmarlo; nulo si no falta ninguno.</summary>
    public static async Task<Error?> FaltanteAsync(IApplicationDbContext db, InventoryDocument documento, CancellationToken ct)
    {
        if (!AdmiteCampos(documento.Class)) return null;
        var obligatorios = (await db.DocumentFieldDefinitions.AsNoTracking().Where(c => c.IsActive && c.IsRequired).ToListAsync(ct))
            .Where(c => Aplica(c, documento.Class)).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
        if (obligatorios.Count == 0) return null;
        var conValor = await db.DocumentFieldValues.AsNoTracking().Where(v => v.DocumentId == documento.Id && v.Value != "")
            .Select(v => v.DefinitionId).ToListAsync(ct);
        var falta = obligatorios.FirstOrDefault(c => !conValor.Contains(c.Id));
        return falta is null ? null : ErroresDeCamposAdicionales.Required(falta.Name);
    }

    /// <summary>Lo que va a la factura: «Placa: ABC123» por cada campo sellado que se imprime, en su orden.</summary>
    public static async Task<IReadOnlyList<string>> NotasAsync(IApplicationDbContext db, int documentoId, CancellationToken ct) =>
        (await db.DocumentFieldValues.AsNoTracking().Where(v => v.DocumentId == documentoId && v.PrintOnInvoice && v.Value != "")
            .OrderBy(v => v.SortOrder).ThenBy(v => v.FieldName).Select(v => new { v.FieldName, v.Value }).ToListAsync(ct))
        .Select(v => $"{v.FieldName}: {v.Value}").ToList();

    /// <summary>Copia los valores de un documento a otro (la cotización a la venta que la carga) para los campos que el destino pide.</summary>
    public static async Task CopiarAsync(IApplicationDbContext db, int desde, InventoryDocument hacia, CancellationToken ct)
    {
        var origen = await db.DocumentFieldValues.AsNoTracking().Include(v => v.Definition).Where(v => v.DocumentId == desde).ToListAsync(ct);
        var destino = await db.DocumentFieldValues.Where(v => v.DocumentId == hacia.Id).ToListAsync(ct);
        foreach (var valor in origen.Where(v => v.Definition is { IsActive: true } d && Aplica(d, hacia.Class)))
        {
            if (destino.Any(v => v.DefinitionId == valor.DefinitionId && v.Value != "")) continue;
            Sellar(db, hacia.Id, valor.Definition!, valor.Value, destino);
        }
    }

    /// <summary>Escribe (o reemplaza) el valor sellado del campo en el documento. No guarda.</summary>
    internal static void Sellar(IApplicationDbContext db, int documentoId, DocumentFieldDefinition campo, string valor, List<DocumentFieldValue> actuales)
    {
        var fila = actuales.FirstOrDefault(v => v.DefinitionId == campo.Id);
        if (fila is null)
        {
            fila = new DocumentFieldValue { DocumentId = documentoId, DefinitionId = campo.Id };
            db.DocumentFieldValues.Add(fila);
            actuales.Add(fila);
        }
        fila.FieldCode = campo.Code;
        fila.FieldName = campo.Name;
        fila.PrintOnInvoice = campo.PrintOnInvoice;
        fila.SortOrder = campo.SortOrder;
        fila.Value = valor;
    }
}

/// <summary>Los errores de los campos adicionales. (nuevo)</summary>
public static class ErroresDeCamposAdicionales
{
    public static Error NotFound(string code) => new ErrorConDatos("Inventory.DocumentField.NotFound", $"No hay un campo adicional activo «{code}».", new { code });

    public static Error NotForDocument(string name) => new ErrorConDatos("Inventory.DocumentField.NotForDocument",
        $"El campo «{name}» no se pide en este documento.", new { name });

    public static Error TooLong(string name, int maxLength) => new ErrorConDatos("Inventory.DocumentField.TooLong",
        $"«{name}» admite hasta {maxLength} caracteres.", new { name, maxLength });

    public static Error NotANumber(string name) => new ErrorConDatos("Inventory.DocumentField.NotANumber", $"«{name}» es un número.", new { name });

    public static Error NotADate(string name) => new ErrorConDatos("Inventory.DocumentField.NotADate", $"«{name}» es una fecha (aaaa-mm-dd).", new { name });

    public static Error Required(string name) => new ErrorConDatos("Inventory.DocumentField.Required",
        $"Falta «{name}»: es obligatorio para confirmar.", new { name });

    public static Error DocumentNotDraft() => new("Inventory.DocumentField.DocumentNotDraft",
        "Los campos adicionales se cambian mientras el documento está en borrador.");
}

// ===================================================================================================== definiciones --

/// <summary>
/// Crea (sin <see cref="PublicId"/>) o cambia un campo adicional (<c>POST/PUT /api/inventory/document-fields</c>,
/// <c>Inventory.Parameters.Manage</c>, con <c>Idempotency-Key</c>). El código no cambia al editar. (nuevo)
/// </summary>
public sealed record SaveDocumentFieldCommand(
    Guid? PublicId, string Code, string Name, DocumentFieldType DataType, int MaxLength, bool IsRequired, bool ShowInSales, bool ShowInQuotes,
    bool PrintOnInvoice, int SortOrder, bool IsActive)
    : IRequest<Result<DocumentFieldDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveDocumentFieldCommandValidator : AbstractValidator<SaveDocumentFieldCommand>
{
    public SaveDocumentFieldCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(DocumentFieldDefinition.LargoDelNombre);
        RuleFor(x => x.DataType).IsInEnum();
        RuleFor(x => x.MaxLength).InclusiveBetween(1, DocumentFieldDefinition.TamanoMaximo);
        RuleFor(x => x).Must(x => x.ShowInSales || x.ShowInQuotes).WithMessage("Elija en qué documentos se pide el campo: ventas, cotizaciones o los dos.");
    }
}

public sealed class SaveDocumentFieldCommandHandler(IApplicationDbContext db) : IRequestHandler<SaveDocumentFieldCommand, Result<DocumentFieldDto>>
{
    public async Task<Result<DocumentFieldDto>> Handle(SaveDocumentFieldCommand request, CancellationToken ct)
    {
        DocumentFieldDefinition campo;
        if (request.PublicId is { } id)
        {
            var existente = await db.DocumentFieldDefinitions.FirstOrDefaultAsync(c => c.PublicId == id, ct);
            if (existente is null) return Result.Failure<DocumentFieldDto>(Error.NotFound);
            campo = existente;
        }
        else
        {
            var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
            var otro = await db.DocumentFieldDefinitions.Where(c => c.Code == codigo).Select(c => new { c.Name, c.PublicId }).FirstOrDefaultAsync(ct);
            if (otro is not null) return Result.Failure<DocumentFieldDto>(CodigoDeCatalogo.Duplicado("un campo adicional", codigo, otro.Name, otro.PublicId));
            campo = new DocumentFieldDefinition { Code = codigo };
            db.DocumentFieldDefinitions.Add(campo);
        }

        campo.Name = request.Name.Trim();
        campo.DataType = request.DataType;
        campo.MaxLength = request.MaxLength;
        campo.IsRequired = request.IsRequired;
        campo.ShowInSales = request.ShowInSales;
        campo.ShowInQuotes = request.ShowInQuotes;
        campo.PrintOnInvoice = request.PrintOnInvoice;
        campo.SortOrder = request.SortOrder;
        campo.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(Vista(campo));
    }

    internal static DocumentFieldDto Vista(DocumentFieldDefinition c) =>
        new(c.PublicId, c.Code, c.Name, c.DataType, c.MaxLength, c.IsRequired, c.ShowInSales, c.ShowInQuotes, c.PrintOnInvoice, c.SortOrder, c.IsActive);
}

/// <summary>Los campos adicionales (<c>GET /api/inventory/document-fields?activeOnly=</c>), en su orden. (nuevo)</summary>
public sealed record ListDocumentFieldsQuery(bool ActiveOnly = false) : IRequest<Result<IReadOnlyList<DocumentFieldDto>>>;

public sealed class ListDocumentFieldsQueryValidator : AbstractValidator<ListDocumentFieldsQuery>;

public sealed class ListDocumentFieldsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDocumentFieldsQuery, Result<IReadOnlyList<DocumentFieldDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentFieldDto>>> Handle(ListDocumentFieldsQuery request, CancellationToken ct)
    {
        var campos = db.DocumentFieldDefinitions.AsNoTracking();
        if (request.ActiveOnly) campos = campos.Where(c => c.IsActive);
        var lista = await campos.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync(ct);
        return Result.Success<IReadOnlyList<DocumentFieldDto>>(lista.Select(SaveDocumentFieldCommandHandler.Vista).ToList());
    }
}

// ========================================================================================================== valores --

/// <summary>
/// Los valores de los campos adicionales de un documento de venta o cotización en borrador (<c>PUT /api/inventory/documents/{id}/fields</c>,
/// <c>Inventory.Sales.Create</c>, con <c>Idempotency-Key</c>). Cada campo se valida (tipo y tamaño) y se sella; uno vacío borra el valor. Los
/// obligatorios se exigen al confirmar. (nuevo)
/// </summary>
public sealed record SetDocumentFieldValuesCommand(Guid DocumentPublicId, IReadOnlyList<DocumentFieldValueInput> Values)
    : IRequest<Result<IReadOnlyList<DocumentFieldValueDto>>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SetDocumentFieldValuesCommandValidator : AbstractValidator<SetDocumentFieldValuesCommand>
{
    public SetDocumentFieldValuesCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.Values).NotNull();
        RuleForEach(x => x.Values).ChildRules(v => v.RuleFor(y => y.Code).NotEmpty());
    }
}

public sealed class SetDocumentFieldValuesCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SetDocumentFieldValuesCommand, Result<IReadOnlyList<DocumentFieldValueDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentFieldValueDto>>> Handle(SetDocumentFieldValuesCommand request, CancellationToken ct)
    {
        var documento = await db.InventoryDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == request.DocumentPublicId, ct);
        if (documento is null) return Result.Failure<IReadOnlyList<DocumentFieldValueDto>>(InventoryErrors.DocumentNotFound());
        if (documento.Status != DocumentStatus.Draft) return Result.Failure<IReadOnlyList<DocumentFieldValueDto>>(ErroresDeCamposAdicionales.DocumentNotDraft());

        var campos = await db.DocumentFieldDefinitions.Where(c => c.IsActive).ToListAsync(ct);
        var actuales = await db.DocumentFieldValues.Where(v => v.DocumentId == documento.Id).ToListAsync(ct);
        foreach (var pedido in request.Values)
        {
            var codigo = CodigoDeCatalogo.Normalizar(pedido.Code);
            var campo = campos.FirstOrDefault(c => c.Code == codigo);
            if (campo is null) return Result.Failure<IReadOnlyList<DocumentFieldValueDto>>(ErroresDeCamposAdicionales.NotFound(pedido.Code));
            if (!CamposAdicionalesDeVenta.Aplica(campo, documento.Class))
                return Result.Failure<IReadOnlyList<DocumentFieldValueDto>>(ErroresDeCamposAdicionales.NotForDocument(campo.Name));
            if (string.IsNullOrWhiteSpace(pedido.Value))
            {
                if (actuales.FirstOrDefault(v => v.DefinitionId == campo.Id) is { } fila)
                {
                    db.DocumentFieldValues.Remove(fila);
                    actuales.Remove(fila);
                }
                continue;
            }
            var valor = CamposAdicionalesDeVenta.Interpretar(campo, pedido.Value);
            if (valor.IsFailure) return Result.Failure<IReadOnlyList<DocumentFieldValueDto>>(valor.Error);
            CamposAdicionalesDeVenta.Sellar(db, documento.Id, campo, valor.Value, actuales);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await GetDocumentFieldValuesQueryHandler.ValoresAsync(db, documento.Id, ct));
    }
}

/// <summary>Los valores sellados de un documento (<c>GET /api/inventory/documents/{id}/fields</c>). (nuevo)</summary>
public sealed record GetDocumentFieldValuesQuery(Guid DocumentPublicId) : IRequest<Result<IReadOnlyList<DocumentFieldValueDto>>>;

public sealed class GetDocumentFieldValuesQueryValidator : AbstractValidator<GetDocumentFieldValuesQuery>
{
    public GetDocumentFieldValuesQueryValidator() => RuleFor(x => x.DocumentPublicId).NotEmpty();
}

public sealed class GetDocumentFieldValuesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDocumentFieldValuesQuery, Result<IReadOnlyList<DocumentFieldValueDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentFieldValueDto>>> Handle(GetDocumentFieldValuesQuery request, CancellationToken ct)
    {
        var id = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == request.DocumentPublicId).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        return id is int documento
            ? Result.Success(await ValoresAsync(db, documento, ct))
            : Result.Failure<IReadOnlyList<DocumentFieldValueDto>>(InventoryErrors.DocumentNotFound());
    }

    internal static async Task<IReadOnlyList<DocumentFieldValueDto>> ValoresAsync(IApplicationDbContext db, int documento, CancellationToken ct) =>
        await db.DocumentFieldValues.AsNoTracking().Where(v => v.DocumentId == documento)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.FieldName)
            .Select(v => new DocumentFieldValueDto(v.Definition!.PublicId, v.FieldCode, v.FieldName, v.Value)).ToListAsync(ct);
}
