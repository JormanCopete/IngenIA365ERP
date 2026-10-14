using System.Globalization;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Los documentos al deudor de Cartera en PDF (feature 013, US13, T0789; adaptadores-y-archivos.md §8.10; molde <c>SettlementDocumentReport</c>): una
/// sola plantilla en carta con el <b>encabezado común</b> —membrete con el nombre y el NIT de la cooperativa, logotipo si lo hay, título, número, fecha
/// de emisión en hora de Colombia, el titular con la copia de su identificación y las leyendas (ambiente de pruebas, firmeza, alcance del general,
/// codeudas, ventas fuera de Cartera, proyección)— y el <b>cuerpo por clase</b>: el plan de pagos cuota a cuota con capital, interés, seguros, otros
/// cargos, cuota y saldo, y las tasas efectiva y nominal; el paz y salvo con los créditos certificados en cero; el certificado de saldo por concepto con
/// lo que cuesta cancelar. Al pie, la firma (imagen si la hay, nombre y cargo), el código de verificación en cuatro grupos y el QR a
/// <c>{urlDeLaAplicacion}/cartera/verificar?codigo=…</c>.
/// <para>No calcula nada ni lee la base: todo sale de <see cref="DocumentoDeCarteraParaImprimir"/>. El mismo modelo da los mismos bytes —las fechas de
/// los metadatos son la de emisión, no la del reloj—, aunque reimprimir nunca vuelve a pintar: descarga el PDF guardado al emitir.</para>
/// <para><see cref="TextosImpresos"/> lista, en orden, los textos que la plantilla pinta (el PDF los lleva como glifos, que una prueba no puede leer):
/// las pruebas comprueban sobre esa lista lo que el documento dice y sobre los bytes que el PDF es válido y estable. (nuevo)</para>
/// </summary>
public static class DocumentosDeCarteraReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El PDF del documento.</summary>
    public static byte[] Generate(DocumentoDeCarteraParaImprimir documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var e = documento.Encabezado;
        var qr = Qr(ContenidoDelQr(documento));
        return Document.Create(container => Pagina(container, documento, qr))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"{e.Titulo} {e.Numero}",
                Author = e.Cooperativa,
                Subject = e.Titulo,
                Creator = "IngenIA365ERP",
                Producer = "IngenIA365ERP",
                CreationDate = e.EmitidoEn,
                ModifiedDate = e.EmitidoEn,
            })
            .GeneratePdf();
    }

    /// <summary>Lo que lleva el QR: la URL de verificación del documento.</summary>
    public static string ContenidoDelQr(DocumentoDeCarteraParaImprimir documento) => documento.Encabezado.UrlDeVerificacion;

    /// <summary>Los textos que la plantilla pinta, en el orden en que aparecen (encabezado, leyendas, cuerpo y pie).</summary>
    public static IReadOnlyList<string> TextosImpresos(DocumentoDeCarteraParaImprimir documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var e = documento.Encabezado;
        var textos = new List<string> { e.Cooperativa };
        if (Nit(e) is { } nit) textos.Add(nit);
        textos.Add(e.Titulo.ToUpper(Co));
        textos.Add(NumeroYFecha(e));
        textos.Add(Titular(e));
        textos.AddRange(e.Leyendas);
        textos.AddRange(documento.Cuerpo switch
        {
            CuerpoDePlanDePagos plan => TextosDelPlan(plan),
            CuerpoDePazYSalvo paz => TextosDelPazYSalvo(paz),
            CuerpoDeCertificadoDeSaldo certificado => TextosDelCertificado(certificado),
            _ => [],
        });
        textos.Add(e.Firmante);
        textos.Add(e.CargoDelFirmante);
        textos.Add(LineaDelCodigo(e));
        textos.Add(LineaDeVerificacion(e));
        return textos;
    }

    // ------------------------------------------------------------------------------------------------------------ página --

    private static void Pagina(IDocumentContainer container, DocumentoDeCarteraParaImprimir d, byte[] qr)
    {
        var e = d.Encabezado;
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9));

            if (e.AmbienteDePruebas)
                page.Background().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text(EncabezadoDeDocumentoDeCartera.MarcaDePruebas).FontSize(30).Bold().FontColor(Colors.Grey.Lighten2);

            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    if (e.Logotipo is { Length: > 0 } logotipo) row.ConstantItem(90).Height(54).Image(logotipo).FitArea();
                    row.RelativeItem().PaddingLeft(e.Logotipo is { Length: > 0 } ? 8 : 0).Column(membrete =>
                    {
                        membrete.Item().Text(e.Cooperativa).Bold().FontSize(13);
                        if (Nit(e) is { } nit) membrete.Item().Text(nit);
                    });
                    row.ConstantItem(200).AlignRight().Column(doc =>
                    {
                        doc.Item().AlignRight().Text(e.Titulo.ToUpper(Co)).SemiBold().FontSize(12);
                        doc.Item().AlignRight().Text(NumeroYFecha(e));
                    });
                });
                col.Item().PaddingTop(6).Text(Titular(e));
                foreach (var leyenda in e.Leyendas)
                {
                    var texto = col.Item().PaddingTop(3).Text(leyenda).SemiBold();
                    if (leyenda == EncabezadoDeDocumentoDeCartera.MarcaDePruebas) texto.FontColor(Colors.Red.Darken2).Bold();
                }
                col.Item().PaddingTop(4).PaddingBottom(4).LineHorizontal(1);
            });

            page.Content().PaddingVertical(4).Column(col =>
            {
                switch (d.Cuerpo)
                {
                    case CuerpoDePlanDePagos plan: Plan(col, plan); break;
                    case CuerpoDePazYSalvo paz: PazYSalvo(col, paz); break;
                    case CuerpoDeCertificadoDeSaldo certificado: Certificado(col, certificado); break;
                }

                col.Item().PaddingTop(24).Row(row =>
                {
                    row.RelativeItem().Column(firma =>
                    {
                        if (e.ImagenDeFirma is { Length: > 0 } imagen) firma.Item().Width(150).Height(50).Image(imagen).FitArea();
                        else firma.Item().Height(30);
                        firma.Item().Width(200).LineHorizontal(0.5f);
                        firma.Item().Text(e.Firmante).SemiBold();
                        firma.Item().Text(e.CargoDelFirmante);
                    });
                    row.ConstantItem(96).Image(qr);
                });
                col.Item().PaddingTop(6).Text(LineaDelCodigo(e)).SemiBold();
                col.Item().Text(LineaDeVerificacion(e)).FontSize(7);
            });

            page.Footer().AlignCenter().Text(x =>
            {
                x.Span($"{e.Titulo} {e.Numero} · página ").FontSize(7);
                x.CurrentPageNumber().FontSize(7);
                x.Span(" de ").FontSize(7);
                x.TotalPages().FontSize(7);
            });
        });
    }

    // ----------------------------------------------------------------------------------------------------------- cuerpos --

    private static void Plan(ColumnDescriptor col, CuerpoDePlanDePagos p)
    {
        foreach (var texto in TextosDelPlan(p).Take(3)) col.Item().Text(texto);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(3);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[] { "N.º", "Vence", "Capital", "Interés", "Seguros", "Otros", "Cuota", "Saldo" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var c in p.Filas)
            {
                table.Cell().Element(Celda).Text(c.Numero.ToString(Co));
                table.Cell().Element(Celda).Text(Fecha(c.Vencimiento));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Capital));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Interes));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Seguros));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.OtrosCargos));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Cuota));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.SaldoDeCapital));
            }
        });
        col.Item().PaddingTop(6).Text(TotalDelPlan(p)).SemiBold();
    }

    private static void PazYSalvo(ColumnDescriptor col, CuerpoDePazYSalvo p)
    {
        var textos = TextosDelPazYSalvo(p);
        col.Item().Text(textos[0]);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[] { "Crédito", "Línea", "Calidad", "Saldo" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var c in p.Creditos)
            {
                table.Cell().Element(Celda).Text(c.Numero);
                table.Cell().Element(Celda).Text(c.Linea);
                table.Cell().Element(Celda).Text(c.Papel);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Saldo));
            }
        });
        if (p.Creditos.Count == 0) col.Item().PaddingTop(4).Text("La persona no tiene obligaciones de crédito con la cooperativa.").Italic();
        if (p.VentasFueraDeCartera.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Ventas a crédito que no hacen parte de Cartera").SemiBold();
            foreach (var v in p.VentasFueraDeCartera)
                col.Item().Text($"Factura {v.Factura} del {Fecha(v.Fecha)}: {Pesos(v.Valor)}");
        }
    }

    private static void Certificado(ColumnDescriptor col, CuerpoDeCertificadoDeSaldo c)
    {
        var textos = TextosDelCertificado(c);
        col.Item().Text(textos[0]);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.RelativeColumn(4);
                d.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(EncabezadoDeTabla).Text("Concepto");
                h.Cell().Element(EncabezadoDeTabla).AlignRight().Text("Valor");
            });
            foreach (var concepto in c.PorConcepto)
            {
                table.Cell().Element(Celda).Text(Concepto(concepto.Concepto));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(concepto.Valor));
            }
            table.Cell().Element(Celda).Text("Para cancelar").SemiBold();
            table.Cell().Element(Celda).AlignRight().Text(Pesos(c.ParaCancelar)).SemiBold();
        });
        col.Item().PaddingTop(6).Text(textos[1]);
        col.Item().Text(textos[2]).FontSize(7).FontColor(Colors.Grey.Darken1);
    }

    // ------------------------------------------------------------------------------------------------------------ textos --

    private static IReadOnlyList<string> TextosDelPlan(CuerpoDePlanDePagos p) =>
    [
        $"Crédito {p.NumeroDelCredito} · {p.Linea}",
        $"Plan versión {p.VersionDelPlan.ToString(Co)} vigente desde el {Fecha(p.VigenteDesde)} · capital {Pesos(p.CapitalBase)} · " +
        $"{p.Cuotas.ToString(Co)} cuotas {Periodicidad(p.Periodicidad)}",
        $"Tasa efectiva anual: {(p.TasaEfectivaAnual is { } ea ? Porcentaje(ea) + " EA" : "variable")} · " +
        $"nominal anual: {(p.TasaNominalAnual is { } na ? Porcentaje(na) : "variable")}",
        TotalDelPlan(p),
    ];

    private static string TotalDelPlan(CuerpoDePlanDePagos p) => $"Total de las cuotas: {Pesos(p.TotalCuotas)}";

    private static IReadOnlyList<string> TextosDelPazYSalvo(CuerpoDePazYSalvo p)
    {
        var textos = new List<string>
        {
            p.General
                ? $"La persona se encuentra a paz y salvo por todas sus obligaciones de crédito con la cooperativa al {Fecha(p.Corte)}."
                : $"La obligación se encuentra cancelada y a paz y salvo al {Fecha(p.Corte)}.",
        };
        textos.AddRange(p.Creditos.Select(c => $"{c.Numero} {c.Linea} {c.Papel} {Pesos(c.Saldo)}"));
        textos.AddRange(p.VentasFueraDeCartera.Select(v => $"Factura {v.Factura} del {Fecha(v.Fecha)}: {Pesos(v.Valor)}"));
        return textos;
    }

    private static IReadOnlyList<string> TextosDelCertificado(CuerpoDeCertificadoDeSaldo c)
    {
        var textos = new List<string>
        {
            $"Crédito {c.NumeroDelCredito} · {c.Linea} · saldo al {Fecha(c.Fecha)}" + (c.Proyectada ? " (proyectado sin pagos)" : string.Empty),
            $"Para cancelar: {Pesos(c.ParaCancelar)} · días de mora: {c.DiasDeMora.ToString(Co)}" +
            (c.ValidaHasta is { } hasta ? $" · válido hasta el {Fecha(hasta)}" : string.Empty),
            $"Liquidación {c.HuellaDeLaLiquidacion} · reglas {c.VersionDeReglas}",
        };
        textos.AddRange(c.PorConcepto.Select(x => $"{Concepto(x.Concepto)} {Pesos(x.Valor)}"));
        return textos;
    }

    private static string? Nit(EncabezadoDeDocumentoDeCartera e) => string.IsNullOrWhiteSpace(e.NitDeLaCooperativa) ? null : $"NIT {e.NitDeLaCooperativa}";

    private static string NumeroYFecha(EncabezadoDeDocumentoDeCartera e) =>
        $"N.º {e.Numero} · {e.EmitidoEn.ToString("dd/MM/yyyy HH:mm", Co)}";

    private static string Titular(EncabezadoDeDocumentoDeCartera e) =>
        $"Titular: {e.NombreDelTitular} · {e.TipoDeDocumentoDelTitular} {e.DocumentoDelTitular}";

    private static string LineaDelCodigo(EncabezadoDeDocumentoDeCartera e) => $"Código de verificación: {e.CodigoDeVerificacion}";

    private static string LineaDeVerificacion(EncabezadoDeDocumentoDeCartera e) =>
        $"Un usuario de la cooperativa verifica el documento en {e.UrlDeVerificacion}";

    private static string Periodicidad(string periodicidad) => Enum.TryParse<RepaymentPeriodicity>(periodicidad, out var p)
        ? p switch
        {
            RepaymentPeriodicity.SinglePayment => "de pago único",
            RepaymentPeriodicity.Weekly => "semanales",
            RepaymentPeriodicity.TenDay => "decadales",
            RepaymentPeriodicity.Biweekly => "quincenales",
            RepaymentPeriodicity.Monthly => "mensuales",
            RepaymentPeriodicity.Bimonthly => "bimestrales",
            RepaymentPeriodicity.Quarterly => "trimestrales",
            RepaymentPeriodicity.SemiAnnual => "semestrales",
            RepaymentPeriodicity.Annual => "anuales",
            _ => periodicidad,
        }
        : periodicidad;

    private static string Concepto(string concepto) => Enum.TryParse<LoanConcept>(concepto, out var c)
        ? c switch
        {
            LoanConcept.Principal => "Capital",
            LoanConcept.CurrentInterest => "Interés corriente",
            LoanConcept.DefaultInterest => "Interés de mora",
            LoanConcept.CurrentInterestInSuspense => "Interés corriente en suspenso",
            LoanConcept.DefaultInterestInSuspense => "Interés de mora en suspenso",
            LoanConcept.Insurance => "Seguros",
            LoanConcept.CollectionFee => "Gastos de cobranza",
            LoanConcept.OtherCharge => "Otros cargos",
            LoanConcept.Advance => "Anticipo",
            LoanConcept.CreditBalance => "Saldo a favor",
            _ => concepto,
        }
        : concepto;

    private static IContainer EncabezadoDeTabla(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(3).DefaultTextStyle(x => x.SemiBold().FontSize(8));

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).DefaultTextStyle(x => x.FontSize(8));

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Co);

    private static string Pesos(decimal valor) => "$ " + valor.ToString("#,0.00", Co);

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Co) + " %";

    /// <summary>El PNG del QR con la URL de verificación.</summary>
    private static byte[] Qr(string contenido)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }
}

/// <summary><see cref="IDocumentoDeCarteraRenderer"/> con QuestPDF y QRCoder (T0789). (nuevo)</summary>
public sealed class DocumentoDeCarteraPdfRenderer : IDocumentoDeCarteraRenderer
{
    public byte[] Render(DocumentoDeCarteraParaImprimir documento) => DocumentosDeCarteraReport.Generate(documento);
}
