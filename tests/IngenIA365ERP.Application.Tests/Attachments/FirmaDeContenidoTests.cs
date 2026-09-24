using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Feature 011 (research R6): la confirmación contrasta los primeros bytes del objeto con el tipo
/// declarado. Lo que se prueba es la mentira sobre el tipo —un ejecutable con extensión .pdf—, no el
/// contenido malicioso dentro de un tipo verdadero (eso sería un antivirus, fuera de alcance).
/// </summary>
public class FirmaDeContenidoTests
{
    private const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static byte[] Con(params byte[][] partes) => partes.SelectMany(p => p).Concat(new byte[64]).ToArray();
    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    public static TheoryData<string, byte[]> Validos => new()
    {
        { "application/pdf", Ascii("%PDF-1.7\n%âãÏÓ\n1 0 obj") },
        { "image/png", Con([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]) },
        { "image/jpeg", Con([0xFF, 0xD8, 0xFF, 0xE0]) },
        { "image/gif", Con(Ascii("GIF89a")) },
        { "image/gif", Con(Ascii("GIF87a")) },
        { "image/webp", Con(Ascii("RIFF"), [0x24, 0x00, 0x00, 0x00], Ascii("WEBPVP8 ")) },
        { "application/msword", Con([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]) },
        { "application/vnd.ms-excel", Con([0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]) },
        { Docx, Con([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00], new byte[24], Ascii("[Content_Types].xml")) },
        { Xlsx, Con([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00], new byte[24], Ascii("[Content_Types].xml")) },
        { "text/plain", Encoding.UTF8.GetBytes("Recibo de caja n.º 1234\r\nValor:\t$ 1.500.000\n") },
        { "text/csv", Encoding.Latin1.GetBytes("cédula;nombre;valor\n1023;Núñez;1500\n") },
        { "text/csv; charset=utf-8", Encoding.UTF8.GetBytes("﻿cédula,nombre\n") },
        { "APPLICATION/PDF", Ascii("%PDF-1.4") },
    };

    [Theory]
    [MemberData(nameof(Validos))]
    public void Cada_tipo_admitido_se_reconoce_por_su_firma(string tipo, byte[] inicio) =>
        FirmaDeContenido.Motivo(tipo, inicio, inicio.Length).Should().BeNull();

    [Fact]
    public void Un_ejecutable_renombrado_a_pdf_se_rechaza_y_el_motivo_dice_que_es()
    {
        var exe = Con(Ascii("MZ"), [0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);

        FirmaDeContenido.Motivo("application/pdf", exe, 20_481_234)
            .Should().Be("Se declaró un PDF, pero el contenido es un ejecutable de Windows.");
    }

    [Fact]
    public void Una_imagen_declarada_como_documento_se_rechaza()
    {
        var png = Con([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        FirmaDeContenido.Motivo("application/pdf", png, 5000).Should().Contain("una imagen PNG");
        FirmaDeContenido.Motivo(Docx, png, 5000).Should().StartWith("Se declaró un documento de Word (.docx)");
    }

    [Fact]
    public void Un_binario_declarado_como_texto_se_rechaza()
    {
        var binario = new byte[] { 0x48, 0x6F, 0x6C, 0x61, 0x00, 0x01, 0x02 };

        FirmaDeContenido.Motivo("text/plain", binario, 7).Should().StartWith("Se declaró un texto");
        FirmaDeContenido.Motivo("text/csv", Con(Ascii("MZ")), 66).Should().Contain("ejecutable");
    }

    [Fact]
    public void Un_zip_cualquiera_no_pasa_por_documento_de_Office()
    {
        var zip = Con([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00], Ascii("fotos/playa.jpg"));

        FirmaDeContenido.Motivo(Xlsx, zip, 4096).Should().Be("Se declaró una hoja de Excel (.xlsx), pero el contenido es un archivo comprimido ZIP.");
    }

    [Fact]
    public void Un_archivo_vacio_se_rechaza_sea_cual_sea_el_tipo()
    {
        FirmaDeContenido.Motivo("application/pdf", [], 0).Should().Be("El archivo está vacío.");
        FirmaDeContenido.Motivo("text/plain", [], 0).Should().Be("El archivo está vacío.");
    }

    [Fact]
    public void Un_tipo_fuera_de_la_lista_blanca_no_se_acepta_aunque_la_firma_sea_valida() =>
        FirmaDeContenido.Motivo("application/zip", Con([0x50, 0x4B, 0x03, 0x04]), 68).Should().Contain("no está admitido");

    [Fact]
    public void Todos_los_tipos_de_la_lista_blanca_tienen_firma()
    {
        // Si se agrega un tipo a AttachmentPolicy sin su firma, la confirmación rechazaría todo lo de ese tipo.
        foreach (var tipo in AttachmentPolicy.AllowedMimeTypes)
            FirmaDeContenido.Motivo(tipo, [0x00], 1).Should().NotContain("no está admitido", $"«{tipo}» necesita su firma en FirmaDeContenido");
    }
}
