using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Configuration;

/// <summary>
/// <c>LND_FactTypes</c> (feature 013, T0210, T0223; data-model §1.11; DC-11, T43, T56, C06, C110): el catálogo sembrado de los tipos
/// de hecho que Cartera manda a Contabilidad. Da el <c>PublicId</c> del ámbito de parámetro <c>LendingFactType</c> y el código corto
/// de la <c>ScheduleKey</c>. Lo siembra <c>LendingFactTypesSeeder</c> (Order 90) desde el catálogo de mensajes; nunca se edita a mano.
/// </summary>
public class LendingFactType : AuditableEntity
{
    /// <summary>El <c>Type</c> del mensaje (<c>InteresCausado</c>, <c>PagoAplicado</c>…).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Código corto de 3 a 4 letras mayúsculas (C06), de <c>TipoDeMensaje.CodigoCorto</c> (C117).</summary>
    public string ShortCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsInformational { get; set; }

    /// <summary><c>DS</c>, <c>RC</c>, <c>DN</c>, <c>CA</c>, <c>PV</c>, <c>CN</c> (T54); nulo en los informativos.</summary>
    public string? VoucherTypeCode { get; set; }

    /// <summary><c>EnLinea</c>, <c>PorLotes</c> o <c>NoPasa</c> (texto del parámetro; D8, T56).</summary>
    public string DefaultMode { get; set; } = string.Empty;

    /// <summary><c>FinDeCausacion</c> o <c>CierreDeCartera</c>.</summary>
    public string? DefaultBatchTrigger { get; set; }

    public EntregaDeCartera DeliveredIn { get; set; } = EntregaDeCartera.K1;

    public bool IsSeeded { get; set; } = true;
}
