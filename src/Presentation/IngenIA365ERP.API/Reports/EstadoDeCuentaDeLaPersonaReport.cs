using System.Globalization;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Domain.Lending.Parameters;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// El estado de cuenta de una persona y su simulación de cancelación en PDF (2026-11-16, pedido del dueño; molde del encabezado:
/// <see cref="DocumentosDeCarteraReport"/>): membrete con el logotipo de Cartera, el nombre y el NIT de la cooperativa, el título a la derecha y una
/// línea; los datos de la persona; las tarjetas de resumen (saldo de créditos, total para cancelar, vencido, mora máxima, aportes, ahorros y cupo
/// disponible); la tabla de productos con las columnas que eligió quien imprime (los códigos de <c>ColumnasDelEstadoDeCuenta</c>, por
/// <see cref="TablaDelEstadoDeCuenta"/>); el resumen de conceptos y los avisos. Carta vertical, apaisada si la tabla pasa de
/// <see cref="ColumnasParaApaisado"/> columnas; al pie, la fecha de generación y «Página x de y».
/// <para>Los valores van sin símbolo de moneda: sin centavos (<c>N0</c>) o con ellos (<c>N2</c>) según <c>decimales</c>. No lee la base: todo sale de
/// los DTO de Application. (nuevo)</para>
/// </summary>
public static class EstadoDeCuentaDeLaPersonaReport
{
    /// <summary>Desde cuántas columnas de la tabla de productos la hoja va apaisada.</summary>
    public const int ColumnasParaApaisado = 8;

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El estado de cuenta en PDF con las columnas elegidas (vacías = las de por defecto).</summary>
    public static byte[] Generate(EstadoDeCuentaDeLaPersonaDto e, IEnumerable<string>? columnas, bool decimales, DateTimeOffset generadoEn)
    {
        ArgumentNullException.ThrowIfNull(e);
        var tabla = TablaDelEstadoDeCuenta.Armar(e, columnas).SinOcultas();
        var apaisado = tabla.Columnas.Count > ColumnasParaApaisado;
        var logotipo = Logotipo(e.Membrete?.LogoDataUri);
        return Document.Create(container => container.Page(page =>
            {
                Hoja(page, apaisado, e.Membrete, logotipo, TablaDelEstadoDeCuenta.Titulo, $"Corte al {Fecha(e.Fecha)}", generadoEn);
                page.Content().PaddingVertical(4).Column(col =>
                {
                    DatosDeLaPersona(col, e.Persona, "Fecha de corte", e.Fecha);
                    Tarjetas(col, e.Totales, decimales);
                    Productos(col, tabla, decimales);
                    ResumenDeConceptos(col, e.Totales, decimales);
                    Avisos(col, e.Avisos);
                });
            }))
            .WithMetadata(Metadatos(e.Membrete, TablaDelEstadoDeCuenta.Titulo, generadoEn))
            .GeneratePdf();
    }

    /// <summary>La simulación de cancelación en PDF, con el mismo membrete.</summary>
    public static byte[] GenerateSimulacion(SimulacionDeCancelacionDto s, bool decimales, DateTimeOffset generadoEn)
    {
        ArgumentNullException.ThrowIfNull(s);
        const string titulo = "Simulación de cancelación";
        var logotipo = Logotipo(s.Membrete?.LogoDataUri);
        return Document.Create(container => container.Page(page =>
            {
                Hoja(page, apaisado: false, s.Membrete, logotipo, titulo, $"Pago el {Fecha(s.Fecha)}", generadoEn);
                page.Content().PaddingVertical(4).Column(col =>
                {
                    DatosDeLaPersona(col, s.Persona, "Fecha de pago", s.Fecha);
                    CreditosSimulados(col, s, decimales);
                    col.Item().PaddingTop(8).Text(TextoDelModo(s.ModoDeIntereses)).FontSize(8);
                    col.Item().PaddingTop(2).Text(
                            $"Valores calculados a la fecha de pago sin los pagos ni los movimientos que ocurran antes de ella. Es una simulación: no es una "
                            + "orden de pago y puede cambiar.")
                        .FontSize(8).Italic();
                    Avisos(col, s.Creditos.SelectMany(c => c.Avisos.Select(a => $"{c.Numero}: {a}")).Distinct().ToList());
                });
            }))
            .WithMetadata(Metadatos(s.Membrete, titulo, generadoEn))
            .GeneratePdf();
    }

