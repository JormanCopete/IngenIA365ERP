using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Sales.Payments;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Canonical;

/// <summary>La condición tributaria de la cooperativa a la fecha (parámetros <c>TAX</c>, T21): sus responsabilidades y su tributo. (nuevo)</summary>
public sealed record CondicionTributariaDelEmisor(bool ResponsableDeIva, bool GranContribuyente, bool AgenteDeRetencionIva, bool Autorretenedor);

/// <summary>
/// Lo que el constructor completa desde la plataforma (contracts/dian.md §4.1): la configuración sellada (emisor y ambiente), la
/// resolución y el número que asignó la numeración, la contingencia (que cambia el tipo de documento), la condición tributaria
/// del emisor, el documento electrónico que corrige una nota y el software del POS. (nuevo)
/// </summary>
public sealed record ContextoDelCanonico
{
    public ElectronicEmissionSetting Configuracion { get; init; } = new();

    /// <summary>Nula en las notas (llevan su propio consecutivo).</summary>
    public DianNumberingResolution? Resolucion { get; init; }

    public string Prefijo { get; init; } = string.Empty;

    public long Consecutivo { get; init; }

    public ContingencyType? Contingencia { get; init; }

    public CondicionTributariaDelEmisor Emisor { get; init; } = new(false, false, false, false);

    /// <summary>El documento electrónico que corrige una nota: número, código único y fecha (el concepto lo trae la entrada).</summary>
    public DocumentoCorregidoCanonico? Corregido { get; init; }

    public SoftwarePosCanonico? SoftwarePos { get; init; }
}

/// <summary>El número que asignó la numeración y lo sellado, para <see cref="ConstructorDelCanonico.ConstruirAsync"/>. (nuevo)</summary>
public sealed record NumeracionDelCanonico(
    ElectronicEmissionSetting Configuracion,
    DianNumberingResolution? Resolucion,
    string Prefijo,
    long Consecutivo,
    ContingencyType? Contingencia = null,
    ElectronicDocument? Corregido = null,
    SoftwarePosCanonico? SoftwarePos = null);

/// <summary>
/// Lo sellado que entra al constructor de un evento RADIAN (I5, T803): la configuración, la numeración propia del evento (T802) y el
/// instante en que se pidió (el <c>IssuedAt</c> del documento electrónico). (nuevo)
/// </summary>
public sealed record NumeracionDelEvento(ElectronicEmissionSetting Configuracion, string Prefijo, long Consecutivo, DateTimeOffset EmitidoEn);

/// <summary>El evento RADIAN construido con su JSON, su SHA-256 y su huella (I5, T803). (nuevo)</summary>
public sealed record EventoConstruido(EventoRadianCanonico Evento, string Json, string CanonicalSha256, string EconomicFingerprint);

/// <summary>El canónico construido con su JSON, su SHA-256 (<c>CanonicalSha256</c>) y su huella económica (<c>EconomicFingerprint</c>). (nuevo)</summary>
public sealed record CanonicoConstruido(DocumentoElectronicoCanonico Documento, string Json, string CanonicalSha256, string EconomicFingerprint);

/// <summary>
/// El <b>único</b> constructor de <see cref="DocumentoElectronicoCanonico"/> (feature 012, I4, T703; contracts/dian.md §4): toma la
/// entrada neutral del módulo fuente y la completa con lo que es de plataforma —emisor de la configuración sellada y de los
/// parámetros <c>TAX</c> vigentes, resolución, ambiente y códigos de <see cref="CatalogoDian"/> a la fecha de la operación—. La
/// contraparte sale de la copia fiscal de <b>mayor versión</b> (T52), nunca del maestro de hoy.
///
/// <para>
/// Reglas (§4.2): impuestos por línea, ya redondeados por línea, y los totales como suma exacta de las líneas (FR-017);
/// <c>payable</c> es el <c>Total</c> y <c>amountDue</c> el <c>AmountDue</c> del documento (T26), la retención del comprador viaja
/// como retención y nunca como medio de pago, y lo que separa el total del documento de la suma de las líneas queda en
/// <c>rounding</c>; <c>paymentForm = Credit</c> si algún pago es de clase crédito, y entonces el vencimiento es obligatorio; el tipo
/// de documento según tipo y contingencia (§4.3); sin resolución en las notas.
/// </para>
///
/// <para>
/// Determinista: dos construcciones del mismo documento dan el mismo JSON (<see cref="SerializadorCanonico"/>) y el mismo SHA-256,
/// lo que permite subir el artefacto después del commit (§4.1). Si falta un dato, no construye:
/// <c>ElectronicInvoicing.Document.MissingData</c> con <c>data.missing[] { field, where, permission }</c> (§4.4, Principio VIII).
/// </para>
/// (nuevo)
/// </summary>
public sealed class ConstructorDelCanonico(ILectorDeParametros parametros)
{
    private const string Unidades = "Inventario › Unidades";
    private const string PermisoUnidades = "Inventory.Catalog.Manage";
    private const string Impuestos = "Maestros › Impuestos";
    private const string PermisoImpuestos = "Core.Taxes.Manage";
    private const string Medios = "Maestros › Medios de pago";
    private const string PermisoMedios = "Core.PaymentMeans.Manage";
    private const string Personas = "Maestros › Personas";
    private const string PermisoPersonas = "Core.People.Update";
    private const string Configuracion = "Facturación electrónica › Configuración de emisión";
    private const string PermisoConfiguracion = "ElectronicInvoicing.Settings.Manage";
    private const string Resoluciones = "Facturación electrónica › Resoluciones";
    private const string PermisoResoluciones = "ElectronicInvoicing.Resolutions.Manage";
    private const string Cajas = "Inventario › Puntos de venta › Cajas";
    private const string PermisoCajas = "Inventory.PointsOfSale.Manage";

