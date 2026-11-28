using System.Globalization;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.ElectronicInvoicing.Channels.Conecta;

/// <summary>Lo que sale del traductor: la ruta y el cuerpo, o los mensajes de por qué no se puede enviar (<c>InvalidData</c>, sin llamar).</summary>
public sealed record PeticionConecta(string Ruta, object Cuerpo, IReadOnlyList<MensajeDelCanal> Avisos);

/// <summary>
/// Del canónico del ERP (v2) al esquema de entrada de IngenIA Conecta (feature 028, contracts/canal-conecta.md §4). <b>Puro</b>: sin red ni
/// base; lo prueban los casos dorados. Ningún código DIAN se inventa aquí: todos vienen del canónico, que los resolvió con
/// <see cref="CatalogoDian"/>. Lo que Conecta no puede expresar se dice antes de enviar (D3) y lo que se aproxima queda en un aviso (D2). (nuevo)
/// </summary>
public static class TraductorAConecta
{
    public const string RutaFactura = "/v1/invoices";
    public const string RutaNotaCredito = "/v1/credit-notes";
    public const string RutaNotaDebito = "/v1/debit-notes";

    public const string ReglaTipo = "CONECTA-TIPO";
    public const string ReglaTributoPorUnidad = "CONECTA-TRIBUTO-POR-UNIDAD";
    public const string ReglaReferencia = "CONECTA-REFERENCIA";
    public const string ReglaMedios = "CONECTA-MEDIOS";

    /// <summary>Forma de pago (tabla 13.3.4.1 del anexo 1.9, en el catálogo del ERP): 1 contado, 2 crédito.</summary>
    private const string FormaContado = "1";
    private const string FormaCredito = "2";

    /// <summary>Medio «instrumento no definido» (tabla 13.3.4.2, en el catálogo del ERP), para un documento sin pagos (p. ej. una nota).</summary>
    private const string MedioNoDefinido = "1";

    /// <summary>Régimen del adquirente en el esquema de Conecta: 48 responsable de IVA, 49 no responsable (por cotejar con el contador).</summary>
    private const string RegimenResponsable = "48";
    private const string RegimenNoResponsable = "49";
    private const string TributoIva = "01";

    private const int LargoMinimoDeDiscrepancia = 20;
    private const int LargoMaximoDeDiscrepancia = 5000;

    /// <summary>Traduce, o devuelve por qué no se puede.</summary>
    /// <param name="documento">El canónico sellado.</param>
    /// <param name="tenantId">El tenant de la cooperativa en Conecta.</param>
    /// <param name="enviarCorreo">Si el canal entrega al comprador (D9); si no, el correo no viaja.</param>
    /// <param name="claveDelErp">La clave de idempotencia del ERP (va en <c>metadata</c>).</param>
    public static (PeticionConecta? Peticion, IReadOnlyList<MensajeDelCanal> Errores) Traducir(DocumentoElectronicoCanonico documento, Guid tenantId,
        bool enviarCorreo, string? claveDelErp)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var errores = new List<MensajeDelCanal>();
        var avisos = new List<MensajeDelCanal>();

        var ruta = documento.Kind switch
        {
            ElectronicDocumentKind.Invoice => RutaFactura,
            ElectronicDocumentKind.CreditNote => RutaNotaCredito,
            ElectronicDocumentKind.DebitNote => RutaNotaDebito,
            _ => null,
        };
        if (ruta is null)
        {
            errores.Add(Rechazo(ReglaTipo, $"CONECTA: el canal no emite documentos {documento.Kind}.",
                $"IngenIA Conecta todavía no recibe {documento.Kind} desde el ERP."));
            return (null, errores);
        }

        var lineas = Lineas(documento, errores);
        var pago = Pago(documento, avisos);
        var adquirente = Adquirente(documento, enviarCorreo);
        var notas = Notas(documento);
        var metadata = Metadata(documento, claveDelErp);
        var fecha = DateOnly.FromDateTime(documento.IssuedAt.DateTime);

        object? cuerpo = null;
        if (documento.Kind == ElectronicDocumentKind.Invoice)
        {
            cuerpo = new FacturaConecta(tenantId, documento.Number.Prefix, documento.Number.Consecutive, fecha, documento.OperationTypeCode,
                documento.DianDocumentTypeCode, pago, adquirente, lineas, notas, null, documento.References.Order, metadata);
        }
        else
        {
            var (discrepancia, referida) = Referencia(documento, errores);
            if (discrepancia is not null)
                cuerpo = new NotaConecta(tenantId, documento.Number.Prefix, documento.Number.Consecutive, fecha, documento.OperationTypeCode,
                    discrepancia, referida, null, pago, adquirente, lineas, notas, null, documento.References.Order, metadata);
        }

