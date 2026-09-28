using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing;

/// <summary>
/// Una resolución de numeración de la DIAN (<c>COR_DianNumberingResolutions</c>; feature 012, I4, T692; data-model §18;
/// contracts/dian.md §9). Nunca se numera fuera de una resolución vigente y asociada al canal sellado.
///
/// <para>
/// <see cref="LastIssuedNumber"/> nace en <c>RangeFrom − 1</c> (nada emitido) y lo incrementa <b>sólo</b>
/// <c>NumeradorFiscal</c>, bajo el cerrojo, como última fila del orden canónico (T16, <c>SoloElNumeradorNumera</c>). No
/// se llama <c>NextNumber</c>. Mientras no se haya emitido nada, mover <see cref="RangeFrom"/> lo arrastra; con algo
/// emitido ya no (el <c>PUT</c> responde <c>ElectronicInvoicing.Resolution.InUse</c>). <see cref="BaseEntity.RowVersion"/>
/// es su control de concurrencia.
/// </para>
/// </summary>
public class DianNumberingResolution : AuditableEntity
{
    public ResolutionKind Kind { get; set; }

    /// <summary>Sólo en <see cref="ResolutionKind.Contingency"/>: a qué tipo respalda.</summary>
    public ResolutionKind? BacksUpKind { get; set; }

    /// <summary>Número de la resolución (máx. 30).</summary>
    public string ResolutionNumber { get; set; } = string.Empty;

    public DateOnly ResolutionDate { get; set; }

    /// <summary>Alfanumérico (máx. 4; Res. 165 art. 11 num. 4).</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Primer número del rango. Mientras no se haya emitido nada, arrastra a <see cref="LastIssuedNumber"/>.</summary>
    public long RangeFrom
    {
        get;
        set
        {
            var nadaEmitido = LastIssuedNumber == field - 1;
            field = value;
            if (nadaEmitido) LastIssuedNumber = value - 1;
        }
    }

    public long RangeTo { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly ValidTo { get; set; }

    /// <summary>Las de pruebas no valen en producción.</summary>
    public DianEnvironment Environment { get; set; } = DianEnvironment.Testing;

    /// <summary>El último número emitido; nace en <c>RangeFrom − 1</c>. Lo escribe sólo <c>NumeradorFiscal</c>.</summary>
    public long LastIssuedNumber { get; set; } = -1;

    public bool IsActive { get; set; } = true;

    /// <summary>Máx. 300.</summary>
    public string? Notes { get; set; }

    /// <summary>Canales asociados, con su vigencia.</summary>
    public ICollection<DianResolutionChannel> Channels { get; set; } = [];

    /// <summary>¿Ya se emitió algún número?</summary>
    public bool TieneNumerosEmitidos => LastIssuedNumber >= RangeFrom;

    /// <summary>¿Se acabó el rango?</summary>
    public bool Agotada => LastIssuedNumber >= RangeTo;

    /// <summary>Cuántos números quedan.</summary>
    public long Disponibles => Math.Max(0, RangeTo - LastIssuedNumber);

    /// <summary>Vigente a la fecha (inclusive en los dos extremos).</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && ValidTo >= fecha;
}
