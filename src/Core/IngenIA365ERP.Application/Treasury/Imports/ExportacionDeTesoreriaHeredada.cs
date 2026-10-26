using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Imports;

// Feature 015, fase 46 (T0472; data-model §24 paso 2; FR-161): la exportación para archivo de TODA fila de la Tesorería anterior —cheques, conceptos y facturas, también
// las dadas de baja— antes de su retiro. Vive sólo hasta el retiro: lee las entidades heredadas, que la migración RetiroDeLaTesoreriaHeredada borra, y se va con ellas en
// el mismo commit. Por eso la exportación se corre con la versión ANTERIOR al retiro (runbook docs/operaciones/tesoreria-retiro-heredado.md, paso 2).

/// <summary>Un archivo de la exportación, ya armado (el xlsx lo arma la API con el exportador de tablas; el csv, <see cref="CsvDeTablaHeredada"/>). (nuevo)</summary>
public sealed record ArchivoDeExportacionHeredada(string Nombre, string TipoDeContenido, byte[] Contenido);

/// <summary>Un archivo guardado: su adjunto no borrable y su huella SHA-256. (nuevo)</summary>
public sealed record LegacyExportFileDto(string FileName, Guid AttachmentPublicId, string Sha256, long Bytes);

/// <summary>El resultado de la exportación: los archivos y las filas por tabla. (nuevo)</summary>
public sealed record LegacyTreasuryExportDto(IReadOnlyList<LegacyExportFileDto> Files, IReadOnlyDictionary<string, int> Rows);

/// <summary>
/// <c>GET /api/treasury/legacy-export/tables</c> (<c>Treasury.Imports.Run</c>): las tres tablas de la Tesorería anterior como <see cref="TablaExportable"/> —una por
/// tabla, todas sus columnas, también las filas dadas de baja—, para revisarlas o archivarlas. (nuevo)
/// </summary>
public sealed record GetLegacyTreasuryTablesQuery : IRequest<Result<IReadOnlyList<TablaExportable>>>;

public sealed class GetLegacyTreasuryTablesQueryHandler(IApplicationDbContext db) : IRequestHandler<GetLegacyTreasuryTablesQuery, Result<IReadOnlyList<TablaExportable>>>
{
    public const string Cheques = "Cheques de la Tesorería anterior";
    public const string Conceptos = "Conceptos de la Tesorería anterior";
    public const string Facturas = "Facturas de la Tesorería anterior";

    private static ColumnaExportable C(string nombre, TipoDeColumna tipo = TipoDeColumna.Texto) => new(nombre, tipo);

    public async Task<Result<IReadOnlyList<TablaExportable>>> Handle(GetLegacyTreasuryTablesQuery request, CancellationToken ct)
    {
        var cheques = await db.Checks.AsNoTracking().IgnoreQueryFilters().OrderBy(c => c.Id).ToListAsync(ct);
        var conceptos = await db.TreasuryConcepts.AsNoTracking().IgnoreQueryFilters().OrderBy(c => c.Id).ToListAsync(ct);
        var facturas = await db.TreasuryInvoices.AsNoTracking().IgnoreQueryFilters().OrderBy(f => f.Id).ToListAsync(ct);
        const string Sub = "Exportación para archivo antes del retiro (FR-161)";
        IReadOnlyList<TablaExportable> tablas =
        [
            new(Cheques, Sub,
                [C("Id", TipoDeColumna.Entero), C("PublicId"), C("Concepto"), C("Banco", TipoDeColumna.Entero), C("Consecutivo", TipoDeColumna.Entero),
                    C("Persona", TipoDeColumna.Entero), C("Comprobante"), C("NumeroComprobante", TipoDeColumna.Entero), C("Fecha", TipoDeColumna.Fecha),
                    C("NumeroCheque", TipoDeColumna.Entero), C("Valor", TipoDeColumna.Moneda), C("Estado"), C("DetalleAnulacion"), C("UsuarioAnulacion"),
                    C("ComprobanteAnulacion"), C("NumeroComprobanteAnulacion", TipoDeColumna.Entero), C("FechaAnulacion"), C("UsuarioRegistro"), C("FechaRegistro"),
                    C("DadoDeBaja")],
                cheques.Select(c => new FilaExportable([c.Id, c.PublicId.ToString(), c.ConceptCode, c.BankId, c.SequentialNumber, c.PersonId, c.VoucherCode, c.VoucherNumber,
                    c.CheckDate, c.CheckNumber, c.Amount, c.Status, c.VoidDetail, c.VoidUserId, c.VoidVoucherCode, c.VoidVoucherNumber, c.VoidDate, c.RecordUserId,
                    c.RecordDate, c.IsDeleted])).ToList(), null, [$"{cheques.Count} filas; «E» = girado sin cobrar."]),
            new(Conceptos, Sub,
                [C("Id", TipoDeColumna.Entero), C("PublicId"), C("Codigo"), C("Nombre"), C("NombreCorto"), C("Tipo"), C("CuentaDebito", TipoDeColumna.Entero),
                    C("CuentaCredito", TipoDeColumna.Entero), C("DadoDeBaja")],
                conceptos.Select(c => new FilaExportable([c.Id, c.PublicId.ToString(), c.ConceptCode, c.Name, c.ShortName, c.ConceptType, c.DebitAccountId, c.CreditAccountId,
                    c.IsDeleted])).ToList(), null, [$"{conceptos.Count} filas; sus reemplazos son los conceptos varios de Recaudo (FR-027)."]),
            new(Facturas, Sub,
                [C("Id", TipoDeColumna.Entero), C("PublicId"), C("Concepto"), C("Consecutivo", TipoDeColumna.Entero), C("FechaIngreso", TipoDeColumna.Fecha),
                    C("Periodo", TipoDeColumna.Entero), C("Factura"), C("FechaFactura", TipoDeColumna.Fecha), C("Persona", TipoDeColumna.Entero),
                    C("Vencimiento", TipoDeColumna.Fecha), C("Cuenta"), C("CentroDeCosto"), C("Sucursal"), C("Documento"), C("Descripcion"), C("Valor", TipoDeColumna.Moneda),
                    C("FechaProgramada", TipoDeColumna.Fecha), C("FechaPago", TipoDeColumna.Fecha), C("Comprobante"), C("NumeroComprobante", TipoDeColumna.Entero),
                    C("Banco"), C("NumeroCheque", TipoDeColumna.Entero), C("ValorCheque", TipoDeColumna.Moneda), C("Estado"), C("TipoDocumento"), C("NumeroDocumento"),
                    C("FormaDePago"), C("ConComision"), C("DadoDeBaja")],
                facturas.Select(f => new FilaExportable([f.Id, f.PublicId.ToString(), f.ConceptCode, f.ConsecutiveNumber, f.EntryDate, f.PeriodCode, f.InvoiceNumber,
                    f.InvoiceDate, f.PersonId, f.DueDate, f.AccountCode, f.CostCenterId, f.BranchId, f.DocumentCode, f.Description, f.Amount, f.ScheduledDate,
                    f.PaymentDate, f.VoucherCode, f.VoucherNumber, f.BankId, f.CheckNumber, f.CheckAmount, f.Status, f.DocumentType, f.DocumentNumber, f.PaymentForm,
                    f.HasCommission, f.IsDeleted])).ToList(), null, [$"{facturas.Count} filas; «P» = pendiente de pago."]),
        ];
        return Result.Success(tablas);
    }
}