    /// <summary>
    /// Construye con la condición tributaria del emisor leída de los parámetros <c>TAX</c> vigentes a la fecha de la operación. Un
    /// parámetro que no se puede leer toma su defecto del catálogo de parámetros (el lector lo resuelve).
    /// </summary>
    public async Task<Result<CanonicoConstruido>> ConstruirAsync(EntradaDeDocumentoElectronico entrada, NumeracionDelCanonico numeracion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(numeracion);
        var fecha = entrada.OperationDate;
        var emisor = new CondicionTributariaDelEmisor(
            await LeerAsync(ParametrosTributarios.ResponsableIva, fecha, ct),
            await LeerAsync(ParametrosTributarios.GranContribuyente, fecha, ct),
            await LeerAsync(ParametrosTributarios.AgenteRetencionIva, fecha, ct),
            await LeerAsync(ParametrosTributarios.Autorretenedor, fecha, ct));

        var corregido = numeracion.Corregido is { } c
            ? new DocumentoCorregidoCanonico(c.Number, c.UniqueCode, c.IssueDate, null)
            : null;

        return Construir(entrada, new ContextoDelCanonico
        {
            Configuracion = numeracion.Configuracion,
            Resolucion = numeracion.Resolucion,
            Prefijo = numeracion.Prefijo,
            Consecutivo = numeracion.Consecutivo,
            Contingencia = numeracion.Contingencia,
            Emisor = emisor,
            Corregido = corregido,
            SoftwarePos = numeracion.SoftwarePos,
        });
    }

