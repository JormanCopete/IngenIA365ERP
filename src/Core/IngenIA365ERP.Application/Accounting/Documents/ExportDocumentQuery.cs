using FluentValidation;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Documents;

/// <summary>
/// 2026-11-16 (pedido del dueño, revisión con la contadora): el comprobante —cualquiera, del módulo que sea y en el estado que
/// esté— como tabla para Excel, con las mismas columnas de la pantalla y una fila por línea. Lee lo mismo que la pantalla y que
/// el PDF (<see cref="GetDocumentQuery"/>), así que el alcance y los permisos son los de consultar el comprobante. El exportador
/// le pone el filtro de Excel a los encabezados para buscar dentro del archivo.
/// </summary>
public sealed record ExportDocumentQuery(Guid PublicId) : IRequest<Result<ComprobanteExportado>>;

/// <summary>La tabla y el nombre del archivo sin extensión (el exportador la pone según el formato).</summary>
public sealed record ComprobanteExportado(TablaExportable Tabla, string NombreBase);

public sealed class ExportDocumentQueryValidator : AbstractValidator<ExportDocumentQuery>
{
    public ExportDocumentQueryValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class ExportDocumentQueryHandler(ISender sender) : IRequestHandler<ExportDocumentQuery, Result<ComprobanteExportado>>
{
    public async Task<Result<ComprobanteExportado>> Handle(ExportDocumentQuery request, CancellationToken ct)
    {
        var comprobante = await sender.Send(new GetDocumentQuery(request.PublicId), ct);
        if (comprobante.IsFailure) return Result.Failure<ComprobanteExportado>(comprobante.Error);
        var d = comprobante.Value;

        var tabla = ComoTabla(d);
        var nombre = d.Number is { } n ? $"comprobante-{d.VoucherTypeCode}-{n}" : $"borrador-{d.VoucherTypeCode}-{d.Date:yyyyMMdd}";
        return Result.Success(new ComprobanteExportado(tabla, nombre));
    }

    /// <summary>Las columnas de la pantalla, la cuenta y el tercero partidos en código y nombre para poder filtrar y ordenar por cada uno.</summary>
    public static TablaExportable ComoTabla(ComprobanteDto d)
    {
        var columnas = new List<ColumnaExportable>
        {
            new("Línea", TipoDeColumna.Entero),
            new("Cuenta"),
            new("Nombre de la cuenta"),
            new("Identificación del tercero"),
            new("Tercero"),
            new("Tipo doc. cruce"),
            new("Número doc. cruce"),
            new("Centro de costo"),
            new("Sucursal"),
            new("Débito", TipoDeColumna.Moneda),
            new("Crédito", TipoDeColumna.Moneda),
            new("Base", TipoDeColumna.Moneda),
            new("Detalle"),
        };

        var filas = d.Lines.OrderBy(l => l.LineNumber).Select(l => new FilaExportable(
        [
            l.LineNumber, l.AccountCode, l.AccountName, l.PersonTaxId, l.PersonName, l.CrossDocumentType, l.CrossDocumentNumber,
            l.CostCenterName, l.BranchName, l.Debit, l.Credit, l.TaxBase, l.Detail,
        ])).ToList();

        var totales = new FilaExportable(
            [null, "Totales", null, null, null, null, null, null, null, d.Lines.Sum(l => l.Debit), d.Lines.Sum(l => l.Credit), null, null], Resaltada: true);

        var notas = new List<string>
        {
            $"Registró {d.RegisteredBy} el {d.RegisteredAt.ToLocalTime():dd/MM/yyyy HH:mm}"
                + (d.PostedBy is not null ? $"; contabilizó {d.PostedBy} el {d.PostedAt?.ToLocalTime():dd/MM/yyyy HH:mm}." : "."),
            $"Origen: {d.Origin.ModuleName}.",
        };
        if (d.Reversal.ReversedByNumber is { } por) notas.Add($"Reversado por {por}. Motivo: {d.Reversal.Reason}");
        if (d.Reversal.ReversesNumber is { } a) notas.Add($"Reversa a {a}.");

        return new TablaExportable(
            $"Comprobante {d.Referencia}",
            $"{d.VoucherTypeCode} · {d.VoucherTypeName} · {d.Date:dd/MM/yyyy} · {Estado(d.Status)} · {d.Description}",
            columnas, filas, totales, notas);
    }

    private static string Estado(string status) => status switch
    {
        "Draft" => "Borrador",
        "Posted" => "Contabilizado",
        "Reversed" => "Reversado",
        _ => status,
    };
}
