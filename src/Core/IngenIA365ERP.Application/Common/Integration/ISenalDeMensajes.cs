using System.Threading.Channels;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// La señal en proceso que despierta al despachador cuando se guardan mensajes nuevos (feature 012, T10, T078;
/// decisiones-transversales §1.3 paso 12). La avisa <c>ApplicationDbContext</c> desde su evento
/// <c>SavedChanges</c> cuando el guardado incluyó mensajes; la escucha <c>DespachadorDeMensajes</c> (I2), que
/// además sondea cada 5 s: perder un aviso sólo retrasa la entrega hasta el siguiente sondeo, nunca la pierde.
///
/// <para>
/// El aviso sale del <c>SaveChanges</c>, no del commit: dentro de una transacción explícita el despachador puede
/// despertar antes de que la fila sea visible, no la ve y la toma en el sondeo siguiente. La fase 12 (US7) amplía
/// esta interfaz si le hace falta; no la crea.
/// </para>
/// </summary>
public interface ISenalDeMensajes
{
    /// <summary>Avisa que se guardaron estos mensajes (sus <c>PublicId</c>). Nunca bloquea ni falla.</summary>
    void Avisar(IReadOnlyCollection<Guid> mensajes);

    /// <summary>Los avisos, para el despachador.</summary>
    ChannelReader<Guid> Avisos { get; }
}
