using System.Globalization;
using IngenIA365ERP.Application.Lending.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports.Captaciones;

/// <summary>
/// Los cuerpos de los documentos al titular de captaciones en PDF (feature 014, US13, T195; contracts/documentos.md §2.1 a §2.5; FR-068, FR-101 a FR-105):
/// el extracto de cuenta, el certificado de saldo, el certificado tributario anual, el certificado de retención, el título del CDAT «no negociable» y la
/// respuesta al juzgado. Los pinta dentro de la plantilla común de <see cref="DocumentosDeCarteraReport"/> —membrete, titular, leyendas, firma, código de
/// verificación y QR—, que delega aquí lo que no es de la 013. Como allá, <see cref="Textos"/> lista en orden lo que el cuerpo pinta, para que las pruebas
/// comprueben lo que el documento dice. No calcula nada ni lee la base. (nuevo)
/// </summary>
public static class DocumentosDeCaptacionesReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>¿El cuerpo es de captaciones? (los que pinta esta clase).</summary>
    public static bool EsDeCaptaciones(CuerpoDeDocumentoDeCartera cuerpo) => cuerpo is CuerpoDeExtractoDeCuenta or CuerpoDeCertificadoDeSaldoDeCuenta
        or CuerpoDeCertificadoTributarioDeCaptaciones or CuerpoDeCertificadoDeRetencion or CuerpoDeTituloDeCdat or CuerpoDeRespuestaAlJuzgado;

    /// <summary>Pinta el cuerpo en la columna del contenido.</summary>
    public static void Pintar(ColumnDescriptor col, CuerpoDeDocumentoDeCartera cuerpo)
    {
        switch (cuerpo)
        {
            case CuerpoDeExtractoDeCuenta x: Extracto(col, x); break;
            case CuerpoDeCertificadoDeSaldoDeCuenta x: CertificadoDeSaldo(col, x); break;
            case CuerpoDeCertificadoTributarioDeCaptaciones x: CertificadoTributario(col, x); break;
            case CuerpoDeCertificadoDeRetencion x: CertificadoDeRetencion(col, x); break;
            case CuerpoDeTituloDeCdat x: Titulo(col, x); break;
            case CuerpoDeRespuestaAlJuzgado x: Respuesta(col, x); break;
        }
    }

    /// <summary>Los textos que pinta el cuerpo, en orden.</summary>
    public static IReadOnlyList<string> Textos(CuerpoDeDocumentoDeCartera cuerpo) => cuerpo switch
    {
        CuerpoDeExtractoDeCuenta x => TextosDelExtracto(x),
        CuerpoDeCertificadoDeSaldoDeCuenta x => TextosDelCertificadoDeSaldo(x),
        CuerpoDeCertificadoTributarioDeCaptaciones x => TextosDelCertificadoTributario(x),
        CuerpoDeCertificadoDeRetencion x => TextosDelCertificadoDeRetencion(x),
        CuerpoDeTituloDeCdat x => TextosDelTitulo(x),
        CuerpoDeRespuestaAlJuzgado x => TextosDeLaRespuesta(x),
        _ => [],
    };

    // ---------------------------------------------------------------------------------------------------------- extracto --

    private static void Extracto(ColumnDescriptor col, CuerpoDeExtractoDeCuenta x)
    {
        var t = TextosDelExtracto(x);
        col.Item().Text(t[0]).SemiBold();
        col.Item().Text(t[1]);
        Titulares(col, x.Titulares);
        col.Item().PaddingTop(6).Text($"Saldo inicial: {Pesos(x.SaldoInicial)}").SemiBold();
        Conceptos(col, x.SaldoInicialPorConcepto);
        col.Item().PaddingTop(8).Text("Movimientos del período (por fecha valor)").SemiBold();
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(2);
                c.RelativeColumn(4);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[] { "Fecha valor", "Fecha contable", "Tipo", "Operación", "Canal", "Valor", "Saldo" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var m in x.Movimientos)
            {
                table.Cell().Element(Celda).Text(Fecha(m.FechaValor));
                table.Cell().Element(Celda).Text(m.FechaContable is { } f ? Fecha(f) : string.Empty);
                table.Cell().Element(Celda).Text(m.Reversa is { } r ? $"{m.Tipo} (de {r})" : m.Tipo);
                table.Cell().Element(Celda).Text(m.Numero ?? string.Empty);
                table.Cell().Element(Celda).Text(m.Canal ?? string.Empty);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(m.Valor));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(m.Saldo));
            }
        });
        if (x.Movimientos.Count == 0) col.Item().PaddingTop(4).Text("La cuenta no tuvo movimientos en el período.").Italic();
        foreach (var linea in TextosDeResumen(x)) col.Item().PaddingTop(3).Text(linea);
        if (x.Cuotas.Count > 0)
        {
            col.Item().PaddingTop(8).Text("Cuotas exigibles del período").SemiBold();
            foreach (var q in x.Cuotas) col.Item().Text(Cuota(q));
        }
        col.Item().PaddingTop(8).Text($"Saldo final: {Pesos(x.SaldoFinal)}").SemiBold();
        Conceptos(col, x.SaldoFinalPorConcepto);
        col.Item().PaddingTop(4).Text(Disponible(x.Disponible));
    }

    private static IReadOnlyList<string> TextosDelExtracto(CuerpoDeExtractoDeCuenta x)
    {
        var t = new List<string>
        {
            $"Cuenta {x.NumeroDeCuenta} · {x.Linea} · {x.Clase} · oficina {x.Oficina}",
            $"Extracto del {Fecha(x.Desde)} al {Fecha(x.Hasta)}",
        };
        t.AddRange(x.Titulares.Select(Titular));
        t.Add($"Saldo inicial: {Pesos(x.SaldoInicial)}");
        t.AddRange(x.SaldoInicialPorConcepto.Select(c => $"{c.Concepto} {Pesos(c.Valor)}"));
        t.AddRange(x.Movimientos.Select(m => $"{Fecha(m.FechaValor)} {m.Tipo} {m.Numero} {Pesos(m.Valor)} {Pesos(m.Saldo)}"));
        t.AddRange(TextosDeResumen(x));
        t.AddRange(x.Cuotas.Select(Cuota));
        t.Add($"Saldo final: {Pesos(x.SaldoFinal)}");
        t.AddRange(x.SaldoFinalPorConcepto.Select(c => $"{c.Concepto} {Pesos(c.Valor)}"));
        t.Add(Disponible(x.Disponible));
        return t;
    }

    private static IEnumerable<string> TextosDeResumen(CuerpoDeExtractoDeCuenta x)
    {
        if (x.Intereses is { } i)
            yield return $"Intereses: causados {Pesos(i.Causado)}, liquidados {Pesos(i.Liquidado)}, ajuste de redondeo {Pesos(i.AjusteDeRedondeo)}"
                         + (i.Tasa is { } tasa ? $" · tasa {tasa}" : string.Empty) + (i.MetodoDeSaldo is { } m ? $" · {m}" : string.Empty);
        if (x.Retencion is { } r)
            yield return r.SinRetencion ?? $"Retención en la fuente: base {Pesos(r.Base)}, componente inflacionario descontado {Pesos(r.ComponenteInflacionario)}"
                         + (r.Tarifa is { } t ? $", tarifa {t}" : string.Empty) + $", retenido {Pesos(r.Retencion)}";
        if (x.Gmf is { } g)
            yield return $"GMF: cobrado {Pesos(g.Cobrado)}, exento usado {Pesos(g.ExentoUsado)} ({g.Politica})";
        if (x.Cargos is { } c)
            yield return $"Cargos: cobrados {Pesos(c.Cobrados)}, pendientes {Pesos(c.Pendientes)}, condonados {Pesos(c.Condonados)}";
    }

    // ------------------------------------------------------------------------------------------------ certificado de saldo --

    private static void CertificadoDeSaldo(ColumnDescriptor col, CuerpoDeCertificadoDeSaldoDeCuenta x)
    {
        var t = TextosDelCertificadoDeSaldo(x);
        col.Item().Text(t[0]).SemiBold();
        col.Item().Text(t[1]);
        Titulares(col, x.Titulares);
        Conceptos(col, x.PorConcepto);
        col.Item().PaddingTop(4).Text($"Saldo del titular al {Fecha(x.Fecha)}: {Pesos(x.SaldoDelTitular)}").SemiBold();
        if (x.Restricciones is { } rs)
        {
            col.Item().PaddingTop(6).Text("Bloqueos y embargos vigentes").SemiBold();
            if (rs.Count == 0) col.Item().Text("Sin bloqueos ni embargos vigentes.").Italic();
            foreach (var r in rs) col.Item().Text($"{r.Clase}: {r.Detalle} — {Pesos(r.Valor)}");
        }
        col.Item().PaddingTop(4).Text($"Huella del saldo {x.HuellaDelSaldo}").FontSize(7).FontColor(Colors.Grey.Darken1);
    }

    private static IReadOnlyList<string> TextosDelCertificadoDeSaldo(CuerpoDeCertificadoDeSaldoDeCuenta x)
    {
        var t = new List<string>
        {
            $"Cuenta {x.NumeroDeCuenta} · {x.Linea} · {x.Clase}",
            $"Estado {x.Estado} · abierta el {Fecha(x.Apertura)} · manejo {x.Manejo}",
        };
        t.AddRange(x.Titulares.Select(Titular));
        t.AddRange(x.PorConcepto.Select(c => $"{c.Concepto} {Pesos(c.Valor)}"));
        t.Add($"Saldo del titular al {Fecha(x.Fecha)}: {Pesos(x.SaldoDelTitular)}");
        if (x.Restricciones is { } rs) t.AddRange(rs.Select(r => $"{r.Clase}: {r.Detalle} — {Pesos(r.Valor)}"));
        return t;
    }

    // --------------------------------------------------------------------------------------------- certificado tributario --

    private static void CertificadoTributario(ColumnDescriptor col, CuerpoDeCertificadoTributarioDeCaptaciones x)
    {
        var t = TextosDelCertificadoTributario(x);
        col.Item().Text(t[0]).SemiBold();
        col.Item().Text(t[1]);
        TablaTributaria(col, x.Captaciones);
        col.Item().PaddingTop(4).Text(Totales(x)).SemiBold();
        if (x.IncluyeAportes)
        {
            col.Item().PaddingTop(8).Text("Aportes sociales").SemiBold();
            if (x.Aportes.Count == 0) col.Item().Text("Sin aportes al 31 de diciembre.").Italic();
            foreach (var a in x.Aportes) col.Item().Text(Aporte(a, x.Año));
        }
    }

    private static void TablaTributaria(ColumnDescriptor col, IReadOnlyList<CuentaTributariaImpresa> cuentas)
    {
        if (cuentas.Count == 0) return;
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[]
                         {
                             "Cuenta", "Clase", "Proporción", "Saldo al 31/12", "Rendimientos", "Componente inflacionario", "Retención", "GMF",
                         })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var c in cuentas)
            {
                table.Cell().Element(Celda).Text(c.Numero);
                table.Cell().Element(Celda).Text(c.Clase);
                table.Cell().Element(Celda).AlignRight().Text(Porcentaje(c.Proporcion));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.SaldoAl31DeDiciembre));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Rendimientos));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.ComponenteInflacionario));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Retencion));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Gmf));
            }
        });
    }

    private static IReadOnlyList<string> TextosDelCertificadoTributario(CuerpoDeCertificadoTributarioDeCaptaciones x)
    {
        var año = x.Año.ToString(CultureInfo.InvariantCulture);
        var t = new List<string>
        {
            $"Año gravable {año} · agente retenedor {x.AgenteRetenedor}" + (x.NitDelAgente is { } nit ? $" NIT {nit}" : string.Empty),
            $"Rendimientos financieros, retención en la fuente, componente inflacionario y GMF del año {año}, y saldos al 31/12/{año}, por cuenta.",
        };
        t.AddRange(x.Captaciones.Select(c => $"{c.Numero} {c.Clase} {Porcentaje(c.Proporcion)} {Pesos(c.SaldoAl31DeDiciembre)} {Pesos(c.Rendimientos)} "
                                              + $"{Pesos(c.ComponenteInflacionario)} {Pesos(c.Retencion)} {Pesos(c.Gmf)}"));
        t.Add(Totales(x));
        if (x.IncluyeAportes) t.AddRange(x.Aportes.Select(a => Aporte(a, x.Año)));
        return t;
    }

    private static string Totales(CuerpoDeCertificadoTributarioDeCaptaciones x) =>
        $"Totales: saldo {Pesos(x.TotalSaldo)} · rendimientos {Pesos(x.TotalRendimientos)} · componente inflacionario {Pesos(x.TotalComponenteInflacionario)} · "
        + $"retención {Pesos(x.TotalRetencion)} · GMF {Pesos(x.TotalGmf)}";

    private static string Aporte(CuentaTributariaImpresa a, short año) =>
        $"Aportes {a.Numero}: saldo al 31/12/{año.ToString(CultureInfo.InvariantCulture)} {Pesos(a.SaldoAl31DeDiciembre)}, revalorización del año {Pesos(a.Revalorizacion)}";

    // -------------------------------------------------------------------------------------------- certificado de retención --

    private static void CertificadoDeRetencion(ColumnDescriptor col, CuerpoDeCertificadoDeRetencion x)
    {
        var t = TextosDelCertificadoDeRetencion(x);
        col.Item().Text(t[0]).SemiBold();
        col.Item().Text(t[1]);
        col.Item().PaddingTop(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(5);
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
            });
            table.Header(h =>
            {
                foreach (var titulo in new[] { "Concepto", "Tarifa", "Rendimientos", "Componente inflacionario", "Base sometida", "Retenido" })
                    h.Cell().Element(EncabezadoDeTabla).Text(titulo);
            });
            foreach (var r in x.Renglones)
            {
                table.Cell().Element(Celda).Text(r.Concepto);
                table.Cell().Element(Celda).AlignRight().Text(r.Tarifa ?? string.Empty);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(r.Rendimientos));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(r.ComponenteInflacionario));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(r.Base));
                table.Cell().Element(Celda).AlignRight().Text(Pesos(r.Retencion));
            }
        });
        col.Item().PaddingTop(4).Text(t[^1]).SemiBold();
    }

    private static IReadOnlyList<string> TextosDelCertificadoDeRetencion(CuerpoDeCertificadoDeRetencion x)
    {
        var t = new List<string>
        {
            $"Agente retenedor {x.AgenteRetenedor}" + (x.NitDelAgente is { } nit ? $" NIT {nit}" : string.Empty)
                                                  + (x.DireccionDelAgente is { } d ? $" · {d}" : string.Empty) + (x.Ciudad is { } c ? $" · {c}" : string.Empty),
            $"Retención en la fuente practicada entre el {Fecha(x.Desde)} y el {Fecha(x.Hasta)}",
        };
        t.AddRange(x.Renglones.Select(r => $"{r.Concepto} {r.Tarifa} {Pesos(r.Base)} {Pesos(r.Retencion)}"));
        t.Add($"Total base sometida a retención {Pesos(x.TotalBase)} · total retenido {Pesos(x.TotalRetencion)}");
        return t;
    }

    // ------------------------------------------------------------------------------------------------------------ título --

    private static void Titulo(ColumnDescriptor col, CuerpoDeTituloDeCdat x)
    {
        col.Item().AlignCenter().Text(CuerpoDeTituloDeCdat.LeyendaNoNegociable).FontSize(18).Bold().FontColor(Colors.Red.Darken3);
        var t = TextosDelTitulo(x);
        foreach (var linea in t.Skip(1).Take(2)) col.Item().Text(linea).SemiBold();
        Titulares(col, x.Titulares);
        col.Item().PaddingTop(6).Text("Condiciones").SemiBold();
        foreach (var linea in Condiciones(x)) col.Item().Text(linea);
        col.Item().PaddingTop(6).Text("Reglas del reglamento").SemiBold();
        foreach (var regla in x.Reglas) col.Item().Text($"• {regla}");
        if (x.SeguroDeDepositos is { } s) col.Item().PaddingTop(6).Text(s).Italic();
    }

    private static IReadOnlyList<string> TextosDelTitulo(CuerpoDeTituloDeCdat x)
    {
        var t = new List<string>
        {
            CuerpoDeTituloDeCdat.LeyendaNoNegociable,
            $"Título {x.NumeroDelTitulo} · cuenta {x.NumeroDeCuenta} · {x.Linea} · versión {x.Version.ToString(Co)} ({x.Causa})",
            x.TituloAnterior is { } a ? $"Reemplaza al título {a}" : "Primer título del CDAT",
        };
        t.AddRange(x.Titulares.Select(Titular));
        t.Add($"Manejo {x.Manejo}");
        t.AddRange(Condiciones(x));
        t.AddRange(x.Reglas);
        if (x.SeguroDeDepositos is { } s) t.Add(s);
        return t;
    }

    private static IEnumerable<string> Condiciones(CuerpoDeTituloDeCdat x)
    {
        yield return $"Capital {Pesos(x.Capital)} · constituido el {Fecha(x.Constitucion)} · esta versión desde el {Fecha(x.InicioDeLaVersion)}";
        yield return $"Plazo {x.PlazoEnDias.ToString(Co)} días · vence el {Fecha(x.Vencimiento)}";
        yield return $"Tasa efectiva anual {Porcentaje(x.TasaEfectivaAnual)}" + (x.TasaNominal is { } n ? $" · nominal {Porcentaje(n)} {x.Periodicidad}" : string.Empty)
                     + $" · tasa de la tabla {Porcentaje(x.TasaDeLaTabla)}" + (x.MargenNegociado is { } m ? $" · margen negociado {Porcentaje(m)}" : string.Empty);
        yield return $"Intereses: periodicidad {x.Periodicidad}, modalidad {x.Modalidad}, destino: {x.DestinoDeLosIntereses}";
    }

    // ------------------------------------------------------------------------------------------------ respuesta al juzgado --

    private static void Respuesta(ColumnDescriptor col, CuerpoDeRespuestaAlJuzgado x)
    {
        var t = TextosDeLaRespuesta(x);
        foreach (var linea in t.Take(3)) col.Item().Text(linea).SemiBold();
        col.Item().PaddingTop(8).Text(x.Texto);
        if (t.Count > 4) col.Item().PaddingTop(6).Text(t[^1]);
    }

    private static IReadOnlyList<string> TextosDeLaRespuesta(CuerpoDeRespuestaAlJuzgado x)
    {
        var t = new List<string>
        {
            $"{x.Clase} · enviada el {Fecha(x.FechaDeEnvio)}",
            $"{x.Autoridad} · oficio {x.Oficio} del {Fecha(x.FechaDelOficio)}" + (x.Proceso is { } p ? $" · proceso {p}" : string.Empty),
            $"Demandante: {x.Demandante}",
            x.Texto,
        };
        if (x.Retenido is not null || x.Consignado is not null)
            t.Add($"Retenido a la fecha {Pesos(x.Retenido ?? 0m)} · consignado {Pesos(x.Consignado ?? 0m)}");
        return t;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private static void Titulares(ColumnDescriptor col, IReadOnlyList<TitularImpreso> titulares)
    {
        foreach (var t in titulares) col.Item().Text(Titular(t)).FontSize(8);
    }

    private static void Conceptos(ColumnDescriptor col, IReadOnlyList<ConceptoDeCuentaImpreso> conceptos)
    {
        if (conceptos.Count == 0) return;
        col.Item().PaddingTop(2).Table(table =>
        {
            table.ColumnsDefinition(d =>
            {
                d.RelativeColumn(4);
                d.RelativeColumn(2);
            });
            foreach (var c in conceptos)
            {
                table.Cell().Element(Celda).Text(c.Concepto);
                table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Valor));
            }
        });
    }

    private static string Titular(TitularImpreso t) =>
        $"{t.Papel}: {t.Nombre} · {t.Documento}" + (t.Proporcion is { } p ? $" · proporción {Porcentaje(p)}" : string.Empty);

    private static string Cuota(CuotaDelExtractoImpresa q) =>
        $"Cuota {q.Periodo}, vence el {Fecha(q.Vence)}: {Pesos(q.Valor)}" + (q.Pendiente > 0m ? $", pendiente {Pesos(q.Pendiente)}" : ", pagada");

    private static string Disponible(DisponibleImpreso d) =>
        $"Disponible {Pesos(d.Disponible)} = saldo {Pesos(d.Saldo)} − bloqueos {Pesos(d.Bloqueado)} − embargos {Pesos(d.Embargado)} − canje {Pesos(d.EnCanje)} "
        + $"− mínimo {Pesos(d.Minimo)}";

    private static IContainer EncabezadoDeTabla(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(3).DefaultTextStyle(x => x.SemiBold().FontSize(8));

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).DefaultTextStyle(x => x.FontSize(8));

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Co);

    private static string Pesos(decimal valor) => "$ " + valor.ToString("#,0.00", Co);

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Co) + " %";
}
