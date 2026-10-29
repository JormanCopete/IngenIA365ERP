namespace IngenIA365ERP.Domain.Treasury.Saldos;

/// <summary>Un cheque girado en tránsito a la fecha: su valor, si los libros ya lo reflejan y cuándo se espera que el banco lo pague. (nuevo)</summary>
public sealed record ChequeEnTransito(Guid IssuedCheckPublicId, long CheckNumber, decimal Amount, DateOnly IssuedOn, DateOnly? ExpectedOn, bool InBooks);

/// <summary>Una remesa en tránsito a la fecha: <paramref name="Amount"/> positivo (de bóveda a banco) suma, negativo (de banco a bóveda) resta. (nuevo)</summary>
public sealed record RemesaEnTransito(Guid RemittancePublicId, string Number, decimal Amount, DateOnly DispatchDate, DateOnly? ExpectedOn);

/// <summary>El disponible proyectado con sus partes. (nuevo)</summary>
public sealed record CalculoDelDisponible(decimal BookBalance, decimal ApprovedNotSent, decimal ChecksInTransit, decimal ChecksNotInBooks, decimal RemittancesInTransit,
    decimal ProjectedAvailable);

/// <summary>
/// El disponible proyectado de una cuenta propia (feature 015, segunda ronda del 2026-10-27, D-i; FR-103). Motor puro. La base es el <b>saldo en libros</b>, el único al día
/// a cualquier fecha. Sobre ella: los lotes aprobados sin enviar restan (los libros mueven el banco al quedar «girado»); cada remesa en tránsito suma o resta con su signo
/// (los libros mueven el banco sólo con <c>RemesaConfirmada</c>); y un cheque en tránsito <b>sólo resta si los libros no lo reflejan</b>: los libros ya acreditaron el
/// banco al emitirlo (<c>ChequeGiradoEmitido</c>), así que restarlo otra vez lo contaría dos veces. Todos los cheques en tránsito se informan
/// (<see cref="CalculoDelDisponible.ChecksInTransit"/>); sólo los no reflejados pesan (<see cref="CalculoDelDisponible.ChecksNotInBooks"/>). (nuevo)
/// </summary>
public static class DisponibleProyectado
{
    public static CalculoDelDisponible Calcular(decimal libros, decimal aprobadosSinEnviar, IEnumerable<ChequeEnTransito> cheques, IEnumerable<RemesaEnTransito> remesas)
    {
        ArgumentNullException.ThrowIfNull(cheques);
        ArgumentNullException.ThrowIfNull(remesas);
        var lista = cheques.ToList();
        var enTransito = lista.Sum(c => c.Amount);
        var fueraDeLibros = lista.Where(c => !c.InBooks).Sum(c => c.Amount);
        var deRemesas = remesas.Sum(r => r.Amount);
        return new CalculoDelDisponible(libros, aprobadosSinEnviar, enTransito, fueraDeLibros, deRemesas, libros - aprobadosSinEnviar - fueraDeLibros + deRemesas);
    }
}
