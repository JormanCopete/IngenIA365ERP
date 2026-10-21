namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>
/// Marca un comando que sabe en qué oficina ocurre (feature 013, T67, F10; research R136; data-model §0.12): la oficina del crédito o
/// de la solicitud, por el <c>PublicId</c> de su sucursal (<c>COR_Branches</c>). La auditoría la copia al metadato <c>Office</c>; un
/// comando de Cartera sin la marca (o con la oficina nula) lleva la oficina por defecto de quien lo hace
/// (<c>IAlcanceDeCartera.OficinaPorDefecto</c>). (nuevo)
/// </summary>
public interface IConOficina
{
    Guid? BranchPublicId { get; }
}
