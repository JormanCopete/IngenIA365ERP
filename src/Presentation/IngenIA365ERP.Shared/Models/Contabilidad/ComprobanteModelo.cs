using IngenIA365ERP.Shared.Components.Shared;
using IngenIA365ERP.Shared.Services.Contabilidad;

namespace IngenIA365ERP.Shared.Models.Contabilidad;

/// <summary>
/// Una línea tal como la ve la ventana de digitación (feature 009, US3): la cuenta elegida con sus
/// reglas, el tercero elegido y las referencias por <c>PublicId</c>. Se convierte al cuerpo de
/// <c>POST /documents/drafts</c> y <c>/validate</c> con <see cref="ARequest"/>.
/// </summary>
public sealed class LineaDeComprobanteModelo
{
    public CuentaBuscadaDto? Cuenta { get; set; }
    public PersonSearchPicker.PersonSearchItem? Tercero { get; set; }
    public Guid? SucursalPublicId { get; set; }
    public Guid? CentroPublicId { get; set; }
    public string? TipoCruce { get; set; }
    public string? NumeroCruce { get; set; }
    public decimal Debito { get; set; }
    public decimal Credito { get; set; }
    public string? Detalle { get; set; }
    public decimal? Base { get; set; }

    public LineaDeBorradorRequest ARequest() =>
        new(Cuenta?.Code ?? string.Empty, SucursalPublicId, CentroPublicId, Tercero?.PublicId,
            string.IsNullOrWhiteSpace(TipoCruce) ? null : TipoCruce, string.IsNullOrWhiteSpace(NumeroCruce) ? null : NumeroCruce,
            Debito, Credito, string.IsNullOrWhiteSpace(Detalle) ? null : Detalle, Base);

    /// <summary>Copia para «duplicar línea»: todo menos los importes, que se digitan.</summary>
    public LineaDeComprobanteModelo Duplicar() => new()
    {
        Cuenta = Cuenta, Tercero = Tercero, SucursalPublicId = SucursalPublicId, CentroPublicId = CentroPublicId,
        TipoCruce = TipoCruce, NumeroCruce = NumeroCruce, Detalle = Detalle,
    };

    /// <summary>Línea nueva que repite tercero, centro y sucursal de la anterior (U3: lo repetido no se vuelve a digitar).</summary>
    public LineaDeComprobanteModelo Siguiente() => new()
    {
        Tercero = Tercero, SucursalPublicId = SucursalPublicId, CentroPublicId = CentroPublicId, Detalle = Detalle,
    };

    public static LineaDeComprobanteModelo Desde(LineaDeComprobanteDto l, CuentaBuscadaDto? cuenta) => new()
    {
        Cuenta = cuenta ?? new CuentaBuscadaDto(l.AccountPublicId, l.AccountCode, l.AccountName, string.Empty, false, false, false, false, false),
        Tercero = l.PersonPublicId is { } p ? new PersonSearchPicker.PersonSearchItem { PublicId = p, IdentificationNumber = l.PersonTaxId ?? string.Empty, FullName = l.PersonName ?? string.Empty } : null,
        SucursalPublicId = l.BranchPublicId == Guid.Empty ? null : l.BranchPublicId,
        CentroPublicId = l.CostCenterPublicId,
        TipoCruce = l.CrossDocumentType,
        NumeroCruce = l.CrossDocumentNumber,
        Debito = l.Debit,
        Credito = l.Credit,
        Detalle = l.Detail,
        Base = l.TaxBase,
    };
}

/// <summary>Cabecera y líneas de la ventana; los totales se calculan aquí para el pie.</summary>
public sealed class ComprobanteModelo
{
    public string TipoCode { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string Descripcion { get; set; } = string.Empty;
    public List<LineaDeComprobanteModelo> Lineas { get; set; } = [];

    public decimal TotalDebito => Lineas.Sum(l => l.Debito);
    public decimal TotalCredito => Lineas.Sum(l => l.Credito);
    public decimal Diferencia => TotalDebito - TotalCredito;
    public bool Cuadra => Diferencia == 0m && Lineas.Count >= 2;

    public BorradorRequest ARequest() =>
        new(TipoCode, DateOnly.FromDateTime(Fecha), Descripcion, Lineas.Select(l => l.ARequest()).ToList());

    public static ComprobanteModelo Desde(ComprobanteDto d, IReadOnlyDictionary<string, CuentaBuscadaDto>? cuentas = null) => new()
    {
        TipoCode = d.VoucherTypeCode,
        Fecha = d.Date.ToDateTime(TimeOnly.MinValue),
        Descripcion = d.Description,
        Lineas = d.Lines.Select(l => LineaDeComprobanteModelo.Desde(l, cuentas?.GetValueOrDefault(l.AccountCode))).ToList(),
    };
}
