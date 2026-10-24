using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// El único que numera un documento de Recaudo (feature 015, T0039; R-13; FR-007, FR-026; data-model §0.3). Numera sobre <c>COR_Sequences</c>
/// con la mecánica del <c>Numerador</c> de la 012, una fila por tipo de documento y prefijo con la clave <c>RCD:{código}:{prefijo}</c> en
/// <c>Name</c> (decisión del dueño, DT §2.7), y lo llama la confirmación <b>al final</b>, dentro de su transacción:
/// <list type="number">
/// <item>busca la fila de la clave; si falta, la crea el cerrojo (<see cref="ICerrojoDeRecaudo.AsegurarNumeracionAsync"/>) sobre el índice único
/// filtrado, para que dos confirmaciones simultáneas no numeren lo mismo;</item>
/// <item>la bloquea (<see cref="ICerrojoDeRecaudo.BloquearNumeracionAsync"/>, el último nivel) y la lee ya bloqueada;</item>
/// <item>arma <c>prefijo + número</c> con <see cref="DigitosPorDefecto"/> e incrementa <c>NextSequence</c> por EF: el <c>SaveChanges</c> de la
/// confirmación guarda todo junto. Sin huecos: una operación rechazada se deshace con su transacción y no consume número.</item>
/// </list>
/// Fuera de éste y de <c>NumeradorDeTesoreria</c> nadie escribe <c>NextSequence</c> (<c>SoloElNumeradorNumera</c>).
/// </summary>
public sealed class NumeradorDeRecaudo(IApplicationDbContext db, ICerrojoDeRecaudo cerrojo)
{
    /// <summary>Dígitos mínimos del número (con ceros a la izquierda).</summary>
    public const int DigitosPorDefecto = 6;

    /// <summary>El largo máximo del número visible (data-model §0.3).</summary>
    public const int LargoMaximo = 30;

    /// <summary>La clave de la fila de numeración de un tipo de documento y prefijo.</summary>
    public static string Clave(string codigoDelTipo, string prefijo) => $"{CatalogoDeRecaudo.Modulo}:{codigoDelTipo}:{prefijo}";

    /// <summary>Asigna el siguiente número del tipo <paramref name="codigoDelTipo"/> con su <paramref name="prefijo"/>.</summary>
    public async Task<string> AsignarAsync(string codigoDelTipo, string prefijo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoDelTipo);
        ArgumentNullException.ThrowIfNull(prefijo);
        var secuencia = await SecuenciaAsync(Clave(codigoDelTipo, prefijo), prefijo, ct);
        var numero = Formatear(secuencia.ShortName ?? prefijo, secuencia.NextSequence);
        secuencia.NextSequence = secuencia.NextSequence + 1;
        if (numero.Length > LargoMaximo)
            throw new InvalidOperationException($"El número {numero} del tipo {codigoDelTipo} pasa de {LargoMaximo} caracteres.");
        return numero;
    }

    /// <summary>
    /// Devuelve el último número asignado en esta unidad de trabajo si nadie lo usó (feature 015, US6, T0194; nuevo): un proceso no interactivo numera antes de los
    /// contratos (el contrato guarda el número) y, si el contrato rechaza la línea entera, la tanda sigue en la misma transacción: el número vuelve para que no
    /// quede un hueco. Sólo baja <c>NextSequence</c> si <paramref name="numero"/> es el último que dio esta fila; si no, no hace nada.
    /// </summary>
    public void Devolver(string codigoDelTipo, string prefijo, string numero)
    {
        var clave = Clave(codigoDelTipo, prefijo);
        var secuencia = db.Sequences.Local.FirstOrDefault(s => s.Name == clave && s.DocumentType == CatalogoDeRecaudo.Modulo);
        if (secuencia is null || secuencia.NextSequence <= 1) return;
        if (Formatear(secuencia.ShortName ?? prefijo, secuencia.NextSequence - 1) == numero) secuencia.NextSequence -= 1;
    }

    /// <summary><c>prefijo + valor</c> con al menos <see cref="DigitosPorDefecto"/> dígitos.</summary>
    public static string Formatear(string prefijo, long valor) =>
        prefijo + valor.ToString(CultureInfo.InvariantCulture).PadLeft(DigitosPorDefecto, '0');

    private async Task<Sequence> SecuenciaAsync(string clave, string prefijo, CancellationToken ct)
    {
        // Una fila ya seguida en esta unidad (otra confirmación de la misma tanda): se sigue con ella, que lleva el valor al día.
        var secuencia = db.Sequences.Local.FirstOrDefault(s => s.Name == clave && s.DocumentType == CatalogoDeRecaudo.Modulo);
        if (secuencia is not null) return secuencia;

        var id = await ExistenteAsync(clave, ct);
        if (id is null)
        {
            await cerrojo.AsegurarNumeracionAsync(clave, prefijo, ct);
            id = await ExistenteAsync(clave, ct);
        }
        if (id is null)
        {
            secuencia = new Sequence { Name = clave, ShortName = prefijo, DocumentType = CatalogoDeRecaudo.Modulo, IsAutomatic = true, NextSequence = 1 };
            db.Sequences.Add(secuencia);
            return secuencia;
        }

        // Bloquea primero y lee después: así se lee el valor que dejó el último que numeró.
        await cerrojo.BloquearNumeracionAsync(id.Value, ct);
        return await db.Sequences.FirstAsync(s => s.Id == id.Value, ct);
    }

    private Task<int?> ExistenteAsync(string clave, CancellationToken ct) =>
        db.Sequences.AsNoTracking()
            .Where(s => s.Name == clave && s.DocumentType == CatalogoDeRecaudo.Modulo)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
}
