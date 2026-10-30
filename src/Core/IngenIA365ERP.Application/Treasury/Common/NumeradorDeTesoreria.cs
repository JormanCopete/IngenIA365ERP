using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>
/// El único que numera un documento de la Tesorería nueva (feature 015, T0039; R-13; FR-007): la misma mecánica que <c>NumeradorDeRecaudo</c>
/// sobre <c>COR_Sequences</c>, con la clave <c>TES:{código}</c> en <c>Name</c> y el prefijo en <c>ShortName</c>, llamado al final de la
/// confirmación (lote, archivo de pago, cheque, remesa). Un rechazo no consume número.
/// </summary>
public sealed class NumeradorDeTesoreria(IApplicationDbContext db, ICerrojoDeTesoreria cerrojo)
{
    public const int DigitosPorDefecto = Receipts.Common.NumeradorDeRecaudo.DigitosPorDefecto;

    public const int LargoMaximo = Receipts.Common.NumeradorDeRecaudo.LargoMaximo;

    /// <summary>La clave de la fila de numeración de una clase de documento.</summary>
    public static string Clave(string codigo) => $"{CatalogoDeTesoreria.Modulo}:{codigo}";

    /// <summary>
    /// Numera un documento ya armado, al final de la operación (R-13; corrida del 2026-10-26): el número lo escribe el numerador, no quien llama
    /// (<c>SoloElNumeradorNumera</c>).
    /// </summary>
    public async Task NumerarAsync(IngenIA365ERP.Domain.Common.IDocumentoNumerado documento, string codigo, string prefijo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documento);
        documento.Number = await AsignarAsync(codigo, prefijo, ct);
    }

    /// <summary>Asigna el siguiente número de la clase <paramref name="codigo"/>; el <paramref name="prefijo"/> se fija al crear la fila.</summary>
    public async Task<string> AsignarAsync(string codigo, string prefijo, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(prefijo);
        var clave = Clave(codigo);
        var secuencia = db.Sequences.Local.FirstOrDefault(s => s.Name == clave && s.DocumentType == CatalogoDeTesoreria.Modulo);
        if (secuencia is null)
        {
            var id = await ExistenteAsync(clave, ct);
            if (id is null)
            {
                await cerrojo.AsegurarNumeracionAsync(clave, prefijo, ct);
                id = await ExistenteAsync(clave, ct);
            }
            if (id is null)
            {
                secuencia = new Sequence { Name = clave, ShortName = prefijo, DocumentType = CatalogoDeTesoreria.Modulo, IsAutomatic = true, NextSequence = 1 };
                db.Sequences.Add(secuencia);
            }
            else
            {
                await cerrojo.BloquearNumeracionAsync(id.Value, ct);
                secuencia = await db.Sequences.FirstAsync(s => s.Id == id.Value, ct);
            }
        }

        var numero = Receipts.Common.NumeradorDeRecaudo.Formatear(secuencia.ShortName ?? prefijo, secuencia.NextSequence);
        secuencia.NextSequence = secuencia.NextSequence + 1;
        if (numero.Length > LargoMaximo)
            throw new InvalidOperationException($"El número {numero} de {codigo} pasa de {LargoMaximo} caracteres.");
        return numero;
    }

    private Task<int?> ExistenteAsync(string clave, CancellationToken ct) =>
        db.Sequences.AsNoTracking()
            .Where(s => s.Name == clave && s.DocumentType == CatalogoDeTesoreria.Modulo)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);
}