    /// <summary>Cómo se cobró el interés de la cuota en curso, en palabras.</summary>
    public static string TextoDelModo(string modo) => modo switch
    {
        ParametrosDeCartera.InteresesDeLaCuotaCompleta =>
            "Intereses: se cobra el interés completo de la cuota en curso aunque se pague antes de su vencimiento.",
        ParametrosDeCartera.InteresesCorridos => "Intereses: se cobra el interés causado día a día hasta la fecha de pago.",
        _ => "Intereses: según cada crédito (su línea y el tope legal del prepago), el causado hasta la fecha de pago o el completo de la cuota en curso.",
    };

    // ------------------------------------------------------------------------------------------------------------ hoja --

    internal static void Hoja(PageDescriptor page, bool apaisado, MembreteDto? m, Image? logotipo, string titulo, string subtitulo, DateTimeOffset generadoEn)
    {
        page.Size(apaisado ? PageSizes.Letter.Landscape() : PageSizes.Letter);
        page.Margin(32);
        page.DefaultTextStyle(x => x.FontSize(9));

        page.Header().Column(col =>
        {
            col.Item().Row(row =>
            {
                if (logotipo is not null) row.ConstantItem(90).Height(54).Image(logotipo).FitArea();
                row.RelativeItem().PaddingLeft(logotipo is not null ? 8 : 0).Column(membrete =>
                {
                    membrete.Item().Text(m?.Cooperativa ?? MembreteDeCartera.CooperativaSinNombre).Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(m?.Nit)) membrete.Item().Text($"NIT {m.Nit}");
                });
                row.ConstantItem(220).AlignRight().Column(doc =>
                {
                    doc.Item().AlignRight().Text(titulo.ToUpper(Co)).SemiBold().FontSize(12);
                    doc.Item().AlignRight().Text(subtitulo);
                });
            });
            col.Item().PaddingTop(4).PaddingBottom(4).LineHorizontal(1);
        });

