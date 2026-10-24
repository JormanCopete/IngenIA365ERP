using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

// Feature 015, US10 (T0267; E-01 c, enmienda C10 de la 013; contratos-v2.md §2.3; mensajes.md §5.4): el archivo de desembolsos de Cartera viaja a Tesorería para
// que registre resultados y concilie (D16). Origen el archivo (LND_DisbursementFiles), clave Confirmation:{archivo:N} —el generado y el anulado se distinguen por
// el tipo: las claves de evento son un conjunto cerrado y Voiding:{x} no existe (DT §2.7)—, related la primera orden, tercero la cooperativa (sin persona), y lo
// origina la persona que genera o anula el archivo.

/// <summary>El archivo como adjunto de módulo (ámbito <c>LoanDisbursement</c>). (nuevo)</summary>
public sealed record ArchivoDeDesembolsosAdjuntoV1
{
    public Guid AttachmentPublicId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string Sha256 { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public string? FormatCode { get; init; }
}

/// <summary>La cuenta de origen elegida al generar: Tesorería la casa con su cuenta propia por banco y número. (nuevo)</summary>
public sealed record CuentaDeOrigenDelArchivoV1
{
    public string? BankCode { get; init; }

    public string AccountNumber { get; init; } = string.Empty;
}

/// <summary>Una orden del archivo. (nuevo)</summary>
public sealed record OrdenDelArchivoV1
{
    public Guid OrderPublicId { get; init; }

    public string OrderNumber { get; init; } = string.Empty;

    public decimal NetAmount { get; init; }

    public Guid PayeePersonPublicId { get; init; }
}

/// <summary><c>ArchivoDeDesembolsosGenerado</c> v1 (<c>CAR → Treasury</c>): Cartera generó un archivo de desembolsos; Tesorería lo guarda con sus órdenes y no las paga. (nuevo)</summary>
public sealed record ArchivoDeDesembolsosGeneradoV1
{
    public const string Type = "ArchivoDeDesembolsosGenerado";

    public Guid FilePublicId { get; init; }

    public string FileNumber { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public ArchivoDeDesembolsosAdjuntoV1 File { get; init; } = new();

    public CuentaDeOrigenDelArchivoV1? SourceBankAccount { get; init; }

    public IReadOnlyList<OrdenDelArchivoV1> Orders { get; init; } = [];

    public decimal Total { get; init; }

    public int LineCount { get; init; }

    public UserRefV1 GeneratedBy { get; init; } = new();
}

/// <summary><c>ArchivoDeDesembolsosAnulado</c> v1 (<c>CAR → Treasury</c>): Cartera anuló el archivo; sus órdenes quedan a la espera de otro archivo sin resultado. (nuevo)</summary>
public sealed record ArchivoDeDesembolsosAnuladoV1
{
    public const string Type = "ArchivoDeDesembolsosAnulado";

    public Guid FilePublicId { get; init; }

    public string FileNumber { get; init; } = string.Empty;

    public int Version { get; init; } = 1;

    public ArchivoDeDesembolsosAdjuntoV1 File { get; init; } = new();

    public CuentaDeOrigenDelArchivoV1? SourceBankAccount { get; init; }

    public IReadOnlyList<OrdenDelArchivoV1> Orders { get; init; } = [];

    public decimal Total { get; init; }

    public int LineCount { get; init; }

    public UserRefV1 GeneratedBy { get; init; } = new();

    public string Reason { get; init; } = string.Empty;
}
