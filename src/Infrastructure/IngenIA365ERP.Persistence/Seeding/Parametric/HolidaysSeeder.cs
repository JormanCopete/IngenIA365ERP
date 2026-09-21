using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 010 (R6, data-model §2.2): el calendario de festivos de <c>PAY_Holidays</c> para los
/// años que la semilla cubre (<see cref="Años"/>, 2026–2028; cada año se suma uno), calculado con
/// <see cref="FestivosLey51"/>. Inserta lo que falta <b>por fecha</b> y no toca nada: un festivo
/// <c>Decreed</c> (un puente decretado) o <c>Manual</c> que la cooperativa registró en una fecha
/// se respeta, y si coincide con uno de la Ley 51 el de la ley no entra (la fecha ya es festivo).
/// El contador de días hábiles lee sólo esta tabla; la ley vive aquí, en la semilla, y no en
/// <c>Domain/Payroll</c> (SC-008).
/// </summary>
public sealed class HolidaysSeeder : IDataSeeder
{
    public int Order => 74;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    /// <summary>Los años sembrados. En diciembre se agrega el siguiente y la semilla lo alcanza en el próximo arranque.</summary>
    public static readonly IReadOnlyList<int> Años = [2026, 2027, 2028];

    public static IReadOnlyList<Holiday> Catalogo() =>
        Años.SelectMany(año => FestivosLey51.DelAño(año).Select(f => new Holiday
        {
            Date = f.Fecha,
            Name = f.Nombre,
            Origin = OrigenDe(f.Origen),
            Year = (short)f.Fecha.Year,
            CreatedBy = SeedContext.ParametricCreatedBy,
        })).ToList();

    public static HolidayOrigin OrigenDe(FestivosLey51.Origen origen) => origen switch
    {
        FestivosLey51.Origen.Fijo => HolidayOrigin.Ley51Fixed,
        FestivosLey51.Origen.TrasladadoAlLunes => HolidayOrigin.Ley51MovedToMonday,
        FestivosLey51.Origen.Pascua => HolidayOrigin.Ley51Easter,
        _ => throw new ArgumentOutOfRangeException(nameof(origen), origen, "Origen de festivo sin equivalente en la tabla."),
    };

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory).</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var años = Años.Select(a => (short)a).ToList();
        var ocupadas = (await db.Holidays.IgnoreQueryFilters()
                .Where(h => años.Contains(h.Year))
                .Select(h => h.Date)
                .ToListAsync(ct))
            .ToHashSet();

        var insertados = 0;
        foreach (var festivo in Catalogo())
        {
            if (ocupadas.Contains(festivo.Date)) continue;
            db.Holidays.Add(festivo);
            ocupadas.Add(festivo.Date);
            insertados++;
        }
        if (insertados > 0) await db.SaveChangesAsync(ct);
        return insertados;
    }
}