    private async Task<bool> LeerAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var r = await parametros.LeerComoAsync<bool>(ParametrosTributarios.Modulo, clave, fecha, ct: ct);
        return r.IsSuccess && r.Value;
    }

    /// <summary>La regla pura: la entrada, lo de plataforma y el catálogo (el embebido si no se da otro).</summary>
    public static Result<CanonicoConstruido> Construir(EntradaDeDocumentoElectronico entrada, ContextoDelCanonico contexto, CatalogoDian? catalogo = null)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(contexto);
        catalogo ??= CatalogoDian.Embebido;
        var fecha = entrada.OperationDate;
        var faltan = new List<DatoFaltante>();
        var esNota = EsNota(entrada.Kind);

        // ---- tipo de documento (§4.3)
        var tipo = catalogo.TipoDeDocumento(entrada.Kind, contexto.Contingencia, fecha);
        if (tipo is null)
            faltan.Add(new DatoFaltante("dianDocumentTypeCode", "Catálogo DIAN", null,
                $"El catálogo DIAN vigente al {Fecha(fecha)} no trae el tipo de documento de {entrada.Kind}."));

        // ---- número y resolución
        var numero = new NumeroCanonico(contexto.Prefijo, contexto.Consecutivo,
            contexto.Prefijo + contexto.Consecutivo.ToString(CultureInfo.InvariantCulture));
        ResolucionCanonica? resolucion = null;
        if (!esNota)
        {
            if (contexto.Resolucion is { } r)
                resolucion = new ResolucionCanonica(r.ResolutionNumber, r.ResolutionDate, r.RangeFrom, r.RangeTo, r.ValidFrom, r.ValidTo);
            else
                faltan.Add(new DatoFaltante("resolution", Resoluciones, PermisoResoluciones,
                    $"No hay resolución de numeración para el documento {numero.Full}: regístrela en {Resoluciones}."));
        }

        var emitidoEn = new DateTimeOffset(DateTime.SpecifyKind(entrada.ConfirmedAtUtc, DateTimeKind.Utc)).ToOffset(IDateTimeService.DesfaseColombia);

        // ---- partes
        var emisor = Emisor(contexto, catalogo, fecha, faltan);
        var contraparte = Contraparte(entrada, catalogo, fecha, faltan);

        // ---- líneas, impuestos y retenciones
        var lineas = new List<LineaCanonica>();
        foreach (var linea in entrada.Lineas.OrderBy(l => l.LineNumber))
            lineas.Add(Linea(linea, entrada, catalogo, fecha, faltan));

        var retenciones = entrada.Impuestos.Where(i => i.EsRetencion).ToList();
        foreach (var r in retenciones) ValidarTributo(r, catalogo, fecha, faltan);
        var withholdings = retenciones
            .GroupBy(r => (Codigo: r.DianTaxCode ?? string.Empty, r.Rate))
            .OrderBy(g => g.Key.Codigo, StringComparer.Ordinal).ThenBy(g => g.Key.Rate)
            .Select(g => new RetencionCanonica(g.Key.Codigo, g.Key.Rate, g.Sum(x => x.Base), g.Sum(x => x.Amount)))
            .ToList();
        var impuestosDelDocumento = entrada.Impuestos.Where(i => !i.EsRetencion && i.LineNumber is null).ToList();
        foreach (var i in impuestosDelDocumento) ValidarTributo(i, catalogo, fecha, faltan);

        // ---- pagos y forma de pago
        var pagos = new List<PagoCanonico>();
        var indice = 0;
        foreach (var pago in entrada.Pagos)
        {
            indice++;
            if (catalogo.MedioDePago(pago.DianPaymentMeansCode, fecha) is null)
            {
                faltan.Add(new DatoFaltante($"payments[{indice.ToString(CultureInfo.InvariantCulture)}].dianPaymentMeansCode",
                    $"{Medios} ({pago.MeansName})", PermisoMedios,
                    $"El medio de pago {pago.MeansName} no tiene un código DIAN válido: complételo en {Medios} ({PermisoMedios})."));
            }
            pagos.Add(new PagoCanonico(pago.DianPaymentMeansCode ?? string.Empty, pago.Amount, pago.Reference));
        }
        var credito = entrada.Pagos.Any(p => ClasesDeMedio.EsCredito(p.MeansClass));
        if (catalogo.FormaDePago(credito, fecha) is null)
            faltan.Add(new DatoFaltante("paymentForm", "Catálogo DIAN", null, $"El catálogo DIAN vigente al {Fecha(fecha)} no trae la forma de pago."));
        if (credito && entrada.DueDate is null)
            faltan.Add(new DatoFaltante("dueDate", "El documento (fecha de vencimiento del crédito)", null,
                "Una venta a crédito exige la fecha de vencimiento: complétela en el documento."));

        // ---- referencias
        DocumentoCorregidoCanonico? corregido = null;
        if (esNota)
            corregido = Corregido(entrada, contexto, catalogo, fecha, faltan);

        // ---- bloques
        ContingenciaCanonica? contingencia = contexto.Contingencia == ContingencyType.Issuer03
            ? new ContingenciaCanonica(nameof(ContingencyType.Issuer03), numero.Full, emitidoEn)
            : null;
        var pos = Pos(entrada, contexto, catalogo, fecha, faltan);
        DocumentoSoporteCanonico? soporte = entrada.Kind is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote
            ? entrada.DocumentoSoporte is { } ds
                ? new DocumentoSoporteCanonico(ds.Generation, ds.PeriodFrom, ds.PeriodTo)
                : new DocumentoSoporteCanonico(GeneracionPorOperacion, null, null)
            : null;

        if (faltan.Count > 0) return Result.Failure<CanonicoConstruido>(ErroresDeFacturacionElectronica.MissingData(faltan));

        // ---- totales (suma exacta de las líneas)
        var lineExtension = lineas.Sum(l => l.LineExtension);
        var allowances = lineas.Sum(l => l.Allowances.Sum(a => a.Amount));
        var taxExclusive = lineExtension - allowances;
        var taxes = lineas.Sum(l => l.Taxes.Sum(t => t.Amount)) + impuestosDelDocumento.Sum(i => i.Amount);
        var taxInclusive = taxExclusive + taxes;
        var charges = 0m;
        var payable = entrada.Totales.Total;

        var documento = new DocumentoElectronicoCanonico
        {
            Kind = entrada.Kind,
            DianDocumentTypeCode = tipo!.Codigo,
            OperationTypeCode = tipo.TipoDeOperacion,
            Environment = contexto.Configuracion.Environment,
            Number = numero,
            Resolution = resolucion,
            IssuedAt = emitidoEn,
            DueDate = credito ? entrada.DueDate : null,
            Currency = entrada.Currency,
            ExchangeRate = entrada.ExchangeRate,
            PaymentForm = credito ? FormaCredito : FormaContado,
            Payments = pagos,
            Issuer = emisor,
            Counterparty = contraparte,
            Lines = lineas,
            Withholdings = withholdings,
            Totals = new TotalesCanonicos
            {
                LineExtension = lineExtension,
                Allowances = allowances,
                TaxExclusive = taxExclusive,
                Taxes = taxes,
                TaxInclusive = taxInclusive,
                Charges = charges,
                Rounding = payable - taxInclusive - charges,
                Payable = payable,
                Withholdings = withholdings.Sum(w => w.Amount),
                AmountDue = entrada.Totales.AmountDue,
            },
            References = new ReferenciasCanonicas
            {
                Corrected = corregido,
                Order = entrada.OrderReference,
                Despatches = entrada.Despatches.ToList(),
            },
            Contingency = contingencia,
            Pos = pos,
            SupportDocument = soporte,
            Notes = entrada.Notas.ToList(),
            Source = new OrigenCanonico(entrada.SourceModule, entrada.DocumentPublicId, entrada.DocumentClass, entrada.DocumentNumber),
        };

        var json = SerializadorCanonico.Serializar(documento);
        return Result.Success(new CanonicoConstruido(documento, json, SerializadorCanonico.Sha256(json), HuellaEconomica.Calcular(DatosFiscales(documento))));
    }

    /// <summary>
    /// La vista del canónico que leen <see cref="HuellaEconomica"/> y <see cref="ReglaDeCorreccionFiscal"/> (Domain no conoce el
    /// canónico). La base de cada línea es su <c>lineExtension</c> y su descuento la suma de sus <c>allowances</c>.
    /// </summary>
    public static DatosFiscalesDelDocumento DatosFiscales(DocumentoElectronicoCanonico documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var c = documento.Counterparty;
        return new DatosFiscalesDelDocumento(
            new ContraparteFiscal(c.IdTypeCode, c.TaxId, c.CheckDigit, c.Name, c.Address.Line, c.Address.CityDaneCode,
                c.ReceptionEmail, c.Phone, c.Responsibilities),
            documento.Lines.Select(l => new LineaFiscal(
                l.LineNumber, l.ProductCode, l.Quantity, l.LineExtension, l.Allowances.Sum(a => a.Amount),
                l.Taxes.Select(t => new ImporteFiscal(t.DianTaxCode, t.Rate ?? t.AmountPerUnit ?? 0m, t.Base, t.Amount)).ToList(),
                [])).ToList(),
            documento.Withholdings.Select(w => new ImporteFiscal(w.DianTaxCode, w.Rate ?? 0m, w.Base, w.Amount)).ToList(),
            new TotalesFiscales(documento.Totals.LineExtension, documento.Totals.Allowances, documento.Totals.TaxExclusive,
                documento.Totals.Taxes, documento.Totals.TaxInclusive, documento.Totals.Charges, documento.Totals.Rounding,
                documento.Totals.Payable, documento.Totals.Withholdings, documento.Totals.AmountDue));
    }

    // ------------------------------------------------------------------------------------------ eventos RADIAN (I5, T803) --

    /// <summary>
    /// Arma el evento RADIAN (030 o 032) con la condición tributaria de la cooperativa leída de los parámetros <c>TAX</c> vigentes a la fecha
    /// del evento. La misma entrada y la misma numeración dan los mismos bytes (el procesador lo vuelve a armar antes de transmitirlo). (nuevo)
    /// </summary>
    public async Task<Result<EventoConstruido>> ConstruirEventoAsync(EntradaDeEventoRadian entrada, NumeracionDelEvento numeracion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(numeracion);
        var fecha = DateOnly.FromDateTime(numeracion.EmitidoEn.ToOffset(IDateTimeService.DesfaseColombia).DateTime);
        var emisor = new CondicionTributariaDelEmisor(
            await LeerAsync(ParametrosTributarios.ResponsableIva, fecha, ct),
            await LeerAsync(ParametrosTributarios.GranContribuyente, fecha, ct),
            await LeerAsync(ParametrosTributarios.AgenteRetencionIva, fecha, ct),
            await LeerAsync(ParametrosTributarios.Autorretenedor, fecha, ct));
        return ConstruirEvento(entrada, numeracion, emisor);
    }

    /// <summary>
    /// La regla pura del evento (contracts/dian.md §4.3, §14.3): tipo 96 y código del evento de <see cref="CatalogoDian"/> a la fecha, emisor
    /// de la configuración sellada, proveedor de la entrada, factura referenciada con su CUFE (sin él → <c>MissingData</c>) y, en el 032, la
    /// recepción. La huella económica del evento es la de lo que refiere: tipo, factura y CUFE. (nuevo)
    /// </summary>
    public static Result<EventoConstruido> ConstruirEvento(EntradaDeEventoRadian entrada, NumeracionDelEvento numeracion, CondicionTributariaDelEmisor emisor)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(numeracion);
        var catalogo = CatalogoDian.Embebido;
        var emitidoEn = new DateTimeOffset(DateTime.SpecifyKind(numeracion.EmitidoEn.UtcDateTime, DateTimeKind.Utc))
            .ToOffset(IDateTimeService.DesfaseColombia);
        emitidoEn = emitidoEn.AddTicks(-(emitidoEn.Ticks % TimeSpan.TicksPerSecond));
        var fecha = DateOnly.FromDateTime(emitidoEn.DateTime);
        var faltan = new List<DatoFaltante>();

        var tipo = catalogo.TipoDeDocumento(entrada.Kind, null, fecha);
        if (tipo?.CodigoDeEvento is null)
            faltan.Add(new DatoFaltante("eventCode", Configuracion, PermisoConfiguracion,
                $"El catálogo DIAN vigente el {fecha:dd/MM/yyyy} no trae el código del evento {entrada.Kind}."));
        if (string.IsNullOrWhiteSpace(entrada.InvoiceCufe))
            faltan.Add(new DatoFaltante("referencedInvoice.uniqueCode", "Compras › Facturas del proveedor", "Inventory.Purchases.Create",
                $"La factura del proveedor {entrada.InvoiceNumber} no tiene CUFE: sin él no hay evento RADIAN. Regístrelo en la factura."));
        if (entrada.Kind == ElectronicDocumentKind.RadianEvent032 && string.IsNullOrWhiteSpace(entrada.ReceiptNumber))
            faltan.Add(new DatoFaltante("receipt.number", "Compras › Recepciones", "Inventory.Purchases.Confirm",
                $"La factura del proveedor {entrada.InvoiceNumber} no tiene una recepción confirmada: el recibo del bien (032) la exige."));

        var contexto = new ContextoDelCanonico { Configuracion = numeracion.Configuracion, Emisor = emisor };
        var cooperativa = Emisor(contexto, catalogo, fecha, faltan);
        if (faltan.Count > 0) return Result.Failure<EventoConstruido>(ErroresDeFacturacionElectronica.MissingData(faltan));

        var s = entrada.Supplier;
        var evento = new EventoRadianCanonico(
            1,
            entrada.Kind,
            tipo!.Codigo,
            tipo.CodigoDeEvento!,
            numeracion.Configuracion.Environment,
            new NumeroCanonico(numeracion.Prefijo, numeracion.Consecutivo, numeracion.Prefijo + numeracion.Consecutivo.ToString(CultureInfo.InvariantCulture)),
            emitidoEn,
            cooperativa,
            new ParteCanonica
            {
                Role = RolProveedor,
                TaxId = s.TaxId.Trim(),
                CheckDigit = string.IsNullOrWhiteSpace(s.CheckDigit) ? null : s.CheckDigit.Trim(),
                IdTypeCode = s.IdTypeCode,
                PersonTypeCode = s.PersonTypeCode,
                Name = s.Name.Trim(),
            },
            new DocumentoCorregidoCanonico(entrada.InvoiceNumber, entrada.InvoiceCufe!.Trim().ToLowerInvariant(), entrada.InvoiceIssueDate, null),
            entrada.Kind == ElectronicDocumentKind.RadianEvent032 && entrada.ReceiptDate is { } recibida
                ? new RecepcionCanonica(entrada.ReceiptNumber!, recibida)
                : null,
            entrada.IssuedBy,
            new OrigenCanonico(entrada.SourceModule, entrada.DocumentPublicId, entrada.DocumentClass, entrada.DocumentNumber),
            []);

        var json = SerializadorCanonico.Serializar(evento);
        var huella = SerializadorCanonico.Sha256(SerializadorCanonico.Serializar(new { evento.Kind, evento.EventCode, evento.ReferencedInvoice }));
        return Result.Success(new EventoConstruido(evento, json, SerializadorCanonico.Sha256(json), huella));
    }

    /// <summary>¿Es una nota (sin resolución, con consecutivo propio y documento corregido)?</summary>
    public static bool EsNota(ElectronicDocumentKind tipo) => tipo is ElectronicDocumentKind.CreditNote or ElectronicDocumentKind.DebitNote
        or ElectronicDocumentKind.PosAdjustmentNote or ElectronicDocumentKind.SupportDocumentAdjustmentNote;

    /// <summary>El valor de <c>paymentForm</c> de contado y de crédito (contracts/dian.md §4.2).</summary>
    public const string FormaContado = "Cash";

    public const string FormaCredito = "Credit";

    /// <summary>El valor de <c>role</c> del comprador y del proveedor.</summary>
    public const string RolComprador = "Buyer";

    public const string RolProveedor = "Supplier";

    /// <summary>La generación del documento soporte por defecto (<c>DocumentoSoporte.Generacion = PorOperacion</c>).</summary>
    public const string GeneracionPorOperacion = "PerOperation";

    // ------------------------------------------------------------------------------------------ partes --

    private static ParteCanonica Emisor(ContextoDelCanonico contexto, CatalogoDian catalogo, DateOnly fecha, List<DatoFaltante> faltan)
    {
        var s = contexto.Configuracion;
        void Exigir(string valor, string campo, string que)
        {
            if (string.IsNullOrWhiteSpace(valor))
                faltan.Add(new DatoFaltante($"issuer.{campo}", Configuracion, PermisoConfiguracion,
                    $"Falta {que} del emisor: complételo en {Configuracion} ({PermisoConfiguracion})."));
        }
        Exigir(s.IssuerTaxId, "taxId", "el NIT");
        Exigir(s.IssuerCheckDigit, "checkDigit", "el dígito de verificación");
        Exigir(s.IssuerBusinessName, "name", "la razón social");
        Exigir(s.IssuerAddress, "address.line", "la dirección");
        Exigir(s.IssuerMunicipalityDaneCode, "address.cityDaneCode", "el municipio (DIVIPOLA)");
        Exigir(s.IssuerEmail, "email", "el correo");

        var e = contexto.Emisor;
        return new ParteCanonica
        {
            TaxId = s.IssuerTaxId.Trim(),
            CheckDigit = s.IssuerCheckDigit.Trim(),
            IdTypeCode = catalogo.IdentificacionDeJuridica(fecha) ?? string.Empty,
            PersonTypeCode = catalogo.TipoDePersonaDe(juridica: true, fecha) ?? string.Empty,
            Name = s.IssuerBusinessName.Trim(),
            Responsibilities = catalogo.ResponsabilidadesDe(new MarcasTributarias(e.GranContribuyente, e.Autorretenedor, e.AgenteDeRetencionIva, false), fecha),
            TaxSchemeCode = catalogo.EsquemaTributarioDe(e.ResponsableDeIva, fecha) ?? string.Empty,
            Address = new DireccionCanonica(s.IssuerAddress.Trim(), s.IssuerMunicipalityDaneCode.Trim(), catalogo.PaisPorDefecto(fecha)),
            Email = s.IssuerEmail.Trim(),
        };
    }

    private static ParteCanonica Contraparte(EntradaDeDocumentoElectronico entrada, CatalogoDian catalogo, DateOnly fecha, List<DatoFaltante> faltan)
    {
        var rol = entrada.Kind is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote
            ? RolProveedor
            : RolComprador;
        var consumidorFinal = catalogo.ConsumidorFinal(fecha);
        var foto = entrada.Contrapartes.MaxBy(f => f.Version);

        if (foto is null)
        {
            if (consumidorFinal is null || rol == RolProveedor)
            {
                faltan.Add(new DatoFaltante("counterparty", Personas, PermisoPersonas,
                    "El documento no tiene contraparte: elija la persona (o el consumidor final) antes de confirmar."));
                return ParteCanonica.Vacia;
            }
            return new ParteCanonica
            {
                Role = rol,
                IsFinalConsumer = true,
                TaxId = consumidorFinal.Numero,
                IdTypeCode = consumidorFinal.TipoDeIdentificacion,
                PersonTypeCode = catalogo.TipoDePersonaDe(juridica: false, fecha) ?? string.Empty,
                Name = consumidorFinal.Nombre,
                Responsibilities = catalogo.ResponsabilidadesDe(new MarcasTributarias(false, false, false, false), fecha),
                TaxSchemeCode = catalogo.EsquemaTributarioDe(false, fecha) ?? string.Empty,
                Address = new DireccionCanonica(null, null, catalogo.PaisPorDefecto(fecha)),
            };
        }

        var donde = $"{Personas} (identificación {foto.TaxId})";
        if (string.IsNullOrWhiteSpace(foto.TaxId))
            faltan.Add(new DatoFaltante("counterparty.taxId", Personas, PermisoPersonas, "La contraparte no tiene número de identificación."));
        if (catalogo.TipoDeIdentificacion(foto.IdTypeCode, fecha) is null)
            faltan.Add(new DatoFaltante("counterparty.idTypeCode", donde, PermisoPersonas,
                $"El tipo de identificación de {foto.LegalName} no tiene equivalente DIAN: corríjalo en {Personas} ({PermisoPersonas})."));
        if (string.IsNullOrWhiteSpace(foto.OrganizationType))
            faltan.Add(new DatoFaltante("counterparty.personTypeCode", donde, PermisoPersonas,
                $"No se sabe si {foto.LegalName} es persona natural o jurídica: complételo en {Personas} ({PermisoPersonas})."));

        var responsabilidades = string.IsNullOrWhiteSpace(foto.Responsibilities)
            ? catalogo.ResponsabilidadesDe(new MarcasTributarias(foto.IsLargeContributor, foto.IsSelfWithholder, foto.IsVatWithholdingAgent, foto.IsSimpleTaxRegime), fecha)
            : foto.Responsibilities.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new ParteCanonica
        {
            Role = rol,
            IsFinalConsumer = consumidorFinal is not null
                && string.Equals(foto.TaxId.Trim(), consumidorFinal.Numero, StringComparison.Ordinal)
                && string.Equals(foto.IdTypeCode, consumidorFinal.TipoDeIdentificacion, StringComparison.Ordinal),
            TaxId = foto.TaxId.Trim(),
            CheckDigit = string.IsNullOrWhiteSpace(foto.CheckDigit) ? null : foto.CheckDigit.Trim(),
            IdTypeCode = foto.IdTypeCode,
            PersonTypeCode = foto.OrganizationType,
            Name = foto.LegalName.Trim(),
            Responsibilities = responsabilidades,
            TaxSchemeCode = string.IsNullOrWhiteSpace(foto.TaxSchemeCode)
                ? catalogo.EsquemaTributarioDe(foto.IsVatResponsible, fecha) ?? string.Empty
                : foto.TaxSchemeCode,
            Address = new DireccionCanonica(foto.Address?.Trim(), foto.MunicipalityDaneCode, foto.CountryCode ?? catalogo.PaisPorDefecto(fecha)),
            Ciiu = foto.CiiuCode,
            Phone = foto.Phone,
            ReceptionEmail = foto.Email,
            PartySnapshotVersion = foto.Version,
        };
    }

    // ------------------------------------------------------------------------------------------ líneas --

    private static LineaCanonica Linea(LineaDeEntrada linea, EntradaDeDocumentoElectronico entrada, CatalogoDian catalogo, DateOnly fecha, List<DatoFaltante> faltan)
    {
        var n = linea.LineNumber.ToString(CultureInfo.InvariantCulture);
        if (!catalogo.EsUnidadValida(linea.DianUnitCode, fecha))
        {
            faltan.Add(new DatoFaltante($"lines[{n}].unitCode", $"{Unidades} (unidad {linea.UnitCode})", PermisoUnidades,
                $"La unidad {linea.UnitCode} no tiene código DIAN: complételo en {Unidades} ({PermisoUnidades})."));
        }

        var impuestos = entrada.Impuestos.Where(i => !i.EsRetencion && i.LineNumber == linea.LineNumber).ToList();
        foreach (var i in impuestos) ValidarTributo(i, catalogo, fecha, faltan);

        var descuentos = linea.DiscountAmount == 0m
            ? []
            : new List<DescuentoCanonico>
            {
                new(null, linea.GrossAmount == 0m ? null : decimal.Round(linea.DiscountAmount / linea.GrossAmount, 6, MidpointRounding.AwayFromZero),
                    linea.GrossAmount, linea.DiscountAmount),
            };

        return new LineaCanonica
        {
            LineNumber = linea.LineNumber,
            ProductCode = linea.ProductCode,
            Description = linea.Description,
            Quantity = linea.Quantity,
            UnitCode = linea.DianUnitCode?.Trim().ToUpperInvariant() ?? string.Empty,
            UnitPrice = linea.UnitPrice,
            LineExtension = linea.GrossAmount,
            Allowances = descuentos,
            Taxes = impuestos
                .GroupBy(i => (Codigo: i.DianTaxCode ?? string.Empty, i.Rate, i.AmountPerUnit))
                .OrderBy(g => g.Key.Codigo, StringComparer.Ordinal).ThenBy(g => g.Key.Rate).ThenBy(g => g.Key.AmountPerUnit)
                .Select(g => new ImpuestoCanonico(g.Key.Codigo, g.Key.Rate, g.Key.AmountPerUnit,
                    g.Any(x => x.TaxableUnits is not null) ? g.Sum(x => x.TaxableUnits ?? 0m) : null,
                    g.Sum(x => x.Base), g.Sum(x => x.Amount)))
                .ToList(),
        };
    }

    private static void ValidarTributo(ImpuestoDeEntrada impuesto, CatalogoDian catalogo, DateOnly fecha, List<DatoFaltante> faltan)
    {
        if (catalogo.EsTributoValido(impuesto.DianTaxCode, fecha)) return;
        var campo = impuesto.EsRetencion
            ? "withholdings.dianTaxCode"
            : impuesto.LineNumber is { } n ? $"lines[{n.ToString(CultureInfo.InvariantCulture)}].taxes.dianTaxCode" : "taxes.dianTaxCode";
        faltan.Add(new DatoFaltante(campo, $"{Impuestos} (tarifa {impuesto.TaxRateCode})", PermisoImpuestos,
            $"El impuesto de la tarifa {impuesto.TaxRateCode} no tiene tributo DIAN válido: complételo en {Impuestos} ({PermisoImpuestos})."));
    }

    // ------------------------------------------------------------------------------------------ notas y POS --

    private static DocumentoCorregidoCanonico? Corregido(EntradaDeDocumentoElectronico entrada, ContextoDelCanonico contexto, CatalogoDian catalogo,
        DateOnly fecha, List<DatoFaltante> faltan)
    {
        var correccion = entrada.Correccion;
        var base_ = contexto.Corregido
            ?? (correccion is null ? null : new DocumentoCorregidoCanonico(correccion.CorrectedDocumentNumber, null, correccion.CorrectedIssueDate, null));
        if (base_ is null)
        {
            faltan.Add(new DatoFaltante("references.corrected", "El documento (documento que corrige la nota)", null,
                "La nota no dice qué documento corrige."));
            return null;
        }

        var concepto = correccion?.CorrectionConceptCode;
        if (ClaseDeNota(entrada.Kind) is not { } clase || catalogo.ConceptoDeCorreccion(clase, concepto, fecha) is null)
        {
            faltan.Add(new DatoFaltante("references.corrected.correctionConceptCode", "El documento (concepto de corrección)", null,
                "La nota no tiene un concepto de corrección DIAN válido: elíjalo en el documento."));
        }
        return base_ with { CorrectionConceptCode = concepto };
    }

    private static ClaseDeNotaDian? ClaseDeNota(ElectronicDocumentKind tipo) => tipo switch
    {
        ElectronicDocumentKind.CreditNote => ClaseDeNotaDian.NotaCredito,
        ElectronicDocumentKind.DebitNote => ClaseDeNotaDian.NotaDebito,
        ElectronicDocumentKind.PosAdjustmentNote => ClaseDeNotaDian.NotaDeAjustePos,
        ElectronicDocumentKind.SupportDocumentAdjustmentNote => ClaseDeNotaDian.NotaDeAjusteDelDocumentoSoporte,
        _ => null,
    };

    private static BloquePosCanonico? Pos(EntradaDeDocumentoElectronico entrada, ContextoDelCanonico contexto, CatalogoDian catalogo,
        DateOnly fecha, List<DatoFaltante> faltan)
    {
        if (entrada.Kind is not (ElectronicDocumentKind.PosEquivalent or ElectronicDocumentKind.PosAdjustmentNote)) return null;
        var pos = entrada.Pos;
        if (entrada.Kind == ElectronicDocumentKind.PosEquivalent)
        {
            if (string.IsNullOrWhiteSpace(pos?.CashRegisterPlate))
                faltan.Add(new DatoFaltante("pos.cashRegisterPlate", Cajas, PermisoCajas,
                    $"La caja no tiene placa o serial, que pide el documento equivalente POS: complétela en {Cajas} ({PermisoCajas})."));
            if (!catalogo.EsTipoDeCajaValido(pos?.CashRegisterTypeCode, fecha))
                faltan.Add(new DatoFaltante("pos.cashRegisterTypeCode", Cajas, PermisoCajas,
                    $"La caja no tiene un tipo de caja DIAN válido: complételo en {Cajas} ({PermisoCajas})."));
        }
        return new BloquePosCanonico(pos?.CashRegisterPlate, pos?.Location, pos?.CashierName, pos?.CashRegisterTypeCode, contexto.SoftwarePos);
    }

    private static string Fecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
