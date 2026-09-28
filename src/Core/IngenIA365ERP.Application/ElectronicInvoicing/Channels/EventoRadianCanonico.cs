using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Channels;

/// <summary>
/// Forma mínima de un evento RADIAN que emite la cooperativa como adquirente (feature 012, I4, T700; contracts/dian.md §3.1 y
/// §14.3): 030 acuse de recibo y 032 recibo del bien. I4 sólo la declara para que el puerto quede completo; la emisión, su
/// numeración y su constructor son de I5 (US13, <c>EmitRadianEventCommand</c>), que puede ampliarla. (nuevo)
/// </summary>
/// <param name="SchemaVersion">Versión de la forma; 1.</param>
/// <param name="Kind"><see cref="ElectronicDocumentKind.RadianEvent030"/> o <see cref="ElectronicDocumentKind.RadianEvent032"/>.</param>
/// <param name="DianDocumentTypeCode">Tipo DIAN del <c>ApplicationResponse</c>, de <c>CatalogoDian</c>.</param>
/// <param name="EventCode">Código del evento, de <c>CatalogoDian</c>.</param>
/// <param name="Environment">Ambiente sellado.</param>
/// <param name="Number">Numeración propia del evento (la define I5).</param>
/// <param name="IssuedAt">Fecha y hora locales del evento.</param>
/// <param name="Issuer">La cooperativa (adquirente que emite el evento).</param>
/// <param name="Supplier">El proveedor que expidió la factura.</param>
/// <param name="ReferencedInvoice">La factura del proveedor: número, CUFE y fecha.</param>
/// <param name="Notes">Notas libres.</param>
public sealed record EventoRadianCanonico(
    int SchemaVersion,
    ElectronicDocumentKind Kind,
    string DianDocumentTypeCode,
    string EventCode,
    DianEnvironment Environment,
    NumeroCanonico Number,
    DateTimeOffset IssuedAt,
    ParteCanonica Issuer,
    ParteCanonica Supplier,
    DocumentoCorregidoCanonico ReferencedInvoice,
    IReadOnlyList<string> Notes);
