namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>Lo que se edita en el formulario de una auxiliar (feature 009, US2): reglas, banco e impuesto. Va y viene de <see cref="CuentaDto"/>/<see cref="CuentaRequest"/>.</summary>
public sealed class ReglasDeCuentaModelo
{
    public HashSet<string> Modulos { get; set; } = ["CNT"];
    public bool ExigeTercero { get; set; }
    public bool ExigeDocumento { get; set; }
    public bool ExigeCentroDeCosto { get; set; }
    public bool ExigeSucursal { get; set; }

    public bool EsBancaria { get; set; }
    public Guid? BancoPublicId { get; set; }
    public string? NumeroDeCuenta { get; set; }

    public bool EsDeImpuesto { get; set; }
    public string ClaseDeImpuesto { get; set; } = "Withholding";
    public string? ConceptoTributario { get; set; }
    public bool ExigeBase { get; set; }
    public List<TarifaDto> Tarifas { get; set; } = [];

    public static ReglasDeCuentaModelo Desde(CuentaDto c) => new()
    {
        Modulos = [.. c.EnabledModules],
        ExigeTercero = c.RequiresThirdParty,
        ExigeDocumento = c.RequiresCrossDocument,
        ExigeCentroDeCosto = c.RequiresCostCenter,
        ExigeSucursal = c.RequiresBranch,
        EsBancaria = c.Bank is not null,
        BancoPublicId = c.Bank?.BankPublicId,
        NumeroDeCuenta = c.Bank?.AccountNumber,
        EsDeImpuesto = c.Tax is not null,
        ClaseDeImpuesto = c.Tax?.Kind ?? "Withholding",
        ConceptoTributario = c.Tax?.ConceptCode,
        ExigeBase = c.Tax?.RequiresTaxBase ?? false,
        Tarifas = c.Tax is null ? [] : [.. c.Tax.Rates],
    };

    public CuentaRequest ARequest(string code, string name, Guid? parentPublicId) => new(
        code, name, parentPublicId, [.. Modulos], ExigeTercero, ExigeDocumento, ExigeCentroDeCosto, ExigeSucursal,
        EsBancaria && BancoPublicId is { } b ? new CuentaBancariaRequest(b, NumeroDeCuenta) : null,
        EsDeImpuesto ? new CuentaDeImpuestoRequest(ClaseDeImpuesto, ConceptoTributario, ExigeBase, [.. Tarifas.OrderBy(t => t.ValidFrom)]) : null);

    public static readonly IReadOnlyList<(string Codigo, string Nombre)> ModulosDisponibles =
    [
        ("CNT", "Contabilidad (digitación)"), ("NOM", "Nómina"), ("CAR", "Cartera"), ("INV", "Inventario"),
        ("TES", "Tesorería"), ("CDT", "CDT y ahorros"), ("ACT", "Activos"),
    ];

    public static readonly IReadOnlyList<(string Codigo, string Nombre)> ClasesDeImpuesto =
    [
        ("Withholding", "Retención en la fuente"), ("Vat", "IVA"), ("Ica", "ICA / retención de ICA"), ("Gmf", "GMF (4×1000)"), ("IncomeTax", "Impuesto de renta"),
    ];
}