        return errores.Count > 0 || cuerpo is null ? (null, errores) : (new PeticionConecta(ruta, cuerpo, avisos), []);
    }

    // ------------------------------------------------------------------------------------------ partes --

    private static List<LineaConecta> Lineas(DocumentoElectronicoCanonico documento, List<MensajeDelCanal> errores)
    {
        var lineas = new List<LineaConecta>();
        foreach (var l in documento.Lines.OrderBy(x => x.LineNumber))
        {
            var tributos = new List<TributoConecta>();
            foreach (var t in l.Taxes)
            {
                if (t.Rate is not { } tarifa)
                {
                    // D3: Conecta sólo recibe tarifa; un tributo por unidad (bolsas, INC por unidad) no se puede expresar.
                    errores.Add(Rechazo(ReglaTributoPorUnidad,
                        $"CONECTA: la línea {l.LineNumber} ({l.ProductCode}) tiene el tributo {t.DianTaxCode} por unidad, que Conecta no recibe.",
                        $"El producto {l.ProductCode} lleva un impuesto por unidad que IngenIA Conecta todavía no admite."));
                    continue;
                }
                tributos.Add(new TributoConecta(t.DianTaxCode, Porcentaje(tarifa), Exempt: tarifa == 0m));
            }
            var descuentos = l.Allowances
                .Select(a => new DescuentoConecta(false, string.IsNullOrWhiteSpace(a.ReasonCode) ? "Descuento" : a.ReasonCode!,
                    a.Percent is { } p ? Porcentaje(p) : null, a.Amount))
                .ToList();
            lineas.Add(new LineaConecta(l.ProductCode, l.Description, l.Quantity, l.UnitCode, l.UnitPrice,
                tributos.Count > 0 ? tributos : null, descuentos.Count > 0 ? descuentos : null));
        }
        return lineas;
    }

    /// <summary>D2: Conecta recibe un solo medio. Va el de mayor valor; si hay más, todos quedan en las notas y en un aviso.</summary>
    private static PagoConecta Pago(DocumentoElectronicoCanonico documento, List<MensajeDelCanal> avisos)
    {
        var forma = string.Equals(documento.PaymentForm, "Credit", StringComparison.OrdinalIgnoreCase) ? FormaCredito : FormaContado;
        var principal = documento.Payments.OrderByDescending(p => p.Amount).ThenBy(p => p.DianPaymentMeansCode, StringComparer.Ordinal).FirstOrDefault();
        if (documento.Payments.Count > 1)
            avisos.Add(new MensajeDelCanal(ReglaMedios, TipoDeMensajeDelCanal.Notificacion,
                $"CONECTA: el documento tiene {documento.Payments.Count} medios de pago; se informó el {principal!.DianPaymentMeansCode} (el de mayor valor) y los demás en las notas.",
                "Conecta recibe un solo medio de pago: se informó el de mayor valor y los demás quedaron en las notas."));
        return new PagoConecta(forma, principal?.DianPaymentMeansCode is { Length: > 0 } medio ? medio : MedioNoDefinido, documento.DueDate);
    }

    private static AdquirenteConecta Adquirente(DocumentoElectronicoCanonico documento, bool enviarCorreo)
    {
        var c = documento.Counterparty;
        var d = c.Address;
        // Sin municipio (consumidor final), el del emisor: Conecta exige la dirección (por cotejar con su validador).
        var municipio = d.CityDaneCode ?? documento.Issuer.Address.CityDaneCode ?? string.Empty;
        var direccion = new DireccionConecta(
            municipio,
            d.CityName ?? documento.Issuer.Address.CityName ?? string.Empty,
            d.DepartmentCode ?? NombresDePersona.Departamento(municipio) ?? string.Empty,
            d.DepartmentName ?? documento.Issuer.Address.DepartmentName ?? string.Empty,
            d.Line ?? string.Empty,
            null,
            string.IsNullOrWhiteSpace(d.CountryCode) ? "CO" : d.CountryCode!);
        return new AdquirenteConecta(
            c.IdTypeCode,
            c.TaxId,
            string.IsNullOrWhiteSpace(c.CheckDigit) ? null : c.CheckDigit,
            c.Name,
            null,
            c.Responsibilities.Count > 0 ? c.Responsibilities : null,
            string.Equals(c.TaxSchemeCode, TributoIva, StringComparison.Ordinal) ? RegimenResponsable : RegimenNoResponsable,
            enviarCorreo ? c.ReceptionEmail : null,
            direccion,
            string.IsNullOrWhiteSpace(c.PersonTypeCode) ? null : c.PersonTypeCode,
            c.Phone,
            c.GivenNames,
            c.FamilyNames,
            string.IsNullOrWhiteSpace(c.TaxSchemeCode) ? null : c.TaxSchemeCode);
    }

    private static List<string>? Notas(DocumentoElectronicoCanonico documento)
    {
        var notas = documento.Notes.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        if (documento.Payments.Count > 1)
            notas.Add("Medios de pago: " + string.Join("; ", documento.Payments.Select(p =>
                string.Create(CultureInfo.InvariantCulture, $"{p.DianPaymentMeansCode} {p.Amount:0.00}"))));
        return notas.Count > 0 ? notas : null;
    }

    private static Dictionary<string, string> Metadata(DocumentoElectronicoCanonico documento, string? claveDelErp)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["erpDocumentPublicId"] = documento.Source.DocumentPublicId.ToString("D"),
            ["erpNumber"] = documento.Number.Full,
            ["canonicalSha256"] = SerializadorCanonico.Sha256(SerializadorCanonico.Serializar(documento)),
        };
        if (!string.IsNullOrWhiteSpace(claveDelErp)) metadata["erpIdempotencyKey"] = claveDelErp!;
        // D3: las retenciones informativas del comprador no tienen dónde ir en la factura de Conecta.
        if (documento.Withholdings.Count > 0)
            metadata["withholdings"] = string.Join(";", documento.Withholdings.Select(w =>
                string.Create(CultureInfo.InvariantCulture, $"{w.DianTaxCode}:{w.Rate:0.######}:{w.Base:0.00}:{w.Amount:0.00}")));
        return metadata;
    }

    private static (DiscrepanciaConecta?, FacturaReferidaConecta?) Referencia(DocumentoElectronicoCanonico documento, List<MensajeDelCanal> errores)
    {
        var corregido = documento.References.Corrected;
        if (corregido is null || string.IsNullOrWhiteSpace(corregido.CorrectionConceptCode))
        {
            errores.Add(Rechazo(ReglaReferencia, "CONECTA: la nota no trae el documento que corrige ni su concepto.",
                "La nota no indica la factura que corrige o el concepto de corrección."));
            return (null, null);
        }
        if (string.IsNullOrWhiteSpace(corregido.UniqueCode))
        {
            errores.Add(Rechazo(ReglaReferencia, $"CONECTA: la factura {corregido.Number} que corrige la nota no tiene CUFE.",
                $"La factura {corregido.Number} todavía no está validada: la nota se envía cuando lo esté."));
            return (null, null);
        }

        var clase = documento.Kind == ElectronicDocumentKind.DebitNote ? ClaseDeNotaDian.NotaDebito : ClaseDeNotaDian.NotaCredito;
        var fecha = DateOnly.FromDateTime(documento.IssuedAt.DateTime);
        var concepto = CatalogoDian.Embebido.ConceptoDeCorreccion(clase, corregido.CorrectionConceptCode, fecha)?.Nombre ?? corregido.CorrectionConceptCode!;
        var motivo = documento.Notes.FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))?.Trim();
        var descripcion = string.IsNullOrWhiteSpace(motivo) ? concepto : $"{concepto}: {motivo}";
        if (descripcion.Length < LargoMinimoDeDiscrepancia) descripcion = $"{descripcion} (factura {corregido.Number})";
        if (descripcion.Length < LargoMinimoDeDiscrepancia) descripcion = descripcion.PadRight(LargoMinimoDeDiscrepancia, '.');
        if (descripcion.Length > LargoMaximoDeDiscrepancia) descripcion = descripcion[..LargoMaximoDeDiscrepancia];

        return (new DiscrepanciaConecta(corregido.CorrectionConceptCode!, descripcion),
            new FacturaReferidaConecta(corregido.Number, corregido.UniqueCode!, corregido.IssueDate));
    }

    /// <summary>El canónico lleva la tarifa en fracción (0,19); Conecta la recibe en porcentaje (19).</summary>
    public static decimal Porcentaje(decimal fraccion) => decimal.Round(fraccion * 100m, 4);

    private static MensajeDelCanal Rechazo(string regla, string texto, string traduccion) =>
        new(regla, TipoDeMensajeDelCanal.Rechazo, texto, traduccion);
}
