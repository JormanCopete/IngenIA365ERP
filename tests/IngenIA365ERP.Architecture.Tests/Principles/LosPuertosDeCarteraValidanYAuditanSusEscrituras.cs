using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, desviación III y X (T101): los métodos de escritura de los puertos que corren fuera de MediatR
/// (<c>IContratoDeObligaciones</c>, <c>IReservasDeCartera</c>) ejecutan su validador y escriben su evento.
///
/// <para>La lista nace vacía en el Setup (feature 013, T0001) y la llenan US5 y US6 con los archivos de sus implementaciones. US5 (T0526): el
/// contrato real de obligaciones. Su auditoría no se llama <c>Audit…</c>: el intento va por <c>AuditoriaDeEscriturasDeCartera.IntentoAsync</c> (un
/// contexto aparte, antes de validar) y el hecho por <c>HechoAsync</c> (en <c>COR_AuditOutbox</c>, dentro de la transacción), así que la regla los
/// reconoce por su nombre y además mira método por método que cada escritura (aplicar, reversar, resolver) escriba el intento, pase por su
/// validador FluentValidation y deje el hecho.</para>
/// </summary>
public class LosPuertosDeCarteraValidanYAuditanSusEscrituras
{
    /// <summary>Implementaciones de puertos de escritura (relativas a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // US5 (T0547): las cinco operaciones reales del contrato de obligaciones.
        "Core/IngenIA365ERP.Application/Lending/Integration/ContratoDeObligacionesDeCartera.cs",
        // US6 (T0577, T0584): reservar y liberar el número de una venta a crédito.
        "Core/IngenIA365ERP.Application/Lending/Integration/ReservasDeCartera.cs",
    ];

    /// <summary>Las escrituras de las reservas (US6): cada una escribe su intento antes de pasar por su validador explícito.</summary>
    private static readonly string[] EscriturasDeLasReservas = ["ReservarAsync", "LiberarAsync"];

    private static readonly Regex Valida = new(@"\bValidateAsync\(|\bValidar\w*\(", RegexOptions.Compiled);

    /// <summary>
    /// <c>Audit…(</c> / <c>Auditar…(</c> de los puertos de la 012, o el intento y el hecho de <c>AuditoriaDeEscriturasDeCartera</c> (US5).
    /// </summary>
    private static readonly Regex Audita = new(@"\bAudit\w*\(|\bAuditar\w*\(|\b(IntentoAsync|HechoAsync)\(", RegexOptions.Compiled);

    /// <summary>Las escrituras del contrato y lo que cada una tiene que hacer en su propio cuerpo.</summary>
    private static readonly string[] EscriturasDelContrato = ["AplicarAsync", "ReversarAsync", "ResolverSaldoAFavorAsync"];

    [Fact]
    public void Cada_puerto_de_escritura_valida_y_audita()
    {
        var archivos = ReglaSobreFuente.ArchivosNombrados(Archivos).ToList();
        Assert.Equal(Archivos.Length, archivos.Count);
        var faltantes = ReglaSobreFuente.Faltantes(archivos, Valida).Select(a => a + ": no valida")
            .Concat(ReglaSobreFuente.Faltantes(archivos, Audita).Select(a => a + ": no audita"))
            .ToList();
        ReglaSobreFuente.SinInfracciones(faltantes, "Un puerto de Cartera escribe sin validar o sin auditar (T101)");
    }

    [Fact]
    public void Cada_escritura_del_contrato_escribe_el_intento_valida_y_deja_el_hecho()
    {
        var archivo = ReglaSobreFuente.ArchivosNombrados(Archivos).Single(a => a.EndsWith("ContratoDeObligacionesDeCartera.cs", StringComparison.Ordinal));
        var texto = FuenteSinComentarios.Leer(archivo);
        var infracciones = new List<string>();
        foreach (var metodo in EscriturasDelContrato)
        {
            var cuerpo = CuerpoDe(texto, metodo);
            if (cuerpo is null) { infracciones.Add($"{metodo}: no está"); continue; }
            var intento = cuerpo.IndexOf("auditoria.IntentoAsync(", StringComparison.Ordinal);
            var validador = Regex.Match(cuerpo, @"RechazoDeLoteAsync\(validador\w*,");
            if (intento < 0) infracciones.Add($"{metodo}: no escribe el intento (Lending.ObligationContract.Attempted)");
            if (!validador.Success) infracciones.Add($"{metodo}: no pasa por su validador");
            else if (intento >= 0 && intento > validador.Index) infracciones.Add($"{metodo}: valida antes de escribir el intento (§3.3 paso 1)");
            if (!cuerpo.Contains("auditoria.HechoAsync(", StringComparison.Ordinal)) infracciones.Add($"{metodo}: no deja el hecho");
        }

        // El rechazo de lote también deja su hecho (…Rejected), y la validación de forma es la de FluentValidation.
        Assert.Contains("validador.ValidateAsync(", texto, StringComparison.Ordinal);
        ReglaSobreFuente.SinInfracciones(infracciones, "Una escritura del contrato de obligaciones no valida o no audita (T101, T68)");
    }

    /// <summary>
    /// US6 (T0577; T22, T68): <c>ReservarAsync</c> y <c>LiberarAsync</c> escriben el intento (en su contexto aparte) antes de pasar por su validador
    /// explícito, y el archivo deja el hecho (<c>Reserved</c>, <c>Released</c>) por <c>HechoAsync</c>.
    /// </summary>
    [Fact]
    public void Reservar_y_liberar_escriben_el_intento_validan_y_dejan_el_hecho()
    {
        var archivo = ReglaSobreFuente.ArchivosNombrados(Archivos).Single(a => a.EndsWith("ReservasDeCartera.cs", StringComparison.Ordinal));
        var texto = FuenteSinComentarios.Leer(archivo);
        var infracciones = new List<string>();
        foreach (var metodo in EscriturasDeLasReservas)
        {
            var cuerpo = CuerpoDe(texto, metodo);
            if (cuerpo is null) { infracciones.Add($"{metodo}: no está"); continue; }
            var intento = cuerpo.IndexOf("auditoria.IntentoAsync(", StringComparison.Ordinal);
            var validador = Regex.Match(cuerpo, @"validador\w*\.ValidateAsync\(");
            if (intento < 0) infracciones.Add($"{metodo}: no escribe el intento");
            if (!validador.Success) infracciones.Add($"{metodo}: no pasa por su validador");
            else if (intento >= 0 && intento > validador.Index) infracciones.Add($"{metodo}: valida antes de escribir el intento");
        }
        if (Regex.Matches(texto, @"auditoria\.HechoAsync\(").Count < 2) infracciones.Add("las reservas no dejan el hecho de reservar y el de liberar");
        ReglaSobreFuente.SinInfracciones(infracciones, "Una escritura de las reservas de número no valida o no audita (T22, T68)");
    }

    /// <summary>El cuerpo de un método público del archivo: desde su firma hasta la siguiente firma pública (o el final).</summary>
    private static string? CuerpoDe(string texto, string metodo)
    {
        var firma = Regex.Match(texto, $@"public\s+async\s+Task<[^\n]*>\s+{Regex.Escape(metodo)}\(");
        if (!firma.Success) return null;
        var siguiente = Regex.Match(texto[(firma.Index + firma.Length)..], @"\n\s{4}(public|private|internal)\s");
        return siguiente.Success ? texto.Substring(firma.Index, firma.Length + siguiente.Index) : texto[firma.Index..];
    }
}