        page.Footer().Row(row =>
        {
            row.RelativeItem().Text($"Generado el {generadoEn.ToString("dd/MM/yyyy HH:mm", Co)}").FontSize(7);
            row.RelativeItem().AlignRight().Text(x =>
            {
                x.Span("Página ").FontSize(7);
                x.CurrentPageNumber().FontSize(7);
                x.Span(" de ").FontSize(7);
                x.TotalPages().FontSize(7);
            });
        });
    }

    internal static DocumentMetadata Metadatos(MembreteDto? m, string titulo, DateTimeOffset generadoEn) => new()
    {
        Title = titulo,
        Author = m?.Cooperativa ?? MembreteDeCartera.CooperativaSinNombre,
        Subject = titulo,
        Creator = "IngenIA365ERP",
        Producer = "IngenIA365ERP",
        CreationDate = generadoEn,
        ModifiedDate = generadoEn,
    };

    // ------------------------------------------------------------------------------------------------------- bloques --

    internal static void DatosDeLaPersona(ColumnDescriptor col, PersonaDelEstadoDeCuentaDto p, string etiquetaDeFecha, DateOnly fecha)
    {
        col.Item().Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(70);
                c.RelativeColumn(3);
                c.ConstantColumn(70);
                c.RelativeColumn(2);
            });
            void Par(string etiqueta, string? valor)
            {
                table.Cell().PaddingVertical(1).Text(etiqueta).SemiBold().FontSize(8);
                table.Cell().PaddingVertical(1).Text(string.IsNullOrWhiteSpace(valor) ? "—" : valor).FontSize(8);
            }
            Par("Nombre", p.Nombre);
            Par("Documento", p.Documento);
            Par("Asociado", p.EsAsociado ? "Sí" : "No");
            Par("Celular", p.Celular);
            Par("Correo", p.Correo);
            Par(etiquetaDeFecha, Fecha(fecha));
        });
    }

    private static void Tarjetas(ColumnDescriptor col, TotalesDelEstadoDeCuentaDto t, bool decimales)
    {
        var tarjetas = new List<(string Etiqueta, string Valor)>
        {
            ("Saldo de créditos", Valor(t.SaldoDeCreditos, decimales)),
            ("Total para cancelar", Valor(t.ParaCancelar, decimales)),
            ("Vencido", Valor(t.ValorVencido, decimales)),
            ("Mora máxima", $"{t.DiasDeMoraMaximos.ToString(Co)} días"),
            ("Aportes", Valor(t.SaldoEnAportes, decimales)),
            ("Ahorros", Valor(t.SaldoEnAhorros, decimales)),
            ("Cupo disponible", t.CupoDisponible is { } cupo ? Valor(cupo, decimales) : "—"),
        };
        col.Item().PaddingTop(8).Row(row =>
        {
            foreach (var (etiqueta, valor) in tarjetas)
                row.RelativeItem().Padding(2).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(4).Column(c =>
                {
                    c.Item().Text(etiqueta).FontSize(7).FontColor(Colors.Grey.Darken2);
                    c.Item().Text(valor).SemiBold().FontSize(9);
                });
        });
    }

    private static void Productos(ColumnDescriptor col, TablaExportable tabla, bool decimales)
    {
        col.Item().PaddingTop(10).Text("Productos").SemiBold().FontSize(10);
        if (tabla.Filas.Count == 0)
        {
            col.Item().PaddingTop(2).Text("La persona no tiene créditos, aportes ni ahorros en el estado de cuenta.").FontSize(8);
            return;
        }
        var letra = tabla.Columnas.Count > 12 ? 7f : 8f;
        col.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                foreach (var columna in tabla.Columnas) c.RelativeColumn(Numerica(columna.Tipo) ? 2 : 3);
            });
            table.Header(h =>
            {
                foreach (var columna in tabla.Columnas)
                {
                    var celda = h.Cell().Element(c => EncabezadoDeTabla(c, letra));
                    (Numerica(columna.Tipo) ? celda.AlignRight() : celda).Text(columna.Nombre);
                }
            });
            foreach (var fila in tabla.Filas)
                for (var i = 0; i < tabla.Columnas.Count; i++)
                {
                    var tipo = tabla.Columnas[i].Tipo;
                    var celda = table.Cell().Element(c => Celda(c, letra));
                    (Numerica(tipo) ? celda.AlignRight() : celda).Text(Texto(i < fila.Valores.Count ? fila.Valores[i] : null, tipo, decimales));
                }
        });
    }

    private static void ResumenDeConceptos(ColumnDescriptor col, TotalesDelEstadoDeCuentaDto t, bool decimales)
    {
        if (!t.ConResumenDeConceptos) return;
        col.Item().PaddingTop(10).Text("Resumen de conceptos como deudor").SemiBold().FontSize(10);
        var conceptos = new List<(string Concepto, decimal Valor)>
        {
            ("Capital", t.ConceptoCapital ?? 0m), ("Interés corriente", t.ConceptoInteresCorriente ?? 0m), ("Interés de mora", t.ConceptoInteresDeMora ?? 0m),
            ("Seguro", t.ConceptoSeguro ?? 0m),
        };
        // 2026-11-16 (pedido del dueño; feature 024, fase 2): la administración, aparte, si la debe.
        if (t.ConceptoAdministracion is > 0m or < 0m) conceptos.Add(("Administración", t.ConceptoAdministracion.Value));
        conceptos.Add(("Otros", t.ConceptoOtros ?? 0m));
        col.Item().PaddingTop(4).Width(280).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(2);
            });
            foreach (var (concepto, valor) in conceptos)
            {
                table.Cell().Element(c => Celda(c, 8f)).Text(concepto);
                table.Cell().Element(c => Celda(c, 8f)).AlignRight().Text(Valor(valor, decimales));
            }
            table.Cell().Element(c => EncabezadoDeTabla(c, 8f)).Text("Total");
            table.Cell().Element(c => EncabezadoDeTabla(c, 8f)).AlignRight().Text(Valor(t.TotalDeConceptos, decimales));
        });
        col.Item().PaddingTop(2).Text("El total para cancelar descuenta el anticipo de cada crédito.").FontSize(7).FontColor(Colors.Grey.Darken1);
    }

    private static void CreditosSimulados(ColumnDescriptor col, SimulacionDeCancelacionDto s, bool decimales)
    {
        // 2026-11-16 (pedido del dueño; feature 024, fase 2): la administración, aparte, si algún crédito la debe.
        var conAdministracion = s.Creditos.Any(c => c.Administracion != 0m);
        string[] titulos = conAdministracion
            ? ["Crédito", "Línea", "Capital", "Interés corriente", "Interés de mora", "Seguro", "Administración", "Otros", "Anticipo", "Total"]
            : ["Crédito", "Línea", "Capital", "Interés corriente", "Interés de mora", "Seguro", "Otros", "Anticipo", "Total"];
        col.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                for (var i = 2; i < titulos.Length; i++) c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                for (var i = 0; i < titulos.Length; i++)
                {
                    var celda = h.Cell().Element(c => EncabezadoDeTabla(c, 7.5f));
                    (i >= 2 ? celda.AlignRight() : celda).Text(titulos[i]);
                }
            });
            foreach (var c in s.Creditos)
            {
                table.Cell().Element(x => Celda(x, 7.5f)).Text(c.Numero);
                table.Cell().Element(x => Celda(x, 7.5f)).Text(c.Linea);
                decimal[] valores = conAdministracion
                    ? [c.Capital, c.InteresCorriente, c.InteresDeMora, c.Seguro, c.Administracion, c.Otros, c.Anticipo, c.Total]
                    : [c.Capital, c.InteresCorriente, c.InteresDeMora, c.Seguro, c.Otros, c.Anticipo, c.Total];
                foreach (var v in valores)
                    table.Cell().Element(x => Celda(x, 7.5f)).AlignRight().Text(Valor(v, decimales));
            }
            table.Cell().ColumnSpan(2).Element(x => EncabezadoDeTabla(x, 7.5f)).Text("Total");
            var totales = new List<decimal>
            {
                s.Creditos.Sum(c => c.Capital), s.Creditos.Sum(c => c.InteresCorriente), s.Creditos.Sum(c => c.InteresDeMora), s.Creditos.Sum(c => c.Seguro),
            };
            if (conAdministracion) totales.Add(s.Creditos.Sum(c => c.Administracion));
            totales.AddRange([s.Creditos.Sum(c => c.Otros), s.Creditos.Sum(c => c.Anticipo), s.Total]);
            foreach (var v in totales)
                table.Cell().Element(x => EncabezadoDeTabla(x, 7.5f)).AlignRight().Text(Valor(v, decimales));
        });
        if (s.Creditos.Any(c => c.InteresNoCausadoDeLaCuotaEnCurso is > 0m))
            col.Item().PaddingTop(2).Text("El interés corriente incluye lo que falta por causar de la cuota en curso.").FontSize(7)
                .FontColor(Colors.Grey.Darken1);
    }

    internal static void Avisos(ColumnDescriptor col, IReadOnlyList<string> avisos)
    {
        if (avisos.Count == 0) return;
        col.Item().PaddingTop(10).Text("Avisos").SemiBold().FontSize(10);
        foreach (var aviso in avisos) col.Item().PaddingTop(1).Text($"• {aviso}").FontSize(8);
    }

    // -------------------------------------------------------------------------------------------------------- piezas --

    /// <summary>El logotipo del data URI, o nulo si no hay o no es una imagen que el PDF pueda pintar (un logotipo malo no impide imprimir).</summary>
    internal static Image? Logotipo(string? dataUri)
    {
        if (string.IsNullOrWhiteSpace(dataUri)) return null;
        var coma = dataUri.IndexOf(',');
        if (coma < 0) return null;
        try
        {
            return Image.FromBinaryData(Convert.FromBase64String(dataUri[(coma + 1)..]));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Un base64 roto o un formato que QuestPDF no lee (SVG, por ejemplo): el estado de cuenta sale sin logotipo.
            return null;
        }
    }

    private static bool Numerica(TipoDeColumna tipo) => tipo is TipoDeColumna.Entero or TipoDeColumna.Moneda or TipoDeColumna.Decimal
        or TipoDeColumna.Porcentaje or TipoDeColumna.Cantidad or TipoDeColumna.Costo;

    /// <summary>Un valor de la tabla: los de moneda con o sin centavos y sin símbolo; los demás como los pinta el exportador de tablas.</summary>
    private static string Texto(object? valor, TipoDeColumna tipo, bool decimales) => valor switch
    {
        null => "—",
        decimal d when tipo == TipoDeColumna.Moneda => Valor(d, decimales),
        _ => ExportadorDeTablas.Texto(valor, tipo),
    };

    /// <summary>Un valor en pesos sin símbolo: <c>1.234.567</c> o <c>1.234.567,89</c>.</summary>
    public static string Valor(decimal valor, bool decimales) => valor.ToString(decimales ? "N2" : "N0", Co);

    internal static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Co);

    internal static IContainer EncabezadoDeTabla(IContainer c, float letra) =>
        c.Background(Colors.Grey.Lighten3).Padding(3).DefaultTextStyle(x => x.SemiBold().FontSize(letra));

    internal static IContainer Celda(IContainer c, float letra) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).DefaultTextStyle(x => x.FontSize(letra));
}