/// <summary>El csv de una tabla de la exportación: UTF-8 con BOM, separado por «;», números y fechas en formato invariable (para que la huella no dependa de la cultura). (nuevo)</summary>
public static class CsvDeTablaHeredada
{
    public static byte[] Escribir(TablaExportable tabla)
    {
        var sb = new StringBuilder();
        sb.AppendJoin(';', tabla.Columnas.Select(c => Campo(c.Nombre))).Append("\r\n");
        foreach (var fila in tabla.Filas)
            sb.AppendJoin(';', fila.Valores.Select(v => Campo(Texto(v)))).Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Texto(object? v) => v switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? string.Empty,
    };

    private static string Campo(string s) => s.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{s.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : s;
}

/// <summary>
/// <c>POST /api/treasury/legacy-export</c> (<c>Treasury.Imports.Run</c>, con clave; fase 46, T0472): guarda los archivos de la exportación como adjuntos <b>no borrables</b>
/// <c>TreasuryLegacyExport</c> de la configuración de Tesorería, con su huella SHA-256, y lo audita con las filas por tabla. (nuevo)
/// </summary>
public sealed record ArchiveLegacyTreasuryExportCommand(IReadOnlyList<ArchivoDeExportacionHeredada> Files, IReadOnlyDictionary<string, int> Rows)
    : IRequest<Result<LegacyTreasuryExportDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ArchiveLegacyTreasuryExportCommandValidator : AbstractValidator<ArchiveLegacyTreasuryExportCommand>
{
    public ArchiveLegacyTreasuryExportCommandValidator()
    {
        RuleFor(x => x.Files).NotEmpty();
        RuleForEach(x => x.Files).Must(f => f.Contenido is { Length: > 0 } && !string.IsNullOrWhiteSpace(f.Nombre));
    }
}

public sealed class ArchiveLegacyTreasuryExportCommandHandler(
    IApplicationDbContext db,
    ISender sender,
    IActorActual actorActual,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<ArchiveLegacyTreasuryExportCommand, Result<LegacyTreasuryExportDto>>
{
    public const string TipoDeAdjunto = AdjuntosDeModulo.ExportacionDeTesoreriaHeredada;

    public async Task<Result<LegacyTreasuryExportDto>> Handle(ArchiveLegacyTreasuryExportCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var setup = await db.TreasurySetups.AsNoTracking().Where(s => s.Id == TreasurySetup.IdDeLaFilaUnica).Select(s => (Guid?)s.PublicId).FirstOrDefaultAsync(ct);
        if (setup is not { } dueno) return Result.Failure<LegacyTreasuryExportDto>(ErroresDeTesoreria.NoEncontrado("La configuración de Tesorería"));
        var guardados = new List<LegacyExportFileDto>();
        foreach (var archivo in request.Files)
        {
            var huella = Convert.ToHexStringLower(SHA256.HashData(archivo.Contenido));
            var subida = await sender.Send(new UploadAttachmentCommand(TipoDeAdjunto, dueno, archivo.Nombre, archivo.TipoDeContenido, archivo.Contenido), ct);
            if (subida.IsFailure) return Result.Failure<LegacyTreasuryExportDto>(subida.Error);
            guardados.Add(new LegacyExportFileDto(archivo.Nombre, subida.Value, huella, archivo.Contenido.LongLength));
        }
        await auditoria.AgregarAsync("Treasury.LegacyExport.Archived", "TreasurySetup", dueno, null,
            new { files = guardados.Select(g => new { g.FileName, g.AttachmentPublicId, g.Sha256, g.Bytes }), rows = request.Rows, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new LegacyTreasuryExportDto(guardados, request.Rows));
    }
}
