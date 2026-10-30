using FluentValidation;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Receipts.Contabilizacion;

/// <summary>
/// Contabiliza una <b>unidad</b> de hechos de Recaudo (feature 015, T0257; contabilidad.md §3; molde <c>PostLendingFactsCommand</c>): un comprobante del tipo del
/// hecho principal por la matriz <c>ACC_ReceiptsPostingRules</c> y siempre por <c>AccountingPoster</c>. <b>Sin ruta ni permisos</b>: lo envía sólo el despachador,
/// por <c>DestinoContabilidad</c> (<c>LosComandosDeConsumoNoTienenRuta</c>). (nuevo)
/// </summary>
public sealed record PostReceiptsFactsCommand(IReadOnlyList<Guid> MessagePublicIds, Guid? BatchPublicId)
    : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class PostReceiptsFactsCommandValidator : AbstractValidator<PostReceiptsFactsCommand>
{
    public PostReceiptsFactsCommandValidator()
    {
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("La unidad no trae hechos.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty).When(x => x.BatchPublicId is not null);
    }
}

public sealed class PostReceiptsFactsCommandHandler(ContabilizacionDeModulo contabilizacion) : IRequestHandler<PostReceiptsFactsCommand, Result<ResultadoDeConsumo>>
{
    public Task<Result<ResultadoDeConsumo>> Handle(PostReceiptsFactsCommand request, CancellationToken ct) =>
        contabilizacion.UnidadAsync(MatricesDeRecaudoYTesoreria.Recaudo, request.MessagePublicIds, request.BatchPublicId, ct);
}

/// <summary>
/// Contabiliza un <b>grupo resumido</b> de recibos de caja de un lote (feature 015, T0257; contabilidad.md §7): un comprobante por día y punto con las líneas sumadas
/// por cuenta, lado, sucursal y centro, y el detalle sólo donde la cuenta exige tercero, cruce o base. Una unidad que falla no detiene a las demás. <b>Sin ruta ni
/// permisos</b>. (nuevo)
/// </summary>
public sealed record PostReceiptsSummaryGroupCommand(Guid BatchPublicId, string GroupKey, IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<IReadOnlyList<ResultadoDeUnidad>>>, IReintentableAnteConcurrencia;

public sealed class PostReceiptsSummaryGroupCommandValidator : AbstractValidator<PostReceiptsSummaryGroupCommand>
{
    public PostReceiptsSummaryGroupCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.GroupKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("El grupo no trae hechos.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class PostReceiptsSummaryGroupCommandHandler(ContabilizacionDeModulo contabilizacion)
    : IRequestHandler<PostReceiptsSummaryGroupCommand, Result<IReadOnlyList<ResultadoDeUnidad>>>
{
    public Task<Result<IReadOnlyList<ResultadoDeUnidad>>> Handle(PostReceiptsSummaryGroupCommand request, CancellationToken ct) =>
        contabilizacion.GrupoAsync(MatricesDeRecaudoYTesoreria.Recaudo, request.BatchPublicId, request.MessagePublicIds, ct);
}
