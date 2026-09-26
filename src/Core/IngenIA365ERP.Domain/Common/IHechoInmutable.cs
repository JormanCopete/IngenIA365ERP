namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Feature 012 (decisiones-transversales T17, T18; data-model §0): un <b>hecho</b>. Se inserta una
/// vez y no se modifica ni se borra nunca (Principio XI): lo que estaba mal se corrige con otro hecho,
/// no reescribiendo éste. Lo son la línea de kardex, el consumo de capa, el mensaje de integración y sus
/// dependencias, la decisión de aprobación y el ancla de la cadena de auditoría.
///
/// <para>
/// Es sólo un marcador. Quien lo hace cumplir es <c>ApplicationDbContext.SaveChangesAsync</c>, que
/// rechaza un <c>Modified</c> o <c>Deleted</c> sobre estas entidades (T137), y la prueba
/// <c>LosHechosInmutablesNoSeModifican</c>.
/// </para>
/// </summary>
public interface IHechoInmutable;
