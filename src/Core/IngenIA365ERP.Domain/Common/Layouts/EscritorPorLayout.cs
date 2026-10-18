using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.Domain.Common.Layouts;

/// <summary>
/// Lo que devuelve <see cref="EscritorPorLayout.Escribir"/>: si se escribió, los bytes, su SHA-256 (hexadecimal en minúsculas), el texto,
/// cuántas líneas tiene y todas las inconsistencias (las advertencias acompañan a un archivo escrito; con una bloqueante no hay archivo).
/// </summary>
public sealed record ResultadoDeEscritura(
    bool Escrito,
    byte[]? Contenido,
    string? Sha256,
    string? Texto,
    int Lineas,
    IReadOnlyList<InconsistenciaDeLayout> Inconsistencias)
{
    public int Bloqueantes => Inconsistencias.Count(i => i.Severidad == SeveridadDeInconsistencia.Blocking);

    public int Advertencias => Inconsistencias.Count(i => i.Severidad == SeveridadDeInconsistencia.Warning);
}

/// <summary>
/// Escribe el archivo por su layout (feature 013, K5, US16; adaptadores-y-archivos.md §7.1): primero valida con
/// <see cref="ValidadorDeLayout"/> y, si hay <b>una sola</b> bloqueante, no escribe nada —ni esa fila ni las demás—; si no, una línea por
/// fila, cada campo formateado y ajustado (ancho fijo) o separado por el delimitador, con el fin de línea del layout también tras la
/// última, en su codificación (UTF-8 sin marca de orden), y el SHA-256 de esos bytes. Determinista: las mismas filas dan los mismos
/// bytes. (nuevo)
/// </summary>
public static class EscritorPorLayout
{
    public static ResultadoDeEscritura Escribir(LayoutDeArchivo layout, IReadOnlyList<FilaDeLayout> filas)
    {
        var inconsistencias = ValidadorDeLayout.Validar(layout, filas);
        if (inconsistencias.Any(i => i.Severidad == SeveridadDeInconsistencia.Blocking))
            return new ResultadoDeEscritura(false, null, null, null, 0, inconsistencias);

        var fin = layout.FinDeLinea;
        var sb = new StringBuilder();
        foreach (var fila in filas)
        {
            var registro = layout.Registro(fila.Registro)!;
            var campos = registro.EnOrden(layout.FileKind)
                .Select(c => FormateadorDeCampo.Ajustar(layout, c, FormateadorDeCampo.Crudo(layout, c, fila).Texto!));
            sb.Append(layout.FileKind == TipoDeArchivoDeLayout.FixedWidth ? string.Concat(campos) : string.Join(layout.Delimiter, campos));
            sb.Append(fin);
        }
        var texto = sb.ToString();
        var bytes = layout.Codificacion().GetBytes(texto);
        return new ResultadoDeEscritura(true, bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)), texto, filas.Count, inconsistencias);
    }
}
