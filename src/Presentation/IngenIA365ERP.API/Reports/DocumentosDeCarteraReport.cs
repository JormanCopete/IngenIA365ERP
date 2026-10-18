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
            CuerpoDeExtracto extracto => TextosDelExtracto(extracto),
            CuerpoDeCertificadoTributario tributario => TextosDelCertificadoTributario(tributario),
            CuerpoDeConstanciaDeAportes constancia => TextosDeLaConstancia(constancia),
            CuerpoDePazYSalvoDeAportes paz => TextosDelPazYSalvoDeAportes(paz),
            CuerpoDeLiquidacionDeRetiro retiro => TextosDeLaLiquidacionDeRetiro(retiro),
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
                    case CuerpoDeExtracto extracto: Extracto(col, extracto); break;
                    case CuerpoDeCertificadoTributario tributario: CertificadoTributario(col, tributario); break;
                    case CuerpoDeConstanciaDeAportes constancia: Constancia(col, constancia); break;
                    case CuerpoDePazYSalvoDeAportes paz: PazYSalvoDeAportes(col, paz); break;
                    case CuerpoDeLiquidacionDeRetiro retiro: LiquidacionDeRetiro(col, retiro); break;
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

    // --------------------------------------------------------------------------------------------------------- K5 (T0973) --

    private static void Extracto(ColumnDescriptor col, CuerpoDeExtracto x)
    {
        var textos = TextosDelExtracto(x);
        col.Item().Text(textos[0]);
        col.Item().PaddingTop(6).Text("Saldo inicial").SemiBold();
        TablaDeConceptos(col, x.SaldoInicial, "Saldo inicial", x.TotalInicial);
        col.Item().PaddingTop(8).Text("Operaciones del período").SemiBold();
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(5);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[] { "Fecha valor", "Descripción", "Capital", "Intereses", "Otros", "Total" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var l in x.Lineas)
            {
                table.Cell().Element(Celda).Text(Fecha(l.Fecha));
                table.Cell().Element(Celda).Text(DescripcionDeLinea(l));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(l.Capital));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(l.Intereses));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(l.Otros));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(l.Total));
            }
        });
        if (x.Lineas.Count == 0) col.Item().PaddingTop(4).Text("El crédito no tuvo operaciones en el período.").Italic();
        col.Item().PaddingTop(8).Text("Saldo final").SemiBold();
        TablaDeConceptos(col, x.SaldoFinal, "Saldo final", x.TotalFinal);
    }

    private static string DescripcionDeLinea(LineaDeExtractoImpresa l) =>
        l.ValorInformativo is { } v ? $"{l.Descripcion} ({Pesos(v)}, sin efecto en el saldo)" : l.Descripcion;

    private static void TablaDeConceptos(ColumnDescriptor col, IReadOnlyList<ConceptoCertificado> conceptos, string total, decimal valor)
    {
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.RelativeColumn(4);
                d.RelativeColumn(2);
            });
            foreach (var concepto in conceptos)
            {
                table.Cell().Element(Celda).Text(Concepto(concepto.Concepto));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(concepto.Valor));
            }
            table.Cell().Element(Celda).Text(total).SemiBold();
            table.Cell().Element(Celda).AlignRight().Text(Pesos(valor)).SemiBold();
        });
    }

    private static void CertificadoTributario(ColumnDescriptor col, CuerpoDeCertificadoTributario c)
    {
        var textos = TextosDelCertificadoTributario(c);
        col.Item().Text(textos[0]);
        TablaTributaria(col, c.Creditos);
        if (c.Creditos.Count == 0) col.Item().PaddingTop(4).Text("La persona no tuvo otros créditos con la cooperativa en el año.").Italic();
        foreach (var renglon in RenglonesAparte(c.Año, c.Creditos)) col.Item().PaddingTop(2).Text(renglon);
        col.Item().PaddingTop(4).Text(TotalTributario(c)).SemiBold();
        col.Item().PaddingTop(10).Text(CuerpoDeCertificadoTributario.TituloDeVivienda).SemiBold();
        if (c.Vivienda.Count == 0)
        {
            col.Item().PaddingTop(2).Text(CuerpoDeCertificadoTributario.SinVivienda).Italic();
        }
        else
        {
            TablaTributaria(col, c.Vivienda);
            foreach (var renglon in RenglonesAparte(c.Año, c.Vivienda)) col.Item().PaddingTop(2).Text(renglon);
            col.Item().PaddingTop(4).Text(TotalDeVivienda(c)).SemiBold();
        }
    }

    /// <summary>
    /// Decisiones del dueño del 2026-10-18: los renglones aparte de cada crédito, debajo de su tabla —el anticipo recibido en el año y pendiente de
    /// aplicar al 31 de diciembre y la corrección monetaria UVR causada en el año—; sólo los que no son cero. No suman a los intereses pagados.
    /// </summary>
    private static IEnumerable<string> RenglonesAparte(short año, IReadOnlyList<CreditoCertificadoTributario> creditos)
    {
        var a = año.ToString(CultureInfo.InvariantCulture);
        foreach (var x in creditos)
        {
            if (x.AnticipoRecibido != 0m)
                yield return string.Format(CultureInfo.InvariantCulture, CuerpoDeCertificadoTributario.RenglonDeAnticipoRecibido, x.Numero, a, Pesos(x.AnticipoRecibido));
            if (x.CorreccionMonetaria != 0m)
                yield return string.Format(CultureInfo.InvariantCulture, CuerpoDeCertificadoTributario.RenglonDeCorreccionMonetaria, x.Numero, a,
                    Pesos(x.CorreccionMonetaria));
        }
    }

    private static void TablaTributaria(ColumnDescriptor col, IReadOnlyList<CreditoCertificadoTributario> creditos)
    {
        if (creditos.Count == 0) return;
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[]
                         {
                             "Crédito", "Línea", "Interés corriente pagado", "Interés de mora pagado", "Saldo de capital al 31/12",
                             "Saldo de intereses al 31/12", "Saldo total al 31/12",
                         })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var x in creditos)
            {
                table.Cell().Element(Celda).Text(x.Numero);
                table.Cell().Element(Celda).Text(x.Linea);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(x.InteresCorriente));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(x.InteresDeMora));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(x.SaldoDeCapital));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(x.SaldoDeIntereses));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(x.SaldoTotal));
            }
        });
    }

    private static IReadOnlyList<string> TextosDelExtracto(CuerpoDeExtracto x)
    {
        var textos = new List<string>
        {
            $"Crédito {x.NumeroDelCredito} · {x.Linea} · extracto del {Fecha(x.Desde)} al {Fecha(x.Hasta)}",
            $"Saldo inicial {Pesos(x.TotalInicial)}",
        };
        textos.AddRange(x.SaldoInicial.Select(c => $"{Concepto(c.Concepto)} {Pesos(c.Valor)}"));
        textos.AddRange(x.Lineas.Select(l => $"{Fecha(l.Fecha)} {DescripcionDeLinea(l)} {Pesos(l.Total)}"));
        textos.Add($"Saldo final {Pesos(x.TotalFinal)}");
        textos.AddRange(x.SaldoFinal.Select(c => $"{Concepto(c.Concepto)} {Pesos(c.Valor)}"));
        return textos;
    }

    private static IReadOnlyList<string> TextosDelCertificadoTributario(CuerpoDeCertificadoTributario c)
    {
        var año = c.Año.ToString(CultureInfo.InvariantCulture);
        var textos = new List<string> { $"Intereses pagados durante el año {año} y saldo de la deuda al 31/12/{año}, por crédito." };
        textos.AddRange(c.Creditos.Select(x => $"{x.Numero} {x.Linea} {Pesos(x.InteresesPagados)} {Pesos(x.SaldoTotal)}"));
        textos.AddRange(RenglonesAparte(c.Año, c.Creditos));
        textos.Add(TotalTributario(c));
        textos.Add(CuerpoDeCertificadoTributario.TituloDeVivienda);
        if (c.Vivienda.Count == 0)
        {
            textos.Add(CuerpoDeCertificadoTributario.SinVivienda);
        }
        else
        {
            textos.AddRange(c.Vivienda.Select(x => $"{x.Numero} {x.Linea} {Pesos(x.InteresesPagados)} {Pesos(x.SaldoTotal)}"));
            textos.AddRange(RenglonesAparte(c.Año, c.Vivienda));
            textos.Add(TotalDeVivienda(c));
        }
        return textos;
    }

    private static string TotalTributario(CuerpoDeCertificadoTributario c) => $"Total de intereses pagados: {Pesos(c.TotalInteresesPagados)}";

    private static string TotalDeVivienda(CuerpoDeCertificadoTributario c) =>
        $"Total de intereses pagados en créditos de vivienda: {Pesos(c.TotalInteresesDeVivienda)}";

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

    // ----------------------------------------------------------------------------------- aportes (feature 014, T087) --

    private static void Constancia(ColumnDescriptor col, CuerpoDeConstanciaDeAportes c)
    {
        var textos = TextosDeLaConstancia(c);
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
                foreach (var titulo in new[] { "Clase de aporte", "Saldo" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var x in c.PorClase)
            {
                table.Cell().Element(Celda).Text(x.Clase);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(x.Valor));
            }
            table.Cell().Element(Celda).Text("Total de aportes").SemiBold();
            table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Total)).SemiBold();
        });
        foreach (var t in textos.Skip(1).Take(c.AhorroPermanente is null ? 1 : 2)) col.Item().PaddingTop(4).Text(t);
        if (c.CuotasPendientes.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Cuotas pendientes al corte").SemiBold();
            foreach (var q in c.CuotasPendientes) col.Item().Text(Pendiente(q));
        }
        else col.Item().PaddingTop(4).Text("Sin cuotas de aportes pendientes al corte.").Italic();
    }

    private static IReadOnlyList<string> TextosDeLaConstancia(CuerpoDeConstanciaDeAportes c)
    {
        var textos = new List<string>
        {
            $"Cuenta de aportes {c.NumeroDeCuenta} · {c.Linea} · saldo al {Fecha(c.Corte)}"
            + (c.Vinculacion is { } v ? $" · asociado desde el {Fecha(v)}" : string.Empty),
            $"Mínimo no reducible: {Pesos(c.Minimo)}",
        };
        if (c.AhorroPermanente is { } p) textos.Add($"Ahorro permanente: {Pesos(p)}");
        textos.AddRange(c.PorClase.Select(x => $"{x.Clase} {Pesos(x.Valor)}"));
        textos.Add($"Total de aportes {Pesos(c.Total)}");
        textos.AddRange(c.CuotasPendientes.Select(Pendiente));
        return textos;
    }

    // Feature 014, US10 (T103; documentos.md §2.8): la liquidación del retiro del asociado.
    private static void LiquidacionDeRetiro(ColumnDescriptor col, CuerpoDeLiquidacionDeRetiro r)
    {
        foreach (var t in Encabezado(r)) col.Item().PaddingTop(2).Text(t);
        col.Item().PaddingTop(8).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.RelativeColumn(2);
                d.RelativeColumn(5);
                d.RelativeColumn(2);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[] { "Bloque", "Detalle", "Valor" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var l in r.Lineas)
            {
                table.Cell().Element(Celda).Text(l.Bloque);
                table.Cell().Element(Celda).Text(l.Detalle);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(l.Valor));
            }
        });
        if (r.Cruces.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Cruces con los créditos (prelación: vencidos primero, luego mayor tasa)").SemiBold();
            foreach (var c in r.Cruces) col.Item().Text(Cruce(c));
        }
        col.Item().PaddingTop(6).Text(r.Perdidas);
        col.Item().PaddingTop(4).Text(Devolucion(r)).SemiBold();
        foreach (var cdat in r.CdatQueSiguen) col.Item().Text($"El CDAT {cdat} sigue hasta su vencimiento.");
    }

    private static IReadOnlyList<string> Encabezado(CuerpoDeLiquidacionDeRetiro r) =>
    [
        $"Retiro {r.Numero} · causa: {r.Causa} · radicado el {Fecha(r.Radicado)}"
        + (r.FechaEfectiva is { } f ? $" · fecha del retiro {Fecha(f)}" : string.Empty),
        r.Aprobacion is { } a ? $"Aprobado por {a}" : "Sin aprobación registrada",
        $"Liquidación {r.Version} del {Fecha(r.FechaDeLiquidacion)}",
    ];

    private static string Cruce(CruceImpreso c) =>
        $"{c.Prelacion}. Crédito {c.Credito}: saldo antes {Pesos(c.SaldoAntes)}, aplicado {Pesos(c.Aplicado)}, saldo después {Pesos(c.SaldoDespues)}";

    private static string Devolucion(CuerpoDeLiquidacionDeRetiro r) => r.DevolucionPendientePorMinimo
        ? $"Devolución de {Pesos(r.Devolucion)}: {CuerpoDeLiquidacionDeRetiro.LeyendaPendiente}"
        : $"Devolución de {Pesos(r.Devolucion)} por {r.FormaDeDevolucion}"
          + (r.DevolverHasta is { } h ? $", a más tardar el {Fecha(h)}" : string.Empty) + ".";

    private static IReadOnlyList<string> TextosDeLaLiquidacionDeRetiro(CuerpoDeLiquidacionDeRetiro r)
    {
        var textos = new List<string>(Encabezado(r));
        textos.AddRange(r.Lineas.Select(l => $"{l.Bloque} {l.Detalle} {Pesos(l.Valor)}"));
        textos.AddRange(r.Cruces.Select(Cruce));
        textos.Add(r.Perdidas);
        textos.Add(Devolucion(r));
        return textos;
    }

    private static void PazYSalvoDeAportes(ColumnDescriptor col, CuerpoDePazYSalvoDeAportes p)
    {
        foreach (var t in TextosDelPazYSalvoDeAportes(p)) col.Item().PaddingTop(4).Text(t);
    }

    private static IReadOnlyList<string> TextosDelPazYSalvoDeAportes(CuerpoDePazYSalvoDeAportes p)
    {
        var textos = new List<string>
        {
            $"El asociado se encuentra a paz y salvo por las cuotas de aportes de la cuenta {p.NumeroDeCuenta} ({p.Linea}) al {Fecha(p.Corte)}.",
            $"Saldo de aportes: {Pesos(p.SaldoDeAportes)}",
        };
        if (p.FirmezaHasta is { } f) textos.Add($"Sujeto a firmeza hasta el {Fecha(f)}.");
        return textos;
    }

    private static string Pendiente(CuotaDeAportesPendiente q) => $"Cuota {q.Periodo} de la cuenta {q.Cuenta}, vence el {Fecha(q.Vencimiento)}: {Pesos(q.Pendiente)}";

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
